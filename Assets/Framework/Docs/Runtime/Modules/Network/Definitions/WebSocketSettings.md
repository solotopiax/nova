# WebSocketSettings

**类签名**：`[Serializable] public class WebSocketSettings`
**命名空间**：`NovaFramework.Runtime`

WebSocket 管理器配置，在 Inspector 中集中管理 WebSocket 连接、认证、心跳与重连的全部参数。当前还暴露了 `AutoReconnectFailedUIAssetLocation` 字段，但运行时只负责透传到 `WebSocketManagerConfig`，`WebSocketManager` 本体尚未消费这条配置。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `WebSocketSettings.cs` | `WebSocketSettings` | 定义：8 个序列化字段（含默认值） |

---

## § 3 继承关系

```
WebSocketSettings（[Serializable] class，无继承）
```

---

## § 4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `ConnectTimeout` | `float` | `10f` | WebSocket 连接超时时间（秒） |
| `AuthenticateTimeout` | `float` | `10f` | WebSocket 身份认证超时时间（秒） |
| `HeartBeatTimeInterval` | `float` | `20f` | 心跳发送间隔（秒） |
| `HeartBeatTimeout` | `float` | `10f` | 心跳响应超时时间（秒） |
| `EnableAutoReconnect` | `bool` | `true` | 是否启用 WebSocket 自动重连 |
| `AutoReconnectMaxCounter` | `int` | `5` | 最大重连次数 |
| `AutoReconnectTimeInterval` | `float` | `3f` | 重连间隔时间（秒） |
| `AutoReconnectFailedUIAssetLocation` | `string` | `null` | 自动重连失败提示界面的资源地址；当前运行时尚未消费 |

---

## § 5 完整公开 API

```csharp
// 数据类，无方法；通过字段直接访问
float ConnectTimeout
float AuthenticateTimeout
float HeartBeatTimeInterval
float HeartBeatTimeout
bool  EnableAutoReconnect
int   AutoReconnectMaxCounter
float AutoReconnectTimeInterval
string AutoReconnectFailedUIAssetLocation
```

---

## § 11 使用示例

```csharp
// Inspector 中配置 m_WebSocketSettings
// NetworkComponent.Start 中映射到 WebSocketManagerConfig
m_WebSocketManager.Initialize(new WebSocketManagerConfig
{
    ConnectTimeout        = m_WebSocketSettings.ConnectTimeout,
    AuthenticateTimeout   = m_WebSocketSettings.AuthenticateTimeout,
    HeartBeatTimeInterval = m_WebSocketSettings.HeartBeatTimeInterval,
    HeartBeatTimeout      = m_WebSocketSettings.HeartBeatTimeout,
    AutoReconnectMaxCounter       = m_WebSocketSettings.AutoReconnectMaxCounter,
    AutoReconnectTimeInterval     = m_WebSocketSettings.AutoReconnectTimeInterval,
    EnableAutoReconnect           = m_WebSocketSettings.EnableAutoReconnect,
    AutoReconnectFailedUIAssetLocation = m_WebSocketSettings.AutoReconnectFailedUIAssetLocation,
    CoroutineRunner       = this
});
```

---

## § 13 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [WebSocketManager.md](../WebSocketManager/WebSocketManager.md)
