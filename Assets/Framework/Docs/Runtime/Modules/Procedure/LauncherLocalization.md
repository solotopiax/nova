# LauncherLocalization

`LauncherLocalization` 是启动期轻量本地化解析器。

它存在的原因很明确：

- 在正式 `LocalizationManager` 和资源系统完全就绪之前
- 仍然让 Splash / CheckVersion / Hotfix / AppDownload / LoadDll 这条链上的 UI 能安全显示本地化文本

## 什么时候先看这页

优先看这页的场景：

- 你要排查启动期文案为什么没有命中。
- 你要确认启动期为什么不依赖 `LocalizationManager` 也能显示文本。
- 你要看启动期语言是怎么决定的。

## 核心语义

### 1. 完全走 `Resources`，不依赖资源系统

当前实现只用：

- `Resources.Load<TextAsset>(path)`

因此它和：

- `LocalizationManager`
- `IAssetManager`
- `EventManager`

都是解耦的。

### 2. `Initialize()` 幂等

`Initialize(jsonPathTemplate)` 在第一次成功进入后会把 `s_IsInitialized` 置真。

后续重复调用会直接返回，不会重新切语言或重载 JSON。

### 3. 语言解析优先级

启动期语言解析顺序是：

- 持久化语言偏好
- 系统语言映射
- `English`

这里不依赖正式本地化模块的完整支持语言列表。

### 4. JSON 加载失败会回退到 English，再回退到空字典

流程是：

1. 先尝试当前解析语言
2. 失败则尝试 `English`
3. 再失败则使用空字典

此时 `GetText(key)` 会直接返回 key 本身。

## 调用方可依赖的语义

- `GetText(key)`：
  - key 为空时返回空串
  - 命中返回文本
  - miss 返回 key 本身
- `Language` 表示当前启动期解析器正在使用的语言

## 风险点 / 易错点

- 这套链路是“启动期专用”，不要把它和正式游戏内多语言系统混成同一条调试路径。
- `Initialize()` 幂等，不能靠重复调用来实现启动期手动切语言。
- 系统语言映射只覆盖一组常见语言；未覆盖语言会退回 `English`。

## 继续阅读

关键源码：

- [LauncherLocalization.cs](../../../../Scripts/Runtime/Modules/Procedure/LauncherUI/LauncherLocalization.cs)

相关文档：

- [LauncherUIController.md](LauncherUIController.md)
- [LauncherLocalizedText.md](LauncherLocalizedText.md)
- [LauncherDialogLocalizedText.md](LauncherDialogLocalizedText.md)
- [LauncherSettings.md](LauncherSettings.md)
