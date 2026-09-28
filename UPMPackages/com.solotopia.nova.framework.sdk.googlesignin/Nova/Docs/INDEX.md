# Nova Framework - SDK - Google Sign-In Docs

## Documents

- [`GoogleSignInPlugin.md`](./GoogleSignInPlugin.md): plugin API, lifecycle, and login flow.
- [`GoogleSignInUserData.md`](./GoogleSignInUserData.md): user data returned by the plugin.
- [`GoogleSignInPluginConfig.md`](./GoogleSignInPluginConfig.md): runtime configuration fields and platform setup notes.
- [`OpenSourceCompliance.md`](./OpenSourceCompliance.md): package license boundary, third-party dependencies, and public release risks.

## Current State

- Runtime integration lives under `Nova/Scripts/Runtime`.
- Native or third-party integration lives under `Core/Plugins`.
- Demo scene lives under `Assets/Samples/GoogleSigninDemo`.
- Runtime config is injected automatically through `GoogleSignInPlugin.ConfigType`; Android preprocessing registers ProGuard rules for the package's custom Java bridge.
- Successful login publishes `SDKDataKeys.OpenId` and `SDKDataKeys.ThirdLoginProvider` for cross-plugin identity synchronization.
## Nova SDK 生命周期

GoogleSignIn 只提供第三方身份验证，不需要游戏 UID，因此不实现登录资料接收接口。它与无依赖插件并发初始化；业务需在插件状态为 `Ready` 后使用。取得 Google 身份后仍须由游戏后端确认登录，再调用 `Nova.SDK.Login(gameUid, userProperties)` 同步其他 SDK。
