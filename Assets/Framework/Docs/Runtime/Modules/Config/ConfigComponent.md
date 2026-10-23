# ConfigComponent

`ConfigComponent` 是 `Nova.Config` 对应的 Runtime 组件门面。它负责创建 `IConfigManager`、下发 `ConfigManagerConfig`，并透传加载与读取入口。

公开配置属性：

- `Platform / Channel / DevelopMode`
- `AppConfigs / Namespace / HybridConfigs / Custom`
- SDK PluginConfig 与 Kit Config 查询
- `Custom.GetString / GetInt / GetFloat / GetBool`：按 JSONPath 读取嵌套对象与数组路径；远端优先，本地其次，最后使用调用方默认值
- `RefreshAppConfigAsync`：显式拉取一轮 GM 后台应用配置

`Start()` 只执行 `Initialize(...)`，不会自动调用 `LoadAsync()`。`LoadAsync()` 恢复本地默认值与磁盘缓存后立即完成，并在后台等待 Network 路由就绪后自动刷新一次；不会阻塞现有启动流程，也不要求项目 Procedure 增加调用。

属性读取不触发加载或刷新。未创建 Manager 时，Platform/Channel 为 `None`，DevelopMode 为 `Debug`，IsLoadOver 为 `false`，配置引用与 Namespace 为 null，AllPluginConfigs 为空数组；默认 Manager 已创建但未加载时 Namespace 为 `string.Empty`。这些回退值不是就绪证据。配置对象及 AllPluginConfigs 的只读接口指向当前 SO 的原对象/活列表，不做深复制；读取列表不会初始化 SDK。`PrivacyConfigs` 通过可选 `IPrivacyConfigProvider` 读取，默认实现仅完整加载后暴露；自定义 Manager 未实现该接口时返回 null。`OnDestroy()` 只清组件 Manager 引用，正式关闭和句柄归还由框架管理器链执行。

关键源码：[ConfigComponent.cs](../../../../Scripts/Runtime/Modules/Config/ConfigComponent.cs)、[ConfigComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Config/ConfigComponent.Visitors.cs)。
