# GoogleSignInPlugin

`GoogleSignInPlugin` is Nova's Android Google Sign-In authentication plugin.
It implements `IAuthPlugin`, returns Nova `AuthResult` values, and stores the
most recent native user payload in `CurrentUserData`.

## Public API

```csharp
public sealed partial class GoogleSignInPlugin : SDKPluginBase, IAuthPlugin
{
    public override string Name { get; }
    public override int Priority { get; }
    public bool IsLoggedIn { get; }
    public GoogleSignInUserData CurrentUserData { get; }
    public UniTask<AuthResult> LoginAsync(string provider, CancellationToken ct = default);
    public UniTask LogoutAsync(CancellationToken ct = default);
}
```

| Member | Notes |
|---|---|
| `Name => "Google"` | SDK plugin identifier. |
| `Priority => 60` | Stable notification/reverse disposal order; initialization follows declared dependencies. |
| `IsLoggedIn` | `true` only when both `UserId` and `IdToken` are present. |
| `CurrentUserData` | Latest login payload as `GoogleSignInUserData`. |
| `LoginAsync` | Starts the Android login flow and returns `AuthResult`. |
| `LogoutAsync` | Clears Android credential state and current user data. |

## Usage

Enable `GoogleSignInPluginConfig` in ConfigMaster. `GoogleSignInPlugin` declares
the config through `ConfigType`, so SDKManager injects it automatically during
initialization. Wait for initialization to finish, then get the strongly typed plugin.

```csharp
GoogleSignInPlugin plugin = Nova.SDK.Get<GoogleSignInPlugin>();
AuthResult result = await plugin.LoginAsync("Google");
if (result.Success)
{
    string userId = plugin.CurrentUserData?.UserId;
    string idToken = plugin.CurrentUserData?.IdToken;
}
```

After a successful login, `LoginAsync` updates `CurrentUserData`.
`AuthResult.Token` is the Google ID Token for this login result. Callers should
not cache stale ID Tokens for long periods. When server verification is needed,
send the current login result token to the server-side authentication flow.

Successful login also publishes `SDKDataKeys.OpenId` with `UserId` and
`SDKDataKeys.ThirdLoginProvider` with the provider name. Analytics plugins can consume
these slots without taking a direct dependency on the Google Sign-In package.

## Config

Runtime configuration is documented in
[GoogleSignInPluginConfig.md](./GoogleSignInPluginConfig.md). Key fields:

- `ClientId`
- `RequestEmail`
- `FilterByAuthorizedAccounts`
- `AutoSelectEnabled`
- `AutoRestoreOnInitialize`

When `AutoRestoreOnInitialize` is `true`, initialization attempts to restore a
previous Google sign-in. Restore failures are logged and do not prevent callers
from explicitly calling `LoginAsync`.

## Token Fields

Google Sign-In maps only the Google ID Token to Nova `AuthResult.Token`.
`GoogleSignInUserData` also keeps `UserId`, `Email`, `DisplayName`, and
`AvatarUrl`. These fields can be empty or unavailable depending on Android
platform behavior, account state, consent, and profile data.

## Android Credential Manager Flow

The Android bridge uses Credential Manager and Google ID Token credentials:

- `LoginAsync` requests a Google ID Token through Credential Manager.
- `FilterByAuthorizedAccounts == true` first requests authorized accounts; if
  that request fails, the bridge retries without the authorized-account filter.
- `AutoSelectEnabled` is passed to Credential Manager auto-select behavior.
- `LogoutAsync` clears platform credential state.

Android builds need a valid `ClientId`. Before release, recheck the current
Google Play, Google Identity Services, Credential Manager, privacy disclosure,
and login text or button requirements.

Android 桥先发出原生登录/恢复调用，再用 `AttachExternalCancellation` 绑定等待。当前没有内置超时，取消只结束托管等待，不撤销已启动的 Credential Manager 操作；桥发起调用前也不自动切换主线程。业务应在 Unity 主线程发起，按需传入取消/超时令牌，并处理平台 UI 的迟到结果。非 Android Player 的登录与恢复不受支持；登出在这些平台直接完成。

`LoginAsync` 将服务调用中的普通异常转换为失败结果，但继续抛取消异常；服务返回后的结果映射和状态写入不在该捕获范围内。自动恢复可能先发布非空 UserId，而当前 `IsLoggedIn` 仍因缺少 IdToken 为 false；数据槽已有 OpenID 不能单独证明登录凭据完整。`CurrentUserData` 为缓存引用，读取不刷新原厂状态。

Android 登出只提交原生 `signOut`，不等待原厂退出确认，也不采用传入的取消令牌；JNI 调用失败会继续抛出，后续本地清理也不会执行。本地 `SetLoginState(null)` 不主动清除已发布的 OpenID/Provider 数据槽；完整 SDK 释放由基类另行处理。原生桥返回结果后会切回主线程，发起调用时取得的 Activity 包装引用当前没有配对 Dispose，不能从桥类的局部 using 推断全部 JNI 引用都已释放。

Android preprocessing registers ProGuard rules through `NovaBuildContext` to
keep the package's custom `com.solotopia.nova.googlesignin` Java bridge when
R8/minify is enabled.

ProGuard 规则先登记，再读取 Google 配置；无配置时也可能已有规则进入上下文。ClientId 为空当前只记录 Warning，不主动令构建失败；运行时 Android 登录/恢复才拒绝空 ClientId，空白字符串没有额外校验。构建日志不能代替 Web Client ID、原生依赖解析和真机认证验收。

## Data Handling Expectations

- Treat `CurrentUserData` as the package's current login-state payload.
- Treat `IdToken`, `Email`, `DisplayName`, and `AvatarUrl` as nullable or
  revocable data.
- Do not make server-trust decisions from client-side token parsing alone.
- Handle cancellation, platform login failure, the user closing platform UI,
  network failure, and empty returned fields.
- Do not publish OAuth client secrets, private signing material, provisioning
  profiles, or environment-specific service files with sample or package docs.

## GoogleSigninDemo

示例的 Google 认证、游戏服登录和 GameBind 绑定是三个阶段：平台认证成功只缓存平台 ID；绑定时按需登录游戏账号，再请求绑定，冲突只提示后续 QueryConflict/Resolve，不自动选账号或覆盖存档。GameLogin 自身已通知 SDK，不应由页面重复调用 `Nova.SDK.Login`。

页面的认证等待不随 UIView 关闭取消，关闭只清本页缓存与显示，迟到任务仍可能更新旧页面。平台登出不清除 GameLogin 的 UID，也不撤销 GameBind 绑定；当前用户数据显示不能当作后台账号归属证明。

## Related

- [GoogleSignInPluginConfig.md](./GoogleSignInPluginConfig.md)
- [GoogleSignInUserData.md](./GoogleSignInUserData.md)
- [OpenSourceCompliance.md](./OpenSourceCompliance.md)

导入路径重写、旧版本目录清理、Sample 基类与启动流程的共同边界，见已安装 Framework 的 `Docs/Onboarding/PROJECT_STRUCTURE.md` 中“Sample 的导入、辅助工具与页面边界”；本页只补充当前示例的差异。
