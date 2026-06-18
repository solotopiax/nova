# HttpResponse

**类签名**：`public sealed class HttpResponse : IReference`
**命名空间**：`NovaFramework.Runtime`

HTTP 响应数据，实现 IReference 支持 ReferencePool 池化复用。封装请求完成后的状态码、正文、原始字节、响应头、错误信息及下载进度信息。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `HttpResponse.cs` | `sealed class HttpResponse : IReference` | HTTP 响应数据定义，实现 IReference 池化，通过 Create 工厂方法从 ReferencePool 获取实例 |

---

## § 5 完整公开 API

```csharp
// --- 构造器（ReferencePool 要求的公开空参构造器；TotalBytes 初始化为 -1）---
public HttpResponse()

// --- 工厂方法（public SPI，从 ReferencePool 获取实例并初始化）---
public static HttpResponse Create(int statusCode, string body, byte[] rawData, Dictionary<string, string> headers, string error, bool isSuccess, long downloadedBytes, long totalBytes)

// --- IReference.Clear（归还池时重置所有字段；TotalBytes 重置为 -1）---
public void Clear()

// --- 属性（private set）---
public int StatusCode { get; private set; }
public string Body { get; private set; }
public byte[] RawData { get; private set; }
public Dictionary<string, string> Headers { get; private set; }
public string Error { get; private set; }
public bool IsSuccess { get; private set; }
public long DownloadedBytes { get; private set; }
public long TotalBytes { get; private set; }

// --- 计算属性 ---
// TotalBytes > 0 时返回 Clamp(DownloadedBytes / TotalBytes, 0, 1)；否则返回 0
public float DownloadProgress { get; }
```

普通业务调用方不应直接创建有状态实例，应通过 `IHttpManager` / `IDownloadService` 的异步方法获得；可选 HTTP 后端实现可以通过 `Create(...)` 创建池化响应。消费完毕后通过 `ReferencePool.Put(response)` 归还。

---

## § 11 使用示例

```csharp
// 通过 IHttpManager.GetAsync 获取 HttpResponse
HttpResponse response = await Nova.Network.GetAsync("https://api.example.com/data");
try
{
    if (response.IsSuccess)
    {
        string json = response.Body;
        Debug.Log($"StatusCode={response.StatusCode}, Body={json}");
    }
    else
    {
        Debug.LogWarning($"请求失败：{response.Error}，StatusCode={response.StatusCode}");
    }
}
finally
{
    ReferencePool.Put(response);
}

// 二进制下载场景（带进度 + 归还）
HttpResponse binResponse = await Nova.Network.DownloadBinaryAsync(
    "https://cdn.example.com/asset.bundle",
    idleTimeout: 30,
    progressCallback: (progress) =>
    {
        Debug.Log($"已下载：{progress.DownloadedBytes}/{progress.TotalBytes} bytes，进度 {progress.DownloadProgress * 100f:F1}%");
        ReferencePool.Put(progress);
    });
try
{
    if (binResponse.IsSuccess)
        File.WriteAllBytes(localPath, binResponse.RawData);
}
finally
{
    ReferencePool.Put(binResponse);
}
```

---

## § 13 关联文档

- [IHttpManager.md](../IHttpManager.md)
- [IDownloadService.md](../IDownloadService.md)
- [HttpManager.md](../HttpManager.md)
