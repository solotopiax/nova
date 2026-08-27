# ProcedureAppDownload

`ProcedureAppDownload` 是启动链里的“大版本下载提示”流程。

它统一承接两类情况：

- 强制更新提示
- 推荐更新提示

## 输入 / 输出

### 输入

- `AppComponent.DownloadRoute`
- `ProcedureDataKeys.AppVersionResult`
- `ProcedureDataKeys.HasAssetPatch`

### 输出

- 大版本更新对话框显示与重复显示
- 强制更新取消时退出应用
- 推荐更新取消后回到 `ProcedureHotfix` 或 `ProcedureLoadDll`

## 主链路

### 1. OnEnter：读取黑板并立刻弹框

进入流程时会读取：

- `AppVersionResult`
- `HasAssetPatch`

然后立刻调用 `ShowDialog()`。

如果误收到 `NoDownload` 或未知枚举值，会 warning 后直接跳过下载提示，不会把异常输入当成推荐更新，也不会记录放弃时间。

### 2. Confirm：按路由执行跳商店或下载

确认按钮回调 `OnConfirm()` 会读取 `AppComponent.DownloadRoute`：

- `Store`：调用 `OpenStoreAsync(CancellationToken)`
- `Apk`：调用 `DownloadAsync(CancellationToken)`

无论成功、失败还是异常，`finally` 都会再次 `ShowDialog()`。

### 3. Cancel：强制更新退出，推荐更新继续

- `ForcedDownload`：调用 `Nova.Self.QuitApplication()`，运行时退出应用，Editor 下同时停止 PlayMode
- `RecommendedDownload`：先调用 `AppComponent.RecordRecommendedDownloadDismissed()` 持久化本次放弃时间，再令 `m_Complete = true`

### 4. OnUpdate：推荐更新取消后按补丁状态续行

当流程完成后：

- `RecommendedDownload && HasAssetPatch == true`：进入 `ProcedureHotfix`
- 其他情况：进入 `ProcedureLoadDll`

## 风险点 / 易错点

- 当前 `DownloadAsync()` 在 `AppManager.Download.cs` 里仍是待补实现；非商店路由链路目前并未真正打通。
- 确认后的下载/跳商店操作不会自动让流程继续，而是重新显示弹窗，等待用户下一次操作。
- 只有推荐更新取消分支会记录放弃时间；确认更新与强制更新取消均不会影响推荐提示间隔。
- 放弃时间落盘失败只输出 warning，推荐取消分支仍必须继续后续启动流程。

## 继续阅读

关键源码：

- [ProcedureAppDownload.cs](../../../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureAppDownload.cs)
- [AppManager.Download.cs](../../../../../Scripts/Runtime/Modules/App/Managers/AppManager/Implements/AppManager.Download.cs)

相关文档：

- [../ProcedureLoadDll.md](../ProcedureLoadDll.md)
- [../ProcedureHotfix.md](../ProcedureHotfix.md)
- [../ProcedureDataKeys.md](../ProcedureDataKeys.md)
- [../../App/AppComponent.md](../../App/AppComponent.md)
- [../../App/Definitions/AppDownloadRoute.md](../../App/Definitions/AppDownloadRoute.md)
- [../../App/Definitions/AppVersionResult.md](../../App/Definitions/AppVersionResult.md)
