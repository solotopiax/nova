# MaxAdPlugin

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
| `MaxAdPlugin.UserId.cs` | `MaxAdPlugin` | 用户身份同步：override `SetUserId` 调用 `MaxSdk.SetUserId(userId)` |
| `MaxAdChannelConfig.cs` | `MaxAdChannelConfig` | 渠道配置数据对象，实现 `IAdChannelConfig` |
| `FacebookAdSetting.cs` | `FacebookAdSetting` | Facebook 广告 SDK 隐私设置内部工具类 |
| `Editor/BuildProcessor/MaxAdPluginBuildProcessor.cs` | `MaxAdPluginBuildProcessor` | 构建预处理：把 MaxAdChannelConfig 的 SdkKey / AdMob AppId 写入 AppLovinSettings.asset |
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
| `m_RVPlacementId` | `string` | `null` | 激励视频广告位 ID，从 `MaxAdChannelConfig` 缓存 |
| `m_InterPlacementId` | `string` | `null` | 插屏广告位 ID，从 `MaxAdChannelConfig` 缓存 |
| `m_BannerPlacementId` | `string` | `null` | Banner 广告位 ID，从 `MaxAdChannelConfig` 缓存 |
| `m_AppOpenPlacementId` | `string` | `null` | 开屏广告位 ID，从 `MaxAdChannelConfig` 缓存 |
| `m_RVTcs` | `UniTaskCompletionSource<AdResult>` | `null` | 激励视频展示挂起句柄；`ShowRVAsync` 创建，`OnRVHidden`/`OnRVDisplayFailed` 完成 |
| `m_InterTcs` | `UniTaskCompletionSource<AdResult>` | `null` | 插屏展示挂起句柄；`ShowInterAsync` 创建，`OnInterHidden`/`OnInterDisplayFailed` 完成 |
| `m_AppOpenTcs` | `UniTaskCompletionSource<AdResult>` | `null` | 开屏展示挂起句柄；`ShowAppOpenAsync` 创建，`OnAppOpenHidden`/`OnAppOpenDisplayFailed` 完成 |
| `m_RVRewarded` | `bool` | `false` | 激励视频奖励标记；`OnRVReceivedReward` 置 `true`，`OnRVHidden` 读取后清零 |
| `m_BannerPosition` | `MaxSdkBase.AdViewPosition` | `BottomCenter` | Banner 当前位置；`UpdateBannerPosition` 同步更新 |
| `m_CountryCode` | `string` | `null` | MAX SDK 初始化完成后由 `SdkConfiguration` 返回的国家代码 |

### MaxAdChannelConfig 新增字段（Task 1 追加）

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_AdMobAppIdAndroid` | `string` | `null` | AdMob Android App ID，构建预处理写入 AppLovinSettings.AdMobAndroidAppId |
| `m_AdMobAppIdIOS` | `string` | `null` | AdMob iOS App ID，构建预处理写入 AppLovinSettings.AdMobIosAppId |

---

## §5 完整公开 API

### 插件元数据

```csharp
// MAX 渠道插件名称标识
string Name { get; }  // => "MaxAdPlugin"

// 对应广告渠道类型
AdChannelType Channel { get; }  // => AdChannelType.MAX
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
// 异步初始化 MAX SDK：缓存 PlacementId、初始化 FacebookAdSetting、
// 注册 OnSdkInitializedEvent、调用 MaxSdk.InitializeSdk()、等待回调
protected override async UniTask InitChannelSDKAsync(IAdChannelConfig config, CancellationToken ct)

// 销毁：反注册全部 MAX SDK 回调（RV / Inter / Banner / AppOpen）
// 由基类 DisposeAsync 调用
protected override UniTask DisposeChannelSDKAsync()
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
// 创建 Banner 并设置自适应参数
// 内部调用 MaxSdk.CreateBanner(placementId, AdViewConfiguration) +
//   SetBannerExtraParameter("adaptive_banner", "true")
// Banner 创建后不自动显示，需调用 ShowBanner()
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
// 显示 Banner（需先完成 RequestAsync）
public override void ShowBanner()

// 隐藏 Banner（不销毁，可再次 ShowBanner）
public override void HideBanner()

// 销毁 Banner 并通知聚合层标记为隐藏状态
public override void DestroyBanner()

// 更新 Banner 位置（枚举方式）
// BannerPosition 枚举：Top/Bottom → TopCenter/BottomCenter；
//   TopLeft/TopRight/BottomLeft/BottomRight → 对应 MaxSdkBase.AdViewPosition
public override void UpdateBannerPosition(BannerPosition position)

// 更新 Banner 位置（屏幕像素坐标方式）
public override void UpdateBannerPosition(Vector2 position)

// 启动 Banner 自动刷新
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
  缓存 4 个 PlacementId（RV / Inter / Banner / AppOpen）
          │
          ▼
  FacebookAdSetting.Initialize()
  ├─ iOS 14.5+：SetAdvertiserTrackingEnabled(true)
  └─ Android/iOS：SetDataProcessingOptions(["LDU"], 0, 0)
          │
          ▼
  MaxSdk.SetIsAgeRestrictedUser / MaxSdk 其他全局设置
  InvokeEventsOnUnityMainThread = true
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
  ├─ Log.Debug 打印国家代码
  ├─ SetCreativeDebuggerEnabled(cfg.CreativeDebuggerEnabled)
  ├─ RegisterCallbacks()
  │    ├─ RegisterRVCallbacks()      → guard(m_RVPlacementId) → RegisterAdUnits + 注册 8 个事件
  │    ├─ RegisterInterCallbacks()   → guard(m_InterPlacementId) → RegisterAdUnits + 注册 7 个事件
  │    ├─ RegisterBannerCallbacks()  → guard(m_BannerPlacementId) → RegisterAdUnits + 注册 5 个事件
  │    └─ RegisterAppOpenCallbacks() → guard(m_AppOpenPlacementId) → RegisterAdUnits + 注册 7 个事件
  ├─ [条件] MaxSdk.ShowMediationDebugger()（cfg.MediationDebuggerEnabled == true）
  ├─ RaiseInitResult(true)           ← 通知聚合层初始化成功
  └─ initTcs.TrySetResult(true)
          │
          ▼
  await initTcs.Task 返回
  InitChannelSDKAsync 完成
```

**guard 说明：** `RegisterXxxCallbacks` 方法开头检查对应 `placementId` 是否为空字符串，为空则跳过注册，该格式的回调不会触发，`RequestAsync` 对该格式的调用也不会产生实际请求。

---

## §10 常见误区

### 误区一：Banner 展示使用 ShowAsync

Banner 的展示与隐藏不走 `OnShowAsync` 路径。`OnShowAsync` 仅处理全屏广告（Rewarded / Interstitial / AppOpen），Banner 通过 `ShowBanner()` / `HideBanner()` 直接控制。

```csharp
// 错误：Banner 调 ShowAsync 不会显示广告，框架层不路由到此路径
await adPlugin.ShowAsync(AdFormat.Banner, placementId, ct);

// 正确：先 RequestAsync 创建 Banner，再手动控制显示/隐藏
await adPlugin.RequestAsync(AdFormat.Banner, placementId, ct);
adPlugin.ShowBanner();
adPlugin.HideBanner();
```

### 误区二：在 MaxAdChannelConfig 中配置静音

`MaxSdk.SetMuted()` 的参数 `MuteAd` 由聚合层（`AdPluginBase`）在 `InitChannelSDKAsync` 调用前注入，不是 `MaxAdChannelConfig` 的字段。在 Config 里添加 `MuteAd` 字段不会生效，必须通过聚合层的静音接口统一控制。

```csharp
// 错误：MaxAdChannelConfig 没有 MuteAd 字段，此写法无法编译
config.MuteAd = true;

// 正确：通过聚合层接口设置，聚合层会在初始化时透传给各渠道插件
Nova.SDK.Get<IAdPlugin>().SetMute(true);
```

### 误区三：DisplayFailed 后等待 HiddenEvent

对于全屏广告（Rewarded / Interstitial / AppOpen），MAX SDK 在展示失败（`OnXxxDisplayFailed`）后**不会**触发 `OnXxxHidden` 回调。`MaxAdPlugin` 在 `DisplayFailed` 回调中直接调用 `m_XxxTcs.TrySetResult(失败 result)` 结束挂起，不依赖 `Hidden` 事件。如果在外部封装时假设"展示失败后一定还会有 Hidden 收尾"，将导致 `ShowAsync` 永远不返回。

```csharp
// 危险假设：以为 DisplayFailed 后还会收到 Hidden，结果 await 永远挂起
// 正确认知：DisplayFailed → TrySetResult(失败) → ShowAsync 立即返回失败结果
AdResult result = await adPlugin.ShowAsync(AdFormat.Rewarded, placementId, ct);
if (!result.IsSuccess)
{
    // DisplayFailed 路径在此处正常返回，无需额外超时保护
}
```

---

## §11 使用示例

### 初始化（由聚合层自动驱动，无需手动调用）

`MaxAdPlugin` 由 `AdPluginBase` 聚合层统一初始化，业务层只需确保 `MaxAdChannelConfig` 已正确配置在 ScriptableObject 中。

### 激励视频完整流程

```csharp
var adPlugin = Nova.SDK.Get<IAdPlugin>();

// 加载激励视频
await adPlugin.RequestAsync(AdFormat.Rewarded, placementId, ct);

// 展示并等待结果（挂起直到广告关闭或展示失败）
AdResult result = await adPlugin.ShowAsync(AdFormat.Rewarded, placementId, ct);

if (result.IsSuccess && result.IsRewarded)
{
    // 发放奖励
    GiveReward();
}
else
{
    // 展示失败或用户未观看完毕
    Log.Warning("RV not rewarded");
}
```

### 插屏展示

```csharp
var adPlugin = Nova.SDK.Get<IAdPlugin>();

await adPlugin.RequestAsync(AdFormat.Interstitial, placementId, ct);
AdResult result = await adPlugin.ShowAsync(AdFormat.Interstitial, placementId, ct);

if (!result.IsSuccess)
{
    Log.Warning($"Interstitial show failed: {result.ErrorCode}");
}
```

### Banner 控制

```csharp
var adPlugin = Nova.SDK.Get<IAdPlugin>();

// 创建 Banner（位置默认 BottomCenter，自适应高度已启用）
await adPlugin.RequestAsync(AdFormat.Banner, placementId, ct);

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
var adPlugin = Nova.SDK.Get<IAdPlugin>();

await adPlugin.RequestAsync(AdFormat.AppOpen, placementId, ct);
AdResult result = await adPlugin.ShowAsync(AdFormat.AppOpen, placementId, ct);
```

---

## §13 关联文档

- [`link.xml`](../link.xml) — IL2CPP 代码裁剪保留规则，确保 MAX SDK 反射入口不被剥离
- [`MaxAdPluginBuildProcessor.md`](./MaxAdPluginBuildProcessor.md) — 构建预处理器
