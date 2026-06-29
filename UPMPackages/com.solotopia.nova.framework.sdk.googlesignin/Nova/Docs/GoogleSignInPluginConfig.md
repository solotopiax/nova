# GoogleSignInPluginConfig

`GoogleSignInPluginConfig` 保存安卓谷歌登录运行时配置：谷歌网页客户端编号和登录行为开关。

## Public API

```csharp
public sealed class GoogleSignInPluginConfig : ISDKPluginConfig
{
    public string ClientId { get; }
    public bool RequestEmail { get; }
    public bool FilterByAuthorizedAccounts { get; }
    public bool AutoSelectEnabled { get; }
    public bool AutoRestoreOnInitialize { get; }
    public string DisplayName { get; }
}
```

## Config Fields

| Field | Notes |
|---|---|
| `ClientId` | 谷歌网页客户端编号，用于身份令牌请求。 |
| `RequestEmail` | 登录时请求邮箱。 |
| `FilterByAuthorizedAccounts` | 优先使用已授权账号。 |
| `AutoSelectEnabled` | 允许系统自动选择账号。 |
| `AutoRestoreOnInitialize` | 初始化时恢复上次登录。 |
| `DisplayName => "Google"` | 配置界面显示名。 |

## Usage

```csharp
var config = new GoogleSignInPluginConfig(
    clientId: "xxx.apps.googleusercontent.com",
    requestEmail: true,
    filterByAuthorizedAccounts: true,
    autoSelectEnabled: true,
    autoRestoreOnInitialize: false);
```

在 Nova SDK 初始化前注入配置。初始化后修改配置，不会自动重建原生登录状态。

## Android Credential Manager Flow

安卓使用 `ClientId` 构建谷歌身份令牌请求。
`FilterByAuthorizedAccounts == true` 时先请求已授权账号；平台返回错误后，桥接层会去掉账号过滤并重试一次。
`AutoSelectEnabled` 传给 Credential Manager，用于控制自动选择资格。

## Data Handling Expectations

`ClientId` 是平台配置标识，不是客户端密钥。除非明确作为发版决策，不要在正式包复用测试项目标识。
不要随包或示例配置发布 OAuth 客户端密钥、私钥、签名证书、描述文件或环境专属服务文件。
发版前重新核对谷歌和安卓平台要求。

## Related

- [GoogleSignInPlugin.md](./GoogleSignInPlugin.md)
- [OpenSourceCompliance.md](./OpenSourceCompliance.md)
