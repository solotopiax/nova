"""Keep recent feedback online and maintain verified backups in the service process."""

from contextlib import closing
from datetime import datetime, timedelta, timezone
import gzip
from pathlib import Path
import re
import shutil
import sqlite3
import sys
from threading import Event, Thread
import time
from uuid import uuid4

from database import (
    ARCHIVE_REPORT_DAYS, CURRENT_USAGE_SELECT, DEVELOPMENT_ENTRIES, MODULE_DEVELOPMENT_CALL,
    MODULE_IMPLEMENTATION_SELECT, RECENT_REPORT_DAYS, USAGE_ENTRIES_SELECT, encoded, now,
)


SECONDS_PER_DAY = 86400
CHECK_INTERVAL = 15 * 60
BATCH_SIZE = 256
RUN_TIMEOUT = 20 * 60
LEASE_SECONDS = 30 * 60
BACKUP_COUNT = 7
MAINTENANCE_HOUR = 3
BACKUP_NAME = re.compile(r"nova-issues-\d{4}-\d{2}-\d{2}\.sqlite3\Z")
TEMP_BACKUP_NAME = re.compile(r"\.backup-[0-9a-f]{32}\.tmp(?:-wal|-shm|-journal)?\Z")


ARCHIVE_CANDIDATES = """
WITH first_reports AS (
    SELECT (SELECT MIN(rowid) FROM current_reports WHERE project_id=a.project_id) AS rowid
    FROM (SELECT DISTINCT project_id FROM automatic_workspaces) a
)
SELECT r.rowid FROM reports r
WHERE r.observed_at<:cutoff AND r.received_at<:cutoff
AND r.rowid NOT IN (SELECT rowid FROM first_reports WHERE rowid IS NOT NULL)
AND (
    r.kind IN ('usage','workflow','task')
    OR (r.kind='implementation' AND json_extract(r.payload,'$.stage')='editor-module')
    OR (
        (r.kind='hook_activity' OR (r.kind='implementation' AND json_extract(r.payload,'$.stage')='unity-mcp-connection'))
        AND r.rowid<>(
            SELECT latest.rowid FROM reports latest
            WHERE latest.project_id=r.project_id AND latest.workspace=r.workspace AND latest.kind=r.kind
            AND (r.kind='hook_activity' OR json_extract(latest.payload,'$.stage')='unity-mcp-connection')
            ORDER BY latest.observed_at DESC,latest.received_at DESC,latest.rowid DESC LIMIT 1
        )
    )
)
AND (
    r.kind!='usage' OR json_extract(r.payload,'$.action_operation') IS NULL
    OR NOT EXISTS (
        SELECT 1 FROM reports peer
        WHERE peer.kind='usage' AND peer.project_id=r.project_id AND peer.workspace=r.workspace
        AND json_extract(peer.payload,'$.action_operation')=json_extract(r.payload,'$.action_operation')
        AND (peer.observed_at>=:cutoff OR peer.received_at>=:cutoff OR peer.rowid IN (SELECT rowid FROM first_reports))
    )
)
ORDER BY r.received_at,r.rowid LIMIT :limit
"""


def archive_batch(database, stamp):
    """在同一事务压缩、保存历史汇总及回执，再移除在线明细。"""
    bindings = {"cutoff": stamp - RECENT_REPORT_DAYS * SECONDS_PER_DAY, "limit": BATCH_SIZE}
    with database.connect() as db:
        db.execute("BEGIN IMMEDIATE")
        db.execute("CREATE TEMP TABLE archive_candidates(rowid INTEGER PRIMARY KEY)")
        db.execute(f"INSERT INTO archive_candidates {ARCHIVE_CANDIDATES}", bindings)
        if not db.execute("SELECT 1 FROM archive_candidates LIMIT 1").fetchone():
            return 0

        # 一次 Action 的两侧观察使用同一归档事务，即使落在两批的交界处。
        db.execute("""
            INSERT OR IGNORE INTO archive_candidates
            SELECT peer.rowid FROM archive_candidates c JOIN reports r ON r.rowid=c.rowid
            JOIN reports peer ON peer.project_id=r.project_id AND peer.workspace=r.workspace
              AND json_extract(peer.payload,'$.action_operation')=json_extract(r.payload,'$.action_operation')
            WHERE r.kind='usage' AND peer.kind='usage'
            """)
        db.execute(f"""
            CREATE TEMP TABLE archive_usage AS
            WITH candidate_reports AS (
                SELECT r.* FROM archive_candidates c JOIN current_reports r ON r.rowid=c.rowid
            ), candidate_usage AS ({CURRENT_USAGE_SELECT.format(report_source="candidate_reports")}),
               entries AS ({USAGE_ENTRIES_SELECT.format(usage_source="candidate_usage")}),
               implementation AS ({MODULE_IMPLEMENTATION_SELECT.format(report_source="candidate_reports")})
            SELECT project_id,module,operation,source,entry,observed_at,outcome,NULL AS play_calls FROM entries
            UNION ALL
            SELECT project_id,module,stage,source,'play',observed_at,outcome,play_calls
            FROM implementation WHERE play_session OR play_calls IS NOT NULL
            """)
        db.execute(f"""
            INSERT INTO archived_usage_history
            SELECT project_id,module,operation,source,entry,COUNT(*),
                   SUM(CASE WHEN entry IN {DEVELOPMENT_ENTRIES} THEN {MODULE_DEVELOPMENT_CALL} ELSE 0 END),SUM(play_calls)
            FROM archive_usage GROUP BY project_id,module,operation,source,entry
            ON CONFLICT(project_id,module,operation,source,entry) DO UPDATE SET
                count=archived_usage_history.count+excluded.count,
                development_calls=archived_usage_history.development_calls+excluded.development_calls,
                play_calls=CASE WHEN archived_usage_history.play_calls IS NOT NULL OR excluded.play_calls IS NOT NULL
                                THEN COALESCE(archived_usage_history.play_calls,0)+COALESCE(excluded.play_calls,0) END
            """)
        db.execute("""
            INSERT INTO archived_module_history
            SELECT project_id,detail_module,MAX(observed_at),MAX(CASE WHEN outcome='success' THEN observed_at ELSE 0 END),
                   MAX(CASE WHEN entry='play' THEN observed_at ELSE 0 END)
            FROM (
                SELECT *, CASE WHEN operation IN ('nova.project.bundle.build-asset','nova.project.bundle.build-raw-file')
                               THEN 'asset' ELSE module END AS detail_module
                FROM archive_usage WHERE entry!='skill'
            ) GROUP BY project_id,detail_module
            ON CONFLICT(project_id,module) DO UPDATE SET
                last_use=MAX(archived_module_history.last_use,excluded.last_use),
                last_success=MAX(archived_module_history.last_success,excluded.last_success),
                last_play=MAX(archived_module_history.last_play,excluded.last_play)
            """)
        db.execute("""
            INSERT OR IGNORE INTO archived_actions
            SELECT r.project_id,r.workspace,json_extract(r.payload,'$.action_operation')
            FROM archive_candidates c JOIN reports r ON r.rowid=c.rowid
            WHERE r.kind='usage' AND json_extract(r.payload,'$.action_operation') IS NOT NULL
            """)
        rows = list(map(dict, db.execute("""
            SELECT r.*,a.id AS activity_id FROM archive_candidates c JOIN reports r ON r.rowid=c.rowid
            LEFT JOIN activity_receipts a ON a.project_id=r.project_id AND a.report_id=r.id
            ORDER BY r.rowid
            """)))
        months = {}
        for row in rows:
            month = datetime.fromtimestamp(row["observed_at"], timezone.utc).strftime("%Y-%m")
            months.setdefault((row["project_id"], month), []).append(row)
        for (project, month), records in months.items():
            raw = encoded(records).encode("utf-8")
            blob = gzip.compress(raw, compresslevel=6, mtime=0)
            expires_at = max(max(item["observed_at"], item["received_at"]) for item in records)
            expires_at += ARCHIVE_REPORT_DAYS * SECONDS_PER_DAY
            archive_id = db.execute("""
                INSERT INTO report_archives(project_id,month,expires_at,payload) VALUES (?,?,?,?)
                """, (project, month, expires_at, blob)).lastrowid
            saved = db.execute("SELECT payload FROM report_archives WHERE id=?", (archive_id,)).fetchone()[0]
            if gzip.decompress(saved) != raw:
                raise ValueError("归档校验失败。")
            db.executemany("INSERT INTO archived_report_receipts VALUES (?,?,?,?,?)",
                           ((project, item["id"], archive_id, item["digest"], item["received_at"]) for item in records))
        db.execute("DELETE FROM reports WHERE rowid IN (SELECT rowid FROM archive_candidates)")
        return len(rows)


def check_backup(path):
    """验证完整的 SQLite 副本能读取。"""
    with closing(sqlite3.connect(path.resolve().as_uri() + "?mode=ro", uri=True)) as db:
        if db.execute("PRAGMA quick_check").fetchall() != [("ok",)]:
            raise ValueError("数据库备份校验失败。")


def backup(db_path, output, progress=None):
    """在线复制 SQLite，并检查完整副本后才将其作为有效备份。"""
    db_path, output = Path(db_path), Path(output)
    if not db_path.is_file():
        raise ValueError("找不到待备份的数据库。")
    output.touch(mode=0o600, exist_ok=False)
    try:
        with closing(sqlite3.connect(db_path.resolve().as_uri() + "?mode=ro", uri=True)) as source:
            with closing(sqlite3.connect(output)) as destination:
                source.backup(destination, pages=128, progress=progress, sleep=.05)
        check_backup(output)
    except BaseException:
        output.unlink(missing_ok=True)
        raise


class DatabaseMaintenance:
    """由服务调度每日维护，异常或重启后按 SQLite 中的进度重试。"""

    def __init__(self, database):
        self.database = database
        self.stop = Event()
        self.thread = None

    def start(self):
        self.thread = Thread(target=self.run, name="nova-issues-maintenance", daemon=True)
        self.thread.start()

    def close(self):
        self.stop.set()
        if self.thread is not None:
            self.thread.join()

    def status(self):
        with self.database.connect() as db:
            row = db.execute("SELECT last_success,failures FROM database_maintenance WHERE id=1").fetchone()
        return {"active": self.thread is not None and self.thread.is_alive(), "last_success": row["last_success"],
                "failures": row["failures"], "recent_days": RECENT_REPORT_DAYS, "archive_days": ARCHIVE_REPORT_DAYS}

    def run(self):
        while not self.stop.is_set():
            try:
                self.run_due()
            except (OSError, ValueError, sqlite3.Error) as error:
                if not self.stop.is_set():
                    print(f"nova-issues 数据库维护稍后重试。{error}", file=sys.stderr, flush=True)
            self.stop.wait(CHECK_INTERVAL)

    def run_due(self):
        stamp = now()
        local_time = datetime.fromtimestamp(stamp)
        cycle = local_time.replace(hour=MAINTENANCE_HOUR, minute=0, second=0, microsecond=0)
        if cycle > local_time:
            cycle -= timedelta(days=1)

        lease = stamp + LEASE_SECONDS
        with self.database.connect() as db:
            claimed = db.execute("""
                UPDATE database_maintenance SET lease_until=? WHERE id=1 AND lease_until<=? AND last_success<?
                """, (lease, stamp, int(cycle.timestamp()))).rowcount
        if not claimed:
            return False

        deadline = time.monotonic() + RUN_TIMEOUT

        def check_progress(*_):
            if self.stop.is_set() or time.monotonic() >= deadline:
                raise InterruptedError("本轮维护已让出，下次继续。")

        succeeded = False
        try:
            self.daily_backup(cycle, check_progress)
            while True:
                check_progress()
                if not archive_batch(self.database, stamp):
                    break
                self.stop.wait(.02)
            while True:
                check_progress()
                with self.database.connect() as db:
                    deleted = db.execute("""
                        DELETE FROM report_archives WHERE id IN (
                            SELECT id FROM report_archives WHERE expires_at<=? ORDER BY expires_at LIMIT ?
                        )
                        """, (stamp, BATCH_SIZE)).rowcount
                if deleted < BATCH_SIZE:
                    break
                self.stop.wait(.02)
            self.reclaim_space()
            succeeded = True
            return True
        finally:
            with self.database.connect() as db:
                if succeeded:
                    db.execute("""
                        UPDATE database_maintenance SET lease_until=0,last_success=?,failures=0 WHERE id=1 AND lease_until=?
                        """, (now(), lease))
                else:
                    db.execute("""
                        UPDATE database_maintenance SET lease_until=0,failures=failures+? WHERE id=1 AND lease_until=?
                        """, (int(not self.stop.is_set()), lease))

    def daily_backup(self, cycle, progress):
        directory = self.database.path.parent / "maintenance-backups"
        directory.mkdir(mode=0o700, exist_ok=True)
        for item in directory.iterdir():
            if TEMP_BACKUP_NAME.fullmatch(item.name) and item.is_file():
                item.unlink()
        target = directory / f"nova-issues-{cycle:%Y-%m-%d}.sqlite3"
        valid = False
        if target.is_file():
            try:
                check_backup(target)
                valid = True
            except (OSError, ValueError, sqlite3.Error):
                pass
        if not valid:
            with self.database.connect() as db:
                size = db.execute("PRAGMA page_count").fetchone()[0] * db.execute("PRAGMA page_size").fetchone()[0]
            if shutil.disk_usage(directory).free < size:
                raise OSError("剩余空间不足以创建新备份。")
            temporary = directory / f".backup-{uuid4().hex}.tmp"
            try:
                backup(self.database.path, temporary, progress)
                temporary.replace(target)
            finally:
                temporary.unlink(missing_ok=True)
        backups = sorted((item for item in directory.iterdir() if BACKUP_NAME.fullmatch(item.name) and item.is_file()),
                         reverse=True)
        for old in backups[BACKUP_COUNT:]:
            old.unlink()

    def reclaim_space(self):
        """保留 SQLite 的在线写入能力，低峰再尝试回收足够大的空闲空间。"""
        with self.database.connect() as db:
            db.execute("PRAGMA busy_timeout=200")
            db.execute("PRAGMA wal_checkpoint(PASSIVE)").fetchone()
            page_size = db.execute("PRAGMA page_size").fetchone()[0]
            pages = db.execute("PRAGMA page_count").fetchone()[0]
            free_pages = db.execute("PRAGMA freelist_count").fetchone()[0]
            if (MAINTENANCE_HOUR <= datetime.now().hour < 5 and free_pages * page_size >= 16 * 1024 * 1024
                    and free_pages * 4 >= pages and shutil.disk_usage(self.database.path.parent).free >= 2 * pages * page_size):
                deadline = time.monotonic() + 2
                db.set_progress_handler(lambda: int(self.stop.is_set() or time.monotonic() >= deadline), 1000)
                try:
                    db.execute("VACUUM")
                    db.execute("PRAGMA wal_checkpoint(TRUNCATE)").fetchone()
                except sqlite3.OperationalError as error:
                    if getattr(error, "sqlite_errorcode", None) not in (sqlite3.SQLITE_BUSY, sqlite3.SQLITE_INTERRUPT):
                        raise
                finally:
                    db.set_progress_handler(None, 0)
