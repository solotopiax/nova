# IAuthPlugin

**类签名**：`public interface IAuthPlugin : ISDKPlugin`
**命名空间**：`NovaFramework.Runtime`
**源码文件**：`Assets/Framework/Scripts/Runtime/Modules/SDK/Plugins/Account/IAuthPlugin.cs`

第三方登录接口，抽象 Google、Apple、Facebook 等平台的登录能力。

## 公开 API

```csharp
public interface IAuthPlugin : ISDKPlugin
{
    bool IsLoggedIn { get; }
    UniTask<AuthResult> LoginAsync(string provider, CancellationToken ct = default);
    UniTask LogoutAsync(CancellationToken ct = default);
}
```

当前登录数据由具体插件暴露，例如 `GoogleSignInPlugin.CurrentUserData`。

`provider`、ct 与返回的 `AuthResult` 属于第三方登录流程；`IsLoggedIn` 只反映实现的插件状态，不能替代游戏服验证。业务应在服务端确认 UID 后再调用 `Nova.SDK.Login` 或 `RegisterAndLogin` 分发会话。Logout 的平台动作由实现定义，不代表游戏服注销或删除账号；`EndLoginSession` 也不是原生 Logout 的替代入口。

## 使用示例

```csharp
if (Nova.SDK.TryGet<IAuthPlugin>(out var authPlugin) && !authPlugin.IsLoggedIn)
{
    AuthResult result = await authPlugin.LoginAsync("google", ct);
    if (result.Success)
    {
        foreach (var tracker in Nova.SDK.GetAll<ITrackPlugin>())
        {
            tracker.SetUserId(result.UserId);
        }
    }
}
```

## 相关文档

- [ISDKPlugin.md](../../Definitions/ISDKPlugin.md)
- [../../Definitions/Data.md](../../Definitions/Data.md)
