# Nova Framework - SDK - Google Sign-In

Nova Google Sign-In SDK package for Unity. It provides a Nova adapter around
Google Sign-In on Android, returns Nova `AuthResult` values, and keeps
Android-specific native code inside this UPM package.

## What This Package Contains

- `Nova/**`: Nova-owned C# adapter layer, runtime plugin, configuration, data
  types, editor build processor, tests, and package documentation.
- `Core/Plugins/Android/**`: Nova native Android bridge code that calls Android
  Credential Manager and Google Identity APIs.
- `Nova/Scripts/Editor/Dependencies/GoogleSignInDependencies.xml`: EDM4U
  dependency declarations for Android Gradle artifacts.

The package does not vendor Google or AndroidX dependency source code.

## Native Dependencies

Android Google Identity dependencies are resolved by EDM4U through Gradle and
are not vendored as source in this package:

- `androidx.credentials:credentials:1.3.0`
- `androidx.credentials:credentials-play-services-auth:1.3.0`
- `com.google.android.gms:play-services-auth:21.1.1`
- `com.google.android.libraries.identity.googleid:googleid:1.1.1`

## Login Behavior

`GoogleSignInPlugin` implements Nova `IAuthPlugin` and returns an `AuthResult`.
On success, `AuthResult.Token` is the Google ID Token returned by the Android
platform flow. `CurrentUserData` keeps the latest `GoogleSignInUserData`
including `UserId`, `IdToken`, `Email`, `DisplayName`, and `AvatarUrl`.

Android uses Credential Manager with Google ID Token credentials. When
`FilterByAuthorizedAccounts` is enabled, the bridge first requests authorized
accounts and retries without that filter if the platform request fails.

Callers should treat ID Tokens as sensitive authentication material, send them
only to the server or authentication flow that verifies them, and avoid long-term
client-side caching of stale tokens.

## Configuration

Configure `GoogleSignInPluginConfig` before Nova SDK initialization:

- `ClientId`: Android Google Sign-In web client ID used for ID Token requests.
- `RequestEmail`: requests email data when supported by the Android platform
  flow.
- `FilterByAuthorizedAccounts`: Android Credential Manager authorized-account
  filtering.
- `AutoSelectEnabled`: Android Credential Manager auto-select behavior.
- `AutoRestoreOnInitialize`: attempts to restore a previous Google sign-in
  during plugin initialization.

Public packages and sample configs must not include OAuth client secrets,
private signing material, provisioning profiles, or environment-specific service
files such as production `google-services.json` or `GoogleService-Info.plist`.
Client IDs are platform identifiers, not secrets, but release owners should
still confirm that sample values are intentional for public distribution.

## Open Source And Redistribution Status

The Google Sign-In package layer, `Nova/**`, and Nova bridge code under
`Core/Plugins/Android/**` are Solotopia/Nova content distributed under the
package license. Build-time dependencies resolved by EDM4U keep their own
upstream licenses and terms; they are not relicensed as MIT by this package.

Public distribution of this package should retain `LICENSE.md` and
`THIRD_PARTY_NOTICES.md`. See `Nova/Docs/OpenSourceCompliance.md` for the
current package-level compliance notes and release checks.

## Release Checklist

- Keep `LICENSE.md` and `THIRD_PARTY_NOTICES.md` in the distributed package.
- Re-run dependency resolution and record the actual Android artifacts used by
  the release build.
- Recheck Google, AndroidX, Google Play, and Android policy requirements that
  apply at release time.
- Confirm public sample `ClientId` values are intentional public identifiers.
- Scan broader release inputs for OAuth client secrets, private keys, signing
  certificates, provisioning profiles, production service files, and other
  environment-specific material.
- Confirm privacy disclosures and account sign-out or deletion UX still match
  the current app release requirements.
