"""Focused integration tests for the issue service and its local agent."""

import concurrent.futures
from datetime import datetime, timedelta
import http.client
import importlib.util
import json
import os
from pathlib import Path
import secrets
import sqlite3
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from unittest.mock import patch
import uuid

from database import Database
from serve import ACCOUNTS, NovaIssuesServer, backup, password_hash


AGENT_FILE = Path(__file__).resolve().parents[2] / "Assets/Framework/Agents/Tools/nova_issues_agent.py"
SPEC = importlib.util.spec_from_file_location("nova_issues_agent_under_test", AGENT_FILE)
agent = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(agent)


def git(root, *arguments):
    return subprocess.check_output(["git", "--no-optional-locks", *arguments], cwd=root, stderr=subprocess.DEVNULL)


class IssuesTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.passwords = {user: secrets.token_urlsafe(32) for user in ACCOUNTS}
        cls.accounts = {}
        for user, password in cls.passwords.items():
            salt = secrets.token_bytes(16)
            cls.accounts[user] = (salt, password_hash(password, salt))

    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.base = Path(folder.name).resolve()
        self.database = Database(self.base / "service.sqlite3")
        self.server = NovaIssuesServer(("127.0.0.1", 0), self.database, self.accounts)
        thread = threading.Thread(target=self.server.serve_forever, kwargs={"poll_interval": 0.02}, daemon=True)
        thread.start()
        self.addCleanup(thread.join)
        self.addCleanup(self.server.server_close)
        self.addCleanup(self.server.shutdown)
        self.url = f"http://127.0.0.1:{self.server.server_port}"
        self.project_token = self.database.register("project-a", "项目甲", "QA")

    def http(self, path, payload=None, headers=None):
        conn = http.client.HTTPConnection("127.0.0.1", self.server.server_port, timeout=5)
        try:
            values = headers.copy() if headers else {}
            if payload is not None:
                values.setdefault("Content-Type", "application/json")
            conn.request("GET" if payload is None else "POST", path,
                         body=None if payload is None else json.dumps(payload), headers=values)
            reply = conn.getresponse()
            content = reply.read()
            if reply.getheader("Content-Type", "").startswith("application/json"):
                content = json.loads(content)
            return reply.status, content, dict(reply.getheaders())
        finally:
            conn.close()

    def login(self, name="taoye"):
        status, body, headers = self.http("/api/session", {"username": name, "password": self.passwords[name]},
                                          {"Origin": self.url})
        self.assertEqual(status, 200)
        self.assertIn("HttpOnly", headers["Set-Cookie"])
        return {"Cookie": headers["Set-Cookie"].split(";")[0], "X-Nova-CSRF": body["csrf"], "Origin": self.url}

    def ingest(self, event, project="project-a", token=None):
        return self.http("/api/v1/reports", event,
                         {"X-Nova-Project": project, "Authorization": "Bearer " + (token or self.project_token)})

    def sample_issue(self, code="sdk-case"):
        return {"version": 1, "id": uuid.uuid4().hex, "workspace": uuid.uuid4().hex,
                "observed_at": int(time.time()), "kind": "issue",
                "data": {"code": code, "area": "sdk", "source": "editor", "framework": "0.7.13",
                         "title": "启动阶段未完成", "summary": "两个 Editor 观察点记录到同一阶段。",
                         "expected": "插件应返回终态。", "actual": "同一次初始化仍在等待。",
                         "priority": "P1", "evidence": [{"source": "editor.preload", "detail": "阶段状态仍在等待。"}]}}

    def fixture_project(self):
        root = self.base / "Project"
        (root / "Packages").mkdir(parents=True)
        (root / "ProjectSettings").mkdir()
        (root / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.4.2f1\n")
        package = root / "Packages" / agent.FRAMEWORK
        package.mkdir()
        (package / "package.json").write_text(json.dumps({"name": agent.FRAMEWORK, "version": "0.7.13"}))
        skill = package / "Agents/Skills/nova-project-configure-runtime"
        skill.mkdir(parents=True)
        (skill / "SKILL.md").write_text("# 配置运行时配置\n", encoding="utf-8")
        (root / "Packages/manifest.json").write_text(json.dumps({"dependencies": {agent.FRAMEWORK: "0.7.13"}}))
        (root / ".gitignore").write_text("/Library/\n")
        git(root, "init", "-q")
        (root / "ProjectSettings/ProjectSettings.asset").write_text(
            "PlayerSettings:\n  productGUID: 1234567890abcdef1234567890abcdef\n  productName: Fixture Project\n")
        folder = agent.project_folder(root)
        return root, folder

    def event(self, name, turn="test-turn", **fields):
        return {"hook_event_name": name, "session_id": "test-session", "turn_id": turn} | fields

    def test_two_accounts_and_protected_routes(self):
        status, html, _ = self.http("/")
        self.assertEqual(status, 200)
        self.assertIn(b'id="login-form"', html)
        self.assertEqual(self.http("/api/maintainer/overview")[0], 401)
        self.assertEqual(self.http("/api/register", {"username": "other"})[0], 404)
        self.assertEqual(self.http("/api/session", {"username": "other", "password": "incorrect"},
                                   {"Origin": self.url})[0], 401)
        for username in ACCOUNTS:
            session = self.login(username)
            self.assertEqual(self.http("/api/maintainer/overview", headers=session)[0], 200)
            self.assertEqual(self.http("/api/session/logout", {}, session | {"X-Nova-CSRF": "missing"})[0], 403)
            self.assertEqual(self.http("/api/session/logout", {}, session)[0], 200)
            self.assertEqual(self.http("/api/maintainer/overview", headers=session)[0], 401)

    def test_idempotent_delivery_concurrent_projects_and_backup(self):
        event = self.sample_issue()
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            results = list(pool.map(self.ingest, [event] * 4))
        self.assertEqual(sorted(item[0] for item in results), [200, 200, 200, 201])
        self.assertEqual(len({item[1]["issue_id"] for item in results}), 1)
        issue = results[0][1]["issue_id"]
        self.assertEqual(self.ingest(event | {"observed_at": event["observed_at"] - 1})[0], 409)
        second = self.database.register("project-b", "项目乙", "QA")
        self.assertEqual(self.ingest(self.sample_issue(), "project-b")[0], 401)
        self.assertEqual(self.ingest(self.sample_issue(), "project-b", second)[1]["issue_id"], issue)
        session = self.login()
        record = self.http(f"/api/maintainer/issues/{issue}", headers=session)[1]
        self.assertEqual(len(record["occurrences"]), 2)
        edit = {"revision": 1, "status": "awaiting_verification", "priority": "P1", "assignee": "维护组",
                "fixed_version": "0.7.14", "note": "等待项目组在指定版本复验。"}
        self.assertEqual(self.http(f"/api/maintainer/issues/{issue}", edit, session)[0], 200)
        self.assertEqual(self.http(f"/api/maintainer/issues/{issue}", edit, session)[0], 409)
        copied = self.base / "backup.sqlite3"
        backup(self.database.path, copied)
        with sqlite3.connect(copied) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM reports").fetchone()[0], 2)

    def test_issue_project_filter_combines_with_search_and_finds_older_issues(self):
        second = self.database.register("project-b", "项目乙", "QA")
        self.database.register("project-empty", "尚未上报", "QA")
        shared = self.ingest(self.sample_issue("sdk-shared"))[1]["issue_id"]
        self.assertEqual(self.ingest(self.sample_issue("sdk-shared"), "project-b", second)[1]["issue_id"], shared)
        only_a = self.ingest(self.sample_issue("sdk-project-a"))[1]["issue_id"]
        only_b = self.ingest(self.sample_issue("sdk-project-b"), "project-b", second)[1]["issue_id"]

        # 让优先级更高的问题占满总览列表，验证先筛选再取结果。
        stamp = int(time.time())
        with self.database.connect() as db:
            db.executemany("""
                INSERT INTO issues (fingerprint,code,area,title,priority,first_seen,last_seen)
                VALUES (?, ?, 'sdk', '启动阻塞', 'P0', ?, ?)
                """, ((f"blocking-{i}", f"blocking-{i}", stamp, stamp) for i in range(502)))
            db.executemany("""
                INSERT INTO issues (fingerprint,code,area,title,priority,status,first_seen,last_seen)
                VALUES (?, ?, 'sdk', '启动阻塞', 'P0', ?, ?, ?)
                """, ((status, status, status, stamp, stamp) for status in ("closed", "duplicate")))

        route = "/api/maintainer/issues"
        self.assertEqual(self.http(f"{route}?project=project-a")[0], 401)
        session = self.login()
        overview = self.http("/api/maintainer/overview", headers=session)[1]
        self.assertEqual(len(overview["issues"]), 500)
        self.assertEqual(overview["issue_counts"], {"active": 505, "p0": 502})
        self.assertNotIn(shared, {item["id"] for item in overview["issues"]})

        def issues_for(query):
            status, payload, _ = self.http(f"{route}?{query}", headers=session)
            self.assertEqual(status, 200, payload)
            return payload["issues"]

        self.assertEqual({item["id"] for item in issues_for("project=project-a")}, {shared, only_a})
        self.assertEqual({item["id"] for item in issues_for("project=project-b")}, {shared, only_b})
        self.assertEqual(issues_for("project=project-empty"), [])
        query = "project=project-a&status=open&priority=P1"
        matches = issues_for(f"{query}&q=SDK-SHARED")
        self.assertEqual([(item["id"], item["projects"], item["occurrences"]) for item in matches], [(shared, 2, 2)])
        self.assertEqual(issues_for(f"{query}&q=sdk-project-b"), [])
        self.assertEqual(issues_for("project=project-a&status=closed&priority=P1"), [])

    def test_prompt_missed_route_is_local_then_reported(self):
        root, folder = self.fixture_project()
        before = git(root, "status", "--porcelain", "--untracked-files=all")
        with agent.ledger(folder) as db:
            agent.save(db, "destination", {"project": "legacy-project", "server": "http://127.0.0.1:8787"})
            agent.handle_hook(root, db, self.event("UserPromptSubmit", prompt="帮我导出运行时配置。示例秘密 token=fixture-value"))
            execution = self.event("PostToolUse", tool_name="exec_command", tool_use_id="invoke",
                tool_input={"cmd": "python3 project_export.py"},
                tool_response="Chunk ID: fixture\nWall time: 0.02 seconds\nProcess exited with code 0\nFinal output:\n已导出。")
            self.assertTrue(agent.relevant_hook(execution))
            agent.handle_hook(root, db, execution)
            agent.handle_hook(root, db, self.event("Stop", last_assistant_message="已通过临时绕行完成。"))
            self.assertEqual(db.execute("SELECT status FROM turns").fetchone()[0], "confirmed")
            event = db.execute("SELECT payload FROM outbox WHERE json_extract(payload,'$.kind')='issue'").fetchone()[0]
            self.assertNotIn("fixture-value", event)
            self.assertNotIn(str(root), event)
        with patch.object(agent, "REPORT_SERVER", self.url), patch.object(agent, "deliver", return_value=(None, "transport")):
            agent.work(root, folder, "", single=True)
        with agent.ledger(folder) as db:
            self.assertGreater(db.execute("SELECT COUNT(*) FROM outbox WHERE accepted IS NULL").fetchone()[0], 0)
            db.execute("UPDATE outbox SET ready_at=0")
        with patch.object(agent, "REPORT_SERVER", self.url):
            agent.work(root, folder, "", single=True)
        with agent.ledger(folder) as db:
            self.assertEqual(agent.state(db, "destination")["server"], self.url)
        detail = self.database.overview(14)
        self.assertEqual(detail["issues"][0]["code"], "missing-nova-route.config")
        self.assertEqual(detail["issues"][0]["occurrences"], 1)
        self.assertEqual(git(root, "status", "--porcelain", "--untracked-files=all"), before)
        self.assertEqual(git(root, "ls-files", "--", "Library"), b"")

    def test_completed_language_without_verified_execution_stays_pending(self):
        root, folder = self.fixture_project()
        cases = (
            ("answer", "", None),
            ("failed-export", "python3 project_export.py", {"exit_code": 1}),
            ("missing-result", "python3 project_export.py", None),
            ("help", "python3 project_export.py --help", {"exit_code": 0}),
            ("editor-status", "unity command editor status", {"exit_code": 0}),
            ("dotnet-inspection", "dotnet list ProjectExport.csproj package", {"exit_code": 0}),
            ("output-text", "python3 project_export.py",
             "Chunk ID: fixture\nWall time: 0.02 seconds\nOutput:\nProcess exited with code 0\n"),
            ("legacy-hint", "", None),
        )
        with agent.ledger(folder) as db:
            for turn, command, result in cases:
                with self.subTest(turn=turn):
                    prompt = self.event("UserPromptSubmit", turn, prompt="请列一下导出运行时配置的步骤。")
                    agent.handle_hook(root, db, prompt)
                    if command:
                        agent.handle_hook(root, db, self.event("PostToolUse", turn, tool_name="Bash",
                            tool_use_id=turn, tool_input={"command": command}, tool_response=result))
                    if turn == "legacy-hint":
                        db.execute("UPDATE turns SET actual=1 WHERE id=?", (agent.turn_key(prompt),))
                    pending = agent.handle_hook(root, db, self.event(
                        "Stop", turn, last_assistant_message="已生成的文件可以在输出目录中核对。"))
                    self.assertFalse(pending)
                    status = db.execute("SELECT status FROM turns WHERE id=?", (agent.turn_key(prompt),)).fetchone()[0]
                    self.assertEqual(status, "pending-evidence")
            self.assertEqual(db.execute("SELECT COUNT(*) FROM outbox WHERE json_extract(payload,'$.kind')='issue'").fetchone()[0], 0)

    def test_routed_tasks_blocked_tasks_and_pure_business_are_not_issues(self):
        root, folder = self.fixture_project()
        with agent.ledger(folder) as db:
            agent.handle_hook(root, db, self.event("UserPromptSubmit", prompt="导出运行时配置"))
            agent.handle_hook(root, db, self.event("PostToolUse",
                tool_name="mcp__nova__nova_project_action", tool_use_id="call-1",
                tool_input={"action_id": "nova.project.config.export-runtime", "operation": "execute"},
                tool_response={"status": "success", "recovery_token": "nova-action-operation-v1:operation-1"}))
            agent.handle_hook(root, db, self.event("Stop", last_assistant_message="已完成导出。"))
            agent.handle_hook(root, db, self.event("UserPromptSubmit", "blocked", prompt="导出运行时配置"))
            agent.handle_hook(root, db, self.event("Stop", "blocked", last_assistant_message="需要你选择平台。"))
            agent.handle_hook(root, db, self.event("UserPromptSubmit", "ordinary", prompt="加一个游戏关卡。"))
            agent.handle_hook(root, db, self.event("Stop", "ordinary", last_assistant_message="已完成。"))
            self.assertEqual(db.execute("SELECT COUNT(*) FROM outbox WHERE json_extract(payload,'$.kind')='issue'").fetchone()[0], 0)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM outbox WHERE json_extract(payload,'$.kind')='usage'").fetchone()[0], 1)

    def test_codex_keeps_turns_local_and_reports_module_counts(self):
        """普通任务留在本地，真实模块调用继续去重并同步到维护台。"""
        root, folder = self.fixture_project()
        package = root / "Packages" / agent.FRAMEWORK
        (root / "Packages/manifest.json").write_text('{"dependencies":{}}')
        self.assertEqual(agent.nova_project(root), root)
        skill = package / "Agents/Skills/nova-project-integrate-event"
        (skill / "references").mkdir(parents=True)
        (skill / "SKILL.md").touch()
        (skill / "references/contract.json").write_text(json.dumps({
            "id": skill.name, "actionAdapters": [{"kind": "csharp-api", "entry": "Nova.Event.Subscribe<T>"}],
        }))
        cli = package / "Agents/Tools/nova_table_source.py"
        cli.parent.mkdir(parents=True)
        cli.touch()
        prompt = self.event("UserPromptSubmit", prompt="改一个业务页面 fixture-private-prompt")
        action = {"action_id": "nova.project.table.export", "operation": "execute"}
        execute = self.event("PostToolUse", tool_name="mcp__nova__nova_project_action", tool_use_id="execute-1",
                             tool_input=action, tool_response={"content": [{"text": '{"status":"success"}'}]})
        read_skill = self.event("PostToolUse", tool_name="Bash", tool_use_id="skill-1",
                                tool_input={"command": f'cat "{skill / "SKILL.md"}"'})
        cli_tool = self.event("PostToolUse", tool_name="Bash", tool_use_id="cli-1", tool_input={
            "command": f'python3 -B "{cli}" --project-root "{root}" execute --confirmation-token fixture-private-arg',
        }, tool_response={"exit_code": 1})
        with agent.ledger(folder) as db, patch.object(agent, "snapshot", side_effect=AssertionError("hook rebuilt inventory")):
            self.assertFalse(agent.handle_hook(root, db, self.event("SessionStart")))
            for event in (prompt, prompt, execute, execute, read_skill,
                          read_skill | {"tool_use_id": "skill-2"}, cli_tool):
                self.assertTrue(agent.relevant_hook(event))
                agent.handle_hook(root, db, event)
            agent.handle_hook(root, db, self.event("PostToolUse", tool_name="mcp__nova__execute_custom_tool",
                tool_use_id="execute-2", tool_input={"tool_name": "nova_project_action", "parameters": action},
                tool_response={"isError": True}))
            agent.handle_hook(root, db, execute | {"tool_use_id": "plan", "tool_input": action | {"operation": "plan"}})
            agent.handle_hook(root, db, self.event("PostToolUse", tool_name="Bash", tool_use_id="unity-1",
                tool_input={"command": "unity command script recompile"}))
            agent.handle_hook(root, db, self.event("Stop", session_id="another-codex-session"))
            agent.handle_hook(root, db, self.event("Stop"))
            reports = [json.loads(row["payload"]) for row in db.execute("SELECT payload FROM outbox")]
            self.assertEqual(db.execute("SELECT COUNT(*) FROM turns").fetchone()[0], 2)
            self.assertEqual([item["kind"] for item in reports], ["usage"] * 5)
            self.assertNotIn("fixture-private", json.dumps(reports))
            self.assertNotIn(str(root), json.dumps(reports))
            # 升级前已排队的任务基础信息不再发送。
            agent.enqueue(db, "task", {"framework": "0.7.13", "unity": "6000.4.2f1"})
        self.assertFalse(agent.relevant_hook(self.event("PostToolUse", tool_name="mcp__other__read")))

        with patch.object(agent, "REPORT_SERVER", self.url), patch.object(agent, "automatic_report_health", return_value={
            "automatic_reporting": True,
        }):
            agent.work(root, folder, "", single=True)
        with agent.ledger(folder) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM outbox WHERE error='server-upgrade-required'").fetchone()[0], 5)
            db.execute("UPDATE outbox SET ready_at=0")
        with patch.object(agent, "REPORT_SERVER", self.url):
            agent.work(root, folder, "", single=True)
        session = self.login()
        overview = self.http("/api/maintainer/overview?days=1", headers=session)[1]
        self.assertEqual(overview["codex_activity"], [])
        usage = {item["module"]: item for item in overview["codex_modules"]}
        self.assertEqual((usage["table"]["calls"], usage["table"]["problems"]), (3, 2))
        self.assertEqual((usage["event"]["calls"], usage["event"]["skill_turns"]), (0, 1))
        self.assertEqual(usage["unity-cli"]["calls"], 1)
        project = overview["codex_modules"][0]["project_id"]
        detail = self.http(f"/api/maintainer/projects/{project}", headers=session)[1]
        self.assertEqual(detail["tasks"], [])
        self.assertEqual(detail["snapshots"], [])

    def test_editor_environment_reports_once_per_local_compile_day_or_changed_versions(self):
        """跨日但未编译时保留本地观察，版本变化和首次编译进入可重试队列。"""
        root, folder = self.fixture_project()
        started = datetime.now().replace(hour=10, minute=0, second=0, microsecond=0) - timedelta(days=3)
        observed = {"event": "snapshot", "packages": [], "tools": {}, "enabled": []}
        compile_finished = {"event": "compile-finished", "id": "compile-1"}

        def observe(offset, event, expected):
            """重新打开队列验证日期和去重状态均已持久保存。"""
            stamp = int((started + timedelta(days=offset)).timestamp())
            with patch.object(agent, "now", return_value=stamp), agent.ledger(folder) as db:
                agent.editor_event(root, db, event)
                count = db.execute("SELECT COUNT(*) FROM outbox WHERE json_extract(payload,'$.kind')='snapshot'")
                self.assertEqual(count.fetchone()[0], expected)

        observe(0, observed, 1)
        observe(0, compile_finished, 1)
        observe(1, observed | {"tools": {"mcp": {"state": "connected"}}}, 1)
        observe(1, compile_finished, 2)
        observe(1, compile_finished | {"id": "compile-2"}, 2)
        package = root / "Packages" / agent.FRAMEWORK / "package.json"
        package.write_text(json.dumps({"name": agent.FRAMEWORK, "version": "0.7.14"}))
        observe(1, observed, 3)
        (root / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.4.3f1\n")
        observe(1, observed, 4)
        observe(1, compile_finished, 4)
        observe(2, observed | {"compilation_day": (started + timedelta(days=1)).date().isoformat()}, 4)
        observe(2, observed | {"compilation_day": (started + timedelta(days=2)).date().isoformat()}, 5)

        with patch.object(agent, "REPORT_SERVER", self.url), patch.object(agent, "deliver", return_value=(None, "transport")):
            agent.work(root, folder, "", single=True)
        with agent.ledger(folder) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM outbox WHERE accepted IS NULL AND attempts=1").fetchone()[0], 5)
            db.execute("UPDATE outbox SET ready_at=0")
        with patch.object(agent, "REPORT_SERVER", self.url):
            agent.work(root, folder, "", single=True)
        overview = self.database.overview(14)
        self.assertEqual(overview["workspaces"][0]["framework"], "0.7.14")
        self.assertEqual(overview["workspaces"][0]["unity"], "6000.4.3f1")
        project = self.database.project_detail(overview["workspaces"][0]["project_id"])
        self.assertEqual(len(project["snapshots"]), 5)

    def test_sdk_wait_requires_a_live_editor_observation(self):
        root, folder = self.fixture_project()
        with agent.ledger(folder) as db:
            agent.editor_event(root, db, {"event": "sdk-start", "id": "play-1"})
            waiting = agent.state(db, "sdk")
            waiting["started"] -= 62
            agent.save(db, "sdk", waiting)
            agent.inspect_pending(db)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM outbox").fetchone()[0], 0)
            agent.editor_event(root, db, {"event": "snapshot", "sdk_attempt": "play-1", "packages": [],
                                          "tools": {}, "enabled": []})
            agent.inspect_pending(db)
            agent.inspect_pending(db)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM outbox WHERE json_extract(payload,'$.kind')='issue'").fetchone()[0], 1)

    def test_git_and_automatic_hook_contract(self):
        """无接入配置的真实 CLI 钩子通过后台 worker 完成首次自动上报。"""
        root, folder = self.fixture_project()
        (root / ".gitignore").write_text("")
        self.assertIsNone(agent.project_folder(root))
        (root / ".gitignore").write_text("/Library/\n")
        before = git(root, "status", "--porcelain", "--untracked-files=all")
        legacy_id = uuid.uuid4().hex
        with agent.ledger(folder) as db:
            old_issue = agent.routing_issue(agent.ROUTE_BY_SKILL["nova-project-configure-runtime"], "0.7.13", False)
            old_issue["evidence"][1] = {"source": "codex.PostToolUse", "detail": "未观察到对应入口。"}
            agent.enqueue(db, "issue", old_issue, legacy_id)
            self.assertFalse(agent.has_pending_reports(db))
        script = self.base / AGENT_FILE.name
        script.write_text(AGENT_FILE.read_text(encoding="utf-8").replace(agent.REPORT_SERVER, self.url), encoding="utf-8")
        command = [sys.executable, "-B", str(script), "hook"]
        environment = {key: value for key, value in os.environ.items() if not key.startswith("NOVA_ISSUES_")}
        for request in (
            self.event("UserPromptSubmit", cwd=str(root), prompt="导出运行时配置"),
            self.event("PostToolUse", cwd=str(root), tool_name="Bash", tool_use_id="invoke",
                       tool_input={"command": "python3 config_export.py"}, tool_response={"exit_code": 0}),
            self.event("Stop", cwd=str(root), last_assistant_message="我已完成导出。"),
        ):
            completed = subprocess.run(command, input=json.dumps(request).encode(), capture_output=True,
                                       timeout=5, check=True, env=environment)
            self.assertEqual(json.loads(completed.stdout), {})
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline and not self.database.overview(14)["issues"]:
            time.sleep(0.1)
        issues = self.database.overview(14)["issues"]
        self.assertEqual([(item["code"], item["occurrences"]) for item in issues], [("missing-nova-route.config", 1)])
        self.assertIsNotNone(self.database.project_detail("auto-1234567890abcdef1234567890abcdef"))
        with agent.ledger(folder) as db:
            self.assertEqual(db.execute("SELECT route FROM turns").fetchone()[0], "nova-project-configure-runtime")
            self.assertEqual(tuple(db.execute("SELECT attempts, accepted FROM outbox WHERE id=?", (legacy_id,)).fetchone()), (0, None))
        self.assertEqual(git(root, "status", "--porcelain", "--untracked-files=all"), before)

    def test_automatic_workspace_credentials(self):
        """同一 Unity 项目归集多工作区，重复报告复用回执且凭据只绑定自身。"""
        project = "auto-" + uuid.uuid4().hex
        token = "a" * 64
        event = self.sample_issue()

        def send(report, credential=token, project_id=project):
            return self.http("/api/v1/auto-reports", {"name": "示例项目", "report": report},
                             {"X-Nova-Project": project_id, "Authorization": "Bearer " + credential})

        self.assertTrue(self.http("/health")[1]["automatic_reporting"])
        self.assertEqual(send(event)[0], 201)
        self.assertEqual(send(event)[0], 200)
        self.assertEqual(send(event, "b" * 64)[0], 401)
        self.assertEqual(send(event, project_id="auto-" + uuid.uuid4().hex)[0], 401)
        self.assertEqual(send(self.sample_issue(), "b" * 64)[0], 201)
        self.assertEqual(self.ingest(self.sample_issue(), project, token)[0], 401)
        self.assertEqual(self.http("/api/maintainer/overview", headers={"Authorization": "Bearer " + token})[0], 401)


if __name__ == "__main__":
    unittest.main()
