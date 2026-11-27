#!/usr/bin/env python3
"""Start the standalone nova-issues-server and manage its local data."""

import argparse
from collections import defaultdict, deque
from concurrent.futures import ThreadPoolExecutor
from getpass import getpass
import hashlib
import hmac
from http import cookies
from http.server import BaseHTTPRequestHandler, HTTPServer
from ipaddress import ip_address, ip_network
import json
import os
from pathlib import Path
import re
import secrets
import signal
import sqlite3
import ssl
import sys
import threading
import time
from urllib.parse import parse_qs, urlsplit

from database import (
    ACTIVITY_SECTIONS, DEVELOPMENT_ENTRIES, MODULE_ALIASES, RECENT_REPORT_DAYS, Conflict, Database, EnrollmentLimit,
)
from maintenance import DatabaseMaintenance, backup
from protocol import (
    InvalidInput, MAINTAINER_ISSUE_STATES, UUID, enum, int_range, key, label, obj, retired_feedback, validate_event,
)
from registries import RegistryVersions


ROOT = Path(__file__).resolve().parent
ACCOUNTS = ("taoye", "yingzheng")
COOKIE = "nova_issues_session"
SESSION_COOKIE_MAX_AGE = 400 * 24 * 60 * 60
TOKEN = re.compile(r"[A-Za-z0-9_-]{32,128}\Z")
MAX_BODY = 256 * 1024
MAX_ACTIVITY_ID = 9007199254740991
AUTO_REPORT_NETWORKS = tuple(map(ip_network, ("127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16")))
ASSETS = {
    "/": ("index.html", "text/html; charset=utf-8"),
    "/app.js": ("app.js", "text/javascript; charset=utf-8"),
    "/style.css": ("style.css", "text/css; charset=utf-8"),
}
USAGE_DESCRIPTIONS = ROOT / "usage-descriptions.json"
UNITY_USAGE_DESCRIPTIONS = ROOT / "unity-usage-descriptions.json"
USAGE_MODULES = ROOT / "usage-modules.json"
USAGE_SOURCES = ("codex", "editor", "legacy")
USAGE_ENTRY_TYPES = (*DEVELOPMENT_ENTRIES, "editor-tool", "play", "skill")
IMPLEMENTATION_USAGE_DESCRIPTIONS = {
    "config-export": "导出 Config 运行配置并记录执行结果。",
    "pipify-batch": "运行整个 Pipify Batch 并记录执行结果。",
    "plugpals-query": "在 PlugPals 查询包仓库并记录查询结果。",
    "bundle-build": "在 Unity Editor 构建标准资源包或原生文件包并记录结果。",
    "config-load": "在 Unity Editor Play 中成功读取 Config 运行配置。",
    "editor-module": "记录 Unity Editor Play 中模块成功调用的上报时间和次数。",
}
NOVA_CLI_MODULES = {
    "nova_table_source": "table", "nova_tracks_source": "tracks", "nova_excel_template": "excel",
    "nova_capabilities": "toolchain", "nova_skills": "toolchain",
}
INSPECTOR_EXPORTS = {
    "table": ("code", "data", "all"), "localization": ("text", "font"), "ui": ("code", "data", "all"),
    "sound": ("code", "data", "all"),
    "vibrate": ("emphasis-code", "emphasis-data", "emphasis-all", "custom-code", "custom-data", "custom-all"),
    "network": ("hostkey-code", "hostkey-data", "hostkey-all", "netcmd-code", "netcmd-data", "netcmd-all",
                "proto-file", "proto-all"),
}


def load_web_assets():
    """一次性读取当前服务提供的 Web 文件。"""
    return {route: ((ROOT / "web" / filename).read_bytes(), content_type)
            for route, (filename, content_type) in ASSETS.items()}


def runtime_revision(assets=None, descriptions=None, modules=None):
    """用服务端源码和实际加载的 Web 内容生成可核对的版本标识。"""
    digest = hashlib.sha256()
    for name in ("serve.py", "database.py", "maintenance.py", "protocol.py", "registries.py"):
        digest.update(hashlib.sha256((ROOT / name).read_bytes()).digest())
    if assets is None:
        assets = load_web_assets()
    for content, _ in assets.values():
        digest.update(hashlib.sha256(content).digest())
    if descriptions is None:
        descriptions = load_usage_descriptions()
    if modules is None:
        modules = load_usage_modules()
    digest.update(hashlib.sha256(json.dumps([descriptions, modules], sort_keys=True).encode("utf-8")).digest())
    return digest.hexdigest()


def load_usage_descriptions():
    """读取随服务部署的 Nova 与 Unity CLI 说明。"""
    return json.loads(USAGE_DESCRIPTIONS.read_bytes()) | json.loads(UNITY_USAGE_DESCRIPTIONS.read_bytes())


def load_usage_modules():
    """读取随维护台部署的 Framework 模块导航。"""
    return json.loads(USAGE_MODULES.read_bytes())


def usage_description(row, descriptions):
    """按操作编号查找用途，自定义命令展示已知的调用入口。"""
    operation = row["operation"]
    if row["entry"] in ("editor-tool", "play"):
        return IMPLEMENTATION_USAGE_DESCRIPTIONS.get(operation, "")
    if operation in descriptions:
        return descriptions[operation]
    if row["entry"] == "inspector":
        module = row["module"]
        name = {"table": "Table", "localization": "本地化", "ui": "UI", "network": "网络",
                "sound": "声音", "vibrate": "振动"}.get(module)
        scope = operation.removeprefix(f"editor.{module}.export-")
        label = {"code": "代码", "data": "数据", "all": "代码和数据", "text": "本地化文本",
                 "font": "本地化字体", "hostkey-code": "域名表代码", "hostkey-data": "域名表数据",
                 "hostkey-all": "域名表代码和数据", "netcmd-code": "指令表代码", "netcmd-data": "指令表数据",
                 "netcmd-all": "指令表代码和数据", "proto-file": "单份协议的 C# 类型",
                 "proto-all": "已配置协议的 C# 类型", "emphasis-code": "强调振动代码",
                 "emphasis-data": "强调振动数据", "emphasis-all": "强调振动代码和数据",
                 "custom-code": "自定义振动代码", "custom-data": "自定义振动数据",
                 "custom-all": "自定义振动代码和数据"}.get(scope)
        if name and label:
            return f"在 Unity 的{name}检视面板手动导出{label}。"
    if row["entry"] == "unity-cli" and operation.startswith("unity."):
        command = " ".join(operation.removeprefix("unity.").rsplit(".", 1))
        return f"通过 Unity CLI 调用「{command}」。具体用途由提供该命令的项目或包定义。"
    if row["entry"] == "unity-mcp":
        return f"通过 Unity MCP 调用「{operation.removeprefix('unity-mcp.')}」。"
    return ""


def usage_breakdown(rows, descriptions, module_names, selected_module):
    """按操作明细的上报条数及调用次数汇总各维度。"""
    def category(identifier, **metadata):
        return {"id": identifier, "count": 0, "history": 0, **metadata}

    def module_category(identifier, *, tracked=False, **metadata):
        return category(identifier, tracked=tracked, development_calls=0, play_calls=None, **metadata)

    modules = {MODULE_ALIASES.get(name, name): module_category(MODULE_ALIASES.get(name, name), label=label)
               for name, label in module_names.items()}
    modules["unity-mcp"] = module_category("unity-mcp", label="Unity MCP", tracked=True)
    operations = {}
    sources = {name: category(name) for name in USAGE_SOURCES}
    entries = {name: category(name) for name in (*USAGE_ENTRY_TYPES, "")}
    for operation, description in descriptions.items():
        if retired_feedback("usage", {"operation": operation}):
            continue
        operations[operation] = category(operation, description=description)
        parts = operation.split(".")
        domain = NOVA_CLI_MODULES.get(parts[0])
        if operation.startswith("nova.project.") and len(parts) >= 4:
            domain = parts[2]
        elif operation.startswith("unity."):
            domain = "unity-cli"
        if domain:
            domain = MODULE_ALIASES.get(domain, domain)
            modules.setdefault(domain, module_category(domain))["tracked"] = True

    for domain, scopes in INSPECTOR_EXPORTS.items():
        for scope in scopes:
            operation = f"editor.{domain}.export-{scope}"
            description = usage_description({"operation": operation, "module": domain, "entry": "inspector"}, descriptions)
            operations[operation] = category(operation, description=description)
        domain = MODULE_ALIASES.get(domain, domain)
        modules.setdefault(domain, module_category(domain))["tracked"] = True

    for report in rows:
        domain = MODULE_ALIASES.get(report["module"], report["module"])
        operation = report["operation"]
        module = modules.setdefault(domain, module_category(domain))
        if operation not in operations:
            operations[operation] = category(operation, description=usage_description(report, descriptions))
        categories = [
            operations[operation],
            sources.setdefault(report["source"], category(report["source"])),
            entries.setdefault(report["entry"], category(report["entry"])),
        ]
        if report["entry"] in (*DEVELOPMENT_ENTRIES, "editor-tool"):
            module["tracked"] = True
            categories.append(module)
        module["development_calls"] += report["development_calls"]
        if report["entry"] == "editor-tool" and report["history"]:
            module["tool_history"] = True
        if report["entry"] == "play" and report["history"]:
            module["play_history"] = True
            if report["play_calls"] is not None:
                module["play_calls"] = (module["play_calls"] or 0) + report["play_calls"]
        for item in categories:
            item["count"] += report["count"]
            item["history"] += report["history"]

    return {
        "total": sum(row["count"] for row in operations.values()),
        "calls": sum(row["count"] for row in modules.values()),
        "development_calls": sum(row["development_calls"] for row in modules.values()),
        "play_calls": sum(row["play_calls"] or 0 for row in modules.values()),
        "selected_module": MODULE_ALIASES.get(selected_module, selected_module),
        "skill_turns": entries["skill"]["count"],
        "dimensions": {name: list(items.values()) for name, items in
                       (("module", modules), ("operation", operations), ("source", sources), ("entry", entries))},
    }


def activity_positions(query, suffix):
    """读取浏览器按栏目保存的接收编号或旧版已读数量。"""
    positions = {}
    for section in ACTIVITY_SECTIONS:
        values = query.get(f"{section}_{suffix}")
        if values is None:
            continue
        if len(values) != 1 or len(values[0]) > 16 or not values[0].isdecimal():
            raise InvalidInput("已读进度无效。")
        positions[section] = int_range(int(values[0]), 0, MAX_ACTIVITY_ID)
    return positions


def unread_filter(value):
    """校验本浏览器的已读进度和最近收到的上报位置，仅供本次列表查询。"""
    obj(value, ("after", "through", "viewed"))
    if not isinstance(value["viewed"], list):
        raise InvalidInput("已读进度无效。")
    return {
        "unread_after": int_range(value["after"], 0, MAX_ACTIVITY_ID),
        "unread_through": int_range(value["through"], 0, MAX_ACTIVITY_ID),
        "unread_viewed": json.dumps([int_range(item, 1, MAX_ACTIVITY_ID) for item in value["viewed"]]),
    }


def requested_page(query, default_size=50):
    """接受维护台使用的页大小，兼容没有分页参数的旧请求。"""
    if "page" not in query and "page_size" not in query:
        return None
    number = query.get("page", ["1"])
    size = query.get("page_size", [str(default_size)])
    if len(number) != 1 or not re.fullmatch(r"[0-9]{1,10}", number[0]) or size not in (["10"], ["50"], ["100"], ["200"]):
        raise InvalidInput("页码或每页条数无效。")
    return int_range(int(number[0]), 1, 2147483647), int(size[0])


def password_hash(password, salt):
    return hashlib.scrypt(password.encode("utf-8"), salt=salt, n=32768, r=8, p=1, maxmem=64 << 20)


def account_version(salt, password_digest):
    """以当前账号的凭据标记验证长期会话。"""
    return hashlib.sha256(salt + password_digest).hexdigest()


def read_accounts(path):
    values = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(values, dict) or set(values) != set(ACCOUNTS):
        raise ValueError("账号文件必须恰好包含服务的两个内置账号。")
    result = {}
    for name, value in values.items():
        obj(value, ("salt", "hash"))
        salt, hashed = bytes.fromhex(value["salt"]), bytes.fromhex(value["hash"])
        if len(salt) != 16 or len(hashed) != 64:
            raise ValueError("账号文件中的密码摘要格式无效。")
        result[name] = (salt, hashed)
    return result


def seed_accounts(path):
    if path.exists():
        raise ValueError("账号文件已存在。请为新部署选择独立路径。")
    result = {}
    for user in ACCOUNTS:
        password = getpass(f"{user} 的密码：")
        if len(password) < 12 or len(password) > 256:
            raise ValueError("密码长度须介于 12 到 256 字符。")
        salt = secrets.token_bytes(16)
        result[user] = {"salt": salt.hex(), "hash": password_hash(password, salt).hex()}
    path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    with os.fdopen(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), "w", encoding="utf-8") as file:
        json.dump(result, file, indent=2)
        file.write("\n")


class LoginLimits:
    def __init__(self):
        self.attempts = defaultdict(deque)
        self.lock = threading.Lock()

    def allow(self, address):
        stamp = time.monotonic()
        with self.lock:
            if len(self.attempts) >= 1024:
                self.attempts = defaultdict(deque, {
                    ip: events for ip, events in self.attempts.items() if events and stamp - events[-1] < 90
                })
                if len(self.attempts) >= 1024 and address not in self.attempts:
                    return False
            entries = self.attempts[address]
            while entries and stamp - entries[0] >= 90:
                entries.popleft()
            if len(entries) >= 8:
                return False
            entries.append(stamp)
            return True

    def clear(self, address):
        with self.lock:
            self.attempts.pop(address, None)


class NovaIssuesServer(HTTPServer):
    request_queue_size = 64

    def __init__(self, address, database, accounts, tls=None):
        self.database = database
        self.accounts = accounts
        self.tls = tls
        self.login_limits = LoginLimits()
        self.enrollment_limits = LoginLimits()
        self.registry_versions = RegistryVersions()
        self.slots = threading.BoundedSemaphore(32)
        self.pool = ThreadPoolExecutor(max_workers=32)
        self.assets = load_web_assets()
        self.usage_descriptions = load_usage_descriptions()
        self.usage_modules = load_usage_modules()
        self.revision = runtime_revision(self.assets, self.usage_descriptions, self.usage_modules)
        self.maintenance = DatabaseMaintenance(database)
        super().__init__(address, Handler)

    def process_request(self, request, client_address):
        if not self.slots.acquire(blocking=False):
            self.close_request(request)
            return
        self.pool.submit(self.serve_request, request, client_address)

    def serve_request(self, request, client_address):
        try:
            request.settimeout(8)
            if self.tls:
                request = self.tls.wrap_socket(request, server_side=True)
            self.finish_request(request, client_address)
        except (OSError, TimeoutError):
            pass
        finally:
            self.shutdown_request(request)
            self.slots.release()

    def server_close(self):
        super().server_close()
        self.maintenance.close()
        self.pool.shutdown()
        self.registry_versions.close()


class HttpError(Exception):
    def __init__(self, status, message):
        super().__init__(message)
        self.status = status


class Handler(BaseHTTPRequestHandler):
    server_version = "nova-issues-server"
    sys_version = ""

    def log_message(self, _format, *args):
        pass

    def reply(self, status, payload, content_type="application/json; charset=utf-8", headers=()):
        content = payload if isinstance(payload, bytes) else json.dumps(
            payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(content)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("X-Frame-Options", "DENY")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("Content-Security-Policy",
                         "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'")
        for name, value in headers:
            self.send_header(name, value)
        self.end_headers()
        self.wfile.write(content)

    def body(self):
        if self.headers.get("Transfer-Encoding") or self.headers.get_content_type() != "application/json":
            raise HttpError(415, "请使用 JSON 请求。")
        length = self.headers.get("Content-Length", "")
        if not length.isdecimal() or len(length) > 7 or not 1 <= int(length) <= MAX_BODY:
            raise HttpError(413, "请求体缺失或超限。")
        try:
            return json.loads(self.rfile.read(int(length)))
        except (ValueError, RecursionError) as exc:
            raise InvalidInput("JSON 格式无效。") from exc

    def project_credentials(self):
        project = self.headers.get("X-Nova-Project", "")
        scheme, sep, token = self.headers.get("Authorization", "").partition(" ")
        if scheme.lower() != "bearer" or not sep or not TOKEN.fullmatch(token):
            raise HttpError(401, "需要项目上报凭据。")
        return key(project), token

    def browser_session(self, *, mutation=False):
        try:
            cookie = cookies.SimpleCookie(self.headers.get("Cookie", ""))
            token = cookie[COOKIE].value if COOKIE in cookie else ""
        except cookies.CookieError:
            token = ""
        session = self.server.database.session(token) if TOKEN.fullmatch(token) else None
        if not session:
            raise HttpError(401, "请先登录。")
        account = self.server.accounts.get(session["username"])
        version = session["account_version"]
        if account is None or (version is not None and not hmac.compare_digest(version, account_version(*account))):
            raise HttpError(401, "请先登录。")
        if mutation:
            self.require_origin()
            csrf = self.headers.get("X-Nova-CSRF", "")
            if not TOKEN.fullmatch(csrf) or not hmac.compare_digest(csrf, session["csrf"]):
                raise HttpError(403, "会话校验失败，请刷新页面。")
        return token, session

    def require_origin(self):
        host = self.headers.get("Host", "")
        scheme = "https" if self.server.tls else "http"
        if not host or self.headers.get("Origin", "") != f"{scheme}://{host}":
            raise HttpError(403, "请求必须来自本服务页面。")

    def session_cookie(self, token, max_age):
        secure = "; Secure" if self.server.tls else ""
        return ("Set-Cookie", f"{COOKIE}={token}; HttpOnly; SameSite=Strict; Path=/; Max-Age={max_age}{secure}")

    def login(self):
        self.require_origin()
        data = obj(self.body(), ("username", "password"))
        username, password = data["username"], data["password"]
        if not isinstance(username, str) or len(username) > 80 or not isinstance(password, str) or len(password) > 256:
            raise InvalidInput("账号或密码格式无效。")
        address = self.client_address[0]
        if not self.server.login_limits.allow(address):
            raise HttpError(429, "登录尝试过多，请稍后再试。")
        salt, wanted = self.server.accounts.get(username, (bytes(16), bytes(64)))
        accepted = hmac.compare_digest(password_hash(password, salt), wanted)
        if not accepted or username not in self.server.accounts:
            raise HttpError(401, "账号或密码错误。")
        self.server.login_limits.clear(address)
        token, csrf = self.server.database.start_session(username, account_version(salt, wanted))
        self.reply(200, {"username": username, "csrf": csrf},
                   headers=(self.session_cookie(token, SESSION_COOKIE_MAX_AGE),))

    def do_GET(self):
        self.dispatch("GET")

    def do_POST(self):
        self.dispatch("POST")

    def do_DELETE(self):
        self.dispatch("DELETE")

    def dispatch(self, method):
        try:
            self.route(method, urlsplit(self.path))
        except HttpError as exc:
            self.reply(exc.status, {"error": str(exc)})
        except (InvalidInput, UnicodeError) as exc:
            self.reply(400, {"error": str(exc) if isinstance(exc, InvalidInput) else "请求编码无效。"})
        except Conflict as exc:
            self.reply(409, {"error": str(exc)})
        except EnrollmentLimit:
            self.reply(429, {"error": "自动登记请求过多，请稍后重试。"})
        except PermissionError:
            self.reply(401, {"error": "项目凭据无效。"})
        except sqlite3.Error:
            self.reply(503, {"error": "数据库暂时不可用。"})

    def route(self, method, url):
        path = url.path
        db = self.server.database
        if method == "GET" and path in self.server.assets:
            content, content_type = self.server.assets[path]
            self.reply(200, content, content_type)
        elif method == "GET" and path == "/health":
            self.reply(200, {"status": "ok", "service": "nova-issues-server", "protocol": 1,
                             "automatic_reporting": self.allows_automatic_reporting(), "codex_telemetry": True,
                             "project_snapshot_details": True, "git_user_name_reporting": True,
                             "snapshot_operating_system": True, "snapshot_version_and_hook_counts": True,
                             "hook_activity": True, "implementation_tracking": True,
                             "execution_evidence": True, "feedback_timings": True, "play_call_counts": True,
                             "compact_reports": True, "unity_mcp_monitoring": True,
                             "workflow_monitoring": True, "database_maintenance": self.server.maintenance.status(),
                             "revision": self.server.revision})
        elif method == "POST" and path == "/api/session":
            self.login()
        elif method == "GET" and path == "/api/session":
            token, session = self.browser_session()
            headers = (self.session_cookie(token, SESSION_COOKIE_MAX_AGE),) if session["account_version"] else ()
            self.reply(200, {"username": session["username"], "csrf": session["csrf"]}, headers=headers)
        elif method == "POST" and path == "/api/session/logout":
            token, _ = self.browser_session(mutation=True)
            db.end_session(token)
            self.reply(200, {"ok": True}, headers=(self.session_cookie("", 0),))
        elif method == "POST" and path == "/api/v1/reports":
            project, token = self.project_credentials()
            event = validate_event(self.body())
            self.report_receipt(event, db.ingest(project, event, token))
        elif method == "POST" and path == "/api/v1/auto-reports":
            self.automatic_report()
        elif method == "GET" and (match := re.fullmatch(r"/api/v1/issues/([0-9]{1,15})", path)):
            project, token = self.project_credentials()
            if not db.authorize_project(project, token):
                raise HttpError(401, "项目凭据无效。")
            self.reply_found(db.project_issue(project, int(match[1])))
        elif path.startswith("/api/maintainer/"):
            _, session = self.browser_session(mutation=method != "GET")
            self.maintainer(method, url, session["username"])
        else:
            raise HttpError(404, "找不到请求的入口。")

    def allows_automatic_reporting(self):
        """根据实际 TCP 对端判断，代理请求头不参与上报来源校验。"""
        address = ip_address(self.client_address[0])
        return any(address in network for network in AUTO_REPORT_NETWORKS)

    def automatic_report(self):
        """接收内网工作区的首次及后续上报，权限只限于写入自身记录。"""
        if not self.allows_automatic_reporting():
            raise HttpError(403, "自动上报只对局域网开放。")
        project, token = self.project_credentials()
        if not project.startswith("auto-") or not UUID.fullmatch(project[5:]):
            raise InvalidInput("自动登记项目标识无效。")
        data = obj(self.body(), ("name", "report"))
        name = label(data["name"], 100)
        event = validate_event(data["report"])
        self.report_receipt(event, self.server.database.ingest(
            project, event, token, automatic_name=name,
            allow_registration=lambda: self.server.enrollment_limits.allow(self.client_address[0]),
        ))

    def report_receipt(self, event, result):
        """返回约定回执，记录已存在时也带回原接收时间。"""
        created, issue_id, received = result
        self.reply(201 if created else 200, {
            "accepted": True, "id": event["id"], "issue_id": issue_id, "received_at": received,
        })

    def reply_found(self, result):
        if result is None:
            raise HttpError(404, "没有找到对应记录。")
        self.reply(200, result)

    def maintainer(self, method, url, actor):
        """处理使用维护账号会话授权的查询和修改。"""
        db = self.server.database
        path = url.path.removeprefix("/api/maintainer/")
        if ((method == "GET" and path in ("overview", "usage", "usage-analysis", "usage-occurrences")) or
                (method == "POST" and path in ("usage", "usage-analysis"))):
            query = parse_qs(url.query)
            days_value = query.get("days", ["14"])[0]
            if not days_value.isdecimal() or len(days_value) > 3:
                raise InvalidInput("时间范围无效。")
            days = int_range(int(days_value), 1, RECENT_REPORT_DAYS)
            if path == "overview":
                include_lists = query.get("lists") != ["0"]
                if query.get("activity") == ["1"]:
                    result = db.overview(days, activity_after=activity_positions(query, "after"),
                                         previous_read=activity_positions(query, "seen"), include_lists=include_lists)
                else:
                    result = db.overview(days, include_lists=include_lists)
            elif path == "usage-occurrences":
                activity_key = query.get("key", [""])[0]
                if not activity_key or len(activity_key) > 1400:
                    raise InvalidInput("操作标识无效。")
                occurrences, pagination = db.list_usage_occurrences(days, activity_key, requested_page(query) or (1, 50))
                self.reply(200, {"usage-occurrences": occurrences, "pagination": pagination})
                return
            else:
                project = query.get("project", [""])[0]
                module = query.get("module", [""])[0]
                entry = enum(query.get("entry", [""])[0], ("", *USAGE_ENTRY_TYPES, "legacy"))
                scope = {
                    "project_id": key(project) if project else "",
                    "module": key(module) if module else "",
                    "source": enum(query.get("source", [""])[0], ("", *USAGE_SOURCES)),
                    "entry": "" if entry == "legacy" else entry or None,
                }
                unread = unread_filter(self.body()) if method == "POST" else None
                if path == "usage-analysis":
                    rows = db.usage_analysis(days, unread=unread, **scope)
                    breakdown = usage_breakdown(rows, self.server.usage_descriptions,
                                                self.server.usage_modules, scope["module"])
                    self.reply(200, breakdown)
                    return
                usage, pagination = db.list_usage(
                    days, sort=enum(query.get("sort", ["count"])[0], ("count", "latest")),
                    page=requested_page(query), unread=unread,
                    **scope,
                )
                result = {"usage": usage}
                if pagination is not None:
                    result["pagination"] = pagination
            for row in result.get("usage", []):
                row["description"] = usage_description(row, self.server.usage_descriptions)
            self.reply(200, result)
        elif method in ("GET", "POST") and path == "issues":
            query = parse_qs(url.query)
            project = query.get("project", [""])[0]
            status = query.get("status", [""])[0]
            issues, pagination = db.list_issues(
                project_id=key(project) if project else "",
                status=enum(status, MAINTAINER_ISSUE_STATES) if status else "",
                search=query.get("q", [""])[0].strip(),
                sort=enum(query.get("sort", [""])[0], ("", "latest", "count")),
                page=requested_page(query),
                unread=unread_filter(self.body()) if method == "POST" else None,
            )
            result = {"issues": issues}
            if pagination is not None:
                result["pagination"] = pagination
            self.reply(200, result)
        elif method == "POST" and path == "projects":
            data = obj(self.body(), ("id", "name", "team"))
            project_id = key(data["id"])
            token = db.register(project_id, label(data["name"], 100), label(data["team"], 100, empty=True))
            self.reply(201, {"id": project_id, "token": token})
        elif method == "POST" and (match := re.fullmatch(r"projects/([^/]+)/rotate", path)):
            token = db.rotate(key(match[1]))
            self.reply_found({"id": match[1], "token": token} if token else None)
        elif method == "POST" and (match := re.fullmatch(r"projects/([^/]+)/team", path)):
            data = obj(self.body(), ("team", "previous_team"))
            team = label(data["team"], 100, empty=True)
            previous = label(data["previous_team"], 100, empty=True)
            self.reply_found(db.update_team(key(match[1]), team, previous))
        elif method == "GET" and (match := re.fullmatch(r"projects/([^/]+)", path)):
            self.reply_found(db.project_detail(key(match[1])))
        elif method == "GET" and (match := re.fullmatch(r"projects/([^/]+)/snapshots", path)):
            page = requested_page(parse_qs(url.query), default_size=10) or (1, 10)
            self.reply_found(db.list_project_snapshots(key(match[1]), page))
        elif method == "GET" and (match := re.fullmatch(r"projects/([^/]+)/workflow", path)):
            query = parse_qs(url.query)
            value = query.get("trace", [""])[0]
            rows, pagination = db.list_workflow(key(match[1]), key(value, UUID) if value else "",
                                                requested_page(query) or (1, 50))
            self.reply(200, {"workflow": rows, "pagination": pagination})
        elif method == "DELETE" and (match := re.fullmatch(r"projects/([^/]+)", path)):
            self.reply_found({"deleted": True} if db.delete_project(key(match[1])) else None)
        elif method == "GET" and (match := re.fullmatch(r"projects/([^/]+)/package-versions", path)):
            project_id = key(match[1])
            snapshot_ids = parse_qs(url.query, keep_blank_values=True).get("snapshot")
            if snapshot_ids:
                snapshot = db.project_snapshot(project_id, key(snapshot_ids[0], UUID))
                if snapshot is None:
                    raise HttpError(404, "没有找到对应环境快照。")
                packages, pending = self.server.registry_versions.for_snapshot(snapshot)
                self.reply(200, {"packages": packages, "pending": pending})
            else:
                detail = db.project_detail(project_id)
                if detail is None:
                    raise HttpError(404, "没有找到对应记录。")
                packages = {package["id"] for snapshot in detail["snapshots"] for package in snapshot["data"]["packages"]}
                self.reply(200, {"packages": self.server.registry_versions.for_packages(packages)})
        elif method == "GET" and (match := re.fullmatch(r"issues/([0-9]{1,15})/projects", path)):
            self.reply_found(db.issue_projects(int(match[1])))
        elif (match := re.fullmatch(r"issues/([0-9]{1,15})", path)):
            issue_id = int(match[1])
            if method == "GET":
                detail = db.issue_detail(issue_id)
                self.reply_found(None if detail is None else detail | {"assignees": sorted(self.server.accounts)})
            elif method == "DELETE":
                self.reply_found({"deleted": True} if db.delete_issue(issue_id) else None)
            elif method == "POST":
                data = obj(self.body(), ("revision", "status", "assignee", "note"))
                int_range(data["revision"], 1, 1000000000)
                enum(data["status"], MAINTAINER_ISSUE_STATES)
                data["assignee"] = label(data["assignee"], 80, empty=True)
                if data["assignee"] and data["assignee"] not in self.server.accounts:
                    raise InvalidInput("请从现有账号中选择负责人。")
                data["note"] = label(data["note"], 1000, empty=True)
                if not db.update_issue(issue_id, data, actor):
                    raise HttpError(404, "没有找到对应 Issue。")
                self.reply(200, {"updated": True})
        else:
            raise HttpError(404, "找不到请求的入口。")


def stop_on_signal(_signum, _frame):
    """退出请求循环，让服务关闭监听并等待已开始的请求。"""
    raise KeyboardInterrupt


def main():
    parser = argparse.ArgumentParser(description="nova-issues-server")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8787)
    parser.add_argument("--data-dir", type=Path, default=ROOT / "data")
    parser.add_argument("--accounts", type=Path, default=ROOT / ".local" / "accounts.json")
    parser.add_argument("--init-accounts", action="store_true")
    parser.add_argument("--backup", type=Path)
    parser.add_argument("--tls-cert", type=Path)
    parser.add_argument("--tls-key", type=Path)
    args = parser.parse_args()
    try:
        if args.init_accounts:
            seed_accounts(args.accounts)
            print("两个账号的密码摘要已保存。")
            return 0
        db_path = args.data_dir / "nova-issues.sqlite3"
        if args.backup:
            backup(db_path, args.backup)
            print("数据库备份完成。")
            return 0
        tls = None
        if bool(args.tls_cert) != bool(args.tls_key):
            raise ValueError("TLS 证书与私钥路径需同时提供。")
        if args.tls_cert:
            tls = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
            tls.minimum_version = ssl.TLSVersion.TLSv1_2
            tls.load_cert_chain(args.tls_cert, args.tls_key)
        if not args.accounts.is_file():
            raise ValueError("缺少部署用账号文件。请复制预置账号文件，或使用 --init-accounts。")
        accounts = read_accounts(args.accounts)
        with NovaIssuesServer((args.host, args.port), Database(db_path), accounts, tls) as server:
            try:
                signal.signal(signal.SIGTERM, stop_on_signal)
                print(f"nova-issues-server listening on {server.server_address[0]}:{server.server_port}", flush=True)
                server.maintenance.start()
                server.serve_forever()
            except KeyboardInterrupt:
                pass
        return 0
    except (OSError, ValueError, TypeError) as exc:
        print(str(exc), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
