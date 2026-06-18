# WebSocketManagerConfig

**类签名**：`public class WebSocketManagerConfig`
**命名空间**：`NovaFramework.Runtime`

WebSocket 管理器配置类，用于 `WebSocketManager.Initialize()` 时传入初始化参数。控制连接/认证/心跳超时、自动重连策略、协程运行器及特殊消息创建委托。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketManagerConfig.cs` | WebSocket 管理器配置数据类定义 |

## 关键字段/属性

| 字段 | 类型 | 默认值 | 说明 |
|------|------|------|------|
| `ConnectTimeout` | `float` | `10f` | 连接超时时间（秒） |
| `AuthenticateTimeout` | `float` | `10f` | 身份认证超时时间（秒） |
| `HeartBeatTimeInterval` | `float` | `20f` | 心跳发送间隔（秒） |
| `HeartBeatTimeout` | `float` | `10f` | 心跳响应超时时间（秒） |
| `AutoReconnectMaxCounter` | `int` | `5` | 自动重连最大次数，超过后触发 OnReconnectFailed 事件 |
| `AutoReconnectTimeInterval` | `float` | `3f` | 自动重连间隔时间（秒） |
| `EnableAutoReconnect` | `bool` | `true` | 是否启用自动重连机制 |
| `AutoReconnectFailedUIAssetLocation` | `string` | `null` | 自动重连失败提示界面的 Asset 地址；当前配置会被透传，但 `WebSocketManager` 运行时尚未消费 |
| `CoroutineRunner` | `ICoroutineRunner` | `null` | 协程运行器接口，由 NetworkComponent 注入 |
| `SpecialMessageCreator` | `Func<NetChannelType, string, NetMessageBase>` | `null` | 特殊消息创建委托（心跳/认证消息），messageCategory 值为 `"heartbeat"` 或 `"authenticate"` |

## 公开 API

```csharp
// 纯数据类，无方法。所有字段均可直接赋值
public float ConnectTimeout = 10f;
public float AuthenticateTimeout = 10f;
public float HeartBeatTimeInterval = 20f;
public float HeartBeatTimeout = 10f;
public int AutoReconnectMaxCounter = 5;
public float AutoReconnectTimeInterval = 3f;
public bool EnableAutoReconnect = true;
public string AutoReconnectFailedUIAssetLocation;
public ICoroutineRunner CoroutineRunner;
public Func<WebSocketScope.NetChannelType, string, WebSocketScope.NetMessageBase> SpecialMessageCreator;
```

## 关联文档

- [WebSocketManager](../WebSocketManager.md)
- [IWebSocketManager](../IWebSocketManager.md)
- [WebSocketScope](../WebSocket/WebSocketScope.md)
