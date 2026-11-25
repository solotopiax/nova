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

from build_usage_descriptions import FRAMEWORK_ROOT, build_descriptions
from database import Database
from registries import APPLOVIN_REGISTRY, OPENUPM_REGISTRY, REGISTRIES, UNITY_REGISTRY
from serve import ACCOUNTS, UNITY_USAGE_DESCRIPTIONS, USAGE_DESCRIPTIONS, Handler, NovaIssuesServer, backup, password_hash


AGENT_FILE = Path(__file__).resolve().parents[2] / "Assets/Framework/Agents/Tools/nova_issues_agent.py"
SPEC = importlib.util.spec_from_file_location("nova_issues_agent_under_test", AGENT_FILE)
agent = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(agent)
REVIEW_SPEC = importlib.util.spec_from_file_location("nova_hook_review_under_test", AGENT_FILE.with_name("nova_hook_review.py"))
hook_review = importlib.util.module_from_spec(REVIEW_SPEC)
with patch.dict(sys.modules, {"nova_issues_agent": agent}):
    REVIEW_SPEC.loader.exec_module(hook_review)


def git(root, *arguments):
    return subprocess.check_output(["git", "--no-optional-locks", *arguments], cwd=root, stderr=subprocess.DEVNULL)


class HookReviewTest(unittest.TestCase):
    def test_feedback_reminder_blocks_until_decision_and_repeats_on_later_days_or_upgrade(self):
        """暂缓只放行当日的当前版本，未收到决定、跨天和升级都继续阻塞。"""
        definitions = {"sessionStart": [{"nova start"}], "stop": [{"nova stop"}]}
        start = {"eventName": "sessionStart", "command": "nova start", "isManaged": False, "enabled": True,
                 "trustStatus": "trusted"}
        stop = start | {"eventName": "stop", "command": "nova stop"}
        unrelated = start | {"command": "another hook", "trustStatus": "untrusted"}
        cases = (
            ([start, stop | {"trustStatus": "modified"}], 1, 0, 1),
            ([start, stop | {"enabled": False}], 0, 1, 0),
            ([start, stop | {"enabled": False, "trustStatus": "untrusted"}], 0, 1, 1),
            (None, 0, 2, None),
        )
        for rows, pending, disabled, unapproved in cases:
            with (self.subTest(pending=pending, disabled=disabled, unapproved=unapproved),
                  tempfile.TemporaryDirectory() as temporary, patch.object(hook_review, "read_json") as metadata,
                  patch.object(hook_review, "date") as clock):
                folder = Path(temporary)
                metadata.return_value = {"name": agent.FRAMEWORK, "version": "0.7.15"}
                trusted = hook_review.classify_hooks([start, stop, unrelated], definitions)
                self.assertEqual(hook_review.task_review(trusted, folder), trusted)
                self.assertEqual(trusted["unapproved"], 0)
                self.assertFalse((folder / hook_review.REVIEW_DB).exists())
                inactive = hook_review.classify_hooks(rows, definitions)
                token = None
                for day in (24, 25, 26):
                    today = datetime(2026, 11, day).date()
                    clock.today.return_value = today
                    first = hook_review.task_review(inactive, folder)
                    self.assertEqual((first["status"], first["reason"], first["localDay"]),
                                     ("blocked", "feedback_reminder", today.isoformat()))
                    self.assertEqual((first["pending"], first["disabled"], first["unapproved"]),
                                     (pending, disabled, unapproved))
                    self.assertEqual(hook_review.task_review(inactive, folder, defer_token=token), first)
                    token = first["reminderToken"]
                    deferred = hook_review.task_review(inactive, folder, defer_token=token)
                    self.assertEqual((deferred["status"], deferred["reason"]), ("ready", "feedback_deferred"))
                    self.assertEqual(hook_review.task_review(inactive, folder), deferred)
                metadata.return_value["version"] = "0.7.16"
                upgraded = hook_review.task_review(inactive, folder, defer_token=token)
                self.assertEqual((upgraded["status"], upgraded["frameworkVersion"]), ("blocked", "0.7.16"))
                self.assertNotEqual(upgraded["reminderToken"], token)
                self.assertEqual(hook_review.task_review(trusted, folder), trusted)


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

    def test_retired_device_sdk_reports_are_neither_sent_nor_displayed(self):
        root, folder = self.fixture_project()
        stamp = int(time.time()) - 10
        data = {"stage": "sdk-device", "framework": "0.7.15", "unity": "6000.4.2f1",
                "platform": "Android", "sdk": "Nova.Sdk.Sample", "build": uuid.uuid4().hex,
                "attempt": uuid.uuid4().hex, "outcome": "success", "milliseconds": 50}
        report_id = uuid.uuid4().hex
        with agent.ledger(folder) as db:
            agent.enqueue(db, "implementation", data, report_id, stamp)
            cached = db.execute("SELECT payload FROM outbox WHERE id=?", (report_id,)).fetchone()["payload"]
            self.assertFalse(agent.has_pending_reports(db))
        with patch.object(agent, "automatic_report_health") as health, patch.object(agent, "deliver") as send:
            agent.work(root, folder, "", single=True)
            health.assert_not_called()
            send.assert_not_called()
        with agent.ledger(folder) as db:
            self.assertEqual(cached, db.execute("SELECT payload FROM outbox WHERE id=?", (report_id,)).fetchone()["payload"])
        report = json.loads(cached)
        self.assertEqual(self.ingest(report)[0], 400)
        # 模拟旧版服务器已有的记录，确认新版查询不再展示。
        self.database.ingest("project-a", report, self.project_token)
        status, detail, _ = self.http("/api/maintainer/projects/project-a", headers=self.login())
        self.assertEqual(status, 200)
        self.assertEqual(detail["implementation"], [])

    def test_editor_play_and_upgrade_keep_independent_execution_evidence(self):
        self.assertTrue(self.http("/health")[1]["execution_evidence"])
        stamp = int(time.time())
        workspace, play, upgrade = (uuid.uuid4().hex for _ in range(3))

        def record(stage, observed_at, **data):
            return self.ingest({"version": 1, "id": uuid.uuid4().hex, "workspace": workspace,
                                "observed_at": observed_at, "kind": "implementation", "data": {
                                    "stage": stage, "framework": "0.7.15", "unity": "6000.4.2f1", **data}})[0]

        for offset in (2, 1):
            self.assertEqual(record("editor-module", stamp - offset, step="table", outcome="success"), 201)
        for phase, result, offset, sequence in (("sdk", "waiting", 8, 2), ("sdk", "success", 1, 2),
                                                 ("load-dll", "advanced", 10, 1)):
            self.assertEqual(record("startup-stage", stamp - offset, step=phase, outcome=result, attempt=play,
                                    sequence=sequence, milliseconds=18000), 201)
        for sequence, (phase, result, version) in enumerate((
            ("request", "running", "0.7.15"), ("compile", "failed", "0.7.15"),
            ("compile", "success", "0.7.16"), ("load", "recovered", "0.7.16"),
        ), 1):
            self.assertEqual(record("framework-upgrade", stamp, step=phase, outcome=result, framework=version,
                                    attempt=upgrade, from_framework="0.7.15", requested_framework="0.7.16",
                                    sequence=sequence, milliseconds=1000), 201)
        status, detail, _ = self.http("/api/maintainer/projects/project-a", headers=self.login())
        self.assertEqual(status, 200)
        self.assertEqual(detail["module_evidence"][0]["play_sessions"], 2)
        self.assertEqual(detail["module_evidence"][0]["count"], 0)
        events = [(row["data"]["stage"], row["data"]["step"], row["data"]["outcome"])
                  for row in detail["implementation"]]
        self.assertIn(("startup-stage", "sdk", "success"), events)
        self.assertNotIn(("startup-stage", "sdk", "waiting"), events)
        self.assertIn(("framework-upgrade", "load", "recovered"), events)
        self.assertNotIn(("framework-upgrade", "compile", "failed"), events)

    def test_project_implementation_keeps_actual_stage_workspace_and_time(self):
        stamp = int(time.time())
        workspace = uuid.uuid4().hex
        other_workspace = uuid.uuid4().hex

        def report(kind, observed_at, **data):
            return {"version": 1, "id": uuid.uuid4().hex, "workspace": workspace, "observed_at": observed_at,
                    "kind": kind, "data": {"framework": "0.7.15"} | data}

        baseline = report("implementation", stamp - 10, unity="6000.4.2f1", platform="Android", stage="workspace",
                          requested_framework="0.7.16", editor_framework="0.7.15",
                          tools={"config": "configured", "pipify": "configured", "plugpals": "pending"},
                          sdk_packages=[{"id": agent.FRAMEWORK + ".sdk.firebase", "version": "0.1.2"}],
                          sdk_configs=["Nova.Firebase.Config"])
        batch = report("implementation", stamp - 5, stage="pipify-batch", unity="6000.4.2f1",
                       platform="Android", outcome="failed", step="export.config", milliseconds=1500)
        late = report("implementation", stamp - 60, stage="pipify-batch", unity="6000.4.2f1",
                      platform="Android", outcome="success")
        xcode = report("implementation", stamp - 3, stage="player-build", unity="6000.4.2f1",
                       platform="iOS", build_kind="project", development=True, outcome="success")
        secondary = report("implementation", stamp - 2, stage="player-build", unity="6000.4.2f1",
                           platform="Android", build_kind="player", development=True, outcome="success")
        secondary["workspace"] = other_workspace
        for event in (baseline, batch, late, xcode, secondary):
            self.assertEqual(self.ingest(event)[0], 201)
        for entry in ("skill", "action"):
            self.assertEqual(self.ingest(report("usage", stamp, module="config", operation="config.apply",
                                                source="codex", entry=entry, outcome="success"))[0], 201)
        forbidden = report("implementation", stamp, stage="player-build", unity="6000.4.2f1",
                           outcome="success", build_kind="player", development=True, sdk_verified=True)
        self.assertEqual(self.ingest(forbidden)[0], 400)
        self.assertTrue(self.http("/health")[1]["implementation_tracking"])

        headers = self.login()
        status, detail, _ = self.http("/api/maintainer/projects/project-a", headers=headers)
        self.assertEqual(status, 200)
        facts = detail["implementation"]
        self.assertEqual(len(facts), 4)
        current_batch = next(fact for fact in facts if fact["data"]["stage"] == "pipify-batch")
        self.assertEqual((current_batch["observed_at"], current_batch["data"]["outcome"]), (stamp - 5, "failed"))
        self.assertGreaterEqual(current_batch["received_at"], current_batch["observed_at"])
        self.assertEqual(sorted(fact["data"]["build_kind"] for fact in facts if fact["data"]["stage"] == "player-build"),
                         ["player", "project"])
        self.assertEqual(detail["module_evidence"][0]["count"], 1)
        _, overview, _ = self.http("/api/maintainer/overview", headers=headers)
        self.assertEqual(overview["implementation"][0]["requested_framework"], "0.7.16")

    def test_project_implementation_keeps_verified_progress_across_workspaces(self):
        stamp = int(time.time())
        historical = stamp - 31 * 86400
        first, second = uuid.uuid4().hex, uuid.uuid4().hex

        def record(workspace, stage, observed_at, **data):
            report = {"version": 1, "id": uuid.uuid4().hex, "workspace": workspace,
                      "observed_at": observed_at, "kind": "implementation", "data": {
                          "stage": stage, "framework": "0.7.16" if workspace == first else "0.7.17",
                          "unity": "6000.4.2f1", **data}}
            self.assertEqual(self.ingest(report)[0], 201)

        record(first, "workspace", historical, editor_framework="0.7.16", sdk_packages=[], sdk_configs=[],
               tools={"config": "configured", "pipify": "pending", "plugpals": "pending"})
        record(first, "config-export", historical, outcome="success", platform="Android")
        record(first, "editor-module", historical, outcome="success", step="asset")
        record(second, "workspace", stamp - 5, editor_framework="0.7.17", sdk_packages=[], sdk_configs=[],
               tools={"config": "pending", "pipify": "configured", "plugpals": "pending"})

        headers = self.login()
        path = "/api/maintainer/projects/project-a"
        _, detail, _ = self.http(path, headers=headers)
        self.assertEqual(detail["configured_tools"], ["config", "pipify"])
        export = next(row for row in detail["implementation_outcomes"] if row["data"]["stage"] == "config-export")
        self.assertEqual((export["workspace"], export["data"]["outcome"]), (first, "success"))

        record(first, "config-export", stamp - 2, outcome="failed", platform="Android")
        record(second, "config-export", stamp - 15, outcome="blocked", platform="Android")
        record(first, "editor-module", stamp - 4, outcome="success", step="table")
        record(second, "editor-module", stamp - 3, outcome="success", step="table")
        _, detail, _ = self.http(path, headers=headers)
        exports = [row for row in detail["implementation_outcomes"] if row["data"]["stage"] == "config-export"]
        self.assertEqual([(row["data"]["outcome"], row["observed_at"]) for row in exports],
                         [("failed", stamp - 2), ("success", historical)])
        modules = {row["module"]: row for row in detail["project_module_evidence"]}
        self.assertEqual((modules["asset"]["play_sessions"], modules["asset"]["last_success"]), (0, historical))
        self.assertEqual((modules["table"]["play_sessions"], modules["table"]["last_success"]), (2, stamp - 3))
        _, overview, _ = self.http("/api/maintainer/overview", headers=headers)
        self.assertEqual(overview["configured_tools"]["project-a"], detail["configured_tools"])

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
        """重复上报只保留一份，快速处理保护旧备注与其他维护者的修改。"""
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
        with self.database.connect() as db:
            db.execute("UPDATE issues SET status='awaiting_verification',fixed_version='0.7.14' WHERE id=?", (issue,))
            db.execute("INSERT INTO history (issue_id,actor,status,note,created_at) VALUES (?,?,?,?,?)",
                       (issue, "taoye", "awaiting_verification", "旧版记录：待复验。", event["observed_at"]))
        session = self.login()
        route = f"/api/maintainer/issues/{issue}"
        record = self.http(route, headers=session)[1]
        self.assertEqual(len(record["occurrences"]), 2)
        self.assertEqual(record["assignees"], sorted(ACCOUNTS))
        self.assertEqual(record["issue"]["status"], "open")
        self.assertNotIn("fixed_version", record["issue"])
        edit = {"revision": 1, "status": "closed", "assignee": "yingzheng", "note": ""}
        self.assertEqual(self.http(route, edit | {"assignee": "维护组"}, session)[0], 400)
        self.assertEqual(self.http(route, edit | {"status": "working"}, session)[0], 400)
        self.assertEqual(self.http(route, edit, session)[0], 200)
        self.assertEqual(self.http(route, edit, session)[0], 409)
        note = "已核对。\n补充处理依据。"
        self.assertEqual(self.http(route, edit | {"revision": 2, "note": note}, self.login("yingzheng"))[0], 200)
        record = self.http(route, headers=session)[1]
        self.assertEqual((record["issue"]["status"], record["issue"]["assignee"]), ("closed", "yingzheng"))
        self.assertEqual([(item["actor"], item["status"], item["note"]) for item in record["history"]], [
            ("yingzheng", "closed", note), ("taoye", "closed", ""), ("taoye", "open", "旧版记录：待复验。"),
        ])
        legacy_headers = {"X-Nova-Project": "project-a", "Authorization": "Bearer " + self.project_token}
        self.assertEqual(self.http(f"/api/v1/issues/{issue}", headers=legacy_headers)[1]["fixed_version"], "0.7.14")
        copied = self.base / "backup.sqlite3"
        backup(self.database.path, copied)
        with sqlite3.connect(copied) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM reports").fetchone()[0], 2)

    def test_issue_project_filter_combines_with_search_and_finds_older_issues(self):
        """筛选和排序都在截取列表前生效，影响项目数保留全部项目。"""
        second = self.database.register("project-b", "项目乙", "QA")
        self.database.register("project-empty", "尚未上报", "QA")
        stamp = int(time.time())
        shared = self.ingest(self.sample_issue("sdk-shared") | {"observed_at": stamp - 10})[1]["issue_id"]
        self.assertEqual(self.ingest(self.sample_issue("sdk-shared") | {"observed_at": stamp - 5},
                                     "project-b", second)[1]["issue_id"], shared)
        self.assertEqual(self.ingest(self.sample_issue("sdk-shared") | {"observed_at": stamp - 15})[1]["issue_id"], shared)
        only_a = self.ingest(self.sample_issue("sdk-project-a") | {"observed_at": stamp})[1]["issue_id"]
        only_b = self.ingest(self.sample_issue("sdk-project-b") | {"observed_at": stamp - 1},
                             "project-b", second)[1]["issue_id"]

        # 用发生时间更晚的问题占满总览列表，即使旧上报把它们标成 P3。
        with self.database.connect() as db:
            db.execute("UPDATE issues SET status='working' WHERE id=?", (shared,))
            db.executemany("""
                INSERT INTO issues (fingerprint,code,area,title,priority,first_seen,last_seen)
                VALUES (?, ?, 'sdk', '启动阻塞', 'P3', ?, ?)
                """, ((f"blocking-{i}", f"blocking-{i}", stamp + 1, stamp + 1) for i in range(502)))
            db.executemany("""
                INSERT INTO issues (fingerprint,code,area,title,priority,status,first_seen,last_seen)
                VALUES (?, ?, 'sdk', '启动阻塞', 'P0', ?, ?, ?)
                """, ((status, status, status, stamp, stamp) for status in ("closed", "duplicate")))

        route = "/api/maintainer/issues"
        self.assertEqual(self.http(f"{route}?project=project-a")[0], 401)
        projects_route = f"{route}/{shared}/projects"
        self.assertEqual(self.http(projects_route)[0], 401)
        session = self.login()
        overview = self.http("/api/maintainer/overview", headers=session)[1]
        self.assertEqual(len(overview["issues"]), 500)
        self.assertEqual(overview["issue_counts"], {"active": 505, "handled": 2})
        self.assertNotIn(shared, {item["id"] for item in overview["issues"]})

        def issues_for(query):
            status, payload, _ = self.http(f"{route}?{query}", headers=session)
            self.assertEqual(status, 200, payload)
            return payload["issues"]

        self.assertEqual({item["id"] for item in issues_for("project=project-a")}, {shared, only_a})
        self.assertEqual({item["id"] for item in issues_for("project=project-b")}, {shared, only_b})
        self.assertEqual(issues_for("project=project-empty"), [])
        query = "project=project-a&status=open"
        self.assertEqual([item["id"] for item in issues_for(f"{query}&sort=latest")], [only_a, shared])
        self.assertEqual([item["id"] for item in issues_for(f"{query}&sort=count")], [shared, only_a])
        self.assertEqual([item["id"] for item in issues_for("sort=count")[:3]], [shared, only_a, only_b])
        self.assertEqual(self.http(f"{route}?sort=unsupported", headers=session)[0], 400)
        matches = issues_for(f"{query}&q=SDK-SHARED")
        self.assertEqual([(item["id"], item["projects"], item["occurrences"]) for item in matches], [(shared, 2, 3)])
        self.assertEqual(matches[0]["status"], "open")
        self.assertEqual(issues_for(f"{query}&q=sdk-project-b"), [])
        self.assertEqual(issues_for("project=project-a&status=closed"), [])
        self.assertEqual([item["status"] for item in issues_for("status=closed")], ["closed", "closed"])
        status, payload, _ = self.http(projects_route, headers=session)
        self.assertEqual(status, 200)
        self.assertEqual({(project["id"], project["name"]) for project in payload["projects"]},
                         {("project-a", "项目甲"), ("project-b", "项目乙")})

    def test_issue_package_and_log_follow_the_reported_occurrence(self):
        """旧上报和精简上报都能定位所属包，迟到记录不覆盖最近发生的版本。"""
        stamp = int(time.time())
        sdk = agent.FRAMEWORK + ".sdk.ad"
        location = f"Packages/{sdk}/Scripts/Editor/AdSettings.cs(13,8)"
        sdk_issue = {"area": "editor-compilation", "summary": location, "framework": "0.7.16",
                     "actual": "Unity 编译器报告 CS0117。"}
        latest = self.sample_issue("unity.CS0117.same-location") | {"observed_at": stamp}
        latest["data"].update(sdk_issue | {"evidence": [{"source": "unity.compiler", "detail": "包版本 0.3.1。"}]})
        status, receipt, _ = self.ingest(latest)
        self.assertEqual(status, 201, receipt)
        sdk_id = receipt["issue_id"]

        earlier = self.sample_issue("unity.CS0117.same-location") | {"observed_at": stamp - 10}
        detail_line = "编译器记录的附加信息。"
        earlier["data"].update(sdk_issue | {"version": "0.3.0",
            "evidence": [{"source": "unity.compiler", "detail": detail_line}]})
        self.assertEqual(self.ingest(earlier)[1]["issue_id"], sdk_id)

        meta = f"Packages/{agent.FRAMEWORK}/Agents/Tools/nova_hook_review.py.meta"
        guid = self.sample_issue("unity.invalid-meta-guid.hook-review")
        guid["data"].update({"area": "package-import", "summary": meta, "framework": "0.7.15",
                             "version": "0.7.15", "actual": "Unity 忽略了对应资源。",
                             "evidence": [{"source": "unity.package-import", "detail": f"{meta}。Unity 忽略了对应资源。"}]})
        guid_id = self.ingest(guid)[1]["issue_id"]
        unrelated_id = self.ingest(self.sample_issue())[1]["issue_id"]

        session = self.login()
        route = "/api/maintainer/issues"
        issues = {row["id"]: row for row in self.http(route, headers=session)[1]["issues"]}
        self.assertEqual(issues[sdk_id]["package"], {"id": sdk, "version": "0.3.1"})
        self.assertEqual(issues[guid_id]["package"], {"id": agent.FRAMEWORK, "version": "0.7.15"})
        self.assertIsNone(issues[unrelated_id]["package"])

        detail = self.http(f"{route}/{sdk_id}", headers=session)[1]
        self.assertEqual(detail["issue"]["package"], issues[sdk_id]["package"])
        by_version = {row["package"]["version"]: row["error_log"]["text"] for row in detail["occurrences"]}
        self.assertEqual(by_version, {"0.3.1": f"{location}\nCS0117: Unity 编译器报告 CS0117。",
                                      "0.3.0": f"{location}\nCS0117: Unity 编译器报告 CS0117。\n{detail_line}"})
        guid_occurrence = self.http(f"{route}/{guid_id}", headers=session)[1]["occurrences"][0]
        self.assertEqual(guid_occurrence["error_log"]["text"], f"{meta}\ninvalid-meta-guid: Unity 忽略了对应资源。")
        unrelated = self.http(f"{route}/{unrelated_id}", headers=session)[1]["occurrences"][0]
        self.assertIsNone(unrelated["error_log"])

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
            self.assertTrue(agent.handle_hook(root, db, self.event("SessionStart")))
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
            self.assertCountEqual([item["kind"] for item in reports], ["hook_activity"] + ["usage"] * 5)
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
            self.assertEqual(db.execute("SELECT COUNT(*) FROM outbox WHERE error='server-upgrade-required'").fetchone()[0], 6)
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

    def test_usage_history_identifies_editor_receipts_and_adds_operation_descriptions(self):
        action = "nova.project.hotfix.refresh-game-dlls"
        receipt = {"module": "hotfix", "operation": action, "framework": "0.7.14", "outcome": "success"}
        skill = {"module": "router", "operation": "nova-project-router", "framework": "0.7.15",
                 "outcome": "executed", "source": "codex", "entry": "skill"}
        preload = {"module": "sdk", "operation": "editor.preload.sdk", "framework": "0.7.14", "outcome": "success"}
        unknown = {"module": "other", "operation": "other.method", "framework": "0.7.14", "outcome": "success"}
        unity = {"module": "unity-cli", "framework": "0.7.15", "outcome": "success",
                 "source": "codex", "entry": "unity-cli"}
        workspace = uuid.uuid4().hex
        events = []
        for data in (receipt, receipt | {"source": "editor", "entry": "action"},
                     receipt | {"source": "codex", "entry": "action"}, skill,
                     preload, preload | {"source": "editor"}, unknown,
                     unity | {"operation": "unity.capture.screenshot"}, unity | {"operation": "unity.custom.refresh"}):
            event = {"version": 1, "id": uuid.uuid4().hex, "workspace": workspace,
                     "observed_at": int(time.time()), "kind": "usage", "data": data}
            self.assertEqual(self.ingest(event)[0], 201)
            events.append(event)

        session = self.login()
        status, overview, _ = self.http("/api/maintainer/overview", headers=session)
        self.assertEqual(status, 200)
        rows = {(row["operation"], row["source"]): row for row in overview["usage"]}
        editor_action = rows[(action, "editor")]
        self.assertEqual((editor_action["entry"], editor_action["count"]), ("action", 2))
        self.assertIn("编译并刷新当前构建目标对应的业务热更 DLL", editor_action["description"])
        self.assertEqual(rows[(action, "codex")]["count"], 1)
        self.assertIn("Nova 项目任务路由", rows[(skill["operation"], "codex")]["description"])
        sdk = rows[(preload["operation"], "editor")]
        self.assertEqual((sdk["entry"], sdk["count"]), ("preload", 2))
        legacy = rows[(unknown["operation"], "legacy")]
        self.assertEqual((legacy["entry"], legacy["description"]), ("", ""))
        self.assertIn("截取 Scene 或 Game", rows[("unity.capture.screenshot", "codex")]["description"])
        self.assertIn("custom refresh", rows[("unity.custom.refresh", "codex")]["description"])
        status, result, _ = self.http("/api/maintainer/usage?source=editor&module=hotfix", headers=session)
        self.assertEqual(status, 200, result)
        self.assertEqual([(row["operation"], row["source"], row["count"]) for row in result["usage"]],
                         [(action, "editor", 2)])
        self.assertEqual(result["usage"][0]["description"], editor_action["description"])
        self.assertEqual(self.ingest(events[0])[0], 200)
        with self.database.connect() as db:
            original = db.execute("SELECT payload FROM reports WHERE id=?", (events[0]["id"],)).fetchone()
        self.assertEqual(json.loads(original["payload"]), receipt)

    def test_usage_sort_uses_observation_time_and_applies_before_limit(self):
        """低频的新记录可排到前面，晚到的旧上报仍按实际发生时间排序。"""
        self.database.register("project-b", "项目乙", "QA")
        stamp = int(time.time())
        workspace = uuid.uuid4().hex

        def report_row(project, operation, observed_at, received_at):
            """用固定发生与接收时间填充使用记录，保持原有聚合维度。"""
            payload = {"module": "hotfix", "operation": operation, "framework": "0.7.15",
                       "outcome": "success", "source": "editor", "entry": "action"}
            return (project, uuid.uuid4().hex, workspace, json.dumps(payload), observed_at, received_at)

        with self.database.connect() as db:
            insert = """
                INSERT INTO reports (project_id,id,workspace,kind,digest,payload,observed_at,received_at)
                VALUES (?, ?, ?, 'usage', 'fixture', ?, ?, ?)
                """
            db.executemany(insert, (
                report_row("project-a", f"fixture.operation-{index}", stamp - age, stamp - age)
                for index in range(500) for age in (100, 200)
            ))
            db.executemany(insert, (
                report_row("project-b", "fixture.recent", stamp - 5, stamp - 4),
                report_row("project-a", "fixture.frequent", stamp - 10, stamp - 9),
                report_row("project-a", "fixture.frequent", stamp - 20, stamp - 8),
                report_row("project-a", "fixture.frequent", stamp - 30, stamp),
            ))

        route = "/api/maintainer/usage"
        self.assertEqual(self.http(route)[0], 401)
        session = self.login()

        def usage_for(query):
            """读取待检查的维护台列表。"""
            status, payload, _ = self.http(f"{route}?{query}", headers=session)
            self.assertEqual(status, 200, payload)
            return payload["usage"]

        by_count = usage_for("days=14")
        self.assertEqual(len(by_count), 500)
        self.assertEqual((by_count[0]["operation"], by_count[0]["count"]), ("fixture.frequent", 3))
        self.assertNotIn("fixture.recent", {row["operation"] for row in by_count})
        by_latest = usage_for("days=14&sort=latest")
        self.assertEqual([(row["operation"], row["last_used"]) for row in by_latest[:2]],
                         [("fixture.recent", stamp - 5), ("fixture.frequent", stamp - 10)])
        self.assertEqual(usage_for("days=14&sort=count&project=project-b"), [by_latest[0]])
        self.assertEqual(self.http(f"{route}?sort=unsupported", headers=session)[0], 400)

    def test_usage_descriptions_follow_action_and_skill_sources(self):
        published = json.loads(USAGE_DESCRIPTIONS.read_text(encoding="utf-8"))
        for operation, description in build_descriptions(FRAMEWORK_ROOT).items():
            self.assertEqual(published.get(operation), description, operation)
        for script, (_, commands) in agent.NOVA_CLI.items():
            for command in commands:
                self.assertTrue(published[f"{Path(script).stem}.{command}"])
        unity = json.loads(UNITY_USAGE_DESCRIPTIONS.read_text(encoding="utf-8"))
        self.assertTrue(all(name.startswith("unity.") and text for name, text in unity.items()))

    def test_hook_activity_is_immediate_and_hourly_with_compatible_retry(self):
        root, folder = self.fixture_project()
        first_run = int(time.time()) - 7200
        with agent.ledger(folder) as db:
            with patch.object(agent, "now", return_value=first_run):
                self.assertTrue(agent.handle_hook(root, db, self.event("SessionStart")))
            with patch.object(agent, "now", return_value=first_run + 3599):
                self.assertFalse(agent.handle_hook(root, db, self.event("UserPromptSubmit", prompt="private task prompt")))

        def record(stamp):
            with agent.ledger(folder) as db:
                return agent.report_hook_activity(db, "0.7.13", stamp)

        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            self.assertCountEqual(pool.map(record, (first_run + 3600, first_run + 3601)), [True, False])

        with agent.ledger(folder) as db:
            reports = [json.loads(row["payload"]) for row in db.execute("SELECT payload FROM outbox ORDER BY rowid")]
            self.assertEqual([item["kind"] for item in reports], ["hook_activity", "hook_activity"])
            self.assertEqual([item["data"] for item in reports], [{"framework": "0.7.13"}] * 2)
            self.assertEqual(reports[0]["observed_at"], first_run)
            self.assertIn(reports[1]["observed_at"], (first_run + 3600, first_run + 3601))
            workspace = agent.state(db, "workspace")
        with patch.object(agent, "REPORT_SERVER", self.url):
            with patch.object(agent, "automatic_report_health", return_value={"automatic_reporting": True}):
                agent.work(root, folder, "", single=True)
            with agent.ledger(folder) as db:
                pending = db.execute("SELECT id,accepted,error FROM outbox ORDER BY rowid").fetchall()
                self.assertEqual([tuple(row) for row in pending], [
                    (report["id"], None, "server-upgrade-required") for report in reports
                ])
                db.execute("UPDATE outbox SET ready_at=0")
            agent.work(root, folder, "", single=True)

        project = "auto-1234567890abcdef1234567890abcdef"
        self.assertEqual(self.database.project_detail(project)["hook_activity"], [{
            "workspace": workspace, "last_run": reports[1]["observed_at"], "framework": "0.7.13",
        }])
        records = self.database.overview(1)
        self.assertEqual(next(row for row in records["projects"] if row["id"] == project)["last_hook_run"], reports[1]["observed_at"])
        self.assertEqual(records["workspaces"], [])
        self.assertEqual(records["usage"], [])

    def test_hook_activity_uses_observed_time_instead_of_delivery_order(self):
        self.assertTrue(self.http("/health")[1]["hook_activity"])
        recent = int(time.time()) - 30
        workspace = uuid.uuid4().hex
        another_workspace = uuid.uuid4().hex

        def run(workspace_id, observed_at, framework):
            return {"version": 1, "id": uuid.uuid4().hex, "workspace": workspace_id,
                    "observed_at": observed_at, "kind": "hook_activity", "data": {"framework": framework}}

        self.assertEqual(self.ingest(run(workspace, recent, "0.7.15"))[0], 201)
        self.assertEqual(self.ingest(run(workspace, recent - 7200, "0.7.14"))[0], 201)
        older = run(another_workspace, recent - 3600, "0.7.14")
        self.assertEqual(self.ingest(older | {"data": older["data"] | {"prompt": "private"}})[0], 400)
        self.assertEqual(self.ingest(older)[0], 201)
        self.assertEqual(self.ingest(older)[0], 200)
        session = self.login()
        overview = self.http("/api/maintainer/overview?days=1", headers=session)[1]
        self.assertEqual(overview["projects"][0]["last_hook_run"], recent)
        detail = self.http("/api/maintainer/projects/project-a", headers=session)[1]
        self.assertEqual(detail["hook_activity"], [
            {"workspace": workspace, "last_run": recent, "framework": "0.7.15"},
            {"workspace": another_workspace, "last_run": recent - 3600, "framework": "0.7.14"},
        ])

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

    def test_environment_details_update_and_wait_for_compatible_service(self):
        root, folder = self.fixture_project()
        packages = [{"id": agent.FRAMEWORK, "version": "0.7.13", "source": "registry"}]
        snapshot = {"event": "snapshot", "packages": packages, "tools": {}, "codex_hooks": "pending",
                    "application_identifiers": {"Android": "com.fixture.game"}}
        with agent.ledger(folder) as db:
            agent.editor_event(root, db, snapshot)
            agent.editor_event(root, db, snapshot)
            agent.editor_event(root, db, snapshot | {"codex_hooks": "incomplete"})
            agent.editor_event(root, db, snapshot | {"codex_hooks": "complete"})
            agent.editor_event(root, db, snapshot | {"codex_hooks": "complete",
                               "application_identifiers": {"Android": "com.fixture.newgame", "iOS": "com.fixture.ios"}})

        old_health = {"automatic_reporting": True, "codex_telemetry": True}
        with patch.object(agent, "automatic_report_health", return_value=old_health), patch.object(agent, "deliver") as send:
            agent.work(root, folder, "", single=True)
            send.assert_not_called()
        with agent.ledger(folder) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM outbox WHERE error='server-upgrade-required'").fetchone()[0], 4)
            db.execute("UPDATE outbox SET ready_at=0")
        with patch.object(agent, "REPORT_SERVER", self.url):
            agent.work(root, folder, "", single=True)

        project_id = self.database.overview(14)["workspaces"][0]["project_id"]
        snapshots = self.database.project_detail(project_id)["snapshots"]
        self.assertEqual([item["data"]["codex_hooks"] for item in snapshots], ["complete", "complete", "incomplete", "pending"])
        self.assertEqual(snapshots[0]["data"]["application_identifiers"], {"Android": "com.fixture.newgame", "iOS": "com.fixture.ios"})

    def test_git_name_follows_workspace_configuration_and_waits_for_support(self):
        """工程名覆盖全局名，改名和清除名称都会刷新环境快照。"""
        root, folder = self.fixture_project()
        global_config = self.base / "user.gitconfig"
        global_config.write_text("[user]\n\tname = 全局配置名\n", encoding="utf-8")
        snapshot = {"event": "snapshot", "packages": [], "tools": {}}
        started = int(time.time()) - 10

        def observe(db, seconds):
            with patch.object(agent, "now", return_value=started + seconds):
                agent.editor_event(root, db, snapshot)

        with patch.dict(os.environ, {"GIT_CONFIG_GLOBAL": str(global_config), "GIT_CONFIG_NOSYSTEM": "1"}):
            with agent.ledger(folder) as db:
                observe(db, 0)
                git(root, "config", "--local", "user.name", "工程配置名")
                git(root, "config", "--local", "user.email", "fixture@example.test")
                observe(db, 1)
                observe(db, 2)
                git(root, "config", "--local", "user.name", "新的配置名")
                observe(db, 3)
                git(root, "config", "--local", "--unset", "user.name")
                global_config.write_text("", encoding="utf-8")
                observe(db, 4)
                payloads = [row[0] for row in db.execute("SELECT payload FROM outbox ORDER BY rowid")]
                self.assertEqual([json.loads(row)["data"].get("git_user_name") for row in payloads],
                                 ["全局配置名", "工程配置名", "新的配置名", None])
                self.assertTrue(all("fixture@example.test" not in row for row in payloads))

            old_health = {"automatic_reporting": True, "codex_telemetry": True, "project_snapshot_details": True}
            with patch.object(agent, "REPORT_SERVER", self.url), patch.object(agent, "automatic_report_health", return_value=old_health):
                agent.work(root, folder, "", single=True)
            with agent.ledger(folder) as db:
                self.assertEqual(db.execute("SELECT COUNT(*) FROM outbox WHERE error='server-upgrade-required'").fetchone()[0], 3)
                self.assertEqual(db.execute("SELECT COUNT(*) FROM report_receipts").fetchone()[0], 1)
                db.execute("UPDATE outbox SET ready_at=0")
            with patch.object(agent, "REPORT_SERVER", self.url):
                agent.work(root, folder, "", single=True)

        project = self.database.overview(14)["workspaces"][0]["project_id"]
        snapshots = self.http(f"/api/maintainer/projects/{project}", headers=self.login())[1]["snapshots"]
        self.assertEqual([item["data"].get("git_user_name") for item in snapshots],
                         [None, "新的配置名", "工程配置名", "全局配置名"])

    def test_compact_reports_preserve_data_used_by_maintainers(self):
        """后台确认能力后，环境和问题只提交有用字段，旧待发正文保持原样。"""
        root, folder = self.fixture_project()
        git(root, "config", "--local", "user.name", "快照上报人")
        snapshot = {
            "event": "snapshot", "codex_hooks": "pending",
            "packages": [{"id": agent.FRAMEWORK, "version": "0.7.13", "source": "registry"}],
            "application_identifiers": {"Android": "com.fixture.game"}, "enabled": ["NovaFramework.SDK.Example"],
            "platform": "Android", "channel": "Store", "mode": "Debug", "coordinate_source": "ConfigMaster",
            "tools": {"unity-cli": {"state": "connected"}, "mcp": {"state": "installed"},
                      "pipeline": {"state": "registered"}},
        }
        with agent.ledger(folder) as db:
            agent.editor_event(root, db, snapshot)
            legacy = json.loads(db.execute("SELECT payload FROM outbox").fetchone()[0])
        self.assertEqual(legacy["data"]["packages"][0]["evidence"], "registered")
        _, health, _ = self.http("/health")
        self.assertIs(health["compact_reports"], True)
        with patch.object(agent, "REPORT_SERVER", self.url):
            agent.work(root, folder, "", single=True)
        with agent.ledger(folder) as db:
            self.assertIs(agent.state(db, agent.COMPACT_REPORTS_KEY), True)
            agent.editor_event(root, db, snapshot | {"codex_hooks": "complete"})
            issue = self.sample_issue("compact-report")["data"]
            agent.enqueue(db, "issue", issue)
            waiting = db.execute("SELECT id,payload FROM outbox ORDER BY rowid").fetchall()
        current = json.loads(waiting[0]["payload"])["data"]
        self.assertEqual(set(current), {"framework", "unity", "packages", "tools", "coverage", "enabled",
                                       "application_identifiers", "codex_hooks", "git_user_name"})
        self.assertEqual(current["packages"], snapshot["packages"])
        self.assertEqual(set(current["tools"]), {"unity-cli"})
        self.assertEqual(current["application_identifiers"], snapshot["application_identifiers"])
        self.assertEqual(current["enabled"], snapshot["enabled"])
        self.assertEqual(current["git_user_name"], "快照上报人")
        self.assertEqual(json.loads(waiting[1]["payload"])["data"],
                         {key: value for key, value in issue.items() if key != "priority"})

        health.pop("compact_reports")
        with patch.object(agent, "automatic_report_health", return_value=health), patch.object(agent, "deliver") as send:
            agent.work(root, folder, "", single=True)
            send.assert_not_called()
        with agent.ledger(folder) as db:
            self.assertEqual(db.execute("SELECT id,payload FROM outbox ORDER BY rowid").fetchall(), waiting)
            self.assertEqual(db.execute("SELECT DISTINCT error FROM outbox").fetchone()[0], "server-upgrade-required")
            db.execute("UPDATE outbox SET ready_at=0")
        with patch.object(agent, "REPORT_SERVER", self.url):
            agent.work(root, folder, "", single=True)
        project = self.database.project_detail("auto-1234567890abcdef1234567890abcdef")
        self.assertEqual({row["id"]: row["data"] for row in project["snapshots"]}, {
            legacy["id"]: legacy["data"], waiting[0]["id"]: current,
        })
        self.assertEqual(self.database.overview(14)["issues"][0]["occurrences"], 1)

    def test_only_matching_receipt_removes_payload_and_retry_is_idempotent(self):
        """已落库但返回了错误编号时保留正文，下次补发使用服务端原回执。"""
        root, folder = self.fixture_project()
        report_id = uuid.uuid4().hex
        issue = self.sample_issue()["data"]
        with agent.ledger(folder) as db:
            agent.enqueue(db, "issue", issue, report_id)
            payload = db.execute("SELECT payload FROM outbox").fetchone()[0]

        def wrong_receipt(handler, _event, result):
            """模拟服务已落库，却返回了另一个上报编号。"""
            handler.reply(201, {"accepted": True, "id": uuid.uuid4().hex, "issue_id": result[1]})

        with patch.object(agent, "REPORT_SERVER", self.url), patch.object(Handler, "report_receipt", wrong_receipt):
            agent.work(root, folder, "", single=True)
        with agent.ledger(folder) as db:
            self.assertEqual(tuple(db.execute("SELECT payload,error FROM outbox").fetchone()),
                             (payload, "invalid-receipt"))
            self.assertIsNone(db.execute("SELECT id FROM report_receipts").fetchone())
            db.execute("UPDATE outbox SET ready_at=0")
        with patch.object(agent, "REPORT_SERVER", self.url):
            agent.work(root, folder, "", single=True)

        issues = self.database.overview(14)["issues"]
        self.assertEqual(issues[0]["occurrences"], 1)
        with agent.ledger(folder) as db:
            agent.enqueue(db, "issue", issue, report_id, agent.now() + 1)
            self.assertIsNone(db.execute("SELECT id FROM outbox").fetchone())
            self.assertEqual(tuple(db.execute("SELECT id,kind,issue_id FROM report_receipts").fetchone()),
                             (report_id, "issue", issues[0]["id"]))
        status = json.loads(subprocess.check_output(
            [sys.executable, "-B", str(AGENT_FILE), "--project", str(root), "status"], timeout=5))
        self.assertEqual(status["outbox"], [])
        self.assertEqual(status["receipts"][0]["id"], report_id)

    def test_legacy_cache_migration_reclaims_file_and_keeps_pending_payload(self):
        """旧版成功缓存可以回收磁盘页，未来待发的记录仍保持原样。"""
        root, folder = self.fixture_project()
        path = folder / "agent.sqlite3"
        stamp = agent.now()
        with sqlite3.connect(path) as db:
            db.executescript(agent.SCHEMA)
            db.execute("DROP TRIGGER discard_delivered_report")
            payload = json.dumps({"kind": "snapshot", "data": "x" * (1 << 17)})
            db.executemany("INSERT INTO outbox (id,payload,ready_at,accepted) VALUES (?,?,?,?)", [
                (uuid.uuid4().hex, payload, stamp, stamp if index else stamp - agent.RECEIPT_RETENTION_SECONDS - 60)
                for index in range(16)
            ])
            pending = json.dumps({"kind": "usage", "data": {"module": "table"}})
            db.execute("INSERT INTO outbox (id,payload,ready_at) VALUES ('pending',?,?)", (pending, stamp + 300))
        db.close()
        before = path.stat().st_size
        with patch.object(agent, "automatic_report_health") as health:
            agent.work(root, folder, "", single=True)
            health.assert_not_called()
        with agent.ledger(folder) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM report_receipts").fetchone()[0], 15)
            self.assertEqual(tuple(db.execute("SELECT id,payload,accepted FROM outbox").fetchone()),
                             ("pending", pending, None))
        self.assertLess(path.stat().st_size, before // 2)

    def test_project_package_versions_and_older_snapshots(self):
        public, internal, third_party = "com.solotopia.framework", "com.fixture.internal", "com.thirdparty.sdk"
        stamp = int(time.time())
        report = {"version": 1, "id": uuid.uuid4().hex, "workspace": uuid.uuid4().hex,
                  "kind": "snapshot", "observed_at": stamp,
                  "data": {"framework": "0.7.14", "unity": "6000.4.2f1", "tools": {}, "coverage": {},
                           "packages": [{"id": name, "version": "1.0.0", "source": "registry", "evidence": "registered"}
                                        for name in (third_party, internal, public)]}}
        rich = report | {"data": report["data"] | {"codex_hooks": "complete",
                                                   "application_identifiers": {"Android": "com.fixture.game"}}}
        self.assertEqual(self.ingest(rich | {"data": rich["data"] | {"codex_hooks": "authorized"}})[0], 400)
        self.assertEqual(self.ingest(rich | {"data": rich["data"] | {"git_user_name": "invalid\nname"}})[0], 400)
        self.assertEqual(self.ingest(rich | {"data": rich["data"] | {"application_identifiers": {"Android": "/Users/private"}}})[0], 400)
        self.assertEqual(self.ingest(rich)[0], 201)
        legacy = report | {"id": uuid.uuid4().hex, "observed_at": stamp - 1}
        self.assertEqual(self.ingest(legacy)[0], 201)
        session = self.login()
        detail = self.http("/api/maintainer/projects/project-a", headers=session)[1]
        self.assertEqual(detail["snapshots"][0]["data"]["codex_hooks"], "complete")
        self.assertNotIn("codex_hooks", detail["snapshots"][1]["data"])

        catalogs = {REGISTRIES["public"]: {public: "2.0.0"}, REGISTRIES["internal"]: {internal: "1.0.0"}}
        route = "/api/maintainer/projects/project-a/package-versions"
        self.assertEqual(self.http(route)[0], 401)
        with patch("registries.fetch_catalog", side_effect=catalogs.get) as fetch:
            expected = {public: {"registry": "public", "latest": "2.0.0"},
                        internal: {"registry": "internal", "latest": "1.0.0"}}
            self.assertEqual(self.http(route, headers=session)[1]["packages"], expected)
            self.assertEqual(self.http(route, headers=session)[1]["packages"], expected)
            self.assertEqual(fetch.call_count, 2)
            self.server.registry_versions.expires["internal"] = 0
            catalogs[REGISTRIES["internal"]] = None
            expected[internal]["latest"] = None
            self.assertEqual(self.http(route, headers=session)[1]["packages"], expected)

    def test_third_party_versions_match_the_selected_snapshot_unity(self):
        unity_package = "com.unity.fixture"
        applovin = "com.applovin.mediation.adapters.fixture"
        openupm = "com.fixture.public-tool"
        builtin = "com.unity.ugui"
        local = "com.unity.embedded-tool"
        unknown = "com.fixture.project-local"
        missing = "com.unity.unrecognized-version"
        packages = [{"id": name, "version": version, "source": source, "evidence": "registered"}
                    for name, version, source in ((unity_package, "1.9.0", "registry"), (applovin, "1.1.0-beta.2", "registry"),
                                                  (openupm, "1.0.0", "registry"), (builtin, "2.0.0", "builtin"),
                                                  (local, "1.0.0", "embedded"), (unknown, "1.0.0", "registry"),
                                                  (missing, "5.0.0", "registry"))]
        snapshots = {}
        for unity in ("6000.4.2f1", "6000.4.2b9", "2022.3.62f1"):
            identifier = uuid.uuid4().hex
            event = {"version": 1, "id": identifier, "workspace": uuid.uuid4().hex, "kind": "snapshot",
                     "observed_at": int(time.time()), "data": {"framework": "0.7.14", "unity": unity,
                     "tools": {}, "coverage": {}, "packages": packages}}
            self.assertEqual(self.ingest(event)[0], 201)
            snapshots[unity] = identifier

        def manifest(name, releases):
            return {"name": name, "versions": releases}

        metadata = {
            (UNITY_REGISTRY, "/" + unity_package): manifest(unity_package, {
                "1.9.0": {"unity": "2022.3"}, "1.10.0": {"unity": "2022.3", "unityRelease": "62f1"},
                "2.0.0": {"unity": "6000.4", "unityRelease": "2f1"},
                "2.1.0": {"unity": "6000.4", "unityRelease": "3f1"}, "3.0.0-exp.1": {},
            }),
            (UNITY_REGISTRY, "/" + missing): manifest(missing, {"6.0.0": {}}),
            (APPLOVIN_REGISTRY, "/" + applovin): manifest(applovin, {"1.1.0-beta.2": {}, "1.1.0-beta.10": {}}),
            (OPENUPM_REGISTRY, "/-/all"): {openupm: {"dist-tags": {"latest": "1.0.1"}}},
            (OPENUPM_REGISTRY, "/" + openupm): manifest(openupm, {"1.0.0": {}, "1.0.1": {}}),
        }
        path = "/api/maintainer/projects/project-a"
        session = self.login()
        detail = self.http(path, headers=session)[1]
        self.assertEqual({snapshot["id"] for snapshot in detail["snapshots"]}, set(snapshots.values()))
        with patch("registries.registry_json", side_effect=lambda url, route: metadata.get((url, route))) as fetch:
            for unity, latest in (("6000.4.2f1", "2.0.0"), ("6000.4.2b9", "1.10.0"), ("2022.3.62f1", "1.10.0")):
                url = f"{path}/package-versions?snapshot={snapshots[unity]}"
                status, response, _ = self.http(url, headers=session)
                self.assertEqual(status, 200, response)
                self.assertFalse(response["pending"])
                self.assertEqual(response["packages"], {
                    unity_package: {"registry": "third-party", "latest": latest},
                    applovin: {"registry": "third-party", "latest": "1.1.0-beta.10"},
                    openupm: {"registry": "third-party", "latest": "1.0.1"},
                })
            self.assertEqual(self.http(url.replace("/project-a/", "/another-project/"), headers=session)[0], 404)
            self.assertCountEqual([call.args for call in fetch.call_args_list],
                                  [*metadata, *((origin, "/-/all") for origin in REGISTRIES.values())])

    def test_team_edit_survives_upgrade_and_automatic_reports(self):
        project = "auto-" + uuid.uuid4().hex
        token = "a" * 64
        event = self.sample_issue()
        credentials = {"X-Nova-Project": project, "Authorization": "Bearer " + token}
        report = {"name": "示例项目", "report": event}
        self.assertEqual(self.http("/api/v1/auto-reports", report, credentials)[0], 201)
        self.assertEqual(self.database.project_detail(project)["project"]["team"], "")
        self.database.register("manual", "手动项目", "自动登记")
        with self.database.connect() as db:
            db.execute("UPDATE projects SET team='自动登记' WHERE id=?", (project,))
            db.execute("PRAGMA user_version=1")
        reopened = Database(self.database.path)
        self.assertEqual(reopened.project_detail(project)["project"]["team"], "")
        self.assertEqual(reopened.project_detail("manual")["project"]["team"], "自动登记")

        route = f"/api/maintainer/projects/{project}/team"
        update = {"team": "  玩法组  ", "previous_team": ""}
        self.assertEqual(self.http(route, update, credentials)[0], 401)
        session = self.login()
        self.assertEqual(self.http(route, update, session | {"X-Nova-CSRF": ""})[0], 403)
        self.assertEqual(self.http(route, update, session)[1]["team"], "玩法组")
        self.assertEqual(self.http(route, update, session)[0], 200)
        self.assertEqual(self.http(route, update | {"team": "其他组"}, session)[0], 409)

        self.assertEqual(self.http("/api/v1/auto-reports", report | {"report": event | {"id": uuid.uuid4().hex}}, credentials)[0], 201)
        latest = Database(self.database.path).project_detail(project)["project"]
        self.assertEqual((latest["name"], latest["team"]), ("示例项目", "玩法组"))
        self.assertEqual(self.http(route, {"team": "", "previous_team": "玩法组"}, session)[1]["team"], "")

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
