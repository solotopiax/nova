# HttpManagerBase

**类签名**：`internal abstract class HttpManagerBase : FrameworkManager, IHttpManager`
**命名空间**：`NovaFramework.Runtime`

HTTP 管理器抽象基类，继承 `FrameworkManager` 并实现 `IHttpManager`（含 `IDownloadService`）接口。声明全部 7 个抽象方法，`Priority = 8`，由 `HttpManager` 密封实现。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `HttpManagerBase.cs` | `abstract HttpManagerBase` | HTTP 管理器抽象基类，7 个 abstract 声明 |

---

## § 3 继承关系

```
FrameworkManager
  └── HttpManagerBase (abstract) : IHttpManager : IDownloadService   Priority = 8
        └── HttpManager (sealed partial)
```

---

## § 4 关键字段表

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `Priority` | `int`（override） | `8` | 框架管理器优先级，值越小越先 Update、越后 Shutdown |

---

## § 5 完整公开 API

```csharp
// --- 优先级 ---
public override int Priority => 8

// --- 生命周期（abstract）---
public abstract void Initialize(HttpManagerConfig config)
public abstract override void Update()
public abstract override void Shutdown()

// --- 异步 HTTP 请求（abstract）---
public abstract UniTask<HttpResponse> GetAsync(string url, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
public abstract UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
public abstract UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
public abstract UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)

// --- 继承自 IDownloadService（abstract）---
public abstract UniTask<HttpResponse> DownloadBinaryAsync(string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null, CancellationToken cancellationToken = default)
public abstract UniTask<HttpResponse> DownloadTextAsync(string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null, CancellationToken cancellationToken = default)
```

---

## § 13 关联文档

- [IHttpManager.md](IHttpManager.md)
- [IDownloadService.md](IDownloadService.md)
- [HttpManager.md](HttpManager.md)
- [HttpManagerConfig.md](Definitions/HttpManagerConfig.md)
