# AssetDownloader

**类签名**：`internal sealed class AssetDownloader : IAssetDownloader`
**命名空间**：`NovaFramework.Runtime`

资源下载器，包装 YooAsset ResourceDownloaderOperation 并暴露为 IAssetDownloader 接口。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Definitions/AssetDownloader.cs` | `AssetDownloader` | IAssetDownloader 实现，包装 YooAsset 下载操作 |

---

## 关键字段表

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_Operation` | `ResourceDownloaderOperation` | 被包装的 YooAsset 下载操作实例（readonly） |
| `m_Scope` | `string` | 切片来源描述（readonly），构造时传入 |
| `m_LastSampleTime` | `float` | 上次速率采样时刻（Time.realtimeSinceStartup） |
| `m_LastSampleBytes` | `long` | 上次采样时已完成字节数，差分计算速率用 |
| `m_DownloadSpeed` | `long` | 当前速率缓存（bytes/s），OnProgress 内约 500ms 刷新一次 |
| `c_SpeedSampleInterval` | `float const` | 速率采样间隔 0.5s |

---

## 完整公开 API

```csharp
// scope 可选，默认 "all"；按 tag 传 "tags:..."；按 Asset 地址传 "locations:N"
public AssetDownloader(ResourceDownloaderOperation operation, string scope = "all")

// IAssetDownloader 实现
int TotalCount { get; }        // → m_Operation.TotalDownloadCount
long TotalBytes { get; }       // → m_Operation.TotalDownloadBytes
int FinishedCount { get; }     // → m_Operation.CurrentDownloadCount
long FinishedBytes { get; }    // → m_Operation.CurrentDownloadBytes
float Progress { get; }        // → m_Operation.Progress
bool IsEmpty { get; }          // TotalCount <= 0
long DownloadSpeed { get; }    // → m_DownloadSpeed，约 500ms 差分估算，结束后归 0
string Scope { get; }          // → m_Scope，来源描述字符串

event Action<int, int, long, long> OnProgress;
event Action<string, string, long> OnFileStarted;

async UniTask<bool> RunAsync(CancellationToken ct = default)
void Cancel()
```

---

## 关键算法

**RunAsync**：
1. IsEmpty 时直接返回 `true`
2. `m_Operation.StartDownload()` 触发底层下载
3. `UniTask.WaitUntil(() => m_Operation.IsDone, cancellationToken: ct)`
4. ct 取消时调 `Cancel()` 停底层操作并重抛 `OperationCanceledException`
5. 完成后取消订阅 OnProgress，返回 `m_Operation.Status == EOperationStatus.Succeeded`

---

## 使用示例

```csharp
// 由 AssetManager.CreateDownloader 创建，外部通过 IAssetDownloader 使用
IAssetDownloader downloader = Nova.Asset.CreateDownloader();
bool ok = await downloader.RunAsync(ct);
```

---

## 关联文档

- [IAssetDownloader.md](../Interfaces/IAssetDownloader.md)
- [IAssetManager.md](../Interfaces/IAssetManager.md)
