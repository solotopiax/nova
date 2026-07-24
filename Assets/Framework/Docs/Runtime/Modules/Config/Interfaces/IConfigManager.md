# IConfigManager

`IConfigManager` 是 Runtime 配置读取契约。`LoadAsync()` 完成后，公开配置面如下：

```csharp
public interface IConfigManager
{
    bool IsLoadOver { get; }
    PlatformType Platform { get; }
    ChannelType Channel { get; }
    DevelopMode DevelopMode { get; }
    AppConfigs AppConfigs { get; }
    string Namespace { get; }
    HybridConfigs HybridConfigs { get; }
    CustomConfigs CustomConfigs { get; }

    void Initialize(ConfigManagerConfig config);
    UniTask LoadAsync();
    T GetSDKPluginConfig<T>() where T : class, ISDKPluginConfig;
    ISDKPluginConfig GetSDKPluginConfig(Type type);
    T GetKitConfig<T>() where T : class, IKitConfig;
    IKitConfig GetKitConfig(Type type);
    IReadOnlyCollection<ISDKPluginConfig> GetAllPluginConfigs();
}
```

Editor-only 的 `YooAssetEditorConfigs`、`HybridEditorConfigs`、`CDNEditorConfigs` 不属于此接口，也不会由 Runtime 组件持有或透传。

关联文档：[ConfigManager.md](../ConfigManager.md)、[ConfigRuntimeSO.md](../ConfigRuntimeSO.md)。
