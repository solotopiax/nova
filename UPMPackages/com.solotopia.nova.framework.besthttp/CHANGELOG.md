# Changelog

## [0.0.6] - 2026-06-18

### Changed

- 例行版本升级。

## [0.0.5] - 2026-06-16

### Changed
- 升级内部依赖 `com.tivadar.best.http` 3.0.8 → 3.0.17、`com.tivadar.best.tlssecurity` 3.0.1 → 3.0.4。

## [0.0.4] - 2026-06-16

### Changed
- 将 `com.tivadar.best.http` / `com.tivadar.best.tlssecurity` 移入 `dependencies`（依赖权威源），`nova.requiredLibraries` 仅保留展示元数据并补全 `purchaseUrl`。
- Runtime asmdef 移除 `versionDefines`、`defineConstraints` 置空（不再依赖 `NOVA_BEST_HTTP` 宏，改由 `dependencies` 保证 BestHTTP 程序集存在）。

## [0.0.3] - 2026-06-15

### Changed
- 刷新 BestHTTP Runtime asmdef 配置，随本轮包内变更发布新版本。

## [0.0.2] - 2026-06-15

### Changed

- 更新包级授权文件内容。

## [0.0.1] - 2026-06-15

### Added

- 新增 Nova Framework 的 BestHTTP 可选 HTTP 后端适配包。
