# HttpManagerConfig

类签名： public class HttpManagerConfig  
命名空间： NovaFramework.Runtime

HTTP 管理器初始化配置。由 NetworkComponent 将 Inspector 中的 UWR 埋点、业务主备策略与默认请求超时注入 HttpManager。

## 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| Managers/HttpManager/Definitions/HttpManagerConfig.cs | HttpManagerConfig | HTTP 初始化数据类 |

## 公开 API

~~~csharp
public class HttpManagerConfig
{
    public bool EnableUWRTracks = true;
    public bool PreferLastSuccessfulHost = true;
    public int BusinessFallbackRoundCount = 1;
    public int RetryRequestCount = 1;
    public float RequestTimeout = 60f;
}
~~~

## 使用位置

`EnableUwrTelemetry` 与 `BusinessRequestRoundCount` 是带 Obsolete 的兼容属性，分别直接读写 `EnableUWRTracks` 与 `BusinessFallbackRoundCount`，不是另一组开关或计数。DTO 本身不做校验；内置 Manager 初始化时将轮数钳制为至少 1、重试次数钳制为至少 0，其余字段直接读取。此后修改配置对象不会更新已初始化的 Manager。

每次 Initialize 都清除最近成功偏好并创建新的 UnityWebRequestTransport；它不是取消既有请求或安全重载传输的接口。管理器尚未 Initialize 时没有可用传输；Shutdown 会清引用/偏好，但不 Abort 在飞请求。

~~~csharp
m_HttpManager.Initialize(new HttpManagerConfig
{
    EnableUWRTracks = m_HttpSettings.EnableUWRTracks,
    PreferLastSuccessfulHost = m_HttpSettings.PreferLastSuccessfulHost,
    BusinessFallbackRoundCount = m_HttpSettings.BusinessFallbackRoundCount,
    RetryRequestCount = m_HttpSettings.RetryRequestCount,
    RequestTimeout = m_HttpSettings.RequestTimeout,
});
~~~

## 关联文档

- [HttpManager.md](../HttpManager.md)
- [NetworkComponent.md](../../NetworkComponent.md)
