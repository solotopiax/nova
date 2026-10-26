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

- 按 `DataFormat` 加载 `SupportedLanguagesAssetLocation` 指向的 Luban 支持语言表
- 加载字体表并建立 `Language -> FontRows` 映射
- 解析当前语言
- 切换语言并重建文本表 / 扁平文本缓存
- 保存语言偏好并广播 `LocalizationRefreshEventData`

### 它不负责什么

- 不负责场景组件生命周期
- 不负责 UI 文本组件的具体刷新
- 不负责导出本地化数据

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
- 支持语言、字体和文本使用同一个 `LubanDataFormat`
- JSON 走 `LubanDataReceiver`，Binary 走 `LubanBinaryDataReceiver` 与生成的 `*TablesBinding`
- 支持语言由独立 `LocalizationSupportedLanguagesTables` 构建，要求行实现 `ILocalizationSupportedLanguageRow`；当前 Runtime 不提供旧裸 JSON 数组回退
- 字体数据通过 Luban 表加载进 `m_FontDatas`

`LoadAsync()` 和 `LoadSync()` 每次都会重新读取基础数据。支持语言先收集本次全部有效语言，再替换当前名单；加载未完成或失败时保留旧名单，重复或重叠加载不会累加名单。字体表在本次构建成功后整体替换当前字体映射，重复加载不会累加同一批字体行；新快照不再包含的语言或标记会移除。加载失败、缓存为空或 Tables 构建失败时保留原字体映射，已取得的只读字体列表也不会被原地追加或清空。基础加载完成后，仍需单独调用当前语言初始化入口。

也就是说，支持语言来自独立导出的 `SupportedLanguages` 列表，不从文本单元的 Excel Sheet 缓存字段推断。

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

系统语言映射先采用 Unity 的 `Application.systemLanguage`。当 Unity 返回 `Unknown` 时，
共用解析器会尝试当前 UI Culture 的语言标签（为空时尝试当前 Culture），识别
Albanian、Croatian、Filipino、Georgian、Hindi、Macedonian、Malay、Malayalam、
Persian、PortugueseBrazil、SerbianCyrillic 和 SerbianLatin。Unity 返回宽泛的
`Portuguese` 或 `SerboCroatian` 时，也会用标签区分巴西葡语、克罗地亚语和塞尔维亚语文字变体；
明确的其他 Unity 语言值仍以 Unity 为准。地区变体未进入支持语言列表时，先尝试原有宽泛
Unity 映射，再按配置回退。标签由运行平台提供，若没有可用标签，则保留原有映射及回退结果。

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
5. `SaveLanguageToPersist(language)`，并同步写入供下次冷启动读取的非敏感语言镜像
6. `FireRefreshEvent(oldLanguage, language)`

其中刷新事件走的是 `m_EventManager.FireNow(...)`，是同步立即广播。

### 6. 异步切换有重入保护

`SetLanguageAsync()` 使用 `m_LanguageSwitchVersion` 做版本保护：

- 每次切换先自增版本号
- 每个请求把数据加载到自己的临时缓存
- await 返回后如果版本已变化，旧请求直接终止，不提交临时缓存
- 通过版本检查的请求才进入缓存提交；提交本身没有失败回滚

该保护只适用于通过前置校验的异步请求。同步切换不递增版本；请求当前语言也会提前返回，不会撤销另一个在飞目标，见下文。

## 高价值 API 面

- 基础加载：`LoadAsync()` / `LoadSync()`
- 解析与初始化：`ResolveLanguage()` / `InitCurrentLanguageSync()` / `InitCurrentLanguageAsync()`
- 切换语言：`SetLanguageSync()` / `SetLanguageAsync()`
- 快速文本：`HasText()` / `GetText()`
- 类型化文本：`HasTexts<T>()` / `GetTexts<T>()`
- 字体：`GetFontData(mark)` / `GetFontData(language, mark)` / `HasFontDatas()` / `GetFontDatas()`

`GetFontData(mark)` 以当前 `Language` 查询；显式语言重载不切换当前语言。字体表实际使用 `Language + Mark`，按该语言列表的原始顺序取首个 Ordinal 精确匹配项。空标记、语言没有数据或标记未命中均返回 `null`，不回退到默认语言或其他标记，不加载字体/材质，也不改写 `AssetLocation`、`MaterialName` 或 `FontSizeScaleRatio`。`TextLocalizing.RefreshFont()` 复用此查询后沿原链加载资源。

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

## 返回值、快照与失败中间态

Initialize 拒绝 null config，保存同一对象与两个单元 List 引用，null List 才新建空列表；不会清旧字典或递增切换版本，依赖不存在也不在这里报错。LoadAsync 忽略支持语言加载的 bool，字体 false 只告警，正常到末尾始终返回 true；LoadSync 没有成功返回值。两者均不加载当前语言文本，也不能证明表或字体资源完整。

支持语言表的 Name 以忽略大小写的 Enum.TryParse 解析，跳过 Unspecified，保留行顺序及重复值，没有 Enum.IsDefined 校验；数字字符串可进入未知枚举值。GetSupportedLanguages 暴露同一个 List 的只读接口，下一次成功加载会改变该视图。字体表先建立本次私有映射，完整构建后替换；GetFontDatas 返回该轮行列表引用，旧列表不会因后续换映射被追加或清空。有字体行只证明配置存在，不保证地址资源可加载、类型为 TMP_FontAsset 或字形齐全。

文本单元地址以 `Txt.Format(unit.AssetLocation, language.ToString())` 得到。同步读取不汇总逐单元 bool；异步 WhenAll 中某个 false 只告警，只要缓存有数据仍可能提交。因此部分表成功不代表所有翻译完备。JSON 缺表提供空 JArray 并告警，Binary 缺 byte[] 表直接抛异常；两者不能视作相同的失败结果。

CommitLanguageTextCache 先清主缓存并复制本轮数据，再清/构表、清/扁平化字典；构表或行属性异常可能留下空表、部分新文本而 Language 仍是旧值，不提供事务回滚。扁平化跳 null 行和空 Name，Value=null 转空串，跨表重复 Name 后写覆盖；DataList=null 未统一保护。GetText(null/空串) 返回空串，缺键告警并返回键本身；HasText 只查键存在。类型化表按 typeof(T) 精确查找，字符串重载按 Type.Name 取首项，同短名跨命名空间不保证唯一，查询不自动加载。

SetLanguageAsync 无 ct 参数；只有通过非 Unspecified、已支持、不同当前语言校验后才递增版本。同步切换、Initialize、Shutdown 都不递增该版本，不能据此承诺最后一次任意调用获胜。InitCurrentLanguageSync/Async 返回 Resolve 的候选，即使切换被拒或文本为空，也可能正常返回该候选；验收应核当前 Language 与目标 GetText。

ApplyLanguageSwitchResult 先写 Language，再保存正式偏好/启动镜像，最后 FireNow。正式 Save 或事件链异常不回滚语言与文本；缺 EventManager 则告警且不广播。事件只能在同步回调内消费，不能持有其池对象。Shutdown 清缓存与依赖，不负责释放 TextLocalizing 已持有的字体/材质资源。

数据加载委托取得 TextAsset 后先 Release handle，再把同一对象交给 Receiver；ReleaseAsset 是空回调，不是延后释放入口。LoadTextAssetSync/Async 也在读 `.text` 前 Release，异步辅助函数的 finally 只保护 handle 归还；这些路径没有自动持有 TextAsset 到解析结束的保证，资源寿命须按实际加载/回收场景验证。

## 启动镜像与正式语言偏好

正式偏好按 config 的 PersistClassifyName/PersistItemKey 通过 IPlayerPrefsManager 读写，Enum.TryParse 忽略大小写但不校验 IsDefined。保存执行 SetString→Save(分类)，之后才写 `PlatformPlayerPrefs` 的非敏感镜像 `Nova.Localization.BootstrapLanguage.v1`。正式依赖/config 缺失会整体返回，镜像也不更新；正式 Save 异常传播，镜像自身异常只告警。

LocalizationBootstrapLanguage 在 SubsystemRegistration 恢复 `(Unspecified, true, English)` 默认策略，不删除镜像。组件 Awake Configure 发布策略，多个组件以后写覆盖；正式 Manager 和独立启动文本共用决策器，但各自提供该阶段可用语言集合。镜像读取额外拒绝未定义枚举与 Unspecified，不能拿启动镜像读取规则替代正式偏好规则。

Resolve 最终可能返回未被支持的配置 fallback 或 English；解析成功不等于加载成功。isSupported 为 null 时所有候选不被支持，delegate 自身异常传播。可选 systemLocale 只有 null 才读取 Culture，显式空串用于跳过地区细分；Culture 是当前线程 UI Culture（空时普通 Culture），不是直接调用设备地区 API。标签只覆盖源码列出的语言与 pt-BR/sr-Latn 变体，不能当完整 BCP 47 映射；Unity Chinese 映射 ChineseSimplified。
