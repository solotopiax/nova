# HttpSettings

类签名： [Serializable] public class HttpSettings  
命名空间： NovaFramework.Runtime

HTTP 管理器的序列化配置。框架 HTTP 固定使用 UnityWebRequest 和系统 DNS，提供 UWR 埋点、HostKey + NetCmd 主备策略与请求超时配置。

## 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| [Network/Definitions/HttpSettings.cs](../../../../../Scripts/Runtime/Modules/Network/Definitions/HttpSettings.cs) | HttpSettings | UWR 埋点、业务主备策略与 HTTP 请求超时字段 |

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
    [Min(0.1f)]
    public float StartupAppConfigRequestTimeout = 5f;
}
~~~

`EnableUWRTracks` 控制 UWR 请求链事件是否派发到通用 `ITrackPlugin`。`PreferLastSuccessfulHost` 仅影响 HostKey + NetCmd 业务请求，默认优先当前进程中同 HostKey 最近成功的域名。`BusinessFallbackRoundCount` 是每个重试周期内的完整候选轮数；全部轮数耗尽后才消耗一次 `RetryRequestCount`。若单轮有 C 个候选，最多物理请求数为 `C × BusinessFallbackRoundCount × (RetryRequestCount + 1)`。

`RequestTimeout` 是每一次物理 HTTP 请求独享的完整总超时。候选链不会由候选数、轮数或重试次数推导额外的链路总超时；Nova.Network 的 HTTP API 将超时参数传为 -1 时使用此默认值。

`StartupAppConfigRequestTimeout` 是「启动时GM配置请求超时时间（秒）」，默认 5 秒，按每次物理请求分别计时。主地址超时后，备用地址获得独立的完整时限，重试也重新计时，语义与 `RequestTimeout` 一致；整轮总耗时会随候选数量与重试次数增加。独立发起的手动刷新与普通业务请求继续使用 `RequestTimeout`；并发刷新合流时沿用先发起请求的单次超时设置。

该值由 `NetworkComponent` 注入 [NetworkManagerConfig](../NetworkManager/Definitions/NetworkManagerConfig.md)，通过内部就绪接口提供给 Config；不传给通用 `HttpManagerConfig`，也不改变 Network 加载路由数据的职责。运行时对非正数、NaN 或无穷值回退为 5 秒；Inspector 最小可编辑值为 0.1 秒。修改 Inspector 后从下一次初始化生效，不在运行中重新注入已有 Manager。

兼容属性 `BusinessRequestRoundCount` 读写同一个 BusinessFallbackRoundCount 字段，`EnableUwrTelemetry` 读写同一个 EnableUWRTracks 字段，没有独立开关或重试计数。setter 本身不 clamp，也不会自动更新已经 Initialize 的 Manager；Inspector 标注不代替运行时初始化校验。

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
