#!/usr/bin/env python3
"""Nova Project Skills 投影工具的消费者侧契约测试。"""

from __future__ import annotations

import importlib.util
import hashlib
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

    COMMON_BASELINE = (
        "触发后先读取当前 Framework 的 `Docs/START_HERE.md`，"
        "作为所有 `nova-project-*` Skill 的共同底线。"
    )

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
        docs_root = self.agents_root.parent / "Docs"
        docs_root.mkdir()
        (docs_root / "START_HERE.md").write_text(
            "# Nova 项目组入口\n", encoding="utf-8"
        )
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
            "capabilityGroups": {
                "assessment": ["nova-project-router"],
                "ui": ["nova-project-ui-create-view"],
            },
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
                "# Test Skill\n\n"
                f"{self.COMMON_BASELINE}\n\n"
                "## 渐进式披露\n\n"
                "仅在需要执行具体动作时读取对应 references。\n",
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
                        "actionAdapters": [
                            {
                                "kind": "workspace-inspection",
                                "entry": "test adapter",
                                "when": "测试",
                            }
                        ],
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

    def _write_consumer_manifest(self, framework_dependency: str = "file:../../Framework"):
        """为模拟消费工程写入指向当前测试 Framework 的最小 UPM manifest。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True, exist_ok=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": framework_dependency,
                    }
                }
            ),
            encoding="utf-8",
        )

    def _read_catalog(self) -> dict:
        """读取当前临时真源 Catalog，供升级和删除场景改写。"""
        return json.loads((self.agents_root / "catalog.json").read_text(encoding="utf-8"))

    def _write_catalog(self, catalog: dict) -> None:
        """原子性不属于测试对象，测试直接更新临时真源 Catalog。"""
        (self.agents_root / "catalog.json").write_text(
            json.dumps(catalog, indent=2), encoding="utf-8"
        )

    def _append_skill(self, skill_id: str) -> None:
        """在临时真源增加一个合法 Operation，以模拟 Framework 升级新增 Skill。"""
        catalog = self._read_catalog()
        entry = {
            "id": skill_id,
            "path": f"Skills/{skill_id}",
            "kind": "operation",
            "status": "experimental",
            "journeys": ["feature"],
            "effects": ["read"],
            "minimumEvidence": "static",
        }
        catalog["skills"].append(entry)
        self._write_catalog(catalog)
        skill_dir = self.agents_root / entry["path"]
        references_dir = skill_dir / "references"
        references_dir.mkdir(parents=True)
        (skill_dir / "SKILL.md").write_text(
            "---\n"
            f"name: {skill_id}\n"
            f"description: 测试 {skill_id} 的升级投影。\n"
            "---\n\n"
            "# Test Skill\n\n"
            f"{self.COMMON_BASELINE}\n\n"
            "## 渐进式披露\n\n"
            "仅在需要执行具体动作时读取对应 references。\n",
            encoding="utf-8",
        )
        (references_dir / "contract.json").write_text(
            json.dumps(
                {
                    "schemaVersion": 1,
                    "id": skill_id,
                    "kind": "operation",
                    "compatibility": {"framework": ">=0.6.9"},
                    "requires": [],
                    "actionAdapters": [
                        {
                            "kind": "workspace-inspection",
                            "entry": "test adapter",
                            "when": "测试",
                        }
                    ],
                    "inputs": [{"name": "projectRoot", "required": True}],
                    "effects": ["read"],
                    "writeScope": {"allow": [], "deny": []},
                    "locks": [],
                    "idempotency": "read-only",
                    "confirmation": {"requiredFor": [], "rule": "测试"},
                    "minimumEvidence": "static",
                    "resultStates": ["success", "partial", "blocked", "not_applicable"],
                    "evidence": ["测试证据"],
                },
                indent=2,
            ),
            encoding="utf-8",
        )

    def _append_workflow(self, skill_id: str, requires: list[str]) -> None:
        """在临时真源追加一个 Workflow，供内部 DAG 约束测试使用。"""
        self._append_skill(skill_id)
        catalog = self._read_catalog()
        entry = next(item for item in catalog["skills"] if item["id"] == skill_id)
        entry["kind"] = "workflow"
        self._write_catalog(catalog)
        contract_path = (
            self.agents_root / entry["path"] / "references" / "contract.json"
        )
        contract = json.loads(contract_path.read_text(encoding="utf-8"))
        contract["kind"] = "workflow"
        contract["requires"] = requires
        contract_path.write_text(json.dumps(contract, indent=2), encoding="utf-8")

    def _set_action_adapters(self, skill_id: str, adapters: list[dict]) -> None:
        """为指定临时 Skill 写入待验证的 Action Adapter 列表。"""
        contract_path = (
            self.agents_root / "Skills" / skill_id / "references" / "contract.json"
        )
        contract = json.loads(contract_path.read_text(encoding="utf-8"))
        contract["actionAdapters"] = adapters
        contract_path.write_text(json.dumps(contract, indent=2), encoding="utf-8")

    def _write_action_sources(
        self, registered_action_ids: list[str], exposed_action_ids: list[str]
    ) -> None:
        """写入最小 Handler 与 MCP 策略源码，模拟 Framework 的静态事实。"""
        handlers_root = (
            self.agents_root.parent
            / "Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Test"
        )
        handlers_root.mkdir(parents=True, exist_ok=True)
        handler_lines = ["namespace NovaFramework.Editor\n{\n"]
        for index, action_id in enumerate(registered_action_ids):
            handler_lines.extend(
                (
                    "    [AgentAction(\n",
                    f'        "{action_id}",\n',
                    '        "测试",\n',
                    '        "test",\n',
                    "        AgentActionOperationType.Inspect)]\n",
                    f"    internal sealed class TestAction{index} {{ }}\n",
                )
            )
        handler_lines.append("}\n")
        (handlers_root / "TestActions.cs").write_text(
            "".join(handler_lines), encoding="utf-8"
        )

        gateway_path = (
            self.root
            / "UPMPackages"
            / "com.solotopia.nova.framework.mcp"
            / "Nova/Editor/NovaProjectActionGateway.cs"
        )
        gateway_path.parent.mkdir(parents=True, exist_ok=True)
        policies = "\n".join(
            f'            new ExposurePolicy("{action_id}"),'
            for action_id in exposed_action_ids
        )
        gateway_path.write_text(
            "namespace NovaFramework.Mcp.Editor\n{\n"
            "    internal static class NovaProjectActionGateway\n"
            "    {\n"
            "        private static readonly ExposurePolicy[] s_ExposurePolicies =\n"
            "        {\n"
            f"{policies}\n"
            "        };\n"
            "    }\n"
            "}\n",
            encoding="utf-8",
        )

    def test_validate_accepts_matching_catalog_skill_and_contract(self):
        """Catalog、SKILL.md 与 contract.json 一致时不应报告错误。"""
        tool = load_tool()

        self.assertEqual([], tool.validate_agents_root(self.agents_root))

    def test_validate_accepts_all_registered_agent_actions_exposed(self):
        """全部已注册 Project Action 均进入显式白名单时应通过。"""
        self._write_action_sources(
            ["nova.project.test.run", "nova.project.test.second"],
            ["nova.project.test.run", "nova.project.test.second"],
        )
        self._set_action_adapters(
            "nova-project-router",
            [
                {
                    "kind": "agent-action",
                    "entry": "nova.project.test.run",
                    "when": "测试可调度 Action",
                },
                {
                    "kind": "agent-action",
                    "entry": "nova.project.test.second",
                    "when": "测试第二个可调度 Action",
                },
            ],
        )

        self.assertEqual([], load_tool().validate_agents_root(self.agents_root))

    def test_validate_rejects_agent_action_with_non_exact_id(self):
        """Action Adapter 不得把调用参数或多个 ID 混进 entry。"""
        self._write_action_sources(
            ["nova.project.test.run"], ["nova.project.test.run"]
        )
        self._set_action_adapters(
            "nova-project-router",
            [
                {
                    "kind": "agent-action",
                    "entry": "nova_project_action(action_id=nova.project.test.run)",
                    "when": "测试",
                }
            ],
        )

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("精确 nova.project" in error for error in errors))

    def test_validate_rejects_unregistered_agent_action(self):
        """Skill 不能声明仅存在于文案而未注册到 Framework Handler 的 Action。"""
        self._write_action_sources(
            ["nova.project.other.run"], ["nova.project.other.run"]
        )
        self._set_action_adapters(
            "nova-project-router",
            [
                {
                    "kind": "agent-action",
                    "entry": "nova.project.test.run",
                    "when": "测试",
                }
            ],
        )

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("未在 Framework AgentAction Handler 注册" in error for error in errors))

    def test_validate_rejects_unexposed_agent_action(self):
        """已注册但未列入 MCP 策略的 Action 不能标记为可调度。"""
        self._write_action_sources(["nova.project.test.run"], [])
        self._set_action_adapters(
            "nova-project-router",
            [
                {
                    "kind": "agent-action",
                    "entry": "nova.project.test.run",
                    "when": "测试",
                }
            ],
        )

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("未出现在 MCP ExposurePolicy" in error for error in errors))
        self.assertTrue(any("未完整进入 MCP ExposurePolicy" in error for error in errors))

    def test_validate_rejects_exposed_blocked_agent_action(self):
        """已开放的 Action 不能同时标记为 blocked，避免误导路由。"""
        self._write_action_sources(
            ["nova.project.test.run"], ["nova.project.test.run"]
        )
        self._set_action_adapters(
            "nova-project-router",
            [
                {
                    "kind": "agent-action-blocked",
                    "entry": "nova.project.test.run",
                    "when": "测试",
                }
            ],
        )

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("agent-action-blocked 已出现在 MCP ExposurePolicy" in error for error in errors))

    def test_validate_rejects_csharp_api_as_dispatchable_action(self):
        """普通 C# API 不能借用 Action ID 伪装成 MCP 可调度入口。"""
        self._set_action_adapters(
            "nova-project-router",
            [
                {
                    "kind": "csharp-api",
                    "entry": "nova.project.test.run",
                    "when": "测试",
                }
            ],
        )

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("csharp-api 不是可调度 Action" in error for error in errors))

    def test_validate_rejects_missing_shared_quick_start(self):
        """没有共同入口文档时不能发布无法按共同底线执行的 Skill 真源。"""
        (self.agents_root.parent / "Docs" / "START_HERE.md").unlink()

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("Docs/START_HERE.md" in error for error in errors))

    def test_validate_rejects_common_baseline_outside_first_body_paragraph(self):
        """共同底线不能藏在后文，避免 Agent 在动作前漏读。"""
        skill_path = self.agents_root / "Skills" / "nova-project-router" / "SKILL.md"
        skill_path.write_text(
            "---\n"
            "name: nova-project-router\n"
            "description: 测试。\n"
            "---\n\n"
            "# Test Skill\n\n"
            "先执行其它步骤。\n\n"
            f"{self.COMMON_BASELINE}\n\n"
            "## 渐进式披露\n\n"
            "仅在需要执行具体动作时读取对应 references。\n",
            encoding="utf-8",
        )

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("首个正文段落" in error for error in errors))

    def test_validate_rejects_missing_progressive_disclosure_route(self):
        """每个 Skill 都必须明确后续资料按需读取的路由，而非全量展开。"""
        skill_path = self.agents_root / "Skills" / "nova-project-router" / "SKILL.md"
        skill_path.write_text(
            skill_path.read_text(encoding="utf-8").replace(
                "## 渐进式披露\n\n仅在需要执行具体动作时读取对应 references。\n",
                "## 执行\n\n读取所有资料。\n",
            ),
            encoding="utf-8",
        )

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("渐进式披露" in error for error in errors))

    def test_validate_rejects_contract_without_action_adapters(self):
        """Skill 不能只写自然语言流程，必须声明可重复调用的动作入口。"""
        contract_path = (
            self.agents_root
            / "Skills"
            / "nova-project-router"
            / "references"
            / "contract.json"
        )
        contract = json.loads(contract_path.read_text(encoding="utf-8"))
        del contract["actionAdapters"]
        contract_path.write_text(json.dumps(contract, indent=2), encoding="utf-8")

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("actionAdapters" in error for error in errors))

    def test_validate_rejects_workflow_as_action_adapter(self):
        """Workflow 是编排层，不能伪装为实际执行 Action Adapter。"""
        contract_path = (
            self.agents_root
            / "Skills"
            / "nova-project-router"
            / "references"
            / "contract.json"
        )
        contract = json.loads(contract_path.read_text(encoding="utf-8"))
        contract["actionAdapters"] = [
            {
                "kind": "workflow",
                "entry": "not an executor",
                "when": "测试",
            }
        ]
        contract_path.write_text(json.dumps(contract, indent=2), encoding="utf-8")

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("actionAdapters" in error for error in errors))

    def test_validate_accepts_build_specific_evidence(self):
        """Bundle 构建必须使用构建产物证据，不能以编译通过替代。"""
        catalog = self._read_catalog()
        build_entry = catalog["skills"][1]
        build_entry["effects"].append("generated-output")
        build_entry["effects"].append("build")
        build_entry["minimumEvidence"] = "bundle-build"
        self._write_catalog(catalog)
        contract_path = (
            self.agents_root
            / build_entry["path"]
            / "references"
            / "contract.json"
        )
        contract = json.loads(contract_path.read_text(encoding="utf-8"))
        contract["effects"] = build_entry["effects"]
        contract["minimumEvidence"] = "bundle-build"
        contract["actionAdapters"] = [
            {
                "kind": "pipify",
                "entry": "bundlebuilder.build",
                "when": "已冻结本地构建输入",
            }
        ]
        contract_path.write_text(json.dumps(contract, indent=2), encoding="utf-8")

        self.assertEqual([], load_tool().validate_agents_root(self.agents_root))

    def test_validate_rejects_build_effect_without_build_artifact_evidence(self):
        """声明 build 副作用时不能用 compile 或 play 充当构建产物证据。"""
        catalog = self._read_catalog()
        build_entry = catalog["skills"][1]
        build_entry["effects"].append("build")
        self._write_catalog(catalog)
        contract_path = (
            self.agents_root
            / build_entry["path"]
            / "references"
            / "contract.json"
        )
        contract = json.loads(contract_path.read_text(encoding="utf-8"))
        contract["effects"] = build_entry["effects"]
        contract_path.write_text(json.dumps(contract, indent=2), encoding="utf-8")

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("build 副作用" in error for error in errors))

    def test_validate_rejects_build_evidence_without_build_effect(self):
        """构建产物级证据只能用于声明 build 副作用的 Skill。"""
        catalog = self._read_catalog()
        build_entry = catalog["skills"][1]
        build_entry["minimumEvidence"] = "bundle-build"
        self._write_catalog(catalog)
        contract_path = (
            self.agents_root
            / build_entry["path"]
            / "references"
            / "contract.json"
        )
        contract = json.loads(contract_path.read_text(encoding="utf-8"))
        contract["minimumEvidence"] = "bundle-build"
        contract_path.write_text(json.dumps(contract, indent=2), encoding="utf-8")

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("构建产物级证据" in error for error in errors))

    def test_catalog_schema_declares_p1_lifecycle_and_build_values(self):
        """静态 Schema 必须与 P1 的生命周期与构建证据契约保持一致。"""
        canonical_schema_path = TOOL_PATH.parent.parent / "Schemas" / "catalog.schema.json"
        schema = json.loads(canonical_schema_path.read_text(encoding="utf-8"))
        skill_properties = schema["properties"]["skills"]["items"]["properties"]

        self.assertIn("replacedBy", skill_properties)
        self.assertIn("build", skill_properties["effects"]["items"]["enum"])
        self.assertIn("bundle-build", skill_properties["minimumEvidence"]["enum"])
        self.assertIn("player-build", skill_properties["minimumEvidence"]["enum"])

    def test_current_catalog_registers_project_skill_set_including_next_wave_skills(self):
        """Catalog 必须发现当前完整消费端能力集合。"""
        canonical_catalog_path = TOOL_PATH.parent.parent / "catalog.json"
        catalog = json.loads(canonical_catalog_path.read_text(encoding="utf-8"))
        entries = {entry["id"]: entry for entry in catalog["skills"]}

        self.assertEqual(
            {
                "nova-project-router",
                "nova-project-check-readiness",
                "nova-project-export-tables",
                "nova-project-integrate-table",
                "nova-project-configure-runtime",
                "nova-project-diagnose-startup",
                "nova-project-ui-create-view",
                "nova-project-update-ui-view",
                "nova-project-integrate-resource",
                "nova-project-setup-entry-scene",
                "nova-project-update-localization",
                "nova-project-build-bundles",
                "nova-project-build-player",
                "nova-project-data-driven-ui",
                "nova-project-integrate-network-api",
                "nova-project-integrate-sound",
                "nova-project-refresh-hotfix-dlls",
                "nova-project-manage-upm-package",
                "nova-project-upgrade-framework",
                "nova-project-integrate-vibration",
                "nova-project-integrate-procedure",
                "nova-project-generate-hybridclr-artifacts",
                "nova-project-integrate-event",
                "nova-project-integrate-persistence",
                "nova-project-integrate-content-scene",
                "nova-project-diagnose-build",
                "nova-project-onboard-sdk-kit",
                "nova-project-diagnose-device-runtime",
                "nova-project-preflight-build",
                "nova-project-resolve-android-dependencies",
            },
            set(entries),
        )
        self.assertEqual("workflow", entries["nova-project-data-driven-ui"]["kind"])
        self.assertIn("build", entries["nova-project-build-bundles"]["effects"])
        self.assertEqual(
            "bundle-build",
            entries["nova-project-build-bundles"]["minimumEvidence"],
        )
        self.assertEqual(
            "player-build",
            entries["nova-project-build-player"]["minimumEvidence"],
        )
        network_api = entries["nova-project-integrate-network-api"]
        self.assertEqual("operation", network_api["kind"])
        self.assertEqual(["feature", "network"], network_api["journeys"])
        self.assertEqual(
            ["workspace-write", "unity-write", "generated-output"],
            network_api["effects"],
        )
        self.assertEqual("play", network_api["minimumEvidence"])
        self.assertIn(
            "nova-project-integrate-network-api",
            catalog["capabilityGroups"]["network"],
        )

        agents_root = TOOL_PATH.parent.parent
        contract = json.loads(
            (
                agents_root
                / "Skills"
                / "nova-project-integrate-network-api"
                / "references"
                / "contract.json"
            ).read_text(encoding="utf-8")
        )
        input_names = {item["name"] for item in contract["inputs"]}
        input_requirements = {
            item["name"]: item["required"] for item in contract["inputs"]
        }
        adapter_entries = {item["entry"] for item in contract["actionAdapters"]}
        skill_content = (
            agents_root
            / "Skills"
            / "nova-project-integrate-network-api"
            / "SKILL.md"
        ).read_text(encoding="utf-8")
        openai_yaml = (
            agents_root
            / "Skills"
            / "nova-project-integrate-network-api"
            / "agents"
            / "openai.yaml"
        ).read_text(encoding="utf-8")
        agents_index = (agents_root / "INDEX.md").read_text(encoding="utf-8")
        router_content = (
            agents_root / "Skills" / "nova-project-router" / "SKILL.md"
        ).read_text(encoding="utf-8")

        self.assertEqual([], contract["requires"])
        self.assertIn("routeAndProtocolContract", input_names)
        self.assertIn("requestResponseAndReplaySemantics", input_names)
        self.assertIn("authenticationContract", input_names)
        self.assertIn("testEndpointAccountAndSuccessProbe", input_names)
        self.assertNotIn("testEndpointAndSuccessProbe", input_names)
        self.assertTrue(input_requirements["authenticationContract"])
        self.assertTrue(input_requirements["testEndpointAccountAndSuccessProbe"])
        self.assertIn("nova.project.network.export", adapter_entries)
        self.assertTrue(any("Nova.Network.LoadAsync" in entry for entry in adapter_entries))
        self.assertIn("nova.project.network.export", skill_content)
        self.assertNotIn("export.network.hostkey.data", adapter_entries)
        self.assertIn("不猜测协议", skill_content)
        self.assertIn("4xx / 5xx", skill_content)
        self.assertIn("内部 `NetService`", skill_content)
        self.assertIn("无测试端点、账号或成功探针时返回 `blocked`", skill_content)
        self.assertIn("输入已确认且已执行允许步骤", skill_content)
        self.assertIn("当前 Catalog 共 30 项", agents_index)
        self.assertNotIn("当前包含 13 个实验性 Skill", agents_index)
        self.assertIn("必填输入未确认按 Skill 返回 `blocked`", agents_index)
        self.assertIn(
            "输入已确认且已执行但未达到更高证据层级时才返回 `partial`",
            agents_index,
        )
        router_network_ambiguity_line = next(
            line
            for line in router_content.splitlines()
            if line.startswith("| 登录 / 领奖 / 业务 API |")
        )
        router_network_route_line = next(
            line
            for line in router_content.splitlines()
            if "nova-project-integrate-network-api" in line
        )
        self.assertIn(
            "HostKey / NetCmd 行、协议、认证、请求/响应、重放语义、业务调用入口、测试端点、测试账号和成功探针",
            router_network_ambiguity_line,
        )
        self.assertIn(
            "已确认协议、认证、路由、请求/响应、重放语义、业务调用入口、测试端点、测试账号和成功探针",
            router_network_route_line,
        )
        confirmation_content = json.dumps(contract["confirmation"], ensure_ascii=False)
        evidence_content = json.dumps(contract["evidence"], ensure_ascii=False)
        for required_confirmation_input in ("authentication", "测试账号", "成功探针"):
            self.assertIn(required_confirmation_input, confirmation_content)
        for required_evidence_input in ("认证", "账号", "成功探针"):
            self.assertIn(required_evidence_input, evidence_content)
        self.assertIn("$nova-project-integrate-network-api", openai_yaml)

    def test_current_catalog_registers_event_persist_scene_sdk_and_diagnostics_wave(self):
        """本轮八项能力必须保持业务写入、只读诊断与受控 Action 的边界。"""
        agents_root = TOOL_PATH.parent.parent
        catalog = json.loads((agents_root / "catalog.json").read_text(encoding="utf-8"))
        entries = {entry["id"]: entry for entry in catalog["skills"]}

        def contract(skill_id: str) -> dict:
            return json.loads(
                (agents_root / "Skills" / skill_id / "references" / "contract.json")
                .read_text(encoding="utf-8")
            )

        for skill_id in (
            "nova-project-integrate-event",
            "nova-project-integrate-persistence",
            "nova-project-integrate-content-scene",
        ):
            self.assertEqual(["workspace-write"], entries[skill_id]["effects"])
            self.assertEqual("play", entries[skill_id]["minimumEvidence"])
            self.assertEqual([], contract(skill_id)["requires"])

        for skill_id in (
            "nova-project-diagnose-build",
            "nova-project-diagnose-device-runtime",
            "nova-project-preflight-build",
        ):
            self.assertEqual(["read"], entries[skill_id]["effects"])
            self.assertEqual([], contract(skill_id)["writeScope"]["allow"])

        onboarding = contract("nova-project-onboard-sdk-kit")
        self.assertEqual(
            ["nova-project-manage-upm-package", "nova-project-configure-runtime"],
            onboarding["requires"],
        )
        self.assertEqual("workflow", entries["nova-project-onboard-sdk-kit"]["kind"])

        preflight_entries = {
            adapter["entry"] for adapter in contract("nova-project-preflight-build")["actionAdapters"]
        }
        self.assertTrue(any("nova.project.build.inspect-readiness" in entry for entry in preflight_entries))

        android = contract("nova-project-resolve-android-dependencies")
        self.assertEqual(
            ["agent-action"],
            [adapter["kind"] for adapter in android["actionAdapters"]],
        )
        self.assertEqual(
            ["nova.project.android.resolve-dependencies"],
            [adapter["entry"] for adapter in android["actionAdapters"]],
        )
        self.assertIn("generated-output", android["effects"])
        self.assertIn(
            "nova-project-resolve-android-dependencies",
            catalog["capabilityGroups"]["build"],
        )

    def test_current_catalog_registers_sound_integration_skill(self):
        """P2-A 的声音闭环必须以真实 Adapter、播放证据和安全边界登记。"""
        agents_root = TOOL_PATH.parent.parent
        catalog = json.loads((agents_root / "catalog.json").read_text(encoding="utf-8"))
        entries = {entry["id"]: entry for entry in catalog["skills"]}
        sound = entries["nova-project-integrate-sound"]

        self.assertEqual("operation", sound["kind"])
        self.assertEqual(["feature", "sound"], sound["journeys"])
        self.assertEqual(
            ["workspace-write", "unity-write", "generated-output"],
            sound["effects"],
        )
        self.assertEqual("play", sound["minimumEvidence"])
        self.assertIn("nova-project-integrate-sound", catalog["capabilityGroups"]["sound"])

        sound_root = agents_root / "Skills" / "nova-project-integrate-sound"
        contract = json.loads(
            (sound_root / "references" / "contract.json").read_text(encoding="utf-8")
        )
        input_requirements = {
            item["name"]: item["required"] for item in contract["inputs"]
        }
        adapter_entries = {item["entry"] for item in contract["actionAdapters"]}
        skill_content = (sound_root / "SKILL.md").read_text(encoding="utf-8")
        router_content = (
            agents_root / "Skills" / "nova-project-router" / "SKILL.md"
        ).read_text(encoding="utf-8")
        agents_index = (agents_root / "INDEX.md").read_text(encoding="utf-8")
        docs_index = (agents_root.parent / "Docs" / "INDEX.md").read_text(
            encoding="utf-8"
        )
        quick_start = (agents_root.parent / "Docs" / "START_HERE.md").read_text(
            encoding="utf-8"
        )
        openai_yaml = (sound_root / "agents" / "openai.yaml").read_text(
            encoding="utf-8"
        )

        self.assertEqual([], contract["requires"])
        for required_input in (
            "projectRoot",
            "soundComponentSettingsAndActiveScene",
            "soundTableAndExportScope",
            "audioClipOwnershipCollectorAndAddress",
            "soundRowAndGroupContract",
            "businessTriggerAndLifecycle",
            "runtimeSuccessProbe",
            "activeConfigMasterAndResolvedYooAssetPaths",
            "targetPlatformChannelAndMode",
        ):
            self.assertTrue(input_requirements.get(required_input))
        self.assertIn(
            "EditorUtil.Config.DimensionalResolver.ResolveYooAsset / "
            "YooAsset.Editor.SettingLoader.LoadSettingDataAtPath<BundleCollectorSetting>",
            adapter_entries,
        )
        self.assertNotIn(
            "EditorUtil.Config.YooAssetInjector.LoadBundleCollector",
            adapter_entries,
        )
        self.assertIn("nova.project.sound.export", adapter_entries)
        self.assertNotIn("export.sound.data / export.sound.code", adapter_entries)
        self.assertIn(
            "Nova.Sound.LoadAsync / HasSoundGroup / PlaySound / StopSound / ReleaseAssetBySerialID",
            adapter_entries,
        )
        self.assertIn("serialID 不是播放成功证据", skill_content)
        self.assertIn("LoadAsync()==true 也不是播放成功证据", skill_content)
        self.assertIn("输入已确认且已执行允许步骤", skill_content)
        self.assertIn("实际 AudioSource 播放", skill_content)
        self.assertIn("不得默认使用 `export.excel.all`", skill_content)
        self.assertIn("YooAssetEditorConfigsMask", skill_content)
        self.assertIn("ResolveYooAsset", skill_content)
        self.assertIn("不得调用 `YooAssetInjector.LoadBundleCollector`", skill_content)
        self.assertTrue(
            {
                "active-config-master",
                "yooasset-config-coordinate",
            }
            <= set(contract["locks"])
        )
        self.assertIn("当前 Catalog 共 30 项", agents_index)
        self.assertIn("nova-project-integrate-sound", agents_index)
        self.assertIn("当前 30 项能力", docs_index)
        self.assertIn("大厅 BGM", quick_start)
        router_sound_ambiguity_line = next(
            line
            for line in router_content.splitlines()
            if line.startswith("| BGM / 背景音乐 / 点击音效 / 声音 |")
        )
        router_sound_route_line = next(
            line
            for line in router_content.splitlines()
            if "nova-project-integrate-sound" in line
        )
        self.assertIn(
            "真实 AudioClip、Collector、地址、Sound 表、声音组、加载入口、业务触发、停止生命周期和实际播放成功探针",
            router_sound_ambiguity_line,
        )
        self.assertIn(
            "已确认声音表、真实 AudioClip、Collector、地址、声音组、加载入口、业务触发、停止生命周期和实际播放探针",
            router_sound_route_line,
        )
        self.assertIn("$nova-project-integrate-sound", openai_yaml)
        for relative_path in (
            "SKILL.md",
            "SKILL.md.meta",
            "agents.meta",
            "agents/openai.yaml",
            "agents/openai.yaml.meta",
            "references.meta",
            "references/contract.json",
            "references/contract.json.meta",
        ):
            self.assertTrue((sound_root / relative_path).is_file())
        self.assertTrue((sound_root.parent / "nova-project-integrate-sound.meta").is_file())

    def test_current_catalog_registers_hotfix_dll_refresh_skill(self):
        """P2 业务热更 DLL 刷新必须锁定单坐标 compile-copy-import 闭环。"""
        agents_root = TOOL_PATH.parent.parent
        catalog = json.loads((agents_root / "catalog.json").read_text(encoding="utf-8"))
        entries = {entry["id"]: entry for entry in catalog["skills"]}
        hotfix = entries["nova-project-refresh-hotfix-dlls"]

        self.assertEqual(30, len(entries))
        self.assertEqual("operation", hotfix["kind"])
        self.assertEqual(["build", "hotfix"], hotfix["journeys"])
        self.assertEqual(
            ["workspace-write", "unity-write", "generated-output"],
            hotfix["effects"],
        )
        self.assertEqual("compile", hotfix["minimumEvidence"])
        self.assertIn(
            "nova-project-refresh-hotfix-dlls",
            catalog["capabilityGroups"]["hotfix"],
        )

        skill_root = agents_root / "Skills" / "nova-project-refresh-hotfix-dlls"
        contract = json.loads(
            (skill_root / "references" / "contract.json").read_text(encoding="utf-8")
        )
        input_requirements = {
            item["name"]: item["required"] for item in contract["inputs"]
        }
        adapter_entries = {item["entry"] for item in contract["actionAdapters"]}
        skill_content = (skill_root / "SKILL.md").read_text(encoding="utf-8")
        openai_yaml = (skill_root / "agents" / "openai.yaml").read_text(
            encoding="utf-8"
        )
        router_content = (
            agents_root / "Skills" / "nova-project-router" / "SKILL.md"
        ).read_text(encoding="utf-8")
        agents_index = (agents_root / "INDEX.md").read_text(encoding="utf-8")
        docs_index = (agents_root.parent / "Docs" / "INDEX.md").read_text(
            encoding="utf-8"
        )
        quick_start = (agents_root.parent / "Docs" / "START_HERE.md").read_text(
            encoding="utf-8"
        )
        hybridclr_docs = (
            agents_root.parent
            / "Docs"
            / "Editor"
            / "EditorUtil"
            / "EditorUtil.HybridCLR"
            / "EditorUtil.HybridCLR.md"
        ).read_text(encoding="utf-8")

        self.assertEqual([], contract["requires"])
        for required_input in (
            "projectRoot",
            "activeBuildTarget",
            "developmentBuild",
            "activeConfigMaster",
            "platformChannelDevelopMode",
            "resolvedGameDllEntries",
            "executionEntry",
        ):
            self.assertTrue(input_requirements[required_input])
        self.assertNotIn("hotUpdateAssemblySet", input_requirements)
        self.assertNotIn("runtimeSmokeContext", input_requirements)
        self.assertEqual(
            {
                "nova.project.hotfix.refresh-game-dlls",
            },
            adapter_entries,
        )
        self.assertEqual("agent-action", contract["actionAdapters"][0]["kind"])
        self.assertIn("compile -> copy", contract["actionAdapters"][0]["when"])
        self.assertEqual(
            [
                "unity-editor",
                "asset-database",
                "build-settings",
                "active-config-master",
                "hybridclr-hot-update-output",
                "game-dll-targets",
            ],
            contract["locks"],
        )
        self.assertEqual("ensure-state", contract["idempotency"])
        self.assertEqual("compile", contract["minimumEvidence"])
        self.assertEqual(
            ["success", "partial", "blocked", "not_applicable"],
            contract["resultStates"],
        )
        write_scope = json.dumps(contract["writeScope"], ensure_ascii=False)
        for allowed_scope in (
            "current activeBuildTarget",
            "complete HybridCLR compile output root",
            "StartupGameDlls + RunningGameDlls",
            "Unity import",
            "本次证据",
        ):
            self.assertIn(allowed_scope, write_scope)
        for denied_scope in (
            "AOT metadata",
            "link.xml",
            "GenerateAll",
            "GeneratedCpp",
            "Il2CppDef",
            "ConfigMaster",
            "ConfigRuntime",
            "Bundle",
            "Player",
            "CDN",
            "Git",
            "Framework",
            "Library",
            "其他 Target",
        ):
            self.assertIn(denied_scope, write_scope)
        confirmation = json.dumps(contract["confirmation"], ensure_ascii=False)
        for frozen_value in (
            "activeBuildTarget",
            "developmentBuild",
            "activeConfigMaster",
            "Platform/Channel/DevelopMode",
            "完整 HybridCLR 编译输出根目录",
            "完整 DLL 映射",
            "executionEntry",
            "旧确认失效",
        ):
            self.assertIn(frozen_value, confirmation)

        self.assertIn("禁止单独调用 `CopyGameDlls`", skill_content)
        self.assertIn("compile -> copy", skill_content)
        self.assertIn("SHA-256", skill_content)
        self.assertNotIn("runtimeSmokeContext", skill_content)
        self.assertNotIn("hotUpdateAssemblySet", skill_content)
        self.assertIn("固定为 MCP Action", skill_content)
        self.assertIn("不退化为任意代码执行", skill_content)
        self.assertIn("完整 HybridCLR 编译输出根目录", skill_content)
        self.assertIn("不得把编译或本地 DLL 刷新称为 Bundle、Player、CDN", skill_content)
        self.assertIn("运行时或真机成功", skill_content)
        self.assertIn("full GenerateAll", skill_content)
        self.assertIn("对应既有或未来 Operation", skill_content)
        self.assertIn("$nova-project-refresh-hotfix-dlls", openai_yaml)

        router_hotfix_ambiguity_line = next(
            line
            for line in router_content.splitlines()
            if line.startswith("| HybridCLR / 热更 DLL |")
        )
        router_hotfix_route_line = next(
            line
            for line in router_content.splitlines()
            if "nova-project-refresh-hotfix-dlls" in line
        )
        self.assertIn(
            "业务 DLL 本地刷新、full AOT、Bundle、Player、CDN 或运行时诊断",
            router_hotfix_ambiguity_line,
        )
        self.assertIn("compile -> copy", router_hotfix_route_line)
        self.assertIn("full AOT、Bundle、Player", router_hotfix_route_line)
        self.assertIn("当前 Catalog 共 30 项", agents_index)
        self.assertIn("仅刷新本地业务 DLL，不是发布", agents_index)
        self.assertIn("当前 30 项能力", docs_index)
        self.assertIn("刷新 HybridCLR 业务热更 DLL", docs_index)
        self.assertIn("当前 30 项", quick_start)
        self.assertIn("刷新 HybridCLR 业务热更 DLL", quick_start)
        self.assertIn("ConfigMasterSO.HybridEditorConfigs", hybridclr_docs)
        self.assertNotIn("ConfigMasterSO.AotMetadataDlls", hybridclr_docs)

    def test_data_driven_ui_workflow_declares_required_operation_boundaries(self):
        """数据驱动 UI 编排 Table 与 UI Operation 时必须显式继承两者的授权边界。"""
        skills_root = TOOL_PATH.parent.parent / "Skills"

        def read_contract(skill_id: str) -> dict:
            return json.loads(
                (skills_root / skill_id / "references" / "contract.json").read_text(
                    encoding="utf-8"
                )
            )

        workflow = read_contract("nova-project-data-driven-ui")
        table = read_contract("nova-project-integrate-table")
        ui_create = read_contract("nova-project-ui-create-view")
        workflow_input_names = {item["name"] for item in workflow["inputs"]}
        child_input_names = {
            item["name"] for child in (table, ui_create) for item in child["inputs"]
        }
        workflow_allowed_scope = set(workflow["writeScope"]["allow"])
        child_allowed_scope = {
            path
            for child in (table, ui_create)
            for path in child["writeScope"]["allow"]
        }
        workflow_adapter_kinds = {
            adapter["kind"] for adapter in workflow["actionAdapters"]
        }
        workflow_confirmation = "\n".join(workflow["confirmation"]["requiredFor"])

        self.assertTrue(child_input_names <= workflow_input_names)
        self.assertTrue(child_allowed_scope <= workflow_allowed_scope)
        self.assertTrue(set(table["locks"]) <= set(workflow["locks"]))
        self.assertTrue(
            {"workspace-edit", "unity-editor-api", "agent-action", "csharp-api", "unity-editor-automation", "unity-menu"}
            <= workflow_adapter_kinds
        )
        self.assertIn("ProjectId", workflow_confirmation)
        self.assertIn("DescriptionId", workflow_confirmation)
        self.assertIn("DataTarget", workflow_confirmation)
        self.assertIn("Asset address", workflow_confirmation)

    def test_docs_expose_current_catalog_skills_and_progressive_entry(self):
        """项目组入口必须能发现全部 Catalog Skill 与渐进式执行方式。"""
        canonical_catalog_path = TOOL_PATH.parent.parent / "catalog.json"
        catalog = json.loads(canonical_catalog_path.read_text(encoding="utf-8"))
        agents_index = (TOOL_PATH.parent.parent / "INDEX.md").read_text(encoding="utf-8")
        quick_start = (TOOL_PATH.parent.parent.parent / "Docs" / "START_HERE.md").read_text(
            encoding="utf-8"
        )

        for entry in catalog["skills"]:
            self.assertIn(entry["id"], agents_index)
        self.assertIn("渐进式披露", quick_start)
        self.assertIn("自然语言", quick_start)

    def test_validate_accepts_deprecated_skill_with_replacement(self):
        """弃用 Skill 可保留一次发布期，并明确引导到当前替代 Skill。"""
        catalog = self._read_catalog()
        catalog["skills"][0]["status"] = "deprecated"
        catalog["skills"][0]["replacedBy"] = "nova-project-ui-create-view"
        self._write_catalog(catalog)

        self.assertEqual([], load_tool().validate_agents_root(self.agents_root))

    def test_validate_rejects_deprecated_skill_without_replacement(self):
        """已弃用 Skill 不能没有项目组可执行的替代路径。"""
        catalog = self._read_catalog()
        catalog["skills"][0]["status"] = "deprecated"
        self._write_catalog(catalog)

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("replacedBy" in error for error in errors))

    def test_validate_rejects_cycle_in_workflow_requires(self):
        """Workflow 的内部 DAG 不能形成循环；这与安装和全量发现无关。"""
        catalog = self._read_catalog()
        for entry in catalog["skills"]:
            entry["kind"] = "workflow"
        self._write_catalog(catalog)
        first_contract_path = (
            self.agents_root
            / "Skills"
            / "nova-project-router"
            / "references"
            / "contract.json"
        )
        second_contract_path = (
            self.agents_root
            / "Skills"
            / "nova-project-ui-create-view"
            / "references"
            / "contract.json"
        )
        first_contract = json.loads(first_contract_path.read_text(encoding="utf-8"))
        second_contract = json.loads(second_contract_path.read_text(encoding="utf-8"))
        first_contract["kind"] = "workflow"
        second_contract["kind"] = "workflow"
        first_contract["requires"] = ["nova-project-ui-create-view"]
        second_contract["requires"] = ["nova-project-router"]
        first_contract_path.write_text(json.dumps(first_contract, indent=2), encoding="utf-8")
        second_contract_path.write_text(json.dumps(second_contract, indent=2), encoding="utf-8")

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("循环" in error for error in errors))

    def test_validate_rejects_requires_on_non_workflow(self):
        """Operation 和 Router 不得把 requires 当作安装或隐藏编排依赖。"""
        contract_path = (
            self.agents_root
            / "Skills"
            / "nova-project-router"
            / "references"
            / "contract.json"
        )
        contract = json.loads(contract_path.read_text(encoding="utf-8"))
        contract["requires"] = ["nova-project-ui-create-view"]
        contract_path.write_text(json.dumps(contract, indent=2), encoding="utf-8")

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("仅 Workflow" in error for error in errors))

    def test_validate_rejects_workflow_requires_non_operation(self):
        """Workflow 的内部 DAG 只能编排可独立验收的 Operation。"""
        self._append_workflow(
            "nova-project-test-workflow", ["nova-project-router"]
        )

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("只能依赖 Operation" in error for error in errors))

    def test_cli_adapters_use_resolved_package_project_and_host_python(self):
        """消费端 CLI 必须使用已解析包、项目根及宿主平台可用的 Python 3.9+。"""
        for skill_id in (
            "nova-project-check-readiness",
            "nova-project-diagnose-startup",
        ):
            contract_path = (
                TOOL_PATH.parent.parent
                / "Skills"
                / skill_id
                / "references"
                / "contract.json"
            )
            contract = json.loads(contract_path.read_text(encoding="utf-8"))
            cli_adapters = [
                adapter
                for adapter in contract["actionAdapters"]
                if adapter["kind"] == "cli"
            ]
            cli_entries = {adapter["entry"] for adapter in cli_adapters}
            cli_conditions = {adapter["when"] for adapter in cli_adapters}
            for cli_entry in cli_entries:
                self.assertNotIn("Assets/Framework", cli_entry)
                self.assertIn("Agents/Tools/nova_skills.py", cli_entry)
                self.assertIn("--project-root <projectRoot>", cli_entry)
            self.assertTrue(any(entry.startswith("python3 ") for entry in cli_entries))
            self.assertTrue(any(entry.startswith("py -3 ") for entry in cli_entries))
            self.assertTrue(any("macOS/Linux" in condition for condition in cli_conditions))
            self.assertTrue(any("Windows" in condition for condition in cli_conditions))
            skill_path = TOOL_PATH.parent.parent / "Skills" / skill_id / "SKILL.md"
            skill_content = skill_path.read_text(encoding="utf-8")
            self.assertIn("Python 3.9+", skill_content)
            self.assertIn("py -3", skill_content)

    def test_docs_reference_real_bundlebuilder_pipify_step(self):
        """项目文档不能把旧 Step 名称教给消费端 Agent。"""
        docs_index = (TOOL_PATH.parent.parent.parent / "Docs" / "INDEX.md").read_text(
            encoding="utf-8"
        )

        self.assertIn("bundlebuilder.build", docs_index)
        self.assertNotIn("assetbundle.build", docs_index)

    def test_tree_hash_sorts_normalized_posix_relative_paths_by_utf8_bytes(self):
        """Python 与 Editor bridge 必须以同一 UTF-8 字节序计算真实 Skill 目录哈希。"""
        skill_dir = self.root / "HashFixture"
        files = {
            "SKILL.md": b"root\n",
            "agents.meta": b"folder metadata\n",
            "agents/openai.yaml": b"interface:\n",
        }
        for relative_path, content in files.items():
            path = skill_dir / relative_path
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)

        digest = hashlib.sha256()
        for relative_path in ("SKILL.md", "agents.meta", "agents/openai.yaml"):
            digest.update(relative_path.encode("utf-8"))
            digest.update(b"\0")
            digest.update(files[relative_path])
            digest.update(b"\0")

        self.assertEqual(digest.hexdigest(), load_tool()._tree_hash(skill_dir))

    def test_validate_accepts_the_canonical_framework_agents_tree(self):
        """随 Framework 发布的真实 Agents 真源也必须通过同一份契约校验。"""
        canonical_agents_root = TOOL_PATH.parent.parent

        self.assertEqual([], load_tool().validate_agents_root(canonical_agents_root))

    def test_canonical_project_skills_read_shared_quick_start_first(self):
        """每个随包项目组 Skill 都要先加载同一份 Nova 共同底线。"""
        canonical_agents_root = TOOL_PATH.parent.parent
        catalog = load_tool().load_catalog(canonical_agents_root)
        expected_first_paragraph = (
            "触发后先读取当前 Framework 的 `Docs/START_HERE.md`，"
            "作为所有 `nova-project-*` Skill 的共同底线。"
        )

        self.assertTrue((canonical_agents_root.parent / "Docs" / "START_HERE.md").is_file())
        for entry in catalog["skills"]:
            skill_id = entry["id"]
            with self.subTest(skill_id=skill_id):
                content = (
                    canonical_agents_root / entry["path"] / "SKILL.md"
                ).read_text(encoding="utf-8")
                body = content.split("---", 2)[2].strip()
                paragraphs = [paragraph.strip() for paragraph in body.split("\n\n") if paragraph.strip()]

                self.assertGreaterEqual(len(paragraphs), 2)
                self.assertEqual(expected_first_paragraph, paragraphs[1])

    def test_quick_start_declares_shared_project_skill_source_and_namespace(self):
        """项目组入口必须说明共享真源及其与框架开发 Skill 的边界。"""
        quick_start_path = TOOL_PATH.parent.parent.parent / "Docs" / "START_HERE.md"
        content = quick_start_path.read_text(encoding="utf-8")

        self.assertIn("所有 `nova-project-*` Skill 触发后都先读取本页", content)
        self.assertIn("开发态和消费态共享的唯一 Git 真源", content)
        self.assertIn("框架开发态 Skill 保留在仓库根 `.agents/skills/`", content)

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

    def test_quick_start_documents_package_snapshot_and_project_local_boundary(self):
        """快速入口必须区分开发仓真源、安装态快照和项目本地投影。"""
        quick_start_path = TOOL_PATH.parent.parent.parent / "Docs" / "START_HERE.md"
        content = quick_start_path.read_text(encoding="utf-8")

        self.assertIn("Assets/Framework/Agents/Skills/", content)
        self.assertIn("PackageInfo.resolvedPath/Agents/Skills/", content)
        self.assertIn(".agents/skills", content)
        self.assertIn("nova-skills.transaction.json", content)
        self.assertIn("不需要执行 `sync`", content)
        self.assertIn("自然语言", content)
        self.assertIn("不需要执行 `sync` 或手工复制", content)
        self.assertNotIn("Framework 包内 `Assets/Framework/Agents/Skills/`", content)

    def test_project_skill_guidance_describes_direct_automatic_projection(self):
        """项目组入口只说明自动全量投影，不引入不存在的安装选择。"""
        guidance_paths = [
            TOOL_PATH.parent.parent / "AGENTS.md",
            TOOL_PATH.parent.parent / "INDEX.md",
            TOOL_PATH.parent.parent.parent / "Docs" / "START_HERE.md",
            TOOL_PATH.parent.parent.parent / "Docs" / "INDEX.md",
            TOOL_PATH.parent.parent.parent / "README.md",
        ]

        for guidance_path in guidance_paths:
            with self.subTest(guidance_path=guidance_path):
                content = guidance_path.read_text(encoding="utf-8")
                self.assertIn("自动", content)
                self.assertIn("不需要执行 `sync` 或手工复制", content)

    def test_quick_start_requires_explicit_consent_before_consumer_gitignore_write(self):
        """消费项目 Git 忽略规则必须可复制，但不得授权 bridge 静默改写该文件。"""
        quick_start_path = TOOL_PATH.parent.parent.parent / "Docs" / "START_HERE.md"
        quick_start = quick_start_path.read_text(encoding="utf-8")
        documentation_index = (quick_start_path.parent / "INDEX.md").read_text(encoding="utf-8")

        self.assertIn("不会创建、修改或覆盖消费者项目的 `.gitignore`", quick_start)
        self.assertIn("自行跟踪 `.agents/`", quick_start)
        self.assertIn("用户已明确确认", quick_start)
        self.assertIn(
            "```gitignore\n"
            "/.agents/skills/nova-project-*/\n"
            "/.agents/nova-skills.lock.json\n"
            "/.agents/nova-skills.transaction.json\n"
            "/.agents/.nova-skills-staging/\n"
            "```",
            quick_start,
        )
        self.assertIn("消费者 Git 边界见 [START_HERE.md]", documentation_index)

    def test_project_skill_docs_route_to_the_canonical_quick_start_boundary(self):
        """维护入口不得把开发仓物理路径误写为消费端的包内路径。"""
        framework_root = TOOL_PATH.parents[2]
        documents = {
            framework_root / "README.md": "Docs/START_HERE.md",
            framework_root / "Agents" / "AGENTS.md": "../Docs/START_HERE.md",
            framework_root / "Agents" / "INDEX.md": "../Docs/START_HERE.md",
        }

        for document_path, quick_start_link in documents.items():
            content = document_path.read_text(encoding="utf-8")
            self.assertIn(quick_start_link, content)
            self.assertNotIn("Framework 包内 `Assets/Framework/Agents/Skills/`", content)

    def test_root_gitignore_ignores_only_project_local_nova_skill_projection(self):
        """自动投影不得污染 Git，且不能误忽略项目自己维护的其它 .agents 内容。"""
        root_gitignore = TOOL_PATH.parents[4] / ".gitignore"
        content = root_gitignore.read_text(encoding="utf-8")

        for rule in (
            "/.agents/skills/nova-project-*/",
            "/.agents/nova-skills.lock.json",
            "/.agents/nova-skills.transaction.json",
            "/.agents/.nova-skills-staging/",
            "**/__pycache__.meta",
            "**/*.pyc.meta",
        ):
            self.assertIn(rule, content)
        self.assertNotIn("/.agents/\n", content)

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

    def test_validate_accepts_catalog_without_capability_groups(self):
        """全量分发以 catalog.skills 为准，能力分组只是可选导航信息。"""
        catalog_path = self.agents_root / "catalog.json"
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        catalog.pop("capabilityGroups")
        catalog_path.write_text(json.dumps(catalog), encoding="utf-8")

        self.assertEqual([], load_tool().validate_agents_root(self.agents_root))

    def test_validate_accepts_first_release_catalog_schema_version_one(self):
        """唯一全量 Catalog 格式使用 schemaVersion 1。"""
        catalog = self._read_catalog()
        catalog["schemaVersion"] = 1
        self._write_catalog(catalog)

        self.assertEqual([], load_tool().validate_agents_root(self.agents_root))

    def test_validate_rejects_unrecognized_catalog_schema_version_two(self):
        """首发格式之外的 Catalog 版本不能被静默当成另一套安装方案。"""
        catalog = self._read_catalog()
        catalog["schemaVersion"] = 2
        self._write_catalog(catalog)

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("schemaVersion 必须为 1" in error for error in errors))

    def test_validate_rejects_unknown_catalog_field(self):
        """Catalog 顶层未知字段必须按当前 schema fail-closed。"""
        catalog = self._read_catalog()
        catalog["unexpected"] = True
        self._write_catalog(catalog)

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("未知字段" in error and "unexpected" in error for error in errors))

    def test_validate_rejects_unknown_skill_entry_field(self):
        """Skill entry 未知字段不能绕过当前 schema 的 additionalProperties=false。"""
        catalog = self._read_catalog()
        catalog["skills"][0]["unexpected"] = True
        self._write_catalog(catalog)

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("未知字段" in error and "unexpected" in error for error in errors))

    def test_validate_requires_skill_status_and_journeys(self):
        """每个 Skill 都必须显式声明 status 与 journeys，不能依赖隐式默认值。"""
        catalog = self._read_catalog()
        catalog["skills"][0].pop("status")
        catalog["skills"][1].pop("journeys")
        self._write_catalog(catalog)

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("nova-project-router" in error and "status" in error for error in errors))
        self.assertTrue(
            any("nova-project-ui-create-view" in error and "journeys" in error for error in errors)
        )

    def test_validate_rejects_unsupported_skill_status(self):
        """status 只能使用 experimental、stable 或 deprecated。"""
        catalog = self._read_catalog()
        catalog["skills"][0]["status"] = "preview"
        self._write_catalog(catalog)

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("status" in error and "preview" in error for error in errors))

    def test_validate_rejects_empty_or_non_string_journeys(self):
        """journeys 必须是非空字符串数组。"""
        for journeys in ([], ["assessment", 1]):
            with self.subTest(journeys=journeys):
                catalog = self._read_catalog()
                catalog["skills"][0]["journeys"] = journeys
                self._write_catalog(catalog)

                errors = load_tool().validate_agents_root(self.agents_root)

                self.assertTrue(any("journeys" in error for error in errors))

    def test_validate_rejects_duplicate_skill_effects(self):
        """effects 必须唯一，避免 Catalog 与 contract 的副作用语义含混。"""
        catalog = self._read_catalog()
        catalog["skills"][0]["effects"] = ["read", "read"]
        self._write_catalog(catalog)

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("effects" in error and "重复" in error for error in errors))

    def test_validate_rejects_skill_id_outside_nova_project_namespace(self):
        """Catalog 不得把用户或其它系统的 Skill 纳入 Nova 受管投影。"""
        catalog = self._read_catalog()
        catalog["skills"][0]["id"] = "project-private-skill"
        catalog["skills"][0]["path"] = "Skills/project-private-skill"
        self._write_catalog(catalog)

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("项目组 Skill id" in error for error in errors))

    def test_reconcile_rejects_non_project_skill_in_managed_state(self):
        """既有 lock 也不得借受管记录取得用户或其它系统 Skill 的所有权。"""
        self._write_consumer_manifest()
        agents_dir = self.project_root / ".agents"
        agents_dir.mkdir(parents=True)
        zero_hash = "0" * 64
        (agents_dir / "nova-skills.lock.json").write_text(
            json.dumps(
                {
                    "schemaVersion": 1,
                    "package": "com.solotopia.nova.framework",
                    "packageVersion": "0.6.9",
                    "catalogHash": zero_hash,
                    "managed": {
                        "other-system-skill": {
                            "sourceHash": zero_hash,
                            "targetHash": zero_hash,
                        }
                    },
                }
            ),
            encoding="utf-8",
        )

        with self.assertRaisesRegex(RuntimeError, "非项目组 Skill id"):
            load_tool().reconcile(self.project_root)

    def test_reconcile_projects_every_catalog_skill(self):
        """首次 reconcile 必须投影 Catalog 声明的完整 Skill 集。"""
        catalog_path = self.agents_root / "catalog.json"
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        catalog.pop("capabilityGroups")
        catalog_path.write_text(json.dumps(catalog), encoding="utf-8")
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
        expected_ids = [entry["id"] for entry in catalog["skills"]]

        dry_run = tool.reconcile(self.project_root, dry_run=True)
        self.assertEqual(expected_ids, dry_run["added"])
        self.assertFalse((self.project_root / ".agents").exists())

        result = tool.reconcile(self.project_root)
        self.assertEqual(expected_ids, result["added"])
        for skill_id in expected_ids:
            self.assertTrue(
                (self.project_root / ".agents" / "skills" / skill_id / "SKILL.md").is_file()
            )
        state = json.loads(
            (self.project_root / ".agents" / "nova-skills.lock.json").read_text(
                encoding="utf-8"
            )
        )
        self.assertEqual(1, state["schemaVersion"])
        self.assertEqual(
            {"schemaVersion", "package", "packageVersion", "catalogHash", "managed"},
            set(state),
        )
        self.assertEqual(set(expected_ids), set(state["managed"]))

    def test_reconcile_rejects_unexpected_lock_field_without_rewriting_it(self):
        """受管 lock 的未知字段必须保留现场，不能据此认领消费者目录。"""
        self._write_consumer_manifest()
        agents_dir = self.project_root / ".agents"
        agents_dir.mkdir(parents=True)
        state_path = agents_dir / "nova-skills.lock.json"
        state_with_unexpected_field = {
            "schemaVersion": 1,
            "package": "com.solotopia.nova.framework",
            "packageVersion": "0.6.9",
            "catalogHash": "0" * 64,
            "unexpectedField": "unexpected",
            "managed": {},
        }
        original_content = json.dumps(state_with_unexpected_field)
        state_path.write_text(original_content, encoding="utf-8")

        with self.assertRaisesRegex(RuntimeError, "unexpectedField"):
            load_tool().reconcile(self.project_root)

        self.assertEqual(original_content, state_path.read_text(encoding="utf-8"))
        self.assertFalse((agents_dir / "skills").exists())

    def test_reconcile_rejects_unexpected_transaction_field_without_recovery(self):
        """受管 journal 的未知字段必须保留现场，不能被 bridge 当作可续传事务。"""
        self._write_consumer_manifest()
        tool = load_tool()
        transaction_id = "a" * 32
        agents_dir = self.project_root / ".agents"
        staging_dir = agents_dir / ".nova-skills-staging" / transaction_id
        source_dir = self.agents_root / "Skills" / "nova-project-router"
        staged_skill = staging_dir / "nova-project-router"
        staged_skill.parent.mkdir(parents=True)
        shutil.copytree(source_dir, staged_skill)
        skill_hash = tool._tree_hash(staged_skill)
        transaction_with_unexpected_field = {
            "schemaVersion": 1,
            "transactionId": transaction_id,
            "previousState": None,
            "finalState": {
                "schemaVersion": 1,
                "package": "com.solotopia.nova.framework",
                "packageVersion": "0.6.9",
                "unexpectedField": "unexpected",
                "managed": {
                    "nova-project-router": {
                        "sourceHash": skill_hash,
                        "targetHash": skill_hash,
                    }
                },
            },
            "pending": [
                {
                    "id": "nova-project-router",
                    "sourceHash": skill_hash,
                    "targetHash": skill_hash,
                }
            ],
        }
        transaction_path = agents_dir / "nova-skills.transaction.json"
        original_content = json.dumps(transaction_with_unexpected_field)
        transaction_path.write_text(original_content, encoding="utf-8")

        with self.assertRaisesRegex(RuntimeError, "unexpectedField"):
            tool.reconcile(self.project_root)

        self.assertEqual(original_content, transaction_path.read_text(encoding="utf-8"))
        self.assertTrue(staged_skill.is_dir())
        self.assertFalse((agents_dir / "skills" / "nova-project-router").exists())

    def test_reconcile_preserves_user_collision_and_updates_other_skills(self):
        """同名用户 Skill 不得被认领或覆盖，其他安全的 Nova Skill 仍应继续同步。"""
        catalog_path = self.agents_root / "catalog.json"
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        catalog.pop("capabilityGroups")
        catalog_path.write_text(json.dumps(catalog), encoding="utf-8")
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
        (user_skill / "SKILL.md").write_text("user owned", encoding="utf-8")

        result = load_tool().reconcile(self.project_root)

        self.assertEqual("user owned", (user_skill / "SKILL.md").read_text(encoding="utf-8"))
        self.assertEqual(["nova-project-ui-create-view"], result["added"])
        self.assertEqual(
            [{"id": "nova-project-router", "reason": "unowned-collision"}],
            result["conflicts"],
        )

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

    def test_reconcile_rejects_explicit_agents_root_symlink(self):
        """显式测试入口也不能通过预先 resolve 绕过 Agents 根软链检查。"""
        alternate_framework = self.root / "AlternateFramework"
        shutil.copytree(self.agents_root.parent, alternate_framework)
        shutil.rmtree(self.agents_root)
        os.symlink(alternate_framework / "Agents", self.agents_root)

        with self.assertRaisesRegex(RuntimeError, "Agents.*软链"):
            load_tool().reconcile(
                self.project_root,
                agents_root=self.agents_root,
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

    def test_resolve_registry_cache_accepts_hashed_directory_with_matching_version(self):
        """Unity 的 registry 缓存目录使用哈希后缀时，仍按 lock 版本解析。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {"dependencies": {"com.solotopia.nova.framework": "0.6.13"}}
            ),
            encoding="utf-8",
        )
        (packages_dir / "packages-lock.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": {
                            "version": "0.6.13",
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
            / "com.solotopia.nova.framework@50452fefa54c"
        )
        (cache_package / "Agents").mkdir(parents=True)
        (cache_package / "package.json").write_text(
            json.dumps(
                {
                    "name": "com.solotopia.nova.framework",
                    "version": "0.6.13",
                }
            ),
            encoding="utf-8",
        )

        self.assertEqual(
            (cache_package / "Agents").resolve(),
            load_tool().resolve_agents_root(self.project_root),
        )

    def test_resolve_registry_cache_rejects_multiple_matching_hashed_directories(self):
        """同一 lock 版本命中多个哈希缓存时，不得任意选择其中一个。"""
        packages_dir = self.project_root / "Packages"
        packages_dir.mkdir(parents=True)
        (packages_dir / "manifest.json").write_text(
            json.dumps(
                {"dependencies": {"com.solotopia.nova.framework": "0.6.13"}}
            ),
            encoding="utf-8",
        )
        (packages_dir / "packages-lock.json").write_text(
            json.dumps(
                {
                    "dependencies": {
                        "com.solotopia.nova.framework": {
                            "version": "0.6.13",
                            "source": "registry",
                        }
                    }
                }
            ),
            encoding="utf-8",
        )
        for cache_suffix in ("50452fefa54c", "8a51b18a04c2"):
            cache_package = (
                self.project_root
                / "Library"
                / "PackageCache"
                / f"com.solotopia.nova.framework@{cache_suffix}"
            )
            (cache_package / "Agents").mkdir(parents=True)
            (cache_package / "package.json").write_text(
                json.dumps(
                    {
                        "name": "com.solotopia.nova.framework",
                        "version": "0.6.13",
                    }
                ),
                encoding="utf-8",
            )

        with self.assertRaisesRegex(RuntimeError, "多个 registry Framework 候选"):
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

    def test_reconcile_is_idempotent_and_preserves_unrelated_user_skill(self):
        """重复 reconcile 只报告 unchanged，且不触碰不重名的项目私有 Skill。"""
        self._write_consumer_manifest()
        user_skill = self.project_root / ".agents" / "skills" / "project-private-skill"
        user_skill.mkdir(parents=True)
        (user_skill / "SKILL.md").write_text("private", encoding="utf-8")

        tool = load_tool()
        first = tool.reconcile(self.project_root)
        result = tool.reconcile(self.project_root)

        projected = self.project_root / ".agents" / "skills" / "nova-project-router"
        self.assertEqual(
            ["nova-project-router", "nova-project-ui-create-view"], first["added"]
        )
        self.assertTrue((projected / "SKILL.md").is_file())
        self.assertEqual(
            ["nova-project-router", "nova-project-ui-create-view"], result["unchanged"]
        )
        self.assertEqual("private", (user_skill / "SKILL.md").read_text(encoding="utf-8"))

    def test_reconcile_preserves_only_same_name_user_skill_and_writes_other_state(self):
        """仅有同名用户碰撞时也可安全登记其他全量受管 Skill。"""
        self._write_consumer_manifest()
        user_skill = self.project_root / ".agents" / "skills" / "nova-project-router"
        user_skill.mkdir(parents=True)
        marker = user_skill / "SKILL.md"
        marker.write_text("user owned", encoding="utf-8")

        result = load_tool().reconcile(self.project_root)

        self.assertEqual("user owned", marker.read_text(encoding="utf-8"))
        self.assertEqual(["nova-project-ui-create-view"], result["added"])
        state = json.loads(
            (self.project_root / ".agents" / "nova-skills.lock.json").read_text(
                encoding="utf-8"
            )
        )
        self.assertNotIn("nova-project-router", state["managed"])

    def test_reconcile_rejects_project_internal_agents_root_symlink_without_writing_alias(self):
        """.agents 指向项目内目录时也不能越过受管投影的物理写入边界。"""
        self._write_consumer_manifest()
        aliased_root = self.project_root / "Assets" / "AgentAlias"
        aliased_root.mkdir(parents=True)
        os.symlink(aliased_root, self.project_root / ".agents", target_is_directory=True)

        with self.assertRaisesRegex(RuntimeError, "软链"):
            load_tool().reconcile(self.project_root)

        self.assertFalse((aliased_root / "skills").exists())
        self.assertFalse((aliased_root / "nova-skills.lock.json").exists())

    def test_reconcile_rejects_project_internal_skills_root_symlink_without_writing_alias(self):
        """.agents/skills 是项目内软链时也不能把 Skill 写到其他业务目录。"""
        self._write_consumer_manifest()
        agents_dir = self.project_root / ".agents"
        agents_dir.mkdir()
        aliased_skills = self.project_root / "Assets" / "AliasedSkills"
        aliased_skills.mkdir(parents=True)
        os.symlink(aliased_skills, agents_dir / "skills", target_is_directory=True)

        with self.assertRaisesRegex(RuntimeError, "软链"):
            load_tool().reconcile(self.project_root)

        self.assertFalse((aliased_skills / "nova-project-router").exists())
        self.assertFalse((agents_dir / "nova-skills.lock.json").exists())

    def test_reconcile_rejects_project_internal_state_symlink_without_writing_alias(self):
        """lock 软链不能把受管状态重定向到项目内任意业务文件。"""
        self._write_consumer_manifest()
        agents_dir = self.project_root / ".agents"
        (agents_dir / "skills").mkdir(parents=True)
        aliased_state = self.project_root / "Assets" / "aliased-state.json"
        aliased_state.parent.mkdir(parents=True)
        os.symlink(aliased_state, agents_dir / "nova-skills.lock.json")

        with self.assertRaisesRegex(RuntimeError, "软链"):
            load_tool().reconcile(self.project_root)

        self.assertFalse(aliased_state.exists())
        self.assertFalse((agents_dir / "skills" / "nova-project-router").exists())

    def test_reconcile_recovers_after_interruption_between_new_skill_replacements(self):
        """首次全量 reconcile 中断后重试必须续传新增事务，不能遗留孤儿 Skill。"""
        self._write_consumer_manifest()
        tool = load_tool()
        target_root = self.project_root / ".agents" / "skills"
        first_target = target_root / "nova-project-router"
        second_target = target_root / "nova-project-ui-create-view"
        original_replace = tool.os.replace

        def interrupt_second_skill(source, destination):
            destination_path = Path(destination)
            if destination_path.name == "nova-project-ui-create-view":
                raise KeyboardInterrupt("模拟全量投影中断")
            return original_replace(source, destination)

        with mock.patch.object(tool.os, "replace", side_effect=interrupt_second_skill):
            with self.assertRaisesRegex(KeyboardInterrupt, "中断"):
                tool.reconcile(self.project_root)

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

        tool.reconcile(self.project_root)

        self.assertTrue(first_target.is_dir())
        self.assertTrue(second_target.is_dir())
        self.assertTrue(state_path.is_file())
        self.assertFalse(transaction_path.exists())
        self.assertFalse(any(tool.doctor(self.project_root).values()))

    def test_reconcile_rejects_state_changed_before_transaction_finalization(self):
        """事务完成前 lock 被外部改写时不得被最终状态静默覆盖。"""
        self._write_consumer_manifest()
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
                tool.reconcile(self.project_root)

        self.assertTrue(state_changed)
        self.assertEqual(foreign_state, json.loads(state_path.read_text(encoding="utf-8")))
        self.assertTrue(transaction_path.is_file())

    def test_reconcile_rejects_source_change_during_hidden_staging_without_visible_projection(self):
        """复制期间真源变化时不得登记彼此不一致的 source/target 哈希。"""
        self._write_consumer_manifest()
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
                tool.reconcile(self.project_root)

        agents_dir = self.project_root / ".agents"
        self.assertFalse((agents_dir / "skills" / "nova-project-router").exists())
        self.assertFalse((agents_dir / "nova-skills.lock.json").exists())
        self.assertFalse((agents_dir / "nova-skills.transaction.json").exists())

    def test_reconcile_rejects_an_active_atomic_lock_without_changing_its_owner(self):
        """另一进程持有内核锁时不得进入同一全量 reconcile 临界区。"""
        self._write_consumer_manifest()
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
            lock_path = (
                self.project_root
                / "Library"
                / "Nova"
                / "AgentSkills"
                / ".nova-skills-sync.lock"
            )
            owner_before = lock_path.read_text(encoding="utf-8")
            with self.assertRaisesRegex(RuntimeError, "正在进行"):
                load_tool().reconcile(self.project_root)
            self.assertEqual(owner_before, lock_path.read_text(encoding="utf-8"))
            self.assertFalse(
                (self.project_root / ".agents" / "skills" / "nova-project-router").exists()
            )
            self.assertFalse(
                (self.project_root / ".agents" / ".nova-skills-sync.lock").exists()
            )
            _, holder_stderr = holder.communicate(input="\n", timeout=10)
        self.assertEqual(0, holder.returncode, holder_stderr)
        self.assertTrue(holder.stdin.closed)
        self.assertTrue(holder.stdout.closed)
        self.assertTrue(holder.stderr.closed)

    def test_reconcile_reuses_a_released_kernel_lock_before_projecting(self):
        """已释放的隐藏锁文件可复用，且不会靠删除 lock inode 实现恢复。"""
        self._write_consumer_manifest()
        lock_path = (
            self.project_root
            / "Library"
            / "Nova"
            / "AgentSkills"
            / ".nova-skills-sync.lock"
        )
        lock_path.parent.mkdir(parents=True)
        lock_path.write_text(
            json.dumps({"schemaVersion": 1, "processId": -1, "token": "b" * 32}),
            encoding="utf-8",
        )

        result = load_tool().reconcile(self.project_root)

        self.assertEqual(
            ["nova-project-router", "nova-project-ui-create-view"], result["added"]
        )
        self.assertTrue(lock_path.is_file())
        owner = json.loads(lock_path.read_text(encoding="utf-8"))
        self.assertEqual(1, owner["schemaVersion"])
        self.assertRegex(owner["token"], r"^[0-9a-f]{32}$")
        self.assertFalse(
            (self.project_root / ".agents" / ".nova-skills-sync.lock").exists()
        )

    def test_reconcile_rejects_library_sync_lock_symlink_without_touching_target(self):
        """Library 共享锁若是软链，reconcile 不得跟随它覆盖任意文件。"""
        self._write_consumer_manifest()
        outside = self.root / "outside-lock-owner.json"
        outside.write_text("outside", encoding="utf-8")
        lock_path = (
            self.project_root
            / "Library"
            / "Nova"
            / "AgentSkills"
            / ".nova-skills-sync.lock"
        )
        lock_path.parent.mkdir(parents=True)
        os.symlink(outside, lock_path)

        with self.assertRaisesRegex(RuntimeError, "软链|安全同步锁"):
            load_tool().reconcile(self.project_root)

        self.assertEqual("outside", outside.read_text(encoding="utf-8"))
        self.assertFalse(
            (self.project_root / ".agents" / "skills" / "nova-project-router").exists()
        )

    def test_projection_sync_lock_rejects_same_process_reentry(self):
        """同一 Python 进程也必须互斥，避免 record lock 的进程级语义放行重入。"""
        tool = load_tool()

        with tool._projection_sync_lock(self.project_root):
            with self.assertRaisesRegex(RuntimeError, "正在进行"):
                with tool._projection_sync_lock(self.project_root):
                    self.fail("同进程重入不应进入临界区")
        with tool._projection_sync_lock(self.project_root):
            pass

    def test_canonical_reconcile_projects_real_skills_into_temporary_consumer(self):
        """真实 Framework Agents 必须完整投影当前 Catalog 的全部已发布 Skill。"""
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
        expected_skill_ids = [
            entry["id"]
            for entry in json.loads(
                (canonical_agents_root / "catalog.json").read_text(encoding="utf-8")
            )["skills"]
        ]

        dry_run = tool.reconcile(self.project_root, dry_run=True)
        self.assertEqual(expected_skill_ids, dry_run["added"])
        self.assertFalse((self.project_root / ".agents").exists())

        result = tool.reconcile(self.project_root)

        self.assertEqual(expected_skill_ids, result["added"])
        for skill_id in expected_skill_ids:
            self.assertTrue(
                (self.project_root / ".agents" / "skills" / skill_id / "SKILL.md").is_file()
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

    def test_reconcile_removes_unmodified_managed_skill_removed_from_catalog(self):
        """上游删除且受管副本未改时，reconcile 必须删除目标和 state 记录。"""
        self._write_consumer_manifest()
        tool = load_tool()
        tool.reconcile(self.project_root)
        catalog = self._read_catalog()
        catalog["skills"] = [
            entry for entry in catalog["skills"] if entry["id"] != "nova-project-ui-create-view"
        ]
        for group_ids in catalog.get("capabilityGroups", {}).values():
            if "nova-project-ui-create-view" in group_ids:
                group_ids.remove("nova-project-ui-create-view")
        self._write_catalog(catalog)
        shutil.rmtree(self.agents_root / "Skills" / "nova-project-ui-create-view")
        state_path = self.project_root / ".agents" / "nova-skills.lock.json"

        result = tool.reconcile(self.project_root)
        state = json.loads(state_path.read_text(encoding="utf-8"))
        self.assertEqual(["nova-project-ui-create-view"], result["removed"])
        self.assertNotIn("nova-project-ui-create-view", state["managed"])
        self.assertFalse(
            (self.project_root / ".agents" / "skills" / "nova-project-ui-create-view").exists()
        )

    def test_reconcile_state_uses_no_absolute_source_or_target_paths(self):
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

        load_tool().reconcile(self.project_root)

        state_path = self.project_root / ".agents" / "nova-skills.lock.json"
        state_text = state_path.read_text(encoding="utf-8")
        state = json.loads(state_text)
        managed_entry = state["managed"]["nova-project-router"]
        self.assertNotIn(str(self.project_root.resolve()), state_text)
        self.assertNotIn(str(canonical_agents_root.resolve()), state_text)
        self.assertEqual(1, state["schemaVersion"])
        self.assertIn("catalogHash", state)
        self.assertEqual({"sourceHash", "targetHash"}, set(managed_entry))

    def test_doctor_reports_catalog_member_missing_from_managed_state(self):
        """lock 遗漏 Catalog 中的 Skill 时，doctor 不能把投影误报为健康。"""
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
        tool.reconcile(self.project_root)
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
        tool.reconcile(self.project_root)
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
        tool.reconcile(self.project_root)

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

    def test_validate_rejects_unlisted_flat_skill_directory(self):
        """真源中遗漏 Catalog 的平铺目录不能在升级后悄悄失去全量发现。"""
        extra_skill = self.agents_root / "Skills" / "nova-project-unlisted"
        extra_skill.mkdir(parents=True)
        (extra_skill / "SKILL.md").write_text(
            "---\nname: nova-project-unlisted\ndescription: 漏登记测试。\n---\n",
            encoding="utf-8",
        )

        errors = load_tool().validate_agents_root(self.agents_root)

        self.assertTrue(any("未登记" in error for error in errors))

    def test_reconcile_updates_unmodified_managed_skill_after_framework_upgrade(self):
        """受管副本未改时，真源更新必须自动升级，而不是要求项目组手动处理。"""
        self._write_consumer_manifest()
        tool = load_tool()
        tool.reconcile(self.project_root)
        source_skill = self.agents_root / "Skills" / "nova-project-router" / "SKILL.md"
        source_skill.write_text(
            source_skill.read_text(encoding="utf-8") + "\n升级后的说明。\n",
            encoding="utf-8",
        )

        result = tool.reconcile(self.project_root)
        target_skill = self.project_root / ".agents" / "skills" / "nova-project-router" / "SKILL.md"

        self.assertEqual(["nova-project-router"], result["updated"])
        self.assertIn("升级后的说明。", target_skill.read_text(encoding="utf-8"))

    def test_reconcile_preserves_modified_managed_skill_and_continues_safe_changes(self):
        """项目改过的受管副本不覆盖，但其他新增 Skill 仍应完成同步。"""
        self._write_consumer_manifest()
        tool = load_tool()
        tool.reconcile(self.project_root)
        target_skill = self.project_root / ".agents" / "skills" / "nova-project-router" / "SKILL.md"
        target_skill.write_text("project override", encoding="utf-8")
        source_skill = self.agents_root / "Skills" / "nova-project-router" / "SKILL.md"
        source_skill.write_text(
            source_skill.read_text(encoding="utf-8") + "\n上游新版本。\n",
            encoding="utf-8",
        )
        self._append_skill("nova-project-added-after-upgrade")

        result = tool.reconcile(self.project_root)

        self.assertEqual("project override", target_skill.read_text(encoding="utf-8"))
        self.assertEqual(["nova-project-added-after-upgrade"], result["added"])
        self.assertIn(
            {"id": "nova-project-router", "reason": "modified-managed"},
            result["conflicts"],
        )

    def test_reconcile_preserves_modified_managed_skill_when_source_removes_it(self):
        """上游删除不能删除项目已修改的受管副本，也不能遗忘其受管记录。"""
        self._write_consumer_manifest()
        tool = load_tool()
        tool.reconcile(self.project_root)
        target_dir = self.project_root / ".agents" / "skills" / "nova-project-router"
        (target_dir / "SKILL.md").write_text("project override", encoding="utf-8")
        catalog = self._read_catalog()
        catalog["skills"] = [
            entry for entry in catalog["skills"] if entry["id"] != "nova-project-router"
        ]
        for group_ids in catalog.get("capabilityGroups", {}).values():
            if "nova-project-router" in group_ids:
                group_ids.remove("nova-project-router")
        self._write_catalog(catalog)
        shutil.rmtree(self.agents_root / "Skills" / "nova-project-router")

        result = tool.reconcile(self.project_root)
        state = json.loads(
            (self.project_root / ".agents" / "nova-skills.lock.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertTrue(target_dir.is_dir())
        self.assertIn("nova-project-router", state["managed"])
        self.assertIn(
            {"id": "nova-project-router", "reason": "modified-managed"},
            result["conflicts"],
        )

    def test_reconcile_removes_missing_managed_skill_alongside_other_actions(self):
        """缺失的旧受管副本不能让同轮新增 Skill 留下不可恢复的 journal。"""
        self._write_consumer_manifest()
        tool = load_tool()
        tool.reconcile(self.project_root)

        removed_id = "nova-project-ui-create-view"
        shutil.rmtree(self.project_root / ".agents" / "skills" / removed_id)
        shutil.rmtree(self.agents_root / "Skills" / removed_id)
        catalog = self._read_catalog()
        catalog["skills"] = [
            entry for entry in catalog["skills"] if entry["id"] != removed_id
        ]
        for group_ids in catalog.get("capabilityGroups", {}).values():
            if removed_id in group_ids:
                group_ids.remove(removed_id)
        self._write_catalog(catalog)
        added_id = "nova-project-added-after-missing-removal"
        self._append_skill(added_id)

        result = tool.reconcile(self.project_root)
        state = json.loads(
            (self.project_root / ".agents" / "nova-skills.lock.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual([added_id], result["added"])
        self.assertEqual([removed_id], result["removed"])
        self.assertTrue((self.project_root / ".agents" / "skills" / added_id).is_dir())
        self.assertNotIn(removed_id, state["managed"])
        self.assertFalse(
            (self.project_root / ".agents" / "nova-skills.transaction.json").exists()
        )

    def test_reconcile_reports_missing_managed_catalog_skill_and_continues_safe_add(self):
        """Catalog 仍声明的受管目录缺失时应保留所有权并继续其它安全 action。"""
        self._write_consumer_manifest()
        tool = load_tool()
        initial = tool.reconcile(self.project_root)
        self.assertEqual("success", initial["status"])
        missing_id = "nova-project-router"
        shutil.rmtree(self.project_root / ".agents" / "skills" / missing_id)
        added_id = "nova-project-added-beside-missing"
        self._append_skill(added_id)

        result = tool.reconcile(self.project_root)
        state = json.loads(
            (self.project_root / ".agents" / "nova-skills.lock.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual("partial", result["status"])
        self.assertEqual([added_id], result["added"])
        self.assertIn(
            {"id": missing_id, "reason": "missing-managed"},
            result["conflicts"],
        )
        self.assertIn(missing_id, state["managed"])
        self.assertFalse((self.project_root / ".agents" / "skills" / missing_id).exists())
        self.assertTrue((self.project_root / ".agents" / "skills" / added_id).is_dir())

    def test_reconcile_rejects_transaction_with_unplanned_final_managed_entry(self):
        """损坏 journal 不得借 finalState 认领未出现在 pending 中的用户目录。"""
        self._write_consumer_manifest()
        agents_dir = self.project_root / ".agents"
        agents_dir.mkdir(parents=True)
        zero_hash = "0" * 64
        transaction_path = agents_dir / "nova-skills.transaction.json"
        transaction_path.write_text(
            json.dumps(
                {
                    "schemaVersion": 1,
                    "transactionId": "a" * 32,
                    "previousState": None,
                    "finalState": {
                        "schemaVersion": 1,
                        "package": "com.solotopia.nova.framework",
                        "packageVersion": "0.6.9",
                        "catalogHash": zero_hash,
                        "managed": {
                            "nova-project-router": {
                                "sourceHash": zero_hash,
                                "targetHash": zero_hash,
                            }
                        },
                    },
                    "pending": [
                        {
                            "action": "add",
                            "id": "nova-project-ui-create-view",
                            "sourceHash": zero_hash,
                            "targetHash": zero_hash,
                        }
                    ],
                }
            ),
            encoding="utf-8",
        )

        with self.assertRaisesRegex(RuntimeError, "finalState.*不一致"):
            load_tool().reconcile(self.project_root)

        self.assertTrue(transaction_path.is_file())
        self.assertFalse((agents_dir / "skills" / "nova-project-router").exists())

    def test_transaction_rejects_add_for_skill_owned_by_previous_state(self):
        """journal 的 add 不得覆盖 previousState 已拥有的 Skill。"""
        tool = load_tool()
        agents_dir = self.project_root / ".agents"
        agents_dir.mkdir(parents=True)
        old_hash = "0" * 64
        new_hash = "1" * 64
        previous_state = {
            "schemaVersion": 1,
            "package": "com.solotopia.nova.framework",
            "packageVersion": "0.6.9",
            "catalogHash": "2" * 64,
            "managed": {
                "nova-project-router": {
                    "sourceHash": old_hash,
                    "targetHash": old_hash,
                }
            },
        }
        final_state = json.loads(json.dumps(previous_state))
        final_state["managed"]["nova-project-router"] = {
            "sourceHash": new_hash,
            "targetHash": new_hash,
        }
        transaction_path = agents_dir / "nova-skills.transaction.json"
        transaction_path.write_text(
            json.dumps(
                {
                    "schemaVersion": 1,
                    "transactionId": "c" * 32,
                    "previousState": previous_state,
                    "finalState": final_state,
                    "pending": [
                        {
                            "action": "add",
                            "id": "nova-project-router",
                            "sourceHash": new_hash,
                            "targetHash": new_hash,
                        }
                    ],
                }
            ),
            encoding="utf-8",
        )

        with self.assertRaisesRegex(RuntimeError, "add.*previousState|已经受管"):
            tool._read_transaction(transaction_path)

    def test_update_recovery_blocks_when_target_and_backup_are_both_missing(self):
        """update 丢失旧目标与 backup 时不能把 staged 新版当成安全恢复。"""
        tool = load_tool()
        agents_dir = self.project_root / ".agents"
        transaction_id = "d" * 32
        staged_skill = (
            agents_dir
            / ".nova-skills-staging"
            / transaction_id
            / "new"
            / "nova-project-router"
        )
        staged_skill.parent.mkdir(parents=True)
        shutil.copytree(
            self.agents_root / "Skills" / "nova-project-router",
            staged_skill,
        )
        new_hash = tool._tree_hash(staged_skill)
        old_hash = "0" * 64
        previous_state = {
            "schemaVersion": 1,
            "package": "com.solotopia.nova.framework",
            "packageVersion": "0.6.9",
            "catalogHash": "2" * 64,
            "managed": {
                "nova-project-router": {
                    "sourceHash": old_hash,
                    "targetHash": old_hash,
                }
            },
        }
        final_state = json.loads(json.dumps(previous_state))
        final_state["managed"]["nova-project-router"] = {
            "sourceHash": new_hash,
            "targetHash": new_hash,
        }
        state_path = agents_dir / "nova-skills.lock.json"
        state_path.write_text(json.dumps(previous_state), encoding="utf-8")
        transaction_path = agents_dir / "nova-skills.transaction.json"
        transaction_path.write_text(
            json.dumps(
                {
                    "schemaVersion": 1,
                    "transactionId": transaction_id,
                    "previousState": previous_state,
                    "finalState": final_state,
                    "pending": [
                        {
                            "action": "update",
                            "id": "nova-project-router",
                            "sourceHash": new_hash,
                            "targetHash": new_hash,
                            "previousTargetHash": old_hash,
                        }
                    ],
                }
            ),
            encoding="utf-8",
        )

        with self.assertRaisesRegex(RuntimeError, "目标.*备份.*缺失|无法安全恢复"):
            tool._resume_transaction(
                self.project_root,
                agents_dir / "skills",
                state_path,
            )

        self.assertFalse((agents_dir / "skills" / "nova-project-router").exists())
        self.assertTrue(staged_skill.is_dir())
        self.assertTrue(transaction_path.is_file())

    def test_reconcile_recovers_interrupted_managed_skill_update(self):
        """更新已受管副本时中断，重试必须从 backup/staging 恢复到新版本。"""
        self._write_consumer_manifest()
        tool = load_tool()
        tool.reconcile(self.project_root)
        source_skill = self.agents_root / "Skills" / "nova-project-router" / "SKILL.md"
        source_skill.write_text(
            source_skill.read_text(encoding="utf-8") + "\n更新恢复测试。\n",
            encoding="utf-8",
        )
        target_dir = self.project_root / ".agents" / "skills" / "nova-project-router"
        original_replace = tool.os.replace
        replacement_count = 0

        def interrupt_staged_update(source, destination):
            """旧目录移入 backup 后，在新目录进入目标位置前中断。"""
            nonlocal replacement_count
            replacement_count += 1
            if replacement_count == 3:
                raise KeyboardInterrupt("模拟更新中断")
            return original_replace(source, destination)

        with mock.patch.object(tool.os, "replace", side_effect=interrupt_staged_update):
            with self.assertRaisesRegex(KeyboardInterrupt, "更新中断"):
                tool.reconcile(self.project_root)

        self.assertFalse(target_dir.exists())
        self.assertTrue((self.project_root / ".agents" / "nova-skills.transaction.json").is_file())

        result = tool.reconcile(self.project_root)

        self.assertTrue(target_dir.is_dir())
        self.assertIn("更新恢复测试。", (target_dir / "SKILL.md").read_text(encoding="utf-8"))
        self.assertFalse((self.project_root / ".agents" / "nova-skills.transaction.json").exists())
        self.assertEqual([], result["updated"])

    def test_reconcile_recovers_interrupted_managed_skill_removal(self):
        """删除未改受管副本时中断，重试必须完成删除和最终 state 更新。"""
        self._write_consumer_manifest()
        tool = load_tool()
        tool.reconcile(self.project_root)
        catalog = self._read_catalog()
        catalog["skills"] = [
            entry for entry in catalog["skills"] if entry["id"] != "nova-project-ui-create-view"
        ]
        for group_ids in catalog.get("capabilityGroups", {}).values():
            if "nova-project-ui-create-view" in group_ids:
                group_ids.remove("nova-project-ui-create-view")
        self._write_catalog(catalog)
        shutil.rmtree(self.agents_root / "Skills" / "nova-project-ui-create-view")
        target_dir = self.project_root / ".agents" / "skills" / "nova-project-ui-create-view"
        original_replace = tool.os.replace

        def move_then_interrupt_removal(source, destination):
            """目标移入受控 backup 后模拟崩溃，保留可恢复现场。"""
            result = original_replace(source, destination)
            if Path(destination).parent.name == "backup":
                raise KeyboardInterrupt("模拟删除中断")
            return result

        with mock.patch.object(tool.os, "replace", side_effect=move_then_interrupt_removal):
            with self.assertRaisesRegex(KeyboardInterrupt, "删除中断"):
                tool.reconcile(self.project_root)

        self.assertFalse(target_dir.exists())
        self.assertTrue((self.project_root / ".agents" / "nova-skills.transaction.json").is_file())

        result = tool.reconcile(self.project_root)
        state = json.loads(
            (self.project_root / ".agents" / "nova-skills.lock.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertFalse(target_dir.exists())
        self.assertNotIn("nova-project-ui-create-view", state["managed"])
        self.assertFalse((self.project_root / ".agents" / "nova-skills.transaction.json").exists())
        self.assertEqual([], result["removed"])

    def test_cli_reconcile_returns_one_for_partial_result(self):
        """CLI reconcile 有冲突时以 1 区分 partial，异常仍保留给退出码 2。"""
        user_skill = self.project_root / ".agents" / "skills" / "nova-project-router"
        user_skill.mkdir(parents=True)
        (user_skill / "SKILL.md").write_text("user owned", encoding="utf-8")
        tool = load_tool()

        with mock.patch("builtins.print"):
            exit_code = tool.main(
                [
                    "reconcile",
                    "--project-root",
                    str(self.project_root),
                    "--agents-root",
                    str(self.agents_root),
                ]
            )

        self.assertEqual(1, exit_code)


if __name__ == "__main__":
    unittest.main()
