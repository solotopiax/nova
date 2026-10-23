# IPrivacyConfigProvider

`NovaFramework.Runtime.IPrivacyConfigProvider` 是独立包读取已加载隐私快照的通用能力接口，属于主框架 `NovaFramework.Runtime` 程序集。

```csharp
public interface IPrivacyConfigProvider
{
    PrivacyConfigs PrivacyConfigs { get; }
}
```

默认 `ConfigManagerBase` 实现此接口，`ConfigManager` 仅在配置加载完整提交后返回当前 `PrivacyConfigs`；加载前或关闭后返回 `null`。扩展包可通过 `FrameworkManagersGroup.GetManager<IPrivacyConfigProvider>()` 获取该能力，等快照可用后读取自己的 `ExtensionConfigsJson` 键。

此接口独立于 `IConfigManager`，已有自定义 `IConfigManager` 无需增加成员。希望向可选扩展提供隐私快照的自定义 Manager 可以另外实现此接口；未提供时，扩展不能把尚不可用的配置当作已加载快照。

关联文档：[PrivacyConfigs.md](../PrivacyConfigs.md)、[IConfigManager.md](IConfigManager.md)、[ConfigManager.md](../ConfigManager.md)。
