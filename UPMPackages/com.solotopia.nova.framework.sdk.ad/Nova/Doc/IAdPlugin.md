# IAdPlugin

**类签名**：`public interface IAdPlugin : ISDKPlugin, IBannerControl`
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Nova.SDK.Get<IAdPlugin>()`

IAA 广告聚合调度层唯一业务接口，内部持多个渠道。RequestAsync 并行广播并观察各渠道本轮结果；基类渠道使用广告组调度。ShowAsync 对当前基类渠道按 EnableBidding 选择最高真实 Ready 收益，关闭比价时选择配置序首个可播渠道；非基类自定义渠道边界见下文。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Assets/Framework/Scripts/Runtime/Modules/SDK/Plugins/Ad/IAdPlugin.cs` | `IAdPlugin` | 接口全部定义 |

---

## §3 继承关系

```
ISDKPlugin
  └── IAdPlugin（还继承 IBannerControl）
        └── AdPlugin（sealed partial，UPM 包内唯一实现）
```

`IBannerControl` 提供 Banner 展示控制（ShowBanner / HideBanner 等），由 `AdPlugin` 委托给活跃 Banner 渠道。

事件订阅通过具体类型 `AdPlugin` 的 `Events` 属性访问；接口层不暴露 `Events`，需要 `Nova.SDK.Get<AdPlugin>()` 或自行持有 `IAdPlugin` 并向下转型后访问。

---

## §4 关键字段表

`IAdPlugin` 为纯接口，无字段；唯一实现 `AdPlugin` 的字段见 `AdPlugin.md`。

---

## §5 完整公开 API

```csharp
public interface IAdPlugin : ISDKPlugin, IBannerControl
{
    // === 格式查询 ===

    /// 查询激励、插屏或开屏的原生展示是否仍占用；发出Show即占用，直到真实终态或渠道释放。
    bool IsAdPlaying(AdFormat format);

    // === 国家码 ===

    /// 异步获取广告 SDK 返回的有效国家或地区代码；超时、空值或 IV 返回空字符串。
    UniTask<string> GetCountryCodeAsync(CancellationToken ct = default);

    // === 广告隐私授权 ===

    /// 查询用户是否已经作出明确的广告隐私授权决定。
    bool IsUserConsentSet();

    /// 查询用户是否明确同意；必须结合 IsUserConsentSet() 判断。
    bool HasUserConsent();

    /// 等待广告渠道初始化期间的隐私授权流程结束。
    UniTask WaitForPrivacyFlowAsync(CancellationToken ct = default);

    // === 预加载 ===

    /// 向所有渠道并行发起预加载请求；未注册该格式的渠道 fail-soft 自动过滤。
    /// 任一渠道观察轮成功即返回Success=true；全部渠道观察轮失败时返回最近实际失败。
    /// 业务等待结束后，其他广告组仍可继续后台加载或按统一间隔重试。
    /// 业务层可直接用 result.Success 判断请求是否成功。
    /// customProps 仅在新轮启动时复制；加入已有轮次只观察，不覆盖在途属性。
    /// ct只取消本次业务观察，不取消已经发出的原厂Load或其他调用的观察。
    UniTask<AdLoadResult> RequestAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);

    // === 就绪查询 ===

    /// 查询是否有任意渠道的指定格式广告同时满足本地Ready与原厂真实Ready。
    bool IsReady(AdFormat format);

    // === 展示 ===

    /// 按EnableBidding选择真实Ready渠道；任意全屏广告仍占用时发布Events.ShowFailed的Busy失败。
    /// 无可播渠道时 Log.Warning 并跳过；基类渠道 ct 取消仅结束 await，原生展示占用保留。
    /// 非基类自定义渠道的等待取消与占用记录由渠道自行实现。
    /// 激励奖励资格由官方奖励回调决定，从Events.AdClosed读取UserCompleted/RewardGranted后由业务发奖。
    /// AdFormat.Banner 不适用此方法，Banner 展示请使用 IBannerControl.ShowBanner()。
    /// customProps 会透传到渠道层 nova_ad_show / nova_ad_show_result 成功与失败分支 / nova_ad_hidden 打点。
    UniTask ShowAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);
}
```

> `IBannerControl` 方法（ShowBanner / HideBanner / DestroyBanner / UpdateBannerPosition(BannerPosition) / UpdateBannerPosition(Vector2) / StartBannerAutoRefresh / StopBannerAutoRefresh / SetBannerWidth / GetAdaptiveBannerHeight / SetBannerBackgroundColor）由 `AdPlugin` 委托给活跃 Banner 渠道；调用前须先 `RequestAsync(AdFormat.Banner)` 完成预加载。详见 [IBannerControl.md](./IBannerControl.md)。

所有状态方法和 Banner 控制由 Unity 主线程调用。聚合释放后的新 Initialize / Request / Show 抛 ObjectDisposedException；Request 的业务取消向上传播，不转换为普通加载失败。Show 与隐私等待的取消依赖所选渠道实现，见下文。无渠道、无可请求组和原厂失败以 Success=false 结果返回，具体失败码见 [AdChannelPluginBase.md](./AdChannelPluginBase.md)。

普通请求会先检查所有广告组：空组启动首项、Loading组观察当前轮，WaitingRetry组保持timer，Showing组不建立加载等待。即使已有缓存可播，也先为其他空组补量，再返回缓存成功。仅剩等待或展示中的组时立即返回最近失败或明确不可用结果；没有原厂回调的Loading不会自动超时切换ID，业务应通过ct限制自身等待。

IsAdPlaying 反映基类渠道记录的原生展示占用，包括已经发出 Show 但尚未 displayed 的阶段。激励、插屏和开屏共享全屏互斥；基类 await 取消后仍需等匹配 hidden / displayFailed 或渠道释放才允许下一次 Show。原厂 Show 静默跳过时，不以超时解锁或补载，也不创建未 displayed 的诊断等待。Banner 继续通过 IBannerControl 管理。

直接实现 IAdInternalPlugin 的自定义渠道不自动获得上述组调度、真实 Ready、主线程事件或全屏占用记录。当前比价开关取业务列表首个渠道的基类配置；首项不是 AdChannelPluginBase 时按配置序选择。聚合 Request 自有完成源能在业务取消或聚合释放时结束外层等待；Show 和 WaitForPrivacyFlowAsync 只透传业务 ct，没有额外的聚合生命周期链接或 AttachExternalCancellation 包装，忽略令牌且不在释放时结算的自定义任务可能持续挂起。基类 Show、MAX / AdMob 隐私实现各自处理取消和释放，详见 [AdPlugin.md](./AdPlugin.md#取消令牌的作用范围)。

---

常规 Debug 日志记录业务准备请求/展示、实际调用渠道请求、发起原厂展示和平台原厂回调；广告组准备新一轮加载只在展示后补量（`reason=AutoRefill`）和自动重试（`reason=Retry`）时记录。手动或默认业务请求只记录准备请求、实际渠道请求和平台加载回调。Warning / Error 异常诊断保留。失败回调与下一真实广告 ID 的请求可核对组内串行推进，新轮的 `reason=Retry` 可识别自动重试。字段和可见性边界见 [AdPlugin.md](./AdPlugin.md#本地请求与展示日志) 与 [AdChannelPluginBase.md](./AdChannelPluginBase.md#本地流程日志)。

## §8 初始化时序

`IAdPlugin` 由 `SDKManager` 在 `InitializeAsync` 阶段通过 `Activator.CreateInstance` 创建 `AdPlugin` 实例并注入 `AdPluginConfig`。业务先等待目标插件就绪或 `Nova.SDK.InitializeTask`，确保聚合实例和渠道列表已创建。聚合插件就绪不表示所有原厂渠道都初始化成功；`AdPlugin.Events.InitResult` 逐条转发渠道结果，Sticky 仅保存最后一次通知，不能作为全部渠道就绪的汇总门。

正常生命周期中，SDKManager 每轮创建新聚合和新渠道，已释放 AdPlugin 不支持同对象再次初始化，重复 Dispose 等待原清理结果；未释放时也不要直接重复 Initialize。SDKManager 完整释放后再次初始化会创建新实例，业务需重新获取 IAdPlugin / AdPlugin 并重订阅。SDKComponent.InitializeTask 只缓存首次初始化，重复等待旧任务不会重启 SDK。初始化调用方 ct 只作用于尚未退出的渠道准备与初始化；成功结束后不再长期关闭该渠道事件或业务等待。

国家码不通过 `IAdPlugin` 同步查询。`AdPlugin` 会在内部渠道返回有效国家码后发布 `SDKDataKeys.AdCountryCode`；有限等待的业务或 SDK 消费方使用 `GetCountryCodeAsync(ct)`。现有 ThirdPay 后台解析器通过继承的 `FetchDataAsync(SDKDataKeys.AdCountryCode, ct)` 长期等待首个国家码，不受该业务超时限制。广告模块自身不增加系统区域兜底。等待超时、拿到空值或拿到 `IV` 时直接返回空字符串；调用方取消向上传播，非法等待配置直接返回空字符串。广告模块不做本地持久化缓存。

广告隐私授权状态是同步缓存值。多渠道场景按配置顺序采用第一个已经取得明确决定的渠道：`IsUserConsentSet()` 为 `false` 表示尚未设置或渠道不支持查询；只有它为 `true` 时，`HasUserConsent()` 的 `false` 才表示明确拒绝。

业务启动页应先等待 Nova.SDK.InitializeTask，确保 IAdPlugin 与渠道列表已创建，再等待 WaitForPrivacyFlowAsync(ct)。MAX / AdMob 没有展示隐私弹窗或不适用时正常完成；实际流程结束、失败或渠道释放也会结算各自等待，任务完成不等于已授权或广告可用，仍需按业务需要检查状态。自定义渠道须实现正常结束、取消和释放结算。不要轮询 IsUserConsentSet() 作为启动门禁，否则未展示弹窗的用户可能永久等待。

```csharp
await Nova.SDK.InitializeTask;
if (Nova.SDK.TryGet<IAdPlugin>(out IAdPlugin adPlugin))
{
    await adPlugin.WaitForPrivacyFlowAsync(ct);
}

// 游戏内容加载与隐私流程都完成后再进入业务场景。
EnterGame();
```

---

## §10 常见误区

- **误区：通过 `IAdPlugin` 访问 `Events`**：`Events` 属性不在接口上，需要 `Nova.SDK.Get<AdPlugin>().Events` 或显式转型后访问。
- **误区：调用 `Supports(format)` 检查格式兼容性**：`Supports` 方法已全链路删除；直接调 `RequestAsync`，未注册该格式的渠道会 fail-soft 返回 `Success=false` 的 `AdLoadResult`，不抛异常。
- **误区：取消 await 等于取消原厂广告**：Request 取消仅结束该观察者，基类 Show 取消也不结束原生展示占用；自定义渠道还须自行响应取消，不能紧接着发第二次全屏 Show。
- **误区：释放后继续复用旧聚合和 Events**：完整关闭后从新的 Manager 初始化轮取得新实例，重新建立订阅；不要对旧 AdPlugin 再次 Initialize。
- **误区：从 `ShowAsync` 读取 `AdResult`**：`ShowAsync` 返回 `UniTask`，只用于等待展示流程结束；展示成功、失败和关闭结果分别从 `Events.ShowCompleted`、`Events.ShowFailed`、`Events.AdClosed` 获取。激励发奖应按渠道核对 `AdClosed` 的 `Success`、`UserCompleted` 与 `RewardGranted`，并由业务处理回放和幂等；这些字段不证明业务已实际发奖。
- **误区：Banner 用 ShowAsync**：Banner 走 `RequestAsync(AdFormat.Banner)` 预加载后用 `ShowBanner()` 展示，`ShowAsync` 不适用 Banner 格式。
- **误区：通过广告公共接口同步查询国家码**：公共接口不暴露同步国家码查询；跨 SDK 消费用 `GetCountryCodeAsync(ct)`，广告模块只负责等待运行时数据，超时返回空字符串。
- **误区：只检查 `HasUserConsent()`**：该方法在明确拒绝、尚未设置或渠道不支持查询时都返回 `false`，必须先检查 `IsUserConsentSet()`。
- **误区：循环等待 `IsUserConsentSet()`**：非适用地区或未展示弹窗时可能始终为 `false`；启动门禁应等待 `WaitForPrivacyFlowAsync()`。
- **误区：把AdLoadFailed事件当作本次Request最终失败**：该事件是一个广告ID的结果，组内后项及其他组仍可能成功；业务请求结果以await RequestAsync为准。
- **误区：业务层沿用旧 C# event 订阅方式**：`IAdPlugin` 不再暴露 `OnAdRevenuePaid / OnAdLoaded / OnAdLoadFailed` 等 C# event；业务层改为通过 `AdPlugin.Events.RevenuePaid.Subscribe()` 等方式订阅。

---

## §11 使用示例

以下为业务脚本成员片段，省略 using 与类声明；在聚合插件 Ready 后绑定事件。ShowCompleted / ShowFailed / AdClosed 会同步补发历史记录，因此日志订阅不直接发奖，展示流程先订阅、再开启本次处理标记。示例假定该流程是激励广告的唯一展示入口；多入口项目需统一展示和奖励归属，`AdResult` 没有业务请求 ID。

示例用页面令牌结束观察。取消等待不结束原生广告；若业务要求页面离开或等待取消后仍兑现奖励，应将奖励订阅放到能存活至原生终态的业务服务中，不能在此 finally 或页面销毁时释放它。Replay 仅是内存回放，不能代替奖励幂等记录。

```csharp
private IAdPlugin m_Ad;
private AdPlugin m_AdPlugin;
private readonly List<IDisposable> m_Bag = new List<IDisposable>();

// 聚合 Ready 后建立订阅；Awake / Start 本身不保证 SDK 已就绪。
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

    m_AdPlugin.Events.RevenuePaid.Subscribe(e =>
    {
        // 收益业务通知在Unity主线程；渠道已负责收益埋点，不在这里重复上报。
        Log.Debug($"广告收入通知：{e.Format} / {e.PlacementId} / {e.Revenue}");
    }, m_Bag);
}

// 激励视频完整流程（含 customProps 透传示例）
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

// Banner 广告
async UniTask ShowBanner(CancellationToken ct)
{
    // 无需 Supports 检查；未注册 Banner 的渠道自然 fail-soft
    var loaded = await m_Ad.RequestAsync(AdFormat.Banner, ct: ct);
    if (!loaded.Success) return;
    m_Ad.UpdateBannerPosition(BannerPosition.Bottom);
    m_Ad.ShowBanner();
}

// 取消所有订阅
void OnDestroyed()
{
    foreach (var d in m_Bag) d.Dispose();
    m_Bag.Clear();
}
```

---

## §13 关联文档

- [IBannerControl.md](./IBannerControl.md) — Banner 专属控制接口
- [AdPluginEvents.md](./AdPluginEvents.md) — 事件容器（7 个 ObservableEvent 字段）
- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — 渠道插件抽象基类
- [ObservableEvent.md](../../../../Assets/Framework/Docs/Runtime/Modules/SDK/Events/ObservableEvent.md) — StickyEvent / ReplayEvent 基类与订阅模式
- [ISDKPlugin.md](../../../../Assets/Framework/Docs/Runtime/Modules/SDK/Definitions/ISDKPlugin.md) — 根接口
- [Data.md](../../../../Assets/Framework/Docs/Runtime/Modules/SDK/Definitions/Data.md) — Framework 的 AdFormat / BannerPosition / AdLoadResult
- [AdFormatNotSupportedException.cs](../Scripts/Runtime/Types/AdFormatNotSupportedException.cs) — 渠道未实现已注册格式时的异常定义
- [AdResult.cs](../Scripts/Runtime/Types/AdResult.cs) / [AdEvent.cs](../Scripts/Runtime/Types/AdEvent.cs) — 广告包的展示结果与收益通知载荷
- [AdPluginEvents.md](./AdPluginEvents.md) — 收益业务通知与渠道实际上传的边界
