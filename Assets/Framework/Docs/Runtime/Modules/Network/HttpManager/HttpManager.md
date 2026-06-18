# HttpManager

**类签名**：`internal sealed partial class HttpManager : HttpManagerBase`
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Nova.Network`（通过 `NetworkComponent` 公开方法访问）

HTTP 管理器，通过 `IHttpTransport` 扩展点执行 HTTP 短连接请求（GET / POST / RawData / File）与异步下载。`NovaFramework.Runtime` 不直接依赖 BestHTTP，也不通过 `InternalsVisibleTo` 感知可选后端；BestHTTP 后端由独立 UPM 包 `com.solotopia.nova.framework.besthttp` 注册。
其中 `GetAsync(...)` 会由当前后端关闭本地缓存，确保启动配置、远端规则等读取到最新 GET 响应。DoH 开启时，所有请求会先尝试 `DoHManager` 的 `host -> IP` 缓存，按 IP 候选顺序重试，最后再回落到原始 URL。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `HttpManager.cs` | `sealed partial HttpManager` | 主体：全部 HTTP 请求实现、内部工具方法 |
| `HttpManager.Visitors.cs` | `partial HttpManager` | 字段：m_DoHManager / m_ConnectTimeout / m_RequestTimeout |

---

## § 3 继承关系

```
FrameworkManager
  └── HttpManagerBase (abstract) : IHttpManager : IDownloadService   Priority = 8
        └── HttpManager (sealed partial)
```

---

## § 4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_DoHManager` | `IDoHManager` | `null` | 由 HttpManagerConfig.DoHManager 注入；DoH 开启时负责读取域名缓存、缺失时触发 DNSQuery，并参与请求候选地址重试 |
| `m_ConnectTimeout` | `float` | `20f` | 默认连接超时时间（秒），`connectTimeout = -1` 时使用 |
| `m_RequestTimeout` | `float` | `60f` | 默认请求超时时间（秒），`requestTimeout = -1` 时使用 |

---

## § 5 完整公开 API

```csharp
// --- 生命周期 ---
void Initialize(HttpManagerConfig config)
void Update()
void Shutdown()

// --- 异步 HTTP 接口 ---
UniTask<HttpResponse> GetAsync(string url, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)

// --- 继承自 IDownloadService ---
UniTask<HttpResponse> DownloadBinaryAsync(string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null, CancellationToken cancellationToken = default)
UniTask<HttpResponse> DownloadTextAsync(string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null, CancellationToken cancellationToken = default)
```

---

## § 9 关键算法

### DoH 感知请求候选链

```
ExecuteDoHResilientAsync(originalUrl)
  │
  ├─ BuildRequestUrlCandidatesAsync(originalUrl)
  │    ├─ host = m_DoHManager.GetHostName(originalUrl)
  │    ├─ cachedIPs = m_DoHManager.GetIPAddresses(host)
  │    ├─ 若 cachedIPs 为空 → await m_DoHManager.DNSQuery(originalUrl)
  │    ├─ 把每个 IP 替换进 URL 的 host 部分，保留协议/端口/路径/查询字符串
  │    └─ 最后追加 originalUrl 作为兜底
  │
  └─ 依次尝试 candidateUrls
       ├─ IP 直连时自动补写 `Host` 头（host 或 host:port）
       ├─ 任一候选成功 → 立即返回
       └─ 全部失败 → 返回最后一次失败响应
```

### DownloadBinaryAsync 空闲超时策略

```
DownloadBinaryAsync(url, idleTimeout, progressCallback, cancellationToken)
  │
  ├─ effectiveIdleTimeout = idleTimeout < 0 ? ceil(m_RequestTimeout) : idleTimeout
  ├─ 注册 OnDownloadProgress 回调：更新 lastDownloadedBytes、lastProgressTime
  ├─ LinkedCancellationTokenSource(cancellationToken, idleCts)
  │
  └─ 轮询（每 PlayerLoop.Update tick）
        ├─ responseTask 已完成 → 退出循环
        └─ (Time.realtimeSinceStartup - lastProgressTime) > effectiveIdleTimeout
              → request.Abort()
              → return HttpResponse(isSuccess=false, error="Idle timeout", downloadedBytes=lastDownloadedBytes)
```

---

## § 10 常见误区

| 误区 | 正确理解 |
|---|---|
| `headerInfos` 传入普通字符串 | `headerInfos` 必须是 JSON 对象格式字符串，如 `{"key": "value"}`，内部会 `JObject.Parse` |
| DoH 只影响手动预热，不影响正常请求 | 现在 `Get / Post / PostRawData / PostFile / DownloadBinary / DownloadText` 都会先走 DoH 候选链 |
| `requestTimeout` 传入 0 | 传 -1 使用默认值；传 0 表示 0 秒超时，后端会立即超时，应传正数 |
| `DownloadBinaryAsync` 使用请求超时控制下载 | 应使用 `idleTimeout` 控制空闲等待；长文件下载时只要持续有字节到达就不会超时 |
| 后端替换后 API 不变 | HTTP 对外接口签名不绑定具体后端；切换 BestHTTP 后端不影响 `Nova.Network` 调用方式 |
| 远端 GET 结果会自动刷新 | `GetAsync(...)` 当前会要求后端禁用本地缓存；需要缓存时应在业务层显式持有结果，而不是依赖 HTTP 层缓存 |
| 返回的 HttpResponse 不需要管 | HttpResponse 实现 IReference 池化，框架内部消费的 response 必须通过 `ReferencePool.Put` 归还；进度回调的中间态 HttpResponse 同理 |

---

## § 11 使用示例

```csharp
// --- 底层 UniTask GET ---
HttpResponse resp = await Nova.Network.GetAsync("https://api.example.com/data");
try
{
    if (resp.IsSuccess)
        Debug.Log(resp.Body);
    else
        Debug.LogWarning($"失败：{resp.Error}");
}
finally
{
    ReferencePool.Put(resp);
}

// --- 底层 UniTask POST（字符串 body）---
HttpResponse postResp = await Nova.Network.PostAsync(
    "https://api.example.com/submit", "{\"key\":\"value\"}");
ReferencePool.Put(postResp);

// --- 文件上传 ---
byte[] fileBytes = File.ReadAllBytes("/path/to/file.png");
HttpResponse uploadResp = await Nova.Network.PostFileAsync(
    "https://upload.example.com/upload",
    "{\"userId\":\"123\"}", fileBytes, "avatar.png");
try
{
    Debug.Log(uploadResp.StatusCode);
}
finally
{
    ReferencePool.Put(uploadResp);
}

// --- 二进制下载（空闲超时 + 进度）---
HttpResponse bin = await Nova.Network.DownloadBinaryAsync(
    "https://cdn.example.com/patch.zip",
    idleTimeout: 30,
    progressCallback: (progress) =>
    {
        Debug.Log($"已下载：{progress.DownloadedBytes} bytes，进度 {progress.DownloadProgress * 100f:F1}%");
        ReferencePool.Put(progress);
    });
try
{
    if (bin.IsSuccess)
        File.WriteAllBytes(localPath, bin.RawData);
}
finally
{
    ReferencePool.Put(bin);
}
```

---

## § 12 注意事项

| 场景 | 正确做法 |
|---|---|
| 底层依赖 | `NovaFramework.Runtime` 不直接依赖 BestHTTP；HTTP 通过 public SPI `IHttpTransport` / `IHttpTransportFactory` / `HttpTransportRegistry.Register(...)` 找到已启用后端 |
| 缺少后端 | 未安装或未启用后端时，请求会返回失败的 `HttpResponse`，错误信息会提示安装 `com.solotopia.nova.framework.besthttp` 与 BestHTTP/TLS |
| BestHTTP 后端 | 安装独立包 `com.solotopia.nova.framework.besthttp` 后提供 `NovaFramework.BestHTTP.Runtime`；该 asmdef 通过程序集名引用 `com.Tivadar.Best.HTTP` / `com.Tivadar.Best.TLSSecurity` |
| SPI 使用边界 | `IHttpTransport` 是框架级后端扩展点，供可选传输程序集注册实现；后端实现用 `HttpResponse.Create(...)` 创建池化响应；普通业务代码仍应通过 `Nova.Network` 调用 HTTP API |
| DoH IP 直连 | `HttpManager` 只替换 URL 的 host 部分，其他结构保持不变；IP 直连时会补写 `Host` 请求头 |
| DoH 缓存未命中 | 当前请求会先触发一次 `DNSQuery(url)`，成功后立刻重建候选 URL 再发请求 |
| WebGL 平台 | BestHTTP 在 WebGL 的实机行为待验证，参见 [project_network_webgl_besthttp.md] |
| HTTPS + 证书校验 | 若服务端证书或网关策略不接受 IP 直连，IP 候选可能失败；框架仍会自动回退到原始 URL 兜底 |
| 大文件上传进度监听 | `PostFileAsync` 不提供上传进度回调；需要进度时使用 BestHTTP 原生 API |
| TLS 验证控制 | BestHTTP 的 TLS 验证由 BestHTTP TLS Security 包统一管理，与 DevelopMode 无关 |

---

## § 13 关联文档

- [NetworkComponent.md](../NetworkComponent.md)
- [IHttpManager.md](IHttpManager.md)
- [IDownloadService.md](IDownloadService.md)
- [HttpManagerBase.md](HttpManagerBase.md)
- [HttpResponse.md](Definitions/HttpResponse.md)
- [HttpManagerConfig.md](Definitions/HttpManagerConfig.md)
- [DoHManager.md](../DoHManager/DoHManager.md)
- [NetworkManager.md](../NetworkManager/NetworkManager.md)
