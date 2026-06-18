# SDKPluginBase

**类签名**：`public abstract class SDKPluginBase : ISDKPlugin`  
**命名空间**：`NovaFramework.Runtime`

`SDKPluginBase` 是所有纯 C# SDK 插件的通用基类，负责三件事：生命周期模板、配置类型声明、数据槽位通信。

## 当前关键成员

```csharp
public bool IsAvailable { get; }
public abstract string Name { get; }
public virtual int Priority => 100;
public Type RequiredConfigType => ConfigType;

public UniTask InitializeAsync(ISDKPluginConfig config, CancellationToken ct);
public UniTask DisposeAsync(CancellationToken ct);
public UniTask<object> FetchDataAsync(string key, CancellationToken ct = default);

protected virtual Type ConfigType => null;
protected abstract UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct);
protected abstract UniTask OnDisposeAsync(CancellationToken ct);
protected void PublishData(string key, object value);
```

## 当前语义

- `InitializeAsync(...)` 成功后由基类把 `IsAvailable` 置为 `true`。
- `DisposeAsync(...)` 无论是否异常，都会把 `IsAvailable` 置为 `false`，并取消所有等待中的数据槽位。
- `ConfigType` 返回插件需要的配置类型；`RequiredConfigType` 只是对外公开这一结果。
- `FetchDataAsync` / `PublishData` 用于插件之间的 key-value 异步通信。

## 数据槽位模型

内部维护两组字典：

- `m_DataStore`：已发布的数据
- `m_PendingAwaiters`：正在等待某个 key 的调用方

行为：

- 数据已存在时，`FetchDataAsync` 立即返回。
- 数据不存在时，调用方挂起等待。
- `PublishData` 写入后会唤醒当前所有等待者。
- 插件释放时，所有仍在等待的调用方都会被取消。

## 使用示例

```csharp
public sealed class ExampleDeviceIdPlugin : SDKPluginBase, IDeviceIdProvider
{
    public override string Name => "ExampleDevice";
    protected override Type ConfigType => typeof(ExampleDeviceConfig);

    protected override async UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
    {
        var typed = (ExampleDeviceConfig)config;
        await UniTask.SwitchToMainThread(ct);
        PublishData(SDKDataKeys.FirebasePushToken, typed.MockToken);
    }

    protected override UniTask OnDisposeAsync(CancellationToken ct)
    {
        return UniTask.CompletedTask;
    }

    public string GetDeviceID()
    {
        return "device-id";
    }
}
```

## 关联文档

- [./ISDKPlugin.md](./ISDKPlugin.md)
- [./ISDKPluginConfig.md](./ISDKPluginConfig.md)
- [./SDKDataKeys.md](./SDKDataKeys.md)
- [./PluginBase.md](./PluginBase.md)
