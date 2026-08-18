# Changelog

## [Unreleased]

## [0.0.13] - 2026-08-18

### Changed

- 将 Framework 与 GameLogin 最低依赖同步至 `0.6.15`、`0.1.8`；GameBind 保持本轮未发布的 `0.1.0` 目标版本。

## [0.0.12] - 2026-08-13

### Changed

- 将 Framework、GameLogin 与 GameBind 最低依赖同步至本轮发布版本。

## [0.0.11] - 2026-08-03

### Changed

- 第三方登录提供方数据改用 `ThirdLoginProvider`；Framework、GameLogin 与 GameBind 最低依赖分别提升至 `0.6.4`、`0.1.3` 与 `0.0.9`。

## [0.0.10] - 2026-08-03

### Changed

- AppleSigninDemo 同步 Localization 支持语言表与 JSON / Binary 数据格式能力，并将 Framework 最低依赖提升至 `0.6.3`。

## [0.0.9] - 2026-07-31

### Changed

- 将 `AppleSignInPlugin` 初始化优先级调整为 `50`。

## [0.0.8] - 2026-07-29

### Changed

- 将 Framework、GameLogin 与 GameBind 最低依赖分别提升至 `0.6.0`、`0.1.0` 与 `0.0.6`。
- AppleSigninDemo 同步启动应用配置网络命令、运行时配置与场景覆盖。

## [0.0.7] - 2026-07-23

### Changed

- 配置面板中的插件名称调整为“Apple 登录”，让启用项与业务用途更易辨识。

## [0.0.6] - 2026-07-21

### Changed

- Apple 登录成功后发布 `OpenId` 与 `ThirdLoginProvider` 数据，供 TGA 等分析插件自动同步用户属性。
- 将 Nova Framework 最低依赖版本提升至 `0.5.42`。

## [0.0.5] - 2026-07-13

### Changed

- 提升 Framework、GameLogin 与 GameBind 的依赖下界，保证独立安装时完整解析到本轮 Unity 6000.5 兼容版本。

## [0.0.4] - 2026-07-13

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.37`，避免安装时解析到仍使用旧契约的框架版本。

## [0.0.3] - 2026-07-08

### Fixed

- 清理 sample asmdef 克隆残留引用：删除代码未使用的 gamesave / tga / appsflyer / ad 跨包程序集引用（从 MainDemo 模板克隆时带入，AppleSigninDemo 实际未调用），避免消费工程未安装这些无关包时编译报 CS0234。
- 补全 sample 依赖声明：package.json dependencies 增加 com.solotopia.nova.framework.kit.network.gamelogin + com.solotopia.nova.framework.kit.network.gamebind（AppleSigninDemo 的登录/绑定/存档演示实际使用），修复消费工程 import sample 后缺对应程序集的编译失败。

## [0.0.2] - 2026-06-30

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.32`，修复公网 registry 仅有 0.5.32 而旧声明 0.5.31 缺失导致安装 404 的问题。
- 规整 `AppleSignInPluginConfig` 位置与 Runtime 程序集文件归位。

### Removed

- 移除包内过时的 `AppleSignInPluginTestRunner` / `AppleSignInPluginTests` 及 `EditorTests.asmdef`（非公开 API，不影响消费方）。

## [0.0.1] - 2026-06-19

### Added

- Initial Apple Sign-In SDK package.
