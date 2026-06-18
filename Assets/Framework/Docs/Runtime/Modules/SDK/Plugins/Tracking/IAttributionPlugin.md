# IAttributionPlugin

**类签名**：`public interface IAttributionPlugin : ISDKPlugin`
**命名空间**：`NovaFramework.Runtime`
**源码文件**：`Assets/Framework/Scripts/Runtime/Modules/SDK/Plugins/Tracking/IAttributionPlugin.cs`

归因接口，获取安装来源、Campaign 及 Conversion 数据。

---

## §1 文件头

| 属性 | 值 |
|---|---|
| 类签名 | `public interface IAttributionPlugin : ISDKPlugin` |
| 命名空间 | `NovaFramework.Runtime` |
| 职责 | 定义归因数据获取契约：异步获取归因数据、监听归因就绪事件 |

典型实现：AppsFlyer Plugin、Adjust Plugin。

---

## §2 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `Plugins/Tracking/IAttributionPlugin.cs` | `IAttributionPlugin` | 接口全部定义 |

---

## §3 继承关系

```
ISDKPlugin
  └── IAttributionPlugin
```

---

## §5 完整公开 API

```csharp
public interface IAttributionPlugin : ISDKPlugin
{
    void SetUserId(string userId);
    void TrackEvent(TrackEvent evt);
    void TrackEvent(string eventName, Dictionary<string, object> parameters);
    UniTask<AttributionData> GetAttributionAsync(CancellationToken ct = default);
    event Action<AttributionData> OnAttributionResolved;
}
```

---

## §12 注意事项

- 归因数据通常在安装后**首次启动**时由平台服务器异步返回，回调时间不确定（可能几秒到十几秒）
- `GetAttributionAsync` 与 `OnAttributionResolved` 提供两种获取方式，可按需选择；两者均应在主线程处理结果
- 使用 `CancellationToken` 控制超时策略，避免因网络异常导致无限等待

---

## §11 使用示例

```csharp
if (Nova.SDK.TryGet<IAttributionPlugin>(out var attributionPlugin))
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
        attributionPlugin.SetUserId(userId);
        attributionPlugin.TrackEvent("session_start", null);

        var attribution = await attributionPlugin.GetAttributionAsync(cts.Token);
        Debug.Log($"MediaSource: {attribution.MediaSource}, Campaign: {attribution.Campaign}");
    }
    catch (OperationCanceledException)
    {
        Debug.LogWarning("归因数据获取超时");
    }
}

    attributionPlugin.OnAttributionResolved += data =>
    {
        Debug.Log($"归因就绪 - 来源：{data.MediaSource}, 自然：{data.IsOrganic}");
    };
}
```

---

## §13 关联文档

- [ISDKPlugin.md](../../Definitions/ISDKPlugin.md) — 根接口
- [../../Definitions/Data.md](../../Definitions/Data.md) — AttributionData 数据类
