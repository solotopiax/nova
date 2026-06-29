# Changelog

This file records notable changes to `com.solotopia.nova.framework.sdk.facebook`.

## [0.0.4] - 2026-06-23

### Changed
- Moved the imported official Facebook Unity SDK `18.0.0` into `Core/FacebookSDK`.
- Removed official `Examples` from the package to avoid compiling example-only scripts.
- Moved `DisableBitcode.cs` into the package.
- Reorganized package layout to use `Core` and `Nova` folders.
- Changed the package license metadata to point at `LICENSE.md` because the package contains both Solotopia-authored content and upstream Facebook SDK content.
- Renamed the Nova integration from `FacebookAuthPlugin` to `FacebookPlugin`.

### Added
- Added `FacebookSdkUsage.md` with API notes extracted from the removed examples.
- Added `com.google.external-dependency-manager` as a package dependency for native dependency resolution.
- Added `THIRD_PARTY_NOTICES.md` and `Core/FacebookSDK/LICENSE.txt`.
- Added `FacebookPluginConfig`, auth/profile/friends/share services, avatar cache helpers, and default Graph API paths.
- Added automatic current-user avatar download after login.
- Added fixed friends request `me/friends?fields=id,name,picture`.

### Removed
- Removed example-only `meta.mp4` and `meta-logo.png` assets from the package.

## [0.0.3] - 2026-05-21

### Changed
- Adjusted package structure and removed redundant resources.

## [0.0.2] - 2026-05-21

### Added
- Added `CHANGELOG.md`, `LICENSE.md`, and `README.md`.
