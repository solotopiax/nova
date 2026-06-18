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
void Shutdown()    // 停止所有协程，关闭所有通道

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
  连接超时 ──────────────────────────────────────────► [ConnectFail]
    │ OnConnectSuccess                                       │ EnableAutoReconnect
    ▼                                                        │
[Connected]                                                  ▼
  ├─ 启动 AutoAuthenticate 协程                         [Reconnecting]
  │   ├─ SpecialMessageCreator == null → 直接标记认证完成    │ 达到上限 → OnReconnectFailed
  │   ├─ 发送认证消息 → WaitingAuthenticate = true           │
  │   ├─ 认证超时 → CloseSocket(false) → [Disconnected]     │
  │   └─ 收到认证响应 → IsAuthenticatedSuccess = true        │
  │       → InjectOfflineBufferIntoBuffer → OnAuthenticateSuccess
  ├─ 启动 AutoHeartBeat 协程（等认证完成后开始）
  │   ├─ 间隔 m_HeartBeatTimeInterval 发送心跳
  │   └─ 心跳超时 → CloseSocket(false) → [Disconnected]
  └─ 启动 AutoReconnect 协程（EnableAutoReconnect && autoReconnect）
        └─ 检测到断开 → 等 m_AutoReconnectTimeInterval → 重新连接

[Disconnected] ← DisconnectServer() 主动调用
    IsDisconnectedSubjectively = true → 所有协程退出
```

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
| 以为 OnConnectSuccess 后即可发消息 | 需等待 OnAuthenticateSuccess；若无认证委托则 OnConnectSuccess 后自动完成认证 |
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

---

## § 8 初始化时序

```
NetworkComponent.Awake()
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
  SpecialMessageCreator 为 null 时不发送认证/心跳消息，连接即视为认证完成
```

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
  └─ StartCoroutine(ConnectServerCoroutine(channelIndex))
        │
        ├─ InnerConnectServerCoroutine / InnerConnectServerCoroutineAsync
        │   ├─ OnBeginConnect.Invoke(idx, addr)
        │   ├─ channel.CloseSocket(false) + channel.CreateSocket()
        │   └─ IsConnected → OnConnectSuccess / OnConnectFail
        │
        └─ 连接成功后：
            ├─ StartCoroutine(AutoAuthenticateCoroutine)
            ├─ StartCoroutine(AutoHeartBeatCoroutine)
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
    // 处理消息...
    Nova.Network.RecycleMessage(msg);    // 用完务必回收
};

// --- 2. 注入特殊消息创建委托（认证/心跳）---
// 通常在游戏初始化阶段设置（WebSocketManagerConfig.SpecialMessageCreator）
// 或通过游戏层包装后动态设置

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
| SpecialMessageCreator 返回 null | 视为不需要认证，连接建立后直接标记认证完成；心跳也不发送 |
| RecycleMessage 忘记调用 | 消息对象不归还池，频繁 CreateMessage 会持续 new 对象，增加 GC 压力 |
| TestDisconnectServerAbnormally 与 DisconnectServer 混用 | Test 版本不设置 IsDisconnectedSubjectively，重连协程会继续运行；正式断开请使用 DisconnectServer |

---

## § 13 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [NetworkManager.md](../NetworkManager/NetworkManager.md)
