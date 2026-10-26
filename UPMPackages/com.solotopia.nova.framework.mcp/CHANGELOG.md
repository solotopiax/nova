# Changelog

本文件记录 `com.solotopia.nova.framework.mcp` 的版本变更。格式遵循 [Keep a Changelog](https://keepachangelog.com/)，版本号遵循语义化版本。

## [Unreleased]

## [0.1.7] - 2026-10-26

### Added

- 安装调度可自动补齐 MCP 本机运行时、复用或启动 Coplay 服务并保存当前项目的 Codex 绑定；实际验证 Editor 连接与 Nova 工具，保留其他项目服务、端口和用户配置。

## [0.1.6] - 2026-10-23

### Changed

- 对齐依赖版本：com.coplaydev.unity-mcp 10.2.0 → 10.3.0。

- 兼容声明调整为 Unity 6000.4.2f1，配合 Nova Framework 的 Unity 6000.4.x 支持范围。

## [0.1.5] - 2026-10-23

### Added

- 通过单一 nova_project_action 显式开放 config.patch-coordinate、ui.export、Config 面板、资源密钥、Pipify、对象字段、平台宏、FileFragment 及 Tracks 汇总等 Action，保持 Gateway 白名单、Framework Registry 和文档一致。

- 新增有界 verify_mode=start/status/forget 收件协议，核验仅执行一次并保留完整脱敏响应，兼容同步协议、领域门及旧 partial。

### Changed

- 开放商品目录一次提交 Action，要求最低 Framework API 0.6.40 配对；不开放任意类型、代码或未注册 Action。

### Fixed

- describe 保留凭据字段请求类型结构，移除示例和默认载荷，实际计划及结果值继续脱敏，避免遮蔽 Schema。

- 慢计划可先登记，再按同一请求编号断线取回完整脱敏响应；重复请求不重跑，过期或域重载明确失效，兼容同步计划及执行门。

- 补齐 IAP 商品目录上传文档，按当前 ExposurePolicy 维护开放 Action。

### Breaking

- 最低支持 Unity 6000.6.7f1，与 Nova Framework 基线一致。

## [0.1.4] - 2026-10-12

### Breaking

- 移除仅供已删除能力总览视图使用的 NovaProjectActionExposureSnapshot 与 NovaProjectActionGateway.GetExposureSnapshot；保留 MCP describe、plan、execute、verify 和显式开放策略。

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
