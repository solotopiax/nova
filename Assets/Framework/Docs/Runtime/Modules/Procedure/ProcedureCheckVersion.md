# ProcedureCheckVersion

`ProcedureCheckVersion` 是启动链里的“路由判定”流程。

它负责把两类事实汇总出来：

- App 大版本检查结果
- 当前是否存在资源补丁

然后把结果写进流程黑板，再决定后续进入 `ProcedureAppDownload`、`ProcedureHotfix` 还是 `ProcedureLoadDll`。

## 主链路

### 1. OnEnter：重置状态并启动异步检查

进入流程时会重置：

- `m_CheckComplete = false`
- `m_AppResult = NoDownload`
- `m_HasAssetPatch = false`
- `m_HasError = false`

然后启动 `RunCheckAsync(procedureOwner, CancellationToken).Forget()`。

### 2. RunCheckAsync：先看 App，再按 EnableHotfix 决定是否看资源

异步主链是：

1. `m_AppResult = await appManager.CheckAsync(ct)`
   - 如果 `AppDownloadCheckUrl` 为空，`CheckAsync()` 会直接降级返回 `NoDownload`
   - 不报错、不阻断启动
2. 如果是 `ForcedDownload`
   - 直接标记完成
   - 跳过资源清单和补丁检查
3. 否则继续
   - `EnableHotfix == false`
     - 直接跳过资源清单和补丁检查
     - `m_HasAssetPatch = false`
   - `EnableHotfix == true`
     - `await assetManager.BootstrapAsync(ct)`
     - `await assetManager.LoadManifestAsync(null, ct)`
     - `m_HasAssetPatch = await assetManager.HasPatchAsync(null, ct)`

### 3. OnUpdate：完成后写黑板并路由

检查完成后会先写：

- `ProcedureDataKeys.AppVersionResult`
- `ProcedureDataKeys.HasAssetPatch`

然后按当前实现路由：

- `ForcedDownload`：`ProcedureAppDownload`
- `RecommendedDownload`：`ProcedureAppDownload`
- `NoDownload && HasAssetPatch == true`：`ProcedureHotfix`
- `NoDownload && HasAssetPatch == false`：`ProcedureLoadDll`

推荐更新的特殊点在于：

- 先完成资源补丁检查
- 再进入 `ProcedureAppDownload`
- 用户取消推荐更新后，会根据 `HasAssetPatch` 回到 `ProcedureHotfix` 或 `ProcedureLoadDll`

### 4. 异常保护：检查失败时降级直入 LoadDll

如果检查链里出现不可恢复异常：

- `m_HasError = true`
- `OnUpdate()` 会 warning 后直接 `ChangeState<ProcedureLoadDll>()`

## 风险点 / 易错点

- `ForcedDownload` 会跳过资源检查；它不是“强更后再看资源补丁”的组合路径。
- `RecommendedDownload` 现在是框架内建弹窗分支，不再与 `NoDownload` 共享同一路由。
- `EnableHotfix` 现在只影响资源热更检查，不影响 App 大版本检测是否执行。

## 继续阅读

关键源码：

- [ProcedureCheckVersion.cs](../../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureCheckVersion.cs)

相关文档：

- [ProcedureDataKeys.md](ProcedureDataKeys.md)
- [ProcedureHotfix.md](ProcedureHotfix.md)
- [Procedures/ProcedureAppDownload.md](Procedures/ProcedureAppDownload.md)
- [Procedures/ProcedureLoadDll.md](Procedures/ProcedureLoadDll.md)
- [../App/Definitions/AppVersionResult.md](../App/Definitions/AppVersionResult.md)
