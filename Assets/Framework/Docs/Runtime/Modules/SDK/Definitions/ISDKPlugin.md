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
  ├── IUserPropertyPlugin
  ├── ITrackPlugin
  ├── IMonetizeTrackPlugin
  ├── IAcquisitionTrackPlugin
  ├── IAttributionPlugin
  ├── IAuthPlugin
  ├── IAdPlugin
  ├── IPushPlugin
  ├── IRemoteConfigPlugin
  ├── IIAPPlugin
  └── IDeviceIdProvider
```

## 关键语义

- 本接口只声明能力，不执行初始化、可用性赋值、数据槽位管理或资源清理。下述调度规则由默认 `SDKManager` 提供；模板方法的具体行为见 [SDKPluginBase.md](./SDKPluginBase.md)，不能据接口声明推断任意自定义实现具有相同内部状态机。
- `Priority` 只决定稳定遍历与释放顺序。插件间初始化先后由 `ISDKInitializationDependencies` 声明；未声明的插件并发启动。
- `IsAvailable` 表示插件已成功初始化并可用。
- `FetchDataAsync()` 读取插件发布的数据槽位；key 约定见 [SDKDataKeys.md](./SDKDataKeys.md)。
- `ISDKLoginReceiver` 接收 UID；`ISDKLoginContextReceiver` 接收 UID 和属性快照；`ISDKLoginSessionEndReceiver` 在登出或切换账号时作废旧会话。异步工作须在提交结果前核对 `sessionId`。

- `ISDKRegistrationReceiver.OnSDKRegister(uid, sessionId)` 接收业务明确确认的注册。框架先发送注册再交付同会话登录，插件不得从首次登录推断注册。
- [IUserPropertyPlugin](../Plugins/Tracking/IUserPropertyPlugin.md) 是可选的一次性用户属性能力，当前由 TGA 实现，不改变 `ITrackPlugin.SetUserProperty` 的覆盖语义。

`SDKInitializationDependency` 是只读元数据：构造拒绝 null capabilityType，保存 Required，但不在构造时核对它是否为合法能力类型、是否已启用或有唯一提供者；这些由 Manager 建图检查。Name/配置 DisplayName 用于展示，不是运行时字符串查询键。

## 关联文档

- [Lifecycle.md](./Lifecycle.md)
- [SDKPluginBase.md](./SDKPluginBase.md)
- [ISDKPluginConfig.md](./ISDKPluginConfig.md)
- [SDKDataKeys.md](./SDKDataKeys.md)
- [../Managers/Interfaces/ISDKManager.md](../Managers/Interfaces/ISDKManager.md)
