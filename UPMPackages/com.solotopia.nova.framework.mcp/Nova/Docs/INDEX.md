# Nova Framework MCP Adapter

`com.solotopia.nova.framework.mcp` 是 Nova Framework 显性依赖的 Editor 传输桥。它不实现领域逻辑，只把通过安全策略审核的 Action 以固定 Tool 协议开放给项目组 Agent。

## 入口

- MCP Tool：`nova_project_action`
- 中立程序集：`NovaFramework.Mcp.Editor`
- 默认 Adapter 程序集：`NovaFramework.Mcp.UnityMcp.Editor`
- Core Action Registry：`NovaFramework.Editor.EditorUtil.AgentActions.Registry`
- 运行条件：Unity Editor 已连接当前默认的 Unity MCP Provider，且 Editor 处于可执行工具的稳定状态

## 安装前置

当前完整安装入口是 NovaSpark。它先向消费工程 `Packages/manifest.json` 写入 OpenUPM registry 与精确 scope `com.coplaydev.unity-mcp`，再安装 Framework；UPM 依赖链随后自动解析 `Framework -> 本包 -> com.coplaydev.unity-mcp@10.1.2`。Unity MCP 不需要成为项目顶层 Git 依赖；绕过 NovaSpark 直接安装 Framework 时，工程必须已经配置可解析该包的 registry。Nova 开发工程可另外安装其他 Provider 用于开发态验证，不改变对外包的默认选型。

安装完成不等于外部 Agent 已经连接：本包不自动启动 Server、不占端口、不修改外部 Agent 配置。首次连接或升级后，已有会话若未刷新 Tool 列表，需要重连 MCP 或开启新会话。

## Tool 协议

| operation | 必填输入 | 作用 | 领域写入 |
|---|---|---|---|
| `describe` | 可选 `action_id` | 返回当前 MCP 开放的 Action Descriptor 与 Request Schema | 否 |
| `plan` | `action_id`、`request` | 严格校验并冻结本次计划、写入集与证据要求 | 否；只写 `Library` 审计元数据 |
| `execute` | `action_id`、`plan_id` | 原子消费一次性 Plan；确认型 Action 还需绑定令牌 | 取决于 Action |
| `verify` | `action_id`、`receipt` | 只读核验当前领域状态，不重放 Execute | 否；可更新 `Library` 审计元数据 |

`confirmation_token=plan_id` 只证明调用绑定到同一份 Plan，不证明 MCP 已经验证真人审批。任何需要可信审批而当前通道无法证明的 Action 都必须保持关闭。

## 当前开放边界

Adapter 目前显式允许以下 8 个 Action：

- `nova.project.upm.manage-latest`
- `nova.project.config.validate-coordinate`
- `nova.project.config.inspect-plugin-types`
- `nova.project.config.ensure-plugin-instances`
- `nova.project.config.inspect-bundle-collector`
- `nova.project.config.export-runtime`
- `nova.project.hotfix.refresh-game-dlls`
- `nova.project.build.inspect-readiness`

Nova Project Skills 与 MCP Tool 不是一张清单：29 个 `nova-project-*` 由 Agent 从项目 `.agents/skills/` 发现；当前默认 Adapter 只注册一个 `nova_project_action`，然后由 `describe` 返回上述当前开放 Action。

该清单用于说明当前包版本，不替代 live Registry。运行时必须先调用 `describe`；未注册、Registry 存在 issue、未进入 ExposurePolicy，或带 `Delivery`、`Destructive`、`ExternalWrite`、`Credential` 副作用的 Action 都会 fail-closed。

## 恢复与证据

- ready Plan 的可执行状态只保存在内存中，容量最多 256 项，TTL 为 30 分钟，并且只能消费一次。
- `Library/Nova/AgentActions/Operations/` 只保存最小恢复记录，不保存可执行 HandlerState。
- domain reload 后只允许使用 `recovery_token` 重新 Verify，绝不恢复或重放 Execute。
- `success` 必须覆盖 Action Descriptor 声明的全部最低证据，否则结果降级为 `partial`。

## 排障顺序

1. 确认当前 MCP Provider 连接的 `projectRoot` 是当前消费工程。
2. 确认 `ready_for_tools=true`，Unity 不在编译、更新资产或切换 Play Mode。
3. 调用 `describe` 确认目标 Action 当前确实开放，并按返回的 Request Schema 组装请求。
4. `not_applicable` 表示 Action 未注册或未向 MCP 开放，不得退化为任意 C# 执行。
5. domain reload、Plan 过期、Registry generation 改变或 Editor 状态漂移后重新 Plan，不复用旧 `plan_id`。

## 扩展约束

新增 MCP Action 暴露必须同时完成：Core Registry 注册、专用 DTO 与证据契约、Adapter `ExposurePolicy` 审核、包内文档更新及定向安全测试。仅在 Core 注册 Handler 不代表 MCP 自动开放。
