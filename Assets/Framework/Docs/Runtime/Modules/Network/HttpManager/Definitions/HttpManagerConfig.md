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
