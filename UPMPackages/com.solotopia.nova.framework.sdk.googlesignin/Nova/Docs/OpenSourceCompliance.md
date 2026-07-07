# Open Source Compliance

This note describes the Google Sign-In package boundary and release-time checks.
It is a package-maintenance aid, not a legal guarantee. Release owners should
recheck current upstream licenses, platform terms, and resolved dependency
versions for each public release.

## License Boundary

| Area | Ownership / License Boundary | Notes |
|---|---|---|
| `Nova/**` | Solotopia/Nova MIT content | C# adapter layer, runtime plugin, config, data types, editor dependency declarations, tests, and docs. |
| `Core/Plugins/Android/**` | Solotopia/Nova MIT content | Nova Android bridge code that calls Android Credential Manager and Google Identity APIs. |
| EDM4U Gradle dependencies | Upstream licenses and terms | Resolved at build time; not vendored or relicensed as MIT by this package. |

## Third-Party Dependencies

| Dependency | Version | License / Terms | Resolution Path |
|---|---:|---|---|
| `androidx.credentials:credentials` | `1.3.0` | Apache Software License, Version 2.0 | Gradle via EDM4U |
| `androidx.credentials:credentials-play-services-auth` | `1.3.0` | Apache Software License, Version 2.0 | Gradle via EDM4U |
| `com.google.android.gms:play-services-auth` | `21.1.1` | Android SDK License | Gradle via EDM4U |
| `com.google.android.libraries.identity.googleid:googleid` | `1.1.1` | Android SDK License | Gradle via EDM4U |

## Public Release Requirements

- Retain package `LICENSE.md` and `THIRD_PARTY_NOTICES.md`.
- Re-run EDM4U dependency resolution and record the exact Android artifacts used
  by the release.
- Recheck the resolved dependency license files, required notices, and current
  Google/AndroidX platform terms.
- Confirm public sample configuration values are intentional and not production
  secrets.
- Do not ship OAuth client secrets, private signing material, provisioning
  profiles, production certificates, or environment-specific service files.
- Scan broader release locations beyond this package, including generated build
  outputs, signing folders, provisioning profile folders, Firebase/Google
  service-file locations, and CI artifact staging directories.
- Recheck privacy disclosures, login UI wording, sign-out UX, account deletion
  UX, and user-data handling requirements at release time.

## Known Risks

| Risk | Current Finding | Release Action |
|---|---|---|
| Public Google sample `ClientId` | Sample configs in `Assets/Samples/GoogleSigninDemo/**` contain `YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com`. This is a public client identifier, not an OAuth client secret. | Confirm the value is intentionally public for samples or replace it through release config generation. |
| Secrets in requested scan scope | Requested scan scope did not find `client_secret`, `private_key`, production service files, or provisioning profile material. | Do not treat this as a complete release audit. Scan broader certificate, provisioning, service-file, CI, and generated-output locations before release. |
| Build-time Google/AndroidX dependencies | Gradle artifacts are not vendored in this package and keep upstream licenses or Android SDK terms. | Keep third-party notices current with resolved artifacts and avoid implying MIT relicensing of those dependencies. |
