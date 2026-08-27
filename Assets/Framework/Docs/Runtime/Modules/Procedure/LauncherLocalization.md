# LauncherLocalization

`LauncherLocalization` 是启动期轻量本地化解析器。

它存在的原因很明确：

- 在正式 `LocalizationManager` 和资源系统完全就绪之前
- 仍然让 Splash / CheckVersion / Hotfix / AppDownload / LoadDll 这条链上的 UI 能安全显示本地化文本

## 什么时候先看这页

优先看这页的场景：

- 你要排查启动期文案为什么没有命中。
- 你要确认启动期为什么不等待 `LocalizationManager` 加载资源也能显示文本。
- 你要看启动期语言是怎么决定的。

## 核心语义

### 1. 文案走 `Resources`，语言决策与正式本地化共用

当前实现只用：

- `Resources.Load<TextAsset>(path)`

因此启动文案不依赖：

- `IAssetManager`
- `EventManager`

语言选择则复用 `LocalizationLanguageResolver`，并由 `LocalizationComponent.Awake()` 提前提供
`EditorLanguage`、`RuntimeLanguagePrefer` 和 `FallbackLanguage` 策略。

### 2. `Initialize()` 幂等

`Initialize(jsonPathTemplate)` 在第一次成功进入后会把 `s_IsInitialized` 置真。

后续重复调用会直接返回，不会重新切语言或重载 JSON。

### 3. 语言解析优先级

启动期语言解析遵循正式 Nova 策略：

- Editor 下可用的 `EditorLanguage`
- Runtime 且 `RuntimeLanguagePrefer == false` 时直接使用可用的 `FallbackLanguage`
- 启动期明文语言镜像
- 系统语言映射
- `FallbackLanguage`
- 可用的 `English` 启动文案

正式语言切换成功后会同时更新 AES 正式偏好和明文启动镜像。语言枚举不属于敏感信息，
镜像只用于解决 Splash 早于 Config/Persist 初始化、无法解密正式偏好的时序问题。

这里仍不依赖正式本地化模块的完整支持语言列表；对应精简 JSON 是否存在，就是 Launcher 阶段的可用性边界。

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

- 这套文案加载链仍是“启动期专用”，但语言决策必须与正式游戏内多语言使用同一套策略。
- `Initialize()` 幂等，不能靠重复调用来实现启动期手动切语言。
- 老用户首次升级时还没有启动镜像，当次启动会按系统语言与回退策略选择；正式语言初始化成功后会补写镜像，后续冷启动即可命中。
- 启动镜像对应的精简 JSON 不存在时，该语言会被视为启动期不可用并继续回退。

## 继续阅读

关键源码：

- [LauncherLocalization.cs](../../../../Scripts/Runtime/Modules/Procedure/LauncherUI/LauncherLocalization.cs)

相关文档：

- [LauncherUIController.md](LauncherUIController.md)
- [LauncherLocalizedText.md](LauncherLocalizedText.md)
- [LauncherDialogLocalizedText.md](LauncherDialogLocalizedText.md)
- [LauncherSettings.md](LauncherSettings.md)
