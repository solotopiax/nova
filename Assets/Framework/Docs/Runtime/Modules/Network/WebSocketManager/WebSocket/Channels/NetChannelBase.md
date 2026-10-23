# WebSocketScope.NetChannelBase

**类签名**：`public abstract class NetChannelBase`（嵌套于 `WebSocketScope`）
**命名空间**：`NovaFramework.Runtime`

通信通道抽象基类，封装 WebSocket 连接生命周期、消息发送/接收缓冲与重连支持。每个通道实例绑定一个服务器地址和通道类型，内部管理发送线程（非 WebGL）或发送协程（WebGL）、在线/离线消息缓冲、序列号分配及认证/心跳超时检测。

> **线程安全**：发送缓冲使用 `m_SendBufferLock`（`object` 锁）保护在线缓冲的并发读写，替代了早期的 `m_CanSend` 标志位方案。
> **通道终止**：非 WebGL 先停止收发开关并关闭 Socket，再等待线程入口和两个真实异步循环的完成信号；`Thread.Join(3000)` 不能单独证明异步 I/O 已结束。随后回收在线、离线队列剩余消息。调用方必须实际等待 `OnTerminate()`，并继续驱动 Manager 主线程队列，才能处理已排队的回池收尾。
> **空转保护**：发送与接收循环在无数据时通过 `UniTask.Delay(1, DelayType.Realtime)` 避免 CPU 空转；网络轮询不受游戏暂停影响，EditMode 后台入口也能继续恢复。

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
| `WaitingAuthenticate` | `bool` | 是否正在等待身份认证响应，初始化为 false；认证请求入队前置 true |
| `AuthenticateSendTime` | `DateTime` | 最后一次身份认证消息发送时间 |
| `WaitingHeartBeat` | `bool` | 是否正在等待心跳响应 |
| `HeartBeatSendTime` | `DateTime` | 最后一次心跳消息发送时间 |
| `IsNeedConnect` | `bool`（虚属性） | 是否需要保持连接，默认 true |
| `IsConnected` | `bool`（属性） | 是否已连接（非 WebGL 通过 WebSocket.State 判断，WebGL 通过 JS 层查询） |
| `IsAuthenticatedSuccess` | `bool`（属性） | 通道已连接且不在关闭/主动退出中，同时无需发送认证、无需等待响应 |

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

`CreateSocket` 只在本轮连接仍有效时设置认证状态：连接成功后需要认证、不等待旧响应、清除旧心跳等待；账号认证请求入队后才开始等响应。内部 `PrepareConnect()` 推进显式连接轮次，旧握手、旧认证协程及旧 Socket 的排队响应不得改变新连接状态。

显式连接准备或关闭时会从在线队列移除并回收旧认证/心跳消息；普通业务在线和离线队列保留既有契约，不代替业务幂等或服务端确认。已取出的发送任务捕获当时 Socket 和连接轮次，关闭或替换后不得用新 Socket 发送旧消息；正常关闭引起的发送异常不会终止后续收发循环，取出的消息在收尾中回收，不能自动证明业务未送达。WebGL 收发回调及非 WebGL 主线程回调也在用户回调返回后核对连接身份，并以 `finally` 回收借用消息。

非 WebGL 连接握手使用 `ConnectTimeout` 限时取消源；主动 `CloseSocket(true)` 同时取消当前握手，即使还没有打开 Socket 也保留主动退出标记。关闭输出也受同一超时约束，关闭完成后释放捕获的 Socket，再通知断开事件，避免回调重入时销毁新 Socket。WebGL 打开/关闭等待也受 `ConnectTimeout` 约束，失败、超时或协程释放时移除本轮回调并释放实例；不保证服务端已处理或撤销任何业务请求。

`WebSocketManager.Shutdown()` 仍是发起异步终止后返回的门面；内部实际等待各通道终止，再排空本 Manager 已排队的主线程收尾。它的同步返回不能等同于全部异步收尾已经完成。Play 停止、外部关闭时机及 WebGL 浏览器生命周期仍须按真实平台验证。

Init 启动长驻循环/订阅但不建立连接；不可对活跃实例重复 Init，否则旧线程与事件关系不会被这个入口自动卸载。在线/离线缓冲只在 bytes 和 message 均非 null 时成对加入，忽略无效输入不等于回收消息；公开注入不校验认证、关闭状态或容量。恢复时批量搬移离线缓冲，业务必须自行保证幂等。若两份离线列表数量异常不一致，当前损坏分支直接清列表，不逐对象回池；不能把它当任意数据污染后的可靠恢复手段。

GetSequenceID 按 commandID 分别递增，负 commandID 返回 0；它不处理 int 溢出或并发调用，未 Init 的字典也不可用。序列号、地址字符串和 ConnectionVersion 都不是业务幂等 ID；地址相等按精确字符串比较，无 URI 规范化，传入 null 通道对象的 Equals 重载可抛异常。CheckAuthenticateTimeout/准确拼写为 `CheckHeatBeatTimeout` 的方法只比较 `DateTime.Now` 与发送时间，不自行判断 waiting 标志，系统时钟调整会影响结果；协程间隔的 `WaitForSeconds` 还受游戏时间缩放影响。

非 WebGL 收发 loop 的完成信号证明循环本身退出；接收侧排入的是 async Action，若认证失败回调正在 await Close，队列调用返回不表示这个异步委托也已完成。普通业务消息与已发请求也不会因 Close 被服务端撤销。socket 异常/取消/Disposed 有专门处理，其他配置或用户回调异常仍可能传播，消息归还只覆盖已成功交到通道上层的对象；解包失败的边界见具体 Channel 文档。

## 关联文档

- [TcpChannel](TcpChannel.md)
- [TcpPbChannel](TcpPbChannel.md)
- [NetChannelType](NetChannelType.md)
- [NetMessageBase](../Messages/NetMessageBase.md)
- [WebSocketScope](../WebSocketScope.md)
