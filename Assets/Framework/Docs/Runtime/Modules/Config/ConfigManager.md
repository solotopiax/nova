# ConfigManager

`ConfigManager` 是运行时配置的唯一实现。它的核心职责很明确：

- 通过 `IAssetManager` 异步加载 `ConfigRuntimeSO`
- 以 `m_Runtime` 为唯一数据源对外直读
- 向 SDK、Kit、Procedure 等上层模块暴露统一配置入口

它不做本地字段缓存，不把配置拆成一堆镜像副本，也不负责启动流程编排。

## 什么时候先看这页

优先看这页的场景：

- 你要确认运行时配置什么时候真正可用。
- 你要判断 `Nova.Config.LoadAsync()` 的幂等语义。
- 你要看 SDK / Kit / HybridCLR 依赖的数据是从哪里出来的。
- 你要排查为什么 `Namespace`、`GameEntranceProcedureName`、`AotMetadataDlls` 读不到。

如果你要看导出源头，继续看 [ConfigRuntimeSO.md](ConfigRuntimeSO.md) 和 [ConfigMasterSO.md](ConfigMasterSO.md)。  
如果你要看启动链路，继续看 [ProcedureLoadDll.md](../Procedure/Procedures/ProcedureLoadDll.md)。

## 依赖与边界

### 它依赖什么

- `IAssetManager`
- `ConfigRuntimeSO`
- `ConfigManagerConfig`

### 它对外暴露什么

- `LoadAsync()` 幂等加载
- `Common / Namespace / GameEntranceProcedureName`
- `AotMetadataDlls / GameDlls`
- SDK PluginConfig 查询
- Kit 配置查询

### 它不负责什么

- 不负责导出 `ConfigRuntimeSO`
- 不负责热更流程调度
- 不负责业务入口跳转
- 不负责 SDKManager 本身的初始化逻辑

## 核心流程

### Initialize：只记住配置入口，不做加载

`Initialize(ConfigManagerConfig config)` 做的是：

1. 校验 `config` 非空
2. 校验 `config.AssetLocation` 非空
3. 记录 `m_AssetLocation`
4. 从 `FrameworkManagersGroup` 获取 `IAssetManager`

这一步不触发资源加载，只是把“以后去哪里加载配置”这件事确定下来。

### LoadAsync：唯一加载入口，且是幂等的

`LoadAsync()` 的运行时语义：

- `m_IsLoadOver == true` 时直接返回
- 否则调用 `m_AssetManager.LoadAsync<ConfigRuntimeSO>(m_AssetLocation)`
- 句柄成功返回后持有到 `m_ConfigHandle`
- `m_Runtime = handle.Asset`
- 成功后把 `m_IsLoadOver` 置为 `true`

这意味着：

- `ConfigManager` 不会重复发起二次加载
- 后续所有读取都依赖 `m_Runtime`
- 只要 `ProcedureLoadDll` 已先加载一次，业务层再调用 `Nova.Config.LoadAsync()` 也是复用同一份结果

### Shutdown：配置对象的真正释放入口

`Shutdown()` 里最关键的一行是：

- `m_ConfigHandle?.Release()`

这一步不是可有可无的清理，而是让 `ConfigRuntimeSO` 引用计数归零的唯一入口。

## 高价值 API 面

### 1. 加载与状态

- `Initialize(ConfigManagerConfig config)`
- `LoadAsync()`
- `IsLoadOver`

### 2. 运行时公共配置读取

- `Common`
- `Namespace`
- `GameEntranceProcedureName`
- `DevelopMode`
- `Platform`
- `Channel`

### 3. HybridCLR 相关读取

- `AotMetadataDlls`
- `GameDlls`

### 4. SDK / Kit 查询

- `GetSDKPluginConfig<T>()`
- `GetSDKPluginConfig(Type type)`
- `GetAllPluginConfigs()`
- `GetKitConfig<T>()`
- `GetKitConfig(Type type)`

## 关键状态

- `m_AssetLocation`：决定 `ConfigRuntimeSO` 从哪里加载。
- `m_ConfigHandle`：句柄生命周期控制点，`Shutdown()` 时必须释放。
- `m_Runtime`：所有对外属性和查询的真实数据源。
- `m_IsLoadOver`：唯一幂等短路标志。
- `m_AssetManager`：资源加载入口，不在这里做其他资源逻辑。

## 风险点 / 易错点

- `LoadAsync()` 前读取 `Common / Namespace / GameEntranceProcedureName`，会拿到空或默认值。
- 这里没有本地字段缓存；如果你以为 `AppID`、AES 参数之类被拆到管理器字段里，那是旧认知。
- `ConfigManager` 不负责导出正确性；如果 `ConfigRuntimeSO` 本身导错了，这里只会忠实读取。
- `Shutdown()` 不释放句柄会导致引用计数无法归零。
- `ProcedureLoadDll` 直接依赖 `IConfigManager`，不是通过 `Nova.Config` 外观层调用。

## 继续阅读

关键源码：

- [ConfigManager.cs](../../../../Scripts/Runtime/Modules/Config/Managers/Implements/ConfigManager.cs)
- [ConfigManager.Visitors.cs](../../../../Scripts/Runtime/Modules/Config/Managers/Implements/ConfigManager.Visitors.cs)
- [IConfigManager.cs](../../../../Scripts/Runtime/Modules/Config/Managers/Interfaces/IConfigManager.cs)

相关文档：

- [IConfigManager.md](Interfaces/IConfigManager.md)
- [ConfigRuntimeSO.md](ConfigRuntimeSO.md)
- [ConfigMasterSO.md](ConfigMasterSO.md)
- [ConfigComponent.md](ConfigComponent.md)
- [ProcedureLoadDll.md](../Procedure/Procedures/ProcedureLoadDll.md)
