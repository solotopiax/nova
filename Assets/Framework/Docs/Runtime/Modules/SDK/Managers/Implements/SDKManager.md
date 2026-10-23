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

`Get/TryGet` 选字典中第一个匹配对象，状态查询与单插件等待选排序列表中第一个匹配对象；多个插件实现同一能力时，这些入口不是全体状态聚合，也不保证选择同一个首项。`GetAll` 每次创建新列表，但其中仍是原插件对象。`List.Sort` 对相同 Priority 不保证稳定次序，Priority 只决定排序和逆序释放，不是依赖关系或全局唯一选择键。插件自定义属性 getter 的异常也可能向查询者传播。

依赖声明只在存在提供者时建立边：缺失的可选能力不等待，已存在的可选能力仍参与等待和环检测；必需能力必须最终 `Ready`。多个提供者、自依赖、非法能力、环及依赖环的下游会被阻塞。初始化失败插件用 `CancellationToken.None` 尽力释放，清理也可能延迟完成。配置 getter 位于单插件初始化的内部 try 之外，由外层调度隔离。静态配置元数据与已构造实例声明不符时会跳过实例，此分支不补调 Dispose；反射发现顺序也不构成确定的冲突胜出顺序。

全局流程结束并不代表每个插件成功：缺失 ConfigManager、没有启用配置，或单插件 `Failed/Blocked` 后，都可能完成全局任务。取消或致命失败会取消/故障原完成源；它们直到 `DisposeAsync` 才重建，不能把再次调用视作干净重试。直接 Manager 等待入口不启动初始化，发现尚未开始时可能一直等待；外部取消只取消本次等待。

## 生命周期与关闭

- `BroadcastPause(bool)` → 所有 `ISDKPauseListener`
- `BroadcastFocus(bool)` → 所有 `ISDKFocusListener`
- `BroadcastQuit()` → 所有 `ISDKQuitListener`
- `Shutdown()` → `DisposeAsync(CancellationToken.None).Forget()`

广播遍历所有对应 Listener，不检查 `Ready` 或 `IsAvailable`，逐插件隔离回调异常；退出钩子不等待异步释放。`DisposeAsync` 先请求取消进行中的初始化并等待其终态，再逆 Priority 释放 `Ready` 插件、清集合/会话并重建信号源。它没有并发 Dispose 锁；`Shutdown` 返回时释放可能仍在进行，原生资源是否释放须由插件验证。

登录仅分发已由业务确认的身份，不执行账号认证。空白 UID 被忽略，非空 UID 原样保留；同 UID 每次有效 Login 也会新建 session。属性字典只做浅复制，嵌套值仍共享。Ready 插件优先接收含属性的 Context 接口，否则接收普通接口；注册/登录回调成功才登记已交付，异常后可再补交。`EndLoginSession` 作废当前 UID、属性和待补注册并递增 session，通知 Ready 的结束接收者，不替代厂商 Logout，也不会取消插件自己启动的旧异步任务。最后发送的 `SDKEventData.UserLogin` 是池化事件，只能在回调期间读；跨帧需要复制 UID，不能保留事件引用。公开 Event.Fire 的异常不由 Login 统一吞掉。

`UserLogin.Create(userId)` 只借池并赋字段，不单独拒绝 null/空 UID；输入校验属于 Login 的调用边界。Clear 只把字段置 null，不自行回池。

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
