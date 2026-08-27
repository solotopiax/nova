# TextLocalizing

`TextLocalizing` 是挂在 `TextMeshProUGUI` 上的自动本地化组件。

它解决的是一个很具体的问题：

- 在语言切换时自动刷新文本
- 在启用字体适配时自动切换 TMP 字体与材质
- 为 Arabic / Persian / Hebrew 自动启用 TMP RTL，并在渲染前完成阿拉伯字母连写与中英文数字混排整理

它不参与语言解析，也不负责本地化数据加载。

## 什么时候先看这页

优先看这页的场景：

- 你要排查某个 TMP 文本为什么没有跟着语言切换刷新。
- 你要确认字体标记 `FontMark` 是怎么生效的。
- 你要看组件禁用后为什么字体句柄会被释放。

## 依赖与边界

### 它依赖什么

- `TextMeshProUGUI`
- `IEventManager`
- `ILocalizationManager`
- `IAssetManager`

### 它对外负责什么

- 监听 `LocalizationRefreshEventData`
- 把 `KeyName` 对应的文本写入 TMP
- 按 `FontMark` 找字体配置并异步加载字体 / 材质
- 保持 `GetText()` 与 `TMP.text` 为正常 Unicode 逻辑顺序，只在 TMP 渲染预处理阶段转换 RTL 字形

### 它不负责什么

- 不负责决定当前语言
- 不负责加载本地化表
- 不负责导出或校验本地化源数据
- 不负责按钮顺序、返回箭头、Alignment、RectTransform 或页面布局镜像

## 核心流程

### 1. Awake：缓存依赖

`Awake()` 会缓存：

- `TextMeshProUGUI`
- `IEventManager`
- `ILocalizationManager`
- `IAssetManager`

同时会接管 TMP 的 `ITextPreprocessor`；如果节点原本已有预处理器，会先执行原预处理器，再执行 RTL 处理，并在组件销毁时恢复原引用。

它直接从 `FrameworkManagersGroup` 取 Manager，不走 `Nova.*` 门面。

### 2. OnEnable：订阅刷新事件并立刻应用当前状态

启用时会：

1. 订阅 `LocalizationRefreshEventData`
2. `RefreshText()`
3. `RefreshFont()`

所以这个组件不是“等下一次切语言才第一次生效”，而是启用时就会先刷新一遍。

### 3. 文本刷新走 `GetText()`

`RefreshText()` 的逻辑很直接：

- `KeyName` 为空则跳过
- 从 `m_LocalizationManager.GetText(m_LocalizingKeyName)` 取值
- 写回 `m_TextMeshProUGUI.text`

当当前语言为 Arabic、Persian 或 Hebrew 时，组件还会开启 `isRightToLeftText`。Arabic / Persian 会在渲染前转换为正确连接字形；英文和数字片段保持可读顺序。翻译表和业务代码都不应手工倒序文案。

### 4. 字体刷新依赖 `AutoFontAdapt + FontMark`

`RefreshFont()` 只有在这些条件同时满足时才会继续：

- `m_LocalizationManager` 可用
- `AutoFontAdapt == true`
- `FontMark` 非空
- 当前语言存在字体数据
- 当前语言字体数据里能找到匹配的 `Mark`

找到后才会进入 `LoadAndApplyFont(...)`。

### 5. Disable 时会取消订阅并释放句柄

`OnDisable()` 不只是取消事件订阅，还会：

- `m_LoadedFontHandle?.Release()`
- `m_LoadedMaterialHandle?.Release()`

所以它对资源生命周期是有清理动作的，不只是单纯摘掉回调。

## 高价值 API 面

- `SetKeyName(string keyName)`
- `SetFontMark(string fontMark)`
- 只读属性：`KeyName` / `FontMark`

## 风险点 / 易错点

- 这个组件只支持 `TextMeshProUGUI`，不支持旧版 `UnityEngine.UI.Text`。
- `FontMark` 配了但 `AutoFontAdapt` 没开，字体刷新不会生效。
- 字体和材质加载都是异步的，组件销毁时通过 `GetCancellationTokenOnDestroy()` 取消。
- `OnDisable()` 会释放已加载的字体 / 材质句柄；频繁启停对象会带来重复加载成本。
- 当前字体匹配是对 `Mark` 做 `StringComparison.Ordinal` 精确匹配，不做模糊兼容。
- Arabic 字体必须覆盖 Arabic Presentation Forms；框架沿用现有 `Language + FontMark` 配置加载，不内置业务字体。
- 异步字体请求带刷新版本与语言校验，快速切换语言时旧请求不会覆盖新字体。
- 当前支持的是展示文本，不包含 `TMP_InputField` 的光标、选区和输入法适配。

## 继续阅读

关键源码：

- [TextLocalizing.cs](../../../../Scripts/Runtime/Modules/Localization/TextLocalizing/TextLocalizing.cs)
- [TextLocalizing.Visitors.cs](../../../../Scripts/Runtime/Modules/Localization/TextLocalizing/TextLocalizing.Visitors.cs)
- [TextLocalizing.Methods.cs](../../../../Scripts/Runtime/Modules/Localization/TextLocalizing/TextLocalizing.Methods.cs)

相关文档：

- [LocalizationManager.md](LocalizationManager.md)
- [LocalizationRefreshEventData.md](LocalizationRefreshEventData.md)
- [ILocalizationFontRow.md](ILocalizationFontRow.md)
- [EventManager.md](../Event/EventManager.md)
- [TextLocalizingInspector.md](../../../Editor/Inspectors/CustomInspectors/TextLocalizingInspector.md)
- [TextLocalizingAutoMount.md](../../../Editor/Inspectors/CustomInspectors/TextLocalizingAutoMount.md)
- [TextLocalizingValidator.md](../../../Editor/Inspectors/CustomInspectors/TextLocalizingValidator.md)
