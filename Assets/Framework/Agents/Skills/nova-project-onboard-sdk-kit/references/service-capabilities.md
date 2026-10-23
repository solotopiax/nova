# 广告、支付、运营与系统原生服务

用户说明业务目标后选择下列一组，读取实际包 INDEX 和表内页面，再按共用执行路径复用公共操作。不把渠道、商店或每个服务方法变成顶层 Skill。

## 日常任务的执行落点

先区分查阅参数、修改配置、接入业务代码与执行真实服务。查阅只读当前配置和匹配版本文档；已开放字段的配置修改复用共用路径的 `inspect-panel → patch-panels → export-runtime`；已配置服务的业务调用通过本 Skill 契约已有 `csharp-api` Adapter，落到已授权的项目自有入口、程序集和生命周期。Config、商品目录与 Tracks 的既有 Action/CLI 继续承担各自声明的任务，普通 SDK 方法无需逐个新增 Action。已有入口满足目标时直接复用，只有参数、配置或业务行为变化才修改对应部分，避免重复安装、初始化、导出、导入 Sample 或构建。

目标仅为 Skill 驱动链审查，且用户明确已验收业务、无需复测时，核对当前 Skill、契约 Adapter、匹配版本声明及项目入口即可；不为取得“可执行”结论再次登录、看广告、购买、创建工单、发送事件/任务或曝光。用户业务验收与本轮静态驱动证据分别记录；保留原未执行或未授权历史，不将静态审查记成新设备或后台执行。

| 业务目标 | 既有执行入口与必须绑定的参数 | 结果、生命周期与重复调用边界 |
|---|---|---|
| 请求/展示广告、控制 Banner | `IAdPlugin.RequestAsync(format, customProps, ct)`、`IsReady(format)`、`ShowAsync(format, customProps, ct)`；Banner 加载后使用 `IBannerControl.ShowBanner/HideBanner/DestroyBanner()`。`format` 取实际启用格式，`customProps` 是附加属性字典，不是渠道选择或奖励凭据 | `RequestAsync` 返回 `AdLoadResult`；全屏 `ShowAsync` 无结果值，订阅具体 `AdPlugin.Events` 的展示、关闭、奖励与收入通知。业务领取身份和幂等拥有者沿用项目既有实现；页面只释放自己的订阅与等待，渠道由 SDKManager 持有 |
| 购买、恢复与补单 | `IAPPlugin.PayAsync<IAPResult>(request, ct)`；request 使用实际 Store 的请求类型，`TableId` 对应正式商品表行 ID，`StoreType` 由具体请求决定。恢复与扫描分别使用 `RestorePurchasesAsync<IAPResult>(ct)`、`CheckLocalOrdersAsync(ct)`，专属查询走 `TryGetCapability<T>` | `IsSuccess` 与 `CanDeliver` 分别判断；返回值、Replay 事件和补单结果共用项目订单幂等消费者。`CustomData` 与 `ReceiptParam` 角色不同，后者还受目标 Store 编码/长度约束；恢复/扫描会有真实副作用，不为查询配置自动执行 |
| 打开客服、更新资料与未读查询 | `AIHelpPlugin.Show(entranceId, welcomeMessage)`；`entranceId` 是当前应用后台入口，欢迎语可空且仅在线客服页有效。语言走 `SetLanguage(languageCode)`，资料走 `UpdateUserInfo(name, serverId, userTags, customDataJsonString)`；未读查询走 `FetchUnreadMessageCount/FetchUnreadTaskCount()` | `Show` 返回 bool；`OnMessageArrived`、`OnUnreadTaskCountChanged` 均交付原厂 JSON `string`，按页面生命周期成对订阅/退订，不当作 int 数量。统一登录已交付 UID 时不重复 `Login`；登出仍调用已就绪实例的 `ResetUserInfo()`，日志上传路径不自动配置 |
| 统计事件、属性与归因查询 | `ITrackPlugin.TrackEvent(eventName, Dictionary<string, object>)`、`SetUserProperty(key, stringValue)`；归因走 `IAttributionPlugin.TrackEvent(...)`、`GetAttributionAsync(ct)` 与 `OnAttributionResolved`。事件/字段/目标从既有 Tracks 和项目触发点读取 | 所有调用由 Unity 主线程的项目入口发起；归因查询等待使用页面取消令牌，事件成对退订。统一登录通知已设置用户身份时不重复同步；不重复发送框架内置注册、登录或广告事件，不借一次查询触发额外上报 |
| Push Token 与 Topic | `IPushPlugin.GetTokenAsync(ct)`、`OnTokenRefreshed`；`SetTopicSubscribed(topic, subscribed)` 的 bool 明确为订阅/取消订阅，沿用当前业务 Topic | 先核对 Firebase 已有自动权限与标识上报，避免再次提示或重复上传；Token 只记录存在性。Topic 方法同步发起后台操作，没有成功返回值，不能据此认定服务端已订阅；页面结束取消等待并退订自身处理器 |
| 创建/取消业务 push task | `IFirebasePushTaskPlugin.QueuePushTaskAsync(new FirebasePushTask { TaskKey, TriggerTime, TemplateId, Cancel }, ct)`；`TaskKey` 非空且为业务唯一键，`TriggerTime` 为 Unix **秒**，`TemplateId` 来自当前服务端模板 | 返回 bool 只证明本地缓存写入；同键覆盖旧缓存。`Cancel=true` 只发送 `TaskKey/Cancel`，忽略时间与模板。`ct` 仅取消缓存等待，不能撤销已发送任务；保留原任务键及发送状态，断线先查原结果，不换键盲重发 |
| 刷新实验、读参、曝光与指标 | `DataMasterPlugin.SetUserProperty(key, objectValue)` 在本次拉取前设置；读参使用 `GetParamValue<T>(topicName, paramName, fallback)` 或 JSON 版本，`topicName` 取 `GetTopicNames()`/响应 Params 字典 key；真实展示时 `MarkExposure(topicName)`，指标用 `LogExperimentEvent(eventName, doubleValue, extraContext)` | 首次统一登录上下文已触发刷新时直接等 `WaitForRefreshAsync(ct)`；明确要求再次刷新且已有当前登录用户时调用 `Refresh()` 后再等待，核对 `DataMasterRefreshResult.Success/UserId`。等待本身不发起请求；不以再次 Login 补属性，不把 `experiment.topicId` 当主题名，不为重复任务清理缓存或新建账号 |

## 广告与支付

包前缀为 `com.solotopia.nova.framework.`。

| 包后缀 | 配置入口与按需文档 | 运行探针和边界 |
|---|---|---|
| `sdk.ad` + `sdk.max` | 顶层 `AdPluginConfig.ChannelConfigs` 内加入 `MaxAdChannelConfig`；父 `Nova/Doc/AdPluginConfig.md/IAdPlugin.md`，MAX `Nova/Doc/MaxAdPlugin.md/MaxAdPluginBuildProcessor.md` | `IAdPlugin` 请求结果、目标格式 Ready 和对应展示/关闭事件；MAX 包安装不证明广告位可用 |
| `sdk.admob` | 同父广告列表，内嵌 `AdMobAdChannelConfig`；`Nova/Doc/AdMobAdPlugin.md` | 独立官方 `com.google.ads.mobile` 四格式适配；asmdef 当前在 `[11.5.0,12.0.0)` 启用适配，包清单不直接安装官方 SDK。先核显式安装/配置/原生产物，源码与编译不代替真实 UMP、填充、奖励和收入 |
| `sdk.iap` + `sdk.iap.mobile` | 顶层 `IAPPluginConfig` 内 `Products/StoreConfigs`，加入 `MobileStoreConfig`；父 `Nova/Doc/IAPPlugin.md`，Mobile `Nova/DOCS/MobileStore.md/MobileInitService.md` | 目标 Store `IAPInitResult`、商品可用与真实平台回调分别核对；核心 Ready 不证明商品已获取 |
| `sdk.iap.thirdpay` | 同 IAP 列表内 `ThirdPayStoreConfig`；`Nova/Doc/ThirdPayStore.md/ThirdPayStoreConfig.md` | 登录后的国家与配置解析、平台资格、商品和订单结果；国家/支付页策略由服务端和平台裁决 |
| `sdk.iap.voucher` | 同 IAP 列表内 `VoucherStoreConfig`；`Nova/Doc/VoucherStore.md/VoucherStoreConfig.md` | 只读报价/钱包与扣减结果分别验收；默认不调用测试发放或扣减 |

首次接入先区分补内嵌引用与改现有字段：已注册的 IAP Mobile/ThirdPay/Voucher Store 可按共用契约使用 `nova.project.config.ensure-panel-references`，补齐后重新读取字段、patch 并导出。广告渠道不是顶层 SDK Config，该 Action 当前也不承诺补广告渠道；缺少内嵌渠道对象时核对包文档化 Inspector 和当前受控入口，没有可用入口则报告具体配置前提，不用任意对象 JSON、类型字符串或反射补齐。独立 AdMob 另核其包及官方 SDK 实际安装状态，既有 MAX 配置或广告模块业务验收不改变这些事实。

独立 AdMob 使用 `AppIdAndroid/AppIdIOS/TagForUnderAgeOfConsent` 和四类广告位列表，复用相同广告业务/Config Operation；其初始化依次执行 UMP 更新/必要表单、检查 `CanRequestAds()`、等待 `MobileAds.Initialize` 状态后注册四格式真实 ID；Editor / 非移动平台不自动请求测试广告。UMP 流程用 `WaitForPrivacyFlowAsync`，无法从官方状态取得明确个性化同意，通用授权 bool 保持不支持查询。全屏组表达式当前经兼容 `PlacementIds` 展开后注册单 ID 槽位，尚未保留 `RegisterAdGroups` 的组内串行拓扑。标准 Banner 只支持显示、隐藏、销毁和位置；刷新、宽度、背景方法不因接口存在就认定支持。官方 Settings 注入复用包内 Nova 构建处理器，不在业务重复写原生 Manifest/plist，设备未验收保持 `partial`。

MAX 配置读 `AppKey/AdMobAppIdAndroid/AdMobAppIdIOS`、启用状态和所需 `RVPlacementGroups/InterPlacementGroups/AppOpenPlacementGroups`、`BannerPlacementIds`；旧 `RVPlacementIds/InterPlacementIds/AppOpenPlacementIds` 是展开后的兼容真实 ID 快照。空广告位列表不靠重试补齐；构建处理器和官方 UPM adapters 自动处理的项复用既有 Player 构建，不复制到业务代码。MAX 保留配置组内串行拓扑，统一 `GroupRetryIntervalsSeconds` 默认 `2、4、8、16、32` 秒；只有整组失败推进下标，任一 ID 成功归零，末项后循环。AdMob 当前将组表达式展开为独立单 ID 组并行调度，不能继承 MAX 的组内串行承诺。广告普通业务统一 `Nova.SDK.Get<IAdPlugin>()`；先 `RequestAsync(format, ..., ct)` 并检查结果/`IsReady`。`ShowAsync` 返回 `UniTask`，结果从事件取得；Banner 预加载后走 `IBannerControl`，奖励按渠道和格式核对 `RewardGranted` 与 `UserCompleted`，前者表示奖励资格，后者表示原厂完成状态，二者均不证明业务已实际发奖；MAX 插屏展示成功即满足当前奖励条件，激励以官方奖励回调为准。订阅按生命周期释放。

广告渠道由 `SDKManager → AdPlugin` 按内嵌配置持有，复用 `IAdPlugin/IBannerControl` 与既有 UI、Event、Procedure、Config Operation，业务不重复 `new`、初始化或释放渠道，不为同一接口创建逐广告格式 Skill。新增渠道沿用 `AdChannelPluginBase` 的槽位、批次、重试、事件与释放链；独立可选 `IAdChannelLifecycle` 保持旧 `IAdInternalPlugin` 实现兼容，初始化须响应取消并实际退出任务。只实现旧内部接口的渠道仍可完成配置、桥接与登记并保持活跃，但不自动初始化。聚合先在主线程失效桥接身份、冻结拥有者快照并清空公开查询列表，随后取消生命周期；逐项等待准备和真实初始化退出，再准确退订七类事件并释放渠道，最后清事件缓冲、配置与令牌，重复释放共享同一完成结果。释放优先调用 `IAdChannelLifecycle.DisposeAsync(CancellationToken.None)`，未实现该接口时才调用 `IDisposable.Dispose()`；兼具二者不重复释放，二者均未实现的旧渠道须自管原厂资源。单项取消、退订或释放异常不跳过后续清理。当前 AdPlugin 聚合与 AdChannelPluginBase 渠道实例释放后不复用；完整释放后，新的 SDKManager 初始化周期重新创建聚合和渠道，业务重新获取插件并建立订阅。这是当前广告实现的生命周期边界，不推广为所有 ISDKPlugin 实现的统一限制。

页面 `ct` 不撤销已发起的原生广告动作。聚合 RequestAsync 自身提供业务等待取消；标准基类渠道的 ShowAsync 自带调用方和渠道生命周期取消，MAX/AdMob 的隐私等待响应传入令牌并在初始化退出或释放时结束。当前聚合 ShowAsync、WaitForPrivacyFlowAsync 直接等待渠道任务，旧自定义渠道若忽略令牌且不结算等待，页面取消或聚合释放不能保证这些等待结束。聚合初始化令牌只控制渠道准备与初始化等待，成功退出后再取消它不会关闭聚合事件桥接或取消后续请求；整轮结束须由统一所有者释放。页面关闭先取消自身会话、释放自己的订阅句柄，等待后核对当前会话再回写；SDK 整体释放交给统一所有者，页面不 `Clear()` 共享事件。MAX 初始化保留方法内的准确委托和局部 TCS：原厂 handler 只完成本轮等待，`AttachExternalCancellation(ct)` 后由 `SwitchToMainThread(ct)` 阻止取消后的状态更新，`finally` 精确退订；不另维护初始化 session 字段。释放先摘并清空展示完成源再取消，注销格式回调并销毁全部已创建 Banner，最后清国家码与授权缓存；不撤回已经发布的 SDK 数据或原生全屏展示。源审查与本地取消/退订探针不能替代设备广告显示、奖励回调、收入或厂商后台证据。

IAP 商品位于内联 `Products`，通过 ConfigWindow/Inspector 既有 `导出 SKU 模板`、`导入 SKU Excel` 批量维护。导入会全量覆盖当前列表，先核对源、行数、SKU/类型/价格/币种及删除差异；它不走 Luban Table 导出。随后保存 Master→导出目标 Runtime→核对 ProductTable 与 Store 消费。商品为空不能称支付接入完成。

自然语言导入已有商品工作簿使用[确定性只读转换](inline-products.md)，再复用 Config `patch-panels` 保存完整列表；不要每轮重写解析/导入代码或把转换结果当成已保存。

Mobile 六条 CmdName 使用当前 `MobileStoreConfig` 的 Google/Apple 查询、普通验单、订阅验单字段；ThirdPay 使用 `PaymentConfigCmdName/QueryPendingOrderCmdName/VerifyIapCmdName`；Voucher 使用 `GetVoucherListCmdName/DeductVoucherCmdName/TestGrantVoucherCmdName`。复用 Network Operation 完成正式路由与协议，不把默认指令名当成已部署服务。

支付统一 `IAPPlugin.PayAsync<T>` / `RestorePurchasesAsync<T>`，专属能力用 `TryGetCapability`，避免业务依赖内部 Store 或按商店堆转发。Editor `EnableAlwaysPaySucceed` 只验证模拟路径，正式发货依据服务端验单和幂等订单。支付、补单、Restore 可能改本地与服务端状态，接入探针默认不创建订单。

商品目录先沿本 Skill 的[内联商品维护](inline-products.md)维护，再正式保存并导出目标 Runtime。独立提交使用[商品目录上传](product-catalog-upload.md)的 `nova.project.iap.upload-product-catalog`；请求只有 `runtimeGuid + developMode`，冻结已保存 Master 的显式 `ExportTarget`、六文件与内存态、环境/客户端版本、完整规范目录及最终请求和固定桥身份。先核包内 `Nova/Doc/IAPProductCatalogProtocol.md`，再沿正式 Plan、精确外部授权、一次 Execute 与只读 Verify；不以未导出草稿、选中行或 Excel 筛选结果作为上传源。

旧 Inspector 与无参数 Step `iap.product_catalog.upload` 仍复用 `IAPProductCatalogUploader.UploadCurrentAsync(CancellationToken)`，执行时读取当前导出配置；通用 Batch 没有自动取得新 Action 的目录预冻结。精确 HTTP 409 且 `code=CATALOG_VERSION_CONFLICT` 仍 Warning 后正常返回并表示新目录未应用，Step/Batch 成功不表示目录生效；不自动改版本或重传。新 Action 即使收到 2xx 也最高 `partial`，没有权威后台应用契约就不称落库成功；错误、取消、重载和仅有发送意图均不自动重发。上传只使用 App AES，不输出凭据、Body 或原响应，也不执行支付、补单和发货；目录结果不升级 `sdk-kit.iap.business` 整任务。

ThirdPay 奖励透传使用 `IAPThirdPayRequest.FirstRechargeReward`、`ExtraBonusReward` 和 `IAPThirdPayRewardItem`，只影响本次支付 URL，不替代服务端验单或幂等发货。商品 Proto 的 tag 重排兼容边界以包内 `ThirdPayStore.md` 为准；本地生成类匹配不证明已部署服务端或旧客户端兼容。Mobile 的 Android 前台恢复检查、已有购买验单及确认继续复用包内 Store 生命周期，不在业务 View 再建恢复轮询或重复确认。

业务 View 的支付和查询在开始时保存所属会话与取消令牌，等待后、异常反馈和按钮恢复前核对页面仍打开且所有者未变；关闭时先取消本会话，再清除模块引用。GameLogin 当前没有页面取消参数，旧结果只停止页面回写，不撤销身份请求或支付订单。复用导入的 `IAPDemo/README.md` 与 Sample 实现核对关闭、立即重开和旧任务结束；这些 C# 业务 API 的存在不代表已注册对应 MCP Action，真实平台与履约证据仍需单独取得。

## 统计、归因、推送、客服与 ABTest

| 包后缀 / 公开能力 | 顶层 Config 的关注项 | INDEX 后按需文档与探针 |
|---|---|---|
| `sdk.tga` / `ITrackPlugin`、`IDeviceIdProvider` | `TGAPluginConfig.AppID/ServerCmdName/ReportCmdName/Mode/TimeZone/IsTestUser/AssignDeviceIdToDistinctId` | `Nova/Doc/TGAPlugin.md/TGAPluginConfig.md`；Ready、地址解析与设备/用户标识已发布，仅报告非空状态 |
| `sdk.appsflyer` / `IAttributionPlugin` | `AppsFlyerPluginConfig.DevKey/AppId/ReportCmdName`、日志与 OneLink 相关项 | `Nova/Doc/AppsFlyerPlugin.md`；目标 Ready、归因完成/失败信号；Editor mock 不证明真实归因 |
| `sdk.firebase` / Analytics、`IPushPlugin`、push task | `FirebasePluginConfig.ReportCmdName/PushCmdName/PushFlushIntervalSeconds/PushFlushBatchSize/AutoRequestNotificationPermission` | `Nova/Doc/FirebasePlugin.md/FirebasePluginConfig.md`；依赖就绪与初始化信号，通知权限、Token 和真实送达分别验证 |
| `sdk.aihelp` / 客服、语言和用户资料 | `AIHelpPluginConfig.ServerCmdName/AppId/InitialLanguage/EnableLogging` | `Nova/Doc/AIHelpPlugin.md`；初始化信号和非空 SDK 版本；已授权展示才验证 Show/IsShowing |
| `sdk.datamaster.abtest` / `DataMasterPlugin` | `DataMasterPluginConfig.AppId/AesKey/DefaultConfig` 与六项网络策略 | `Nova/Doc/DataMasterPlugin.md`；默认配置读取、`WaitForRefreshAsync(ct)` 的结果与账号；真实实验命中需要设备 |

TGA、AIHelp 和各服务上报 CmdName 先从正式 HostKey/NetCmd 解析。AppsFlyer 当前通过 `ISDKInitializationDependencies` 要求 `ITrackPlugin`，还需对应设备/访客数据；不按 Priority 猜顺序、不为了归因包强行装另一项业务包。当前源的配置失败会进入 Failed；包文档若仍写“早退即可用”或“内部吞异常”，应核对匹配版本的 `OnInitializeAsync`，不按旧说明验收。

Firebase 的真实 Analytics/Messaging 限 Android/iOS，WebGL 不编译插件。默认 push 缓存阈值为 100 秒/5 条，`AutoRequestNotificationPermission=true` 可能在初始化后触发系统权限弹窗；接入前读当前项目策略并冻结是否允许触发。权限、Token 生成和消息送达不同，Token 不回显、不为验收自动订阅主题或发送 push task。原生服务文件与后台配置按目标包和平台文档核对。

DataMaster 直接启用 Config 类型，无需 SDK 能力族选型。首次拉取需要业务属性时使用统一 `Login(uid, properties)` 一次交付；默认网络策略为 `FeatureFallbackRounds=1/FeatureRetries=1/ReportFallbackRounds=1/ReportRetries=1/RequestTimeoutSeconds=10/PreferLastSuccessfulHost=true`，完整轮数和额外重试不同。Editor 只验证默认值，不证明远端实验命中。`AesKey` 通过包文档化安全入口配置，不进入计划、脚本或日志。

AIHelp 当前不实现 `ISDKLoginSessionEndReceiver`，项目登出除统一 `EndLoginSession()` 外仍需对已就绪实例调用 `ResetUserInfo()`；该业务生命周期修改交给匹配 Operation。AppsFlyer 会话结束只阻止旧账号待完成上报，不清原厂 Customer User ID/归因缓存；按包公开接口与项目策略核对，不推断统一登出已清理所有服务状态。

新增埋点复用 `ITrackPlugin/IAttributionPlugin` 和项目既有事件路径，读取现有 Tracks 登记与字段归属，不把请求点击当广告素材点击，不重复上报统一注册/登录。客服只在项目已有入口使用公开 Plugin；不直接引入原厂 API。

登记和汇总按[Tracks 维护](tracks-maintenance.md)分清权威 Workbook、业务 source、已有生成接口与尚无受控执行器的范围。

## 通知权限、应用内评价与系统设置

这些能力属于已安装 Framework 的 Native 模块，没有需要另装的 Native SDK/Kit。直接读取 `Docs/Runtime/Modules/Native/NativeComponent.md` 与 `Managers/Interfaces/INativeManager.md`；当前门面为 `NovaFramework.Runtime.Nova.Native`，实现契约为 `INativeManager`。`NativeManagerConfig.EnableAndroidAppInstall` 默认关闭，仅控制可选自身 APK 安装；通知与评价参数仍由显式 API 提供。保留已有 `NativeComponent` 与框架启动/关闭链，不重复创建 Manager 或调用 Initialize。

用户要求“通知按钮”“评价入口”“打开设置”时，先核对已有业务页面/流程、调用时机、目标平台与真实系统请求是否获授权。复用当前业务 source 编辑及 UI/Event/Procedure 绑定 Operation；不要求 ConfigMaster、SDK Config 或额外包管理步骤。只做本地接入时不通过运行按钮触发系统提示、评价或设置跳转。

| 用户目标 | 唯一公开调用与结果边界 |
|---|---|
| 查询通知状态 | `GetNotificationPermissionStatusAsync(ct)`；系统状态为 `Unknown/Unsupported/NotDetermined/Denied/Authorized/Provisional/Ephemeral`，本地缓存不能替代当前状态 |
| 显式请求通知权限 | `RequestNotificationPermissionAsync(options, ct)`；默认 `Alert \| Sound \| Badge`，可选 iOS `Provisional`，`None` 或未知位拒绝。`IsOperationSuccessful` 表示系统请求流程完成，拒绝也可为 true；授权看独立 `Status` |
| 请求应用内评价 | `RequestInAppReviewAsync(ct)`；`Unsupported/Unavailable/RequestDispatched/Failed`。`RequestDispatched` 仅交给系统，不证明弹窗展示、用户评价或提交；业务决定显式触发与频率，不在启动或页面打开时自动请求 |
| 打开应用设置 / 精准通知设置 | `OpenAppSettingsAsync()` / `OpenNotificationSettingsAsync()`；bool 仅证明跳转请求发起。精准通知设置不支持时 false，不自动回退应用根设置；返回后从已确认业务入口重新查询权限 |

框架初始化不主动查询/请求权限或评价，但 Firebase 的 `AutoRequestNotificationPermission` 可能在其初始化后请求；先核对现有策略，不能用 Native 分支重复提示。相同 options 的并发权限请求共享底层调用、不同 options 串行；评价并发共享。`ct` 只取消当前等待，不撤销已发起系统请求；页面关闭/账号切换后不回写旧 UI，也不自动重试。两种设置跳转 API 没有 ct，保留其真实签名；业务在 await 后检查当前会话。统一 Manager Shutdown 取消 pending、丢弃迟到回调，不把“等待取消”当系统动作撤销。

Editor/非移动平台返回真实 Unsupported，设置返回 false。Android 通知请求 API 33+，应用通知总开关 API 24+，精准通知设置 API 26+；iOS 精准通知设置 15.4+，评价 16+ 且需前台活动场景。平台物料与原生桥接按 Framework 当前 Native 文档和构建处理器核对：Android `POST_NOTIFICATIONS`，iOS `UserNotifications.framework/StoreKit.framework`；这不证明 APNs、Push capability 或真实消息送达已配置。

本地业务 source、绑定、编译及 Unsupported 回退可逐层验收；真机权限状态、跳转及原生请求须由明确设备/系统版本和授权交互取得证据。系统本身不提供的评价展示/提交证据不能被列成可自动验证指标。这里的 C# API 不是可直接调度的 Nova Action；没有现有可达业务入口时按已授权项目范围建立正式入口，不编临时任意代码执行器。

## 继续执行与结果

参数与内联 Excel 归 Config Operation；网络协议归 Network Operation；业务按钮、订阅、支付发货/存档生命周期分别归 UI、Event、Procedure、Persistence Operation。编译或启动失败先诊断最早失败阶段；Android 原生依赖复用 Resolve，真正打包交给 Build Player。设备与厂商后台必需项缺失时报告本地 `partial`，保留明确待验收项；不把 Ready、模拟支付、默认参数或一次无异常日志标成后台成功。


## Android 自身 APK 下载与显式安装

下载属于 App 的 `Nova.App.DownloadAsync(ct)`，返回 persistentDataPath 下完整文件路径；内置下载仅 Android Player，HTTPS 主备各一次、取消止链、临时文件验证后原子提交。下载路径不表示签名有效或安装完成。安装是 Native 的可选 `INativeAppInstaller`，旧 `INativeManager` 不新增必实现成员；业务调用 `Nova.Native.InstallApkAsync(path, ct)`，详见 Native 的 `Managers/Interfaces/INativeAppInstaller.md`。

先复用 edit-settings 读取并保存项目自有启动 NativeComponent 的 `m_NativeManagerConfig.EnableAndroidAppInstall`，默认 false，开启后需重新构建；不能直接改安装包或 Framework 快照。APK 文件只接受 persistentDataPath 内绝对路径，API28+，严格有效签名、同包名/当前 signer 集合、递增 versionCode；拒 split 与 signer 轮换，不绑定远端 Check 命中版本。来源许可不足返回 PermissionRequired；用户明确操作后可调用 OpenPackageInstallSettingsAsync，返回后用户重试安装。PackageInstaller 的 pending user action只是系统确认界面，等待最终结果；不能承诺静默安装、自动重启或取消后回滚已完成安装。

内置 ProcedureAppDownload 保存返回路径，安装失败/取消重试复用该文件，默认不开启安装时显示文件已保存结果。现有项目需要补 LauncherDialogType.AppDownloaded=4 的 Title/Content/Confirm 文本绑定；不能只改 JSON 或枚举。Framework 自有 androidlib 与 apksig 是平台交付输入，Java编译/D8不替代完整 Gradle/R8/最终 Manifest/真机安装结果；目标设备验证复用 build-player 平台CLI与device-runtime能力，不新增顶层 Skill。
