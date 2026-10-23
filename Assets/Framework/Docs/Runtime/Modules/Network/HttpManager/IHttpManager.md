# IHttpManager

类签名： public interface IHttpManager : IDownloadService  
命名空间： NovaFramework.Runtime

HTTP 管理器公开契约。HTTP 固定使用 UnityWebRequest 和系统 DNS；返回的 HttpResponse 是引用池对象，调用方使用完毕必须 ReferencePool.Put。

## 继承关系

~~~text
IDownloadService
  └── IHttpManager
        └── HttpManagerBase
              └── HttpManager
~~~

## 公开 API

~~~csharp
void Initialize(HttpManagerConfig config);

UniTask<HttpResponse> GetAsync(
    string url,
    float requestTimeout = -1f,
    string headerInfos = null);

UniTask<HttpResponse> PostAsync(
    string url,
    string contentString,
    float requestTimeout = -1f,
    string headerInfos = null);

UniTask<HttpResponse> PostRawDataAsync(
    string url,
    byte[] contentBytes,
    float requestTimeout = -1f,
    string headerInfos = null);

UniTask<HttpResponse> PostFileAsync(
    string url,
    string bodyJsonData,
    byte[] fileBytes,
    string fileName,
    float requestTimeout = -1f,
    string headerInfos = null);

UniTask<HttpResponse> DownloadBinaryAsync(
    string url,
    int idleTimeout = -1,
    Action<HttpResponse> progressCallback = null,
    CancellationToken cancellationToken = default);

UniTask<HttpResponse> DownloadTextAsync(
    string url,
    int idleTimeout = -1,
    Action<HttpResponse> progressCallback = null,
    CancellationToken cancellationToken = default);
~~~

内置实现中，requestTimeout 的任意负数使用初始化配置的 RequestTimeout（默认 60 秒）；0 会在请求仍未完成的轮询中立即触发超时，并不表示无限等待。正数超时同时使用向上取整的 UWR timeout 和实时钟检查；配置层不校验 NaN/Infinity，业务应传有限有效秒数。headerInfos 可为 null/空串，否则必须是 JSON 对象字符串；GetAsync 会附加 Cache-Control: no-cache，除非调用方已经提供该请求头。

| 输入 | 内置实现的发送规则 |
|---|---|
| `url` | 当前这一次请求的完整 URL，不拼 HostKey/NetCmd。 |
| `contentString` | null 转空串，再按 UTF-8 编码；默认 Content-Type 仍是 `application/octet-stream`，不是自动 JSON。 |
| `contentBytes` | null 按空字节发送；本层不深拷贝，也不自动 AES、GZip 或 Base64。请求完成前不要修改传入数组。 |
| `headerInfos` | 一级属性转换为请求头；值通过 ToString 转为字符串。可以覆盖默认 Content-Type；无效 JSON/请求头异常可能向上传播。 |
| `bodyJsonData` | null/空串不添加普通表单字段；否则要求 JSON 对象，仅把一级属性转为 multipart 文本字段。 |
| `fileBytes / fileName` | null 分别转为空字节/空名称；文件表单项固定为 `file`，文件项 Content-Type 为 `application/octet-stream`。 |

上表描述内置 Manager/Transport 的处理；常用 `Nova.Network` 门面还先校验 URL 非 null/非空，并拒绝 null 的原始字节或文件字节、null/空的文件名。不能把底层空值转换理解为组件门面允许同样的输入。

构造 URL、解析 JSON、设置请求头或编码 multipart 的失败可能发生在传输执行 try 之外，不承诺所有错误都返回失败 HttpResponse。实际发送中的超时、取消和传输异常才由内置执行链映射成失败响应；调用方既要处理异常，也要对已取得的最终响应负责归池。失败响应可能仍包含正文、原始字节、响应头和正式状态码。

普通 HTTP API 只请求调用方传入的一个 URL，不会自动推断或补充备用地址。框架内部的 HostKey + NetCmd 业务协议另行取得主、备 URL，并复用同一份请求体与请求头完成切换。

## 可选物理取消能力

同一源码文件还声明 `public interface ICancellableHttpManager`。内置 `HttpManagerBase` 同时实现两种接口；`IHttpManager` 的旧成员和默认参数完整保留，旧自定义实现无需补成员。`INetworkManager` 仍只负责路由等网络管理能力。

~~~csharp
UniTask<HttpResponse> GetAsync(string url, float requestTimeout,
    string headerInfos, CancellationToken cancellationToken);
UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout,
    string headerInfos, CancellationToken cancellationToken);
UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout,
    string headerInfos, CancellationToken cancellationToken);
UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes,
    string fileName, float requestTimeout, string headerInfos, CancellationToken cancellationToken);
~~~

这些重载把令牌放最后，原参数全部必填，因此 `GetAsync(url, default)` 等旧调用仍选中旧签名。`Nova.Network` 的同名重载先检查当前管理器是否实现 `ICancellableHttpManager`；不支持时在发送前抛 `NotSupportedException`，不会退回不可取消入口，即使传入 `CancellationToken.None` 也是如此。

- 发送前已取消：不构建/发送 UWR，不产生发送埋点，返回 `IsSuccess=false`、`StatusCode=0`、`DeliveryState=NotReachedServer` 的池化响应。
- 请求进行中取消：内置传输在后续 `Update` 轮询中调用 `UnityWebRequest.Abort()`，释放 UWR 和 handler，然后返回失败响应。取消以响应表示，任务不因传输取消抛 `OperationCanceledException`；已收到的状态、正文与交付分类沿用响应契约。
- 取消不能撤销服务端已执行状态。未取得正式响应且无法证明未到达服务端时，交付状态为 `Unknown`；不得据此直接重放有副作用的请求。
- 旧无令牌入口仍不支持进行中取消。管理器 `Shutdown()` 和页面退出不会自动 Abort；拥有者需主动取消传入的令牌，继续等待取得最终响应并且仅归池一次。`CancellationTokenSource.Dispose()` 不等于取消。

不要给返回池化响应的请求套 `AttachExternalCancellation` 来替代此能力：它只停止包装等待，底层请求仍运行，调用方可能失去最终响应的归池责任。

~~~csharp
// cancellationToken 由页面/Service拥有者在退出时主动取消。
HttpResponse response = await Nova.Network.GetAsync(url, -1f, null, cancellationToken);
try
{
    if (cancellationToken.IsCancellationRequested) return; // 拒绝迟到的业务结果
    if (response.IsSuccess) Debug.Log(response.Body);
}
finally
{
    ReferencePool.Put(response);
}
~~~

## 使用示例

~~~csharp
HttpResponse response = await Nova.Network.GetAsync(
    "https://api.example.com/config");
try
{
    if (response.IsSuccess)
    {
        Debug.Log(response.Body);
    }
}
finally
{
    ReferencePool.Put(response);
}
~~~

## 关联文档

- [HttpManager.md](HttpManager.md)
- [HttpManagerBase.md](HttpManagerBase.md)
- [IDownloadService.md](IDownloadService.md)
- [HttpResponse.md](Definitions/HttpResponse.md)
