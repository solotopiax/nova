# Third-Party Notices

## Scope

This file describes third-party sources, dependency declarations, and public distribution notes for `com.solotopia.nova.framework.sdk.facebook`. For the package-level license boundary, see [LICENSE.md](./LICENSE.md).

## Vendored Upstream Content

- `Facebook Unity SDK`
  - Upstream project: `https://github.com/facebook/facebook-sdk-for-unity`
  - Imported SDK version: `18.0.0`
  - Package path: `Core/FacebookSDK/**`
  - Bundled upstream license file: `Core/FacebookSDK/LICENSE.txt`
  - This content is upstream Facebook SDK material. It is not Solotopia MIT content and must not be documented as MIT-only.

`Core/Editor/DisableBitcode.cs` was imported with the Facebook SDK package and is treated as upstream SDK material unless replaced by a Solotopia-owned implementation.

## Build-Time Native Dependencies Resolved By EDM4U

The upstream SDK declares native dependencies in `Core/FacebookSDK/Plugins/Editor/Dependencies.xml`.

Android dependencies:

- `com.parse.bolts:bolts-android:1.4.0`
- `com.facebook.android:facebook-core:[18.0.0,19)`
- `com.facebook.android:facebook-applinks:[18.0.0,19)`
- `com.facebook.android:facebook-login:[18.0.0,19)`
- `com.facebook.android:facebook-share:[18.0.0,19)`
- `com.facebook.android:facebook-gamingservices:[18.0.0,19)`

iOS pods:

- `FBSDKCoreKit_Basics ~> 18.0.0`
- `FBSDKCoreKit ~> 18.0.0`
- `FBSDKLoginKit ~> 18.0.0`
- `FBSDKShareKit ~> 18.0.0`
- `FBSDKGamingServicesKit ~> 18.0.0`

## Policy And Trademark Notes

- Facebook SDK usage remains subject to the Facebook platform terms, developer policies, App Review requirements, and applicable privacy requirements.
- Facebook names, logos, and marks should be used only in ways permitted by the current Facebook or Meta brand and trademark guidance.
- Release owners must recheck current policy, trademark, privacy, App Review, and ATT requirements at release time.

## Files Intentionally Excluded

- Official Facebook Unity SDK `Examples` folder.
- Facebook example-only media files such as `meta-logo.png` and `meta.mp4`.
- Private Facebook app credentials.

Before public distribution, separately scan project-level samples, Unity assets, local build settings, and CI secrets because those locations are outside this package notice file.
