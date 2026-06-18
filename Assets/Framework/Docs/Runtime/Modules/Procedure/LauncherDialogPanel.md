# LauncherDialogPanel

`LauncherDialogPanel` 是启动期通用弹窗面板。

它负责三件事：

- 按 `LauncherDialogType` 激活对应文本
- 绑定确认 / 取消按钮回调
- 控制取消按钮显隐

## 什么时候先看这页

优先看这页的场景：

- 你要排查为什么启动期弹窗显示了错误文案。
- 你要确认取消按钮为什么有时隐藏。
- 你要看按钮回调会不会累积绑定。

## 核心语义

### 1. `Show(type, onConfirm, onCancel)` 是主入口

调用时会：

- 记录当前 `LauncherDialogType`
- 记录是否存在取消回调
- 刷新全部本地化文本条目
- 先 `RemoveAllListeners()`，再重新绑定按钮回调
- 根据 `onCancel != null` 控制取消按钮显隐
- 最后 `SetActive(true)`

### 2. 文本按类型激活，不是外部直接传文案

调用方不会把具体文案字符串传进来。

真正的文本来源是：

- `LauncherDialogLocalizedText[]`
- `LauncherLocalization`

### 3. `Hide()` 只是隐藏，不销毁

销毁动作仍由 `LauncherUIController.DestroyDialog()` 负责。

## 风险点 / 易错点

- 每次 `Show()` 都会清理旧监听，因此不会出现按钮回调累积。
- `onCancel == null` 时不是“空实现取消”，而是直接隐藏取消按钮。
- 文本行是否显示由 `LauncherDialogLocalizedText.ApplyByType()` 决定，不是按钮状态顺带控制出来的。

## 继续阅读

关键源码：

- [LauncherDialogPanel.cs](../../../../Scripts/Runtime/Modules/Procedure/LauncherUI/LauncherDialogPanel.cs)

相关文档：

- [LauncherDialogLocalizedText.md](LauncherDialogLocalizedText.md)
- [LauncherDialogType.md](LauncherDialogType.md)
- [LauncherUIController.md](LauncherUIController.md)
- [LauncherLocalization.md](LauncherLocalization.md)
