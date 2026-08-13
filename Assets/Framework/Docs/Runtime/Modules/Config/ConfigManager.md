# ConfigManager

`ConfigManager` 是 Runtime 配置实现：通过 `IAssetManager` 加载 `ConfigRuntimeSO`，恢复应用配置磁盘快照，并在 `Shutdown()` 释放资源句柄与取消后台等待。

它公开：

- `Platform / Channel / DevelopMode`
- `AppConfigs / Namespace / HybridConfigs / Custom`
- SDK PluginConfig 与 Kit Config 查询
- `Custom.GetString / GetInt / GetFloat / GetBool` 与 `RefreshAppConfigAsync`

它不读取或持有 YooAsset、HybridCLR 构建路径、`link.xml`、CDN 部署等 Editor 数据。

`LoadAsync()` 以 `m_IsLoadOver` 保证幂等。加载 ConfigRuntimeSO 后，先使用独立 `PrivacyConfigs` 初始化 `Util.Encrypt.AES` 默认 Key/IV，再建立本地默认快照并恢复 `persistentDataPath/Nova/Config/app-config.json`；之后才标记 Config 可用。`Shutdown()` 会同步清空 AES 默认密钥，避免禁用 Domain Reload 时残留旧配置。

远端 `PbNetAppCustomConfigResp.value` 必须是以 object 为根的完整 JSON，可以包含嵌套对象、数组以及本地未声明的路径。成功响应完整替换远端快照；磁盘使用同目录 `.tmp` 加原子替换。网络失败、未配置或自定义 NetworkManager 没有就绪信号时保留当前值；JSON/协议错误记录 Error，同样不阻塞启动。

业务通过 `Nova.Config.Custom` 按 JSONPath 读取。路径优先查询远端快照，远端缺失或非 null 值转换失败时回退 `ConfigRuntimeSO.Custom` 本地字符串，再失败时返回调用方默认值；远端显式 `null` 直接返回调用方默认值。基础类型使用固定区域格式，布尔值支持 `true/false` 与 GM 常用的 `1/0`。

关联文档：[IConfigManager.md](Interfaces/IConfigManager.md)、[ConfigRuntimeSO.md](ConfigRuntimeSO.md)、[ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)。
