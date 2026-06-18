# Framework Runtime

**程序集**：`NovaFramework.Runtime.asmdef`  
**命名空间**：`NovaFramework.Runtime`

## 入口说明

`Runtime/` 只记录当前运行时代码事实：

- `Core/`：通用基础设施，如集合、FSM、Log、Reference、Table、Path、Extensions。
- `Modules/`：框架模块层，包含 `Nova`、各 `FrameworkComponent`、各 `FrameworkManager` 及其定义类型。

当前 `Docs` 中不再把 `Tmp/` 当成 Runtime 正式子模块入口。

## 模块导航

| 模块 | 全局入口 | 主文档 |
|---|---|---|
| Nova | `Nova.Self` | [Nova.md](Modules/Nova/Nova.md) |
| App | `Nova.App` | [AppComponent.md](Modules/App/AppComponent.md) |
| Asset | `Nova.Asset` | [AssetComponent.md](Modules/Asset/AssetComponent.md) |
| Config | `Nova.Config` | [ConfigComponent.md](Modules/Config/ConfigComponent.md) |
| Prefab | `Nova.Prefab` | [PrefabComponent.md](Modules/Prefab/PrefabComponent.md) |
| Event | `Nova.Event` | [EventComponent.md](Modules/Event/EventComponent.md) |
| Table | `Nova.Table` | [TableComponent.md](Modules/Table/TableComponent.md) |
| Localization | `Nova.Localization` | [LocalizationComponent.md](Modules/Localization/LocalizationComponent.md) |
| UI | `Nova.UI` | [UIComponent.md](Modules/UI/UIComponent.md) |
| Network | `Nova.Network` | [NetworkComponent.md](Modules/Network/NetworkComponent.md) |
| Procedure | `Nova.Procedure` | [ProcedureComponent.md](Modules/Procedure/ProcedureComponent.md) |
| ObjectPool | `Nova.ObjectPool` | [ObjectPoolComponent.md](Modules/ObjectPool/ObjectPoolComponent.md) |
| Persist | `Nova.Persist` | [PersistComponent.md](Modules/Persist/PersistComponent.md) |
| Sound | `Nova.Sound` | [SoundComponent.md](Modules/Sound/SoundComponent.md) |
| Vibrate | `Nova.Vibrate` | [VibrateComponent.md](Modules/Vibrate/VibrateComponent.md) |
| SDK | `Nova.SDK` | [SDKComponent.md](Modules/SDK/SDKComponent.md) |
| Debug | `Nova.Debug` | [DebugComponent.md](Modules/Debug/DebugComponent.md) |

## 说明

- `Prefab` 当前有独立全局入口 `Nova.Prefab`。
- `Asset` 负责资源句柄与下载，`Prefab` 负责实例化与对象生命周期。
- `ProcedureHotfix` 是流程文档，不代表存在独立的 `Hotfix` 模块。

更多导航见 [Modules.md](Modules/Modules.md)。
