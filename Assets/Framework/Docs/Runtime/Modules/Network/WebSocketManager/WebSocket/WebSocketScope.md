# WebSocketScope

**类签名**：`public partial class WebSocketScope`
**命名空间**：`NovaFramework.Runtime`

WebSocket 模块所有类型的命名容器（分部类）。将通道类型、消息类型、通道基类、消息基类、WebGL 桥接等全部以嵌套类/枚举的形式组织在此命名空间下，通过 `partial` 关键字分布在多个文件中，保持逻辑内聚。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketScope.cs` | 分部类主体（空壳） |
| `WebSocketState.cs` | 嵌套枚举 `WebSocketState`：连接就绪状态 |
| `Messages/WebSocketScope.MessageType.cs` | 嵌套枚举 `MessageType`：内置协议 ID |
| `Messages/WebSocketScope.NetMessageBase.cs` | 嵌套抽象类 `NetMessageBase`：消息基类 |
| `Messages/WebSocketScope.NetMessageTcpBase.cs` | 嵌套类 `NetMessageTcpBase`：TCP 消息基类 |
| `Messages/WebSocketScope.TcpMessage.cs` | 嵌套密封类 `TcpMessage`：TCP 明文消息 |
| `Messages/WebSocketScope.TcpPbMessage.cs` | 嵌套密封类 `TcpPbMessage`：TCP Protobuf 消息 |
| `Channels/WebSocketScope.NetChannelType.cs` | 嵌套枚举 `NetChannelType`：通道类型 |
| `Channels/WebSocketScope.NetChannelBase.cs` | 嵌套抽象类 `NetChannelBase`：通道基类 |
| `Channels/WebSocketScope.TcpChannel.cs` | 嵌套密封类 `TcpChannel`：TCP 明文通道 |
| `Channels/WebSocketScope.TcpPbChannel.cs` | 嵌套密封类 `TcpPbChannel`：TCP Protobuf 通道 |
| `WebSocketScope.WebGL.cs` | 嵌套静态类 `WebGL`：WebGL 平台原生 JS 库桥接 |

## 类型总览

```
WebSocketScope (partial)
  ├── enum WebSocketState          // 连接就绪状态
  ├── enum MessageType             // 内置协议 ID
  ├── enum NetChannelType          // 通道类型
  ├── abstract class NetMessageBase       // 消息基类
  │     └── class NetMessageTcpBase       // TCP 消息基类
  │           ├── sealed class TcpMessage    // TCP 明文消息
  │           └── sealed class TcpPbMessage  // TCP Protobuf 消息
  ├── interface IWebSocketManagerBridge   // 管理器轻量桥接接口
  ├── abstract class NetChannelBase       // 通道基类
  │     ├── sealed class TcpChannel       // TCP 明文通道
  │     └── sealed class TcpPbChannel     // TCP Protobuf 通道
  └── static class WebGL                 // WebGL 原生库桥接（仅 WebGL 平台编译）
```

## 关联文档

- [WebSocketState](WebSocketState.md)
- [MessageType](Messages/MessageType.md)
- [NetMessageBase](Messages/NetMessageBase.md)
- [NetMessageTcpBase](Messages/NetMessageTcpBase.md)
- [TcpMessage](Messages/TcpMessage.md)
- [TcpPbMessage](Messages/TcpPbMessage.md)
- [NetChannelType](Channels/NetChannelType.md)
- [NetChannelBase](Channels/NetChannelBase.md)
- [TcpChannel](Channels/TcpChannel.md)
- [TcpPbChannel](Channels/TcpPbChannel.md)
- [WebGL](WebGL.md)
- [WebSocketManager](../WebSocketManager.md)
