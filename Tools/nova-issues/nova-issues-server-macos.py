#!/usr/bin/env python3
"""在 macOS 上注册、启动或更新 nova-issues-server。"""

import os
from pathlib import Path
import re
import sys


SERVICE_DIR = Path(__file__).resolve().parent
NOVA_ROOT = SERVICE_DIR.parents[1]
ISSUES_LABEL = "com.solotopia.nova-issues-server"
ISSUES_PORT = 8787

# 直接执行脚本时从 Nova 的 Tools 目录加载共用启动工具。
sys.dont_write_bytecode = True
sys.path.insert(0, str(SERVICE_DIR.parent / "_shared"))

import macos_launchd
from serve import read_accounts, runtime_revision


def check_revision(revision):
    """核对 Nova Issues 已就绪并返回是否加载了当前版本。"""
    payload = macos_launchd.probe_json("127.0.0.1", ISSUES_PORT, "/health")
    if isinstance(payload, dict) and payload.get("service") == "nova-issues-server" and payload.get("status") == "ok":
        return payload.get("revision") == revision
    return None


def matches_issues_process(pid, process, script):
    """确认是当前 Nova 目录内以正常启动参数运行的 Python 服务。"""
    python = r"python(?:3(?:\.\d+)?t?)?"
    match = re.fullmatch(rf"(?:.*/)?{python}(?: -[Bu]+)* (.+?serve\.py)(?: (.*))?",
                         process.arguments, re.IGNORECASE)
    if not match or not re.fullmatch(python, process.executable, re.IGNORECASE):
        return False
    # 带自定义账号、数据目录等参数的实例不应切换到默认数据。
    flags = (match[2] or "").split()
    while flags:
        key, separator, value = flags.pop(0).partition("=")
        if not separator and flags:
            value = flags.pop(0)
        if key == "--port" and value == str(ISSUES_PORT):
            continue
        if key != "--host" or not value or value.startswith("-"):
            return False
    script_path = Path(match[1])
    if not script_path.is_absolute():
        result = macos_launchd.command(
            macos_launchd.LSOF, "-nP", "-a", "-p", str(pid), "-d", "cwd", "-Fn",
            admin=process.uid != os.geteuid(), required=False,
        )
        directory = re.search(r"^n(/.+)$", result.stdout, re.MULTILINE)
        if not directory:
            return False
        script_path = Path(directory[1]) / script_path
    return script_path.resolve() == Path(script).resolve()


def main():
    """用当前仓库的服务文件注册并启动 Nova Issues。"""
    account = macos_launchd.deployment_account()
    script = SERVICE_DIR / "serve.py"
    accounts = SERVICE_DIR / ".local" / "accounts.json"
    for path in (script, accounts):
        if not path.is_file() or not os.access(path, os.R_OK):
            raise RuntimeError(f"部署账号无法读取 {path}，请先准备该文件。")
    read_accounts(accounts)
    revision = runtime_revision()
    config = macos_launchd.daemon_config(
        account, ISSUES_LABEL, NOVA_ROOT,
        [sys.executable, "-B", str(script), "--host", "0.0.0.0", "--port", str(ISSUES_PORT)],
    )

    def matches_process(pid, process, _addresses):
        """确认占用端口的是当前仓库启动的 Nova Issues。"""
        return matches_issues_process(pid, process, script)

    # 旧版安装沿用其 PATH，其他配置逐项核对。
    macos_launchd.install_service(
        config, "0.0.0.0", ISSUES_PORT, matches_process,
        check_version=lambda: check_revision(revision), keep_installed_path=True,
    )
    print(f"nova-issues-server 运行版本 {revision[:12]}。", flush=True)


if __name__ == "__main__":
    sys.exit(macos_launchd.run_entrypoint(main))
