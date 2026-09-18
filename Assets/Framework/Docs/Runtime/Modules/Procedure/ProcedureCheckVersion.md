# ProcedureCheckVersion

`ProcedureCheckVersion` 是启动链的路由判定点。它把 App 大版本结果、常规资源补丁结果和 WebGL 启动 Warmup 需求分开计算，最后写入流程黑板并决定后续进入 `ProcedureAppDownload`、`ProcedureHotfix` 或 `ProcedureLoadDll`。

## 主链路

1. 先执行 `await IAppManager.CheckAsync(ct)`。
2. `ForcedDownload` 立即结束资源判断，路由 `ProcedureAppDownload`。
3. 非强更时，按当前 `EnableHotfix` 与 WebGL 策略决定是否 Bootstrap、加载 Manifest、检查补丁和/或准备 Warmup。
4. 把 `AppVersionResult`、`HasAssetPatch`、`RequiresStartupAssetWork` 写入黑板。
5. 推荐更新优先进入 `ProcedureAppDownload`；其余情况由 `RequiresStartupAssetWork` 决定是否进 `ProcedureHotfix`。

## WebGL 与非 WebGL 的路由差异

| 平台 / 状态 | `HasAssetPatch` | `RequiresStartupAssetWork` | 后续资源流程 |
|---|---|---|---|
| 非 WebGL，存在补丁 | `true` | `true` | `ProcedureHotfix` 创建 Downloader |
| 非 WebGL，无补丁 | `false` | `false` | `ProcedureLoadDll` |
| WebGL `OnDemand` | 不执行 Downloader 差异检查 | `false` | 跳过 `ProcedureHotfix`，业务异步按需加载 |
| WebGL `TagsOnLaunch` | 不执行 Downloader 差异检查 | `true` | `ProcedureHotfix` 创建 Tag WarmupGroup |
| WebGL `AllOnLaunch` | 不执行 Downloader 差异检查 | `true` | `ProcedureHotfix` 创建全量 WarmupGroup |

因此 WebGL 的 `HasAssetPatch` 固定为 `false`；只有 `RequiresStartupAssetWork` 才是启动路由真相。`OnDemand` 不会进入 Downloader / Warmup 阶段。

## EnableHotfix 的边界

`EnableHotfix` 只关闭常规资源补丁检查和 Downloader 路径，不关闭 App 大版本检查。

- `EnableHotfix=false` 且不需要 WebGL Warmup：不 Bootstrap、不加载 Manifest，直入 `ProcedureLoadDll`。
- `EnableHotfix=false` 且 WebGL 为 `TagsOnLaunch` / `AllOnLaunch`：仍 Bootstrap、加载 Manifest，并进入 `ProcedureHotfix` 完成启动预热。
- `OnDemand` 不创建启动 WarmupGroup；`TagsOnLaunch` 没有有效 Tag 时也会先降级为 `OnDemand`。

## 推荐更新取消后的续行

`RecommendedDownload` 仍会在资源判断之后先进入 `ProcedureAppDownload`。用户取消推荐更新时，`ProcedureAppDownload` 读取 `RequiresStartupAssetWork`：

- `true`：进入 `ProcedureHotfix`，执行 Downloader 或 WebGL Warmup。
- `false`：直接进入 `ProcedureLoadDll`。

不再用 `HasAssetPatch` 决定这条路由，避免 WebGL `OnDemand` 被错误带回 Hotfix。

## 本地可启动版本提交

当不需要进入启动资源工作阶段时，流程调用 `CommitBootableVersion()`。需要 Warmup 时则由 `ProcedureHotfix` 成功后提交；失败、取消或用户跳过不会把当前状态当作新的可启动资源版本。

## 异常与取消

`OperationCanceledException` 表示流程已离开，不继续路由。其他未恢复异常会记 Warning 并安全降级到 `ProcedureLoadDll`，而不是卡死在启动检查阶段。

## 相关文档

- [ProcedureDataKeys.md](ProcedureDataKeys.md)
- [ProcedureHotfix.md](ProcedureHotfix.md)
- [Procedures/ProcedureAppDownload.md](Procedures/ProcedureAppDownload.md)
- [WebGLAssetStrategies.md](../Asset/WebGLAssetStrategies.md)
