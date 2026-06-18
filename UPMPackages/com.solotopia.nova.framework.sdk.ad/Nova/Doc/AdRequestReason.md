# AdRequestReason

**类签名**：`public enum AdRequestReason`
**命名空间**：`NovaFramework.Runtime`

广告请求原因枚举，描述本次 `RequestAsync` 由何种场景触发，随 `nova_ad_request` 事件的 `nova_ad_reason` 字段上报。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Definitions/AdRequestReason.cs` | `AdRequestReason` | 枚举全部定义 |

文件位于 `Nova/Scripts/Runtime/`。

---

## §5 枚举值表

| 值 | 整数 | 何时使用 |
|---|---|---|
| `Auto` | `0` | 默认值；外部首次主动调起（如启动阶段预加载），调用方不指定 reason 时自动使用此值 |
| `Manual` | `1` | 业务层显式调起；区别于启动自动加载，由玩家交互或业务逻辑主动触发 |
| `Retry` | `2` | 状态机失败后自动重试；由基类 `ScheduleRetry` / `DelayedRetryAsync` 内部注入，业务层无需手动传递 |
| `AutoRefill` | `3` | 广告展示结束后自动续杯补量；由基类 `MarkShown` 内部注入，续杯时 customProps 同步置 null |

> `nova_ad_reason` 仅出现在 `nova_ad_request` 事件中，其他 8 个事件不携带此字段。

---

## §11 使用示例

```csharp
// 渠道层主动调起：传 Manual，后续 fill / fill_fail 打点可携带业务维度
// （reason 与 customProps 参数属于 IAdInternalPlugin / AdChannelPluginBase 层，
//  业务层公开接口 IAdPlugin.RequestAsync 只接受 format + ct）
await channelPlugin.RequestAsync(
    AdFormat.RewardedVideo,
    AdRequestReason.Manual,
    new Dictionary<string, object> { { "scene", "level_end" } },
    ct);

// 展示结束续杯：由 MarkShown 自动注入 AutoRefill，业务层无需关心
// 示意代码（实际在 AdChannelPluginBase.Methods.cs 的 MarkShown 中执行）：
// RequestAsync(unit.Format, AdRequestReason.AutoRefill, null, CancellationToken.None)

// 业务层调用入口（不感知 reason / customProps）：
await Nova.SDK.Get<IAdPlugin>().RequestAsync(AdFormat.RewardedVideo, ct);
```

---

## §13 关联文档

- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — 基类；`RequestAsync` 使用此枚举，`AdUnit.LastReason` 存储当前值
