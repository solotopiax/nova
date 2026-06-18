# Framework 架构总览

本文只描述 **当前代码事实**。  
历史决策、演进原因、术语边界请转到 `Assets/Framework/Minds/`。

## 总体结构

Nova 当前是“`Nova` 根组件 + 多个 `FrameworkComponent` 子模块 + 多个 `FrameworkManager` 纯 C# 管理器”的三层结构：

- `Nova` 是场景中的根组件，负责收集并暴露全局静态入口。
- 各 `*Component` 负责 Unity 侧序列化字段、生命周期接线和对外薄代理。
- 各 `*Manager` / `*ManagerBase` 承担真正的模块逻辑，并由 `FrameworkManagersGroup` 按 `Priority` 调度 `Update()` / `Shutdown()`。

## 全局入口

当前 `Nova` 暴露的静态入口见 `Assets/Framework/Scripts/Runtime/Modules/Nova/Nova.Visitors.cs`：

| 静态入口 | 对应组件 | 说明 |
|---|---|---|
| `Nova.App` | `AppComponent` | 版本检查、更新路由 |
| `Nova.Asset` | `AssetComponent` | 资源加载、下载、场景加载 |
| `Nova.Config` | `ConfigComponent` | 运行时配置访问 |
| `Nova.Prefab` | `PrefabComponent` | Prefab 实例化与回收 |
| `Nova.Event` | `EventComponent` | 事件系统 |
| `Nova.Table` | `TableComponent` | 表格系统 |
| `Nova.Localization` | `LocalizationComponent` | 多语言与字体适配 |
| `Nova.UI` | `UIComponent` | UI 打开、关闭、分组管理 |
| `Nova.Network` | `NetworkComponent` | DoH / HTTP / Net / WebSocket |
| `Nova.Procedure` | `ProcedureComponent` | 流程系统 |
| `Nova.ObjectPool` | `ObjectPoolComponent` | 对象池 |
| `Nova.Persist` | `PersistComponent` | PlayerPrefs / SQLite / FileFragment |
| `Nova.Sound` | `SoundComponent` | 声音系统 |
| `Nova.Vibrate` | `VibrateComponent` | 振动系统 |
| `Nova.SDK` | `SDKComponent` | SDK 插件装配与初始化 |
| `Nova.Debug` | `DebugComponent` | 调试窗口与诊断能力 |

当前运行时入口已经收敛到现行 `Nova.*` 模块集合，旧版热更入口、旧式资源加载器入口与旧式 Prefab 混合入口均不再存在。

## 关键边界

### Asset 与 Prefab 已分层

- `AssetComponent` / `IAssetManager` 负责资源句柄、场景句柄、原始文件、批量下载。
- `PrefabComponent` / `IPrefabManager` 负责实例化、回收和 Prefab 生命周期。
- Prefab 不再是旧式“资源加载器的一部分”，而是独立模块。

### Procedure 与 HybridCLR 仍是启动链路的一部分

- 热更 DLL 加载发生在 `ProcedureLoadDll`。
- `ProcedureHotfix` 现在是流程名，不是独立 `Hotfix` 模块。
- 业务入口 Procedure 由 `ConfigRuntimeSO.GameEntranceProcedureName` 与 `ProcedureManager` 配合决定。

### SDK 走插件列表装配

- `SDKComponent` 持有 `SDKPluginEntry` 列表。
- `SDKManager` 根据启用项、优先级和配置类型初始化插件。
- SDK 配置来自 `ConfigManager` / `ConfigRuntimeSO`，不是手写字典。

## Manager Priority（当前代码）

以下优先级来自各 `*ManagerBase.Priority`：

| 模块 | Priority |
|---|---:|
| Persist | 0 |
| Debug | 0 |
| Event | 1 |
| ObjectPool | 2 |
| Asset | 4 |
| Procedure | 6 |
| Localization | 6 |
| UI | 7 |
| Http | 8 |
| WebSocket | 9 |
| Config | 10 |
| Network | 10 |
| Prefab | 10 |
| App | 11 |
| DoH | 11 |
| Table | 14 |
| SDK | 16 |
| Vibrate | 18 |
| Sound | 19 |

说明：

- 同优先级模块的相对顺序不应在文档里假定，除非代码明确约束。
- `NetworkComponent` 内部有 `DoHManager`、`HttpManager`、`NetworkManager`、`WebSocketManager` 四条子链路。

## Editor 侧扩展模型

- Runtime 模块的 Inspector 统一继承 `BaseComponentInspector`。
- `BaseComponentInspector` 负责基础 Inspector 生命周期、编译状态处理、`serializedObject.Update()` 与最终刷新。
- `IEditorRuntimeDrawer` 是**可选扩展接口**，由具体 Inspector 自行决定是否维护列表并在运行时调用；它不是基类统一调度契约。

## 当前架构的几个高频结论

- `Nova.*` 是全局访问外观层，不是业务逻辑真正实现层。
- `Component` 负责 Unity 入口，`Manager` 负责模块逻辑。
- Asset/Prefab、Procedure/HybridCLR、Config/SDK 是当前最容易跨模块联动的三组边界。
- 如果要回答“为什么这样设计”，请去 `Minds`；如果要回答“现在代码怎么实现”，以 `Docs + 源码` 为准。
