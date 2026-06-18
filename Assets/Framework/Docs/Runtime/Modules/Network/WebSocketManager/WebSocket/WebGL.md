# WebSocketScope.WebGL

**类签名**：`public static class WebGL`（嵌套于 `WebSocketScope`，仅在 `!UNITY_EDITOR && UNITY_WEBGL` 条件编译下存在）
**命名空间**：`NovaFramework.Runtime`

WebGL 平台 WebSocket 原生 JS 库桥接类。通过 `DllImport("__Internal")` 调用 JavaScript WebSocket API，并通过静态 C# 事件将 JS 回调分发到 C# 消费方，避免直接依赖任何 Framework 模块。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketScope.WebGL.cs` | WebGL 原生库接口桥接定义 |

## 委托类型

| 委托 | 签名 | 说明 |
|------|------|------|
| `OnOpenCallback` | `(int instanceId)` | WebSocket 连接打开回调 |
| `OnMessageCallback` | `(int instanceId, IntPtr msgPtr, int msgSize)` | 收到二进制消息回调 |
| `OnMessageStrCallback` | `(int instanceId, IntPtr msgStrPtr)` | 收到字符串消息回调 |
| `OnErrorCallback` | `(int instanceId, IntPtr errorPtr)` | 发生错误回调 |
| `OnCloseCallback` | `(int instanceId, int closeCode, IntPtr reasonPtr)` | 关闭回调 |

## 事件

| 事件 | 签名 | 说明 |
|------|------|------|
| `OnOpenWebSocket` | `Action<int>` | WebSocket 连接成功事件 |
| `OnMessageWebSocket` | `Action<int, byte[]>` | 收到二进制消息事件 |
| `OnMessageStrWebSocket` | `Action<int, string>` | 收到字符串消息事件 |
| `OnErrorWebSocket` | `Action<int, string>` | 发生错误事件 |
| `OnCloseWebSocket` | `Action<int, int, string>` | 关闭事件 |

## 公开 API

```csharp
// JS 原生方法（DllImport）
static extern int WebSocketConnect(int instanceId)
static extern int WebSocketClose(int instanceId, int code, string reason)
static extern int WebSocketSend(int instanceId, byte[] dataPtr, int dataLength)
static extern int WebSocketSendStr(int instanceId, string data)
static extern int WebSocketGetState(int instanceId)
static extern int WebSocketAllocate(string url, string binaryType)
static extern int WebSocketAddSubProtocol(int instanceId, string protocol)
static extern void WebSocketFree(int instanceId)

// 回调设置方法（DllImport）
static extern void WebSocketSetOnOpen(OnOpenCallback callback)
static extern void WebSocketSetOnMessage(OnMessageCallback callback)
static extern void WebSocketSetOnMessageStr(OnMessageStrCallback callback)
static extern void WebSocketSetOnError(OnErrorCallback callback)
static extern void WebSocketSetOnClose(OnCloseCallback callback)

// 初始化：向 JS 层注册所有事件回调，必须在 WebGL 平台初始化时调用一次
static void Initialize()
```

## 关联文档

- [WebSocketScope](WebSocketScope.md)
- [NetChannelBase](Channels/NetChannelBase.md)
- [WebSocketState](WebSocketState.md)
