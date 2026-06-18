# SDKComponent

**类签名**：`[DisallowMultipleComponent] public sealed partial class SDKComponent : FrameworkComponent`  
**命名空间**：`NovaFramework.Runtime`  
**全局访问**：`Nova.SDK`

`SDKComponent` 是 SDK 模块的场景入口，负责创建 `ISDKManager`、传入插件条目，并把 Unity 生命周期转发给插件系统。

## 文件拆分

| 文件 | 说明 |
|---|---|
| `SDKComponent.cs` | `Awake` / `Start` / `OnDestroy` 和对外薄委托 API |
| `SDKComponent.Visitors.cs` | 序列化字段、只读属性、`InitializeTask` |
| `SDKComponent.Methods.cs` | `GetOrCreateInitializeTask()` |
| `SDKComponent.Lifecycle.cs` | `OnApplicationPause` / `OnApplicationFocus` / `OnApplicationQuit` 转发 |

## 当前公开成员

```csharp
public string CurManagerTypeName { get; }
public IReadOnlyList<SDKPluginEntry> PluginEntries { get; }
public ISDKManager SDKManager { get; }
public UniTask InitializeTask { get; }
public bool IsInitialized { get; }

public TPlugin Get<TPlugin>() where TPlugin : class, ISDKPlugin;
public bool TryGet<TPlugin>(out TPlugin plugin) where TPlugin : class, ISDKPlugin;
public IReadOnlyList<TInterface> GetAll<TInterface>() where TInterface : class, ISDKPlugin;
public void Login(string userId);
```

## 当前初始化流程

1. `Awake()` 中创建 `m_SDKManager`。
2. `Start()` 中调用：

```csharp
m_SDKManager.Initialize(new SDKManagerConfig { PluginEntries = m_PluginEntries });
```

3. 首次访问 `InitializeTask` 时，调用 `m_SDKManager.InitializeAsync(GetCancellationTokenOnDestroy())`。
4. 之后重复访问 `InitializeTask` 会返回同一个缓存任务。

## 生命周期代理

- `OnApplicationPause(bool)` → `BroadcastPause(bool)`
- `OnApplicationFocus(bool)` → `BroadcastFocus(bool)`
- `OnApplicationQuit()` → `BroadcastQuit()`

如果 `m_SDKManager == null`，这些代理会直接返回。

## 使用示例

```csharp
await Nova.SDK.InitializeTask;

if (Nova.SDK.TryGet<IAdPlugin>(out var adPlugin))
{
    AdLoadResult result = await adPlugin.RequestAsync(AdFormat.Rewarded, ct: ct);
    if (result.Success && adPlugin.IsReady(AdFormat.Rewarded))
    {
        await adPlugin.ShowAsync(AdFormat.Rewarded, ct: ct);
    }
}

foreach (ITrackPlugin tracker in Nova.SDK.GetAll<ITrackPlugin>())
{
    tracker.TrackEvent("startup", null);
}
```

## 注意事项

- 插件配置统一来自 `ConfigManager`，`SDKComponent` 本身不承载配置注入逻辑。
- `InitializeTask` 在 `m_SDKManager == null` 时返回 `UniTask.CompletedTask`。
- `[DisallowMultipleComponent]` 要求一个 GameObject 上最多只能挂一个 `SDKComponent`。

## 关联文档

- [Managers/Interfaces/ISDKManager.md](./Managers/Interfaces/ISDKManager.md)
- [Managers/Implements/SDKManager.md](./Managers/Implements/SDKManager.md)
- [Managers/Definitions/SDKManagerConfig.md](./Managers/Definitions/SDKManagerConfig.md)
- [Definitions/SDKPluginEntry.md](./Definitions/SDKPluginEntry.md)
- [ARCHITECTURE.md](./ARCHITECTURE.md)
