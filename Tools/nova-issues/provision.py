#!/usr/bin/env python3
"""Prepare the administrator-managed Codex hook files without editing host policy."""

import argparse
import json
import os
from pathlib import Path
import shlex
import shutil
import subprocess
import sys


EVENTS = ("SessionStart", "UserPromptSubmit", "PostToolUse", "Stop", "Interrupt")
POST_TOOL_MATCHER = r"^(?:Bash|exec_command|apply_patch|Read|Write|Edit|nova_project_action|mcp__.+__(?:nova_project_action|execute_custom_tool))$"
AGENT_SOURCE = Path(__file__).resolve().parents[2] / "Assets/Framework/Agents/Tools/nova_issues_agent.py"


def hook_configuration(python, script):
    """钩子仅传入随包脚本，Agent 自行识别工程和上报地址。"""
    command = [str(python), "-B", str(script), "hook"]
    quote = subprocess.list2cmdline if os.name == "nt" else shlex.join
    encoded = json.dumps(quote(command), ensure_ascii=False)
    lines = ["# Merge this fragment into the existing administrator-managed Codex requirements.",
             "[features]", "hooks = true", "", "[hooks]",
             f"{'windows_managed_dir' if os.name == 'nt' else 'managed_dir'} = {json.dumps(str(script.parent), ensure_ascii=False)}"]
    for event in EVENTS:
        lines.extend(("", f"[[hooks.{event}]]"))
        if event == "PostToolUse":
            lines.append(f"matcher = {json.dumps(POST_TOOL_MATCHER)}")
        lines.extend((f"[[hooks.{event}.hooks]]", 'type = "command"', f"command = {encoded}", "timeout = 3"))
        if os.name == "nt":
            lines.append(f"command_windows = {encoded}")
    return "\n".join(lines) + "\n"


def main():
    """为需要逐轮检查的 Codex 工作站生成受管钩子片段。"""
    parser = argparse.ArgumentParser(description="准备 nova-issues 的受管 Codex 配置片段")
    parser.add_argument("--output", type=Path, required=True, help="维护端分发目录")
    parser.add_argument("--enrollment", type=Path, help=argparse.SUPPRESS)
    parser.add_argument("--agent-source", type=Path, default=AGENT_SOURCE)
    parser.add_argument("--python", type=Path, default=Path(sys.executable))
    options = parser.parse_args()
    output = options.output.resolve()
    source = options.agent_source.resolve()
    if not source.is_file() or not options.python.is_absolute():
        parser.error("必须提供有效的 Agent 源码和工作站的 Python 绝对路径。")
    output.mkdir(mode=0o700, parents=True, exist_ok=True)
    agent = output / "nova_issues_agent.py"
    fragment = output / "nova-issues.requirements.toml"
    if agent.exists() or fragment.exists():
        parser.error("输出目录已有生成文件，请选择新目录。")
    shutil.copyfile(source, agent)
    fragment.write_text(hook_configuration(options.python, agent), encoding="utf-8")
    print(fragment)
    return 0


if __name__ == "__main__":
    sys.exit(main())
