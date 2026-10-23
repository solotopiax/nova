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
- 支持语言列表从哪个 Luban 数据资源读取，以及使用 JSON 还是 Binary

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

- `DataFormat` 默认 `Json`，统一控制文本、字体和支持语言表的运行时解码。
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

## 默认值与引用

源码默认 DataFormat=Json、EditorLanguage=Unspecified、RuntimeLanguagePrefer=true、FallbackLanguage=English、AutoFontAdapt=false，两个 List、支持语言地址与持久化键为 null。组件 Start 会用 Inspector 字段覆盖，不能据 DTO 初值断言场景配置。

属性赋值不验证枚举、地址或范围，不复制列表。Manager 保存 config 引用及初始化时取得的两个 List 引用；之后给 config 换一份 List 不等于替换 Manager 已缓存的 List，修改原 List 则可能影响后续读取。配置变动不自动导出、重载或切换语言。
