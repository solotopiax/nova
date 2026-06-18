# LauncherDialogLocalizedText

`LauncherDialogLocalizedText` 是弹窗专用的启动期本地化条目。

它在 `LauncherLocalizedText` 的基础上多加了一层：

- 这条文本属于哪个 `LauncherDialogType`

## 契约定位

它服务于 `LauncherDialogPanel`，用于按弹窗类型激活或隐藏对应文本行。

## 调用方可依赖的语义

### 1. `DialogType`

定义这条文本属于哪个弹窗类型。

`LauncherDialogPanel.Show(type, ...)` 时，会用它来筛选应该显示哪些文本行。

### 2. `Text` / `LocalizationKey`

语义与 `LauncherLocalizedText` 一致：

- `Text` 是目标 `TMP_Text`
- `LocalizationKey` 是启动期轻量本地化 key

### 3. `ApplyByType(activeType, hasCancelAction)`

调用时会：

- 如果当前条目的 `DialogType == activeType`
  - 激活文本对象
  - 刷新文本
- 否则隐藏文本对象

当前实现里 `hasCancelAction` 还是预留参数，并未实际参与分支逻辑。

## 风险点 / 易错点

- 文本显隐由 `ApplyByType()` 控制，取消按钮显隐则由 `LauncherDialogPanel` 自己控制，两者不是同一层逻辑。
- 如果同一个 `DialogType` 配了多条文本，它们会一起激活，不会自动互斥。

## 继续阅读

关键源码：

- [LauncherDialogLocalizedText.cs](../../../../Scripts/Runtime/Modules/Procedure/LauncherUI/LauncherDialogLocalizedText.cs)

相关文档：

- [LauncherDialogPanel.md](LauncherDialogPanel.md)
- [LauncherDialogType.md](LauncherDialogType.md)
- [LauncherLocalization.md](LauncherLocalization.md)
