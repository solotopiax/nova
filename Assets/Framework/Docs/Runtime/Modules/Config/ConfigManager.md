# ConfigManager

`ConfigManager` 是 Runtime 配置实现：通过 `IAssetManager` 加载 `ConfigRuntimeSO`，恢复应用配置磁盘快照，并在 `Shutdown()` 释放资源句柄与取消后台等待。

它公开：

- `Platform / Channel / DevelopMode`
- `AppConfigs / Namespace / HybridConfigs / Custom`
- SDK PluginConfig 与 Kit Config 查询
- `Custom.GetString / GetInt / GetFloat / GetBool`、`WaitForStartupAppConfigAsync` 与 `RefreshAppConfigAsync`

它不读取或持有 YooAsset、HybridCLR 构建路径、`link.xml`、CDN 部署等 Editor 数据。

`LoadAsync()` 以 `m_IsLoadOver` 保证幂等。Editor 会在幂等短路前检查当前项目固定 Privacy AES 维护标记；有效未结束、损坏或无法核实的标记均先阻断，不加载配置或沿用旧默认密钥。门禁仅 `UNITY_EDITOR` 编译，Player 不读取项目维护目录。加载 ConfigRuntimeSO 后先验证 Platform 与 Channel 均为已定义的非 `None` 值；无效时释放句柄并中断启动。验证通过后使用独立 `PrivacyConfigs` 初始化 `Util.Encrypt.AES` 默认 Key/IV，再建立本地默认快照并恢复 `persistentDataPath/Config/app-custom-config.json`；之后才标记 Config 可用。配置成功提交后，通过 `ConfigManagerBase` 实现的通用 `IPrivacyConfigProvider.PrivacyConfigs` 提供快照给独立扩展；ConfigManager 不直接初始化或依赖 MemoryProtection。该可选包自行读取一次配置并管理轮询与退出，见 MemoryProtection 包文档（包 `com.solotopia.nova.framework.memory.protection`，包内 [按包定位：Nova/Docs/MemoryProtection.md](../SDK/INDEX.md#sdk--kit-包文档发现)）。`Shutdown()` 释放 ConfigRuntimeSO 句柄与内部资源；默认 AES 密钥由 `FrameworkManagersGroup` 在全部 Manager 逆序 Shutdown 完成后的 `finally` 中统一清空，避免禁用 Domain Reload 时残留上一次运行的默认凭据。

远端 `PbNetAppCustomConfigResp.value` 必须是以 object 为根的完整 JSON，可以包含嵌套对象、数组以及本地未声明的路径。成功响应完整替换远端快照；磁盘使用同目录 `.tmp` 加原子替换。网络失败、未配置或自定义 NetworkManager 没有就绪信号时保留当前值；JSON/协议错误记录 Error。显式等待启动拉取的流程会等本轮结束，失败后仍可继续启动；`Config.LoadAsync()` 自身不等待远端。

`LoadAsync()` 尚未完成时，并发调用共享本轮完成源和唯一资源加载；失败后的下一次调用重新加载。`Shutdown()` 或重新初始化会取消旧调用的等待，旧资源请求若随后返回，只归还它的句柄，不提交到新生命周期。成功提交的句柄由管理器持有，仍在 `Shutdown()` 唯一释放；未提交的句柄在加载流程退出前归还。Editor 隐私维护门继续在每次调用与幂等短路前执行。

生命周期替换先摘下旧加载完成源并建立新 CTS，再调用旧 CTS 的 `Cancel()`、`Dispose()` 和旧等待者的取消通知，防止同步取消回调重入后被旧尾部状态覆盖。重新 `Initialize` 时旧 CTS 的取消回调若抛异常，其后的 `Dispose()` 和加载等待者通知仍可能被跳过。`Shutdown()` 同样先清字段，句柄释放放在 `try` 中，取消通知放在 `finally` 中；取消回调异常也会继续进入内层 `finally`，释放 CTS 并结束启动等待，句柄或取消回调异常仍向上传播。重新 `Initialize` 不清已加载标记、已持有配置或启动结果，不是完整卸载后重载；上述共享策略只描述默认实现与主线程调用，不承诺任意线程并发安全。

应用配置拉取成功后只输出一条 `应用配置拉取成功：{PbNetAppCustomConfigResp Proto JSON}`；不输出请求开始、指令名称、配置名称、键数量或通用网络成功包装日志。

业务通过 `Nova.Config.Custom` 按 JSONPath 读取。路径优先查询远端快照，远端缺失或非 null 值转换失败时回退 `ConfigRuntimeSO.Custom` 本地字符串，再失败时返回调用方默认值；远端显式 `null` 直接返回调用方默认值。基础类型使用固定区域格式，布尔值支持 `true/false` 与 GM 常用的 `1/0`。

`WaitForStartupAppConfigAsync(CancellationToken)` 只等待本次启动的独立完成源；该源在后台等待 Network 前建立，成功、失败、未配置或缺少就绪信号均固化为最终结果，并一直保留到 `Shutdown()`。重复等待、预加载重试和后续手动刷新不会替换或重启它。Config 尚未加载时返回 `false`，但不会提前固化未来的启动结果。调用者 token 只取消自己的等待，框架关闭则以 `false` 结束共享启动等待。调用顺序必须是先确认 `Nova.Network.LoadAsync()` 成功，再等待启动配置；否则未加载路由时仍会等待就绪信号。

`RefreshAppConfigAsync()` 在尚未加载时返回 `false`；进行中的调用共享本轮完成源，完成后清空，后续显式调用仍可重新请求。发起者捕获取消和请求异常后通常返回 `false`，但等待共享刷新完成源的调用者仍可能收到 `Shutdown()` 发出的取消异常。`true` 只表示新远端树已应用到内存：磁盘写入失败会告警，仍继续应用，下一次启动可能恢复旧缓存。启动层不会因失败自动再次发起一轮；每轮内部的主备与重试仍服从 Network Inspector 配置。

远端刷新捕获发起时的 CTS 和快照，并把生命周期 token 传入 `NetService`。响应返回后同时核对取消状态、CTS 与快照身份，防止关闭、重新初始化后的旧响应写入新快照。关闭只发出取消并解除等待，不等待底层传输退出；这也不表示服务端没有处理已发出的请求。

启动后台任务在 Network 路由就绪后，从内部 `INetworkReadySignal.StartupAppConfigRequestTimeout` 读取单次请求超时，来源为 Network Inspector 的 `HttpSettings.StartupAppConfigRequestTimeout`，默认 5 秒。该值通过 `NetService` 的内部超时重载传入 `PostBusinessRawDataAsync`，由底层为每次主备请求与重试分别计时；Config 不创建整轮总计时器。整轮耗时可能超过 5 秒，全部候选和重试结束后才固化最终结果，启动层不会另发新一轮。

内部刷新合流时沿用先发起请求的单次超时设置，共享该轮完成结果，不重新计时或额外取消已有请求。core 把发起生命周期的取消传给 `NetService`，在网络返回后检查取消、生命周期和快照身份，关闭或重新初始化后迟到的响应不能写入内存或缓存。调用者取消启动等待只影响自身，后续手动刷新也不改写已经保存的启动结果。

独立显式 `RefreshAppConfigAsync()` 传入单次请求参数 `-1f`，沿用 `HttpSettings.RequestTimeout`，默认每次请求 60 秒；其他业务调用也保持此行为。公开 `NetService.SendAsync` 三参数和四参数重载保持签名兼容，启动拉取专用超时通过框架内部五参数重载传递。

缓存仅按固定文件内的 `Name` 与当前 `CustomName` 做 ordinal 精确匹配，不含过期时间、平台或 AppID 隔离。`CustomName` 不参与文件路径拼接。`AppConfigDiskCache` 同步读取完整文本；写入用固定的 `path + ".tmp"`，没有并发文件锁，失败时只尽力删除临时文件。解析、内存替换和磁盘提交各有自己的失败边界，不构成跨进程事务。

对应权威协议为 [pb_net_app_custom_config.proto](../../../../Protos/pb_net_app_custom_config.proto)：请求 `head=1/key=2`，key 取 CustomName；响应 `head=1/value=2`，value 是上述完整 JSON 字符串。CustomConfigCmdName 解析指令行，这两个配置项都非空白才读取缓存或自动刷新；检查只判断形态，不 Trim、不证明 NetCmd 或后台项实际存在。

关联文档：[IConfigManager.md](Interfaces/IConfigManager.md)、[ConfigRuntimeSO.md](ConfigRuntimeSO.md)、[ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)。
