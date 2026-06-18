# IAuthPlugin

**类签名**：`public interface IAuthPlugin : ISDKPlugin`
**命名空间**：`NovaFramework.Runtime`
**源码文件**：`Assets/Framework/Scripts/Runtime/Modules/SDK/Plugins/Account/IAuthPlugin.cs`

第三方账号登录接口，抽象 Google、Apple、Facebook 等平台的 OAuth 登录能力。

---

## §1 文件头

| 属性 | 值 |
|---|---|
| 类签名 | `public interface IAuthPlugin : ISDKPlugin` |
| 命名空间 | `NovaFramework.Runtime` |
| 职责 | 定义第三方登录契约：登录状态查询、异步登录、异步注销 |

---

## §2 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `Plugins/Account/IAuthPlugin.cs` | `IAuthPlugin` | 接口全部定义 |

---

## §3 继承关系

```
ISDKPlugin
  └── IAuthPlugin
```

---

## §5 完整公开 API

```csharp
public interface IAuthPlugin : ISDKPlugin
{
    // === 状态查询 ===

    /// 当前用户是否已登录。主线程只读；登录成功后为 true，注销后为 false。
    bool IsLoggedIn { get; }

    /// 当前已登录用户的平台用户 ID。未登录时返回 null 或空字符串。
    string CurrentUserId { get; }

    // === 生命周期 ===

    /// 异步发起第三方登录流程。
    /// provider：登录平台标识（如 "google"、"apple"、"facebook"），大小写不敏感。
    /// 用户取消时：AuthResult.Success=false 且 ErrorMessage 含 "UserCancelled" 语义标记。
    UniTask<AuthResult> LoginAsync(string provider, CancellationToken ct = default);

    /// 异步注销当前登录用户，清除本地令牌与会话。
    /// 未登录时调用为幂等操作，不抛异常。
    UniTask LogoutAsync(CancellationToken ct = default);
}
```

---

## §11 使用示例

```csharp
if (Nova.SDK.TryGet<IAuthPlugin>(out var authPlugin))
{
    if (!authPlugin.IsLoggedIn)
    {
        var result = await authPlugin.LoginAsync("google", ct);
        if (result.Success)
        {
            // 登录成功后设置埋点用户 ID
            foreach (var tracker in Nova.SDK.GetAll<ITrackPlugin>())
                tracker.SetUserId(result.UserId);
        }
        else if (result.ErrorMessage?.Contains("UserCancelled") == true)
        {
            Debug.Log("用户取消登录");
        }
    }
}
```

---

## §13 关联文档

- [ISDKPlugin.md](../../Definitions/ISDKPlugin.md) — 根接口
- [../../Definitions/Data.md](../../Definitions/Data.md) — AuthResult 数据类
