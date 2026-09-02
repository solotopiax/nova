# EditorUtil.AgentActions

`EditorUtil.AgentActions` 是 Nova Project Skills 的受控 C# 执行内核。它只注册输入可类型化、写入集可说明、成功可自动验证的稳定项目组操作，不接受任意 C# 类型名、方法名、反射参数或代码字符串。

总体规划与原候选评审见 [NovaProjectActions-Overall-Design.md](NovaProjectActions-Overall-Design.md)。

## 当前能力

Registry 当前注册 19 个 Action：

| Action ID | Operation | RequiredEvidence | MCP |
|---|---|---|---|
| `nova.project.upm.manage-latest` | Package | PackageResolution | 开放 |
| `nova.project.upm.uninstall-direct` | Package | PackageResolution | 未开放：Destructive |
| `nova.project.config.validate-coordinate` | Inspect | Static | 开放 |
| `nova.project.config.inspect-plugin-types` | Inspect | Static | 开放 |
| `nova.project.config.ensure-plugin-instances` | Ensure | Static | 开放 |
| `nova.project.config.inspect-bundle-collector` | Inspect | Static | 开放 |
| `nova.project.config.export-runtime` | Generate | Static + Artifact | 开放 |
| `nova.project.hotfix.refresh-game-dlls` | Generate | Compile + Artifact | 开放 |
| `nova.project.build.inspect-readiness` | Inspect | Static | 开放 |
| `nova.project.table.export` | Generate | Static + Artifact | 开放 |
| `nova.project.network.export` | Generate | Static + Artifact | 开放 |
| `nova.project.sound.export` | Generate | Static + Artifact | 开放 |
| `nova.project.vibration.export` | Generate | Static + Artifact | 开放 |
| `nova.project.localization.export` | Generate | Static + Artifact | 开放 |
| `nova.project.android.resolve-dependencies` | Generate | PackageResolution + Artifact | 未开放：Destructive |
| `nova.project.hotfix.generate-artifacts` | Generate | Compile + Artifact | 未开放：Destructive |
| `nova.project.bundle.build-asset` | Build | Artifact | 未开放：Destructive |
| `nova.project.bundle.build-raw-file` | Build | Artifact | 未开放：Destructive |
| `nova.project.player.build` | Build | Artifact | 未开放：Destructive + ExternalWrite |

Action 的 Request Schema、effects、locks、contractMajor、idempotency 和 confirmation 以运行中的 Registry Descriptor 为准，不维护第二份静态 Action Catalog。

`nova.project.config.export-runtime` 的请求仍显式携带 `platform / channel / developMode`，但 `platform` 必须等于 Plan 时 Unity 当前 Active BuildTarget 映射的 Nova `PlatformType`。未映射为 Android / iOS / WebGL、请求不一致，或 Execute 前 Active BuildTarget 漂移，都会以 `blocked` 收口并要求先切换 BuildTarget 后重新 Plan；该限制不改变底层 `EditorUtil.Config.Exporter.Export` 的显式平台 API。

`channel` 接受 `ChannelType` 的全部声明名称，包含 `None`；`None` 表示无特定运营渠道，不会被 Config 校验、导出、插件补齐或构建预检拦截。`PlatformType.None` 仍被拒绝。

## 代码组织

```text
EditorUtil.AgentActions/
├── Definitions/
│   ├── AgentActionContracts.cs
│   └── AgentActionModels.cs
├── Core/
│   ├── AgentActionHandler.cs
│   ├── AgentActionLockManager.cs
│   ├── AgentActionOperationStore.cs
│   ├── AgentActionPlanStore.cs
│   └── AgentActionRuntime.cs
├── Handlers/
│   ├── Build/
│   ├── Ensure/
│   ├── Generate/
│   ├── Inspect/
│   └── Package/
├── EditorUtil.AgentActions.Registry.cs
└── EditorUtil.AgentActions.Dispatcher.cs
```

Registry 只扫描 `NovaFramework.Editor` 程序集中的 `IAgentActionHandler`；消费项目程序集不能注册可执行 Action。每个 Handler 只负责 DTO、输入冻结、领域 API 调用、Receipt 和证据，不复制领域算法，也不依赖 EditorWindow。

## Descriptor 契约

每个 Handler 通过 `AgentActionAttribute` 声明：

- 稳定 ID 与 Domain；
- `Inspect / Ensure / Generate / Build / Package / RuntimeProbe / Delivery`；
- Workspace、Unity、External、BuildArtifact、Destructive、Credential 副作用；
- `ReadOnly / EnsureState / ReplaceGeneratedOutput / CreateIfAbsent / SubmitOnce`；
- Static、Compile、PackageResolution、Artifact、Play、Device、External 证据；
- confirmation、stable-editor、reload semantics、locks 与 contractMajor。

写入、Build 与 Package Action 还应显式声明 `RequiresEditMode`。Dispatcher 会在 Plan 入口、异步 Plan 保存前和 Execute 写入前分别检查 Play Mode 状态；该门与 `RequiresStableEditor` 独立，避免未来 `RuntimeProbe` 被错误禁止在 Play Mode 运行。

Registry 在不可变快照中发布描述和 Handler；每次 Rebuild 增加 generation。重复 ID、非法 ID、无证据、写入却无锁/确认、ReadOnly 却声明写入、无严格 Schema 的 Handler 不进入可执行 Registry。Registry 存在任何 issue 时 MCP 整体 fail-closed。

## 请求 Schema

每个 Action 使用独立 `[Serializable]` DTO。Core 从 DTO 生成 `RequestSchemaJson`，并由同一 `AgentActionRequestContract<T>` 执行解析：

- 最大 64 KiB、最大深度 64；
- 根必须是 JSON object；
- 拒绝重复字段和未知字段；
- 拒绝缺失必填字段和 token 类型错误；
- 禁止 Newtonsoft `TypeNameHandling`；
- Handler 再执行领域语义校验。

MCP 只做 transport 上限与 Descriptor 顶层字段预检，不能复制一套 Action 专用 Schema。

## 公共入口

```csharp
AgentActionDescriptor Describe(string actionId)
IReadOnlyList<AgentActionDescriptor> GetAll()

Task<AgentActionPlan> PlanAsync(
    string actionId,
    string requestJson,
    CancellationToken cancellationToken)

Task<AgentActionResult> ExecuteAsync(
    string planId,
    string confirmationToken,
    CancellationToken cancellationToken)

Task<AgentActionResult> ExecuteAsync(
    string expectedActionId,
    string planId,
    string confirmationToken,
    CancellationToken cancellationToken)

Task<AgentActionResult> VerifyAsync(
    string actionId,
    string receiptOrRecoveryToken,
    CancellationToken cancellationToken)

Task<AgentActionResult> RunAsync(
    string actionId,
    string requestJson,
    CancellationToken cancellationToken)
```

MCP 必须使用含 `expectedActionId` 的 Execute 重载。Core 原子消费 Plan 后、任何领域写入前核对 Action ID，避免从其他入口产生的未开放计划绕过 MCP allowlist。三参数 Execute 保留给同程序集/受信直接调用；`RunAsync` 只允许无需确认的 Action，MCP 不暴露它。

## PlanStore

- 只保存 ready 计划的可执行 HandlerState；
- 默认容量 256；
- 默认 TTL 30 分钟；
- 添加、过期清理、移除和 TryTake 在锁内完成；
- TryTake 原子单次消费；确认失败或执行失败也不能复用；
- 超限不驱逐仍有效计划；
- domain reload 后内存计划全部失效；
- 可释放 HandlerState 在过期、移除或执行结束时 Dispose。

## OperationStore 与恢复

ready Plan 会先在以下目录建立最小持久化 Operation：

```text
Library/Nova/AgentActions/Operations/
```

记录包含 operationId、Action/contract、Registry generation、请求 SHA-256、write set、状态与领域 Receipt；不保存原始请求。文件以同目录临时文件原子替换，单条最大 256 KiB，路径只接受 32 位十六进制 operationId。

`recovery_token` 只能加载当前 Action/contract 的 Receipt 并进入 Verify。它不能恢复 HandlerState、不能判断 Execute 应否继续、不能重放写操作。若 Execute 在 domain reload 前已提交但未回传，调用方也只能等待稳定后 Verify。

Plan 和 Verify 会写/更新这条 `Library` 基础设施记录；“Plan/Verify 只读”特指不写领域状态、项目资产或外部目标。

## 调度顺序

### Plan

1. 主线程与 stable-editor 门；
2. Registry lookup 与严格请求解析；
3. 取得 Descriptor locks；
4. Handler 只读冻结领域计划；
5. 核对 Registry generation；
6. 写入 PlanStore；
7. 建立 OperationStore；失败则移除可执行计划并 fail-closed。

### Execute

1. 原子 TryTake；
2. 核对 expectedActionId、Registry generation、confirmation、Editor 状态；
3. 取得 locks；
4. 持久化 `executing`；
5. 调用一次 Handler Execute；
6. 统一包裹 `actionId + contractMajor + payload` Receipt；
7. 按 RequiredEvidence 归一化状态并持久化结果；
8. 任意异常只返回 `partial`，不重放。

### Verify

1. 接受 Receipt envelope 或 recovery token；
2. 核对 Action 与 contractMajor；
3. stable-editor 与 locks；
4. Handler 只读验证领域状态；
5. 证据不足时把 success 降为 partial；
6. 可更新 Operation 最近验证状态。

## MCP 调用桥

Framework 的必需 Editor 配套包 `com.solotopia.nova.framework.mcp` 当前默认提供 Unity MCP 自定义 Tool：

```text
nova_project_action
```

调用：

1. `describe`：列出当前开放 Action 或返回一个 Descriptor/Request Schema；
2. `plan`：传 `action_id + request`；
3. `execute`：必须传 `action_id + plan_id`；确认型 Action 再传 `confirmation_token=plan_id`；
4. `verify`：参数名仍为 `receipt`，其值优先使用 Core 返回的 `recovery_token`；不要把内部领域 Receipt 当作新的执行授权。

当前显式 ExposurePolicy：

```text
nova.project.upm.manage-latest
nova.project.config.validate-coordinate
nova.project.config.inspect-plugin-types
nova.project.config.ensure-plugin-instances
nova.project.config.inspect-bundle-collector
nova.project.config.export-runtime
nova.project.hotfix.refresh-game-dlls
nova.project.build.inspect-readiness
nova.project.table.export
nova.project.network.export
nova.project.sound.export
nova.project.vibration.export
nova.project.localization.export
```

Adapter 还会拒绝所有 `Delivery`，以及含 `Destructive / ExternalWrite / Credential` 的 Action。`confirmation_token` 是调用方声明的计划绑定，不是可信人类审批证明，因此不能用它开放上述高风险效果。

Tool 只返回安全 DTO；项目根路径替换为 `<project-root>`，敏感字段打码，URL 去除认证、query 和 fragment。包依赖为 `Framework -> Nova MCP -> Unity MCP`；程序集依赖保持单向：`NovaFramework.Mcp.Editor` 只定义中立 SPI 和 Gateway，独立 `NovaFramework.Mcp.UnityMcp.Editor` 适配默认 Provider，Framework 在 domain load 注册唯一 Action Provider。30 个 Skill 不会逐个成为 MCP Tool；默认 Adapter 只注册 `nova_project_action`，再由 live `describe` 返回当前开放 Action。

## 状态与证据

- `success`：满足 Descriptor 全部最低证据；
- `partial`：已提交、等待稳定、发生中断或证据不足；
- `blocked`：写前安全拒绝，或无法安全继续；
- `not_applicable`：Action 未注册或当前项目不适用；
- Plan 额外使用 `ready`。

校验 Action 的“执行成功”和“业务对象有效”是两层语义。例如 `config.validate-coordinate` 可以 `success + data.valid=false`，表示校验闭环成功完成但目标配置存在问题。

## 扩展要求

新增 Action 前必须证明输入、写入集、稳定领域 API 和自动证据均已成立。Table、Localization、Network、Sound、Vibration 已补齐稳定 Settings resolver、精确输出规划与 data/code 分证据；UI 仍需先形成稳定设置与输出契约。环境检查需要先抽取无 `SessionState` 写入的 pure probe；CDN Delivery 需要可信审批、凭据隔离与外部审计。

不得通过新增 Handler 绕过这些前置条件，也不得建立静态 Action Catalog 与 Registry 竞争真相。
