# IAssetDownloader

**类签名**：`public interface IAssetDownloader`
**命名空间**：`NovaFramework.Runtime`

资源下载器接口，封装底层资源框架的下载操作，业务层持有后调 RunAsync 触发实际下载。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Interfaces/IAssetDownloader.cs` | `IAssetDownloader` | 接口定义 |

---

## 完整公开 API

```csharp
// 属性
int TotalCount { get; }       // 总文件数
long TotalBytes { get; }      // 总字节数
int FinishedCount { get; }    // 已完成文件数
long FinishedBytes { get; }   // 已完成字节数
float Progress { get; }       // 进度 0~1
bool IsEmpty { get; }         // 是否空下载（TotalCount == 0）
long DownloadSpeed { get; }   // 当前下载速率 bytes/s，基于约 500ms 采样窗口差分估算；未启动或已结束返回 0
string Scope { get; }         // 切片来源描述，整包="all"，按 tag="tags:a,b"，按 Asset 地址="locations:N"；仅用于日志/UI

// 事件
event Action<int, int, long, long> OnProgress;  // 参数: (finishedCount, totalCount, finishedBytes, totalBytes)
event Action<string, string, long> OnFileStarted; // 参数: (bundleName, fileName, fileSize)

// 方法
UniTask<bool> RunAsync(CancellationToken ct = default)  // 启动下载，true=成功，false=失败
void Cancel()                                            // 取消下载
```

---

## 使用示例

```csharp
IAssetDownloader downloader = Nova.Asset.CreateDownloader(package: null, concurrency: 5, retry: 3);
if (!downloader.IsEmpty)
{
    downloader.OnProgress += (done, total, doneBytes, totalBytes) =>
    {
        Debug.Log($"{done}/{total} files, {doneBytes}/{totalBytes} bytes");
    };
    bool success = await downloader.RunAsync(ct);
}
```

---

## 关联文档

- [IAssetManager.md](IAssetManager.md)
- [AssetDownloader.md](../Definitions/AssetDownloader.md)
