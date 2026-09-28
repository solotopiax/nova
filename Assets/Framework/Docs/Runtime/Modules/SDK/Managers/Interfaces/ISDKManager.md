# ISDKManager

**类签名**：`public interface ISDKManager`  
**命名空间**：`NovaFramework.Runtime`

`ISDKManager` 定义 SDK 模块的公开编排契约：实例化插件、异步初始化、查询、生命周期广播与登录事件转发。

## 当前公开 API

```csharp
public interface ISDKManager
{
    void Initialize(SDKManagerConfig config);
    UniTask InitializeAsync(CancellationToken ct = default);
    UniTask DisposeAsync(CancellationToken ct = default);

    bool IsInitialized { get; }
    UniTask WaitForInitializedAsync(CancellationToken ct = default);
    SDKPluginInitializationState GetPluginInitializationState<TPlugin>() where TPlugin : class, ISDKPlugin;
    UniTask<SDKPluginInitializationState> WaitForPluginAsync<TPlugin>(CancellationToken ct = default) where TPlugin : class, ISDKPlugin;

    TPlugin Get<TPlugin>() where TPlugin : class, ISDKPlugin;
    bool TryGet<TPlugin>(out TPlugin plugin) where TPlugin : class, ISDKPlugin;
    IReadOnlyList<TInterface> GetAll<TInterface>() where TInterface : class, ISDKPlugin;

    void BroadcastPause(bool isPaused);
    void BroadcastFocus(bool hasFocus);
    void BroadcastQuit();

    void Login(string userId);
    void Login(string userId, IReadOnlyDictionary<string, object> userProperties);
    void EndLoginSession();
}
```

## 关键语义

- `Initialize(...)` 只负责读取 `PluginEntries` 元数据并缓存 Manager 依赖，不会实例化插件，也不会执行插件自己的 `InitializeAsync(...)`。
- `InitializeAsync(...)` 按 `ConfigMaster.EnabledSDKs` 实例化插件；无依赖插件并发启动，声明了能力依赖的插件等待提供者完成。`Priority` 只决定稳定遍历和释放顺序。
- 需要配置的插件通过 `RequiredConfigType` 向 `IConfigManager` 申请配置，不再通过 Manager 手写注入。
- `Get<T>()` / `TryGet<T>()` 既可以传具体插件类型，也可以传单实例接口类型，但只返回 `IsAvailable == true` 的插件。
- `GetAll<T>()` 返回所有实现指定接口且 `IsAvailable == true` 的插件，按 `ISDKPlugin.Priority` 升序。
- `WaitForPluginAsync<T>()` 只等待目标插件；返回 `Ready` 才表示可用。`WaitForInitializedAsync()` 等待所有已启用插件取得最终状态。
- `Login(uid, properties)` 保存属性快照，晚就绪插件会收到最新会话；切换账号再次调用，登出调用 `EndLoginSession()`。

## 使用顺序

```text
SDKComponent.Start()
  -> Manager.Initialize(new SDKManagerConfig { PluginEntries = ... })

首次访问 Nova.SDK.InitializeTask
  -> Manager.InitializeAsync(ct)

运行期
  -> Get<T>() / TryGet<T>() / GetAll<T>()
  -> BroadcastPause / BroadcastFocus / BroadcastQuit
  -> Login(userId)
```

## 使用示例

```csharp
await Nova.SDK.InitializeTask;

ISDKManager manager = Nova.SDK.SDKManager;

if (manager.TryGet<IAuthPlugin>(out var authPlugin) && !authPlugin.IsLoggedIn)
{
    AuthResult result = await authPlugin.LoginAsync("google", ct);
    if (result.Success)
    {
        manager.Login(result.UserId);
    }
}

foreach (ITrackPlugin tracker in manager.GetAll<ITrackPlugin>())
{
    tracker.TrackEvent("login_success", null);
}
```

## 关联文档

- [../Implements/SDKManagerBase.md](../Implements/SDKManagerBase.md)
- [../Implements/SDKManager.md](../Implements/SDKManager.md)
- [../Definitions/SDKManagerConfig.md](../Definitions/SDKManagerConfig.md)
- [../../SDKComponent.md](../../SDKComponent.md)
