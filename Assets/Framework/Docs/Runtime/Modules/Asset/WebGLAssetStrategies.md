# WebGL 资源运行策略

Nova 已为浏览器 WebGL 实现三种资源运行策略：`TagsOnLaunch`、`OnDemand` 与 `AllOnLaunch`。它们只决定**浏览器运行时何时请求 Bundle、是否临时持有内存引用**；不新增 `WebPlayMode`，仍复用 YooAsset 的 `OfflinePlayMode` 和 `HostPlayMode`。

> [!IMPORTANT]
> WebGL 必须使用异步资源接口。YooAsset 的 Web 文件系统不支持同步 Bundle 加载，`LoadSync`、`LoadSceneSync` 等同步路径会失败；业务应改用对应的 `LoadAsync` API。

## 先分清三个层次

| 层次 | WebGL 下的含义 | 不代表什么 |
|---|---|---|
| 构建部署 | Pipify 的 `BundledCopyOption` 决定当前 Package 是纯 CDN、按 Tag 放入 `StreamingAssets`，还是全量放入 `StreamingAssets` | 浏览器已经下载或加载这些 Bundle |
| 请求来源 | 运行时以 YooAsset 官方 `BuiltinCatalog.bytes` 是否存在为依据，组合 `WebServer` 与 `WebNetwork` | Android/iOS Sandbox 下载器 |
| 运行策略 | 决定 Bundle 是首次业务使用时请求、启动按 Tag 请求，还是启动全部请求并常驻 | `StreamingAssets` 的拷贝范围 |

WebGL 导出后的 `StreamingAssets` 是随网页一起部署在服务器上的 URL 目录。即使选择 `ClearAndCopyAll`，浏览器也只会在 Manifest 或实际资源加载发生时请求对应文件，不会在打开网页时自动下载全部 Bundle。

`HostPlayMode` 仍可用于同一 Player 后续独立发布资源；`OfflinePlayMode` 适用于资源与 Player 严格一一对应且不需要远端更新的项目。这里的“Offline”是 YooAsset 的运行模式名，不保证浏览器断网后一定可运行。

## 首包构建布局与运行时文件系统

WebGL 在 Pipify 的 `BundledCopyOption` 中只开放三项：

| 构建选项 | `StreamingAssets` 内容 | HostPlayMode 文件系统 | OfflinePlayMode |
|---|---|---|---|
| `None` | 不放 Catalog、Manifest 和 Bundle，并清理目标 Package 的旧首包目录 | 仅 `WebNetwork`，资源全部来自 CDN | 初始化失败 |
| `ClearAndCopyByTags` | Manifest、Catalog 与指定 Tag 的 Bundle | `WebServer + WebNetwork`，首包命中本地站点，其余走 CDN | 不允许 |
| `ClearAndCopyAll` | Manifest、Catalog 与全部 Bundle | `WebServer + WebNetwork`；当前首包 Bundle 优先来自本地站点，后续版本仍可来自 CDN | 纯 `WebServer`，推荐且完整支持 |

`OnlyCopyAll` 与 `OnlyCopyByTags` 在 WebGL 下不开放，因为它们不会先清理旧 Package 目录，历史文件可能污染实际布局。`None` 会由 Nova 精确清理目标 Package 的旧首包目录。

Nova 不生成额外布局标记，也不修改 YooAsset 原包。Host 启动时会探测 `StreamingAssets/{YooFolderName}/{PackageName}/BuiltinCatalog.bytes`：可访问时启用 `WebServer + WebNetwork`，不存在或不可访问时使用纯 `WebNetwork`。Catalog 未命中时由 Nova 输出一条 Warning 说明已切换纯 CDN；浏览器或小游戏开发者工具的 Network 面板仍会保留真实 HTTP 404，它不是 Nova 的 Error 日志。Offline 固定使用纯 `WebServer`，不探测也不回退 CDN；缺少 Catalog 时由 YooAsset 初始化明确失败。

YooAsset 的 Catalog 只能证明首包目录存在，不能区分按 Tag 与全量拷贝。因此 Offline 的构建契约仍要求 `ClearAndCopyAll`；若错误使用按 Tag 首包，初始化可能成功，但访问未随包资源时会失败。

## 三种策略

| 策略 | 默认值 | 启动行为 | 启动 Group 生命周期 | 适用场景 |
|---|---:|---|---|---|
| `TagsOnLaunch` | 是（值为 `0`） | `ProcedureHotfix` 按 `LaunchHotfixTags` 创建 WarmupGroup | 成功后立即 `Release()`，启动 DLL 消费完成后 `CleanupAsync()` | 推荐默认；提前平滑一小批启动后马上要用的资源 |
| `OnDemand` | 否 | 不创建启动 WarmupGroup，直接进入业务异步加载 | 无 | 小包体，或可接受首次资源加载等待 |
| `AllOnLaunch` | 否 | Manifest 就绪后预热默认 Package 的全部资源 | AssetManager 持有到 `Shutdown()` | 资源量小、内存充足、希望运行期尽量平滑 |

`OnDemand` 与 `TagsOnLaunch` 不要望文生义地混为一类：即使填了 `LaunchHotfixTags`，`OnDemand` 也完全不读取它们来发起启动加载，并且会跳过 `ProcedureHotfix`。`TagsOnLaunch` 才会在启动中读取这些 Tag。`AllOnLaunch` 则忽略 Tag，预热默认 Package 的全部资源。

WebGL 不使用 Downloader 做补丁差异检查；`EnableHotfix` 也不会禁止 `TagsOnLaunch` / `AllOnLaunch` 的启动预热。也就是说，禁用热更新但选择这两种策略时，启动仍会 Bootstrap、加载 Manifest 并进入 `ProcedureHotfix` 完成 Warmup；`OnDemand` 才不进入该阶段。

### LaunchHotfixTags 的双平台语义

- **Android/iOS 等非 WebGL 平台**：它是启动补丁检查与 Downloader 下载范围；空列表表示整包。
- **WebGL**：它只定义 `TagsOnLaunch` 的启动预热范围；`StreamingAssets` 拷贝范围由 Pipify 的 `BundledCopyOption` 单独决定。空白项和重复项会自动忽略；清理后没有有效 Tag 时，`TagsOnLaunch` 自动降级为 `OnDemand`。

因此 WebGL Inspector 显示为“启动预热 Tag 列表”。它不是首包裁剪开关，也不是“资源已经缓存”的证明。

## IAssetWarmupGroup：有所有权的批量预热

`IAssetWarmupGroup` 是一组异步 Bundle 加载任务和其 `BundleFileHandle` 的所有权容器；它不是 Procedure 阶段，也不负责 Loading UI。创建 Group 前，目标 Package 必须已经完成 `LoadManifestAsync()`。

```csharp
// 业务主动预热的最小范式：创建者负责最终 Release。
IAssetWarmupGroup group = Nova.Asset.CreateWarmupByTags(new[] { "Lobby" });
try
{
    bool succeeded = await group.RunAsync(ct);
    if (!succeeded)
    {
        Debug.LogWarning(group.Error);
    }
}
finally
{
    group.Release(); // 幂等；也可使用 Dispose()
    await Nova.Asset.CleanupAsync();
}
```

公开创建入口：

- `CreateWarmupAll(package)`：目标 Package 的全部资源。
- `CreateWarmupByTags(tags, package)`：Tag 并集；空白和重复项被忽略，清理后为空会抛 `ArgumentException`。
- `CreateWarmupByLocations(locations, package)`：指定资源地址；空白、重复和无效地址被忽略，清理后为空会抛 `ArgumentException`。

Group 提供 `TotalCount`、`FinishedCount`、`Progress`、`OnProgress`、`Succeeded` 和 `Error`。进度是**完成项数**，不是下载字节数；UI 不应把它伪装成精确字节进度。

### Release、Cleanup 与取消边界

- `Release()` / `Dispose()` 会幂等释放 Group 持有的全部 `BundleFileHandle`；它只撤销预热引用。
- `CleanupAsync()` 才会扫描并卸载引用归零的 Provider 与 BundleLoader。因此 `TagsOnLaunch` 在预热成功后先 `Release()`，等 `ProcedureLoadDll` 消费完 AOT 与业务 DLL 后再执行 `CleanupAsync()`，避免相邻启动阶段重复请求不具备持久缓存保证的 Bundle。
- `Cancel()` 会停止 Group 自己的等待、释放已创建 Handle。YooAsset 没有可由 WarmupGroup 单独保证的底层 HTTP 取消接口；已经发出的 Bundle 请求可能继续到自然结束。
- 业务主动创建的 Group 由业务创建者负责释放。框架创建的 `TagsOnLaunch` Group 由 `ProcedureHotfix` 自动收口；没有 `Nova.Asset.ReleaseLaunchWarmup()` 这类业务侧接口。
- `AllOnLaunch` 的成功 Group 由 AssetManager 接管，在 `YooAssets.Destroy()` 前释放；它故意保持 Bundle 内存常驻。

## Unity Web Cache 与内存不是一回事

标准、**未加密 AssetBundle** 在 WebGL 下使用 `DownloadHandlerAssetBundle`，且 Nova 默认不关闭 Unity Web Cache；这类请求可以命中 Unity Web Cache。Warmup 后释放 Group 时，若没有其他引用，Bundle 运行时内存可以被 Cleanup 回收，但浏览器管理的 Web Cache 不会因而被删除。

以下类型走字节下载和内存装载，不应承诺“Warmup 后一定留在 Unity Web Cache”：

| Bundle 类型 | WebGL 装载路径 | TagsOnLaunch 的缓存承诺 |
|---|---|---|
| 未加密 AssetBundle | `DownloadHandlerAssetBundle` | 可利用 Unity Web Cache；仍受浏览器清理、无痕模式和容量淘汰影响 |
| 加密 AssetBundle | 下载字节 → `IBundleMemoryDecryptor` → `AssetBundle.LoadFromMemoryAsync` | 不承诺进入 Unity Web Cache |
| RawBundle | 下载字节 → 内存 RawBundle | 不承诺进入 Unity Web Cache |
| ArchiveBundle | 下载字节 → 内存 ArchiveBundle | 不承诺进入 Unity Web Cache |

加密、Raw、Archive 资源仍可被 Warmup 加载，但如果随后释放 Group，它们不能被当作“已持久缓存”。对这些资源，要么让业务在需要时异步加载，要么在确有常驻需求时选择合适的业务 Handle 生命周期或 `AllOnLaunch`，并做浏览器实测。

Unity Web Cache 由浏览器管理，不等同于 Android/iOS 的 Sandbox 持久文件：用户清缓存、无痕模式和容量淘汰都可能令下次请求重新下载。

## 下载器与超时

WebGL 的 `CreateDownloader*()` 不提供 Android/iOS Sandbox 式“提前下载到本地”的语义；启动流程不会调用 Downloader 做差异检查或预下载。空 Downloader 既不代表首包完整，也不代表浏览器已经缓存 Bundle。WebGL 的有效预热方式是业务异步加载或 `IAssetWarmupGroup`。

WebGL 不使用“单文件字节流入超时”。`WebGLBundleRequestTimeout`（默认 300 秒）通过同一个 UnityWebRequest creator 同时覆盖 `WebServer` 的 `StreamingAssets` Bundle 请求和 `WebNetwork` 的 CDN Bundle 请求；每个物理请求独立计时。只有 Host 的 CDN 请求使用 Asset 主备轮次、重试、最近成功域名优先和 UWR 埋点，StreamingAssets 请求不参与 CDN 候选轮换。

元数据有自己的超时：`.version` 用 `CheckTimeout`，`.hash/.bytes` 用 `ManifestRequestTimeout`（默认 60 秒）。Host 远端元数据请求复用 CDN 主备轮次、重试、最近成功域名优先和 UWR 埋点；StreamingAssets 元数据请求只使用对应超时。远端 Manifest 候选耗尽时，只有探测到官方 Catalog 的 Host 包才会回退到随 Player 发布的 `StreamingAssets` 首包 Manifest；纯 CDN 没有 Catalog，不进入该回退。

## 构建与迁移边界

- `BuildAssetBundle`、`BuildRawFileBundle`、Pipify 和受控 Build Action 在 WebGL 目标下支持 `None`、`ClearAndCopyByTags`、`ClearAndCopyAll`；非 WebGL 保留 YooAsset 原有五种选项。
- Nova 不额外生成首包布局标记，也不修改 YooAsset 厂商包源码；运行时只依赖 YooAsset 构建生成的官方 Catalog。为确保 `None` 能清除旧 Catalog，项目构建应统一走上述 Nova 入口。
- WebGL 的运行策略与首包拷贝布局互相独立：前者决定何时请求，后者决定从 StreamingAssets 还是 CDN 请求。
- 旧 `PreloadAsync` 已从 `IAssetManager`、`AssetManagerBase`、`AssetManager` 与 `AssetComponent` 删除，不保留兼容转发。迁移到 `CreateWarmupAll` / `CreateWarmupByTags` / `CreateWarmupByLocations`，并明确持有与释放 Group。
- 本策略仅覆盖浏览器 WebGL，暂不覆盖微信小游戏。

## 验证边界

源码与 Editor 契约测试可证明策略路由和构建参数，不能代替浏览器验证。发版前仍应分别验证：三种首包布局产物、三种运行策略的请求时序、Tags 释放后的未加密 AssetBundle 缓存命中、Raw/Archive/加密资源行为、失败/取消、缓存被浏览器清理后的重新请求、内存峰值、CDN/CORS，以及有首包和纯 CDN 两类 Manifest 失败路径。

## 关联文档

- [IAssetWarmupGroup.md](AssetManager/Interfaces/IAssetWarmupGroup.md)
- [AssetComponent.md](AssetComponent.md)
- [AssetManager.md](AssetManager/Implements/AssetManager.md)
- [AssetManagerConfig.md](AssetManager/Definitions/AssetManagerConfig.md)
- [ProcedureCheckVersion.md](../Procedure/ProcedureCheckVersion.md)
- [ProcedureHotfix.md](../Procedure/ProcedureHotfix.md)
- [EditorUtil.BundleBuilder.md](../../../Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md)
