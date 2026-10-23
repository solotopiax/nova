# ConfigManager

`ConfigManager` 是 Runtime 配置实现：通过 `IAssetManager` 加载 `ConfigRuntimeSO`，恢复应用配置磁盘快照，并在 `Shutdown()` 释放资源句柄与取消后台等待。

它公开：

- `Platform / Channel / DevelopMode`
- `AppConfigs / Namespace / HybridConfigs / Custom`
- SDK PluginConfig 与 Kit Config 查询
- `Custom.GetString / GetInt / GetFloat / GetBool` 与 `RefreshAppConfigAsync`

它不读取或持有 YooAsset、HybridCLR 构建路径、`link.xml`、CDN 部署等 Editor 数据。

`LoadAsync()` 以 `m_IsLoadOver` 保证幂等。Editor 会在幂等短路前检查当前项目固定 Privacy AES 维护标记；有效未结束、损坏或无法核实的标记均先阻断，不加载配置或沿用旧默认密钥。门禁仅 `UNITY_EDITOR` 编译，Player 不读取项目维护目录。加载 ConfigRuntimeSO 后先验证 Platform 与 Channel 均为已定义的非 `None` 值；无效时释放句柄并中断启动。验证通过后使用独立 `PrivacyConfigs` 初始化 `Util.Encrypt.AES` 默认 Key/IV，再建立本地默认快照并恢复 `persistentDataPath/Config/app-custom-config.json`；之后才标记 Config 可用。配置成功提交后，通过 `ConfigManagerBase` 实现的通用 `IPrivacyConfigProvider.PrivacyConfigs` 提供快照给独立扩展；ConfigManager 不直接初始化或依赖 MemoryProtection。该可选包自行读取一次配置并管理轮询与退出，见 [MemoryProtection 包文档](../../../../../../UPMPackages/com.solotopia.nova.framework.memory.protection/Nova/Docs/MemoryProtection.md)。`Shutdown()` 释放 ConfigRuntimeSO 句柄与内部资源；默认 AES 密钥由 `FrameworkManagersGroup` 在全部 Manager 逆序 Shutdown 完成后的 `finally` 中统一清空，避免禁用 Domain Reload 时残留上一次运行的默认凭据。

远端 `PbNetAppCustomConfigResp.value` 必须是以 object 为根的完整 JSON，可以包含嵌套对象、数组以及本地未声明的路径。成功响应完整替换远端快照；磁盘使用同目录 `.tmp` 加原子替换。网络失败、未配置或自定义 NetworkManager 没有就绪信号时保留当前值；JSON/协议错误记录 Error，同样不阻塞启动。

`LoadAsync()` 尚未完成时，并发调用共享本轮完成源和唯一资源加载；失败后的下一次调用重新加载。`Shutdown()` 或重新初始化会取消旧调用的等待，旧资源请求若随后返回，只归还它的句柄，不提交到新生命周期。成功提交的句柄由管理器持有，仍在 `Shutdown()` 唯一释放；未提交的句柄在加载流程退出前归还。Editor 隐私维护门继续在每次调用与幂等短路前执行。

生命周期替换先摘下旧加载完成源并建立新 CTS，再调用旧 CTS 的 `Cancel()`、`Dispose()` 和旧等待者的取消通知，防止同步取消回调重入后被旧尾部状态覆盖。`Shutdown()` 同样先清字段，句柄释放放在 `try` 中，取消通知放在 `finally` 中。这里没有逐步异常隔离：旧 CTS 的取消回调若抛异常，后续 `Dispose()` 和等待者通知仍可能被跳过；句柄释放异常也会向上抛出。重新 `Initialize` 不清已加载标记或已持有配置，不是完整卸载后重载；上述共享加载策略只描述默认实现与主线程调用，不承诺任意线程并发安全。

应用配置拉取成功后只输出一条 `应用配置拉取成功：{PbNetAppCustomConfigResp Proto JSON}`；不输出请求开始、指令名称、配置名称、键数量或通用网络成功包装日志。

业务通过 `Nova.Config.Custom` 按 JSONPath 读取。路径优先查询远端快照，远端缺失或非 null 值转换失败时回退 `ConfigRuntimeSO.Custom` 本地字符串，再失败时返回调用方默认值；远端显式 `null` 直接返回调用方默认值。基础类型使用固定区域格式，布尔值支持 `true/false` 与 GM 常用的 `1/0`。

`RefreshAppConfigAsync()` 在尚未加载时返回 `false`；进行中的调用共享本轮完成源。发起者捕获取消和请求异常后通常返回 `false`，但等待共享完成源的调用者仍可能收到 `Shutdown()` 发出的取消异常。`true` 只表示新远端树已应用到内存：磁盘写入失败会告警，仍继续应用，下一次启动可能恢复旧缓存。自动刷新标志在开始等待 Network 时就置位；等待失败不会自动重试，需显式刷新或进入新的管理器生命周期。

加载 SO 的代际校验不能推广到远端刷新：刷新请求没有向 `NetService` 传生命周期 token，响应后只检查**当前** CTS 是否存在且未取消，没有核对发起时的 CTS。关闭后重新初始化期间，旧请求若迟到，可能面对新快照继续执行；关闭也不等待底层网络完成。需要账号或配置代际隔离的调用方不能把此入口视作取消后绝无写入的保证。

缓存仅按固定文件内的 `Name` 与当前 `CustomName` 做 ordinal 精确匹配，不含过期时间、平台或 AppID 隔离。`CustomName` 不参与文件路径拼接。`AppConfigDiskCache` 同步读取完整文本；写入用固定的 `path + ".tmp"`，没有并发文件锁，失败时只尽力删除临时文件。解析、内存替换和磁盘提交各有自己的失败边界，不构成跨进程事务。

对应权威协议为 `pb_net_app_custom_config.proto`：请求 `head=1/key=2`，key 取 CustomName；响应 `head=1/value=2`，value 是上述完整 JSON 字符串。CustomConfigCmdName 解析指令行，这两个配置项都非空白才读取缓存或自动刷新；检查只判断形态，不 Trim、不证明 NetCmd 或后台项实际存在。

关联文档：[IConfigManager.md](Interfaces/IConfigManager.md)、[ConfigRuntimeSO.md](ConfigRuntimeSO.md)、[ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)。
