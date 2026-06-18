# WebSocketScope.TcpPbChannel

**类签名**：`public sealed class TcpPbChannel : NetChannelBase`（嵌套于 `WebSocketScope`）
**命名空间**：`NovaFramework.Runtime`

TCP Protobuf 通道，消息体为 Protobuf 编码的字节流。负责 `TcpPbMessage` 的封包/解包，心跳和认证消息通过协议 ID `MessageType.HeartBeatPb` / `MessageType.AuthenticatePb` 识别。认证响应的成功判定由外部注入的 `AuthenticateResponseValidator` 委托完成，若未注入则默认视为认证成功。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketScope.TcpPbChannel.cs` | TCP Protobuf 通道实现（含 WebGL 和非 WebGL 两套接收逻辑） |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `AuthenticateResponseValidator` | `Func<byte[], bool>` | 认证响应校验委托，参数为 TcpPbMessage.Bytes，返回 true 表示认证成功。若为 null 则默认成功 |
| `IsNeedConnect` | `bool`（override） | 始终返回 `true` |

## 公开 API

```csharp
// 将 TcpPbMessage 封装为二进制帧：[协议ID 4B][序列号 4B][Pb字节流 NB]
override byte[] PackageMessageToBytes(NetMessageBase message)

// 判断是否为心跳消息（CommandID == MessageType.HeartBeatPb）
override bool IsHeartBeatMessage(NetMessageBase message)

// 判断是否为身份认证消息（CommandID == MessageType.AuthenticatePb）
override bool IsAuthenticateMessage(NetMessageBase message)

// 判断认证响应是否成功（由 AuthenticateResponseValidator 委托决定）
override bool IsAuthenticateMessageSuccess(NetMessageBase message)

// 获取通道类型
override NetChannelType GetNetChannelType()  // => NetChannelType.TcpPb

// 判断通道类型与地址是否匹配
override bool Equals(NetChannelType channelType, string serverAddress)
```

## 消息解包流程

```
接收字节流 recvBytes
  ├─ CommandID  = recvBytes[0..3]（网络字节序→主机字节序）
  ├─ SequenceID = recvBytes[4..7]
  └─ Bytes      = recvBytes[8..]（Protobuf 编码字节流，原样保留）
```

## 关联文档

- [NetChannelBase](NetChannelBase.md)
- [TcpPbMessage](../Messages/TcpPbMessage.md)
- [MessageType](../Messages/MessageType.md)
- [NetChannelType](NetChannelType.md)
