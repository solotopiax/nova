# HttpResponse

**类签名**：`public sealed class HttpResponse : IReference`
**命名空间**：`NovaFramework.Runtime`

HTTP 响应数据，实现 IReference 支持 ReferencePool 池化复用。除状态码、正文和下载信息外，还记录请求是否明确未到达服务器、无法确认或已经获得正式 HTTP 响应。

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

// --- IReference.Clear（归还池时重置所有字段；TotalBytes 重置为 -1）---
public void Clear()

// --- 属性（private set）---
public int StatusCode { get; private set; }
public string Body { get; private set; }
public byte[] RawData { get; private set; }
public Dictionary<string, string> Headers { get; private set; }
public string Error { get; private set; }
public bool IsSuccess { get; private set; }
public bool HasServerResponse { get; }
public HttpDeliveryState DeliveryState { get; private set; }
public long DownloadedBytes { get; private set; }
public long TotalBytes { get; private set; }

// --- 计算属性 ---
// TotalBytes > 0 时返回 Clamp(DownloadedBytes / TotalBytes, 0, 1)；否则返回 0
public float DownloadProgress { get; }
```

`HttpDeliveryState` 只表达网络通信层结果：`NotReachedServer` 表示明确停在 DNS、TCP 或 TLS 建连阶段；`Unknown` 表示超时等无法确认情况；`ServerResponded` 表示服务器已返回正式 HTTP 响应，不区分业务结果成功或失败。

普通业务调用方不能直接创建有状态实例，应通过 `IHttpManager` / `IDownloadService` 的异步方法获得。消费完毕后通过 `ReferencePool.Put(response)` 归还。

## 字段、失败结果与所有权

- `IsSuccess` 来自 UWR.Result.Success；`HasServerResponse` 仅检查 DeliveryState 是否为 ServerResponded。4xx/5xx 及已有正式响应后的处理失败可以有正文、RawData 和 Headers，因此不能用“字段非空”判断成功。
- 最终响应构建时复制响应头为大小写不敏感字典，并在 UWR 释放前取出正文/字节。`HttpResponse.Create()` 自身仅保存传入引用，不深拷贝 RawData/Headers，也不验证 isSuccess 与状态码是否一致；statusCode 大于 0 会强制 DeliveryState 为 ServerResponded。
- 正常完成时，可信非负 Content-Length 写入 TotalBytes；没有该头时回退为已完成字节数。失败构建没有可信长度时回退为当前 RawData 长度，否则 -1。内部 `TotalBytesIsKnown` 仅对明确长度为 true，因此 TotalBytes 非负不一定代表服务器事先声明了总量。
- `DownloadProgress` 在 TotalBytes 大于 0 时计算并钳制到 0～1，其余返回 0；它不是成功判据。下载中间响应与最终响应是两种不同所有权，详见 [IDownloadService.md](../IDownloadService.md#内置下载的边界)。
- `Clear()` 把状态码、字节数归零，正文/字节/头/错误置 null，成功为 false，总量为 -1，交付状态为 Unknown，并清空内部传输诊断字段。它不取消或 Dispose 网络请求，也不清除被其他引用持有的数组/字典内容。
- Put 后禁止再访问响应对象。需要继续使用的数据应先保存到自己的变量；仅保存数组/字典引用不等于深拷贝，需要隔离后续修改时自行复制。

内部诊断字段 `TransportState`、`UploadedBytes`、`TotalBytesIsKnown` 由 Create 填充，供框架埋点使用，不属于公开请求配置。状态码与无符号字节数转入响应时分别饱和到 int/long 上限；这只防统计溢出，不额外校验标准 HTTP 状态码范围。

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
        // progress 仅在本次回调期间有效，由框架在回调结束后自动归还引用池。
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
