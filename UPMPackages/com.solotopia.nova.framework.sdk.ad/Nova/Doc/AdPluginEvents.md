# AdPluginEvents

**类签名**：`public sealed class AdPluginEvents`
**命名空间**：`NovaFramework.SDK.AdPlugin.Runtime`
**全局访问**：`Nova.SDK.Get<AdPlugin>().Events`

AdPlugin 事件容器，持有 7 个 ObservableEvent 字段，汇聚当前聚合实例的初始化、加载、展示、收益与关闭业务通知；点击等渠道打点不提供独立业务事件。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Nova/Scripts/Runtime/AdPluginEvents.cs` | `AdPluginEvents` | 七个事件字段 |
| `Nova/Scripts/Runtime/Types/AdResult.cs` | `AdResult` | 展示、失败和关闭载荷 |
| `Nova/Scripts/Runtime/Types/AdEvent.cs` / `AdEventType.cs` | `AdEvent` / `AdEventType` | 收益载荷及事件类别 |

---

## §5 完整公开 API

`AdPluginEvents` 本身无方法；全部 API 通过各字段上的 `ObservableEvent<T>` 基类方法访问：

| 字段 | 类型 | 模式 | 容量 | 说明 |
|---|---|---|---|---|
| `InitResult` | `StickyEvent<bool>` | Sticky | — | 单个渠道初始化结果；订阅时补发最近一次通知，不表示所有渠道成功 |
| `AdLoaded` | `StickyEvent<AdLoadResult>` | Sticky | — | 单个广告ID加载成功；`Success=true`，订阅时补发最近一次结果，不代表整次Request批次状态 |
| `AdLoadFailed` | `StickyEvent<AdLoadResult>` | Sticky | — | 单个广告ID加载失败；`Success=false`，组内后项或其他组仍可能成功，不代表整次Request最终失败 |
| `ShowCompleted` | `ReplayEvent<AdResult>` | Replay | 32 | 广告展示成功（SDK displayed）；MAX 插屏此时 RewardGranted=true。每条记录独立有意义，不可被后续事件覆盖，不表示广告已关闭或激励已完成 |
| `ShowFailed` | `ReplayEvent<AdResult>` | Replay | 32 | 原厂展示失败，或聚合层因已有全屏占用拒绝新Show的Busy失败；每条记录独立有意义 |
| `RevenuePaid` | `ReplayEvent<AdEvent>` | Replay | 32 | Unity主线程收益业务通知；补收最近32条，纯收益即时上报由渠道层处理 |
| `AdClosed` | `ReplayEvent<AdResult>` | Replay | 32 | 广告关闭；每条关闭记录独立有意义，业务层需逐条响应 |

> 所有字段均为 `readonly`，禁止外部替换实例。

### 请求观察、单 ID 事件与线程边界

`InitResult` 不携带渠道 ID，也不是全渠道初始化汇总。一次成功后仍可能收到另一渠道的失败；Sticky 补发最近一次结果。聚合插件 Ready 只代表自身初始化完成，原厂广告可用性由渠道成功门、`RequestAsync` 和 `IsReady` 决定。

AdLoaded和AdLoadFailed描述已接受的单个广告ID加载结果。A1失败会发布AdLoadFailed，但同组仍可尝试A2；其他组也继续运行。RequestAsync的await结果由独立批次观察决定：任意可播成功即完成，全体观察轮失败后返回最近实际失败一次。后台新轮可以继续发布新事件，不能再次完成已经失败或取消的旧观察者。Sticky补发是历史最后一条事件，不能用它代替IsReady的原厂真实Ready查询。

基类渠道的加载、展示、关闭、收益状态写和业务事件通知在Unity主线程串行执行；MAX的ad_impression及非Banner ad_ilrd纯上报可在收益回调原线程即时执行。Banner累计存档与业务通知仍在主线程。RevenuePaid供业务观察，不替代渠道收益上报，也不用于推导激励奖励资格。

聚合 Request 的 ct 结束本次业务等待，不清已经发出的原厂 Loading；基类渠道的 Show 同样只取消外层等待，原生展示占用保留。IsAdPlaying 读取基类渠道的占用，在 Show 发出后保持对应全屏格式为 true，直到匹配 hidden / displayFailed 或渠道释放。基类重复终态只收口一次；Busy 失败不会结束正在播放的原广告。ShowCompleted 只表示 displayed，不能把它当作关闭或激励完成。

直接实现 IAdInternalPlugin 的自定义渠道须自行保证回调线程、真实 Ready 和终态去重；聚合桥接直接转发事件，既不自动切主线程，也不记录这类渠道的原生展示占用。聚合 Show 与隐私等待仅透传业务 ct，没有附加聚合生命周期或外部取消包装；忽略 ct 的自定义任务可能在取消或聚合释放后继续挂起，具体边界见 [AdPlugin.md](./AdPlugin.md#取消令牌的作用范围)。

本地日志与业务事件需要分开判断：MAX 原始回调在过滤前记录，原始回调日志不保证发布事件，单 ID 失败事件也不等于 Request 最终失败。同组失败后继续请求下一真实广告 ID，可通过平台失败回调与实际渠道请求核对；广告组新轮日志仅在展示后补量（`reason=AutoRefill`）和自动重试（`reason=Retry`）时记录，手动或默认业务请求只记录准备请求、实际渠道请求和平台加载回调。日志仅记录关键节点，事件和原生占用仍按既有真实回调推进。详细日志字段见 [AdPlugin.md](./AdPlugin.md#本地请求与展示日志) 与 [AdChannelPluginBase.md](./AdChannelPluginBase.md#本地流程日志)。

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

### 结果、收益载荷与事件枚举

以下载荷归属广告聚合包的 `NovaFramework.SDK.AdPlugin.Runtime`；Framework 保留 `AdFormat`、`BannerPosition` 和 `AdLoadResult`。它们是公开可写字段的数据类，没有构造时校验；字段含义由渠道填充，不能把 XML 注释中的预期值当成强制约束。

| `AdResult` 字段 | 类型 | 含义 |
|---|---|---|
| `Success` | `bool` | 当前 displayed、失败或关闭回调的结果，不是所有展示阶段的汇总 |
| `UserCompleted` | `bool` | 激励完成资格；MAX 在激励关闭时取官方奖励回调状态 |
| `PlacementId` | `string` | 本次广告 ID |
| `Format` | `AdFormat` | 广告格式 |
| `ErrorMessage` | `string` | 失败原因；数据类不保证失败时一定非空 |
| `Network` | `string` | 投放网络，渠道未填时为 null |
| `Revenue` | `double` | 渠道填入的收益；MAX 使用 USD，不由 DTO 强制非负 |
| `Currency` | `string` | 货币代码；MAX 填 USD，数据类本身默认 null |
| `RewardGranted` | `bool` | 奖励资格，不表示业务已发奖，时点见上表 |

| `AdEvent` 字段 | 类型 | 含义 |
|---|---|---|
| `EventType` | `AdEventType` | 载荷类别；当前 MAX 收益通知未显式赋值，默认 Revenue |
| `Format` | `AdFormat` | 曝光广告格式 |
| `PlacementId` / `Network` | `string` | 真实 ID / 投放网络 |
| `Revenue` / `Currency` | `double` / `string` | 收益和货币；当前 MAX 填原厂收益与 USD |
| `Precision` | `string` | 原厂收益精度，如 exact、estimated、publisher_defined |
| `Timestamp` | `DateTime` | 事件时间字段；当前 MAX 未赋值，保持默认值，不能当作自动生成的 UTC 曝光时间 |

`AdEventType` 为 `Revenue=0`、`Loaded=1`、`LoadFailed=2`、`Displayed=3`、`Clicked=4`、`Closed=5`。它不对应七个事件字段的一一映射，当前 `AdPluginEvents` 没有独立点击业务事件；点击由渠道埋点入口处理。默认数值、布尔字段为 0 / false，引用字段为 null，未填的 `Format` 默认为 Rewarded。

通用订阅模式（以 `InitResult` 为例）：

```csharp
// 订阅，返回取消句柄
IDisposable sub = events.InitResult.Subscribe(handler);

// 订阅并绑定生命周期容器（推荐）
events.InitResult.Subscribe(handler, bag);

// 清空缓冲并移除全部订阅（AdPlugin.OnDisposeAsync 时由框架调用）
events.InitResult.Clear();
```

### 释放与新的聚合实例

`AdPlugin` 先关闭聚合入口并将所有渠道记录置为无效，取消聚合请求观察与尚未退出的渠道初始化。等待每个渠道的准备和实际初始化方法退出后，才用保存的原始委托最终退订七类事件并释放渠道。已被 SDK 捕获的旧桥接委托检查聚合释放状态和记录 Active，释放后不再回写旧 Events；单个事件移除器抛错不阻断其余退订。

初始化调用方 ct 只在渠道准备与初始化期间关联。取消发生时先通知渠道退出；准备方法结束后，无效记录才退出业务列表。渠道成功完成初始化后，该链接已解除，再取消原初始化 ct 不会关闭已成功渠道的事件桥接或聚合业务等待。业务页面应释放自己的订阅句柄；SDK 整体关闭由 Manager 释放聚合。

完整释放最终对七个事件调用 Clear()，清除订阅与历史记录。同一旧聚合对象的 Events 属性保持原容器引用，但该聚合和已释放渠道不可再次初始化；重复释放等待同一次清理结果。SDKManager 完成释放后若开启新一轮初始化，会创建新的 AdPlugin、新渠道和新 Events。业务须重新取得新 IAdPlugin / AdPlugin，重新订阅，不能继续使用旧容器；组件 InitializeTask 仍是单次缓存，重新等待旧任务不会启动新一轮。业务页面关闭不调用共享事件的 Clear()，也不释放整个广告插件。

---

## §11 使用示例

以下为业务脚本成员片段，省略 using 与类声明；在聚合插件 Ready 后绑定事件。ShowCompleted / ShowFailed / AdClosed 会同步补发历史记录，因此日志订阅不直接发奖，展示流程先订阅、再开启本次处理标记。示例假定该流程是激励广告的唯一展示入口；多入口项目需统一展示和奖励归属，`AdResult` 没有业务请求 ID。

示例用页面令牌结束观察。取消等待不结束原生广告；若业务要求页面离开或等待取消后仍兑现奖励，应将奖励订阅放到能存活至原生终态的业务服务中，不能在此 finally 或页面销毁时释放它。Replay 仅是内存回放，不能代替奖励幂等记录。

```csharp
// 在业务脚本 OnStart 中订阅广告事件
private readonly List<IDisposable> m_AdBag = new List<IDisposable>();
private AdPlugin m_AdPlugin;

void OnStart()
{
    // 注意：需要具体类型 AdPlugin，不能通过 IAdPlugin 接口访问 Events
    m_AdPlugin = Nova.SDK.Get<AdPlugin>();
    var ev = m_AdPlugin.Events;

    // InitResult：Sticky，补发最近一个渠道的初始化结果。
    ev.InitResult.Subscribe(success =>
    {
        if (!success) Log.Warning("收到一个广告渠道的初始化失败通知");
    }, m_AdBag);

    // ShowCompleted 只观察展示通知；历史回放不能直接作为本次发奖入口。
    ev.ShowCompleted.Subscribe(result =>
    {
        Log.Debug($"广告展示成功：{result.Format} / {result.PlacementId}");
    }, m_AdBag);

    // AdClosed：日志可接受回放；本次激励奖励由下面的展示流程单独处理。
    ev.AdClosed.Subscribe(result =>
    {
        Log.Debug($"广告关闭：{result.Format} / {result.PlacementId}");
    }, m_AdBag);

    // RevenuePaid：业务收入通知；MAX 适配层已负责派发 ad_ilrd 和 ad_impression。
    ev.RevenuePaid.Subscribe(e =>
    {
        Log.Debug($"广告收入通知：{e.Format} / {e.PlacementId} / {e.Revenue}");
    }, m_AdBag);
}

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
- [AdEvent.cs](../Scripts/Runtime/Types/AdEvent.cs) / [AdEventType.cs](../Scripts/Runtime/Types/AdEventType.cs) — 收益载荷与类别，实际默认值见本文
