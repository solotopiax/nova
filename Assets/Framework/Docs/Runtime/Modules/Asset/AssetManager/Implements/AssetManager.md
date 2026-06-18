# AssetManager

`AssetManager` 是 Nova 资源系统的真实运行核心，也是 YooAsset 的框架封装层。  
它负责三类事情：

- 启动资源系统并注册包
- 管理清单、补丁检查与下载器创建
- 返回统一的 Handle 接口，屏蔽底层资源框架细节

## 什么时候先看这页

优先看这页的场景：

- 你要排查 `BootstrapAsync` / `LoadManifestAsync` 的真实时序。
- 你要看为什么 `HasPatchAsync()` 在没加载清单时也能工作。
- 你要确认默认包名、热更地址 URL、解密器到底在哪里生效。
- 你要区分哪些 API 支持显式 `package`，哪些永远走默认包。

## 依赖与边界

### 它依赖什么

- `AssetManagerConfig`
- YooAsset
- `AssetRemoteService`
- `AssetDownloader`
- 各种 Handle Adapter

### 它对外负责什么

- 注册资源包
- 初始化包运行模式
- 请求版本并加载 Manifest
- 检查补丁、创建下载器
- 返回统一 Handle
- 执行缓存预热与回收

### 它不负责什么

- 不负责 Unity 场景组件生命周期
- 不负责业务何时释放 Handle
- 不负责 Prefab 实例管理
- 不负责热更流程编排

## 核心流程

### 1. Initialize：只缓存配置，不触底层资源系统

`Initialize(config)` 只做两件事：

1. 记录 `m_Config`
2. 新建 `m_Cts`

它不会注册包，也不会初始化 YooAsset。

### 2. BootstrapAsync：真正把资源系统“立起来”

`BootstrapAsync()` 做的事：

1. 校验 `Packages` 非空
2. 解析 `m_DefaultPackageName`
3. 按 `DecryptorType` 创建 `m_Decryptor`
4. 在需要时 `YooAssets.Initialize()`
5. 遍历 `m_Config.Packages`，逐个 `CreatePackage` 或 `GetPackage`

这一层完成后，包才算注册完成。

### 3. LoadManifestAsync：初始化包运行模式并加载清单

`LoadManifestAsync(package)` 的关键语义：

1. `ResolvePackageName` 解析包名，空值走默认包
2. 已在 `m_ManifestLoadedPackages` 中则直接返回
3. 如果包尚未成功初始化，则先 `InitializePackageAsync(options)`
4. 再 `RequestPackageVersionAsync()`
5. 再 `LoadPackageManifestAsync(version, 60)`
6. 成功后先 `SaveLocalCachedVersion(name, pkg.GetPackageVersion())` 记录当前激活版本号到本地，再把包名记入 `m_ManifestLoadedPackages`

这一步既做“包初始化”，也做“版本 + 清单拉取”，而且是按包幂等的。每次远端正常加载成功都会把版本号写入 `persistentDataPath/Persist/Asset/CachedVersion/{package}.version`，作为下次启动远端不可达时的离线回退依据。

HostPlayMode 下如果 `RequestPackageVersionAsync()` 或 `LoadPackageManifestAsync(version, 60)` 因 DNS、弱网或服务器不可达失败，`AssetManager` 会按 `TryRecoverManifestAsync` 编排的**三级回退链**逐级尝试：

1. **沿用当前已激活清单**：若包已加载过清单（`PackageValid`，如 `RefreshManifestAsync` 弱网场景），直接复用。
2. **本地缓存版本离线加载**（`TryFallbackToLocalCachedManifestAsync`）：读回本地记录的版本号，在当前 Host 包上直接 `LoadPackageManifestAsync(localVersion, 60)`——YooAsset 的清单下载操作首步即 `CheckExists`，本地沙盒已缓存该版本则零网络命中。**不销毁包、不切运行模式，保留玩家已下载的全部增量缓存。**
3. **随包内置清单回退**（`TryFallbackToBuiltinManifestAsync`）：销毁 Host 包 → `OfflinePlayModeOptions` 重新初始化 → 从内置版本文件与内置 Manifest 加载（回退到首包版本，丢弃增量）。

整体优先级是：远端最新清单 → 已激活清单 → 本地缓存版本清单 → 随包内置清单 → 抛出原始远端错误。  
全链路只使用 YooAsset 公开 API，不修改 YooAsset 源码，也不直接读取沙盒 Manifest 内部结构。本地缓存版本回退随 HostPlayMode 默认开启（与内置回退同门控 `CanFallbackToBuiltinManifest`），无独立开关；本地无记录或缓存清单缺失时自动降级到内置回退，三级全失败仍保留原始远端失败并中断，避免把必需资源缺失伪装成可继续状态。

#### 本地缓存版本记录文件（CachedVersion）

二级回退（本地缓存版本离线加载）依赖一份 Nova 自管的版本记录文件，由 `SaveLocalCachedVersion` / `TryLoadLocalCachedVersion` / `GetLocalCachedVersionFilePath` 三个 `private static` 纯函数维护。

**路径**：`persistentDataPath/Persist/Asset/CachedVersion/{package}.version`（经 `Path.Persistent.GetFileFullPath` 拼接，与 `Persist/FileFragment`、`Persist/SQLite` 同体系）。

**写入 / 覆盖时机**（`File.WriteAllText` 整体覆盖，非追加）：

- **唯一写入点**：`LoadManifestAsync` 中远端版本请求 + 清单加载**两步都成功**后，写入 `pkg.GetPackageVersion()`（刚激活的版本号）。
- 触发场景：① 每次联网冷启动成功；② `RefreshManifestAsync` 热更切新清单成功（先 `Remove` 再重走 `LoadManifestAsync`），把记录**覆盖为新版本号**，使记录始终跟随玩家热更到的最新版本。
- 因 `LoadManifestAsync` 按包幂等，一次进程内同包通常只写一次，除非走 `RefreshManifestAsync`。
- `version` 为空时 `SaveLocalCachedVersion` 直接 return，不会写出空文件。

**刻意不写的场景（防污染，关键设计意图）**：三级回退路径——沿用已激活清单 / 本地缓存版本回退 / builtin 内置回退——**一律不写**。断网启动回退到 builtin 首包时绝不能把记录改写成首包版本，否则下次又只能退回首包。记录永远保持「上次远端成功确认的版本」。一句话：**只有远端真正成功（首启或热更切版）才覆盖，任何离线回退都不动它。**

**跨平台可达性**：该路径走 `Application.persistentDataPath`（iOS app 沙盒 / Android `files` 目录，各平台官方可写持久区），配 `System.IO.File/Directory` + 绝对路径（`NormalizeSeparator` 统一 `/`），iOS/Android 一致可读写；首次无目录时 `Directory.CreateDirectory` 递归创建。它与 YooAsset 自身沙盒缓存（`GetMobileCacheRoot()` 同样返回 `persistentDataPath`）属同一套读写机制——YooAsset 缓存能读写，本记录文件必然也能，且回退要命中的缓存清单本就在同一 persistentDataPath 根下。注意它**不在 StreamingAssets**（后者在 Android 打进 apk、不能 `File.ReadAllText` 直读，那是内置回退读首包版本时才会遇到的约束），故纯 File API 即可。

### 4. HasPatchAsync：没加载过清单时会自动补前置步骤

`HasPatchAsync()` 不要求上层先手动调用 `LoadManifestAsync()`。  
如果目标包还没进 `m_ManifestLoadedPackages`，它会先内部加载清单，再创建下载器看 `TotalDownloadCount`。

### 5. Load / Preload / Cleanup：大多数资源操作默认只走默认包

这是非常重要的当前事实：

- `LoadSync/Async`
- `LoadSubsSync/Async`
- `LoadAllSync/Async`
- `LoadRawSync/Async`
- `LoadSceneSync/Async`
- `PreloadAsync`

这些 API 内部都直接使用 `m_DefaultPackageName`。

也就是说，当前设计里并不是每个加载 API 都支持任意包名切换。  
显式 `package` 主要出现在清单、下载器、tag 查询、回收这类 API 上。

在 Unity Editor 下，如果 `EditorPlayMode` 不是 `EditorSimulateMode`，这些真实 AssetBundle 加载 API 会在资源出句柄前执行一次 **Editor-only shader 重绑**：

- `LoadSync/Async`
- `LoadSubsSync/Async`
- `LoadAllSync/Async`

它只把 bundle 反序列化出的 `Material.shader` 按同名 shader 重新绑定到当前 Editor 进程可用的 shader。  
这个逻辑用于 Host/Offline/Web PlayMode 在 Editor 里预览真实包，避免跨平台 bundle 中的 shader 对象在 Editor 渲染端显示为洋红色块；Player 运行时和 `EditorSimulateMode` 不执行这一步。

### 6. Shutdown：底层资源系统真正的清理点

`Shutdown()` 会：

1. `Cancel + Dispose` 生命周期 `CancellationTokenSource`
2. `YooAssets.Destroy()`
3. 清空已加载 Manifest 包集合
4. 清空已注册包字典
5. 清空解密器和配置引用

这一步之后再调用加载 API，不再成立。

## 高价值 API 面

### 1. 启动与清单

- `Initialize(config)`
- `BootstrapAsync()`
- `LoadManifestAsync(package)`
- `RefreshManifestAsync(package)`

### 2. 补丁

- `HasPatchAsync(package)`
- `CreateDownloader(...)`
- `CreateDownloaderByTags(...)`
- `CreateDownloaderByLocations(...)`

### 3. 资源加载

- `LoadAsync<T>()`
- `LoadSync<T>()`
- `LoadRawAsync()`
- `LoadSceneAsync()`
- `LoadSubsAsync<T>()`
- `LoadAllAsync<T>()`

### 4. 缓存治理

- `PreloadAsync(...)`
- `CleanupAsync(package)`
- `ClearUnusedCacheAsync(package)`

## 关键状态

- `m_Config`：所有运行模式、热更地址 URL / 兜底 URL、热更策略的输入源
- `m_DefaultPackageName`：大部分加载 API 的真实目标包
- `m_Packages`：已注册的 YooAsset 包
- `m_ManifestLoadedPackages`：清单幂等集合
- `m_Decryptor`：沙盒文件系统解密器实例
- `m_Cts`：Manager 生命周期取消源

## 风险点 / 易错点

- `Initialize()` 不等于 `BootstrapAsync()`；只注入配置，不做包注册。
- `LoadManifestAsync()` 之前必须至少完成一次 `BootstrapAsync()`，否则包都还没注册。
- HostPlayMode 远端版本或 Manifest 请求失败时走三级回退链（已激活清单 → 本地缓存版本 → 内置清单）；这只是弱网启动容错，不代表热更成功。其中本地缓存版本回退依赖上次成功启动写入的 `persistentDataPath/Persist/Asset/CachedVersion/{package}.version`：首次安装即断网（无该记录）时该级失效，自动降级到内置清单。
- 大多数 `Load*` API 都默认走 `m_DefaultPackageName`；如果你以为它们支持多包透传，那是错的。
- Editor 下用 Host/Offline/Web PlayMode 跑真实包时，TMP 或普通材质出现洋红色块，优先检查 shader bundle 与当前 Editor 渲染端是否跨平台；AssetManager 会对已加载资源做同名 shader 重绑，但这只服务编辑器预览，不代表 Player 会走同一套修复路径。
- `CreateDownloaderByLocations()` 对空数组会直接抛异常；“整包下载”应该用 `CreateDownloader()`。
- `CreateDownloaderByLocations()` 遇到无效 location 会跳过并记 warning，不会整体失败。
- `ClearUnusedCacheAsync()` 需要当前 Manifest 已可用，否则“未使用”没有判定基准。
- `Shutdown()` 会 `YooAssets.Destroy()`；这是全局级清理，不能把它当成局部无害重置。

## 继续阅读

关键源码：

- [AssetManager.cs](../../../../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.cs)
- [AssetManager.Methods.cs](../../../../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.Methods.cs)
- [AssetManager.Load.cs](../../../../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.Load.cs)
- [AssetManager.Cleanup.cs](../../../../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.Cleanup.cs)

相关文档：

- [IAssetManager.md](../Interfaces/IAssetManager.md)
- [AssetComponent.md](../../AssetComponent.md)
- [AssetManagerConfig.md](../Definitions/AssetManagerConfig.md)
- [IAssetHandle.md](../Interfaces/IAssetHandle.md)
- [IAssetDownloader.md](../Interfaces/IAssetDownloader.md)
