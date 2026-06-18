# IWebSocketManager

**类签名**：`public interface IWebSocketManager`
**命名空间**：`NovaFramework.Runtime`

WebSocket 管理器公开接口，定义长连接通道的生命周期管理、消息收发与断线重连的全部契约。支持多通道类型（Tcp / TcpPb）、自动重连机制及丰富的连接状态事件。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `IWebSocketManager.cs` | WebSocket 管理器接口定义 |

## 公开 API

```csharp
// 初始化
void Initialize(WebSocketManagerConfig config)

// 连接服务器（创建或复用同类型同地址通道）
void ConnectServer(WebSocketScope.NetChannelType channelType, string serverAddress, bool autoReconnect = true)

// 手动触发重连（仅对已存在的通道生效）
void ReconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress)

// 主动断开连接（终止自动重连）
void DisconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress)

// 模拟非主观异常断开（保留自动重连机制，用于测试）
void TestDisconnectServerAbnormally(WebSocketScope.NetChannelType channelType, string serverAddress)

// 查询连接状态
bool IsConnected(WebSocketScope.NetChannelType channelType, string serverAddress)

// 查询认证状态
bool IsAuthenticatedSuccess(WebSocketScope.NetChannelType channelType, string serverAddress)

// 发送消息（已连接 → 立即发；断线可重连 → 离线缓冲；其他 → false）
bool SendMessage(WebSocketScope.NetChannelType channelType, string serverAddress, WebSocketScope.NetMessageBase message)

// 从对象池获取消息对象
WebSocketScope.NetMessageBase CreateMessage(WebSocketScope.NetChannelType channelType)

// 将消息对象归还对象池
void RecycleMessage(WebSocketScope.NetMessageBase message)
```

## 事件

| 事件 | 签名 | 说明 |
|------|------|------|
| `OnBeginConnect` | `Action<int, string>` | 开始连接，参数：通道索引, 服务器地址 |
| `OnConnectSuccess` | `Action<int, string>` | 连接成功 |
| `OnConnectFail` | `Action<int, string>` | 连接失败 |
| `OnDisconnect` | `Action<int, string>` | 断开连接 |
| `OnReconnectFailed` | `Action<int, string>` | 重连失败（已达重连上限） |
| `OnAuthenticateSuccess` | `Action<int, string>` | 认证成功 |
| `OnAuthenticateFail` | `Action<int, string>` | 认证失败 |
| `OnReceiveMessage` | `Action<NetChannelBase, NetMessageBase>` | 收到消息 |
| `OnSendMessage` | `Action<NetChannelBase, NetMessageBase>` | 发送消息成功 |

## 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `ConnectTimeout` | `float` | 连接超时时间（秒） |
| `AuthenticateTimeout` | `float` | 身份认证超时时间（秒） |
| `HeartBeatTimeout` | `float` | 心跳超时时间（秒） |
| `NetChannels` | `IReadOnlyList<NetChannelBase>` | 所有通信通道实例（只读） |

## 关联文档

- [WebSocketManager](WebSocketManager.md)
- [WebSocketManagerBase](WebSocketManagerBase.md)
- [WebSocketManagerConfig](Definitions/WebSocketManagerConfig.md)
- [WebSocketScope](WebSocket/WebSocketScope.md)
