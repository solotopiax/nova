# Facebook Package Open Source Compliance

This note summarizes the compliance boundary for `com.solotopia.nova.framework.sdk.facebook`. It is a release checklist aid, not a legal guarantee. Recheck all upstream license, policy, privacy, App Review, ATT, and trademark requirements at release time.

## License Boundary

| Package area | Source | Status for public release |
|---|---|---|
| `Nova/**` | Solotopia / Nova adapter layer | Solotopia / Nova MIT content. Keep `LICENSE.md` with public distribution. |
| Root package docs and metadata authored by Solotopia | Solotopia / Nova packaging | Solotopia / Nova MIT content. Keep notices with public distribution. |
| `Core/FacebookSDK/**` | Upstream Facebook Unity SDK `18.0.0` | Upstream Facebook SDK material. Keep `Core/FacebookSDK/LICENSE.txt` and follow Facebook platform terms, developer policies, and notice requirements. |
| `Core/Editor/DisableBitcode.cs` | Imported with the Facebook SDK package | Treat as upstream SDK material unless replaced by a Nova-owned implementation. |

The whole package must not be described as pure MIT because it includes upstream Facebook SDK material under `Core/FacebookSDK/**`.

## Third-Party Dependency List

Declared in `Core/FacebookSDK/Plugins/Editor/Dependencies.xml`.

Android dependencies resolved by EDM4U:

- `com.parse.bolts:bolts-android:1.4.0`
- `com.facebook.android:facebook-core:[18.0.0,19)`
- `com.facebook.android:facebook-applinks:[18.0.0,19)`
- `com.facebook.android:facebook-login:[18.0.0,19)`
- `com.facebook.android:facebook-share:[18.0.0,19)`
- `com.facebook.android:facebook-gamingservices:[18.0.0,19)`

iOS pods resolved by EDM4U:

- `FBSDKCoreKit_Basics ~> 18.0.0`
- `FBSDKCoreKit ~> 18.0.0`
- `FBSDKLoginKit ~> 18.0.0`
- `FBSDKShareKit ~> 18.0.0`
- `FBSDKGamingServicesKit ~> 18.0.0`

## Public Release Requirements

- Retain `LICENSE.md`, `THIRD_PARTY_NOTICES.md`, and `Core/FacebookSDK/LICENSE.txt`.
- Keep the official Facebook SDK `Examples` folder excluded unless a separate review explicitly approves inclusion.
- Keep Facebook example-only media such as `meta-logo.png` and `meta.mp4` excluded.
- Do not include private Facebook app credentials in package docs, package metadata, sample configs, scenes, or committed Unity assets.
- Recheck Facebook SDK license, developer policy, App Review permission requirements, ATT prompt copy, privacy disclosures, and brand/trademark guidance at release time.
- Recheck `Dependencies.xml` before release and update this document if upstream Android artifacts or iOS pods change.

## Known Risks To Recheck Before Public Release

| Risk | Observed example | Scope note | Required action |
|---|---|---|---|
| Real-looking sample `clientTokens` | `f5c530e84d8456c8000d21ec435417fe` in `Assets/Samples/FacebookDemo/Resources/FacebookSettings.asset` | Outside this Facebook package documentation scope. | Verify whether it is public-safe test data. Remove, rotate, or replace before public release if needed. |
| Public `appIds` | `2704927663220433` in `Assets/Samples/FacebookDemo/Resources/FacebookSettings.asset` | Outside this Facebook package documentation scope. | Confirm whether public disclosure is intended. Replace with placeholder sample data if not. |
| Local Android keystore path | `androidKeystorePath: D:\SoloGames-X\nova\Docs\Programs\Certificates\Android\solotopia.keystore` | The path points outside the package scan scope and should be separately checked/sanitized before public release. | Remove local absolute paths from public samples and verify the referenced keystore is not published. |
| Platform policy drift | Facebook login, friends, sharing, ATT, and profile data policies can change. | Current docs describe current package behavior only. | Recheck policy and App Review requirements at release time. |
