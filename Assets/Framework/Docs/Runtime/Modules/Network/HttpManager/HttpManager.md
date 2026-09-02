# HttpManager

类签名： internal sealed partial class HttpManager : HttpManagerBase  
命名空间： NovaFramework.Runtime  
全局访问： Nova.Network

HTTP 短连接管理器，固定以 UnityWebRequest 和系统 DNS 执行 GET、POST、文件上传与下载。框架不再提供可替换 HTTP 后端、指定连接 IP 或网络遥测扩展点。

## 文件表

| 文件 | 说明 |
|---|---|
| HttpManager.cs | 初始化固定的 UnityWebRequest 实现，并转发公开 HTTP 与下载 API |
| HttpManager.Visitors.cs | 默认请求总超时、业务路由偏好与内部传输字段 |
| HttpManager.Methods.cs | HostKey + NetCmd 的最近成功优先、多轮与重试请求链 |
| HttpManager.Telemetry.cs | 单 URL 和业务主备链的 UWR 埋点编排 |
| Diagnostics/UwrNetworkTelemetry.cs | `1 start → 0～N error → 1 end` 事件构造与派发 |
| Transports/UnityWebRequestTransport.cs | 请求、上传、下载、取消、进度与空闲超时实现 |

## 公开 API

公开签名见 [IHttpManager.md](IHttpManager.md)。所有 requestTimeout 参数传 -1 时使用默认 60 秒；HTTP 只提供请求总超时参数。

GetAsync 禁用不可控的本地缓存复用。DownloadBinaryAsync 与 DownloadTextAsync 的 idleTimeout 传 -1 时使用默认请求超时；只要持续收到新字节，下载不会因总耗时而被空闲超时中断。

## HostKey + NetCmd 主备链

业务 Kit 通过 NetService 发起的请求会先冻结请求字节和请求头，再使用 NetworkManager.ResolveNetCmdUrls 提供的主备地址。默认启用当前进程内的“最近成功域名优先”；记录按 HostKey 隔离，仅在新成功覆盖、配置候选已不包含该域名、网络可达性变化或管理器关闭时失效，普通整链失败不会清除。

~~~text
最近成功域名（如有） → 其余主备候选
~~~

每个候选都是一次独立的 UnityWebRequest，通过系统 DNS 解析。任何正式 HTTP 响应都会立即结束链路，包含 2xx、4xx 与 5xx；只有未取得正式 HTTP 响应的传输失败才会尝试下一个候选。`BusinessFallbackRoundCount` 定义每次完整执行包含的候选轮数；全部轮数耗尽后才消耗一次 `RetryRequestCount`，每次重试重新执行全部轮次。若单轮去重候选数为 C、轮数为 R、重试次数为 K，则最多物理请求数为 `C × R × (K + 1)`。

~~~text
本轮第一候选
  ├─ 收到 HTTP 响应（任意状态码） → 返回该响应，不切换
  └─ 无正式 HTTP 响应            → 尝试备用域名
       ├─ 收到 HTTP 响应          → 返回该响应
       └─ 仍无正式 HTTP 响应      → 返回最终失败响应
~~~

若某次失败无法确认请求是否到达服务器，最终 HttpResponse.DeliveryState 会标记为 Unknown；可明确归因于域名解析、TLS、证书或建连失败的请求标记为 NotReachedServer。有副作用的业务接口仍应由既有协议保证重复请求安全。

该主备链仅属于 HostKey + NetCmd 业务协议。普通 Nova.Network HTTP 调用、文件上传和调用方自行提供的单 URL 下载不会被框架自动扩展为主备请求；App 与 Asset 的更新路径各自维护配置和失败分类，但共同复用 Core 的候选计划、完整轮次、完整重试周期与最近成功偏好机制。Asset 的每个文件独立冻结计划，不再由并发 Bundle 共享一个包级推进游标；YooAsset 仍负责触发物理下载和重试回调。

## UWR 埋点

启用 `EnableUWRTracks` 后，每条逻辑请求严格上报 `1 uwr_request_start → 0～N uwr_request_error → 1 uwr_request_end`。业务主备、多轮和重试仍属于同一条逻辑链；`uwr_retry_index`、`uwr_round_index`、`uwr_candidate_index` 与 `uwr_send_index` 可还原每次物理发送。三个事件通过 `uwr_chain_id` 关联，schema 版本固定为 1；完整字段定义见 `Assets/Framework/Tracks/Tracks.xlsx`。

## 使用示例

~~~csharp
HttpResponse response = await Nova.Network.PostAsync(
    "https://api.example.com/submit",
    "{\"key\":\"value\"}");
try
{
    Debug.Log(response.StatusCode);
}
finally
{
    ReferencePool.Put(response);
}
~~~

## 注意事项

| 场景 | 当前语义 |
|---|---|
| HTTP 后端 | 固定使用 Unity 自带 UnityWebRequest。 |
| DNS | 使用系统 DNS；请求 URL 保持域名形式。 |
| HTTP 状态码 | 4xx/5xx 是正式服务器响应，会终止业务主备链。 |
| 超时 | RequestTimeout 由每次物理请求完整使用；不存在自动推导的候选链总超时。 |
| 响应对象 | HttpResponse 是池化对象；所有取得它的调用方负责归还。 |
| 下载进度 | 中间 HttpResponse 仅在当次回调内有效，由框架自动归还；回调异常会被隔离并停止后续进度回调，不中止下载。 |

## 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [IHttpManager.md](IHttpManager.md)
- [HttpManagerConfig.md](Definitions/HttpManagerConfig.md)
- [HttpResponse.md](Definitions/HttpResponse.md)
- [NetService.md](../NetService.md)
- [NetworkManager.md](../NetworkManager/NetworkManager.md)
