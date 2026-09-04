---
name: nova-project-build-bundles
description: Use when Nova 项目组需要为已配置的 YooAsset Package 按明确平台、版本、缓存与首包拷贝模式生成本地 Bundle 产物，并核验实际构建结果时使用。
---

# Nova 构建本地 Bundle

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`、`Docs/Onboarding/RESOURCE_WORKFLOW.md` 与 `Docs/Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md`。仅在项目明确选择现有 Pipify Batch 时读取 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`；不要为直接构建加载全部 Pipify Step 说明。

该 Operation 只生成本地 YooAsset Bundle 及其构建报告，不部署 CDN、不清理远端、不上传商店，不执行 Git 或 UPM 发布。

## Input

冻结项目根、目标平台、PackageName、BuildVersion、实际输出根、执行入口，以及缓存、依赖数据库、压缩、文件名、加密器和 BundledCopy 选项。实际输出根必须解析为当前适配器固定的项目 `/Bundles`；不要虚构自定义输出参数。目标平台、输出根、Package、版本策略或构建/拷贝模式不明确时返回 `blocked`。

检查目标 Package 与 Collector 配置已经存在，所需导出物由项目提前准备。不要因为要构建 Bundle 就自动执行 Config、Table、UI 或全量 Excel 导出；缺少输入时报告具体前置缺口。

## 已有 Action Adapter

Framework 已注册并开放 `nova.project.bundle.build-asset` 与 `nova.project.bundle.build-raw-file`。先用 `describe` 读取目标 Action 的当前 Schema，再按冻结的 Package、Target、版本、缓存和拷贝参数执行 `plan -> execute -> verify`；删除或重建输出目录仍必须使用当前一次性 PlanId 确认。不得退化到 `execute_code`、反射或临时 C#。

## Artifact → Evidence

Artifact 是 `BuildResult.OutputPackageDirectory` 指向的本地 Package 目录及本次 YooAsset 报告。只有 `BuildResult.Success` 为真、输出目录存在，并能对应冻结的 Target、PackageName 与 BuildVersion 时才返回 `success`；编译通过或目录里存在旧文件都不算 Bundle 构建证据。

报告实际 Adapter、冻结参数、输出目录、构建结果和未验证项。构建失败、产物无法归属本次输入或恢复状态不确定时返回 `partial` 或 `blocked`，不得把旧 Bundle、远端可用性或 Player 可运行性推断为成功。
