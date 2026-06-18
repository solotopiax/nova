# IHttpManager

**类签名**：`public interface IHttpManager : IDownloadService`
**命名空间**：`NovaFramework.Runtime`

HTTP 管理器公开接口，继承 `IDownloadService`，定义 HTTP 短连接请求（GET / POST / RawData / File）的全部契约。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `IHttpManager.cs` | `interface IHttpManager` | HTTP 管理器接口定义，继承 IDownloadService |

---

## § 3 继承关系

```
IDownloadService (public interface)
  └── IHttpManager : IDownloadService
        └── HttpManagerBase (abstract) : IHttpManager
              └── HttpManager (sealed partial)
```

---

## § 4 关键字段表

接口无字段。

---

## § 5 完整公开 API

```csharp
// --- 生命周期 ---
void Initialize(HttpManagerConfig config)

// --- 异步 HTTP 接口（来自 IHttpManager）---

// GET 请求；requestTimeout/connectTimeout 传 -1 使用默认值；headerInfos 为 JSON 键值对格式；默认禁用本地 HTTP 缓存
UniTask<HttpResponse> GetAsync(string url, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)

// POST 请求（字符串 body）
UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)

// POST 请求（字节流 body，明文）
UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)

// POST 请求（multipart 文件上传）
UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)

// --- 继承自 IDownloadService ---

// 异步下载二进制数据（空闲超时 + 进度回调）
// progressCallback 参数为包含已下载字节数与总字节数的 HttpResponse（中间态，IsSuccess 为 false）
UniTask<HttpResponse> DownloadBinaryAsync(string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null, CancellationToken cancellationToken = default)

// 异步下载文本内容（返回 HttpResponse，文本在 Body 字段中）
UniTask<HttpResponse> DownloadTextAsync(string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null, CancellationToken cancellationToken = default)
```

---

## § 11 使用示例

```csharp
// --- UniTask 异步 GET ---
HttpResponse response = await Nova.Network.GetAsync("https://api.example.com/config");
try
{
    if (response.IsSuccess)
        Debug.Log(response.Body);
}
finally
{
    ReferencePool.Put(response);
}

// --- UniTask 异步 POST（字符串 body）---
HttpResponse postResp = await Nova.Network.PostAsync(
    "https://api.example.com/submit", "{\"key\":\"value\"}");
try
{
    // 处理响应
}
finally
{
    ReferencePool.Put(postResp);
}

// --- 下载二进制（继承自 IDownloadService）---
HttpResponse bin = await Nova.Network.DownloadBinaryAsync(
    "https://cdn.example.com/patch.zip", idleTimeout: 30,
    progressCallback: (progress) =>
    {
        try
        {
            Debug.Log($"已下载 {progress.DownloadedBytes} bytes，进度 {progress.DownloadProgress * 100f:F1}%");
        }
        finally
        {
            ReferencePool.Put(progress);
        }
    });
try
{
    // 处理下载结果
}
finally
{
    ReferencePool.Put(bin);
}
```

---

## § 13 关联文档

- [IDownloadService.md](IDownloadService.md)
- [HttpResponse.md](Definitions/HttpResponse.md)
- [HttpManager.md](HttpManager.md)
- [HttpManagerBase.md](HttpManagerBase.md)
- [HttpManagerConfig.md](Definitions/HttpManagerConfig.md)
