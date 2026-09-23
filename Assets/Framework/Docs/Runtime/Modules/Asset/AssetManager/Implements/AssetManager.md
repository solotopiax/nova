# AssetManager

`AssetManager` 是 Nova 资源系统的真实运行核心，也是 YooAsset 的框架封装层。  
它负责三类事情：

- 启动资源系统并注册包
- 管理清单、补丁检查与下载器创建
- 返回统一的 Handle 接口，屏蔽底层资源框架细节
- 创建并收口有明确所有权的 Bundle WarmupGroup

## 什么时候先看这页

优先看这页的场景：

- 你要排查 `BootstrapAsync` / `LoadManifestAsync` 的真实时序。
- 你要看为什么 `HasPatchAsync()` 在没加载清单时也能工作。
- 你要确认默认包名、热更地址 URL 到底在哪里生效。
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
3. 在需要时 `YooAssets.Initialize()`
4. 遍历 `m_Config.Packages`，逐个 `CreatePackage` 或 `GetPackage`

这一层完成后，包才算注册完成。

### 3. LoadManifestAsync：初始化包运行模式并加载清单

`LoadManifestAsync(package)` 的关键语义：

1. `ResolvePackageName` 解析包名，空值走默认包
2. 已在 `m_ManifestLoadedPackages` 中则直接返回
3. WebGL Host 模式先读取 Player 构建期生成的 `nova-webgl-layout.txt`，确定当前 Package 是否包含 YooAsset 官方 `BuiltinCatalog.bytes`
4. 如果包尚未成功初始化，再按配置尝试启动白名单检查
5. 调用 `InitializePackageAsync(options)`
6. 再 `RequestPackageVersionAsync()`
7. 再按 `ManifestRequestTimeout` 执行 `LoadPackageManifestAsync`
8. 成功后只把包名记入 `m_ManifestLoadedPackages`；Manifest 激活本身不再推进本地可启动版本

WebGL 初始化时还把 `BundleLoadingMaxConcurrency` 设为 `MaxDownloadConcurrency`（最小 1），限制无工作线程环境中同时启动的 Bundle 加载数量。

这一步既做“包初始化”，也做“版本 + 清单拉取”，而且是按包幂等的。只有当前启动下载策略确认就绪后，`ProcedureCheckVersion` / `ProcedureHotfix` 才通过 `CommitBootableVersion` 推进本地可启动版本。

#### 启动设备白名单

启动白名单默认关闭，并同时要求 `EnableHotfix=true`、有效模式为 HostPlayMode、白名单文件 URL 与元数据根 URL 至少各有一个有效地址。本地 DeviceID 来自 `persistentDataPath/Asset/asset-check-device-id.dat`；首次没有缓存时直接跳过，SDK 插件完成初始化后通过 `SaveAssetCheckDeviceId` 原子写入 UTF-8 明文，供后续启动使用。

成功拉取并解析的白名单会按包保存在当前进程内存中。业务可通过 `Nova.Asset.TryIsDeviceInStartupWhitelist(deviceId, out matched)` 同步查询默认包的缓存数据；该调用不触发网络，也不改变当前设备已经确定的资源路由。返回 `false` 表示白名单数据尚不可用或 DeviceID 无效，返回 `true` 时由 `matched` 区分命中与未命中。

`VersionsCheckWhiteList.json` 使用独立的 `StartupWhitelistFallbackRoundCount`、`StartupWhitelistRetryRequestCount`、`StartupWhitelistPreferLastSuccessfulHost`、`StartupWhitelistEnableUWRTracks` 与 `StartupWhitelistCheckTimeout`；默认分别为 `1`、`1`、`true`、`true`、`5`。这些配置不影响白名单命中后的版本元数据请求或 Bundle 下载。

内置 `HttpManager` 下，白名单文件通过不创建 Network 逻辑链的物理 UWR 入口请求，由 Asset 统一管理主备、轮次、重试和 `uwr_*` 链路埋点；兼容自定义 `IHttpManager` 时回退到 `DownloadTextAsync`。资源下载、CDN 与热更新由 YooAsset 的 UnityWebRequest 后端和 `AssetDownloadUrlPolicy` 独立路由。三条链统一按候选数 `C`、完整轮数 `R`、重试次数 `K` 形成 `C × R × (K + 1)` 次物理尝试；每次重试都会重新执行全部轮次。404/408/416/429、5xx、无响应及内容校验失败继续下一个候选；401/403 和其他 4xx 立即停止。`.version` / `.hash` / `.bytes` 未命中白名单时使用常规主备，命中时使用白名单主备后再到常规主备；全部尝试失败后才进入现有离线回退。Bundle 始终走常规主机地址。

`EnableUWRTracks` 开启时，YooAsset URL/重试适配层按文件上报统一的 `uwr_request_start/error/end`，并附带 `uwr_download_operation_id`、`uwr_package` 与 `uwr_file_type`。非 WebGL HostPlayMode 的缓存下载成功回调发生在文件校验完成后；WebGL HostPlayMode 的 WebNetwork 内存 Bundle 路径由 YooAsset 在内容校验前回调 HTTP 成功，因此发生校验重试时会以同一 download operation 下的新 UWR 链继续，不能伪造为已经取得最终内容校验成功。

每个文件冻结自己的候选计划，并发下载互不推进。成功域名会作为后续新文件的优先起点；完整失败保留已有成功偏好，只有候选配置已不再包含该域名或 Manager 关闭时清除。普通按需加载仍保持开启；同步加载行为不变。

启动诊断统一使用 `Log.Debug`：输出功能门控状态、白名单文件主备请求结果、命中/未命中结果，以及命中后 `.version` / `.hash` / `.bytes` 的实际请求成功或失败（包含文件名和完整 URL）。命中日志会输出完整 DeviceID，便于现场核对白名单内容；Bundle 请求不会进入这组元数据日志。

HostPlayMode 下，如果 `RequestPackageVersionAsync()` 或 `LoadPackageManifestAsync` 因 DNS、弱网或服务器不可达失败，`AssetManager` 会按平台执行对应回退。`.version` 使用 `CheckTimeout`，`.hash/.bytes` 使用 `ManifestRequestTimeout`；三类元数据共用 Asset 主备轮次、下载重试次数、最近成功域名优先和 UWR 埋点策略。

非 WebGL 平台按 `TryRecoverManifestAsync` 编排的**三级回退链**逐级尝试：

1. **沿用当前已激活清单**：若包已加载过清单（`PackageValid`，如 `RefreshManifestAsync` 弱网场景），并且按当前 `LaunchHotfixTags` 复核启动范围完整，才直接复用；不完整则继续降级。
2. **本地可启动版本离线加载**（`TryFallbackToLocalBootableManifestAsync`）：读回本地记录的资源版本与 `PackageFilePrefix`。跨 App 版本覆盖安装时，先把旧前缀对应的缓存 Manifest/hash 原子映射为当前前缀文件，再由当前 Host 包加载并按 `LaunchHotfixTags` 复核启动范围。旧版纯版本号记录会扫描缓存中的同版本文件对；候选内容冲突时拒绝猜测并继续降级。
3. **随包内置清单回退**（`TryFallbackToBuiltinManifestAsync`）：临时用 `OfflinePlayMode` 读取内置资源版本，然后恢复 `HostPlayMode`；恢复时把当前内置 Manifest/hash 复制到 Sandbox 并从 Sandbox 激活。最终运行态仍保留 Builtin + Sandbox，未随包资源不会失去文件系统归属。

整体优先级是：远端最新清单 → 已激活清单 → 本地可启动版本清单 → 随包内置清单 → 抛出原始远端错误。
全链路不修改 YooAsset 源码；Nova 兼容层只在前缀迁移时定位并复制 Sandbox 中成对的 Manifest/hash 文件，其余加载与校验仍走 YooAsset API。本地可启动版本回退随 HostPlayMode 默认开启；本地无记录、缓存 Manifest 缺失或当前启动范围不完整时自动降级到内置回退。

WebGL Player 构建后处理会扫描输出目录中的 `StreamingAssets/{YooFolder}/{Package}/BuiltinCatalog.bytes`，并生成始终存在的 `StreamingAssets/nova-webgl-layout.txt`。WebGL Host 运行时读取该清单：Package 在清单中时使用 `WebServer + WebNetwork`，否则使用纯 `WebNetwork`；纯 CDN 不再请求不存在的 Catalog，因此不会产生预期 404。旧 Player 缺少或损坏清单时才回退原 Catalog 探测以保持兼容。WebGL Offline 不读取布局清单，也不创建远端文件系统，固定使用纯 `WebServer`；因此必须用 `ClearAndCopyAll` 构建完整首包，Catalog 缺失时由 YooAsset 初始化明确失败。

Host 探测到 Catalog 时，远端元数据候选耗尽后，Nova 可临时把 `.version/.hash/.bytes` 路由到 `StreamingAssets/{YooFolder}/{Package}`，加载随 Player 发布的首包 Manifest，并在成功或失败后立即恢复远端元数据路由。纯 CDN 没有 Catalog，因此明确跳过该回退。`LaunchHotfixTags` 只在 `TagsOnLaunch` 中选择启动预热范围，不代替 Pipify 的首包拷贝选择。详见 [WebGLAssetStrategies.md](../../WebGLAssetStrategies.md)。

WebGL Bundle 不使用 `IdleTimeout` 字节流入看门狗，而是为每个 WebServer（StreamingAssets）或 WebNetwork（CDN）Bundle 请求应用 `WebGLBundleRequestTimeout`，默认 300 秒。每个主备候选独立计时，超时后仍按现有候选轮次与重试策略推进；该值不影响 `.version/.hash/.bytes` 的独立超时。

#### 本地可启动版本记录文件（LastBootableVersion）

二级回退依赖一份 Nova 自管的版本记录文件，由 `SaveLocalBootableVersion` / `TryLoadLocalBootableVersion` / `GetLocalBootableVersionFilePath` 维护。

**路径**：`persistentDataPath/Asset/{package}.version`，根路径仍由 Unity `Application.persistentDataPath` 决定。

记录内容为带 schema 的 JSON，同时保存资源版本和当时的 `PackageFilePrefix`；仍兼容读取旧版纯版本号文本。旧记录首次成功恢复后会自动升级为新格式。

**写入 / 覆盖时机**（`File.WriteAllText` 整体覆盖，非追加）：

- `LaunchHotfixTags` 为空时，整包 Downloader 无差异或完整下载成功后写入。
- `LaunchHotfixTags` 非空时，对应 Tag 启动范围无差异或完整下载成功后写入。
- 下载失败、取消或用户跳过补丁时不写入。
- `version` 为空时 `SaveLocalBootableVersion` 直接返回，不写空文件。

三级离线回退路径一律不写，避免 builtin 首包覆盖此前可启动版本。旧 `Persist/Asset/CachedVersion` 记录不会读取或自动迁移，因为它只能证明 Manifest 曾激活，不能证明启动范围完整。

**跨平台可达性**：该路径走 `Application.persistentDataPath`（iOS app 沙盒 / Android `files` 目录，各平台官方可写持久区），配 `System.IO.File/Directory` + 绝对路径（`NormalizeSeparator` 统一 `/`），iOS/Android 一致可读写；首次无目录时 `Directory.CreateDirectory` 递归创建。它与 YooAsset 自身沙盒缓存（`GetMobileCacheRoot()` 同样返回 `persistentDataPath`）属同一套读写机制——YooAsset 缓存能读写，本记录文件必然也能，且回退要命中的缓存清单本就在同一 persistentDataPath 根下。注意它**不在 StreamingAssets**（后者在 Android 打进 apk、不能 `File.ReadAllText` 直读，那是内置回退读首包版本时才会遇到的约束），故纯 File API 即可。

### 4. HasPatchAsync / HasPatchByTagsAsync：没加载过清单时会自动补前置步骤

`HasPatchAsync()` 与 `HasPatchByTagsAsync()` 都不要求上层先手动调用 `LoadManifestAsync()`。
如果目标包还没进 `m_ManifestLoadedPackages`，它们会先内部加载清单，再创建下载器看 `TotalDownloadCount`。

- `HasPatchAsync()` 检查整包范围，保留既有调用语义。
- `HasPatchByTagsAsync(tags)` 检查指定 Tag 范围；`tags` 为 null 或空数组时等价整包。
- 启动链在 `LaunchHotfixTags` 非空时使用 Tag 范围判断，保证进入 `ProcedureHotfix` 的条件与实际下载范围一致。

### 5. Load / Warmup / Cleanup：加载默认走默认包，Warmup 可显式选包

这是非常重要的当前事实：

- `LoadSync/Async`
- `LoadSubsSync/Async`
- `LoadAllSync/Async`
- `LoadRawSync/Async`
- `LoadSceneSync/Async`

这些 Load API 内部都直接使用 `m_DefaultPackageName`。

`CreateWarmupAll`、`CreateWarmupByTags`、`CreateWarmupByLocations` 则提供可选 `package` 参数：省略时走默认包，传入包名时在该已加载 Manifest 的 Package 中创建 Group。显式 `package` 也出现在清单、下载器、tag 查询与回收 API 中。

`LoadRawSync/Async` 的 Nova 公共签名与调用方式不变，但路径行为不是完全兼容。YooAsset 3.0.5 下内部改为 `AssetHandle + RawFileObject`：`GetBytes()` 从 `RawFileObject` 可靠返回原始内容副本；异步路径尽力从 `EnsureBundleFileAsync` 获取底层 bundle 文件路径，失败不影响字节加载。同步操作无法等待 Ensure，Web/内存文件系统也可能不支持本地路径，所以 `FilePath` 可以为 null。同步、异步、异常与取消路径都由 AssetManager/Adapter 成对释放 `AssetHandle`。仓库检索未发现框架内部的 `IRawFileHandle.FilePath` 消费方；外部消费方需要按新语义复核。

WebGL 下 `OfflinePlayMode` 使用 WebServer 文件系统，`HostPlayMode` 使用 WebServer + WebNetwork 文件系统。WebGL 没有可靠的字节流入看门狗，因此 WebServer 与 WebNetwork 都使用 `WebGLBundleRequestTimeout` 控制 Bundle 单次请求时长；非 WebGL HostPlayMode 的 Sandbox 文件系统仍保留 watchdog 配置。

`CreateWarmup*` 只创建 `IAssetWarmupGroup`，不会自动发起请求；调用方需要 `RunAsync()` 并在终止时 `Release()`。Group 的 Release 只是释放 BundleFileHandle 引用；随后 `CleanupAsync()` 才会卸载引用归零的运行时 Bundle。`TagsOnLaunch` 由启动流程先 Release，并在启动 DLL 消费完成后 Cleanup；`AllOnLaunch` 成功后由 `m_AllOnLaunchWarmupGroup` 持有到 Shutdown，业务主动创建的普通 Group 仍由创建者负责释放。

`IAssetWarmupGroup.Cancel()` 只停止等待并释放已创建 Handle；它不能承诺已经发出的 Web 请求立刻取消。WebGL 下所有业务资源加载也必须使用异步 API，不能把 Warmup 当作同步加载的替代品。

在 Unity Editor 下，如果 `EditorPlayMode` 不是 `EditorSimulateMode`，这些真实 AssetBundle 加载 API 会在资源出句柄前执行一次 **Editor-only shader 重绑**：

- `LoadSync/Async`
- `LoadSubsSync/Async`
- `LoadAllSync/Async`

它只把 bundle 反序列化出的 `Material.shader` 按同名 shader 重新绑定到当前 Editor 进程可用的 shader。  
这个逻辑用于 Host/Offline PlayMode 在 Editor 里预览真实包，避免跨平台 bundle 中的 shader 对象在 Editor 渲染端显示为洋红色块；Player 运行时和 `EditorSimulateMode` 不执行这一步。

### 6. Shutdown：底层资源系统真正的清理点

`Shutdown()` 会：

1. `Cancel + Dispose` 生命周期 `CancellationTokenSource`
2. 先释放 `AllOnLaunch` 接管的 WarmupGroup
3. `YooAssets.Destroy()`
4. 清空已加载 Manifest 包集合
5. 清空已注册包字典
6. 清空配置引用

这一步之后再调用加载 API，不再成立。

## 高价值 API 面

### 1. 启动与清单

- `Initialize(config)`
- `BootstrapAsync()`
- `LoadManifestAsync(package)`
- `RefreshManifestAsync(package)`

### 2. 补丁

- `HasPatchAsync(package)`
- `HasPatchByTagsAsync(tags, package)`
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

- `CreateWarmupAll(...)`
- `CreateWarmupByTags(...)`
- `CreateWarmupByLocations(...)`
- `CleanupAsync(package)`
- `ClearUnusedCacheAsync(package)`

## 关键状态

- `m_Config`：所有运行模式、热更地址 URL / 兜底 URL、热更策略的输入源
- `m_DefaultPackageName`：大部分加载 API 的真实目标包
- `m_Packages`：已注册的 YooAsset 包
- `m_ManifestLoadedPackages`：清单幂等集合
- `m_StartupWhitelistCheckedPackages / m_StartupWhitelistMatchedPackages`：本次进程的白名单检查与命中状态
- `m_Cts`：Manager 生命周期取消源
- `m_AllOnLaunchWarmupGroup`：WebGL AllOnLaunch 成功后持有到 Shutdown 的预热组

## 风险点 / 易错点

- `Initialize()` 不等于 `BootstrapAsync()`；只注入配置，不做包注册。
- `LoadManifestAsync()` 之前必须至少完成一次 `BootstrapAsync()`，否则包都还没注册。
- 非 WebGL HostPlayMode 的远端版本或 Manifest 请求失败时走三级回退链（已激活清单 → 本地可启动版本 → 内置清单）。本地记录位于 `persistentDataPath/Asset/{package}.version`，包含资源版本与文件名前缀，并会按当前启动 Tag 范围复核；首次安装无记录时自动降级内置清单，最终仍保持 HostPlayMode 的 Builtin + Sandbox 文件系统。WebGL Host 只有在探测到官方 Catalog 时才使用 WebServer + WebNetwork 并允许首包元数据回退，否则使用纯 WebNetwork；WebGL Offline 固定使用纯 WebServer，二者都不使用 Sandbox 本地版本回退。
- 大多数 `Load*` API 都默认走 `m_DefaultPackageName`；如果你以为它们支持多包透传，那是错的。
- Raw 文件内容应通过 `IRawFileHandle.GetBytes()` 获取；`FilePath` 是底层 bundle 路径，不能假定为可直接读取的原始文件路径。
- Editor 下用 Host/Offline PlayMode 跑真实包时，TMP 或普通材质出现洋红色块，优先检查 shader bundle 与当前 Editor 渲染端是否跨平台；AssetManager 会对已加载资源做同名 shader 重绑，但这只服务编辑器预览，不代表 Player 会走同一套修复路径。
- `CreateDownloaderByLocations()` 对空数组会直接抛异常；“整包下载”应该用 `CreateDownloader()`。
- `CreateDownloaderByLocations()` 遇到无效 location 会跳过并记 warning，不会整体失败。
- `ClearUnusedCacheAsync()` 需要当前 Manifest 已可用，否则“未使用”没有判定基准。
- `CreateWarmup*()` 同样要求目标 Package 已加载 Manifest；不要把它当作自动 Bootstrap 或自动 Manifest 加载 API。
- 标准未加密 AssetBundle 的 WebGL 请求可使用 Unity Web Cache；加密 AssetBundle、RawBundle、ArchiveBundle 走字节内存装载，Warmup 后不应承诺仍留在 Web Cache。
- `Shutdown()` 会 `YooAssets.Destroy()`；这是全局级清理，不能把它当成局部无害重置。

## 继续阅读

关键源码：

- [AssetManager.cs](../../../../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.cs)
- [AssetManager.Methods.cs](../../../../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.Methods.cs)
- [AssetManager.WebGLCatalog.cs](../../../../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.WebGLCatalog.cs)
- [AssetManager.Load.cs](../../../../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.Load.cs)
- [AssetManager.Cleanup.cs](../../../../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.Cleanup.cs)

相关文档：

- [IAssetManager.md](../Interfaces/IAssetManager.md)
- [AssetComponent.md](../../AssetComponent.md)
- [AssetManagerConfig.md](../Definitions/AssetManagerConfig.md)
- [IAssetHandle.md](../Interfaces/IAssetHandle.md)
- [IAssetDownloader.md](../Interfaces/IAssetDownloader.md)
- [IAssetWarmupGroup.md](../Interfaces/IAssetWarmupGroup.md)
- [WebGLAssetStrategies.md](../../WebGLAssetStrategies.md)
