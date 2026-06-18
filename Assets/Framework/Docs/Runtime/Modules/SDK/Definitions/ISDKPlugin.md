# ISDKPlugin

**类签名**：`public interface ISDKPlugin`  
**命名空间**：`NovaFramework.Runtime`

SDK 插件根契约。所有插件由 `SDKManager` 管理生命周期，可选实现 Focus / Pause / Quit 三类广播接口。

## 当前公开 API

```csharp
public interface ISDKPlugin
{
    string Name { get; }
    int Priority { get; }
    bool IsAvailable { get; }

    UniTask InitializeAsync(ISDKPluginConfig config, CancellationToken ct);
    UniTask DisposeAsync(CancellationToken ct);
    UniTask<object> FetchDataAsync(string key, CancellationToken ct = default);
}
```

## 当前家族接口

```text
ISDKPlugin
  ├── ITrackPlugin
  ├── IMonetizeTrackPlugin
  ├── IAttributionPlugin
  ├── IAuthPlugin
  ├── IAdPlugin
  ├── IPushPlugin
  ├── IRemoteConfigPlugin
  └── IDeviceIdProvider
```

## 关键语义

- `Priority` 越小越早初始化。
- `IsAvailable` 表示插件已成功初始化并可用。
- `FetchDataAsync()` 读取插件发布的数据槽位；key 约定见 [SDKDataKeys.md](./SDKDataKeys.md)。

## 关联文档

- [Lifecycle.md](./Lifecycle.md)
- [SDKPluginBase.md](./SDKPluginBase.md)
- [ISDKPluginConfig.md](./ISDKPluginConfig.md)
- [SDKDataKeys.md](./SDKDataKeys.md)
- [../Managers/Interfaces/ISDKManager.md](../Managers/Interfaces/ISDKManager.md)
