# MainDemo 33 Demo 设计稿

> 设计期产出物。锚点：PAT-65（8 维覆盖）+ PAT-71（三段式模板）+ PAT-72（18 步 SOP）+ ADR-033（sample 自闭包）。
> 范围：MainDemo（framework sample）唯一。33 叶子覆盖 Core 9 / Modules 16 / HybridCLR 3 / Integration 5。

## 历史决策检索

- 命中 PAT-65（active）：8 维矩阵 + ≤12 叶子 + 重载折叠 4 类代表
- 命中 PAT-71（draft / Inbox）：三段式（TitleBar 64px / InteractionArea flex / FeedbackArea 200px）+ Nova 门面 + API 副标题 + 反馈日志带 API 前缀 + `PauseCoveredUIView=false`
- 命中 PAT-72（draft / Inbox）：18 步 SOP + 图片命名规范 + 串并行切分 + Demo_ 前缀
- 命中 ADR-033（accepted）：MainDemo 仅演示 framework 包对外接口，不跨 package 聚合，全部走 `Nova.UI.OpenUIViewAsync<T>()`
- **本方案与历史决策关系：兼容 + 补强**——本设计稿是 PAT-65/71/72 第 1-3 步落地产物，无推翻。

---

## § 1 BaseDemoView 抽象基类草签

> `BaseDemoView : UIView`，partial 三件套（cs / .Visitors.cs / .Methods.cs），位于 `Scripts/Runtime/UIs/BaseDemoView/`。

### Visitors（字段表）

| 修饰符 | 类型 | 名称 | 默认 | 说明 |
|---|---|---|---|---|
| `[SerializeField]` | `TextMeshProUGUI` | `m_TitleText` | — | 顶部居中标题 |
| `[SerializeField]` | `TextMeshProUGUI` | `m_ApiHintText` | — | 顶部 API 副标题（左对齐） |
| `[SerializeField]` | `Button` | `m_CloseButton` | — | 右上角 X 按钮 |
| `[SerializeField]` | `RectTransform` | `m_InteractionRoot` | — | 中部交互区根（ScrollRect content） |
| `[SerializeField]` | `RectTransform` | `m_FeedbackContent` | — | 底部反馈区 content（ScrollRect content） |
| `[SerializeField]` | `TextMeshProUGUI` | `m_FeedbackLineTemplate` | — | 反馈单行模板（disable 状态） |
| `[SerializeField]` | `Button` | `m_ClearFeedbackButton` | — | 反馈区清空按钮 |
| `[SerializeField]` | `ScrollRect` | `m_FeedbackScrollRect` | — | 反馈区滚动容器（自动滚至最新） |
| 私有 | `Queue<TextMeshProUGUI>` | `m_FeedbackPool` | new | 反馈行复用池（避免重复 Instantiate） |
| 私有 | `const int` | `c_MaxFeedbackLines` | 200 | 反馈区最大保留行数（FIFO 滚动剔除） |
| 公有属性 | `RectTransform` | `InteractionRoot` | — | 子类挂交互元素的根节点（getter 暴露 `m_InteractionRoot`） |

### 公开方法签名

```csharp
// cs（公有 + override）
protected override void OnInit(object userData);
protected override void OnOpen(object userData);
protected override void OnClose(bool isShutdown, object userData);

// Methods.cs（protected）
protected void SetTitle(string text);
protected void SetApiHint(string apiSig);
protected void AppendFeedback(string line, FeedbackLevel level = FeedbackLevel.Info);
protected void ClearFeedback();
private void OnCloseButtonClick();           // 调 Nova.UI.CloseUIView(this)
private void OnClearFeedbackButtonClick();   // 调 ClearFeedback()
```

### `FeedbackLevel` 枚举（位于 `BaseDemoView.Definitions.cs`）

```csharp
public enum FeedbackLevel { Info, Success, Warn, Error }
```

颜色映射：Info=#CCCCCC / Success=#4CAF50 / Warn=#FFB300 / Error=#E53935。

### 强制约束

- Awake/Start 由 UIView 基类托管，子类**禁覆盖** Unity 生命周期，重写 `OnInit/OnOpen/OnClose`
- 反馈行 `Instantiate(m_FeedbackLineTemplate, m_FeedbackContent)` 走 ObjectPool 池化
- `AppendFeedback` 强制行首加 `> ` 前缀，最大 200 行 FIFO

---

## § 2 8 维覆盖矩阵汇总

> 横向：33 demo（编号 1.1 - 4.5）；纵向：PAT-65 8 维。每格："C"=代表性折叠 / "F"=完整覆盖 / "S"=跳过。

| Demo | 生命周期 | 重载族 | 异步 | 配置驱动 | 事件回调 | 错误边界 | 跨模块 | Editor工具 |
|---|---|---|---|---|---|---|---|---|
| 1.1 FrameworkComponent | F | S | S | S | S | S | S | S |
| 1.2 FrameworkManager | F | S | S | C | S | S | S | S |
| 1.3 Fsm | F | C | S | S | C | C | S | S |
| 1.4 Reference | F | C | S | S | S | C | S | S |
| 1.5 Log | F | C | S | C | S | S | S | S |
| 1.6 Util | S | C | S | S | S | S | S | S |
| 1.7 Collections | F | C | S | S | S | S | S | S |
| 1.8 Extensions | S | C | S | S | S | S | S | S |
| 1.9 Edge Cases | S | S | S | S | S | F | S | S |
| 2.1 App | C | C | F | C | S | C | S | S |
| 2.2 Asset | C | C | F | C | S | C | S | S |
| 2.3 Prefab | C | C | F | S | S | C | C | S |
| 2.4 Config | C | S | F | F | S | S | S | S |
| 2.5 Event | F | C | S | S | F | C | S | S |
| 2.6 Table | C | C | F | F | S | C | S | S |
| 2.7 Localization | C | C | F | F | C | S | S | S |
| 2.8 UI | F | C | F | F | F | C | S | S |
| 2.9 Network | C | C | F | C | F | C | S | S |
| 2.10 Procedure | C | S | S | C | C | S | S | S |
| 2.11 ObjectPool | F | C | S | C | S | C | C | S |
| 2.12 Persist | C | C | F | C | S | C | S | S |
| 2.13 Sound | C | C | F | C | S | C | C | S |
| 2.14 Vibrate | C | C | F | C | S | C | S | S |
| 2.15 SDK | C | C | F | F | S | S | S | S |
| 2.16 Debug | C | S | S | C | F | S | S | S |
| 3.1 AOT metadata | S | S | S | F | S | S | C | S |
| 3.2 业务 dll | S | S | S | F | S | S | C | S |
| 3.3 业务 Procedure 注册时序 | S | S | S | S | F | S | F | S |
| 4.1 UI+Localization | C | S | F | C | F | S | F | S |
| 4.2 UI+Asset | C | C | F | S | S | C | F | S |
| 4.3 Procedure+Asset | S | S | S | F | S | S | F | S |
| 4.4 Event+Network | C | S | F | S | F | S | F | S |
| 4.5 Config+HybridCLR Namespace | S | S | S | F | S | S | F | S |

**审查结论：**
- Editor 工具维度全 S：MainDemo 不演示 Inspector / EditorWindow（PAT-65 §8 默认仅演示业务可见部分，Editor 工具走 Inspector 自动展示）
- 重载族折叠达标：UI 22 重载 → 4 个代表；ObjectPool/Asset 同理
- 边界覆盖集中在 1.9 Edge Cases 与各模块 C 格，不分散

---

## § 3 33 叶子细化设计

> 命名：类名 = `Demo<Module><Topic>View`；prefab 同名；UI 注册表 Name 同名。变体：**R**=只读快照型 / **I**=交互触发型。

### 1. Core 核心库（9 个，全 R）

#### 1.1 DemoFrameworkComponentView ｜ R
- API 副标题：`FrameworkComponent.Awake() / Start() / OnDestroy()`
- 主题：FrameworkComponent 三段生命周期可视化
- InteractionArea：3 张信息卡片（Awake/Start/OnDestroy）+ 1 张运行时子组件清单（Nova.Self.GetComponentsInChildren）
- 反馈样例：`> Nova.Self → 18 children active`
- 资源：无

#### 1.2 DemoFrameworkManagerView ｜ R
- API 副标题：`Util.TypeCreator.Create<I{Xxx}Manager>() / Manager.Priority`
- 主题：三层继承链 + Priority 排序
- InteractionArea：所有已注册 Manager 列表（TypeName / Priority / 当前实现类）
- 反馈样例：`> AssetManager Priority=12 → IAssetManager`
- 资源：无

#### 1.3 DemoFsmView ｜ R
- API 副标题：`Fsm<T>.Start<TState>() / ChangeState / AddStates`
- 主题：FSM 三态切换可视化
- InteractionArea：3 个状态节点图（A/B/C）+ 当前态高亮 + 「切换到下一态」按钮
- 反馈样例：`> Fsm<Demo>.ChangeState<StateB>() → Current=StateB`
- 资源：无（纯 UI 矩形）

#### 1.4 DemoReferenceView ｜ R
- API 副标题：`ReferencePool.Acquire<T>() / Release(reference)`
- 主题：引用池 Get/Put 计数
- InteractionArea：`Acquire×1` `Release×1` `Acquire×100` 3 按钮 + 实时显示 Using/Free
- 反馈样例：`> ReferencePool.Acquire<DemoData>() → Using=1 Free=0`
- 资源：无

#### 1.5 DemoLogView ｜ R
- API 副标题：`Log.Debug / Log.Warning / Log.Error`
- 主题：日志级别与 LogTag
- InteractionArea：3 按钮（Debug/Warning/Error）+ LogTag 下拉
- 反馈样例：`> Log.Warning(LogTag.UI, "demo") → Console`
- 资源：无

#### 1.6 DemoUtilView ｜ R
- API 副标题：`Util.Json.ToJson(obj) / Util.Encrypt.AESEncrypt(text, key)`
- 主题：Util 高频静态工具集合
- InteractionArea：3 子卡片（Json 互转 / AES 加密 / MD5 计算），每卡片 1 输入框 + 1 按钮
- 反馈样例：`> Util.MD5.HashString("hello") → 5d41402abc4b...`
- 资源：无

#### 1.7 DemoCollectionsView ｜ R
- API 副标题：`new NovaLinkedSet<int>() / .Add(x) / .Contains(x)`
- 主题：NovaLinkedSet 有序去重
- InteractionArea：1 输入框（int）+ Add/Remove/Clear 3 按钮 + 实时打印当前序列
- 反馈样例：`> NovaLinkedSet.Add(42) → Count=3 [1,7,42]`
- 资源：无

#### 1.8 DemoExtensionsView ｜ R
- API 副标题：`"hello".ToTitleCase() / list.IsNullOrEmpty()`
- 主题：扩展方法 5 个代表
- InteractionArea：5 行卡片，每行 1 输入 + 1 按钮（ToTitleCase / IsNullOrEmpty / FirstOrNull / Clamp / Repeat）
- 反馈样例：`> list.IsNullOrEmpty() → True`
- 资源：无

#### 1.9 DemoEdgeCasesView ｜ R
- API 副标题：`ReferencePool.Release(null) / Fsm.ChangeState before Start`
- 主题：核心层错误边界 4 个代表
- InteractionArea：4 按钮分别触发：Release(null) / 重复 Subscribe 同 handler / FSM 切到不存在的态 / Reference Acquire 后未 Release 检测
- 反馈样例：`> ReferencePool.Release(null) → throws ArgumentNullException`
- 资源：无

### 2. Modules 模块库（16 个）

#### 2.1 DemoAppView ｜ I
- API 副标题：`Nova.App.CheckAsync() / DownloadAsync() / OpenStoreAsync()`
- 主题：App 大版本检查链路
- InteractionArea：3 按钮（Check / Download / OpenStore）+ 当前版本号卡片（来自 Application.version）+ 路由下拉（Store / Apk）
- 反馈样例：`> Nova.App.CheckAsync() → Result=NoDownload`、`> Nova.App.OpenStoreAsync() → opened=true`
- 资源：无

#### 2.2 DemoAssetView ｜ I
- API 副标题：`Nova.Asset.LoadAsync<Sprite>(location, ct)`
- 主题：Asset 同步/异步/取消三态
- InteractionArea：1 location 输入（默认 `sprite_icon_tree`）+ Sync/Async/Cancel 3 按钮 + 加载结果展示 Image
- 反馈样例：`> Nova.Asset.LoadAsync<Sprite>("sprite_icon_tree") -> loaded 256x256`、`> Nova.Asset.Release(loc) -> ok`
- 依赖资源：sprite `sprite_icon_tree`

#### 2.3 DemoPrefabView ｜ I
- API 副标题：`Nova.Prefab.InstantiateAsync(location) / Destroy(go)`
- 主题：Prefab 异步实例化 + 单路释放
- InteractionArea：「实例化」「销毁」2 按钮 + 实例化容器 RectTransform + 当前实例计数卡片
- 反馈样例：`> Nova.Prefab.InstantiateAsync("DemoPrefabBlock") -> 1 instance`
- 依赖资源：prefab `DemoPrefabBlock`（UI 绿色方块 Image + DemoPrefabBlockSpinner 自旋脚本，120×120，DemoPrefabView 私有依赖）

#### 2.4 DemoConfigView ｜ R
- API 副标题：`Nova.Config.ConfigManager.Common / .Namespace / GetSDKPluginConfig<T>()`
- 主题：Config 运行态快照展示
- InteractionArea：4 卡片（DevelopMode / Platform / Channel / Namespace）+ Common 字段表（AppID/AppAesKey）+ EnabledSDKs 列表
- 反馈样例：`> Nova.Config.ConfigManager.Namespace → "Game.Runtime"`
- 资源：无

#### 2.5 DemoEventView ｜ I
- API 副标题：`Nova.Event.Subscribe<T>(h) / Fire(sender, e) / Unsubscribe<T>(h)`
- 主题：Event 订阅/发布/取消
- InteractionArea：2 按钮（Subscribe / Unsubscribe）+ 1 按钮（Fire 一次 DemoPingEventData）+ 当前订阅数卡片
- 反馈样例：`> Nova.Event.Fire(this, DemoPingEventData) → handlers=1`
- 资源：无（DemoPingEventData 见数据清单）

#### 2.6 DemoTableView ｜ R
- API 副标题：`Nova.Table.GetTable<TbDemoItem>() / HasTable<T>()`
- 主题：Luban 表格读取
- InteractionArea：2 按钮（HasTable / GetTable）+ 表格预览 GridLayout（10 行 × 4 列）
- 反馈样例：`> Nova.Table.GetTable<TbDemoItem>() → 10 rows`
- 数据：xlsx `Demo_Item`（10 行：Id/Name/Icon/Price）

#### 2.7 DemoLocalizationView ｜ I
- API 副标题：`Nova.Localization.SetLanguageAsync(lang) / GetText(name)`
- 主题：本地化切语言 + 取词
- InteractionArea：Language 下拉（取自 GetSupportedLanguages）+ 切换按钮 + 5 个 demo TextLocalizing 标签（Demo_Greet / Demo_Confirm / ...）
- 反馈样例：`> Nova.Localization.SetLanguageAsync(ChineseSimplified) → done`
- 数据：xlsx `Demo_Localization`（5 个 key × 3 语言）

#### 2.8 DemoUIView ｜ I
- API 副标题：`Nova.UI.OpenUIViewAsync<T>(userData) / CloseUIView(serialID)`
- 主题：UI 嵌套打开/关闭 + UIGroup
- InteractionArea：「OpenUIViewAsync<DemoToastView>」「OpenUIViewAsync<DemoDialogView>」「Close all」3 按钮 + 当前打开列表卡片
- 反馈样例：`> Nova.UI.OpenUIViewAsync<DemoToastView>() → SerialID=2003`
- 依赖：本设计稿额外 2 个辅助 view `DemoToastView` `DemoDialogView`（轻量 prefab，仅含一行文字 + 自闭定时器）

#### 2.9 DemoNetworkView ｜ I
- API 副标题：`Nova.Network.GetAsync(url) / PostAsync(url, body) / ConnectServer(...)`
- 主题：HTTP GET/POST + WebSocket 连接占位
- InteractionArea：1 url 输入（默认 `https://httpbin.org/get`）+ Get/Post/Connect 3 按钮 + 响应预览卡片
- 反馈样例：`> Nova.Network.GetAsync("...") → 200 OK len=312`
- 资源：无（在线请求）

#### 2.10 DemoProcedureView ｜ R
- API 副标题：`Nova.Procedure.GetProcedure<T>() / HasProcedure<T>()`
- 主题：Procedure 已注册流程列表 + 当前态
- InteractionArea：当前 Procedure 名卡片 + 已注册流程列表（TypeName / IsCurrent）
- 反馈样例：`> Nova.Procedure.GetProcedure<ProcedureLoadDll>() → exists=true`
- 资源：无

#### 2.11 DemoObjectPoolView ｜ I
- API 副标题：`Nova.ObjectPool.CreateSingleGettingObjectPool<T>() / GetObjectPool<T>()`
- 主题：对象池创建 + Spawn/Recycle 计数
- InteractionArea：「Create」「Spawn×1」「Recycle×1」「Destroy」4 按钮 + 当前 Using/Free/Capacity 卡片
- 反馈样例：`> Nova.ObjectPool.CreateSingleGettingObjectPool<DemoBullet>() → ok`
- 依赖：DemoBullet : ObjectBase 占位类

#### 2.12 DemoPersistView ｜ I
- API 副标题：`Nova.Persist.PlayerPrefs.SetInt(k, v) / GetInt(k)`
- 主题：PlayerPrefsManager 读写
- InteractionArea：key 输入 + value 输入 + Set/Get/Has/Delete 4 按钮 + 当前所有键值列表
- 反馈样例：`> Nova.Persist.PlayerPrefs.SetInt("demo_score", 42) → ok`
- 资源：无

#### 2.13 DemoSoundView ｜ I
- API 副标题：`Nova.Sound.PlaySound(group, location, params) / StopSound(serialID)`
- 主题：声音播放/停止/暂停
- InteractionArea：声音组下拉（BGM/SFX）+ 资源 location 下拉 + Play/Stop/Pause/Resume 4 按钮 + 当前播放列表
- 反馈样例：`> Nova.Sound.PlaySound("BGM", "bgm_main") -> SerialID=1003`
- 数据：xlsx `Demo_Sound`（3 行：bgm_main / sfx_click / sfx_confirm）
- 资源：sound `bgm_main.ogg` / `sfx_click.wav` / `sfx_confirm.wav`

#### 2.14 DemoVibrateView ｜ I
- API 副标题：`Nova.Vibrate.Play(VibrateType.Light) / PlayCustom(...) / StopAll()`
- 主题：振动 类型/自定义/紧急 三态
- InteractionArea：VibrateType 下拉 + Play/PlayCustom/PlayEmphasis/StopAll 4 按钮 + IsSupported 卡片
- 反馈样例：`> Nova.Vibrate.Play(Light) → ok / supported=true`
- 数据：xlsx `Demo_VibrateCustom`（2 行）+ `Demo_VibrateEmphasis`（2 行）

#### 2.15 DemoSDKView ｜ R
- API 副标题：`Nova.SDK.GetAll<ISDKPlugin>() / TryGet<TPlugin>(out p)`
- 主题：SDK 已注册插件清单
- InteractionArea：插件列表（Name / Priority / IsAvailable / Type）+ 「Login(demoUserId)」按钮（broadcast 演示）
- 反馈样例：`> Nova.SDK.GetAll<ISDKPlugin>() → 0 plugins (MainDemo 未启用)`
- 资源：无

#### 2.16 DemoDebugView ｜ I
- API 副标题：`Nova.Debug.IsActiveWindow / Active / Deactive`（具体 API 与 DebugComponent 对齐）
- 主题：Debug 窗口开关 + FPS 实时
- InteractionArea：「Toggle Debug Window」按钮 + 当前 Mode 卡片 + 实时 FPS / RAM 卡片
- 反馈样例：`> Nova.Debug.Activate() → Window=Mini`
- 资源：无

### 3. HybridCLR 运行时热更新（3 个，全 R）

#### 3.1 DemoHybridClrAotMetadataView ｜ R
- API 副标题：`Util.HybridCLR.LoadAotMetadataAsync(dlls)`（启动期已调，本 demo 仅展示快照）
- 主题：AOT metadata 已加载列表
- InteractionArea：DLL 名列表（来自 `Nova.Config.ConfigManager.AotMetadataDlls`）+ HomologousImageMode 卡片（SuperSet）
- 反馈样例：`> AotMetadataDlls → 5 entries (mscorlib.dll, System.dll, ...)`
- 资源：无

#### 3.2 DemoHybridClrGameDllView ｜ R
- API 副标题：`Util.HybridCLR.LoadGameAssemblyAsync(dlls)`
- 主题：业务 dll 已加载列表
- InteractionArea：dll 名列表（来自 ConfigManager.GameDlls）+ 命名空间卡片
- 反馈样例：`> GameDlls → Game.Runtime.dll loaded`（MainDemo 场景下可能为空，显式标注）
- 资源：无

#### 3.3 DemoHybridClrProcedureRegisterView ｜ R
- API 副标题：`ProcedureLoadDll → RegisterAdditionalProcedures(...)`
- 主题：业务 Procedure 注册时序快照
- InteractionArea：时序条（Manifest → Config → AOT → DLL → Register → Jump）+ 每步 Elapsed 卡片（来自 ProcedureRunInfo）
- 反馈样例：`> ProcedureLoadDll Elapsed=1.234s → handed off`
- 资源：无

### 4. Integration 跨模块联动（5 个）

#### 4.1 DemoIntegrationUiLocalizationView ｜ I
- API 副标题：`Nova.Localization.SetLanguageAsync + Nova.UI.OpenUIViewAsync<T>`
- 主题：切语言 → UI 内文本自动刷新
- InteractionArea：Language 下拉 + 切换按钮 + 「Open Sub View」按钮（打开 DemoToastView 演示文本同步）
- 反馈样例：`> Set Lang → English / Toast text refreshed`
- 数据：复用 2.7 的 `Demo_Localization`

#### 4.2 DemoIntegrationUiAssetView ｜ I
- API 副标题：`Nova.Asset.LoadAsync<GameObject>(loc) → Nova.UI.OpenUIViewAsync<T>(go)`
- 主题：Asset 异步取 prefab 后 UI 注入
- InteractionArea：location 输入 + 「LoadAndShow」按钮 + 容器卡片
- 反馈样例：`> Nova.Asset.LoadAsync<GameObject>("...") → instantiated; UI handle=2007`
- 依赖资源：prefab `DemoSubPanel`（共用 2.3 资产或新建轻量版）

#### 4.3 DemoIntegrationProcedureAssetView ｜ R
- API 副标题：`ProcedureCheckVersion → ProcedureHotfix → ProcedureLoadDll`
- 主题：热更链路只读快照
- InteractionArea：链路图（5 个节点）+ 每节点状态卡片 + EnableHotfix 总开关卡片（来自 AssetManagerConfig）
- 反馈样例：`> EnableHotfix=false → Hotfix procedures skipped`
- 资源：无

#### 4.4 DemoIntegrationEventNetworkView ｜ I
- API 副标题：`Nova.Network.OnWebSocketReceiveMessage += h → Nova.Event.Fire(this, e)`
- 主题：网络消息桥接到事件总线
- InteractionArea：「Connect Mock」按钮 + 「Send Mock Message」按钮 + 「Subscribe Bridged Event」按钮 + 收到消息卡片
- 反馈样例：`> WS receive → Event.Fire(NetworkBridgedEventData) → handlers=1`
- 资源：无（mock 用本地 echo）

#### 4.5 DemoIntegrationConfigHybridClrView ｜ R
- API 副标题：`Nova.Config.ConfigManager.Namespace → Util.Assembly.GetAssembly(ns)`
- 主题：Namespace 注入 → 程序集解析
- InteractionArea：Namespace 卡片 + 解析得到的 Assembly 卡片 + 已注册 ProcedureBase 子类计数
- 反馈样例：`> Namespace="Game.Runtime" → Assembly resolved / 0 Procedure subclasses (MainDemo)`
- 资源：无

---

## § 4 资源需求汇总清单（去重后）

> 来源 `Solar/Game/Textures/PicsForAtlas/ExampleBookAtlas/`（51 张）+ `Solar/Game/Sounds/`。
> 仅引用规范化后的 final 名，按 PAT-72 第 7 步 7c/d 落盘到 `Assets/Samples/MainDemo/Sprites/DemoAtlas/` + `Assets/Samples/MainDemo/Sounds/`。

### 图片（11 张，最小集）

| Solar 原名（候选） | Nova 规范名 | 用途 | 引用 demo |
|---|---|---|---|
| `Button_Blue` | `button_blue.png` | 通用蓝色按钮底图（默认按钮） | 全部 I 型 demo |
| `Button_Blue_Pressed` | `button_blue_pressed.png` | 按钮按下态 | 同上 |
| `Button_Close` | `button_close.png` | 顶部 X | BaseDemoView |
| `Panel_Bg_Light` | `panel_bg_light.png` | 三段式整体底图 | BaseDemoView |
| `Panel_Title_Bar` | `panel_title_bar.png` | 顶部 64px 底图 | BaseDemoView |
| `Panel_Feedback_Bg` | `panel_feedback_bg.png` | 底部 200px 底图 | BaseDemoView |
| `Field_Input_Frame` | `field_input_frame.png` | 输入框底图 | I 型 demo |
| `Mark_Check` | `mark_check.png` | 勾选标记 | DemoLocalization / DemoSDK |
| `Icon_Coin` | `icon_coin.png` | 演示用图标 | DemoAsset / DemoTable |
| `Icon_Arrow_Red` | `icon_arrow_red.png` | 树形展开箭头（已有） | DemoNavTree |
| `Decorate_01` | `decorate_01.png` | 一般装饰 | DemoFsm（节点圆点） |

> 业务素材（changmao / quanzhang / mystical_title4 / Soldier / UISprite）一律不引入，符合"按需 + 二次规范化"。

### 声音（3 个）

| Solar 原 | Nova 规范名 | 用途 | demo |
|---|---|---|---|
| `Solar/Game/Sounds/Musics/Bgm_xxx.ogg` | `bgm_main.ogg` | BGM 演示 | DemoSound |
| `Solar/Game/Sounds/UISounds/Click_xxx.wav` | `sfx_click.wav` | 点击 SFX | DemoSound |
| `Solar/Game/Sounds/UISounds/Confirm_xxx.wav` | `sfx_confirm.wav` | 确认 SFX | DemoSound |

> 具体取哪个 Bgm / Click / Confirm 由资源期 7b 收尾时挑短小（≤ 3s 的 SFX，≤ 30s 的 BGM）的版本。**待补**。

### Spine（0 个）

MainDemo 33 demo 全部不需要 Spine。`Solar/Game/Spines/Role/` 暂不引入。

### Prefab（4 个，新建）

| Prefab | 路径 | 用途 |
|---|---|---|
| `BaseDemoView.prefab` | `Prefabs/UIs/BaseDemoView/` | 三段式骨架基板 |
| `DemoPrefabBlock.prefab` | `Prefabs/UIs/DemoPrefabView/` | DemoPrefab 实例化目标（UI 绿块 + 自旋，私有依赖） |
| `DemoToastView.prefab` | `Prefabs/UIs/DemoToastView/` | DemoUI 子页面 + Localization 演示 |
| `DemoDialogView.prefab` | `Prefabs/UIs/DemoDialogView/` | DemoUI 子页面（带按钮） |

> 33 个 DemoXxxView.prefab 由 BaseDemoView clone 派生，UnityMCP 串行落地。

---

## § 5 模块源 xlsx 数据追加清单

> 全部 `Demo_` 前缀，避免污染既有数据。位置：`Docs/Designs/Excels/<Module>/...xlsx`。

### Table 模块（`Excels/Tables/...xlsx`）
- 追加 sheet `Demo_Item`：列 Id / Name / Icon / Price，10 行。值如 `1001 / demo_sword / icon_coin / 100`。

### Localization 模块（`Excels/Localizations/...xlsx`）
- 追加 sheet `Demo_Localization`：列 Name / ChineseSimplified / English / Japanese，5 行：
  - `Demo_Greet / 你好 / Hello / こんにちは`
  - `Demo_Confirm / 确定 / Confirm / 確認`
  - `Demo_Cancel / 取消 / Cancel / キャンセル`
  - `Demo_Toast_Tip / 演示提示 / Demo Tip / デモのヒント`
  - `Demo_Switch_Lang / 切换语言 / Switch Language / 言語切替`

### Sound 模块（`Excels/Sounds/...xlsx`）
- 追加 sheet `Demo_Sound`：列 Name / GroupName / AssetLocation / Volume / Loop，3 行：
  - `Demo_BgmMain / BGM / bgm_main / 0.6 / true`
  - `Demo_SfxClick / SFX / sfx_click / 1.0 / false`
  - `Demo_SfxConfirm / SFX / sfx_confirm / 1.0 / false`

### Vibrate 模块（`Excels/Vibrates/...xlsx`）
- 追加 sheet `Demo_VibrateCustom`：列 Name / Intensity / Sharpness / PreDuration / Duration，2 行（Demo_Light / Demo_Heavy）。
- 追加 sheet `Demo_VibrateEmphasis`：列 Name / Amplitude / Frequency / PreDuration / Interval，2 行（Demo_Tap / Demo_Knock）。

### UI 模块（`Excels/UIs/UIs.xlsx`）
- 追加 33 行 Demo*View 注册（详见 § 6）。

### Network / Config / Procedure 模块
- **不追加**（demo 全是只读快照或在线请求 mock，零数据依赖）。

---

## § 6 UI 注册表追加清单（33 行追加 `Excels/UIs/UIs.xlsx`）

> 列：Name / Desc / AssetLocation / UIGroupName / PauseCoveredUIView。`PauseCoveredUIView=false` 全部统一（PAT-71 强制）。`UIGroupName=Demo`（新增分组，深度 50）。

| Name（=类名 +.View） | Desc | AssetLocation | UIGroup | Pause |
|---|---|---|---|---|
| DemoFrameworkComponentView | Core 1.1 FrameworkComponent | UIs/DemoFrameworkComponentView | Demo | false |
| DemoFrameworkManagerView | Core 1.2 FrameworkManager | UIs/DemoFrameworkManagerView | Demo | false |
| DemoFsmView | Core 1.3 Fsm | UIs/DemoFsmView | Demo | false |
| DemoReferenceView | Core 1.4 Reference | UIs/DemoReferenceView | Demo | false |
| DemoLogView | Core 1.5 Log | UIs/DemoLogView | Demo | false |
| DemoUtilView | Core 1.6 Util | UIs/DemoUtilView | Demo | false |
| DemoCollectionsView | Core 1.7 Collections | UIs/DemoCollectionsView | Demo | false |
| DemoExtensionsView | Core 1.8 Extensions | UIs/DemoExtensionsView | Demo | false |
| DemoEdgeCasesView | Core 1.9 Edge Cases | UIs/DemoEdgeCasesView | Demo | false |
| DemoAppView | Modules 2.1 App | UIs/DemoAppView | Demo | false |
| DemoAssetView | Modules 2.2 Asset | UIs/DemoAssetView | Demo | false |
| DemoPrefabView | Modules 2.3 Prefab | UIs/DemoPrefabView | Demo | false |
| DemoConfigView | Modules 2.4 Config | UIs/DemoConfigView | Demo | false |
| DemoEventView | Modules 2.5 Event | UIs/DemoEventView | Demo | false |
| DemoTableView | Modules 2.6 Table | UIs/DemoTableView | Demo | false |
| DemoLocalizationView | Modules 2.7 Localization | UIs/DemoLocalizationView | Demo | false |
| DemoUIView | Modules 2.8 UI | UIs/DemoUIView | Demo | false |
| DemoNetworkView | Modules 2.9 Network | UIs/DemoNetworkView | Demo | false |
| DemoProcedureView | Modules 2.10 Procedure | UIs/DemoProcedureView | Demo | false |
| DemoObjectPoolView | Modules 2.11 ObjectPool | UIs/DemoObjectPoolView | Demo | false |
| DemoPersistView | Modules 2.12 Persist | UIs/DemoPersistView | Demo | false |
| DemoSoundView | Modules 2.13 Sound | UIs/DemoSoundView | Demo | false |
| DemoVibrateView | Modules 2.14 Vibrate | UIs/DemoVibrateView | Demo | false |
| DemoSDKView | Modules 2.15 SDK | UIs/DemoSDKView | Demo | false |
| DemoDebugView | Modules 2.16 Debug | UIs/DemoDebugView | Demo | false |
| DemoHybridClrAotMetadataView | HybridCLR 3.1 AOT metadata | UIs/DemoHybridClrAotMetadataView | Demo | false |
| DemoHybridClrGameDllView | HybridCLR 3.2 业务 dll | UIs/DemoHybridClrGameDllView | Demo | false |
| DemoHybridClrProcedureRegisterView | HybridCLR 3.3 Procedure 注册时序 | UIs/DemoHybridClrProcedureRegisterView | Demo | false |
| DemoIntegrationUiLocalizationView | Integration 4.1 UI+Localization | UIs/DemoIntegrationUiLocalizationView | Demo | false |
| DemoIntegrationUiAssetView | Integration 4.2 UI+Asset | UIs/DemoIntegrationUiAssetView | Demo | false |
| DemoIntegrationProcedureAssetView | Integration 4.3 Procedure+Asset | UIs/DemoIntegrationProcedureAssetView | Demo | false |
| DemoIntegrationEventNetworkView | Integration 4.4 Event+Network | UIs/DemoIntegrationEventNetworkView | Demo | false |
| DemoIntegrationConfigHybridClrView | Integration 4.5 Config+HybridCLR | UIs/DemoIntegrationConfigHybridClrView | Demo | false |

> 辅助 view（DemoToastView / DemoDialogView）由 2.8 / 4.1 / 4.2 共享，独立追加 2 行（Pause=false / UIGroup=DemoSub）。

---

## § 7 4 路并发切分定稿

> 命名 A/B/C/D = 4 路 runtime-coder + E = doc-writer。前置阻塞：D 路先单独跑 BaseDemoView，A/B/C 等 BaseDemoView 落盘后并发起步。

### D 路（基板，先行 1 阶段）
- **负责**：BaseDemoView 三件套 + DemoToastView/DemoDialogView 辅助 view
- **文件**：
  - `Scripts/Runtime/UIs/BaseDemoView/BaseDemoView.cs`
  - `Scripts/Runtime/UIs/BaseDemoView/BaseDemoView.Visitors.cs`
  - `Scripts/Runtime/UIs/BaseDemoView/BaseDemoView.Methods.cs`
  - `Scripts/Runtime/UIs/BaseDemoView/BaseDemoView.Definitions.cs`
  - `Scripts/Runtime/UIs/DemoToastView/DemoToastView.cs`
  - `Scripts/Runtime/UIs/DemoDialogView/DemoDialogView.cs`
- **阻塞**：无（最先起跑）
- **完成信号**：BaseDemoView 编译通过 + UI 注册表 2 行 Toast/Dialog 入表 → 通知 A/B/C 起步

### A 路（Core 9 + Edge Cases）
- **负责**：1.1-1.9 全部 9 个
- **类**：DemoFrameworkComponentView / DemoFrameworkManagerView / DemoFsmView / DemoReferenceView / DemoLogView / DemoUtilView / DemoCollectionsView / DemoExtensionsView / DemoEdgeCasesView
- **文件**：每类 1 cs（≤ 150 行单文件即可，阈值见规则 §八），共 9 cs
- **阻塞**：等 D 路 BaseDemoView 落盘

### B 路（Modules 前 8）
- **负责**：2.1-2.8（App / Asset / Prefab / Config / Event / Table / Localization / UI）
- **类**：DemoAppView / DemoAssetView / DemoPrefabView / DemoConfigView / DemoEventView / DemoTableView / DemoLocalizationView / DemoUIView
- **文件**：每类 1-2 cs（DemoAssetView / DemoUIView 体量大，可能拆 Methods.cs）
- **阻塞**：等 D 路 + Table/Localization xlsx 数据追加完成

### C 路（Modules 后 8 + HybridCLR + Integration）
- **负责**：2.9-2.16 + 3.1-3.3 + 4.1-4.5（共 16 个）
- **类**：DemoNetworkView / DemoProcedureView / DemoObjectPoolView / DemoPersistView / DemoSoundView / DemoVibrateView / DemoSDKView / DemoDebugView / DemoHybridClrAotMetadataView / DemoHybridClrGameDllView / DemoHybridClrProcedureRegisterView / DemoIntegrationUiLocalizationView / DemoIntegrationUiAssetView / DemoIntegrationProcedureAssetView / DemoIntegrationEventNetworkView / DemoIntegrationConfigHybridClrView
- **文件**：每类 1 cs（多为只读快照，体量 ≤ 80 行）
- **阻塞**：等 D 路 + Sound/Vibrate xlsx 数据追加完成

### E 路（doc-writer，全程并行）
- **负责**：本设计稿落地后同步更新 L2 md（每模块新增 demo 章节）+ INDEX 索引
- **文件**：各模块 L2 md 末尾追加「Sample 演示位置」段，禁止动现有正文
- **阻塞**：无（与代码并行）

> 文件级零冲突：A/B/C/D 各自的目录互不交叉；UI 注册表 xlsx 由 editor-coder 单线追加（PAT-72 步 8）。

---

## § 8 不确定项 / 待补清单

1. **Solar 声音具体取哪 3 个**（bgm_main / sfx_click / sfx_confirm）：需在资源期 7b 听过后挑选；同时确认采样率/格式与 Nova SoundManager 兼容性。**待用户拍板或 editor-coder 现场决定。**
2. **DemoDebug API 副标题准确签名**：DebugComponent 公开 API 在本次设计未深探，副标题暂写 `Nova.Debug.Activate / Deactivate`，落地前由 runtime-coder 核实真实方法名。
3. **DemoSDK 在 MainDemo 场景下默认无插件**：MainDemo 自带的 Nova.prefab 是否启用任何 SDKPlugin？若全空则 demo 信息卡片为空清单（这是合理的"零插件态"演示），需用户确认是否额外搭一个 fake plugin 演示注册流程。
4. **DemoCollections 重载族折叠**：NovaLinkedList / NovaMultiDictionary / NovaOrderedDictionary 是否合并到 1.7 单 demo 还是各拆一叶？目前 DemoTreeData.cs 只有 1.7 单叶，按"代表性折叠"原则放在 1.7 内子卡片切换。**确认无需拆叶。**
5. **DemoUtil 子卡片选择**：Util 工具集体量大（Json / Encrypt / SysIO / MD5 / Convert / TypeCreator / Assembly / HybridCLR），1.6 单叶选 3 子卡片（Json / AES / MD5）覆盖代表性，**Convert / SysIO / TypeCreator 是否补卡片**待用户拍板。
6. **DemoIntegrationEventNetwork 4.4 mock 方式**：要不要起一个本机 echo websocket？还是直接 fake `Nova.Event.Fire` 模拟收消息后桥接？建议后者（无外部依赖），**待确认**。
7. **DemoNavTree 现有图标 `icon_arrow_red` 来源**：当前 prefab 已有该图，是否保留作为 § 4 资源清单一员还是另选规范化名？暂按保留处理。
8. **UI 注册表 `UIGroupName=Demo` 是否与现有 UISettings 冲突**：需在 UISettings.UIGroupSettings 列表追加 `Demo` 组（Depth=50，Visible=true），**待 editor-coder 在 UIComponent Inspector 实操确认**。
9. **辅助 view DemoToastView/DemoDialogView 是否登记 DemoTreeData 叶子**：当前 DemoTreeData.cs 33 叶不含它们；它们是 2.8/4.1/4.2 内部 spawn 的子页面，**不登记叶子**——需用户确认与现有 33 数对齐。
10. **DemoIntegrationProcedureAsset 4.3 真做热更链路演示还是只读快照**：背景说明"4.3 / 4.5 走只读快照"，但若 EnableHotfix=false 则链路展示无意义，**确认 4.3 仅展示当前总开关 + 节点状态即可，不实际跑下载**。

> 设计稿到此结束。

