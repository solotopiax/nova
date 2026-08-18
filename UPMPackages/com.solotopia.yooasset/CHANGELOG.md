# Changelog

## [1.1.0] - 2026-08-18

### Changed (local fork)

- Core 基线升级为官方 YooAsset 3.0.5（commit `94422fc41491228eed0999ce4845d7b23ee2b8ae`），保留 `com.solotopia.yooasset` 私包身份与外层 `1.1.0` 版本。
- Unity 6000.4 的 Scriptable Build Pipeline 依赖对齐为 `2.6.1`，构建任务使用 `CreateBuiltInBundle`。
- 保留 Nova 显式 Settings 路径注入、BundleCollector 缓存重置、YooAssetSettings 注入与 `NovaFramework.Editor` 友元扩展。
- Runtime asmdef 的 versionDefines 改为私包名，版本条件与私包 `1.1.0` 对齐，并采用 3.0.5 上游的 `YOOASSET_3*` 宏名称。
- 归一上游大小写冲突目录为 `BuiltinFileSystem/Operations/internal/`，保留上游 `internal.meta` GUID。
- Nova 的 `IRawFileHandle` 签名和调用方式保持不变，`GetBytes()` 继续可靠返回原始内容副本；`FilePath` 从旧原始文件绝对路径改为 best-effort 底层 bundle 路径，同步/Web/内存/不支持 Ensure 的路径允许为 null。该项不是完全语义兼容；仓库检索未发现框架内部 `FilePath` 消费方，外部消费方需改用 `GetBytes()`。

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
