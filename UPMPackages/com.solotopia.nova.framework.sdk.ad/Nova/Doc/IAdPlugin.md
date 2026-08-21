# IAdPlugin

**类签名**：`public interface IAdPlugin : ISDKPlugin, IBannerControl`
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Nova.SDK.Get<IAdPlugin>()`

IAA 广告聚合调度层唯一业务接口，内部持多个渠道，RequestAsync 并行广播，ShowAsync 选 Revenue 最高渠道执行。

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

    /// 查询指定广告格式是否正在播放中（插屏/RV 播放期间为 true）。
    bool IsAdPlaying(AdFormat format);

    // === 国家码 ===

    /// 异步获取广告 SDK 返回的有效国家或地区代码；超时后使用广告模块上次成功缓存，仍不可用时返回空字符串。
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
    /// 返回统一的 AdLoadResult；Success=true 表示加载成功，Success=false 时携带 ErrorCode / ErrorMessage。
    /// 业务层可直接用 result.Success 判断请求是否成功。
    /// customProps 会透传到渠道层 nova_ad_request / nova_ad_fill / nova_ad_fill_fail 打点。
    UniTask<AdLoadResult> RequestAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);

    // === 就绪查询 ===

    /// 查询是否有任意渠道的指定格式广告已就绪可立即展示。
    bool IsReady(AdFormat format);

    // === 展示 ===

    /// 展示指定格式广告，选 Revenue 最高的就绪渠道执行；无就绪渠道时 Log.Warning 并跳过（不抛异常）。
    /// AdFormat.Rewarded 时 AdResult.UserCompleted == true 表示用户看完，据此发放奖励。
    /// AdFormat.Banner 不适用此方法，Banner 展示请使用 IBannerControl.ShowBanner()。
    /// customProps 会透传到渠道层 nova_ad_show / nova_ad_show_result 失败分支 / nova_ad_hidden 打点。
    UniTask ShowAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);
}
```

> `IBannerControl` 方法（ShowBanner / HideBanner / DestroyBanner / UpdateBannerPosition(BannerPosition) / UpdateBannerPosition(Vector2) / StartBannerAutoRefresh / StopBannerAutoRefresh / SetBannerWidth / GetAdaptiveBannerHeight / SetBannerBackgroundColor）由 `AdPlugin` 委托给活跃 Banner 渠道；调用前须先 `RequestAsync(AdFormat.Banner)` 完成预加载。详见 [IBannerControl.md](./IBannerControl.md)。

---

## §8 初始化时序

`IAdPlugin` 由 `SDKManager` 在 `InitializeAsync` 阶段通过 `TypeCreator` 创建 `AdPlugin` 实例并注入 `AdPluginConfig`。业务层无需关心初始化顺序，可直接通过 `Events.InitResult.Subscribe` 等待 SDK 就绪通知。

国家码不通过 `IAdPlugin` 同步查询。`AdPlugin` 会在内部渠道返回有效国家码后发布 `SDKDataKeys.AdCountryCode` 并写入广告模块上次成功缓存；其他插件应通过 `GetCountryCodeAsync(ct)` 统一等待最终结果，不再直接等待数据槽位或自行做系统区域兜底。只有等待超时时会读取广告模块缓存，拿到空值或 `IV` 时直接返回空字符串。

广告隐私授权状态是同步缓存值。多渠道场景按配置顺序采用第一个已经取得明确决定的渠道：`IsUserConsentSet()` 为 `false` 表示尚未设置或渠道不支持查询；只有它为 `true` 时，`HasUserConsent()` 的 `false` 才表示明确拒绝。

业务启动页应先等待 `Nova.SDK.InitializeTask`，确保 `IAdPlugin` 与渠道列表已创建，再等待 `WaitForPrivacyFlowAsync(ct)`。没有展示隐私弹窗或渠道不支持隐私流程时该任务正常完成；展示弹窗时在用户同意或拒绝后完成。不要轮询 `IsUserConsentSet()` 作为启动门禁，否则未展示弹窗的用户可能永久等待。

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
- **误区：从 `ShowAsync` 读取 `AdResult`**：`ShowAsync` 返回 `UniTask`，只用于等待展示流程结束；展示成功、失败和关闭结果分别从 `Events.ShowCompleted`、`Events.ShowFailed`、`Events.AdClosed` 获取。激励奖励以 `AdClosed` 的 `UserCompleted == true` 为准。
- **误区：Banner 用 ShowAsync**：Banner 走 `RequestAsync(AdFormat.Banner)` 预加载后用 `ShowBanner()` 展示，`ShowAsync` 不适用 Banner 格式。
- **误区：通过广告公共接口同步查询国家码**：公共接口不暴露同步国家码查询；跨 SDK 消费用 `GetCountryCodeAsync(ct)`，等待、超时和缓存兜底均由广告模块负责。
- **误区：只检查 `HasUserConsent()`**：该方法在明确拒绝、尚未设置或渠道不支持查询时都返回 `false`，必须先检查 `IsUserConsentSet()`。
- **误区：循环等待 `IsUserConsentSet()`**：非适用地区或未展示弹窗时可能始终为 `false`；启动门禁应等待 `WaitForPrivacyFlowAsync()`。
- **误区：业务层沿用旧 C# event 订阅方式**：`IAdPlugin` 不再暴露 `OnAdRevenuePaid / OnAdLoaded / OnAdLoadFailed` 等 C# event；业务层改为通过 `AdPlugin.Events.RevenuePaid.Subscribe()` 等方式订阅。

---

## §11 使用示例

```csharp
// 通过接口获取（不含 Events 访问）
var ad = Nova.SDK.Get<IAdPlugin>();

// 若需要 Events，使用具体类型
var adPlugin = Nova.SDK.Get<AdPlugin>();
private readonly List<IDisposable> m_Bag = new List<IDisposable>();

// 订阅事件（在 OnStart / OnAwake 中）
void SetupEvents()
{
    adPlugin.Events.InitResult.Subscribe(ok =>
    {
        if (ok) RequestAds();
    }, m_Bag);

    adPlugin.Events.ShowCompleted.Subscribe(result =>
    {
        Log.Debug($"广告展示成功：{result.Format} / {result.PlacementId}");
    }, m_Bag);

    adPlugin.Events.ShowFailed.Subscribe(result =>
    {
        Log.Warning($"广告展示失败：{result.Format} / {result.ErrorMessage}");
    }, m_Bag);

    adPlugin.Events.AdClosed.Subscribe(result =>
    {
        if (result.Success && result.UserCompleted)
            GiveReward();
    }, m_Bag);

    adPlugin.Events.RevenuePaid.Subscribe(e =>
    {
        foreach (var t in Nova.SDK.GetAll<IMonetizeTrackPlugin>())
            t.TrackAdRevenue(e);
    }, m_Bag);
}

// 激励视频完整流程（含 customProps 透传示例）
async UniTask ShowRewardedAd(CancellationToken ct)
{
    // 无需提前 Supports 检查；未注册该格式的渠道 fail-soft 返回 Success=false
    var requestProps = new Dictionary<string, object> { { "scene", "main_menu" } };
    var loadResult = await ad.RequestAsync(AdFormat.Rewarded, requestProps, ct);
    if (!loadResult.Success) return;  // 全部失败，可读取 ErrorCode / ErrorMessage

    var showProps = new Dictionary<string, object> { { "scene", "main_menu" } };
    // ShowAsync 只等待流程结束；结果由上面的 ShowCompleted / ShowFailed / AdClosed 订阅处理。
    await ad.ShowAsync(AdFormat.Rewarded, showProps, ct);
}

// Banner 广告
async UniTask ShowBanner(CancellationToken ct)
{
    // 无需 Supports 检查；未注册 Banner 的渠道自然 fail-soft
    await ad.RequestAsync(AdFormat.Banner, ct: ct);
    ad.UpdateBannerPosition(BannerPosition.Bottom);
    ad.ShowBanner();
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
- [Data.md](../../../../Assets/Framework/Docs/Runtime/Modules/SDK/Definitions/Data.md) — AdFormat / AdResult / AdEvent / BannerPosition / AdLoadResult
- [Exceptions.md](../../../../Assets/Framework/Docs/Runtime/Modules/SDK/Definitions/Exceptions.md) — AdFormatNotSupportedException
- [IMonetizeTrackPlugin.md](../../../../Assets/Framework/Docs/Runtime/Modules/SDK/Plugins/Tracking/IMonetizeTrackPlugin.md) — RevenuePaid 上报目标
