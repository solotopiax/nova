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
public void Login(string userId, IReadOnlyDictionary<string, object> userProperties);
public void RegisterAndLogin(string userId);
public void EndLoginSession();
public SDKPluginInitializationState GetPluginInitializationState<TPlugin>() where TPlugin : class, ISDKPlugin;
public UniTask<SDKPluginInitializationState> WaitForPluginAsync<TPlugin>(CancellationToken ct = default) where TPlugin : class, ISDKPlugin;
```

## 当前初始化流程

1. `Awake()` 中创建 `m_SDKManager`。
2. `Start()` 中调用 `m_SDKManager.Initialize(...)` 缓存跨模块依赖：

```csharp
m_SDKManager.Initialize(new SDKManagerConfig { PluginEntries = m_PluginEntries });
```

3. 首次访问 `InitializeTask` 时会先确保 Manager 已缓存跨模块依赖，再创建一次 `m_SDKManager.InitializeAsync(GetCancellationTokenOnDestroy())`，并通过 `AsyncLazy` 的多等待者完成源共享结果。
4. `InitializeAsync` 内部按 `ConfigMaster.EnabledSDKs` 实例化启用插件；无依赖插件并发初始化，声明了能力依赖的插件只等待所需提供者。`PluginEntries` 不参与运行时启用或排序。
5. 初始化进行中可由多个调用方并发等待；初始化完成后也可重复等待同一结果，底层初始化始终只执行一次。

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

## 明确注册与登录

服务端明确创建新用户后调用 `Nova.SDK.RegisterAndLogin(uid)`；此入口按注册、登录顺序通知插件，包含一次 `Login`，调用方无需再调用 `Login(uid)`。同一 UID 重复调用也照常通知，调用方负责决定注册时机。既有用户仍调用 `Login(uid, properties)`，每次调用产生新会话，保留原有登录广播语义。

注册可早于插件就绪；只向晚就绪插件补交当前账号的注册和登录。登出或切换账号清理旧账号待补交注册。该入口不从微信校验结果猜测新用户。自定义 Manager 未实现可选 `ISDKRegistrationManager` 时，仅警告并保留 `Login`，不会伪造注册。

## 注意事项

- 插件启用与配置统一来自 `ConfigManager` / `ConfigMaster.EnabledSDKs`，`SDKComponent` 本身不承载配置注入逻辑。
- `m_PluginEntries` 仅保留 Inspector 选型元数据，不作为运行时启用、实例化或 Priority 来源。
- 后续去除组件侧配置状态时，依赖缓存、抢跑兜底和重复初始化保护应收敛到 `SDKManager` 自身保证。
- `InitializeTask` 在 `m_SDKManager == null` 时返回 `UniTask.CompletedTask`。
- `InitializeTask` 支持初始化中的并发等待与完成后的重复等待；失败后的再次等待取得原任务结果，不会自行创建一次新的 SDK 初始化。
- 只需要某个 SDK 时等待 `WaitForPluginAsync<T>()`，并确认结果为 `Ready`；全局 `InitializeTask` 仍等所有已启用 SDK 得出结果。
- 如果首次 DataMaster 拉取需要 `country_code` 等业务属性，登录成功后一次调用 `Login(uid, properties)`。先 `Login(uid)` 再取得插件写属性，不能保证属性赶上首次拉取。
- `[DisallowMultipleComponent]` 要求一个 GameObject 上最多只能挂一个 `SDKComponent`。

`Start` 只缓存依赖，不主动启动异步插件初始化。`WaitForPluginAsync<T>` 会先触发组件共享初始化任务，但需要检查返回状态是否为 Ready。组件销毁只清 lazy、配置标记和 Manager 引用，不在此正文等待释放；公开查询/登录薄委托大多没有 Manager null 防护，Awake 前或销毁后调用不能指望一律返回 false。PluginEntries 是 Inspector 元数据活列表的只读接口，不是配置快照或运行时启用凭据。

## 关联文档

- [Managers/Interfaces/ISDKManager.md](./Managers/Interfaces/ISDKManager.md)
- [Managers/Implements/SDKManager.md](./Managers/Implements/SDKManager.md)
- [Managers/Definitions/SDKManagerConfig.md](./Managers/Definitions/SDKManagerConfig.md)
- [Definitions/SDKPluginEntry.md](./Definitions/SDKPluginEntry.md)
- [ARCHITECTURE.md](./ARCHITECTURE.md)
