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

from database import Conflict, Database, EnrollmentLimit
from protocol import (
    InvalidInput, PRIORITIES, STATUSES, UUID, enum, int_range, key, label, obj, validate_event,
)
from registries import RegistryVersions


ROOT = Path(__file__).resolve().parent
ACCOUNTS = ("taoye", "yingzheng")
COOKIE = "nova_issues_session"
SESSION_TTL = 8 * 60 * 60
TOKEN = re.compile(r"[A-Za-z0-9_-]{32,128}\Z")
MAX_BODY = 256 * 1024
AUTO_REPORT_NETWORKS = tuple(map(ip_network, ("127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16")))
ASSETS = {
    "/": ("index.html", "text/html; charset=utf-8"),
    "/app.js": ("app.js", "text/javascript; charset=utf-8"),
    "/style.css": ("style.css", "text/css; charset=utf-8"),
}
USAGE_DESCRIPTIONS = ROOT / "usage-descriptions.json"
UNITY_USAGE_DESCRIPTIONS = ROOT / "unity-usage-descriptions.json"


def load_web_assets():
    """一次性读取当前服务提供的 Web 文件。"""
    return {route: ((ROOT / "web" / filename).read_bytes(), content_type)
            for route, (filename, content_type) in ASSETS.items()}


def runtime_revision(assets=None, descriptions=None):
    """用服务端源码和实际加载的 Web 内容生成可核对的版本标识。"""
    digest = hashlib.sha256()
    for name in ("serve.py", "database.py", "protocol.py", "registries.py"):
        digest.update(hashlib.sha256((ROOT / name).read_bytes()).digest())
    if assets is None:
        assets = load_web_assets()
    for content, _ in assets.values():
        digest.update(hashlib.sha256(content).digest())
    if descriptions is None:
        descriptions = load_usage_descriptions()
    digest.update(hashlib.sha256(json.dumps(descriptions, sort_keys=True).encode("utf-8")).digest())
    return digest.hexdigest()


def load_usage_descriptions():
    """读取随服务部署的 Nova 与 Unity CLI 说明。"""
    return json.loads(USAGE_DESCRIPTIONS.read_bytes()) | json.loads(UNITY_USAGE_DESCRIPTIONS.read_bytes())


def usage_description(row, descriptions):
    """按操作编号查找用途，自定义命令展示已知的调用入口。"""
    operation = row["operation"]
    if operation in descriptions:
        return descriptions[operation]
    if row["entry"] == "unity-cli" and operation.startswith("unity."):
        command = " ".join(operation.removeprefix("unity.").rsplit(".", 1))
        return f"通过 Unity CLI 调用「{command}」。具体用途由提供该命令的项目或包定义。"
    return ""


def password_hash(password, salt):
    return hashlib.scrypt(password.encode("utf-8"), salt=salt, n=32768, r=8, p=1, maxmem=64 << 20)


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
        self.revision = runtime_revision(self.assets, self.usage_descriptions)
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

    def session_cookie(self, token, ttl):
        secure = "; Secure" if self.server.tls else ""
        return ("Set-Cookie", f"{COOKIE}={token}; HttpOnly; SameSite=Strict; Path=/; Max-Age={ttl}{secure}")

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
        token, csrf = self.server.database.start_session(username, SESSION_TTL)
        self.reply(200, {"username": username, "csrf": csrf}, headers=(self.session_cookie(token, SESSION_TTL),))

    def do_GET(self):
        self.dispatch("GET")

    def do_POST(self):
        self.dispatch("POST")

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
                             "hook_activity": True, "implementation_tracking": True,
                             "execution_evidence": True, "revision": self.server.revision})
        elif method == "POST" and path == "/api/session":
            self.login()
        elif method == "GET" and path == "/api/session":
            self.reply(200, self.browser_session()[1])
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
            _, session = self.browser_session(mutation=method == "POST")
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
        db = self.server.database
        path = url.path.removeprefix("/api/maintainer/")
        if method == "GET" and path == "overview":
            days = parse_qs(url.query).get("days", ["14"])[0]
            if not days.isdecimal() or len(days) > 3:
                raise InvalidInput("时间范围无效。")
            overview = db.overview(int_range(int(days), 1, 365))
            for row in overview["usage"]:
                row["description"] = usage_description(row, self.server.usage_descriptions)
            self.reply(200, overview)
        elif method == "GET" and path == "issues":
            query = parse_qs(url.query)
            project = query.get("project", [""])[0]
            status = query.get("status", [""])[0]
            priority = query.get("priority", [""])[0]
            issues = db.list_issues(
                project_id=key(project) if project else "",
                status=enum(status, STATUSES) if status else "",
                priority=enum(priority, PRIORITIES) if priority else "",
                search=query.get("q", [""])[0].strip(),
            )
            self.reply(200, {"issues": issues})
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
        elif (match := re.fullmatch(r"issues/([0-9]{1,15})", path)):
            issue_id = int(match[1])
            if method == "GET":
                self.reply_found(db.issue_detail(issue_id))
            else:
                data = obj(self.body(), ("revision", "status", "priority", "assignee", "fixed_version", "note"),
                           ("duplicate_of",))
                int_range(data["revision"], 1, 1000000000)
                status = enum(data["status"], STATUSES)
                enum(data["priority"], PRIORITIES)
                label(data["assignee"], 80, empty=True)
                label(data["fixed_version"], 80, empty=status != "awaiting_verification")
                label(data["note"], 1000, empty=status != "closed")
                if status == "duplicate":
                    int_range(data.get("duplicate_of"), 1, 10 ** 15 - 1)
                if not db.update_issue(issue_id, data, actor):
                    raise HttpError(404, "没有找到对应 Issue。")
                self.reply(200, {"updated": True})
        else:
            raise HttpError(404, "找不到请求的入口。")


def backup(db_path, output):
    if not db_path.is_file():
        raise ValueError("找不到待备份的数据库。")
    output.touch(mode=0o600, exist_ok=False)
    try:
        source = sqlite3.connect(db_path)
        destination = sqlite3.connect(output)
        try:
            source.backup(destination)
        finally:
            destination.close()
            source.close()
    except Exception:
        output.unlink()
        raise


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
                server.serve_forever()
            except KeyboardInterrupt:
                pass
        return 0
    except (OSError, ValueError, TypeError) as exc:
        print(str(exc), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
