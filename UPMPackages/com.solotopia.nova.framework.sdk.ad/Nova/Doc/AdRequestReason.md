# AdRequestReason

**类签名**：`public enum AdRequestReason`
**命名空间**：`NovaFramework.SDK.AdPlugin.Runtime`

广告请求原因枚举，描述一次广告组加载轮由什么场景启动。基类在实际发出单 ID 加载前，将它写入 `nova_ad_request` 的 `nova_ad_reason` 属性；该原因不是业务请求的最终结果。

---

## §2 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `Types/AdRequestReason.cs` | `AdRequestReason` | 请求原因枚举定义 |

文件位于 `Nova/Scripts/Runtime/`。

---

## §5 枚举值表

| 值 | 整数 | 使用时机 |
|---|---|---|
| `Auto` | 0 | 渠道 `RequestAsync` 的默认原因，外部主动请求不指定原因时使用 |
| `Manual` | 1 | 渠道调用方主动标记业务请求；区别于默认自动请求 |
| `Retry` | 2 | 整组本轮全部 ID 失败并等待当前间隔后，由 `RetryGroupAsync` 通过 `ReserveGroupRound` 启动新的组轮 |
| `AutoRefill` | 3 | 非 Banner 真实关闭或展示失败后，由 `RefillAfterShow` 检查该格式全部组并启动空闲组补量 |

新轮保存启动者的原因与自定义属性，组内从 a1 切到 a2、a3 时沿用本轮原因；单个 ID 失败不会立即把后项原因改为 `Retry`。加入已有 Loading 轮的重叠请求只观察，不覆盖该轮的原因或属性。

后台重试和展示后补量的新轮使用空自定义属性。`nova_ad_reason` 只用于 `nova_ad_request`；fill / fill_fail 使用请求属性，但不另加该原因字段。已有缓存返回、仅观察已有轮、等待间隔或展示中跳过的组，不因一次业务调用而重复上报 request。

---

## §11 使用示例

### 渠道层指定原因

`reason` 参数属于 `IAdInternalPlugin / AdChannelPluginBase`；业务聚合接口不开放它。

~~~csharp
var props = new Dictionary<string, object> { { "scene", "level_end" } };
var result = await channelPlugin.RequestAsync(
    AdFormat.Rewarded,
    AdRequestReason.Manual,
    props,
    ct);
~~~

~~~text
Manual 请求轮：
  a1 失败 → a2 失败 → a3 失败
  三次尝试的 reason 都是 Manual
  → 整组等待当前间隔
  → 新轮 a1 / a2 / a3 的 reason 为 Retry
~~~

关闭或展示失败后的 `AutoRefill` 由基类自动发起，业务无需手动模拟补量。其他 Ready / Loading / WaitingRetry 组保持既有状态和轮次。

### 业务层透传属性

业务 `IAdPlugin.RequestAsync` 支持 `customProps`，签名为 `RequestAsync(format, customProps = null, ct = default)`，不接收 `reason`。

~~~csharp
var ad = Nova.SDK.Get<IAdPlugin>();
var props = new Dictionary<string, object> { { "scene", "level_end" } };
var result = await ad.RequestAsync(AdFormat.Rewarded, props, ct);

// 只提供取消令牌时使用命名参数，避免把 ct 当作属性字典。
var anotherResult = await ad.RequestAsync(AdFormat.Interstitial, ct: ct);
~~~

业务请求取消只结束自身观察，不取消已发出的原厂 Load、其他观察者或后台组重试。

---

## §13 关联文档

- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — 广告组轮次、属性快照与循环重试
- [IAdPlugin.md](./IAdPlugin.md) — 业务请求参数与结果
- [AdPluginConfig.md](./AdPluginConfig.md) — 共享广告组重试间隔
