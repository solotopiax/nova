# Changelog

## [1.0.6] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [1.0.5] - 2026-06-15

### Changed
- 补发上次 release 后的包内变更，并对齐本轮 UPM 发布版本。

## [1.0.4] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [1.0.3] - 2026-05-28
### Changed (by taoye)
- 暴露 YooAssetConfiguration.SetSettings（internal）注入点，支持外部按路径注入 YooAssetSettings，避开 Resources.Load 多副本玄学
- 新增 SettingLoader.LoadSettingDataAtPath<T> 按路径加载重载，替代 AssetDatabase.FindAssets 全工程扫描
- AssemblyInfo 新增 NovaFramework.Editor 友元声明，使注入层可访问 internal API

---

## [1.0.2] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [1.0.1] - 2026-05-21

### Added
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。

### Changed
- 跟随主框架 0.5.0 升版。
