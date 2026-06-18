# AdChannelPluginBase 打点扩展（Wave 2）设计

> 日期：2026-05-15  
> 范围：`Nova/Scripts/Runtime/`  
> 参考：旧版 Solar `ADBaseManager.Methods.Track` 方案

---

## §1 · 总览

把 `AdChannelPluginBase` 打点能力对齐旧版 `ADBaseManager.Methods.Track.cs`，补齐字段、事件与自定义参数支持。

### 4 大变更

1. **基类持有当前 channel 标识**：`AdChannelPluginBase` 新增抽象属性 `AdChannelType Channel { get; }`，派生类实现（如 `MaxAdPlugin` 返回 `AdChannelType.MAX`）。`Name` 属性保留，`nova_ad_channel` 打点字段用 `Name` 字符串。
2. **打点字段全面替换**：所有 `nova_ad_platform` 字段下线 → 改名 `nova_ad_channel`（值为 `Name` 字符串）；新增 `nova_ad_reason` / `nova_ad_result` 字段；不引入 `nova_advalue_avg`。
3. **新增 2 个事件**：`nova_ad_init`（`RaiseInitResult` 内打）+ `nova_ad_show_result`（`RaiseShowCompleted` / `RaiseShowFailed` 内打）。
4. **自定义参数支持**：`RequestAsync` 与 `ShowAsync` 加 `customProps`；`AdUnit` 槽位持有 `RequestCustomProps` / `ShowCustomProps`；fill/fill_fail/show/show_result/hidden 打点合并对应槽位的 customProps。

---

## §2 · 完整事件 / 字段对照表

> 9 个事件全口径列出。**所有事件都带 `nova_ad_channel = Name` 字符串**（无 `nova_ad_platform`）。

| # | 事件名 | 触发点 | 字段 |
|---|--------|--------|------|
| 1 | `nova_ad_init` | `RaiseInitResult(success)` | `nova_success`(bool), `nova_ad_channel` |
| 2 | `nova_ad_request` | `RequestAsync` 内每个发起的 AdUnit 一次 | `nova_ad_channel`, `nova_ad_format`(int), `nova_ad_id`, `nova_ad_reason`(int) + customProps |
| 3 | `nova_ad_fill` | `RaiseAdLoaded` | `nova_ad_channel`, `nova_ad_format`, `nova_ad_id`, `nova_ad_result`=Success(int), `nova_ad_network` + RequestCustomProps |
| 4 | `nova_ad_fill_fail` | `RaiseAdLoadFailed` | `nova_ad_channel`, `nova_ad_format`, `nova_ad_id`, `nova_ad_result`=Fail(int), `nova_ad_errorcode`, `nova_ad_error_message` + RequestCustomProps |
| 5 | `nova_ad_show` | 派生类调 `TrackAdShow`（SDK 展示回调内） | `nova_ad_channel`, `nova_ad_format`, `nova_ad_id` + ShowCustomProps |
| 6 | `nova_ad_show_result` ⭐新增 | `RaiseShowCompleted`=Success / `RaiseShowFailed`=Fail | `nova_ad_channel`, `nova_ad_format`, `nova_ad_id`, `nova_ad_result`(int) + ShowCustomProps |
| 7 | `nova_ad_click` | 派生类调 `TrackAdClick`（SDK 点击回调内） | `nova_ad_channel`, `nova_ad_format`, `nova_ad_id` + ShowCustomProps |
| 8 | `nova_ad_hidden` | `RaiseAdClosed(result, rewarded)`（统一基类内打） | `nova_ad_channel`, `nova_ad_format`, `nova_ad_id`, `nova_can_get_reward`(bool) + ShowCustomProps |
| 9 | `nova_ad_revenue_paid` | `RaiseRevenue` | `nova_ad_channel`, `nova_ad_format`, `nova_ad_id`, `nova_ad_network`, `nova_ad_revenue`, `nova_ad_currency` |

**注意：**
- `nova_ad_platform` 字段在所有打点中**整体下线**
- `nova_ad_reason` 仅 `nova_ad_request` 事件携带
- `nova_ad_result` 出现在 fill / fill_fail / show_result 三个事件中（int：Success=1 / Fail=0，按 `AdResult` 实际定义）

---

## §3 · 接口与签名变更

### 新增 channel 标识

```csharp
public abstract class AdChannelPluginBase : IAdInternalPlugin
{
    public abstract AdChannelType Channel { get; }   // 新增
    public abstract string Name { get; }             // 保留，用作 nova_ad_channel 值
}
```

派生侧（`MaxAdPlugin`）：

```csharp
public override AdChannelType Channel => AdChannelType.MAX;
public override string Name => "MAX";
```

### Reason 枚举（新建文件 `Definitions/AdRequestReason.cs`）

```csharp
public enum AdRequestReason
{
    Auto = 0,         // 外部首次主动调起（默认）
    Manual = 1,       // 业务显式调起
    Retry = 2,        // 状态机失败重试
    AutoRefill = 3,   // Show 后自动续杯
}
```

### 公开 API 签名变更

```csharp
// 旧
public UniTask RequestAsync(AdFormat format, CancellationToken ct = default);
public UniTask<AdResult> ShowAsync(AdFormat format, CancellationToken ct = default);

// 新（已落地）
public UniTask<AdLoadResult> RequestAsync(
    AdFormat format,
    AdRequestReason reason = AdRequestReason.Auto,
    Dictionary<string, object> customProps = null,
    CancellationToken ct = default);

public UniTask<AdResult> ShowAsync(
    AdFormat format,
    Dictionary<string, object> customProps = null,
    CancellationToken ct = default);
```

`IAdInternalPlugin` 同步更新签名。

> `Supports(AdFormat)` 已全链路删除（`IAdInternalPlugin` / `IAdPlugin` / `AdChannelPluginBase` / `AdPlugin`）。是否支持某 AdFormat 由 `RegisterAdUnits` 注册的槽位推导，未注册的格式 `RequestAsync` 返回 default，`IsReady` 返回 false，`ShowAsync` 返回失败结果（fail-soft）。

### 派生侧实现点（保持不变）

```csharp
protected virtual UniTask OnRequestAsync(AdFormat format, string placementId, CancellationToken ct);
protected virtual UniTask<AdResult> OnShowAsync(AdFormat format, string placementId, CancellationToken ct);
```

> 派生类不感知 reason / customProps，状态机层面就完成合并。

### `AdUnit` 新增字段（已落地）

```csharp
public Dictionary<string, object> RequestCustomProps;  // RequestAsync 传入，fill/fill_fail 合并
public Dictionary<string, object> ShowCustomProps;     // ShowAsync 传入，show/show_result/hidden 合并
public AdRequestReason LastReason;                     // 仅 nova_ad_request 用
// 注：MaxRetryCount / RetryIntervalSec 已从 AdUnit 移除，重试参数全局化到 AdChannelConfigList
```

### 派生侧打点入口梳理

| 旧 protected API | 新归属 |
|---|---|
| `TrackAdShow(format, placementId, extra=null)` | 派生类**仍调**（SDK 展示回调内）；基类自动合并 `unit.ShowCustomProps` |
| `TrackAdClick(format, placementId)` | 派生类**仍调**（SDK 点击回调内）；基类自动合并 `unit.ShowCustomProps` |
| `TrackAdClose(format, placementId, rewarded)` | **删除**——并入 `RaiseAdClosed(result, rewarded)` |
| `TrackAdRequest(format, placementId)` | **删除**——`RequestAsync` 内基类自动打 |

`RaiseAdClosed` 签名变更：

```csharp
// 旧
protected void RaiseAdClosed(AdResult result);
// 新
protected void RaiseAdClosed(AdResult result, bool rewarded = false);
```

---

## §4 · 文件落地清单

### 新建（1 个）

| 文件 | 内容 |
|------|------|
| `Definitions/AdRequestReason.cs` | enum：Auto/Manual/Retry/AutoRefill |

### 改动 ad 包（5 个）

全部位于 `Nova/Scripts/Runtime/`：

| 文件 | 动作 |
|------|------|
| `Interfaces/IAdInternalPlugin.cs` | RequestAsync/ShowAsync 签名加 reason + customProps；新增 `AdChannelType Channel { get; }` |
| `AdChannelPluginBase.cs` | 公开 API 签名同步；新增 abstract `Channel`；`RaiseAdClosed` 加 rewarded 参数 |
| `AdChannelPluginBase.Definitions.cs` | `AdUnit` 新增 `RequestCustomProps` / `ShowCustomProps` / `LastReason` |
| `AdChannelPluginBase.Methods.cs` | `RequestAsync` 写入 `LastReason` + `RequestCustomProps`；`ShowAsync` 写入 `ShowCustomProps`；续杯调 RequestAsync(AutoRefill, null)；`BuildBaseProps` 字段名 `nova_ad_platform` → `nova_ad_channel`；新增工具 `MergeCustom`；删除 `TrackAdRequest` 与 `TrackAdClose` |
| `AdChannelPluginBase.Track.cs` | 9 事件全口径打点：①新增 `nova_ad_init`（RaiseInitResult）②`RaiseShowCompleted/Failed` 增 `nova_ad_show_result` ③fill/fill_fail 加 `nova_ad_result` + `MergeCustom(RequestCustomProps)` ④`RaiseAdClosed` 加 rewarded，并入 nova_ad_hidden 打点 |

### 改动 max 包（1 个）

| 文件 | 动作 |
|------|------|
| 包 `com.solotopia.nova.framework.sdk.max`，运行时文件 `Nova/Scripts/Runtime/MaxAdPlugin.cs` | 新增 `public override AdChannelType Channel => AdChannelType.MAX;` |

### 不动

`IAdChannelConfig.cs` / `IBannerControl.cs` / `AdPlugin.cs` / `AdEvent.cs` / `AdLoadEvent.cs`（公开类型 `AdLoadResult`）/ `AdResult.cs` / 其他 payload 类。

---

## §5 · 风险与边界

| 风险 | 缓解 |
|------|------|
| 公开签名变更破坏既有调用方 | `RequestAsync(format)` / `ShowAsync(format)` 调用编译兼容（默认参数）；`IAdInternalPlugin` 唯一实现 `AdChannelPluginBase` 受影响 |
| 续杯 AutoRefill 时 ShowCustomProps 未清 | `MarkShown` 内显式 `unit.ShowCustomProps = null`；续杯走 `RequestAsync(format, AutoRefill, null, CancellationToken.None)` |
| 派生类调 TrackAdShow/Click 时怎么传 customProps | 派生侧调用面不变；基类内查 `unit.ShowCustomProps` 自动合并 |
| fill / fill_fail customProps 来自 Request 阶段 | `unit.RequestCustomProps` 携带；`RaiseAdLoaded` / `RaiseAdLoadFailed` 打点时合并；续杯时 `RequestAsync(..., null, ...)`，`RequestCustomProps` 同步置 null |
| `Channel` 抽象属性破坏 `MaxAdPlugin` 编译 | 已列入 max 包改动 |
| `TrackAdClose` 删除影响 | 该方法 `protected` 仅派生类用；已并入 `RaiseAdClosed(result, rewarded)`，无外部影响 |
| `nova_ad_platform` 下线影响 BI | 已确认 channel=Name 字符串取代 platform；BI 侧字段名同步切（交付通知项） |
| `Supports` 删除影响调用方 | `IAdPlugin.Supports` 已删除；调用方直接 `RequestAsync` 取结果，未注册格式 fail-soft 返回 default/false，不抛异常 |
| `NotifyBatchFailed` 每次失败都通知调用方 | solar 语义：底层重试独立运转，调用方收到 default 后可重新调用 `RequestAsync`；下一轮重试成功后新 `RequestAsync` 可取到结果 |

---

## §6 · 自检计划

写完代码后核对：

1. 9 个事件字段表（§2）逐字段对照实现
2. `grep nova_ad_platform` 在打点 dict 内**零出现**
3. 全部 `RaiseAdClosed` 调用走新签名
4. 续杯路径 `MarkShown` → `ShowCustomProps = null` → `RequestAsync(AutoRefill, null)` → `RequestCustomProps = null`
5. `Channel` 属性在 `AdChannelPluginBase` 为 abstract、`MaxAdPlugin` 已实现
6. `nova_advalue_avg` 字段在打点 dict 内**零出现**
7. UnityMCP `read_console` 编译通过
