# IDownloadService

**类签名**：`public interface IDownloadService`
**命名空间**：`NovaFramework.Runtime`

下载服务接口，提供异步二进制文件下载与文本下载能力，支持进度回调与取消令牌。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Managers/HttpManager/Interfaces/IDownloadService.cs` | `interface IDownloadService` | 下载服务接口定义 |

---

## §5 完整公开 API

```csharp
public interface IDownloadService
{
    // 异步下载二进制数据
    // idleTimeout：空闲超时（秒），连续 N 秒无新字节时中止，传 -1 使用默认值
    // progressCallback：进度回调，参数为包含已下载字节数与总字节数的 HttpResponse（中间态）
    // cancellationToken：取消令牌
    UniTask<HttpResponse> DownloadBinaryAsync(
        string url,
        int idleTimeout = -1,
        Action<HttpResponse> progressCallback = null,
        CancellationToken cancellationToken = default);

    // 异步下载文本内容
    // idleTimeout：空闲超时（秒），连续 N 秒无新字节时中止，传 -1 使用默认值
    // progressCallback：进度回调
    // cancellationToken：取消令牌
    UniTask<HttpResponse> DownloadTextAsync(
        string url,
        int idleTimeout = -1,
        Action<HttpResponse> progressCallback = null,
        CancellationToken cancellationToken = default);
}
```

---

## §11 使用示例

```csharp
// --- 下载二进制文件（带进度与取消）---
CancellationTokenSource cts = new CancellationTokenSource();

HttpResponse response = await Nova.Network.DownloadBinaryAsync(
    "https://cdn.example.com/patch.zip",
    idleTimeout: 30,
    progressCallback: (progress) =>
    {
        Debug.Log($"进度：{progress.DownloadProgress * 100f:F1}%");
        ReferencePool.Put(progress);
    },
    cancellationToken: cts.Token);

try
{
    if (response.IsSuccess)
        File.WriteAllBytes(localPath, response.RawData);
}
finally
{
    ReferencePool.Put(response);
}

// --- 下载文本（版本 JSON）---
HttpResponse textResponse = await Nova.Network.DownloadTextAsync(
    "https://cdn.example.com/version.json",
    idleTimeout: 10);

try
{
    if (textResponse.IsSuccess)
    {
        // 解析版本信息
    }
}
finally
{
    ReferencePool.Put(textResponse);
}
```

---

## §13 关联文档

- [HttpResponse.md](Definitions/HttpResponse.md)
- [IHttpManager.md](IHttpManager.md)
- [HttpManager.md](HttpManager.md)
