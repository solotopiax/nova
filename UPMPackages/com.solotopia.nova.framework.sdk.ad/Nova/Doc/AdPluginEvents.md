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
| `ShowCompleted` | `ReplayEvent<AdResult>` | Replay | 32 | 广告播放完成；每条记录独立有意义，不可被后续事件覆盖 |
| `ShowFailed` | `ReplayEvent<AdResult>` | Replay | 32 | 广告播放失败；每条失败记录独立有意义，业务层需逐条处理 |
| `RevenuePaid` | `ReplayEvent<AdEvent>` | Replay | 32 | 广告收益；每次收益须上报，不可丢失，Replay 保障无漏发 |
| `AdClosed` | `ReplayEvent<AdResult>` | Replay | 32 | 广告关闭；每条关闭记录独立有意义，业务层需逐条响应 |

> 所有字段均为 `readonly`，禁止外部替换实例。

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

    // ShowCompleted：Replay，补发历史播放完成记录
    ev.ShowCompleted.Subscribe(result =>
    {
        if (result.Success && result.UserCompleted)
            GiveReward();
    }, m_AdBag);

    // RevenuePaid：Replay，收益不可丢失
    ev.RevenuePaid.Subscribe(e =>
    {
        foreach (var t in Nova.SDK.GetAll<IMonetizeTrackPlugin>())
            t.TrackAdRevenue(e);
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

- [../../Events/ObservableEvent.md](../../Events/ObservableEvent.md) — StickyEvent / ReplayEvent 基类与 API
- [IAdPlugin.md](./IAdPlugin.md) — 通过具体类型 `AdPlugin` 的 `Events` 属性暴露此容器
- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — 渠道侧触发各 event，AdPlugin 桥接到此容器
- [../../Definitions/Data.md](../../Definitions/Data.md) — AdResult / AdEvent / AdLoadResult 载荷类型
