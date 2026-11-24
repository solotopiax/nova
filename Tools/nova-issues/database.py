"""Durable server storage and project-scoped ingestion."""

from contextlib import contextmanager
import hashlib
import hmac
import json
from pathlib import Path
import secrets
import sqlite3
import time


SCHEMA = """
CREATE TABLE IF NOT EXISTS projects (
    id TEXT PRIMARY KEY, name TEXT NOT NULL, team TEXT NOT NULL, token_hash TEXT NOT NULL,
    created_at INTEGER NOT NULL, last_seen INTEGER
);
CREATE TABLE IF NOT EXISTS automatic_workspaces (
    workspace TEXT PRIMARY KEY, project_id TEXT NOT NULL REFERENCES projects(id), token_hash TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS automatic_workspaces_project ON automatic_workspaces(project_id);
CREATE TABLE IF NOT EXISTS issues (
    id INTEGER PRIMARY KEY, fingerprint TEXT NOT NULL UNIQUE, code TEXT NOT NULL, area TEXT NOT NULL,
    title TEXT NOT NULL, priority TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'open',
    assignee TEXT NOT NULL DEFAULT '', fixed_version TEXT NOT NULL DEFAULT '',
    duplicate_of INTEGER REFERENCES issues(id), revision INTEGER NOT NULL DEFAULT 1,
    first_seen INTEGER NOT NULL, last_seen INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS reports (
    project_id TEXT NOT NULL REFERENCES projects(id), id TEXT NOT NULL, workspace TEXT NOT NULL,
    kind TEXT NOT NULL, digest TEXT NOT NULL, payload TEXT NOT NULL, observed_at INTEGER NOT NULL,
    received_at INTEGER NOT NULL, issue_id INTEGER REFERENCES issues(id),
    PRIMARY KEY (project_id, id)
);
CREATE INDEX IF NOT EXISTS reports_project_kind_time ON reports(project_id, kind, received_at DESC);
CREATE INDEX IF NOT EXISTS reports_kind_time ON reports(kind, received_at DESC);
CREATE INDEX IF NOT EXISTS reports_kind_observed ON reports(kind, observed_at DESC);
CREATE INDEX IF NOT EXISTS reports_issue ON reports(issue_id, received_at DESC);
CREATE TABLE IF NOT EXISTS history (
    id INTEGER PRIMARY KEY, issue_id INTEGER NOT NULL REFERENCES issues(id),
    actor TEXT NOT NULL, status TEXT NOT NULL, note TEXT NOT NULL, created_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS history_issue ON history(issue_id, id DESC);
CREATE TABLE IF NOT EXISTS sessions (
    token_hash TEXT PRIMARY KEY, username TEXT NOT NULL, csrf TEXT NOT NULL, expires_at INTEGER NOT NULL
);
"""


class Conflict(Exception):
    pass


class EnrollmentLimit(Exception):
    """自动登记的来源达到当前窗口内的容量。"""


def digest(value):
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def now():
    return int(time.time())


def encoded(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), sort_keys=True)


class Database:
    def __init__(self, path):
        self.path = Path(path)
        self.path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
        with self.connect() as db:
            version = db.execute("PRAGMA user_version").fetchone()[0]
            if version not in (0, 1, 2):
                raise ValueError("数据库版本不受支持。")
            db.execute("PRAGMA journal_mode=WAL")
            db.executescript(SCHEMA)
            if version < 2:
                db.execute("BEGIN IMMEDIATE")
                if db.execute("PRAGMA user_version").fetchone()[0] < 2:
                    db.execute("""
                        UPDATE projects SET team='' WHERE team='自动登记'
                        AND id IN (SELECT project_id FROM automatic_workspaces)
                        """)
                    db.execute("PRAGMA user_version=2")

    @contextmanager
    def connect(self):
        db = sqlite3.connect(self.path, timeout=5)
        db.row_factory = sqlite3.Row
        db.execute("PRAGMA foreign_keys=ON")
        try:
            with db:
                yield db
        finally:
            db.close()

    def register(self, project_id, name, team):
        token = secrets.token_urlsafe(36)
        try:
            with self.connect() as db:
                db.execute("INSERT INTO projects VALUES (?, ?, ?, ?, ?, NULL)",
                           (project_id, name, team, digest(token), now()))
        except sqlite3.IntegrityError as exc:
            raise Conflict("该项目标识已存在。") from exc
        return token

    def rotate(self, project_id):
        token = secrets.token_urlsafe(36)
        with self.connect() as db:
            changed = db.execute("UPDATE projects SET token_hash=? WHERE id=?", (digest(token), project_id))
            if not changed.rowcount:
                return None
        return token

    def update_team(self, project_id, team, previous):
        """更新维护者填写的团队。相同结果可以重试，过期编辑需要重新读取。"""
        with self.connect() as db:
            updated = db.execute("UPDATE projects SET team=? WHERE id=? AND team IN (?,?)",
                                 (team, project_id, previous, team))
            if updated.rowcount:
                return {"id": project_id, "team": team}
            if db.execute("SELECT 1 FROM projects WHERE id=?", (project_id,)).fetchone() is None:
                return None
            raise Conflict("团队已被更新，请重新打开后再修改。")

    def authorize_project(self, project_id, token):
        with self.connect() as db:
            row = db.execute("SELECT token_hash FROM projects WHERE id=?", (project_id,)).fetchone()
        return row is not None and hmac.compare_digest(row["token_hash"], digest(token))

    def start_session(self, user, ttl):
        token, csrf = secrets.token_urlsafe(36), secrets.token_urlsafe(24)
        stamp = now()
        with self.connect() as db:
            db.execute("DELETE FROM sessions WHERE expires_at<=?", (stamp,))
            db.execute("INSERT INTO sessions VALUES (?, ?, ?, ?)", (digest(token), user, csrf, stamp + ttl))
        return token, csrf

    def session(self, token):
        with self.connect() as db:
            row = db.execute("SELECT username, csrf FROM sessions WHERE token_hash=? AND expires_at>?",
                             (digest(token), now())).fetchone()
        return dict(row) if row is not None else None

    def end_session(self, token):
        with self.connect() as db:
            db.execute("DELETE FROM sessions WHERE token_hash=?", (digest(token),))

    @staticmethod
    def authorize_workspace(db, project_id, workspace, token, name, allow_registration):
        """在接收事务内登记工作区，后续请求只接收该工作区自己的凭据。"""
        credential = db.execute("SELECT project_id, token_hash FROM automatic_workspaces WHERE workspace=?",
                                (workspace,)).fetchone()
        token_hash = digest(token)
        if credential is not None:
            if credential["project_id"] != project_id or not hmac.compare_digest(credential["token_hash"], token_hash):
                raise PermissionError()
            return
        project = db.execute("SELECT id FROM projects WHERE id=?", (project_id,)).fetchone()
        if project is not None and db.execute("SELECT 1 FROM automatic_workspaces WHERE project_id=? LIMIT 1",
                                             (project_id,)).fetchone() is None:
            raise Conflict("此项目标识已用于维护端登记。")
        if not allow_registration():
            raise EnrollmentLimit()
        if project is None:
            db.execute("INSERT INTO projects VALUES (?, ?, ?, ?, ?, NULL)",
                       (project_id, name, "", "", now()))
        db.execute("INSERT INTO automatic_workspaces VALUES (?, ?, ?)", (workspace, project_id, token_hash))

    def ingest(self, project_id, event, token, *, automatic_name=None, allow_registration=None):
        """接受与凭据绑定的上报，同一项目的同一记录重复提交时复用回执。"""
        stamp = now()
        content = encoded(event)
        payload_hash = digest(content)
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            if automatic_name is None:
                auth = db.execute("SELECT token_hash FROM projects WHERE id=?", (project_id,)).fetchone()
                if auth is None or not hmac.compare_digest(auth["token_hash"], digest(token)):
                    raise PermissionError()
            else:
                self.authorize_workspace(db, project_id, event["workspace"], token, automatic_name, allow_registration)
            existing = db.execute("SELECT digest, issue_id, received_at FROM reports WHERE project_id=? AND id=?",
                                  (project_id, event["id"])).fetchone()
            if existing is not None:
                if existing["digest"] != payload_hash:
                    raise Conflict("同一个记录标识已经关联到不同内容。")
                return (False, existing["issue_id"], existing["received_at"])
            issue_id = self.attach_issue(db, event) if event["kind"] == "issue" else None
            db.execute("INSERT INTO reports VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                       (project_id, event["id"], event["workspace"], event["kind"],
                        payload_hash, encoded(event["data"]), event["observed_at"], stamp, issue_id))
            db.execute("UPDATE projects SET last_seen=? WHERE id=?", (stamp, project_id))
            return (True, issue_id, stamp)

    @staticmethod
    def attach_issue(db, event):
        item = event["data"]
        timestamp = event["observed_at"]
        fingerprint = digest(encoded([item["area"], item["code"]]))
        row = db.execute("SELECT id, duplicate_of FROM issues WHERE fingerprint=?", (fingerprint,)).fetchone()
        if row is None:
            result = db.execute(
                """INSERT INTO issues (fingerprint, code, area, title, priority, first_seen, last_seen)
                   VALUES (?, ?, ?, ?, ?, ?, ?)""",
                (fingerprint, item["code"], item["area"], item["title"], item["priority"], timestamp, timestamp))
            return result.lastrowid
        issue_id = row["duplicate_of"] or row["id"]
        db.execute("UPDATE issues SET first_seen=MIN(first_seen,?), last_seen=MAX(last_seen,?) WHERE id=?",
                   (timestamp, timestamp, issue_id))
        return issue_id

    def overview(self, days):
        """汇总维护台数据，并从同一份数据库快照读取未读计数基线。"""
        since = now() - days * 86400
        with self.connect() as db:
            db.execute("BEGIN")
            projects = db.execute("""
                SELECT p.id, p.name, p.team, p.created_at, p.last_seen, h.last_hook_run
                FROM projects AS p LEFT JOIN (
                    SELECT project_id, MAX(observed_at) AS last_hook_run
                    FROM reports WHERE kind='hook_activity' GROUP BY project_id
                ) AS h ON h.project_id=p.id ORDER BY p.name
                """)
            latest = db.execute("""
                SELECT workspace, project_id, received_at, json_extract(payload,'$.framework') AS framework,
                       json_extract(payload,'$.unity') AS unity,
                       json_extract(payload,'$.coverage') AS coverage
                FROM (
                    SELECT *, ROW_NUMBER() OVER (
                        PARTITION BY project_id, workspace ORDER BY received_at DESC, rowid DESC) AS n
                    FROM reports WHERE kind='snapshot'
                ) WHERE n=1 ORDER BY received_at DESC
                """)
            implementation = db.execute("""
                SELECT project_id,workspace,observed_at,received_at,
                       json_extract(payload,'$.tools') AS tools,
                       json_extract(payload,'$.framework') AS framework,
                       json_extract(payload,'$.requested_framework') AS requested_framework,
                       json_extract(payload,'$.editor_framework') AS editor_framework
                FROM (
                    SELECT *, ROW_NUMBER() OVER (
                        PARTITION BY project_id,workspace ORDER BY observed_at DESC,received_at DESC,rowid DESC
                    ) AS n FROM reports WHERE kind='implementation' AND json_extract(payload,'$.stage')='workspace'
                ) WHERE n=1 ORDER BY observed_at DESC
                """)
            usage = db.execute("""
                WITH recorded AS (
                    SELECT project_id, json_extract(payload,'$.module') AS module,
                           json_extract(payload,'$.operation') AS operation,
                           json_extract(payload,'$.outcome') AS outcome,
                           json_extract(payload,'$.framework') AS framework,
                           json_extract(payload,'$.source') AS recorded_source,
                           json_extract(payload,'$.entry') AS recorded_entry
                    FROM reports WHERE kind='usage' AND observed_at>=?
                ), editor_entries AS (
                    -- 旧版 Editor 回执和 SDK 预加载有固定格式，原报告保持原样。
                    SELECT *, CASE
                        WHEN recorded_entry IS NOT NULL OR COALESCE(recorded_source,'editor')!='editor' THEN NULL
                        WHEN operation GLOB ('nova.project.' || module || '.?*') AND outcome!='executed' THEN 'action'
                        WHEN module='sdk' AND operation='editor.preload.sdk' AND outcome='success' THEN 'preload'
                    END AS editor_entry FROM recorded
                )
                SELECT project_id, module, operation, outcome, framework,
                       COALESCE(recorded_source, CASE WHEN editor_entry IS NOT NULL THEN 'editor' ELSE 'legacy' END) AS source,
                       COALESCE(recorded_entry, editor_entry, '') AS entry, COUNT(*) AS count
                FROM editor_entries
                GROUP BY project_id,module,operation,outcome,framework,source,entry ORDER BY count DESC LIMIT 500
                """, (since,))
            codex_activity = db.execute("""
                SELECT project_id, COUNT(*) AS turns, COUNT(DISTINCT workspace) AS workspaces,
                       MAX(observed_at) AS last_task, MAX(received_at) AS last_report
                FROM reports WHERE kind='task' AND observed_at>=? GROUP BY project_id
                """, (since,))
            codex_modules = db.execute("""
                SELECT project_id, json_extract(payload,'$.module') AS module,
                       COUNT(CASE WHEN json_extract(payload,'$.entry')!='skill' THEN 1 END) AS calls,
                       COUNT(CASE WHEN json_extract(payload,'$.entry')='skill' THEN 1 END) AS skill_turns,
                       COUNT(CASE WHEN json_extract(payload,'$.outcome') IN ('failed','blocked') THEN 1 END) AS problems,
                       MAX(observed_at) AS last_used
                FROM reports WHERE kind='usage' AND observed_at>=? AND json_extract(payload,'$.source')='codex'
                GROUP BY project_id,module ORDER BY calls DESC,skill_turns DESC,module,project_id
                """, (since,))
            issue_counts = db.execute("""
                SELECT COUNT(*) AS active, COUNT(CASE WHEN priority='P0' THEN 1 END) AS p0
                FROM issues WHERE status NOT IN ('closed', 'duplicate')
                """).fetchone()
            activity_totals = db.execute("""
                WITH counts AS (
                    SELECT project_id, MIN(rowid) AS first_report,
                           COUNT(CASE WHEN kind='issue' THEN 1 END) AS issues,
                           COUNT(CASE WHEN kind IN ('snapshot', 'hook_activity') THEN 1 END) AS project_reports,
                           COUNT(CASE WHEN kind='usage' THEN 1 END) AS usage
                    FROM reports GROUP BY project_id
                )
                -- 首次环境或钩子上报同时自动登记项目时，项目栏目只计一次。
                SELECT COALESCE(SUM(c.issues), 0) AS issues,
                       COUNT(*) + COALESCE(SUM(c.project_reports), 0) - COUNT(CASE
                           WHEN r.kind IN ('snapshot', 'hook_activity') AND EXISTS (
                               SELECT 1 FROM automatic_workspaces WHERE project_id=p.id
                           ) THEN 1 END) AS projects,
                       COALESCE(SUM(c.usage), 0) AS usage
                FROM projects p LEFT JOIN counts c ON c.project_id=p.id
                LEFT JOIN reports r ON r.rowid=c.first_report
                """).fetchone()
            return {"projects": list(map(dict, projects)), "workspaces": list(map(dict, latest)),
                    "implementation": [{**dict(item), "tools": json.loads(item["tools"])} for item in implementation],
                    "usage": list(map(dict, usage)), "issues": self.issue_rows(db),
                    "issue_counts": dict(issue_counts), "activity_totals": dict(activity_totals),
                    "codex_activity": list(map(dict, codex_activity)), "codex_modules": list(map(dict, codex_modules)),
                    "server_time": now(), "days": days}

    def list_issues(self, *, project_id="", status="", priority="", search=""):
        """按维护台的筛选条件读取问题列表。"""
        with self.connect() as db:
            return self.issue_rows(db, project_id, status, priority, search)

    @staticmethod
    def issue_rows(db, project_id="", status="", priority="", search=""):
        """按项目的发生记录筛选问题，再统计匹配问题的全部发生次数。"""
        conditions = []
        values = []
        if project_id:
            conditions.append("i.id IN (SELECT issue_id FROM reports WHERE project_id=? AND kind='issue')")
            values.append(project_id)
        if status:
            conditions.append("i.status=?")
            values.append(status)
        if priority:
            conditions.append("i.priority=?")
            values.append(priority)
        if search:
            db.create_function("issue_text_lower", 1, str.lower, deterministic=True)
            conditions.append("instr(issue_text_lower('#' || i.id || ' ' || i.area || ' ' || i.title || ' ' || i.code), ?) > 0")
            values.append(search.lower())
        where = "WHERE " + " AND ".join(conditions) if conditions else ""
        rows = db.execute(f"""
            SELECT i.id,i.code,i.area,i.title,i.priority,i.status,i.assignee,i.fixed_version,
                   i.first_seen,i.last_seen,i.duplicate_of,
                   COUNT(r.id) AS occurrences, COUNT(DISTINCT r.project_id) AS projects
            FROM issues i LEFT JOIN reports r ON r.issue_id=i.id {where}
            GROUP BY i.id
            ORDER BY CASE i.status WHEN 'closed' THEN 1 WHEN 'duplicate' THEN 2 ELSE 0 END,
                     i.priority,i.last_seen DESC LIMIT 500
            """, values)
        return list(map(dict, rows))

    @staticmethod
    def decode(row):
        item = dict(row)
        item["data"] = json.loads(item.pop("payload"))
        return item

    def project_detail(self, project_id):
        """返回项目环境、各工作区最近的钩子运行和历史任务基础信息。"""
        with self.connect() as db:
            project = db.execute("SELECT id,name,team,created_at,last_seen FROM projects WHERE id=?",
                                 (project_id,)).fetchone()
            if project is None:
                return None
            snapshots = db.execute("""
                SELECT id, workspace, observed_at, received_at, payload FROM reports
                WHERE project_id=? AND kind='snapshot' ORDER BY observed_at DESC, received_at DESC, rowid DESC LIMIT 100
                """, (project_id,))
            tasks = db.execute("""
                SELECT workspace, observed_at, received_at, payload FROM reports
                WHERE project_id=? AND kind='task' ORDER BY received_at DESC, rowid DESC LIMIT 30
                """, (project_id,))
            hook_activity = db.execute("""
                SELECT workspace, observed_at AS last_run, json_extract(payload,'$.framework') AS framework
                FROM (
                    SELECT workspace, observed_at, payload, ROW_NUMBER() OVER (
                        PARTITION BY workspace ORDER BY observed_at DESC, received_at DESC, rowid DESC
                    ) AS n FROM reports WHERE project_id=? AND kind='hook_activity'
                ) WHERE n=1 ORDER BY last_run DESC, workspace
                """, (project_id,))
            implementation = db.execute("""
                WITH facts AS (
                    SELECT rowid,workspace,observed_at,received_at,payload,
                           json_extract(payload,'$.stage') AS stage
                    FROM reports WHERE project_id=? AND kind='implementation'
                      AND json_extract(payload,'$.stage')!='sdk-device'
                ), ranked AS (
                    SELECT *, ROW_NUMBER() OVER (
                        PARTITION BY workspace,stage,
                           CASE WHEN stage NOT IN ('workspace','framework-upgrade') THEN json_extract(payload,'$.framework') END,
                           CASE WHEN stage!='workspace' THEN json_extract(payload,'$.unity') END,
                           CASE WHEN stage!='workspace' THEN json_extract(payload,'$.platform') END,
                           CASE WHEN stage!='workspace' THEN json_extract(payload,'$.channel') END,
                           CASE WHEN stage!='workspace' THEN json_extract(payload,'$.mode') END,
                           CASE WHEN stage IN ('plugpals-query','editor-module','startup-stage','framework-upgrade')
                                THEN json_extract(payload,'$.step') END,
                           json_extract(payload,'$.attempt'),
                           json_extract(payload,'$.build_kind'),json_extract(payload,'$.development')
                        ORDER BY json_extract(payload,'$.sequence') DESC,observed_at DESC,
                            CASE json_extract(payload,'$.outcome') WHEN 'running' THEN 0 WHEN 'waiting' THEN 1 ELSE 2 END DESC,
                            received_at DESC,rowid DESC
                    ) AS n FROM facts
                )
                SELECT workspace,observed_at,received_at,payload FROM ranked WHERE n=1
                ORDER BY stage!='workspace',observed_at DESC,received_at DESC,rowid DESC LIMIT 400
                """, (project_id,))
            modules = db.execute("""
                WITH events AS (
                    SELECT workspace,observed_at,received_at,json_extract(payload,'$.framework') AS framework,
                           json_extract(payload,'$.module') AS module,json_extract(payload,'$.outcome') AS outcome,
                           0 AS play_session
                    FROM reports WHERE project_id=? AND kind='usage' AND observed_at>=?
                      AND COALESCE(json_extract(payload,'$.entry'),'')!='skill'
                    UNION ALL
                    SELECT workspace,observed_at,received_at,json_extract(payload,'$.framework'),
                           json_extract(payload,'$.step'),'success',1
                    FROM reports WHERE project_id=? AND kind='implementation' AND observed_at>=?
                      AND json_extract(payload,'$.stage')='editor-module'
                )
                SELECT workspace,framework,module,SUM(1-play_session) AS count,SUM(play_session) AS play_sessions,
                       MAX(observed_at) AS last_use,
                       MAX(CASE WHEN outcome='success' THEN observed_at END) AS last_success,
                       SUM(CASE WHEN outcome IN ('failed','blocked') THEN 1 ELSE 0 END) AS problems
                FROM events GROUP BY workspace,framework,module ORDER BY last_use DESC LIMIT 500
                """, (project_id, now() - 30 * 86400, project_id, now() - 30 * 86400))
            upgrade_issues = db.execute("""
                SELECT workspace,observed_at,received_at,issue_id,payload FROM (
                    SELECT *, ROW_NUMBER() OVER (
                        PARTITION BY workspace,json_extract(payload,'$.framework')
                        ORDER BY observed_at DESC,received_at DESC,rowid DESC
                    ) AS n FROM reports WHERE project_id=? AND kind='issue'
                        AND json_extract(payload,'$.area') IN ('editor-compilation','package-import')
                ) WHERE n=1 ORDER BY observed_at DESC LIMIT 100
                """, (project_id,))
            return {"project": dict(project), "snapshots": list(map(self.decode, snapshots)),
                    "tasks": list(map(self.decode, tasks)), "hook_activity": list(map(dict, hook_activity)),
                    "implementation": list(map(self.decode, implementation)), "module_evidence": list(map(dict, modules)),
                    "upgrade_issues": list(map(self.decode, upgrade_issues))}

    def project_snapshot(self, project_id, report_id):
        """读取归属当前项目的单份环境快照。"""
        with self.connect() as db:
            row = db.execute("SELECT payload FROM reports WHERE project_id=? AND id=? AND kind='snapshot'",
                             (project_id, report_id)).fetchone()
            return json.loads(row["payload"]) if row is not None else None

    def issue_detail(self, issue_id):
        with self.connect() as db:
            issue = db.execute("SELECT * FROM issues WHERE id=?", (issue_id,)).fetchone()
            if issue is None:
                return None
            reports = db.execute("""
                SELECT r.project_id, p.name, r.workspace, r.observed_at, r.received_at, r.payload
                FROM reports r JOIN projects p ON p.id=r.project_id WHERE r.issue_id=?
                ORDER BY r.received_at DESC, r.rowid DESC LIMIT 200
                """, (issue_id,))
            history = db.execute("SELECT actor,status,note,created_at FROM history WHERE issue_id=? "
                                 "ORDER BY id DESC LIMIT 100", (issue_id,))
            return {"issue": dict(issue), "occurrences": list(map(self.decode, reports)),
                    "history": list(map(dict, history))}

    def project_issue(self, project_id, issue_id):
        with self.connect() as db:
            row = db.execute("""
                SELECT i.id,target.status,target.fixed_version,i.duplicate_of
                FROM issues i JOIN issues target ON target.id=COALESCE(i.duplicate_of,i.id)
                WHERE i.id=? AND EXISTS (SELECT 1 FROM reports r WHERE r.issue_id=target.id AND r.project_id=?)
                """, (issue_id, project_id)).fetchone()
            return dict(row) if row is not None else None

    def update_issue(self, issue_id, update, actor):
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            row = db.execute("SELECT status,revision FROM issues WHERE id=?", (issue_id,)).fetchone()
            if row is None:
                return False
            if row["revision"] != update["revision"]:
                raise Conflict("Issue 已被更新，请刷新后重试。")
            if row["status"] == "duplicate":
                raise Conflict("请在归并后的 Issue 上继续管理。")
            target = update.get("duplicate_of")
            if update["status"] == "duplicate":
                other = db.execute("SELECT status,first_seen,last_seen FROM issues WHERE id=?", (target,)).fetchone()
                if target == issue_id or other is None or other["status"] == "duplicate":
                    raise Conflict("请选择另一个有效 Issue。")
                db.execute("UPDATE reports SET issue_id=? WHERE issue_id=?", (target, issue_id))
                db.execute("UPDATE issues SET duplicate_of=?,revision=revision+1 WHERE duplicate_of=?",
                           (target, issue_id))
                db.execute("""
                    UPDATE issues SET first_seen=MIN(first_seen,(SELECT first_seen FROM issues WHERE id=?)),
                    last_seen=MAX(last_seen,(SELECT last_seen FROM issues WHERE id=?)), revision=revision+1
                    WHERE id=?
                    """, (issue_id, issue_id, target))
                db.execute("INSERT INTO history (issue_id,actor,status,note,created_at) VALUES (?,?,?,?,?)",
                           (target, actor, other["status"], f"归并了 Issue #{issue_id}。", now()))
            db.execute("""
                UPDATE issues SET status=?,priority=?,assignee=?,fixed_version=?,duplicate_of=?,
                                  revision=revision+1 WHERE id=?
                """, (update["status"], update["priority"], update["assignee"], update["fixed_version"],
                      target if update["status"] == "duplicate" else None, issue_id))
            db.execute("INSERT INTO history (issue_id,actor,status,note,created_at) VALUES (?,?,?,?,?)",
                       (issue_id, actor, update["status"], update["note"], now()))
        return True
