# AppleSignInPluginConfig

`AppleSignInPluginConfig` 控制 Apple 登录请求参数。

```csharp
public sealed class AppleSignInPluginConfig : ISDKPluginConfig
```

## 字段

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `RequestFullName` | `true` | 登录时请求姓名；Apple 可能只在首次授权时返回。 |
