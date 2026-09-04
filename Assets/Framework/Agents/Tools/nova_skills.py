#!/usr/bin/env python3
"""Nova Project Skills 的真源校验、消费者投影与漂移诊断工具。"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import stat
import sys
import tempfile
import threading
import uuid
from contextlib import contextmanager
from pathlib import Path
from typing import Any
from urllib.parse import unquote, urlparse

try:
    import fcntl
except ImportError:  # pragma: no cover - Windows 的等价实现由 msvcrt 提供。
    fcntl = None

try:
    import msvcrt
except ImportError:  # pragma: no cover - Unix 的等价实现由 fcntl 提供。
    msvcrt = None


FRAMEWORK_PACKAGE_NAME = "com.solotopia.nova.framework"
PROJECT_SKILL_ID_PREFIX = "nova-project-"
CATALOG_FILE_NAME = "catalog.json"
STATE_FILE_NAME = "nova-skills.lock.json"
TRANSACTION_FILE_NAME = "nova-skills.transaction.json"
STAGING_DIRECTORY_NAME = ".nova-skills-staging"
SYNC_LOCK_FILE_NAME = ".nova-skills-sync.lock"
# Catalog、state 与 transaction 共享唯一的 schemaVersion 1；任何不匹配的格式均 fail-closed。
CATALOG_SCHEMA_VERSION = 1
STATE_SCHEMA_VERSION = 1
TRANSACTION_SCHEMA_VERSION = 1
SKILL_NAME_PATTERN = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
TRANSACTION_ID_PATTERN = re.compile(r"^[0-9a-f]{32}$")
SKILL_KINDS = {"router", "operation", "workflow"}
SKILL_EFFECTS = {"read", "workspace-write", "unity-write", "generated-output", "build"}
MINIMUM_EVIDENCE_LEVELS = {"static", "compile", "play", "bundle-build", "player-build"}
BUILD_ARTIFACT_EVIDENCE_LEVELS = {"bundle-build", "player-build"}
CONTRACT_IDEMPOTENCY = {"read-only", "ensure-state", "orchestrate"}
CONTRACT_RESULT_STATES = {"success", "partial", "blocked", "not_applicable"}
ACTION_ADAPTER_KINDS = {
    "agent-action",
    "agent-action-blocked",
    "csharp-api",
    "cli",
    "pipify",
    "unity-editor-api",
    "unity-editor-automation",
    "unity-menu",
    "workspace-edit",
    "workspace-inspection",
}
# Action Adapter 的 entry 不是任意命令。只有这两类声明才表示 Framework Project Action；
# 已注册 Action 默认必须使用 agent-action 并进入 MCP 显式白名单。
AGENT_ACTION_ADAPTER_KINDS = {"agent-action", "agent-action-blocked"}
AGENT_ACTION_ID_PATTERN = re.compile(
    r"^nova\.project\.[a-z0-9]+(?:-[a-z0-9]+)*\.[a-z0-9]+(?:-[a-z0-9]+)*$"
)
AGENT_ACTION_ATTRIBUTE_PATTERN = re.compile(
    r'\[\s*AgentAction(?:Attribute)?\s*\(\s*"(?P<id>[^"]+)"', re.MULTILINE
)
EXPOSURE_POLICY_PATTERN = re.compile(
    r'\bnew\s+ExposurePolicy\s*\(\s*"(?P<id>[^"]+)"', re.MULTILINE
)
AGENT_ACTION_HANDLERS_RELATIVE_PATH = Path(
    "Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers"
)
MCP_PACKAGE_NAME = "com.solotopia.nova.framework.mcp"
MCP_GATEWAY_RELATIVE_PATH = Path("Nova/Editor/NovaProjectActionGateway.cs")
COMMON_BASELINE_SENTENCE = (
    "触发后先读取当前 Framework 的 `Docs/START_HERE.md`，"
    "作为所有 `nova-project-*` Skill 的共同底线。"
)
PROGRESSIVE_DISCLOSURE_HEADING = "## 渐进式披露"
SHA256_HEX_PATTERN = re.compile(r"^[0-9a-f]{64}$")
RECONCILE_ACTIONS = {"add", "update", "remove"}
SKILL_STATUSES = {"experimental", "stable", "deprecated"}
CATALOG_FIELDS = {"schemaVersion", "package", "capabilityGroups", "skills"}
CATALOG_SKILL_FIELDS = {
    "id",
    "path",
    "kind",
    "status",
    "journeys",
    "effects",
    "minimumEvidence",
    "replacedBy",
}
STATE_FIELDS = {"schemaVersion", "package", "packageVersion", "catalogHash", "managed"}
TRANSACTION_FIELDS = {
    "schemaVersion",
    "transactionId",
    "previousState",
    "finalState",
    "pending",
}
_PROCESS_LOCK_GUARD = threading.Lock()
_PROCESS_LOCK_PATHS: set[str] = set()


class NovaSkillsError(RuntimeError):
    """表示无法在不越权或不覆盖用户内容的前提下继续执行。"""


def _is_managed_skill_id(skill_id: object) -> bool:
    """判断 id 是否属于 Nova 可以声明所有权的项目组 Skill 命名空间。"""
    return (
        isinstance(skill_id, str)
        and SKILL_NAME_PATTERN.fullmatch(skill_id) is not None
        and skill_id.startswith(PROJECT_SKILL_ID_PREFIX)
    )


def _read_json(path: Path) -> dict[str, Any]:
    """读取 JSON 对象，并将格式问题转换为可交付的错误。"""
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise NovaSkillsError(f"缺少文件：{path}") from exc
    except json.JSONDecodeError as exc:
        raise NovaSkillsError(f"JSON 无法解析：{path}：{exc.msg}") from exc
    if not isinstance(value, dict):
        raise NovaSkillsError(f"JSON 根节点必须是对象：{path}")
    return value


def _write_json_atomically(path: Path, value: dict[str, Any]) -> None:
    """使用同目录临时文件原子写入受管状态，避免中断留下半份 lock。"""
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            json.dump(value, handle, ensure_ascii=False, indent=2, sort_keys=True)
            handle.write("\n")
        os.replace(temporary_path, path)
    finally:
        if temporary_path.exists():
            temporary_path.unlink()


def _read_skill_name(skill_path: Path) -> str | None:
    """从标准 YAML frontmatter 读取 name，避免引入额外 YAML 运行依赖。"""
    content = skill_path.read_text(encoding="utf-8")
    match = re.match(r"^---\n(.*?)\n---", content, re.DOTALL)
    if not match:
        return None
    for line in match.group(1).splitlines():
        name_match = re.match(r"^name:\s*([^\s#]+)\s*$", line)
        if name_match:
            return name_match.group(1).strip().strip('"').strip("'")
    return None


def _skill_body(skill_path: Path) -> str | None:
    """提取 SKILL.md 的 frontmatter 后正文，供共同执行契约做最小结构校验。"""
    content = skill_path.read_text(encoding="utf-8")
    match = re.match(r"^---\r?\n.*?\r?\n---(?:\r?\n|$)", content, re.DOTALL)
    if match is None:
        return None
    return content[match.end() :].strip()


def _first_body_paragraph(body: str) -> str | None:
    """跳过 Markdown 标题，返回 frontmatter 后首个真正的正文段落。"""
    for paragraph in re.split(r"\r?\n\s*\r?\n", body):
        normalized = paragraph.strip()
        if not normalized:
            continue
        lines = [line.strip() for line in normalized.splitlines() if line.strip()]
        if lines and all(line.startswith("#") for line in lines):
            continue
        return normalized
    return None


def _validate_skill_progressive_disclosure(skill_id: str, skill_path: Path) -> list[str]:
    """校验共同入口先行，且 Skill 至少声明按需读取资料的渐进式路由。"""
    errors: list[str] = []
    body = _skill_body(skill_path)
    if body is None:
        return [f"{skill_id} 的 SKILL.md 缺少可解析的 frontmatter"]
    first_paragraph = _first_body_paragraph(body)
    if first_paragraph is None or COMMON_BASELINE_SENTENCE not in first_paragraph:
        errors.append(f"{skill_id} 的共同底线必须位于 frontmatter 后首个正文段落")
    if PROGRESSIVE_DISCLOSURE_HEADING not in body or "仅在" not in body:
        errors.append(f"{skill_id} 的 SKILL.md 必须声明渐进式披露的按需读取路由")
    return errors


def _find_descendant_symlink(directory: Path) -> Path | None:
    """返回目录树中的首个软链或 junction，防止哈希或复制跟随真源外文件。"""
    for candidate in directory.rglob("*"):
        if _is_link_or_junction(candidate):
            return candidate
    return None


def _tree_hash(directory: Path) -> str:
    """计算目录内常规文件的稳定内容哈希，用于检测受管副本漂移。"""
    symlink = _find_descendant_symlink(directory)
    if symlink is not None:
        raise NovaSkillsError(f"目录包含不允许的软链：{symlink}")
    digest = hashlib.sha256()
    files = [
        (candidate.relative_to(directory).as_posix().encode("utf-8"), candidate)
        for candidate in directory.rglob("*")
        if candidate.is_file()
    ]
    for relative_path, path in sorted(files, key=lambda item: item[0]):
        digest.update(relative_path)
        digest.update(b"\0")
        digest.update(path.read_bytes())
        digest.update(b"\0")
    return digest.hexdigest()


def _file_hash(path: Path) -> str:
    """计算单个受管元数据文件的内容哈希，供状态识别当前 Catalog 版本。"""
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _safe_child(root: Path, relative_path: str) -> Path | None:
    """解析 Catalog 相对路径，并拒绝离开 Agents 真源的路径穿越。"""
    candidate = (root / relative_path).resolve()
    try:
        candidate.relative_to(root.resolve())
    except ValueError:
        return None
    return candidate


def _is_link_or_junction(path: Path) -> bool:
    """识别软链及 Windows junction，避免受管投影沿链接写入业务目录。"""
    if path.is_symlink():
        return True
    is_junction = getattr(path, "is_junction", None)
    if callable(is_junction):
        return is_junction()
    if os.name != "nt":
        return False
    try:
        file_attributes = getattr(path.lstat(), "st_file_attributes", 0)
    except FileNotFoundError:
        # 首次同步前尚未创建的受管路径不是 junction。
        return False
    reparse_point = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x0400)
    return bool(file_attributes & reparse_point)


def _catalog_entries(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    """取得 Catalog 条目列表，并在结构异常时快速失败。"""
    entries = catalog.get("skills")
    if not isinstance(entries, list):
        raise NovaSkillsError("catalog.json 的 skills 必须是数组")
    if not all(isinstance(entry, dict) for entry in entries):
        raise NovaSkillsError("catalog.json 的 skills 只能包含对象")
    return entries


def _validate_contract_shape(skill_id: str, contract: dict[str, Any]) -> list[str]:
    """校验除 Catalog 镜像字段以外的安全契约，避免空值比较掩盖缺失定义。"""
    errors: list[str] = []
    required_fields = {
        "compatibility",
        "requires",
        "actionAdapters",
        "inputs",
        "writeScope",
        "locks",
        "idempotency",
        "confirmation",
        "resultStates",
        "evidence",
    }
    for field in sorted(required_fields):
        if field not in contract:
            errors.append(f"{skill_id} 的 contract.json 缺少 {field}")

    compatibility = contract.get("compatibility")
    if not isinstance(compatibility, dict) or not isinstance(compatibility.get("framework"), str):
        errors.append(f"{skill_id} 的 contract.json compatibility 必须声明 framework")
    requires = contract.get("requires")
    if not isinstance(requires, list) or any(
        not isinstance(required_id, str) or not SKILL_NAME_PATTERN.fullmatch(required_id)
        for required_id in requires
    ):
        errors.append(f"{skill_id} 的 contract.json requires 必须是合法 Skill id 数组")
    elif len(requires) != len(set(requires)):
        errors.append(f"{skill_id} 的 contract.json requires 不能重复")

    action_adapters = contract.get("actionAdapters")
    if not isinstance(action_adapters, list) or not action_adapters:
        errors.append(f"{skill_id} 的 contract.json actionAdapters 必须是非空数组")
    else:
        seen_adapters: set[tuple[str, str, str]] = set()
        seen_agent_action_ids: set[str] = set()
        for adapter in action_adapters:
            if not isinstance(adapter, dict) or set(adapter) != {"kind", "entry", "when"}:
                errors.append(
                    f"{skill_id} 的 contract.json actionAdapters 项必须只含 kind、entry、when"
                )
                continue
            kind = adapter.get("kind")
            entry = adapter.get("entry")
            when = adapter.get("when")
            if kind not in ACTION_ADAPTER_KINDS or not isinstance(entry, str) or not entry or not isinstance(when, str) or not when:
                errors.append(f"{skill_id} 的 contract.json actionAdapters 项不合法")
                continue
            identity = (kind, entry, when)
            if identity in seen_adapters:
                errors.append(f"{skill_id} 的 contract.json actionAdapters 不能重复")
                continue
            seen_adapters.add(identity)
            if kind in AGENT_ACTION_ADAPTER_KINDS:
                if AGENT_ACTION_ID_PATTERN.fullmatch(entry) is None:
                    errors.append(
                        f"{skill_id} 的 {kind} entry 必须是精确 nova.project.<domain>.<verb> Action ID"
                    )
                elif entry in seen_agent_action_ids:
                    errors.append(
                        f"{skill_id} 的 contract.json 同一 Agent Action 只能声明一次：{entry}"
                    )
                else:
                    seen_agent_action_ids.add(entry)
            elif kind == "csharp-api" and (
                "nova.project." in entry or "nova_project_action" in entry
            ):
                errors.append(
                    f"{skill_id} 的 csharp-api 不是可调度 Action；请改用 agent-action 或 agent-action-blocked"
                )

    inputs = contract.get("inputs")
    if not isinstance(inputs, list) or not inputs:
        errors.append(f"{skill_id} 的 contract.json inputs 必须是非空数组")
    elif any(
        not isinstance(item, dict)
        or not isinstance(item.get("name"), str)
        or not item["name"]
        or not isinstance(item.get("required"), bool)
        for item in inputs
    ):
        errors.append(f"{skill_id} 的 contract.json inputs 必须声明 name 与 required")

    write_scope = contract.get("writeScope")
    if not isinstance(write_scope, dict) or any(
        not isinstance(write_scope.get(field), list)
        or any(not isinstance(path, str) for path in write_scope[field])
        for field in ("allow", "deny")
    ):
        errors.append(f"{skill_id} 的 contract.json writeScope 必须含 allow 与 deny 字符串数组")

    locks = contract.get("locks")
    if not isinstance(locks, list) or any(not isinstance(lock, str) or not lock for lock in locks):
        errors.append(f"{skill_id} 的 contract.json locks 必须是字符串数组")
    elif len(locks) != len(set(locks)):
        errors.append(f"{skill_id} 的 contract.json locks 不能重复")

    if contract.get("idempotency") not in CONTRACT_IDEMPOTENCY:
        errors.append(
            f"{skill_id} 的 contract.json idempotency 必须是 {sorted(CONTRACT_IDEMPOTENCY)} 之一"
        )
    confirmation = contract.get("confirmation")
    if not isinstance(confirmation, dict) or not isinstance(confirmation.get("rule"), str) or not isinstance(
        confirmation.get("requiredFor"), list
    ) or any(not isinstance(item, str) for item in confirmation.get("requiredFor", [])):
        errors.append(f"{skill_id} 的 contract.json confirmation 必须含 rule 与 requiredFor")

    result_states = contract.get("resultStates")
    if not isinstance(result_states, list) or not result_states or any(
        state not in CONTRACT_RESULT_STATES for state in result_states
    ):
        errors.append(f"{skill_id} 的 contract.json resultStates 不合法")
    elif len(result_states) != len(set(result_states)):
        errors.append(f"{skill_id} 的 contract.json resultStates 不能重复")
    evidence = contract.get("evidence")
    if not isinstance(evidence, list) or not evidence or any(
        not isinstance(item, str) or not item for item in evidence
    ):
        errors.append(f"{skill_id} 的 contract.json evidence 必须是非空字符串数组")
    return errors


def _find_mcp_gateway(framework_root: Path) -> Path:
    """定位与 Framework 配套的 MCP 网关源码，拒绝猜测任意第三方目录。"""
    candidates: list[Path] = []
    for ancestor in (framework_root, *framework_root.parents):
        candidates.append(
            ancestor / "UPMPackages" / MCP_PACKAGE_NAME / MCP_GATEWAY_RELATIVE_PATH
        )
        candidates.append(
            ancestor / "Packages" / MCP_PACKAGE_NAME / MCP_GATEWAY_RELATIVE_PATH
        )

    # PackageCache 中的 Framework 与 MCP 包通常是同级目录；只检查精确包名前缀。
    package_cache_root = framework_root.parent
    if package_cache_root.is_dir():
        candidates.extend(
            sorted(
                package_root / MCP_GATEWAY_RELATIVE_PATH
                for package_root in package_cache_root.glob(f"{MCP_PACKAGE_NAME}@*")
                if package_root.is_dir()
            )
        )

    for candidate in candidates:
        if candidate.is_file():
            return candidate
    raise NovaSkillsError(
        "未找到 com.solotopia.nova.framework.mcp 的 NovaProjectActionGateway.cs，"
        "无法校验 Agent Action 的 MCP ExposurePolicy"
    )


def _discover_registered_agent_actions(framework_root: Path) -> set[str]:
    """从 Framework Handler 的 [AgentAction] 特性提取真实注册 ID。"""
    handlers_root = framework_root / AGENT_ACTION_HANDLERS_RELATIVE_PATH
    if not handlers_root.is_dir():
        raise NovaSkillsError(
            f"未找到 Framework AgentAction Handler 目录：{handlers_root}"
        )
    action_ids: set[str] = set()
    for source_path in sorted(handlers_root.rglob("*.cs")):
        content = source_path.read_text(encoding="utf-8")
        for match in AGENT_ACTION_ATTRIBUTE_PATTERN.finditer(content):
            action_id = match.group("id")
            if AGENT_ACTION_ID_PATTERN.fullmatch(action_id) is not None:
                action_ids.add(action_id)
    if not action_ids:
        raise NovaSkillsError(
            f"Framework AgentAction Handler 未发现任何 [AgentAction] 注册：{handlers_root}"
        )
    return action_ids


def _discover_exposed_agent_actions(framework_root: Path) -> set[str]:
    """从 MCP 网关唯一 ExposurePolicy 提取当前可直接调度的 Action ID。"""
    gateway_path = _find_mcp_gateway(framework_root)
    content = gateway_path.read_text(encoding="utf-8")
    return {
        match.group("id")
        for match in EXPOSURE_POLICY_PATTERN.finditer(content)
        if AGENT_ACTION_ID_PATTERN.fullmatch(match.group("id")) is not None
    }


def _validate_agent_action_adapters(
    skill_id: str,
    contract: dict[str, Any],
    registered_action_ids: set[str],
    exposed_action_ids: set[str],
) -> list[str]:
    """校验 Skill 的可调度声明同时满足 Framework 注册与 MCP 开放边界。"""
    errors: list[str] = []
    action_adapters = contract.get("actionAdapters")
    if not isinstance(action_adapters, list):
        return errors
    for adapter in action_adapters:
        if not isinstance(adapter, dict):
            continue
        kind = adapter.get("kind")
        entry = adapter.get("entry")
        if kind not in AGENT_ACTION_ADAPTER_KINDS or not isinstance(entry, str):
            continue
        if AGENT_ACTION_ID_PATTERN.fullmatch(entry) is None:
            # entry 形状问题已由 _validate_contract_shape 报出，避免重复噪声。
            continue
        if entry not in registered_action_ids:
            errors.append(
                f"{skill_id} 的 {kind} 未在 Framework AgentAction Handler 注册：{entry}"
            )
            continue
        if kind == "agent-action" and entry not in exposed_action_ids:
            errors.append(
                f"{skill_id} 的 agent-action 未出现在 MCP ExposurePolicy：{entry}"
            )
        elif kind == "agent-action-blocked" and entry in exposed_action_ids:
            errors.append(
                f"{skill_id} 的 agent-action-blocked 已出现在 MCP ExposurePolicy：{entry}"
            )
    return errors


def _resolve_file_dependency(manifest_path: Path, dependency: str) -> Path:
    """解析本地 Unity file: 依赖；网络 UNC URI 一律拒绝以避免跨主机误投影。"""
    raw_path = dependency.removeprefix("file:")
    decoded_path = unquote(raw_path)
    if raw_path.startswith("//"):
        parsed = urlparse(dependency)
        if parsed.netloc not in ("", "localhost"):
            raise NovaSkillsError("不支持网络 file: URI；请使用本机 Framework 包路径")
        decoded_path = unquote(parsed.path)
    if decoded_path.startswith("//"):
        raise NovaSkillsError("不支持网络 file: URI；请使用本机 Framework 包路径")
    if re.match(r"^/[A-Za-z]:/", decoded_path):
        decoded_path = decoded_path[1:]
    decoded = Path(decoded_path)
    return (decoded if decoded.is_absolute() else manifest_path.parent / decoded).resolve()


def _agents_from_package_root(
    package_root: Path, source_label: str, expected_version: str | None = None
) -> Path:
    """验证解析到的 UPM 包身份及可校验版本，再返回其中的 Agents 目录。"""
    package_root = Path(package_root).resolve()
    package = _read_json(package_root / "package.json")
    if package.get("name") != FRAMEWORK_PACKAGE_NAME:
        raise NovaSkillsError(f"{source_label} 不是 Nova Framework 包：{package_root}")
    if expected_version is not None and package.get("version") != expected_version:
        raise NovaSkillsError(
            f"{source_label} 版本与 packages-lock.json 不一致："
            f"期望 {expected_version}，实际 {package.get('version')!r}"
        )
    agents_root = package_root / "Agents"
    if _is_link_or_junction(agents_root):
        raise NovaSkillsError(f"{source_label} 的 Agents 目录不能是软链或 junction：{agents_root}")
    if not agents_root.is_dir():
        raise NovaSkillsError(f"{source_label} Framework 包未包含 Agents：{agents_root}")
    try:
        agents_root.resolve().relative_to(package_root)
    except ValueError as exc:
        raise NovaSkillsError(f"{source_label} 的 Agents 目录越过 Framework 包边界：{agents_root}") from exc
    return agents_root


def _read_lock_entry(project_root: Path) -> dict[str, Any] | None:
    """读取 Framework 的 lock 条目；没有 lock 时保留 manifest-only 本地开发路径。"""
    lock_path = project_root / "Packages" / "packages-lock.json"
    if not lock_path.is_file():
        return None
    lock = _read_json(lock_path)
    dependencies = lock.get("dependencies")
    if not isinstance(dependencies, dict):
        raise NovaSkillsError("Packages/packages-lock.json 缺少 dependencies 对象")
    entry = dependencies.get(FRAMEWORK_PACKAGE_NAME)
    if entry is None:
        return None
    if not isinstance(entry, dict):
        raise NovaSkillsError("Framework 的 packages-lock.json 条目必须是对象")
    return entry


def _validate_file_lock(
    manifest_path: Path, dependency: str, lock_entry: dict[str, Any] | None
) -> None:
    """交叉校验 manifest 与 local lock，避免 file: 声明和已解析来源互相矛盾。"""
    if lock_entry is None:
        return
    if lock_entry.get("source") != "local":
        raise NovaSkillsError("file: Framework 依赖与 packages-lock.json source 不一致")
    locked_dependency = lock_entry.get("version")
    if not isinstance(locked_dependency, str) or not locked_dependency.startswith("file:"):
        raise NovaSkillsError("local Framework lock 必须使用 file: version")
    if _resolve_file_dependency(manifest_path, dependency) != _resolve_file_dependency(
        manifest_path, locked_dependency
    ):
        raise NovaSkillsError("manifest 与 packages-lock.json 指向不同的 Framework 本地包")


def _package_cache_agents(project_root: Path, lock_entry: dict[str, Any]) -> Path:
    """按 lock source 精确定位 PackageCache，拒绝从任意缓存候选中猜测来源。"""
    source = lock_entry.get("source")
    version = lock_entry.get("version")
    cache_root = project_root / "Library" / "PackageCache"
    if not cache_root.is_dir():
        raise NovaSkillsError("未找到 Unity PackageCache，无法验证已安装的 Framework 包")

    if source == "registry":
        if not isinstance(version, str) or not version or version.startswith("file:"):
            raise NovaSkillsError("registry Framework lock 缺少有效版本")
        candidates: list[Path] = []
        for candidate in sorted(cache_root.glob(f"{FRAMEWORK_PACKAGE_NAME}@*")):
            if not candidate.is_dir():
                continue
            try:
                package = _read_json(candidate / "package.json")
            except NovaSkillsError:
                continue
            if (
                package.get("name") == FRAMEWORK_PACKAGE_NAME
                and package.get("version") == version
            ):
                candidates.append(candidate)
        if len(candidates) == 1:
            return _agents_from_package_root(candidates[0], "PackageCache", version)
        if len(candidates) > 1:
            raise NovaSkillsError("PackageCache 中存在多个 registry Framework 候选，无法安全选择")

        package_root = cache_root / f"{FRAMEWORK_PACKAGE_NAME}@{version}"
        if package_root.is_dir():
            return _agents_from_package_root(package_root, "PackageCache", version)
        raise NovaSkillsError("PackageCache 中未找到 lock 指定版本的 Framework 包")

    if source == "git":
        git_hash = lock_entry.get("hash")
        if git_hash is not None and not isinstance(git_hash, str):
            raise NovaSkillsError("git Framework lock 的 hash 必须是字符串")
        candidates: list[Path] = []
        for candidate in sorted(cache_root.glob(f"{FRAMEWORK_PACKAGE_NAME}@*")):
            if not candidate.is_dir():
                continue
            candidate_hash = candidate.name.removeprefix(f"{FRAMEWORK_PACKAGE_NAME}@")
            if git_hash and not (
                git_hash.startswith(candidate_hash) or candidate_hash.startswith(git_hash)
            ):
                continue
            try:
                package = _read_json(candidate / "package.json")
            except NovaSkillsError:
                continue
            if package.get("name") == FRAMEWORK_PACKAGE_NAME:
                candidates.append(candidate)
        if len(candidates) == 1:
            return _agents_from_package_root(candidates[0], "PackageCache")
        if len(candidates) > 1:
            raise NovaSkillsError("PackageCache 中存在多个 git Framework 候选，无法安全选择")
        raise NovaSkillsError("PackageCache 中未找到 lock 指定的 git Framework 包")

    if source == "local":
        raise NovaSkillsError("packages-lock.json 为 local，但 manifest 未声明 file: Framework 依赖")
    if source == "embedded":
        raise NovaSkillsError("packages-lock.json 声明 embedded Framework，但包目录不存在")
    raise NovaSkillsError(f"不支持的 Framework packages-lock.json source：{source!r}")


def load_catalog(agents_root: Path) -> dict[str, Any]:
    """加载指定 Agents 真源的 Catalog。"""
    return _read_json(Path(agents_root) / CATALOG_FILE_NAME)


def validate_agents_root(agents_root: Path) -> list[str]:
    """校验 Catalog、Skill 前置元数据与机器契约是否相互一致。"""
    agents_root = Path(agents_root)
    if _is_link_or_junction(agents_root):
        return [f"Agents 真源目录不能是软链或 junction：{agents_root}"]
    agents_root = agents_root.resolve()
    if not agents_root.is_dir():
        return [f"Agents 真源目录不存在：{agents_root}"]
    symlink = _find_descendant_symlink(agents_root)
    if symlink is not None:
        return [f"Agents 真源包含不允许的软链：{symlink}"]
    errors: list[str] = []
    try:
        catalog = load_catalog(agents_root)
        entries = _catalog_entries(catalog)
    except NovaSkillsError as exc:
        return [str(exc)]

    if catalog.get("schemaVersion") != CATALOG_SCHEMA_VERSION:
        errors.append(
            f"catalog.json 的 schemaVersion 必须为 {CATALOG_SCHEMA_VERSION}"
        )
    if catalog.get("package") != FRAMEWORK_PACKAGE_NAME:
        errors.append(f"catalog.json 的 package 必须为 {FRAMEWORK_PACKAGE_NAME}")
    unknown_catalog_fields = sorted(set(catalog) - CATALOG_FIELDS)
    if unknown_catalog_fields:
        errors.append(
            "catalog.json 包含未知字段：" + ", ".join(unknown_catalog_fields)
        )

    package_json = agents_root.parent / "package.json"
    try:
        package = _read_json(package_json)
        if package.get("name") != FRAMEWORK_PACKAGE_NAME:
            errors.append(f"{package_json} 未声明正确的 Framework 包名")
    except NovaSkillsError as exc:
        errors.append(str(exc))

    quick_start = agents_root.parent / "Docs" / "START_HERE.md"
    if not quick_start.is_file():
        errors.append(f"缺少项目组共同入口文档：{quick_start}")

    seen_ids: set[str] = set()
    known_ids: set[str] = set()
    contracts: list[tuple[str, dict[str, Any]]] = []
    entries_by_id: dict[str, dict[str, Any]] = {}
    replacements: dict[str, str] = {}
    for entry in entries:
        skill_id = entry.get("id")
        relative_path = entry.get("path")
        unknown_entry_fields = sorted(set(entry) - CATALOG_SKILL_FIELDS)
        if unknown_entry_fields:
            errors.append(
                f"{skill_id!r} 的 Catalog 条目包含未知字段："
                + ", ".join(unknown_entry_fields)
            )
        if not isinstance(skill_id, str) or not SKILL_NAME_PATTERN.fullmatch(skill_id):
            errors.append(f"Skill id 非法：{skill_id!r}")
            continue
        if not _is_managed_skill_id(skill_id):
            errors.append(f"{skill_id} 不是 Nova 项目组 Skill id，必须以 {PROJECT_SKILL_ID_PREFIX} 开头")
        if skill_id in seen_ids:
            errors.append(f"Skill id 重复：{skill_id}")
            continue
        seen_ids.add(skill_id)
        known_ids.add(skill_id)
        entries_by_id[skill_id] = entry

        kind = entry.get("kind")
        if kind not in SKILL_KINDS:
            errors.append(f"{skill_id} 的 kind 必须是 {sorted(SKILL_KINDS)} 之一")
        status = entry.get("status")
        if status not in SKILL_STATUSES:
            errors.append(
                f"{skill_id} 的 status {status!r} 必须是 {sorted(SKILL_STATUSES)} 之一"
            )
        replacement = entry.get("replacedBy")
        if status == "deprecated":
            if not isinstance(replacement, str) or not _is_managed_skill_id(replacement):
                errors.append(f"{skill_id} 已弃用时必须声明合法 replacedBy")
            else:
                replacements[skill_id] = replacement
        elif replacement is not None:
            errors.append(f"{skill_id} 仅 deprecated 状态可以声明 replacedBy")
        journeys = entry.get("journeys")
        if not isinstance(journeys, list) or not journeys or any(
            not isinstance(journey, str) for journey in journeys
        ):
            errors.append(f"{skill_id} 的 journeys 必须是非空字符串数组")
        effects = entry.get("effects")
        if not isinstance(effects, list) or not effects or any(
            not isinstance(effect, str) or effect not in SKILL_EFFECTS
            for effect in effects
        ):
            errors.append(f"{skill_id} 的 effects 必须是非空且受支持的数组")
        elif len(set(effects)) != len(effects):
            errors.append(f"{skill_id} 的 effects 不得包含重复项")
        minimum_evidence = entry.get("minimumEvidence")
        if minimum_evidence not in MINIMUM_EVIDENCE_LEVELS:
            errors.append(
                f"{skill_id} 的 minimumEvidence 必须是 {sorted(MINIMUM_EVIDENCE_LEVELS)} 之一"
            )
        elif isinstance(effects, list) and all(effect in SKILL_EFFECTS for effect in effects):
            # 构建会产生 Bundle 或 Player 产物，不能由一般编译或 Play 证据替代。
            if "build" in effects and minimum_evidence not in BUILD_ARTIFACT_EVIDENCE_LEVELS:
                errors.append(
                    f"{skill_id} 声明 build 副作用时 minimumEvidence 必须是构建产物级证据"
                )
            elif "build" not in effects and minimum_evidence in BUILD_ARTIFACT_EVIDENCE_LEVELS:
                errors.append(
                    f"{skill_id} 使用构建产物级证据时必须声明 build 副作用"
                )
        if not isinstance(relative_path, str):
            errors.append(f"{skill_id} 缺少 path")
            continue
        if relative_path != f"Skills/{skill_id}":
            errors.append(f"{skill_id} 必须位于平铺目录 Skills/{skill_id}")
        skill_dir = _safe_child(agents_root, relative_path)
        if skill_dir is None:
            errors.append(f"{skill_id} 的 path 越过 Agents 真源：{relative_path}")
            continue
        skill_file = skill_dir / "SKILL.md"
        contract_file = skill_dir / "references" / "contract.json"
        if not skill_file.is_file():
            errors.append(f"{skill_id} 缺少 SKILL.md：{skill_file}")
            continue
        if _read_skill_name(skill_file) != skill_id:
            errors.append(f"{skill_id} 与 SKILL.md frontmatter name 不一致")
        errors.extend(_validate_skill_progressive_disclosure(skill_id, skill_file))
        try:
            contract = _read_json(contract_file)
        except NovaSkillsError as exc:
            errors.append(str(exc))
            continue
        contracts.append((skill_id, contract))
        errors.extend(_validate_contract_shape(skill_id, contract))
        if contract.get("schemaVersion") != 1:
            errors.append(f"{skill_id} 的 contract.json schemaVersion 必须为 1")
        if contract.get("id") != skill_id:
            errors.append(f"{skill_id} 与 contract.json id 不一致")
        if contract.get("kind") not in SKILL_KINDS:
            errors.append(f"{skill_id} 的 contract.json 缺少有效 kind")
        elif contract.get("kind") != kind:
            errors.append(f"{skill_id} 与 contract.json kind 不一致")
        contract_effects = contract.get("effects")
        if not isinstance(contract_effects, list) or not contract_effects or any(
            effect not in SKILL_EFFECTS for effect in contract_effects
        ):
            errors.append(f"{skill_id} 的 contract.json 缺少有效 effects")
        elif contract_effects != effects:
            errors.append(f"{skill_id} 与 contract.json effects 不一致")
        contract_evidence = contract.get("minimumEvidence")
        if contract_evidence not in MINIMUM_EVIDENCE_LEVELS:
            errors.append(f"{skill_id} 的 contract.json 缺少有效 minimumEvidence")
        elif contract_evidence != minimum_evidence:
            errors.append(f"{skill_id} 与 contract.json minimumEvidence 不一致")

    # 只有声明 Agent Action 的 Skill 才要求当前源码树同时提供 Handler 与 MCP 网关。
    # 普通 C# API、Pipify 和 Unity 自动化仍是底层实现入口，不能被误判为可调度 Action。
    has_agent_action_adapter = any(
        isinstance(adapter, dict)
        and adapter.get("kind") in AGENT_ACTION_ADAPTER_KINDS
        for _, contract in contracts
        if isinstance(contract.get("actionAdapters"), list)
        for adapter in contract["actionAdapters"]
    )
    if has_agent_action_adapter:
        try:
            framework_root = agents_root.parent
            registered_action_ids = _discover_registered_agent_actions(framework_root)
            exposed_action_ids = _discover_exposed_agent_actions(framework_root)
        except NovaSkillsError as exc:
            errors.append(str(exc))
        else:
            missing_exposure = sorted(registered_action_ids - exposed_action_ids)
            stale_exposure = sorted(exposed_action_ids - registered_action_ids)
            if missing_exposure:
                errors.append(
                    "Framework 已注册 Project Action 未完整进入 MCP ExposurePolicy："
                    + ", ".join(missing_exposure)
                )
            if stale_exposure:
                errors.append(
                    "MCP ExposurePolicy 包含未注册 Project Action："
                    + ", ".join(stale_exposure)
                )
            for skill_id, contract in contracts:
                errors.extend(
                    _validate_agent_action_adapters(
                        skill_id,
                        contract,
                        registered_action_ids,
                        exposed_action_ids,
                    )
                )

    for skill_id, contract in contracts:
        requires = contract.get("requires")
        if not isinstance(requires, list):
            continue
        # requires 仅描述 Workflow 内部的 Operation DAG，不能成为安装或隐式执行依赖。
        skill_kind = entries_by_id[skill_id].get("kind")
        if requires and skill_kind != "workflow":
            errors.append(f"{skill_id} 的 contract.json requires 仅 Workflow 可声明")
        for required_id in requires:
            if required_id == skill_id:
                errors.append(f"{skill_id} 的 contract.json 不可依赖自身")
            elif required_id not in known_ids:
                errors.append(f"{skill_id} 的 contract.json 依赖不存在的 Skill：{required_id}")
            elif skill_kind == "workflow" and entries_by_id[required_id].get("kind") != "operation":
                errors.append(
                    f"{skill_id} 的 Workflow requires 只能依赖 Operation：{required_id}"
                )

    for skill_id, replacement in replacements.items():
        if replacement == skill_id:
            errors.append(f"{skill_id} 的 replacedBy 不可指向自身")
            continue
        target = entries_by_id.get(replacement)
        if target is None:
            errors.append(f"{skill_id} 的 replacedBy 指向不存在的 Skill：{replacement}")
        elif target.get("status") == "deprecated":
            errors.append(f"{skill_id} 的 replacedBy 不可继续指向已弃用 Skill：{replacement}")

    def append_cycles(graph: dict[str, list[str]], label: str) -> None:
        visited: set[str] = set()
        visiting: set[str] = set()

        def visit(skill_id: str) -> None:
            if skill_id in visiting:
                errors.append(f"{label} 出现循环：{skill_id}")
                return
            if skill_id in visited:
                return
            visiting.add(skill_id)
            for next_id in graph.get(skill_id, []):
                if next_id in graph:
                    visit(next_id)
            visiting.remove(skill_id)
            visited.add(skill_id)

        for graph_skill_id in sorted(graph):
            visit(graph_skill_id)

    append_cycles({skill_id: [replacement] for skill_id, replacement in replacements.items()}, "replacedBy")
    append_cycles(
        {
            skill_id: list(contract.get("requires", []))
            for skill_id, contract in contracts
            if isinstance(contract.get("requires"), list)
        },
        "requires",
    )

    skills_root = agents_root / "Skills"
    if not skills_root.is_dir():
        errors.append(f"缺少平铺 Skills 真源目录：{skills_root}")
    else:
        for child in sorted(skills_root.iterdir(), key=lambda item: item.name):
            if child.is_dir() and child.name not in known_ids:
                errors.append(f"Skills/{child.name} 未登记在 catalog.skills")

    capability_groups = catalog.get("capabilityGroups")
    if capability_groups is not None:
        if not isinstance(capability_groups, dict):
            errors.append("catalog.json 的 capabilityGroups 必须是对象")
        else:
            for group_name, group_ids in capability_groups.items():
                if not isinstance(group_name, str) or not isinstance(group_ids, list):
                    errors.append("capabilityGroups 的名称和值必须分别是字符串和数组")
                    continue
                seen_group_ids: set[str] = set()
                for skill_id in group_ids:
                    if not isinstance(skill_id, str):
                        errors.append(f"能力分组 {group_name} 只能引用字符串 Skill id")
                        continue
                    if skill_id in seen_group_ids:
                        errors.append(f"能力分组 {group_name} 重复引用 Skill：{skill_id}")
                        continue
                    seen_group_ids.add(skill_id)
                    if skill_id not in known_ids:
                        errors.append(f"能力分组 {group_name} 引用了不存在的 Skill：{skill_id}")
    return errors


def resolve_agents_root(
    project_root: Path, resolved_package_root: Path | None = None
) -> Path:
    """以已解析事实优先、再以 manifest 与 lock 交叉校验定位消费端 Framework Agents。"""
    project_root = Path(project_root).resolve()
    if resolved_package_root is not None:
        return _agents_from_package_root(resolved_package_root, "Unity PackageInfo.resolvedPath")

    manifest_path = project_root / "Packages" / "manifest.json"
    manifest = _read_json(manifest_path)
    dependencies = manifest.get("dependencies")
    if not isinstance(dependencies, dict):
        raise NovaSkillsError("Packages/manifest.json 缺少 dependencies 对象")
    dependency = dependencies.get(FRAMEWORK_PACKAGE_NAME)
    if not isinstance(dependency, str):
        raise NovaSkillsError("Packages/manifest.json 未声明 Nova Framework 依赖")
    lock_entry = _read_lock_entry(project_root)
    if isinstance(dependency, str) and dependency.startswith("file:"):
        _validate_file_lock(manifest_path, dependency, lock_entry)
        return _agents_from_package_root(
            _resolve_file_dependency(manifest_path, dependency), "file: 依赖"
        )

    embedded_package = project_root / "Packages" / FRAMEWORK_PACKAGE_NAME
    if lock_entry is not None and lock_entry.get("source") == "embedded":
        if not embedded_package.is_dir():
            raise NovaSkillsError("packages-lock.json 声明 embedded Framework，但包目录不存在")
        version = lock_entry.get("version")
        expected_version = version if isinstance(version, str) and ":" not in version else None
        return _agents_from_package_root(embedded_package, "嵌入包", expected_version)
    if embedded_package.is_dir():
        raise NovaSkillsError("发现嵌入包，但 packages-lock.json 未声明 source=embedded")
    if lock_entry is None:
        raise NovaSkillsError("未找到 Framework 的 packages-lock.json 条目，无法安全定位 PackageCache")
    return _package_cache_agents(project_root, lock_entry)


def _catalog_skill_ids(catalog: dict[str, Any]) -> list[str]:
    """按 Catalog 声明顺序返回全量可发现 Skill id，不提供选择性安装分支。"""
    return [str(entry["id"]) for entry in _catalog_entries(catalog)]


def _state_path(project_root: Path) -> Path:
    """返回消费者项目的非源内容受管状态文件路径。"""
    return project_root / ".agents" / STATE_FILE_NAME


def _managed_paths(project_root: Path) -> tuple[Path, Path]:
    """返回物理 `.agents` 路径，并拒绝任何项目内或项目外的链接重定向。"""
    project_root = Path(project_root).resolve()
    agents_dir = project_root / ".agents"
    target_root = agents_dir / "skills"
    state_path = agents_dir / STATE_FILE_NAME
    transaction_path = agents_dir / TRANSACTION_FILE_NAME
    staging_root = agents_dir / STAGING_DIRECTORY_NAME
    for label, path in (
        (".agents", agents_dir),
        (".agents/skills", target_root),
        (STATE_FILE_NAME, state_path),
        (TRANSACTION_FILE_NAME, transaction_path),
        (STAGING_DIRECTORY_NAME, staging_root),
    ):
        if _is_link_or_junction(path):
            raise NovaSkillsError(f"受管投影路径不能是软链或 junction：{label}")
    if agents_dir.exists() and not agents_dir.is_dir():
        raise NovaSkillsError(".agents 必须是目录，拒绝修改受管投影")
    if target_root.exists() and not target_root.is_dir():
        raise NovaSkillsError(".agents/skills 必须是目录，拒绝修改受管投影")
    if state_path.exists() and not state_path.is_file():
        raise NovaSkillsError(f"{STATE_FILE_NAME} 必须是普通文件，拒绝修改受管投影")
    if transaction_path.exists() and not transaction_path.is_file():
        raise NovaSkillsError(f"{TRANSACTION_FILE_NAME} 必须是普通文件，拒绝修改受管投影")
    if staging_root.exists() and not staging_root.is_dir():
        raise NovaSkillsError(f"{STAGING_DIRECTORY_NAME} 必须是目录，拒绝修改受管投影")
    return target_root, state_path


def _sync_lock_path(project_root: Path) -> Path:
    """返回 Library 中的跨语言共享锁，并拒绝父目录或锁文件链接重定向。"""
    project_root = Path(project_root).resolve()
    library_dir = project_root / "Library"
    nova_dir = library_dir / "Nova"
    lock_dir = nova_dir / "AgentSkills"
    lock_path = lock_dir / SYNC_LOCK_FILE_NAME
    for label, path in (
        ("Library", library_dir),
        ("Library/Nova", nova_dir),
        ("Library/Nova/AgentSkills", lock_dir),
        ("Library/Nova/AgentSkills/" + SYNC_LOCK_FILE_NAME, lock_path),
    ):
        if _is_link_or_junction(path):
            raise NovaSkillsError(f"共享同步锁路径不能是软链或 junction：{label}")
    for label, path in (
        ("Library", library_dir),
        ("Library/Nova", nova_dir),
        ("Library/Nova/AgentSkills", lock_dir),
    ):
        if path.exists() and not path.is_dir():
            raise NovaSkillsError(f"共享同步锁父路径必须是目录：{label}")
    if lock_path.exists() and not lock_path.is_file():
        raise NovaSkillsError(f"{SYNC_LOCK_FILE_NAME} 必须是普通文件，拒绝获取共享同步锁")
    return lock_path


def _validate_managed_entries(
    managed: Any, state_path: Path, label: str = "受管状态"
) -> dict[str, dict[str, Any]]:
    """校验可移动的受管哈希记录，拒绝把损坏 lock 当成用户目录所有权。"""
    if not isinstance(managed, dict):
        raise NovaSkillsError(f"{label}格式错误：{state_path}")
    normalized: dict[str, dict[str, Any]] = {}
    for skill_id, entry in managed.items():
        if not isinstance(skill_id, str) or not SKILL_NAME_PATTERN.fullmatch(skill_id):
            raise NovaSkillsError(f"{label}包含非法 Skill id：{skill_id!r}")
        if not _is_managed_skill_id(skill_id):
            raise NovaSkillsError(f"{label}包含非项目组 Skill id：{skill_id}")
        if not isinstance(entry, dict):
            raise NovaSkillsError(f"{label}中 {skill_id} 的记录必须是对象")
        normalized_entry: dict[str, Any] = {}
        for hash_name in ("sourceHash", "targetHash"):
            value = entry.get(hash_name)
            if not isinstance(value, str) or not SHA256_HEX_PATTERN.fullmatch(value):
                raise NovaSkillsError(f"{label}中 {skill_id} 的 {hash_name} 非法")
            normalized_entry[hash_name] = value
        normalized[skill_id] = normalized_entry
    return normalized


def _validate_state_header(state: dict[str, Any], state_path: Path, schema_version: int) -> None:
    """校验首发受管状态的包身份字段，防止其它包伪造 Nova 受管状态。"""
    if state.get("schemaVersion") != schema_version:
        raise NovaSkillsError(f"受管状态 schemaVersion 不受支持：{state_path}")
    if state.get("package") != FRAMEWORK_PACKAGE_NAME:
        raise NovaSkillsError(f"受管状态未声明正确的 Framework 包：{state_path}")
    if not isinstance(state.get("packageVersion"), str) or not state["packageVersion"]:
        raise NovaSkillsError(f"受管状态缺少 packageVersion：{state_path}")


def _read_managed_state(state_path: Path) -> dict[str, Any]:
    """读取唯一的全量受管状态；未知字段不能被静默当成兼容格式。"""
    state = _read_json(state_path)
    _validate_exact_fields(state, STATE_FIELDS, "受管状态")
    _validate_state_header(state, state_path, STATE_SCHEMA_VERSION)
    catalog_hash = state.get("catalogHash")
    if not isinstance(catalog_hash, str) or not SHA256_HEX_PATTERN.fullmatch(catalog_hash):
        raise NovaSkillsError(f"受管状态缺少合法 catalogHash：{state_path}")
    state["managed"] = _validate_managed_entries(state.get("managed"), state_path)
    return state


def _transaction_paths(state_path: Path) -> tuple[Path, Path]:
    """根据 lock 的固定位置派生事务日志和隐藏 staging 根目录。"""
    agents_dir = state_path.parent
    return agents_dir / TRANSACTION_FILE_NAME, agents_dir / STAGING_DIRECTORY_NAME


def _managed_state_payload(
    package: dict[str, Any], catalog_hash: str, managed: dict[str, dict[str, Any]]
) -> dict[str, Any]:
    """构造首发全量状态，不在状态中记录机器绝对路径或安装分组。"""
    package_name = package.get("name")
    package_version = package.get("version")
    if package_name != FRAMEWORK_PACKAGE_NAME or not isinstance(package_version, str) or not package_version:
        raise NovaSkillsError("Framework package.json 缺少可用的包名或版本，无法写入受管状态")
    if not SHA256_HEX_PATTERN.fullmatch(catalog_hash):
        raise NovaSkillsError("Catalog 哈希非法，无法写入受管状态")
    return {
        "schemaVersion": STATE_SCHEMA_VERSION,
        "package": package_name,
        "packageVersion": package_version,
        "catalogHash": catalog_hash,
        "managed": managed,
    }


def _read_state_for_reconcile(
    state_path: Path,
) -> tuple[dict[str, Any] | None, dict[str, dict[str, Any]]]:
    """读取唯一受管状态；目标冲突由后续规划逐项保留为 partial。"""
    if not state_path.is_file():
        return None, {}
    state = _read_managed_state(state_path)
    return state, {skill_id: dict(entry) for skill_id, entry in state["managed"].items()}


def _validate_transaction_state(
    state: Any, transaction_path: Path, label: str
) -> dict[str, dict[str, Any]]:
    """校验 journal 中前后状态的受管边界，并返回规范化的 managed 集合。"""
    if state is None:
        return {}
    if not isinstance(state, dict):
        raise NovaSkillsError(f"受管事务 {label} 格式错误：{transaction_path}")
    _validate_exact_fields(state, STATE_FIELDS, f"受管事务 {label}")
    _validate_state_header(state, transaction_path, STATE_SCHEMA_VERSION)
    catalog_hash = state.get("catalogHash")
    if not isinstance(catalog_hash, str) or not SHA256_HEX_PATTERN.fullmatch(catalog_hash):
        raise NovaSkillsError(f"受管事务 {label} 缺少合法 catalogHash：{transaction_path}")
    managed = _validate_managed_entries(state.get("managed"), transaction_path, f"受管事务 {label}")
    state["managed"] = managed
    return managed


def _validate_exact_fields(
    value: dict[str, Any], expected_fields: set[str], label: str
) -> None:
    """要求首发持久化对象字段集合完全匹配，避免伪造或混入未支持语义。"""
    missing_fields = sorted(expected_fields - set(value))
    unknown_fields = sorted(set(value) - expected_fields)
    if unknown_fields:
        raise NovaSkillsError(f"{label} 包含未知字段：{', '.join(unknown_fields)}")
    if missing_fields:
        raise NovaSkillsError(f"{label} 缺少字段：{', '.join(missing_fields)}")


def _validate_transaction(
    transaction: dict[str, Any], transaction_path: Path
) -> dict[str, Any]:
    """校验新增、更新、删除 journal 的状态转换与哈希约束。"""
    _validate_exact_fields(transaction, TRANSACTION_FIELDS, "受管事务")
    transaction_id = transaction.get("transactionId")
    if not isinstance(transaction_id, str) or not TRANSACTION_ID_PATTERN.fullmatch(transaction_id):
        raise NovaSkillsError(f"受管事务缺少合法 transactionId：{transaction_path}")
    previous_state = transaction.get("previousState")
    previous_managed = _validate_transaction_state(
        previous_state, transaction_path, "previousState"
    )
    final_state = transaction.get("finalState")
    if not isinstance(final_state, dict) or final_state.get("schemaVersion") != STATE_SCHEMA_VERSION:
        raise NovaSkillsError(f"受管事务 finalState schemaVersion 不受支持：{transaction_path}")
    managed = _validate_transaction_state(final_state, transaction_path, "finalState")
    pending = transaction.get("pending")
    if not isinstance(pending, list) or not pending:
        raise NovaSkillsError(f"受管事务 pending 必须是非空数组：{transaction_path}")
    seen_ids: set[str] = set()
    normalized_pending: list[dict[str, str]] = []
    for item in pending:
        if not isinstance(item, dict):
            raise NovaSkillsError(f"受管事务 pending 包含非法条目：{transaction_path}")
        action = item.get("action")
        if action not in RECONCILE_ACTIONS:
            raise NovaSkillsError(f"受管事务 pending 包含未知 action：{transaction_path}")
        skill_id = item.get("id")
        if not isinstance(skill_id, str) or not SKILL_NAME_PATTERN.fullmatch(skill_id):
            raise NovaSkillsError(f"受管事务 pending 包含非法 Skill id：{transaction_path}")
        if not _is_managed_skill_id(skill_id):
            raise NovaSkillsError(f"受管事务 pending 包含非项目组 Skill id：{skill_id}")
        if skill_id in seen_ids:
            raise NovaSkillsError(f"受管事务 pending 重复 Skill：{skill_id}")
        seen_ids.add(skill_id)
        normalized_item: dict[str, str] = {"action": action, "id": skill_id}
        if action in {"add", "update"}:
            for hash_name in ("sourceHash", "targetHash"):
                value = item.get(hash_name)
                if not isinstance(value, str) or not SHA256_HEX_PATTERN.fullmatch(value):
                    raise NovaSkillsError(f"受管事务中 {skill_id} 的 {hash_name} 非法")
                normalized_item[hash_name] = value
        if action in {"update", "remove"}:
            previous_target_hash = item.get("previousTargetHash")
            if not isinstance(previous_target_hash, str) or not SHA256_HEX_PATTERN.fullmatch(
                previous_target_hash
            ):
                raise NovaSkillsError(f"受管事务中 {skill_id} 的 previousTargetHash 非法")
            normalized_item["previousTargetHash"] = previous_target_hash
        normalized_pending.append(normalized_item)

    expected_managed = {
        skill_id: dict(entry) for skill_id, entry in previous_managed.items()
    }
    for item in normalized_pending:
        action = item["action"]
        skill_id = item["id"]
        if action == "add":
            if skill_id in expected_managed:
                raise NovaSkillsError(
                    f"受管事务 add 不能覆盖 previousState 已经受管的 Skill：{skill_id}"
                )
            expected_managed[skill_id] = {
                "sourceHash": item["sourceHash"],
                "targetHash": item["targetHash"],
            }
            continue
        previous_entry = expected_managed.get(skill_id)
        if previous_entry is None or previous_entry["targetHash"] != item["previousTargetHash"]:
            raise NovaSkillsError(
                f"受管事务 {skill_id} 的 previousTargetHash 与 previousState 不一致"
            )
        if action == "update":
            expected_managed[skill_id] = {
                "sourceHash": item["sourceHash"],
                "targetHash": item["targetHash"],
            }
        else:
            expected_managed.pop(skill_id)
    if managed != expected_managed:
        raise NovaSkillsError(
            f"受管事务 finalState 与 previousState/pending 不一致：{transaction_path}"
        )
    return transaction


def _read_transaction(transaction_path: Path) -> dict[str, Any]:
    """读取唯一事务格式，并拒绝任何不能安全证明的状态转换。"""
    transaction = _read_json(transaction_path)
    if transaction.get("schemaVersion") != TRANSACTION_SCHEMA_VERSION:
        raise NovaSkillsError(f"受管事务 schemaVersion 不受支持：{transaction_path}")
    return _validate_transaction(transaction, transaction_path)


def _acquire_kernel_lock(file_descriptor: int) -> str:
    """获取内核级非阻塞排他锁；进程异常退出时由操作系统自动释放。"""
    if fcntl is not None:
        try:
            # 固定首字节的 POSIX record lock，可与 C# FileStream.Lock(0, 1) 互斥。
            fcntl.lockf(file_descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB, 1, 0, os.SEEK_SET)
        except BlockingIOError as exc:
            raise NovaSkillsError("另一个 Nova Skill 同步正在进行，请等待其完成后再重试") from exc
        except OSError as exc:
            raise NovaSkillsError(f"无法获取安全同步锁：{exc}") from exc
        return "fcntl"
    if msvcrt is not None:  # pragma: no cover - 在 Windows CI/Editor Host 上覆盖。
        try:
            if os.fstat(file_descriptor).st_size == 0:
                os.write(file_descriptor, b"\0")
                os.fsync(file_descriptor)
            os.lseek(file_descriptor, 0, os.SEEK_SET)
            msvcrt.locking(file_descriptor, msvcrt.LK_NBLCK, 1)
        except OSError as exc:
            raise NovaSkillsError("另一个 Nova Skill 同步正在进行，请等待其完成后再重试") from exc
        return "msvcrt"
    raise NovaSkillsError("当前 Python 平台不支持安全的跨进程同步锁")


def _release_kernel_lock(file_descriptor: int, lock_kind: str) -> None:
    """释放当前进程持有的内核锁；文件 inode 保留以避免删除锁导致的新竞态。"""
    if lock_kind == "fcntl" and fcntl is not None:
        fcntl.lockf(file_descriptor, fcntl.LOCK_UN, 1, 0, os.SEEK_SET)
    elif lock_kind == "msvcrt" and msvcrt is not None:  # pragma: no cover - Windows 平台。
        os.lseek(file_descriptor, 0, os.SEEK_SET)
        msvcrt.locking(file_descriptor, msvcrt.LK_UNLCK, 1)


def _write_lock_metadata(file_descriptor: int, owner: dict[str, Any]) -> None:
    """在已持有内核锁后写入诊断 owner，不参与锁正确性判断。"""
    payload = json.dumps(owner, ensure_ascii=False, sort_keys=True).encode("utf-8")
    os.lseek(file_descriptor, 0, os.SEEK_SET)
    os.ftruncate(file_descriptor, 0)
    written = 0
    while written < len(payload):
        written += os.write(file_descriptor, payload[written:])
    os.fsync(file_descriptor)


def _acquire_process_lock(lock_path: Path) -> str:
    """登记当前进程内的锁路径，补足 POSIX record lock 允许同进程重入的语义。"""
    lock_key = str(lock_path.resolve(strict=False))
    with _PROCESS_LOCK_GUARD:
        if lock_key in _PROCESS_LOCK_PATHS:
            raise NovaSkillsError("另一个 Nova Skill 同步正在进行，请等待其完成后再重试")
        _PROCESS_LOCK_PATHS.add(lock_key)
    return lock_key


def _release_process_lock(lock_key: str) -> None:
    """释放当前进程内的共享锁登记，保证异常路径也可再次同步。"""
    with _PROCESS_LOCK_GUARD:
        _PROCESS_LOCK_PATHS.discard(lock_key)


@contextmanager
def _projection_sync_lock(project_root: Path) -> Any:
    """以 Library 持久文件和进程内门闩串行化跨语言全量 reconcile。"""
    _managed_paths(project_root)
    lock_path = _sync_lock_path(project_root)
    lock_path.parent.mkdir(parents=True, exist_ok=True)
    lock_path = _sync_lock_path(project_root)
    lock_key = _acquire_process_lock(lock_path)
    flags = os.O_RDWR | os.O_CREAT
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        try:
            file_descriptor = os.open(lock_path, flags, 0o600)
        except OSError as exc:
            raise NovaSkillsError(f"无法打开安全同步锁：{exc}") from exc
        lock_kind: str | None = None
        try:
            lock_kind = _acquire_kernel_lock(file_descriptor)
            _managed_paths(project_root)
            _sync_lock_path(project_root)
            _write_lock_metadata(
                file_descriptor,
                {"schemaVersion": 1, "processId": os.getpid(), "token": uuid.uuid4().hex},
            )
            yield
        finally:
            if lock_kind is not None:
                try:
                    _release_kernel_lock(file_descriptor, lock_kind)
                except OSError:
                    pass
            os.close(file_descriptor)
    finally:
        _release_process_lock(lock_key)


def _begin_transaction(
    project_root: Path,
    target_root: Path,
    state_path: Path,
    previous_state: dict[str, Any] | None,
    final_state: dict[str, Any],
    planned_actions: list[dict[str, Any]],
) -> dict[str, Any]:
    """将新增/更新内容写入 staging，再原子登记可恢复的全量 reconcile 事务。"""
    transaction_path, staging_root = _transaction_paths(state_path)
    _managed_paths(project_root)
    target_root.mkdir(parents=True, exist_ok=True)
    staging_root.mkdir(parents=True, exist_ok=True)
    _managed_paths(project_root)
    transaction_id = uuid.uuid4().hex
    staging_dir = staging_root / transaction_id
    staging_dir.mkdir()
    staged_new_root = staging_dir / "new"
    staged_new_root.mkdir()
    pending: list[dict[str, Any]] = []
    journal_written = False
    try:
        for action in planned_actions:
            action_name = action["action"]
            skill_id = action["id"]
            pending_item: dict[str, Any] = {"action": action_name, "id": skill_id}
            if action_name in {"add", "update"}:
                source_dir = action["sourceDir"]
                source_hash = action["sourceHash"]
                staged_skill = staged_new_root / skill_id
                shutil.copytree(source_dir, staged_skill)
                target_hash = _tree_hash(staged_skill)
                source_hash_after_copy = _tree_hash(source_dir)
                if source_hash != target_hash or source_hash_after_copy != source_hash:
                    raise NovaSkillsError(
                        f"复制 {skill_id} 时 Framework 真源发生变化，拒绝登记混合版本投影"
                    )
                pending_item["sourceHash"] = source_hash
                pending_item["targetHash"] = target_hash
            if action_name in {"update", "remove"}:
                pending_item["previousTargetHash"] = action["previousTargetHash"]
            pending.append(pending_item)
        transaction = {
            "schemaVersion": TRANSACTION_SCHEMA_VERSION,
            "transactionId": transaction_id,
            "previousState": previous_state,
            "finalState": final_state,
            "pending": pending,
        }
        _managed_paths(project_root)
        _write_json_atomically(transaction_path, transaction)
        journal_written = True
        return transaction
    except BaseException:
        if not journal_written:
            shutil.rmtree(staging_dir, ignore_errors=True)
            try:
                staging_root.rmdir()
            except OSError:
                pass
        raise


def _resume_transaction(project_root: Path, target_root: Path, state_path: Path) -> bool:
    """续传新增、更新或删除事务；最终目标一致后才原子写入最终 state。"""
    transaction_path, staging_root = _transaction_paths(state_path)
    if not transaction_path.is_file():
        return False
    transaction = _read_transaction(transaction_path)
    transaction_id = transaction["transactionId"]
    staging_dir = staging_root / transaction_id
    if _is_link_or_junction(staging_dir) or not staging_dir.is_dir():
        raise NovaSkillsError(f"受管事务 staging 不存在或不是普通目录：{staging_dir}")
    if _find_descendant_symlink(staging_dir) is not None:
        raise NovaSkillsError(f"受管事务 staging 包含软链或 junction：{staging_dir}")
    previous_state = transaction["previousState"]
    final_state = transaction["finalState"]
    current_state = _read_json(state_path) if state_path.is_file() else None
    if current_state != previous_state and current_state != final_state:
        raise NovaSkillsError("受管状态已在中断事务期间变化，拒绝覆盖并等待人工处理")

    _managed_paths(project_root)
    target_root.mkdir(parents=True, exist_ok=True)
    staged_new_root = staging_dir / "new"
    backup_root = staging_dir / "backup"
    for item in transaction["pending"]:
        action_name = item["action"]
        skill_id = item["id"]
        target_dir = target_root / skill_id
        if _is_link_or_junction(target_dir):
            raise NovaSkillsError(f"中断事务目标是软链，拒绝恢复：{target_dir}")
        staged_skill = staged_new_root / skill_id
        backup_skill = backup_root / skill_id
        if _is_link_or_junction(staged_skill) or _is_link_or_junction(backup_skill):
            raise NovaSkillsError(f"中断事务 staging 包含链接目标：{skill_id}")

        if action_name == "add":
            expected_hash = item["targetHash"]
            if target_dir.exists():
                if not target_dir.is_dir() or _tree_hash(target_dir) != expected_hash:
                    raise NovaSkillsError(f"中断新增目标已变化，拒绝恢复：{target_dir}")
                continue
            if not staged_skill.is_dir() or _tree_hash(staged_skill) != expected_hash:
                raise NovaSkillsError(f"中断新增 staging 已变化：{staged_skill}")
            _managed_paths(project_root)
            os.replace(staged_skill, target_dir)
            if _tree_hash(target_dir) != expected_hash:
                raise NovaSkillsError(f"中断新增恢复后哈希不一致：{target_dir}")
            continue

        previous_target_hash = item["previousTargetHash"]
        if action_name == "update":
            expected_hash = item["targetHash"]
            if target_dir.exists() and target_dir.is_dir() and _tree_hash(target_dir) == expected_hash:
                continue
            if not target_dir.exists() and not backup_skill.exists():
                raise NovaSkillsError(
                    f"中断更新目标与备份都缺失，无法安全恢复：{skill_id}"
                )
            if target_dir.exists() and (
                not target_dir.is_dir() or _tree_hash(target_dir) != previous_target_hash
            ):
                raise NovaSkillsError(f"中断更新目标已变化，拒绝恢复：{target_dir}")
            if backup_skill.exists() and (
                not backup_skill.is_dir() or _tree_hash(backup_skill) != previous_target_hash
            ):
                raise NovaSkillsError(f"中断更新备份已变化，拒绝恢复：{backup_skill}")
            if target_dir.exists():
                if backup_skill.exists():
                    raise NovaSkillsError(f"中断更新同时存在旧目标与备份：{skill_id}")
                backup_root.mkdir(exist_ok=True)
                _managed_paths(project_root)
                os.replace(target_dir, backup_skill)
            if not staged_skill.is_dir() or _tree_hash(staged_skill) != expected_hash:
                raise NovaSkillsError(f"中断更新 staging 已变化：{staged_skill}")
            _managed_paths(project_root)
            os.replace(staged_skill, target_dir)
            if _tree_hash(target_dir) != expected_hash:
                raise NovaSkillsError(f"中断更新恢复后哈希不一致：{target_dir}")
            continue

        if action_name == "remove":
            if target_dir.exists():
                if not target_dir.is_dir() or _tree_hash(target_dir) != previous_target_hash:
                    raise NovaSkillsError(f"中断删除目标已变化，拒绝恢复：{target_dir}")
                if backup_skill.exists():
                    raise NovaSkillsError(f"中断删除同时存在目标与备份：{skill_id}")
                backup_root.mkdir(exist_ok=True)
                _managed_paths(project_root)
                os.replace(target_dir, backup_skill)
            if not backup_skill.exists():
                # 目标在建立事务前或恢复前已不存在。此时没有可删除的用户内容，
                # 删除受管记录即可完成该幂等 remove 动作。
                continue
            if not backup_skill.is_dir() or _tree_hash(backup_skill) != previous_target_hash:
                raise NovaSkillsError(f"中断删除备份已变化，拒绝恢复：{backup_skill}")
            continue

        raise NovaSkillsError(f"中断事务包含未知 action：{action_name}")

    _managed_paths(project_root)
    latest_state = _read_json(state_path) if state_path.is_file() else None
    if latest_state != current_state and latest_state != final_state:
        raise NovaSkillsError("受管状态在事务完成前发生变化，拒绝覆盖并等待人工处理")
    if latest_state != final_state:
        _write_json_atomically(state_path, final_state)
    _managed_paths(project_root)
    transaction_path.unlink()
    shutil.rmtree(staging_dir)
    try:
        staging_root.rmdir()
    except OSError:
        pass
    return True


def _plan_reconcile(
    project_root: Path, agents_root: Path, dry_run: bool
) -> tuple[
    Path,
    Path,
    dict[str, Any],
    dict[str, Any] | None,
    dict[str, Any],
    list[dict[str, Any]],
    dict[str, Any],
]:
    """冻结全量 Catalog 与消费者当前状态，规划安全的新增、更新、删除和冲突。"""
    errors = validate_agents_root(agents_root)
    if errors:
        raise NovaSkillsError("Agents 真源校验失败：\n- " + "\n- ".join(errors))
    agents_root = Path(agents_root).resolve()
    catalog = load_catalog(agents_root)
    entries = _catalog_entries(catalog)
    target_root, state_path = _managed_paths(project_root)
    previous_state, managed = _read_state_for_reconcile(state_path)
    final_managed = {skill_id: dict(entry) for skill_id, entry in managed.items()}
    catalog_ids = _catalog_skill_ids(catalog)
    catalog_id_set = set(catalog_ids)
    actions: list[dict[str, Any]] = []
    added: list[str] = []
    updated: list[str] = []
    removed: list[str] = []
    unchanged: list[str] = []
    conflicts: list[dict[str, str]] = []

    for entry in entries:
        skill_id = str(entry["id"])
        source_dir = _safe_child(agents_root, entry["path"])
        if source_dir is None or not source_dir.is_dir():
            raise NovaSkillsError(f"Skill 路径不安全：{skill_id}")
        target_dir = target_root / skill_id
        source_hash = _tree_hash(source_dir)
        existing = managed.get(skill_id)
        if _is_link_or_junction(target_dir):
            conflicts.append({"id": skill_id, "reason": "unsafe-link"})
            continue
        if target_dir.exists():
            if not target_dir.is_dir():
                conflicts.append(
                    {
                        "id": skill_id,
                        "reason": "modified-managed"
                        if skill_id in managed
                        else "unowned-collision",
                    }
                )
                continue
            if not isinstance(existing, dict):
                conflicts.append({"id": skill_id, "reason": "unowned-collision"})
                continue
            try:
                target_hash = _tree_hash(target_dir)
            except NovaSkillsError:
                conflicts.append({"id": skill_id, "reason": "unsafe-link"})
                continue
            if target_hash != existing["targetHash"]:
                conflicts.append({"id": skill_id, "reason": "modified-managed"})
                continue
            if source_hash == existing["sourceHash"]:
                unchanged.append(skill_id)
                continue
            actions.append(
                {
                    "action": "update",
                    "id": skill_id,
                    "sourceDir": source_dir,
                    "sourceHash": source_hash,
                    "previousTargetHash": existing["targetHash"],
                }
            )
            final_managed[skill_id] = {"sourceHash": source_hash, "targetHash": source_hash}
            updated.append(skill_id)
            continue
        if isinstance(existing, dict):
            conflicts.append({"id": skill_id, "reason": "missing-managed"})
            continue
        actions.append(
            {
                "action": "add",
                "id": skill_id,
                "sourceDir": source_dir,
                "sourceHash": source_hash,
            }
        )
        final_managed[skill_id] = {"sourceHash": source_hash, "targetHash": source_hash}
        added.append(skill_id)

    for skill_id in sorted(skill_id for skill_id in managed if skill_id not in catalog_id_set):
        target_dir = target_root / skill_id
        existing = managed[skill_id]
        if _is_link_or_junction(target_dir):
            conflicts.append({"id": skill_id, "reason": "unsafe-link"})
            continue
        if not target_dir.exists():
            actions.append(
                {
                    "action": "remove",
                    "id": skill_id,
                    "previousTargetHash": existing["targetHash"],
                }
            )
            final_managed.pop(skill_id, None)
            removed.append(skill_id)
            continue
        if not target_dir.is_dir():
            conflicts.append({"id": skill_id, "reason": "modified-managed"})
            continue
        try:
            target_hash = _tree_hash(target_dir)
        except NovaSkillsError:
            conflicts.append({"id": skill_id, "reason": "unsafe-link"})
            continue
        if target_hash != existing["targetHash"]:
            conflicts.append({"id": skill_id, "reason": "modified-managed"})
            continue
        actions.append(
            {
                "action": "remove",
                "id": skill_id,
                "previousTargetHash": existing["targetHash"],
            }
        )
        final_managed.pop(skill_id, None)
        removed.append(skill_id)

    package = _read_json(agents_root.parent / "package.json")
    final_state = _managed_state_payload(
        package, _file_hash(agents_root / CATALOG_FILE_NAME), final_managed
    )
    result = {
        "status": "partial" if conflicts else "success",
        "agentsRoot": str(agents_root),
        "packageVersion": package["version"],
        "added": added,
        "updated": updated,
        "removed": removed,
        "unchanged": unchanged,
        "conflicts": conflicts,
        "dryRun": dry_run,
    }
    return target_root, state_path, package, previous_state, final_state, actions, result


def reconcile(
    project_root: Path, agents_root: Path | None = None, dry_run: bool = False
) -> dict[str, Any]:
    """将当前 Framework Catalog 的全部项目组 Skill 安全桥接到消费者 `.agents/skills`。"""
    project_root = Path(project_root).resolve()
    _, state_path = _managed_paths(project_root)
    transaction_path, _ = _transaction_paths(state_path)
    if dry_run and transaction_path.is_file():
        raise NovaSkillsError("存在未完成的 Nova Skill 投影事务；请先不带 --dry-run 重试以恢复")

    if not dry_run and transaction_path.is_file():
        with _projection_sync_lock(project_root):
            target_root, current_state_path = _managed_paths(project_root)
            _resume_transaction(project_root, target_root, current_state_path)

    resolved_agents_root = (
        resolve_agents_root(project_root) if agents_root is None else Path(agents_root)
    )
    if dry_run:
        *_, result = _plan_reconcile(project_root, resolved_agents_root, dry_run=True)
        return result

    with _projection_sync_lock(project_root):
        target_root, current_state_path = _managed_paths(project_root)
        _resume_transaction(project_root, target_root, current_state_path)
        (
            target_root,
            current_state_path,
            package,
            previous_state,
            final_state,
            planned_actions,
            result,
        ) = _plan_reconcile(project_root, resolved_agents_root, dry_run=False)
        if not planned_actions:
            _managed_paths(project_root)
            current_state = _read_json(current_state_path) if current_state_path.is_file() else None
            if current_state != previous_state and current_state != final_state:
                raise NovaSkillsError("受管状态在 reconcile 规划后发生变化，拒绝覆盖")
            if current_state != final_state:
                _write_json_atomically(current_state_path, final_state)
            return result

        _begin_transaction(
            project_root,
            target_root,
            current_state_path,
            previous_state,
            final_state,
            planned_actions,
        )
        _resume_transaction(project_root, target_root, current_state_path)
        return result


def doctor(project_root: Path) -> dict[str, list[str]]:
    """只读比较当前已解析 Framework 与受管投影，报告缺失、修改和来源漂移。"""
    project_root = Path(project_root).resolve()
    target_root, state_path = _managed_paths(project_root)
    transaction_path, _ = _transaction_paths(state_path)
    report = {
        "missing": [],
        "modified": [],
        "sourceChanged": [],
        "resolutionChanged": [],
        "uninitialized": [],
        "interrupted": [],
    }
    if transaction_path.is_file():
        report["interrupted"].append(TRANSACTION_FILE_NAME)
    if not state_path.is_file():
        report["uninitialized"].append(STATE_FILE_NAME)
        return report
    state = _read_managed_state(state_path)
    managed = state["managed"]

    current_agents_root: Path | None = None
    current_entries: dict[str, dict[str, Any]] = {}
    expected_ids: set[str] = set(managed)
    try:
        current_agents_root = resolve_agents_root(project_root)
        validation_errors = validate_agents_root(current_agents_root)
        if validation_errors:
            raise NovaSkillsError("当前 Framework Agents 真源校验失败")
        catalog = load_catalog(current_agents_root)
        current_entries = {entry["id"]: entry for entry in _catalog_entries(catalog)}
        expected_ids = set(_catalog_skill_ids(catalog))
        for skill_id in expected_ids:
            if skill_id not in managed:
                report["missing"].append(skill_id)
        for skill_id in managed:
            if skill_id not in expected_ids:
                report["sourceChanged"].append(skill_id)
        package = _read_json(current_agents_root.parent / "package.json")
        if state.get("packageVersion") != package.get("version"):
            report["resolutionChanged"].append("package-version")
        if state.get("catalogHash") != _file_hash(current_agents_root / CATALOG_FILE_NAME):
            report["resolutionChanged"].append("catalog")
    except NovaSkillsError:
        report["resolutionChanged"].append("unresolved")
        current_agents_root = None

    for skill_id in sorted(managed):
        entry = managed.get(skill_id)
        if entry is None:
            continue
        target = target_root / skill_id
        if not target.is_dir():
            report["missing"].append(skill_id)
        elif _is_link_or_junction(target):
            report["modified"].append(skill_id)
        else:
            try:
                if _tree_hash(target) != entry["targetHash"]:
                    report["modified"].append(skill_id)
            except NovaSkillsError:
                report["modified"].append(skill_id)

        if current_agents_root is None:
            continue
        current_entry = current_entries.get(skill_id)
        if not isinstance(current_entry, dict):
            report["sourceChanged"].append(skill_id)
            continue
        source_dir = _safe_child(current_agents_root, str(current_entry.get("path", "")))
        if source_dir is None or not source_dir.is_dir():
            report["sourceChanged"].append(skill_id)
            continue
        if _tree_hash(source_dir) != entry["sourceHash"]:
            report["sourceChanged"].append(skill_id)
    for key, values in report.items():
        report[key] = sorted(set(values))
    return report


def _build_parser() -> argparse.ArgumentParser:
    """构建 CLI 参数，所有子命令均可用于 CI 或 Agent Action Adapter。"""
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    validate_parser = subparsers.add_parser("validate", help="校验 Agents 真源")
    validate_parser.add_argument("--agents-root", required=True, type=Path)
    resolve_parser = subparsers.add_parser("resolve", help="定位消费者已安装的 Framework Agents")
    resolve_parser.add_argument("--project-root", required=True, type=Path)
    reconcile_parser = subparsers.add_parser(
        "reconcile", help="全量桥接当前 Framework Catalog 到 .agents/skills"
    )
    reconcile_parser.add_argument("--project-root", required=True, type=Path)
    reconcile_parser.add_argument("--agents-root", type=Path)
    reconcile_parser.add_argument("--dry-run", action="store_true")
    doctor_parser = subparsers.add_parser("doctor", help="只读诊断受管投影漂移")
    doctor_parser.add_argument("--project-root", required=True, type=Path)
    return parser


def main(argv: list[str] | None = None) -> int:
    """执行 CLI，并将可机器解析结果写到标准输出。"""
    args = _build_parser().parse_args(argv)
    try:
        if args.command == "validate":
            errors = validate_agents_root(args.agents_root)
            result: dict[str, Any] = {"valid": not errors, "errors": errors}
            exit_code = 0 if not errors else 1
        elif args.command == "resolve":
            result = {"agentsRoot": str(resolve_agents_root(args.project_root))}
            exit_code = 0
        elif args.command == "reconcile":
            result = reconcile(args.project_root, args.agents_root, args.dry_run)
            exit_code = 1 if result.get("status") == "partial" else 0
        else:
            result = doctor(args.project_root)
            exit_code = 0 if not any(result.values()) else 1
    except NovaSkillsError as exc:
        result = {"error": str(exc)}
        exit_code = 2
    print(json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True))
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
