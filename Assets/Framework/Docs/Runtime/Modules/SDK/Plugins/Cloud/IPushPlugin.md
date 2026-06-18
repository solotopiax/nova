# IPushPlugin

**类签名**：`public interface IPushPlugin : ISDKPlugin`
**命名空间**：`NovaFramework.Runtime`

推送通知接口，抽象 Firebase Cloud Messaging / APNs 等平台推送能力。

---

## §2 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `Plugins/Cloud/IPushPlugin.cs` | `IPushPlugin` | 接口全部定义 |

---

## §5 完整公开 API

```csharp
public interface IPushPlugin : ISDKPlugin
{
    /// 异步获取当前设备的推送令牌。
    /// 令牌尚未就绪时等待平台下发；网络异常时抛对应异常由调用方处理。
    UniTask<PushToken> GetTokenAsync(CancellationToken ct = default);

    /// 订阅或取消订阅指定推送主题。
    /// subscribed=true 订阅，false 取消；后台操作失败时 Plugin 记录日志，不向调用方抛异常。
    void SetTopicSubscribed(string topic, bool subscribed);

    /// 推送令牌刷新事件，在平台颁发新令牌时于主线程触发。
    /// 业务层应监听此事件并将新令牌上报至游戏服务器以维持推送有效性。
    event Action<PushToken> OnTokenRefreshed;
}
```

---

## §11 使用示例

```csharp
if (Nova.SDK.TryGet<IPushPlugin>(out var pushPlugin))
{
    // 获取初始令牌
    try
    {
        var token = await pushPlugin.GetTokenAsync(ct);
        await ServerApi.RegisterPushToken(token.Value, token.Provider, ct);
    }
    catch (Exception e)
    {
        Debug.LogWarning($"获取推送令牌失败：{e.Message}");
    }

    // 监听令牌刷新
    pushPlugin.OnTokenRefreshed += async token =>
    {
        await ServerApi.RegisterPushToken(token.Value, token.Provider, CancellationToken.None);
    };

    // 订阅主题
    pushPlugin.SetTopicSubscribed("news", true);
}
```

---

## §13 关联文档

- [ISDKPlugin.md](../../Definitions/ISDKPlugin.md)
- [../../Definitions/Data.md](../../Definitions/Data.md)
