# ConfigComponent

`ConfigComponent` 是配置系统的场景入口，也是 `Nova.Config` 对应的 Unity 组件门面。  
它本身很薄，核心职责只有三件事：

- 反射创建 `IConfigManager`
- 把 Inspector 中的 `AssetLocation` 下发给 Manager
- 对外透传加载和配置查询入口

真正的加载与数据持有都在 `ConfigManager`。

## 什么时候先看这页

优先看这页的场景：

- 你要确认配置模块在场景里是怎么挂起来的。
- 你要看为什么 `Nova.Config.LoadAsync()` 没有在 `Start()` 自动执行。
- 你要判断某个配置属性是组件门面，还是 Manager 真正持有的数据。
- 你要排查 Inspector 中 `CurManagerTypeName / AssetLocation` 对运行时的影响。

如果你要看运行时真实数据源和幂等加载语义，继续看 [ConfigManager.md](ConfigManager.md)。

## 依赖与边界

### 它依赖什么

- `IConfigManager`
- `ConfigManagerConfig`
- `Util.TypeCreator`
- Inspector 序列化字段 `m_CurManagerTypeName`
- Inspector 序列化字段 `m_AssetLocation`

### 它对外暴露什么

- `LoadAsync()`
- `IsLoadOver`
- `DevelopMode / Common / Namespace`
- `Platform / Channel`
- `GameEntranceProcedureName`
- `AotMetadataDlls / GameDlls`
- `GetSDKPluginConfig*`
- `GetKitConfig*`
- `AllPluginConfigs`

### 它不负责什么

- 不负责真正加载 `ConfigRuntimeSO`
- 不负责缓存配置数据
- 不负责启动流程编排
- 不负责导出 `ConfigRuntimeSO`

## 核心流程

### Awake：反射创建 Manager

`Awake()` 做两件事：

1. `base.Awake()`
2. `Util.TypeCreator.Create<IConfigManager>(m_CurManagerTypeName)`

如果类型名无效，会直接抛出 `InvalidOperationException`。

### Start：只初始化，不加载

`Start()` 只调用：

- `m_ConfigManager.Initialize(new ConfigManagerConfig { AssetLocation = m_AssetLocation })`

这一步只是告诉 `ConfigManager` 去哪里加载运行时配置。  
它不会自动调用 `LoadAsync()`。

### LoadAsync：只是透传

`ConfigComponent.LoadAsync()` 没有自己的加载逻辑，直接透传给 `m_ConfigManager.LoadAsync()`。

这意味着：

- 幂等语义由 `ConfigManager` 提供
- 异常也由 `ConfigManager` 决定是否抛出
- `ProcedureLoadDll` 和业务层都可以复用同一个入口

## 高价值 API 面

### 1. 生命周期与入口

- `CurManagerTypeName`
- `LoadAsync()`
- `IsLoadOver`

### 2. 运行时公共配置

- `DevelopMode`
- `Common`
- `Namespace`
- `Platform`
- `Channel`
- `GameEntranceProcedureName`

### 3. HybridCLR 配置读取

- `AotMetadataDlls`
- `GameDlls`

### 4. SDK / Kit 查询

- `GetSDKPluginConfig<T>()`
- `GetSDKPluginConfig(Type type)`
- `GetKitConfig<T>()`
- `GetKitConfig(Type type)`
- `AllPluginConfigs`

## 关键状态

- `m_CurManagerTypeName`：控制反射创建哪种 `IConfigManager` 实现
- `m_AssetLocation`：`ConfigRuntimeSO` 的资源地址
- `m_ConfigManager`：真实配置实现
- `IsLoadOver`：只是 Manager 状态的外观透传

## 风险点 / 易错点

- `Start()` 不会自动加载配置；如果外层没显式调 `LoadAsync()`，大部分属性都只会返回空值或默认值。
- `CurManagerTypeName` 配错会在 `Awake()` 直接失败，不是延后到加载时才暴露。
- 组件上的属性基本都是门面；如果你要排查幂等、句柄释放、异常抛出，应该直接看 `ConfigManager`。
- `OnDestroy()` 这里只是把 `m_ConfigManager` 置空，不是配置资源真正释放点；真正释放在 `ConfigManager.Shutdown()`。
- `AllPluginConfigs` 适合运行时展示和遍历，不等于“所有导出配置”，它只反映当前已加载且启用的 SDK Plugin 配置。

## 继续阅读

关键源码：

- [ConfigComponent.cs](../../../../Scripts/Runtime/Modules/Config/ConfigComponent.cs)
- [ConfigComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Config/ConfigComponent.Visitors.cs)

相关文档：

- [ConfigManager.md](ConfigManager.md)
- [IConfigManager.md](Interfaces/IConfigManager.md)
- [ConfigRuntimeSO.md](ConfigRuntimeSO.md)
- [ConfigMasterSO.md](ConfigMasterSO.md)
- [ProcedureLoadDll.md](../Procedure/Procedures/ProcedureLoadDll.md)
