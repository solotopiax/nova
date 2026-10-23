# Nova Framework - SDK - MAX

> 包名：`com.solotopia.nova.framework.sdk.max`
> 当前版本：`0.2.0`

MAX 广告聚合插件，提供广告展示服务。

本包只包含 Nova MAX 适配层，第三方 MAX SDK 由官方 UPM 包 `com.applovin.mediation.ads@8.6.4` 提供，不再内置 `Core/MaxSdk`。MAX mediation adapters 也由本包 `package.json` 统一声明为 AppLovin 官方 UPM 依赖。

Unity Ads Android adapter 固定为 `4200101.0.0`（AppLovin adapter `4.20.1.1`、Unity Ads SDK `4.20.1`），iOS adapter 固定为 `4200001.0.0`（`4.20.0.1`、Unity Ads SDK `4.20.0`），LINE iOS adapter 固定为 `3010102.0.0`（`3.1.1.2`、LINE SDK `3.1.1`）。Android 官方修复包移除了错误的 iOS SPM 版本占位符；两个 iOS 官方包使用真实三段 tag，解决 Xcode 读取旧四段版本时崩溃。

iOS 构建时，Nova 自有顺序 `44` 后处理会将上述 LINE、Unity Ads 原生 adapter 统一交给 CocoaPods，避免其他 MAX Pods 和两项 SPM adapter 同时生产 `AppLovinSDK.framework`。Firebase 等其他 SDK 保留原有 SPM 配置，两个 iOS 原生 SDK 版本保持不变。该步骤随包自动执行，要求可运行的 CocoaPods；未知版本或工程结构会明确令构建失败，修复后重新导出。普通回写异常恢复原始两文件，超时或强制中断时必须重新导出，具体边界见[构建文档](./Nova/Doc/MaxAdPluginBuildProcessor.md#ios-原生依赖归属)。

Android 构建时，若 Unity 为 `6000.4.4f1` 及以上且 AppLovin Integration Manager 开启 Quality Service（不论是否勾选 Custom Launcher Manifest），临时修复 `MaxSafeDKLauncherManifestFix` 会向生成的 launcher manifest 补写 `android:name="com.safedk.android.SafeDKApplication"`，不改动项目模板；已有其他自定义 Application 时保留并告警。MAX 修复后删除该文件即可，详见[构建文档](./Nova/Doc/MaxAdPluginBuildProcessor.md#android-safedk-launcher-manifest-临时修复)。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
{
  "dependencies": {
    "com.solotopia.nova.framework.sdk.max": "0.2.0"
  }
}
```

本包固定依赖 Ad `1.2.0`、Framework `0.7.0` 和 MAX `8.6.4`，最低 Unity `6000.6.7f1`；adapter 版本以 [package.json](./package.json) 为准。当前含 14 组 Android/iOS adapter 和 Yandex Android adapter，未声明 Yandex iOS。MAX 原厂包传递依赖 EDM4U，消费工程需提供对应 registry。

Nova PlugPals 会把本包 `nova.scopedRegistries` 写到工程顶层 manifest；通过 Unity 原生 Package Manager 或直接改 manifest 安装时，需要合并以下 registry，保留项目已有条目：

```json
{
  "scopedRegistries": [
    {
      "name": "AppLovin MAX Unity",
      "url": "https://unity.packages.applovin.com/",
      "scopes": [
        "com.applovin.mediation.ads",
        "com.applovin.mediation.adapters",
        "com.applovin.mediation.dsp"
      ]
    },
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.google.external-dependency-manager"]
    }
  ]
}
```

实际原生广告支持 Android/iOS；Editor 和其他平台使用 MAX 的 Editor 门面，不能视为真机广告验证。配置 `AdPluginConfig.ChannelConfigs.Items` 中的 MAX 项并导出 `ConfigRuntimeSO` 后，由当前 `AdPlugin` 聚合层为新建的每个 MAX 渠道实例初始化一次，初始化完成不自动预载。AppKey 与 AdMob 平台 App ID 在构建期写入官方 `AppLovinSettings.asset`；当前构建处理器不校验广告组语法，运行时会跳过无效格式。完整构建链见 [构建文档](./Nova/Doc/MaxAdPluginBuildProcessor.md)。

## 当前行为

- RV、Inter、AppOpen默认组间并行、组内串行。三种PlacementGroups仍为字符串List，每项一组、项内`,`或`|`分隔；`["A1,A2,A3", "B1"]`先请求A1和B1。成功停止本组并归零间隔下标，末项失败按ad统一GroupRetryIntervalsSeconds（默认2、4、8、16、32秒）等待后从组首继续；仅整组失败推进一次下标，末尾循环。Banner仍为真实单ID列表。展示选本地和原厂都Ready且收益最高的缓存。
- 插屏 displayed 回调中的 `Success`、`RewardGranted` 均为 true；激励关闭时的奖励资格取 MAX 官方奖励回调状态，开屏保持 false。业务实际发奖由调用方处理。
- 激励、插屏和开屏的 `nova_ad_show`、`nova_ad_show_result`、`nova_ad_hidden`、`ad_ilrd`、`ad_impression` 关联同次展示的 `nova_ad_impression_id`；关闭时没有匹配上下文则不附加 ID。
- 收益打点插件在使用时获取并缓存：有值直接使用，空值在当前回调线程查询，仍不可用则逐目标 Warning 并跳过本次派发；下一次事件再尝试，不等待初始化或排队补发。
- 非 Banner 每次收益回调分别派发 `ad_ilrd` 和 `ad_impression`；MAX 全部 Banner 广告位共享 ILRD 批次，默认累计 5 次派发一次，`ad_impression` 仍逐次派发。
- Banner 控制 API 使用配置列表中的首个 ID。`ShowBanner()` 同时启动 MAX 自动刷新，`HideBanner()` 同时停止自动刷新。
- `MaxAdChannelConfig.BannerAutoRefreshIntervalSeconds` 默认 `10` 秒；Inspector 字段范围为 `10–120`，运行时 getter 为 `5–120`，当前下限不一致。getter 不证明 native 已采纳 5 秒刷新。
- Banner native view 按广告位幂等创建；加载失败后若业务仍要求显示，后续加载成功会恢复展示。
- 当前适配层不设置 `adaptive_banner`；开启自动刷新前会通过 `ad_refresh_seconds` extra parameter 写入面板配置的刷新间隔。
- 当前源码也未设置禁用 MAX SDK 自动重试或禁用非 Banner B2B 广告位参数，这两项不属于现版本已接入能力。
- MAX 初始化完成时缓存用户广告隐私授权状态，通过广告公共接口 `WaitForPrivacyFlowAsync()`、`IsUserConsentSet()` 与 `HasUserConsent()` 暴露；初始化取消、异常或释放也会结束隐私等待。该任务完成不等于用户同意，查询不会自动刷新后续变更；CMP、隐私政策 URL、ATT 仍需项目配置。

## 生命周期与兼容

“初始化一次”指每个 `MaxAdPlugin` 渠道实例只初始化一次；SDKManager 完整释放后可开启新的初始化周期，创建新的 `IAdPlugin` 聚合及 MAX 渠道，不能再次初始化已释放的旧实例。应先等待旧聚合的共享释放任务实际完成，再显式调用并等待 SDKManager 新初始化周期的 `InitializeAsync`，重新获取 `IAdPlugin`；同一 SDKComponent 的 `Nova.SDK.InitializeTask` 仍缓存首次初始化结果，不会自然启动新周期；业务同时释放旧 `AdPlugin.Events` 订阅，并向新聚合重新订阅，旧订阅和回放不会迁移。

聚合在进入渠道配置、事件绑定和初始化前持有实例，准备或初始化失败也保留释放所有权。释放先关闭桥接及请求入口，取消等待，待各渠道准备和实际初始化方法退出后准确退订并清理。重复释放等待同一清理终点，传入已取消令牌也不会跳过清理；MAX 清除展示完成源、收益会话、曝光、Banner 与国家/授权缓存。释放完成不会撤销已发起的原厂全屏展示或已经进入外部分析插件的调用。初始化调用方令牌只控制初始化等待，成功后取消它不会关闭当前聚合的桥接；结束生命周期须走所属 Manager 的释放流程。

旧单ID列表自然成为单ID组，首次仍并发；失败后不再立即重试N次，按统一间隔数组循环等待，成功后从第一档重新开始。旧逐广告重试次数、间隔访问器和槽位字段已删除。字段改名保留Unity资产迁移，旧PlacementIds访问器返回真实展开ID。运行时初始化检查重复 ID 与语法，无效格式不注册；当前构建处理器没有该门禁，原始组表达式不会送入 MAX。

加载/展示结果先快照再回主线程推进；收益纯打点即时上报。业务取消只结束等待，全屏占用到匹配hidden/displayFailed或Dispose才结束。MAX Show若内部静默拒绝，不以超时证明未在展示或解除占用，不创建未displayed的诊断等待。MAX没有request token或Cancel Load，Nova仅保证主动Load组内串行。MAX8.6.4 iOS RV expiry事件名尾空格属于原厂限制，不在本包改厂商源码。

30 个已注册广告回调均以 `Log.Debug(LogTag.Max, ...)` 为首句，正文以格式名开头，不重复 MAX 前缀；实际原厂 Show 调用也有 Debug 日志。手动或默认业务请求仅记录准备请求、实际渠道请求与平台加载回调；展示记录准备展示、原厂 Show 与平台回调，广告组新轮仅在展示后补量（`reason=AutoRefill`）和自动重试（`reason=Retry`）时记录，详见[主文档](./Nova/Doc/MaxAdPlugin.md#本地调用与回调日志)。Warning / Error 异常诊断保留，`LogEnable` 只控制原厂详细日志。全屏终态先取出并清空对应 TCS 与真实 ID，再通知聚合层并在 finally 完成本次等待，避免重入影响新的展示。

Banner 控制通过当前有效聚合调用，先请求并成功创建视图；MAX 渠道的低层控制方法不额外防御空首项或已释放旧引用。Banner 宽度直接传入原厂（Android dp / iOS points）；自适应高度查询没有密度换算，也不会开启 `adaptive_banner`。终态事件使用最近 32 条回放，业务发奖必须处理历史回放与幂等；完整示例见主文档。

接入验收须覆盖 Android/iOS Player 构建、Xcode 编译、真机 CMP、四格式广告、奖励、收益、Banner 单位/刷新及 SDKManager 新初始化周期；源码核对不能替代这些平台验证。构建前组配置与 App ID 的检查边界见[构建文档](./Nova/Doc/MaxAdPluginBuildProcessor.md#构建前广告配置边界)。

## 文档

- [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)
- [Nova/Doc/MaxAdPlugin.md](./Nova/Doc/MaxAdPlugin.md)
- [奖励资格、回调与埋点的对应关系](./Nova/Doc/MaxAdPlugin.md#回调与埋点对照)
- [Nova/Doc/MaxAdPluginBuildProcessor.md](./Nova/Doc/MaxAdPluginBuildProcessor.md)

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 当前开源状态

- 当前结论：不应直接进入公开仓；公开版只应保留 Nova 适配层与接入文档，不应继续提交 AppLovin MAX 插件本体。

## 许可与第三方声明

- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- 上游来源、第三方声明与当前再分发边界见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
- AppLovin SDK 本体、adapter、资源与上游声明由官方 UPM 包提供。
