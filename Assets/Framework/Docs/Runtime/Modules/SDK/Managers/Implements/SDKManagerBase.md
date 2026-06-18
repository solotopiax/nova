# SDKManagerBase

**类签名**：`internal abstract class SDKManagerBase : FrameworkManager, ISDKManager`  
**命名空间**：`NovaFramework.Runtime`

`SDKManagerBase` 只做两件事：固定 `Priority = 16`，并把 `ISDKManager` 与 `FrameworkManager` 需要的成员声明为抽象方法。

## 当前抽象成员

```csharp
public override int Priority => 16;

public abstract void Initialize(SDKManagerConfig config);
public abstract UniTask InitializeAsync(CancellationToken ct = default);
public abstract UniTask DisposeAsync(CancellationToken ct = default);
public abstract bool IsInitialized { get; }
public abstract UniTask WaitForInitializedAsync(CancellationToken ct = default);
public abstract TPlugin Get<TPlugin>() where TPlugin : class, ISDKPlugin;
public abstract bool TryGet<TPlugin>(out TPlugin plugin) where TPlugin : class, ISDKPlugin;
public abstract IReadOnlyList<TInterface> GetAll<TInterface>() where TInterface : class, ISDKPlugin;
public abstract void BroadcastPause(bool isPaused);
public abstract void BroadcastFocus(bool hasFocus);
public abstract void BroadcastQuit();
public abstract void Login(string userId);
public abstract override void Update();
public abstract override void Shutdown();
```

## 注意事项

- 该类本身不含任何实例字段和业务逻辑。
- 当前抽象成员只覆盖插件编排、查询、广播与关闭。
- `Priority = 16` 是当前代码事实；如果运行顺序发生调整，应同步修正相关 `Docs` 和 `Minds` 条目。

## 关联文档

- [./SDKManager.md](./SDKManager.md)
- [../Interfaces/ISDKManager.md](../Interfaces/ISDKManager.md)
