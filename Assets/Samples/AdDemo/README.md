# AdDemo

本 Sample 展示广告聚合插件的请求、就绪查询、全屏展示与 Banner 控制，业务 View 为 `NovaFramework.Sdk.Ad.Samples.Runtime.DemoAdView`。当前渠道选项固定为 MAX。独立 AdMob 包已有 UMP、原厂初始化、四格式加载、展示及 Ready 实现，但本 Sample 未接入该渠道；其官方 SDK 安装条件与展开 ID 的调度限制见独立包文档。

## 先登录，再操作广告

页面打开时只显示交互区的“登录”按钮，广告渠道、请求、就绪检查、展示及 Banner 控件全部隐藏。点击登录会调用 `Nova.Network.Kit<Login>().Async(string.Empty, string.Empty, false)`；成功后隐藏登录按钮并显示广告控件，失败时保留登录按钮供重试。标题栏与反馈区仍可使用。关闭再打开页面需要重新点击登录；关闭页面不会取消 GameLogin 的身份请求，旧页面的迟到结果不会修改新页面。

本示例需要安装 `com.solotopia.nova.framework.kit.network.gamelogin`，可从 `Nova → Open PlugPals → 外网仓库` 安装。该依赖在 Sample 描述符和 Runtime asmdef 中声明，不是广告基础包的强制依赖；直接通过 Unity Package Manager 导入 Sample 时仍须确认已经安装。`ConfigMaster` / `ConfigRuntime` 的 `LoginKitConfig.LoginCmdName` 使用 `GameAccountLogin`，对应 NetworkCmd 表源、正式 JSON 和生成访问属性中的 `HTTP_POST /v1/user/loginV1`；沿用现有 `GameServer`。Prefab 的 `m_LoginButton` 已绑定 `LoginButton`，广告交互区默认隐藏。广告 SDK 初始化仍由现有预加载流程负责。维护预制体时，登录按钮须正确赋给 `m_LoginButton`，且为 `m_InteractionRoot` 的直接子节点；代码以这个关系互斥显示登录按钮和其他广告控件，不能只新增脚本字段而漏掉 Prefab 绑定。

## 广告组配置

在广告聚合面板的 MAX 中，`RVPlacementGroups`（激励）、`InterPlacementGroups`（插屏）、`AppOpenPlacementGroups`（开屏）都是 List，每项是一组，项内用英文逗号 `,` 或竖线 `|` 分隔串行尝试的真实 ID，两种分隔符同义。没有广告组选择模式。

例如列表 `["A1,A2,A3", "B1|B2"]` 表示两个组，Inspector 中分别填写 `A1,A2,A3` 和 `B1|B2`；首次 A1 / B1 并发请求。A1 失败推进 A2，A2 失败推进 A3，任一项成功停止本组后项并归零该组间隔下标；B 组独立运行。整个组本轮全部 ID 失败后，才按 `AdPluginConfig.ChannelConfigs.GroupRetryIntervalsSeconds` 等待并从首项开始下一轮。全部渠道与格式共用同一序列，各组独立计数，到末项后循环回首项，默认等待 `2、4、8、16、32、2……` 秒；空序列或任意非有限正数使整个序列回退默认值。

示例名称需要替换成后台真实 ID，不把示例 ID 或整个 JSON 数组填入正式配置。解析时 Trim 并过滤空项；同一格式内重复 ID、分号、中文分隔符和 JSON 结构会报错，不自动去重。旧 Sample 单 ID 列表每项自然成为单 ID 组；`BannerPlacementIds` 仍是每项一个真实 ID，并沿用 MAX 原厂自动刷新，不使用全屏组串行策略。

配置源为 [Editor/ConfigMaster.asset](./Editor/ConfigMaster.asset)，正式 Config 导出物为 [Configs/ConfigRuntime.asset](./Configs/ConfigRuntime.asset)。旧序列化字段由 `FormerlySerializedAs` 迁移。编辑时选择目标 Platform / Channel / DevelopMode 坐标，保存 Master，再正式导出该坐标的 Runtime 配置，核对原始组串、展开真实 ID 与全局间隔秒数；配置导出受 Unity Active BuildTarget 门禁约束。

当前 Runtime 配置使用启用的 MAX 渠道，激励、插屏和 Banner 各保留一个真实 ID，开屏列表为空。Master 中广告聚合面板当前按 Platform 取数（`ByPlatform=1`，`ByChannel=0`，`ByDevelopMode=0`）；修改时仍应核对当前编辑坐标和实际导出结果。正式配置不包含示例多组广告 ID。

## 广告收益上报

MAX Banner 后台协议与 ad_ilrd 共用一份累计和存档，当前阈值为 5：前四次保存，第五次在 `TrackMaxAdIlrd` 派发时上传同一批总金额，广告位取本次回调 ID。全部打点目标不可用时保留累计，后台请求同步等待；恢复后只随一次 ILRD 派发触发协议。批次清理不等待网络确认，网络失败只记录警告。AdMob 尚无独立分析 ad_ilrd，在 Paid 处显式使用共享累计入口。非 Banner 不受阈值影响，ad_impression 和原始收益通知仍逐次触发。协议条数按批次计，不携带曝光次数。

收益上报与广告加载、预载相互独立。当前 Sample 初始化完成后不会自动请求 Rewarded、Interstitial 或 AppOpen，Banner 也不会因初始化自动创建；业务仍需显式调用 `RequestAsync`，再按事件和 `IsReady` 结果决定展示。

当前 [Editor/ConfigMaster.asset](Editor/ConfigMaster.asset) 的广告全局配置已将 `m_RevenueReportCmdName` 设置为 `AdRevenueReport`。渠道在自己的收益派发入口显式调用基类 `StartRevenueReport`：MAX 为 `TrackMaxAdIlrd`，AdMob 为 `TrackAdMobRevenue`。基类生成稳定的 `report_id`，由 `Services/AdRevenueReportNetService.cs` 发送 `PbNetIAAReportReq`；业务页面无需直接调用网络接口。`RaiseRevenue` / `RaiseRevenueImmediately` 只通知原始收益。

请求中的字段映射为：`AdFormat.ToString()` → `iaa.ad_type`，`AdChannelType.ToString()` → `ad_channel`，真实广告位 → `iaa.ad_id`，美元收益 → `iaa.price_usd`。协议类型固定为 `PbIAAReportData`、`PbNetIAAReportReq` 和 `PbNetIAAReportResp`，客户端命令名使用 `AdRevenueReport`，不改写为太极 V2 类型名；服务端路由仍以协议源注释的 `POST /v1/taijiv2/report` 为准。

`AdPlugin.Events.RevenuePaid` 只是 Unity 主线程收益业务通知，表示收益事件已经进入广告聚合层，不表示网络请求已成功或服务端已经计入。网络失败、业务 `error_code`、`duplicate` 等结果以收益上报 Service 日志和响应为准；旧资产将 `m_RevenueReportCmdName` 留空时保持兼容并跳过自动上报。

安装包按实际日志顺序核对：“广告收益上报配置 / 未启用”（Info）→“收到广告收益 / 已跳过”（Debug）→“广告收益上报参数”（Debug）→“广告收益上报网络结果”（Debug）。当前没有单独的 Info“准备发送”日志；关闭 Debug 时只看到初始化 Info 和异常 Warning，并不意味着协议一定没有发出。响应 JSON 可能省略 false / 0，外层状态和 `data.iaa_report_id / utc_day / duplicate / error_code` 需分开核对。MAX 原厂日志开关不能替代 Nova Debug 与 Player 日志配置。旧字段自动迁移；更新 Runtime 后还须更新设备实际使用的配置 Bundle、首包或热更新版本。完整发送与错误边界见[收益协议与日志](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.ad` 包内 `Nova/Doc/INDEX.md#广告收益上报-proto-协议层`）。

## 独立协议测试页

主仓根目录的 `AdRevenueReportTest.html` 与 `AdRevenueReportTestServer.cjs` 是维护者本地协议工具，不属于 Sample 或 UPM 包的运行依赖。页面固定测试身份与应用 AES 参数，广告类型绑定生成时 `ConfigRuntime` 中的真实 ID；这是配置快照，修改运行时配置后不会自动刷新 HTML。每次点击发送都是新 report_id，没有自动重试；页面参数预览是 JSON，实际发送与客户端一致的加密 Protobuf，并解密解析统一外层及收益响应。

浏览器页面通过同源本地服务原样转发请求字节和客户端业务头；本地文件会跳转到测试服务。部署到网站时须部署同源转发，或由收益接口允许对应域名及 `POST / OPTIONS` 和业务请求头，不能仅靠移动 HTML 或 `no-cors` 解决。Key / IV 写在源码中不代表浏览器不可读取；测试页不要作为公开配置或正式账号入口。维护者工具的固定身份、端口和本地服务运行状态不属于广告模块契约。

独立页面能检查路由、加密和收益协议响应，不能证明 MAX 原厂 ILRD 已触发、Banner 的五次累计已执行、Demo 登录或真机广告链路已通过。原始日志、客户端发包、业务响应和服务端计入仍需按各自阶段核对。

## 入口与依赖

[AdDemo.unity](./AdDemo.unity) 是本地演示场景。`ProcedurePreload` 按顺序加载 Persist、Localization、Network，等待 `Nova.SDK.InitializeTask`，再加载 Table、UI、Sound、Vibrate，之后进入 `ProcedurePlaying` 并通过 `Nova.UI.OpenUIViewAsync<DemoAdView>()` 打开页面。

当前源码通过 `Nova.SDK.TryGet(out AdPlugin adPlugin)` 获取聚合插件；找不到时在反馈区提示不可用。等待聚合 SDK 初始化任务结束，仅表示配置和聚合初始化步骤已执行，不能替代 MAX 原厂初始化或 `IsReady(format)` 检查。

当前仓内包清单为 `sdk.ad@1.2.2` 与 `sdk.max@0.2.1`；MAX 包依赖 `com.applovin.mediation.ads@8.6.6` 及其原厂适配器，实际原生依赖由消费工程解析。iOS 接入前还须核对 [MAX 构建处理器](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.max` 包内 `Nova/Doc/MaxAdPluginBuildProcessor.md`） 中适配器声明与 Pod/SPM 归属脚本的一致性，当前版本差异不能用 Editor 初始化成功代替。本 Sample Runtime asmdef 为 `NovaFramework.Sdk.Ad.Samples.Runtime`，`autoReferenced=false`，排除 WebGL 并声明 `!UNITY_WEBGL`。本 Sample 不提供 WebGL 广告运行验证。

## 业务调用与参数

页面按钮采用以下调用；请求和展示都使用页面本次打开期间的 `m_AdViewCts.Token`：

| 操作 | 当前调用 | 反馈含义 |
|---|---|---|
| 请求激励 / 插屏 / 开屏 | `RequestAsync(format, customProps, token)` | 返回 `AdLoadResult`；页面按 `Success` 显示成功或本轮失败 |
| 查询激励 / 插屏 / 开屏就绪 | `IsReady(format)` | 返回当前任一渠道是否有该格式可播广告 |
| 展示激励 / 插屏 / 开屏 | `ShowAsync(format, customProps, token)` | 返回 `UniTask`，页面只显示“播放调用完成”；实际展示与奖励看事件 |
| 请求 Banner | `RequestAsync(AdFormat.Banner, customProps, token)` | 成功后记录预加载候选，不改变已显示实例归属 |
| 显示 / 隐藏 / 销毁 Banner | `TryShowBanner()` / `HideBanner()` / `DestroyBanner()` | 首次显示重新选择就绪渠道，之后固定控制该实例；Hide 保留归属，Destroy 清除 |

`BuildAdCustomProps` 每次构造新的 `Dictionary<string, object>`，字段如下：

| key | value |
|---|---|
| `debug_ui` | `true` |
| `debug_ad_channel` | 当前 `m_SelectedAdChannel.ToString()`，本 Demo 为 `"MAX"` |
| `debug_format` | `format.ToString()`，如 `"Rewarded"`、`"Interstitial"`、`"Banner"`、`"AppOpen"` |
| `debug_custom_data` | 对应格式输入框的非空原始文本；空文本或输入框缺失时不写此 key |

`debug_ad_channel` 是调试属性，不是渠道路由参数；下拉框回调固定选择 MAX，实际请求仍由聚合层广播到全部已注册渠道，格式未配置等条件由各渠道返回失败。展示按全局 `EnableBidding` 选择收益最高的就绪渠道，关闭竞价则按渠道配置顺序选择首个就绪渠道。`customProps` 用于渠道广告打点追加属性；`debug_custom_data` 不是已实现的原厂回传参数或奖励凭据，也不替代广告位 ID。

本轮所观察的全部有效广告组失败会返回失败，已启动的组继续后台重试；配置错误、格式未配置或渠道不可用不代表存在正在运行的组。单次 `AdLoadFailed` 只代表某个 ID 的尝试失败，后续 ID 仍可能继续成功。

任一全屏广告仍在原生展示时，新全屏 `ShowAsync` 会发布 `ShowFailed`（`Busy(-12)`）并正常返回；没有就绪渠道时会记录警告并跳过。因此页面的“播放调用完成”或 Banner 按钮反馈不能作为原厂广告已展示的证明。

Banner 显示按钮仅在 `TryShowBanner()` 返回 true 时提示“已发出 Banner 显示调用”；返回 false 时给出警告，不再无条件提示成功。首次请求失败后的后台重试或取消等待后加载成功仍可由该按钮发现。MAX / AdMob 只有首个配置 Banner ID 就绪才可被选择，其他 ID 的原厂 loaded 日志不能证明该首 ID 或聚合显示归属已准备好。

## 事件与生命周期

页面 `OnOpen` 建立等待令牌并订阅以下 `AdPlugin.Events`；`OnClose` 取消、释放令牌并成对注销订阅：

| 事件 | payload | 页面观察的实际语义 |
|---|---|---|
| `InitResult` | `bool` | 渠道初始化结果的全局通知；Sticky 补发最近结果 |
| `AdLoaded` | `AdLoadResult` | 一个真实 ID 已加载且可播；Sticky 补发最近成功 |
| `AdLoadFailed` | `AdLoadResult` | 一个真实 ID 尝试失败；Sticky 补发最近失败 |
| `ShowCompleted` | `AdResult` | 原厂 displayed 成功；不表示广告已关闭或激励已完成 |
| `ShowFailed` | `AdResult` | 展示失败，包括全屏 Busy |
| `AdClosed` | `AdResult` | 原厂关闭终态，包含渠道设置的 `RewardGranted` |
| `RevenuePaid` | `AdEvent` | 广告收益业务通知，反馈格式、真实广告位 ID 与收益 |

后四项使用容量 32 的 Replay 事件，重新打开页面可能补收到历史通知；前三项使用 Sticky 事件，不能把补发记录当作本次按钮操作的新结果。全局事件并非本次请求专属，也没有用 `debug_ad_channel` 过滤。

MAX 激励的 `RewardGranted` 以官方奖励回调为准，关闭结果保留该状态；MAX 插屏展示与关闭结果的 `RewardGranted=true`，开屏为 `false`。本 Demo 只显示结果，没有业务发奖逻辑，不能仅凭 `ShowCompleted` 或 `ShowAsync` 返回为激励发奖。

关闭 Demo 页面只取消业务等待和事件订阅，不取消已经发出的原生 Load / Show，也不释放聚合插件或终止渠道后台重试。全屏占用保持到真实关闭或展示失败终态，不能通过取消页面等待立即释放。

请求和展示在开始时固定本次打开的会话与令牌，等待返回或进入异常反馈前检查页面仍可用、令牌未取消且会话未被替换。关闭后立即重开时，旧任务不会向新页面追加成功、失败或取消反馈。请求失败反馈只报告本轮结果，不将配置缺失或渠道不可用误写为后台重试已运行。

聚合插件和渠道由 SDKManager 统一持有，每个实例只服务一轮生命周期。完整释放后，新的 Manager 初始化周期创建新的聚合与渠道；业务重新获取插件并订阅事件，不对已释放对象再次初始化。当前 Demo 页面关闭、重开复用的是仍存活的聚合插件，不会重启整个 SDK 生命周期。

## 验证边界与相关文档

真实填充、断网、切后台、过期广告、全屏互斥、收益和奖励结果需要 Android / iOS 真机及有效广告后台配置验证。编辑器中的配置保存、静态源码对照和按钮调用日志不能替代这些验证；开屏当前未配置真实 ID，不具备本 Sample 的开屏播放验收条件。

- [广告聚合文档索引](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.ad` 包内 `Nova/Doc/INDEX.md`）
- [广告聚合配置与统一间隔](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.ad` 包内 `Nova/Doc/AdPluginConfig.md`）
- [全局广告事件](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.ad` 包内 `Nova/Doc/AdPluginEvents.md`）
- [MAX 渠道文档](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.max` 包内 `Nova/Doc/MaxAdPlugin.md`）
- [MAX 构建处理器](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.max` 包内 `Nova/Doc/MaxAdPluginBuildProcessor.md`）
- [独立 AdMob 当前状态](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.admob` 包内 `Nova/Doc/INDEX.md`）
- [Demo 业务调用源码](./Scripts/Runtime/UIs/DemoAdView/DemoAdView.Methods.cs)

导入路径重写、旧版本目录清理、Sample 基类与启动流程的共同边界，见已安装 Framework 的 [Docs/Onboarding/PROJECT_STRUCTURE.md](README.md#跨包文档定位)（`com.solotopia.nova.framework` 包内 `Docs/Onboarding/PROJECT_STRUCTURE.md`） 中“Sample 的导入、辅助工具与页面边界”；本页只补充当前示例的差异。

## 启动配置等待

`ProcedurePreload` 在网络表加载成功后，等待本次启动的同一轮 GM 应用配置拉取，再继续 SDK 等后续步骤；拉取失败、超时或未配置时继续使用当前配置。仍保留原有八步顺序，预加载重试和重复等待不会重新发起启动拉取。Network Inspector 的“启动时GM配置请求超时时间（秒）”默认每次请求 5 秒，主备请求和重试分别计时，整轮总耗时可能超过 5 秒。

## 跨包文档定位

本页链接相对于当前文件；独立包之间不假定相邻安装。先用 `UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()` 按下表精确包名查找，以该包的 `resolvedPath` 为根读取文档入口，再按包内相对链接进入目标。无 Editor 通道时使用当前有效 manifest 或解析工具给出的实际包位置。

| 包名 | 包内文档入口 | 本文引用的包内位置 |
|---|---|---|
| `com.solotopia.nova.framework` | `Docs/INDEX.md` | `Docs/Onboarding/PROJECT_STRUCTURE.md` |
| `com.solotopia.nova.framework.sdk.ad` | `Nova/Doc/INDEX.md` | `Nova/Doc/AdPluginConfig.md`<br>`Nova/Doc/AdPluginEvents.md`<br>`Nova/Doc/INDEX.md`<br>`Nova/Doc/INDEX.md#广告收益上报-proto-协议层` |
| `com.solotopia.nova.framework.sdk.admob` | `Nova/Doc/INDEX.md` | `Nova/Doc/INDEX.md` |
| `com.solotopia.nova.framework.sdk.max` | `Nova/Doc/INDEX.md` | `Nova/Doc/MaxAdPlugin.md`<br>`Nova/Doc/MaxAdPluginBuildProcessor.md` |
