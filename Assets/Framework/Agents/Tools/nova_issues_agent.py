#!/usr/bin/env python3
"""Local Nova issue analysis, Codex event ingestion and durable delivery."""

import argparse
from contextlib import contextmanager
from datetime import datetime
from functools import lru_cache
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shlex
import shutil
import sqlite3
import subprocess
import sys
import time
import urllib.parse
import uuid


FRAMEWORK = "com.solotopia.nova.framework"
WORKDIR = Path("Library/Nova/Issues")
REPORT_SERVER = "http://172.16.22.175:8787"
ENVIRONMENT_REPORT_KEY = "editor_environment_report"
HOOK_ACTIVITY_REPORT_KEY = "codex_hook_activity_report"
HOOK_ACTIVITY_INTERVAL = 3600
# 共享队列只按已确认的服务能力产生新版正文，已入队的报告按原内容重试。
COMPACT_REPORTS_KEY = "compact_reports"
RECEIPT_RETENTION_SECONDS = 30 * 86400
MINIMUM_RECLAIM_BYTES = 1 << 20
MAX_INPUT = 4 << 20
ID = re.compile(r"[a-zA-Z0-9][a-zA-Z0-9_.-]{0,95}\Z")
VERSION = re.compile(r"[a-zA-Z0-9][a-zA-Z0-9.\-+]{0,79}\Z")
APPLICATION_IDENTIFIER = re.compile(r"[a-zA-Z0-9_-]+(?:\.[a-zA-Z0-9_-]+)+\Z")
PRIVATE = re.compile(
    r"(?i)(\b(?:https?|ssh|file)://|\b[A-Z]:[\\/]|/(?:Users|home|private|tmp)/|"
    r"-----BEGIN [A-Z ]*PRIVATE KEY|\bbearer\s+\S+|"
    r"\b(?:api[_-]?key|access[_-]?token|password|secret)\s*[:=]\s*\S+)"
)
ROUTES = (
    ("nova-project-configure-runtime", "config", "nova.project.config.export-runtime",
     r"(?:导出|生成).{0,14}(?:ConfigRuntime|运行时配置|Nova\s*配置)|(?:ConfigRuntime|运行时配置).{0,14}(?:导出|生成)|export.{0,26}(?:ConfigRuntime|runtime config)"),
    ("nova-project-export-tables", "table", "nova.project.table.export",
     r"(?:导出|生成).{0,10}(?:数据表|Luban\s*表|Table\s*(?:代码|数据))|(?:数据表|Luban\s*表).{0,10}导出|export.{0,18}Luban"),
    ("nova-project-configure-pipeline", "pipeline", "nova.project.pipify.run-batch",
     r"(?:执行|运行|启动).{0,14}(?:Pipify|流水线批次)|(?:Pipify|流水线批次).{0,18}(?:执行|运行)"),
    ("nova-project-generate-hybridclr-artifacts", "hotfix", "nova.project.hotfix.generate-artifacts",
     r"(?:HybridCLR.{0,20}Generate All|Generate All.{0,20}HybridCLR|(?:生成|刷新).{0,12}(?:AOT 泛型|HybridCLR 产物))"),
    ("nova-project-update-localization", "localization", "nova.project.localization.export",
     r"(?:导出|生成).{0,10}(?:本地化|多语言)"),
    ("nova-project-integrate-network-api", "network", "nova.project.network.export",
     r"(?:导出|生成).{0,10}(?:网络表|NetCmd|HostKey)"),
)
ROUTE_BY_SKILL = {route[0]: route for route in ROUTES}
COMPLETE = re.compile(r"已(?:完成|成功|导出|生成|执行|处理|通过)|已.*(?:通过验收|验证通过)|\b(?:completed|succeeded|exported)\b", re.I)
DEFERRED = re.compile(r"(?:请|需要)你(?:确认|提供|选择|执行)|尚未(?:执行|完成)|未能|无法|等(?:待|你)(?:确认|连接|处理)|"
                      r"仅(?:检查|定位|验证|分析)|建议(?:先|你)|\b(?:unable to|not executed|for your approval)\b", re.I)
DETOUR = re.compile(r"手工|临时绕|绕行|规避|workaround", re.I)
FRAMEWORK_FINDING = re.compile(r"Nova.{0,14}(?:框架|协作体系|工具链).{0,20}(?:缺陷|缺口|遗漏|异常|不一致)", re.I)
CONFIRMED_FRAMEWORK_FINDING = re.compile(
    r"(?<!无)(?<!未)(?<!非)已(?:确认|证实)\s*(?:为|是)?\s*"
    r"(?:(?:一|两|多)(?:个|项)\s*)?(?:Nova\s*(?:框架|协作体系|工具链)|框架(?:侧|层)?|Framework)\s*"
    r"(?:问题|缺陷|缺口|遗漏|异常|不一致)"
    r"|(?:Nova\s*框架|框架侧)(?:问题|缺陷|根因)(?:已确认|已证实)|框架侧待修复", re.I
)
FINDING_TASK_CONTEXT = (
    "Nova 自动反馈：执行任务的 Agent 在核实框架问题后，按当前任务授权和反馈状态，立即依实际解析的 "
    "Framework 包内 Docs/Onboarding/NOVA_ISSUES.md 调用 finding 提交脱敏证据。业务已完成或已经绕行时同样处理。"
    "收尾按上报编号检查已送达或待补传状态。证据不足的线索保留在本机。"
)
OBSERVED_TOOLS = ("codex", "unity-cli")
HOOK_EVENTS = {"SessionStart", "UserPromptSubmit", "PostToolUse", "Stop", "Interrupt"}
ACTION_ID = re.compile(r"nova\.project\.([a-z0-9][a-z0-9-]*)\.[a-z0-9][a-z0-9_.-]*\Z")
UNITY_MCP_TOOL = re.compile(r"mcp__(?:unitymcp|nova_unity_[a-f0-9]{16})__([a-zA-Z0-9_.-]+)\Z", re.I)
SKILL_ID = re.compile(r"nova-project-[a-z0-9-]+\Z")
EXECUTION_HINT = re.compile(r"(?:^|[ /])unity(?:[ .]|$)|\b(?:py|python3?|dotnet)\b.{0,140}(?:export|generate|build)", re.I)
UNITY_COMMAND = re.compile(r"^\s*['\"]?(?:[^'\"\r\n]+[/\\])?unity(?:\.exe)?['\"]?\s+command\s+([a-z0-9_]+)\s+([a-z0-9_]+)\b", re.I)
SHELL_TOOLS = ("Bash", "exec_command")
# 旧账本中的 actual=1 只表示命令线索，2 表示已核对成功回执。
VERIFIED_EXECUTION = 2
EXECUTION_EVIDENCE_SOURCE = "codex.PostToolUse.exit-zero"
USAGE_OUTCOMES = ("executed", "success", "partial", "blocked", "failed", "cancelled")
NOVA_CLI = {
    "nova_table_source.py": ("table", {"inspect", "execute"}),
    "nova_tracks_source.py": ("tracks", {"inspect", "execute", "recover"}),
    "nova_excel_template.py": ("excel", {"inspect", "execute"}),
    "nova_capabilities.py": ("toolchain", {"inventory", "describe"}),
    "nova_skills.py": ("toolchain", {"resolve", "doctor"}),
}


SCHEMA = """
CREATE TABLE IF NOT EXISTS state (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS turns (
    id TEXT PRIMARY KEY, route TEXT, version TEXT NOT NULL, started INTEGER NOT NULL,
    evidence TEXT NOT NULL DEFAULT '', actual INTEGER NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'observing', updated INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS turn_findings (
    turn_id TEXT NOT NULL, report_id TEXT NOT NULL,
    PRIMARY KEY(turn_id, report_id)
) WITHOUT ROWID;
CREATE TRIGGER IF NOT EXISTS discard_turn_findings AFTER DELETE ON turns
BEGIN
    DELETE FROM turn_findings WHERE turn_id=OLD.id;
END;
CREATE TABLE IF NOT EXISTS outbox (
    id TEXT PRIMARY KEY, payload TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0,
    ready_at INTEGER NOT NULL, accepted INTEGER, issue_id INTEGER, error TEXT
);
CREATE INDEX IF NOT EXISTS ready_reports ON outbox(accepted, ready_at);
CREATE TABLE IF NOT EXISTS report_receipts (
    id TEXT PRIMARY KEY, kind TEXT NOT NULL, accepted INTEGER NOT NULL, issue_id INTEGER
) WITHOUT ROWID;
CREATE INDEX IF NOT EXISTS report_receipts_by_time ON report_receipts(accepted);
CREATE TRIGGER IF NOT EXISTS skip_delivered_report BEFORE INSERT ON outbox
WHEN EXISTS (SELECT 1 FROM report_receipts WHERE id=NEW.id)
BEGIN
    SELECT RAISE(IGNORE);
END;
CREATE TRIGGER IF NOT EXISTS discard_delivered_report AFTER UPDATE OF accepted ON outbox
WHEN NEW.accepted IS NOT NULL
BEGIN
    INSERT OR IGNORE INTO report_receipts (id,kind,accepted,issue_id)
        VALUES (NEW.id,COALESCE(json_extract(NEW.payload,'$.kind'),'unknown'),NEW.accepted,NEW.issue_id);
    DELETE FROM outbox WHERE id=NEW.id;
END;
"""

# 与 Unity Editor 的待发查询一致，已停用的试验记录和缺少回执的自动入口遗漏保留在本机。
SENDABLE_REPORT_FILTER = f"""
    json_extract(payload,'$.kind')!='task'
    AND COALESCE(json_extract(payload,'$.data.stage'),'') NOT IN
        ('sdk-device','sdk-preload','startup-stage','framework-upgrade','pipify-step')
    AND NOT (COALESCE(json_extract(payload,'$.data.stage'),'')='player-build'
             AND (COALESCE(json_extract(payload,'$.data.platform'),'') NOT IN ('Android','iOS')
                  OR json_extract(payload,'$.data.build_kind')='scripts'))
    AND COALESCE(json_extract(payload,'$.data.operation'),'')!='editor.preload.sdk'
    AND COALESCE(json_extract(payload,'$.data.code'),'')!='editor-sdk-preload-exceeds-60-seconds'
    AND (
        COALESCE(json_extract(payload,'$.data.source'),'')!='codex'
        OR COALESCE(json_extract(payload,'$.data.code'),'') NOT GLOB 'missing-nova-route.*'
        OR EXISTS (SELECT 1 FROM json_each(outbox.payload,'$.data.evidence') AS evidence
                   WHERE json_extract(evidence.value,'$.source')='{EXECUTION_EVIDENCE_SOURCE}')
    )
"""


def now():
    return int(time.time())


def compact(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def event_id(*parts):
    return hashlib.sha256(compact(parts).encode("utf-8")).hexdigest()[:32]


def read_json(path, limit=2 << 20):
    if not path.is_file() or path.stat().st_size > limit:
        return {}
    try:
        result = json.loads(path.read_bytes())
    except (OSError, ValueError, RecursionError):
        return {}
    return result if isinstance(result, dict) else {}


def string(value, limit=160):
    if not isinstance(value, str) or len(value) > limit:
        return ""
    return value


def version(value):
    return value if isinstance(value, str) and VERSION.fullmatch(value) else "unknown"


def identity(value):
    return value if isinstance(value, str) and ID.fullmatch(value) else ""


def text_field(value, limit):
    value = string(value, limit).strip()
    return value if not PRIVATE.search(value) and not any(
        ord(char) < 32 and char not in "\n\t" for char in value) else ""


def run_readonly(command, root, timeout=1):
    try:
        return subprocess.run(command, cwd=root, capture_output=True, timeout=timeout, check=False)
    except (OSError, subprocess.TimeoutExpired):
        return None


def git_user_name(root):
    """读取当前工程生效的 Git 用户名，缺失或不适合显示时返回空值。"""
    result = run_readonly(["git", "--no-optional-locks", "config", "--get", "user.name"], root)
    if result is None or result.returncode != 0:
        return ""
    try:
        name = text_field(result.stdout.decode("utf-8").strip(), 100)
    except UnicodeDecodeError:
        return ""
    return name if not re.search(r"[\x00-\x1f\x7f-\x9f]", name) else ""


def git_safe(root):
    """写入前核对 Git 忽略和跟踪状态，已见仓库标记时直接查这两项。"""
    git = shutil.which("git")
    git_present = any((parent / ".git").exists() for parent in (root, *root.parents))
    if not git:
        return not git_present
    command = [git, "--no-optional-locks"]
    if not git_present:
        inside = run_readonly([*command, "rev-parse", "--is-inside-work-tree"], root)
        if inside is None or inside.returncode:
            return True
        if inside.stdout.strip() != b"true":
            return False
    ignored = run_readonly([*command, "check-ignore", "-q", "--", WORKDIR.as_posix() + "/"], root)
    if ignored is None or ignored.returncode != 0:
        return False
    tracked = run_readonly([*command, "ls-files", "--", WORKDIR.as_posix()], root)
    return tracked is not None and tracked.returncode == 0 and not tracked.stdout


def project_folder(root):
    if not root.is_dir() or not git_safe(root):
        return None
    for part in (root / "Library", root / "Library/Nova", root / WORKDIR):
        if part.is_symlink() or (part.exists() and not part.is_dir()):
            return None
    folder = root / WORKDIR
    if any((folder / name).is_symlink() for name in ("agent.sqlite3", "agent.sqlite3-wal", "agent.sqlite3-shm", "agent.sqlite3-journal")):
        return None
    folder.mkdir(mode=0o700, parents=True, exist_ok=True)
    if folder.resolve() != folder:
        return None
    return folder


def nova_project(cwd):
    """从任务或项目路径识别最近的 Unity 工程，只接受使用 Nova Framework 的项目。"""
    for root in (cwd, *cwd.parents):
        if not (root / "ProjectSettings/ProjectVersion.txt").is_file():
            continue
        manifest, locked = package_data(root)
        if FRAMEWORK in manifest or FRAMEWORK in locked:
            return root
        metadata = read_json(root / "Packages" / FRAMEWORK / "package.json", 96 << 10)
        return root if metadata.get("name") == FRAMEWORK and version(metadata.get("version")) != "unknown" else None
    return None


@contextmanager
def ledger(folder):
    database = folder / "agent.sqlite3"
    try:
        handle = os.open(database, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    except FileExistsError:
        pass
    else:
        os.close(handle)
    db = sqlite3.connect(database, timeout=0.8)
    db.row_factory = sqlite3.Row
    try:
        db.execute("PRAGMA journal_mode=WAL")
        db.execute("PRAGMA temp_store=MEMORY")
        db.executescript(SCHEMA)
        if db.execute("SELECT 1 FROM state WHERE key='workspace'").fetchone() is None:
            with db:
                db.execute("INSERT OR IGNORE INTO state VALUES ('workspace',?)", (uuid.uuid4().hex,))
        with db:
            yield db
    finally:
        db.close()


def state(db, name, default=None):
    row = db.execute("SELECT value FROM state WHERE key=?", (name,)).fetchone()
    if row is None:
        return default
    return row["value"] if name == "workspace" else json.loads(row["value"])


def save(db, name, value):
    db.execute("INSERT OR REPLACE INTO state VALUES (?,?)", (name, compact(value)))


def automatic_credentials(root, db):
    """复用 Editor 和 Codex 共享的工作区身份，保留升级前的本地待发记录。"""
    project = state(db, "automatic_project")
    if project is None:
        try:
            with (root / "ProjectSettings/ProjectSettings.asset").open(encoding="utf-8") as source:
                settings = source.read(65536)
        except (OSError, UnicodeError):
            settings = ""
        product = re.search(r"^  productGUID:[ \t]+([a-fA-F0-9]{32})[ \t]*$", settings, re.M)
        name = re.search(r"^  productName:[ \t]+([^\n]+)$", settings, re.M)
        guid = product[1].lower() if product and int(product[1], 16) else state(db, "workspace")
        display_name = (text_field(name[1], 100) if name else "") or text_field(root.name, 100) or "Nova 项目"
        project = {"id": "auto-" + guid, "name": display_name}
        db.execute("INSERT OR IGNORE INTO state VALUES ('automatic_project',?)", (compact(project),))
        project = state(db, "automatic_project")
    token = state(db, "automatic_token")
    if token is None:
        db.execute("INSERT OR IGNORE INTO state VALUES ('automatic_token',?)", (compact(secrets.token_hex(32)),))
        token = state(db, "automatic_token")
    destination = {"project": project["id"], "server": REPORT_SERVER}
    if state(db, "destination") != destination:
        save(db, "destination", destination)
    return project, token


def enqueue(db, kind, value, report_id=None, observed_at=None):
    """写入固定的上报正文，只有兼容旧服务时才附带旧必填字段。"""
    if kind == "snapshot" and state(db, COMPACT_REPORTS_KEY) is True:
        value = {**value, "packages": [
            {key: item[key] for key in ("id", "version", "source")} for item in value["packages"]
        ]}
    elif kind == "issue" and state(db, COMPACT_REPORTS_KEY) is True:
        value = {key: item for key, item in value.items() if key != "priority"}

    identifier = report_id or uuid.uuid4().hex
    event = {"version": 1, "id": identifier, "workspace": state(db, "workspace"),
             "observed_at": observed_at or now(), "kind": kind, "data": value}
    db.execute("INSERT OR IGNORE INTO outbox (id,payload,ready_at) VALUES (?,?,?)",
               (identifier, compact(event), now()))


def prune_delivered_reports(db):
    """把旧版已确认正文迁为短回执，并清理到期的回执。"""
    db.execute("UPDATE outbox SET accepted=accepted WHERE accepted IS NOT NULL")
    db.execute("DELETE FROM report_receipts WHERE accepted<?", (now() - RECEIPT_RETENTION_SECONDS,))


def reclaim_ledger_space(db):
    """在事务之外归还积累的空闲页，遇到并发访问时留待下次。"""
    try:
        free_pages = db.execute("PRAGMA freelist_count").fetchone()[0]
        if (free_pages * db.execute("PRAGMA page_size").fetchone()[0] >= MINIMUM_RECLAIM_BYTES
                and free_pages * 2 >= db.execute("PRAGMA page_count").fetchone()[0]):
            db.execute("VACUUM")
            db.execute("PRAGMA wal_checkpoint(TRUNCATE)").fetchone()
    except sqlite3.OperationalError:
        pass


def package_data(root):
    package_dir = root / "Packages"
    manifest = read_json(package_dir / "manifest.json").get("dependencies", {})
    locked = read_json(package_dir / "packages-lock.json").get("dependencies", {})
    return (manifest if isinstance(manifest, dict) else {},
            locked if isinstance(locked, dict) else {})


def package_type(value):
    return {
        "registry": "registry", "local": "local", "embedded": "embedded",
        "git": "git", "builtin": "builtin", "localtarball": "local",
    }.get(str(value).lower(), "unknown")


def local_framework(root, manifest, locked):
    declared = manifest.get(FRAMEWORK)
    candidates = [root / "Packages" / FRAMEWORK]
    for spec in (declared, locked.get(FRAMEWORK, {}).get("version") if isinstance(locked.get(FRAMEWORK), dict) else None):
        if isinstance(spec, str) and spec.startswith("file:"):
            candidates.append((root / "Packages" / urllib.parse.unquote(spec[5:])).resolve())
    for path in candidates:
        package = read_json(path / "package.json", 96 << 10)
        if package.get("name") == FRAMEWORK and version(package.get("version")) != "unknown":
            return package["version"], path
    return None, None


def resolved_framework(root, db):
    manifest, locked = package_data(root)
    installed, path = local_framework(root, manifest, locked)
    if path is not None:
        return installed, path
    source = state(db, "editor_source")
    if isinstance(source, str) and Path(source).is_absolute():
        candidate = Path(source).resolve()
        metadata = read_json(candidate / "package.json", 96 << 10)
        locked_version = locked.get(FRAMEWORK)
        expected = version(locked_version.get("version")) if isinstance(locked_version, dict) else "unknown"
        found = version(metadata.get("version"))
        if metadata.get("name") == FRAMEWORK and found != "unknown" and expected in ("unknown", found):
            return found, candidate
    lock = locked.get(FRAMEWORK)
    expected = version(lock.get("version")) if isinstance(lock, dict) else "unknown"
    if isinstance(lock, dict) and lock.get("source") == "registry" and expected != "unknown":
        matches = []
        for candidate in (root / "Library" / "PackageCache").glob(f"{FRAMEWORK}@*"):
            metadata = read_json(candidate / "package.json", 96 << 10)
            if metadata.get("name") == FRAMEWORK and metadata.get("version") == expected:
                matches.append(candidate.resolve())
        if len(matches) == 1:
            return expected, matches[0]
    return None, None


def unity_version(root):
    """从当前 Unity 工程读取版本，不启动 Editor 或 CLI。"""
    with (root / "ProjectSettings/ProjectVersion.txt").open(encoding="utf-8") as file:
        found = re.search(r"^m_EditorVersion:\s*(\S+)", file.read(4096), re.M)
    return version(found[1]) if found else "unknown"


def observed_framework(root, db):
    """读取当前 Framework 版本，供本地问题归因和模块使用记录引用。"""
    manifest, locked = package_data(root)
    installed, _ = local_framework(root, manifest, locked)
    lock = locked.get(FRAMEWORK)
    declared = lock.get("version") if isinstance(lock, dict) else manifest.get(FRAMEWORK)
    registered = next((item["version"] for item in state(db, "editor", {}).get("packages", [])
                       if item["id"] == FRAMEWORK), None)
    framework = installed or version(declared)
    return registered if framework == "unknown" and registered else framework


def snapshot(root, db):
    """用注册包和本地包清单生成环境快照，只采集维护台会展示的模块和工具。"""
    manifest, locked = package_data(root)
    editor = state(db, "editor", {})
    packages = {}
    for name, item in locked.items():
        if not identity(name) or not isinstance(item, dict):
            continue
        if name.startswith("com.unity.modules."):
            continue
        packages[name] = {"id": name, "version": version(item.get("version")), "source": package_type(item.get("source")),
                          "evidence": "resolved"}
    for name, item in manifest.items():
        if identity(name) and name not in packages:
            packages[name] = {"id": name, "version": version(item), "source": "unknown", "evidence": "declared"}
    actual_version, _ = local_framework(root, manifest, locked)
    if actual_version:
        packages[FRAMEWORK] = {"id": FRAMEWORK, "version": actual_version, "source": "local", "evidence": "resolved"}
    registered = editor.get("packages", [])
    lock = locked.get(FRAMEWORK, {})
    registered_framework = next((p["version"] for p in registered if p["id"] == FRAMEWORK), "")
    expected = actual_version or (version(lock.get("version")) if isinstance(lock, dict) else "unknown")
    match = bool(registered_framework) and expected in ("unknown", registered_framework)
    if match:
        packages.update({item["id"]: item for item in registered})
    framework = packages.get(FRAMEWORK, {}).get("version", "unknown")
    if FRAMEWORK not in packages:
        return None
    tools = {name: item for name, item in editor.get("tools", {}).items() if name in OBSERVED_TOOLS} if match else {}
    for name, recorded in state(db, "codex_tools", {}).items():
        if name in OBSERVED_TOOLS and now() - recorded["at"] < 1800:
            tools[name] = tools.get(name, {}) | recorded["tool"]
    for name, installed in state(db, "cli_versions", {}).items():
        tools.setdefault(name, {"state": "installed"})["version"] = installed
    result = {"framework": framework, "unity": unity_version(root),
              "packages": sorted(packages.values(), key=lambda item: item["id"])[:350],
              "tools": tools, "coverage": {"agent": "observed",
                                          "codex": "observed" if now() - state(db, "codex_seen", 0) < 1800 else "waiting",
                                          "editor": "observed" if now() - editor.get("at", 0) < 1800 else "waiting"}}
    if match:
        result.update({name: editor[name] for name in ("enabled", "application_identifiers", "codex_hooks")
                       if name in editor})
    operating_system = {"darwin": "macos", "win32": "windows", "linux": "linux"}.get(sys.platform)
    if operating_system:
        result["operating_system"] = operating_system
    return result


def report_editor_environment(root, db, *, compilation_day=None):
    """兼容 Editor 的脚本入口，与原生采集共用工作区每日上报状态。"""
    data = snapshot(root, db)
    if not data:
        return
    project, _ = automatic_credentials(root, db)
    stamp = now()
    day = datetime.fromtimestamp(stamp).date().isoformat()
    previous = state(db, ENVIRONMENT_REPORT_KEY, {})
    fields = [project["id"], data["framework"], data["unity"]]
    identifiers = data.get("application_identifiers", {})
    fields.extend((
        "|".join(f"{name}={identifiers[name]}" for name in sorted(identifiers)),
        data.get("codex_hooks"),
        "\n".join(f"{item['id']}|{item['version']}|{item['source']}"
                  for item in sorted(data["packages"], key=lambda item: item["id"])),
        "\n".join(sorted(set(data.get("enabled", [])))),
        "\n".join(f"{name}|{item['state']}|{item.get('version', 'unknown')}"
                  for name, item in sorted(data["tools"].items())),
        data.get("operating_system"),
    ))
    git_name = git_user_name(root)
    if git_name:
        data["git_user_name"] = git_name
        fields.append(git_name)
    signature = event_id(*fields)
    if previous.get("hash") != signature or compilation_day == day and previous.get("day") != day:
        enqueue(db, "snapshot", data, observed_at=stamp)
        save(db, ENVIRONMENT_REPORT_KEY, {"hash": signature, "day": day})


def expected_route(prompt):
    if re.search(r"不(?:要|用|走|执行).{0,10}(?:Nova|导出|运行|构建)|仅(?:分析|解释|查看|检查)|"
                 r"(?:如何|怎么|是否).{0,12}(?:导出|生成|运行)", prompt, re.I):
        return None
    for route in ROUTES:
        if f"${route[0]}" in prompt or re.search(route[3], prompt, re.I):
            return route
    return None


def turn_key(event):
    session_id, turn_id = string(event.get("session_id"), 160), string(event.get("turn_id"), 160)
    return event_id(session_id, turn_id) if session_id and turn_id else None


def record_turn(root, db, event, identifier):
    """保留本轮归因所需的本地状态和 Framework 版本。"""
    row = db.execute("SELECT * FROM turns WHERE id=?", (identifier,)).fetchone()
    if row is not None:
        return row
    framework = observed_framework(root, db)
    route = expected_route(string(event.get("prompt"), 16000)) if event.get("hook_event_name") == "UserPromptSubmit" else None
    stamp = now()
    db.execute("""
        INSERT OR IGNORE INTO turns (id,route,version,started,updated) VALUES (?,?,?,?,?)
        """, (identifier, route[0] if route else None, framework, stamp, stamp))
    return db.execute("SELECT * FROM turns WHERE id=?", (identifier,)).fetchone()


def tool_operation(event):
    """识别直接调用及已存在的 MCP 自定义工具转发入口。"""
    tool_name = string(event.get("tool_name"), 200)
    arguments = event.get("tool_input")
    if not isinstance(arguments, dict):
        return tool_name, "", "", ""
    command = string(arguments.get("command"), 24000)
    if not command and isinstance(arguments.get("cmd"), str):
        command = string(arguments["cmd"], 24000)
    if not command and isinstance(arguments.get("code"), str):
        command = string(arguments["code"], 24000)
    action_input = arguments
    if tool_name.endswith("__execute_custom_tool") and arguments.get("tool_name") == "nova_project_action":
        action_input = arguments.get("parameters")
    elif not tool_name.endswith("__nova_project_action") and tool_name != "nova_project_action":
        return tool_name, "", "", command
    if isinstance(action_input, dict):
        action = identity(action_input.get("action_id"))
        if ACTION_ID.fullmatch(action):
            return tool_name, action, identity(action_input.get("operation")), command
    return tool_name, "", "", command


def record_usage(db, identifier, framework, module, operation, entry, outcome, invocation, action_operation=None):
    """只写入结构化标识和结果，用轮次与调用编号防止重复记数。"""
    report_id = event_id("codex-usage", state(db, "workspace"), identifier, entry, invocation)
    data = {"module": module, "operation": operation, "framework": framework,
            "outcome": outcome, "source": "codex", "entry": entry}
    if action_operation:
        data["action_operation"] = action_operation
    enqueue(db, "usage", data, report_id)


def unity_mcp_operation(event):
    """仅识别 Coplay Unity MCP 的 Codex 回执，Nova Action 沿用自己的统计。"""
    if event.get("hook_event_name") != "PostToolUse":
        return ""
    match = UNITY_MCP_TOOL.fullmatch(string(event.get("tool_name"), 200))
    if not match:
        return ""
    tool = identity(match[1])
    if tool == "execute_custom_tool":
        arguments = event.get("tool_input")
        tool = identity(arguments.get("tool_name")) if isinstance(arguments, dict) else ""
    return identity(f"unity-mcp.{tool}") if tool and tool != "nova_project_action" else ""


def unity_mcp_outcome(value, depth=0):
    """从 Coplay 和 MCP 回执提取终态，参数和结果正文只在此进程内处理。"""
    if depth > 4:
        return None
    if isinstance(value, dict):
        if value.get("isError") is True or value.get("is_error") is True:
            return "failed"
        status = value.get("status")
        if status in USAGE_OUTCOMES:
            return status
        if status == "error" or value.get("success") is False:
            return "failed"
        if value.get("success") is True:
            return "success"
        for key in ("structuredContent", "structured_content", "result", "content"):
            if outcome := unity_mcp_outcome(value.get(key), depth + 1):
                return outcome
    elif isinstance(value, list):
        for item in value[:8]:
            if isinstance(item, dict) and item.get("type") == "text":
                if outcome := unity_mcp_outcome(item.get("text"), depth + 1):
                    return outcome
    elif isinstance(value, str) and len(value) <= 65536 and value.lstrip().startswith("{"):
        try:
            return unity_mcp_outcome(json.loads(value), depth + 1)
        except (ValueError, RecursionError):
            pass
    return None


def shell_outcome(event):
    """仅从工具结果读取退出状态，输出正文不进入上报字段。"""
    reply = event.get("tool_response")
    exit_code = reply.get("exit_code") if isinstance(reply, dict) else None
    if isinstance(reply, str):
        code = re.search(r"\b(?:exit(?:ed with)? code)\s*:?\s*(-?\d+)", reply[:1024], re.I)
        if code:
            exit_code = int(code[1])
    return "failed" if type(exit_code) is int and exit_code else "executed"


def verified_execution(event, tool_name, command):
    """核对本轮确定性执行命令的成功回执，帮助、预览和复合命令留待取证。"""
    if tool_name not in SHELL_TOOLS or not string(event.get("tool_use_id"), 160):
        return False
    reply = event.get("tool_response")
    if isinstance(reply, dict):
        code = reply.get("exit_code")
        succeeded = type(code) is int and code == 0
    elif isinstance(reply, str):
        succeeded = bool(re.match(
            r"\AChunk ID: [^\r\n]+\r?\nWall time: [^\r\n]+\r?\nProcess exited with code 0[ \t]*(?:\r?\n|\Z)",
            reply[:1024],
        ))
    else:
        succeeded = False
    if not succeeded or re.search(r"[\r\n;&|`]|\$\(", command):
        return False
    try:
        words = shlex.split(command.replace("\\", "/"))
    except ValueError:
        return False
    if not words or {"-h", "--help", "--version", "--dry-run", "--preview", "--plan",
                     "inspect", "describe", "status", "verify", "plan"} & set(words[1:]):
        return False

    unity = UNITY_COMMAND.match(command)
    if unity:
        return bool(re.match(r"(?:export|generate|build)(?:_|$)", unity[2], re.I))
    runtime = Path(words[0]).stem.lower()
    if runtime == "dotnet":
        # 仅识别有明确导出、生成或构建名称的托管工具。
        if len(words) < 2:
            return False
        target = Path(words[1])
        if words[1] == "run" and "--project" in words[2:-1]:
            target = Path(words[words.index("--project") + 1])
        return target.suffix.lower() in (".dll", ".csproj") and bool(re.search(r"export|generate|build", target.stem, re.I))
    if not re.fullmatch(r"py|python(?:3(?:\.\d+)?)?", runtime):
        return False
    script = Path(next((word for word in words[1:] if word not in ("-B", "-u", "-3")), ""))
    return script.suffix.lower() == ".py" and bool(re.search(r"export|generate|build", script.stem, re.I))


def command_cwd(root, event):
    """解析工具执行目录，用于核对 CLI 或 Skill 的实际来源。"""
    arguments = event.get("tool_input")
    workdir = string(arguments.get("workdir"), 2400) if isinstance(arguments, dict) else ""
    workdir = workdir or string(event.get("cwd"), 2400)
    return Path(workdir) if workdir and Path(workdir).is_absolute() else root


def nova_cli_operation(root, db, event, command):
    """识别随包的确定性 CLI 及子命令，排除同名项目脚本。"""
    if not any(name in command for name in NOVA_CLI):
        return None
    try:
        words = shlex.split(command.replace("\\", "/"))
    except ValueError:
        return None
    if not words or not re.fullmatch(r"(?:py|python(?:3(?:\.\d+)?)?)(?:\.exe)?", Path(words[0]).name, re.I):
        return None
    argv = iter(words[1:])
    script = next(argv, "")
    while script in ("-3", "-B", "-u"):
        script = next(argv, "")
    path = command_cwd(root, event) / script
    adapter = NOVA_CLI.get(path.name)
    if not adapter:
        return None
    command_name = next(argv, "")
    if command_name in ("--root", "--project-root"):
        next(argv, None)
        command_name = next(argv, "")
    elif command_name.startswith(("--root=", "--project-root=")):
        command_name = next(argv, "")
    module, commands = adapter
    if command_name not in commands:
        return None
    _, package = resolved_framework(root, db)
    if not package or path.resolve() != package / "Agents" / "Tools" / path.name or not path.is_file():
        return None
    return module, f"{path.stem}.{command_name}"


def observed_cli_usage(root, db, event, command, framework, identifier):
    """统计 Unity CLI 和随包 Nova CLI，排除状态轮询与计划辅助步骤。"""
    tool_id = string(event.get("tool_use_id"), 160)
    if not tool_id:
        return False
    match = UNITY_COMMAND.match(command)
    if match:
        group, name = (item.lower() for item in match.groups())
        if (group, name) in (("editor", "status"), ("script", "recompile_status"), ("test", "list"), ("test", "status")):
            return False
        module, operation, entry = "unity-cli", identity(f"unity.{group}.{name}"), "unity-cli"
        if not operation:
            return False
    else:
        nova = nova_cli_operation(root, db, event, command)
        if not nova:
            return False
        module, operation = nova
        entry = "nova-cli"
    record_usage(db, identifier, framework, module, operation, entry, shell_outcome(event), tool_id)
    return True


def response_dict(value):
    """在 MCP 的包装结果中优先读取实际操作状态。"""
    if isinstance(value, str):
        try:
            parsed = json.loads(value)
        except (ValueError, RecursionError):
            return {}
        return response_dict(parsed)
    if isinstance(value, list):
        for entry in value[:8]:
            result = response_dict(entry)
            if result and "status" in result:
                return result
        return {}
    if isinstance(value, dict):
        if value.get("status") in USAGE_OUTCOMES:
            return value
        for name in ("data", "structuredContent", "content", "text"):
            if name in value and isinstance(value[name], (list, dict, str)):
                result = response_dict(value[name])
                if result and "status" in result:
                    return result
        return value
    return {}


def observed_action_usage(db, event, framework, identifier, action, operation):
    """统计已调用的 Action，每次 execute 只记一次，回执状态用于结果展示。"""
    if operation != "execute":
        return False
    tool_id = string(event.get("tool_use_id"), 160)
    if not tool_id:
        return False
    reply = event.get("tool_response")
    response = response_dict(reply)
    outcome = response.get("status")
    if outcome not in USAGE_OUTCOMES:
        error = isinstance(reply, dict) and (reply.get("isError") is True or reply.get("is_error") is True)
        outcome = "failed" if error else "executed"
    details = response.get("data")
    token = string(details.get("recovery_token"), 100) if isinstance(details, dict) else ""
    match = re.fullmatch(r"nova-action-operation-v1:([0-9a-fA-F]{32})", token)
    # 恢复令牌只用于在本地提取稳定标识，上报值是与 Editor 相同的摘要。
    same_action = response.get("operation") == "execute" and response.get("action_id") == action
    action_operation = event_id("action", action, match[1]) if match and same_action else None
    record_usage(db, identifier, framework, ACTION_ID.fullmatch(action)[1], action, "action", outcome, tool_id,
                 action_operation)
    return True


def observed_skills(root, db, event, tool_name, command, framework, identifier):
    """按实际读取的 Nova Skill 入口按轮计数，避免一次任务反复打开重复计数。"""
    arguments = event.get("tool_input", {})
    if tool_name in SHELL_TOOLS and "SKILL.md" in command:
        try:
            words = shlex.split(command.replace("\\", "/"))
        except ValueError:
            return set()
        if not words or Path(words[0]).name.lower() not in ("cat", "sed", "head", "tail", "bat", "get-content", "type"):
            return set()
    elif tool_name == "Read" and isinstance(arguments, dict):
        words = [string(arguments.get("file_path"), 2400)]
    else:
        return set()
    cwd = command_cwd(root, event)
    result = set()
    for word in words:
        path = cwd / word.replace("\\", "/")
        skill = path.parent.name
        if path.name != "SKILL.md" or not SKILL_ID.fullmatch(skill) or not path.is_file():
            continue
        projected = root / ".agents" / "skills" / skill / "SKILL.md"
        if path.resolve() != projected:
            _, installed = resolved_framework(root, db)
            if not installed or path.resolve() != installed / "Agents" / "Skills" / skill / "SKILL.md":
                continue
        contract = read_json(path.parent / "references" / "contract.json", 256 << 10)
        if contract.get("id") != skill:
            continue
        adapters = contract.get("actionAdapters")
        if not isinstance(adapters, list):
            continue
        action_domains = set()
        facade_modules = set()
        for adapter in adapters:
            if not isinstance(adapter, dict):
                continue
            entry = string(adapter.get("entry"), 2400)
            action = ACTION_ID.fullmatch(entry)
            if adapter.get("kind") == "agent-action" and action:
                action_domains.add(action[1])
            elif adapter.get("kind") == "csharp-api":
                facade_modules.update(match.lower() for match in re.findall(r"\bNova\.([A-Z][A-Za-z0-9]+)\.", entry))
        domains = action_domains or facade_modules
        module = next(iter(domains)) if len(domains) == 1 else skill.removeprefix("nova-project-")
        record_usage(db, identifier, framework, module, skill, "skill", "executed", skill)
        result.add(skill)
    return result


def routing_issue(route, framework, had_workaround):
    """使用本轮真实执行回执生成经过本地核对的入口遗漏报告。"""
    skill, module, action, _ = route
    issue = {
        "code": f"missing-nova-route.{module}", "area": "routing", "priority": "P2",
        "title": f"{module} 任务完成时未观察到应走的 Nova 入口",
        "summary": "本地 Agent 在任务开始时匹配到 Nova 操作，在同一轮任务结束时核对了工具调用和交付结果。",
        "expected": f"在已接入 Nova 的工程中使用 {skill} 或 {action}。",
        "actual": "本轮有执行命令成功退出的回执，未观察到对应的 Nova 入口。",
        "framework": framework, "source": "codex",
        "steps": "在已接入 Nova 的消费工程提出对应功能的实际执行请求，核对本轮调用及完成回执。",
        "evidence": [
            {"source": "codex.UserPromptSubmit", "detail": f"提交时匹配任务 {skill}。"},
            {"source": EXECUTION_EVIDENCE_SOURCE, "detail": f"执行命令成功退出，本轮未观察到对应 Skill 或 {action} 调用。"},
            {"source": "codex.Stop", "detail": "同一轮任务以已完成的交付表述结束。"},
        ],
    }
    if had_workaround:
        issue["workaround"] = "同一轮任务记录提到采用了绕行。"
    return issue


def relevant_hook(event):
    """在打开工程账本前跳过与 Nova 采集和归因无关的工具回调。"""
    name = event.get("hook_event_name")
    if name != "PostToolUse":
        return name in HOOK_EVENTS
    tool, action, _, command = tool_operation(event)
    arguments = event.get("tool_input")
    if action or tool in ("apply_patch", "Write", "Edit"):
        return True
    if tool == "Read" and isinstance(arguments, dict):
        return string(arguments.get("file_path"), 2400).endswith("SKILL.md")
    return tool in SHELL_TOOLS and bool("SKILL.md" in command or EXECUTION_HINT.search(command)
                                        or UNITY_COMMAND.match(command) or any(script in command for script in NOVA_CLI))


def report_hook_activity(db, framework, stamp):
    """同一工作区首次运行立即入队，后续按实际运行时间限频。"""
    cutoff = stamp - HOOK_ACTIVITY_INTERVAL
    previous = state(db, HOOK_ACTIVITY_REPORT_KEY)
    if previous is not None and previous > cutoff:
        return False
    # 竞争中的钩子必须在同一事务里取得上报资格并入队。
    claimed = db.execute("""
        INSERT INTO state (key,value) VALUES (?,?)
        ON CONFLICT(key) DO UPDATE SET value=excluded.value WHERE CAST(state.value AS INTEGER)<=?
    """, (HOOK_ACTIVITY_REPORT_KEY, compact(stamp), cutoff))
    if claimed.rowcount != 1:
        return False
    enqueue(db, "hook_activity", {"framework": framework}, observed_at=stamp)
    return True


def handle_hook(root, db, event, response=None):
    """上报限频的钩子运行记录、模块用量和已确认问题，任务观察保存在工作区。"""
    name = event.get("hook_event_name")
    if name not in HOOK_EVENTS:
        return False
    identifier = turn_key(event)
    if name == "UserPromptSubmit" and response is not None:
        context = FINDING_TASK_CONTEXT
        if identifier:
            context += f" 本轮调用 finding 时，将 {identifier} 填入 turn 字段，供收尾核对。"
        response["hookSpecificOutput"] = {"hookEventName": name, "additionalContext": context}
    stamp = now()
    if stamp - state(db, "codex_seen", 0) >= 30:
        save(db, "codex_seen", stamp)
        tools = state(db, "codex_tools", {})
        tools["codex"] = {"at": stamp, "tool": {"state": "observed"}}
        save(db, "codex_tools", tools)
    if name == "SessionStart":
        report_hook_activity(db, observed_framework(root, db), stamp)
        return has_pending_reports(db)
    if not identifier:
        return False
    turn = record_turn(root, db, event, identifier)
    framework = turn["version"]
    pending = report_hook_activity(db, framework, stamp)
    if name == "PostToolUse":
        tool_name, action, operation, command = tool_operation(event)
        skills = observed_skills(root, db, event, tool_name, command, framework, identifier)
        route = ROUTE_BY_SKILL.get(turn["route"])
        if route:
            trusted_path = f"{route[0]}/SKILL.md"
            if action == route[2] or route[0] in skills or (
                tool_name in SHELL_TOOLS and trusted_path in command.replace("\\", "/")
            ):
                db.execute("UPDATE turns SET evidence=?, updated=? WHERE id=?",
                           ("action" if action else "skill", stamp, identifier))
            if turn["actual"] != VERIFIED_EXECUTION and verified_execution(event, tool_name, command):
                db.execute("UPDATE turns SET actual=?,updated=? WHERE id=?", (VERIFIED_EXECUTION, stamp, identifier))
        if action:
            pending |= observed_action_usage(db, event, framework, identifier, action, operation)
        elif tool_name in SHELL_TOOLS:
            pending |= observed_cli_usage(root, db, event, command, framework, identifier)
        return pending or bool(skills)
    if name == "Stop":
        message = string(event.get("last_assistant_message"), 30000)
        if turn["status"] == "observing":
            confirmed_hint = CONFIRMED_FRAMEWORK_FINDING.search(message)
            recorded = has_turn_finding(db, identifier)
            complete = bool(COMPLETE.search(message)) and not DEFERRED.search(message)
            route = ROUTE_BY_SKILL.get(turn["route"])
            candidate = (route and complete and turn["actual"] == VERIFIED_EXECUTION
                         and not turn["evidence"] and turn["version"] != "unknown"
                         and event.get("permission_mode") != "plan" and not event.get("stop_hook_active"))
            installed, package = resolved_framework(root, db) if candidate else (None, None)
            gap = bool(package and turn["version"] == installed and
                       (package / "Agents" / "Skills" / route[0] / "SKILL.md").is_file())
            if gap:
                enqueue(db, "issue", routing_issue(route, turn["version"], bool(DETOUR.search(message))),
                        event_id("missing-nova-route", identifier, route[0]))
                pending = True
                result = "confirmed"
            elif recorded:
                result = "confirmed"
            elif confirmed_hint or (route and not turn["evidence"]) or FRAMEWORK_FINDING.search(message):
                result = "pending-evidence"
            else:
                result = "reviewed"
            closed = db.execute("UPDATE turns SET status=?,updated=? WHERE id=? AND status='observing'",
                                (result, stamp, identifier)).rowcount
            if (closed and confirmed_hint and not gap and response is not None and not event.get("stop_hook_active")
                    and event.get("permission_mode") != "plan" and not recorded):
                # 结论文字只触发复核，包内证据仍由 finding 验证。
                response.update(decision="block", reason=(
                    "本轮结论提到已确认的 Nova 框架问题，请复核是否已提交全部有证据的问题。"
                    "已有上报编号时按编号查回执。漏项按当前用户授权和反馈状态调用 finding，"
                    f"将本轮标识 {identifier} 填入 turn。证据或授权不足时在收尾说明实际状态。"
                ))
    elif name == "Interrupt":
        db.execute("UPDATE turns SET status='interrupted',updated=? WHERE id=? AND status='observing'",
                   (stamp, identifier))
    return pending


def editor_snapshot(root, db, payload):
    """保留 Editor 观察证据，再按共享的环境上报规则处理。"""
    packages = []
    for item in payload.get("packages", [])[:350]:
        name = identity(item.get("id")) if isinstance(item, dict) else ""
        if name and not name.startswith("com.unity.modules."):
            packages.append({"id": name, "version": version(item.get("version")),
                             "source": package_type(item.get("source")), "evidence": "registered"})
    result = {"packages": packages, "at": now(), "enabled": [], "tools": {}}
    identifiers = payload.get("application_identifiers")
    if isinstance(identifiers, dict) and len(identifiers) <= 3:
        result["application_identifiers"] = {name: value for name, value in identifiers.items()
                                             if identity(name) and APPLICATION_IDENTIFIER.fullmatch(string(value, 255))}
    if payload.get("codex_hooks") in ("complete", "pending", "incomplete"):
        result["codex_hooks"] = payload["codex_hooks"]
    enabled = payload.get("enabled")
    for value in enabled[:100] if isinstance(enabled, list) else []:
        if identity(value):
            result["enabled"].append(value)
    for name, item in payload.get("tools", {}).items():
        if name in OBSERVED_TOOLS and isinstance(item, dict) and identity(item.get("state")):
            result["tools"][name] = {"state": item["state"], "version": version(item.get("version"))}
    save(db, "editor", result)
    report_editor_environment(root, db, compilation_day=payload.get("compilation_day"))


def editor_event(root, db, payload):
    """兼容旧 Editor 入口，编译只作为环境上报触发条件。"""
    kind = payload.get("event")
    if kind == "snapshot" and isinstance(payload.get("packages"), list) and isinstance(payload.get("tools"), dict):
        editor_snapshot(root, db, payload)
        return
    if kind == "compile-finished" and identity(payload.get("id")):
        report_editor_environment(root, db, compilation_day=datetime.fromtimestamp(now()).date().isoformat())


def inspect_pending(db):
    """结束长期未返回执行证据的 Codex 任务观察。"""
    db.execute("UPDATE turns SET status='pending-evidence',updated=? WHERE status='observing' AND started<?",
               (now(), now() - 3600))


def finding(root, db, payload):
    """核对包内证据锚点，登记已确认的问题并返回可查询的上报编号。"""
    framework, path = resolved_framework(root, db)
    anchor = payload.get("anchor")
    if not isinstance(anchor, dict) or path is None:
        return False
    relative = string(anchor.get("file"), 240)
    file = (path / relative).resolve() if relative else path
    line = anchor.get("line")
    match = string(anchor.get("text"), 300).strip()
    if path not in file.parents or not file.is_file() or file.suffix not in (".cs", ".md", ".json", ".py"):
        return False
    if not match or type(line) is not int or not 1 <= line <= 8000 or file.stat().st_size > 768 << 10:
        return False
    lines = file.read_text(encoding="utf-8-sig").splitlines()
    if line > len(lines) or match not in lines[line - 1]:
        return False
    code = identity(payload.get("code"))
    area = identity(payload.get("area"))
    occurrence = identity(payload.get("id"))
    if not code or not area or not occurrence or payload.get("confirmed") is not True:
        return False
    fields = (("title", 150), ("summary", 1400), ("expected", 1400), ("actual", 1400))
    issue = {name: text_field(payload.get(name), limit) for name, limit in fields}
    if not all(issue.values()):
        return False
    for name in ("workaround", "steps"):
        value = text_field(payload.get(name), 1400)
        if value:
            issue[name] = value
    issue.update({"code": code, "area": area, "framework": framework, "source": "local-agent",
                  "priority": payload.get("priority") if payload.get("priority") in ("P0", "P1", "P2", "P3") else "P2",
                  "evidence": [{"source": "framework.source",
                                "detail": f"{file.relative_to(path).as_posix()} 第 {line} 行已与本地证据核对。"}]})
    report_id = event_id("finding", occurrence, area, code)
    enqueue(db, "issue", issue, report_id)
    turn = identity(payload.get("turn"))
    if turn:
        db.execute("INSERT OR IGNORE INTO turn_findings(turn_id,report_id) SELECT id,? FROM turns WHERE id=?",
                   (report_id, turn))
        db.execute("UPDATE turns SET status='confirmed',updated=? WHERE id=? AND status='pending-evidence'",
                   (now(), turn))
    return report_id


def has_turn_finding(db, identifier):
    """核对本轮已登记的报告是否仍在待发队列或已有回执。"""
    return db.execute("""
        SELECT 1 FROM turn_findings AS f
        WHERE f.turn_id=? AND (
            EXISTS (SELECT 1 FROM outbox WHERE id=f.report_id) OR
            EXISTS (SELECT 1 FROM report_receipts WHERE id=f.report_id AND kind='issue')
        ) LIMIT 1
    """, (identifier,)).fetchone() is not None


def finding_receipt(db, report_id):
    """使用同一个数据库快照读取待发记录或服务端回执。"""
    row = db.execute("""
        SELECT accepted,issue_id,0 AS attempts,NULL AS error FROM report_receipts WHERE id=? AND kind='issue'
        UNION ALL
        SELECT accepted,issue_id,attempts,error FROM outbox WHERE id=? AND json_extract(payload,'$.kind')='issue'
        LIMIT 1
    """, (report_id, report_id)).fetchone()
    if row is None:
        return {"status": "unknown", "report_id": report_id}
    if row["accepted"] is not None:
        return {"status": "delivered", "report_id": report_id,
                "issue_id": row["issue_id"], "accepted": row["accepted"]}
    return {"status": "queued", "report_id": report_id, "attempts": row["attempts"], "error": row["error"]}


@lru_cache(maxsize=1)
def report_client():
    """只在发送进程中加载 HTTP 模块，并复用禁用代理与重定向的客户端。"""
    from urllib.request import build_opener, HTTPRedirectHandler, ProxyHandler

    class NoRedirect(HTTPRedirectHandler):
        """拒绝把固定内网地址请求转发到其他地址。"""

        def redirect_request(self, req, fp, code, msg, headers, newurl):
            """始终拒绝重定向请求。"""
            return None

    return build_opener(ProxyHandler({}), NoRedirect())


def automatic_report_health():
    """读取服务能力，旧版服务仍接收其支持的记录类型。"""
    try:
        with report_client().open(REPORT_SERVER + "/health", timeout=3) as reply:
            response = json.loads(reply.read(4096))
            if (isinstance(response, dict) and response.get("service") == "nova-issues-server"
                    and response.get("protocol") == 1 and response.get("automatic_reporting") is True):
                return response
    except (OSError, ValueError, RecursionError):
        pass
    return {}


def deliver(project, token, row):
    """提交已脱敏的报告，只有服务端确认原记录编号才算成功。"""
    from urllib.error import HTTPError
    from urllib.request import Request

    payload = compact({"name": project["name"], "report": json.loads(row["payload"])})
    request = Request(REPORT_SERVER + "/api/v1/auto-reports", data=payload.encode("utf-8"), method="POST",
                      headers={"Content-Type": "application/json", "X-Nova-Project": project["id"],
                               "Authorization": f"Bearer {token}"})
    try:
        with report_client().open(request, timeout=3) as reply:
            response = json.loads(reply.read(4096))
            if isinstance(response, dict) and response.get("accepted") is True and response.get("id") == row["id"]:
                return (response, "")
            return (None, "invalid-receipt")
    except HTTPError as exc:
        return (None, f"http-{exc.code}")
    except (OSError, ValueError, RecursionError):
        return (None, "transport")


def detached(args, folder):
    options = {"stdin": subprocess.DEVNULL, "stdout": subprocess.DEVNULL, "stderr": subprocess.DEVNULL,
               "cwd": folder}
    if os.name == "nt":
        options["creationflags"] = subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP
    else:
        options["start_new_session"] = True
    return subprocess.Popen(args, **options)


def wake(root, folder):
    """启动本地发送进程，已有运行中的发送者时由它继续处理。"""
    worker_id = uuid.uuid4().hex
    with ledger(folder) as db:
        db.execute("UPDATE state SET value=value WHERE key='workspace'")
        worker = state(db, "worker", {})
        if worker.get("lease", 0) > now():
            return
        save(db, "worker", {"id": worker_id, "lease": now() + 60})
    try:
        detached([sys.executable, "-B", str(Path(__file__).resolve()), "--project", str(root),
                  "worker", "--lease", worker_id], folder)
    except OSError:
        with ledger(folder) as db:
            if state(db, "worker", {}).get("id") == worker_id:
                save(db, "worker", {})


def reconcile_receipts(root, db):
    """恢复 Editor 执行记录，Codex 调用统计由调用钩子独立计数。"""
    directory = root / "Library/Nova/AgentActions/Operations"
    if not directory.is_dir():
        return
    framework = observed_framework(root, db)
    start = state(db, "enrolled_at", now())
    for file in directory.glob("*.json"):
        try:
            stamp = file.stat().st_mtime
        except OSError:
            continue
        if stamp < max(start, now() - 600):
            continue
        record = read_json(file, 256 << 10)
        if record.get("Status") not in ("executed", "verified"):
            continue
        operation = identity(record.get("OperationId"))
        action = identity(record.get("ActionId"))
        outcome = record.get("OutcomeStatus")
        if not operation or not action.startswith("nova.project.") or outcome not in ("success", "partial", "blocked", "failed", "cancelled"):
            continue
        try:
            observed = int(datetime.fromisoformat(record["UpdatedAtUtc"].replace("Z", "+00:00")).timestamp())
        except (ValueError, TypeError, KeyError):
            observed = int(stamp)
        enqueue(db, "usage", {"module": action.split(".")[2], "operation": action,
                              "outcome": outcome, "framework": framework, "source": "editor", "entry": "action"},
                event_id("action", action, operation), observed)


def probe_versions(root):
    versions = {}
    for name, command in (("codex", "codex"), ("unity-cli", "unity")):
        executable = shutil.which(command)
        result = run_readonly([executable, "--version"], root, timeout=2) if executable else None
        if result and result.returncode == 0:
            match = re.search(rb"\b(\d+\.\d+\.\d+(?:[a-zA-Z0-9.+-]*))\b", result.stdout[:240])
            if match:
                versions[name] = version(match[1].decode("ascii"))
    return versions


def has_pending_reports(db):
    """只检查当前策略允许发送的记录。"""
    return db.execute(f"""
        SELECT 1 FROM outbox WHERE accepted IS NULL AND {SENDABLE_REPORT_FILTER} LIMIT 1
        """).fetchone() is not None


def work(root, folder, worker_id, *, single=False):
    """按原上报编号和正文重试，确认后在同一事务内转为短回执。"""
    idle_since = now()
    next_maintenance = 0
    while True:
        if nova_project(root) != root or project_folder(root) != folder:
            return
        with ledger(folder) as db:
            if not single:
                if state(db, "worker", {}).get("id") != worker_id:
                    return
                save(db, "worker", {"id": worker_id, "lease": now() + 60})
            project, token = automatic_credentials(root, db)
            workspace = state(db, "workspace")
            maintenance = now() >= next_maintenance
            if maintenance:
                prune_delivered_reports(db)
                db.execute("DELETE FROM turns WHERE updated<?", (now() - RECEIPT_RETENTION_SECONDS,))
                next_maintenance = now() + 600
            inspect_pending(db)
            if now() - state(db, "last_reconciled", 0) >= 30:
                reconcile_receipts(root, db)
                save(db, "last_reconciled", now())
            probe = not single and now() - state(db, "version_probe_at", 0) >= 600
            ready = db.execute(f"""
                SELECT id,payload,attempts,json_extract(payload,'$.kind') AS kind,
                       json_extract(payload,'$.data.source') AS source,
                       COALESCE(json_type(payload,'$.data.codex_hooks'),
                                json_type(payload,'$.data.application_identifiers')) IS NOT NULL AS has_project_details,
                       json_type(payload,'$.data.git_user_name') IS NOT NULL AS has_git_user_name,
                       json_type(payload,'$.data.operating_system') IS NOT NULL AS has_operating_system,
                       (json_extract(payload,'$.data.stage')='unity-mcp-connection'
                        OR json_extract(payload,'$.data.entry')='unity-mcp') AS needs_unity_mcp_monitoring,
                       ((json_extract(payload,'$.kind')='issue' AND json_type(payload,'$.data.priority') IS NULL)
                        OR (json_extract(payload,'$.kind')='snapshot'
                            AND EXISTS (SELECT 1 FROM json_each(outbox.payload,'$.data.packages') AS package
                                        WHERE json_type(package.value,'$.evidence') IS NULL))) AS requires_compact_reports,
                       json_extract(payload,'$.data.stage')='editor-module' AS has_execution_evidence,
                       json_type(payload,'$.data.calls') IS NOT NULL AS has_play_call_counts,
                       (json_type(payload,'$.data.action_operation') IS NOT NULL
                        OR json_extract(payload,'$.data.stage')='table-ready'
                        OR (json_extract(payload,'$.data.stage')='editor-module'
                            AND json_extract(payload,'$.data.step') IN ('localization','ui','network'))
                        OR (json_extract(payload,'$.data.stage')='pipify-batch'
                            AND json_type(payload,'$.data.attempt') IS NOT NULL)) AS needs_feedback_timings
                FROM outbox WHERE accepted IS NULL AND ready_at<=?
                AND {SENDABLE_REPORT_FILTER}
                ORDER BY json_extract(payload,'$.kind')!='issue',ready_at,rowid LIMIT 8
                """, (now(),)).fetchall()
            pending = has_pending_reports(db)
        health = automatic_report_health() if ready else {}
        compact_reports = health.get(COMPACT_REPORTS_KEY) is True
        if health:
            with ledger(folder) as db:
                if state(db, "workspace") != workspace:
                    return
                if (state(db, COMPACT_REPORTS_KEY) is True) != compact_reports:
                    save(db, COMPACT_REPORTS_KEY, compact_reports)
        delivered = False
        for item in ready:
            if not health:
                received, error = None, "service-unavailable"
            elif item["kind"] == "hook_activity" and health.get("hook_activity") is not True:
                received, error = None, "server-upgrade-required"
            elif item["kind"] == "implementation" and health.get("implementation_tracking") is not True:
                received, error = None, "server-upgrade-required"
            elif item["has_execution_evidence"] and health.get("execution_evidence") is not True:
                received, error = None, "server-upgrade-required"
            elif item["has_play_call_counts"] and health.get("play_call_counts") is not True:
                received, error = None, "server-upgrade-required"
            elif item["needs_unity_mcp_monitoring"] and health.get("unity_mcp_monitoring") is not True:
                received, error = None, "server-upgrade-required"
            elif item["needs_feedback_timings"] and health.get("feedback_timings") is not True:
                received, error = None, "server-upgrade-required"
            elif item["kind"] == "usage" and item["source"] and health.get("codex_telemetry") is not True:
                received, error = None, "server-upgrade-required"
            elif item["has_project_details"] and health.get("project_snapshot_details") is not True:
                received, error = None, "server-upgrade-required"
            elif item["has_git_user_name"] and health.get("git_user_name_reporting") is not True:
                received, error = None, "server-upgrade-required"
            elif item["has_operating_system"] and health.get("snapshot_operating_system") is not True:
                received, error = None, "server-upgrade-required"
            elif item["requires_compact_reports"] and not compact_reports:
                received, error = None, "server-upgrade-required"
            else:
                received, error = deliver(project, token, item)
            if project_folder(root) != folder:
                return
            with ledger(folder) as db:
                if state(db, "workspace") != workspace:
                    return
                if received:
                    db.execute("UPDATE outbox SET accepted=?,issue_id=?,error=NULL WHERE id=?",
                               (now(), received.get("issue_id"), item["id"]))
                    delivered = True
                else:
                    delay = min(300, 2 ** min(item["attempts"] + 1, 9))
                    if error in ("http-400", "http-401", "http-403", "http-409"):
                        delay = 300
                    elif error == "server-upgrade-required":
                        delay = 30
                    db.execute("UPDATE outbox SET attempts=attempts+1,error=?,ready_at=? WHERE id=? AND accepted IS NULL",
                               (error, now() + delay, item["id"]))
        if maintenance or delivered:
            with ledger(folder) as db:
                reclaim_ledger_space(db)
        if probe:
            versions = probe_versions(root)
            with ledger(folder) as db:
                if state(db, "worker", {}).get("id") != worker_id:
                    return
                save(db, "cli_versions", versions)
                save(db, "version_probe_at", now())
        if single:
            return
        if ready or pending:
            idle_since = now()
        elif now() - idle_since >= 42:
            with ledger(folder) as db:
                db.execute("UPDATE state SET value=value WHERE key='workspace'")
                if state(db, "worker", {}).get("id") == worker_id:
                    if has_pending_reports(db):
                        idle_since = now()
                        continue
                    save(db, "worker", {})
            return
        time.sleep(1.5 if ready else 3)


def main():
    """使用已识别的 Nova 工程和内置地址，按触发入口处理本地证据。"""
    parser = argparse.ArgumentParser(description="Nova local issue agent")
    # 兼容已下发的旧钩子命令。上报目标由框架统一决定。
    parser.add_argument("--enrollment", type=Path, help=argparse.SUPPRESS)
    parser.add_argument("--project", type=Path)
    commands = parser.add_subparsers(dest="command", required=True)
    for command in ("hook", "mcp-usage", "editor", "finding", "flush"):
        commands.add_parser(command)
    commands.add_parser("status").add_argument("--report-id", help="check the local queue or receipt for an issue report")
    worker = commands.add_parser("worker")
    worker.add_argument("--lease", required=True)
    args = parser.parse_args()
    report_id = getattr(args, "report_id", None)
    if report_id is not None and not re.fullmatch(r"[0-9a-f]{32}", report_id):
        parser.error("--report-id must be a 32-character hexadecimal report ID")
    mcp_usage = args.command == "mcp-usage"
    hook = args.command == "hook" or mcp_usage
    hook_response = {}
    try:
        raw = sys.stdin.buffer.read(MAX_INPUT + 1) if hook or args.command in ("editor", "finding") else b"{}"
        if len(raw) > MAX_INPUT:
            return 0 if hook else 1
        payload = json.loads(raw or b"{}")
        if not isinstance(payload, dict):
            return 0 if hook else 1
        if mcp_usage:
            operation = unity_mcp_operation(payload)
            identifier = turn_key(payload)
            invocation = string(payload.get("tool_use_id"), 160)
            if not (operation and identifier and invocation):
                return 0
        elif hook and not relevant_hook(payload):
            return 0
        workspace = payload.get("cwd") if hook else args.project
        if not workspace or not Path(workspace).is_absolute():
            return 2 if args.command == "finding" else 0
        root = nova_project(Path(workspace).resolve())
        if not root:
            return 2 if args.command == "finding" else 0
        folder = project_folder(root)
        if not folder:
            return 0 if hook else 1
        if args.command in ("worker", "flush"):
            work(root, folder, getattr(args, "lease", ""), single=args.command == "flush")
            return 0
        should_wake = False
        report = None
        with ledger(folder) as db:
            if state(db, "enrolled_at") is None:
                save(db, "enrolled_at", now())
            if mcp_usage:
                record_usage(db, identifier, observed_framework(root, db), "unity-mcp", operation, "unity-mcp",
                             unity_mcp_outcome(payload.get("tool_response")) or "executed", invocation)
                should_wake = True
            elif hook:
                should_wake = handle_hook(root, db, payload, hook_response)
            elif args.command == "editor":
                source = string(payload.get("framework_root"), 2000)
                if source and Path(source).is_absolute():
                    save(db, "editor_source", source)
                editor_event(root, db, payload)
                should_wake = True
            elif args.command == "finding":
                report_id = finding(root, db, payload)
                if not report_id:
                    print(compact({"status": "rejected", "reason": "invalid-finding"}))
                    return 2
                report = finding_receipt(db, report_id)
                should_wake = report["status"] == "queued"
            elif report_id is not None:
                report = finding_receipt(db, report_id)
            elif args.command == "status":
                print(compact({"turns": [dict(row) for row in db.execute(
                    "SELECT id,route,status,updated FROM turns ORDER BY updated DESC LIMIT 30")],
                    "outbox": [dict(row) for row in db.execute(
                        "SELECT id,json_extract(payload,'$.kind') AS kind,attempts,accepted,issue_id,error "
                        "FROM outbox WHERE accepted IS NULL ORDER BY rowid DESC LIMIT 30")],
                    "receipts": [dict(row) for row in db.execute(
                        "SELECT id,kind,accepted,issue_id FROM report_receipts "
                        "UNION ALL SELECT id,json_extract(payload,'$.kind'),accepted,issue_id "
                        "FROM outbox WHERE accepted IS NOT NULL ORDER BY accepted DESC LIMIT 30")]}))
            if should_wake and state(db, "worker", {}).get("lease", 0) > now():
                should_wake = False
        if should_wake:
            wake(root, folder)
        if report is not None:
            print(compact(report))
        return 0
    except (OSError, ValueError, TypeError, sqlite3.Error, RecursionError):
        return 0 if hook else 1
    finally:
        if hook:
            print(compact(hook_response))


if __name__ == "__main__":
    sys.exit(main())
