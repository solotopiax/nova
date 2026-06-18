# ProcedureHotfix

`ProcedureHotfix` 是资源补丁下载流程。

它负责：

- 显示启动期热更进度 UI
- 创建整包或 Tag 切片下载器
- 处理下载成功、失败、重试、取消四条分支
- 成功后可选清理冗余缓存

它不是资源检查流程，也不是业务资源预加载流程。

## 什么时候先看这页

优先看这页的场景：

- 你要排查为什么热更失败后会弹重试框。
- 你要确认取消热更后为什么有时退出应用、有时继续启动。
- 你要看 `LaunchHotfixTags` 和整包下载是怎么二分的。

## 输入 / 输出

### 输入

- `AssetComponent.MaxDownloadConcurrency`
- `AssetComponent.RetryDownloadCount`
- `AssetComponent.LaunchHotfixTags`
- `AssetComponent.QuitOnFailedOrCancel`
- `AssetComponent.AutoClearUnusedCacheOnHotfix`
- `IAssetManager.CreateDownloader*()`

### 输出

- Hotfix 进度 UI
- Hotfix 失败重试弹窗
- 成功或跳过后进入 `ProcedureLoadDll`

## 主链路

### 1. OnEnter：重置状态，显示进度，启动下载

进入流程时会：

- 重置完成态、成功态、用户重试计数和进度缓存
- 清理 `ProcedureDataKeys.AppVersionResult`
- 清理 `ProcedureDataKeys.HasAssetPatch`
- `LauncherUIController.ShowProgress(LauncherStage.Hotfix)`
- `TryDownloadAsync(CancellationToken).Forget()`

也就是说，这一页开始后，版本检查结果就不再继续保留在流程黑板里。

### 2. TryDownloadAsync：整包下载或 Tag 切片下载

下载器选择规则是：

- `LaunchHotfixTags` 为空：`CreateDownloader(...)`
- `LaunchHotfixTags` 非空：`CreateDownloaderByTags(...)`

如果下载器 `IsEmpty`：

- 直接标记成功
- 不弹框
- 后续 `OnUpdate()` 会直接进入 `ProcedureLoadDll`

### 3. 下载进度会持续写回 UI 和日志

下载过程中会挂两类回调：

- `OnProgress`
- `OnFileStarted`

其中：

- UI 进度通过 `LauncherUIController.UpdateProgress(downloader.Progress)` 更新
- 日志按 10% 档位输出一次，不是每一帧刷日志

### 4. 成功、失败、取消三种结果

#### 成功

- 可选执行 `ClearUnusedCacheAsync(ct)`
- 缓存清理失败只记 warning，不阻断流程
- `m_Success = true`
- `m_Complete = true`

#### 失败

- 显示 `LauncherDialogType.HotfixFailed`
- Confirm 走重试
- Cancel 走退出或跳过

#### 取消

取消按钮行为由 `QuitOnFailedOrCancel` 决定：

- `true`：`Nova.Self.QuitApplication()`，运行时退出应用，Editor 下同时停止 PlayMode
- `false`：视为“跳过热更进入游戏”，`m_Success = false; m_Complete = true`

所以当前实现并不要求热更成功后才允许继续启动。

### 5. OnUpdate：完成后统一进入 LoadDll

只要 `m_Complete == true`，就会：

- 输出最终进度日志
- `ChangeState<ProcedureLoadDll>(procedureOwner)`

无论是“下载成功”还是“用户选择跳过”，目标流程都是 `ProcedureLoadDll`。

### 6. OnLeave：关闭时取消下载，shutdown 时兜底清 UI

离开流程时会：

- 取消并释放当前轮次 `m_DownloadCts`
- `isShutdown == true` 时销毁 Progress 和 Dialog

正常切流程时不主动销毁启动期 UI，仍允许后续流程统一接管。

## 风险点 / 易错点

- `m_UserRetryCount` 只是日志计数，不是上限控制。
- “取消热更”在当前实现里可能仍继续进游戏，这取决于 `QuitOnFailedOrCancel`。
- 热更完成后是否清缓存，不影响是否继续进入后续流程。
- `ProcedureHotfix` 自己会清掉版本检查黑板键，不要期待这些数据还能被更后面的流程继续读取。

## 继续阅读

关键源码：

- [ProcedureHotfix.cs](../../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureHotfix.cs)

相关文档：

- [LauncherUIController.md](LauncherUIController.md)
- [LauncherStage.md](LauncherStage.md)
- [LauncherDialogType.md](LauncherDialogType.md)
- [ProcedureCheckVersion.md](ProcedureCheckVersion.md)
- [Procedures/ProcedureLoadDll.md](Procedures/ProcedureLoadDll.md)
