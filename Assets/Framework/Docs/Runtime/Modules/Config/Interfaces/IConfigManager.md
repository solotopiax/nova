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
    CustomConfig Custom { get; }

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

Custom 配置读取由 `Nova.Config.Custom` 的 `GetString / GetInt / GetFloat / GetBool` 提供，显式刷新由 `Nova.Config.RefreshAppConfigAsync` 提供；默认 `ConfigManager` 通过框架内部 `IAppConfigManager` 实现。公开 `IConfigManager` 不增加这些查询与刷新方法，只把本次功能开发中的旧 `CustomConfigs` 属性直接改为 `Custom`；自定义 Manager 若已接入旧开发版属性，需要同步改名，但不需要实现任何网络刷新或类型化 getter。未提供内部刷新能力时，刷新安全返回 `false`。

自动刷新使用框架内部 `INetworkReadySignal`，没有把等待方法加入公开 `INetworkManager`，因此项目已有自定义 NetworkManager 不会因接口扩张而编译失败；未实现内部信号时只跳过自动刷新。

关联文档：[ConfigManager.md](../ConfigManager.md)、[ConfigRuntimeSO.md](../ConfigRuntimeSO.md)。
