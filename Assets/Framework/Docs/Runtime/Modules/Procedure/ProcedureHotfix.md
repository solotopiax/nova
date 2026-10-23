# ProcedureHotfix

`ProcedureHotfix` 是启动阶段的资源处理流程。

它负责：

- 显示启动期热更进度 UI
- 在非 WebGL 上创建整包或 Tag 切片下载器，在微信 TagsOnLaunch 下使用 Tag 原生预下载器
- 在浏览器 TagsOnLaunch 与 WebGL AllOnLaunch 策略下执行 Bundle Warmup
- 处理下载或 Warmup 的成功、失败、重试、取消分支
- 成功后可选清理冗余缓存

它不是资源检查流程，也不替业务持有普通资源 Handle。业务主动创建的 `IAssetWarmupGroup` 仍由业务负责释放。

## 什么时候先看这页

优先看这页的场景：

- 你要排查为什么热更失败后会弹重试框。
- 你要确认取消热更后为什么有时退出应用、有时继续启动。
- 你要看 `LaunchHotfixTags`、下载器和 WebGL Warmup 是怎么二分的。

## 输入 / 输出

### 输入

- `AssetComponent.MaxDownloadConcurrency`
- `AssetComponent.RetryDownloadCount`
- `AssetComponent.LaunchHotfixTags`
- `AssetComponent.QuitOnFailedOrCancel`
- `AssetComponent.AutoClearUnusedCacheOnHotfix`
- `IAssetManager.CreateDownloader*()`
- 内置 `IAssetStartupWarmupController`（仅用于 WebGL 启动 Warmup 编排）

### 输出

- Hotfix 进度 UI
- Hotfix 失败重试弹窗
- 成功或跳过后进入 `ProcedureLoadDll`

## 主链路

### 1. OnEnter：重置状态并启动下载检查

进入流程时会：

- 重置完成态、成功态、用户重试计数和进度缓存
- 清理 `ProcedureDataKeys.AppVersionResult`
- 清理 `ProcedureDataKeys.HasAssetPatch`
- 清理 `ProcedureDataKeys.RequiresStartupAssetWork`
- `TryDownloadAsync(CancellationToken).Forget()`

也就是说，这一页开始后，版本检查结果就不再继续保留在流程黑板里。

### 2. 先判断本轮是 WebGL Warmup 还是常规下载

`ProcedureCheckVersion` 只会在需要启动资源工作时进入本流程：

- 非 WebGL：有补丁时进入，使用 Downloader。
- WebGL `TagsOnLaunch`：创建按 `LaunchHotfixTags` 选择范围的 WarmupGroup。
- WebGL `AllOnLaunch`：创建默认 Package 全量 WarmupGroup。
- WebGL `OnDemand`：不会进入本流程，直接进入 `ProcedureLoadDll`，业务后续只能异步按需加载。

`EnableHotfix` 不会跳过 WebGL 的 `TagsOnLaunch` / `AllOnLaunch` Warmup；它只控制常规补丁检查和 Downloader 下载链。

### 3. 常规下载：整包下载或 Tag 切片下载

下载器选择规则是：

- `LaunchHotfixTags` 为空：`CreateDownloader(...)`
- `LaunchHotfixTags` 非空：`CreateDownloaderByTags(...)`

如果下载器 `IsEmpty`：

- 调用 `CommitBootableVersion()` 记录当前启动范围已就绪的版本
- 不创建进度面板，避免无下载内容时闪现 0%
- 直接标记成功
- 不弹框
- 后续 `OnUpdate()` 会直接进入 `ProcedureLoadDll`

只有下载器非空时才调用 `LauncherUIController.ShowProgress(LauncherStage.Hotfix)`，随后注册下载回调并开始下载。

### 4. 常规下载进度会持续写回 UI 和日志

下载过程中会挂两类回调：

- `OnProgress`
- `OnFileStarted`

其中：

- UI 进度通过 `LauncherUIController.UpdateProgress(downloader.Progress)` 更新
- 日志按 10% 档位输出一次，不是每一帧刷日志

### 5. WebGL Warmup：按完成项数展示进度

WarmupGroup 的进度是“已完成 Bundle 项数 / 总项数”，不代表字节进度。流程不会把它伪造成下载字节数。

- 成功的 `TagsOnLaunch`：框架立即 `Release()` Group；`ProcedureLoadDll` 消费完 AOT 与业务 DLL 后，再执行 `CleanupAsync()` 回收无其他引用的运行时 Bundle。
- 成功的 `AllOnLaunch`：AssetManager 接管 Group，持有到 `Shutdown()`；不会在本流程释放。
- 失败：释放本轮 Group，显示失败重试框；用户确认时创建新的 Group 重新执行。
- 生命周期取消：停止等待并释放本轮 Group。已发出的 YooAsset / 浏览器请求可能自然结束，不能把取消当成网络已立即停止的证明。

### 6. 成功、失败、取消三种结果

#### 成功

- 常规下载成功：调用 `CommitBootableVersion()` 推进本地可启动版本，并可选执行 `ClearUnusedCacheAsync(ct)`。
- WebGL Warmup 成功：由策略的 Group 收口完成后调用 `CommitBootableVersion()`。
- 内置 `ClearUnusedCacheAsync` 报告底层失败、清单未就绪或等待取消时进入既有异常分支，只记 warning，不打印“冗余缓存清理完成”，仍不阻断进游戏；取消等待不代表底层删除已经停止。
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

WebGL 固定按 `false` 处理，不执行应用强退；Android/iOS 继续使用 Inspector 中保存的配置。

所以当前实现并不要求热更成功后才允许继续启动。

### 7. OnUpdate：完成后统一进入 LoadDll

只要 `m_Complete == true`，就会：

- 输出最终进度日志
- `ChangeState<ProcedureLoadDll>(procedureOwner)`

无论是“下载成功”还是“用户选择跳过”，目标流程都是 `ProcedureLoadDll`。

### 8. OnLeave：关闭时取消当前工作，shutdown 时兜底清 UI

离开流程时会：

- 取消并释放当前轮次 `m_DownloadCts`；它同时是 Downloader 或 Warmup 的等待令牌
- `isShutdown == true` 时销毁 Progress 和 Dialog

正常切流程时不主动销毁启动期 UI，仍允许后续流程统一接管。

## 风险点 / 易错点

- `m_UserRetryCount` 只是日志计数，不是上限控制。
- Android/iOS 的“取消热更”是否继续进游戏取决于 `QuitOnFailedOrCancel`；WebGL 固定继续启动。
- 热更完成后是否清磁盘缓存，不影响是否继续进入后续流程；`TagsOnLaunch` 的 Release 与 DLL 加载后 Cleanup 是固定的内存收口，不由 `AutoClearUnusedCacheOnHotfix` 控制。
- `AllOnLaunch` 是明确的内存常驻策略，必须结合实际浏览器内存峰值选择。
- `ProcedureHotfix` 自己会清掉版本检查黑板键，不要期待这些数据还能被更后面的流程继续读取。

## 参数与生命周期边界

TryDownloadAsync 以 attempt/complete/token 互斥，StartDownloadAttempt 只 Forget，不独立 guard。重试计数只是用户点击次数，不重查 App 版本或重新创建整个 Procedure。常规 downloader 回调 lambda 本流程未显式解绑/释放 downloader；其资源责任以实际 Asset 下载器实现为准，不能将过程完成当作已断全部回调。

失败弹窗缺面板时 Controller 同步调用重试，若 attempt 仍 true 则被 guard 忽略，不能保证此降级总能续行。用户选择跳过设置 success=false/complete=true，仍进入 LoadDll，不证明资源完整；WebGL 强制按跳过处理。离开先 base.Cancel 再取消/释放 linked CTS，普通切换保留面板，Shutdown 才兜底销毁。

Warmup 的完成项数不等字节，total<=0 比率为 1，UI clamp 而日志百分比不 clamp。正常完成先解绑 OnWarmupProgress，再把 group 所有权移交 CompleteLaunchWarmupAsync（本地变量先置空）；此后异常由接手方负责资源。取消只释放仍本地持有 group，finally 释放 linked CTS；清理异常和缓存清理失败不自动变成成功的资源证明。

## 继续阅读

关键源码：

- [ProcedureHotfix.cs](../../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureHotfix.cs)

相关文档：

- [LauncherUIController.md](LauncherUIController.md)
- [LauncherStage.md](LauncherStage.md)
- [LauncherDialogType.md](LauncherDialogType.md)
- [ProcedureCheckVersion.md](ProcedureCheckVersion.md)
- [Procedures/ProcedureLoadDll.md](Procedures/ProcedureLoadDll.md)

## 微信原生预下载分支

实际注入平台预下载的微信 TagsOnLaunch 不创建 WarmupGroup，直接复用 IAssetDownloader。完成请求不持有 Unity Bundle 内存引用；取消与失败由 YooAsset 释放请求和冲突访问门。AllOnLaunch 仍持有 WarmupGroup 到 Shutdown，OnDemand 不进入本阶段。预下载成功不能证明平台缓存永久可用，Web Player 不写原生本地可启动版本。
