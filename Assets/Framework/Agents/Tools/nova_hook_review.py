#!/usr/bin/env python3
"""Read the native Codex review state of this project's Nova feedback hooks."""

import argparse
from contextlib import closing
from datetime import date
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import sqlite3
import subprocess
import sys
import tempfile
from threading import Timer

from nova_issues_agent import FRAMEWORK, WORKDIR, compact, event_id, nova_project, project_folder, read_json, version


HOOK_CONFIG = Path(".codex/hooks.json")
REVIEW_DB = "hook-review.sqlite3"
EVENTS = {
    "SessionStart": "sessionStart",
    "UserPromptSubmit": "userPromptSubmit",
    "PostToolUse": "postToolUse",
    "Stop": "stop",
    "Interrupt": "interrupt",
}
AGENT_NAME = re.compile(r"nova_issues_agent-[a-f0-9]{64}\.py\Z")
TIMEOUT_SECONDS = 3


def feedback_commands(handler, folder):
    if not isinstance(handler, dict) or handler.get("type") != "command":
        return set()
    command = handler.get("command")
    if not isinstance(command, str) or not command.startswith("python3 -B "):
        return set()
    try:
        words = shlex.split(command)
    except ValueError:
        return set()
    if len(words) != 4 or words[:2] != ["python3", "-B"] or words[3] != "hook":
        return set()
    script = Path(words[2])
    if not script.is_absolute() or not AGENT_NAME.fullmatch(script.name) or script.resolve().parent != folder:
        return set()

    commands = {command}
    # Match the PowerShell variant emitted by EditorUtil.NovaIssues.CodexHooks.cs.
    quoted = "'" + words[2].replace("'", "''") + "'"
    windows = (f"if (Get-Command py -ErrorAction SilentlyContinue) {{ py -3 -B {quoted} hook }} "
               f"else {{ python -B {quoted} hook }}")
    if handler.get("commandWindows") == windows:
        commands.add(windows)
    return commands


def feedback_definitions(root):
    path = root / HOOK_CONFIG
    if path.is_symlink() or path.parent.is_symlink():
        raise ValueError("unsafe Codex hook path")
    if not path.exists():
        return {}
    if not path.is_file():
        raise ValueError("invalid Codex hook path")
    with path.open("rb") as source:
        raw = source.read((64 << 10) + 1)
    if len(raw) > (64 << 10):
        raise ValueError("Codex hook file is too large")
    config = json.loads(raw)
    if not isinstance(config, dict) or not isinstance(config.get("hooks", {}), dict):
        raise ValueError("invalid Codex hook file")
    definitions = {}
    for event, native_event in EVENTS.items():
        groups = config.get("hooks", {}).get(event, [])
        if not isinstance(groups, list):
            continue
        for group in groups:
            handlers = group.get("hooks") if isinstance(group, dict) else None
            if not isinstance(handlers, list):
                continue
            for handler in handlers:
                commands = feedback_commands(handler, root / WORKDIR)
                if commands:
                    definitions.setdefault(native_event, []).append(commands)
    return definitions


def codex_binary(root):
    name = "codex.exe" if os.name == "nt" else "codex"
    installed = os.environ.get("CODEX_INSTALL_DIR")
    candidates = [shutil.which(name), Path(installed) / name if installed else None,
                  Path.home() / ".local/bin" / name]
    if sys.platform == "darwin":
        candidates.extend((
            Path("/Applications/ChatGPT.app/Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex"),
            Path("/Applications/Codex.app/Contents/Resources/codex"),
        ))
    elif os.name == "nt" and os.environ.get("LOCALAPPDATA"):
        candidates.append(Path(os.environ["LOCALAPPDATA"]) / "Programs/OpenAI/Codex/bin" / name)
    for candidate in candidates:
        if candidate and Path(candidate).is_absolute():
            path = Path(candidate).resolve()
            if root not in path.parents and path.is_file() and os.access(path, os.X_OK):
                return str(path)
    return None


def rpc(process, method, params=None, request_id=None):
    message = {"method": method}
    if request_id is not None:
        message.update(id=request_id, params=params)
    process.stdin.write((compact(message) + "\n").encode("utf-8"))
    process.stdin.flush()
    if request_id is None:
        return None

    limit = 2 << 20
    while line := process.stdout.readline(limit + 1):
        if len(line) > limit:
            break
        response = json.loads(line)
        if isinstance(response, dict) and response.get("id") == request_id:
            result = response.get("result")
            if isinstance(result, dict):
                return result
            break
    raise ValueError("Codex did not return the requested review information")


def native_hooks(root, codex, folder):
    # Use the user's actual Codex profile. Keep probe state and logs in the ignored Library.
    command = [codex, "app-server", "--listen", "stdio://", "--disable", "plugins",
               "-c", "analytics.enabled=false", "-c", "sqlite_home=" + json.dumps(folder, ensure_ascii=False),
               "-c", "log_dir=" + json.dumps(str(Path(folder) / "log"), ensure_ascii=False)]
    with subprocess.Popen(command, cwd=root, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                          stderr=subprocess.DEVNULL) as process:
        deadline = Timer(TIMEOUT_SECONDS, process.kill)
        deadline.start()
        try:
            rpc(process, "initialize", {"clientInfo": {"name": "nova-hook-review", "version": "1"},
                                        "capabilities": {"experimentalApi": True}}, 1)
            rpc(process, "initialized")
            result = rpc(process, "hooks/list", {"cwds": [str(root)]}, 2)
            workspace = next((row for row in result["data"] if Path(row["cwd"]).resolve() == root), None)
            if workspace is None:
                raise ValueError("Codex did not return the requested workspace")
            hooks = workspace["hooks"]
            if not hooks:
                features = rpc(process, "experimentalFeature/list", {"limit": 500}, 3)
                if any(feature.get("name") == "hooks" and feature.get("enabled") is False
                       for feature in features["data"]):
                    return None  # The host confirmed that all hooks are switched off.
        finally:
            try:
                process.stdin.close()
                process.wait(timeout=0.3)
            except subprocess.TimeoutExpired:
                process.kill()
            except BrokenPipeError:
                process.kill()
            finally:
                deadline.cancel()
    config = root / HOOK_CONFIG
    return [row for row in hooks if row.get("source") == "project"
            and Path(row["sourcePath"]).resolve() == config]


def review_status(state, pending=0, **details):
    return {"status": state, "pending": pending, **details}


def reminder_deferred(folder, scope, *, record):
    """只保存成员明确暂缓的当天决定，旧版本的首次检查记录继续保留。"""
    database = folder / REVIEW_DB
    for suffix in ("", "-journal", "-wal", "-shm"):
        path = folder / (REVIEW_DB + suffix)
        if path.is_symlink() or (path.exists() and not path.is_file()):
            raise OSError("unsafe hook review storage")

    if not record and not database.is_file():
        return False

    with closing(sqlite3.connect(database, isolation_level=None, timeout=0.5)) as db:
        db.execute("""CREATE TABLE IF NOT EXISTS deferred_reminders (
            profile TEXT NOT NULL, version TEXT NOT NULL, local_day TEXT NOT NULL,
            PRIMARY KEY (profile, version, local_day)
        )""")
        if record:
            db.execute("INSERT OR IGNORE INTO deferred_reminders VALUES (?, ?, ?)", scope)
            return True
        return db.execute("""SELECT 1 FROM deferred_reminders
            WHERE profile=? AND version=? AND local_day=?""", scope).fetchone() is not None


def task_review(result, folder, *, defer_token=None):
    """阻塞待审查或停用的任务，只接受本工作区当天、当前版本的暂缓决定。"""
    if not result["pending"] and not result.get("disabled"):
        return result
    metadata = read_json(Path(__file__).resolve().parents[2] / "package.json", 96 << 10)
    framework_version = version(metadata.get("version"))
    if metadata.get("name") != FRAMEWORK or framework_version == "unknown":
        return dict(result, status="blocked", reason="framework_version_unavailable")
    local_day = date.today().isoformat()
    configured_home = os.environ.get("CODEX_HOME")
    codex_home = Path(configured_home) if configured_home else Path.home() / ".codex"
    profile = event_id(os.path.normcase(str(codex_home.resolve())))
    scope = (profile, framework_version, local_day)
    token = event_id(str(folder.resolve()), *scope)
    reminder = dict(result, status="blocked", reason="feedback_reminder", frameworkVersion=framework_version,
                    localDay=local_day, reminderToken=token)
    deferring = defer_token == token
    try:
        if reminder_deferred(folder, scope, record=deferring):
            return dict(result, status="ready", reason="feedback_deferred")
    except (OSError, sqlite3.Error):
        return dict(reminder, status="ready" if deferring else "blocked", reason="reminder_unavailable")
    return reminder


def review(project, *, task_start=False, defer_token=None):
    root = nova_project(project.resolve())
    if root is None:
        return review_status("not_applicable")
    definitions = feedback_definitions(root)
    if not definitions:
        return review_status("not_applicable")
    codex = codex_binary(root)
    if not codex:
        return review_status("unknown", reason="codex_unavailable")
    folder = project_folder(root)
    if folder is None:
        return review_status("unknown", reason="library_unavailable")

    with tempfile.TemporaryDirectory(prefix="hook-review-", dir=folder) as scratch:
        hooks = native_hooks(root, codex, scratch)
    result = classify_hooks(hooks, definitions)
    if (task_start or defer_token is not None) and result["status"] in ("ready", "blocked"):
        return task_review(result, folder, defer_token=defer_token)
    return result


def classify_hooks(hooks, definitions):
    """按宿主的实际授权判定，区分已授权但停用和停用时仍未授权的条目。"""
    if hooks is None:
        return review_status("ready", disabled=sum(map(len, definitions.values())), unapproved=None)
    remaining = {event: groups.copy() for event, groups in definitions.items()}
    pending = 0
    disabled = 0
    unapproved = 0
    for row in hooks:
        event = row.get("eventName")
        groups = remaining.get(event) if isinstance(event, str) else None
        if not groups:
            continue
        command = row.get("command")
        index = next((i for i, commands in enumerate(groups) if command in commands), None)
        if index is None:
            continue

        trust = row.get("trustStatus")
        enabled = row.get("enabled")
        needs_trust = trust in ("untrusted", "modified") and row.get("isManaged") is False
        if enabled is False:
            disabled += 1
            unapproved += int(needs_trust)
        elif enabled is True and trust in ("trusted", "managed"):
            pass
        elif enabled is True and needs_trust:
            pending += 1
            unapproved += 1
        else:
            continue
        groups.pop(index)
    if pending:
        return review_status("blocked", pending, disabled=disabled, unapproved=unapproved)
    if not any(remaining.values()):
        return review_status("ready", disabled=disabled, unapproved=unapproved)
    return review_status("unknown", reason="not_discovered")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--task-start", action="store_true", help="check for pending or disabled hooks before continuing the task")
    mode.add_argument("--defer-reminder", metavar="TOKEN", help="record the member's decision to defer today's reminder")
    args = parser.parse_args()
    try:
        result = review(args.project, task_start=args.task_start, defer_token=args.defer_reminder)
    except (OSError, ValueError, KeyError, TypeError, RuntimeError):
        result = review_status("unknown", reason="probe_failed")
    print(compact(result))


if __name__ == "__main__":
    main()
