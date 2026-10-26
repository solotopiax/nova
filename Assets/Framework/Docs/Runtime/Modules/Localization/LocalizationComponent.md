# LocalizationComponent

`LocalizationComponent` 是本地化模块的场景入口，也是 `Nova.Localization` 对应的组件门面。

它本身不处理语言解析、文本装载或事件广播，职责只有两件事：

- 反射创建 `ILocalizationManager`
- 把 Inspector 配置整理成 `LocalizationManagerConfig` 下发给 Manager

真正的支持语言加载、语言切换、文本表构建和刷新事件，都在 `LocalizationManager`。

## 什么时候先看这页

优先看这页的场景：

- 你要确认 Inspector 上的语言偏好、回退语言、字体适配、数据源配置是怎么进入运行时的。
- 你要区分 `LoadAsync()` 和 `InitCurrentLanguageAsync()` 分别负责什么。
- 你要判断某个 API 是组件门面，还是 Manager 的真实实现。

如果你已经在排查语言解析链或文本切换逻辑，继续看 [LocalizationManager.md](LocalizationManager.md)。

## 依赖与边界

### 它依赖什么

- `ILocalizationManager`
- `LocalizationManagerConfig`
- `LocalizationSettings`
- `Util.TypeCreator`

### 它对外暴露什么

- 基础数据加载入口：`LoadAsync()` / `LoadSync()`
- 当前语言初始化入口：`InitCurrentLanguageSync()` / `InitCurrentLanguageAsync()`
- 语言查询与切换门面
- 文本、文本表、字体数据查询门面

### 它不负责什么

- 不负责真正解析支持语言列表
- 不负责当前语言文本数据加载
- 不负责持久化语言偏好
- 不负责广播 `LocalizationRefreshEventData`

## 核心流程

### Awake：只创建 Manager

`Awake()` 会执行：

1. `base.Awake()`
2. `Util.TypeCreator.Create<ILocalizationManager>(m_CurLocalizationManagerTypeName)`

类型名无效时会直接抛 `InvalidOperationException`。

### Start：只注入配置，不加载文本

`Start()` 会把这些配置打包进 `LocalizationManagerConfig`：

- `FallbackLanguage`
- `EditorLanguage`
- `RuntimeLanguagePrefer`
- `AutoFontAdapt`
- `TextUnitSettings`
- `FontUnitSettings`
- `SupportedLanguagesAssetLocation`
- 持久化键 `PersistClassifyName / PersistItemKey`

这里不会加载当前语言文本，也不会自动决定当前语言。

### Load 与 Init 是两步

- `LoadAsync()` / `LoadSync()`：加载支持语言列表和字体数据
- `InitCurrentLanguageSync()` / `InitCurrentLanguageAsync()`：解析当前语言并加载该语言的文本数据

这两个阶段不能混为一谈。只做 `LoadAsync()` 并不代表 `GetText()` 已可用。

### OnDestroy 只清空引用

`OnDestroy()` 这里只把 `m_LocalizationManager` 置空，不是本地化系统真正的 shutdown 入口。

## 高价值 API 面

- 基础加载：`LoadAsync()` / `LoadSync()`
- 当前语言初始化：`InitCurrentLanguageSync()` / `InitCurrentLanguageAsync()`
- 语言切换：`SetLanguageSync()` / `SetLanguageAsync()`
- 字符串查询：`GetText(string name)`
- 类型化查询：`GetTexts<T>()` / `GetTexts(string tbName)`
- 字体单行查询：`GetFontData(string mark)` / `GetFontData(Language language, string mark)`
- 字体列表查询：`GetFontDatas(Language language)`

字体表使用 `Language + Mark` 定位配置，没有通用 `Name` 字段。`GetFontData("Main")` 查询当前语言，显式语言重载只查指定语言；两者按 `Mark` 的 Ordinal 精确匹配返回首条原始配置，空标记或未命中返回 `null`，不回退语言、不加载字体资源。原列表查询继续保留。

## 风险点 / 易错点

- `LoadAsync()` 只准备“支持语言 + 字体”，不会顺带准备当前语言文本。
- `ResolveLanguage()` 能给出目标语言，但真正切换仍要走 `SetLanguage*()`。
- `GetText()` 依赖当前语言文本缓存；如果还没执行 `InitCurrentLanguage*()`，结果不成立。
- `CurLocalizationManagerTypeName` 填错时会在 `Awake()` 立刻失败，不会延后到运行期。
- 组件层只是门面，不要把语言解析策略写在调用方并试图绕开 Manager。

## 继续阅读

关键源码：

- [LocalizationComponent.cs](../../../../Scripts/Runtime/Modules/Localization/LocalizationComponent.cs)
- [LocalizationComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Localization/LocalizationComponent.Visitors.cs)

相关文档：

- [LocalizationManager.md](LocalizationManager.md)
- [ILocalizationManager.md](ILocalizationManager.md)
- [LocalizationManagerConfig.md](LocalizationManagerConfig.md)
- [LocalizationSettings.md](LocalizationSettings.md)
- [LocalizationRefreshEventData.md](LocalizationRefreshEventData.md)

## 启动输入与代理边界

Awake 在创建 Manager 前先向 LocalizationBootstrapLanguage 发布 EditorLanguage/RuntimeLanguagePrefer/FallbackLanguage 策略；OnDestroy 不撤销该静态策略。Start 将 DataFormat、支持语言地址、文本/字体单元和固定持久化键传入 Manager，列表共用引用；Settings/Manager 没有额外 null 守卫。LocalizationSettings 访问器返回原设置对象，运行时改动不会自动导出或重新加载。

Language 等访问器在 Manager 缺失时分别返回 Unspecified、索引 -1、名称空串，AutoFontAdapt 返回 false；这不同于直接读序列化字段。其余业务方法主要原样转发，无额外成功校验；LoadAsync true 与 InitCurrentLanguage 返回候选都不能证明当前文本已生效。
