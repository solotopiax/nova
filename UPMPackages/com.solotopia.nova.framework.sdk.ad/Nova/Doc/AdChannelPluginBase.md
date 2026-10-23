# AdChannelPluginBase

**类签名**：`public abstract partial class AdChannelPluginBase : IAdInternalPlugin, IAdChannelLifecycle`
**命名空间**：`NovaFramework.SDK.AdPlugin.Runtime`
**源码目录**：`Nova/Scripts/Runtime/AdChannelPluginBase/`

广告渠道共享基类，封装广告组加载、真实 Ready 查询、收益候选选择、展示生命周期、七类事件和通用打点。具体渠道负责初始化原厂 SDK，以及单个真实广告位的 Load / Ready / Show 调用；业务通过聚合层 `IAdPlugin` 使用广告，不传组 ID 或广告位 ID。

全屏广告默认组间并行、组内串行，无额外模式开关。注册了哪些格式的槽位，就具备哪些格式的调度能力；实际可用性还取决于配置校验、渠道初始化和原厂 Ready。

---

## §2 文件表

| 文件 | 职责 |
|---|---|
| `AdChannelPluginBase.cs` | 公开 API、初始化与释放模板、Ready 查询、展示入口及 Banner 默认实现 |
| `AdChannelPluginBase.Visitors.cs` | 渠道全局配置、组与槽位集合、请求批次、生命周期及回调队列字段 |
| `AdChannelPluginBase.Definitions.cs` | `AdUnit`、`AdGroup`、`RequestBatch`、状态枚举、展示占用和 `AdUnitOptions` |
| `AdChannelPluginBase.Groups.cs` | 组注册、轮次观察、组内推进、循环间隔重试及原厂缓存替换 |
| `AdGroupConfigParser.cs` | 组表达式解析、校验及与源配置隔离的深只读计划 |
| `AdChannelPluginBase.Methods.cs` | 单 ID 注册入口、真实 Ready 候选、原厂调用异常处理及展示收尾 |
| `AdChannelPluginBase.Threading.cs` | 回调快照、Unity 主线程 FIFO 队列和即时收益入口 |
| `AdChannelPluginBase.Track.cs` | `Raise*` 入口、通用打点、属性合并和 Banner 累计持久化 |
| `AdChannelConfigList.cs` | 渠道配置列表、全局开关和共享重试间隔配置 |

所有文件均位于上述源码目录。

---

## §3 继承关系

~~~mermaid
classDiagram
    IBannerControl <|-- IAdInternalPlugin
    IAdInternalPlugin <|-- AdChannelPluginBase
    IAdChannelLifecycle <|-- AdChannelPluginBase
    AdChannelPluginBase <|-- MaxAdPlugin
~~~

`IAdInternalPlugin` 继承 `IBannerControl`，不继承 `ISDKPlugin`。渠道实例由广告聚合插件 `AdPlugin` 创建和管理，不作为独立 SDK 插件走 SDKManager 的初始化链。基类提供 Banner 的空 virtual 实现，支持 Banner 的渠道按需重写。

---

## §4 关键字段与嵌套类型

### 渠道字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_AdGroups` | `Dictionary<AdFormat, List<AdGroup>>` | 按格式保存广告组，注册顺序用于同收益候选排序 |
| `m_AdUnits` | `Dictionary<AdFormat, List<AdUnit>>` | 按格式保存所有真实广告位槽位 |
| `m_PendingBatches` | `List<RequestBatch>` | 尚未完成的业务请求，各批次独立观察组轮次 |
| `m_AdConfigFailures` | `Dictionary<AdFormat, string>` | 格式配置错误；该格式不再进入 Load / Ready / Show 调度 |
| `m_ActiveAdShow` | `ActiveAdShow` | 本渠道三种全屏格式共用的展示占用 |
| `m_ChannelLifetimeCts` | `CancellationTokenSource` | 渠道托管生命周期；释放时取消 |
| `m_IsDisposed` | `bool` | 释放门，阻止新请求、旧回调和旧等待重新推进 |
| `m_ChannelDisposeCompletion` | `UniTaskCompletionSource` | 共享释放结果；重复调用等待同一次清理完成 |
| `m_AdCallbackQueue` | `Queue<Action>` | SDK 回调进入 Unity 主线程的 FIFO 队列 |
| `m_AdCallbackGeneration` | `int` | 释放时失效已排队动作的版本号 |
| `m_TrackPlugins` | `IReadOnlyList<ITrackPlugin>` | 初始化时从 `SDKComponent.GetAll<ITrackPlugin>()` 缓存的通用打点目标 |
| `m_BannerIlrdPersistManager` | `IFileFragmentManager` | 初始化时获取，累计回调复用，释放时清除 |

### 配置与派生类属性

| 属性 | 可见性 | 默认值 / 行为 |
|---|---|---|
| `EnableBidding` | internal | `true`；聚合层开启后按渠道收益选择，否则按渠道配置顺序；渠道内仍按最高收益选择槽位 |
| `BannerIlrdInterval` | protected internal | `5`；按收益回调次数累计；小于等于 0 时停止 Banner ILRD 累计上传并清除批次 |
| `MuteAd` | protected internal | `false`；渠道初始化时向原厂应用静音设置 |
| `GroupRetryIntervalsSeconds` | internal | `[2, 4, 8, 16, 32]` 秒；所有组使用同一配置序列，各组独立推进下标 |
| `IsChannelInitialized` | protected virtual | 默认 `true`；具备异步原厂初始化的渠道应重写，以真实成功结果开放加载 |
| `IsDisposed` | protected | 渠道是否已释放 |
| `ChannelLifetimeToken` | protected | 渠道生命周期令牌；不表示原厂提供了 CancelLoad / CancelShow |

ApplyGlobalConfig 在主线程执行；空配置或已释放渠道会忽略，其余调用会写入开关并复制有效间隔数组，正常由聚合在初始化前调用。运行中的渠道与后续配置编辑隔离。源列表为空，或任意间隔不是有限正数时，整个序列回退默认值，配置细节见 [AdPluginConfig.md](./AdPluginConfig.md)。

### 组、槽位和请求批次

| 类型 | 主要字段 | 含义 |
|---|---|---|
| `AdGroup` | `Format`、`Units`、`State`、`ActiveUnitIndex` | 一种格式中的有序广告位组，当前项可以是加载项、缓存项或展示项 |
| `AdGroup` | `RoundId` | 当前托管轮次，用于匹配业务观察、计时器和排队动作；不是原厂请求 token |
| `AdGroup` | `RetryIntervalIndex`、`RetryCancellation` | 下一次整组失败所用的间隔下标，以及本组当前等待任务 |
| `AdGroup` | `LastFailure`、`Reason`、`RequestCustomProps` | 当前轮最近失败、启动原因和属性快照 |
| `AdUnit` | `PlacementId`、`Format`、`Group`、`State` | 唯一真实广告位及所属组、格式和槽位状态 |
| `AdUnit` | `Revenue` | `double` 类型的预期收益，用于 Ready 候选比较；消费或失效后清零 |
| `AdUnit` | `LoadTerminalReceived`、`LastErrorMessage` | 当前加载终态是否已收到，以及最近失败说明 |
| `AdUnit` | `LastReason`、`RequestCustomProps`、`ShowCustomProps` | 单次请求与展示打点使用的属性 |
| `AdUnit` | `RewardGranted` | 收益到达标记；不用于推导业务 `AdResult.RewardGranted` |
| `RequestBatch` | `Tcs`、`PendingRounds`、`LastFailure`、`Registration` | 单次 `RequestAsync` 的完成源、待观察组轮次、最近失败和取消注册 |
| `ActiveAdShow` | `Format`、`PlacementId`、`Displayed` | 当前全屏占用及首次 displayed 防重状态 |

`AdUnitState` 为 `Idle / Loading / Ready / Showing`；`AdGroupState` 另有 `WaitingRetry`。重试计数和等待属于广告组，旧逐广告重试字段与最大重试次数配置已删除。

---

## §5 完整公开 API 与派生类入口

以下签名均来自当前基类；非 virtual 模板方法由基类统一实现。

### 初始化、配置与释放

~~~csharp
public abstract string Name { get; }
public abstract AdChannelType Channel { get; }

public void ApplyGlobalConfig(AdChannelConfigList globals);
public UniTask InitializeAsync(IAdChannelConfig config, CancellationToken ct);
public UniTask DisposeAsync(CancellationToken ct);

protected abstract UniTask InitChannelSDKAsync(IAdChannelConfig config, CancellationToken ct);
protected virtual UniTask DisposeChannelSDKAsync(CancellationToken ct);
protected virtual bool IsChannelInitialized { get; }
protected bool IsDisposed { get; }
protected CancellationToken ChannelLifetimeToken { get; }
~~~

- `Name` 写入 `nova_ad_channel`，也用于渠道日志和 Banner 存储键；`Channel` 返回实际渠道枚举。
- 聚合层在构造成功后先登记释放所有权，再应用全局配置、桥接七类事件、登记业务渠道，最后启动一次渠道初始化。
- InitializeAsync 在 Unity 主线程缓存存储接口与打点插件，将调用方令牌和渠道生命周期链接后传给 InitChannelSDKAsync。该局部链接在方法退出时释放，不把成功初始化后的原调用方令牌长期绑定到渠道；聚合准备流程在返回后还会检查初始化取消。
- 派生类负责注册槽位、原厂事件和 SDK 初始化。初始化成功应根据真实原厂结果开放 `IsChannelInitialized`，并调用 `RaiseInitResult`；该通知本身不会替派生类设置成功门，也不会自动预载。
- `DisposeAsync` 首次调用先保存共享完成源，再关闭入口、失效回调队列、取消渠道生命周期及组等待与业务批次，以 `CancellationToken.None` 调用原厂清理，最后在主线程清除状态、引用和事件。原厂清理不因调用方令牌已取消而跳过；重复调用等待同一次清理，清理失败时观察到同一异常。单阶段取消或原厂清理失败仍继续后续必要清理。
- 基类实现可选接口 IAdChannelLifecycle，MAX、AdMob 等派生类自动具备该契约，原有模板方法签名保持不变。聚合层先等待准备与初始化方法退出，再调用渠道释放；派生初始化必须响应取消，且不能反向等待所属聚合插件的释放任务。
- 已释放的基类渠道不可再次 Initialize，聚合本身也不支持释放后同对象再初始化。SDKManager 新一轮创建新聚合和新渠道，每个新渠道仍只初始化一次；业务重新获取聚合并建立订阅。直接重复初始化一个尚未释放的实例没有统一幂等门，不应绕过聚合所有权调用。

### 注册与配置错误

~~~csharp
protected void RegisterAdGroups(AdFormat format, IReadOnlyList<string> groupExpressions);
protected IReadOnlyList<string> GetRegisteredPlacementIds(AdFormat format);
protected void RegisterAdConfigFailure(AdFormat format, string message);

protected void RegisterAdUnits(AdFormat format, IReadOnlyList<AdUnitOptions> options);

public readonly struct AdUnitOptions
{
    public readonly string PlacementId;
    public AdUnitOptions(string placementId);
}
~~~

`RegisterAdGroups` 先解析并校验整份计划，再累加组与槽位；一份计划失败不会只注册其中一部分。表达式规则与跨格式冲突见 §9。`RegisterAdConfigFailure` 可供渠道完整配置校验器记录格式错误。`GetRegisteredPlacementIds` 返回按组序及组内顺序展开的只读快照，未配置、配置无效或已释放时为空。

配置注册属于初始化阶段，不能当作运行时热替换协议。登记格式失败会阻止后续新请求，但不会主动删除旧槽位、终止在途原厂加载或结算既有观察批次；已开始的一轮不保证因后来修改配置而自动退出。需要另一份拓扑时应沿完整生命周期创建新渠道。

`RegisterAdUnits` 保留既有调用签名，每个真实 ID 注册为一个独立单 ID 组，并累加到已有注册；空 ID 或已注册 ID 会 Warning 后跳过。该入口不拆字符串，避免改写外部渠道的真实 ID。单 ID 组首次请求仍并行，失败后使用统一组间隔序列。

### 渠道配置、类型绑定与共享解析器

以下类型同属 `NovaFramework.SDK.AdPlugin.Runtime`，源码分别位于 `Interfaces/`、`Types/` 和 `AdChannelPluginBase/AdGroupConfigParser.cs`。

| 类型 / 成员 | 当前契约 |
|---|---|
| `IAdChannelConfig.Channel` | `AdChannelType`，渠道标识 |
| `IAdChannelConfig.PluginType` | `Type`，聚合层实际创建的渠道类型 |
| `IAdChannelConfig.Enabled` | `bool`，是否启用该配置 |
| `RVPlacementIds` / `InterPlacementIds` / `BannerPlacementIds` / `AppOpenPlacementIds` | `IReadOnlyList<string>`，真实广告 ID；不应把含分隔符的组表达式传给原厂 |
| `AdChannelType` | `MAX=0`、`AdMob=1`、`IronSource=2`；枚举存在不表示已有可用渠道实现 |
| `AdChannelAttribute.ConfigType` | `Type`，声明插件对应的配置类型；构造器为 `AdChannelAttribute(Type configType)` |

当前 MAX / AdMob 配置额外提供三类全屏的 PlacementGroups 原始表达式；这些属性不在 IAdChannelConfig 上，旧 PlacementIds 访问器返回解析后的真实 ID 快照。MAX 按表达式调用 RegisterAdGroups，当前 AdMob 展开后调用 RegisterAdUnits，实际是多个独立单 ID 组。Banner 继续使用真实 ID 列表。创建渠道按配置的 `PluginType` 执行，当前运行时不会依靠 `AdChannelAttribute` 扫描、发现或查找配置；该特性只声明类型关联，也不执行配置类型合法性校验。

IAdInternalPlugin : IBannerControl 供聚合层调度渠道，不继承 ISDKPlugin；业务优先使用 IAdPlugin。它声明 Channel、用户身份 / 国家码 / 隐私方法、GetMaxRevenue、RequestAsync、IsReady、ShowAsync 和下文七类 C# 事件。IAdChannelLifecycle 是独立可选接口，声明 InitializeAsync(IAdChannelConfig, CancellationToken) 与 DisposeAsync(CancellationToken)；基类已实现，但不能从旧内部接口推断任意自定义渠道都具备生命周期。未实现它的旧渠道不自动初始化，释放时仅在实现 IDisposable 时同步兜底，否则资源由渠道自行管理。

直接实现旧接口的渠道不会自动获得基类广告组、真实 Ready、回调排队或展示占用。聚合事件桥接直接转发原事件线程；其 Show 与隐私等待只透传业务 ct，自定义渠道应自行响应取消并在释放时结束等待，详见[聚合取消边界](./AdPlugin.md#取消令牌的作用范围)。

~~~csharp
public static AdGroupPlanValidationResult Parse(AdFormat format, IReadOnlyList<string> groupExpressions);

public sealed class AdGroupPlanValidationResult
{
    public IReadOnlyList<IReadOnlyList<string>> Groups { get; }
    public IReadOnlyList<string> EffectivePlacementIds { get; }
    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }
    public bool IsValid { get; }
    public AdGroupPlanValidationResult(IReadOnlyList<IReadOnlyList<string>> groups, IReadOnlyList<string> effectivePlacementIds, IReadOnlyList<string> errors, IReadOnlyList<string> warnings);
}
~~~

`Parse` 属于 `AdGroupConfigParser`，不调用原厂或缓存可编辑配置。`Groups` 按组及尝试顺序保存 ID，`EffectivePlacementIds` 按同序展开且不去重；`Errors` 阻止注册，`Warnings` 只诊断，`IsValid` 等于没有错误。未配置格式是合法空计划。无效计划仍可能保留已识别拓扑，必须先检查 `IsValid`。构造器深复制并包装所有集合，空输入转为空只读集合；它不会重新执行解析或完整配置校验。

### 请求、Ready 与展示

~~~csharp
public UniTask<AdLoadResult> RequestAsync(AdFormat format, AdRequestReason reason = AdRequestReason.Auto, Dictionary<string, object> customProps = null, CancellationToken ct = default);
public bool IsReady(AdFormat format);
public virtual float GetMaxRevenue(AdFormat format);
public UniTask<AdResult> ShowAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);
public bool IsAdPlaying(AdFormat format);

protected virtual bool OnIsReady(AdFormat format, string placementId);
protected virtual UniTask OnRequestAsync(AdFormat format, string placementId, CancellationToken ct);
protected virtual UniTask<AdResult> OnShowAsync(AdFormat format, string placementId, CancellationToken ct);
protected virtual UniTask WaitForGroupRetryAsync(float seconds, CancellationToken ct);
~~~

- `RequestAsync` 启动空闲组、观察正在加载的组轮次。首个被观察组成功即返回；全部被观察轮失败时返回最后一次实际失败，后台组重试继续。
- 若已有真实 Ready 缓存，仍先启动其他空闲组，再返回首个按注册顺序找到的缓存结果。缓存返回不是最高收益选择；最高收益由展示候选和 `GetMaxRevenue` 选择。
- `IsReady`、默认 `GetMaxRevenue` 和 `ShowAsync` 候选都同时检查本地 Ready 与 `OnIsReady`。查询发现失效缓存会重置该组；单纯查询不会发 Load。
- `GetMaxRevenue` 无候选时返回 0，保持 virtual 签名，允许外部渠道重写。
- `ShowAsync` 选择收益最高的真实 Ready 槽位；收益相同保留注册顺序。三种全屏格式在本渠道互斥，聚合层还检查跨渠道占用。
- `OnRequestAsync` 每次只处理基类选定的一个真实 ID；任务完成表示调用已发出，加载终态由 `RaiseAdLoaded / RaiseAdLoadFailed` 提供。
- `OnShowAsync` 必须等待真实关闭或展示失败，并调用对应 `Raise*` 入口。仅返回一个结果不能替代状态收尾。业务取消只结束外层等待，传入上述两种原厂调用的令牌是 `ChannelLifetimeToken`。
- `OnIsReady` 默认返回 true，仅适合完全依赖本地状态的既有渠道；有原厂 Ready API 的渠道应接入真实查询，查询异常按不可播处理。
- 原厂调用默认实现抛 `AdFormatNotSupportedException`；支持相关格式的渠道必须重写。
- `WaitForGroupRetryAsync` 默认按真实秒数等待，不受 `Time.timeScale` 影响；该 protected virtual 入口允许派生渠道替换等待实现。

请求失败与取消的区分：

| 场景 | 返回 / 行为 |
|---|---|
| 格式配置错误 | `Success=false`，`ErrorCode=-10`，不发 Load |
| 渠道尚未初始化成功 | `Success=false`，`ErrorCode=-11` |
| 未注册格式 | `Success=false`，`ErrorCode=-1` |
| 只有展示中等不可请求的组 | `Success=false`，`ErrorCode=-2`；WaitingRetry 有最近失败时直接返回该失败 |
| `OnRequestAsync` 异常，或渠道未释放时该调用取消 | 当前活动项失败，`ErrorCode=-3`，继续按组规则推进 |
| loaded 后一次 Update 复查仍不可播 | 当前活动项失败，`ErrorCode=-13` |
| 调用方预取消或渠道已释放 | 取消任务，不发新的原厂调用 |
| 原厂返回加载失败 | 保留原厂错误码、消息和真实 ID |

ShowAsync 无真实 Ready、未初始化或已有全屏占用时返回失败 AdResult，不发 Show；调用方已取消或渠道已释放时返回取消任务。聚合层 Busy 通知另见 [AdPlugin.md](./AdPlugin.md)。已发出的展示不会因业务取消而解除占用。

### 用户身份、国家码与隐私

~~~csharp
public virtual void SetUserId(string userId);
public virtual string GetCountryCode();
public virtual bool IsUserConsentSet();
public virtual bool HasUserConsent();
public virtual UniTask WaitForPrivacyFlowAsync(CancellationToken ct = default);
~~~

默认 SetUserId 为空实现，国家码为空字符串，授权查询为 false，隐私等待立即完成；基类默认隐私方法没有取消检查，也不包装派生类的 override。MAX / AdMob 在各自实现中处理等待取消与释放结算。授权查询必须结合 IsUserConsentSet 区分明确拒绝与尚未设置；隐私任务完成也不表示初始化成功，启动门禁应按需检查实际可用性，不能无限轮询是否已有决定。

聚合层通过 `ISDKLoginReceiver` 接收 SDKManager 当前账号，并逐渠道调用 `SetUserId`；单渠道异常被隔离。渠道不自行订阅旧 UserLogin 事件，原厂是否要求初始化后才能设置身份由渠道适配负责。

### Banner 控制

~~~csharp
public virtual void ShowBanner();
public virtual void HideBanner();
public virtual void DestroyBanner();
public virtual void UpdateBannerPosition(BannerPosition position);
public virtual void UpdateBannerPosition(Vector2 position);
public virtual void StartBannerAutoRefresh();
public virtual void StopBannerAutoRefresh();
public virtual void SetBannerWidth(float width);
public virtual float GetAdaptiveBannerHeight(float width = -1f);
public virtual void SetBannerBackgroundColor(Color color);

protected void MarkBannerHidden(string placementId);
~~~

Banner 预加载后通过这些专属 API 控制，不走全屏 `ShowAsync`。基类默认空实现，自适应高度默认 -1。渠道隐藏或销毁 Banner 后调用 `MarkBannerHidden`，仅重置对应槽位与单 ID 组，不自动续杯。

### 回调与状态入口

~~~csharp
protected void RaiseInitResult(bool success);
protected void RaiseAdLoaded(AdLoadResult e);
protected void RaiseAdLoadFailed(AdLoadResult e);
protected void RaiseRevenue(AdEvent e);
protected void RaiseRevenueImmediately(AdEvent e, Action immediateAction = null);
protected void PostAdCallbackToMainThread(Action action);

protected void RaiseShowCompleted(AdResult result);
protected void RaiseShowCompleted(AdResult result, Dictionary<string, object> extraProps);
protected void RaiseShowFailed(AdResult result);
protected void RaiseShowFailed(AdResult result, Dictionary<string, object> extraProps);
protected void RaiseAdClosed(AdResult result, bool rewarded = false);
protected void RaiseAdClosed(AdResult result, bool rewarded, Dictionary<string, object> extraProps);

protected void RaiseAdCacheReplaced(AdFormat format, string placementId, double revenue);
~~~

结果类 Raise* 复制结果及属性字典结构，RaiseInitResult 使用 bool，然后在主线程更新状态和通知业务；字典值中的业务对象不承诺深复制。直接在 SDK 回调中调用 `Raise*` 即可；原厂可复用的 `AdInfo / ErrorInfo` 必须由渠道先复制所需字段，不能延后读取原厂对象。

`PostAdCallbackToMainThread` 用于还需操作渠道字段、原厂 API 或完成源的回调。已在正式 FIFO drain 内的嵌套 `Raise*` 会同步推进，便于渠道先结束状态再完成自己的展示等待。传入空 action 会抛异常。

`RaiseRevenueImmediately` 先在原 SDK 回调线程执行 `immediateAction`，再让 `RaiseRevenue` 排入主线程写槽位收益和派发业务事件。即时操作异常有日志并继续收益通知；即时操作不应访问 Unity 状态或存储接口。`RaiseRevenue` 本身不代替渠道上传原厂特有的 `ad_ilrd / ad_impression`。

`RaiseAdCacheReplaced` 用于原厂自主过期重载，具体采纳边界见 §9。

### 打点辅助方法

~~~csharp
protected void TrackAdRequest(AdFormat format, string placementId, AdRequestReason reason, Dictionary<string, object> customProps);
protected void TrackAdShow(AdFormat format, string placementId);
protected void TrackAdShow(AdFormat format, string placementId, Dictionary<string, object> extraProps);
protected void TrackAdClick(AdFormat format, string placementId);
protected void TrackBannerIlrdAggregated(double revenue, Func<decimal, bool> trackAction);
protected void TrackBannerIlrdAggregated(string placementId, double revenue, Func<decimal, bool> trackAction);
~~~

`TrackAdRequest` 由基类在每次实际调用 `OnRequestAsync` 前自动上报，渠道不再重复调用。`TrackAdShow` 由渠道在实际调用原厂 Show 前记录展示尝试；displayed 回调只调用 `RaiseShowCompleted`，避免重复 show。点击入口在主线程取本次展示属性后上报。

Banner 不带 `placementId` 的累计重载汇总当前渠道全部 Banner；带 ID 的重载保持按广告位累计的既有语义。`trackAction` 收到 decimal 累计收益，返回 true 表示已尝试派发，基类据此清除批次，不表示分析后台已确认入库。

### 七类事件

| 事件 | 类型 | 含义 / 通用打点 |
|---|---|---|
| `OnInitResult` | `Action<bool>` | 原厂初始化结果；`nova_ad_init` |
| `OnAdLoaded` | `Action<AdLoadResult>` | 已接受的单 ID 加载成功；`nova_ad_fill`；也允许 Banner 被动刷新成功 |
| `OnAdLoadFailed` | `Action<AdLoadResult>` | 已接受的单 ID 尝试失败；`nova_ad_fill_fail`，不代表整个业务 Request 失败 |
| `OnShowCompleted` | `Action<AdResult>` | 首次匹配 displayed；`nova_ad_show_result`（`nova_ad_result=1`），不代表关闭或激励完成 |
| `OnShowFailed` | `Action<AdResult>` | 匹配的展示失败；`nova_ad_show_result`（`nova_ad_result=0`） |
| `OnAdClosed` | `Action<AdResult>` | 匹配的关闭；`nova_ad_hidden` |
| `OnAdRevenuePaid` | `Action<AdEvent>` | 收益业务通知；具体收益打点由渠道的即时操作处理 |

七类事件均在主线程触发，由 `AdPlugin` 桥接到 `AdPluginEvents`。渠道只调用基类 `Raise*`，业务通过聚合层订阅事件。

---

## §6 生命周期状态机

### 插件级生命周期

~~~text
创建渠道
  → ApplyGlobalConfig
  → 桥接事件并登记渠道
  → InitializeAsync：缓存存储与打点插件
  → InitChannelSDKAsync：注册广告组与原厂事件、等待真实初始化结果
  → 初始化成功后开放 Request / Ready / Show

DisposeAsync
  → 关闭入口、失效排队回调、取消渠道生命周期
  → 取消组计时器与业务请求批次、清除全屏展示占用
  → DisposeChannelSDKAsync：解绑原厂事件、销毁视图、结束渠道原生等待
  → 清除组、槽位、引用与七类事件
~~~

释放后回调和旧 timer 不再发新 Load，同一渠道实例不能再次初始化。初始化调用方令牌在方法退出后解除链接，成功后再取消它不等于释放渠道；取消托管初始化等待也不能撤销已经启动的原厂 SDK 初始化。

### 组与槽位状态

~~~text
Idle
  → Request：从组首项开始当前轮
  → Loading
      ├─ 当前项失败且有后项：保持 Loading，串行尝试下一项
      ├─ 任意项成功且真实 Ready：Ready，停止后项，重试下标归零
      └─ 末项失败：WaitingRetry
           → 等待本组当前间隔
           → 新轮从首项 Loading

Ready
  ├─ 原厂缓存已失效：Idle，等待普通请求补充
  └─ Show：Showing
       ├─ displayed：保持 Showing，不补载
       └─ hidden / displayFailed：Idle
            → 全屏格式 AutoRefill 检查全部组
~~~

槽位随组当前项进入 Loading / Ready / Showing；组等待重试时，失败槽位回 Idle。Banner 为单 ID 组，专属视图控制不占用全屏展示；原厂刷新成功可更新缓存，隐藏只重置自身组。

---

## §8 初始化时序与线程

1. AdPlugin 按已启用的 IAdChannelConfig.PluginType 创建渠道实例，立即登记释放所有权，再应用 AdPluginConfig.ChannelConfigs 中的全局配置。
2. 聚合层先桥接事件并登记渠道，再启动 `InitializeAsync`，避免同步原厂回调早于事件订阅。
3. 基类缓存 IFileFragmentManager 和 SDKComponent.GetAll<ITrackPlugin>()，仅在初始化方法内链接初始化令牌与渠道生命周期。
4. 派生渠道校验并注册真实广告位，完成原厂初始化，以实际成功结果驱动 `IsChannelInitialized`，调用 `RaiseInitResult`。
5. 聚合层转发初始化结果及国家码；业务随后主动请求广告，基类没有初始化自动预载。

状态查询、请求、展示、配置和注册入口由 Unity 主线程调用，这些基类入口在后台线程会明确抛错；DisposeAsync 自行切回主线程。Banner 与身份 / 国家码 / 隐私方法为可重写入口，默认空实现不执行线程检查，派生类仍须遵守主线程调用约定。SDK 回调通过 `Raise*` 或 `PostAdCallbackToMainThread` 串行进入主线程 FIFO，原厂 Load / Ready / Show、组与槽位、业务批次、完成源以及通用打点均在该流程推进。

队列先切主线程并让出一次 PlayerLoop 再执行，避免在原厂同步 Load 回调栈中递归切换广告位。单个队列动作异常有日志，后续动作继续；通用打点单目标异常隔离，不阻断其他打点目标。

业务广告事件本身通过普通 C# 委托同步调用，不会逐个隔离订阅者异常。某个订阅者抛错会跳过同次委托后续订阅者及当前队列动作尚未执行的语句；外层 FIFO 记录该动作异常后继续处理后续动作。不要将“队列继续”解释为当前通知已完整交付或全部业务回调都成功。

纯收益即时上传保留原 SDK 线程路径。Banner 累计、存档及阈值派发在主线程执行。初始化缓存存储引用不等于加载存档，启动链必须先完成 `Persist.LoadAsync()`。

---

## §9 关键算法与边界

### 广告组配置与注册

`List<string>` 每项为一组；项内英文逗号或竖线分隔串行 ID，允许混用。Trim 后过滤空项并保持顺序。重复 ID 按大小写敏感的原值判错，不自动去重；分号、中文分隔符和 JSON 结构 / 字符串语法无效。

~~~csharp
// 两个并行组：第一组依次尝试 a1 → a2 → a3，第二组只有 b1。
RegisterAdGroups(AdFormat.Rewarded, new[] { "a1,a2|a3", "b1" });

// Banner 每项必须是单个真实 ID，不允许组内分隔符。
RegisterAdGroups(AdFormat.Banner, new[] { "banner1", "banner2" });
~~~

同一渠道中的 ID 只能归属一个组、一个格式。解析器检查本次格式计划内重复，基类再检查已有注册冲突；跨格式冲突会标记双方格式失败。MAX 在注册前还有完整配置校验器，其他合法格式仍可使用。不同渠道的 ID 不受这一渠道内约束。

### 请求批次与组轮次

~~~text
RequestAsync：
  检查取消、释放、配置和初始化门
  扫描全部组：
    Ready 且原厂 Ready → 记住首个缓存
    Ready 但已失效 → 重置该组并预留新轮
    Idle → 预留首项，记录要启动的组和本批观察轮次
    Loading → 观察已有轮次，不重复 Load
    WaitingRetry → 不提前启动，保留最近失败
    Showing → 不抢占，不加入等待
  若无缓存且有观察轮次 → 先登记完整批次和取消注册
  再发所有预留 Load，即使已有缓存也补其他空组
  返回缓存 / 本批任务 / 当前不可请求失败

组当前轮成功：
  停止后项并保存缓存 → fill → 先移除完成批次再完成 TCS → OnAdLoaded

当前项失败：
  预留后项并排队，或整组耗尽后建立 timer
  → fill_fail
  → 仅整组耗尽才通知观察批次
  → OnAdLoadFailed
~~~

每次调用独立持有 `RequestBatch`，观察的是组和 `RoundId`，切到组内后项不会失去等待目标。任一观察组成功完成本批；全部观察轮失败才返回最近实际失败。批次完成前先移出集合并释放取消注册，再触发可能重入请求的续体。

业务令牌取消只移除自身观察者，原厂 Load、其他观察者和后台重试继续。仅 WaitingRetry / Showing 时立即返回，不创建无限等待批次。SDK 一直没有终态时不自动换 ID，也不新增加载超时；业务通过自己的令牌限制等待。

新轮启动者的 `reason / customProps` 会保存到组和当前槽位，组内后项沿用；加入 Loading 轮的请求只观察，不覆盖它们。属性复制字典结构。后台重试新轮使用 `Retry` 和空属性，展示后补量使用 `AutoRefill` 和空属性。

### 整组失败后的循环间隔

只有末项失败使整组耗尽时，才取 `GroupRetryIntervalsSeconds[RetryIntervalIndex]` 并推进一次下标；到末尾后回到首项。组内前项失败只尝试后项，不增加等待下标。默认循环为：

| 连续整组失败次数 | 等待时间 |
|---|---|
| 1 | 2 秒 |
| 2 | 4 秒 |
| 3 | 8 秒 |
| 4 | 16 秒 |
| 5 | 32 秒 |
| 6 | 2 秒 |
| 后续 | 继续按 2、4、8、16、32 秒循环 |

任意项成功后本组下标归零，后续再次整组失败从配置首项开始等待（默认 2 秒）。各组 timer、轮次和下标独立，共用的是配置序列，没有最大重试轮数。等待期间主动 Request 不抢跑；到期后仍是同一 WaitingRetry 轮且渠道有效，才从首项发新轮。

### 真实 Ready 与展示收尾

loaded 回调必须确认原厂真实 Ready；首次查询不可播时等待一次 Update 后复查，仍不可播按当前项失败推进，不盲目重复当前 ID。重复终态、未知 ID、错误格式或非当前 Loading 项不会推进组。

全屏 Show 在原厂调用前预留占用。displayed 只派发一次展示成功，不消费缓存；hidden / displayFailed 匹配格式、真实 ID 和当前 Showing 状态后收尾一次。先释放占用并保存本次展示属性，再记录结果或 hidden、重置消费组、以 `AutoRefill` 检查该格式全部组，最后通知业务。随后普通检查全部组：真实可播 Ready 缓存保留，Loading / WaitingRetry 沿用既有调度，其他空闲组或失效缓存按普通请求规则补充；消费组从首项补载。

`OnShowAsync` 的普通异常按展示失败收口；取消异常不被当作原厂关闭。渠道终态回调应保证事件处理异常时仍完成本次原生等待，MAX 使用 `finally` 完成已取出的本地 TCS。

原厂 void Show 内部静默返回无法证明广告没有展示。基类不创建未 displayed 的诊断计时器，也不以超时合成关闭、补载或解锁；占用仍由匹配的真实终态或渠道释放解除，业务通过自己的令牌限制等待。

奖励资格由渠道显式设置 `AdResult.UserCompleted / RewardGranted`。MAX 激励以官方奖励回调为准，插屏 displayed 即满足本项目奖励资格，开屏为 false。`RaiseAdClosed` 的 `rewarded` 参数只写 `nova_can_get_reward`，不修改业务奖励字段；实际发奖由业务决定。

### 本地流程日志

基类使用现有 `Log.Debug(LogTag.AD, ...)`，渠道名由 `[{Name}]` 标识。常规流程只保留实际调用渠道请求，以及展示后补量（`AutoRefill`）和自动重试（`Retry`）时的广告组新轮日志；手动或默认业务请求不额外记录新轮。Warning / Error 异常诊断保留。Editor 中 Debug 调用始终编入；Player 需要 `ENABLE_LOG`、`ENABLE_DEBUG_LOG` 或 `ENABLE_DEBUG_AND_ABOVE_LOG` 之一，实际可见性还受 LogHelper 和 Console 过滤影响。MAX 的 `LogEnable` 只控制原厂详细日志。

| 阶段 | 日志可核对的内容 |
|---|---|
| 广告组准备新一轮加载 | 仅 `reason=AutoRefill`（展示后补量）或 `reason=Retry`（自动重试）时记录 `format / group / round / reason / placement / items`；`placement` 为首项真实 ID，`items` 为本组项数 |
| 实际调用渠道请求 | `format / group / round / reason / placement / item`；调用前记录本次真实广告 ID 及组内位置，渠道接口返回不表示加载成功，实际结果由原厂回调决定 |

`group` 使用本组首个真实广告 ID，与渠道及格式组合关联；`round` 是现有托管轮次，不能当作 MAX 请求 token。`item` 从 1 开始显示，运行时 `ActiveUnitIndex` 仍从 0 开始。平台加载失败后，同组还有下一项时继续串行请求该项，可通过失败回调与随后请求的 `placement / item` 核对；整组失败后按配置间隔重试，新轮仍从首项开始。

业务准备请求与展示见 [AdPlugin.md](./AdPlugin.md#本地请求与展示日志)，MAX 实际原厂展示与回调日志见 [MaxAdPlugin.md](../../../com.solotopia.nova.framework.sdk.max/Nova/Doc/MaxAdPlugin.md#本地调用与回调日志)。

### 原厂过期重载与被动刷新

`RaiseAdCacheReplaced` 仅在原厂真实 Ready 时采纳：同组当前 Ready 项，或无活动加载 / 展示的 Idle / WaitingRetry 项。成功采纳后更新当前缓存、取消旧 timer 并归零间隔下标；不完成主动加载轮、不复活已结束批次，也不虚构 request / fill。

Loading / Showing 时的 expiry 仅用于诊断，不当作主动 Load 终态。MAX 8.6.4 的 Load 回调只有真实 ID，没有请求 token 或 CancelLoad：托管 `RoundId` 能过滤旧 timer、排队动作和业务观察，无法识别所有同 ID 跨轮迟到回调，也不能禁止原厂自主 expiry / mediation 请求。MAX 原厂细节见 [MaxAdPlugin.md](../../../com.solotopia.nova.framework.sdk.max/Nova/Doc/MaxAdPlugin.md)。

Banner 每组只含一个真实 ID，多组可以并行；不进行组内多 ID waterfall，也不参加全屏互斥；主动单 ID 加载失败仍走组重试间隔。MAX 的 Banner 视图刷新由原厂驱动：被动成功可更新已注册缓存并派发 fill，被动失败因没有当前主动 Loading 项而不发起 Nova 重试。重复 Request 已创建视图不保证触发一次新的原厂加载。

### Banner 收益累计存储

- 所有存储读写、删除只通过初始化缓存的 `IFileFragmentManager`，并在 Unity 主线程执行；启动链先完成 `Persist.LoadAsync()`。
- MAX 使用不带 ID 的累计入口，同渠道全部 Banner 共用次数和金额；默认第 5 次收益回调尝试派发 `ad_ilrd`。不同渠道隔离，上传广告信息来自触发阈值的回调，收益为整批累计值。
- 分类为 `NovaAdBannerIlrd`；渠道汇总键为 `Nova.Ad.BannerIlrd.{Name}.Count` 和 `Nova.Ad.BannerIlrd.{Name}.Revenue`。
- 带 ID 重载使用 `Nova.Ad.BannerIlrd.{Name}.{placementId}.Count / Revenue`，保持按广告位累计语义；它与渠道汇总批次独立，不自动合并旧数据。
- 次数存 int，金额按 decimal 累计并使用 `InvariantCulture` 文本保存。未满阈值或 `trackAction` 未派发时保存当前批次，并调用 `Save(classify)`。
- 已派发，或间隔小于等于 0 时清除批次，再调用无参数 `Save()` 处理待删分类及备份，避免重启恢复旧批次。
- 存储引用未缓存或保存失败会明确报错，不回退其他存储后端，不迁移旧 Unity `PlayerPrefs`。
- ad_impression 仍由 MAX 每次收益回调即时派发，不随 Banner 累计阈值节流；收益业务事件也不因阈值而省略。
- 渠道释放清除存储接口引用，不删除未满阈值的正式累计存档；后续新渠道按相同存储键继续累计。

### 通用打点与属性合并

`BuildBaseProps` 构造 `nova_ad_channel / nova_ad_format / nova_ad_id`；ID 为空时用空字符串。通用事件扇出到初始化缓存的 `ITrackPlugin`，单目标异常隔离。本次广告组调度沿用既有事件字段，没有新增组埋点字段。

| 事件 | 记录时机 / 额外属性 |
|---|---|
| `nova_ad_init` | 初始化结果；`nova_success / nova_ad_channel` |
| `nova_ad_request` | 实际调用单 ID Load 前；`nova_ad_reason` |
| `nova_ad_fill` | 接受加载成功且真实 Ready；`nova_ad_result=1 / nova_ad_network` |
| `nova_ad_fill_fail` | 接受单 ID 失败；`nova_ad_result=0 / nova_ad_errorcode / nova_ad_error_message` |
| `nova_ad_show` | 原厂 Show 调用前的一次尝试 |
| `nova_ad_show_result` | displayed / displayFailed；`nova_ad_result=1 / 0` |
| `nova_ad_click` | 广告点击 |
| `nova_ad_hidden` | 匹配关闭；`nova_can_get_reward` |

请求属性来自新轮启动者；fill 还合并结果 `CustomProps`。展示事件先合并基础字段，再合并业务 `ShowCustomProps`，最后合并渠道 `extraProps`；已有键不覆盖。失败和关闭在消费组清空前保留展示属性，保证后续补载不丢失本次埋点维度。

没有进入原厂 Show 的预取消、无缓存或 Busy 不记录展示尝试。收益 `ad_ilrd / ad_impression` 的原厂属性和具体目标由渠道适配负责，详见 [AdPluginEvents.md](./AdPluginEvents.md)。

---

## §10 常见误区

- 重写公开模板方法：初始化、释放、请求、Ready 和展示由基类管理；渠道只实现对应 protected abstract / virtual 入口。
- 在 `OnRequestAsync` 内再次遍历全部广告 ID：该方法每次只收到组当前项，额外循环会破坏组内串行。
- 只收到 loaded 就认为可播：必须接入 `OnIsReady`，基类会复查真实状态。
- 把 `OnAdLoadFailed` 当作业务请求最终失败：它是单 ID 结果，组内后项和其他组仍可能成功；以 await `RequestAsync` 的结果为准。
- 请求返回缓存就停止其他组加载：基类仍会启动其他空闲组；业务取消也只影响自身等待。
- 业务取消后立即发下一次全屏 Show：原生占用保留到真实终态或释放，聚合层会拒绝重叠展示。
- 将 displayed 当作关闭或激励完成：displayed 不补载，激励奖励看官方奖励回调及最终关闭结果。
- 收益状态在后台线程更新：只有纯收益上传保留即时路径，槽位、业务事件和 Banner 持久化在主线程执行。
- 原厂回调返回后继续读取其 `AdInfo`：渠道先复制需要的字段，基类再复制统一结果结构。
- Banner 使用全屏 `ShowAsync` 或隐藏后遗漏状态重置：使用 `IBannerControl`，隐藏 / 销毁后调用 `MarkBannerHidden`。
- 没有注册槽位还期待广告加载：未注册格式会明确返回失败，Ready 为 false。
- 原厂 expiry 能结束正在加载的组：自主刷新不能替代主动 Load 终态，也不复活旧业务批次。

---

## §11 使用示例

### 渠道注册与单 ID 加载

以下片段位于已继承基类的 MAX 渠道中；原厂 SDK 初始化、事件注册和释放仍由渠道初始化流程负责。

~~~csharp
// 在 InitChannelSDKAsync 中注册有序组计划。
RegisterAdGroups(AdFormat.Rewarded, new[] { "a1,a2,a3", "b1" });
var rewardedIds = GetRegisteredPlacementIds(AdFormat.Rewarded);
~~~

~~~csharp
/// <summary>
/// 对当前组当前真实 ID 发起一次原厂加载。
/// </summary>
protected override UniTask OnRequestAsync(AdFormat format, string placementId, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();
    MaxSdk.LoadRewardedAd(placementId);
    return UniTask.CompletedTask;
}

/// <summary>
/// 查询该真实广告位是否仍可播放。
/// </summary>
protected override bool OnIsReady(AdFormat format, string placementId)
{
    return MaxSdk.IsRewardedAdReady(placementId);
}
~~~

此片段只注册 Rewarded；多格式渠道按 `format` 分发单次 SDK 调用，不再自行循环组内 ID。真实项目中使用渠道配置字段替代示例 ID。

### 加载回调与展示等待

~~~csharp
/// <summary>
/// 在 SDK 回调返回前复制加载结果，交给基类主线程状态入口。
/// </summary>
private void OnRewardedLoaded(string placementId, MaxSdkBase.AdInfo info)
{
    RaiseAdLoaded(new AdLoadResult
    {
        Format = AdFormat.Rewarded,
        PlacementId = placementId,
        Network = info?.NetworkName,
        Revenue = info?.Revenue ?? 0d,
        Currency = "USD",
    });
}
~~~

加载失败同样先复制错误字段，再调用 `RaiseAdLoadFailed`。展示调用前记录 `TrackAdShow`；displayed 调用 `RaiseShowCompleted`；匹配关闭或展示失败后调用 `RaiseAdClosed / RaiseShowFailed`，再完成渠道自己的展示 TCS。涉及渠道字段和完成源时先通过 `PostAdCallbackToMainThread` 执行，终态领取一次并在 `finally` 完成等待。完整实现见 [MaxAdPlugin.RV.cs](../../../com.solotopia.nova.framework.sdk.max/Nova/Scripts/Runtime/MaxAdPlugin.RV.cs)。

### 业务层请求与展示

~~~csharp
// 业务通过聚合接口，不传广告位 ID 或组 ID。
var ad = Nova.SDK.Get<IAdPlugin>();
var requestProps = new Dictionary<string, object> { { "scene", "level_end" } };
var loaded = await ad.RequestAsync(AdFormat.Rewarded, requestProps, ct);
if (loaded.Success)
{
    await ad.ShowAsync(AdFormat.Rewarded, ct: ct);
}

// Banner：预加载后使用专属控制接口。
var bannerLoaded = await ad.RequestAsync(AdFormat.Banner, ct: ct);
if (bannerLoaded.Success)
{
    ad.ShowBanner();
}
~~~

聚合层 `ShowAsync` 返回 `UniTask`，展示结果从 `AdPlugin.Events` 的展示成功、失败及关闭事件获取；渠道基类 `ShowAsync` 才返回 `UniTask<AdResult>`。订阅方式和奖励处理见 [IAdPlugin.md](./IAdPlugin.md) 与 [AdPluginEvents.md](./AdPluginEvents.md)。

---

## §12 注意事项

- 基类与聚合层的收益选择分两层：渠道内最高收益槽位始终使用真实 Ready；`EnableBidding` 决定聚合层是否按最高收益选渠道。
- 基类没有最大加载重试轮数；业务请求失败与后台重试生命周期独立。外部渠道升级后，单 ID 注册也采用整组失败后的循环间隔。
- 托管轮次与释放版本只能识别本地动作，原厂没有请求 token 时不能完全归属同 ID 跨轮回调；不得在文档中宣称原厂所有请求都受 Nova 串行控制。
- `SetUserId` 默认空实现足以满足无原厂身份 API 的渠道；账号派发与渠道原厂初始化的先后约束在渠道适配中处理。
- 修改 `Name` 会改变 Banner 累计存储键和打点渠道值，应作为兼容性变化评估。
- 数据存储与收益上传结果不是同一概念：`trackAction=true` 只表示派发尝试，不能作为分析后台已入库的证据。

---

## §13 关联文档

- [AdPlugin.md](./AdPlugin.md) — 多渠道创建、调度、事件桥接与释放
- [AdPluginConfig.md](./AdPluginConfig.md) — 全局重试序列及广告配置
- [AdRequestReason.md](./AdRequestReason.md) — 请求原因与轮次属性
- [IAdPlugin.md](./IAdPlugin.md) — 业务接口、结果获取和示例
- [IBannerControl.md](./IBannerControl.md) — Banner 控制接口
- [AdPluginEvents.md](./AdPluginEvents.md) — 七类事件与奖励资格
- [ObservableEvent.md](../../../../Assets/Framework/Docs/Runtime/Modules/SDK/Events/ObservableEvent.md) — 事件订阅、补发与生命周期
- [IAdInternalPlugin.cs](../Scripts/Runtime/Interfaces/IAdInternalPlugin.cs) — 渠道接口源码
- [AdGroupConfigParser.cs](../Scripts/Runtime/AdChannelPluginBase/AdGroupConfigParser.cs) — 配置解析与只读校验结果
- [MaxAdPlugin.md](../../../com.solotopia.nova.framework.sdk.max/Nova/Doc/MaxAdPlugin.md) — MAX 原厂回调与平台边界
