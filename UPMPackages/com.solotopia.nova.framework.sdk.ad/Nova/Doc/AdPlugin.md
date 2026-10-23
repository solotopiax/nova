# AdPlugin

**类签名**：`public sealed partial class AdPlugin : SDKPluginBase, IAdPlugin, ISDKLoginReceiver, ISDKInitializationDependencies`
**命名空间**：`NovaFramework.SDK.AdPlugin.Runtime`
**全局访问**：`Nova.SDK.Get<AdPlugin>()` / `Nova.SDK.Get<IAdPlugin>()`

IAA 广告聚合调度层；初始化时按配置反射创建渠道实例，RequestAsync 并行广播给所有渠道，ShowAsync 按 EnableBidding 选择最高收益或配置序首个就绪渠道执行，渠道事件统一桥接到 Events 容器。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Nova/Scripts/Runtime/AdPlugin.cs` | `AdPlugin` | 公开 API：IAdPlugin 实现（RequestAsync / IsReady / ShowAsync）、IBannerControl 委托实现、SDKPluginBase 生命周期 |
| `Nova/Scripts/Runtime/AdPlugin.Visitors.cs` | `AdPlugin` | 渠道列表与 ChannelRegistration 所有权、请求汇聚状态、生命周期取消源、共享释放结果、Events 及插件元数据 |
| `Nova/Scripts/Runtime/AdPlugin.Methods.cs` | `AdPlugin` | 私有方法：`SelectBestChannel`、`BroadcastRequestAsync`、`ObserveChannelRequestAsync`、`CreateChannel`、`PrepareAndInitializeChannelAsync`、`WireChannelEvents`、`TryUnsubscribeChannelEvent`、`DisposeChannelsAsync`、`RegisterChannel`、`OnChannelInitResult`、`TryPublishCountryCodeFromChannel`、`TryPublishCountryCode` |

---

## §3 继承关系

```
SDKPluginBase
  └── AdPlugin（sealed partial，实现 IAdPlugin、ISDKLoginReceiver、ISDKInitializationDependencies）
        IAdPlugin 继承 ISDKPlugin + IBannerControl
```

---

## §4 关键字段表

| 字段 / 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_ChannelPlugins` | `List<IAdInternalPlugin>` | 空列表（OnInitializeAsync 重建） | 所有已注册渠道插件列表 |
| `m_ActiveBannerChannel` | `IAdInternalPlugin` | `null` | RequestAsync(Banner) 成功后记录的活跃 Banner 渠道；Banner 控制方法委托到此渠道 |
| `m_RuntimeConfig` | `AdPluginConfig` | `null` | SDKManager 注入的广告运行时配置，提供国家码等待超时时间 |
| `m_ChannelRegistrations` | `Dictionary<IAdInternalPlugin, ChannelRegistration>` | 空字典 | 持有全部已构造实例，保存有效门、精确退订动作及准备/初始化退出信号；失败实例也保留释放所有权 |
| `m_ChannelDisposalCompletion` | `UniTaskCompletionSource` | `null` | 首次释放创建；重复调用等待同一次实际清理完成 |
| `m_ChannelLifetimeCts` | `CancellationTokenSource` | 构造时创建，readonly | 聚合自有生命周期；释放时取消 Request 观察与尚未结束的渠道初始化，最后释放 |
| `m_ChannelsDisposed` | `bool` | `false` | 释放开始置 true，拒绝后续 Initialize / Request / Show；不会复位 |
| `Events` | `AdPluginEvents` | `new AdPluginEvents()` | 只读属性，持有 7 个 readonly ObservableEvent 字段 |
| `Name` | `string` | `"AdPlugin"` | 插件友好名 |
| `Priority` | `int` | `80` | 稳定遍历和释放排序；初始化先后由能力依赖决定 |
| `InitializationDependencies` | `IReadOnlyList<SDKInitializationDependency>` | 可选 `ITrackPlugin` 依赖 | 已启用该能力时等待其初始化完成；未启用时仍可初始化广告，不等待所有收益打点 SDK |

---

## §5 完整公开 API

### IAdPlugin 实现

```csharp
/// 向所有渠道并行发起预加载请求；返回 AdLoadResult，Success=true 表示成功，Success=false 时携带错误详情。
/// Banner 格式在首个成功结果后更新 m_ActiveBannerChannel。
public async UniTask<AdLoadResult> RequestAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);

/// 查询是否有任意渠道的指定格式广告已就绪可立即展示。
public bool IsReady(AdFormat format);

/// 查询真实原生展示占用；取消业务等待不解除占用。
public bool IsAdPlaying(AdFormat format);

/// 查询用户是否已经作出明确的广告隐私授权决定。
public bool IsUserConsentSet();

/// 查询用户是否明确同意；必须结合 IsUserConsentSet() 判断。
public bool HasUserConsent();

/// 等待所有已启用广告渠道初始化期间的隐私授权流程结束。
public async UniTask WaitForPrivacyFlowAsync(CancellationToken ct = default);

/// 异步获取广告 SDK 返回的有效国家或地区代码。
/// 优先等待 SDKDataKeys.AdCountryCode；超时或拿到空值 / IV 时直接返回空字符串。
public async UniTask<string> GetCountryCodeAsync(CancellationToken ct = default);

/// 展示指定格式广告；开启比价时选最高收益渠道，否则按配置序选第一个就绪渠道。
/// 已有任一全屏占用时发布 Busy；无就绪渠道时 Log.Warning 并跳过。
/// AdFormat.Banner 不适用此方法，Banner 展示请使用 ShowBanner()。
public async UniTask ShowAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);
```

### IBannerControl 委托实现

```csharp
// 全部委托给 m_ActiveBannerChannel；Banner 控制前须先 RequestAsync(AdFormat.Banner) 完成预加载。
public void ShowBanner();
public void HideBanner();
public void DestroyBanner();   // 销毁后同时清空 m_ActiveBannerChannel
public void UpdateBannerPosition(BannerPosition position);
public void UpdateBannerPosition(Vector2 position);
public void StartBannerAutoRefresh();
public void StopBannerAutoRefresh();
public void SetBannerWidth(float width);
public float GetAdaptiveBannerHeight(float width = -1f);   // 无活跃渠道时返回 -1
public void SetBannerBackgroundColor(Color color);
```

### SDKPluginBase 生命周期

初始化按配置构造渠道，构造成功后立即登记释放所有权，再应用全局参数、桥接事件和登记业务列表，最后通过可选 `IAdChannelLifecycle` 启动渠道 SDK。聚合初始化不自动预载；渠道原厂初始化完成后发 InitResult，单渠道构造、准备或初始化异常不阻塞其他渠道，失败实例退出业务列表但保留释放所有权。

释放先保存共享完成源，回到主线程关闭入口并失效全部记录，冻结所有权快照、清业务列表和 Banner 引用，再取消生命周期。逐渠道等待准备和实际初始化方法退出后，最终准确退订七类桥接并释放；一个事件移除器或一个渠道释放抛错时继续其余清理。最后清事件、配置与取消源。重复释放等待同一次清理完成；调用方令牌已取消也不会跳过必要清理。

正常生命周期由 SDKManager 每轮创建新的 AdPlugin 和渠道；同一渠道实例只初始化一次。已释放聚合对象再次 InitializeAsync 抛 ObjectDisposedException，不恢复旧渠道或 Events；Manager 完整释放后再次初始化会创建新的聚合，业务应重新获取并订阅。SDKComponent.InitializeTask 缓存本组件的首次初始化，重复等待不重启 SDK。未释放时重复直接调用聚合 InitializeAsync 没有统一幂等保护，也不受支持；这些实例管理约定不等于 ISDKPlugin 对所有实现规定永久单次初始化。

SDKManager 通过 `ISDKLoginReceiver.OnSDKLogin(userId, sessionId)` 交付当前登录账号，聚合层逐渠道调用 `SetUserId`；没有额外 UserLogin 事件订阅。该能力的公开入口如下，通常由 SDKManager 驱动：

```csharp
public void OnSDKLogin(string userId, long sessionId);
public IReadOnlyList<SDKInitializationDependency> InitializationDependencies { get; }
```

聚合插件初始化完成表示渠道列表与桥接已创建，各原厂渠道仍可能在初始化中。`Events.InitResult` 每次转发一个渠道的结果，Sticky 只保存最后一条；没有渠道 ID，也不是所有渠道成功的汇总门。业务请求是否可用应以 `RequestAsync` 返回值和 `IsReady` 为准。

### 关键私有方法

```csharp
/// 选出已就绪的渠道。比价模式开启时选 Revenue 最高渠道；关闭时按列表顺序取第一个就绪渠道。
/// 未注册该 format 的渠道 IsReady 必为 false，自然过滤。
private IAdInternalPlugin SelectBestChannel(AdFormat format);

/// 向所有渠道并行发起请求；未注册该 format 的渠道 RequestAsync 返回 Success=false 自然过滤。
private async UniTask<AdLoadResult> BroadcastRequestAsync(AdFormat format, Dictionary<string, object> customProps, CancellationToken ct);

/// 从渠道读取国家码，过滤空值和 IV，占位无效时不发布。
/// 有效国家码会统一转成大写，并通过 SDKDataKeys.AdCountryCode 发布。
private void TryPublishCountryCodeFromChannel(IAdInternalPlugin channel);

/// 发布已经读取到的国家码字符串。
private void TryPublishCountryCode(string countryCode);
```

---

## §7 请求、展示和渠道所有权

RequestAsync 冻结全部渠道后逐一发出请求，每个渠道返回的单次 UniTask 只观察一次，首个成功完成业务请求；所有渠道本轮失败才返回最后失败。完成后仍继续观察其余结果与异常。业务 ct 和聚合生命周期共同控制汇聚完成源，因此即使自定义渠道不响应令牌，外层 Request 也能取消；该渠道底层任务仍可继续挂起，取消不撤销基类组调度或原厂 Load。

所有状态 API 由 Unity 主线程调用。聚合自身不对全部查询逐一检查线程；基类请求、Ready 与展示会检查，自定义渠道须遵守约定。聚合 RequestAsync 没有渠道时返回 -1；渠道异常或独立取消按 `-3` 失败处理，全部渠道未给结果时返回 `-2`。调用方取消抛取消异常；聚合释放后的新 Request / Show 抛 `ObjectDisposedException`。

ShowAsync 按 EnableBidding 选择最高收益渠道或配置序首个 Ready 渠道，基类渠道内固定选最高真实 Ready 收益。当前比价开关只从业务列表首项读取，且该项必须是 AdChannelPluginBase；首项是非基类自定义渠道时走配置序选择。RV / Inter / AppOpen 的跨格式占用查询只覆盖基类渠道；第二次 Show 通过 Events.ShowFailed 反馈 Busy。IsAdPlaying 不感知直接实现内部接口的自定义渠道占用。没有 Ready 时保留日志并跳过。

聚合 `IsReady` 本身只遍历业务列表，没有独立的释放态/null 列表兜底，也不逐项隔离自定义渠道查询异常；正常释放清空列表与基类渠道自己的门禁应分别理解。国家码只做 Trim、大写并排除空值/`IV`，不是 ISO 地区表校验。渠道收益比较使用 float，竞价初值会排除小于等于 -1 的值；接口存在不替代原厂有效收益和 Ready 查询。

### 取消令牌的作用范围

| 入口 | 当前保证 |
|---|---|
| 聚合初始化 ct | 仅在每个渠道的准备与初始化期间链接聚合生命周期；取消需等渠道方法退出才失效该记录。成功退出后不再长期关联原初始化 ct，后续取消不会关闭其事件或请求 |
| 聚合 Request ct | 取消本次汇聚完成源；聚合释放也会取消它。不撤销已发出的原厂加载，不强制结束自定义渠道底层任务 |
| 聚合 Show ct | 入口检查取消后将 ct 传给选中渠道，聚合层没有 linked lifetime 或 AttachExternalCancellation 包装。基类 Show 自带业务 ct 与渠道生命周期的外部取消，等待取消仍保留原生占用；非基类渠道须自行实现取消和释放结算 |
| 聚合 WaitForPrivacyFlowAsync ct | 将 ct 传给各渠道再 WhenAll，没有额外的外部取消或聚合生命周期链接。MAX / AdMob 自行处理等待取消和释放结算；自定义渠道忽略 ct 或不在释放时结算，可持续挂起 |
| Dispose ct | 必需清理不使用调用方 ct 中断；等待真实准备与初始化退出后释放全部持有渠道 |

渠道事件桥接只检查聚合释放状态和 ChannelRegistration.Active，直接在渠道触发事件的线程转发。基类已经统一主线程派发；非基类自定义渠道应自行保证线程、Ready 和回调终态语义。

### 本地请求与展示日志

常规流程日志使用现有 `LogTag.AD` 与渠道的 `LogTag.Max`，只记录请求和展示的关键节点；Warning / Error 异常诊断保留。

| 日志阶段 | 内容与判断边界 |
|---|---|
| 准备请求 | 业务 `RequestAsync` 入口记录 `format` 和渠道数 |
| 实际调用渠道请求 | 基类调用渠道加载入口前记录渠道名及 `format / group / round / reason / placement / item`，其中 `placement` 是本次真实广告 ID |
| 准备展示 | 业务 `ShowAsync` 入口记录 `format`；准备显示 Banner 时记录活跃渠道 |
| 发起原厂展示与回调 | MAX 实际 `Show*` 调用前记录 `format / placement`，原厂加载、展示、点击、奖励、关闭、收益及过期重载回调由渠道记录 |
| 广告组准备新一轮加载 | 仅展示后补量（`reason=AutoRefill`）和自动重试（`reason=Retry`）时记录渠道名及 `format / group / round / reason / placement / items`，新轮从本组首项开始；手动或默认业务请求不额外记录新轮 |

平台加载失败回调后，若同组还有下一项，可通过紧接着的实际渠道请求及其真实 `placement` 核对串行推进；整组失败后的自动重试由新的轮次与 `reason=Retry` 标明。组字段的含义见 [AdChannelPluginBase.md](./AdChannelPluginBase.md#本地流程日志)。

原厂回调日志不保证发布业务事件；displayed 不表示已关闭或完成激励，实际结果仍由 `Events.ShowCompleted / ShowFailed / AdClosed` 等通知判断。Debug 编译条件与输出边界见基类文档，`MaxAdChannelConfig.LogEnable` 只控制 MAX 原厂详细日志。

## §8 初始化时序

1. 校验并构造渠道；成功后立即登记 `ChannelRegistration` 所有权和初始化退出信号。
2. 应用基类全局配置；首次事件绑定前保存完整准确退订动作，再桥接七类事件、登记业务渠道。每个可回调阶段后检查取消和释放门。
3. 渠道实现 `IAdChannelLifecycle` 时调用其 `InitializeAsync`；没有此接口的旧渠道保留准备后的可用性，不自动初始化。准备和初始化所有退出路径都完成独立信号。
4. 有效记录的 InitResult 回桥接，成功时发布规范化国家码；准备或初始化异常失效记录并移出业务列表，保留后续释放所有权。
5. Dispose 失效记录后取消聚合 waiter 与初始化；等待各渠道方法退出，再最终退订并释放，全部完成后结算共享结果。

渠道释放优先调用 `IAdChannelLifecycle.DisposeAsync(CancellationToken.None)`，其次调用 `IDisposable.Dispose()`；两者兼具时只调用异步生命周期入口。未实现两者的旧渠道仍兼容，但资源由渠道自行管理。基类已实现可选生命周期，MAX 和 AdMob 派生类自动接入。

等待针对 C# 准备与初始化方法退出，不表示原生 SDK 已取消。自定义初始化必须响应取消，不能反向等待所属聚合插件的 Dispose，否则双方相互等待；构造函数只负责构造，不同步驱动聚合生命周期。

## §9 国家码数据槽位

`AdPlugin` 是 `SDKDataKeys.AdCountryCode` 的发布方。发布规则如下：

- 来源：已注册渠道的 `IAdInternalPlugin.GetCountryCode()`。
- 过滤：空字符串、空白字符串和 `IV` 不发布。
- 规范化：发布前转成大写，例如 `us` → `US`。
- 发布语义：渠道注册或初始化成功时读到有效国家码就发布到同一数据槽位，后发布值可覆盖前值；广告模块不做本地持久化缓存。

需要有限等待的消费方通过 `IAdPlugin.GetCountryCodeAsync(ct)` 获取国家码；不要轮询具体渠道。现有 ThirdPay 后台解析器通过继承的 `FetchDataAsync(SDKDataKeys.AdCountryCode, ct)` 长期等待首个国家码，这条内部消费链不受广告业务等待超时限制。`GetCountryCodeAsync` 会按 `AdPluginConfig.CountryCodeWaitTimeoutSeconds` 等待广告国家码；等待超时、拿到空值或拿到 `IV` 时直接返回空字符串；调用方取消仍向上传播。配置小于等于 0、NaN 或无穷大时不等待，直接返回空字符串。

## §9.1 广告隐私授权状态

`AdPlugin` 按渠道配置顺序查询 `IAdInternalPlugin.IsUserConsentSet()`，采用第一个已经取得明确决定的渠道结果。业务侧必须组合判断：`IsUserConsentSet() == false` 表示尚未设置或渠道不支持查询；只有它为 `true` 时，`HasUserConsent() == false` 才表示明确拒绝。

启动页如需防止 CMP 展示期间跳转，应在 `Nova.SDK.InitializeTask` 完成后通过 `IAdPlugin.WaitForPrivacyFlowAsync(ct)` 等待。该任务聚合当前已登记渠道的隐私流程；不支持隐私流程的渠道立即完成。取消和释放能否结束等待由各渠道实现决定，当前聚合没有额外包装，见[取消令牌的作用范围](#取消令牌的作用范围)。

---

## §10 常见误区

- **误区：通过 `IAdPlugin` 访问 `Events`**：`Events` 属性不在接口上；须 `Nova.SDK.Get<AdPlugin>().Events` 或显式转型后访问。
- **误区：Banner 用 `ShowAsync`**：Banner 走 `RequestAsync(AdFormat.Banner)` 预加载后用 `ShowBanner()` 展示。
- **误区：从 `ShowAsync` 读取 `AdResult`**：`ShowAsync` 返回 `UniTask`，展示成功、失败和关闭结果分别通过 `Events.ShowCompleted`、`Events.ShowFailed`、`Events.AdClosed` 发布；激励奖励以 `AdClosed.UserCompleted` 为准。
- **误区：直接调 Banner 控制方法而不先 RequestAsync**：`m_ActiveBannerChannel` 在 `RequestAsync(Banner)` 成功后才会被赋值；未预加载时所有 Banner 控制方法为无操作。
- **误区：调 `Supports(format)` 检查格式**：`Supports` 方法已全链路删除；直接 `RequestAsync`，未注册该格式的渠道 fail-soft 返回 `Success=false`，不抛异常。
- **误区：从 `IAdPlugin` 同步查询国家码**：国家码只能通过 `GetCountryCodeAsync(ct)` 异步获取；广告模块只负责等待运行时数据，超时返回空字符串，不负责持久化兜底。
- **误区：只检查 `HasUserConsent()`**：明确拒绝、尚未设置和渠道不支持查询都会返回 `false`，必须结合 `IsUserConsentSet()`。
- **误区：用 IsUserConsentSet() 轮询等待弹窗结束**：无须展示 CMP 时该值可能一直为 false；应等待 WaitForPrivacyFlowAsync()，并核对所用渠道的取消和释放结算实现。
- **误区：释放后继续使用旧聚合或事件容器**：新的 Manager 初始化轮会创建新聚合与渠道；重新获取实例并订阅，旧 AdPlugin 不支持再次 Initialize。

---

## §11 使用示例

以下为业务脚本成员片段，省略 using 与类声明；在聚合插件 Ready 后绑定事件。ShowCompleted / ShowFailed / AdClosed 会同步补发历史记录，因此日志订阅不直接发奖，展示流程先订阅、再开启本次处理标记。示例假定该流程是激励广告的唯一展示入口；多入口项目需统一展示和奖励归属，`AdResult` 没有业务请求 ID。

示例用页面令牌结束观察。取消等待不结束原生广告；若业务要求页面离开或等待取消后仍兑现奖励，应将奖励订阅放到能存活至原生终态的业务服务中，不能在此 finally 或页面销毁时释放它。Replay 仅是内存回放，不能代替奖励幂等记录。

```csharp
private IAdPlugin m_Ad;
private AdPlugin m_AdPlugin;
private readonly List<IDisposable> m_Bag = new List<IDisposable>();

void SetupEvents()
{
    m_Ad = Nova.SDK.Get<IAdPlugin>();
    m_AdPlugin = Nova.SDK.Get<AdPlugin>();
    m_AdPlugin.Events.InitResult.Subscribe(ok =>
    {
        Log.Debug($"收到单个广告渠道初始化结果：{ok}");
    }, m_Bag);

    m_AdPlugin.Events.ShowCompleted.Subscribe(result =>
    {
        Log.Debug($"广告展示成功：{result.Format} / {result.PlacementId}");
    }, m_Bag);

    m_AdPlugin.Events.ShowFailed.Subscribe(result =>
    {
        Log.Warning($"广告展示失败：{result.Format} / {result.ErrorMessage}");
    }, m_Bag);

    m_AdPlugin.Events.AdClosed.Subscribe(result =>
    {
        Log.Debug($"广告关闭：{result.Format} / {result.PlacementId}");
    }, m_Bag);
}

// 激励视频完整流程
async UniTask ShowRewardedAd(CancellationToken ct)
{
    // 唯一展示入口串行调用本方法；订阅时先关闭本轮标记，忽略历史回放。
    var subscriptions = new List<IDisposable>();
    bool waitingForThisShow = false;

    m_AdPlugin.Events.AdClosed.Subscribe(result =>
    {
        if (!waitingForThisShow || result.Format != AdFormat.Rewarded) return;
        waitingForThisShow = false;
        if (result.Success && result.UserCompleted && result.RewardGranted)
            GiveReward(); // 业务还须按自己的请求标识保证幂等。
    }, subscriptions);

    m_AdPlugin.Events.ShowFailed.Subscribe(result =>
    {
        if (!waitingForThisShow || result.Format != AdFormat.Rewarded) return;
        waitingForThisShow = false;
        Log.Warning($"激励展示失败：{result.ErrorMessage}");
    }, subscriptions);

    try
    {
        var requestProps = new Dictionary<string, object> { { "scene", "main_menu" } };
        var loaded = await m_AdPlugin.RequestAsync(AdFormat.Rewarded, requestProps, ct);
        if (!loaded.Success || !m_AdPlugin.IsReady(AdFormat.Rewarded)) return;

        waitingForThisShow = true;
        var showProps = new Dictionary<string, object> { { "scene", "main_menu" } };
        await m_AdPlugin.ShowAsync(AdFormat.Rewarded, showProps, ct);
    }
    finally
    {
        waitingForThisShow = false;
        foreach (var subscription in subscriptions) subscription.Dispose();
    }
}

void OnDestroyed()
{
    foreach (var d in m_Bag) d.Dispose();
    m_Bag.Clear();
}
```

---

## §13 关联文档

- [IAdPlugin.md](./IAdPlugin.md) — 业务公开接口定义
- [AdPluginConfig.md](./AdPluginConfig.md) — 配置，包含 ChannelConfigs（渠道列表 + 全局开关）
- [AdPluginEvents.md](./AdPluginEvents.md) — 事件容器（7 个 ObservableEvent 字段）
- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — 渠道插件抽象基类
- [IBannerControl.md](./IBannerControl.md) — Banner 控制接口
