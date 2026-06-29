# AppleSignInPlugin

`AppleSignInPlugin` 是 Nova 的 Apple 登录插件，实现 `IAuthPlugin`，返回统一的 `AuthResult`。

```csharp
public sealed partial class AppleSignInPlugin : SDKPluginBase, IAuthPlugin
```

## 常用接口

```csharp
public bool IsLoggedIn { get; }
public AppleSignInUserData CurrentUserData { get; }
public UniTask<AuthResult> LoginAsync(string provider, CancellationToken ct = default);
public UniTask LogoutAsync(CancellationToken ct = default);
```

## 说明

- 当前登录数据只从 `CurrentUserData` 读取。
- `CurrentUserData` 只暴露 Apple 用户 ID 和可选姓名。
- iOS 构建会自动注入 `com.apple.developer.applesignin` entitlement。
- `AuthResult.UserId` 使用 Apple 用户 ID；`AuthResult.Token` 不再填充。
- `LogoutAsync` 只清理本地状态。
