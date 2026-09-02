# AssetComponent

`AssetComponent` 是资源系统的场景入口，也是 `Nova.Asset` 对应的 Unity 组件门面。  
它本身不做资源加载实现，主要负责两件事：

- 反射创建 `IAssetManager`
- 把 Inspector 上的资源系统配置下发给 Manager

真正的包注册、清单加载、句柄返回、下载器创建，都在 `AssetManager`。

## 什么时候先看这页

优先看这页的场景：

- 你要确认资源系统的 Inspector 配置是怎么进入运行时的。
- 你要区分 `BootstrapAsync`、`LoadManifestAsync`、`LoadAsync` 分别属于哪一层。
- 你要排查热更相关开关为什么会影响启动流程。
- 你要判断某个 API 是组件门面，还是 Manager 真正实现。

如果你要看资源系统真实运行逻辑，继续看 [AssetManager.md](AssetManager/Implements/AssetManager.md)。

## 依赖与边界

### 它依赖什么

- `IAssetManager`
- `AssetManagerConfig`
- `Util.TypeCreator`
- Inspector 序列化字段

### 它对外暴露什么

- 启动资源系统入口
- 清单加载与补丁检查入口
- 下载器创建入口
- 各类 `Load*` / `Preload*` / `Cleanup*` 门面

### 它不负责什么

- 不负责真正注册 YooAsset 包
- 不负责句柄生命周期管理
- 不负责资源下载执行
- 不负责资源实例化

## 核心流程

### Awake：反射创建 Manager

`Awake()` 会：

1. `base.Awake()`
2. `Util.TypeCreator.Create<IAssetManager>(m_CurAssetManagerTypeName)`

如果类型名无效，会直接抛 `InvalidOperationException`。

### Start：把 Inspector 配置打包成 `AssetManagerConfig`

`Start()` 不会触达底层资源框架，只会把这些配置注入到 `AssetManager`：

- PlayMode
- Packages / DefaultPackageName
- 热更总开关、启动白名单与下载参数
- 当前节点 `DevelopMode` 对应的 Host / Fallback URL 模板
- 解密器类型
- 启动期 tag 下载配置

也就是说，`Start()` 只是“配置注入”，不是“资源系统启动”。

### 运行时调用仍然是薄透传

这些 public API 都只是门面：

- `BootstrapAsync()`
- `LoadManifestAsync()`
- `HasPatchAsync()`
- `HasPatchByTagsAsync()`
- `CreateDownloader*()`
- `Load*()`
- `PreloadAsync()`
- `CleanupAsync()`
- `RefreshManifestAsync()`
- `ClearUnusedCacheAsync()`

真正的时序、幂等、异常和句柄语义都由 `AssetManager` 决定。

## 高价值配置面

### 1. 运行模式

- `m_EditorPlayMode`
- `m_RuntimePlayMode`
- `m_Packages`
- `m_DefaultPackageName`

### 2. 启动热更策略

- `EnableHotfix`
- `m_EnableStartupWhitelist`
- `m_StartupWhitelistUrlDebug` / `m_StartupWhitelistUrlFallbackDebug`
- `m_StartupWhitelistUrlRelease` / `m_StartupWhitelistUrlFallbackRelease`
- `m_StartupWhitelistMetadataRootUrlDebug` / `m_StartupWhitelistMetadataRootUrlFallbackDebug`
- `m_StartupWhitelistMetadataRootUrlRelease` / `m_StartupWhitelistMetadataRootUrlFallbackRelease`
- `m_AutoHotfix`
- `QuitOnFailedOrCancel`
- `MaxDownloadConcurrency`
- `FallbackRoundCount`
- `RetryDownloadCount`
- `PreferLastSuccessfulHost`
- `EnableUWRTracks`
- `LaunchHotfixTags`
- `AutoClearUnusedCacheOnHotfix`

### 3. 远端与解密

- `m_HostServerUrlDebug`
- `m_HostServerUrlFallbackDebug`
- `m_HostServerUrlRelease`
- `m_HostServerUrlFallbackRelease`
- `m_DecryptorType`

## 风险点 / 易错点

- `Start()` 之后资源系统并没有真正启动；如果没显式调用 `BootstrapAsync()`，包还没注册好。
- 所有 `Load*` 方法返回的 Handle 都需要调用方显式释放；这不是组件层自动兜底的事。
- `EnableHotfix`、`RuntimePlayMode`、热更地址 URL 这些配置不会在运行时二次推导；进入 `AssetManagerConfig` 的就是当前节点 `DevelopMode` 已选定的那一组事实。
- `EnableHotfix` 现在只控制资源热更检查 / 下载链路，不再决定 App 大版本检测是否执行。
- 启动白名单还受 `EnableHotfix` 和有效 Host/Web 模式约束；默认关闭，当前 DevelopMode 没有可用白名单文件或元数据根 URL 时自动跳过。
- 首次正常启动没有 DeviceID 缓存时不会请求白名单；SDK 初始化后写入 `persistentDataPath/Asset/asset-check-device-id.dat`，后续启动才参与当次判断。
- URL 中若包含 `{Platform}` / `{Channel}` / `{Package}` / `{Version}`，仍由 Asset 模块在运行时替换；其中 `{Platform}` = Player 编译宏对应的 `PlatformType` 枚举名（不读取 Editor Active BuildTarget 或 ConfigMaster），`{Channel}` = Config 导出时同步到 `AssetComponent` 的渠道快照，`{Package}` = 当前资源包名，`{Version}` = `Application.version`。
- `{Channel}` 快照与同次导出的 `ConfigRuntimeSO.Channel` 同源，但在资源 Bootstrap 前即可读取，避免 Asset 反向依赖尚未加载的运行时配置。
- `OnDestroy()` 这里只是把 `m_AssetManager` 置空，不是底层资源系统真正销毁点；真正销毁在 `AssetManager.Shutdown()`。
- `AssetComponent` 只负责资源系统，不负责 Prefab / UI / Config 等上层消费模块。
- `RetryDownloadCount` 是完整走完所有候选和 `FallbackRoundCount` 后的下载重试次数；每次重试重新执行全部轮次，最大物理尝试数为 `C × R × (K + 1)`，不是每个域名单独只试 K 次。
- 每个文件独立冻结候选计划；最近成功域名只调整后续新文件的起点，整条失败链不会清除已有偏好。

## 继续阅读

关键源码：

- [AssetComponent.cs](../../../../Scripts/Runtime/Modules/Asset/AssetComponent.cs)
- [AssetComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Asset/AssetComponent.Visitors.cs)

相关文档：

- [AssetManager.md](AssetManager/Implements/AssetManager.md)
- [IAssetManager.md](AssetManager/Interfaces/IAssetManager.md)
- [AssetManagerConfig.md](AssetManager/Definitions/AssetManagerConfig.md)
- [IAssetHandle.md](AssetManager/Interfaces/IAssetHandle.md)
- [IAssetDownloader.md](AssetManager/Interfaces/IAssetDownloader.md)
