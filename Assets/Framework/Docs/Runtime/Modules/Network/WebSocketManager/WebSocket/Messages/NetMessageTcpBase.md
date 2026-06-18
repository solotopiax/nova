# WebSocketScope.NetMessageTcpBase

**类签名**：`public class NetMessageTcpBase : NetMessageBase`（嵌套于 `WebSocketScope`）
**命名空间**：`NovaFramework.Runtime`

TCP 协议网络消息基类，在 `NetMessageBase` 基础上增加协议 ID（CommandID）和序列号（SequenceID）。作为 `TcpMessage` 和 `TcpPbMessage` 的共同父类。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketScope.NetMessageTcpBase.cs` | TCP 协议消息基类定义 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `CommandID` | `int` | 协议 ID（通信协议的唯一标识） |
| `SequenceID` | `int` | 序列号（该协议 ID 下的通信顺序编号，从 1 开始自增） |

## 公开 API

```csharp
// 构造方法
NetMessageTcpBase()

// 初始化消息（仅设置协议 ID，序列号由通道层填充）
void Initialize(int commandID)

// 获取消息体字节长度（基类默认返回 0）
override int GetBodyLength()

// 复位协议 ID 与序列号
override void Reset()
```

## 关联文档

- [NetMessageBase](NetMessageBase.md)
- [TcpMessage](TcpMessage.md)
- [TcpPbMessage](TcpPbMessage.md)
