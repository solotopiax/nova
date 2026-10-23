# SDK / Kit 共用执行路径

安装、启用、导出和本地验证共用本路径；包差异由实际解析版本的 INDEX 与定向引用提供。固定调用复用现有 Action，业务 API 保持为 C# 调用，不伪装成 MCP Action。

## 从自然语言取得最少输入

把“加谷歌登录”“启用客服”“接入云存档”转成一个用户目标。先读项目已安装包、现有业务调用、当前 ConfigMaster/Runtime 和活动平台，再确认精确 package id；后缀仅帮助发现，不能证明 registry 可安装、该平台可运行或字段已配置。账号、渠道、是否采用真实设备/服务端验收等无法从项目确定的决策才由用户补充。

安装/升级前走 `nova-project-manage-upm-package` 的 Plan；Resolve 稳定后读取实际 `PackageInfo.resolvedPath/package.json` 的身份、版本、依赖与 INDEX。已解析且满足目标的包跳过 UPM 写入。`package.json`、Runtime/Editor asmdef、平台资源、构建处理器、裁剪和 HybridCLR 条件按文档核对；Sample 的依赖和默认物料不自动提升为业务运行依赖。

## 直接复用公共 Config 操作

先进入 `nova-project-configure-runtime` 并从 live `describe` 获取 Schema。以下为调用语义，字段以当前 Descriptor 为准：

| 操作 | 现有 Action | 执行要点 |
|---|---|---|
| 读取当前坐标与启用类型 | `nova.project.config.validate-coordinate` | 绑定 Master GUID 和目标坐标；只读结果不等于所有字段已经正确 |
| 查顶层 Config 身份 | `nova.project.config.inspect-plugin-types` | `kind=sdk` 或 `kit`；核对 `typeFullName/assemblyIdentity/moduleVersionId` 与目标包声明 |
| 补实例、收敛启用列表 | `nova.project.config.ensure-plugin-instances` | 精确 Config `typeFullName`，明确 `kind/scope/coordinate?/enable`；保留已有实例与字段 |
| 读取已有SDK/Kit面板 | `nova.project.config.inspect-panel` | 绑定Master、精确三维target、panel=SDK/Kit及已有Config FullName；查询真实path/type/editable/掩码与同组坐标，凭据只存在性 |
| 修改已开放字段或维度 | `nova.project.config.patch-panels` | 复用inspect路径和严格JSON valueJson；完整Plan核对共享组广播/维度分裂合并，单Master保存，不导出 |
| 独立整理非规范Master | `nova.project.config.normalize-master` | 明确三维锚点和整Master脱敏差异，独立保存/Verify后重新inspect及Plan，不把整理藏入字段patch |
| 导出目标 Runtime | `nova.project.config.export-runtime` | 绑定 Master、坐标和 `savePath`；目标 Platform 必须匹配活动 BuildTarget |

每个动作复用 `plan → execute → verify`；需要确认时按当前精确授权与 Action 计划传 `confirmation_token=plan_id`。Token 绑定计划，不代替用户授权。断线或重载后用返回 `recovery_token` 只读 Verify，不重放 Execute；读取领域状态，不能只看工具外层成功。

`ensure-plugin-instances` 的 `enable` 必须显式确定：`true` 把 Config 类型 FullName 收敛成全局 `EnabledSDKs/EnabledKits` 中一项，`false` 删除该项，不是“只补实例而保持启用状态”。`scope=coordinate` 只补目标格；`scope=matrix` 补全部有效坐标。启用前会检查全矩阵该类型实例以及维度一致性，已有值不被替换。单格请求被完整性门拒绝时，报告所缺实例；只有用户授权包括全矩阵补齐，才改用 matrix，不自动填其他坐标的业务值。

顶层 SDK 必须实现 `ISDKPluginConfig`，Kit 必须实现 `IKitConfig`。MAX/AdMob 的 `IAdChannelConfig` 在 `AdPluginConfig.ChannelConfigs` 内，Mobile/ThirdPay/Voucher 的 `IIAPStoreConfig` 在 `IAPPluginConfig.StoreConfigs` 内，不登记成另一项顶层 SDK。

非固定 SDK/Kit 字段优先复用 Config Operation 的 `inspect-panel / patch-panels`，读明目标格、真实字段、默认unknown或已核实声明、范围/单位、维度共享、资源引用与生效时机，核对完整物理写集后单Master保存。`patch-coordinate` 仍仅支持 Namespace / AppConfigs 固定字段；不能把复杂管理引用或未开放字段塞入任意JSON。未开放结构使用包文档化专门入口；非规范Master显式normalize后重新inspect，脏资产/窗口草稿先解决归属。宿主缺入口时报告确切类型/字段缺口，不改包、不反射或用 `execute_code` 绕过。已保存字段不为“恢复默认”而重建插件实例。

窗口“导出”会更新活动场景快照；场景不在授权范围时使用 `export-runtime`。Master、Runtime、场景和活动平台分别核对，等待编译稳定后才运行。

## 初始化与探针

SDK 在现有启动链中由 `Nova.SDK.InitializeTask` 启动一次。已有启动链负责初始化时无需业务再次启动；只等待目标能力用 `WaitForPluginAsync<TPlugin>(ct)` 并检查 `SDKPluginInitializationState.Ready`，再用 `Get<TPlugin>()` / `TryGet<TPlugin>()` / `GetAll<TInterface>()` 获取。依赖由 `ISDKInitializationDependencies` 声明，`Priority` 不控制初始化先后。探针同时读包特有状态/回调；Ready 不能替代广告位、商店商品、真实登录或后台结果。

Kit 通过 `Nova.Network.Kit<T>()` 获取公开 Service，由 Service 读取目标 `IKitConfig`；不调用 `Nova.SDK` 初始化 Kit。先核对正式 Network 加载、HostKey/NetCmd 与 Proto 链，再在允许的已有业务入口验证。

游戏服确认登录后统一 `Nova.SDK.Login(uid, userProperties)`；明确注册时仅一次 `RegisterAndLogin(uid)`，其内部包含 Login；登出使用 `EndLoginSession()`。Kit 或项目已有登录入口会自动通知时不重复调用。属性需赶上首次 ABTest 拉取时与 Login 一次提交；不把三方 Auth 成功当作游戏服登录。

没有当前包文档定义的可观测探针时，配置/编译最高 `partial`；输入或安全执行入口缺失且尚未执行时为 `blocked`。后台写入、登录/支付/发奖请求、用户权限和真机操作按各自实际授权执行，不用本地 Mock 验收真实服务。

## 复用路由

| 后续用户目标 | 复用已有 Skill |
|---|---|
| 装包、升级/卸载 direct dependency | `nova-project-manage-upm-package` |
| SDK/Kit 参数、维度与 Runtime 导出 | `nova-project-configure-runtime` |
| HostKey/NetCmd/Proto 与已确认业务协议 | `nova-project-integrate-network-api` |
| 客服/登录/支付入口页面 | `nova-project-ui-create-view` / `nova-project-update-ui-view` |
| 业务流程、事件和本地进度持久化 | `nova-project-integrate-procedure` / `nova-project-integrate-event` / `nova-project-integrate-persistence` |
| Android 原生依赖 | `nova-project-resolve-android-dependencies` |
| 裁剪与 AOT、构建前检查、本地包 | `nova-project-generate-hybridclr-artifacts` / `nova-project-preflight-build` / `nova-project-build-player` |
| 启动/构建/设备问题 | `nova-project-diagnose-startup` / `nova-project-diagnose-build` / `nova-project-diagnose-device-runtime` |

这些是已确认用户目标的条件复用，不增加强制requires DAG。用户只要求配置时完成配置范围；用户要求业务可用时继续执行下面的共享业务分支，把项目自有source、表源、Prefab/Scene写入交给匹配Operation，沿源→正式导出→Config→运行时消费验收，不能止于路由建议或安装扫描。

## 共享业务实现契约

先发现现有业务入口、程序集和状态所有者，冻结一个目标能力及精确自有source、UI/Prefab/Scene绑定、字段/表源、API与协议、成功/失败/取消、账号/交易身份、订阅拥有者、幂等及保存探针。已能读取的名称、路径和状态由Agent取得；玩家进度取舍、SKU/发货策略、推送对象或事件归属等业务决策有多个合理候选时才需用户决定。

复用 `workspace-edit` 直接实现授权业务源文件中的已核实公开调用，固定导出/配置/对象编辑直接调用既有Action/共享MCP，避免反复写临时Runner。保持项目既有组织与接口，不编辑Framework、SDK/Kit、vendor、程序集生成物或Sample；必要资源缺失或绑定未完成不能宣称功能闭环。跨模块仍走Nova公开门面/接口，包专属行为通过目标已发布Plugin/Service公开契约，不直接调用内部NetService或原厂API。

异步使用真实调用生命周期的CancellationToken和UniTask；界面退出/流程离开取消等待，迟到回调核对当前账号/请求身份。Login/Bind/Save当前公开请求没有CancellationToken参数，不凭空添加overload；取消本地等待不证明已取消服务端操作，重试先按真实协议查状态。SDK的 `StickyEvent/ReplayEvent` 有补发语义，`Subscribe` 返回IDisposable或按同一handler Unsubscribe；Replay在加入新handler前同步补发buffer，业务持久消费者/幂等记录必须在订阅前就绪，不能只用易丢的UI布尔值阻止重复发货。需要转成Nova.Event时使用独立业务事件、拷贝载荷并按Event Operation维护Clear和订阅生命周期，不能把SDK事件当池化对象。

| 用户目标 | 直接复用的公开调用与现有Operation | 必须落实的业务行为与证据 |
|---|---|---|
| 游客/三方登录及账号切换 | `IAuthPlugin.LoginAsync/LogoutAsync`；`Nova.Network.Kit<Login>().Async`；UI + Procedure + Network，细节读身份引用 | Auth只证明平台身份，游戏服成功后才采纳UID；GameLogin已有成功路径自动Login/RegisterAndLogin，禁止重复通知；取消/网络失败不自动新建账号 |
| 绑定冲突与云进度 | `Kit<Bind>().QueryBindingAsync/BindAsync/QueryConflictAsync/ResolveAsync`、`Kit<Save>().SetGameVersion/GetAsync/GetFullAsync/SetAsync/SetFullAsync`；UI + Network + Persistence | 10404仅文档定义的游客回退；10402显示双方摘要并由玩家选择guest/existing。Resolve只裁决账号，之后明确本地上传或云覆盖、版本/数据模型、最终账号隔离及Persist耐久性；不按时间自动覆盖 |
| 激励、插屏与Banner | `Nova.SDK.Get<IAdPlugin>()` 的RequestAsync/IsReady/ShowAsync/IBannerControl；`Nova.SDK.Get<AdPlugin>().Events`；UI + Event + Procedure | main thread、取消/加载失败提示、格式/广告位来源；ShowAsync没有AdResult，结果从ShowCompleted/ShowFailed/AdClosed。激励以AdClosed的Success/UserCompleted与已确认领取身份一次消费；RewardGranted不证明已发奖。Banner预加载后ShowBanner，生命周期Hide/Destroy按接口 |
| 购买、补单与恢复 | `Nova.SDK.Get<IAPPlugin>().PayAsync<T>/RestorePurchasesAsync<T>/CheckLocalOrdersAsync/TryGetCapability`、Events；UI + Network + Event + Persistence | 核对Products/Store/SKU与正式验单路由；结果同时检查IsSuccess/CanDeliver及StoreType/OrderId语义。发货、Replay事件和补单共用已确认订单幂等台账，不能请求返回与事件各发一次；Editor模拟不验真实商店/服务端 |
| 客服入口、未读与登出 | `AIHelpPlugin.Show/Close/IsShowing/SetLanguage/UpdateUserInfo/ResetUserInfo`、OnMessageArrived/OnUnreadTaskCountChanged；UI + Event + Procedure | 明确已配置entranceId、语言与业务入口；已有自动登录不重复，统一EndLoginSession之外本包需要显式ResetUserInfo；事件成对注销及迟到账号检查。Show true不证明工单发送成功，不自动上传日志 |
| 通知权限、Token、Topic与任务 | `IPushPlugin.GetTokenAsync/OnTokenRefreshed/SetTopicSubscribed`、`IFirebasePushTaskPlugin.QueuePushTaskAsync`；明确用户触发权限时 `Nova.Native.RequestNotificationPermissionAsync`；UI + Event + Network + Persistence | AutoRequestNotificationPermission实际策略先核对，权限/Token/服务端接收/送达分别验收；不重复自动账号上报。任务Queue只本地缓存，可随后自动发送，TaskKey覆盖/取消、账号、时间与接收方须冻结；Void Topic调用不证明订阅成功 |
| ABTest参数与曝光 | `DataMasterPlugin.GetParamValue<T>/GetParamValueJson/WaitForRefreshAsync/OnConfigRefreshed/OnConfigRefreshFailed/MarkExposure/LogExperimentEvent`；UI + Event + Procedure | 主题/参数类型/fallback和应用时点明确；登录首次属性通过已确认Login上下文，在GameLogin已通知路径不通过第二次Login补传。刷新结果绑定当前账号/请求，曝光只在真实功能展示，上报事件归属与范围冻结；默认值/Editor不证明实验命中 |
| 分析、归因及收益 | `ITrackPlugin.TrackEvent`、`IAttributionPlugin.TrackEvent/GetAttributionAsync/OnAttributionResolved`，广告RevenuePaid必要时复用对应已配置变现接口；Event + Procedure/业务入口 | 使用既有集中事件定义与Tracks归属，触发与接收目标、字段/单位去重明确，不重复框架注册登录/SDK内置点；真实厂商接收独立验收，局部回调/Mock不证明数据已到账 |

每行API以当前实际已安装版本声明为准；只读取命中包INDEX指向文档及必要公开源码。`AdPlugin.Events`不在IAdPlugin上，IAP/客服/实验的专属API不强行加到通用接口；复用是共享初始化、配置、源编辑、生命周期和验收，不能抹平不同服务的结果语义。

当前GameLogin成功路径已更新身份并通知SDK；GameBind的`ResolveAsync`成功仅更新最终身份，没有自动SDK会话通知，也不搬游戏数据。账号切换由项目已有流程拥有者统一处理最终UID、会话通知与存档策略；核实已存在通知后复用，确实缺失时在授权项目业务入口补齐一次，不因“Kit已处理”省略，也不另建重复通知路径。

## 真实服务的精确执行门

本Workflow保留Catalog的本地 workspace/unity/generated-output副作用。业务代码可完成真实API调用链，运行真实服务前单独确认实际外部目标：环境与端点、账号/UID来源、商品/SKU/订单或taskKey/主题、接收方、字段数据、权限触发、请求数量和重放策略；生产交易/云覆盖/删账号、真实发奖、Topic订阅、统计/实验上报不由宽泛“接入完成”无限授权。已有精确授权持续有效，变更上述目标后重新冻结；缺授权不执行相关请求，但继续其余必要本地实现。

真实支付/云写/消息任务断线或取消后先查询已确认订单/数据/任务状态，不盲目重试、不用Mock绕过。需要设备、后台、凭据或账单结果而当前未具备时保留已完成业务链和具体缺项，整体partial；配置只读探针不触发真实业务请求。外部宿主/工具不可用时明确缺哪一种能力，不能把普通C#调用面伪装成已可直接调度的MCP Action。

## 业务交付与清场

源码证据须包括目标可达入口、精确public调用、参数/错误/取消、订阅释放、账号/订单/请求身份、源及生成/Config引用、资源绑定和最小真实消费链。完成Unity编译后从实际入口验证范围内的UI/流程与本地观察；请求确需外部服务而未获证据时不宣称登录、支付、广告、送达、实验或归因成功。只取得源审查/编译/Ready时不能标记业务任务validated。

删除本轮临时探针、脚本/meta、对象、日志和测试配置，Refresh必要的脚本引用；保留用户要求的正式业务源与资产，核对git status及保存范围。交付分别报告已实现、已有证据、外部未验证/工具缺口和清场。当前这些共用条款来自公开源码与Docs审查，不能自行变成当前项目live验收记录。
