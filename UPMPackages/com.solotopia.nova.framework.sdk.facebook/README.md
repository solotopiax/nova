# Nova Framework - SDK - Facebook

Package name: `com.solotopia.nova.framework.sdk.facebook`

This package wraps the official Facebook Unity SDK `18.0.0` in Nova's UPM package layout and exposes `FacebookPlugin`.

## What This Package Contains

- `Nova/**`: Solotopia / Nova-owned adapter layer, including `FacebookPlugin`, configuration, runtime data models, editor integration, and package documentation.
- `Core/FacebookSDK/**`: vendored upstream Facebook Unity SDK material.
- `Core/Editor/DisableBitcode.cs`: imported with the Facebook SDK package and treated as upstream SDK material unless replaced by a Nova-owned implementation.
- `Core/FacebookSDK/Plugins/Editor/Dependencies.xml`: EDM4U dependency declarations for Android artifacts and iOS CocoaPods used by the upstream SDK.

## What This Package Does Not Contain

- Private Facebook app credentials.
- Facebook example-only media such as `meta-logo.png` and `meta.mp4`.
- The official Facebook Unity SDK `Examples` folder.

The official examples were intentionally excluded because they are not part of the Nova adapter layer and may compile sample-only scripts into a project by default.

## Login Behavior

- `FacebookPlugin` implements Nova `IAuthPlugin` login/logout behavior.
- The default login permission request is `public_profile`.
- `email` is not requested by default.
- Friend data requires an additional `user_friends` permission flow through `EnsureFriendsPermissionAsync`.
- Link sharing is handled through the Facebook SDK share dialog wrapper.

## iOS ATT And Limited Profile Data

On iOS, ATT status and Facebook platform policy can limit the profile data returned by the SDK. In limited data cases, the SDK may return only a user id. Callers must treat profile fields, avatar data, and friend data as optional and nullable.

Release builds must recheck the current Facebook platform policy, App Review permission requirements, ATT prompt text, and privacy disclosures at release time. This package documentation describes the current package behavior and does not replace release-time compliance review.

## Open Source And Redistribution Status

This package is not a pure MIT package.

- `Nova/**` and Solotopia-authored package documentation/metadata are the Nova adapter layer and are provided under the Solotopia / Nova MIT license in `LICENSE.md`.
- `Core/FacebookSDK/**` is upstream Facebook Unity SDK material and remains subject to the upstream Facebook SDK license, platform terms, developer policies, and third-party notices.
- Public distribution must retain `LICENSE.md`, `THIRD_PARTY_NOTICES.md`, and `Core/FacebookSDK/LICENSE.txt`.
- Do not describe the full package as MIT-only or as free of Facebook platform obligations.

See `Nova/Doc/OpenSourceCompliance.md` for the package compliance summary and release checklist details.

## Release Checklist

- Confirm no private Facebook app credentials are stored in this package.
- Confirm the official Facebook SDK `Examples` folder and example-only media are not included.
- Confirm `LICENSE.md`, `THIRD_PARTY_NOTICES.md`, and `Core/FacebookSDK/LICENSE.txt` are present.
- Recheck Facebook SDK license, developer policies, platform review requirements, ATT guidance, and trademark usage at release time.
- Recheck EDM4U Android and iOS dependency declarations against `Core/FacebookSDK/Plugins/Editor/Dependencies.xml`.
- Separately scan project-level samples, Unity assets, and local build settings for public `appIds`, sample `clientTokens`, and local keystore paths before any public release.
