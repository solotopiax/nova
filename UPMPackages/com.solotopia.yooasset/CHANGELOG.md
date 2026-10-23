# Changelog

## [Unreleased]

## [1.2.1] - 2026-10-23

### Changed

- 对齐依赖版本：com.unity.scriptablebuildpipeline 2.6.1 → 4.0.1。

- Scriptable Build Pipeline 4.0.1 将序列化类型信息纳入内容 Hash；升级后 Bundle Hash 与增量复用结果可能变化，需重新核验资源构建。

## [1.2.0] - 2026-10-10

### Changed

- Core 基线升级为官方 YooAsset 3.0.6（commit `3b4cfb36cc2e81b9558ab74d65fee2cf2eac2804`），私包版本升为 1.2.0；完整同步上游 Runtime、Editor、Samples 与许可材料，保留 Nova Settings 注入、显式配置路径、BundleCollector 缓存重置和 `NovaFramework.Editor` 友元，以及原生下载缓存校验最终结果通知，共 6 个功能补丁。

- ScriptableBuildTasks 采用官方 `SBP_2_0_1_OR_NEWER` 自动版本适配，移除原功能 fork 标记；依赖继续使用 Scriptable Build Pipeline 2.6.1，Runtime asmdef 保留私包名及 1.1.0 宏下界，所有 asmdef 元数据 GUID 保持不变。

- 移除未发布资源加解密功能的旧类名解析、联动显示和附加筛选注册，仅保留 Bundle Builder 与 Nova/Pipify 共用的可实例化候选规则。

### Fixed

- 跟随 3.0.6 上游将相关 FileSystem Operations 目录统一为 `Internal`（含原本地 BuiltinFileSystem 小写目录），保留对应 meta GUID；保留 IDownloadBackend 的本地注释修正与既有 UXML importer 元数据。

- 保留 Nova 原生下载的内容校验失败重试语义：DownloadAndCacheFileOperation 不在 HTTP 终态提前通知 URL 策略，SFSDownloadBundleOperation 在缓存校验最终成功/失败后统一通知并在失败通知后判定重试；保留 3.0.6 的资源释放与同步加载后台继续下载修复。

- Bundle Builder 加解密下拉排除抽象与不可实例化类型，提供候选筛选和旧清单解密类名解析扩展，与 Nova/Pipify 共用候选及排序。

### Breaking

- 跟随上游移除 `AsyncOperationBase.InternalAbort()`，自定义操作清理须迁移到 `InternalDispose()`；该方法在成功、失败及中止收尾时都会调用，原来仅在中止时执行的逻辑须保留相应条件判断。

- `BundleCollectorSetting.BeginCollect` 由三个参数改为四个参数，调用方须显式传入 `enableAssetPathValidation`；传入 `false` 保留原先不检测资源路径格式控制字符的行为。

- 旧兼容基类 `GameAsyncOperation` 移除 `OnAbort()`；自定义操作应改为继承 `CustomAsyncOperation`，将 `OnStart` / `OnUpdate` 改为 `InternalStart` / `InternalUpdate`，需要同步等待时将 `OnWaitForAsyncComplete` 改为 `InternalWaitForCompletion`，清理迁移到 `InternalDispose` 并区分正常完成与中止。

- `EFileSystemParameter` 插入 `WebPreloadStrategy`，使 `BundleUnpackPolicy` 的数值由 `28` 变为 `29`、`BuiltinFileAccessor` 由 `29` 变为 `30`；持久化或跨边界传递整数值的外部调用方须迁移，现有 `FileSystemParameters.AddParameter` 按枚举名称存储参数。

- Unity 6000 及以上改用 `[UxmlElement]`，不再提供 `ResizeHandle`、`ToggleDisplay`、`ToggleFoldout`、`ToggleRecord`、`ReorderableListView`、`TableViewer`、`TreeViewer` 的嵌套 `UxmlFactory`；外部直接引用这些工厂类型的代码须迁移，旧 Unity 分支仍保留工厂。

## [1.1.1] - 2026-09-02

### Changed

- 更新下载后端接口注释，移除已废弃 BestHTTP 实现的说明并补齐文件末尾换行。

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
