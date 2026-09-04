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
