# IAssetWarmupGroup

**类签名**：`public interface IAssetWarmupGroup : IDisposable`
**命名空间**：`NovaFramework.Runtime`

`IAssetWarmupGroup` 是有明确所有权的 Bundle 批量预热对象。它集中持有 YooAsset `BundleFileHandle`，避免旧 `PreloadAsync` 那种“既不返回句柄、也无法释放”的隐式资源所有权。

## 创建前提与创建方式

创建前，目标 Package 必须已经完成 `LoadManifestAsync()`；否则创建会抛出异常。通过 `IAssetManager` 或 `Nova.Asset` 创建：

```csharp
IAssetWarmupGroup all = Nova.Asset.CreateWarmupAll();
IAssetWarmupGroup tags = Nova.Asset.CreateWarmupByTags(new[] { "Lobby", "Common" });
IAssetWarmupGroup locations = Nova.Asset.CreateWarmupByLocations(new[] { "UI/Lobby" });
```

- `CreateWarmupByTags` 会清理空白和重复 Tag；清理后为空会抛 `ArgumentException`。
- `CreateWarmupByLocations` 会忽略空白、重复和无效地址；清理后为空会抛 `ArgumentException`。
- Group 不会自动开始；必须调用 `RunAsync()`。

## 完整公开 API

```csharp
int TotalCount { get; }
int FinishedCount { get; }
float Progress { get; }        // 0~1，按完成项数计算
bool IsDone { get; }
bool Succeeded { get; }
bool IsReleased { get; }
string Error { get; }
string Scope { get; }          // 仅用于日志 / UI，例如 all、tags:a,b、locations:N

event Action<int, int> OnProgress; // (finishedCount, totalCount)

UniTask<bool> RunAsync(CancellationToken ct = default);
void Cancel();
void Release();
void Dispose();
```

`RunAsync()` 在所有 Bundle 成功时返回 `true`；某个加载失败时返回 `false` 并提供 `Error`。取消令牌触发时会清理已创建的 Handle 并抛出 `OperationCanceledException`。

## 所有权与取消

Group 的创建者必须负责最终 `Release()` 或 `Dispose()`；两者幂等。`Release()` 遍历并释放 Group 持有的 `BundleFileHandle`，但不会删除 Unity Web Cache。若要尽快回收已经引用归零的 Bundle 运行时内存，再调用 `Nova.Asset.CleanupAsync()`。

`Cancel()` 会停止 Group 等待并释放已持有的 Handle。它不承诺取消已经交给 YooAsset / 浏览器的物理 HTTP 请求；已开始请求可能自然结束。因此取消回调不能被当作网络流量已经停止的证明。

WebGL 启动的 `TagsOnLaunch` Group 是框架所有：`ProcedureHotfix` 成功后自动 Release，`ProcedureLoadDll` 消费完启动 DLL 后再 Cleanup；`AllOnLaunch` 成功后由 AssetManager 持有至 Shutdown。只有业务主动创建的普通 Group 才需要业务自行决定释放时机。

## 与旧 PreloadAsync 的迁移

```csharp
// 旧：没有返回资源所有权，已删除
// await Nova.Asset.PreloadAsync(tags);

// 新：调用方能清楚知道谁持有、何时释放
IAssetWarmupGroup group = Nova.Asset.CreateWarmupByTags(tags);
try
{
    if (!await group.RunAsync(ct))
    {
        throw new InvalidOperationException(group.Error);
    }
}
finally
{
    group.Release();
}
```

WebGL 的缓存与内存边界见 [WebGLAssetStrategies.md](../../WebGLAssetStrategies.md)。

## 关联文档

- [IAssetManager.md](IAssetManager.md)
- [AssetComponent.md](../../AssetComponent.md)
- [WebGLAssetStrategies.md](../../WebGLAssetStrategies.md)
