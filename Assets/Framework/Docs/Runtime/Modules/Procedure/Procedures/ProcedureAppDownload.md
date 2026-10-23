# ProcedureAppDownload

`ProcedureAppDownload` 是启动链里的“大版本下载提示”流程。

它统一承接两类情况：

- 强制更新提示
- 推荐更新提示

## 输入 / 输出

### 输入

- `AppComponent.DownloadRoute`
- `ProcedureDataKeys.AppVersionResult`
- `ProcedureDataKeys.RequiresStartupAssetWork`

### 输出

- 大版本更新对话框显示与重复显示
- 强制更新取消时退出应用
- 推荐更新取消后回到 `ProcedureHotfix` 或 `ProcedureLoadDll`

## 主链路

### 1. OnEnter：读取黑板并立刻弹框

进入流程时会读取：

- `AppVersionResult`
- `RequiresStartupAssetWork`

然后立刻调用 `ShowDialog()`。

如果误收到 `NoDownload` 或未知枚举值，会 warning 后直接跳过下载提示，不会把异常输入当成推荐更新，也不会记录放弃时间。

### 2. Confirm：按路由执行跳商店或下载

确认按钮回调 `OnConfirm()` 会读取 `AppComponent.DownloadRoute`：

- `Store`：调用 `OpenStoreAsync(CancellationToken)`
- `Apk`：首次调用 `DownloadAsync(CancellationToken)` 并保存本地路径；后续确认复用该路径，不再次下载
- Native 可选安装已开启：显式调用 `InstallApkAsync(path, ct)`，来源许可不足时打开精准来源设置，返回后由用户再次确认；安装失败/系统取消保留文件等待重试
- Native 未启用、扩展缺失或平台不支持：显示 `AppDownloaded` 文件已保存结果；确认后推荐更新继续、强制更新退出，取消按钮隐藏

`finally` 仅在本轮流程代际仍有效、令牌未取消时刷新对话框；退出后迟到的下载/安装 continuation 不回灌新流程。系统安装成功可能先自然终止旧进程；若旧进程仍存活，则进入 `AppDownloaded` 交付结果确认，推荐更新续行、强制更新退出。框架不主动 kill、不自动重启。

### 3. Cancel：强制更新退出，推荐更新继续

- `ForcedDownload`：调用 `Nova.Self.QuitApplication()`，运行时退出应用，Editor 下同时停止 PlayMode
- `RecommendedDownload`：先调用 `AppComponent.RecordRecommendedDownloadDismissed()` 持久化本次放弃时间，再令 `m_Complete = true`

### 4. OnUpdate：推荐更新取消后按启动资源工作状态续行

当流程完成后：

- `RecommendedDownload && RequiresStartupAssetWork == true`：进入 `ProcedureHotfix`
- 其他情况：进入 `ProcedureLoadDll`

这个键不能用 `HasAssetPatch` 替代：WebGL 不执行 Downloader 差异检查，但 `TagsOnLaunch` / `AllOnLaunch` 仍需要进入 `ProcedureHotfix` 完成启动预热；`OnDemand` 则直接进入 DLL 加载。

## 风险点 / 易错点

- 下载路径只表示已提交本地文件；安装层独立严格验签、同包名/当前 signer/递增 versionCode，未绑定远端命中版本。
- APK 下载完成后不会重复下载；未启用安装时文件交付确认才继续推荐更新，强更仍要求退出。安装失败、取消或来源设置返回后的确认复用保存路径。
- 只有推荐更新取消分支会记录放弃时间；确认更新与强制更新取消均不会影响推荐提示间隔。
- 放弃时间落盘失败只输出 warning，推荐取消分支仍必须继续后续启动流程。
- 离开流程时会清理 `AppVersionResult`、`HasAssetPatch` 与 `RequiresStartupAssetWork` 三个启动黑板键；后续流程不应继续依赖它们。

## 参数与生命周期边界

每次进入重置完成、路径、fileDelivered 与重入门并递增代际；OnLeave 先 base 取消，再递增代际、清三黑板键及销毁 Dialog，不论是否 Shutdown。有效代际校验与冻结 token 用于保护 await 后状态，正式 APK 不在此删除。OnConfirm 在 try 前取得 AppComponent/路由，缺组件等异常不受内部 catch 保障；Store 路由跳转返回 false 也会按现流程再显示对话框，文件交付结果只有后续显式确认才完成。

## 继续阅读

关键源码：

- [ProcedureAppDownload.cs](../../../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureAppDownload.cs)
- [AppManager.Download.cs](../../../../../Scripts/Runtime/Modules/App/Managers/AppManager/Implements/AppManager.Download.cs)

相关文档：

- [ProcedureLoadDll.md](ProcedureLoadDll.md)
- [../ProcedureHotfix.md](../ProcedureHotfix.md)
- [../ProcedureDataKeys.md](../ProcedureDataKeys.md)
- [../../App/AppComponent.md](../../App/AppComponent.md)
- [../../App/Definitions/AppDownloadRoute.md](../../App/Definitions/AppDownloadRoute.md)
- [../../App/Definitions/AppVersionResult.md](../../App/Definitions/AppVersionResult.md)
