# LauncherDialogType

`LauncherDialogType` 是启动期弹窗类型枚举。

当前可用值有：

- `ForcedDownload`
- `RecommendedDownload`
- `HotfixFailed`
- `PreloadFailed`

## 当前语义边界

- `LauncherUIController.ShowDialog(type, ...)` 会把它传给 `LauncherDialogPanel`
- `LauncherDialogPanel` 再按它激活对应的 `LauncherDialogLocalizedText`

## 风险点 / 易错点

- 新增一个枚举值，并不会自动带来对应的面板文本；还需要同步补 `LauncherDialogLocalizedText` 配置和启动期本地化内容。
- 当前数值顺序固定为：`ForcedDownload = 0`、`RecommendedDownload = 1`、`HotfixFailed = 2`、`PreloadFailed = 3`；改枚举时必须同步迁移 prefab 里的 `m_DialogType` 序列化值。

## 继续阅读

关键源码：

- [LauncherDialogType.cs](../../../../Scripts/Runtime/Modules/Procedure/LauncherUI/LauncherDialogType.cs)

相关文档：

- [LauncherDialogPanel.md](LauncherDialogPanel.md)
- [LauncherDialogLocalizedText.md](LauncherDialogLocalizedText.md)
- [LauncherUIController.md](LauncherUIController.md)
