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
3. 缓存 `IHttpManager`，供可选启动白名单文件请求使用

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
3. 如果包尚未成功初始化，先按配置尝试启动白名单检查
4. 调用 `InitializePackageAsync(options)`
5. 再 `RequestPackageVersionAsync()`
6. 再 `LoadPackageManifestAsync(version, 60)`
7. 成功后只把包名记入 `m_ManifestLoadedPackages`；Manifest 激活本身不再推进本地可启动版本

这一步既做“包初始化”，也做“版本 + 清单拉取”，而且是按包幂等的。只有当前启动下载策略确认就绪后，`ProcedureCheckVersion` / `ProcedureHotfix` 才通过 `CommitBootableVersion` 推进本地可启动版本。

#### 启动设备白名单

启动白名单默认关闭，并同时要求 `EnableHotfix=true`、有效模式为 Host/Web、白名单文件 URL 与元数据根 URL 至少各有一个有效地址。本地 DeviceID 来自 `persistentDataPath/Asset/asset-check-device-id.dat`；首次没有缓存时直接跳过，SDK 插件完成初始化后通过 `SaveAssetCheckDeviceId` 原子写入 UTF-8 明文，供后续启动使用。

白名单文件通过 `IHttpManager.DownloadTextAsync` 按主备顺序请求，但资源下载/CDN 与热更新保持原有系统 DNS 机制，不进入业务 DoH 路由。文件必须是 DeviceID JSON 字符串数组；网络失败、空响应或非法 JSON 均按未命中继续。`.version` / `.hash` / `.bytes` 版本元数据无论是否命中白名单，都会按候选顺序处理传输失败及 HTTP 200 但内容非法/损坏的失败：未命中时是常规主备，命中时是白名单主备后再到常规主备；全部候选失败后才进入现有离线回退。Bundle 始终走常规主机地址。

启动诊断统一使用 `Log.Debug`：输出功能门控状态、白名单文件主备请求结果、命中/未命中结果，以及命中后 `.version` / `.hash` / `.bytes` 的实际请求成功或失败（包含文件名和完整 URL）。命中日志会输出完整 DeviceID，便于现场核对白名单内容；Bundle 请求不会进入这组元数据日志。

HostPlayMode 下如果 `RequestPackageVersionAsync()` 或 `LoadPackageManifestAsync(version, 60)` 因 DNS、弱网或服务器不可达失败，`AssetManager` 会按 `TryRecoverManifestAsync` 编排的**三级回退链**逐级尝试：

1. **沿用当前已激活清单**：若包已加载过清单（`PackageValid`，如 `RefreshManifestAsync` 弱网场景），直接复用。
2. **本地可启动版本离线加载**（`TryFallbackToLocalBootableManifestAsync`）：读回本地记录的版本号，在当前 Host 包上直接加载缓存 Manifest，再按当前 `LaunchHotfixTags` 重建启动下载范围；只有下载数为 0 才接受该版本。缓存被清理或 Tag 配置变化导致范围不完整时继续降级内置清单。
3. **随包内置清单回退**（`TryFallbackToBuiltinManifestAsync`）：销毁 Host 包 → `OfflinePlayModeOptions` 重新初始化 → 从内置版本文件与内置 Manifest 加载（回退到首包版本，丢弃增量）。

整体优先级是：远端最新清单 → 已激活清单 → 本地可启动版本清单 → 随包内置清单 → 抛出原始远端错误。
全链路只使用 YooAsset 公开 API，不修改 YooAsset 源码，也不直接读取沙盒 Manifest 内部结构。本地可启动版本回退随 HostPlayMode 默认开启；本地无记录、缓存 Manifest 缺失或当前启动范围不完整时自动降级到内置回退。

#### 本地可启动版本记录文件（LastBootableVersion）

二级回退依赖一份 Nova 自管的版本记录文件，由 `SaveLocalBootableVersion` / `TryLoadLocalBootableVersion` / `GetLocalBootableVersionFilePath` 维护。

**路径**：`persistentDataPath/Asset/{package}.version`，根路径仍由 Unity `Application.persistentDataPath` 决定。

**写入 / 覆盖时机**（`File.WriteAllText` 整体覆盖，非追加）：

- `LaunchHotfixTags` 为空时，整包 Downloader 无差异或完整下载成功后写入。
- `LaunchHotfixTags` 非空时，对应 Tag 启动范围无差异或完整下载成功后写入。
- 下载失败、取消或用户跳过补丁时不写入。
- `version` 为空时 `SaveLocalBootableVersion` 直接返回，不写空文件。

三级离线回退路径一律不写，避免 builtin 首包覆盖此前可启动版本。旧 `Persist/Asset/CachedVersion` 记录不会读取或自动迁移，因为它只能证明 Manifest 曾激活，不能证明启动范围完整。

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

`LoadRawSync/Async` 的 Nova 公共签名与调用方式不变，但路径行为不是完全兼容。YooAsset 3.0.5 下内部改为 `AssetHandle + RawFileObject`：`GetBytes()` 从 `RawFileObject` 可靠返回原始内容副本；异步路径尽力从 `EnsureBundleFileAsync` 获取底层 bundle 文件路径，失败不影响字节加载。同步操作无法等待 Ensure，Web/内存文件系统也可能不支持本地路径，所以 `FilePath` 可以为 null。同步、异步、异常与取消路径都由 AssetManager/Adapter 成对释放 `AssetHandle`。仓库检索未发现框架内部的 `IRawFileHandle.FilePath` 消费方；外部消费方需要按新语义复核。

WebPlayMode 使用 3.0.5 的 `WebNetworkFileSystemParameters`。该文件系统不接受 Sandbox 专用的 `DownloadWatchdogTimeout`；HostPlayMode 的 Sandbox 文件系统仍保留 watchdog 配置。

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
- `m_StartupWhitelistCheckedPackages / m_StartupWhitelistMatchedPackages`：本次进程的白名单检查与命中状态
- `m_Decryptor`：沙盒文件系统解密器实例
- `m_Cts`：Manager 生命周期取消源

## 风险点 / 易错点

- `Initialize()` 不等于 `BootstrapAsync()`；只注入配置，不做包注册。
- `LoadManifestAsync()` 之前必须至少完成一次 `BootstrapAsync()`，否则包都还没注册。
- HostPlayMode 远端版本或 Manifest 请求失败时走三级回退链（已激活清单 → 本地可启动版本 → 内置清单）。本地记录位于 `persistentDataPath/Asset/{package}.version`，并会按当前启动 Tag 范围复核；首次安装无记录时自动降级内置清单。
- 大多数 `Load*` API 都默认走 `m_DefaultPackageName`；如果你以为它们支持多包透传，那是错的。
- Raw 文件内容应通过 `IRawFileHandle.GetBytes()` 获取；`FilePath` 是底层 bundle 路径，不能假定为可直接读取的原始文件路径。
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
