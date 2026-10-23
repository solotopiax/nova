# FacebookPlugin

`FacebookPlugin` is Nova's Facebook login and acquisition tracking plugin. It implements `IAuthPlugin` and `IAcquisitionTrackPlugin`, and wraps Facebook Unity SDK initialization, login, profile/avatar access, friend queries, link sharing, and App Events.

## Public API

| Member | Notes |
|---|---|
| `Name => "Facebook"` | Plugin id used by `Nova.SDK.Get<FacebookPlugin>()` and logging. |
| `Priority => 40` | Stable notification/reverse disposal order; initialization follows declared dependencies. |
| `IsLoggedIn` | `true` when the plugin has both `UserId` and `AccessToken`. |
| `CurrentUserData` | Current login data as `FacebookUserData`. |
| `Profile` | Profile and avatar service, type `FacebookProfileService`. |
| `Friends` | Friend service, type `FacebookFriendsService`. |
| `Share` | Link sharing service, type `FacebookShareService`. |
| `LoginAsync(string provider, CancellationToken ct = default)` | Starts Facebook login and returns Nova `AuthResult`. |
| `LogoutAsync(CancellationToken ct = default)` | Calls Facebook logout and clears local current-user state. |
| `EnsureFriendsPermissionAsync(CancellationToken ct = default)` | Requests `user_friends` permission for platform-visible friend data. |
| `SetUserId(string userId)` | Syncs the Nova business user id to Facebook App Events through `FB.Mobile.UserID`. |
| `TrackEvent(TrackEvent evt)` | Logs a Facebook App Event from the common Nova event payload. |
| `TrackEvent(string eventName, Dictionary<string, object> parameters)` | Logs a Facebook App Event through `FB.LogAppEvent`. |

## Config

`FacebookPluginConfig` stores Facebook runtime configuration. `FacebookPlugin` declares it through `ConfigType`; after the config is enabled in ConfigMaster, SDKManager resolves and injects it automatically during initialization. Game code should not call a separate manual config API. Do not store private App ID or Client Token values in sample scenes or temporary scripts intended for public release.

| Field | Notes |
|---|---|
| `FacebookAppId` | Facebook App ID. |
| `FacebookClientToken` | Facebook Client Token. |
| `AutoDownloadAvatarOnLogin` | Whether to download the current user's avatar after login. |
| `AvatarSize` | Avatar request size. Defaults to `FacebookPluginConfig.DefaultAvatarSize`. |
| `DisplayName => "Facebook"` | Display name used by configuration UI. |

The default login request asks only for `public_profile`. `email` is not requested by default. Friend lists require an additional `user_friends` request through `EnsureFriendsPermissionAsync`.

Before Android or iOS builds, `FacebookPluginBuildProcessor` copies `FacebookAppId` and `FacebookClientToken` into the Facebook SDK's `FacebookSettings`. Android preprocessing also registers Facebook ProGuard rules through `NovaBuildContext` so the Unity Java bridge survives R8/minify. Android builds run the Facebook SDK's official `ManifestMod.GenerateManifest()` during Nova's AfterNova preprocess phase, after Nova has materialized its own AndroidManifest.xml changes on disk.

## Login Result And Current User

`LoginAsync` returns a failed `AuthResult` when the SDK is not initialized or Facebook login fails. Failure details are written to `ErrorMessage`.

After successful login, the plugin updates `CurrentUserData` with the current Facebook user data:

- `UserId`
- `AccessToken`
- `AvatarPath`

It also publishes `SDKDataKeys.OpenId` with `UserId` and `SDKDataKeys.ThirdLoginProvider` with the provider name, allowing analytics plugins such as TGA to consume the login identity without a direct package dependency.

SDKManager delivers the current business account through `ISDKLoginReceiver.OnSDKLogin`, which calls `SetUserId(userId)`. A plugin that becomes Ready after login receives the current account through the same Manager contract; the plugin does not subscribe to `SDKEventData.UserLogin` itself.

`AuthResult.Token` maps to the Facebook Access Token. Callers should use `CurrentUserData` for current login state and should not cache old tokens or avatar paths beyond the active session contract.

认证、初始化和分享等待没有默认超时，取消只停止本地等待，不撤销已交给 Facebook 的操作；非结果型异常仍可能向上抛出。初始化的显示回调直接把 `Time.timeScale` 设为 `1` 或 `0`，不会保存业务原速度。`OnDisposeAsync` 只清本地引用和登录缓存，不执行原厂退出；`SetLoginState(null)` 也不主动清共享数据槽，完整 SDK 基类释放是另一条链。

## Profile And Avatar

`Profile.GetCurrentProfileAsync` reads the current profile through the Graph API. `Profile.DownloadAvatarAsync`, `Profile.GetAvatarTextureAsync`, and `Profile.GetAvatarPathAsync` use `CurrentUserData.UserId` when `facebookId` is empty.

Avatar cache path:

```text
Application.persistentDataPath/Nova/Facebook/Avatars/{facebookId}.png
```

If `AutoDownloadAvatarOnLogin` is `true`, the plugin downloads the current user's avatar after login and writes the path back to `CurrentUserData.AvatarPath`.

`GetAvatarTextureAsync` 成功返回的新 `Texture2D` 由调用方持有并在不再使用时销毁，不由插件自动管理。当前实现没有保证切回主线程，调用方须在 Unity 主线程使用纹理 API；`LoadImage` 返回 false 时方法返回 null，但这一失败路径没有销毁刚创建的纹理。头像文件和纹理加载失败应分别处理，不能把登录成功当作头像可用。

`GetAvatarPathAsync` 仅预测路径，不检查文件存在或下载。文件缓存命中时不校验内容、期限或预取消令牌；传入的用户 ID 没有安全路径段检查，也未对 Graph 路径转义，不能接受不可信路径片段。自动头像下载会吞并记录包括取消在内的异常，等待期间没有核对账号代际，旧账号头像路径可能附到新缓存用户。当前实现也不销毁原厂返回的头像纹理，业务须区分磁盘文件和 Unity 纹理的所有权。

## Friends

Friend data is read through this Graph request:

```text
me/friends?fields=id,name,picture
```

`Friends.GetFriendsAsync` returns friends visible to the current app and account. `Friends.GetFriendsWithAvatarsAsync` adds avatar downloads on top of the friend data. Facebook may return no friends depending on account relationships, permissions, App Review state, and app configuration. Release builds must recheck current Facebook platform policy before relying on friend behavior.

好友读取不自动申请权限或翻页，错误/缺失结构可能表现为空列表；带头像查询按顺序执行，首个异常会停止后续项，已写的缓存不回滚。分享的 Success 仅表示厂商回调未报错且未取消，不证明接收者收到，更不能据此自动发奖。

## Sharing

`Share.ShareLinkAsync(FacebookShareRequest request, CancellationToken ct = default)` wraps `FB.ShareLink` and returns `FacebookShareResult`.

`FacebookShareRequest` must include a shareable link. If the link is empty, the service returns a failed result and writes `ErrorMessage`. For a broader Facebook Unity SDK API map extracted from the removed examples, see [FacebookSdkUsage.md](./FacebookSdkUsage.md).

## Acquisition Tracking

`FacebookPlugin` implements `IAcquisitionTrackPlugin` for 投放 / 买量转化事件. `TrackEvent` forwards events to Facebook App Events through `FB.LogAppEvent`; empty event names are ignored. Parameter dictionaries may be `null`; empty keys and `null` values are skipped, primitive values are preserved, and unsupported objects are converted with `ToString()`.

This interface intentionally does not expose `SetUserProperty`, because Facebook App Events integration in this package only needs user id sync and event logging.

## ATT-Limited Data Handling

On iOS, ATT status and Facebook platform limits can reduce returned profile data. In limited data cases, callers may receive only a user id. Treat profile fields, avatars, and friend data as optional. The plugin exposes what the current SDK returns; it does not infer identity and does not fill missing fields with fixed placeholder data.

Before release, recheck Facebook platform policy, App Review permission requirements, ATT prompt text, and privacy disclosures. This document describes the current Nova package behavior and does not replace external platform compliance review.

## FacebookDemo

示例的“解绑”回调实际只退出 Facebook 平台会话、清除页面缓存 ID 和资料展示，不调用游戏服解绑，也不清 GameLogin 的 UID。平台登录、游戏账号登录和 GameBind 绑定分别执行；绑定冲突只提示继续查询和裁决，不自动选择保留哪份进度。

关闭页面不取消已有认证、资料或好友请求，也不销毁页面缓存的头像纹理；这与 `RefreshProfile` 替换旧纹理时的清理不同。事件参数预览只手工转义反斜杠和双引号，值全部变成字符串，不能作为任意 JSON 的序列化器或真实埋点载荷。界面显示“发送成功”只表示本地调用完成，后台接收须单独核验。

## Error Handling Expectations

- `LoginAsync` and `EnsureFriendsPermissionAsync` return `AuthResult.Success == false` on failure and provide details through `ErrorMessage`.
- `Share.ShareLinkAsync` returns `FacebookShareResult.Success == false` on failure and provides details through `ErrorMessage`.
- Profile, avatar, and friend APIs are asynchronous. Callers must handle cancellation, network failure, missing permissions, and empty data.
- Facebook plugin initialization failure should affect Facebook plugin availability only and should not block unrelated SDK plugins.

导入路径重写、旧版本目录清理、Sample 基类与启动流程的共同边界，见已安装 Framework 的 `Docs/Onboarding/PROJECT_STRUCTURE.md` 中“Sample 的导入、辅助工具与页面边界”；本页只补充当前示例的差异。
