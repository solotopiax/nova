# SDKComponent

**类签名**：`[DisallowMultipleComponent] public sealed partial class SDKComponent : FrameworkComponent`  
**命名空间**：`NovaFramework.Runtime`  
**全局访问**：`Nova.SDK`

`SDKComponent` 是 SDK 模块的场景入口，负责创建 `ISDKManager`、暴露 `InitializeTask`，并把 Unity 生命周期转发给插件系统。SDK 插件的运行时启用、实例化与配置注入均由 `SDKManager` 基于 `ConfigMaster.EnabledSDKs` 处理，`SDKComponent` 不再承担运行时插件选型职责。

## 文件拆分

| 文件 | 说明 |
|---|---|
| `SDKComponent.cs` | `Awake` / `Start` / `OnDestroy` 和对外薄委托 API |
| `SDKComponent.Visitors.cs` | 序列化字段、只读属性、`InitializeTask` |
| `SDKComponent.Methods.cs` | `GetOrCreateInitializeTask()`，负责缓存单次初始化任务 |
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
2. `Start()` 中调用 `m_SDKManager.Initialize(...)` 缓存跨模块依赖：

```csharp
m_SDKManager.Initialize(new SDKManagerConfig { PluginEntries = m_PluginEntries });
```

3. 首次访问 `InitializeTask` 时会先确保 Manager 已缓存跨模块依赖，再创建并缓存一次 `m_SDKManager.InitializeAsync(GetCancellationTokenOnDestroy())` 任务。
4. `InitializeAsync` 内部按 `ConfigMaster.EnabledSDKs` 实例化启用插件，并按 `ISDKPlugin.Priority` 初始化；`PluginEntries` 不参与运行时启用或排序。
5. 之后重复访问 `InitializeTask` 会返回同一个缓存任务。

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

- 插件启用与配置统一来自 `ConfigManager` / `ConfigMaster.EnabledSDKs`，`SDKComponent` 本身不承载配置注入逻辑。
- `m_PluginEntries` 仅保留 Inspector 选型元数据，不作为运行时启用、实例化或 Priority 来源。
- 后续去除组件侧配置状态时，依赖缓存、抢跑兜底和重复初始化保护应收敛到 `SDKManager` 自身保证。
- `InitializeTask` 在 `m_SDKManager == null` 时返回 `UniTask.CompletedTask`。
- `[DisallowMultipleComponent]` 要求一个 GameObject 上最多只能挂一个 `SDKComponent`。

## 关联文档

- [Managers/Interfaces/ISDKManager.md](./Managers/Interfaces/ISDKManager.md)
- [Managers/Implements/SDKManager.md](./Managers/Implements/SDKManager.md)
- [Managers/Definitions/SDKManagerConfig.md](./Managers/Definitions/SDKManagerConfig.md)
- [Definitions/SDKPluginEntry.md](./Definitions/SDKPluginEntry.md)
- [ARCHITECTURE.md](./ARCHITECTURE.md)
