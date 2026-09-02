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

requestTimeout = -1 使用 HttpSettings.RequestTimeout（默认 60 秒）。headerInfos 必须是 JSON 对象字符串；GetAsync 会附加 Cache-Control: no-cache，除非调用方已经提供该请求头。

普通 HTTP API 只请求调用方传入的一个 URL，不会自动推断或补充备用地址。框架内部的 HostKey + NetCmd 业务协议另行取得主、备 URL，并复用同一份请求体与请求头完成切换。

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
