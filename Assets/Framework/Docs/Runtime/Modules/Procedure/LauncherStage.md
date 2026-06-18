# LauncherStage

`LauncherStage` 是启动期阶段枚举。

当前它的主要作用是给启动流程保留语义标签参数，而不是直接驱动文本映射。

## 当前阶段

- `CheckVersion`
- `Hotfix`
- `Preload`

## 当前语义边界

- `LauncherUIController.ShowProgress(stage)` 会接收它
- 但当前源码中，`stage` 只是保留的语义参数
- Progress 文本本身由面板和 `LauncherLocalization` 决定，不再由这个枚举直接索引

## 风险点 / 易错点

- 旧理解里把它当成“进度文本映射键”已经不准确了。
- 新增阶段枚举值，不会自动带来对应 UI 文本，你仍然需要补面板侧和启动期本地化内容。

## 继续阅读

关键源码：

- [LauncherStage.cs](../../../../Scripts/Runtime/Modules/Procedure/LauncherUI/LauncherStage.cs)

相关文档：

- [LauncherUIController.md](LauncherUIController.md)
- [ProcedureCheckVersion.md](ProcedureCheckVersion.md)
- [ProcedureHotfix.md](ProcedureHotfix.md)
