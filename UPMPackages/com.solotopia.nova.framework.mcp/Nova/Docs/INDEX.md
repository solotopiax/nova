# Nova Framework MCP Adapter

`com.solotopia.nova.framework.mcp` 是 Nova Framework 显性依赖的 Editor 传输桥。它不实现领域逻辑，只把通过安全策略审核的 Action 以固定 Tool 协议开放给项目组 Agent。

## 用人话理解本包

本包是 Nova 给 Unity MCP 提供的专用扩展，当前复用 Unity MCP 服务。AI 按 Skill 明确任务后，直接选择 `nova_project_action`；Unity MCP 接收和传递这次调用，Nova 网关再分发到对应 Action。Action 已在 Unity Editor 内部，可以直接调用 Unity API 和 Nova 既有工具。

“注册工具”就是告诉 Unity MCP 工具叫什么、接受什么参数、收到调用时交给哪个函数。当前注册实现见 [NovaProjectActionUnityMcpTool.cs](../UnityMcp/Editor/NovaProjectActionUnityMcpTool.cs)。通过 Unity MCP 连接是当前默认适配器的选择；中立契约与 Action 内核仍和具体 Provider 分开。

完整概念说明见已安装 Framework 的 `Docs/Onboarding/AI_SKILLS_ACTIONS_MCP.md`；开发仓对应 `Assets/Framework/Docs/Onboarding/AI_SKILLS_ACTIONS_MCP.md`。

## 入口

- MCP Tool：`nova_project_action`
- 中立程序集：`NovaFramework.Mcp.Editor`
- 默认 Adapter 程序集：`NovaFramework.Mcp.UnityMcp.Editor`
- Core Action Registry：`NovaFramework.Editor.EditorUtil.AgentActions.Registry`
- 运行条件：Unity Editor 已连接当前默认的 Unity MCP Provider，且 Editor 处于可执行工具的稳定状态

## 安装前置

当前完整安装入口是 NovaSpark。它先向消费工程 `Packages/manifest.json` 写入 OpenUPM registry 与精确 scope `com.coplaydev.unity-mcp`，再安装 Framework；UPM 依赖链随后自动解析 `Framework -> 本包 -> com.coplaydev.unity-mcp@10.3.0`。Unity MCP 不需要成为项目顶层 Git 依赖；绕过 NovaSpark 直接安装 Framework 时，工程必须已经配置可解析该包的 registry。Nova 开发工程可另外安装其他 Provider 用于开发态验证，不改变对外包的默认选型。

安装调度现在会自动准备服务与当前项目 Codex 绑定；安装包存在、端口监听、Editor 连接和当前聊天工具可见性仍是不同层级的证据。

## 自动服务与当前项目绑定

`NovaFramework.Mcp.Editor.NovaToolchainBinding.EnsureAsync(projectRoot, cancellationToken)` 返回 `UniTask<BindingResult>`；结果包含 `ProjectRoot`、`Status` 和 `Detail`，当前域最近状态可从 `LastResult` 读取。入口只接受当前 Editor 项目，同一时刻串行执行，不接受任意 C#、反射或 Action 请求。Provider 通过 `RegisterProvider(providerId, ensure)` 显式注册；Coplay Adapter 的 `InitializeOnLoad` 只注册回调，实际工作由安装调度调用入口后开始。中立程序集不反向引用 Framework 或 Coplay。

默认 HTTP 流程：

1. 验证已经连到当前 Editor 的 Provider，正确连接直接复用，不重启本机服务。
2. 未连通时，在线程池执行原厂运行时版本检查；缺失时调用原厂 `UvInstaller` 自动安装 uv，安装器自身最多等待 120 秒。原厂入口不接受取消信号，因此取消/重载不保证同步终止其子进程；三分钟会话接力租期避免立即并发重装，调用方稍后自动重试。日志与进程原始输出不进入结果。
3. 保留既有 loopback URL。端口有监听时只尝试连接，不终止进程；端口空闲时用原厂固定服务版本参数和后台启动器新增服务。原厂总启动入口会先尝试停止当前端口服务，因此自动绑定不调用该入口，也不调用全部客户端配置。启动接力由会话标记保持，首次下载尚未开始监听时返回 `partial`，避免每次重试都再启动一份服务。
4. 通过 Provider 实际状态和异步验证确认当前 Editor 已连接；确认并启用 `nova_project_action`，HTTP 连接重新登记当前工具，再调用只读网关 `describe`。不会执行任何业务 Action。
5. 先匹配当前项目和 `CODEX_HOME/config.toml` 中已有启用配置；一致时只读复用。缺少绑定时，在项目 `.codex/config.toml` 的专属受管区块新增服务器项，先严格解析、保留区块外文本、核对并发变化，再原子保存与读回验证。临时写入文件在所有退出路径清理；不自动修改 Git 忽略、项目信任或用户全局配置。

`success` 只表示当前 Editor 的 Provider 连接、Nova `describe` 和客户端精确绑定已经核验；新写和既有配置、HTTP 和 stdio 均采用同一本地部署标准，不根据等待时间推断宿主已加载。stdio 核验针对当前 Editor 桥接，宿主将依配置启动外部服务；当前聊天的 `tools/list`/实际通道可用性必须由外部宿主现场核验，包内没有可强制热加载聊天工具的 API。宿主会在加载受信任项目配置时连接；说明见 [Codex MCP](https://learn.chatgpt.com/docs/extend/mcp)。

`partial` 用于运行时服务仍在准备或被重载/取消中断；不会要求项目组手工安装或配置。`blocked` 用于项目不匹配、Provider 缺失、网络或权限失败、端口服务不兼容、网关失败、损坏/冲突/外部链接配置，或宿主明确不信任项目。调用方有界重试；不会以关掉其他项目服务、覆盖凭据或提高信任权限绕过阻碍。

既有远程连接和精确客户端绑定均有效时可复用；远程连接未通或缺少可复用绑定时保留地址与认证并返回 `blocked`，不改成本机，也不把凭据复制到项目或新增未经认证核验的绑定。既有 stdio 选择也保留：自动准备 uv、Editor 桥接和同传输启动配置，但不把监听就绪当作外部 stdio 服务已被宿主加载。启动日志位于当前项目 `Library/Nova/Toolchain/mcp-server-launch.log`，用于实际后台进程排障，不作为历史验收档案；自动结果不转储该日志。

## Tool 协议

| operation | 必填输入 | 作用 | 领域写入 |
|---|---|---|---|
| `describe` | 可选 `action_id` | 返回当前 MCP 开放的 Action Descriptor 与 Request Schema | 否 |
| `plan` | `action_id`、`request` | 严格校验并冻结本次计划、写入集与证据要求 | 否；只写 `Library` 审计元数据 |
| `execute` | `action_id`、`plan_id` | 原子消费一次性 Plan；确认型 Action 还需绑定令牌 | 取决于 Action |
| `verify` | `action_id`、`receipt` | 只读核验当前领域状态，不重放 Execute | 否；可更新 `Library` 审计元数据 |

`confirmation_token=plan_id` 把调用绑定到同一份一次性 Plan。所有已注册 Project Action 默认开放，但该开放不取消精确目标确认、资源锁、Receipt、脱敏或 Verify。

令牌相等只证明请求绑定，不证明人类已批准；网关将授权来源标为 `caller_asserted`，调用方仍须遵守当前任务授权。传输信封总量最多 64 KiB，普通标识字符串最多 512 字节、Receipt 最多 64 KiB；请求树深度上限 64（属性节点也计层）、字符串值最多 16 KiB。它们是传输边界，required、嵌套结构与领域值仍由 Core 严格解析，不能用网关接收成功代替领域准入。

## 长计划的传输记录

`operation` 仍只有四个阶段。预计捕获较慢的计划，在当前 Tool Schema 已提供 `plan_mode` 与 `request_id` 时，可在 `operation=plan` 内选择以下传输子模式；不传 `plan_mode` 保持原同步调用。

优先使用当前真实可调用、参数 Schema 已包含 `plan_mode/request_id` 的 Tool 路由。若外部 MCP 服务仍用旧 `nova_project_action` Schema 拒绝新字段，而 Unity 的 custom-tools 资源已列出更新后的同名 Tool，可通过现有 `execute_custom_tool` 转交同一个 Nova Tool：`tool_name="nova_project_action"`，`parameters` 为本文完整请求对象。它仍进入同一中立 Gateway、严格 Action Schema、暴露策略及 Core 门；不是任意 C#、反射或第二条领域执行旁路。仅当当前 Provider 真实提供该入口且新 Schema 已核实才使用。单独新建客户端会话未必刷新服务端缓存；缺可用转交入口时保留具体阻碍，由用户后续按 Provider 正式方式刷新或重启服务，不擅改服务器配置或当前重启。

| plan_mode | 同时必填 | 作用 |
|---|---|---|
| `start` | `action_id`、`request_id`、`request` | 先登记并返回 `queued`，下一次 Editor update 经原严格 Plan 入口捕获 |
| `status` | `action_id`、`request_id` | 非消费式读取该记录和原网关脱敏响应；断线后用同号取回 |
| `forget` | `action_id`、`request_id` | 仅释放已结束的传输记录；不消费、删除或重放领域 Plan/Operation |

Agent 先生成并保留一个 N 格式 GUID 作为 `request_id`，无需让用户填写。登记响应丢失时，使用同一编号、同一 Action 和同一请求重试 `start`；对象属性顺序不影响摘要，数组顺序有意义。同号改参、改 Action、记录失效或域重载都不自动重跑。`forget` 后该编号不得再次使用；另一次独立计划必须生成新编号。

例如 Player 的四字段请求仍放在 `request` 内，传输字段不进入 Action DTO：

```json
{
  "operation": "plan",
  "plan_mode": "start",
  "request_id": "<Agent预先生成并保留的32位GUID>",
  "action_id": "nova.project.player.build",
  "request": {
    "target": "WebGL",
    "outputPath": "Build/WebGL/Current",
    "developmentBuild": true,
    "buildMode": "build"
  }
}
```

查询时保留 `operation=plan`，改为 `plan_mode=status` 并只带相同 `action_id/request_id`。外层 `completed` 只表示原计划响应已保存；必须继续查看 `payload.response.success` 和 `payload.response.data.status`。只有原计划为 `ready` 才可能进入原 `execute`；`blocked` 诊断不会因取回成功升级。`payload.plan_candidate` 只说明同域、未超过原计划期限的 ready 候选，不能证明 Registry generation、输入或一次性 Plan 仍有效，最终仍由 Core Execute 检查。

收件箱最多 16 条记录、同一时刻一项活动计划；每条响应最多 2 MiB，超过时明确为 `response_too_large`，不截断后伪称完整。记录 TTL 为一小时；过期清除响应、保留失效编号，直到明确 `forget` 才释放容量，避免过期后悄然重放。SessionState 只保存请求摘要及已有脱敏响应，不保存原请求、凭据或可执行 HandlerState；它用于同一 Editor 进程会话，不能承诺 Editor 退出后恢复。域重载后状态为 `interrupted`，旧响应仅作诊断，旧内存计划不可恢复；需要继续时重新计划，不重放执行。

此入口仍在 Unity 主线程串行执行，慢捕获期间 Editor 可能暂不响应，传输也可能断开；恢复连接后用 `status` 读取。它不修改 Provider 超时、心跳、统计设置、第三方回调或构建门，也不代表 Player 构建、设备或外部服务验收已完成。已有安全诊断记录的读取和释放不要求当前 Registry 正常；新登记和实际开始捕获仍分别验证当前暴露策略，领域 Plan 保留原全部检查。

## 长核验的传输记录

捕获较慢或需要完整后验 Evidence 的核验，可在当前 Tool Schema 已提供 `verify_mode/request_id` 时使用 `operation=verify` 的 `start/status/forget`；省略 `verify_mode` 保持原同步协议。新模式只保存同一次原 Verify 的完整脱敏响应，不改变 Action、Receipt、资源锁、观察门或结果判据，也不调用 Execute。旧同步请求已经丢失的响应不能由此补回。

| verify_mode | 同时必填 | 作用 |
|---|---|---|
| `start` | `action_id`、`request_id`、`receipt` | 先登记并返回 `queued`；下一次 Editor update 恰一次进入原 Verify |
| `status` | `action_id`、`request_id` | 读取已登记状态与同一次原响应；不带 Receipt，不再调用 Verify |
| `forget` | `action_id`、`request_id` | 仅释放本条终态传输记录；不改变原 Receipt、Operation 或领域结果 |

```json
{
  "operation": "verify",
  "verify_mode": "start",
  "request_id": "<Agent预先生成并保留的32位GUID>",
  "action_id": "nova.project.hotfix.generate-artifacts",
  "receipt": "<同一次Execute返回的原recovery_token>"
}
```

Agent 保留新生成的 N 格式 GUID。登记响应断线时，同号、同 Action、同 Receipt 的 `start` 只取已有状态；同号改 Action 或 Receipt 拒绝。随后使用相同 `operation/action_id/request_id` 与 `verify_mode=status`，严格不带 `receipt`。`completed` 只说明已保存原响应，须读取 `payload.response.success`、`payload.response.data.status` 以及完整 `payload.response.data.payload.evidence`；原 `partial`、`blocked` 或错误不会升级。Verify 响应不含 `plan_candidate`。在当前会话完整读取并核对原后验响应后，才显式 `forget`；该编号不再复用。

Verify 使用独立的 `Nova.ProjectActions.VerifyJobs.v1` SessionState 命名空间，与 Plan 记录互不覆盖；每个命名空间仍最多16条、每条脱敏响应仍最多2MiB、TTL仍一小时，Plan/Verify合计只允许一项活动登记。复用原有界算法：超限明确 `response_too_large`，不截断或扩大预算；过期清掉响应但保留编号墓碑，域重载标记 `interrupted`，均不自动重跑。登记只持久化 Action、Receipt 摘要及传输元数据，原输入 Receipt 不另存；原结果自带的不透明 `recovery_token` 仍保留于完整脱敏响应。SessionState 只承载当前会话的传输记录，Editor 退出后不承诺恢复；调用方须及时读取完整返回，按 Action 最低证据核验，并在对话中报告结果。临时响应、日志和摘要在当次验收后清理，不另存历史验收档案；Operation journal 仍不保存 Evidence 正文，尚在使用的恢复材料按原事务生命周期保留。

新登记与实际开始仍分别核当前暴露策略，`status/forget` 不依赖当前 Registry 正常。重载、过期、容量、存储或Provider失败应保留准确终态；不得用新编号自动再核验来掩盖旧响应缺失。需要后续独立核验时，先明确原调用已结束，再依原Action合同决定，不能重放Execute。若远端缓存旧 Tool Schema，沿上节已核实的正式 custom-tool 转交方式调用同一 Gateway；不改服务器配置或第三方超时，不增加任意代码入口。

## 当前开放边界

Adapter 目前显式允许 Registry 中全部 37 个 Action：

- `nova.project.upm.manage-latest`
- `nova.project.upm.uninstall-direct`
- `nova.project.config.validate-coordinate`
- `nova.project.config.patch-coordinate`
- `nova.project.config.inspect-panel`
- `nova.project.config.patch-panels`
- `nova.project.config.ensure-panel-references`
- `nova.project.config.normalize-master`
- `nova.project.config.inspect-plugin-types`
- `nova.project.config.ensure-plugin-instances`
- `nova.project.config.inspect-bundle-collector`
- `nova.project.config.export-runtime`
- `nova.project.config.generate-resource-keys`
- `nova.project.hotfix.refresh-game-dlls`
- `nova.project.hotfix.generate-artifacts`
- `nova.project.build.inspect-readiness`
- `nova.project.bundle.build-asset`
- `nova.project.bundle.build-raw-file`
- `nova.project.player.build`
- `nova.project.android.resolve-dependencies`
- `nova.project.table.export`
- `nova.project.tracks.generate-registry`
- `nova.project.iap.upload-product-catalog`
- `nova.project.ui.export`
- `nova.project.network.export`
- `nova.project.sound.export`
- `nova.project.vibration.export`
- `nova.project.localization.export`
- `nova.project.pipify.run-batch`
- `nova.project.pipify.inspect-settings`
- `nova.project.pipify.configure-batch`
- `nova.project.settings.inspect-target`
- `nova.project.settings.patch-target`
- `nova.project.settings.patch-build-defines`
- `nova.project.persistence.inspect-data`
- `nova.project.persistence.migrate-encryption`
- `nova.project.persistence.recover-maintenance`

Nova Project Skills 与 MCP Tool 不是一张清单：33 个 `nova-project-*` 由 Agent 从项目 `.agents/skills/` 发现；当前默认 Adapter 只注册一个 `nova_project_action`，然后由 `describe` 返回上述当前开放 Action。

能力发现继续使用 `describe`；原能力总览视图专用的 `NovaProjectActionGateway.GetExposureSnapshot` 与 `NovaProjectActionExposureSnapshot` 已移除，外部 C# 调用方需改用现有 `describe` 协议。

该清单用于说明当前包版本，不替代 live Registry。运行时必须先调用 `describe`；未注册、Registry 存在 issue、Schema 无效，或 Registry 与 ExposurePolicy 不完全一致时整个 Action 面都会 fail-closed。高风险副作用不会隐藏 Action，但仍必须由 Descriptor 准确声明并经过完整执行协议。

`nova.project.pipify.run-batch` 的 Execute 只登记异步任务并返回 recovery token；Verify 轮询持久化 Job 状态。Job 在真实运行期间持有资源锁，状态写入 `Library/Nova/Pipify/Jobs/`；domain reload 前未结束的任务标记为 `Interrupted`，绝不自动恢复或重放。

`describe` 保留凭据字段的参数名及类型结构，不将 Schema 当实际值遮蔽；示例、默认和描述载荷移除。Plan/Execute/Verify 的实际数据仍按敏感字段脱敏。

脱敏按已知敏感字段名、当前项目根及 URL 用户信息/query/fragment处理，不是通用秘密扫描；未知自定义字段或自由文本仍可能泄漏，领域 Handler 应先避免输出秘密。描述、计划和结果 DTO 只是当前发现/返回快照，数组属性可能保留原引用；读取或修改 DTO 不获得锁、不延长期限，也不使 Action Ready。

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

中立 Provider Registry 只接受程序集 `NovaFramework.Editor` 中的 `NovaFramework.Editor.NovaProjectActionContractProvider`。同一实例重复注册幂等，不同实例即使同类型也拒绝替换；静态引用保留到 Domain 结束，没有注销或 Dispose 入口。精确名称检查不是程序集签名认证，TryGet 也只返回引用，完整 Registry/Schema/开放策略仍在每次网关准入时核验。

新增 Project Action 必须同时完成：Core Registry 注册、专用 DTO 与证据契约、Adapter 显式 `ExposurePolicy`、包内文档更新及定向安全测试。Registry 与白名单集合必须完全一致，任一侧遗漏都会整体 fail-closed。
