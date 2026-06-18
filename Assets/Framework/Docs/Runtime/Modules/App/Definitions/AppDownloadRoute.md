# AppDownloadRoute

`AppDownloadRoute` 定义的是命中大版本更新后的下载路由方式。

当前可用值有：

- `Store`
- `Apk`

## 当前语义边界

- `Store`：后续流程会走 `OpenStoreAsync()`
- `Apk`：后续流程会走 `DownloadAsync()`

但要注意，当前实现上只有商店跳转链是完成的；`Apk` 对应的 `DownloadAsync()` 仍是占位骨架。

## 风险点 / 易错点

- 把 `DownloadRoute` 设成 `Apk`，不代表当前项目已经具备可用下载链。
- 这个枚举只负责分流，不负责解析目标 URL。

## 继续阅读

关键源码：

- [AppDownloadRoute.cs](../../../../../Scripts/Runtime/Modules/App/Definitions/AppDownloadRoute.cs)

相关文档：

- [AppManagerConfig.md](AppManagerConfig.md)
- [../AppManager/AppManager.md](../AppManager/AppManager.md)
- [../../Procedure/Procedures/ProcedureAppDownload.md](../../Procedure/Procedures/ProcedureAppDownload.md)
