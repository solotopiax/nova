# Runtime/Modules

当前 `Runtime/Modules/` 的正式模块索引如下。

## 核心基类

| 文档 | 说明 |
|---|---|
| [FrameworkComponent.md](FrameworkComponent.md) | 所有组件基类与组件注册组 |
| [FrameworkManager.md](FrameworkManager.md) | 所有管理器基类与管理器调度组 |
| [Nova.md](Nova/Nova.md) | 框架根组件与静态入口 |

## 业务模块

| 模块 | 主文档 | 说明 |
|---|---|---|
| App | [AppComponent.md](App/AppComponent.md) | 版本检查与更新路由 |
| Asset | [AssetComponent.md](Asset/AssetComponent.md) | 资源加载、下载、场景加载 |
| Config | [ConfigComponent.md](Config/ConfigComponent.md) | 运行时配置访问与导出物读取 |
| Prefab | [PrefabComponent.md](Prefab/PrefabComponent.md) | Prefab 实例化与回收 |
| Event | [EventComponent.md](Event/EventComponent.md) | 事件系统 |
| Table | [TableComponent.md](Table/TableComponent.md) | 表格系统 |
| Localization | [LocalizationComponent.md](Localization/LocalizationComponent.md) | 文本与字体本地化 |
| UI | [UIComponent.md](UI/UIComponent.md) | UI 打开、关闭与分组管理 |
| Network | [NetworkComponent.md](Network/NetworkComponent.md) | DoH / HTTP / Net / WebSocket |
| Procedure | [ProcedureComponent.md](Procedure/ProcedureComponent.md) | 启动流程与状态切换 |
| ObjectPool | [ObjectPoolComponent.md](ObjectPool/ObjectPoolComponent.md) | 对象池 |
| Persist | [PersistComponent.md](Persist/PersistComponent.md) | 持久化能力 |
| Sound | [SoundComponent.md](Sound/SoundComponent.md) | 声音系统 |
| Vibrate | [VibrateComponent.md](Vibrate/VibrateComponent.md) | 振动系统 |
| SDK | [SDKComponent.md](SDK/SDKComponent.md) | SDK 插件系统 |
| Debug | [DebugComponent.md](Debug/DebugComponent.md) | 调试系统 |

## 模块内的高频补充文档

- Asset：`IAssetManager`、各类 Handle 接口、`AssetManagerConfig`
- Config：`ConfigRuntimeSO`、`ConfigMasterSO`、`PlatformChannelEntry`
- Procedure：`BuiltInProcedures`、`LauncherSettings`、`ProcedureManager`
- SDK：`ARCHITECTURE.md`、`INDEX.md`
- Persist：`PlayerPrefsManager`、`SQLiteManager`、`FileFragmentManager`

## 已废弃的旧认知

旧版模块命名、旧式资源加载器名与旧目录层次已经全部退出当前模块索引。如果在其他文档里仍看到这类名称，应视为历史残留而不是现行事实。
