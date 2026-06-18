# WebSocketScope.NetMessageBase

**类签名**：`public abstract class NetMessageBase`（嵌套于 `WebSocketScope`）
**命名空间**：`NovaFramework.Runtime`

网络消息抽象基类，定义消息结构的通用外壳与可复用接口。所有 WebSocket 消息均继承此类，支持对象池复用（通过 `Reset()` 方法）。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketScope.NetMessageBase.cs` | 网络消息抽象基类定义 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `s_OuterShellLength` | `const int` | 通用消息外壳长度（字节）：协议 ID 4B + 序列号 4B = 8（编译期常量） |

## 公开 API

```csharp
// 获取消息体字节长度
abstract int GetBodyLength()

// 复位消息对象到初始状态（供对象池复用）
abstract void Reset()
```

## 继承关系

```
NetMessageBase (abstract)
  └── NetMessageTcpBase
        ├── TcpMessage (sealed)
        └── TcpPbMessage (sealed)
```

## 关联文档

- [NetMessageTcpBase](NetMessageTcpBase.md)
- [TcpMessage](TcpMessage.md)
- [TcpPbMessage](TcpPbMessage.md)
- [WebSocketScope](../WebSocketScope.md)
