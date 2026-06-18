# Lifecycle 钩子接口

**命名空间**：`NovaFramework.Runtime`

`ISDKFocusListener`、`ISDKPauseListener`、`ISDKQuitListener` 当前都定义在 `Definitions/ISDKPlugin.cs` 同一文件中，不存在单独的 `Definitions/Lifecycle/*.cs` 目录。

## 当前接口

```csharp
public interface ISDKFocusListener
{
    void OnFocus(bool hasFocus);
}

public interface ISDKPauseListener
{
    void OnPause(bool isPaused);
}

public interface ISDKQuitListener
{
    void OnQuit();
}
```

## 调用来源

- `SDKComponent.OnApplicationFocus` -> `SDKManager.BroadcastFocus`
- `SDKComponent.OnApplicationPause` -> `SDKManager.BroadcastPause`
- `SDKComponent.OnApplicationQuit` -> `SDKManager.BroadcastQuit`

全部广播都在主线程同步执行。

## 关联文档

- [ISDKPlugin.md](./ISDKPlugin.md)
- [../SDKComponent.md](../SDKComponent.md)
- [../Managers/Interfaces/ISDKManager.md](../Managers/Interfaces/ISDKManager.md)
