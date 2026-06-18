# WebSocketScope.MessageType

**类签名**：`public enum MessageType`（嵌套于 `WebSocketScope`）
**命名空间**：`NovaFramework.Runtime`

内置协议 ID 枚举，用于识别心跳与认证消息。TCP 明文通道和 TCP Protobuf 通道各有独立的协议 ID 段。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketScope.MessageType.cs` | 消息类型（协议 ID）枚举定义 |

## 枚举值

| 值 | 编号 | 说明 |
|------|------|------|
| `None` | 0 | 无效 |
| `Authenticate` | 5002 | TCP 明文认证协议 ID |
| `HeartBeat` | 5003 | TCP 明文心跳协议 ID |
| `AuthenticatePb` | 6002 | TCP Protobuf 认证协议 ID |
| `HeartBeatPb` | 6003 | TCP Protobuf 心跳协议 ID |

## 关联文档

- [WebSocketScope](../WebSocketScope.md)
- [TcpChannel](../Channels/TcpChannel.md)
- [TcpPbChannel](../Channels/TcpPbChannel.md)
