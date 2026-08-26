# FacebookPlugin

`FacebookPlugin` is Nova's Facebook login and acquisition tracking plugin. It implements `IAuthPlugin` and `IAcquisitionTrackPlugin`, and wraps Facebook Unity SDK initialization, login, profile/avatar access, friend queries, link sharing, and App Events.

## Public API

| Member | Notes |
|---|---|
| `Name => "Facebook"` | Plugin id used by `Nova.SDK.Get<FacebookPlugin>()` and logging. |
| `Priority => 40` | Initialization priority. |
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

Before Android or iOS builds, `FacebookPluginBuildProcessor` copies `FacebookAppId` and `FacebookClientToken` into the Facebook SDK's `FacebookSettings`. Android builds run the Facebook SDK's official `ManifestMod.GenerateManifest()` during Nova's AfterNova preprocess phase, after Nova has materialized its own AndroidManifest.xml changes on disk.

## Login Result And Current User

`LoginAsync` returns a failed `AuthResult` when the SDK is not initialized or Facebook login fails. Failure details are written to `ErrorMessage`.

After successful login, the plugin updates `CurrentUserData` with the current Facebook user data:

- `UserId`
- `AccessToken`
- `AvatarPath`

It also publishes `SDKDataKeys.OpenId` with `UserId` and `SDKDataKeys.ThirdLoginProvider` with the provider name, allowing analytics plugins such as TGA to consume the login identity without a direct package dependency.

During initialization, the plugin also subscribes to `SDKEventData.UserLogin`. When the Nova business user logs in, `OnUserLogin` calls `SetUserId(login.UserId)` so Facebook App Events can associate acquisition events with the business user id.

`AuthResult.Token` maps to the Facebook Access Token. Callers should use `CurrentUserData` for current login state and should not cache old tokens or avatar paths beyond the active session contract.

## Profile And Avatar

`Profile.GetCurrentProfileAsync` reads the current profile through the Graph API. `Profile.DownloadAvatarAsync`, `Profile.GetAvatarTextureAsync`, and `Profile.GetAvatarPathAsync` use `CurrentUserData.UserId` when `facebookId` is empty.

Avatar cache path:

```text
Application.persistentDataPath/Nova/Facebook/Avatars/{facebookId}.png
```

If `AutoDownloadAvatarOnLogin` is `true`, the plugin downloads the current user's avatar after login and writes the path back to `CurrentUserData.AvatarPath`.

## Friends

Friend data is read through this Graph request:

```text
me/friends?fields=id,name,picture
```

`Friends.GetFriendsAsync` returns friends visible to the current app and account. `Friends.GetFriendsWithAvatarsAsync` adds avatar downloads on top of the friend data. Facebook may return no friends depending on account relationships, permissions, App Review state, and app configuration. Release builds must recheck current Facebook platform policy before relying on friend behavior.

## Sharing

`Share.ShareLinkAsync(FacebookShareRequest request, CancellationToken ct = default)` wraps `FB.ShareLink` and returns `FacebookShareResult`.

`FacebookShareRequest` must include a shareable link. If the link is empty, the service returns a failed result and writes `ErrorMessage`. For a broader Facebook Unity SDK API map extracted from the removed examples, see [FacebookSdkUsage.md](./FacebookSdkUsage.md).

## Acquisition Tracking

`FacebookPlugin` implements `IAcquisitionTrackPlugin` for 投放 / 买量转化事件. `TrackEvent` forwards events to Facebook App Events through `FB.LogAppEvent`; empty event names are ignored. Parameter dictionaries may be `null`; empty keys and `null` values are skipped, primitive values are preserved, and unsupported objects are converted with `ToString()`.

This interface intentionally does not expose `SetUserProperty`, because Facebook App Events integration in this package only needs user id sync and event logging.

## ATT-Limited Data Handling

On iOS, ATT status and Facebook platform limits can reduce returned profile data. In limited data cases, callers may receive only a user id. Treat profile fields, avatars, and friend data as optional. The plugin exposes what the current SDK returns; it does not infer identity and does not fill missing fields with fixed placeholder data.

Before release, recheck Facebook platform policy, App Review permission requirements, ATT prompt text, and privacy disclosures. This document describes the current Nova package behavior and does not replace external platform compliance review.

## Error Handling Expectations

- `LoginAsync` and `EnsureFriendsPermissionAsync` return `AuthResult.Success == false` on failure and provide details through `ErrorMessage`.
- `Share.ShareLinkAsync` returns `FacebookShareResult.Success == false` on failure and provides details through `ErrorMessage`.
- Profile, avatar, and friend APIs are asynchronous. Callers must handle cancellation, network failure, missing permissions, and empty data.
- Facebook plugin initialization failure should affect Facebook plugin availability only and should not block unrelated SDK plugins.
