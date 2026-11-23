#!/usr/bin/env python3
"""在 macOS 上注册并启动内网 Verdaccio。"""

import json
import os
from pathlib import Path
import re
import shutil
import sys


CONFIG_PATH = Path("/opt/verdaccio-internal/config.yaml")
VERDACCIO_LABEL = "com.solotopia.verdaccio-internal"

# 直接执行脚本时从 Nova 的 Tools 目录加载共用启动工具。
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "_shared"))

import macos_launchd


def verdaccio_listener(node, arguments):
    """用当前安装的 Verdaccio 解析原配置，取得实际监听端口和进程标题。"""
    reader = """
const { realpathSync } = require('node:fs');
const { createRequire } = require('node:module');
const requireFromCli = createRequire(realpathSync(process.argv[1]));
const configModule = requireFromCli('@verdaccio/config');
const config = configModule.parseConfigFile(process.argv[2]);
let listener;
if (configModule.getListenAddress) {
    listener = configModule.getListenAddress(config.listen, { warn() {} });
} else {
    const getList = configModule.getListListenAddresses ||
        requireFromCli('../build/lib/cli/utils.js').getListListenAddresses;
    listener = getList(undefined, config.listen).find(address => address.port);
}
process.stdout.write(JSON.stringify({ ...listener, title: config.web?.title || 'verdaccio' }));
"""
    result = macos_launchd.command(node, "-e", reader, arguments[0], arguments[-1], required=False)
    if result.returncode:
        raise RuntimeError(f"无法用当前 Verdaccio 读取配置 {arguments[-1]}。\n{result.stderr.strip()}")
    try:
        listener = json.loads(result.stdout)
        return listener["host"], int(listener["port"]), listener["proto"], listener["title"]
    except (KeyError, TypeError, ValueError) as error:
        raise RuntimeError(f"{arguments[-1]} 未提供有效的 Verdaccio TCP 监听地址。") from error


def matches_verdaccio_process(process, addresses, port, protocol, title, arguments):
    """优先核对 Verdaccio 启动参数，标题覆盖参数后用绑定和服务响应核对。"""
    if process.executable != "node" or not addresses:
        return False
    match = re.fullmatch(r"(?:.*/)?node (.+?) (?:-c |--config[ =])(.+)", process.arguments)
    if match:
        cli, config = Path(match[1]), Path(match[2])
        return (
            cli.is_absolute() and cli.resolve() == Path(arguments[0]).resolve()
            and config.is_absolute() and config.resolve() == Path(arguments[-1]).resolve()
        )
    # Verdaccio 改写进程标题时，macOS 可能读不到原始 -c 参数。
    return process.arguments == title and any(
        macos_launchd.probe_json(
            ("::1" if family == "IPv6" else "127.0.0.1") if address == "*" else address,
            port, "/-/ping", protocol,
        ) == {}
        for family, address in addresses
    )


def main():
    """读取 Verdaccio 的实际配置，注册并启动内网服务。"""
    account = macos_launchd.deployment_account()
    if not CONFIG_PATH.is_file() or not os.access(CONFIG_PATH, os.R_OK):
        raise RuntimeError(f"部署账号无法读取 {CONFIG_PATH}，请先准备该文件。")

    executables = {}
    for name in ("node", "verdaccio"):
        executable = shutil.which(name)
        if not executable:
            raise RuntimeError(f"当前终端找不到 {name}，请用能运行 Verdaccio 的账号执行脚本。")
        executables[name] = Path(executable).absolute()
    arguments = [str(executables["verdaccio"]), "-c", str(CONFIG_PATH)]
    config = macos_launchd.daemon_config(
        account, VERDACCIO_LABEL, CONFIG_PATH.parent, arguments,
        search_path=(str(executables["node"].parent), str(executables["verdaccio"].parent)),
    )
    host, port, protocol, title = verdaccio_listener(str(executables["node"]), arguments)

    def matches_process(_pid, process, addresses):
        """核对占用端口的 Verdaccio 实例与部署配置一致。"""
        return matches_verdaccio_process(process, addresses, port, protocol, title, arguments)

    macos_launchd.install_service(config, host, port, matches_process)


if __name__ == "__main__":
    sys.exit(macos_launchd.run_entrypoint(main))
