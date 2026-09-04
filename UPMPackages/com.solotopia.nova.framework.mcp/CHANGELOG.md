# Changelog

本文件记录 `com.solotopia.nova.framework.mcp` 的版本变更。格式遵循 [Keep a Changelog](https://keepachangelog.com/)，版本号遵循语义化版本。

## [Unreleased]

## [0.1.3] - 2026-09-04

### Changed

- 默认 Unity MCP Provider 依赖从 `10.1.2` 升级到 `10.2.0`。
- 所有已注册 `nova.project.*` Action 默认进入显式 MCP 白名单；Registry 与白名单不完全一致时 Gateway 整体 fail-closed。
- 新增 `nova.project.pipify.run-batch` 传输支持，异步 Batch 通过 recovery token 轮询结果。

## [0.1.2] - 2026-08-26

### Added

- 新增 Project Action 暴露快照接口，供 Framework 能力总览读取 MCP 策略与实际开放 Action。

## [0.1.1] - 2026-08-21

### Added

- 向 MCP 开放表、网络、声音、振动与本地化五类受控导出 Action。
- 更新包内 Action 索引与使用说明，使新增导出能力可从消费项目发现。

## [0.1.0] - 2026-08-20

### Added

- 新增受限 `nova_project_action` MCP Tool，提供 Nova Project Action 的 `describe`、`plan`、`execute` 与 `verify` 传输桥。
- 仅允许显式开放的稳定 Action ID，不提供任意 C#、类型名、方法名或反射执行能力。
- Execute 在任何领域写入前绑定 `action_id + plan_id`；跨 domain reload 只使用 Core recovery token 进行 Verify。
- 当前显式开放 UPM、Config 与业务热更 DLL 刷新 7 个 Action，高风险 Build/Delivery 保持关闭。
- 补齐 `Nova/`、`Core/`、包内 Docs 与 UPM 三件套的自解释结构，并明确当前默认 Unity MCP 依赖。
- `coreVersion` 从第三方 Provider 版本解耦，使用 Nova MCP 自身的 `0.0.1` 契约基线；Provider 版本只由 `dependencies` 表达。
- 程序集名称与根命名空间统一为 `NovaFramework.Mcp.Editor`。
