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

### 调试登录与接口调用

运行时 Debug 的 Console 标签中选择 `SDK → Facebook`，可查看初始化、登录/好友授权、退出、Graph 请求、资料、头像缓存与下载、好友和分享的关键过程。Graph GET 使用本地 `request` 编号对应开始与返回；它只用于诊断，不保证外部操作隔离。日常技术诊断（包括 App Events 用户同步与事件调用）统一使用 Debug，失败或缺失数据使用 Warning；不使用业务 Info 或触发 Crash。

不需要新增配置。Editor 保留这些日志；Player 使用框架现有 `Nova/Enable Logs/Enable Debug And Above Logs` 菜单启用 Debug 及以上等级，或通过已有构建配置保留 `ENABLE_LOG` / `ENABLE_DEBUG_AND_ABOVE_LOG`。精确等级宏 `ENABLE_DEBUG_LOG` 只保留 Debug，本身不会同时开启 Warning。只开启 Warning 及以上时看不到正常调用过程；Console 还需允许显示对应等级。标签筛选不能开启已被编译裁剪的日志。

长请求路径、错误文本和事件名在日志调用处简单截断，最多显示 240 字符（含 `...`）。接口返回打印字段数、是否包含纹理及成功/失败状态，不展开完整响应或事件参数值。直接沿用框架现有日志开关、等级和 Console 筛选，不增加独立诊断或脱敏工具。

登录请求已包含 `public_profile`，回调记录当前令牌是否包含该权限；不会为了日志追加重复授权，没有再次弹窗也不足以判断权限是否缺失。登录成功与资料/头像可用分别记录，Graph 错误和头像空结果有单独提示。昵称仍由 `Profile.GetCurrentProfileAsync` 读取，不自动加入 `CurrentUserData`。日志不改变原有返回值、权限、取消或异常传播契约；平台真实权限与头像昵称是否可用仍需真机核验。

按以下顺序查看日志即可定位当前停在哪一步：

| 现象 | 查看内容与下一步 |
|---|---|
| 点击登录没有日志 | 确认使用包含 Facebook 标签和日志的 Framework / Facebook 包；检查 Player 日志宏、Console 等级和标签筛选。Demo 的页面反馈区不自动转发到 Console，插件日志是独立的调用记录。 |
| 有“登录开始”，没有“登录回调” | 说明调用已进入原厂授权流程，但尚未观察到回调；结合平台登录窗口及设备日志继续定位，不以此断言登录失败。登录没有默认超时，取消本地等待也不撤销原厂操作。 |
| 登录回调失败或取消 | 查看同组 Warning 的 `error` 摘要，以及回调的“用户取消”“含用户ID”“含Token”和权限布尔值。 |
| 登录成功但昵称为空 | 确认业务或 Demo 已调用 `Profile.GetCurrentProfileAsync()`；查看 `me?fields=id,name,picture` 对应 Graph 请求的结果和“资料读取完成”的“含昵称”状态。 |
| 头像没有显示 | 先查自动下载开关与“头像缓存可用”，再查缓存命中、Graph 图片结果、文件写入及纹理解码日志。自动下载关闭时，显式资料读取仍可能下载头像；登录成功不表示图片已取得。 |
| 好友或分享没有达到预期 | 好友需单独申请 `user_friends`，空列表不一定是接口错误；分享成功仅表示 SDK 回调未报错且未取消。App Event 日志只证明已交给 SDK，不证明后台已接收。 |

240 字符限制针对单个动态文本，不是整条日志总长度；截断只影响日志展示，接口收到的原参数、原错误返回及业务结果保持原样。

- `LoginAsync` and `EnsureFriendsPermissionAsync` return `AuthResult.Success == false` on failure and provide details through `ErrorMessage`.
- `Share.ShareLinkAsync` returns `FacebookShareResult.Success == false` on failure and provides details through `ErrorMessage`.
- Profile, avatar, and friend APIs are asynchronous. Callers must handle cancellation, network failure, missing permissions, and empty data.
- Facebook plugin initialization failure should affect Facebook plugin availability only and should not block unrelated SDK plugins.

导入路径重写、旧版本目录清理、Sample 基类与启动流程的共同边界，见已安装 Framework 的 [Docs/Onboarding/PROJECT_STRUCTURE.md](../../README.md#跨包文档定位)（`com.solotopia.nova.framework` 包内 `Docs/Onboarding/PROJECT_STRUCTURE.md`） 中“Sample 的导入、辅助工具与页面边界”；本页只补充当前示例的差异。
