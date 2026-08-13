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
CATALOG_FILE_NAME = "catalog.json"
STATE_FILE_NAME = "nova-skills.lock.json"
TRANSACTION_FILE_NAME = "nova-skills.transaction.json"
STAGING_DIRECTORY_NAME = ".nova-skills-staging"
SYNC_LOCK_FILE_NAME = ".nova-skills-sync.lock"
SKILL_NAME_PATTERN = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
TRANSACTION_ID_PATTERN = re.compile(r"^[0-9a-f]{32}$")
SKILL_KINDS = {"router", "operation", "workflow"}
SKILL_EFFECTS = {"read", "workspace-write", "unity-write", "generated-output"}
MINIMUM_EVIDENCE_LEVELS = {"static", "compile", "play"}
CONTRACT_IDEMPOTENCY = {"read-only", "ensure-state", "orchestrate"}
CONTRACT_RESULT_STATES = {"success", "partial", "blocked", "not_applicable"}
SHA256_HEX_PATTERN = re.compile(r"^[0-9a-f]{64}$")


class NovaSkillsError(RuntimeError):
    """表示无法在不越权或不覆盖用户内容的前提下继续执行。"""


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
    for path in sorted(candidate for candidate in directory.rglob("*") if candidate.is_file()):
        relative_path = path.relative_to(directory).as_posix().encode("utf-8")
        digest.update(relative_path)
        digest.update(b"\0")
        digest.update(path.read_bytes())
        digest.update(b"\0")
    return digest.hexdigest()


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
        package_root = cache_root / f"{FRAMEWORK_PACKAGE_NAME}@{version}"
        if not package_root.is_dir():
            raise NovaSkillsError("PackageCache 中未找到 lock 指定版本的 Framework 包")
        return _agents_from_package_root(package_root, "PackageCache", version)

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

    if catalog.get("schemaVersion") != 1:
        errors.append("catalog.json 的 schemaVersion 必须为 1")
    if catalog.get("package") != FRAMEWORK_PACKAGE_NAME:
        errors.append(f"catalog.json 的 package 必须为 {FRAMEWORK_PACKAGE_NAME}")

    package_json = agents_root.parent / "package.json"
    try:
        package = _read_json(package_json)
        if package.get("name") != FRAMEWORK_PACKAGE_NAME:
            errors.append(f"{package_json} 未声明正确的 Framework 包名")
    except NovaSkillsError as exc:
        errors.append(str(exc))

    seen_ids: set[str] = set()
    known_ids: set[str] = set()
    contracts: list[tuple[str, dict[str, Any]]] = []
    for entry in entries:
        skill_id = entry.get("id")
        relative_path = entry.get("path")
        if not isinstance(skill_id, str) or not SKILL_NAME_PATTERN.fullmatch(skill_id):
            errors.append(f"Skill id 非法：{skill_id!r}")
            continue
        if skill_id in seen_ids:
            errors.append(f"Skill id 重复：{skill_id}")
            continue
        seen_ids.add(skill_id)
        known_ids.add(skill_id)

        kind = entry.get("kind")
        if kind not in SKILL_KINDS:
            errors.append(f"{skill_id} 的 kind 必须是 {sorted(SKILL_KINDS)} 之一")
        effects = entry.get("effects")
        if not isinstance(effects, list) or not effects or any(
            effect not in SKILL_EFFECTS for effect in effects
        ):
            errors.append(f"{skill_id} 的 effects 必须是非空且受支持的数组")
        minimum_evidence = entry.get("minimumEvidence")
        if minimum_evidence not in MINIMUM_EVIDENCE_LEVELS:
            errors.append(
                f"{skill_id} 的 minimumEvidence 必须是 {sorted(MINIMUM_EVIDENCE_LEVELS)} 之一"
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

    for skill_id, contract in contracts:
        requires = contract.get("requires")
        if not isinstance(requires, list):
            continue
        for required_id in requires:
            if required_id == skill_id:
                errors.append(f"{skill_id} 的 contract.json 不可依赖自身")
            elif required_id not in known_ids:
                errors.append(f"{skill_id} 的 contract.json 依赖不存在的 Skill：{required_id}")

    profiles = catalog.get("profiles")
    if not isinstance(profiles, dict):
        errors.append("catalog.json 的 profiles 必须是对象")
    else:
        for profile_name, profile_ids in profiles.items():
            if not isinstance(profile_name, str) or not isinstance(profile_ids, list):
                errors.append("profiles 的名称和值必须分别是字符串和数组")
                continue
            seen_profile_ids: set[str] = set()
            for skill_id in profile_ids:
                if not isinstance(skill_id, str):
                    errors.append(f"Profile {profile_name} 只能引用字符串 Skill id")
                    continue
                if skill_id in seen_profile_ids:
                    errors.append(f"Profile {profile_name} 重复引用 Skill：{skill_id}")
                    continue
                seen_profile_ids.add(skill_id)
                if skill_id not in known_ids:
                    errors.append(f"Profile {profile_name} 引用了不存在的 Skill：{skill_id}")
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


def _select_profile(catalog: dict[str, Any], profile: str) -> list[dict[str, Any]]:
    """按 Profile 选择显式允许投影的条目，保持 Catalog 声明顺序。"""
    profiles = catalog.get("profiles")
    if not isinstance(profiles, dict) or profile not in profiles:
        raise NovaSkillsError(f"不存在的 Skill Profile：{profile}")
    requested_ids = profiles[profile]
    if not isinstance(requested_ids, list):
        raise NovaSkillsError(f"Profile {profile} 必须是 Skill id 数组")
    entries = {entry["id"]: entry for entry in _catalog_entries(catalog)}
    return [entries[skill_id] for skill_id in requested_ids]


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
    sync_lock_path = agents_dir / SYNC_LOCK_FILE_NAME
    for label, path in (
        (".agents", agents_dir),
        (".agents/skills", target_root),
        (STATE_FILE_NAME, state_path),
        (TRANSACTION_FILE_NAME, transaction_path),
        (STAGING_DIRECTORY_NAME, staging_root),
        (SYNC_LOCK_FILE_NAME, sync_lock_path),
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
    if sync_lock_path.exists() and not sync_lock_path.is_file():
        raise NovaSkillsError(f"{SYNC_LOCK_FILE_NAME} 必须是普通文件，拒绝修改受管投影")
    return target_root, state_path


def _read_managed_state(state_path: Path) -> tuple[str, dict[str, dict[str, Any]]]:
    """读取受管状态，并只接受可由当前项目与 Catalog 重建的最小字段。"""
    state = _read_json(state_path)
    if state.get("schemaVersion") != 1:
        raise NovaSkillsError(f"受管状态 schemaVersion 不受支持：{state_path}")
    if state.get("package") != FRAMEWORK_PACKAGE_NAME:
        raise NovaSkillsError(f"受管状态未声明正确的 Framework 包：{state_path}")
    if not isinstance(state.get("packageVersion"), str) or not state["packageVersion"]:
        raise NovaSkillsError(f"受管状态缺少 packageVersion：{state_path}")
    profile = state.get("profile")
    if not isinstance(profile, str) or not profile:
        raise NovaSkillsError(f"受管状态缺少 Profile：{state_path}")
    managed = state.get("managed")
    if not isinstance(managed, dict):
        raise NovaSkillsError(f"受管状态格式错误：{state_path}")
    for skill_id, entry in managed.items():
        if not isinstance(skill_id, str) or not SKILL_NAME_PATTERN.fullmatch(skill_id):
            raise NovaSkillsError(f"受管状态包含非法 Skill id：{skill_id!r}")
        if not isinstance(entry, dict):
            raise NovaSkillsError(f"受管状态中 {skill_id} 的记录必须是对象")
        for hash_name in ("sourceHash", "targetHash"):
            value = entry.get(hash_name)
            if not isinstance(value, str) or not SHA256_HEX_PATTERN.fullmatch(value):
                raise NovaSkillsError(f"受管状态中 {skill_id} 的 {hash_name} 非法")
    return profile, managed


def _transaction_paths(state_path: Path) -> tuple[Path, Path]:
    """根据 lock 的固定位置派生事务日志和隐藏 staging 根目录。"""
    agents_dir = state_path.parent
    return agents_dir / TRANSACTION_FILE_NAME, agents_dir / STAGING_DIRECTORY_NAME


def _managed_state_payload(
    package: dict[str, Any], profile: str, managed: dict[str, dict[str, Any]]
) -> dict[str, Any]:
    """构造可移动的最小受管状态，不在状态中记录任何机器绝对路径。"""
    package_name = package.get("name")
    package_version = package.get("version")
    if package_name != FRAMEWORK_PACKAGE_NAME or not isinstance(package_version, str) or not package_version:
        raise NovaSkillsError("Framework package.json 缺少可用的包名或版本，无法写入受管状态")
    return {
        "schemaVersion": 1,
        "package": package_name,
        "packageVersion": package_version,
        "profile": profile,
        "managed": managed,
    }


def _read_transaction(transaction_path: Path) -> dict[str, Any]:
    """读取中断恢复日志，并拒绝任何不能安全恢复的伪造或损坏内容。"""
    transaction = _read_json(transaction_path)
    if transaction.get("schemaVersion") != 1:
        raise NovaSkillsError(f"受管事务 schemaVersion 不受支持：{transaction_path}")
    transaction_id = transaction.get("transactionId")
    if not isinstance(transaction_id, str) or not TRANSACTION_ID_PATTERN.fullmatch(transaction_id):
        raise NovaSkillsError(f"受管事务缺少合法 transactionId：{transaction_path}")
    previous_state = transaction.get("previousState")
    if previous_state is not None and not isinstance(previous_state, dict):
        raise NovaSkillsError(f"受管事务 previousState 格式错误：{transaction_path}")
    final_state = transaction.get("finalState")
    if not isinstance(final_state, dict):
        raise NovaSkillsError(f"受管事务缺少 finalState：{transaction_path}")
    if final_state.get("schemaVersion") != 1 or final_state.get("package") != FRAMEWORK_PACKAGE_NAME:
        raise NovaSkillsError(f"受管事务 finalState 包身份错误：{transaction_path}")
    if not isinstance(final_state.get("packageVersion"), str) or not final_state["packageVersion"]:
        raise NovaSkillsError(f"受管事务 finalState 缺少 packageVersion：{transaction_path}")
    if not isinstance(final_state.get("profile"), str) or not final_state["profile"]:
        raise NovaSkillsError(f"受管事务 finalState 缺少 Profile：{transaction_path}")
    managed = final_state.get("managed")
    if not isinstance(managed, dict):
        raise NovaSkillsError(f"受管事务 finalState managed 格式错误：{transaction_path}")
    for skill_id, managed_entry in managed.items():
        if not isinstance(skill_id, str) or not SKILL_NAME_PATTERN.fullmatch(skill_id):
            raise NovaSkillsError(f"受管事务 finalState 包含非法 Skill id：{transaction_path}")
        if not isinstance(managed_entry, dict):
            raise NovaSkillsError(f"受管事务 finalState 中 {skill_id} 记录非法")
        for hash_name in ("sourceHash", "targetHash"):
            value = managed_entry.get(hash_name)
            if not isinstance(value, str) or not SHA256_HEX_PATTERN.fullmatch(value):
                raise NovaSkillsError(f"受管事务 finalState 中 {skill_id} 的 {hash_name} 非法")
    pending = transaction.get("pending")
    if not isinstance(pending, list) or not pending:
        raise NovaSkillsError(f"受管事务 pending 必须是非空数组：{transaction_path}")
    seen_ids: set[str] = set()
    for item in pending:
        if not isinstance(item, dict):
            raise NovaSkillsError(f"受管事务 pending 包含非法条目：{transaction_path}")
        skill_id = item.get("id")
        if not isinstance(skill_id, str) or not SKILL_NAME_PATTERN.fullmatch(skill_id):
            raise NovaSkillsError(f"受管事务 pending 包含非法 Skill id：{transaction_path}")
        if skill_id in seen_ids:
            raise NovaSkillsError(f"受管事务 pending 重复 Skill：{skill_id}")
        seen_ids.add(skill_id)
        managed_entry = managed.get(skill_id)
        if not isinstance(managed_entry, dict):
            raise NovaSkillsError(f"受管事务缺少 {skill_id} 的最终哈希：{transaction_path}")
        for hash_name in ("sourceHash", "targetHash"):
            value = item.get(hash_name)
            if not isinstance(value, str) or not SHA256_HEX_PATTERN.fullmatch(value):
                raise NovaSkillsError(f"受管事务中 {skill_id} 的 {hash_name} 非法")
            if managed_entry.get(hash_name) != value:
                raise NovaSkillsError(f"受管事务中 {skill_id} 的 {hash_name} 与 finalState 不一致")
    return transaction


def _acquire_kernel_lock(file_descriptor: int) -> str:
    """获取内核级非阻塞排他锁；进程异常退出时由操作系统自动释放。"""
    if fcntl is not None:
        try:
            fcntl.flock(file_descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
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
        fcntl.flock(file_descriptor, fcntl.LOCK_UN)
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


@contextmanager
def _projection_sync_lock(project_root: Path) -> Any:
    """以持久文件上的内核锁串行化 Profile 同步，避免 stale 回收和 inode 删除竞态。"""
    _, state_path = _managed_paths(project_root)
    agents_dir = state_path.parent
    agents_dir.mkdir(parents=True, exist_ok=True)
    _managed_paths(project_root)
    lock_path = agents_dir / SYNC_LOCK_FILE_NAME
    flags = os.O_RDWR | os.O_CREAT
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        file_descriptor = os.open(lock_path, flags, 0o600)
    except OSError as exc:
        raise NovaSkillsError(f"无法打开安全同步锁：{exc}") from exc
    lock_kind: str | None = None
    try:
        lock_kind = _acquire_kernel_lock(file_descriptor)
        _managed_paths(project_root)
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


def _begin_transaction(
    project_root: Path,
    target_root: Path,
    state_path: Path,
    package: dict[str, Any],
    profile: str,
    managed: dict[str, dict[str, Any]],
    previous_state: dict[str, Any] | None,
    planned: list[tuple[dict[str, Any], Path, Path, str]],
) -> dict[str, Any]:
    """将整组待投影 Skill 写入隐藏 staging，并原子登记可恢复事务。"""
    transaction_path, staging_root = _transaction_paths(state_path)
    _managed_paths(project_root)
    target_root.mkdir(parents=True, exist_ok=True)
    staging_root.mkdir(parents=True, exist_ok=True)
    _managed_paths(project_root)
    transaction_id = uuid.uuid4().hex
    staging_dir = staging_root / transaction_id
    staging_dir.mkdir()
    final_managed = {skill_id: dict(entry) for skill_id, entry in managed.items()}
    pending: list[dict[str, str]] = []
    journal_written = False
    try:
        for entry, source_dir, _, source_hash in planned:
            skill_id = entry["id"]
            staged_skill = staging_dir / skill_id
            shutil.copytree(source_dir, staged_skill)
            target_hash = _tree_hash(staged_skill)
            source_hash_after_copy = _tree_hash(source_dir)
            if source_hash != target_hash or source_hash_after_copy != source_hash:
                raise NovaSkillsError(
                    f"复制 {skill_id} 时 Framework 真源发生变化，拒绝登记混合版本投影"
                )
            final_managed[skill_id] = {
                "sourceHash": source_hash,
                "targetHash": target_hash,
            }
            pending.append(
                {
                    "id": skill_id,
                    "sourceHash": source_hash,
                    "targetHash": target_hash,
                }
            )
        transaction = {
            "schemaVersion": 1,
            "transactionId": transaction_id,
            "previousState": previous_state,
            "finalState": _managed_state_payload(package, profile, final_managed),
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
    """续传已登记事务；所有目标哈希匹配后才写入最终受管状态。"""
    transaction_path, staging_root = _transaction_paths(state_path)
    if not transaction_path.is_file():
        return False
    transaction = _read_transaction(transaction_path)
    transaction_id = transaction["transactionId"]
    staging_dir = staging_root / transaction_id
    if _is_link_or_junction(staging_dir) or not staging_dir.is_dir():
        raise NovaSkillsError(f"受管事务 staging 不存在或不是普通目录：{staging_dir}")
    previous_state = transaction["previousState"]
    final_state = transaction["finalState"]
    current_state = _read_json(state_path) if state_path.is_file() else None
    if current_state != previous_state and current_state != final_state:
        raise NovaSkillsError("受管状态已在中断事务期间变化，拒绝覆盖并等待人工处理")

    _managed_paths(project_root)
    target_root.mkdir(parents=True, exist_ok=True)
    for item in transaction["pending"]:
        skill_id = item["id"]
        expected_hash = item["targetHash"]
        target_dir = target_root / skill_id
        staged_skill = staging_dir / skill_id
        if _is_link_or_junction(target_dir):
            raise NovaSkillsError(f"中断事务目标是软链，拒绝恢复：{target_dir}")
        if target_dir.exists():
            if not target_dir.is_dir() or _tree_hash(target_dir) != expected_hash:
                raise NovaSkillsError(f"中断事务目标已变化，拒绝恢复：{target_dir}")
            continue
        if _is_link_or_junction(staged_skill) or not staged_skill.is_dir():
            raise NovaSkillsError(f"中断事务 staging 缺少 Skill：{staged_skill}")
        if _tree_hash(staged_skill) != expected_hash:
            raise NovaSkillsError(f"中断事务 staging 已变化，拒绝恢复：{staged_skill}")
        _managed_paths(project_root)
        os.replace(staged_skill, target_dir)
        if _tree_hash(target_dir) != expected_hash:
            raise NovaSkillsError(f"中断事务恢复后哈希不一致：{target_dir}")

    _managed_paths(project_root)
    latest_state = _read_json(state_path) if state_path.is_file() else None
    if latest_state != current_state and latest_state != final_state:
        raise NovaSkillsError("受管状态在事务完成前发生变化，拒绝覆盖并等待人工处理")
    if latest_state != final_state:
        _write_json_atomically(state_path, final_state)
    _managed_paths(project_root)
    transaction_path.unlink()
    try:
        staging_dir.rmdir()
    except OSError:
        pass
    try:
        staging_root.rmdir()
    except OSError:
        pass
    return True


def _plan_sync(
    project_root: Path, agents_root: Path, profile: str, dry_run: bool
) -> tuple[
    Path,
    Path,
    dict[str, Any],
    dict[str, dict[str, Any]],
    dict[str, Any] | None,
    list[tuple[dict[str, Any], Path, Path, str]],
    dict[str, Any],
]:
    """在不落盘的前提下冻结本次 Profile 投影的输入、写入集与受管基线。"""
    errors = validate_agents_root(agents_root)
    if errors:
        raise NovaSkillsError("Agents 真源校验失败：\n- " + "\n- ".join(errors))
    agents_root = Path(agents_root).resolve()
    catalog = load_catalog(agents_root)
    selected_entries = _select_profile(catalog, profile)
    target_root, state_path = _managed_paths(project_root)
    previous_state = _read_json(state_path) if state_path.is_file() else None
    if previous_state is not None:
        _, managed = _read_managed_state(state_path)
    else:
        managed = {}
    selected_ids = {entry["id"] for entry in selected_entries}
    retained_ids = sorted(str(skill_id) for skill_id in managed if skill_id not in selected_ids)
    if retained_ids:
        raise NovaSkillsError(
            f"Profile {profile} 会保留不属于该 Profile 的受管 Skill："
            f"{', '.join(retained_ids)}；P0 不自动删除既有投影，请显式处理后再切换"
        )

    planned: list[tuple[dict[str, Any], Path, Path, str]] = []
    skipped: list[str] = []
    for entry in selected_entries:
        skill_id = entry["id"]
        source_dir = _safe_child(agents_root, entry["path"])
        if source_dir is None or not source_dir.is_dir():
            raise NovaSkillsError(f"Skill 路径不安全：{skill_id}")
        target_dir = target_root / skill_id
        source_hash = _tree_hash(source_dir)
        if _is_link_or_junction(target_dir):
            raise NovaSkillsError(f"目标 Skill 是软链或 junction，拒绝修改：{target_dir}")
        if target_dir.exists():
            if not target_dir.is_dir():
                raise NovaSkillsError(f"目标 Skill 必须是目录，拒绝修改：{target_dir}")
            existing = managed.get(skill_id)
            if not isinstance(existing, dict):
                raise NovaSkillsError(f"目标目录已存在且不属于受管投影：{target_dir}")
            if _tree_hash(target_dir) != existing.get("targetHash"):
                raise NovaSkillsError(f"受管 Skill 已被修改，拒绝覆盖：{target_dir}")
            if existing.get("sourceHash") != source_hash:
                raise NovaSkillsError(f"源 Skill 已更新，P0 不自动覆盖旧投影：{skill_id}")
            skipped.append(skill_id)
            continue
        planned.append((entry, source_dir, target_dir, source_hash))

    package = _read_json(agents_root.parent / "package.json")
    result = {
        "agentsRoot": str(agents_root),
        "profile": profile,
        "projected": [entry["id"] for entry, _, _, _ in planned],
        "skipped": skipped,
        "dryRun": dry_run,
    }
    return target_root, state_path, package, managed, previous_state, planned, result


def sync(project_root: Path, agents_root: Path | None = None, profile: str = "core", dry_run: bool = False) -> dict[str, Any]:
    """将选中 Profile 以可恢复事务安全投影到消费者项目根 `.agents/skills`。"""
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
        *_, result = _plan_sync(project_root, resolved_agents_root, profile, dry_run=True)
        return result

    with _projection_sync_lock(project_root):
        target_root, current_state_path = _managed_paths(project_root)
        _resume_transaction(project_root, target_root, current_state_path)
        (
            target_root,
            current_state_path,
            package,
            managed,
            previous_state,
            planned,
            result,
        ) = _plan_sync(project_root, resolved_agents_root, profile, dry_run=False)
        if not planned:
            _managed_paths(project_root)
            _write_json_atomically(
                current_state_path, _managed_state_payload(package, profile, managed)
            )
            return result

        _begin_transaction(
            project_root,
            target_root,
            current_state_path,
            package,
            profile,
            managed,
            previous_state,
            planned,
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
    profile, managed = _read_managed_state(state_path)

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
        expected_ids = {entry["id"] for entry in _select_profile(catalog, profile)}
        for skill_id in expected_ids:
            if skill_id not in managed:
                report["missing"].append(skill_id)
        for skill_id in managed:
            if skill_id not in expected_ids:
                report["modified"].append(skill_id)
        package = _read_json(current_agents_root.parent / "package.json")
        state = _read_json(state_path)
        if state.get("packageVersion") != package.get("version"):
            report["resolutionChanged"].append("package-version")
    except NovaSkillsError:
        report["resolutionChanged"].append("unresolved")
        current_agents_root = None

    for skill_id in sorted(expected_ids):
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
    sync_parser = subparsers.add_parser("sync", help="投影选中 Profile 到 .agents/skills")
    sync_parser.add_argument("--project-root", required=True, type=Path)
    sync_parser.add_argument("--agents-root", type=Path)
    sync_parser.add_argument("--profile", default="core")
    sync_parser.add_argument("--dry-run", action="store_true")
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
        elif args.command == "sync":
            result = sync(args.project_root, args.agents_root, args.profile, args.dry_run)
            exit_code = 0
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
