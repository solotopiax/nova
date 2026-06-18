# AppDownloadRule

`AppDownloadRule` 表示一次版本检查命中的规则类型。

当前定义了三个值：

- `None`
- `Recommended`
- `Forced`

## 当前实现事实

`AppManager.ParseVersionResult(...)` 现在只会写两种命中规则：

- `Recommended`
- `Forced`

其中：

- `Recommended`：本地版本低于 CDN 下发的推荐更新版本，且本地启用了推荐更新规则
- `Forced`：本地版本低于 CDN 下发的强制更新版本，且本地启用了强制更新规则

## 风险点 / 易错点

- `MatchedRule == None` 也可能出现在检查失败或 URL 为空的降级路径，不只是在真正“无更新”场景。
- `AppDownloadRule` 只表示命中的规则，不表示流程最终跳到了哪个 Procedure。

## 继续阅读

关键源码：

- [AppDownloadRule.cs](../../../../../Scripts/Runtime/Modules/App/Definitions/AppDownloadRule.cs)

相关文档：

- [../AppManager/AppManager.md](../AppManager/AppManager.md)
- [AppVersionResult.md](AppVersionResult.md)
