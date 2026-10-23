# MaxAdPlugin

> 最后更新：2026-10-15
> 当前代码事实：`UPMPackages/com.solotopia.nova.framework.sdk.max/Nova/Scripts/Runtime/**`

**类签名**：`[AdChannel(typeof(MaxAdChannelConfig))] public sealed partial class MaxAdPlugin : AdChannelPluginBase`
**命名空间**：`NovaFramework.SDK.MaxAdPlugin.Runtime`
**业务入口**：通过 `Nova.SDK.Get<IAdPlugin>()` 获取广告聚合接口；`MaxAdPlugin` 是聚合层创建的 MAX 渠道实例，不是该查询的返回类型。需要订阅结果时使用 `Nova.SDK.Get<AdPlugin>().Events`。

AppLovin MAX 广告渠道插件，负责 MAX SDK 初始化、激励视频/插屏/Banner/开屏的加载与展示，以及收入回调上报。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `MaxAdPlugin.cs` | `MaxAdPlugin` | 主实现：`override` 方法、SDK 初始化、格式分发 |
| `MaxAdPlugin.Visitors.cs` | `MaxAdPlugin` | 私有字段定义 |
| `MaxAdPlugin.Methods.cs` | `MaxAdPlugin` | 完整配置校验与注册、初始化结果、隐私查询、真实 ID 回调匹配及终态清理 |
| `MaxAdPlugin.Callbacks.cs` | `MaxAdPlugin` | MAX SDK 回调注册与反注册 |
| `MaxAdPlugin.Impression.cs` | `MaxAdPlugin` | 全屏展示与收益回调的进程内本地关联诊断 |
| `MaxAdPlugin.RV.cs` | `MaxAdPlugin` | 激励视频加载、展示、回调链 |
| `MaxAdPlugin.Inter.cs` | `MaxAdPlugin` | 插屏加载、展示、回调链 |
| `MaxAdPlugin.AppOpen.cs` | `MaxAdPlugin` | 开屏加载、展示、回调链 |
| `MaxAdPlugin.Banner.cs` | `MaxAdPlugin` | Banner 创建、位置控制、刷新控制、回调链 |
| `MaxAdPlugin.Track.cs` | `MaxAdPlugin` | MAX 特有加载属性与收益打点 |
| `MaxAdPlugin.UserId.cs` | `MaxAdPlugin` | 用户身份同步：override `SetUserId` 调用 `MaxSdk.SetUserId(userId)` |
| `MaxAdChannelConfig.cs` | `MaxAdChannelConfig` | 渠道配置数据对象，实现 `IAdChannelConfig` |
| `MaxAdGroupConfigValidator.cs` | `MaxAdGroupConfigValidator` | 公共纯校验器：解析四种格式并同时标记跨格式重复 ID 的双方 |
| `FacebookAdSetting/FacebookAdSetting.cs` | `FacebookAdSetting` | Facebook 广告 SDK 隐私设置内部工具类 |
| `Editor/BuildProcessor/MaxAdPluginBuildProcessor.cs` | `MaxAdPluginBuildProcessor` | 构建预处理写入 AppLovinSettings.asset、注册 Android ProGuard；iOS 后处理 Embed 并补齐 SKAdNetworkItems |
| `Editor/BuildProcessor/MaxSkAdNetworkPlistCompleter.cs` | `MaxSkAdNetworkPlistCompleter` | iOS 内部工具：离线枚举已装网络，再向官方接口请求并增量合并 SKAdNetwork ID |
| `Editor/BuildProcessor/MaxIOSPodOwnershipPostprocessor.cs` | `MaxIOSPodOwnershipPostprocessor` | iOS 顺序 44 后处理：核对已审官方 adapter 版本并将 LINE / Unity Ads 的两项原生依赖交给 CocoaPods |
| `Editor/BuildProcessor/MaxIOSPodOwnership.rb` | 固定 Ruby 脚本 | 定向回写生成工程的两项 MAX 引用与 Pod 声明，普通异常时恢复原始文件 |
| `Editor/MaxSDKUrlMenuItems.cs` | `MaxSDKUrlMenuItems` | MAX Console / MAX Readme 外部入口菜单 |
| `NovaFramework.SDK.MaxAdPlugin.Runtime.asmdef` | — | Runtime 程序集与 `NOVA_APPLOVIN_MAX` 版本宏 |
| `Editor/NovaFramework.SDK.MaxAdPlugin.Editor.asmdef` | — | Editor 程序集定义，仅 Editor 平台启用 |

---

## UPM 依赖

`MaxSdk`、`MaxSdkBase`、`MaxSdkCallbacks`、`MaxSdkUtils` 均来自官方 AppLovin UPM 包 `com.applovin.mediation.ads`。`com.solotopia.nova.framework.sdk.max` 的 `package.json` 必须声明该依赖，Runtime 程序集引用 `MaxSdk.Scripts`，Editor 程序集引用 `MaxSdk.Scripts.IntegrationManager.Editor`。

Mediation adapters 也由 [package.json](../../package.json) 固定版本声明为 AppLovin 官方 UPM 包，按平台拆为 `.android` / `.ios` 依赖。当前声明 BidMachine、ByteDance、Chartboost、Facebook、Fyber、Google、Google Ad Manager、InMobi、IronSource、Mintegral、Moloco、UnityAds、Vungle、Line 的双平台包，以及 Yandex Android 包；未声明 Yandex iOS。旧 `Core/MaxSdk/Mediation/**/Dependencies.xml` 不再作为依赖来源。Google / Google Ad Manager 当前固定 Android `25030000.0.0`、iOS `13040000.0.0`，不表示安装时自动追踪最新版。

本包依赖 Ad 基础包 `1.2.1`、Framework `0.7.3`、MAX SDK `8.6.6`，支持 Unity `6000.4.x`（最低 `6000.4.2f1`）。官方 SDK 还传递依赖 EDM4U；消费工程必须同时提供 AppLovin 与 EDM4U registry，安装示例见 [README](../../README.md#安装)。Nova PlugPals 会处理本包 `nova.scopedRegistries` 元数据；Unity 原生 Package Manager 不会自动把该元数据写入工程 manifest。

两个 asmdef 均通过官方包版本定义生成 `NOVA_APPLOVIN_MAX`（表达式 `8.5.0`）；Runtime 不限制平台，MAX 门面在 Android/iOS 使用 native SDK，在 Editor 和其他平台使用原厂 Editor 实现，不能据此视为桌面或 WebGL 真广告支持。Editor 程序集仅在 Editor 且非 `UNITY_WEBGL` 时参与编译。宏缺失时渠道初始化报告失败，广告能力不可用。

本包不再提交 AppLovin MAX SDK 本体文件；`UPMPackages/com.solotopia.nova.framework.sdk.max/Core/MaxSdk` 应保持为空或不存在。

---

## §3 继承关系

```
AdChannelPluginBase
  └── MaxAdPlugin  (sealed partial)
        └── [AdChannel(typeof(MaxAdChannelConfig))]  (特性，绑定配置类型)

IAdChannelConfig  <── MaxAdChannelConfig  (配置值对象，非插件本体)
```

`AdChannelPluginBase` 提供 `RaiseAdLoaded` / `RaiseAdLoadFailed` / `RaiseShowFailed` / `RaiseAdClosed` / `RaiseRevenue` / `TrackAdShow` / `TrackAdClick` / `RegisterAdGroups` / `GetRegisteredPlacementIds` / `RegisterAdUnits` / `RaiseAdCacheReplaced` / `MarkBannerHidden` 等受保护方法，子类通过这些方法向聚合层上报事件，不直接操作聚合层接口。

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_RVPlacementIds` | `IReadOnlyList<string>` | `null` | 激励视频真实广告ID快照，由已注册组计划展开 |
| `m_InterPlacementIds` | `IReadOnlyList<string>` | `null` | 插屏真实广告ID快照，由已注册组计划展开 |
| `m_BannerPlacementIds` | `IReadOnlyList<string>` | `null` | Banner 真实广告ID快照，由已注册组计划展开 |
| `BannerPlacementId` | `string` | `null` | Banner 控制 API 使用的首个广告位 ID；列表为空时返回 null |
| `m_BannerAutoRefreshIntervalSeconds` | `int` | `10` | Banner 自动刷新间隔，从 `MaxAdChannelConfig` 缓存，单位为秒 |
| `m_AppOpenPlacementIds` | `IReadOnlyList<string>` | `null` | 开屏真实广告ID快照，由已注册组计划展开 |
| `m_ActiveFullscreenPlacementId` | `string` | `null` | 当前全屏展示的真实 ID；结合各格式 TCS 匹配回调，终态或释放时清空 |
| `m_RVTcs` | `UniTaskCompletionSource<AdResult>` | `null` | 激励视频展示挂起句柄；`ShowRVAsync` 创建，`OnRVHidden`/`OnRVDisplayFailed` 取出并清空后完成 |
| `m_InterTcs` | `UniTaskCompletionSource<AdResult>` | `null` | 插屏展示挂起句柄；`ShowInterAsync` 创建，`OnInterHidden`/`OnInterDisplayFailed` 完成 |
| `m_AppOpenTcs` | `UniTaskCompletionSource<AdResult>` | `null` | 开屏展示挂起句柄；`ShowAppOpenAsync` 创建，`OnAppOpenHidden`/`OnAppOpenDisplayFailed` 完成 |
| `m_RVRewarded` | `bool` | `false` | 激励视频奖励标记；`OnRVReceivedReward` 置 `true`，`OnRVHidden` 读取后清零 |
| `m_BannerPosition` | `MaxSdkBase.AdViewPosition` | `BottomCenter` | Banner 当前位置；`UpdateBannerPosition` 同步更新 |
| `m_CreatedBannerPlacementIds` | `HashSet<string>` | 空集合 | 已创建 native Banner view 的广告位集合，用于避免重复 `CreateBanner` |
| `m_BannerDesiredVisible` | `bool` | `false` | 业务是否期望 Banner 可见；加载恢复后据此决定是否重新显示 |
| `m_CountryCode` | `string` | `null` | MAX SDK 初始化完成后由 `SdkConfiguration.CountryCode` 返回的国家代码；`GetCountryCode()` 对外返回该缓存值 |
| `m_IsUserConsentSet` | `bool` | `false` | MAX SDK 初始化完成时缓存用户是否已明确作出广告隐私授权决定 |
| `m_HasUserConsent` | `bool` | `false` | MAX SDK 初始化完成时缓存授权结果；仅在 `m_IsUserConsentSet` 为 `true` 时表示明确同意或拒绝 |
| `m_PrivacyFlowCompletionSource` | `UniTaskCompletionSource` | 新建未完成句柄 | 初始化完成、取消、异常或释放时结束隐私等待 |
| `m_IsSdkInitialized` | `bool` | `false` | 真实初始化加载门；成功并完成事件注册后才为 true，释放时重置 |
| `m_RevenueMonetizeTracker` | `IMonetizeTrackPlugin` | `null` | 变现插件缓存；通过 GetRevenueMonetizeTracker 按需获取，有值直接返回 |
| `m_RevenueAttributionTracker` | `IAttributionPlugin` | `null` | 归因插件缓存；通过 GetRevenueAttributionTracker 按需获取，有值直接返回 |
| `m_RevenueEventTracker` | `ITrackPlugin` | `null` | 通用埋点插件缓存；通过 GetRevenueEventTracker 按需获取，有值直接返回 |
| `m_RevenueTrackingSession` | `object` | `null` | 当前收益派发会话；释放时置空，派发前核对会话 |
| `m_FullscreenImpressionLock` | `object` | 新建锁对象 | 保护全屏曝光关联状态；收益回调可能来自 SDK 后台线程 |
| `m_FullscreenImpressions` | `Dictionary<AdFormat, FullscreenImpressionContext>` | 空字典 | 当前进程内各全屏格式的本地曝光关联状态，用于连接展示与收益回调 |
| `BannerIlrdInterval` | `int` | `5` | 继承自广告全局配置；Banner ILRD 聚合由 `AdChannelPluginBase` 统一处理，MAX 在满间隔时构造并上传自己的 `ad_ilrd` payload |

### MaxAdChannelConfig 配置字段

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_Enabled` | `bool` | `true` | 是否启用 MAX 渠道 |
| `m_AppKey` | `string` | `null` | AppLovin MAX SDK Key，构建预处理写入 AppLovinSettings |
| `m_AdMobAppIdAndroid` | `string` | `null` | AdMob Android App ID，构建预处理写入 AppLovinSettings.AdMobAndroidAppId |
| `m_AdMobAppIdIOS` | `string` | `null` | AdMob iOS App ID，构建预处理写入 AppLovinSettings.AdMobIosAppId |
| `m_RVPlacementGroups` | `List<string>` | 空列表 | 激励视频广告组表达式；每项一组，项内 `,` 或 `\|` 分隔真实 ID |
| `m_InterPlacementGroups` | `List<string>` | 空列表 | 插屏广告组表达式；每项一组，项内 `,` 或 `\|` 分隔真实 ID |
| `m_BannerPlacementIds` | `List<string>` | 空列表 | Banner 广告位 ID 列表；控制 API 使用首项 |
| `m_BannerAutoRefreshIntervalSeconds` | `int` | `10` | Banner 自动刷新间隔；字段 `[Range(10, 120)]`，运行时 getter 实际 Clamp 为 `5–120` 秒，两者下限当前不一致 |
| `m_AppOpenPlacementGroups` | `List<string>` | 空列表 | 开屏广告组表达式；每项一组，项内 `,` 或 `\|` 分隔真实 ID |
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

### 渠道配置与纯校验 API

`MaxAdChannelConfig` 的业务属性均为只读，序列化字段由 Config 面板维护；三个旧 `*PlacementIds` 访问器保留解析快照兼容，不返回原始组表达式。

```csharp
public AdChannelType Channel { get; } // MAX
public Type PluginType { get; }       // typeof(MaxAdPlugin)
public bool Enabled { get; }
public string AppKey { get; }
public string AdMobAppIdAndroid { get; }
public string AdMobAppIdIOS { get; }
public int BannerAutoRefreshIntervalSeconds { get; }
public IReadOnlyList<string> RVPlacementGroups { get; }
public IReadOnlyList<string> InterPlacementGroups { get; }
public IReadOnlyList<string> AppOpenPlacementGroups { get; }
public IReadOnlyList<string> RVPlacementIds { get; }
public IReadOnlyList<string> InterPlacementIds { get; }
public IReadOnlyList<string> AppOpenPlacementIds { get; }
public IReadOnlyList<string> BannerPlacementIds { get; }
public bool LogEnable { get; }
public bool CreativeDebuggerEnabled { get; }
public bool MediationDebuggerEnabled { get; }

// MaxAdGroupConfigValidator 的公开静态方法：纯校验，不修改配置或注册广告。
public static IReadOnlyDictionary<AdFormat, AdGroupPlanValidationResult>
    Validate(MaxAdChannelConfig config);
```

三个 `*PlacementGroups` 属性提供原始列表的不可写视图，后续配置编辑会影响下一次读取；运行时注册会物化独立计划。三个旧 `*PlacementIds` 属性每次解析当前对应格式并返回展开快照，格式自身无效时为空；跨格式重复必须用完整 `Validate` 检查，不能只凭旧 getter 判断。`BannerPlacementIds` 仍返回原列表的 `IReadOnlyList` 接口，业务不应强制转换后修改配置。

`Validate(null)` 返回四种格式均无效的诊断；有效空列表返回合法空计划。跨格式冲突同时使涉及的双方无效，错误不影响无关格式；该方法不会自动抛出构建失败异常。

### 用户身份同步（override，来自 MaxAdPlugin.UserId.cs）

```csharp
// 同步登录用户 userId 到 MAX SDK。
// 由 AdPlugin 的用户身份同步入口向已创建渠道转发。
// MaxSdk 门面统一跨平台（Android/iOS/Editor），无需 #if 分支。
public override void SetUserId(string userId)
```

SDKManager 的登录交付由聚合层逐渠道同步 `userId`，单渠道异常被隔离；MAX 不使用聚合登录入口的 `sessionId`。当前适配层没有独立的登出清 UID API，渠道释放也不调用原厂账号重置；业务不能把清空本地收益会话或开启 SDKManager 的新初始化周期解释为已经清除 MAX 原厂用户标识。

### 初始化与销毁

```csharp
// 继承的渠道生命周期入口；由聚合层对新渠道实例调用一次，要求 Unity 主线程。
// 初始化关联调用方与渠道生命周期令牌，不缓存重复初始化结果；已释放实例抛 ObjectDisposedException。
public UniTask InitializeAsync(IAdChannelConfig config, CancellationToken ct)

// 继承的共享释放入口；重复调用等待同一清理结果，ct 不用于中断必要清理。
public UniTask DisposeAsync(CancellationToken ct)

// 异步初始化 MAX SDK：先校验全部格式、注册全屏组/Banner 单 ID 并缓存真实 ID，
// 订阅初始化事件并调用 MaxSdk.InitializeSdk()；成功回调注册格式事件并开放加载，不自动预载
protected override async UniTask InitChannelSDKAsync(IAdChannelConfig config, CancellationToken ct)

// 释放：结束隐私与原生展示等待，解绑格式事件并销毁本渠道创建的全部 Banner
// 基类取消渠道生命周期后，初始化方法通过自身 finally 解绑本次初始化事件
// 由基类 DisposeAsync 调用；外部取消不跳过必要清理
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

三个全屏格式的 `OnIsReady(AdFormat format, string placementId)` 分别调用 `MaxSdk.IsRewardedAdReady`、`MaxSdk.IsInterstitialReady`、`MaxSdk.IsAppOpenAdReady`。基类在本地缓存复用、收益比较与展示前复查这些真实 ID；本地加载成功不代表缓存以后一直可播放。Banner 只沿用本地视图状态。

### 加载（Banner）

```csharp
// 首次请求时创建 Banner，并将背景色设为白色；同一 placementId 未销毁前不重复创建
// Request 阶段不设置 extra parameter；ad_refresh_seconds 在 StartBannerAutoRefresh() 中写入
// Banner 创建后是否显示由 ShowBanner()/HideBanner() 维护的业务可见性期望决定
protected override UniTask OnRequestAsync(AdFormat format, string placementId, CancellationToken ct)
// format == AdFormat.Banner 时路由至此路径
```

### 展示（Rewarded / Interstitial / AppOpen）

MAX 插屏收到 displayed 回调即表示满足本项目的插屏奖励条件，`OnShowCompleted` 的结果同时携带 `Success=true` 和 `RewardGranted=true`，不等待收益回调。插屏关闭结果继续保持 `RewardGranted=true`；展示失败时 `Success`、`RewardGranted` 均为 false。激励关闭时 `UserCompleted` 和 `RewardGranted` 都取 `m_RVRewarded`，开屏为 false。基类关闭入口不会用收益到达状态重新计算奖励资格，实际发奖由业务处理。

`Events.ShowCompleted` 在广告展示成功时通知；业务接口 `ShowAsync` 返回 `UniTask`，只在关闭或展示失败时结束等待，结果从事件读取。插屏 displayed 与 AdClosed 两个时点都可带有奖励资格，业务应选定一个入口处理同次展示的奖励。

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

// 更新 Banner 位置（屏幕左上角起算的坐标方式）；当前将 x/y 转 int 后直接传给 MAX。
public override void UpdateBannerPosition(Vector2 position)

// 将配置间隔写入 ad_refresh_seconds 后启动 Banner 自动刷新
public override void StartBannerAutoRefresh()

// 停止 Banner 自动刷新
public override void StopBannerAutoRefresh()

// 设置 Banner 宽度；当前转 int 后直接传给 MAX：Android 使用 dp，iOS 使用 points。
public override void SetBannerWidth(float width)

// 获取原厂自适应 Banner 高度。
// width < 0 时使用 Screen.width，否则转 int；直接委托 MaxSdkUtils.GetAdaptiveBannerHeight，无密度换算。
public override float GetAdaptiveBannerHeight(float width = -1)

// 设置 Banner 背景色
public override void SetBannerBackgroundColor(Color color)
```

---

## §6 初始化状态机

~~~text
InitChannelSDKAsync（聚合层创建渠道后只调用一次）
  → 校验完整配置，注册全屏广告组或 Banner 单 ID 槽位
  → 缓存真实 ID，沿原流程初始化 Facebook GDPR、静音与日志
  → 用本地委托订阅初始化事件，调用 MaxSdk.InitializeSdk
  → 原厂回调仅完成本地 TCS；主流程等待后切回 Unity 主线程
  → OnSdkInitializedCallback 缓存国家与授权，检查实际 IsSuccessfullyInitialized
  → 成功时设置调试器并注册格式事件，全部完成后开放加载
  → 结束隐私等待，通过基类队列派发 RaiseInitResult
  → finally 解绑本次初始化委托；取消或异常时也结束隐私等待
~~~

初始化一次指每个新建的 `MaxAdPlugin` 渠道实例只调用一次，由所属 `AdPlugin` 聚合层保证。SDKManager 完整释放后开启新的初始化周期，创建新 `IAdPlugin` 聚合及新 MAX 渠道；已释放实例不可复用，不能再次 `InitializeAsync`。MAX 不额外维护重复初始化等待身份、广告位注册标记或格式事件防重标记，也不提供并发或重复初始化结果缓存。状态结果仍由 `m_IsSdkInitialized` 表示，启动中或失败时基类请求返回 `-11`，不自动预载。

**注册说明：** 初始化先运行 MaxAdGroupConfigValidator，再从 RV/Inter/AppOpenPlacementGroups 注册拓扑，缓存 GetRegisteredPlacementIds 的真实 ID；原厂成功后只订阅有效且非空格式。相同渠道跨格式冲突双方无效；当前构建 processor 不调用此校验器，不会因广告组语法错误自动阻止 AppLovinSettings 注入，见 [构建处理器边界](./MaxAdPluginBuildProcessor.md#构建前广告配置边界)。

基类 `InitializeAsync` 入口要求已在 Unity 主线程；MAX SDK 设置沿该入口执行，原厂等待结束后再用 `SwitchToMainThread(ct)` 更新状态。`OnSdkInitializedCallback` 不再调用 PostAdCallbackToMainThread。基类已将初始化令牌关联渠道生命周期，释放会取消尚未完成的初始化等待，阻止迟到续体重新注册。

异常沿原调用链交由聚合层统一处理。唯一 `finally` 覆盖配置、Facebook 和 SDK 设置，也覆盖原厂初始化等待；使用不带取消令牌的主线程切换，保证取消或异常后仍能解绑和结束隐私等待。取消只结束 Nova 的等待，不能取消已经启动的原厂 SDK 初始化。初始化业务通知仍由既有队列隔离订阅者异常。

全部 30 个已注册广告回调（Rewarded 9、Interstitial 8、AppOpen 8、Banner 5）均在方法入口先调用 `Log.Debug(LogTag.Max, ...)`，再过滤回调、复制快照或派发到主线程，便于诊断未知广告位、重复或释放后到达的回调。加载与收益日志包含格式、广告位、网络、`revenueUSD` 和精度，失败日志包含错误码和消息，过期重载日志包含旧、新广告信息与 `expiredRevenueUSD / revenueUSD`。日志正文以 Rewarded / Interstitial / AppOpen / Banner 格式名开头，渠道由 `LogTag.Max` 标识，不重复写 MAX 前缀。日志与实际调用的输出边界见下文；`MaxAdChannelConfig.LogEnable` 仅控制 MAX 原厂详细日志。

当该 Debug 调用编入当前程序时，收益日志插值中的 `double → decimal` 转换发生在回调过滤及即时收益上报的保护范围之前；NaN、Infinity 或越界收入可能在这里抛出异常。即时上报的 try/catch 不能覆盖此前的格式化，不能据此宣称所有原厂异常收入都已隔离。

**线程说明：** MAX 广告状态回调先快照再排 Unity 主线程，加载状态、组批次及展示完成源均在主线程推进。RaiseRevenueImmediately 保留纯收益打点 SDK 原线程即时派发，收益槽位和业务事件进入主线程；Banner 累计持久化同样在主线程。displayed 不补载，hidden/displayFailed 匹配当前格式和真实 ID 后只收尾一次。

**展示等待：** MAX 使用各格式 TCS 和一个当前全屏广告位 ID，不再维护展示等待类、格式字典或额外 displayed 标记。全屏互斥与业务取消隔离由聚合层和基类负责，displayed 防重由基类负责；曝光关联层保留自己的展示标志供收益诊断。终态先取出局部 TCS 并清空成员，再调用 RaiseAdClosed/RaiseShowFailed，最后在 finally 完成局部 TCS，避免业务通知中重入展示后误完成新等待。Dispose 同样先取出并清空，再取消各局部完成源。

**完整释放：** MAX 渠道先关闭初始化加载门并结束隐私等待，取出并清空 RV、Interstitial、AppOpen 的展示完成源和当前广告位，复位奖励标记后再取消等待、清除曝光与收益会话，逐格式注销事件并销毁全部已创建 Banner。单个 Banner 销毁异常不会跳过其他广告位。最后清除 Banner 集合、期望可见性、国家代码和授权缓存；保留旧渠道实例直接查询时，`GetCountryCode()` 返回空字符串，`IsUserConsentSet()` / `HasUserConsent()` 返回 `false`。初始化仍由方法内的准确委托、局部 TCS 与 `finally` 配对退订，不另维护初始化会话字段。缓存复位只清理本渠道实例，不撤回已经发布的 SDK 数据或已进入外部插件的调用，也不撤销已发起的原生全屏展示。

### 共享释放与新生命周期

正常业务由所属 SDK Manager 驱动聚合释放，不直接释放、缓存或重建 MAX 渠道。聚合在配置注入、事件绑定和可选初始化之前登记渠道所有权；准备或初始化失败的 MAX 实例仍会在聚合释放时收回。释放先关闭聚合请求和事件桥接，取消挂起等待，等待渠道准备及实际初始化方法的 `finally` 退出，再准确解绑七类渠道事件并调用 MAX 的 `IAdChannelLifecycle.DisposeAsync(CancellationToken.None)`。单渠道失败被隔离，其余渠道继续清理。

MAX 所继承的基类释放先关闭回调代际和主线程队列，取消渠道生命周期、广告组重试和批次，再执行上文的 MAX 清理，最后清空槽位、组、配置失败缓存、打点/持久化管理器引用和业务事件。首次释放先发布共享完成源，重复或同步重入调用等待同一次真实清理；调用方令牌即使已经取消也不会跳过清理。基类清理异常返回同一共享结果，聚合会记录单渠道异常并继续。聚合等待初始化退出的保护适用于标准创建路径；业务不要绕过聚合并发手动初始化、释放同一渠道。

初始化调用方令牌与聚合生命周期仅在准备/初始化等待期间关联；渠道初始化方法成功退出后，再取消初始化调用方令牌不会关闭聚合桥接或释放渠道。业务结束本轮必须等待 SDKManager 完整释放，显式调用并等待其新初始化周期的 `InitializeAsync`，随后重新获取 `IAdPlugin` / `AdPlugin` 并向新 `AdPlugin.Events` 建立订阅。同一 SDKComponent 的 `Nova.SDK.InitializeTask` 仍返回首次初始化的缓存结果，不会触发或等待 SDKManager 的新初始化周期。旧聚合、MAX 缓存、订阅和回放不会自动迁移到新实例；业务拥有的旧订阅句柄仍须自行释放。MAX 的清理不撤销已经发起的原厂全屏播放，下一轮 native 初始化与跨生命周期晚到回调仍需目标平台验收。

### 本地调用与回调日志

实际 `MaxSdk.Show*` 调用前记录 `format / placement`。Show 日志位于曝光上下文和 `TrackAdShow` 之后，它表示 Nova 调用原厂接口，不保证原厂已接受或显示；实际展示结果继续由真实回调决定。

请求由基类在调用渠道加载入口前记录渠道名及 `format / group / round / reason / placement / item`。加载接口返回不表示加载成功，原厂 loaded / failed 回调记录真实广告 ID、网络或错误码与消息。当前项失败后，若同组还有下一项，基类继续串行请求下一真实 ID，可通过失败回调与紧接着的渠道请求核对。广告组新轮日志仅在展示后补量（`reason=AutoRefill`）和自动重试（`reason=Retry`）时记录；手动或默认业务请求只记录准备请求、实际渠道请求和平台加载回调。

全部 30 个已注册原厂回调在方法入口先记录，包含加载、展示、点击、奖励、关闭、收益和过期重载；回调日志不保证该回调被采纳或发布业务事件。常规流程只记录这些关键节点，Warning / Error 异常诊断保留。

这些日志复用现有 `Log.Debug(LogTag.Max, ...)`，展示后补量和自动重试的广告组新轮、实际渠道请求字段见 [AdChannelPluginBase](../../../com.solotopia.nova.framework.sdk.ad/Nova/Doc/AdChannelPluginBase.md#本地流程日志)，业务准备请求与展示见 [AdPlugin](../../../com.solotopia.nova.framework.sdk.ad/Nova/Doc/AdPlugin.md#本地请求与展示日志)。Editor 中 Debug 调用始终编入；Player 需要 `ENABLE_LOG`、`ENABLE_DEBUG_LOG` 或 `ENABLE_DEBUG_AND_ABOVE_LOG` 之一，实际可见性还受 LogHelper 和 Console 过滤影响。`LogEnable` 只控制 MAX 原厂详细日志。

### 隐私等待与缓存边界

`WaitForPrivacyFlowAsync(ct)` 是初始化期流程结束信号：未展示 CMP 时随初始化完成；展示 CMP 时由原厂初始化完成回调结束。初始化取消、异常和 Dispose 的清理也会结束该信号，因此完成任务本身不证明 SDK 初始化成功或用户同意。MAX 配置不自动开启 CMP、填写隐私政策 URL 或请求 ATT，相关原厂设置仍由项目与官方 SDK 配置。

`IsUserConsentSet()` / `HasUserConsent()` 只返回初始化完成时读取的缓存，不会自动刷新后续用户变更。首次启动时业务先等 `Nova.SDK.InitializeTask`，再等待聚合接口的隐私任务；SDKManager 新初始化周期应显式等待本周期的 `InitializeAsync` 后重新获取聚合；不要循环等待 `IsUserConsentSet()`。Facebook 的现有初始化只在 Android/iOS Player 写入 `LDU` 数据处理选项，不等于已设置 ATT 或统一管理所有网络授权。

### 收益打点插件的按需获取

- 初始化阶段不获取收益打点插件。实际派发 `ad_ilrd` / `ad_impression` 时分别调用三个获取方法：有缓存直接返回，缓存为空才向 SDK 管理器查询，成功后保存引用，仍不可用时记录警告。
- 插件查询沿用 `ISDKManager.TryGet` 的可用性规则。三个获取方法不判断当前线程，缓存为空时直接查询；查询失败或抛异常时逐目标 Warning，继续处理其他目标，不再排入主线程重试。底层 SDKManager 的集合没有并发同步，本适配层的异常隔离不等于为管理器提供线程安全保证。
- 直接查询后仍缺失时，逐目标记录包含事件名、接口名和原因的 Warning，并跳过该目标本次事件；下一次事件继续尝试。此路径不等待 SDK 初始化，也不额外存储失败事件补发。
- 释放渠道时清空缓存并结束会话，后续派发不再复用旧插件；已经进入插件的调用不撤回。
- 单插件异常继续隔离。派发返回值仍表示至少已尝试调用一个插件，不表示分析后台确认接收。Banner ILRD 聚合入口仍在主线程累计和查询，全缺失时保留既有累计批次，至少向一个目标尝试派发后按既有规则清空，不单独为缺失目标保留批次。
- 三种收益能力分别查询与派发；同一插件若同时实现多种能力，当前链路不按实例去重，可能收到多次调用。跨能力收口应按实际消费插件核对，不能据“每能力一次”宣称“每实例一次”。

### Banner 收益打点

- `ad_impression` 在每次 MAX Banner 收益回调中派发，不因 Banner 刷新频繁而节流；非 Banner 同样逐次派发。插件在当前回调线程按需获取并派发。
- MAX 全部 Banner 广告位的 `ad_ilrd` 共用同一组次数和收益累计，通过基类不带 `placementId` 的 `TrackBannerIlrdAggregated` 在主线程处理；默认值 `5` 表示总计第 5 次 Banner 收益回调时上传，已尝试派发后清空批次。上传载荷的广告位和网络信息来自触发阈值的那次回调。`BannerIlrdInterval=1` 表示逐次派发，`0` 或负数会停止 Banner ILRD 并清除旧累计；`ad_impression` 仍逐次派发。
- 未达到间隔的 Banner 累计次数和累计金额由 `AdChannelPluginBase` 通过渠道初始化时缓存的 `IFileFragmentManager` 写入 `NovaAdBannerIlrd` 分类的 `Nova.Ad.BannerIlrd.Max.Count` / `Nova.Ad.BannerIlrd.Max.Revenue` 条目；SDK 后台回调会切回主线程进行累计、保存和清理。启动链仍须先完成 `Persist.LoadAsync()`；原 `PlayerPrefs` 和旧的按广告位累计条目不自动合并。
- `ad_impression` 保留平台字段 `ad_format`，并补充与 Nova 广告事件一致的 `nova_ad_format`，便于跨事件统一筛选；`ad_ilrd.publisher_revenue` / `ad_ilrd.value` 为数值，`ad_ilrd.af_revenue` 为文本；未满间隔的内部存档金额使用 invariant decimal 字符串保存。

### 全屏广告曝光关联

- Rewarded、Interstitial、AppOpen 在调用 MAX 展示接口前生成进程内本地 `nova_ad_impression_id`；调用播放接口前上报 `nova_ad_show`；displayed / display failed 回调分别上报 `nova_ad_show_result`（`nova_ad_result=1/0`），`nova_ad_show`、`nova_ad_show_result`、`nova_ad_hidden` 与收益事件 `ad_ilrd`、`ad_impression` 共用同一次尝试的 ID。
- MAX 三种全屏广告关闭时，通过 `RaiseAdClosed(result, rewarded, extraProps)` 将当前曝光 ID 合并到 `nova_ad_hidden`。读取关闭属性不创建新 ID，也不删除上下文，保留晚到收益的关联；没有匹配上下文时不附加该字段，下一次展示仍生成新 ID。新增关闭重载需与包含该能力的 Ad 基础包配套发布。
- `ad_ilrd` 与 `ad_impression` 增加 `nova_ad_revenue_callback_index`，从 1 开始记录同一本地曝光收到的收益回调次数；`nova_ad_correlation_status` 取值为 `best_effort_match`、`revenue_before_show` 或 `orphan`。`best_effort_match` 只表示回调匹配到当前展示尝试，不表示 MAX 提供了原生唯一曝光证明。
- 收益回调先于 displayed 到达时仍复用 ShowAsync 已建立的 ID；没有展示上下文时生成诊断 ID 并标记 `orphan`。
- 当前关联层不按时间、素材或金额丢弃已采纳的收益回调；未注册广告位或已释放实例的回调仍由渠道门过滤。MAX `AdInfo` 没有可依赖的原生唯一曝光 ID，仅凭时间、素材和金额硬去重可能误删连续真实曝光；分析侧可按 `nova_ad_impression_id` 与回调序号识别重复形态，取得真机证据后再决定是否抑制。
- 同格式、同广告位的上一曝光若在下一次 `ShowAsync` 后才迟到回调，MAX 现有字段无法无歧义地区分它属于前一次还是当前一次；因此本地 ID 只能用于诊断候选重复，不能直接作为财务口径的自动去重键。
- 以上关联只覆盖当前运行进程；进程重启后的厂商回调无法与重启前本地 ID 建立强关联。

### 展示打点时序

- `ShowRVAsync` / `ShowInterAsync` / `ShowAppOpenAsync`：建立曝光上下文 → `TrackAdShow` → `MaxSdk.Show*`。
- `On*Displayed`：标记已展示并取得曝光 ID → `RaiseShowCompleted` → `nova_ad_show_result`（1）。
- `On*DisplayFailed`：保存曝光 ID 并清理未展示上下文 → `RaiseShowFailed` → `nova_ad_show_result`（0）→ 结束等待。
- `On*Hidden`：读取同次曝光 ID → `RaiseAdClosed` 保存业务属性并完成续杯 → `nova_ad_hidden` → 主线程关闭通知；调用方随后结束 `ShowAsync` 等待。
- 成功、失败与关闭打点都保留业务 `ShowCustomProps`；有匹配上下文时携带同次尝试的 `nova_ad_impression_id`。关闭读取不生成新 ID，下一次 `BeginFullscreenImpression` 才建立新的展示尝试。
- 没有就绪广告或预取消而未进入 MAX 播放接口时，不上报展示尝试。MAX 展示调用的同步异常作为明确失败收口并上报 `nova_ad_show_result=0`，结束等待；它不是厂商回调。

### 回调与埋点对照

| 事件 | 触发时机 | 全屏曝光关联属性 |
|---|---|---|
| `nova_ad_show` | 调用 MAX 展示接口前 | `nova_ad_impression_id` |
| `nova_ad_show_result` | displayed / display failed，或展示调用同步异常 | 同次 ID，另有 `nova_ad_result=1/0` |
| `nova_ad_hidden` | hidden | 匹配到的同次 ID，另有 `nova_can_get_reward` |
| `ad_ilrd` | 收益回调；Banner 达到累计阈值时派发 | 全屏 ID、收益回调序号及关联状态 |
| `ad_impression` | 每次收益回调 | 全屏 ID、同一收益回调序号及关联状态 |

以上是发送给分析平台的事件属性，不是 `AdResult` 的字段。`nova_ad_hidden` 表示广告关闭；激励是否达到奖励条件应看官方奖励回调状态。`RewardGranted` 与 `nova_can_get_reward` 的取值见展示章节及 Ad 包的 `AdPluginEvents` 文档。

比较 TGA 事件数量时先按广告格式筛选：非 Banner 每次收益回调各派发一条 `ad_ilrd` 和 `ad_impression`；Banner 的默认聚合阈值为 5，因此两者条数不要求相等。插件缺失时该目标本次事件会被跳过，需同时排查客户端 Warning；派发调用本身不代表后台确认入库。

### Banner 可见性恢复

- MAX Banner native view 按 placementId 幂等创建；同一个 placement 未 Destroy 前，后续 `RequestAsync(AdFormat.Banner)` 不重复 `CreateBanner`。
- 创建集合在原厂 Create 之前登记；Create 同步抛异常时该 ID 可能仍在集合中，后续请求不保证自动重新创建。`OnIsReady` 对已注册 Banner 返回 true，仍依赖基类本地 Ready，未查询原厂 Banner 对象是否存在。异常与 Destroy 后重建需要实际核对原厂资源状态。
- `ShowBanner()` 会记录业务期望 Banner 保持可见；`HideBanner()` / `DestroyBanner()` 会清除该期望。
- `ShowBanner()` 会先调用 `StartBannerAutoRefresh()`，`HideBanner()` 会先调用 `StopBannerAutoRefresh()`；启动时先对当前首个 Banner ID 写入 `ad_refresh_seconds`，再调用 MAX 自动刷新接口。
- `OnBannerLoaded` 成功后，如果业务期望仍为可见，会再次调用 `MaxSdk.ShowBanner(adUnitId)`，用于恢复加载失败后又成功的 Banner 展示。
- `DestroyBanner()` 会从已创建集合中移除 placementId；Destroy 后下一次 `RequestAsync(AdFormat.Banner)` 会重新创建 native view。
- Banner 专属控制方法固定操作 `m_BannerPlacementIds` 的首个 ID；虽然加载阶段支持多个真实 Banner ID 并行创建，业务配置应把实际展示用 ID 放在首位。当前有效聚合仅在最近一次 Banner 请求成功后记录活跃渠道；业务应先请求并检查结果，再从该聚合调用控制方法。MAX 的低层控制方法会直接调用原厂，没有额外的空首项或已释放旧引用保护，不作为绕过聚合的业务入口。
- Banner 自动刷新仍由 MAX 驱动。被动加载成功可以更新已注册 Banner 缓存并上报 fill；没有主动 Loading 的刷新失败不启动 Nova 全屏组轮或额外 fallback。重复请求已创建视图只恢复可见性或跳过创建，不保证发出新的原厂加载。

### MAX SDK 参数边界

- 当前实现不调用 `SetBannerExtraParameter(..., "adaptive_banner", "true")`；调用高度查询也不会启用自适应广告。MAX 8.6.4 的高度 helper 注明自适应能力来自 AdMob / Google Ad Manager，不能泛化为所有 mediation 网络已支持。
- `ad_refresh_seconds` 使用 `MaxAdChannelConfig.BannerAutoRefreshIntervalSeconds`，默认 `10` 秒；Inspector 字段范围为 `10–120`，getter 实际为 `5–120`。原厂是否采纳额外参数仍由 SDK 决定，不能以 getter 的 5 秒下限证明 native 已支持 5 秒刷新。
- `SetBannerWidth` 与坐标 API 直接把整数传给原厂；宽度是 Android dp / iOS points。高度 helper 也没有密度换算，当前默认又直接使用 Unity 的 `Screen.width`，不要将返回值直接当作 Canvas 像素高度；应在目标平台按实际布局核对。
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

对于全屏广告（Rewarded / Interstitial / AppOpen），展示失败后不能依赖 MAX 再发送 `OnXxxHidden`。适配器在匹配 `DisplayFailed` 时领取一次原生终态，先由基类解除占用并安排续杯，再完成本次原生等待；重复失败或随后到达的关闭不会再次收尾。关闭、展示失败及同步展示异常均保证即使事件派发异常也结束原生等待。

```csharp
// 正确认知：DisplayFailed → ShowFailed 事件 + ShowAsync 完成，不再等待 Hidden。
var adPlugin = Nova.SDK.Get<AdPlugin>();
var subscriptions = new List<IDisposable>();
adPlugin.Events.ShowFailed.Subscribe(result =>
{
    if (result.Format == AdFormat.Rewarded)
        Log.Warning(result.ErrorMessage);
}, subscriptions);

try
{
    await adPlugin.ShowAsync(AdFormat.Rewarded, ct: ct);
}
finally
{
    foreach (var subscription in subscriptions) subscription.Dispose();
}
```

---

## §11 使用示例

### 初始化（由聚合层自动驱动，无需手动调用）

`MaxAdPlugin` 由当前 `AdPlugin` 聚合层创建，每个渠道实例统一初始化一次；SDKManager 的新初始化周期使用新的聚合和渠道，不复用已释放实例。业务先把 `MaxAdChannelConfig` 配置到 `AdPluginConfig.ChannelConfigs.Items` 并正式导出 `ConfigRuntimeSO`，再等待 `Nova.SDK.InitializeTask`；不直接 new 渠道或再次调用初始化。AppKey 在构建期注入官方 `AppLovinSettings`，MAX Runtime 不在初始化时调用 `SetSdkKey`。

以下代码用于 SDKComponent 的首次启动；其 `InitializeTask` 缓存首次初始化结果。SDKManager 新初始化周期须显式等待本周期的 `InitializeAsync`，不能靠再次 await 旧 `InitializeTask` 启动或等待。

```csharp
await Nova.SDK.InitializeTask;
var ad = Nova.SDK.Get<IAdPlugin>();
await ad.WaitForPrivacyFlowAsync(ct);
bool hasExplicitDecision = ad.IsUserConsentSet();
bool consent = hasExplicitDecision && ad.HasUserConsent();
```

以下事件属于当前 `AdPlugin.Events`，ShowCompleted / ShowFailed / AdClosed 使用最近 32 条回放。SDKManager 开启新初始化周期时先释放旧订阅，再对新 `AdPlugin` 重新执行订阅流程；长期服务也不能继续持有旧接口。订阅发生时可能立即收到历史结果；示例先关闭本轮标记再订阅，且订阅句柄须随所属页面或流程释放。示例假定该业务流程是当前格式的唯一展示入口，多入口项目应统一协调展示与奖励归属。实际发奖还应使用业务请求标识做幂等，广告结果的本地曝光诊断 ID 不是财务或奖励凭证。

示例的 finally 会在等待取消时释放订阅，之后的原生关闭不会再由该流程发奖。业务若需要页面离开后仍兑现奖励，奖励订阅应归属能存活至原生终态的业务服务；取消 await 不会取消原生展示。

### 激励视频完整流程

```csharp
var adPlugin = Nova.SDK.Get<AdPlugin>();
var subscriptions = new List<IDisposable>();
bool waitingForThisShow = false;

// 先订阅再开启本轮标记，历史回放不能触发本轮奖励。
adPlugin.Events.AdClosed.Subscribe(result =>
{
    if (!waitingForThisShow || result.Format != AdFormat.Rewarded) return;
    waitingForThisShow = false;
    if (result.Success && result.UserCompleted && result.RewardGranted)
        GiveReward(); // 业务发奖接口还须按业务请求标识保证幂等。
}, subscriptions);

adPlugin.Events.ShowFailed.Subscribe(result =>
{
    if (!waitingForThisShow || result.Format != AdFormat.Rewarded) return;
    waitingForThisShow = false;
    Log.Warning($"激励视频展示失败：{result.ErrorMessage}");
}, subscriptions);

try
{
    // 单个广告位失败事件不等于本次请求失败；观察 RequestAsync 的汇总结果。
    var load = await adPlugin.RequestAsync(AdFormat.Rewarded, ct: ct);
    if (!load.Success || !adPlugin.IsReady(AdFormat.Rewarded)) return;
    waitingForThisShow = true;
    await adPlugin.ShowAsync(AdFormat.Rewarded, ct: ct);
}
finally
{
    waitingForThisShow = false;
    foreach (var subscription in subscriptions) subscription.Dispose();
}
```

### 插屏展示

```csharp
var adPlugin = Nova.SDK.Get<AdPlugin>();
var subscriptions = new List<IDisposable>();

adPlugin.Events.ShowFailed.Subscribe(result =>
{
    if (result.Format == AdFormat.Interstitial)
        Log.Warning($"插屏展示失败：{result.ErrorMessage}");
}, subscriptions);

try
{
    var load = await adPlugin.RequestAsync(AdFormat.Interstitial, ct: ct);
    if (load.Success && adPlugin.IsReady(AdFormat.Interstitial))
        await adPlugin.ShowAsync(AdFormat.Interstitial, ct: ct);
}
finally
{
    foreach (var subscription in subscriptions) subscription.Dispose();
}
```

### Banner 控制

```csharp
var adPlugin = Nova.SDK.Get<IAdPlugin>();

// 创建 Banner（位置默认 BottomCenter；当前不设置 adaptive_banner extra parameter）
var load = await adPlugin.RequestAsync(AdFormat.Banner, ct: ct);
if (!load.Success) return;

// 显示
adPlugin.ShowBanner();

// 查询原厂 helper 高度；当前以 Screen.width 透传，不会开启自适应广告。
// 接入 UI 前须核对目标平台密度、单位与实际布局。
float bannerHeight = adPlugin.GetAdaptiveBannerHeight();

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
        Log.Warning($"开屏展示失败：{result.ErrorMessage}");
}, subscriptions);

try
{
    var load = await adPlugin.RequestAsync(AdFormat.AppOpen, ct: ct);
    if (load.Success && adPlugin.IsReady(AdFormat.AppOpen))
        await adPlugin.ShowAsync(AdFormat.AppOpen, ct: ct);
}
finally
{
    foreach (var subscription in subscriptions) subscription.Dispose();
}
```

---

## §13 关联文档

- [`MaxAdPluginBuildProcessor.md`](./MaxAdPluginBuildProcessor.md) — 构建预处理器
- [Ad 聚合接口](../../../com.solotopia.nova.framework.sdk.ad/Nova/Doc/IAdPlugin.md) — 业务请求、展示和隐私查询
- [AdPluginEvents](../../../com.solotopia.nova.framework.sdk.ad/Nova/Doc/AdPluginEvents.md) — displayed、终态与回放事件
- [AdChannelPluginBase](../../../com.solotopia.nova.framework.sdk.ad/Nova/Doc/AdChannelPluginBase.md) — 组调度、真实 Ready、取消隔离与收益累计
- [Runtime 主实现](../Scripts/Runtime/MaxAdPlugin.cs)、[配置](../Scripts/Runtime/MaxAdChannelConfig.cs)、[配置校验器](../Scripts/Runtime/MaxAdGroupConfigValidator.cs)、[展示终态方法](../Scripts/Runtime/MaxAdPlugin.Methods.cs)、[回调注册与释放](../Scripts/Runtime/MaxAdPlugin.Callbacks.cs)、[收益实现](../Scripts/Runtime/MaxAdPlugin.Track.cs) — 当前事实依据

## 广告组配置与原厂边界

三种全屏 `*PlacementGroups` 字段仍为 `List<string>`，每项一组，项内用英文 `,` 或 `|` 分隔真实 ID。组间并行、组内按顺序尝试；当前项失败立即推进下一项，成功停止本组后续项。末项失败后按 `AdPluginConfig.ChannelConfigs.GroupRetryIntervalsSeconds` 中本组当前下标等待，再从本组首项开始新轮；默认整组失败依次等待 2、4、8、16、32、2 秒并持续循环，没有最大轮数。该数组由所有渠道与格式共用，但每组下标独立；空数组或任意非有限正数会使整个数组回退默认值。仅整组失败推进一次下标，任意项成功后本组下标归零。旧 `*PlacementIds` 属性每次解析当前配置并返回真实 ID 快照，无效表达式返回空列表；Unity 旧私有字段通过 `FormerlySerializedAs` 迁移。旧单 ID 列表成为多个单 ID 组，旧逐广告重试字段与配置访问器已删除。

初始化回调的实际 `IsSuccessfullyInitialized` 驱动加载门；启动中或失败时请求返回 `-11`，无自动预载。渠道初始化由聚合层对每个新实例调用一次，不额外提供重复初始化结果缓存；旧实例释放后不可复用，下一轮在 SDKManager 的新初始化周期使用新聚合及新渠道并重建业务订阅。`OnIsReady` 对接 MAX 三种全屏真实查询，缓存复用、收益比较与展示候选都只接收仍可播放的缓存。全屏 `Show` 互斥，第二次调用返回 Busy 且不覆盖原生完成源；单个业务等待取消不取消原生展示，占用保留到匹配 `hidden`、`displayFailed` 或 `Dispose`。基类不创建未 `displayed` 的诊断等待，也不以超时合成关闭、补载或解锁；MAX 原厂 `void Show` 内部静默拒绝仍无法保证自动恢复，业务仅能用自己的令牌结束等待。

Rewarded、Interstitial、AppOpen 的 expiry 重载事件只在无活动加载的 Idle / Waiting，或同一 Ready 缓存时采纳，并复查原厂 Ready；采纳后取消旧 timer、成功归零间隔下标，不结算旧业务请求批次。Loading / Showing 不作为主动加载终态，另一 Ready ID 也不会被替换。MAX 8.6.4 没有 Load 请求 token / Cancel Load，原厂自主 reload 不属于 Nova 主动组内串行保证。

MAX 8.6.6 的 iOS 原厂 `MAUnityAdManager.m` 已将奖励重载事件名修正为 `"OnExpiredRewardedAdReloadedEvent"`，与 C# `MaxSdkCallbacks` 使用的事件名一致。缓存失效仍通过真实 Ready 检查识别；奖励重载事件的设备表现须在目标工程验证。

### 验证边界

接入或升级时，须在目标工程执行 Android/iOS Player 构建、Xcode 原生编译、签名安装与真机运行，并按实际配置覆盖 CMP/ATT、四格式加载与展示、奖励、收益回调、Banner 单位与刷新、SDKManager 各初始化周期的新聚合/MAX 切换及跨生命周期晚到回调。文档说明、源码核对或 Editor 门面运行不能作为上述平台验收的替代；每次结果在当次对话报告，不沿用历史通过状态。
