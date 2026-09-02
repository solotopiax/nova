# HttpManagerBase

类签名： internal abstract class HttpManagerBase : FrameworkManager, IHttpManager  
命名空间： NovaFramework.Runtime

HTTP 管理器抽象基类，统一声明 HTTP 请求、下载与生命周期契约；Priority = 8，由 HttpManager 实现。

## 继承关系

~~~text
FrameworkManager
  └── HttpManagerBase : IHttpManager : IDownloadService   Priority = 8
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

public abstract UniTask<HttpResponse> DownloadBinaryAsync(
    string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null,
    CancellationToken cancellationToken = default);
public abstract UniTask<HttpResponse> DownloadTextAsync(
    string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null,
    CancellationToken cancellationToken = default);
~~~

## 关联文档

- [IHttpManager.md](IHttpManager.md)
- [HttpManager.md](HttpManager.md)
- [HttpManagerConfig.md](Definitions/HttpManagerConfig.md)
