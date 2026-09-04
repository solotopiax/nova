# MaxAdPlugin

> 最后更新：2026-07-30
> 当前代码事实：`UPMPackages/com.solotopia.nova.framework.sdk.max/Nova/Scripts/Runtime/**`

**类签名**：`[AdChannel(typeof(MaxAdChannelConfig))] public sealed partial class MaxAdPlugin : AdChannelPluginBase`
**命名空间**：`NovaFramework.SDK.MaxAdPlugin.Runtime`
**全局访问**：通过 `Nova.SDK.Get<IAdPlugin>()` 获取实例

AppLovin MAX 广告渠道插件，负责 MAX SDK 初始化、激励视频/插屏/Banner/开屏的加载与展示，以及收入回调上报。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `MaxAdPlugin.cs` | `MaxAdPlugin` | 主实现：`override` 方法、SDK 初始化、格式分发 |
| `MaxAdPlugin.Visitors.cs` | `MaxAdPlugin` | 私有字段定义 |
| `MaxAdPlugin.Callbacks.cs` | `MaxAdPlugin` | MAX SDK 回调注册与反注册 |
| `MaxAdPlugin.RV.cs` | `MaxAdPlugin` | 激励视频加载、展示、回调链 |
| `MaxAdPlugin.Inter.cs` | `MaxAdPlugin` | 插屏加载、展示、回调链 |
| `MaxAdPlugin.AppOpen.cs` | `MaxAdPlugin` | 开屏加载、展示、回调链 |
| `MaxAdPlugin.Banner.cs` | `MaxAdPlugin` | Banner 创建、位置控制、刷新控制、回调链 |
| `MaxAdPlugin.Track.cs` | `MaxAdPlugin` | MAX 特有加载属性与收益打点 |
| `MaxAdPlugin.UserId.cs` | `MaxAdPlugin` | 用户身份同步：override `SetUserId` 调用 `MaxSdk.SetUserId(userId)` |
| `MaxAdChannelConfig.cs` | `MaxAdChannelConfig` | 渠道配置数据对象，实现 `IAdChannelConfig` |
| `FacebookAdSetting.cs` | `FacebookAdSetting` | Facebook 广告 SDK 隐私设置内部工具类 |
| `Editor/BuildProcessor/MaxAdPluginBuildProcessor.cs` | `MaxAdPluginBuildProcessor` | 构建预处理：写入 AppLovinSettings.asset，并为 Android 注册 MAX 相关 ProGuard 规则 |
| `Editor/NovaFramework.SDK.MaxAdPlugin.Editor.asmdef` | — | Editor 程序集定义，仅 Editor 平台启用 |

---

## UPM 依赖

`MaxSdk`、`MaxSdkBase`、`MaxSdkCallbacks`、`MaxSdkUtils` 均来自官方 AppLovin UPM 包 `com.applovin.mediation.ads`。`com.solotopia.nova.framework.sdk.max` 的 `package.json` 必须声明该依赖，Runtime 程序集引用 `MaxSdk.Scripts`，Editor 程序集引用 `MaxSdk.Scripts.IntegrationManager.Editor`。

Mediation adapters 也由 `package.json` 统一声明为 AppLovin 官方 UPM 包，按平台拆为 `.android` / `.ios` 两条依赖。旧 `Core/MaxSdk/Mediation/**/Dependencies.xml` 不再作为依赖来源；Google 与 GoogleAdManager Android adapter 使用 AppLovin registry 当前可用的新版本。

本包不再提交 AppLovin MAX SDK 本体文件；`UPMPackages/com.solotopia.nova.framework.sdk.max/Core/MaxSdk` 应保持为空或不存在。

---

## §3 继承关系

```
AdChannelPluginBase
  └── MaxAdPlugin  (sealed partial)
        └── [AdChannel(typeof(MaxAdChannelConfig))]  (特性，绑定配置类型)

IAdChannelConfig  <── MaxAdChannelConfig  (配置值对象，非插件本体)
```

`AdChannelPluginBase` 提供 `RaiseAdLoaded` / `RaiseAdLoadFailed` / `RaiseShowFailed` / `RaiseAdClosed` / `RaiseRevenue` / `TrackAdShow` / `TrackAdClick` / `RegisterAdUnits` / `MarkBannerHidden` 等受保护方法，子类通过这些方法向聚合层上报事件，不直接操作聚合层接口。

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_RVPlacementIds` | `IReadOnlyList<string>` | `null` | 激励视频广告位 ID 列表，从 `MaxAdChannelConfig` 缓存 |
| `m_InterPlacementIds` | `IReadOnlyList<string>` | `null` | 插屏广告位 ID 列表，从 `MaxAdChannelConfig` 缓存 |
| `m_BannerPlacementIds` | `IReadOnlyList<string>` | `null` | Banner 广告位 ID 列表，从 `MaxAdChannelConfig` 缓存 |
| `BannerPlacementId` | `string` | `null` | Banner 控制 API 使用的首个广告位 ID；列表为空时返回 null |
| `m_BannerAutoRefreshIntervalSeconds` | `int` | `10` | Banner 自动刷新间隔，从 `MaxAdChannelConfig` 缓存，单位为秒 |
| `m_AppOpenPlacementIds` | `IReadOnlyList<string>` | `null` | 开屏广告位 ID 列表，从 `MaxAdChannelConfig` 缓存 |
| `m_RVTcs` | `UniTaskCompletionSource<AdResult>` | `null` | 激励视频展示挂起句柄；`ShowRVAsync` 创建，`OnRVHidden`/`OnRVDisplayFailed` 完成 |
| `m_InterTcs` | `UniTaskCompletionSource<AdResult>` | `null` | 插屏展示挂起句柄；`ShowInterAsync` 创建，`OnInterHidden`/`OnInterDisplayFailed` 完成 |
| `m_AppOpenTcs` | `UniTaskCompletionSource<AdResult>` | `null` | 开屏展示挂起句柄；`ShowAppOpenAsync` 创建，`OnAppOpenHidden`/`OnAppOpenDisplayFailed` 完成 |
| `m_RVRewarded` | `bool` | `false` | 激励视频奖励标记；`OnRVReceivedReward` 置 `true`，`OnRVHidden` 读取后清零 |
| `m_BannerPosition` | `MaxSdkBase.AdViewPosition` | `BottomCenter` | Banner 当前位置；`UpdateBannerPosition` 同步更新 |
| `m_CreatedBannerPlacementIds` | `HashSet<string>` | 空集合 | 已创建 native Banner view 的广告位集合，用于避免重复 `CreateBanner` |
| `m_BannerDesiredVisible` | `bool` | `false` | 业务是否期望 Banner 可见；加载恢复后据此决定是否重新显示 |
| `m_CountryCode` | `string` | `null` | MAX SDK 初始化完成后由 `SdkConfiguration.CountryCode` 返回的国家代码；`GetCountryCode()` 对外返回该缓存值 |
| `m_IsUserConsentSet` | `bool` | `false` | MAX SDK 初始化完成时缓存用户是否已明确作出广告隐私授权决定 |
| `m_HasUserConsent` | `bool` | `false` | MAX SDK 初始化完成时缓存授权结果；仅在 `m_IsUserConsentSet` 为 `true` 时表示明确同意或拒绝 |
| `m_RevenueMonetizeTracker` | `IMonetizeTrackPlugin` | `null` | 收益回调打点用的变现插件引用；初始化主线程缓存 |
| `m_RevenueAttributionTracker` | `IAttributionPlugin` | `null` | 收益回调打点用的归因插件引用；初始化主线程缓存 |
| `m_RevenueEventTracker` | `ITrackPlugin` | `null` | 收益回调打点用的通用埋点插件引用；初始化主线程缓存 |
| `BannerIlrdInterval` | `int` | `5` | 继承自广告全局配置；Banner ILRD 聚合由 `AdChannelPluginBase` 统一处理，MAX 在满间隔时构造并上传自己的 `ad_ilrd` payload |

### MaxAdChannelConfig 配置字段

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_Enabled` | `bool` | `true` | 是否启用 MAX 渠道 |
| `m_AppKey` | `string` | `null` | AppLovin MAX SDK Key，构建预处理写入 AppLovinSettings |
| `m_AdMobAppIdAndroid` | `string` | `null` | AdMob Android App ID，构建预处理写入 AppLovinSettings.AdMobAndroidAppId |
| `m_AdMobAppIdIOS` | `string` | `null` | AdMob iOS App ID，构建预处理写入 AppLovinSettings.AdMobIosAppId |
| `m_RVPlacementIds` | `List<string>` | 空列表 | 激励视频广告位 ID 列表 |
| `m_InterPlacementIds` | `List<string>` | 空列表 | 插屏广告位 ID 列表 |
| `m_BannerPlacementIds` | `List<string>` | 空列表 | Banner 广告位 ID 列表；控制 API 使用首项 |
| `m_BannerAutoRefreshIntervalSeconds` | `int` | `10` | Banner 自动刷新间隔，面板和运行时均限制为 `5–120` 秒 |
| `m_AppOpenPlacementIds` | `List<string>` | 空列表 | AppOpen 广告位 ID 列表 |
| `m_LogEnable` | `bool` | `false` | MAX SDK 详细日志开关 |
| `m_CreativeDebuggerEnabled` | `bool` | `false` | Creative Debugger 开关 |
| `m_MediationDebuggerEnabled` | `bool` | `false` | 初始化完成后是否显示 Mediation Debugger |

---

## §5 完整公开 API

### 插件元数据

```csharp
// MAX 渠道插件名称标识
string Name { get; }  // => "Max"

// 对应广告渠道类型
AdChannelType Channel { get; }  // => AdChannelType.MAX

// 返回 MAX SDK 初始化回调中的 SdkConfiguration.CountryCode；尚未初始化或未返回时为空字符串。
public override string GetCountryCode()

// 返回 MAX 初始化完成时缓存的用户是否已作出广告隐私授权决定。
public override bool IsUserConsentSet()

// 返回 MAX 初始化完成时缓存的授权结果；必须结合 IsUserConsentSet() 判断。
public override bool HasUserConsent()

// 等待 MAX 初始化期间的 Consent Flow 结束；无弹窗时随初始化完成。
public override UniTask WaitForPrivacyFlowAsync(CancellationToken ct = default)
```

### 用户身份同步（override，来自 MaxAdPlugin.UserId.cs）

```csharp
// 同步登录用户 userId 到 MAX SDK。
// 由 AdPlugin 订阅 SDKEventData.UserLogin 后 fanout 调用，也可由业务层直接调用。
// MaxSdk 门面统一跨平台（Android/iOS/Editor），无需 #if 分支。
public override void SetUserId(string userId)
```

### 初始化与销毁

```csharp
// 异步初始化 MAX SDK：缓存各格式 PlacementId 列表、初始化 FacebookAdSetting、
// 注册 OnSdkInitializedEvent、调用 MaxSdk.InitializeSdk()、等待回调
protected override async UniTask InitChannelSDKAsync(IAdChannelConfig config, CancellationToken ct)

// 销毁：反注册全部 MAX SDK 回调（RV / Inter / Banner / AppOpen）
// 由基类 DisposeAsync 调用
protected override UniTask DisposeChannelSDKAsync(CancellationToken ct)
```

### 加载（Rewarded）

```csharp
// 发起激励视频加载请求
// 内部调用 MaxSdk.LoadRewardedAd(placementId)
// 结果通过 RaiseAdLoaded / RaiseAdLoadFailed 回调上报
protected override UniTask OnRequestAsync(AdFormat format, string placementId, CancellationToken ct)
// format == AdFormat.Rewarded 时路由至此路径
```

### 加载（Interstitial）

```csharp
// 发起插屏加载请求
// 内部调用 MaxSdk.LoadInterstitial(placementId)
protected override UniTask OnRequestAsync(AdFormat format, string placementId, CancellationToken ct)
// format == AdFormat.Interstitial 时路由至此路径
```

### 加载（AppOpen）

```csharp
// 发起开屏加载请求
// 内部调用 MaxSdk.LoadAppOpenAd(placementId)
protected override UniTask OnRequestAsync(AdFormat format, string placementId, CancellationToken ct)
// format == AdFormat.AppOpen 时路由至此路径
```

### 加载（Banner）

```csharp
// 首次请求时创建 Banner，并将背景色设为白色；同一 placementId 未销毁前不重复创建
// Request 阶段不设置 extra parameter；ad_refresh_seconds 在 StartBannerAutoRefresh() 中写入
// Banner 创建后是否显示由 ShowBanner()/HideBanner() 维护的业务可见性期望决定
protected override UniTask OnRequestAsync(AdFormat format, string placementId, CancellationToken ct)
// format == AdFormat.Banner 时路由至此路径
```

### 展示（Rewarded / Interstitial / AppOpen）

```csharp
// 展示全屏广告并挂起等待关闭结果
// Rewarded  → MaxSdk.ShowRewardedAd(placementId)，奖励状态由 m_RVRewarded 追踪
// Inter     → MaxSdk.ShowInterstitial(placementId)
// AppOpen   → MaxSdk.ShowAppOpenAd(placementId)
// Banner 不走此方法，使用下方 Banner 专属控制 API
protected override UniTask<AdResult> OnShowAsync(AdFormat format, string placementId, CancellationToken ct)
```

### Banner 专属控制

```csharp
// 显示配置列表中的首个 Banner，并启动 MAX 自动刷新
public override void ShowBanner()

// 隐藏配置列表中的首个 Banner，并停止 MAX 自动刷新；不销毁，可再次 ShowBanner
public override void HideBanner()

// 销毁 Banner 并通知聚合层标记为隐藏状态
public override void DestroyBanner()

// 更新 Banner 位置（枚举方式）
// BannerPosition 枚举：Top/Bottom → TopCenter/BottomCenter；
//   TopLeft/TopRight/BottomLeft/BottomRight → 对应 MaxSdkBase.AdViewPosition
public override void UpdateBannerPosition(BannerPosition position)

// 更新 Banner 位置（屏幕像素坐标方式）
public override void UpdateBannerPosition(Vector2 position)

// 将配置间隔写入 ad_refresh_seconds 后启动 Banner 自动刷新
public override void StartBannerAutoRefresh()

// 停止 Banner 自动刷新
public override void StopBannerAutoRefresh()

// 设置 Banner 宽度（像素）
public override void SetBannerWidth(float width)

// 获取自适应 Banner 高度（像素）
// width < 0 时使用 Screen.width；内部委托 MaxSdkUtils.GetAdaptiveBannerHeight
public override float GetAdaptiveBannerHeight(float width = -1)

// 设置 Banner 背景色
public override void SetBannerBackgroundColor(Color color)
```

---

## §6 初始化状态机

```
InitChannelSDKAsync(config, ct)
          │
          ▼
  转型 MaxAdChannelConfig
  缓存 4 类 PlacementId 列表（RV / Inter / Banner / AppOpen）
  缓存 Banner 自动刷新间隔（10–120 秒）
  缓存收益打点插件引用
          │
          ▼
  FacebookAdSetting.Initialize()
  ├─ iOS 14.5+：SetAdvertiserTrackingEnabled(true)
  └─ Android/iOS：SetDataProcessingOptions(["LDU"], 0, 0)
          │
          ▼
  SetMuted(MuteAd)               ← MuteAd 由聚合层注入，非 Config 字段
  SetVerboseLogging(cfg.LogEnable)
          │
          ▼
  new UniTaskCompletionSource<bool> initTcs
  注册 MaxSdkCallbacks.OnSdkInitializedEvent lambda
          │
          ▼
  MaxSdk.InitializeSdk()
          │
          ▼ （异步，MAX SDK 回调）
  OnSdkInitializedCallback(sdkConfig, cfg, initTcs)
  ├─ 缓存 m_CountryCode
  ├─ 缓存 m_IsUserConsentSet 与 m_HasUserConsent
  ├─ 完成 WaitForPrivacyFlowAsync 等待信号
  ├─ Log.Debug 打印国家代码
  ├─ SetCreativeDebuggerEnabled(cfg.CreativeDebuggerEnabled)
  ├─ RegisterCallbacks()
  │    ├─ RegisterRVCallbacks()      → guard(m_RVPlacementIds.Count) → 逐 ID RegisterAdUnits + 注册 8 个事件
  │    ├─ RegisterInterCallbacks()   → guard(m_InterPlacementIds.Count) → 逐 ID RegisterAdUnits + 注册 7 个事件
  │    ├─ RegisterBannerCallbacks()  → guard(m_BannerPlacementIds.Count) → 逐 ID RegisterAdUnits + 注册 5 个事件
  │    └─ RegisterAppOpenCallbacks() → guard(m_AppOpenPlacementIds.Count) → 逐 ID RegisterAdUnits + 注册 7 个事件
  ├─ [条件] MaxSdk.ShowMediationDebugger()（cfg.MediationDebuggerEnabled == true）
  ├─ RaiseInitResult(true)           ← 通知聚合层初始化成功
  └─ initTcs.TrySetResult(true)
          │
          ▼
  await initTcs.Task 返回
  InitChannelSDKAsync 完成
```

**guard 说明：** `RegisterXxxCallbacks` 方法开头检查对应广告位 ID 列表是否为空；空列表会跳过该格式。非空列表中的每个 ID 都注册为独立 `AdUnit`，一次 `RequestAsync(format)` 会并行请求该格式下全部 Idle 槽位，任一成功即可结束当前请求批次。

**线程说明：** MAX 不再设置 `MaxSdkBase.InvokeEventsOnUnityMainThread = true`。MAX 回调按“打点/状态”和“业务回调”拆分处理：不依赖 UI 的打点、Nova 状态机推进、批次完成通知可以在 SDK 原始回调线程即时执行，避免用户看完广告后立即杀进程造成收益或行为打点丢失；只有 `OnAdLoaded` / `OnAdLoadFailed` / `OnAdRevenuePaid` / `OnShowCompleted` / `OnShowFailed` / `OnAdClosed` 等对外业务事件由基类通过 `PostAdCallbackToMainThread` 排入 Unity 主线程。收益回调统一调用 `RaiseRevenueImmediately`：其中 MAX 特有收益打点立即执行，`RaiseRevenue` 同步推进收益状态，`RevenuePaid` 事件由基类排入主线程。非 Banner 的 displayed 回调只触发 `OnShowCompleted`，不触发自动续杯；续杯在 hidden 或 display failed 收口时发生。Banner 不接入 `RaiseShowCompleted`，避免自动刷新期间反复触发。

### Banner 收益打点

- `ad_impression` 在每次 MAX Banner 收益回调中即时上传，不因 Banner 刷新频繁而节流；非 Banner 收益打点同样在 MAX 收益回调线程即时上传。
- Banner 的 `ad_ilrd` 按基类 `TrackBannerIlrdAggregated` 统一累计上传；默认值 `5` 表示每 5 次 Banner 收益回调上传 1 次。
- 未达到间隔的 Banner 累计次数和累计金额由 `AdChannelPluginBase` 写入 `PlayerPrefs`，避免下次游戏启动丢失未完成批次。
- `ad_impression` 和 `ad_ilrd` 都不新增额外属性，且保持既有属性类型：`ad_ilrd.publisher_revenue` / `ad_ilrd.value` 为数值，`ad_ilrd.af_revenue` 为文本；未满间隔的内部存档金额使用 invariant decimal 字符串保存。

### Banner 可见性恢复

- MAX Banner native view 按 placementId 幂等创建；同一个 placement 未 Destroy 前，后续 `RequestAsync(AdFormat.Banner)` 不重复 `CreateBanner`。
- `ShowBanner()` 会记录业务期望 Banner 保持可见；`HideBanner()` / `DestroyBanner()` 会清除该期望。
- `ShowBanner()` 会先调用 `StartBannerAutoRefresh()`，`HideBanner()` 会先调用 `StopBannerAutoRefresh()`；启动时先对当前首个 Banner ID 写入 `ad_refresh_seconds`，再调用 MAX 自动刷新接口。
- `OnBannerLoaded` 成功后，如果业务期望仍为可见，会再次调用 `MaxSdk.ShowBanner(adUnitId)`，用于恢复加载失败后又成功的 Banner 展示。
- `DestroyBanner()` 会从已创建集合中移除 placementId；Destroy 后下一次 `RequestAsync(AdFormat.Banner)` 会重新创建 native view。
- Banner 专属控制方法固定操作 `m_BannerPlacementIds` 的首个 ID；虽然加载阶段支持多个 Banner ID 并行创建，业务配置应把实际展示用 ID 放在首位。

### MAX SDK 参数边界

- 当前实现不调用 `SetBannerExtraParameter(..., "adaptive_banner", "true")`；`ad_refresh_seconds` 使用 `MaxAdChannelConfig.BannerAutoRefreshIntervalSeconds`，默认 `10` 秒并限制为 `5–120` 秒。
- 当前实现没有设置禁用 MAX SDK 自动重试的 extra parameter，也没有向 MAX SDK 注入禁用 B2B 的非 Banner 广告位 ID 列表。若业务依赖这两项能力，需要先在 Runtime 适配层实现，不能仅靠配置字段生效。

---

## §10 常见误区

### 误区一：Banner 展示使用 ShowAsync

Banner 的展示与隐藏不走 `OnShowAsync` 路径。`OnShowAsync` 仅处理全屏广告（Rewarded / Interstitial / AppOpen），Banner 通过 `ShowBanner()` / `HideBanner()` 直接控制。

```csharp
// 错误：Banner 调 ShowAsync 不会显示广告，框架层不路由到此路径
await adPlugin.ShowAsync(AdFormat.Banner, ct: ct);

// 正确：先 RequestAsync 创建 Banner，再手动控制显示/隐藏
await adPlugin.RequestAsync(AdFormat.Banner, ct: ct);
adPlugin.ShowBanner();
adPlugin.HideBanner();
```

### 误区二：在 MaxAdChannelConfig 中配置静音

`MaxSdk.SetMuted()` 的参数 `MuteAd` 由 SDK Ad 的全局 `AdPluginConfig.ChannelConfigs` 在 `InitChannelSDKAsync` 调用前注入，不是 `MaxAdChannelConfig` 的字段。当前没有运行时 `SetMute` API，应在 Config 面板的广告渠道全局配置中设置“广告静音”。

```csharp
// 错误：MaxAdChannelConfig 没有 MuteAd 字段，此写法无法编译
config.MuteAd = true;

// 正确：在 Config 面板的 AdPluginConfig.ChannelConfigs 全局设置中开启“广告静音”；
// AdPlugin 初始化渠道前会通过 ApplyGlobalConfig 透传给 MaxAdPlugin。
```

### 误区三：DisplayFailed 后等待 HiddenEvent

对于全屏广告（Rewarded / Interstitial / AppOpen），MAX SDK 在展示失败（`OnXxxDisplayFailed`）后**不会**触发 `OnXxxHidden` 回调。`MaxAdPlugin` 在 `DisplayFailed` 回调中直接调用 `m_XxxTcs.TrySetResult(失败 result)` 结束挂起，不依赖 `Hidden` 事件。如果在外部封装时假设"展示失败后一定还会有 Hidden 收尾"，将导致 `ShowAsync` 永远不返回。

```csharp
// 正确认知：DisplayFailed → ShowFailed 事件 + ShowAsync 完成，不再等待 Hidden。
var adPlugin = Nova.SDK.Get<AdPlugin>();
var subscriptions = new List<IDisposable>();
adPlugin.Events.ShowFailed.Subscribe(result =>
{
    if (result.Format == AdFormat.Rewarded)
        Log.Warning(result.ErrorMessage);
}, subscriptions);

await adPlugin.ShowAsync(AdFormat.Rewarded, ct: ct);
```

---

## §11 使用示例

### 初始化（由聚合层自动驱动，无需手动调用）

`MaxAdPlugin` 由 `AdPluginBase` 聚合层统一初始化，业务层只需确保 `MaxAdChannelConfig` 已正确配置在 ScriptableObject 中。

### 激励视频完整流程

```csharp
var adPlugin = Nova.SDK.Get<AdPlugin>();
var subscriptions = new List<IDisposable>();

// 生命周期内只订阅一次；ShowCompleted 是 displayed，最终奖励结果看 AdClosed。
adPlugin.Events.AdClosed.Subscribe(result =>
{
    if (result.Format == AdFormat.Rewarded && result.UserCompleted)
        GiveReward();
}, subscriptions);

adPlugin.Events.ShowFailed.Subscribe(result =>
{
    if (result.Format == AdFormat.Rewarded)
        Log.Warning($"RV show failed: {result.ErrorMessage}");
}, subscriptions);

// 加载激励视频
await adPlugin.RequestAsync(AdFormat.Rewarded, ct: ct);

// 等待广告关闭或展示失败；结果由上面的事件订阅处理。
await adPlugin.ShowAsync(AdFormat.Rewarded, ct: ct);
```

### 插屏展示

```csharp
var adPlugin = Nova.SDK.Get<AdPlugin>();
var subscriptions = new List<IDisposable>();

adPlugin.Events.ShowFailed.Subscribe(result =>
{
    if (result.Format == AdFormat.Interstitial)
        Log.Warning($"Interstitial show failed: {result.ErrorMessage}");
}, subscriptions);

await adPlugin.RequestAsync(AdFormat.Interstitial, ct: ct);
await adPlugin.ShowAsync(AdFormat.Interstitial, ct: ct);
```

### Banner 控制

```csharp
var adPlugin = Nova.SDK.Get<IAdPlugin>();

// 创建 Banner（位置默认 BottomCenter；当前不设置 adaptive_banner extra parameter）
await adPlugin.RequestAsync(AdFormat.Banner, ct: ct);

// 显示
adPlugin.ShowBanner();

// 查询自适应高度（用于 UI 布局适配）
float bannerHeight = adPlugin.GetAdaptiveBannerHeight();  // 使用 Screen.width

// 更新位置
adPlugin.UpdateBannerPosition(BannerPosition.Top);

// 隐藏（不销毁）
adPlugin.HideBanner();

// 销毁（需要重新 RequestAsync 才能再次显示）
adPlugin.DestroyBanner();
```

### 开屏广告

```csharp
var adPlugin = Nova.SDK.Get<AdPlugin>();
var subscriptions = new List<IDisposable>();

adPlugin.Events.ShowFailed.Subscribe(result =>
{
    if (result.Format == AdFormat.AppOpen)
        Log.Warning($"AppOpen show failed: {result.ErrorMessage}");
}, subscriptions);

await adPlugin.RequestAsync(AdFormat.AppOpen, ct: ct);
await adPlugin.ShowAsync(AdFormat.AppOpen, ct: ct);
```

---

## §13 关联文档

- [`MaxAdPluginBuildProcessor.md`](./MaxAdPluginBuildProcessor.md) — 构建预处理器
