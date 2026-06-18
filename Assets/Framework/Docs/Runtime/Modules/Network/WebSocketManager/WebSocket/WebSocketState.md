# WebSocketScope.WebSocketState

**类签名**：`public enum WebSocketState : ushort`（嵌套于 `WebSocketScope`）
**命名空间**：`NovaFramework.Runtime`

WebSocket 连接就绪状态枚举，对应 HTML5 WebSocket ReadyState 标准定义。在 `NetChannelBase` 中用于判断连接是否可用。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketState.cs` | WebSocket 连接状态枚举定义 |

## 枚举值

| 值 | 编号 | 说明 |
|------|------|------|
| `Connecting` | 0 | 连接尚未建立 |
| `Open` | 1 | 连接已建立，可以通信 |
| `Closing` | 2 | 连接正在进行关闭握手，或已调用 close 方法 |
| `Closed` | 3 | 连接已关闭或无法建立 |

## 关联文档

- [WebSocketScope](WebSocketScope.md)
- [NetChannelBase](Channels/NetChannelBase.md)
