# IAcquisitionTrackPlugin

**类型签名**：`public interface IAcquisitionTrackPlugin : ISDKPlugin`  
**命名空间**：`NovaFramework.Runtime`

投放打点接口，用于买量、投放优化和用户获取转化事件上报。它与 `IAttributionPlugin` 不同：`IAttributionPlugin` 侧重获取归因数据，`IAcquisitionTrackPlugin` 侧重把应用内转化事件上报给投放平台。

## 当前公开 API

```csharp
public interface IAcquisitionTrackPlugin : ISDKPlugin
{
    void SetUserId(string userId);
    void TrackEvent(TrackEvent evt);
    void TrackEvent(string eventName, Dictionary<string, object> parameters);
}
```

## 语义约定

- `SetUserId()` 设置当前业务用户 ID，用于平台侧关联投放转化事件。
- `TrackEvent(TrackEvent evt)` 与 `TrackEvent(string, Dictionary<string, object>)` 上报投放转化事件。
- 接口不包含 `SetUserProperty()`；不要求投放平台支持通用用户属性写入。
- 参数字典允许为 `null`；实现层负责按平台能力过滤空 key、空 value 和不支持的值类型。

## 当前实现

- Facebook 包的 `FacebookPlugin` 实现该接口，并通过 Facebook Unity SDK `FB.LogAppEvent` 上报 App Events。
- Facebook 初始化后订阅 `SDKEventData.UserLogin`，在业务用户登录时把 `UserId` 同步到 Facebook App Events。

## 关联文档

- [ITrackPlugin.md](./ITrackPlugin.md)
- [IMonetizeTrackPlugin.md](./IMonetizeTrackPlugin.md)
- [IAttributionPlugin.md](./IAttributionPlugin.md)
- [../../Definitions/ISDKPlugin.md](../../Definitions/ISDKPlugin.md)
