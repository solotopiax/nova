---
name: nova-project-integrate-network-api
description: Use when 项目组要正式导出已有 HostKey、NetCmd 或已确认 Proto 的配置和访问代码，或接入 HTTP / WebSocket 业务调用并验证实际响应与退出生命周期时使用。
---

# Nova 接入业务网络 API

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。执行确定性导出时先通过 `nova_project_action` 对 `nova.project.network.export` 执行 `describe`；只有 Action 报告配置问题时才读取对应 Exporter 文档。仅在检查 HostKey、NetCmd 源和导出边界时读 `Docs/Editor/DataPipeline/Implements/Networks/NetworkExporter.md`；更新 Inspector 设置时读 `Docs/Editor/Inspectors/NetworkComponentInspector/NetworkComponentInspector.md`；运行时加载、路由和 HTTP 验证时读 `Docs/Runtime/Modules/Network/NetworkComponent.md` 与 `Docs/Runtime/Modules/Network/NetworkManager/NetworkManager.md`。不要递归加载全部 Network、Proto 或 Pipify 文档。

先从用户目标选择仅导出或业务接入。业务接入再从现有调用确认HTTP或WebSocket。仅在目标是长连接、推送消息、认证/心跳/重连时读取 [WebSocket业务接入](references/websocket-integration.md)，直接在授权项目业务源中复用`Nova.Network`公开长连接接口；下列HostKey/NetCmd、HTTP方法和主备规则仅适用于HTTP及确实使用这些表的业务。WebSocket按精确`ws/wss`地址、通道类型和消息协议冻结，不套用HTTP路由，不为每个transport新建Skill。

## 仅导出已有配置和代码

用户只要求导出已有网络配置的 `all` / `code` 产物时，选择合同中的 `taskProfiles[id=export-code-data]`，对应 `network.export`。冻结项目根、已保存活动场景、唯一 `Nova` / `NetworkComponent`、现有 `NetworkSettings` / `ProtoSettings`、非空明确的 `targets`（`hostkey`、`netcmd`、`proto`）、对应真实源与正式输出范围；选中 `hostkey` 时另核有效 `ConfigRuntime` 的 `DevelopMode`，未选中时不强制该前置。`proto` 必须已有明确 Schema 与生成工具。不要求业务 Owner、HTTP/WebSocket 运行入口、账号、测试端点或实际响应；`data` 单独导出不属于此 profile。

追加冻结 `activeConfigMasterAndGeneratedNamespace`：选中 `hostkey` / `netcmd` 时，从 `ProjectSettings/Nova/Globals.json` 读取 `configMasterGuid` / `configMasterPathHint`，经当前 Action 的 `describe/plan` 与 Unity Editor / MCP 只读配置查阅核对实际可加载 `ConfigMasterSO` 的 GUID、资产路径和顶层 `Namespace`。Luban 的 `RuntimeProvider.GetNamespace()` 读取这个顶层值，不读取 `ConfigRuntimeSO.Namespace`，也不调用 `DimensionalResolver.ResolveNamespace`；可按需读取 `Docs/Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ExportHelper.md` 核对来源。缺少绑定或身份不一致时 `blocked`，不得依据场景、第一个候选 Master 或旧生成代码补猜。已绑定 Master 明确空值时允许 global namespace，并冻结该预期。 仅选 `proto` 时记录 Master 前置不适用，从已确认 Schema 和实际 protoc 生成配置冻结命名空间与类型全名；Proto 不经过此 Luban 来源。该输入不额外要求 `ConfigRuntime`、Collector、账号或端点，选中 `hostkey` 的原 `DevelopMode` 前置仍保留。

Plan 必须记录上述身份与命名空间期望，Execute 在任何写入前复核；身份、值或适用目标漂移时重新计划，不能沿用旧 `PlanId`。以当前 live Action 返回的字段和已发布实现为准；当前宿主若未提供相应冻结或复核门，代码导出为 `blocked`，不得称该防护已实现或已验证。验收同时核对生成命名空间、完整类型名和既有消费者与冻结预期一致；SHA 与编译分别保留，不能替代类型身份兼容性检查。

复用 `nova.project.network.export`，live `describe → plan → execute` 冻结 `scope/targets` 和完整写入集。业务接入的全链确认输入适用于集成分支；本分支只冻结 `profile.requiredInputs`、当前 Action 的 `PlanId/writeSet`，危险副作用的原确认门、全部禁止范围、锁和幂等规则仍继承，不能由 profile 降低。等待此次 Unity 编译和脚本重载后，以 Core 的 `recovery_token` 只读 `verify`，核版本 2 Receipt 的完整输出、路径、类型与 SHA；写入已开始或重载导致 `partial` 时不重放 Execute。全部计划产物、范围外状态、真实编译及本轮清场均符合冻结范围，才报告“所选网络配置/代码导出成功”；缺产物或编译证据仍为 `partial`，不承诺协议可用、实际响应或完整网络接入。本分支验收后结束，下面仅适用于业务接入。

## 业务接入的冻结输入与阻断门

HTTP先区分既有请求生命周期维护与新协议接入，并确认是否实际使用HostKey / NetCmd。既有单URL请求只补页面退出取消时，冻结当前 `NetworkComponent` 与管理器、原URL/方法/编码/响应处理、请求拥有者/退出代际、允许写入的业务入口和受控延迟取消探针；从当前源码复核既有协议，保留认证与请求体，不要求新路由表、表导出或新账号，不修改协议或发出未经授权的外部请求。新协议接入才冻结协议、请求字段、认证方式（可明确为无认证）、响应成功/业务失败和重放语义、测试端点与所需账号；使用路由表的请求再冻结HostKey / NetCmd唯一源、行、Path与主备。Agent先从项目事实取得已有路径、类型和配置，用户只补无法读取的协议或业务决策。适用分支的必要输入不明时返回 `blocked`；仅有域名与 Path 时不能声称新协议已接入。

HostKey 表的 `Value` 主域名列必须存在且有效行必须填写。`FallbackValue` 可为空，旧表缺少备用列时仍校验主域名；不要为兼容旧表批量改 schema。使用官方模板时先替换 `XXXXX` 主域名占位符，再从项目正式表源导出。模板的备用列默认空，不推测备用地址，也不把模板读取当作项目导出成功。

按表内名称读取时，NetCmd 使用 `Nova.Network.ResolveNetCmdRow(cmdName)`，HostKey 使用 `ResolveHostKeyRow(hostKeyName)`；二者的名称都对应各自行的 `Name`。HostKey 查询返回已加载原始行、缺失返回 null，不自动加载、校验或改写路由；发送继续复用原路由链。旧 `GetNetCmd<T>()` / `GetNetCmd(tbName)` 同时支持 HostKey 与 NetCmd 生成表；`GetNetCmdUrl(tbName, dtName)` 的 `tbName` 是表类型短名，`dtName` 是指令行 Name，不能以空表名代替单行名查询。标准 Proto 仍先解析行再传入 `SendProtoAsync`。

- 不猜测协议：Proto + AES、JSON、form、raw body 与既有业务 Kit 是不同路径。Proto Schema、生成范围或服务端字段不明确时不导出、不改写业务代码。
- 业务代码复用已有适用 Service；缺少 Service 时可在已授权项目源中创建业务入口，使用已确认的 `Nova.Network` 公开 API。缺协议、认证、成功定义或重放语义仍阻断；没有现成 Service 不构成永久阻断。不得直接调用面向 Sibling Kit 的隐藏 `NetService` / `NetBuilder` 或身份写入接口。
- 标准 Proto + AES 使用 `Nova.Network.SendProtoAsync`：传入正式 `HTTP_POST` NetCmd 行、仅一次且无副作用的 `Header → 正式请求` 工厂和正式响应 Parser。先完成 Start 绑定、Config 与 Network 加载；工厂显式绑定协议 Head。门面复用原主备、最近成功优先、轮数、重试、AES、解析和取消链，不自行选域名、续签、登录或重发业务错误；登录、绑定和存档优先使用已有 Kit。取消不能撤销服务端已执行状态；新增热更泛型调用还须核对 AOT 泛型、link 与业务 DLL。
- 普通 JSON/form/raw 使用适用的 `GetAsync / PostAsync / PostRawDataAsync / PostFileAsync`，按服务端契约显式编码 Content-Type 与请求体。单 URL 不自动主备或框架重试；旧无令牌签名保留，新令牌末位重载在全部原参数必填时提供物理取消。需页面/Service生命周期取消时读取 `Docs/Runtime/Modules/Network/HttpManager/IHttpManager.md` 的取消与响应所有权契约，核对当前管理器支持 `ICancellableHttpManager`：不支持时新入口发送前抛 `NotSupportedException`，不能回退后声称已取消。预取消不发送、返回 `NotReachedServer` 失败响应；在飞取消于Update轮询Abort，返回失败 `HttpResponse` 而非取消异常。每次最终响应仍在 `finally` 中 `ReferencePool.Put` 且仅一次，先复制所需内容；退出主动取消令牌后继续等待响应，拒绝迟到业务结果，令牌源Dispose或Manager Shutdown不等于取消。不要用 `AttachExternalCancellation` 丢弃仍运行的底层请求及其池响应。项目 Service 可按已确认的幂等、错误分类、次数和退避实现有限业务重试及签名续期；不得把交付状态未知的副作用请求直接重发。旧入口取消只能停止后续尝试或拒绝迟到结果；确需跨 URL 通信策略且现接口不能满足时保持 blocked 并明确缺项。
- 主备验证只以受控的通信失败为证据。正式 HTTP `4xx / 5xx` 是业务响应，不得被包装成“备域接管成功”。会产生服务端状态的登录、领奖或下单请求还必须确认重放语义和测试环境。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的 HostKey / NetCmd 源、行与路径 | 项目已确认的源编辑入口；仅定向修改允许的行 | 指定的主备域名与指令路由 | 源、行、HTTP 方法、HostKey、Path 与确认记录一致 |
| 已确认的 NetworkComponent、数据格式和导出范围 | `nova.project.network.export` | 正式 HostKey / NetCmd / Proto 数据与类型产物 | Action Verify 成功，且 ConfigRuntime 的 DevelopMode 与导出事实一致 |
| 已确认协议与业务调用入口 | 复用或创建项目自有 Service；标准协议使用 `Nova.Network.SendProtoAsync`，普通 HTTP 使用适用的公开单 URL API | 可调用的目标业务请求 | 原主备/AES/Proto链复用，普通响应逐次finally回池；业务代码不直接依赖隐藏内部实现 |
| 既有单URL请求、页面/Service拥有者和退出取消目标 | 在已授权业务源中传入原参数与拥有者令牌，退出主动Cancel并保留await/finally | 既有请求的生命周期取消 | 受控端点收到请求后取消，真实连接断开、最终失败response仅归池一次、迟到业务结果不进入已退出页面；不因没有路由表或登录账号阻断 |
| 已确认测试端点、账号与成功探针 | `Nova.Network.LoadAsync`、`GetNetCmdUrl()`、`ResolveNetCmdRow()` 与适用的请求 API | 运行时路由与目标响应 | Play Mode 中加载、路由、请求和响应符合冻结的成功定义 |

## 业务接入的实施与验证边界

以下步骤用于HTTP分支；WebSocket完整输入、源码实现、消息所有权、生命周期及验证按对应引用执行。公共C#调用通过项目业务`workspace-edit`落地，不声称新增了稳定可调度MCP Action；固定导出与配置复用已有Action，不重新生成同义Runner。

1. 先核实际管理器初始化与业务入口。既有单URL取消维护按原协议核URL、拥有者/退出、当前管理器支持、新重载参数和最终响应归池；缺取消探针或确实缺初始化时保留相应验证缺项。新协议接入核已确认协议、认证、响应/重放与适用测试端点；认证可明确无认证，账号仅在协议要求时必需。使用HostKey/NetCmd的分支才检查Config/Network加载与唯一路由，适用必要输入缺失时返回 `blocked`。
2. 只修改冻结的源行和业务调用入口。NetworkComponent、配置资产、Prefab 与 Scene 引用只能经 Unity Editor / MCP 修改；禁止手写 YAML，禁止手改导出产物。
3. 仅在本轮修改路由表或Proto源时通过 `nova.project.network.export` 定向导出 HostKey / NetCmd / Proto；既有单URL取消维护以及不使用这些源的普通请求跳过此步。scope规范化后Proto不支持data；适用导出的Verify核版本2 Receipt完整输出、路径/类型与Hash，旧/缺项证据保持partial。适用导出前置Config缺失、导出或校验失败时停止，不清理其他网络输出，不退化为任意C#、反射或临时Pipify。
4. 使用路由表时在Play Mode先 `Nova.Network.LoadAsync()` 并验证URL。新协议接入再向精确授权测试端点发可审计请求并核正式响应；既有取消维护从实际业务入口用已授权受控延迟端点验证请求已到达后Cancel、物理连接终止、最终失败响应/唯一归池、退出代际和迟到结果，不要求新增表加载或登录账号。不能用Mock或仅取消包装等待证明物理Abort。
5. 既有取消维护在真实请求、退出取消/终态、响应归池与迟到拒绝均成立后按该范围报告 `success`；不声称服务端回滚或新业务接口验收。新协议接入只有适用路由、请求与预期响应都成立才报告 `success`；需要这些输入的分支无测试端点、账号或成功探针时返回 `blocked`。输入已确认且已执行允许步骤，但因当前运行环境无法完成Play证据（包括仅能取得导出或编译证据）时报告 `partial`；未执行允许步骤时不得报告 `partial`；适用协议或主备语义不明时返回 `blocked`。

不默认修改服务端、凭据、生产数据、Framework 包、其他 NetCmd、其他 HostKey 或 Git。删除、外部写入、凭据使用、生产请求、Git commit / push 均需要本 Skill 之外的精确确认。
