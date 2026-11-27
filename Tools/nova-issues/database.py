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

from protocol import RETIRED_SDK_ISSUE, VERSION, retired_feedback


# 维护台按两种状态读取历史记录，旧版上报和项目查询继续使用原有字段。
MAINTAINER_ISSUE_STATUS = "CASE WHEN status IN ('closed', 'duplicate') THEN 'closed' ELSE 'open' END"
FRAMEWORK_PACKAGE = "com.solotopia.nova.framework"
ISSUE_PACKAGE_LOCATION = re.compile(rf"Packages/({re.escape(FRAMEWORK_PACKAGE)}(?:\.[a-z0-9_.-]+)?)/[^\r\n]+")
ISSUE_PACKAGE_VERSION = re.compile(r"包版本 ([a-zA-Z0-9.+-]+)。")
ISSUE_LATEST_PAYLOAD = """(
    SELECT r.payload FROM current_reports r WHERE r.issue_id=i.id AND r.kind='issue'
    ORDER BY r.observed_at DESC,r.received_at DESC,r.rowid DESC LIMIT 1
)"""
ACTIVITY_SECTIONS = ("projects", "issues", "usage")
DEVELOPMENT_ENTRIES = ("action", "unity-cli", "nova-cli", "inspector", "unity-mcp")
MODULE_ALIASES = {"persist": "persistence", "vibrate": "vibration"}
RECENT_REPORT_DAYS = 30
ARCHIVE_REPORT_DAYS = 365

# 同一次 Action 沿用首次接收的编号，结果采用实际执行者的记录。
CURRENT_USAGE_SELECT = """
SELECT rowid,project_id,workspace,observed_at,received_at,activity_id,
       CASE WHEN codex_seen=1 THEN json_set(payload,'$.source','codex','$.entry','action') ELSE payload END AS payload
FROM (
    SELECT r.*, MIN(a.id) OVER operation AS activity_id,
           MAX(CASE WHEN json_extract(payload,'$.source')='codex'
                         AND json_type(payload,'$.action_operation') IS NOT NULL THEN 1 ELSE 0 END)
               OVER operation AS codex_seen,
           ROW_NUMBER() OVER (operation ORDER BY (json_extract(payload,'$.source') IS 'editor') DESC,
                                 observed_at,received_at,r.rowid) AS n
    FROM {report_source} r JOIN activity_receipts a ON a.project_id=r.project_id AND a.report_id=r.id
    WHERE r.kind='usage' AND NOT EXISTS (
        SELECT 1 FROM archived_actions past WHERE past.project_id=r.project_id AND past.workspace=r.workspace
        AND past.operation_id=json_extract(r.payload,'$.action_operation')
    )
    WINDOW operation AS (PARTITION BY r.project_id,workspace,COALESCE(json_extract(payload,'$.action_operation'),r.id))
) WHERE n=1
"""
CURRENT_USAGE = "CREATE VIEW IF NOT EXISTS current_usage AS\n" + CURRENT_USAGE_SELECT.format(report_source="current_reports")

# 列表与未读记录共用分组字段和历史入口识别规则。
USAGE_GROUP = "project_id,module,operation,outcome,framework,source,entry"
USAGE_SCOPE = """
    observed_at>=:since AND (:project='' OR project_id=:project)
    AND (:module='' OR module=:module OR module=CASE :module
        WHEN 'persistence' THEN 'persist' WHEN 'persist' THEN 'persistence'
        WHEN 'vibration' THEN 'vibrate' WHEN 'vibrate' THEN 'vibration' END)
    AND (:source='' OR source=:source) AND (:entry IS NULL OR entry=:entry)
"""
USAGE_ENTRIES_SELECT = """
WITH recorded AS (
    SELECT project_id,json_extract(payload,'$.module') AS module,
           json_extract(payload,'$.operation') AS operation,json_extract(payload,'$.outcome') AS outcome,
           json_extract(payload,'$.framework') AS framework,json_extract(payload,'$.source') AS recorded_source,
           json_extract(payload,'$.entry') AS recorded_entry,observed_at,activity_id
    FROM {usage_source}
), editor_entries AS (
    SELECT *, CASE
        WHEN recorded_entry IS NOT NULL OR COALESCE(recorded_source,'editor')!='editor' THEN NULL
        WHEN operation GLOB ('nova.project.' || module || '.?*') AND outcome!='executed' THEN 'action'
        WHEN recorded_source='editor' AND operation GLOB ('editor.' || module || '.export-*') THEN 'inspector'
    END AS editor_entry FROM recorded
)
SELECT project_id,module,operation,outcome,framework,observed_at,activity_id,
       COALESCE(recorded_source, CASE WHEN editor_entry IS NOT NULL THEN 'editor' ELSE 'legacy' END) AS source,
       COALESCE(recorded_entry, editor_entry, '') AS entry
FROM editor_entries
"""
USAGE_ENTRIES = "CREATE VIEW IF NOT EXISTS usage_entries AS\n" + USAGE_ENTRIES_SELECT.format(usage_source="current_usage")

# 这四种 Action 的实际执行由专项工具上报，模块汇总只计执行回执。
MODULE_DEVELOPMENT_CALL = """CASE WHEN operation IN (
    'nova.project.config.export-runtime','nova.project.pipify.run-batch',
    'nova.project.bundle.build-asset','nova.project.bundle.build-raw-file'
) THEN 0 ELSE 1 END"""

# 模块排行和项目详情共用执行证据。没有 calls 的旧记录只提供场次。
MODULE_IMPLEMENTATION_SELECT = """
SELECT project_id,report_id,workspace,observed_at,framework,outcome,stage,'editor' AS source,
       CASE stage WHEN 'config-export' THEN 'config' WHEN 'config-load' THEN 'config'
                  WHEN 'pipify-batch' THEN 'pipify' WHEN 'plugpals-query' THEN 'upm'
                  WHEN 'bundle-build' THEN 'bundle' ELSE step END AS module,
       CASE WHEN stage IN ('config-export','pipify-batch','plugpals-query','bundle-build')
            THEN 1 ELSE 0 END AS development_call,
       CASE WHEN stage='editor-module' AND outcome='success' AND calls IS NULL
            THEN 1 ELSE 0 END AS play_session,
       CASE WHEN stage='editor-module' AND outcome='success' THEN calls
            WHEN stage='config-load' AND outcome='success' THEN 1 END AS play_calls
FROM (
    SELECT project_id,id AS report_id,workspace,observed_at,json_extract(payload,'$.framework') AS framework,
           json_extract(payload,'$.stage') AS stage,json_extract(payload,'$.step') AS step,
           json_extract(payload,'$.outcome') AS outcome,json_extract(payload,'$.calls') AS calls
    FROM {report_source} WHERE kind='implementation'
)
WHERE stage IN ('config-export','pipify-batch','plugpals-query','bundle-build','config-load','editor-module')
"""
MODULE_IMPLEMENTATION_EVENTS = MODULE_IMPLEMENTATION_SELECT.format(report_source="current_reports")

# 把已计入模块统计的执行上报投影为明细。旧上报可查询，现有已读进度沿用接收编号。
IMPLEMENTATION_USAGE = f"""
CREATE VIEW IF NOT EXISTS implementation_usage AS
SELECT e.project_id,e.report_id,e.module,e.stage AS operation,e.outcome,e.framework,e.observed_at,
       a.id AS activity_id,e.source,CASE WHEN e.development_call THEN 'editor-tool' ELSE 'play' END AS entry,
       e.development_call,e.play_calls
FROM ({MODULE_IMPLEMENTATION_EVENTS}) e
LEFT JOIN activity_receipts a ON a.project_id=e.project_id AND a.report_id=e.report_id
WHERE e.development_call OR e.play_session OR e.play_calls IS NOT NULL
"""

# 同一个明细来源同时提供上报条数、开发次数和 Play 调用次数。
MAINTAINER_USAGE = f"""
CREATE VIEW IF NOT EXISTS maintainer_usage AS
SELECT project_id,module,operation,outcome,framework,observed_at,activity_id,source,entry,
       CASE WHEN entry IN {DEVELOPMENT_ENTRIES} THEN {MODULE_DEVELOPMENT_CALL} ELSE 0 END AS development_call,
       NULL AS play_calls
FROM usage_entries
UNION ALL
SELECT project_id,module,operation,outcome,framework,observed_at,activity_id,source,entry,
       development_call,play_calls FROM implementation_usage
"""

# 全量接收记录独立于列表筛选和显示上限。自动登记与首次环境上报只计一次。
MAINTAINER_ACTIVITY = f"""
CREATE VIEW IF NOT EXISTS maintainer_activity AS
WITH first_reports AS (
    SELECT project_id, MIN(rowid) AS first_report FROM current_reports GROUP BY project_id
), registered_with_environment AS (
    SELECT f.project_id FROM first_reports f JOIN reports r ON r.rowid=f.first_report
    WHERE r.kind IN ('snapshot', 'hook_activity') AND EXISTS (
        SELECT 1 FROM automatic_workspaces w WHERE w.project_id=f.project_id
    )
)
SELECT id, 'projects' AS section, project_id AS item FROM activity_receipts
WHERE kind IN ('snapshot', 'hook_activity')
   OR (kind='project' AND project_id NOT IN (SELECT project_id FROM registered_with_environment))
UNION ALL
SELECT a.id, 'issues', CAST(r.issue_id AS TEXT)
FROM current_reports r JOIN activity_receipts a ON a.project_id=r.project_id AND a.report_id=r.id
WHERE r.kind='issue'
UNION ALL
SELECT activity_id, 'usage', json_array({USAGE_GROUP}) FROM usage_entries
"""

DASHBOARD_ACTIVITY = f"""
CREATE VIEW IF NOT EXISTS dashboard_activity AS
SELECT id,section,item FROM maintainer_activity
UNION ALL
SELECT activity_id,'usage',json_array({USAGE_GROUP})
FROM implementation_usage WHERE activity_id IS NOT NULL
"""

# 使用记录的侧栏与未读筛选都只提醒维护台可查的上报。
RECENT_USAGE_ACTIVITY = f"""
SELECT activity_id AS id,'usage' AS section,json_array({USAGE_GROUP}) AS item
FROM maintainer_usage WHERE observed_at>=:usage_unread_since
"""

UNREAD_IDS = """
id>:unread_after AND id<=:unread_through
  AND id NOT IN (SELECT value FROM json_each(:unread_viewed))
"""

UNREAD_SCOPE = f"""
FROM dashboard_activity WHERE section=:unread_section AND {UNREAD_IDS}
"""

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
CREATE INDEX IF NOT EXISTS reports_snapshot_workspace ON reports(project_id, workspace, observed_at DESC, received_at DESC)
    WHERE kind='snapshot';
CREATE INDEX IF NOT EXISTS reports_issue ON reports(issue_id, received_at DESC);
CREATE INDEX IF NOT EXISTS reports_retention_received ON reports(received_at);
CREATE INDEX IF NOT EXISTS reports_archivable_actions
    ON reports(project_id,workspace,json_extract(payload,'$.action_operation')) WHERE kind='usage';
CREATE TABLE IF NOT EXISTS report_archives (
    id INTEGER PRIMARY KEY, project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    month TEXT NOT NULL, expires_at INTEGER NOT NULL, payload BLOB NOT NULL
);
CREATE INDEX IF NOT EXISTS report_archives_by_expiry ON report_archives(expires_at);
CREATE TABLE IF NOT EXISTS archived_report_receipts (
    project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE, report_id TEXT NOT NULL,
    archive_id INTEGER NOT NULL REFERENCES report_archives(id) ON DELETE CASCADE,
    digest TEXT NOT NULL, received_at INTEGER NOT NULL, PRIMARY KEY (project_id,report_id)
);
CREATE INDEX IF NOT EXISTS archived_receipts_by_archive ON archived_report_receipts(archive_id);
CREATE TABLE IF NOT EXISTS archived_actions (
    project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    workspace TEXT NOT NULL, operation_id TEXT NOT NULL, PRIMARY KEY (project_id,workspace,operation_id)
);
CREATE TABLE IF NOT EXISTS archived_usage_history (
    project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE, module TEXT NOT NULL,
    operation TEXT NOT NULL, source TEXT NOT NULL, entry TEXT NOT NULL,
    count INTEGER NOT NULL, development_calls INTEGER NOT NULL, play_calls INTEGER,
    PRIMARY KEY (project_id,module,operation,source,entry)
);
CREATE TABLE IF NOT EXISTS archived_module_history (
    project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE, module TEXT NOT NULL,
    last_use INTEGER NOT NULL, last_success INTEGER NOT NULL, last_play INTEGER NOT NULL,
    PRIMARY KEY (project_id,module)
);
CREATE TABLE IF NOT EXISTS database_maintenance (
    id INTEGER PRIMARY KEY CHECK(id=1), last_success INTEGER NOT NULL DEFAULT 0,
    lease_until INTEGER NOT NULL DEFAULT 0, failures INTEGER NOT NULL DEFAULT 0
);
INSERT OR IGNORE INTO database_maintenance(id) VALUES (1);
-- 历史 SDK 运行记录留在原表，维护台只读取当前支持的证据。
CREATE VIEW IF NOT EXISTS current_reports AS
SELECT rowid, * FROM reports WHERE CASE kind
    WHEN 'usage' THEN COALESCE(json_extract(payload,'$.operation'),'')!='editor.preload.sdk'
    WHEN 'issue' THEN COALESCE(json_extract(payload,'$.code'),'')!='{RETIRED_SDK_ISSUE}'
    WHEN 'implementation' THEN COALESCE(json_extract(payload,'$.stage'),'') NOT IN ('sdk-device','sdk-preload')
        AND NOT (COALESCE(json_extract(payload,'$.stage'),'')='startup-stage'
                 AND COALESCE(json_extract(payload,'$.step'),'')='sdk')
    ELSE 1 END;
CREATE TABLE IF NOT EXISTS history (
    id INTEGER PRIMARY KEY, issue_id INTEGER NOT NULL REFERENCES issues(id),
    actor TEXT NOT NULL, status TEXT NOT NULL, note TEXT NOT NULL, created_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS history_issue ON history(issue_id, id DESC);
CREATE TABLE IF NOT EXISTS sessions (
    token_hash TEXT PRIMARY KEY, username TEXT NOT NULL, csrf TEXT NOT NULL,
    expires_at INTEGER NOT NULL, account_version TEXT
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


def select_list_page(db, query, count_query, bindings, page):
    """在同一份查询快照中读取总数及当前页，末页消失时回到新的末页。"""
    limit, offset = 500, 0
    pagination = None
    if page is not None:
        number, limit = page
        total = db.execute(count_query, bindings).fetchone()[0]
        number = min(number, max(1, (total + limit - 1) // limit))
        offset = (number - 1) * limit
        pagination = {"page": number, "page_size": limit, "total": total}
    rows = db.execute(f"{query} LIMIT :limit OFFSET :offset", bindings | {"limit": limit, "offset": offset})
    return rows, pagination


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
        """初始化服务库，并为历史上报补齐列表使用的接收顺序。"""
        self.path = Path(path)
        self.path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
        with self.connect() as db:
            version = db.execute("PRAGMA user_version").fetchone()[0]
            if version not in (0, 1, 2, 3, 4, 5, 6):
                raise ValueError("数据库版本不受支持。")
            db.execute("PRAGMA journal_mode=WAL")
            db.executescript(SCHEMA)
            if version < 6:
                db.execute("BEGIN IMMEDIATE")
                version = db.execute("PRAGMA user_version").fetchone()[0]
                if version < 2:
                    db.execute("""
                        UPDATE projects SET team='' WHERE team='自动登记'
                        AND id IN (SELECT project_id FROM automatic_workspaces)
                        """)
                if version < 4:
                    self.migrate_activity(db)
                if version < 5:
                    if not any(column["name"] == "account_version" for column in db.execute("PRAGMA table_info(sessions)")):
                        db.execute("ALTER TABLE sessions ADD COLUMN account_version TEXT")
                if version < 6:
                    db.execute("DROP VIEW IF EXISTS current_usage")
                    db.execute(CURRENT_USAGE)
                    db.execute("PRAGMA user_version=6")
            db.execute(IMPLEMENTATION_USAGE)
            db.execute(MAINTAINER_USAGE)
            db.execute(DASHBOARD_ACTIVITY)

    @staticmethod
    def migrate_activity(db):
        """为历史条目补齐接收编号，后续登记和上报在同一写入事务分配编号。"""
        db.execute("""
            CREATE TABLE IF NOT EXISTS activity_receipts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                report_id TEXT, kind TEXT NOT NULL,
                UNIQUE (project_id, report_id),
                FOREIGN KEY (project_id, report_id) REFERENCES reports(project_id, id) ON DELETE CASCADE
            )
            """)
        if db.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name='project_activity'").fetchone():
            db.execute("""
                INSERT INTO activity_receipts(project_id,report_id,kind)
                SELECT a.project_id,a.report_id,COALESCE(r.kind,'project')
                FROM project_activity a LEFT JOIN reports r ON r.project_id=a.project_id AND r.id=a.report_id
                ORDER BY a.id
                """)
            db.execute("DROP TRIGGER IF EXISTS project_registration_activity")
            db.execute("DROP TRIGGER IF EXISTS project_report_activity")
            db.execute("DROP TABLE project_activity")
        db.execute("""
            WITH events AS (
                SELECT id AS project_id,NULL AS report_id,'project' AS kind,created_at AS received_at,
                       0 AS from_report,rowid AS source_order FROM projects
                UNION ALL
                SELECT project_id,id,kind,received_at,1,rowid FROM reports
                WHERE kind IN ('snapshot','hook_activity')
            )
            INSERT INTO activity_receipts(project_id,report_id,kind)
            SELECT e.project_id,e.report_id,e.kind FROM events e
            WHERE NOT EXISTS (
                SELECT 1 FROM activity_receipts a WHERE a.project_id=e.project_id AND a.report_id IS e.report_id
            )
            ORDER BY e.received_at,e.from_report,e.source_order
            """)
        db.execute("""
            INSERT OR IGNORE INTO activity_receipts(project_id,report_id,kind)
            SELECT project_id,id,kind FROM reports WHERE kind IN ('issue','usage') ORDER BY rowid
            """)
        db.execute("""
            CREATE TRIGGER IF NOT EXISTS activity_registration AFTER INSERT ON projects
            BEGIN
                INSERT INTO activity_receipts(project_id,report_id,kind) VALUES (NEW.id,NULL,'project');
            END
            """)
        db.execute("""
            CREATE TRIGGER IF NOT EXISTS activity_report AFTER INSERT ON reports
            WHEN NEW.kind IN ('issue','usage','snapshot','hook_activity')
            BEGIN
                INSERT INTO activity_receipts(project_id,report_id,kind) VALUES (NEW.project_id,NEW.id,NEW.kind);
            END
            """)
        db.execute("DROP VIEW IF EXISTS current_usage")
        db.execute(CURRENT_USAGE)
        db.execute(USAGE_ENTRIES)
        db.execute(MAINTAINER_ACTIVITY)

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

    def start_session(self, user, account_version):
        """旧会话按原期限清理，新会话通过登录时的账号版本验证。"""
        token, csrf = secrets.token_urlsafe(36), secrets.token_urlsafe(24)
        with self.connect() as db:
            db.execute("DELETE FROM sessions WHERE expires_at>0 AND expires_at<=?", (now(),))
            db.execute("DELETE FROM sessions WHERE username=? AND account_version<>?", (user, account_version))
            db.execute("INSERT INTO sessions(token_hash,username,csrf,expires_at,account_version) VALUES (?,?,?,0,?)",
                       (digest(token), user, csrf, account_version))
        return token, csrf

    def session(self, token):
        with self.connect() as db:
            row = db.execute("""
                SELECT username,csrf,account_version FROM sessions WHERE token_hash=?
                AND (expires_at>? OR (expires_at=0 AND account_version IS NOT NULL))
                """, (digest(token), now())).fetchone()
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
            if retired_feedback(event["kind"], event["data"]):
                return (False, None, stamp)
            existing = db.execute("""
                SELECT digest,issue_id,received_at FROM reports WHERE project_id=:project AND id=:id
                UNION ALL
                SELECT digest,NULL,received_at FROM archived_report_receipts WHERE project_id=:project AND report_id=:id
                LIMIT 1
                """, {"project": project_id, "id": event["id"]}).fetchone()
            if existing is not None:
                if existing["digest"] != payload_hash:
                    raise Conflict("同一个记录标识已经关联到不同内容。")
                return (False, existing["issue_id"], existing["received_at"])
            historical_detail = event["kind"] in ("usage", "task", "workflow") or (
                event["kind"] == "implementation" and event["data"]["stage"] == "editor-module"
            )
            if historical_detail and event["observed_at"] < stamp - ARCHIVE_REPORT_DAYS * 86400:
                return (False, None, stamp)
            issue_id = self.attach_issue(db, event) if event["kind"] == "issue" else None
            data = event["data"]
            if event["kind"] == "implementation":
                data = {key: value for key, value in data.items() if key not in ("attempt", "sequence", "milliseconds")}
            db.execute("INSERT INTO reports VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                       (project_id, event["id"], event["workspace"], event["kind"],
                        payload_hash, encoded(data), event["observed_at"], stamp, issue_id))
            if event["kind"] == "implementation":
                db.execute("""
                    INSERT INTO activity_receipts(project_id,report_id,kind)
                    SELECT project_id,report_id,'implementation' FROM implementation_usage
                    WHERE project_id=? AND report_id=?
                    """, (project_id, event["id"]))
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

    def overview(self, days, *, activity_after=None, previous_read=None, include_lists=True):
        """从同一份数据库快照读取维护台数据和浏览器尚需判断的上报。"""
        since = now() - days * 86400
        with self.connect() as db:
            db.execute("BEGIN")
            projects = db.execute("""
                SELECT p.id, p.name, p.team, p.created_at, p.last_seen, h.last_hook_run
                FROM projects AS p LEFT JOIN (
                    SELECT project_id, MAX(observed_at) AS last_hook_run
                    FROM reports WHERE kind='hook_activity' GROUP BY project_id
                ) AS h ON h.project_id=p.id ORDER BY p.name, p.id
                """)
            latest = db.execute("""
                SELECT workspace, project_id, observed_at, received_at, json_extract(payload,'$.framework') AS framework,
                       json_extract(payload,'$.unity') AS unity,
                       json_extract(payload,'$.coverage') AS coverage
                FROM (
                    SELECT *, ROW_NUMBER() OVER (
                        PARTITION BY project_id, workspace ORDER BY observed_at DESC, received_at DESC, rowid DESC) AS n
                    FROM reports WHERE kind='snapshot'
                ) WHERE n=1 ORDER BY observed_at DESC
                """)
            snapshot_count = db.execute("SELECT COUNT(*) FROM reports WHERE kind='snapshot' AND observed_at>=?",
                                        (now() - 86400,)).fetchone()[0]
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
                SELECT DISTINCT module FROM maintainer_usage WHERE observed_at>=? ORDER BY module
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
            activity_totals = dict.fromkeys(ACTIVITY_SECTIONS, 0)
            activity_totals.update(db.execute("SELECT section, COUNT(*) FROM dashboard_activity GROUP BY section"))
            result = {"projects": list(map(dict, projects)), "workspaces": list(map(dict, latest)),
                      "implementation": [{**dict(item), "tools": json.loads(item["tools"])} for item in implementation],
                      "configured_tools": self.configured_implementation_tools(db),
                      "usage_modules": [row["module"] for row in usage_modules],
                      "issue_counts": dict(issue_counts), "activity_totals": activity_totals,
                      "codex_activity": list(map(dict, codex_activity)), "codex_modules": list(map(dict, codex_modules)),
                      "snapshot_count_24h": snapshot_count, "server_time": now(), "days": days}
            if include_lists:
                result["usage"], _ = self.usage_rows(db, since)
                result["issues"], _ = self.issue_rows(db)
            if activity_after is not None:
                result["activity"] = self.activity_since(db, activity_totals, activity_after, previous_read or {})
            return result

    @staticmethod
    def activity_since(db, totals, after, previous_read):
        """按栏目返回接收编号与所属条目，首次读取得出起点，兼容旧版已读数量。"""
        latest = db.execute("SELECT COALESCE(MAX(seq),0) FROM sqlite_sequence WHERE name='activity_receipts'").fetchone()[0]
        starts = dict.fromkeys(ACTIVITY_SECTIONS, latest)
        for section in ACTIVITY_SECTIONS:
            if section in after:
                starts[section] = min(after[section], latest)
            elif 0 <= (seen := previous_read.get(section, -1)) < totals[section]:
                starts[section] = 0 if not seen else db.execute("""
                    SELECT id FROM dashboard_activity WHERE section=? ORDER BY id LIMIT 1 OFFSET ?
                    """, (section, seen - 1)).fetchone()[0]
        sections = {section: {"after": start, "items": []} for section, start in starts.items()}
        if any(start < latest for start in starts.values()):
            bindings = {**starts, "usage_unread_since": now() - RECENT_REPORT_DAYS * 86400}
            for activity_id, section, item in db.execute(f"""
                SELECT id,section,item FROM dashboard_activity
                WHERE (section='projects' AND id>:projects) OR (section='issues' AND id>:issues)
                UNION ALL
                SELECT id,section,item FROM ({RECENT_USAGE_ACTIVITY}) WHERE id>:usage
                ORDER BY id
                """, bindings):
                sections[section]["items"].append((activity_id, item))
        return {"latest": latest, "sections": sections}

    def list_usage(self, days, *, project_id="", module="", source="", entry=None, sort="count", page=None, unread=None):
        """读取选定时间范围及筛选条件下的使用汇总。"""
        with self.connect() as db:
            db.execute("BEGIN")
            return self.usage_rows(db, now() - days * 86400, project_id=project_id, module=module,
                                   source=source, entry=entry, sort=sort, page=page, unread=unread)

    @staticmethod
    def usage_scope(bindings, unread):
        """分析与列表均保留匹配未读条目的完整期内次数。"""
        if unread is None:
            return USAGE_SCOPE
        bindings.update(unread, usage_unread_since=now() - RECENT_REPORT_DAYS * 86400)
        return f"""
        {USAGE_SCOPE} AND json_array({USAGE_GROUP}) IN (
            SELECT item FROM ({RECENT_USAGE_ACTIVITY}) WHERE {UNREAD_IDS}
        )
        """

    @staticmethod
    def usage_rows(db, since, *, project_id="", module="", source="", entry=None, sort="count", page=None, unread=None):
        """分页前按未读条目筛选，保留时间范围内该条目的全部次数。"""
        order = "last_used DESC, count DESC" if sort == "latest" else "count DESC, last_used DESC"
        bindings = {"since": since, "project": project_id, "module": module, "source": source, "entry": entry}
        scope = Database.usage_scope(bindings, unread)
        grouped = f"""
            FROM maintainer_usage
            WHERE {scope}
            GROUP BY {USAGE_GROUP}
            """
        query = f"""
            SELECT {USAGE_GROUP}, json_array({USAGE_GROUP}) AS activity_key,
                   COUNT(*) AS count, SUM(development_call) AS development_calls, SUM(play_calls) AS play_calls,
                   MAX(observed_at) AS last_used
            {grouped}
            ORDER BY {order}, {USAGE_GROUP}
            """
        rows, pagination = select_list_page(db, query, f"SELECT COUNT(*) FROM (SELECT 1 {grouped})", bindings, page)
        return list(map(dict, rows)), pagination

    def list_usage_occurrences(self, days, activity_key, page):
        """从与操作汇总相同的去重记录读取每一次发生时间。"""
        with self.connect() as db:
            db.execute("BEGIN")
            scope = f"FROM maintainer_usage WHERE observed_at>=:since AND json_array({USAGE_GROUP})=:item"
            rows, pagination = select_list_page(db, f"SELECT observed_at,play_calls {scope} "
                                                "ORDER BY observed_at DESC, activity_id DESC, play_calls DESC",
                                                f"SELECT COUNT(*) {scope}",
                                                {"since": now() - days * 86400, "item": activity_key}, page)
            return list(map(dict, rows)), pagination

    def list_workflow(self, project_id, trace, page):
        """读取一个项目的协作记录，支持按同一次任务或操作检查全部回执。"""
        with self.connect() as db:
            db.execute("BEGIN")
            scope = "FROM reports WHERE project_id=:project AND kind='workflow' AND observed_at>=:since"
            if trace:
                scope += " AND (json_extract(payload,'$.trace')=:trace OR json_extract(payload,'$.receipt')=:trace)"
            bindings = {"project": project_id, "trace": trace, "since": now() - RECENT_REPORT_DAYS * 86400}
            rows, pagination = select_list_page(db, f"SELECT workspace,observed_at,payload {scope} "
                                                "ORDER BY observed_at DESC,rowid DESC",
                                                f"SELECT COUNT(*) {scope}", bindings, page)
            return list(map(self.decode, rows)), pagination

    def usage_analysis(self, days, *, project_id="", module="", source="", entry=None, unread=None):
        """从操作明细统计上报条数和调用次数，保留所选项目的历史证据。"""
        bindings = {"since": now() - days * 86400, "project": project_id, "module": module, "source": source, "entry": entry}
        scope = self.usage_scope(bindings, unread)
        with self.connect() as db:
            rows = db.execute(f"""
                WITH totals AS (
                    SELECT module,operation,source,entry,COUNT(CASE WHEN in_scope THEN 1 END) AS count,
                           COUNT(CASE WHEN :project='' OR project_id=:project THEN 1 END) AS history,
                           SUM(CASE WHEN in_scope THEN development_call ELSE 0 END) AS development_calls,
                           SUM(CASE WHEN :project='' OR project_id=:project THEN
                               CASE WHEN in_scope THEN play_calls WHEN play_calls IS NOT NULL THEN 0 END
                           END) AS play_calls
                    FROM (SELECT *, ({scope}) AS in_scope FROM maintainer_usage)
                    GROUP BY module,operation,source,entry
                    UNION ALL
                    SELECT module,operation,source,entry,0,SUM(count),0,CASE WHEN COUNT(play_calls) THEN 0 END
                    FROM archived_usage_history WHERE :project='' OR project_id=:project
                    GROUP BY module,operation,source,entry
                )
                SELECT module,operation,source,entry,SUM(count) AS count,SUM(history) AS history,
                       SUM(development_calls) AS development_calls,SUM(play_calls) AS play_calls
                FROM totals
                GROUP BY module,operation,source,entry
                """, bindings)
            return list(map(dict, rows))

    def list_issues(self, *, project_id="", status="", search="", sort="", page=None, unread=None):
        """按维护台选择的筛选与排序读取问题列表。"""
        with self.connect() as db:
            db.execute("BEGIN")
            return self.issue_rows(db, project_id, status, search, sort, page=page, unread=unread)

    @staticmethod
    def issue_rows(db, project_id="", status="", search="", sort="", *, page=None, unread=None):
        """按项目、状态及未读条件筛选问题，保留匹配问题的全部发生次数。"""
        conditions = ["i.code!=:retired_code"]
        bindings = {"retired_code": RETIRED_SDK_ISSUE}
        if project_id:
            conditions.append("i.id IN (SELECT issue_id FROM current_reports WHERE project_id=:project AND kind='issue')")
            bindings["project"] = project_id
        if status:
            conditions.append(f"({MAINTAINER_ISSUE_STATUS})=:status")
            bindings["status"] = status
        if search:
            db.create_function("issue_text_lower", 1, str.lower, deterministic=True)
            conditions.append("instr(issue_text_lower('#' || i.id || ' ' || i.area || ' ' || i.title || ' ' || i.code), :search) > 0")
            bindings["search"] = search.lower()
        if unread is not None:
            conditions.append(f"i.id IN (SELECT CAST(item AS INTEGER) {UNREAD_SCOPE})")
            bindings.update(unread, unread_section="issues")
        where = "WHERE " + " AND ".join(conditions)
        order = {"latest": "i.last_seen DESC", "count": "occurrences DESC, i.last_seen DESC"}.get(
            sort, f"({MAINTAINER_ISSUE_STATUS})='closed', i.last_seen DESC")
        query = f"""
            SELECT i.id,i.code,i.area,i.title,{MAINTAINER_ISSUE_STATUS} AS status,i.assignee,
                   i.first_seen,i.last_seen,i.duplicate_of,
                   COUNT(r.id) AS occurrences, COUNT(DISTINCT r.project_id) AS projects,
                   {ISSUE_LATEST_PAYLOAD} AS latest_payload
            FROM issues i LEFT JOIN current_reports r ON r.issue_id=i.id {where}
            GROUP BY i.id
            ORDER BY {order}, i.id DESC
            """
        rows, pagination = select_list_page(db, query, f"SELECT COUNT(*) FROM issues i {where}", bindings, page)
        return list(map(issue_summary, rows)), pagination

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

    @staticmethod
    def snapshot_rows(db, project_id, page=None):
        """项目详情和分页沿用同一份采集时间排序。"""
        scope = "FROM reports WHERE project_id=:project AND kind='snapshot'"
        query = f"""
            SELECT id,workspace,observed_at,received_at,payload {scope}
            ORDER BY observed_at DESC,received_at DESC,rowid DESC
        """
        bindings = {"project": project_id}
        if page is None:
            return db.execute(f"{query} LIMIT 100", bindings), None
        return select_list_page(db, query, f"SELECT COUNT(*) {scope}", bindings, page)

    def list_project_snapshots(self, project_id, page):
        """按项目分页读取环境快照，在同一数据库快照中计算总数。"""
        with self.connect() as db:
            db.execute("BEGIN")
            if db.execute("SELECT 1 FROM projects WHERE id=?", (project_id,)).fetchone() is None:
                return None
            rows, pagination = self.snapshot_rows(db, project_id, page)
            return {"snapshots": list(map(self.decode, rows)), "pagination": pagination}

    def project_detail(self, project_id):
        """从同一份数据库快照读取项目环境、实施进展和上报来源。"""
        with self.connect() as db:
            db.execute("BEGIN")
            project = db.execute("SELECT id,name,team,created_at,last_seen FROM projects WHERE id=?",
                                 (project_id,)).fetchone()
            if project is None:
                return None
            snapshots, _ = self.snapshot_rows(db, project_id)
            since = now() - RECENT_REPORT_DAYS * 86400
            tasks = db.execute("""
                SELECT workspace, observed_at, received_at, payload FROM reports
                WHERE project_id=? AND kind='task' AND observed_at>=?
                ORDER BY received_at DESC, rowid DESC LIMIT 30
                """, (project_id, since))
            hook_activity = db.execute("""
                SELECT workspace, observed_at AS last_run, json_extract(payload,'$.framework') AS framework
                FROM (
                    SELECT workspace, observed_at, payload, ROW_NUMBER() OVER (
                        PARTITION BY workspace ORDER BY observed_at DESC, received_at DESC, rowid DESC
                    ) AS n FROM reports WHERE project_id=? AND kind='hook_activity'
                ) WHERE n=1 ORDER BY last_run DESC, workspace
                """, (project_id,))
            unity_mcp_connections = db.execute("""
                SELECT workspace,observed_at,received_at,payload FROM (
                    SELECT *, ROW_NUMBER() OVER (
                        PARTITION BY workspace ORDER BY observed_at DESC,received_at DESC,rowid DESC
                    ) AS n FROM reports WHERE project_id=? AND kind='implementation'
                      AND json_extract(payload,'$.stage')='unity-mcp-connection'
                ) WHERE n=1 ORDER BY observed_at DESC,workspace
                """, (project_id,))
            implementation = db.execute("""
                SELECT workspace,observed_at,received_at,payload FROM (
                    SELECT *, ROW_NUMBER() OVER (
                        PARTITION BY workspace ORDER BY observed_at DESC,received_at DESC,rowid DESC
                    ) AS n FROM current_reports WHERE project_id=? AND kind='implementation'
                      AND json_extract(payload,'$.stage')='workspace'
                ) WHERE n=1 ORDER BY observed_at DESC,received_at DESC,rowid DESC LIMIT 400
                """, (project_id,))
            # 成功和其余结果各取最近一条，保留早于当前结果的验证证据。
            outcomes = db.execute("""
                WITH facts AS (
                    SELECT rowid,workspace,observed_at,received_at,payload,
                           json_extract(payload,'$.stage') AS stage,
                           json_extract(payload,'$.outcome')='success' AS succeeded
                    FROM current_reports WHERE project_id=? AND kind='implementation'
                      AND (json_extract(payload,'$.stage') IN ('config-export','config-load','pipify-batch',
                           'plugpals-query','bundle-build','editor-module','table-ready')
                           OR (json_extract(payload,'$.stage')='player-build'
                               AND (json_extract(payload,'$.platform') IN ('Android','iOS')
                                    OR json_type(payload,'$.batch_mode') IS NOT NULL)
                               AND json_extract(payload,'$.build_kind') IN ('player','project')))
                )
                SELECT workspace,observed_at,received_at,
                       json_remove(payload,'$.attempt','$.sequence','$.milliseconds') AS payload FROM (
                    SELECT *, ROW_NUMBER() OVER (
                        PARTITION BY stage,succeeded,workspace,
                            json_extract(payload,'$.framework'),json_extract(payload,'$.unity'),
                            CASE WHEN stage IN ('player-build','bundle-build') THEN json_extract(payload,'$.platform') END,
                            CASE WHEN stage IN ('plugpals-query','editor-module') THEN json_extract(payload,'$.step') END,
                            json_extract(payload,'$.build_kind'),json_extract(payload,'$.development'),
                            COALESCE(json_extract(payload,'$.batch_mode'),0)
                        ORDER BY observed_at DESC,received_at DESC,rowid DESC
                    ) AS n FROM facts
                ) WHERE n=1 ORDER BY observed_at DESC,received_at DESC,rowid DESC
                """, (project_id,))
            modules = list(map(dict, db.execute(f"""
                WITH tool_events AS ({MODULE_IMPLEMENTATION_EVENTS}), usage_events AS (
                    SELECT workspace,observed_at,json_extract(payload,'$.framework') AS framework,
                           json_extract(payload,'$.module') AS module,json_extract(payload,'$.outcome') AS outcome,
                           json_extract(payload,'$.operation') AS operation,
                           CASE WHEN json_extract(payload,'$.source')='editor'
                                AND json_extract(payload,'$.operation')
                                    GLOB ('editor.' || json_extract(payload,'$.module') || '.export-*')
                                THEN 1 ELSE 0 END AS manual_export
                    FROM current_usage WHERE project_id=:project
                      AND COALESCE(json_extract(payload,'$.entry'),'')!='skill'
                ), events AS (
                    SELECT workspace,observed_at,framework,
                           CASE WHEN operation IN ('nova.project.bundle.build-asset','nova.project.bundle.build-raw-file')
                                THEN 'asset' ELSE module END AS module,outcome,1 AS usage_call,manual_export,
                           {MODULE_DEVELOPMENT_CALL} AS development_call,
                           0 AS play_session,0 AS play_calls
                    FROM usage_events
                    UNION ALL
                    SELECT workspace,observed_at,framework,
                           CASE module WHEN 'bundle' THEN 'asset' ELSE module END,
                           outcome,0,0,development_call,play_session,COALESCE(play_calls,0)
                    FROM tool_events WHERE project_id=:project
                ), recent AS (
                    SELECT workspace,framework,module,SUM(usage_call) AS count,SUM(play_session) AS play_sessions,
                           MAX(observed_at) AS last_use,
                           MAX(CASE WHEN outcome='success' THEN observed_at END) AS last_success,
                           SUM(CASE WHEN outcome IN ('failed','blocked') THEN usage_call ELSE 0 END) AS problems,
                           SUM(manual_export) AS manual_exports,
                           SUM(development_call) AS development_calls,SUM(play_calls) AS play_calls
                    FROM events WHERE observed_at>=:since
                    GROUP BY workspace,framework,module ORDER BY last_use DESC LIMIT 500
                ), summary_events AS (
                    SELECT * FROM events
                    UNION ALL
                    SELECT NULL,last_use,NULL,module,NULL,0,0,0,0,0
                    FROM archived_module_history WHERE project_id=:project
                    UNION ALL
                    SELECT NULL,last_success,NULL,module,'success',0,0,0,0,0
                    FROM archived_module_history WHERE project_id=:project AND last_success>0
                )
                SELECT * FROM recent
                UNION ALL
                SELECT NULL,NULL,COALESCE(aliases.value,evidence.module),
                       SUM(CASE WHEN observed_at>=:since THEN usage_call ELSE 0 END),
                       SUM(CASE WHEN observed_at>=:since THEN play_session ELSE 0 END),
                       MAX(observed_at) AS last_use,
                       MAX(CASE WHEN outcome='success' THEN observed_at END) AS last_success,
                       SUM(CASE WHEN observed_at>=:since AND outcome IN ('failed','blocked') THEN usage_call ELSE 0 END),
                       SUM(CASE WHEN observed_at>=:since THEN manual_export ELSE 0 END),
                       SUM(CASE WHEN observed_at>=:since THEN development_call ELSE 0 END),
                       SUM(CASE WHEN observed_at>=:since THEN play_calls ELSE 0 END)
                FROM summary_events AS evidence
                LEFT JOIN json_each(:module_aliases) AS aliases ON aliases.key=evidence.module
                GROUP BY COALESCE(aliases.value,evidence.module)
                """, {"project": project_id, "since": since, "module_aliases": encoded(MODULE_ALIASES)})))
            upgrade_issues = db.execute("""
                SELECT workspace,observed_at,received_at,issue_id,payload FROM (
                    SELECT *, ROW_NUMBER() OVER (
                        PARTITION BY workspace,json_extract(payload,'$.framework')
                        ORDER BY observed_at DESC,received_at DESC,rowid DESC
                    ) AS n FROM reports WHERE project_id=? AND kind='issue'
                        AND json_extract(payload,'$.area') IN ('editor-compilation','package-import')
                ) WHERE n=1 ORDER BY observed_at DESC LIMIT 100
                """, (project_id,))
            archived_play_at = db.execute("SELECT COALESCE(MAX(last_play),0) FROM archived_module_history WHERE project_id=?",
                                          (project_id,)).fetchone()[0]
            return {"project": dict(project), "snapshots": list(map(self.decode, snapshots)),
                    "tasks": list(map(self.decode, tasks)), "hook_activity": list(map(dict, hook_activity)),
                    "archived_play_at": archived_play_at,
                    "unity_mcp_connections": list(map(self.decode, unity_mcp_connections)),
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
