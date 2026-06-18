# WebSocketScope.TcpMessage

**类签名**：`public sealed class TcpMessage : NetMessageTcpBase`（嵌套于 `WebSocketScope`）
**命名空间**：`NovaFramework.Runtime`

TCP 明文协议网络消息，消息体为多条 UTF-8 字符串的二进制打包。每条字符串前置 4 字节网络字节序长度头。用于 `TcpChannel` 的消息编解码。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketScope.TcpMessage.cs` | TCP 明文消息定义 |

## 消息帧格式

```
+---------+-----------+------------+--------------+-----+------------+--------------+
|   4字节  |   4字节   |  消息1长度  |   消息1内容  | ... |  消息X长度  |  消息X内容   |
|  协议ID  |  序列号   |   (4字节)   |  (UTF-8)    |     |   (4字节)  |  (UTF-8)    |
+---------+-----------+------------+--------------+-----+------------+--------------+
```

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `Messages` | `List<string>` | 消息字符串列表（发送时编码，接收时解码后填充） |
| `m_BodyBytes` | `List<byte>`（私有） | 消息体字节流，Initialize 时预计算 |

## 公开 API

```csharp
// 构造方法
TcpMessage()

// 初始化发送消息（序列号由通道自动分配，消息体同步预计算）
void Initialize(NetChannelBase channel, int commandID, List<string> messages)

// 获取消息体字节长度
override int GetBodyLength()

// 复位消息对象（清空消息列表和消息体字节缓存，供对象池复用）
override void Reset()

// 获取消息体字节数组
byte[] GetBodyBytes()
```

## 关联文档

- [NetMessageTcpBase](NetMessageTcpBase.md)
- [TcpChannel](../Channels/TcpChannel.md)
- [WebSocketScope](../WebSocketScope.md)
