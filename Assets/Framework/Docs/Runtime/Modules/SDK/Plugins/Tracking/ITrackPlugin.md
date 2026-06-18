# ITrackPlugin

**类签名**：`public interface ITrackPlugin : ISDKPlugin`
**命名空间**：`NovaFramework.Runtime`
**源码文件**：`Assets/Framework/Scripts/Runtime/Modules/SDK/Plugins/Tracking/ITrackPlugin.cs`

通用事件埋点接口，抽象用户属性设置与自定义事件上报能力。

---

## §1 文件头

| 属性 | 值 |
|---|---|
| 类签名 | `public interface ITrackPlugin : ISDKPlugin` |
| 命名空间 | `NovaFramework.Runtime` |
| 职责 | 定义通用分析埋点契约：设置用户 ID、设置用户属性、上报自定义事件 |

典型实现：TGATrackPlugin、FirebaseAnalyticsPlugin 等。

---

## §2 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `Plugins/Tracking/ITrackPlugin.cs` | `ITrackPlugin` | 接口全部定义 |

---

## §3 继承关系

```
ISDKPlugin
  └── ITrackPlugin
```

---

## §5 完整公开 API

```csharp
public interface ITrackPlugin : ISDKPlugin
{
    void SetUserId(string userId);
    void SetUserProperty(string key, string value);
    void TrackEvent(TrackEvent evt);
    void TrackEvent(string eventName, Dictionary<string, object> parameters);
}
```

---

## §11 使用示例

```csharp
// 登录后设置用户 ID（扇出到所有通用埋点插件）
foreach (var tracker in Nova.SDK.GetAll<ITrackPlugin>())
{
    tracker.SetUserId(loginResult.UserId);
}

// 两种上报方式都可用
foreach (var tracker in Nova.SDK.GetAll<ITrackPlugin>())
{
    tracker.TrackEvent("level_complete", new Dictionary<string, object>
    {
        { "level", 5 },
        { "score", 3200 }
    });
}
```

---

## §13 关联文档

- [ISDKPlugin.md](../../Definitions/ISDKPlugin.md) — 根接口
- [IMonetizeTrackPlugin.md](./IMonetizeTrackPlugin.md) — 变现事件埋点接口
- [../../Definitions/Data.md](../../Definitions/Data.md) — TrackEvent 数据类
