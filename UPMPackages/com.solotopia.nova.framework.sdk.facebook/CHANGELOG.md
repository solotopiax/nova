# Changelog

This file records notable changes to `com.solotopia.nova.framework.sdk.facebook`.

## [0.0.7] - 2026-07-13

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.37`，避免安装时解析到仍使用旧契约的框架版本。

## [0.0.6] - 2026-07-08

### Fixed

- 清理 sample asmdef 克隆残留引用：删除代码未使用的 gamesave / tga / appsflyer / ad 跨包程序集引用（从 MainDemo 模板克隆时带入，FacebookDemo 实际未调用），避免消费工程未安装这些无关包时编译报 CS0234。
- 补全 sample 依赖声明：package.json dependencies 增加 com.solotopia.nova.framework.kit.network.gamelogin + com.solotopia.nova.framework.kit.network.gamebind（FacebookDemo 的登录/绑定/存档演示实际使用），修复消费工程 import sample 后缺对应程序集的编译失败。

## [0.0.5] - 2026-07-08

### Changed

- 首次纳入正常发版（此前在发布脚本禁发名单，现移除）；FacebookDemo sample 的真实 App ID / Client Token 在发版时自动脱敏为占位后才进公开侧，主仓保留真值。
- 物料配置对齐。

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
