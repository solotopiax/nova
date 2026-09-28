# SDKManager

**类签名**：`internal sealed partial class SDKManager : SDKManagerBase`  
**命名空间**：`NovaFramework.Runtime`

`SDKManager` 是 `ISDKManager` 的唯一实现，负责插件实例化、初始化编排、查询、生命周期广播和登录事件转发。

## 文件拆分

| 文件 | 说明 |
|---|---|
| `SDKManager.cs` | 公开 override：`Initialize`、`InitializeAsync`、`DisposeAsync`、`Get`、`TryGet`、`GetAll`、`Broadcast*`、`Login`、`Update`、`Shutdown` |
| `SDKManager.Visitors.cs` | 内部字段与属性 |
| `SDKManager.Methods.cs` | 插件实例化、依赖解析、单插件初始化和登录补交 |

## 当前关键字段

| 字段 | 说明 |
|---|---|
| `m_Plugins` | 以插件具体 `Type` 为键保存实例 |
| `m_SortedPlugins` | 按 `ISDKPlugin.Priority` 升序保存实例，用于稳定遍历、广播和 `GetAll`；Priority 不构成初始化依赖 |
| m_InitializedTcs | WaitForInitializedAsync 的完成信号 |
| `m_IsInitialized` | 异步初始化是否已完成 |
| `m_EventManager` | `Login` 时发送 `SDKEventData.UserLogin` |
| `m_ConfigManager` | 按 `RequiredConfigType` 拉取插件配置 |

当前配置来源统一为 `IConfigManager`。

## 当前工作流

### Initialize

- 不按 `PluginEntries` 实例化插件，也不读取 Entry Priority 参与排序
- 缓存 `IEventManager` 和 `IConfigManager`
- 不在 `Initialize` 阶段实例化插件

### InitializeAsync

- 先通过 `PluginBase<TConfig>` 或 `SDKPluginConfigTypeAttribute` 静态读取配置类型，仅构造 `ConfigMaster.EnabledSDKs` 命中的插件
- Editor Play 的反射发现会排除引用 NUnit 或 Unity Test Runner 的测试程序集；Player 不执行该引用检查，避免 HybridCLR 程序集依赖解析影响插件发现
- 未启用插件不会执行构造函数或字段初始化；缺少静态配置元数据的旧式插件会记录诊断并跳过
- 先建立所有插件的完成信号，校验依赖缺失、重复提供者与循环；无依赖插件并发启动，有依赖插件只等待声明的能力
- 单插件初始化失败只记日志，不中断其他插件
- 所有插件得到最终状态后，若存在可用 `IDeviceIdProvider`，将非空 `GetDeviceID()` 通过 `IAssetManager.SaveAssetCheckDeviceId` 写入启动白名单缓存；失败不影响初始化
- 全部完成后设置 `m_IsInitialized = true`
- `WaitForPluginAsync<T>()` 可独立等待目标；`Ready` 才能通过 `Get/TryGet/GetAll` 查询。`InitializeTask` 仍等待全体，若某插件初始化永不返回，等待仍不会结束。
- `Login(uid, properties)` 保存属性快照；插件就绪后自动补交，重复登录或切换账号更新会话编号。旧 `Login(uid)` 保留，业务属性须由接收插件自行提前准备。

### InitializePluginAsync

- 对已准入并构造的插件读取 `RequiredConfigType`
- 若插件需要配置，则通过 `m_ConfigManager.GetSDKPluginConfig(requiredConfigType)` 获取
- 未取到配置时记警告并跳过该插件初始化
- 成功后调用 `plugin.InitializeAsync(config, ct)`
- Unity Editor 中插件抛出 `PlatformNotSupportedException` 时按平台不适用处理：记录警告、保持不可用并继续初始化其他插件；其他异常仍按初始化错误隔离

## 查询语义

- `Get<T>()` / `TryGet<T>()` 通过遍历 `m_Plugins.Values` 做 `candidate is T && candidate.IsAvailable && state == Ready` 判断。
- 这意味着查询既支持具体插件类型，也支持接口类型。
- `GetAll<T>()` 只返回 `IsAvailable == true` 且状态为 `Ready` 的实例，并保持 `ISDKPlugin.Priority` 升序。

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
