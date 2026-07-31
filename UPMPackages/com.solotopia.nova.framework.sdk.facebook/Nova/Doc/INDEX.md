# Nova Framework - SDK - Facebook Docs

## Documents

- [`FacebookPlugin.md`](./FacebookPlugin.md): plugin API, lifecycle, and login flow.
- [`FacebookUserData.md`](./FacebookUserData.md): user data returned by the plugin.
- [`FacebookSdkUsage.md`](./FacebookSdkUsage.md): Facebook Unity SDK API map retained from the removed official examples.
- [`OpenSourceCompliance.md`](./OpenSourceCompliance.md): package license boundary, third-party dependencies, and public release risks.

## Current State

- Runtime integration lives under `Nova/Scripts/Runtime`.
- Native or third-party integration lives under `Core/FacebookSDK`.
- Demo scene lives under `Assets/Samples/FacebookDemo`.
- Runtime config is injected automatically through `FacebookPlugin.ConfigType`; the build processor writes App ID and Client Token into `FacebookSettings`.
- Successful login publishes `SDKDataKeys.OpenId` and `SDKDataKeys.ThirdPlatform` for cross-plugin identity synchronization.
