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

    TPlugin Get<TPlugin>() where TPlugin : class, ISDKPlugin;
    bool TryGet<TPlugin>(out TPlugin plugin) where TPlugin : class, ISDKPlugin;
    IReadOnlyList<TInterface> GetAll<TInterface>() where TInterface : class, ISDKPlugin;

    void BroadcastPause(bool isPaused);
    void BroadcastFocus(bool hasFocus);
    void BroadcastQuit();

    void Login(string userId);
}
```

## 关键语义

- `Initialize(...)` 只负责根据 `PluginEntries` 反射实例化插件并建立索引，不会执行插件自己的 `InitializeAsync(...)`。
- `InitializeAsync(...)` 按 `Priority` 升序分桶，同一优先级并行初始化。
- 需要配置的插件通过 `RequiredConfigType` 向 `IConfigManager` 申请配置，不再通过 Manager 手写注入。
- `Get<T>()` / `TryGet<T>()` 既可以传具体插件类型，也可以传单实例接口类型。
- `GetAll<T>()` 返回所有实现指定接口且 `IsAvailable == true` 的插件，按 `Priority` 升序。

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
