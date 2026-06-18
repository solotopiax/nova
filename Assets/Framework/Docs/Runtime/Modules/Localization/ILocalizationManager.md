# ILocalizationManager

`ILocalizationManager` 定义的是本地化模块的运行时契约。

调用方真正应该依赖的不是“接口里有哪些成员”，而是这些成员承诺了哪些稳定语义：

- 基础数据和当前语言文本是分阶段加载的
- 当前语言有统一解析链
- 文本既支持快捷字符串访问，也支持 Luban 类型化访问

## 契约定位

直接依赖它的通常是：

- `LocalizationComponent`
- 启动流程里的语言初始化逻辑
- 需要读取文本或切换语言的运行时代码

它描述的是“本地化系统对外保证什么”，不是“内部如何实现 Luban 或 Asset 加载”。

## 调用方可依赖的语义

### 1. 初始化与加载被拆成两步

- `Initialize(LocalizationManagerConfig config)`：只接收配置和依赖
- `LoadAsync()` / `LoadSync()`：准备支持语言列表和字体数据
- `InitCurrentLanguageSync()` / `InitCurrentLanguageAsync()`：解析当前语言并加载该语言文本

调用方不应该假设 `Initialize()` 或 `LoadAsync()` 之后 `GetText()` 就一定可用。

### 2. 当前语言有统一解析语义

- `ResolveLanguage()` 是唯一正式解析入口
- 它会综合编辑器强制语言、持久化偏好、系统语言和回退语言

如果这条链被改动，会直接影响启动流程、语言偏好和 UI 初始化结果。

### 3. 语言切换必须落到缓存重建

- `SetLanguageSync()` / `SetLanguageAsync()` 不是简单改一个枚举值
- 成功切换意味着当前语言文本缓存已经重建完成
- 失败切换不应留下“语言字段已变，但文本还是旧的”半成品状态

### 4. 文本查询有两层访问面

- `GetText(name)`：字符串快捷访问
- `GetTexts<T>() / GetTexts(string)`：Luban 表访问

调用方可以依赖：

- 两者都以“当前语言”作为上下文
- `GetText()` 面向高频 UI 字符串
- `GetTexts<T>()` 面向结构化文本表

### 5. 字体适配与文本系统分开

- `HasFontDatas()` / `GetFontDatas()` 只承诺字体适配数据查询
- 它们不代表当前语言文本一定已经初始化完成

## 最小 API 面

- 初始化：`Initialize(LocalizationManagerConfig config)`
- 基础加载：`LoadAsync()` / `LoadSync()`
- 当前语言初始化：`InitCurrentLanguageSync()` / `InitCurrentLanguageAsync()`
- 语言解析与切换：`ResolveLanguage()` / `SetLanguageSync()` / `SetLanguageAsync()`
- 文本查询：`GetText()` / `GetTexts<T>()`
- 字体查询：`GetFontDatas(Language language)`

## 变更影响面

如果这里的契约变化，会直接影响：

- [LocalizationComponent.md](LocalizationComponent.md)
- [LocalizationManager.md](LocalizationManager.md)
- 所有依赖 `Nova.Localization` 的启动流程和运行时代码

尤其高风险的是：

- `Load*` 与 `InitCurrentLanguage*` 的边界变化
- `ResolveLanguage()` 优先级变化
- `GetText()` 与 `GetTexts<T>()` 的数据一致性变化
- 语言切换是否仍保证刷新事件和持久化写入

## 相关实现

关键源码：

- [ILocalizationManager.cs](../../../../Scripts/Runtime/Modules/Localization/Managers/Interfaces/ILocalizationManager.cs)

相关文档：

- [LocalizationComponent.md](LocalizationComponent.md)
- [LocalizationManager.md](LocalizationManager.md)
- [LocalizationManagerConfig.md](LocalizationManagerConfig.md)
- [LocalizationRefreshEventData.md](LocalizationRefreshEventData.md)
- [ILocalizationTextRow.md](ILocalizationTextRow.md)
- [ILocalizationFontRow.md](ILocalizationFontRow.md)
