# LocalizationManager

`LocalizationManager` 是本地化模块的真实运行核心。

它负责三件事：

- 加载支持语言列表与字体数据
- 按环境和持久化状态解析当前语言
- 切换语言后重建文本缓存并广播刷新事件

`LocalizationComponent` 只是门面；语言解析链、文本表构建、持久化和事件广播都在这里。

## 什么时候先看这页

优先看这页的场景：

- 你要排查为什么当前语言不是预期值。
- 你要确认支持语言列表现在从哪里读取。
- 你要看 `GetText()` 和 `GetTexts<T>()` 的数据是不是同一份来源。
- 你要排查异步切语言时为什么旧请求会被覆盖。

## 依赖与边界

### 它依赖什么

- `IAssetManager`
- `IPlayerPrefsManager`
- `IEventManager`
- `IConfigManager`
- Luban 表加载链路

### 它对外负责什么

- 加载 `SupportedLanguagesAssetLocation` 指向的语言列表 JSON
- 加载字体表并建立 `Language -> FontRows` 映射
- 解析当前语言
- 切换语言并重建文本表 / 扁平文本缓存
- 保存语言偏好并广播 `LocalizationRefreshEventData`

### 它不负责什么

- 不负责场景组件生命周期
- 不负责 UI 文本组件的具体刷新
- 不负责导出本地化 JSON

## 核心流程

### 1. Initialize：接管配置与跨模块依赖

`Initialize(config)` 会：

- 缓存 `LocalizationManagerConfig`
- 缓存文本单元与字体单元设置
- 从 `FrameworkManagersGroup` 获取 `IAssetManager`、`IPlayerPrefsManager`、`IEventManager`
- 把当前语言重置为 `Language.Unspecified`

它不会自动装载文本。

### 2. LoadAsync / LoadSync：只加载语言列表和字体

当前实现里：

- 支持语言列表来自 `m_Config.SupportedLanguagesAssetLocation`
- 语言列表通过 JSON 数组解析成 `m_SupportedLanguages`
- 字体数据通过 Luban 表加载进 `m_FontDatas`

也就是说，支持语言已经不再从 `TextUnitSettings.DataTypeNames` 推断。

### 3. ResolveLanguage：按环境走两条主链

当前解析规则是：

- Editor 且 `EditorLanguage` 有效并被支持：直接使用编辑器强制语言
- Editor 其他情况：走 `ResolveByPersistThenSystem()`
- Runtime 且 `RuntimeLanguagePrefer == false`：优先回退语言，否则支持列表第一项
- Runtime 且 `RuntimeLanguagePrefer == true`：走 `ResolveByPersistThenSystem()`

`ResolveByPersistThenSystem()` 的优先级是：

- 持久化语言
- 系统语言
- 回退语言
- 支持列表第一项
- 最终回退到 `FallbackLanguage` 或 `English`

### 4. InitCurrentLanguage：解析后立即切换

- `InitCurrentLanguageSync()`：`ResolveLanguage()` 后调用 `SetLanguageSync()`
- `InitCurrentLanguageAsync()`：`ResolveLanguage()` 后调用 `SetLanguageAsync()`

所以“确定当前语言”和“真正把文本切进去”是同一层动作。

### 5. SetLanguageSync / Async：重建文本缓存并广播事件

切换前会先做三项校验：

- 目标语言不能是 `Unspecified`
- 必须出现在支持列表里
- 不能与当前语言相同

切换成功后会：

1. 重新加载该语言的文本单元
2. 用临时缓存替换主缓存
3. 重新构建 Luban 文本表
4. 重新扁平化到 `m_LanguageTexts`
5. `SaveLanguageToPersist(language)`
6. `FireRefreshEvent(oldLanguage, language)`

其中刷新事件走的是 `m_EventManager.FireNow(...)`，是同步立即广播。

### 6. 异步切换有重入保护

`SetLanguageAsync()` 使用 `m_LanguageSwitchVersion` 做版本保护：

- 每次切换先自增版本号
- await 返回后如果版本已变化，旧请求直接终止

这保证了快速连续切语言时，只有最后一次请求生效。

## 高价值 API 面

- 基础加载：`LoadAsync()` / `LoadSync()`
- 解析与初始化：`ResolveLanguage()` / `InitCurrentLanguageSync()` / `InitCurrentLanguageAsync()`
- 切换语言：`SetLanguageSync()` / `SetLanguageAsync()`
- 快速文本：`HasText()` / `GetText()`
- 类型化文本：`HasTexts<T>()` / `GetTexts<T>()`
- 字体：`HasFontDatas()` / `GetFontDatas()`

## 关键状态

- `m_SupportedLanguages`：支持语言白名单
- `m_Language`：当前语言
- `m_TextTableDatas`：当前语言的 Luban 文本表
- `m_LanguageTexts`：兼容 `GetText()` 的扁平字典
- `m_FontDatas`：字体适配数据
- `m_LanguageSwitchVersion`：异步切换版本号

## 风险点 / 易错点

- 如果没先加载支持语言列表，`ResolveLanguage()` 的结果会退化，后续切换也可能被拒绝。
- `GetText()` 和 `GetTexts<T>()` 来自同一轮语言文本加载，但访问层不同；前者查扁平字典，后者查 Luban 表实例。
- `FireRefreshEvent()` 是同步立即广播；监听方如果做重逻辑，会把切语言路径一并拖慢。
- `m_EventManager` 缺失时语言切换仍能成功，但不会发刷新事件。
- `RuntimeLanguagePrefer == false` 时不会优先系统语言，这是很容易被误判的地方。

## 继续阅读

关键源码：

- [LocalizationManager.cs](../../../../Scripts/Runtime/Modules/Localization/Managers/Implements/LocalizationManager.cs)
- [LocalizationManager.Methods.cs](../../../../Scripts/Runtime/Modules/Localization/Managers/Implements/LocalizationManager.Methods.cs)
- [LocalizationManager.Visitors.cs](../../../../Scripts/Runtime/Modules/Localization/Managers/Implements/LocalizationManager.Visitors.cs)

相关文档：

- [LocalizationComponent.md](LocalizationComponent.md)
- [ILocalizationManager.md](ILocalizationManager.md)
- [LocalizationManagerConfig.md](LocalizationManagerConfig.md)
- [LocalizationRefreshEventData.md](LocalizationRefreshEventData.md)
- [EventManager.md](../Event/EventManager.md)
