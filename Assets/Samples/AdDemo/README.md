# AdDemo

本 Sample 展示广告聚合插件的请求、就绪查询、全屏展示与 Banner 控制，业务 View 为 `NovaFramework.Sdk.Ad.Samples.Runtime.DemoAdView`。当前渠道选项固定为 MAX。独立 AdMob 包已有 UMP、原厂初始化、四格式加载、展示及 Ready 实现，但本 Sample 未接入该渠道；其官方 SDK 安装条件与展开 ID 的调度限制见独立包文档。

## 广告组配置

在广告聚合面板的 MAX 中，`RVPlacementGroups`（激励）、`InterPlacementGroups`（插屏）、`AppOpenPlacementGroups`（开屏）都是 List，每项是一组，项内用英文逗号 `,` 或竖线 `|` 分隔串行尝试的真实 ID，两种分隔符同义。没有广告组选择模式。

例如列表 `["A1,A2,A3", "B1|B2"]` 表示两个组，Inspector 中分别填写 `A1,A2,A3` 和 `B1|B2`；首次 A1 / B1 并发请求。A1 失败推进 A2，A2 失败推进 A3，任一项成功停止本组后项并归零该组间隔下标；B 组独立运行。整个组本轮全部 ID 失败后，才按 `AdPluginConfig.ChannelConfigs.GroupRetryIntervalsSeconds` 等待并从首项开始下一轮。全部渠道与格式共用同一序列，各组独立计数，到末项后循环回首项，默认等待 `2、4、8、16、32、2……` 秒；空序列或任意非有限正数使整个序列回退默认值。

示例名称需要替换成后台真实 ID，不把示例 ID 或整个 JSON 数组填入正式配置。解析时 Trim 并过滤空项；同一格式内重复 ID、分号、中文分隔符和 JSON 结构会报错，不自动去重。旧 Sample 单 ID 列表每项自然成为单 ID 组；`BannerPlacementIds` 仍是每项一个真实 ID，并沿用 MAX 原厂自动刷新，不使用全屏组串行策略。

配置源为 [Editor/ConfigMaster.asset](./Editor/ConfigMaster.asset)，正式 Config 导出物为 [Configs/ConfigRuntime.asset](./Configs/ConfigRuntime.asset)。旧序列化字段由 `FormerlySerializedAs` 迁移。编辑时选择目标 Platform / Channel / DevelopMode 坐标，保存 Master，再正式导出该坐标的 Runtime 配置，核对原始组串、展开真实 ID 与全局间隔秒数；配置导出受 Unity Active BuildTarget 门禁约束。

当前 Runtime 配置使用启用的 MAX 渠道，激励、插屏和 Banner 各保留一个真实 ID，开屏列表为空。Master 中广告聚合面板当前按 Platform 取数（`ByPlatform=1`，`ByChannel=0`，`ByDevelopMode=0`）；修改时仍应核对当前编辑坐标和实际导出结果。正式配置不包含示例多组广告 ID。

## 入口与依赖

[AdDemo.unity](./AdDemo.unity) 是本地演示场景。`ProcedurePreload` 按顺序加载 Persist、Localization、Network，等待 `Nova.SDK.InitializeTask`，再加载 Table、UI、Sound、Vibrate，之后进入 `ProcedurePlaying` 并通过 `Nova.UI.OpenUIViewAsync<DemoAdView>()` 打开页面。

当前源码通过 `Nova.SDK.TryGet(out AdPlugin adPlugin)` 获取聚合插件；找不到时在反馈区提示不可用。等待聚合 SDK 初始化任务结束，仅表示配置和聚合初始化步骤已执行，不能替代 MAX 原厂初始化或 `IsReady(format)` 检查。

当前仓内声明为 `sdk.ad@1.1.16` 与 `sdk.max@0.1.13`；MAX 包依赖 `com.applovin.mediation.ads@8.6.4` 及其原厂适配器，实际原生依赖由消费工程解析。本 Sample Runtime asmdef 为 `NovaFramework.Sdk.Ad.Samples.Runtime`，`autoReferenced=false`，排除 WebGL 并声明 `!UNITY_WEBGL`。本 Sample 不提供 WebGL 广告运行验证。

## 业务调用与参数

页面按钮采用以下调用；请求和展示都使用页面本次打开期间的 `m_AdViewCts.Token`：

| 操作 | 当前调用 | 反馈含义 |
|---|---|---|
| 请求激励 / 插屏 / 开屏 | `RequestAsync(format, customProps, token)` | 返回 `AdLoadResult`；页面按 `Success` 显示成功或本轮失败 |
| 查询激励 / 插屏 / 开屏就绪 | `IsReady(format)` | 返回当前任一渠道是否有该格式可播广告 |
| 展示激励 / 插屏 / 开屏 | `ShowAsync(format, customProps, token)` | 返回 `UniTask`，页面只显示“播放调用完成”；实际展示与奖励看事件 |
| 请求 Banner | `RequestAsync(AdFormat.Banner, customProps, token)` | 成功后由聚合层记录活跃 Banner 渠道 |
| 显示 / 隐藏 / 销毁 Banner | `ShowBanner()` / `HideBanner()` / `DestroyBanner()` | 无参数同步转发；无活跃 Banner 渠道时不执行原厂操作 |

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

- [广告聚合文档索引](../../../UPMPackages/com.solotopia.nova.framework.sdk.ad/Nova/Doc/INDEX.md)
- [广告聚合配置与统一间隔](../../../UPMPackages/com.solotopia.nova.framework.sdk.ad/Nova/Doc/AdPluginConfig.md)
- [全局广告事件](../../../UPMPackages/com.solotopia.nova.framework.sdk.ad/Nova/Doc/AdPluginEvents.md)
- [MAX 渠道文档](../../../UPMPackages/com.solotopia.nova.framework.sdk.max/Nova/Doc/MaxAdPlugin.md)
- [MAX 构建处理器](../../../UPMPackages/com.solotopia.nova.framework.sdk.max/Nova/Doc/MaxAdPluginBuildProcessor.md)
- [独立 AdMob 当前状态](../../../UPMPackages/com.solotopia.nova.framework.sdk.admob/Nova/Doc/INDEX.md)
- [Demo 业务调用源码](./Scripts/Runtime/UIs/DemoAdView/DemoAdView.Methods.cs)

导入路径重写、旧版本目录清理、Sample 基类与启动流程的共同边界，见已安装 Framework 的 `Docs/Onboarding/PROJECT_STRUCTURE.md` 中“Sample 的导入、辅助工具与页面边界”；本页只补充当前示例的差异。
