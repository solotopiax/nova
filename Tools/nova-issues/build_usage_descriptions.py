#!/usr/bin/env python3
"""从 Nova 现有能力说明生成维护台独立部署所需的操作提示。"""

import argparse
import json
from pathlib import Path
import re
import subprocess


SERVICE_ROOT = Path(__file__).resolve().parent
FRAMEWORK_ROOT = SERVICE_ROOT.parents[1] / "Assets/Framework"
OUTPUT = SERVICE_ROOT / "usage-descriptions.json"
UNITY_OUTPUT = SERVICE_ROOT / "unity-usage-descriptions.json"
MODULE_OUTPUT = SERVICE_ROOT / "usage-modules.json"
ACTION = re.compile(r'\[\s*AgentAction(?:Attribute)?\s*\(\s*"(nova\.project\.[^"]+)"\s*,(.*?)\)\s*\]', re.S)
DESCRIPTION = re.compile(r'\bDescription\s*=\s*("(?:\\.|[^"\\])*")')
SKILL_HEADER = re.compile(r"\A---\n(.*?)\n---\s*\n# ([^\n]+)", re.S)

# 命令只记录稳定标识，维护台的文案不包含任何工作区参数或输出。
COMMAND_DESCRIPTIONS = {
    "editor.preload.sdk": "在 Unity Editor 中进入游戏时完成 SDK 预加载。",
    "nova_capabilities.describe": "查看指定 Nova 功能的说明、用法和当前限制。",
    "nova_capabilities.inventory": "查看当前项目可用的 Nova 功能和执行入口。",
    "nova_excel_template.inspect": "检查 Excel 源表及其模板和样式。",
    "nova_excel_template.execute": "按已确认的计划创建或调整 Excel 源表。",
    "nova_skills.doctor": "检查项目中的 Nova Skill 是否齐全、过期或被修改。",
    "nova_skills.resolve": "定位当前 Unity 工程实际使用的 Nova Framework 和 Skill 目录。",
    "nova_table_source.inspect": "查看 Nova 数据表源文件的字段和数据结构。",
    "nova_table_source.execute": "按已确认的计划更新 Nova 数据表源文件。",
    "nova_tracks_source.inspect": "查看项目的 Tracks 埋点表及其结构。",
    "nova_tracks_source.execute": "按已确认的计划更新 Tracks 埋点表。",
    "nova_tracks_source.recover": "从执行回执恢复 Tracks 埋点表的上次写入。",
}


def build_descriptions(framework_root):
    """复用 Action 声明和随包 Skill 标题及用途，生成按操作标识查询的提示。"""
    descriptions = COMMAND_DESCRIPTIONS.copy()
    handlers = framework_root / "Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers"
    for path in handlers.rglob("*.cs"):
        for operation, declaration in ACTION.findall(path.read_text(encoding="utf-8-sig")):
            description = DESCRIPTION.search(declaration)
            if description is None:
                raise ValueError(f"{operation} 缺少 Action 说明。")
            descriptions[operation] = json.loads(description[1])

    agents = framework_root / "Agents"
    catalog = json.loads((agents / "catalog.json").read_text(encoding="utf-8"))
    for skill in catalog["skills"]:
        path = agents / skill["path"] / "SKILL.md"
        match = SKILL_HEADER.match(path.read_text(encoding="utf-8-sig"))
        if match is None:
            raise ValueError(f"{skill['id']} 缺少 Skill 标题。")
        purpose = re.search(r"^description: (.+)", match[1], re.M)
        if purpose is None:
            raise ValueError(f"{skill['id']} 缺少 Skill 用途。")
        title = match[2].strip()
        purpose_summary = purpose[1].strip().removeprefix("Use when ").split("。", 1)[0]
        descriptions[skill["id"]] = f"查阅「{title}」。{purpose_summary}。"
    return descriptions


def build_module_names(framework_root):
    """从现行 Runtime 模块导航生成独立部署所需的模块名。"""
    source = (framework_root / "Docs/Runtime/Runtime.md").read_text(encoding="utf-8")
    navigation = source.split("## 模块导航\n", 1)[1].split("\n## ", 1)[0]
    titles = re.findall(r"^\| ([A-Za-z][A-Za-z0-9]*) \| `Nova\.[A-Za-z][A-Za-z0-9]*` \|", navigation, re.M)
    if not titles:
        raise ValueError("Runtime 模块导航缺少模块。")
    return {title.lower(): title for title in titles}


def unity_command_descriptions(project):
    """只读获取目标 Editor 中 Unity Pipeline 自带命令的说明。"""
    result = {}
    offset = 0
    while True:
        reply = subprocess.run([
            "unity", "command", "--project-path", str(project.resolve()), "--caller", "plugin", "--skill", "unity-cli",
            "--limit", "200", "--offset", str(offset), "--format", "json",
        ], check=True, capture_output=True, text=True, timeout=15)
        page = json.loads(reply.stdout)["data"]
        commands = page["commands"]
        if not commands:
            break
        for command in commands:
            if command.get("package") not in ("Unity.Pipeline", "Unity.Pipeline.Editor"):
                continue
            name = ".".join(part for part in (command.get("group"), command["name"]) if part)
            result[f"unity.{name}"] = command["description"].strip()
        offset += len(commands)
        if offset >= page["total"]:
            break
    if not result:
        raise ValueError("目标 Editor 未返回 Unity Pipeline 自带命令。")
    return result


def write_descriptions(path, descriptions):
    """以稳定顺序保存操作说明，保留已知历史标识。"""
    path.write_text(json.dumps(descriptions, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-project", type=Path, help="同时从该已打开的 Editor 补齐官方 Unity CLI 命令")
    args = parser.parse_args()
    existing = json.loads(OUTPUT.read_text(encoding="utf-8")) if OUTPUT.exists() else {}
    nova_descriptions = {operation: text for operation, text in existing.items() if not operation.startswith("unity.")}
    nova_descriptions.update(build_descriptions(FRAMEWORK_ROOT))
    if args.unity_project:
        existing_unity = json.loads(UNITY_OUTPUT.read_text(encoding="utf-8")) if UNITY_OUTPUT.exists() else {}
        write_descriptions(UNITY_OUTPUT, unity_command_descriptions(args.unity_project) | existing_unity)
    write_descriptions(OUTPUT, nova_descriptions)
    write_descriptions(MODULE_OUTPUT, build_module_names(FRAMEWORK_ROOT))
