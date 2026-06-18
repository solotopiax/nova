# AdPlugin

**类签名**：`public sealed partial class AdPlugin : SDKPluginBase, IAdPlugin`
**命名空间**：`NovaFramework.SDK.AdPlugin.Runtime`
**全局访问**：`Nova.SDK.Get<AdPlugin>()` / `Nova.SDK.Get<IAdPlugin>()`

IAA 广告聚合调度层；初始化时按配置反射创建渠道实例，RequestAsync 并行广播给所有渠道，ShowAsync 选 Revenue 最高的就绪渠道执行，渠道事件统一桥接到 Events 容器。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Nova/Scripts/Runtime/AdPlugin.cs` | `AdPlugin` | 公开 API：IAdPlugin 实现（RequestAsync / IsReady / ShowAsync）、IBannerControl 委托实现、SDKPluginBase 生命周期 |
| `Nova/Scripts/Runtime/AdPlugin.Visitors.cs` | `AdPlugin` | 字段与属性：`m_ChannelPlugins`、`m_ActiveBannerChannel`、`Events`、`Name`、`Priority` |
| `Nova/Scripts/Runtime/AdPlugin.Methods.cs` | `AdPlugin` | 私有方法：`SelectBestChannel`、`BroadcastRequestAsync`、`SafeRequestAsync`、`CreateChannel`、`WireChannelEvents`、`RegisterChannel` |

---

## §3 继承关系

```
SDKPluginBase
  └── AdPlugin（sealed partial，实现 IAdPlugin）
        IAdPlugin 继承 ISDKPlugin + IBannerControl
```

---

## §4 关键字段表

| 字段 / 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_ChannelPlugins` | `List<IAdInternalPlugin>` | `null`（OnInitializeAsync 初始化） | 所有已注册渠道插件列表 |
| `m_ActiveBannerChannel` | `IAdInternalPlugin` | `null` | RequestAsync(Banner) 成功后记录的活跃 Banner 渠道；Banner 控制方法委托到此渠道 |
| `m_EventManager` | `IEventManager` | `null` | 事件管理器引用；OnInitializeAsync 末尾取得，OnDisposeAsync 开头清空 |
| `Events` | `AdPluginEvents` | `new AdPluginEvents()` | 事件容器，readonly，持有 7 个 ObservableEvent 字段 |
| `Name` | `string` | `"AdPlugin"` | 插件友好名 |
| `Priority` | `int` | `50` | 初始化优先级；高于框架默认值 100 |

---

## §5 完整公开 API

### IAdPlugin 实现

```csharp
/// 向所有渠道并行发起预加载请求；返回 AdLoadResult，Success=true 表示成功，Success=false 时携带错误详情。
/// Banner 格式在首个成功结果后更新 m_ActiveBannerChannel。
public async UniTask<AdLoadResult> RequestAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);

/// 查询是否有任意渠道的指定格式广告已就绪可立即展示。
public bool IsReady(AdFormat format);

/// 查询指定广告格式是否有任意渠道正在播放中；当前默认返回 false，业务层协调防重入。
public bool IsAdPlaying(AdFormat format);

/// 展示指定格式广告，选 Revenue 最高的就绪渠道执行；无就绪渠道时 Log.Warning 并跳过。
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

### SDKPluginBase 生命周期（override）

```csharp
/// 配置类型声明；SDKManager 自动注入 AdPluginConfig。
protected override Type ConfigType => typeof(AdPluginConfig);

/// 初始化：遍历 ChannelConfigs，反射创建渠道实例、ApplyGlobalConfig、InitializeAsync、WireChannelEvents、RegisterChannel；
/// 渠道列表构建完毕后直接方法组订阅 SDKEventData.UserLogin（m_EventManager + OnUserLogin）。
protected override UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct);

/// 释放：先 Unsubscribe SDKEventData.UserLogin 并清空 m_EventManager；
/// 再调用 Events 所有 ObservableEvent 字段的 Clear() 释放缓冲。
protected override UniTask OnDisposeAsync(CancellationToken ct);
```

### 私有方法（仅文档化，不对外公开）

```csharp
/// UserLogin 事件处理器；遍历 m_ChannelPlugins，对每个 channel 调用 SetUserId(login.UserId)；
/// 单个 channel 抛出异常时 Log.Error 并继续（try/catch 隔离，不影响其他渠道）。
private void OnUserLogin(object sender, EventData e);
```

### 关键私有方法

```csharp
/// 选出已就绪的渠道。比价模式开启时选 Revenue 最高渠道；关闭时按列表顺序取第一个就绪渠道。
/// 未注册该 format 的渠道 IsReady 必为 false，自然过滤。
private IAdInternalPlugin SelectBestChannel(AdFormat format);

/// 向所有渠道并行发起请求；未注册该 format 的渠道 RequestAsync 返回 Success=false 自然过滤。
private async UniTask<AdLoadResult> BroadcastRequestAsync(AdFormat format, Dictionary<string, object> customProps, CancellationToken ct);
```

---

## §7 事件订阅生命周期

AdPlugin 在 `OnInitializeAsync` 结尾订阅 `SDKEventData.UserLogin`，在 `OnDisposeAsync` 开头退订，以确保 Dispose 后 SDK 已销毁不再回调。

```
OnInitializeAsync:
  ... 构建 m_ChannelPlugins ...
  m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>()
  m_EventManager.Subscribe<SDKEventData.UserLogin>(OnUserLogin)

OnDisposeAsync:
  m_EventManager.Unsubscribe<SDKEventData.UserLogin>(OnUserLogin)  ← 先退订
  m_EventManager = null
  ... Events.*.Clear() ...

OnUserLogin(sender, e):
  for each channel in m_ChannelPlugins:
    try { channel.SetUserId(login.UserId) }
    catch { Log.Error + 继续 }
```

**直接方法组订阅：** Subscribe/Unsubscribe 直接传 `OnUserLogin` 方法组，CLR 委托 Equals 按 Target+Method 比对，同一实例方法的两次方法组转换结果 Equals，Unsubscribe 可正确配对，无需字段缓存。

---

## §8 初始化时序

1. `SDKManager` 创建 `AdPlugin` 实例，调用 `OnInitializeAsync(adConfig, ct)`。
2. 遍历 `adConfig.ChannelConfigs.Items`，对每个已启用渠道：
   a. `CreateChannel`：反射创建实例 → `ApplyGlobalConfig(adConfig.ChannelConfigs)` → `InitializeAsync(ct).Forget()`。
   b. `WireChannelEvents`：订阅渠道 7 类事件，桥接到 `Events` 容器。
   c. `RegisterChannel`：追加到 `m_ChannelPlugins`。
3. 渠道 SDK 异步初始化，回调通过 `RaiseInitResult` → `Events.InitResult.Invoke` 通知业务层。
4. 渠道列表构建完毕后，从 `FrameworkManagersGroup` 取 `IEventManager` 并订阅 `SDKEventData.UserLogin`。

---

## §10 常见误区

- **误区：通过 `IAdPlugin` 访问 `Events`**：`Events` 属性不在接口上；须 `Nova.SDK.Get<AdPlugin>().Events` 或显式转型后访问。
- **误区：Banner 用 `ShowAsync`**：Banner 走 `RequestAsync(AdFormat.Banner)` 预加载后用 `ShowBanner()` 展示。
- **误区：激励视频只判断 Success**：须同时判断 `AdResult.UserCompleted == true` 才发奖励。
- **误区：直接调 Banner 控制方法而不先 RequestAsync**：`m_ActiveBannerChannel` 在 `RequestAsync(Banner)` 成功后才会被赋值；未预加载时所有 Banner 控制方法为无操作。
- **误区：调 `Supports(format)` 检查格式**：`Supports` 方法已全链路删除；直接 `RequestAsync`，未注册该格式的渠道 fail-soft 返回 `Success=false`，不抛异常。

---

## §11 使用示例

```csharp
// 通过接口获取（不含 Events 访问）
var ad = Nova.SDK.Get<IAdPlugin>();

// 若需要 Events，使用具体类型
var adPlugin = Nova.SDK.Get<AdPlugin>();
private readonly List<IDisposable> m_Bag = new List<IDisposable>();

void SetupEvents()
{
    adPlugin.Events.InitResult.Subscribe(ok =>
    {
        if (ok) RequestAds();
    }, m_Bag);

    adPlugin.Events.ShowCompleted.Subscribe(result =>
    {
        if (result.Success && result.UserCompleted)
            GiveReward();
    }, m_Bag);
}

// 激励视频完整流程
async UniTask ShowRewardedAd(CancellationToken ct)
{
    // 无需提前 Supports 检查，直接 RequestAsync；未注册格式的渠道 fail-soft 返回 Success=false
    var loadResult = await ad.RequestAsync(AdFormat.RewardedVideo, null, ct);
    if (!loadResult.Success) return;

    var result = await ad.ShowAsync(AdFormat.RewardedVideo, null, ct);
    if (result.Success && result.UserCompleted)
        GiveReward();
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
