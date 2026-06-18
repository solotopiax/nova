# IAssetManager

`IAssetManager` 定义的是资源系统契约，不是 YooAsset API 抄录。  
调用方真正应该依赖的是这些语义：

- 什么时候先启动资源系统
- 什么时候 Manifest 才可用
- 句柄由谁释放
- 哪些能力属于“资源获取”，哪些属于“下载与缓存治理”

## 契约定位

它把资源系统拆成四层能力：

- 启动层：`Initialize / BootstrapAsync / LoadManifestAsync`
- 补丁层：`HasPatchAsync / CreateDownloader*`
- 资源层：`Load* / Preload*`
- 治理层：`CleanupAsync / RefreshManifestAsync / ClearUnusedCacheAsync`

直接依赖它的通常是：

- `AssetComponent`
- `ProcedureCheckVersion / ProcedureHotfix / ProcedureLoadDll`
- `ConfigManager`
- `PrefabManager`
- 任何需要显式加载资源的运行时代码

## 调用方可依赖的语义

### 1. 启动被拆成多个阶段

- `Initialize(config)`：只接收配置
- `BootstrapAsync()`：注册包、准备底层资源系统
- `LoadManifestAsync(package)`：初始化包并加载版本 / 清单

不要把这三步当成一个动作。

### 2. 所有 `Load*` 返回的都是句柄，而不是裸资源

调用方可以依赖：

- 主资源：`IAssetHandle<T>`
- 子资源：`ISubAssetsHandle<T>`
- 全资源：`IAllAssetsHandle<T>`
- 原始文件：`IRawFileHandle`
- 场景：`ISceneHandle`

核心语义是：

- 资源可读不等于引用计数已自动回收
- 句柄释放责任在调用方

### 3. 补丁检查与下载器是分开的

- `HasPatchAsync()`：只回答“是否有补丁”
- `CreateDownloader*()`：只创建下载器

真正下载发生在下载器自己的执行流程里，不在 `IAssetManager` 契约本身里。

### 4. 缓存治理是显式动作

- `CleanupAsync()`：回收运行时未使用资源
- `RefreshManifestAsync()`：强制刷新清单
- `ClearUnusedCacheAsync()`：清理磁盘冗余 Bundle

调用方不应该假设这些动作会在资源加载后自动发生。

## 最小 API 面

- 启动：`BootstrapAsync()` / `LoadManifestAsync()`
- 补丁：`HasPatchAsync()` / `CreateDownloader()`
- 加载：`LoadAsync<T>()` / `LoadSync<T>()`
- 场景：`LoadSceneAsync()`
- 原始文件：`LoadRawAsync()`
- 预热：`PreloadAsync()`
- 回收：`CleanupAsync()`

## 变更影响面

如果这里的契约变化，会直接影响：

- [AssetComponent.md](../../AssetComponent.md)
- [AssetManager.md](../Implements/AssetManager.md)
- [ConfigManager.md](../../../Config/ConfigManager.md)
- [ProcedureLoadDll.md](../../../Procedure/Procedures/ProcedureLoadDll.md)
- [PrefabManager.md](../../../Prefab/PrefabManager/PrefabManager.md)

高风险变化包括：

- Handle 是否仍由调用方显式释放
- `BootstrapAsync` 与 `LoadManifestAsync` 的阶段边界
- `HasPatchAsync()` 是否继续允许内部补前置清单加载
- `Load*` 是否仍以默认包为主

## 相关实现

关键源码：

- [IAssetManager.cs](../../../../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Interfaces/IAssetManager.cs)

相关文档：

- [AssetManager.md](../Implements/AssetManager.md)
- [AssetComponent.md](../../AssetComponent.md)
- [AssetManagerConfig.md](../Definitions/AssetManagerConfig.md)
- [IAssetHandle.md](IAssetHandle.md)
- [IAssetDownloader.md](IAssetDownloader.md)
