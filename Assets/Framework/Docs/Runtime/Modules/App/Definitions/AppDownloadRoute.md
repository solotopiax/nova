# AppDownloadRoute

`AppDownloadRoute` 定义的是命中大版本更新后的下载路由方式。

当前可用值有：

- `Store`
- `Apk`

## 当前语义边界

- `Store`：后续流程会走 `OpenStoreAsync()`
- `Apk`：后续流程会走 `DownloadAsync()`

内置 `Apk` 路由只在 Android Player 下载文件并返回本地绝对路径；它不隐式安装，路径不代表签名或目标版本已经验证。

## 风险点 / 易错点

- `Apk` 还要求启用更新、命中规则及有效 HTTPS 主备地址；Editor、iOS、WebGL 明确拒绝文件下载。
- 这个枚举只负责分流，不负责解析目标 URL。

## 继续阅读

关键源码：

- [AppDownloadRoute.cs](../../../../../Scripts/Runtime/Modules/App/Definitions/AppDownloadRoute.cs)

相关文档：

- [AppManagerConfig.md](AppManagerConfig.md)
- [../AppManager/AppManager.md](../AppManager/AppManager.md)
- [../../Procedure/Procedures/ProcedureAppDownload.md](../../Procedure/Procedures/ProcedureAppDownload.md)
