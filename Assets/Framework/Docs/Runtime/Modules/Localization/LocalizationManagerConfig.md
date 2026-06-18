# LocalizationManagerConfig

`LocalizationManagerConfig` 是本地化系统的初始化配置。

它把 `LocalizationComponent` Inspector 上的关键信息整理成 Manager 可直接消费的输入。

## 什么时候先看这页

优先看这页的场景：

- 你要确认本地化系统当前有哪些运行时输入。
- 你要排查语言解析链为什么走到了回退语言。
- 你要确认支持语言列表资源地址、持久化键和字体适配开关从哪里来。

## 配置语义

### 1. 语言解析策略

- `EditorLanguage`
- `RuntimeLanguagePrefer`
- `FallbackLanguage`

这三项共同决定 `ResolveLanguage()` 的行为边界。

### 2. 数据源配置

- `TextUnitSettings`
- `FontUnitSettings`
- `SupportedLanguagesAssetLocation`

它们决定：

- 当前项目从哪些文本单元和字体单元构建数据
- 支持语言列表从哪个 JSON 资源读取

### 3. 运行时附加行为

- `AutoFontAdapt`
- `PersistClassifyName`
- `PersistItemKey`

它们分别影响：

- 是否启用字体适配
- 语言偏好写入 PlayerPrefs 时使用的存储位置

## 调用方可依赖的边界

- 这是 Manager 初始化输入，不是 Inspector 直接序列化对象。
- `LocalizationManager.Initialize(...)` 只缓存这些值；真正加载语言列表和文本，要在后续 `Load*()` / `InitCurrentLanguage*()` 中完成。

## 风险点 / 易错点

- `SupportedLanguagesAssetLocation` 为空时，支持语言列表加载会被跳过。
- `RuntimeLanguagePrefer == false` 时，运行时不会优先系统语言，这是策略事实，不是 fallback。
- `PersistClassifyName / PersistItemKey` 改动会直接影响旧语言偏好的兼容读取。

## 继续阅读

关键源码：

- [LocalizationManagerConfig.cs](../../../../Scripts/Runtime/Modules/Localization/Managers/Definitions/LocalizationManagerConfig.cs)

相关文档：

- [LocalizationComponent.md](LocalizationComponent.md)
- [LocalizationSettings.md](LocalizationSettings.md)
- [LocalizationManager.md](LocalizationManager.md)
