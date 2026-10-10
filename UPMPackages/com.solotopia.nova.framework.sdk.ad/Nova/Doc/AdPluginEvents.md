# AdPluginEvents

**类签名**：`public sealed class AdPluginEvents`
**命名空间**：`NovaFramework.SDK.AdPlugin.Runtime`
**全局访问**：`Nova.SDK.Get<AdPlugin>().Events`

AdPlugin 事件容器，持有 7 个 ObservableEvent 字段，封装广告生命周期的全部可观察事件。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Nova/Scripts/Runtime/AdPluginEvents.cs` | `AdPluginEvents` | 全部定义 |

---

## §5 完整公开 API

`AdPluginEvents` 本身无方法；全部 API 通过各字段上的 `ObservableEvent<T>` 基类方法访问：

| 字段 | 类型 | 模式 | 容量 | 说明 |
|---|---|---|---|---|
| `InitResult` | `StickyEvent<bool>` | Sticky | — | SDK 初始化结果；订阅时若已完成初始化则立即补发最新值 |
| `AdLoaded` | `StickyEvent<AdLoadResult>` | Sticky | — | 广告加载成功；事件载荷 `Success=true`，订阅时补发最近一次加载成功结果 |
| `AdLoadFailed` | `StickyEvent<AdLoadResult>` | Sticky | — | 广告加载失败；事件载荷 `Success=false`，订阅时补发最近一次加载失败结果 |
| `ShowCompleted` | `ReplayEvent<AdResult>` | Replay | 32 | 广告展示成功（SDK displayed）；MAX 插屏此时 RewardGranted=true。每条记录独立有意义，不可被后续事件覆盖，不表示广告已关闭或激励已完成 |
| `ShowFailed` | `ReplayEvent<AdResult>` | Replay | 32 | 广告播放失败；每条失败记录独立有意义，业务层需逐条处理 |
| `RevenuePaid` | `ReplayEvent<AdEvent>` | Replay | 32 | 广告收益业务通知；新订阅者补收最近 32 条记录，收益埋点由渠道层处理 |
| `AdClosed` | `ReplayEvent<AdResult>` | Replay | 32 | 广告关闭；每条关闭记录独立有意义，业务层需逐条响应 |

> 所有字段均为 `readonly`，禁止外部替换实例。

### 奖励资格与回调时点

`Success` 表示当前回调对应的操作结果，`UserCompleted` 表示激励广告的官方奖励条件已经满足，`RewardGranted` 表示渠道给出的奖励资格。`RewardGranted` 不是“已发奖”的回执，也不是“已收到收益回调”的替代字段。

当前 MAX 渠道的取值如下；其他渠道须按其实现判断：

| 广告格式与回调 | Success | UserCompleted | RewardGranted | 关闭埋点 nova_can_get_reward |
|---|---|---|---|---|
| 激励展示成功 `ShowCompleted` | true | false | false | 不上报关闭事件 |
| 激励关闭 `AdClosed` | true | 取官方奖励回调状态 | 同 UserCompleted | 同 UserCompleted |
| 插屏展示成功 `ShowCompleted` | true | false | true | 不上报关闭事件 |
| 插屏关闭 `AdClosed` | true | false | true | true |
| 开屏展示成功 / 关闭 | true | false | false | 仅关闭时为 false |
| 任一全屏展示失败 `ShowFailed` | false | false | false | 不上报关闭事件 |

基类关闭入口保留渠道传入的 `AdResult.RewardGranted`；`RaiseAdClosed` 的 `rewarded` 参数只写入埋点属性 `nova_can_get_reward`。MAX 激励通过 `OnAdReceivedRewardEvent` 设置奖励状态，单独收到 `OnAdRevenuePaidEvent` 不会把激励奖励资格改为 true。

插屏展示成功和关闭都携带奖励资格，业务应选定其中一个入口发奖。Replay 会向新订阅者补发近期记录，业务发奖应使用自己的展示请求标识防止重复处理。它是容量为 32 的内存缓冲，不保证超出容量或进程退出后的交付。

`nova_ad_impression_id` 是埋点属性，不是 `AdResult` 字段；当前 MAX 会将它传到全屏关闭埋点，供分析平台串联同次展示。

通用订阅模式（以 `InitResult` 为例）：

```csharp
// 订阅，返回取消句柄
IDisposable sub = events.InitResult.Subscribe(handler);

// 订阅并绑定生命周期容器（推荐）
events.InitResult.Subscribe(handler, bag);

// 清空缓冲并移除全部订阅（AdPlugin.OnDisposeAsync 时由框架调用）
events.InitResult.Clear();
```

---

## §11 使用示例

```csharp
// 在业务脚本 OnStart 中订阅广告事件
private readonly List<IDisposable> m_AdBag = new List<IDisposable>();

void OnStart()
{
    // 注意：需要具体类型 AdPlugin，不能通过 IAdPlugin 接口访问 Events
    var adPlugin = Nova.SDK.Get<AdPlugin>();
    var ev = adPlugin.Events;

    // InitResult：Sticky，订阅时若 SDK 已初始化立即回调
    ev.InitResult.Subscribe(success =>
    {
        if (!success) Log.Warning("广告 SDK 初始化失败");
    }, m_AdBag);

    // ShowCompleted：MAX 插屏展示成功即有奖励资格；此例选择在展示成功时处理插屏奖励。
    ev.ShowCompleted.Subscribe(result =>
    {
        Log.Debug($"广告展示成功：{result.Format} / {result.PlacementId}");
        if (result.Format == AdFormat.Interstitial && result.Success && result.RewardGranted)
            GiveReward(); // 业务方法：应按本次展示请求做好防重复处理。
    }, m_AdBag);

    // AdClosed：此处只处理激励奖励；插屏奖励已由上面的展示成功入口处理。
    ev.AdClosed.Subscribe(result =>
    {
        if (result.Format == AdFormat.Rewarded && result.Success && result.RewardGranted)
            GiveReward();
    }, m_AdBag);

    // RevenuePaid：业务收入通知；MAX 适配层已负责派发 ad_ilrd 和 ad_impression。
    ev.RevenuePaid.Subscribe(e =>
    {
        Log.Debug($"广告收入通知：{e.Format} / {e.PlacementId} / {e.Revenue}");
    }, m_AdBag);
}

void OnDestroyed()
{
    foreach (var d in m_AdBag) d.Dispose();
    m_AdBag.Clear();
}
```

---

## §13 关联文档

- [ObservableEvent.md](../../../../Assets/Framework/Docs/Runtime/Modules/SDK/Events/ObservableEvent.md) — StickyEvent / ReplayEvent 基类与 API
- [IAdPlugin.md](./IAdPlugin.md) — 通过具体类型 `AdPlugin` 的 `Events` 属性暴露此容器
- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — 渠道侧触发各 event，AdPlugin 桥接到此容器
- [AdResult.cs](../Scripts/Runtime/Types/AdResult.cs) — 展示、失败和关闭结果字段的当前定义
