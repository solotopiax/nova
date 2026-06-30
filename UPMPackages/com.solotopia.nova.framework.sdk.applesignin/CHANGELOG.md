# Changelog

## [0.0.2] - 2026-06-30

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.32`，修复公网 registry 仅有 0.5.32 而旧声明 0.5.31 缺失导致安装 404 的问题。
- 规整 `AppleSignInPluginConfig` 位置与 Runtime 程序集文件归位。

### Removed

- 移除包内过时的 `AppleSignInPluginTestRunner` / `AppleSignInPluginTests` 及 `EditorTests.asmdef`（非公开 API，不影响消费方）。

## [0.0.1] - 2026-06-19

### Added

- Initial Apple Sign-In SDK package.
