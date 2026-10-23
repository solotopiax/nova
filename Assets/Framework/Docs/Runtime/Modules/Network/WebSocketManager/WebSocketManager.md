# WebSocketManager

**类签名**：`internal sealed partial class WebSocketManager : WebSocketManagerBase, WebSocketScope.IWebSocketManagerBridge`
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Nova.Network.WebSocketManager`（通过 NetworkComponent 属性访问）

负责 WebSocket 长连接通道（Tcp / TcpPb）的生命周期管理、消息对象池、认证/心跳/重连协程调度，以及跨线程消息队列分发。实现 `IWebSocketManagerBridge` 供 `NetChannelBase` 反向访问管理器能力，避免通道直接依赖具体管理器类型。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `WebSocketManager.cs` | `sealed partial WebSocketManager` | 主体：Initialize / Connect/Disconnect/Reconnect / SendMessage / CreateMessage/RecycleMessage / Update / Shutdown |
| `WebSocketManager.Visitors.cs` | `partial WebSocketManager` | 字段：通道列表、协程句柄字典、消息对象池、配置参数、9 个事件 |
| `WebSocketManager.Methods.cs` | `partial WebSocketManager` | 私有方法：4 条连接/认证/心跳/重连协程、通道查找/停止协程工具 |
| `WebSocketManager.MutexData.cs` | `partial WebSocketManager` | 跨线程队列：LazyToQueueOnMainThread / QueueOnSubThread / MutexDataUpdate |
| `WebSocketManagerBase.cs` | `abstract WebSocketManagerBase` | 基类：继承 FrameworkManager，声明全部抽象方法与事件，Priority = 9 |
| `WebSocketManagerConfig.cs` | `class WebSocketManagerConfig` | 配置数据类：超时参数、ICoroutineRunner、SpecialMessageCreator 委托 |

---

## § 3 继承关系

```
FrameworkManager
  └── WebSocketManagerBase (abstract) : IWebSocketManager   Priority = 9
        └── WebSocketManager (sealed partial) : IWebSocketManagerBridge

内嵌接口（WebSocketScope 内）：
  WebSocketScope.IWebSocketManagerBridge
    ├─ ConnectTimeout / AuthenticateTimeout / HeartBeatTimeout { get; }
    ├─ NetMessageBase CreateMessage(NetChannelType)
    ├─ void RecycleMessage(NetMessageBase)
    └─ void LazyToQueueOnMainThread(Action)
```

---

## § 4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_NetChannels` | `List<NetChannelBase>` | `[]` | 所有通道实例，索引作为通道 ID |
| `m_ConnectServerCoroutines` | `Dictionary<NetChannelBase, Coroutine>` | `{}` | key = 通道实例；连接协程句柄 |
| `m_AutoAuthenticateCoroutines` | `Dictionary<NetChannelBase, Coroutine>` | `{}` | key = 通道实例；认证协程句柄 |
| `m_AutoHeartBeatCoroutines` | `Dictionary<NetChannelBase, Coroutine>` | `{}` | key = 通道实例；心跳协程句柄 |
| `m_AutoReconnectCoroutines` | `Dictionary<NetChannelBase, Coroutine>` | `{}` | key = 通道实例；断线重连协程句柄 |
| `m_CoroutineRunner` | `ICoroutineRunner` | `null` | 由 WebSocketManagerConfig 注入（NetworkComponent 实现） |
| `m_SpecialMessageCreator` | `Func<NetChannelType, string, NetMessageBase>` | `null` | 创建心跳/认证消息的游戏层委托 |
| `m_ConnectTimeout` | `float` | `10f` | 连接超时时间（秒） |
| `m_AuthenticateTimeout` | `float` | `10f` | 身份认证超时时间（秒） |
| `m_HeartBeatTimeInterval` | `float` | `20f` | 心跳发送间隔（秒） |
| `m_HeartBeatTimeout` | `float` | `10f` | 心跳响应超时时间（秒） |
| `m_AutoReconnectMaxCounter` | `int` | `5` | 自动重连最大次数，超过后触发 OnReconnectFailed |
| `m_AutoReconnectTimeInterval` | `float` | `3f` | 自动重连间隔时间（秒） |
| `m_EnableAutoReconnect` | `bool` | `true` | 全局自动重连开关 |
| `m_ActionQueueOnMultiThread` | `Queue<Action>` | `{}` | 子线程写入的待主线程执行委托队列（lock 保护） |
| `m_ActionExecuteQueueOnMainThread` | `Queue<Action>` | `{}` | 每帧从多线程队列平移过来后顺序执行 |
| `m_Lock` | `readonly object` | 已初始化 | 轻量级互斥锁，保护 m_ActionQueueOnMultiThread 的并发访问（替代 Mutex） |

---

## § 5 完整公开 API

```csharp
// --- 生命周期 ---
void Initialize(WebSocketManagerConfig config)
void Update()      // 每帧调用，驱动 MutexDataUpdate 分发跨线程消息
void Shutdown()    // 发起异步终止；内部等待通道退出并排空消息回池队列，同步返回不等于收尾结束

// --- 连接管理 ---
void ConnectServer(NetChannelType channelType, string serverAddress, bool autoReconnect = true)
void ReconnectServer(NetChannelType channelType, string serverAddress)
void DisconnectServer(NetChannelType channelType, string serverAddress)
void TestDisconnectServerAbnormally(NetChannelType channelType, string serverAddress)

// --- 状态查询 ---
bool IsConnected(NetChannelType channelType, string serverAddress)
bool IsAuthenticatedSuccess(NetChannelType channelType, string serverAddress)
IReadOnlyList<WebSocketScope.NetChannelBase> NetChannels { get; }   // 供编辑器运行时面板读取

// --- 消息 ---
NetMessageBase CreateMessage(NetChannelType channelType)
void RecycleMessage(NetMessageBase message)
bool SendMessage(NetChannelType channelType, string serverAddress, NetMessageBase message)

// --- 事件（外部订阅）---
event Action<int, string> OnBeginConnect
event Action<int, string> OnConnectSuccess
event Action<int, string> OnConnectFail
event Action<int, string> OnDisconnect
event Action<int, string> OnReconnectFailed
event Action<int, string> OnAuthenticateSuccess
event Action<int, string> OnAuthenticateFail
event Action<NetChannelBase, NetMessageBase> OnReceiveMessage
event Action<NetChannelBase, NetMessageBase> OnSendMessage

// --- IWebSocketManagerBridge 实现（供 NetChannelBase 调用）---
float ConnectTimeout { get; }
float AuthenticateTimeout { get; }
float HeartBeatTimeout { get; }
void LazyToQueueOnMainThread(Action action)

// --- 跨线程工具（MutexData）---
void QueueOnSubThread(Action<object> actionOnSubThread, Action actionOnMainThread)
void QueueOnSubThread(Action<object> actionOnSubThread, object state, Action actionOnMainThread)
```

---

## § 6 生命周期状态机

通道（NetChannelBase）在 WebSocketManager 管理下的状态转换：

```
[初始化]
    │
    │ ConnectServer()
    ▼
[Connecting]
  连接超时 → 取消握手、释放Socket → [ConnectFail]
    │ OnConnectSuccess
    ▼
[Connected]
  │   ├─ NeedAuthenticateRightNow && !WaitingAuthenticate 时检查认证委托
  │   ├─ 发送认证消息 → NeedAuthenticateRightNow=false、WaitingAuthenticate=true
  │   ├─ 无认证消息创建委托或返回null → 跳过账号认证
  │   ├─ 认证超时 → CloseSocket(false) → [Disconnected]
  │   └─ 收到成功认证响应 → WaitingAuthenticate=false → IsAuthenticatedSuccess=true
  │       → InjectOfflineBufferIntoBuffer → OnAuthenticateSuccess
  ├─ 启动 AutoHeartBeat 协程（等认证完成后开始）
  │   ├─ 间隔 m_HeartBeatTimeInterval 发送心跳
  │   └─ 心跳超时 → CloseSocket(false) → [Disconnected]
  └─ 启动 AutoReconnect 协程（EnableAutoReconnect && autoReconnect）
        └─ 检测到断开 → 等 m_AutoReconnectTimeInterval → 重新连接
            → 每次连接成功重新启动认证与心跳

[Disconnected] ← DisconnectServer() 主动调用
    IsDisconnectedSubjectively = true → 所有协程退出并取消当前握手
```

首次连接失败时尚未启动自动重连协程，只报告 `OnConnectFail`；自动重连配置用于首次连接成功后的断线恢复。显式初连/重连推进内部连接轮次，旧连接/认证/重连续体与旧 Socket 的排队响应不得更改新连接状态。

| 状态字段 | 值 / 含义 |
|---|---|
| `channel.IsConnected` | WebSocket 底层已建立连接 |
| `channel.IsAuthenticatedSuccess` | 认证流程已完成（或不需要认证） |
| `channel.WaitingAuthenticate` | 认证消息已发出，等待服务器响应 |
| `channel.WaitingHeartBeat` | 心跳消息已发出，等待服务器响应 |
| `channel.IsDisconnectedSubjectively` | 主动断开（DisconnectServer），阻止自动重连 |
| `channel.NeedAuthenticateRightNow` | 连接建立后需要立即执行认证 |

### 常见误区

| 误区 | 正确理解 |
|---|---|
| 以为 OnConnectSuccess 后即可完成业务 | 连接、认证、消息入队和服务端确认分别验收；账号认证消息创建入口边界见 § 8 |
| 以为 DisconnectServer 会触发 OnDisconnect | DisconnectServer 主动断开，IsDisconnectedSubjectively = true，不触发 AutoReconnect；OnDisconnect 由 DisconnectServerEvent 触发（通道层回调） |
| 以为 SendMessage 不成功时消息会丢失 | 已连接但尚未认证且非主动断开时，消息写入离线缓冲，认证完成后自动注入 |

---

## § 7 线程模型

WebSocket 底层收发消息在 Socket 子线程执行，事件回调需回到主线程。

```
[Socket 子线程]                          [主线程 Update]
    │                                          │
    │ 收到消息/连接状态变更                      │
    ▼                                          │
LazyToQueueOnMainThread(action)                │
    │                                          │
    └─ lock (m_Lock)                           │
        └─ m_ActionQueueOnMultiThread.Enqueue   │
                                               │ MutexDataUpdate()（每帧）
                                               ├─ lock (m_Lock) { Count 检查 + 全部转移 }
                                               └─ 逐一 Invoke()（在主线程，lock 外执行）
```

| 方法 | 线程安全 | 说明 |
|---|---|---|
| `LazyToQueueOnMainThread` | 线程安全 | lock (m_Lock) 保护，子线程可直接调用 |
| `MutexDataUpdate` | 主线程 | 每帧 Update 调用，转移并执行队列 |
| `SendMessage` | 主线程 | 操作 m_NetChannels，需在主线程调用 |
| `ConnectServer` | 主线程 | 启动协程，需在主线程调用 |

LazyToQueueOnMainThread 只入队，依赖后续 Update/终止排空，不会在终止期改为后台直接执行。MutexDataUpdate 锁内转移队列、锁外调用；用户 Action 抛错会中断本轮，尚未出队的执行项留待下一次处理。QueueOnSubThread 使用 ThreadPool，后台 action 抛错时不会继续排主线程 callback；这些额外任务没有纳入通道 loop 完成信号。Shutdown 未冻结外部新增通道/任意未来入队，不能证明所有消费者后台任务都已经退出。

NetChannels 返回原列表的 IReadOnlyList 视图，元素可变，并非深复制。ConnectServer 只按同类型/精确地址且 !IsConnected 复用，尚在握手/closing 的对象也可能命中；同目标已连接时可新增通道，后续 Find 只取首项。未知通道枚举走 TcpPb 分支。StopChannelCoroutines 只停管理协程，不停止通道长驻 loop 或清业务队列；Initialize 也不验证秒数有限/正值或重置已有通道。

---

## § 8 初始化时序

```
NetworkComponent.Start()
  │
  └─ WebSocketManager.Initialize(WebSocketManagerConfig)
        ├─ m_CoroutineRunner      ← config.CoroutineRunner   (必须非 null，否则 Fatal)
        ├─ m_SpecialMessageCreator ← config.SpecialMessageCreator (可为 null)
        ├─ m_ConnectTimeout       ← config.ConnectTimeout
        ├─ m_AuthenticateTimeout  ← config.AuthenticateTimeout
        ├─ m_HeartBeatTimeInterval ← config.HeartBeatTimeInterval
        ├─ m_HeartBeatTimeout     ← config.HeartBeatTimeout
        ├─ m_AutoReconnectMaxCounter       ← config.AutoReconnectMaxCounter
        ├─ m_AutoReconnectTimeInterval     ← config.AutoReconnectTimeInterval
        ├─ m_EnableAutoReconnect           ← config.EnableAutoReconnect
        ├─ config.AutoReconnectFailedUIAssetLocation（当前仅配置透传，WebSocketManager 运行时尚未消费）
        └─ #if !UNITY_EDITOR && UNITY_WEBGL → WebSocketScope.WebGL.Initialize()

依赖约束：
  m_CoroutineRunner 必须由外部注入（NetworkComponent 实现 ICoroutineRunner）
  SpecialMessageCreator 为 null 时跳过账号认证，不创建心跳；通道完成免认证状态不代表游戏服校验了账号
```

当前 `NetworkComponent.Start()` 未向此配置注入 `SpecialMessageCreator`，公开组件门面也没有动态设置入口。免认证服务可以使用现有门面；需要真实账号认证或心跳时必须先明确项目已有公开初始化契约实际负责该委托，不能通过反射、二次初始化或直接修改通道状态绕过这个缺口。

`NetChannelBase.Init()` 的 `WaitingAuthenticate` 为 `false`，连接成功后设置 `NeedAuthenticateRightNow=true`。认证协程发送消息前将需要发送标记清为 `false`、等待响应置为 `true`；响应成功后清除等待，进入完成分支并补发离线缓冲。没有认证消息时两标记清为 `false`，完成免认证通道。自动重连成功会停止上一连接的认证/心跳协程并重新启动。非 WebGL 握手使用带超时的取消源，主动关闭取消当前握手；WebGL 打开/关闭等待受 `ConnectTimeout` 约束，并在失败、超时或协程释放时清理实例与本轮回调。

当前 `OnAuthenticateFail` 未找到实际触发调用；业务不要仅依赖该事件完成失败收尾。`TcpPbChannel.AuthenticateResponseValidator` 未设置时会接受相应认证消息，也不能据此证明服务端已校验账号。本节描述源码契约；当前消费项目仍须按真实入口验证，Editor 本地服务测试不能替代真账号、生产协议或 WebGL 浏览器验证。

---

## § 9 关键算法

### ConnectServer 通道创建 & 协程链

```
ConnectServer(channelType, serverAddress, autoReconnect)
  │
  ├─ FindFreeNetChannel(channelType, serverAddress)
  │   ├─ 找到空闲通道（同类型同地址未连接）→ 复用
  │   └─ 未找到 → 创建 TcpChannel 或 TcpPbChannel
  │       ├─ channel.Init(this, m_CoroutineRunner, autoReconnect, serverAddress)
  │       ├─ 绑定 ReceiveMessageEvent → OnReceiveMessage
  │       ├─ 绑定 SendMessageEvent    → OnSendMessage
  │       ├─ 绑定 DisconnectServerEvent → OnDisconnect(channelIndex, address)
  │       └─ m_NetChannels.Add(channel)
  │
  ├─ StopChannelCoroutines(channel) + channel.PrepareConnect()
  └─ StartCoroutine(ConnectServerCoroutine(channel))
        │
        ├─ InnerConnectServerCoroutine / InnerConnectServerCoroutineAsync
        │   ├─ OnBeginConnect.Invoke(idx, addr)
        │   ├─ channel.CloseSocket(false) + channel.CreateSocket()
        │   └─ IsConnected → OnConnectSuccess / OnConnectFail
        │
        └─ 连接成功后：
            ├─ StartAuthenticationAndHeartBeat(channel)
            │   ├─ 停止旧认证/心跳协程
            │   └─ 启动本次认证/心跳协程
            └─ EnableAutoReconnect && autoReconnect → StartCoroutine(AutoReconnectServerCoroutine)
```

### SendMessage 发送决策

```
SendMessage(channelType, serverAddress, message)
  │
  ├─ FindNetChannelIndex → channelIndex （-1 → Error + return false）
  ├─ channel.PackageMessageToBytes(message) → bytes
  │
  ├─ !channel.IsNeedConnect    → InjectBytesToBuffer → true
  ├─ channel.IsAuthenticatedSuccess → InjectBytesToBuffer → true
  ├─ !channel.IsDisconnectedSubjectively → InjectBytesToOfflineBuffer → true（离线缓存）
  └─ 其他情况 → Error + return false（已主动断开）
```

---

## § 11 使用示例

```csharp
// --- 1. 注册事件 ---
Nova.Network.OnWebSocketConnectSuccess += (idx, addr) =>
    Debug.Log($"[WS] Channel {idx} connected to {addr}");

Nova.Network.OnWebSocketAuthenticateSuccess += (idx, addr) =>
    Debug.Log($"[WS] Channel {idx} auth OK");

Nova.Network.OnWebSocketReceiveMessage += (channel, msg) =>
{
    // 同步处理借用消息；异步/业务事件先复制需要的数据。
    // 通道会在回调返回后自动回收，不在这里再次 RecycleMessage。
};

// --- 2. 注入特殊消息创建委托（认证/心跳）---
// 先确认项目已有的公开初始化契约实际负责此委托。
// 当前 NetworkComponent.Start 未注入，组件门面没有动态 setter；见 § 8。

// --- 3. 连接 ---
Nova.Network.ConnectServer(
    WebSocketScope.NetChannelType.TcpPb,
    "ws://game.server.com:9000/ws",
    autoReconnect: true
);

// --- 4. 发消息 ---
var msg = Nova.Network.CreateMessage(WebSocketScope.NetChannelType.TcpPb);
// 填充 msg 数据（协议层逻辑）...
bool sent = Nova.Network.SendMessage(
    WebSocketScope.NetChannelType.TcpPb,
    "ws://game.server.com:9000/ws",
    msg
);
// true 表示消息进入发送/离线缓冲，不是服务端业务成功。
// true 后消息交给通道持有；false 且确认未入队时创建者负责回收。

// --- 5. 主动断开 ---
Nova.Network.DisconnectServer(
    WebSocketScope.NetChannelType.TcpPb,
    "ws://game.server.com:9000/ws"
);
```

---

## § 12 注意事项

| 场景 | 正确做法 |
|---|---|
| WebSocketManager.Update 未被调用 | 跨线程消息无法分发，事件回调不执行；当前正常路径由 `Nova.Update() -> FrameworkManagersGroup.Update()` 统一驱动 |
| SpecialMessageCreator 返回 null | 跳过账号认证并完成免认证通道；没有委托也不会创建心跳，不能当作服务端账号校验成功 |
| 收发回调内再次 RecycleMessage | 通道在收发事件返回后自动回收，会造成重复回池；只在回调内同步读取，异步处理先拷贝数据 |
| CreateMessage 后发送失败 | 若确认没有进入任何发送/离线缓冲，由创建者回收；已入队或归属不确定时不得提前或重复回收 |
| 反复 ConnectServer 同一已连接目标 | 当前只复用未连接通道，可能新增同地址通道；复用一个明确连接拥有者，先查询并避免重复启动 |
| TestDisconnectServerAbnormally 与 DisconnectServer 混用 | Test 版本不设置 IsDisconnectedSubjectively，重连协程会继续运行；正式断开请使用 DisconnectServer |

---

## § 13 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [NetworkManager.md](../NetworkManager/NetworkManager.md)
