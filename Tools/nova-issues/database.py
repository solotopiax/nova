"""Durable server storage and project-scoped ingestion."""

from contextlib import contextmanager
import hashlib
import hmac
import json
from pathlib import Path
import re
import secrets
import sqlite3
import time

from protocol import RETIRED_SDK_ISSUE, VERSION, retired_sdk_feedback


# 维护台按两种状态读取历史记录，旧版上报和项目查询继续使用原有字段。
MAINTAINER_ISSUE_STATUS = "CASE WHEN status IN ('closed', 'duplicate') THEN 'closed' ELSE 'open' END"
FRAMEWORK_PACKAGE = "com.solotopia.nova.framework"
ISSUE_PACKAGE_LOCATION = re.compile(rf"Packages/({re.escape(FRAMEWORK_PACKAGE)}(?:\.[a-z0-9_.-]+)?)/[^\r\n]+")
ISSUE_PACKAGE_VERSION = re.compile(r"包版本 ([a-zA-Z0-9.+-]+)。")
ISSUE_LATEST_PAYLOAD = """(
    SELECT r.payload FROM current_reports r WHERE r.issue_id=i.id AND r.kind='issue'
    ORDER BY r.observed_at DESC,r.received_at DESC,r.rowid DESC LIMIT 1
)"""

SCHEMA = f"""
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
CREATE TABLE IF NOT EXISTS issue_sequence (
    singleton INTEGER PRIMARY KEY CHECK (singleton=1), last_id INTEGER NOT NULL
);
INSERT OR IGNORE INTO issue_sequence SELECT 1, COALESCE(MAX(id),0) FROM issues;
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
-- 历史 SDK 运行记录留在原表，维护台只读取当前支持的证据。
CREATE VIEW IF NOT EXISTS current_reports AS
SELECT rowid, * FROM reports WHERE CASE kind
    WHEN 'usage' THEN COALESCE(json_extract(payload,'$.operation'),'')!='editor.preload.sdk'
    WHEN 'issue' THEN COALESCE(json_extract(payload,'$.code'),'')!='{RETIRED_SDK_ISSUE}'
    WHEN 'implementation' THEN COALESCE(json_extract(payload,'$.stage'),'') NOT IN ('sdk-device','sdk-preload')
        AND NOT (COALESCE(json_extract(payload,'$.stage'),'')='startup-stage'
                 AND COALESCE(json_extract(payload,'$.step'),'')='sdk')
    ELSE 1 END;
-- Codex 与 Editor 观察到同一操作时只展示一条，结果和发生时间采用实际执行者的记录。
CREATE VIEW IF NOT EXISTS current_usage AS
SELECT rowid,project_id,workspace,observed_at,received_at,
       CASE WHEN codex_seen=1 THEN json_set(payload,'$.source','codex','$.entry','action') ELSE payload END AS payload
FROM (
    SELECT *, MAX(CASE WHEN json_extract(payload,'$.source')='codex'
                           AND json_type(payload,'$.action_operation') IS NOT NULL THEN 1 ELSE 0 END)
                  OVER operation AS codex_seen,
           ROW_NUMBER() OVER (operation ORDER BY (json_extract(payload,'$.source') IS 'editor') DESC,
                                 observed_at,received_at,rowid) AS n
    FROM current_reports WHERE kind='usage'
    WINDOW operation AS (PARTITION BY project_id,workspace,COALESCE(json_extract(payload,'$.action_operation'),id))
) WHERE n=1;
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


def issue_report_details(data):
    """从新旧报告保留的归因中读取包版本和可展示的诊断内容。"""
    diagnostic = next((entry for entry in data["evidence"]
                       if entry["source"] in ("unity.compiler", "unity.package-import")), None)
    package = None
    version = None
    error_log = None
    if diagnostic:
        location = data["summary"]
        path = ISSUE_PACKAGE_LOCATION.fullmatch(location)
        if path:
            package = path[1]
            compact_version = ISSUE_PACKAGE_VERSION.fullmatch(diagnostic["detail"])
            version = compact_version[1] if compact_version else data.get("version")
            code = data["code"].split(".", 2)
            error = code[1] if len(code) == 3 and code[0] == "unity" else data["code"]
            actual = data["actual"]
            text = f"{location}\n{error}: {actual}"
            if not compact_version and diagnostic["detail"] != f"{location}。{actual}":
                text += f"\n{diagnostic['detail']}"
            error_log = {"source": diagnostic["source"], "text": text}
    elif any(entry["source"] == "framework.source" for entry in data["evidence"]):
        package = FRAMEWORK_PACKAGE

    if package == FRAMEWORK_PACKAGE and version in (None, "", "unknown"):
        version = data["framework"]
    known_version = version if version and version != "unknown" and VERSION.fullmatch(version) else None
    return ({"id": package, "version": known_version} if package else None), error_log


def issue_summary(row):
    """列表和详情头部显示最近发生记录确认的包。"""
    issue = dict(row)
    payload = issue.pop("latest_payload")
    issue["package"] = issue_report_details(json.loads(payload))[0] if payload else None
    return issue


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

    def delete_project(self, project_id):
        """删除项目及其上报，按剩余发生记录更新共享问题。"""
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            affected = db.execute("SELECT DISTINCT issue_id FROM reports WHERE project_id=? AND issue_id IS NOT NULL",
                                  (project_id,)).fetchall()
            db.execute("DELETE FROM reports WHERE project_id=?", (project_id,))
            db.execute("DELETE FROM automatic_workspaces WHERE project_id=?", (project_id,))
            deleted = db.execute("DELETE FROM projects WHERE id=?", (project_id,)).rowcount
            for (issue_id,) in affected:
                first, last = db.execute("SELECT MIN(observed_at), MAX(observed_at) FROM reports WHERE issue_id=?",
                                         (issue_id,)).fetchone()
                if first is None:
                    self._delete_issue_records(db, issue_id)
                else:
                    db.execute("UPDATE issues SET first_seen=?,last_seen=? WHERE id=?", (first, last, issue_id))
            return bool(deleted)

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
            if retired_sdk_feedback(event["kind"], event["data"]):
                return (False, None, stamp)
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
        """按稳定的问题归属合并发生记录；旧表的优先级列对新版报告使用默认值。"""
        item = event["data"]
        timestamp = event["observed_at"]
        fingerprint = digest(encoded([item["area"], item["code"]]))
        row = db.execute("SELECT id, duplicate_of FROM issues WHERE fingerprint=?", (fingerprint,)).fetchone()
        if row is None:
            # 已删除的问题编号不能重新指向不同的问题。旧数据库从当前最大编号继续分配。
            db.execute("UPDATE issue_sequence SET last_id=MAX(last_id,(SELECT COALESCE(MAX(id),0) FROM issues))+1")
            result = db.execute(
                """INSERT INTO issues (id, fingerprint, code, area, title, priority, first_seen, last_seen)
                   VALUES ((SELECT last_id FROM issue_sequence), ?, ?, ?, ?, ?, ?, ?)""",
                (fingerprint, item["code"], item["area"], item["title"], item.get("priority", "P2"), timestamp, timestamp))
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
                ) WHERE n=1 ORDER BY observed_at DESC,received_at DESC,project_id,workspace
                """)
            usage_modules = db.execute("""
                SELECT DISTINCT json_extract(payload,'$.module') AS module
                FROM current_usage WHERE observed_at>=? ORDER BY module
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
                FROM current_usage WHERE observed_at>=? AND json_extract(payload,'$.source')='codex'
                GROUP BY project_id,module ORDER BY calls DESC,skill_turns DESC,module,project_id
                """, (since,))
            issue_counts = db.execute(f"""
                SELECT COUNT(CASE WHEN status='open' THEN 1 END) AS active,
                       COUNT(CASE WHEN status='closed' THEN 1 END) AS handled
                FROM (SELECT {MAINTAINER_ISSUE_STATUS} AS status FROM issues WHERE code!=?)
                """, (RETIRED_SDK_ISSUE,)).fetchone()
            activity_totals = db.execute("""
                WITH counts AS (
                    SELECT project_id, MIN(rowid) AS first_report,
                           COUNT(CASE WHEN kind='issue' THEN 1 END) AS issues,
                           COUNT(CASE WHEN kind IN ('snapshot', 'hook_activity') THEN 1 END) AS project_reports
                    FROM current_reports GROUP BY project_id
                )
                -- 首次环境或钩子上报同时自动登记项目时，项目栏目只计一次。
                SELECT COALESCE(SUM(c.issues), 0) AS issues,
                       COUNT(*) + COALESCE(SUM(c.project_reports), 0) - COUNT(CASE
                           WHEN r.kind IN ('snapshot', 'hook_activity') AND EXISTS (
                               SELECT 1 FROM automatic_workspaces WHERE project_id=p.id
                           ) THEN 1 END) AS projects,
                       (SELECT COUNT(*) FROM current_usage) AS usage
                FROM projects p LEFT JOIN counts c ON c.project_id=p.id
                LEFT JOIN reports r ON r.rowid=c.first_report
                """).fetchone()
            return {"projects": list(map(dict, projects)), "workspaces": list(map(dict, latest)),
                    "implementation": [{**dict(item), "tools": json.loads(item["tools"])} for item in implementation],
                    "configured_tools": self.configured_implementation_tools(db),
                    "usage": self.usage_rows(db, since), "usage_modules": [row["module"] for row in usage_modules],
                    "issues": self.issue_rows(db),
                    "issue_counts": dict(issue_counts), "activity_totals": dict(activity_totals),
                    "codex_activity": list(map(dict, codex_activity)), "codex_modules": list(map(dict, codex_modules)),
                    "server_time": now(), "days": days}

    def list_usage(self, days, *, project_id="", module="", source="", sort="count"):
        """读取选定时间范围及筛选条件下的使用汇总。"""
        with self.connect() as db:
            return self.usage_rows(db, now() - days * 86400, project_id=project_id, module=module,
                                   source=source, sort=sort)

    @staticmethod
    def usage_rows(db, since, *, project_id="", module="", source="", sort="count"):
        """按实际发生时间计算每组的最后一次触发，筛选和排序后取前 500 组。"""
        order = "last_used DESC, count DESC" if sort == "latest" else "count DESC, last_used DESC"
        rows = db.execute(f"""
            WITH recorded AS (
                SELECT project_id, json_extract(payload,'$.module') AS module,
                       json_extract(payload,'$.operation') AS operation,
                       json_extract(payload,'$.outcome') AS outcome,
                       json_extract(payload,'$.framework') AS framework,
                       json_extract(payload,'$.source') AS recorded_source,
                       json_extract(payload,'$.entry') AS recorded_entry, observed_at
                FROM current_usage WHERE observed_at>=:since
                  AND (:project='' OR project_id=:project)
                  AND (:module='' OR json_extract(payload,'$.module')=:module)
            ), editor_entries AS (
                -- Editor 操作沿用原有上报格式，维护台按稳定操作标识显示实际入口。
                SELECT *, CASE
                    WHEN recorded_entry IS NOT NULL OR COALESCE(recorded_source,'editor')!='editor' THEN NULL
                    WHEN operation GLOB ('nova.project.' || module || '.?*') AND outcome!='executed' THEN 'action'
                    WHEN recorded_source='editor' AND operation GLOB ('editor.' || module || '.export-*') THEN 'inspector'
                END AS editor_entry FROM recorded
            ), classified AS (
                SELECT project_id,module,operation,outcome,framework,observed_at,
                       COALESCE(recorded_source, CASE WHEN editor_entry IS NOT NULL THEN 'editor' ELSE 'legacy' END) AS source,
                       COALESCE(recorded_entry, editor_entry, '') AS entry
                FROM editor_entries
            )
            SELECT project_id,module,operation,outcome,framework,source,entry,
                   COUNT(*) AS count, MAX(observed_at) AS last_used
            FROM classified WHERE (:source='' OR source=:source)
            GROUP BY project_id,module,operation,outcome,framework,source,entry
            ORDER BY {order}, project_id,module,operation,outcome,framework,source,entry LIMIT 500
            """, {"since": since, "project": project_id, "module": module, "source": source})
        return list(map(dict, rows))

    def list_issues(self, *, project_id="", status="", search="", sort=""):
        """按维护台选择的筛选与排序读取问题列表。"""
        with self.connect() as db:
            return self.issue_rows(db, project_id, status, search, sort)

    @staticmethod
    def issue_rows(db, project_id="", status="", search="", sort=""):
        """按项目的发生记录筛选问题，再统计匹配问题的全部发生次数。"""
        conditions = ["i.code!=?"]
        values = [RETIRED_SDK_ISSUE]
        if project_id:
            conditions.append("i.id IN (SELECT issue_id FROM current_reports WHERE project_id=? AND kind='issue')")
            values.append(project_id)
        if status:
            conditions.append(f"({MAINTAINER_ISSUE_STATUS})=?")
            values.append(status)
        if search:
            db.create_function("issue_text_lower", 1, str.lower, deterministic=True)
            conditions.append("instr(issue_text_lower('#' || i.id || ' ' || i.area || ' ' || i.title || ' ' || i.code), ?) > 0")
            values.append(search.lower())
        where = "WHERE " + " AND ".join(conditions) if conditions else ""
        order = {"latest": "i.last_seen DESC", "count": "occurrences DESC, i.last_seen DESC"}.get(
            sort, f"({MAINTAINER_ISSUE_STATUS})='closed', i.last_seen DESC")
        rows = db.execute(f"""
            SELECT i.id,i.code,i.area,i.title,{MAINTAINER_ISSUE_STATUS} AS status,i.assignee,
                   i.first_seen,i.last_seen,i.duplicate_of,
                   COUNT(r.id) AS occurrences, COUNT(DISTINCT r.project_id) AS projects,
                   {ISSUE_LATEST_PAYLOAD} AS latest_payload
            FROM issues i LEFT JOIN current_reports r ON r.issue_id=i.id {where}
            GROUP BY i.id
            ORDER BY {order}, i.id DESC LIMIT 500
            """, values)
        return list(map(issue_summary, rows))

    def issue_projects(self, issue_id):
        """读取该问题全部发生记录对应的去重项目，包含较早的上报。"""
        with self.connect() as db:
            rows = db.execute("""
                SELECT DISTINCT p.id,p.name FROM issues i
                LEFT JOIN current_reports r ON r.issue_id=i.id
                LEFT JOIN projects p ON p.id=r.project_id
                WHERE i.id=? AND i.code!=? ORDER BY p.name COLLATE NOCASE,p.id
                """, (issue_id, RETIRED_SDK_ISSUE)).fetchall()
            return {"projects": [dict(row) for row in rows if row["id"] is not None]} if rows else None

    @staticmethod
    def decode(row):
        item = dict(row)
        item["data"] = json.loads(item.pop("payload"))
        return item

    @staticmethod
    def configured_implementation_tools(db, project_id=None):
        """按项目保留各工具已经上报的配置证据。"""
        scope = "AND project_id=?" if project_id is not None else ""
        params = (project_id,) if project_id is not None else ()
        rows = db.execute(f"""
            SELECT project_id, tool.key AS tool
            FROM reports, json_each(reports.payload, '$.tools') AS tool
            WHERE kind='implementation' AND json_extract(payload,'$.stage')='workspace'
              AND tool.value='configured' {scope}
            GROUP BY project_id, tool.key ORDER BY project_id, tool.key
            """, params)
        configured = {}
        for row in rows:
            configured.setdefault(row["project_id"], []).append(row["tool"])
        return configured

    def project_detail(self, project_id):
        """从同一份数据库快照读取项目环境、实施进展和上报来源。"""
        with self.connect() as db:
            db.execute("BEGIN")
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
                    FROM current_reports WHERE project_id=? AND kind='implementation'
                ), ranked AS (
                    SELECT *, ROW_NUMBER() OVER (
                        PARTITION BY workspace,stage,
                           CASE WHEN stage NOT IN ('workspace','framework-upgrade') THEN json_extract(payload,'$.framework') END,
                           CASE WHEN stage!='workspace' THEN json_extract(payload,'$.unity') END,
                           CASE WHEN stage IN ('config-export','config-load','pipify-batch','plugpals-query',
                               'bundle-build','player-build','pipify-step') THEN json_extract(payload,'$.platform') END,
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
            # 成功和其余结果各取最近一条，保留早于当前结果的验证证据。
            outcomes = db.execute("""
                WITH facts AS (
                    SELECT rowid,workspace,observed_at,received_at,payload,
                           json_extract(payload,'$.stage') AS stage,
                           json_extract(payload,'$.outcome')='success' AS succeeded
                    FROM current_reports WHERE project_id=? AND kind='implementation'
                      AND json_extract(payload,'$.stage') IN ('config-export','config-load','pipify-batch',
                          'plugpals-query','bundle-build','player-build','editor-module','table-ready')
                )
                SELECT workspace,observed_at,received_at,payload FROM (
                    SELECT *, ROW_NUMBER() OVER (
                        PARTITION BY stage,succeeded,
                            CASE WHEN stage IN ('player-build','bundle-build') THEN json_extract(payload,'$.platform') END,
                            CASE WHEN stage IN ('plugpals-query','editor-module') THEN json_extract(payload,'$.step') END,
                            json_extract(payload,'$.build_kind'),json_extract(payload,'$.development')
                        ORDER BY observed_at DESC,received_at DESC,rowid DESC
                    ) AS n FROM facts
                ) WHERE n=1 ORDER BY observed_at DESC,received_at DESC,rowid DESC
                """, (project_id,))
            modules = list(map(dict, db.execute("""
                WITH events AS (
                    SELECT workspace,observed_at,json_extract(payload,'$.framework') AS framework,
                           json_extract(payload,'$.module') AS module,json_extract(payload,'$.outcome') AS outcome,
                           0 AS play_session,
                           CASE WHEN json_extract(payload,'$.source')='editor'
                                AND json_extract(payload,'$.operation')
                                    GLOB ('editor.' || json_extract(payload,'$.module') || '.export-*')
                                THEN 1 ELSE 0 END AS manual_export
                    FROM current_usage WHERE project_id=:project
                      AND COALESCE(json_extract(payload,'$.entry'),'')!='skill'
                    UNION ALL
                    SELECT workspace,observed_at,json_extract(payload,'$.framework'),
                           json_extract(payload,'$.step'),'success',1,0
                    FROM current_reports WHERE project_id=:project AND kind='implementation'
                      AND json_extract(payload,'$.stage')='editor-module'
                ), recent AS (
                    SELECT workspace,framework,module,SUM(1-play_session) AS count,SUM(play_session) AS play_sessions,
                           MAX(observed_at) AS last_use,
                           MAX(CASE WHEN outcome='success' THEN observed_at END) AS last_success,
                           SUM(CASE WHEN outcome IN ('failed','blocked') THEN 1 ELSE 0 END) AS problems,
                           SUM(manual_export) AS manual_exports
                    FROM events WHERE observed_at>=:since
                    GROUP BY workspace,framework,module ORDER BY last_use DESC LIMIT 500
                )
                SELECT * FROM recent
                UNION ALL
                SELECT NULL,NULL,module,
                       SUM(CASE WHEN observed_at>=:since THEN 1-play_session ELSE 0 END),
                       SUM(CASE WHEN observed_at>=:since THEN play_session ELSE 0 END),
                       MAX(observed_at) AS last_use,
                       MAX(CASE WHEN outcome='success' THEN observed_at END) AS last_success,
                       SUM(CASE WHEN observed_at>=:since AND outcome IN ('failed','blocked') THEN 1 ELSE 0 END),
                       SUM(CASE WHEN observed_at>=:since THEN manual_export ELSE 0 END)
                FROM events GROUP BY module
                """, {"project": project_id, "since": now() - 30 * 86400})))
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
                    "implementation": list(map(self.decode, implementation)),
                    "configured_tools": self.configured_implementation_tools(db, project_id).get(project_id, []),
                    "implementation_outcomes": list(map(self.decode, outcomes)),
                    "module_evidence": [row for row in modules if row["workspace"] is not None],
                    "project_module_evidence": [row for row in modules if row["workspace"] is None],
                    "upgrade_issues": list(map(self.decode, upgrade_issues))}

    def project_snapshot(self, project_id, report_id):
        """读取归属当前项目的单份环境快照。"""
        with self.connect() as db:
            row = db.execute("SELECT payload FROM reports WHERE project_id=? AND id=? AND kind='snapshot'",
                             (project_id, report_id)).fetchone()
            return json.loads(row["payload"]) if row is not None else None

    def issue_detail(self, issue_id):
        """从同一数据库快照读取维护状态、发生记录和处理备注。"""
        with self.connect() as db:
            db.execute("BEGIN")
            issue = db.execute(f"""
                SELECT id,area,title,{MAINTAINER_ISSUE_STATUS} AS status,assignee,duplicate_of,
                       revision,first_seen,last_seen,{ISSUE_LATEST_PAYLOAD} AS latest_payload
                FROM issues i WHERE id=? AND code!=?
                """, (issue_id, RETIRED_SDK_ISSUE)).fetchone()
            if issue is None:
                return None
            reports = db.execute("""
                SELECT r.project_id, p.name, r.workspace, r.observed_at, r.received_at, r.payload
                FROM current_reports r JOIN projects p ON p.id=r.project_id WHERE r.issue_id=?
                ORDER BY r.received_at DESC, r.rowid DESC LIMIT 200
                """, (issue_id,))
            history = db.execute(f"""
                SELECT actor,{MAINTAINER_ISSUE_STATUS} AS status,note,created_at FROM history
                WHERE issue_id=? ORDER BY id DESC LIMIT 100
                """, (issue_id,))
            occurrences = list(map(self.decode, reports))
            for occurrence in occurrences:
                occurrence["package"], occurrence["error_log"] = issue_report_details(occurrence["data"])
            return {"issue": issue_summary(issue), "occurrences": occurrences,
                    "history": list(map(dict, history))}

    def project_issue(self, project_id, issue_id):
        with self.connect() as db:
            row = db.execute("""
                SELECT i.id,target.status,target.fixed_version,i.duplicate_of
                FROM issues i JOIN issues target ON target.id=COALESCE(i.duplicate_of,i.id)
                WHERE i.id=? AND i.code!=? AND target.code!=?
                  AND EXISTS (SELECT 1 FROM current_reports r WHERE r.issue_id=target.id AND r.project_id=?)
                """, (issue_id, RETIRED_SDK_ISSUE, RETIRED_SDK_ISSUE, project_id)).fetchone()
            return dict(row) if row is not None else None

    def delete_issue(self, issue_id):
        """在同一事务中删除问题、发生记录及处理记录。"""
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            return self._delete_issue_records(db, issue_id)

    @staticmethod
    def _delete_issue_records(db, issue_id):
        """清除指向已删除合并目标的链接，保留其余问题的处理状态。"""
        db.execute("DELETE FROM reports WHERE issue_id=?", (issue_id,))
        db.execute("DELETE FROM history WHERE issue_id=?", (issue_id,))
        db.execute("UPDATE issues SET duplicate_of=NULL,revision=revision+1 WHERE duplicate_of=?", (issue_id,))
        deleted = db.execute("DELETE FROM issues WHERE id=?", (issue_id,)).rowcount
        if deleted:
            db.execute("UPDATE issue_sequence SET last_id=MAX(last_id,?)", (issue_id,))
        return bool(deleted)

    def update_issue(self, issue_id, update, actor):
        """校验编辑版本，在同一事务内保存处理状态、负责人和追加记录。"""
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            row = db.execute("SELECT duplicate_of,revision FROM issues WHERE id=?", (issue_id,)).fetchone()
            if row is None:
                return False
            if row["revision"] != update["revision"]:
                raise Conflict("Issue 已被更新，请刷新后重试。")
            if row["duplicate_of"] is not None:
                raise Conflict("请在归并后的 Issue 上继续管理。")
            db.execute("""
                UPDATE issues SET status=?,assignee=?,revision=revision+1 WHERE id=?
                """, (update["status"], update["assignee"], issue_id))
            db.execute("INSERT INTO history (issue_id,actor,status,note,created_at) VALUES (?,?,?,?,?)",
                       (issue_id, actor, update["status"], update["note"], now()))
        return True
