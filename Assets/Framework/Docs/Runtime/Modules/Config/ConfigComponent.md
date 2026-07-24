# ConfigComponent

`ConfigComponent` 是 `Nova.Config` 对应的 Runtime 组件门面。它负责创建 `IConfigManager`、下发 `ConfigManagerConfig`，并透传加载与读取入口。

公开配置属性：

- `Platform / Channel / DevelopMode`
- `AppConfigs / Namespace / HybridConfigs / CustomConfigs`
- SDK PluginConfig 与 Kit Config 查询

`Start()` 只执行 `Initialize(...)`，不会自动调用 `LoadAsync()`。加载、句柄持有与释放由 [ConfigManager.md](ConfigManager.md) 负责。

关键源码：[ConfigComponent.cs](../../../../Scripts/Runtime/Modules/Config/ConfigComponent.cs)、[ConfigComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Config/ConfigComponent.Visitors.cs)。
