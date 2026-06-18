# IMonetizeTrackPlugin

**类签名**：`public interface IMonetizeTrackPlugin : ISDKPlugin`  
**命名空间**：`NovaFramework.Runtime`

变现分析埋点接口，用于设置用户身份、用户属性并上报事件。

## 当前公开 API

```csharp
public interface IMonetizeTrackPlugin : ISDKPlugin
{
    void SetUserId(string userId);
    void SetUserProperty(string key, string value);
    void TrackEvent(TrackEvent evt);
    void TrackEvent(string eventName, Dictionary<string, object> parameters);
}
```

## 关键语义

- 当前接口不承载购买流程，也不承载旧的 `TrackAdRevenue / TrackPurchase` API。
- 事件名和参数格式由业务与具体插件实现协商，框架只保证统一调用面。

## 关联文档

- [ITrackPlugin.md](./ITrackPlugin.md)
- [../../Definitions/Data.md](../../Definitions/Data.md)
- [../Ad/IAdPlugin.md](../Ad/IAdPlugin.md)
