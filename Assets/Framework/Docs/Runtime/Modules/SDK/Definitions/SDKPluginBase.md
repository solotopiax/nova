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
public static bool TryGetRequiredConfigType(Type pluginType, out Type requiredConfigType);

public UniTask InitializeAsync(ISDKPluginConfig config, CancellationToken ct);
public UniTask DisposeAsync(CancellationToken ct);
public UniTask<object> FetchDataAsync(string key, CancellationToken ct = default);

protected virtual Type ConfigType => null;
protected abstract UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct);
protected abstract UniTask OnDisposeAsync(CancellationToken ct);
protected void PublishData(string key, object value);
```

## 当前语义

- `InitializeAsync(...)` 先等待派生类 `OnInitializeAsync`，再检查传入令牌，均完成后才把 `IsAvailable` 置为 `true`。基类不自动切回主线程，派生实现必须在完成前自行切回。
- 此模板没有重复或并发初始化门禁，也不在入口把既有可用状态重置为 `false`。首次失败不会置为可用；已经成功的实例再次初始化失败时，不能据此推断旧 `IsAvailable` 自动归零。正常使用应由 Manager 管理实例与初始化任务。
- `DisposeAsync(...)` 无论是否异常，都会把 `IsAvailable` 置为 `false`，并取消所有等待中的数据槽位。
- `ConfigType` 返回已构造插件需要的配置类型；`RequiredConfigType` 只是对外公开这一结果。
- Manager 与 Inspector 不会构造候选插件来读取 `RequiredConfigType`，而是通过 `TryGetRequiredConfigType(...)` 读取静态类型元数据。
- 需要配置的新插件优先继承 `PluginBase<TConfig>`；无法迁移基类的兼容插件必须标注 `[SDKPluginConfigType(typeof(MyConfig))]`。
- 未声明上述任一静态元数据的插件会被诊断并跳过，避免未启用 SDK 的构造函数或字段初始化触发原生副作用。
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

同一 key 共享一个完成源，已发布值允许为 null；key 为 null 或空串会被拒绝。调用方取消只结束自己的等待，不移除共享槽位；全部调用方取消后若再无 Publish，槽位仍保留至释放。Publish 覆盖缓存并在锁外唤醒等待者，已经返回的消费者不会自动收到新值。释放在锁内摘取等待者并清两组字典，随后锁外取消；这些锁只保护数据槽位。Fetch/Publish 不检查可用或永久 disposed 状态，晚到 Publish 可以再次写入，派生类仍须结束原生回调和生产任务。

静态配置解析优先查 `PluginBase<TConfig>` 继承链，再查具体类型上不继承的 `SDKPluginConfigTypeAttribute`；特性构造只存 Type，解析仅核对 `ISDKPluginConfig` 可赋值性，不等于具体类型可构造或已启用。强类型桥接不接受 null/错误配置，抛 `InvalidCastException`；token 的主动检查与线程切换不能从抽象声明推得。

## 使用示例

```csharp
[SDKPluginConfigType(typeof(ExampleDeviceConfig))]
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
