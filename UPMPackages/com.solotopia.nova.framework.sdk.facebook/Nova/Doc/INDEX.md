# Nova Framework - SDK - Facebook Docs

## Documents

- [`FacebookPlugin.md`](./FacebookPlugin.md): plugin API, lifecycle, login flow, and App Events acquisition tracking.
- [`FacebookUserData.md`](./FacebookUserData.md): user data returned by the plugin.
- [`FacebookSdkUsage.md`](./FacebookSdkUsage.md): Facebook Unity SDK API map retained from the removed official examples.
- [`OpenSourceCompliance.md`](./OpenSourceCompliance.md): package license boundary, third-party dependencies, and public release risks.

## Current State

- Runtime integration lives under `Nova/Scripts/Runtime`.
- Native or third-party integration lives under `Core/FacebookSDK`.
- Demo scene lives under `Assets/Samples/FacebookDemo`.
- Runtime config is injected automatically through `FacebookPlugin.ConfigType`; the build processor writes App ID and Client Token into `FacebookSettings` and registers Facebook Android ProGuard rules for R8/minify builds.
- Successful login publishes `SDKDataKeys.OpenId` and `SDKDataKeys.ThirdLoginProvider` for cross-plugin identity synchronization.
- App Events acquisition tracking uses `IAcquisitionTrackPlugin` and syncs Nova `SDKEventData.UserLogin` user ids to Facebook.

## SDK 初始化与账号规则

本插件由 ConfigMaster 启用。初始化是否等待其他 SDK 由插件的 `ISDKInitializationDependencies` 能力声明决定，`Priority` 不控制启动先后；业务只需目标能力时使用 `Nova.SDK.WaitForPluginAsync<T>()` 并检查 `Ready`。游戏后端登录成功后调用 `Nova.SDK.Login(uid, userProperties)`，框架会在插件就绪后补交最新账号；切换账号再次调用，登出调用 `EndLoginSession()`。新接入 SDK 应按框架 SDK 插件契约 实现。
