# HttpSettings

类签名： [Serializable] public class HttpSettings  
命名空间： NovaFramework.Runtime

HTTP 管理器的序列化配置。框架 HTTP 固定使用 UnityWebRequest 和系统 DNS，提供 UWR 埋点、HostKey + NetCmd 主备策略与请求超时配置。

## 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| Network/Definitions/HttpSettings.cs | HttpSettings | UWR 埋点、业务主备策略与 HTTP 请求超时字段 |

## 公开 API

~~~csharp
[Serializable]
public class HttpSettings
{
    public bool EnableUWRTracks = true;
    public bool PreferLastSuccessfulHost = true;
    [Min(1)]
    public int BusinessFallbackRoundCount = 1;
    [Min(0)]
    public int RetryRequestCount = 1;
    public float RequestTimeout = 60f;
}
~~~

`EnableUWRTracks` 控制 UWR 请求链事件是否派发到通用 `ITrackPlugin`。`PreferLastSuccessfulHost` 仅影响 HostKey + NetCmd 业务请求，默认优先当前进程中同 HostKey 最近成功的域名。`BusinessFallbackRoundCount` 是每个重试周期内的完整候选轮数；全部轮数耗尽后才消耗一次 `RetryRequestCount`。若单轮有 C 个候选，最多物理请求数为 `C × BusinessFallbackRoundCount × (RetryRequestCount + 1)`。

`RequestTimeout` 是每一次物理 HTTP 请求独享的完整总超时。候选链不会由候选数、轮数或重试次数推导额外的链路总超时；Nova.Network 的 HTTP API 将超时参数传为 -1 时使用此默认值。

## 使用位置

~~~csharp
// NetworkComponent.Start() 中映射到 HttpManagerConfig
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

- [NetworkComponent.md](../NetworkComponent.md)
- [HttpManagerConfig.md](../HttpManager/Definitions/HttpManagerConfig.md)
