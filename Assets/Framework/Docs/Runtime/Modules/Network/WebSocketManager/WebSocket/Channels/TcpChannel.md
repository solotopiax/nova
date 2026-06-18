# WebSocketScope.TcpChannel

**类签名**：`public sealed class TcpChannel : NetChannelBase`（嵌套于 `WebSocketScope`）
**命名空间**：`NovaFramework.Runtime`

TCP 明文通道，消息体为 JSON 字符串列表的二进制打包。负责 `TcpMessage` 的封包/解包，心跳和认证消息通过协议 ID `MessageType.HeartBeat` / `MessageType.Authenticate` 识别，认证结果通过解析 JSON 中的 `success` 字段判定。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketScope.TcpChannel.cs` | TCP 明文通道实现（含 WebGL 和非 WebGL 两套接收逻辑） |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `IsNeedConnect` | `bool`（override） | 始终返回 `true` |

## 公开 API

```csharp
// 将 TcpMessage 封装为二进制帧：[协议ID 4B][序列号 4B][消息体 NB]
override byte[] PackageMessageToBytes(NetMessageBase message)

// 判断是否为心跳消息（CommandID == MessageType.HeartBeat）
override bool IsHeartBeatMessage(NetMessageBase message)

// 判断是否为身份认证消息（CommandID == MessageType.Authenticate）
override bool IsAuthenticateMessage(NetMessageBase message)

// 判断认证响应是否成功（解析 JSON 中的 success 字段）
override bool IsAuthenticateMessageSuccess(NetMessageBase message)

// 获取通道类型
override NetChannelType GetNetChannelType()  // => NetChannelType.Tcp

// 判断通道类型与地址是否匹配
override bool Equals(NetChannelType channelType, string serverAddress)
```

## 消息解包流程

```
接收字节流 recvBytes
  ├─ CommandID  = recvBytes[0..3]（网络字节序→主机字节序）
  ├─ SequenceID = recvBytes[4..7]
  └─ bodyBytes  = recvBytes[8..]
       └─ 循环解析：[4字节长度][UTF-8字符串] → 添加到 Messages 列表
```

## 关联文档

- [NetChannelBase](NetChannelBase.md)
- [TcpMessage](../Messages/TcpMessage.md)
- [MessageType](../Messages/MessageType.md)
- [NetChannelType](NetChannelType.md)
