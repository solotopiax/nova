# SDKManager

**类签名**：`internal sealed partial class SDKManager : SDKManagerBase`  
**命名空间**：`NovaFramework.Runtime`

`SDKManager` 是 `ISDKManager` 的唯一实现，负责插件实例化、初始化编排、查询、生命周期广播和登录事件转发。

## 文件拆分

| 文件 | 说明 |
|---|---|
| `SDKManager.cs` | 公开 override：`Initialize`、`InitializeAsync`、`DisposeAsync`、`Get`、`TryGet`、`GetAll`、`Broadcast*`、`Login`、`Update`、`Shutdown` |
| `SDKManager.Visitors.cs` | 内部字段与属性 |
| `SDKManager.Methods.cs` | `InstantiateEntry`、`InitializePluginAsync`、`GroupByPriority` 等私有方法 |

## 当前关键字段

| 字段 | 说明 |
|---|---|
| `m_Plugins` | 以插件具体 `Type` 为键保存实例 |
| `m_SortedPlugins` | 按 `Priority` 升序保存实例，用于初始化、广播和 `GetAll` |
| `m_InitializedTcs` | `WaitForInitializedAsync` 的完成信号 |
| `m_IsInitialized` | 异步初始化是否已完成 |
| `m_EventManager` | `Login` 时发送 `SDKEventData.UserLogin` |
| `m_ConfigManager` | 按 `RequiredConfigType` 拉取插件配置 |

当前配置来源统一为 `IConfigManager`。

## 当前工作流

### Initialize

- 遍历 `SDKManagerConfig.PluginEntries`
- 跳过 `Enabled == false` 或 `IsMissing == true` 的条目
- 用 `Activator.CreateInstance(pluginType)` 反射实例化插件
- 排序后缓存 `IEventManager` 和 `IConfigManager`

### InitializeAsync

- 先按 `Priority` 分桶
- 再按桶顺序执行 `UniTask.WhenAll`
- 单插件初始化失败只记日志，不中断其他插件
- 全部完成后设置 `m_IsInitialized = true`

### InitializePluginAsync

- 读取 `(plugin as SDKPluginBase)?.RequiredConfigType`
- 若插件需要配置，则通过 `m_ConfigManager.GetSDKPluginConfig(requiredConfigType)` 获取
- 未取到配置时记警告并跳过该插件初始化
- 成功后调用 `plugin.InitializeAsync(config, ct)`

## 查询语义

- `Get<T>()` / `TryGet<T>()` 通过遍历 `m_Plugins.Values` 做 `candidate is T` 判断。
- 这意味着查询既支持具体插件类型，也支持接口类型。
- `GetAll<T>()` 只返回 `IsAvailable == true` 的实例，并保持 `Priority` 升序。

## 生命周期与关闭

- `BroadcastPause(bool)` → 所有 `ISDKPauseListener`
- `BroadcastFocus(bool)` → 所有 `ISDKFocusListener`
- `BroadcastQuit()` → 所有 `ISDKQuitListener`
- `Shutdown()` → `DisposeAsync(CancellationToken.None).Forget()`

## 使用示例

```csharp
ISDKManager manager = Nova.SDK.SDKManager;

await manager.InitializeAsync(ct);

if (manager.TryGet<IRemoteConfigPlugin>(out var remoteConfig))
{
    await remoteConfig.FetchAsync(ct: ct);
}

foreach (ITrackPlugin tracker in manager.GetAll<ITrackPlugin>())
{
    tracker.TrackEvent("session_start", null);
}
```

## 关联文档

- [../Interfaces/ISDKManager.md](../Interfaces/ISDKManager.md)
- [./SDKManagerBase.md](./SDKManagerBase.md)
- [../../SDKComponent.md](../../SDKComponent.md)
- [../../Definitions/SDKPluginBase.md](../../Definitions/SDKPluginBase.md)
