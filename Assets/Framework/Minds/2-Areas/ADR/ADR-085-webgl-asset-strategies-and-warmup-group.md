---
id: ADR-085
title: WebGL 三种资源策略与 IAssetWarmupGroup 句柄分层
status: accepted
date: 2026-09-16
summary: 官方 Catalog 驱动 WebGL 文件系统与预热句柄
category: asset
aliases:
  - WebGL 资源加载策略
  - IAssetWarmupGroup
  - AllOnLaunch
  - OnDemand
  - TagsOnLaunch
keywords:
  - ADR-085
  - WebGL 资源加载
  - LaunchHotfixTags
  - IAssetWarmupGroup
  - BundleFileHandle
  - ClearAndCopyAll
  - BuiltinCatalog
  - OnDemand
  - TagsOnLaunch
  - AllOnLaunch
tags: [adr, nova, asset, webgl, yooasset, handle]
supersedes: []
superseded-by: []
related:
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
  - "[[ADR-051-launch-asset-slice-strategy|ADR-051]]"
  - "[[ADR-052-asset-cache-two-layer-cleanup|ADR-052]]"
  - "[[ADR-065-asset-manifest-three-tier-offline-fallback|ADR-065]]"
  - "[[MOC-Asset]]"
---

# ADR-085：WebGL 三种资源策略与 IAssetWarmupGroup 句柄分层

## 背景（Context）

Nova 的 WebGL 资源链容易混淆四件事：首包放置、Bundle 预下载、内存驻留和业务 Asset 生命周期。尤其需要澄清：

- Web 文件系统的 Downloader 为空，不代表 Bundle 已缓存。
- `LaunchHotfixTags` 是启动必须资源的范围配置，不是加载动作。
- Unity Web Cache 与 Bundle 运行时内存是两层：释放 Bundle Handle 不等于删除浏览器缓存。
- 启动 Warmup 的首要目标是提前填充 Unity Web Cache，不是等待业务 Handle 接管后再释放。

旧 `PreloadAsync` 成功后不释放、也不返回 Handle，框架无法在缓存预热结束后可靠回收临时 Bundle 引用，与 [[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]] 的所有权规则冲突。

## 决策（Decision）

### 1. 平台边界与运行模式

本决策只覆盖浏览器 WebGL，暂不包含微信小游戏。WebGL 不新增 `WebPlayMode`，继续复用 `OfflinePlayMode` 与 `HostPlayMode`，资源策略独立于 PlayMode。

### 2. 三种 WebGL 资源策略

| 策略 | 启动行为 | Handle 生命周期 | 适用场景 |
|---|---|---|---|
| `OnDemand` | 不提前加载 Bundle，业务首次 `LoadAsync` 时获取 | 只由业务 Handle 持有 | 小项目或可接受首次加载等待 |
| `TagsOnLaunch` | `ProcedureHotfix` 加载 `LaunchHotfixTags` 对应 Bundle | 预热成功后立即释放 WarmupGroup，启动 DLL 消费完成后清理无引用 Bundle | Nova 默认策略，适合中大型项目 |
| `AllOnLaunch` | Manifest 就绪后加载目标 Package 的全部 Bundle | WarmupGroup 持有到 AssetManager Shutdown | 资源量小、内存充足、希望运行期平滑 |

三种运行策略与首包构建布局正交：运行策略决定浏览器何时请求、加载与释放 Bundle；`BundledCopyOption` 决定 Bundle 来自 `StreamingAssets`、CDN 或两者组合。`AllOnLaunch` 是项目可选能力，不作为框架默认值。

> [!important] OnDemand 与 TagsOnLaunch 的决定性区别
> `OnDemand` 不会因为配置了 `LaunchHotfixTags` 而创建 WarmupGroup，也不使用这些 Tag 发起资源加载。
> `TagsOnLaunch` 会在启动阶段使用 `LaunchHotfixTags` 创建 `IAssetWarmupGroup`，实际请求对应 Bundle 及其依赖，随后立即释放 Handle；真正的无引用 Bundle 清理由 `ProcedureLoadDll` 消费完启动 DLL 后执行。

### 3. LaunchHotfixTags 只定义启动预热范围

WebGL 下 `LaunchHotfixTags` 不承担首包拷贝范围或完整性约束，只作为 `TagsOnLaunch` 的 Warmup 输入。首包 Tag 由 Pipify 中 `BundledCopyParams` 独立配置；两者可以按项目需求取相同值，但职责不能混用。

配置字段本身不发起请求、不加载 Bundle、不占用运行内存；只有 `TagsOnLaunch` 启动流程会读取它并显式创建 WarmupGroup。WebGL Inspector 对用户显示“启动预热资源 Tag”，内部字段名保持不变。

### 4. WebGL Downloader 不承担 Bundle 预下载

WebServer/WebNetwork 文件系统没有 Sandbox 式 Bundle 预下载能力。`CreateDownloader*()` 为空不能证明缓存完整，WebGL 启动链不得再用 Downloader 数量作为资源完整性或本地可启动版本依据。

WebGL 的有效路径是：

1. 业务直接异步加载；或
2. 通过 `IAssetWarmupGroup` 提前加载 Bundle。

Android/iOS 的 Downloader 与 Sandbox 行为不受此决策影响。

### 5. IAssetWarmupGroup 是成组加载器与 Bundle Handle 保管箱

`IAssetWarmupGroup` 不是 Procedure 阶段，也不负责显示 Loading UI。它是批量加载器和临时的 Bundle Handle 保管箱，负责：

- 按全部资源、Tag 或 Location 选择范围；
- 通过 YooAsset `LoadBundleFileAsync` 异步加载 Bundle 及依赖；
- 集中持有 `BundleFileHandle`；
- 提供进度、失败、取消与幂等 `Release()`；
- 预热成功、失败、取消或 AssetManager Shutdown 时按所属策略释放。

推荐公开创建入口为 `CreateWarmupAll`、`CreateWarmupByTags` 与 `CreateWarmupByLocations`。Loading UI 只订阅 WarmupGroup 的进度，不成为资源所有者。

### 6. 启动缓存预热与内存驻留分层

WebGL 的 `LoadBundleFileAsync` 会通过启用 Unity Web Cache 的 `DownloadHandlerAssetBundle` 请求 Bundle。请求成功后，浏览器缓存与 Bundle 运行时内存彼此独立：

```text
服务器 Bundle
   ↓ Warmup 下载并加载
Unity Web Cache + Bundle 运行时内存
   ↓ WarmupGroup.Release()
撤销 Warmup 引用，暂不删除 Unity Web Cache
   ↓ 启动 DLL 消费完成后 CleanupAsync()
释放无其他引用的 Bundle 运行时内存
```

三种策略的 Handle 生命周期固定为：

- `OnDemand`：不创建启动 WarmupGroup。
- `TagsOnLaunch`：`ProcedureHotfix` 等待预热成功后立即释放 WarmupGroup；`ProcedureLoadDll` 消费完 AOT 与业务 DLL 后再清理无引用 Bundle。业务无需、也不允许调用 `Nova.Asset.ReleaseLaunchWarmup()`。
- `AllOnLaunch`：由 AssetManager 持有 WarmupGroup 到 Shutdown，实现全量常驻。

业务主动调用 `CreateWarmupAll`、`CreateWarmupByTags` 或 `CreateWarmupByLocations` 创建的普通 WarmupGroup，仍由业务按自己的用途负责释放。释放 WarmupGroup 不会删除 Unity Web Cache；后续业务仍必须使用异步加载接口，命中缓存时只省去再次从服务器下载，Bundle 与 Asset 仍需重新加载进内存。

Unity Web Cache 由浏览器管理，不等同于 Android/iOS Sandbox 的持久文件，也不保证永久保留；无痕模式、用户清理和容量淘汰都可能使缓存失效。

### 7. WebGL 首包布局与运行时文件系统必须对应

Nova 管理的 WebGL Bundle/RawFile 构建入口开放三种清理式布局：

| `BundledCopyOption` | 构建结果 | HostPlayMode | OfflinePlayMode |
|---|---|---|---|
| `None` | 不复制 Catalog、Manifest/Bundle，并清理目标 Package 旧首包目录 | 仅 `WebNetwork`，纯 CDN | 初始化失败 |
| `ClearAndCopyByTags` | 复制 Manifest、Catalog 与指定 Tag Bundle | `WebServer + WebNetwork` | 不支持；访问未随包资源会失败 |
| `ClearAndCopyAll` | 复制 Manifest、Catalog 与全部 Bundle | `WebServer + WebNetwork` | 纯 `WebServer`，仅此布局完整支持 |

WebGL 禁止 `OnlyCopyAll` / `OnlyCopyByTags`，因为不清理旧目录会让历史 Catalog 与 Bundle 污染当前构建。Nova 不生成私有布局标记，也不修改 YooAsset 原包。Host 启动时探测 `StreamingAssets/{YooFolderName}/{PackageName}/BuiltinCatalog.bytes`：可访问时创建 `WebServer + WebNetwork`，不存在或探测失败时创建纯 `WebNetwork`。`None` 构建成功后必须删除目标 Package 的旧首包目录，使 Catalog 缺失能够准确表示纯 CDN。

Offline 不探测 Catalog，也不创建 `WebNetwork`，固定使用纯 `WebServer`。它的完整部署契约是 `ClearAndCopyAll`；缺少 Catalog 时由 YooAsset 初始化明确失败。Catalog 不能区分按 Tag 与全量拷贝，因此错误使用 `ClearAndCopyByTags` 时可能初始化成功、但在访问未随包资源时失败。

纯 CDN 没有首包 Manifest，因此不会进入 WebGL 首包 Manifest 回退；探测到 Catalog 的 Host 包仍可在远端元数据失败时使用首包 Manifest。为确保 `None` 清理旧 Catalog，项目构建统一走 Nova Builder、Pipify 或受控 Build Action。

WebGL 导出后 `StreamingAssets` 仍是服务器上的独立 URL 目录，不会因为 CopyAll 就在打开网页时全部下载。`OnDemand` / `TagsOnLaunch` / `AllOnLaunch` 分别决定按需请求、启动请求指定 Tag，或启动请求全部 Bundle。

### 8. 删除旧 PreloadAsync

删除 `IAssetManager`、`AssetManagerBase`、`AssetManager` 与 `AssetComponent` 上的旧 `PreloadAsync`，不保留兼容层或 `[Obsolete]` 转发。新能力统一由 `IAssetWarmupGroup` 表达明确所有权。

## 后果（Consequences）

### 正面

- 首包、网络请求、内存驻留和业务生命周期不再混为一谈。
- WebGL 可按项目部署需要选择纯 CDN、Tag 首包或全量首包，并由运行时自动匹配文件系统。
- WebGL Offline 的职责单一：只读取随站点部署的完整 StreamingAssets，不隐式访问 CDN。
- `OnDemand`、`TagsOnLaunch`、`AllOnLaunch` 覆盖不同项目体量，不强迫所有项目采用同一策略。
- `TagsOnLaunch` 由框架在预热完成后自动释放，不要求业务理解或维护启动 Warmup Handle 生命周期。
- 不修改 YooAsset 厂商源码，升级边界清晰。

### 负面

- `AllOnLaunch` 可能显著增加启动时间和内存峰值，必须由项目主动选择并做浏览器实测。
- `TagsOnLaunch` 的后续首次业务加载仍有 Bundle 解压和 Asset 加载开销，只消除已命中缓存时的远端下载开销。
- Unity Web Cache 可能被浏览器淘汰，不能作为永久离线资源保证。
- 首包布局与运行策略是两组独立配置，项目需要明确选择；配置错误会在初始化或构建阶段失败，而不是静默使用错误来源。
- CopyAll 会增大 WebGL 服务器部署目录，但不会将全部 Bundle 并入网页启动必下的 `.data` 文件。
- 删除 `PreloadAsync` 是公共 API 破坏性变更，外部消费者需要改用 WarmupGroup。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 把空 Downloader 当作已缓存 | Web 文件系统不提供该证明，可能产生错误的启动完整性结论 |
| 保留旧 `PreloadAsync` 隐式持有 | 调用方没有释放入口，违反 Handle 所有权规则 |
| 由业务调用 `Nova.Asset.ReleaseLaunchWarmup()` | 业务难以准确判断释放时机，且启动缓存预热完成后即可由 `ProcedureHotfix` 自动释放 |
| `TagsOnLaunch` 等业务 Handle 接管后再释放 | 这会把缓存预热误做成临时内存常驻，增加调用约束和启动后内存占用 |
| 修改 YooAsset WebNetwork 下载实现 | 增加厂商升级成本；现有 `LoadBundleFileAsync` 足以承载 Warmup |
| 强制所有项目 AllOnLaunch | 大型项目启动流量与内存不可接受 |
| WebGL 允许 `OnlyCopy*` | 旧文件可能残留，无法根据本次配置可靠推导真实部署布局 |
| 在 AssetComponent 再配置一份首包模式 | 与 Pipify 构建参数形成双真相源，运行时仍无法证明 Player 内实际打入了什么 |
| 生成 Nova 私有布局标记 | YooAsset 官方 Catalog 已足以区分“有首包”和“纯 CDN”；额外文件会形成双真相源和升级维护负担 |

## 当前落地状态与验证依据

本决策已落地到 Runtime、Inspector、Bundle 构建入口、Pipify、受控 Build Action、Docs 和契约测试：

- `WebGLAssetStrategy` 固定 `TagsOnLaunch = 0`，旧场景新增字段后仍选择 Nova 默认策略。
- `IAssetManager`、`AssetManagerBase` 与 `AssetComponent` 已删除 `PreloadAsync`，新增 `CreateWarmupAll`、`CreateWarmupByTags`、`CreateWarmupByLocations`；仓外自定义 Manager 需要补齐三项成员。
- `AssetWarmupGroup` 集中持有 `BundleFileHandle`，并提供完成数进度、失败信息、取消和幂等释放。启动流程中的 `TagsOnLaunch` 成功后先 `Release`，等启动 DLL 消费完成后再 `Cleanup`；`AllOnLaunch` 由 AssetManager 持有至 `Shutdown`；失败或取消释放并清理本轮 Group。
- `ProcedureCheckVersion` 将 `HasAssetPatch` 与 `RequiresStartupAssetWork` 分开：WebGL `OnDemand` 不进入 `ProcedureHotfix`，`TagsOnLaunch` / `AllOnLaunch` 则即使关闭常规热更新也会完成 Warmup。推荐 App 更新取消后同样按后者恢复启动资源工作。
- `LaunchHotfixTags` 会清理空白和重复项；WebGL 的 `TagsOnLaunch` 清理后为空时降级为 `OnDemand`。
- `BuildAssetBundle`、`BuildRawFileBundle`、Pipify 和受控 Build Action 在 WebGL 目标支持 `None`、`ClearAndCopyByTags`、`ClearAndCopyAll`，拒绝 `OnlyCopy*`；非 WebGL 保留原有构建选择。
- Nova 不写私有 WebGL 布局文件；`None` 成功后通过 Unity 资产删除语义精确清理该 Package 的旧首包目录及 `.meta`，不清空整个 `StreamingAssets`。微信纯 CDN 前置校验忽略 `.meta`，只把真实 YooAsset 文件视为首包内容。Host 依据官方 `BuiltinCatalog.bytes` 是否可访问选择 WebNetwork-only 或 WebServer + WebNetwork，Offline 固定为 WebServer-only。
- `WebGLBundleRequestTimeout` 覆盖 WebServer（StreamingAssets）和 WebNetwork（CDN）的 Bundle 单次请求；`.version` 使用 `CheckTimeout`，`.hash/.bytes` 使用 `ManifestRequestTimeout`。只有 Host 的 CDN Bundle 与远端元数据请求复用 Asset 的主备、重试、最近成功域名和 UWR 埋点；StreamingAssets 请求使用对应超时，但不参与 CDN 候选轮换。
- WebGL 远端 Manifest 候选耗尽时仍可临时回退随 Player 发布的首包元数据；Bundle 不因该回退改写自身的 WebServer/WebNetwork 拓扑。

缓存结论仍须按资源类型区分：未加密 AssetBundle 可利用 Unity Web Cache（Nova 默认不关闭它）；加密 AssetBundle、RawBundle 和 ArchiveBundle 走字节/内存装载，Warmup 后不承诺留在 Web Cache。`Cancel()` 只能停止 Group 等待并释放其 Handle，不能承诺已发出的 HTTP 请求立刻停止。

当前代码与契约测试依据包括：`WebGLAssetStrategyContractTests`、`ProcedureAppDownloadRegressionTests`、`AssetManagerManifestFallbackRegressionTests`、`BuildActionCommonTests`、`RawFileBundleBuilderTests` 与 `YooAsset305UpgradeContractTests`。这些覆盖 API、路由和构建参数，但不替代浏览器验证；发版前仍需验证三种首包布局产物、三种策略的实际请求时序、TagsOnLaunch 释放后的缓存命中、失败/取消、浏览器缓存清理后的重新请求、内存峰值、CDN/CORS、Manifest 回退及 Android/iOS Downloader 回归。
