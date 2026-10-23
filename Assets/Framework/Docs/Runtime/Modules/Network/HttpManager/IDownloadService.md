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

## 内置下载的边界

- 两个 API 都使用缓冲式 GET，最终响应同时可携带 `RawData` 和 `Body`；文本与二进制入口没有不同的磁盘管线。它们不写文件、不做断点续传或内容哈希校验，也不自动切换备用 URL；调用方根据成功结果自行处理内容。
- 任意负 `idleTimeout` 使用默认请求超时向上取整后的秒数；0 在尚未完成的轮询中立即中止。空闲计时按实时钟运行，每次发现已下载字节数变化就重新计时；没有独立的整次下载总超时，持续有新数据可一直下载。
- 当前下载实现先 SendWebRequest，再在未完成循环中检查令牌。因此预取消下载也可能已经发送；这与普通 HTTP 的令牌末位重载在发送前拒绝取消不同。进行中取消会 Abort 并返回失败响应，不以 `OperationCanceledException` 表示；已完成结果优先，不追溯取消。
- 进度回调只在观察到字节变化时触发，不承诺首个 0% 或最终 100%。参数是 `StatusCode=0`、`IsSuccess=false` 的池化中间态，仅含计数，不带正文/原始字节/响应头；未知或非正总量时 DownloadProgress 为 0。中间态在回调 finally 自动归池，不能保存对象跨回调/await 使用，也不能自行 Put；需要保留时只复制计数。
- 回调抛异常会记录 Warning、停用后续进度回调，下载继续。最终响应由调用方在 finally 唯一归池；取消、失败也一样。失败可保留已经收到的正文和字节，不可据此当作完整内容。
- 每次请求 using 释放 UWR 及 handler；Manager Shutdown 不追踪或中断已有下载。拥有者退出时应 Cancel 并等待最终结果，再处理归池。

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
        // progress 仅在本次回调期间有效，由框架在回调结束后自动归还引用池。
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
