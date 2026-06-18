# ISDKPluginConfig

**类签名**：`public interface ISDKPluginConfig`  
**命名空间**：`NovaFramework.Runtime`

`ISDKPluginConfig` 是 SDK 插件配置接口。当前代码中它不是空 marker，而是要求每个配置对象提供 `DisplayName`。

## 当前定义

```csharp
public interface ISDKPluginConfig
{
    string DisplayName { get; }
}
```

## 当前职责

- 作为 SDK 插件配置对象的统一类型约束。
- 供 `ConfigWindow` 左树显示中文配置名称。
- 被 `SDKManager` 通过 `IConfigManager.GetSDKPluginConfig(requiredConfigType)` 拉取并注入插件。

## 使用示例

```csharp
public sealed class ExampleSDKConfig : ISDKPluginConfig
{
    public string DisplayName => "示例 SDK";
    public string AppId;
    public string AppKey;
}
```

## 关联文档

- [./ISDKPlugin.md](./ISDKPlugin.md)
- [./SDKPluginBase.md](./SDKPluginBase.md)
- [../Managers/Implements/SDKManager.md](../Managers/Implements/SDKManager.md)
