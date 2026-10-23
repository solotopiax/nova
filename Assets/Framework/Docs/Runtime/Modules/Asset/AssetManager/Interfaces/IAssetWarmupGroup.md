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

## 执行状态与失败中间态

Tags 与 locations 先 Trim，再按 Ordinal 保序去重；Tag 无命中抛 InvalidOperationException，locations 全部无效抛 ArgumentException。与下载器不同，空 Tag 不表示整包。构造的 scope 仅用于日志/UI，null 归空串，不参与筛选。

RunAsync 先拒绝已释放实例和并发调用；已结束时直接返回此前 Succeeded，不重新加载或重试。它先为所有项发起 BundleFile 加载，再逐帧等待全部完成；FinishedCount 包括失败终态项，空组 Progress 为 1。成功时持有 handles，失败或取消时释放；失败保留 Error，取消同时抛出。进度监听逐个捕获异常并告警，不改变预热结果。

Release 先标记 IsReleased、取消等待，再逐个释放句柄并清监听；不会重置总数、完成数、先前成功值或错误，也不保证 IsDone 已在异步等待中更新。单个 handle.Release 抛出会中断后续释放，没有 finally 保证全部清完；“幂等”不等于失败后再次调用能够补齐。调用方应在当前所有权结束后丢弃引用。

启动控制器按传入 succeeded 决定接管，不独立复核 Group 结果。All 成功保留到 Shutdown；Tags 成功 Release 后置待清理标记，DLL 消费后才 Cleanup。该 Cleanup 的调用方取消继续抛出并保留待清理标记；其他清理异常只告警，后续标记会清除，不触发重复预热。
