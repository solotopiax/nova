# AppleSignInUserData

`AppleSignInUserData` 保存 Apple 登录回调中客户端需要的数据，并可转换为 Nova `AuthResult`。

```csharp
public AuthResult ToAuthResult(string provider);
```

## 说明

- `AppleSignInUserData` 只暴露 `UserId` 和可选 `FullName`。
- `ToAuthResult` 只要求 `UserId` 非空。
- `AuthResult.UserId` 使用 Apple 用户 ID；`AuthResult.Token` 不再填充。
