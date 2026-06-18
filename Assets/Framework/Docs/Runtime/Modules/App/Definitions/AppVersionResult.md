# AppVersionResult

`AppVersionResult` 是一次 App 大版本检查返回给启动链的结果枚举。

当前可用值有：

- `NoDownload`
- `RecommendedDownload`
- `ForcedDownload`

## 当前语义边界

- `NoDownload`：未命中任何大版本更新规则，继续后续热更检查和启动流程。
- `RecommendedDownload`：命中推荐更新规则；启动链仍会继续做资源补丁检查，但会在检查完成后进入 `ProcedureAppDownload` 弹出可跳过更新提示。
- `ForcedDownload`：命中强制更新规则；直接进入 `ProcedureAppDownload`，并跳过资源补丁检查。

## 风险点 / 易错点

- `RecommendedDownload` 现在是框架内建弹窗分支，不再只是状态信号。
- `NoDownload` 既可能是真正无更新，也可能来自检查失败、URL 为空或 JSON 非法的降级路径。

## 继续阅读

关键源码：

- [AppVersionResult.cs](../../../../../Scripts/Runtime/Modules/App/Definitions/AppVersionResult.cs)

相关文档：

- [../AppManager/AppManager.md](../AppManager/AppManager.md)
- [../../Procedure/ProcedureCheckVersion.md](../../Procedure/ProcedureCheckVersion.md)
- [../../Procedure/Procedures/ProcedureAppDownload.md](../../Procedure/Procedures/ProcedureAppDownload.md)
