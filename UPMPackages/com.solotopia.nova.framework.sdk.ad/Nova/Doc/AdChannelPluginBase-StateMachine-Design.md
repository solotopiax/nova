# AdChannelPluginBase 多 ID 状态机改造设计

> 日期：2026-05-15  
> 范围：`Nova/Scripts/Runtime/AdChannelPluginBase.*`  
> 参考：旧版 Solar ADHelper 方案（ADBaseManager / MAXManager / AdRequestInfo）

---

## §1 · 总览与目标

把旧版 `ADBaseManager` 的 `AdRequest` 状态机 + 多 ID 重试机制下沉到 `AdChannelPluginBase`，让所有渠道（MAX / TopOn / 等）天然具备「多 ID 池 + 独立计数重试 + eCPM 最高优先 Show」能力，业务调用面保持 `RequestAsync(format, ct)` / `IsReady(format)` / `ShowAsync(format, ct)` 不变。

### 改动范围（已锁）

- `AdChannelPluginBase` 三件套（`.cs` / `.Visitors.cs` / `.Methods.cs` / `.Track.cs`）
- 新增 `AdChannelPluginBase.Definitions.cs`（嵌套 enum + AdUnit + AdUnitOptions）
- `IAdInternalPlugin` 公开调用面不变；派生侧实现点（`OnRequestAsync` / `OnShowAsync`）签名追加 `placementId`，`OnIsReady` 删除

### 不动 / 非目标

| 不动 | 原因 |
|------|------|
| `IAdInternalPlugin` 公开 API | 调用面 `RequestAsync(format, ct)` 等签名不变 |
| `IAdChannelConfig` | 不强加 AdUnit 字段，由派生 config（如 `MaxAdChannelConfig`）自行扩展 |
| `AdPlugin` 聚合层 | 跨渠道比价仍由聚合层处理 |
| `MaxAdPlugin` | 真正接 MAX SDK 留给后续 wave |

| 非目标（留给后续 wave） | |
|------|---|
| ILRD 批量上报 | `BannerIlrdInterval` 字段保留但暂不消费 |
| AdValueOptimizer HTTP 上传 | — |
| CountryCode / SetReplaceAdsID / 全局静音消费 | `MuteAd` 字段保留但暂不消费 |
| 跨渠道比价 | 仍由 `AdPlugin.SelectBestChannel` 处理 |

---

## §2 · 状态机与数据结构

### 状态枚举（四状态，无 Failed 终止态）

```csharp
protected enum AdUnitState
{
    Idle,        // 初始 / 重试就绪 / 延时重试期间（可重新发起）
    Loading,     // 已发起请求，等待 SDK 回调
    Ready,       // 已加载完毕，可 Show
    Showing,     // 正在展示
    // Failed 已删除：solar 重试语义下无永久失败终止态
}
```

### 单广告位运行时槽位

```csharp
protected sealed class AdUnit
{
    public string PlacementId;
    public AdFormat Format;
    public AdUnitState State;
    public double Revenue;                // Ready 时存 SDK 报告的 eCPM (USD)；double 类型
    public int RetryCount;                // 达 RetryLoadAdMaxNum 后由 DelayedRetryAsync 清零重置
    public string LastErrorMessage;
    public CancellationTokenSource RetryCts;  // 取消 pending ImmediateRetry / DelayedRetry
    public Dictionary<string, object> RequestCustomProps;
    public Dictionary<string, object> ShowCustomProps;
    public AdRequestReason LastReason;
}
```

> `MaxRetryCount` / `RetryIntervalSec` 已从 `AdUnit` 移除，重试参数统一由全局 `RetryLoadAdMaxNum` / `RetryLoadAdInterv`（来自 `AdChannelConfigList`）控制，通过 `ApplyGlobalConfig(AdChannelConfigList globals)` 注入渠道实例。

### 配置注入数据

```csharp
// 重试参数已提升到全局，AdUnitOptions 只承载 PlacementId
public readonly struct AdUnitOptions
{
    public readonly string PlacementId;
    public AdUnitOptions(string placementId);
}
```

### 关键不变量

- 同一 `placementId` 在状态机内全局唯一（同 format 列表内重复时 `Log.Warning` 跳过）
- `Loading` 状态的 AdUnit 不允许再次发起 `OnRequestAsync`（防重入）
- 只有 `Ready` 状态的 AdUnit 才参与 ShowAsync 候选筛选

---

## §3 · 模板方法重构

### 公开 API（不变）

```csharp
public UniTask RequestAsync(AdFormat format, CancellationToken ct = default);
public bool IsReady(AdFormat format);
public UniTask<AdResult> ShowAsync(AdFormat format, CancellationToken ct = default);
```

### 新行为（基类实现）

| 方法 | 流程 |
|------|------|
| `RequestAsync(format, reason, customProps, ct)` | ① 取该 format 的 AdUnit 列表（不存在或空→ return default，fail-soft）；② 对每个 `Idle` 槽位置 `Loading` 并调 `OnRequestAsync(format, placementId, ct)`（fire-and-forget）；③ SDK 回调通过 `RaiseAdLoaded/RaiseAdLoadFailed` 驱动状态机推进 |
| `IsReady(format)` | 列表里**任一** AdUnit 满足 `State == Ready` 且 `OnIsReady(format, placementId) == true` 才返回 true |
| `ShowAsync(format, customProps, ct)` | ① `PickBestReadyUnit`：筛 `State == Ready` 的 AdUnit，按 `Revenue` 降序取最高（同时通过 `OnIsReady` 确认）；② 置 `Showing`；③ 调 `OnShowAsync(format, placementId, ct)`；④ 结果后置 `Idle` 并自动续杯（Banner 例外）；⑤ 无 Ready 槽位时 fail-soft 返回失败结果 |

### 派生实现点签名

```csharp
protected virtual UniTask OnRequestAsync(AdFormat format, string placementId, CancellationToken ct)
    => throw new AdFormatNotSupportedException(Name, format);

protected virtual UniTask<AdResult> OnShowAsync(AdFormat format, string placementId, CancellationToken ct)
    => throw new AdFormatNotSupportedException(Name, format);

// OnIsReady：新增，IsReady 双条件的 SDK 侧确认扩展点
protected virtual bool OnIsReady(AdFormat format, string placementId) => true;
// 派生类按 switch(format) 调 MaxSdk.IsRewardedAdReady(placementId) 等 SDK 真实查询
```

### SDK 回调 → 状态机推进

派生类（如 `MaxAdPlugin`）在 SDK 回调里继续调 `RaiseAdLoaded(e)` / `RaiseAdLoadFailed(e)` / `RaiseRevenue(e)` / `RaiseShowCompleted/Failed/AdClosed`。基类在这些 `Raise*` 内部除原有事件转发 + 打点外，**额外驱动 AdUnit 状态机**：

| 方法 | 状态机推进 |
|------|-----------|
| `RaiseAdLoaded(e)` | 找到匹配 `placementId` 的 AdUnit，置 `Ready`；`RetryCount = 0`；取消 pending RetryCts |
| `RaiseAdLoadFailed(e)` | 匹配 AdUnit 置 `Idle`，`RetryCount++`；未达 `RetryLoadAdMaxNum` 时 `ImmediateRetry`（Yield 一帧后重发）；达上限时 `ScheduleRetry` 等待 `RetryLoadAdInterv` 秒后清零 RetryCount 继续无限重试 |
| `RaiseRevenue(e)` | 写入 AdUnit `Revenue` 字段 |
| `RaiseShowCompleted(r)` | AdUnit `Showing → Idle`；非 Banner 触发续杯 |
| `RaiseShowFailed(r)` | AdUnit `Showing → Idle`；非 Banner 触发续杯 |
| `RaiseAdClosed(r, rewarded)` | AdUnit `Showing → Idle`；非 Banner 触发续杯 |

执行顺序：**先驱动状态机 → 再事件 fan-out → 再打点**。

---

## §4 · 配置注入

`IAdChannelConfig` 不强加字段——保持现状只持有 `Channel/PluginType/Enabled`。多 ID 由各派生 config 自己加（如 `MaxAdChannelConfig.RvUnits`），各渠道 config 形态自由，不绑定基类。

### 基类 protected API

```csharp
/// 派生类在 InitChannelSDKAsync 内部调用，向状态机注入某个 format 的多 ID 槽位
protected void RegisterAdUnits(AdFormat format, IReadOnlyList<AdUnitOptions> options);
```

校验：
- 空 `options` 直接 `Log.Warning` 跳过
- 同 placementId 重复 `Log.Warning` 跳过

### 派生侧典型用法

```csharp
protected override async UniTask InitChannelSDKAsync(IAdChannelConfig config, CancellationToken ct)
{
    var maxCfg = (MaxAdChannelConfig)config;
    RegisterAdUnits(AdFormat.RewardedVideo, maxCfg.RvUnits);
    RegisterAdUnits(AdFormat.Interstitial, maxCfg.InterUnits);
    RegisterAdUnits(AdFormat.AppOpen, maxCfg.AppOpenUnits);
    RegisterAdUnits(AdFormat.Banner, maxCfg.BannerUnits);
    await MaxSdk.InitializeAsync(...);  // 真正 SDK 初始化
}
```

---

## §5 · Banner 处理

Banner 同 Inter/RV/AppOpen 共用状态机；`IBannerControl` 接口（`ShowBanner/HideBanner/DestroyBanner/UpdateBannerPosition/Start/StopBannerAutoRefresh/SetBannerWidth/GetAdaptiveBannerHeight/SetBannerBackgroundColor`）仍由派生渠道重写，基类保持 `virtual` 空实现不变。

### Banner 状态机特殊点

- `RequestAsync(Banner)` → 与其他 format 一样进 Loading，SDK 回调置 Ready
- `ShowAsync(Banner)` → eCPM 最高的 AdUnit 进 Showing；派生侧 `OnShowAsync` 实现里调 `MaxSdk.ShowBanner(placementId)`
- **Banner 不自动续杯**（Banner 是常驻展示 + SDK 自带刷新机制，不需要框架层主动 RequestAsync）
- `HideBanner / DestroyBanner` → 派生侧调用后，基类需要把对应 Showing 的 AdUnit 回置 Idle；基类暴露 `MarkBannerHidden(placementId)` 给派生类调用

---

## §6 · 文件落地清单

### Implements 目录

| 文件 | 动作 | 说明 |
|------|------|------|
| `AdChannelPluginBase/AdChannelPluginBase.cs` | 改 | 删除 `Supports` abstract；新增 `OnIsReady` protected virtual；`IsReady` 改为双条件；`RequestAsync/ShowAsync` fail-soft 不抛异常；`ApplyGlobalConfig` 改为单参 `(AdChannelConfigList)` |
| `AdChannelPluginBase/AdChannelPluginBase.Visitors.cs` | 改 | 新增 `m_AdUnits` / `m_PendingBatches`；新增 `RetryLoadAdMaxNum` / `RetryLoadAdInterv` internal 属性 |
| `AdChannelPluginBase/AdChannelPluginBase.Methods.cs` | 改 | 新增 `RegisterAdUnits` / `ImmediateRetry` / `ImmediateRetryAsync`；`ScheduleRetry` / `DelayedRetryAsync` 改用全局 `RetryLoadAdInterv` 并清零 `RetryCount` |
| `AdChannelPluginBase/AdChannelPluginBase.Track.cs` | 改 | `NotifyBatchFailed` 改为 solar 语义（每次失败均通知调用方，不依赖 Failed 终止态） |
| `AdChannelPluginBase/AdChannelPluginBase.Definitions.cs` | 改 | 删除 `AdUnitState.Failed`；`AdUnit` 删除 `MaxRetryCount/RetryIntervalSec`；`AdUnitOptions` 改为单参构造；新增 `RequestBatch` 类 |

### 接口/聚合层（不动）

| 文件 | 状态 |
|------|------|
| `IAdInternalPlugin.cs` | 不变（公开调用面没变） |
| `IBannerControl.cs` | 不变 |
| `IAdChannelConfig.cs` | 不变 |
| `MaxAdPlugin.cs` | 本次不改（留给后续 wave 接 SDK） |
| `AdPlugin.cs` | 不变 |

### 文档

- `Nova/Doc/AdChannelPluginBase.md` 由 doc-writer 后续同步
- 本次变更仅影响本包内部实现，不涉及包外文档同步。

---

## §7 · 验证 / 风险

### 编译期验证

UnityMCP `read_console` 确认编译通过——partial class 跨文件 `using` 必须逐文件核对：
- `System.Collections.Generic`（Dictionary / List）
- `System.Threading`（CancellationTokenSource）
- `Cysharp.Threading.Tasks`（UniTask）

### 运行时风险

| 风险 | 缓解 |
|------|------|
| `ImmediateRetryAsync` / `DelayedRetryAsync` 可能泄露 | 每 AdUnit 持 `RetryCts`，`DisposeChannelSDKAsync` 时全部 cancel |
| `OnRequestAsync` 派生侧 SDK 同步抛异常 | `InvokeOnRequestSafeAsync` 捕获异常，回置 `Idle` 而非永远卡 `Loading` |
| 业务并发 `RequestAsync` | `Loading / Showing` 状态门禁，重复进入批次等待，不重复请求 |
| `Showing` 期间业务再次调 `ShowAsync` | `PickBestReadyUnit` 无 Ready 单元，fail-soft 返回失败结果 |
| ImmediateRetry 深递归（回调风暴） | `UniTask.Yield(PlayerLoopTiming.Update)` 打散为帧调度 |

### Spec 自检

- 占位扫描：无 TBD / TODO
- 章节自洽：§3 ShowAsync 「自动续杯」与 §5 Banner 「不自动续杯」不冲突——已在 §3 ⑤⑥ 显式区分
- 模糊度：自动续杯仅适用 RV / Inter / AppOpen，Banner 例外（已写明）
- 范围：仅 `AdChannelPluginBase.*` + 新增 `Definitions.cs`，不动接口与聚合层
