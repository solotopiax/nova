# WebSocketScope.NetChannelBase

**类签名**：`public abstract class NetChannelBase`（嵌套于 `WebSocketScope`）
**命名空间**：`NovaFramework.Runtime`

通信通道抽象基类，封装 WebSocket 连接生命周期、消息发送/接收缓冲与重连支持。每个通道实例绑定一个服务器地址和通道类型，内部管理发送线程（非 WebGL）或发送协程（WebGL）、在线/离线消息缓冲、序列号分配及认证/心跳超时检测。

> **线程安全**：发送缓冲使用 `m_SendBufferLock`（`object` 锁）保护在线缓冲的并发读写，替代了早期的 `m_CanSend` 标志位方案。
> **线程终止**：非 WebGL 平台通过 `Thread.Join(3000)` 限时等待线程退出，而非 `Thread.Abort()`。
> **空转保护**：发送与接收循环在无数据时通过 `UniTask.Delay(1)` 避免 CPU 空转。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketScope.NetChannelBase.cs` | 通信通道抽象基类定义（含 WebGL 和非 WebGL 两套实现） |

## 内嵌接口

### IWebSocketManagerBridge

WebSocketManager 向通道暴露的轻量接口，避免通道持有 Manager 具体类型。

```csharp
public interface IWebSocketManagerBridge
{
    float ConnectTimeout { get; }
    float AuthenticateTimeout { get; }
    float HeartBeatTimeout { get; }
    NetMessageBase CreateMessage(NetChannelType channelType);
    void RecycleMessage(NetMessageBase message);
    void LazyToQueueOnMainThread(Action action);
}
```

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `ServerAddress` | `string`（属性） | 服务器地址 |
| `AutoReconnect` | `bool`（属性） | 是否开启自动重连 |
| `IsDisconnectedSubjectively` | `bool`（属性） | 连接是否已被主动断开（用于阻止自动重连） |
| `NeedAuthenticateRightNow` | `bool` | 是否需要立即发送身份认证消息 |
| `WaitingAuthenticate` | `bool` | 是否正在等待身份认证响应 |
| `AuthenticateSendTime` | `DateTime` | 最后一次身份认证消息发送时间 |
| `WaitingHeartBeat` | `bool` | 是否正在等待心跳响应 |
| `HeartBeatSendTime` | `DateTime` | 最后一次心跳消息发送时间 |
| `IsNeedConnect` | `bool`（虚属性） | 是否需要保持连接，默认 true |
| `IsConnected` | `bool`（属性） | 是否已连接（非 WebGL 通过 WebSocket.State 判断，WebGL 通过 JS 层查询） |
| `IsAuthenticatedSuccess` | `bool`（属性） | 是否身份认证成功 |

## 公开 API

```csharp
// 初始化通道，启动发送/接收线程（或协程）
void Init(IWebSocketManagerBridge bridge, ICoroutineRunner coroutineRunner, bool autoReconnect, string serverAddress)

// 终结通道，停止所有线程/协程并关闭 Socket，清空发送缓冲
UniTask OnTerminate()

// 获取并自增指定协议 ID 的序列号
int GetSequenceID(int commandID)

// 将消息封装为字节数组（由子类实现协议格式）
abstract byte[] PackageMessageToBytes(NetMessageBase message)

// 向在线发送缓冲注入消息
void InjectBytesToBuffer(byte[] bytes, NetMessageBase message)

// 向离线发送缓冲注入消息
void InjectBytesToOfflineBuffer(byte[] bytes, NetMessageBase message)

// 将离线缓冲批量移入在线缓冲（重连后补发）
void InjectOfflineBufferIntoBuffer()

// 判断消息是否为心跳消息
abstract bool IsHeartBeatMessage(NetMessageBase message)

// 判断消息是否为身份认证消息
abstract bool IsAuthenticateMessage(NetMessageBase message)

// 判断身份认证响应是否成功
abstract bool IsAuthenticateMessageSuccess(NetMessageBase message)

// 检查身份认证是否已超时
bool CheckAuthenticateTimeout()

// 检查心跳是否已超时
bool CheckHeatBeatTimeout()

// 获取通信通道类型
abstract NetChannelType GetNetChannelType()

// 比较通道类型与服务器地址是否一致
bool Equals(NetChannelType channelType, NetChannelBase channel)
abstract bool Equals(NetChannelType channelType, string serverAddress)

// 创建 WebSocket 连接
// WebGL: IEnumerator CreateSocket()
// 非 WebGL: UniTask CreateSocket()

// 关闭 WebSocket 连接
// WebGL: IEnumerator CloseSocket(bool subjectively, Action overCallback = null)
// 非 WebGL: UniTask CloseSocket(bool subjectively)
```

## 事件

| 事件 | 签名 | 说明 |
|------|------|------|
| `SendMessageEvent` | `Action<NetChannelBase, NetMessageBase>` | 消息发送成功 |
| `ReceiveMessageEvent` | `Action<NetChannelBase, NetMessageBase>` | 消息接收成功 |
| `DisconnectServerEvent` | `Action<NetChannelBase>` | 与服务器断开连接 |

## 平台差异

| 功能 | 非 WebGL | WebGL |
|------|----------|-------|
| 发送 | 独立 Thread（SendMessageNeedConnect） | 协程（StartCoroutine） |
| 接收 | 独立 Thread（ReceiveMessageNeedConnect） | JS 回调（OnWebGLMessageReceived） |
| 连接 | ClientWebSocket.ConnectAsync | WebGL.WebSocketAllocate + WebSocketConnect |
| 关闭 | ClientWebSocket.CloseOutputAsync | WebGL.WebSocketClose |

## 关联文档

- [TcpChannel](TcpChannel.md)
- [TcpPbChannel](TcpPbChannel.md)
- [NetChannelType](NetChannelType.md)
- [NetMessageBase](../Messages/NetMessageBase.md)
- [WebSocketScope](../WebSocketScope.md)
