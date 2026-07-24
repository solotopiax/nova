# ConfigManager

`ConfigManager` 是 Runtime 配置实现：通过 `IAssetManager` 加载 `ConfigRuntimeSO`，以 `m_Runtime` 为唯一数据源，并在 `Shutdown()` 释放资源句柄。

它直接转发：

- `Platform / Channel / DevelopMode`
- `AppConfigs / Namespace / HybridConfigs / CustomConfigs`
- SDK PluginConfig 与 Kit Config 查询

它不读取或持有 YooAsset、HybridCLR 构建路径、`link.xml`、CDN 部署等 Editor 数据。

`LoadAsync()` 以 `m_IsLoadOver` 保证幂等；加载前配置属性返回空值或默认值。

关联文档：[IConfigManager.md](Interfaces/IConfigManager.md)、[ConfigRuntimeSO.md](ConfigRuntimeSO.md)、[ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)。
