# WebSocketScope.TcpPbMessage

**类签名**：`public sealed class TcpPbMessage : NetMessageTcpBase`（嵌套于 `WebSocketScope`）
**命名空间**：`NovaFramework.Runtime`

TCP Protobuf 协议网络消息，消息体为 Protobuf 编码后的字节流。用于 `TcpPbChannel` 的消息编解码。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketScope.TcpPbMessage.cs` | TCP Protobuf 消息定义 |

## 消息帧格式

```
+---------+-----------+------------+
|   4字节  |   4字节   |   N字节    |
|  协议ID  |  序列号   |  Pb字节流  |
+---------+-----------+------------+
```

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `Bytes` | `byte[]` | Protobuf 编码后的消息字节流 |

## 公开 API

```csharp
// 构造方法
TcpPbMessage()

// 初始化发送消息（序列号由通道自动分配）
void Initialize(NetChannelBase channel, int commandID, byte[] bytes)

// 获取消息体字节长度
override int GetBodyLength()

// 复位消息对象（清空字节流引用，供对象池复用）
override void Reset()
```

## 关联文档

- [NetMessageTcpBase](NetMessageTcpBase.md)
- [TcpPbChannel](../Channels/TcpPbChannel.md)
- [WebSocketScope](../WebSocketScope.md)
