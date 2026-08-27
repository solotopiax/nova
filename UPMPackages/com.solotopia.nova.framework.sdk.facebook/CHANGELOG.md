# Changelog

This file records notable changes to `com.solotopia.nova.framework.sdk.facebook`.

## [Unreleased]

## [0.1.10] - 2026-08-27

### Changed

- FacebookDemo 在 Network 初始化前完成 Localization 加载与当前语言初始化。

## [0.1.9] - 2026-08-27

### Fixed

- 修复 FacebookDemo 覆盖导入或包升级后路径重写遗漏的问题。

## [0.1.8] - 2026-08-26

### Changed

- Android 构建收尾改用 Framework `0.6.18` 的 Nova 前置完成钩子，确保 Facebook 官方 Manifest 生成发生在 Nova 自有处理之后。
- 最低 Framework 依赖提升至 `0.6.18`。

## [0.1.7] - 2026-08-20

### Changed

- FacebookDemo 移除重复场景监听，改由 Framework `0.6.16` 的统一 `SceneRoute` 接管；最低 Framework 依赖同步提升至 `0.6.16`。

## [0.1.6] - 2026-08-18

### Added

- Added Facebook acquisition tracking support through `IAcquisitionTrackPlugin` and `FB.LogAppEvent`.
- Subscribed to Nova `SDKEventData.UserLogin` so business user login syncs `UserId` to Facebook App Events.

### Changed

- 将 Framework 与 GameLogin 最低依赖同步至 `0.6.15`、`0.1.8`；GameBind 保持本轮未发布的 `0.1.0` 目标版本。

## [0.1.5] - 2026-08-13

### Changed

- 将 Framework、GameLogin 与 GameBind 最低依赖同步至本轮发布版本。

## [0.1.4] - 2026-08-12

### Added

- 新增 Facebook Console / Readme 官方快捷入口。

## [0.1.3] - 2026-08-03

### Changed

- 第三方登录提供方数据改用 `ThirdLoginProvider`；Framework、GameLogin 与 GameBind 最低依赖分别提升至 `0.6.4`、`0.1.3` 与 `0.0.9`。

## [0.1.2] - 2026-08-03

### Changed

- FacebookDemo 同步 Localization 支持语言表与 JSON / Binary 数据格式能力，并将 Framework 最低依赖提升至 `0.6.3`。

## [0.1.1] - 2026-07-31

### Changed

- 将 `FacebookPlugin` 初始化优先级调整为 `40`。

## [0.1.0] - 2026-07-29

### Changed

- 将 Framework、GameLogin 与 GameBind 最低依赖分别提升至 `0.6.0`、`0.1.0` 与 `0.0.6`。
- FacebookDemo 同步启动应用配置网络命令、运行时配置与场景覆盖。

## [0.0.9] - 2026-07-21

### Changed

- Facebook 登录成功后发布 `OpenId` 与 `ThirdLoginProvider` 数据，供 TGA 等分析插件自动同步用户属性。
- 将 Nova Framework 最低依赖版本提升至 `0.5.42`。

## [0.0.8] - 2026-07-13

### Changed

- 提升 Framework、GameLogin 与 GameBind 的依赖下界，保证独立安装时完整解析到本轮 Unity 6000.5 兼容版本。

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
