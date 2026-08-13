#!/usr/bin/env python3
"""Nova Project Skills 投影工具的消费者侧契约测试。"""

from __future__ import annotations

import importlib.util
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


TOOL_PATH = Path(__file__).resolve().parents[1] / "nova_skills.py"


def load_tool():
    """按文件路径加载待测工具，避免依赖当前工作目录。"""
    spec = importlib.util.spec_from_file_location("nova_skills", TOOL_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"无法加载工具：{TOOL_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class NovaSkillsToolTests(unittest.TestCase):
    """验证消费工程只投影显式受管的 Nova Project Skills。"""

    def setUp(self):
        """为每个测试建立相互隔离的模拟 UPM 包和消费者工程。"""
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.agents_root = self.root / "Framework" / "Agents"
        self.project_root = self.root / "Consumer"
        self._write_agents_root()

    def tearDown(self):
        """删除本测试创建的临时工程。"""
        self.temp_dir.cleanup()

    def _write_agents_root(self):
        """创建包含一个核心 Router 和一个 UI Skill 的最小真源。"""
        self.agents_root.mkdir(parents=True)
        (self.agents_root.parent / "package.json").write_text(
            json.dumps(
                {
                    "name": "com.solotopia.nova.framework",
                    "version": "0.6.9",
                }
            ),
            encoding="utf-8",
        )
        catalog = {
            "schemaVersion": 1,
            "package": "com.solotopia.nova.framework",
            "profiles": {"core": ["nova-project-router"]},
            "skills": [
                {
                    "id": "nova-project-router",
                    "path": "Skills/nova-project-router",
                    "kind": "router",
                    "status": "experimental",
                    "journeys": ["assessment"],
                    "effects": ["read"],
                    "minimumEvidence": "static",
                },
                {
                    "id": "nova-project-ui-create-view",
                    "path": "Skills/nova-project-ui-create-view",
                    "kind": "operation",
                    "status": "experimental",
                    "journeys": ["feature"],
                    "effects": ["workspace-write", "unity-write"],
                    "minimumEvidence": "compile",
                },
            ],
        }
        (self.agents_root / "catalog.json").write_text(
            json.dumps(catalog, indent=2), encoding="utf-8"
        )
        for entry in catalog["skills"]:
            skill_dir = self.agents_root / entry["path"]
            references_dir = skill_dir / "references"
            references_dir.mkdir(parents=True)
            (skill_dir / "SKILL.md").write_text(
                "---\n"
                f"name: {entry['id']}\n"
                f"description: 测试 {entry['id']} 的受管投影。\n"
                "---\n\n"
                "# Test Skill\n",
                encoding="utf-8",
            )
            (references_dir / "contract.json").write_text(
                json.dumps(
                    {
                        "schemaVersion": 1,
                        "id": entry["id"],
                        "kind": entry["kind"],
                        "compatibility": {"framework": ">=0.6.9"},
                        "requires": [],
                        "inputs": [{"name": "projectRoot", "required": True}],
                        "effects": entry["effects"],
                        "writeScope": {"allow": [], "deny": []},
                        "locks": [],
                        "idempotency": "read-only"
                        if entry["effects"] == ["read"]
                        else "ensure-state",
                        "confirmation": {"requiredFor": [], "rule": "测试"},
                        "minimumEvidence": entry["minimumEvidence"],
                        "resultStates": ["success", "partial", "blocked", "not_applicable"],
                        "evidence": ["测试证据"],
                    },
                    indent=2,
                ),
                encoding="utf-8",
            )

    def test_validate_accepts_matching_catalog_skill_and_contract(self):
        """Catalog、SKILL.md 与 contract.json 一致时不应报告错误。"""
        tool = load_tool()

        self.assertEqual([], tool.validate_agents_root(self.agents_root))

    def test_validate_accepts_the_canonical_framework_agents_tree(self):
        """随 Framework 发布的真实 Agents 真源也必须通过同一份契约校验。"""
        canonical_agents_root = TOOL_PATH.parent.parent

        self.assertEqual([], load_tool().validate_agents_root(canonical_agents_root))

    def test_canonical_ui_create_view_references_existing_framework_docs(self):
        """UI 创建 Skill 引用的随包文档必须存在，不能把 Agent 引向失效路径。"""
        canonical_agents_root = TOOL_PATH.parent.parent
        skill_path = canonical_agents_root / "Skills" / "nova-project-ui-create-view" / "SKILL.md"
        content = skill_path.read_text(encoding="utf-8")
        documentation_root = canonical_agents_root.parent / "Docs"
        required_documents = [
            "Runtime/Modules/UI/UIComponent.md",
            "Runtime/Modules/UI/Definitions/UIView.md",
            "Runtime/Modules/UI/UIManager/UIManager.md",
        ]

        for relative_document in required_documents:
            self.assertIn(f"`Docs/{relative_document}`", content)
            self.assertTrue((documentation_root / relative_document).is_file())

    def test_canonical_ui_create_view_allows_export_without_hand_editing_generated_files(self):
        """UI 导出允许更新生成物，但 Skill 不得暗示可以手工编辑生成物。"""
        canonical_agents_root = TOOL_PATH.parent.parent
        skill_path = canonical_agents_root / "Skills" / "nova-project-ui-create-view" / "SKILL.md"
        content = skill_path.read_text(encoding="utf-8")

        self.assertIn("不手工编辑生成的 C# / JSON", content)
        self.assertIn("仅通过第 4 步选定 UI 导出 Action 更新本次变更必需的生成物", content)

    def test_quick_start_documents_one_time_skill_projection(self):
        """初次接入的 Agent 必须能从随包快速入口发现受管 Skill 初始化动作。"""
        quick_start_path = TOOL_PATH.parent.parent.parent / "Docs" / "START_HERE.md"
        content = quick_start_path.read_text(encoding="utf-8")

        self.assertIn("一次性初始化", content)
        self.assertIn("nova_skills.py sync", content)
        self.assertIn("--profile core --dry-run", content)

    def test_agents_contributor_guide_does_not_require_missing_per_skill_validator(self):
        """维护指引不能要求每个 Skill 自带不存在的 quick_validate.py。"""
        contributor_guide = TOOL_PATH.parent.parent / "AGENTS.md"
        content = contributor_guide.read_text(encoding="utf-8")

        self.assertNotIn("每个 Skill 的 `quick_validate.py`", content)
        self.assertIn("Skill 验证器", content)

    def test_npmignore_excludes_agents_python_cache_and_its_unity_metadata(self):
        """本地 Python 缓存及 Unity 为其生成的 meta 都不能进入 Framework tgz。"""
        npmignore_path = TOOL_PATH.parents[2] / ".npmignore"
        content = npmignore_path.read_text(encoding="utf-8")

        self.assertIn("Agents/Tools/**/__pycache__/", content)
        self.assertIn("Agents/Tools/**/*.pyc", content)
        self.assertIn("Agents/Tools/**/__pycache__.meta", content)
        self.assertIn("Agents/Tools/**/*.pyc.meta", content)

    def test_validate_rejects_duplicate_profile_skill_id_before_sync(self):
        """Profile 不能重复投影同一 Skill，否则会造成中途落盘而状态未写入。"""
        catalog_path = self.agents_root / "catalog.json"
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        catalog["profiles"]["core"].append("nova-project-router")
        catalog_path.write_text(json.dumps(catalog), encoding="utf-8")

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("重复" in error and "Profile core" in error for error in errors))

    def test_validate_rejects_catalog_and_contract_that_omit_same_required_field(self):
        """Catalog 与 contract 同时漏字段也必须失败，不能被 None == None 掩盖。"""
        catalog_path = self.agents_root / "catalog.json"
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        catalog["skills"][0].pop("effects")
        catalog_path.write_text(json.dumps(catalog), encoding="utf-8")
        contract_path = (
            self.agents_root
            / "Skills"
            / "nova-project-router"
            / "references"
            / "contract.json"
        )
        contract = json.loads(contract_path.read_text(encoding="utf-8"))
        contract.pop("effects")
        contract_path.write_text(json.dumps(contract), encoding="utf-8")

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("effects" in error for error in errors))

    def test_validate_rejects_contract_missing_safety_field(self):
        """机器契约不能只校验 identity 字段而遗漏确认与副作用边界。"""
        contract_path = (
            self.agents_root
            / "Skills"
            / "nova-project-router"
            / "references"
            / "contract.json"
        )
        contract = json.loads(contract_path.read_text(encoding="utf-8"))
        contract.pop("confirmation")
        contract_path.write_text(json.dumps(contract), encoding="utf-8")

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("confirmation" in error for error in errors))

    def test_validate_rejects_descendant_symlink_that_escapes_agents_root(self):
        """Skill 内任意层级的软链都不能把真源外内容投影到消费项目。"""
        outside_file = self.root / "outside.md"
        outside_file.write_text("outside", encoding="utf-8")
        symlink_path = (
            self.agents_root / "Skills" / "nova-project-router" / "references" / "outside.md"
        )
        os.symlink(outside_file, symlink_path)

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("软链" in error for error in errors))

    def test_descendant_junction_is_rejected_by_the_same_tree_guard(self):
        """Windows junction 不能绕过仅检查 Unix 软链的真源树边界。"""
        tool = load_tool()
        junction = mock.MagicMock()
        junction.is_symlink.return_value = False
        junction.is_junction.return_value = True

        with mock.patch.object(Path, "rglob", return_value=[junction]):
            self.assertIs(junction, tool._find_descendant_symlink(Path("Agents")))

    def test_legacy_windows_junction_fallback_rejects_reparse_point(self):
        """Python 缺少 Path.is_junction 时仍须拒绝 Windows junction。"""
        tool = load_tool()

        class LegacyWindowsPath:
            """模拟 Python 3.11 的 Path：只有 lstat 重解析点属性。"""

            def is_symlink(self):
                """模拟 junction 不被识别为符号链接。"""
                return False

            def lstat(self):
                """返回 Windows reparse point 属性，且不跟随 junction。"""
                return type("StatResult", (), {"st_file_attributes": 0x0400})()

        with mock.patch.object(tool.os, "name", "nt"):
            self.assertTrue(tool._is_link_or_junction(LegacyWindowsPath()))

    def test_legacy_windows_junction_fallback_allows_missing_and_plain_paths(self):
        """旧版 Windows fallback 不能因受管路径尚未创建而中断首次同步。"""
        tool = load_tool()

        class MissingLegacyWindowsPath:
            """模拟干净项目中尚未创建的受管路径。"""

            def is_symlink(self):
                """模拟非符号链接路径。"""
                return False

            def lstat(self):
                """模拟不存在路径的 lstat 结果。"""
                raise FileNotFoundError("missing clean-project path")

        class PlainLegacyWindowsPath:
            """模拟不带 Windows 文件属性的普通路径。"""

            def is_symlink(self):
                """模拟非符号链接路径。"""
                return False

            def lstat(self):
                """模拟普通文件系统的 stat 结果。"""
                return object()

        with mock.patch.object(tool.os, "name", "nt"):
            self.assertFalse(tool._is_link_or_junction(MissingLegacyWindowsPath()))
            self.assertFalse(tool._is_link_or_junction(PlainLegacyWindowsPath()))

    def test_resolve_prefers_file_dependency_from_manifest(self):
        """消费者工程的 file: 主框架依赖应解析到对应 Agents 真源。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        relative_framework = Path("..") / ".." / "Framework"
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": f"file:{relative_framework}"
                    }
                }
            ),
            encoding="utf-8",
        )
        expected = self.agents_root.resolve()

        self.assertEqual(expected, load_tool().resolve_agents_root(self.project_root))

    def test_resolve_decodes_file_dependency_path_with_spaces(self):
        """file: 依赖中的 URL 编码路径应解析到真实 Framework 包目录。"""
        spaced_framework = self.root / "Framework With Space"
        spaced_agents = spaced_framework / "Agents"
        spaced_agents.mkdir(parents=True)
        (spaced_framework / "package.json").write_text(
            json.dumps(
                {
                    "name": "com.solotopia.nova.framework",
                    "version": "0.6.9",
                }
            ),
            encoding="utf-8",
        )
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework%20With%20Space"
                    }
                }
            ),
            encoding="utf-8",
        )

        self.assertEqual(spaced_agents.resolve(), load_tool().resolve_agents_root(self.project_root))

    def test_resolve_rejects_file_dependency_that_is_not_the_framework_package(self):
        """存在同名 Agents 目录也不能把其他 UPM 包误判为 Nova Framework。"""
        unrelated_package = self.root / "UnrelatedPackage"
        (unrelated_package / "Agents").mkdir(parents=True)
        (unrelated_package / "package.json").write_text(
            json.dumps({"name": "com.example.unrelated", "version": "1.0.0"}),
            encoding="utf-8",
        )
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../UnrelatedPackage"
                    }
                }
            ),
            encoding="utf-8",
        )

        with self.assertRaisesRegex(RuntimeError, "不是 Nova Framework"):
            load_tool().resolve_agents_root(self.project_root)

    def test_resolve_rejects_framework_agents_root_symlink(self):
        """Framework 包内的 Agents 根目录不能用软链越过当前已解析包边界。"""
        alternate_framework = self.root / "AlternateFramework"
        shutil.copytree(self.agents_root.parent, alternate_framework)
        shutil.rmtree(self.agents_root)
        os.symlink(alternate_framework / "Agents", self.agents_root)
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )

        with self.assertRaisesRegex(RuntimeError, "Agents.*软链"):
            load_tool().resolve_agents_root(self.project_root)

    def test_sync_rejects_explicit_agents_root_symlink(self):
        """显式测试入口也不能通过预先 resolve 绕过 Agents 根软链检查。"""
        alternate_framework = self.root / "AlternateFramework"
        shutil.copytree(self.agents_root.parent, alternate_framework)
        shutil.rmtree(self.agents_root)
        os.symlink(alternate_framework / "Agents", self.agents_root)

        with self.assertRaisesRegex(RuntimeError, "Agents.*软链"):
            load_tool().sync(
                self.project_root,
                agents_root=self.agents_root,
                profile="core",
                dry_run=True,
            )

    def test_resolve_rejects_embedded_package_without_embedded_lock_source(self):
        """残留同名目录不能绕过 lock 对嵌入包来源的约束。"""
        packages_dir = self.project_root / "Packages"
        embedded_package = packages_dir / "com.solotopia.nova.framework"
        (embedded_package / "Agents").mkdir(parents=True)
        (embedded_package / "package.json").write_text(
            json.dumps(
                {
                    "name": "com.solotopia.nova.framework",
                    "version": "0.6.9",
                }
            ),
            encoding="utf-8",
        )
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {"dependencies": {"com.solotopia.nova.framework": "0.6.9"}}
            ),
            encoding="utf-8",
        )
        (packages_dir / "packages-lock.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": {
                            "version": "0.6.9",
                            "source": "registry",
                        }
                    }
                }
            ),
            encoding="utf-8",
        )

        with self.assertRaisesRegex(RuntimeError, "嵌入包"):
            load_tool().resolve_agents_root(self.project_root)

    def test_resolve_registry_cache_rejects_stale_package_version(self):
        """Registry lock 指向的版本必须与 PackageCache 内 package.json 一致。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {"dependencies": {"com.solotopia.nova.framework": "0.6.10"}}
            ),
            encoding="utf-8",
        )
        (packages_dir / "packages-lock.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": {
                            "version": "0.6.10",
                            "source": "registry",
                        }
                    }
                }
            ),
            encoding="utf-8",
        )
        cache_package = (
            self.project_root
            / "Library"
            / "PackageCache"
            / "com.solotopia.nova.framework@0.6.10"
        )
        (cache_package / "Agents").mkdir(parents=True)
        (cache_package / "package.json").write_text(
            json.dumps(
                {
                    "name": "com.solotopia.nova.framework",
                    "version": "0.6.9",
                }
            ),
            encoding="utf-8",
        )

        with self.assertRaisesRegex(RuntimeError, "版本"):
            load_tool().resolve_agents_root(self.project_root)

    def test_resolve_rejects_package_cache_when_framework_lock_is_missing(self):
        """没有权威 lock 时，即使只剩一个缓存候选也不能猜测其为当前 Framework。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {"dependencies": {"com.solotopia.nova.framework": "9.9.9"}}
            ),
            encoding="utf-8",
        )
        cache_package = (
            self.project_root
            / "Library"
            / "PackageCache"
            / "com.solotopia.nova.framework@0.1.0"
        )
        (cache_package / "Agents").mkdir(parents=True)
        (cache_package / "package.json").write_text(
            json.dumps(
                {
                    "name": "com.solotopia.nova.framework",
                    "version": "0.1.0",
                }
            ),
            encoding="utf-8",
        )

        with self.assertRaisesRegex(RuntimeError, "packages-lock.json"):
            load_tool().resolve_agents_root(self.project_root)

    def test_sync_projects_only_selected_profile_and_preserves_user_skill(self):
        """同步只能写入 core Profile，且不得覆盖项目组已有同名外部 Skill。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )
        user_skill = self.project_root / ".agents" / "skills" / "project-private-skill"
        user_skill.mkdir(parents=True)
        (user_skill / "SKILL.md").write_text("private", encoding="utf-8")

        result = load_tool().sync(self.project_root, profile="core")

        projected = self.project_root / ".agents" / "skills" / "nova-project-router"
        self.assertEqual(["nova-project-router"], result["projected"])
        self.assertTrue((projected / "SKILL.md").is_file())
        self.assertFalse(
            (self.project_root / ".agents" / "skills" / "nova-project-ui-create-view").exists()
        )
        self.assertEqual("private", (user_skill / "SKILL.md").read_text(encoding="utf-8"))

    def test_sync_rejects_same_name_user_skill_without_creating_state(self):
        """同名用户 Skill 必须原样保留，且失败不能留下受管状态文件。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )
        user_skill = self.project_root / ".agents" / "skills" / "nova-project-router"
        user_skill.mkdir(parents=True)
        marker = user_skill / "SKILL.md"
        marker.write_text("user owned", encoding="utf-8")

        with self.assertRaisesRegex(RuntimeError, "不属于受管投影"):
            load_tool().sync(self.project_root, profile="core")

        self.assertEqual("user owned", marker.read_text(encoding="utf-8"))
        self.assertFalse((self.project_root / ".agents" / "nova-skills.lock.json").exists())

    def test_sync_rejects_project_internal_agents_root_symlink_without_writing_alias(self):
        """.agents 指向项目内目录时也不能越过受管投影的物理写入边界。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )
        aliased_root = self.project_root / "Assets" / "AgentAlias"
        aliased_root.mkdir(parents=True)
        os.symlink(aliased_root, self.project_root / ".agents", target_is_directory=True)

        with self.assertRaisesRegex(RuntimeError, "软链"):
            load_tool().sync(self.project_root, profile="core")

        self.assertFalse((aliased_root / "skills").exists())
        self.assertFalse((aliased_root / "nova-skills.lock.json").exists())

    def test_sync_rejects_project_internal_skills_root_symlink_without_writing_alias(self):
        """.agents/skills 是项目内软链时也不能把 Skill 写到其他业务目录。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )
        agents_dir = self.project_root / ".agents"
        agents_dir.mkdir()
        aliased_skills = self.project_root / "Assets" / "AliasedSkills"
        aliased_skills.mkdir(parents=True)
        os.symlink(aliased_skills, agents_dir / "skills", target_is_directory=True)

        with self.assertRaisesRegex(RuntimeError, "软链"):
            load_tool().sync(self.project_root, profile="core")

        self.assertFalse((aliased_skills / "nova-project-router").exists())
        self.assertFalse((agents_dir / "nova-skills.lock.json").exists())

    def test_sync_rejects_project_internal_state_symlink_without_writing_alias(self):
        """lock 软链不能把受管状态重定向到项目内任意业务文件。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )
        agents_dir = self.project_root / ".agents"
        (agents_dir / "skills").mkdir(parents=True)
        aliased_state = self.project_root / "Assets" / "aliased-state.json"
        aliased_state.parent.mkdir(parents=True)
        os.symlink(aliased_state, agents_dir / "nova-skills.lock.json")

        with self.assertRaisesRegex(RuntimeError, "软链"):
            load_tool().sync(self.project_root, profile="core")

        self.assertFalse(aliased_state.exists())
        self.assertFalse((agents_dir / "skills" / "nova-project-router").exists())

    def test_sync_recovers_after_interruption_between_profile_skill_replacements(self):
        """Profile 中断后重试必须续传受管事务，不能遗留无 lock 的孤儿 Skill。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )
        catalog_path = self.agents_root / "catalog.json"
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        catalog["profiles"]["core"] = [
            "nova-project-router",
            "nova-project-ui-create-view",
        ]
        catalog_path.write_text(json.dumps(catalog), encoding="utf-8")
        tool = load_tool()
        target_root = self.project_root / ".agents" / "skills"
        first_target = target_root / "nova-project-router"
        second_target = target_root / "nova-project-ui-create-view"
        original_replace = tool.os.replace

        def interrupt_second_skill(source, destination):
            destination_path = Path(destination)
            if destination_path.name == "nova-project-ui-create-view":
                raise KeyboardInterrupt("模拟 Profile 投影中断")
            return original_replace(source, destination)

        with mock.patch.object(tool.os, "replace", side_effect=interrupt_second_skill):
            with self.assertRaisesRegex(KeyboardInterrupt, "中断"):
                tool.sync(self.project_root, profile="core")

        transaction_path = self.project_root / ".agents" / "nova-skills.transaction.json"
        state_path = self.project_root / ".agents" / "nova-skills.lock.json"
        self.assertTrue(first_target.is_dir())
        self.assertFalse(second_target.exists())
        self.assertTrue(transaction_path.is_file())
        self.assertFalse(state_path.exists())
        self.assertEqual(
            ["nova-skills.transaction.json"],
            tool.doctor(self.project_root)["interrupted"],
        )

        tool.sync(self.project_root, profile="core")

        self.assertTrue(first_target.is_dir())
        self.assertTrue(second_target.is_dir())
        self.assertTrue(state_path.is_file())
        self.assertFalse(transaction_path.exists())
        self.assertFalse(any(tool.doctor(self.project_root).values()))

    def test_sync_rejects_state_changed_before_transaction_finalization(self):
        """事务完成前 lock 被外部改写时不得被最终状态静默覆盖。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )
        tool = load_tool()
        state_path = self.project_root / ".agents" / "nova-skills.lock.json"
        transaction_path = self.project_root / ".agents" / "nova-skills.transaction.json"
        target_skill = self.project_root / ".agents" / "skills" / "nova-project-router"
        foreign_state = {"external": "state-change"}
        original_tree_hash = tool._tree_hash
        state_changed = False

        def hash_then_change_state(directory):
            """在目标 Skill 移入后、最终状态写入前模拟锁外并发写入。"""
            nonlocal state_changed
            result = original_tree_hash(directory)
            if Path(directory).resolve() == target_skill.resolve() and not state_changed:
                state_path.write_text(json.dumps(foreign_state), encoding="utf-8")
                state_changed = True
            return result

        with mock.patch.object(tool, "_tree_hash", side_effect=hash_then_change_state):
            with self.assertRaisesRegex(RuntimeError, "受管状态.*变化"):
                tool.sync(self.project_root, profile="core")

        self.assertTrue(state_changed)
        self.assertEqual(foreign_state, json.loads(state_path.read_text(encoding="utf-8")))
        self.assertTrue(transaction_path.is_file())

    def test_sync_rejects_source_change_during_hidden_staging_without_visible_projection(self):
        """复制期间真源变化时不得登记彼此不一致的 source/target 哈希。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )
        tool = load_tool()
        source_skill = self.agents_root / "Skills" / "nova-project-router"
        original_copytree = tool.shutil.copytree

        def copy_then_change_source(source, destination, *args, **kwargs):
            copied = original_copytree(source, destination, *args, **kwargs)
            if Path(source).resolve() == source_skill.resolve():
                skill_file = source_skill / "SKILL.md"
                skill_file.write_text(
                    skill_file.read_text(encoding="utf-8") + "\nsource changed during copy\n",
                    encoding="utf-8",
                )
            return copied

        with mock.patch.object(tool.shutil, "copytree", side_effect=copy_then_change_source):
            with self.assertRaisesRegex(RuntimeError, "真源.*变化"):
                tool.sync(self.project_root, profile="core")

        agents_dir = self.project_root / ".agents"
        self.assertFalse((agents_dir / "skills" / "nova-project-router").exists())
        self.assertFalse((agents_dir / "nova-skills.lock.json").exists())
        self.assertFalse((agents_dir / "nova-skills.transaction.json").exists())

    def test_sync_rejects_an_active_atomic_lock_without_changing_its_owner(self):
        """另一进程持有内核锁时不得进入同一 Profile 同步临界区。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )
        holder_script = """
import importlib.util
import sys
from pathlib import Path

spec = importlib.util.spec_from_file_location('held_nova_skills', sys.argv[1])
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
with module._projection_sync_lock(Path(sys.argv[2])):
    print('locked', flush=True)
    sys.stdin.readline()
"""
        with subprocess.Popen(
            [sys.executable, "-c", holder_script, str(TOOL_PATH), str(self.project_root)],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        ) as holder:
            self.assertEqual("locked\n", holder.stdout.readline())
            with self.assertRaisesRegex(RuntimeError, "正在进行"):
                load_tool().sync(self.project_root, profile="core")
            self.assertFalse(
                (self.project_root / ".agents" / "skills" / "nova-project-router").exists()
            )
            _, holder_stderr = holder.communicate(input="\n", timeout=10)
        self.assertEqual(0, holder.returncode, holder_stderr)
        self.assertTrue(holder.stdin.closed)
        self.assertTrue(holder.stdout.closed)
        self.assertTrue(holder.stderr.closed)

    def test_sync_reuses_a_released_kernel_lock_before_projecting(self):
        """已释放的隐藏锁文件可复用，且不会靠删除 lock inode 实现恢复。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )
        agents_dir = self.project_root / ".agents"
        agents_dir.mkdir()
        lock_path = agents_dir / ".nova-skills-sync.lock"
        lock_path.write_text(
            json.dumps({"schemaVersion": 1, "processId": -1, "token": "b" * 32}),
            encoding="utf-8",
        )

        result = load_tool().sync(self.project_root, profile="core")

        self.assertEqual(["nova-project-router"], result["projected"])
        self.assertTrue(lock_path.is_file())
        owner = json.loads(lock_path.read_text(encoding="utf-8"))
        self.assertEqual(1, owner["schemaVersion"])
        self.assertRegex(owner["token"], r"^[0-9a-f]{32}$")

    def test_canonical_p0_sync_projects_real_skills_into_temporary_consumer(self):
        """真实 Framework Agents 必须能在干净消费工程中投影、诊断并保持 Profile 边界。"""
        canonical_agents_root = TOOL_PATH.parent.parent
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": canonical_agents_root.parent.as_uri()
                    }
                }
            ),
            encoding="utf-8",
        )
        tool = load_tool()

        dry_run = tool.sync(self.project_root, profile="p0", dry_run=True)
        self.assertEqual(
            [
                "nova-project-router",
                "nova-project-check-readiness",
                "nova-project-ui-create-view",
                "nova-project-data-driven-ui",
            ],
            dry_run["projected"],
        )
        self.assertFalse((self.project_root / ".agents").exists())

        result = tool.sync(self.project_root, profile="core")

        self.assertEqual(
            ["nova-project-router", "nova-project-check-readiness"], result["projected"]
        )
        self.assertTrue(
            (self.project_root / ".agents" / "skills" / "nova-project-router" / "SKILL.md").is_file()
        )
        self.assertFalse(
            (self.project_root / ".agents" / "skills" / "nova-project-ui-create-view").exists()
        )
        self.assertEqual(
            {
                "missing": [],
                "modified": [],
                "sourceChanged": [],
                "resolutionChanged": [],
                "uninitialized": [],
                "interrupted": [],
            },
            tool.doctor(self.project_root),
        )

    def test_sync_rejects_profile_downgrade_that_would_retain_extra_skills(self):
        """Profile 切换不能把写入型 Skill 留在磁盘上却伪称为更小的 Profile。"""
        canonical_agents_root = TOOL_PATH.parent.parent
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": canonical_agents_root.parent.as_uri()
                    }
                }
            ),
            encoding="utf-8",
        )
        tool = load_tool()
        tool.sync(self.project_root, profile="p0")
        state_path = self.project_root / ".agents" / "nova-skills.lock.json"

        with self.assertRaisesRegex(RuntimeError, "Profile.*保留"):
            tool.sync(self.project_root, profile="core")

        state = json.loads(state_path.read_text(encoding="utf-8"))
        self.assertEqual("p0", state["profile"])
        self.assertTrue(
            (
                self.project_root
                / ".agents"
                / "skills"
                / "nova-project-ui-create-view"
                / "SKILL.md"
            ).is_file()
        )

    def test_sync_state_uses_no_absolute_source_or_target_paths(self):
        """受管状态应随消费项目移动；路径必须从当前项目和 Catalog 推导。"""
        canonical_agents_root = TOOL_PATH.parent.parent
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": canonical_agents_root.parent.as_uri()
                    }
                }
            ),
            encoding="utf-8",
        )

        load_tool().sync(self.project_root, profile="core")

        state_path = self.project_root / ".agents" / "nova-skills.lock.json"
        state_text = state_path.read_text(encoding="utf-8")
        state = json.loads(state_text)
        managed_entry = state["managed"]["nova-project-router"]
        self.assertNotIn(str(self.project_root.resolve()), state_text)
        self.assertNotIn(str(canonical_agents_root.resolve()), state_text)
        self.assertEqual({"sourceHash", "targetHash"}, set(managed_entry))

    def test_doctor_reports_profile_member_missing_from_managed_state(self):
        """lock 遗漏 Profile 中的 Skill 时，doctor 不能把投影误报为健康。"""
        canonical_agents_root = TOOL_PATH.parent.parent
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": canonical_agents_root.parent.as_uri()
                    }
                }
            ),
            encoding="utf-8",
        )
        tool = load_tool()
        tool.sync(self.project_root, profile="core")
        state_path = self.project_root / ".agents" / "nova-skills.lock.json"
        state = json.loads(state_path.read_text(encoding="utf-8"))
        state["managed"].pop("nova-project-check-readiness")
        state_path.write_text(json.dumps(state), encoding="utf-8")
        shutil.rmtree(
            self.project_root / ".agents" / "skills" / "nova-project-check-readiness"
        )

        report = tool.doctor(self.project_root)

        self.assertEqual(["nova-project-check-readiness"], report["missing"])

    def test_doctor_reports_uninitialized_projection(self):
        """没有受管状态时不能把尚未安装的 Skill 误报为健康。"""
        report = load_tool().doctor(self.project_root)

        self.assertEqual(["nova-skills.lock.json"], report["uninitialized"])

    def test_doctor_reports_user_edit_to_managed_projection_without_rewriting_it(self):
        """受管副本被用户修改后，doctor 应报告漂移且不静默覆盖。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )
        tool = load_tool()
        tool.sync(self.project_root, profile="core")
        managed_skill = self.project_root / ".agents" / "skills" / "nova-project-router" / "SKILL.md"
        managed_skill.write_text("user edit", encoding="utf-8")

        report = tool.doctor(self.project_root)

        self.assertEqual(["nova-project-router"], report["modified"])
        self.assertEqual("user edit", managed_skill.read_text(encoding="utf-8"))

    def test_doctor_reports_framework_source_change_without_tying_state_to_checkout_path(self):
        """切换到内容不同的 Framework 应报告源变更，而不是依赖绝对 checkout 路径。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        manifest_path = packages_dir / "manifest.json"
        manifest_path.write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../Framework"
                    }
                }
            ),
            encoding="utf-8",
        )
        tool = load_tool()
        tool.sync(self.project_root, profile="core")

        replacement_framework = self.root / "ReplacementFramework"
        shutil.copytree(self.agents_root.parent, replacement_framework)
        replacement_skill = (
            replacement_framework / "Agents" / "Skills" / "nova-project-router" / "SKILL.md"
        )
        replacement_skill.write_text(
            replacement_skill.read_text(encoding="utf-8") + "\n内容已变更。\n",
            encoding="utf-8",
        )
        manifest_path.write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": "file:../../ReplacementFramework"
                    }
                }
            ),
            encoding="utf-8",
        )

        report = tool.doctor(self.project_root)

        self.assertEqual(["nova-project-router"], report["sourceChanged"])
        self.assertEqual([], report["resolutionChanged"])


if __name__ == "__main__":
    unittest.main()
