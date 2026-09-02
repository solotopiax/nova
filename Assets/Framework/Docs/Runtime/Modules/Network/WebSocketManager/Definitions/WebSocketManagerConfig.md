# WebSocketManagerConfig

类签名： public class WebSocketManagerConfig  
命名空间： NovaFramework.Runtime

WebSocketManager.Initialize 的初始化数据。它独立管理 WebSocket 的连接、认证、心跳与重连超时。

## 字段

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---:|---|
| ConnectTimeout | float | 10f | WebSocket 连接超时时间（秒） |
| AuthenticateTimeout | float | 10f | 身份认证超时时间（秒） |
| HeartBeatTimeInterval | float | 20f | 心跳发送间隔（秒） |
| HeartBeatTimeout | float | 10f | 心跳响应超时时间（秒） |
| AutoReconnectMaxCounter | int | 5 | 自动重连最大次数 |
| AutoReconnectTimeInterval | float | 3f | 自动重连间隔（秒） |
| EnableAutoReconnect | bool | true | 是否启用自动重连 |
| AutoReconnectFailedUIAssetLocation | string | null | 重连失败 UI 的 Asset 地址；当前只透传配置 |
| CoroutineRunner | ICoroutineRunner | null | 由 NetworkComponent 注入的协程运行器 |
| SpecialMessageCreator | Func<NetChannelType, string, NetMessageBase> | null | 心跳和认证消息创建委托 |

## 公开 API

~~~csharp
public float ConnectTimeout = 10f;
public float AuthenticateTimeout = 10f;
public float HeartBeatTimeInterval = 20f;
public float HeartBeatTimeout = 10f;
public int AutoReconnectMaxCounter = 5;
public float AutoReconnectTimeInterval = 3f;
public bool EnableAutoReconnect = true;
public string AutoReconnectFailedUIAssetLocation;
public ICoroutineRunner CoroutineRunner;
public Func<WebSocketScope.NetChannelType, string, WebSocketScope.NetMessageBase>
    SpecialMessageCreator;
~~~

HTTP 的 RequestTimeout 不会替代这里的 ConnectTimeout；两者分别作用于短连接 HTTP 与 WebSocket。

## 关联文档

- [WebSocketManager.md](../WebSocketManager.md)
- [IWebSocketManager.md](../IWebSocketManager.md)
- [WebSocketSettings.md](../../Definitions/WebSocketSettings.md)
