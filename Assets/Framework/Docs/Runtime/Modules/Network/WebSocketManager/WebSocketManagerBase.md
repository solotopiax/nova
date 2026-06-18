# WebSocketManagerBase

**类签名**：`internal abstract class WebSocketManagerBase : FrameworkManager, IWebSocketManager`
**命名空间**：`NovaFramework.Runtime`

WebSocket 管理器抽象基类，继承 `FrameworkManager` 并实现 `IWebSocketManager` 接口。声明所有 WebSocket 管理器的抽象方法、事件与属性，`Priority = 9`。由 `WebSocketManager` 密封实现。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketManagerBase.cs` | WebSocket 管理器抽象基类定义 |

## 继承关系

```
FrameworkManager
  └── WebSocketManagerBase (abstract) : IWebSocketManager
        └── WebSocketManager (sealed partial)
```

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `Priority` | `int`（override） | 框架管理器优先级，固定为 9 |

## 公开 API

```csharp
// 所有方法均为 abstract，由 WebSocketManager 实现
void Initialize(WebSocketManagerConfig config)
void ConnectServer(WebSocketScope.NetChannelType channelType, string serverAddress, bool autoReconnect = true)
void ReconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress)
void DisconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress)
void TestDisconnectServerAbnormally(WebSocketScope.NetChannelType channelType, string serverAddress)
bool IsConnected(WebSocketScope.NetChannelType channelType, string serverAddress)
bool IsAuthenticatedSuccess(WebSocketScope.NetChannelType channelType, string serverAddress)
bool SendMessage(WebSocketScope.NetChannelType channelType, string serverAddress, WebSocketScope.NetMessageBase message)
WebSocketScope.NetMessageBase CreateMessage(WebSocketScope.NetChannelType channelType)
void RecycleMessage(WebSocketScope.NetMessageBase message)

// 事件
event Action<int, string> OnBeginConnect;
event Action<int, string> OnConnectSuccess;
event Action<int, string> OnConnectFail;
event Action<int, string> OnDisconnect;
event Action<int, string> OnReconnectFailed;
event Action<int, string> OnAuthenticateSuccess;
event Action<int, string> OnAuthenticateFail;
event Action<WebSocketScope.NetChannelBase, WebSocketScope.NetMessageBase> OnReceiveMessage;
event Action<WebSocketScope.NetChannelBase, WebSocketScope.NetMessageBase> OnSendMessage;

// 属性
float ConnectTimeout { get; }
float AuthenticateTimeout { get; }
float HeartBeatTimeout { get; }
IReadOnlyList<WebSocketScope.NetChannelBase> NetChannels { get; }

// 生命周期
void Update()
void Shutdown()
```

## 关联文档

- [IWebSocketManager](IWebSocketManager.md)
- [WebSocketManager](WebSocketManager.md)
