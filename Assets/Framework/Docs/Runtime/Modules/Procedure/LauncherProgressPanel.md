# LauncherProgressPanel

`LauncherProgressPanel` 是启动期进度面板组件。

它负责两类显示内容：

- 数值进度条与百分比
- 启动期静态本地化文本

## 什么时候先看这页

优先看这页的场景：

- 你要排查热更或预加载进度为什么没有更新到 UI。
- 你要确认启动期进度文本为什么不依赖事件驱动刷新。

## 核心语义

### 1. `SetProgress(progress)` 是唯一核心公开入口

调用时会：

- 对进度值做 `Mathf.Clamp01`
- 更新 `Slider.value`
- 更新百分比文本，格式是整数百分比，例如 `42%`

### 2. 本地化文本只在生命周期节点刷新

当前实现里：

- `Awake()` 刷一次
- `OnEnable()` 再刷一次

因为启动期语言不会在这段链路里动态切换，所以这里没有事件监听机制。

## 风险点 / 易错点

- 这里的本地化文本不是随运行时语言切换自动变化的。
- 百分比文本由组件内部自己生成，不来自 `LauncherLocalization`，但会按 `LauncherLocalization.Language` 应用 RTL/LTR 渲染方向。
- `ShowProgress(stage)` 里的 `stage` 不是直接传给这个组件做文本映射的键。

## 参数与生命周期边界

Awake/OnEnable 只刷新已有条目，不初始化或重新选择 LauncherLanguage；数组 null 安全。SetProgress 仅展示 Clamp01 后数值及四舍五入整数百分比，空控件跳过，不是资源成功验收；文本通过独立 renderer 写入，不应再让正式 TextLocalizing 覆盖。

## 继续阅读

关键源码：

- [LauncherProgressPanel.cs](../../../../Scripts/Runtime/Modules/Procedure/LauncherUI/LauncherProgressPanel.cs)

相关文档：

- [LauncherUIController.md](LauncherUIController.md)
- [LauncherLocalizedText.md](LauncherLocalizedText.md)
- [LauncherLocalization.md](LauncherLocalization.md)
