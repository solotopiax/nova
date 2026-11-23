"""Nova 服务共用的 macOS 开机自启、进程接管和就绪检查。"""

import fcntl
from http.client import HTTPConnection, HTTPException, HTTPSConnection
import json
import os
from pathlib import Path
import plistlib
import pwd
import re
import signal
import socket
import subprocess
import sys
from tempfile import TemporaryDirectory
import time
from typing import NamedTuple
from xml.parsers.expat import ExpatError


DAEMON_DIR = Path("/Library/LaunchDaemons")
LAUNCHCTL = "/bin/launchctl"
SUDO = "/usr/bin/sudo"
LSOF = "/usr/sbin/lsof"
DEFAULT_SEARCH_PATH = ("/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", "/bin", "/usr/sbin", "/sbin")


def deployment_account():
    """核对系统和部署账号，权限提升仅用于系统注册操作。"""
    if sys.platform != "darwin" or sys.version_info < (3, 10):
        raise RuntimeError("请在目标 Mac 上使用 Python 3.10 或更新版本运行。")
    if os.geteuid() == 0:
        raise RuntimeError("请直接以部署账号运行脚本。脚本会在注册时请求 sudo 权限。")
    return pwd.getpwuid(os.getuid())


def daemon_config(account, label, directory, arguments, *, search_path=()):
    """用稳定的自启标识、日志位置和宿主环境生成系统服务配置。"""
    log_dir = Path(account.pw_dir) / "Library" / "Logs" / "nova-services"
    path = os.pathsep.join(dict.fromkeys((*search_path, *DEFAULT_SEARCH_PATH)))
    return {
        "Label": label,
        "ProgramArguments": arguments,
        "WorkingDirectory": str(directory),
        "UserName": account.pw_name,
        "EnvironmentVariables": {"HOME": account.pw_dir, "PATH": path},
        "RunAtLoad": True,
        "KeepAlive": True,
        "StandardOutPath": str(log_dir / f"{label}.out.log"),
        "StandardErrorPath": str(log_dir / f"{label}.err.log"),
    }


class Process(NamedTuple):
    """记录进程身份，发送退出信号前据此重新核对 PID。"""

    uid: int
    started: str
    executable: str
    arguments: str


def command(*args, admin=False, required=True):
    """执行 macOS 管理命令，按需使用已经验证的 sudo 凭据。"""
    invocation = [SUDO, "-n", *args] if admin else args
    result = subprocess.run(invocation, capture_output=True, text=True, timeout=15)
    if required and result.returncode:
        detail = result.stderr.strip() or result.stdout.strip()
        raise RuntimeError(f"{' '.join(map(str, args))} 执行失败\n{detail}")
    return result


def service_state(label):
    """读取 launchd 中指定系统服务的当前状态。"""
    result = command(LAUNCHCTL, "print", f"system/{label}", required=False)
    return result.stdout if result.returncode == 0 else ""


def service_pid(label):
    """返回本脚本对应的 launchd 服务进程 PID。"""
    match = re.search(r"^\s*pid = (\d+)$", service_state(label), re.MULTILINE)
    return int(match[1]) if match else None


def verify_existing(config, destination, *, keep_installed_path=False):
    """核对已安装的配置和系统来源，允许调用方沿用旧版的搜索路径。"""
    label = config["Label"]
    if destination.is_symlink() or (destination.exists() and not destination.is_file()):
        raise RuntimeError(f"服务配置路径已被占用，请先核对 {destination}。")
    if destination.is_file():
        installed = plistlib.loads(destination.read_bytes())
        expected = config
        if keep_installed_path and isinstance(installed, dict):
            environment = installed.get("EnvironmentVariables")
            path = environment.get("PATH") if isinstance(environment, dict) else None
            if isinstance(path, str):
                folders = path.split(os.pathsep)
                legacy_path = list(dict.fromkeys((*folders[:2], *DEFAULT_SEARCH_PATH)))
                if folders == legacy_path and all(Path(folder).is_absolute() for folder in folders[:2]):
                    expected = config | {"EnvironmentVariables": config["EnvironmentVariables"] | {"PATH": path}}
        if installed != expected:
            raise RuntimeError(f"已注册的配置与本次部署不同，请先核对 {destination}。")
    state = service_state(label)
    if state:
        source = re.search(r"^\s*path = (.+)$", state, re.MULTILINE)
        if not source or Path(source[1]).resolve() != destination.resolve() or not destination.is_file():
            raise RuntimeError(f"系统中已有同名服务 {label}，请先核对其来源。")
    return bool(state)


def probe_json(host, port, path, protocol="http"):
    """直连本机监听地址读取有限的 JSON，不使用浏览器代理或重定向。"""
    connection_type = HTTPSConnection if protocol == "https" else HTTPConnection
    connection = connection_type(host, port, timeout=2)
    try:
        connection.request("GET", path)
        with connection.getresponse() as response:
            if response.status == 200:
                return json.loads(response.read(1024))
    except (HTTPException, OSError, ValueError):
        pass
    finally:
        connection.close()
    return None


def listening_processes(port):
    """按 PID 获取指定端口上的 TCP 监听地址，包括由其他账号启动的进程。"""
    result = command(
        LSOF, "-w", "-nP", "-a", f"-iTCP:{port}", "-sTCP:LISTEN", "-Fptn",
        admin=True, required=False,
    )
    if result.returncode not in (0, 1) or result.stderr:
        raise RuntimeError(f"无法检查 TCP 端口 {port} 的监听进程。\n{result.stderr.strip()}")
    listeners = {}
    pid, family = None, ""
    for line in result.stdout.splitlines():
        field, value = line[:1], line[1:]
        if field == "p":
            pid = int(value)
        elif field == "t":
            family = value
        elif field == "n" and pid is not None:
            host = value.rsplit(":", 1)[0].strip("[]")
            listeners.setdefault(pid, set()).add((family, host))
    return listeners


def read_process(pid):
    """读取 macOS 的进程归属、启动时间、内核程序名和命令行。"""
    result = command(
        "/bin/ps", "-ww", "-p", str(pid), "-o", "uid=,lstart=,stat=,ucomm=,command=",
        required=False,
    )
    fields = result.stdout.strip().split(maxsplit=8)
    if len(fields) == 9 and not fields[6].startswith("Z"):
        return Process(int(fields[0]), " ".join(fields[1:6]), fields[7], fields[8])
    return None


def stop_manual_service(label, host, port, matches_process):
    """用服务自己的进程检查核实端口占用者，再等待其正常退出。"""
    listeners = listening_processes(port)
    managed = service_pid(label)
    if not listeners or managed in listeners:
        return

    # lsof 用 * 表示该地址族内的全地址监听。IPv6 全地址也可能覆盖 IPv4。
    addresses = {("IPv4" if family == socket.AF_INET else "IPv6", address[0])
                 for family, _, _, _, address in socket.getaddrinfo(host, port, type=socket.SOCK_STREAM)}
    addresses = {(family, "*" if address in ("0.0.0.0", "::") else address)
                 for family, address in addresses}
    conflicts = {
        pid: bound for pid, bound in listeners.items()
        if any((family == desired_family and (address == desired or "*" in (address, desired)))
               or (family == "IPv6" and address == "*" and desired_family == "IPv4")
               for family, address in bound for desired_family, desired in addresses)
    }
    if not conflicts:
        return
    if len(conflicts) != 1:
        raise RuntimeError(f"TCP 端口 {port} 上有多个进程与 {label} 的监听地址冲突，请核对进程归属。")

    pid, bound = next(iter(conflicts.items()))
    process = read_process(pid)
    if process is None:
        return
    if not matches_process(pid, process, bound & addresses):
        raise RuntimeError(f"TCP 端口 {port} 由 PID {pid} 监听，无法确认是 {label} 的手动实例。")
    if pid == service_pid(label):
        return
    current = read_process(pid)
    if current is None:
        return
    if current != process:
        raise RuntimeError(f"PID {pid} 的进程身份已变化，请重新运行注册脚本。")

    print(f"正在结束 {label} 的手动实例（PID {pid}）。", flush=True)
    if process.uid == os.geteuid():
        try:
            os.kill(pid, signal.SIGTERM)
        except ProcessLookupError:
            return
    else:
        command("/bin/kill", "-TERM", str(pid), admin=True)
    deadline = time.monotonic() + 20
    while time.monotonic() < deadline:
        current = read_process(pid)
        if current is None or current.started != process.started:
            return
        time.sleep(0.25)
    raise RuntimeError(f"{label} 的手动实例（PID {pid}）尚未退出，请检查该进程后重试。")


def wait_ready(config, port, check_version, *, update):
    """确认受管进程已监听目标端口，需要时重启仍运行旧版本的服务。"""
    label = config["Label"]
    previous_pid = None
    deadline = time.monotonic() + 25
    while time.monotonic() < deadline:
        pid = service_pid(label)
        if pid and pid != previous_pid:
            result = command(
                LSOF, "-nP", "-a", "-p", str(pid), "-iTCP", "-sTCP:LISTEN", "-Fn",
                required=False,
            )
            ports = set(re.findall(r"^n.*:(\d+)$", result.stdout, re.MULTILINE))
            if str(port) in ports:
                if check_version is None:
                    return
                current = check_version()
                if current is not None and service_pid(label) == pid:
                    if current:
                        return
                    if update and previous_pid is None:
                        print(f"正在重启 {label} 以加载当前版本。", flush=True)
                        command(LAUNCHCTL, "kill", "SIGTERM", f"system/{label}", admin=True)
                        previous_pid = pid
                        deadline = time.monotonic() + 25
        time.sleep(0.5)
    raise RuntimeError(f"{label} 未达到就绪状态。请查看 {config['StandardErrorPath']} 和输出日志。")


def install_service(config, host, port, matches_process, *, check_version=None, keep_installed_path=False):
    """注册单个服务、接管已确认的手动实例并等待启动。

    check_version 返回 True 表示已就绪，False 表示旧版本，None 表示暂未就绪。
    """
    label = config["Label"]
    target = f"system/{label}"
    log_dir = Path(config["StandardOutPath"]).parent
    log_dir.mkdir(mode=0o700, parents=True, exist_ok=True)
    with (log_dir / ".register.lock").open("a") as lock, TemporaryDirectory(prefix="nova-launchd-") as temp:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise RuntimeError("该部署账号下已有注册脚本在运行。") from error

        filename = f"{label}.plist"
        destination = DAEMON_DIR / filename
        loaded = verify_existing(config, destination, keep_installed_path=keep_installed_path)
        staged = Path(temp) / filename
        staged.write_bytes(plistlib.dumps(config))
        command("/usr/bin/plutil", "-lint", staged)
        subprocess.run([SUDO, "-v"], check=True)

        installed = started = False
        try:
            if not destination.exists():
                command("/usr/bin/install", "-o", "root", "-g", "wheel", "-m", "644",
                        staged, destination, admin=True)
                installed = True
            stop_manual_service(label, host, port, matches_process)
            command(LAUNCHCTL, "enable", target, admin=True)
            if not loaded:
                command(LAUNCHCTL, "bootstrap", "system", destination, admin=True)
                started = True
            elif service_pid(label) is None:
                command(LAUNCHCTL, "kickstart", target, admin=True)

            wait_ready(config, port, check_version, update=loaded)
            if not verify_existing(config, destination, keep_installed_path=keep_installed_path):
                raise RuntimeError(f"未能核对 {target} 的开机自启配置。")
        except (OSError, RuntimeError, subprocess.SubprocessError, KeyboardInterrupt):
            if started:
                command(LAUNCHCTL, "bootout", target, admin=True)
            if installed:
                command("/bin/rm", "-f", destination, admin=True)
            raise

    print(f"{label} 已设为开机自启，正在监听端口 {port}。", flush=True)
    print(f"服务日志目录 {log_dir}")


def run_entrypoint(main):
    """在两个独立脚本中一致地报告部署失败或用户取消。"""
    try:
        main()
        return 0
    except (OSError, RuntimeError, TypeError, ValueError, subprocess.SubprocessError,
            plistlib.InvalidFileException, ExpatError) as error:
        print(error, file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        return 130
