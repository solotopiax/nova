# Changelog

本文件记录 `com.solotopia.nova.framework.mcp` 的版本变更。格式遵循 [Keep a Changelog](https://keepachangelog.com/)，版本号遵循语义化版本。

## [Unreleased]

### Added

- 开放只读 `nova.project.build.inspect-readiness`，用于在构建前检查 Target、场景、Config、YooAsset Package 与 HybridCLR 前置；不执行构建或修复。
- 新增中立 Project Action Provider SPI 与 Gateway，使 Framework 可以显性依赖本包，同时避免传输 Adapter 反向编译依赖 Framework。
- 新增独立 `NovaFramework.Mcp.UnityMcp.Editor` 程序集，作为当前默认 Unity MCP 薄适配层。

### Changed

- UPM 安装链调整为 `Framework -> Nova MCP -> com.coplaydev.unity-mcp`；正式发布前必须先闭环默认 Provider 的可解析来源。
- 默认 Provider 改由 NovaSpark 配置的 OpenUPM scoped registry 解析，不再要求消费工程写入 Unity MCP 顶层 Git 依赖。
- Descriptor 增加 `requires_edit_mode`，用于在传输层展示 Action 的 Edit Mode 约束。

## [0.1.0] - 2026-08-20

### Added

- 新增受限 `nova_project_action` MCP Tool，提供 Nova Project Action 的 `describe`、`plan`、`execute` 与 `verify` 传输桥。
- 仅允许显式开放的稳定 Action ID，不提供任意 C#、类型名、方法名或反射执行能力。
- Execute 在任何领域写入前绑定 `action_id + plan_id`；跨 domain reload 只使用 Core recovery token 进行 Verify。
- 当前显式开放 UPM、Config 与业务热更 DLL 刷新 7 个 Action，高风险 Build/Delivery 保持关闭。
- 补齐 `Nova/`、`Core/`、包内 Docs 与 UPM 三件套的自解释结构，并明确当前默认 Unity MCP 依赖。
- `coreVersion` 从第三方 Provider 版本解耦，使用 Nova MCP 自身的 `0.0.1` 契约基线；Provider 版本只由 `dependencies` 表达。
- 程序集名称与根命名空间统一为 `NovaFramework.Mcp.Editor`。
