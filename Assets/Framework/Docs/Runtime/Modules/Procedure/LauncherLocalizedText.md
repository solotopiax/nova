# LauncherLocalizedText

`LauncherLocalizedText` 是启动期轻量本地化条目。

它只做一件事：

- 把一个 `TMP_Text` 和一个启动期本地化 key 绑定起来

## 契约定位

它是 `LauncherProgressPanel` 这类启动面板里的最小文本绑定单元，不是完整面板，也不是通用本地化组件。

## 调用方可依赖的语义

### 1. `Text`

目标文本组件。

### 2. `LocalizationKey`

启动期本地化 key，对应 `LauncherLocalization` 读取到的 JSON 条目名。

### 3. `Refresh()`

调用时会：

- 如果 `Text` 或 `LocalizationKey` 为空，直接跳过
- 否则把 `LauncherLocalization.GetText(LocalizationKey)` 写回到 `TMP_Text`

也就是说，miss 时会回退成 key 本身，而不是空串。

## 风险点 / 易错点

- 它依赖的是 `LauncherLocalization`，不是正式的 `LocalizationManager`。
- 这是启动期静态刷新条目，不会像 `TextLocalizing` 那样监听语言切换事件。

## 继续阅读

关键源码：

- [LauncherLocalizedText.cs](../../../../Scripts/Runtime/Modules/Procedure/LauncherUI/LauncherLocalizedText.cs)

相关文档：

- [LauncherLocalization.md](LauncherLocalization.md)
- [LauncherProgressPanel.md](LauncherProgressPanel.md)
- [LauncherDialogLocalizedText.md](LauncherDialogLocalizedText.md)
