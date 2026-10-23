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

### 1. Awake：缓存本地文本并接管渲染预处理器

`Awake()` 只缓存同节点的 `TextMeshProUGUI`，并接管 TMP 的 `ITextPreprocessor`；如果节点原本已有预处理器，会先执行原预处理器，再执行 RTL 处理，并在组件销毁时恢复原引用。

跨模块 Manager 在首次启动等待完成或后续启用时从 `FrameworkManagersGroup` 取得，避免静态场景文本的 `Awake/OnEnable` 早于框架 Manager 创建或初始化；不走 `Nova.*` 组件门面。

### 2. Start：完成首次启动后订阅；后续 OnEnable 立即恢复

首次 `OnEnable()` 不向尚待 `EventComponent.Start()` 初始化的事件池订阅。`Start()` 只发起一次 `UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, destroyToken)`，在下一帧末尾执行：

1. 获取当前 `IEventManager`、`ILocalizationManager`、`IAssetManager`
2. 用真实事件池的 `Check` 确认本实例尚无监听，再订阅 `LocalizationRefreshEventData`
3. `RefreshText()`
4. `RefreshFont()`

这一步避开 `EventManager.Initialize()` 的既有“关闭旧池并重建”语义；不改变公共事件初始化行为，不用每帧轮询维持订阅。静态文本与运行时首次创建的文本都在该一次等待后应用当前状态，等待期间切换的语言会由完成时主动刷新补齐。

等待期间禁用的实例只完成启动门，不订阅或加载字体；之后重新启用时立即完成上述步骤。销毁会取消等待。已有启动状态的实例在后续 `OnEnable()` 中直接恢复：若框架重启后事件管理器身份变化，先安全摘除旧实例仍存在的监听，再换成当前管理器；当前事件池缺少本实例监听时订阅，已有则保留，然后刷新文本和字体。

`OnDisable/OnDestroy` 只注销真实事件池仍持有的委托，池已重建或关闭时不凭旧布尔标记重复注销。框架仍运行时主动重新调用 `EventManager.Initialize()` 会清空所有订阅；本组件不承诺在持续启用期间自动侦测该重置，按现有生命周期重新启用后恢复。

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
- 首次刷新发生在下一帧末尾；不要把首次 `OnEnable()` 尚无监听或尚未刷新误判为绑定失败，也不要用切换组件开关掩盖正常启动问题。
- 独立启动文本通过 `RelinquishTextRendering()` 接管后，该实例不会因为等待完成或后续启用而恢复正式本地化监听。
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

## 字体请求与所有权

SetKeyName/SetFontMark 的相同值直接跳过；null/空 Key 只跳过文字，不清空已有 TMP.text，方向刷新仍先执行。空 Mark、AutoFontAdapt=false 或无匹配行会递增刷新版本并停止新加载，不自动复原旧字体或材质。Mark 取当前语言首个 Ordinal 精确匹配项。

字体通过 LoadAsync<Object> 加载，只在资源是 TMP_FontAsset 时应用。完成后检查 destroy 令牌、启用状态、刷新版本和当前语言；过期请求释放局部 handle，成功请求先释放旧字体 handle 再转存新 handle。资源非空但类型不匹配时不会应用，已转存 handle 仍由组件持有到后续替换/禁用。材质另行 LoadAsync<Material>，成功后替换 fontSharedMaterial；空 MaterialName 直接返回，不显式重置此前材质。

字号始终为首次缓存字号 × FontSizeScaleRatio，没有范围或有限数检查。OnDisable 先递增版本、注销，再释放字体和材质；不保证底层请求立即中止，也不清 TMP 上已有引用。资源 Release 或事件异常可中断后续清理，不能把普通销毁流程描述为每个异常下都完成。配置已导出、语言刷新事件已发或字体行已找到，都不等于目标 UI 的字体实际显示通过。

## RTL 内部处理边界

当前整形实现来源见 [TextShaping NOTICE](../../../../Scripts/Runtime/Modules/Localization/TextShaping/NOTICE.md)，类型仅供框架内部使用。预处理先执行原 ITextPreprocessor，原处理器返回 null 时退回原文本；空文本/非 RTL 直接返回。RTL 明确传 `farsi = (Language == Persian)`、fixTextTags=true、preserveNumbers=true；这覆盖 FixRTL 自身默认的 true/true/false。每个预处理器有一个容量初值 2048 的输出 builder，先 Clear，整形产生视觉顺序后再 Reverse，交给 TMP RTL 布局，TMP.text 保持原逻辑顺序。

FixRTL 使用共享静态 input/glyph builder；Tashkeel 位置与 Ligature 的 LTR/tag 缓冲也为共享静态列表，没有锁或重入保护，异常没有统一 finally 复位。此内部入口追加 output 而不先清，调用方必须清输出并串行使用。独立 ApplyIndependentText 使用明确 Language，将 null 文本转空串，永久 Relinquish 同节点 TextLocalizing，不初始化正式 Manager，也不镜像 UI 布局。

整形依次移除并记录已知 Tashkeel、统一 Arabic/Farsi Yeh、按邻接字母选择隔离/首/中/尾形、合 Lam-Alef（后位写 FFFF 占位）、恢复符号、合六种 Shadda 组合、重排 LTR/数字/标点、修复可识别的标签。Ligature 输出忽略 FFFF 和零宽非连接符；指定括号按邻近 RTL 镜像，英文/数字片段暂存后保持片段顺序。preserveNumbers=false 时才把 ASCII 数字转换为 Farsi/Hindu 数字，fixTextTags=true 时跳过简单识别的标签；数字映射字典可变。GlyphTable 以两套枚举的同名项建表，名称缺失会导致类型初始化失败，未映射字符原样返回。

这些规则不是完整 Unicode 双向算法或 TMP 标签解析器。Char32Utils 只对 `ch < 0xFFFF` 转 char 分类，负数也满足该比较，补充平面字符不作一般字母/符号分类；GlyphFixer 部分判断直接把 int 转 char。TextUtils 的英文仅 ASCII，RTL 按源码的 Arabic/Hebrew 区间判断；IsRTLInput 跳 `<...>` 后只看首个字母，未闭合标签会忽略余文。TextUtils.IsPunctuation 尚未实现而会抛异常，实际 Ligature 调用的是 Char32Utils.IsPunctuation。RichTextFixer 只扫描并反转各个识别的标签片段，不配对嵌套、不解析引号，起点/范围也不统一保护。

FastStringBuilder 内部保存 int 码点槽，Length 不等于 UTF-16 码元数；SetValue(string) 合代理对，非法 UTF-16 或非法 int 在转换时抛出。Append(char)/Set 不验证 Unicode 标量；Get/Set 只受数组容量限制，Length 外可能读到旧槽。Clear、Remove、RemoveAll、Length 收缩只改逻辑长度，不清容量；Length 增长赋值被忽略，负值未拒绝。Insert/Remove/Reverse/Substring 未统一验证范围，失败可留已修改长度/部分内容；Insert 拒绝源为自身，Substring 不拒绝输出自身。SetValue(builder) 实际复制源整个容量，目标仅按源逻辑长度扩容，容量不匹配可能越界；字符串 Replace 未统一验证剩余匹配范围/别名，匹配后跳步可能漏相邻项。扩容翻倍没有溢出/内存失败兜底，ToDebugString 的反斜杠十六进制串不是文本还原格式。这些是维护内部实现时的边界，不应将其当作通用可校验字符串 API。
