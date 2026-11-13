# ConfigComponent

`ConfigComponent` 是 `Nova.Config` 对应的 Runtime 组件门面。它负责创建 `IConfigManager`、下发 `ConfigManagerConfig`，并透传加载与读取入口。

公开配置属性：

- `Platform / Channel / DevelopMode`
- `AppConfigs / Namespace / HybridConfigs / Custom`
- SDK PluginConfig 与 Kit Config 查询
- `Custom.GetString / GetInt / GetFloat / GetBool`：按 JSONPath 读取嵌套对象与数组路径；远端优先，本地其次，最后使用调用方默认值
- `RefreshAppConfigAsync`：显式拉取一轮 GM 后台应用配置
- `WaitForStartupAppConfigAsync(CancellationToken)`：等待本次启动自动拉取的最终结果，重复等待不再发请求；只取消当前调用者的等待

`Start()` 只执行 `Initialize(...)`，不会自动调用 `LoadAsync()`。`LoadAsync()` 恢复本地默认值与磁盘缓存后立即完成，并在后台等待 Network 路由就绪后自动刷新一次；本地配置加载不会等待远端网络。已有项目不增加调用也会自动拉取；需要在后续步骤使用本轮云端结果时，先成功加载网络表，再显式等待启动拉取。MainDemo 及其他 Demo 的标准预加载均在网络步骤接入这一等待，失败或超时仍沿用缓存、本地默认值继续启动，八步顺序和各 Demo 的 SDK 分支保持原样。

```csharp
bool networkReady = await Nova.Network.LoadAsync();
if (!networkReady)
    throw new InvalidOperationException("网络配置加载失败。");

bool refreshed = await Nova.Config.WaitForStartupAppConfigAsync(cancellationToken);
// true 表示本次启动取得的新远端配置已应用到内存；false 时继续使用当前配置。
```

启动结果在本次加载期间保留到 `Shutdown()`，不因成功、失败、后续手动刷新或预加载重试而清空。等待入口不会启动、重试请求；尚未加载 Config、未配置远端名称或自定义 Manager 不支持该能力时返回 `false`。不要用 `RefreshAppConfigAsync()` 代替这个等待：显式刷新在上一轮结束后会再次请求。

启动自动拉取使用 Network Inspector「Http 管理 → 启动时GM配置请求超时时间（秒）」，默认每次请求 5 秒。主地址、备用地址及重试的每次请求分别计时，单次超时后继续执行既有主备和重试策略；整轮全部失败才返回 `false`。它不限制整轮总等待时间，总耗时会随候选数量和重试次数增加。重复等待复用本轮最终结果，不会重发。设置来源及合法范围见 [HttpSettings](../Network/Definitions/HttpSettings.md)。

独立发起的手动刷新和普通业务请求仍使用「业务请求超时时间（秒）」，默认每次请求 60 秒。启动自动拉取与手动刷新并发合流时，等待已有请求完成，沿用先发起请求的单次超时设置。离开流程时传入的 token 只取消当前调用者，框架关闭则结束共享拉取。

属性读取不触发加载或刷新。未创建 Manager 时，Platform/Channel 为 `None`，DevelopMode 为 `Debug`，IsLoadOver 为 `false`，配置引用与 Namespace 为 null，AllPluginConfigs 为空数组；默认 Manager 已创建但未加载时 Namespace 为 `string.Empty`。这些回退值不是就绪证据。配置对象及 AllPluginConfigs 的只读接口指向当前 SO 的原对象/活列表，不做深复制；读取列表不会初始化 SDK。`PrivacyConfigs` 通过可选 `IPrivacyConfigProvider` 读取，默认实现仅完整加载后暴露；自定义 Manager 未实现该接口时返回 null。`OnDestroy()` 只清组件 Manager 引用，正式关闭和句柄归还由框架管理器链执行。

关键源码：[ConfigComponent.cs](../../../../Scripts/Runtime/Modules/Config/ConfigComponent.cs)、[ConfigComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Config/ConfigComponent.Visitors.cs)。
