# HttpManagerBase

类签名： internal abstract class HttpManagerBase : FrameworkManager, IHttpManager, ICancellableHttpManager
命名空间： NovaFramework.Runtime

HTTP 管理器抽象基类，统一声明 HTTP 请求、下载与生命周期契约；Priority = 8，由 HttpManager 实现。

## 继承关系

~~~text
FrameworkManager
  └── HttpManagerBase : IHttpManager, ICancellableHttpManager   Priority = 8
        └── HttpManager
~~~

## API 摘要

~~~csharp
public override int Priority => 8;

public abstract void Initialize(HttpManagerConfig config);
public abstract override void Update();
public abstract override void Shutdown();

public abstract UniTask<HttpResponse> GetAsync(
    string url, float requestTimeout = -1f, string headerInfos = null);
public abstract UniTask<HttpResponse> PostAsync(
    string url, string contentString, float requestTimeout = -1f, string headerInfos = null);
public abstract UniTask<HttpResponse> PostRawDataAsync(
    string url, byte[] contentBytes, float requestTimeout = -1f, string headerInfos = null);
public abstract UniTask<HttpResponse> PostFileAsync(
    string url, string bodyJsonData, byte[] fileBytes, string fileName,
    float requestTimeout = -1f, string headerInfos = null);

public abstract UniTask<HttpResponse> GetAsync(
    string url, float requestTimeout, string headerInfos, CancellationToken cancellationToken);
public abstract UniTask<HttpResponse> PostAsync(
    string url, string contentString, float requestTimeout, string headerInfos, CancellationToken cancellationToken);
public abstract UniTask<HttpResponse> PostRawDataAsync(
    string url, byte[] contentBytes, float requestTimeout, string headerInfos, CancellationToken cancellationToken);
public abstract UniTask<HttpResponse> PostFileAsync(
    string url, string bodyJsonData, byte[] fileBytes, string fileName,
    float requestTimeout, string headerInfos, CancellationToken cancellationToken);

public abstract UniTask<HttpResponse> DownloadBinaryAsync(
    string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null,
    CancellationToken cancellationToken = default);
public abstract UniTask<HttpResponse> DownloadTextAsync(
    string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null,
    CancellationToken cancellationToken = default);
~~~

新重载实现可选 `ICancellableHttpManager` 能力，不向旧 `IHttpManager` 增加成员。令牌位于最后，原参数必填以保留旧默认参数调用的选择。取消返回失败池化响应；完整发送前、进行中、归池和旧实现拒绝语义见 [IHttpManager.md](IHttpManager.md#可选物理取消能力)。

## 关联文档

- [IHttpManager.md](IHttpManager.md)
- [HttpManager.md](HttpManager.md)
- [HttpManagerConfig.md](Definitions/HttpManagerConfig.md)
