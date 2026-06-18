# WebSocketScope.NetChannelType

**类签名**：`public enum NetChannelType : byte`（嵌套于 `WebSocketScope`）
**命名空间**：`NovaFramework.Runtime`

通信通道类型枚举，区分 TCP 明文通道和 TCP Protobuf 通道。在 `WebSocketManager` 的所有通道管理 API 中作为通道标识参数使用。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketScope.NetChannelType.cs` | 通道类型枚举定义 |

## 枚举值

| 值 | 说明 |
|------|------|
| `None` | 无效 |
| `Tcp` | TCP 明文通道 |
| `TcpPb` | TCP Protobuf 通道 |

## 关联文档

- [WebSocketScope](../WebSocketScope.md)
- [NetChannelBase](NetChannelBase.md)
- [TcpChannel](TcpChannel.md)
- [TcpPbChannel](TcpPbChannel.md)
