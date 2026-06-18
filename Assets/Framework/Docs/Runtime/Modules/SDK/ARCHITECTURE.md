# SDK 模块架构总览

`SDK` 模块负责把第三方能力收口为一套稳定的 Nova 插件接口。本文只描述当前代码中的结构事实。

## 核心结构

### 1. 场景入口

- `SDKComponent`

职责：

- 在 `Awake` 中通过 `Util.TypeCreator.Create<ISDKManager>()` 创建 Manager。
- 在 `Start` 中把 Inspector 序列化的 `PluginEntries` 传给 `ISDKManager.Initialize(...)`。
- 对外暴露 `InitializeTask`、`Get`、`TryGet`、`GetAll`、`Login`。
- 把 `OnApplicationPause` / `OnApplicationFocus` / `OnApplicationQuit` 转发给 Manager。

### 2. 管理器层

- `ISDKManager`
- `SDKManagerBase`
- `SDKManager`

职责：

- 按 `SDKPluginEntry` 反射实例化启用的插件。
- 按 `Priority` 升序分桶初始化，同桶并行。
- 通过 `IConfigManager.GetSDKPluginConfig(requiredConfigType)` 给需要配置的插件注入配置。
- 统一处理可用性、失败隔离、生命周期广播与登录事件转发。

### 3. 插件契约层

- 根接口：`ISDKPlugin`
- 可选生命周期接口：`ISDKPauseListener`、`ISDKFocusListener`、`ISDKQuitListener`
- 基类：`SDKPluginBase`、`PluginBase<TConfig>`

当前插件家族：

- `IAuthPlugin`
- `IAdPlugin`
- `ITrackPlugin`
- `IMonetizeTrackPlugin`
- `IAttributionPlugin`
- `IPushPlugin`
- `IRemoteConfigPlugin`
- `IDeviceIdProvider`

### 4. 配置与条目层

- `ISDKPluginConfig`
- `SDKPluginEntry`
- `SDKManagerConfig`

职责：

- `SDKPluginEntry` 负责 Inspector 中的启用状态、类型名和优先级。
- `SDKManagerConfig` 只承载 `PluginEntries` 列表。
- `ISDKPluginConfig` 负责插件配置对象统一类型约束，实际配置由 `ConfigManager` 提供。

### 5. 数据与事件层

- `SDKDataKeys`
- `ObservableEvent<T>`
- `StickyEvent<T>`
- `ReplayEvent<T>`

职责：

- 为插件之间的数据槽位通信和事件回放提供通用基础设施。

## 当前初始化链路

1. `SDKComponent.Awake()` 创建 `ISDKManager`。
2. `SDKComponent.Start()` 调用 `Initialize(new SDKManagerConfig { PluginEntries = m_PluginEntries })`。
3. 首次访问 `InitializeTask` 时，`SDKComponent` 调用 `InitializeAsync(ct)`。
4. `SDKManager` 读取每个插件的 `RequiredConfigType`，再从 `IConfigManager` 拉取配置并注入。
5. 初始化完成后，业务层通过 `Nova.SDK.Get<T>()` / `TryGet<T>()` / `GetAll<T>()` 访问能力。

## 关键边界

- `SDKComponent` 只做入口与生命周期代理，不承载任何第三方 SDK 业务逻辑。
- `SDKManager` 只编排插件，不暴露厂商 SDK 类型。
- 插件接口表达的是 Nova 认可的能力边界，不是厂商原始 API 的逐项镜像。
- 当前结构只保留本文列出的插件家族、配置链路与目录划分。

## 相关阅读

- [INDEX.md](./INDEX.md)
- [SDKComponent.md](./SDKComponent.md)
- [Definitions/ISDKPlugin.md](./Definitions/ISDKPlugin.md)
- [Definitions/SDKPluginBase.md](./Definitions/SDKPluginBase.md)
- [Managers/Interfaces/ISDKManager.md](./Managers/Interfaces/ISDKManager.md)
- [Managers/Implements/SDKManager.md](./Managers/Implements/SDKManager.md)
