# MainDemo — UIs 35 Demo 索引

> 本文档是 35 个演示子页面的完整索引表。每行含：编号 / 类名 / 变体（R=只读快照 / I=交互触发）/ 演示 API / 资源依赖 / 实现状态。
>
> 历史设计背景见 [`DESIGN.md`](DESIGN.md)。本表描述演示入口，不是逐函数全量覆盖或运行验收证明；只读快照中的 API 提示不代表页面实际执行了该调用。

---

## BaseDemoView 基类

| 项 | 说明 |
|---|---|
| 路径 | `BaseDemoView/` |
| 说明 | 三段式骨架（TitleBar 64px + InteractionArea flex + FeedbackArea 200px）+ `AppendFeedback` + `ClearFeedback` |
| 模板方法 | `SetTitle(string)` / `SetButtonApiHint(Button,string)` / `SetFieldApiHint(TMP_Text,string)` / `AppendFeedback(string, FeedbackLevel)` / `ClearFeedback()` |

---

## 1. Core 核心库（9 个）

| 编号 | 类名 | R/I | 演示 API | 资源依赖 |
|---|---|---|---|---|
| 1.1 | `DemoFrameworkComponentView` | I | 生命周期说明；`Nova.Self.PauseGame()` / `ResumeGame()` / `ResetNormalGameSpeed()` / `GameSpeed` / `QuitApplication()` | 无 |
| 1.2 | `DemoFrameworkManagerView` | R | `Util.TypeCreator.Create<I{Xxx}Manager>() / Manager.Priority` | 无 |
| 1.3 | `DemoFsmView` | R | `Fsm<T>.Start<TState>() / ChangeState / AddStates` | 无 |
| 1.4 | `DemoReferenceView` | I | `ReferencePool.Get<T>() / Put(reference)` | 无 |
| 1.5 | `DemoLogView` | R | `Log.Debug / Log.Warning / Log.Error` | 无 |
| 1.6 | `DemoUtilView` | R | `Util.Json.Serialize(obj) / Util.Encrypt.AES.EncryptString(text) / DecryptString(cipher)` | 无 |
| 1.7 | `DemoCollectionsView` | R | `new NovaLinkedSet<int>() / .Add(x) / .Contains(x)` | 无 |
| 1.8 | `DemoExtensionsView` | R | `"hello".ToTitleCase() / list.IsNullOrEmpty()` | 无 |
| 1.9 | `DemoEdgeCasesView` | I | `ReferencePool.Put(null)`、重复订阅与故意不归还引用；关闭不回收故意泄漏项，FSM 按钮仅输出说明 | 无 |

---

## 2. Modules 模块库（17 个）

| 编号 | 类名 | R/I | 演示 API | 资源依赖 |
|---|---|---|---|---|
| 2.1 | `DemoAppView` | I | `Nova.App.CheckAsync() / DownloadAsync() / OpenStoreAsync()`；内置 APK 文件下载仅 Android Player，须启用更新、命中更新并采用 Apk 路由；旧结果不回写重开的页面 | HTTPS 更新规则与下载地址；Editor / iOS / WebGL 拒绝内置 APK 文件下载 |
| 2.2 | `DemoAssetView` | I | `Nova.Asset.LoadAsync<Sprite>(location, ct)` / `IAssetHandle.Release()` | 默认地址 `sprite_icon_tree`；须核对实际采集资源 |
| 2.3 | `DemoPrefabView` | I | `Nova.Prefab.InstantiateAsync(location, parent, ct)` / `Nova.Prefab.Destroy(go)`，旧异步结果销毁回收 | prefab `DemoPrefabBlock` |
| 2.4 | `DemoConfigView` | I | `Nova.Config.AppConfigs / .Namespace / GetSDKPluginConfig<T>()`；`RefreshAppConfigAsync()` / `Custom.GetXxx(path)`；旧刷新不回写重开的页面，关闭不取消全局刷新 | NetCmd `AppCustomConfig`（在线请求）+ ConfigRuntime 本地路径默认值 |
| 2.5 | `DemoEventView` | I | `Nova.Event.Subscribe<T>(h) / Fire(sender, e) / Unsubscribe<T>(h)` | 无 |
| 2.6 | `DemoTableView` | R | `Nova.Table.GetTable<T>() / HasTable<T>()`，逐项校验 4 张表 | xlsx `Demo_Item` / `ListA` / `ListB` / `Map1` |
| 2.7 | `DemoLocalizationView` | I | `Nova.Localization.SetLanguageAsync(lang)` / 五个持久 `TextLocalizing` 绑定（未绑定节点保留 `GetText` 回退） | xlsx `Demo_Localization`（当前 5 key × 简中/英语 2 语言） |
| 2.8 | `DemoUIView` | I | `Nova.UI.OpenUIViewAsync<T>(userData) / CloseUIView(serialID)`；区分加载请求与已打开，关闭含取消加载中打开；失败保留编号供重试 | prefab `DemoToastView` / `DemoDialogView`；只清理本页拥有的子页面 |
| 2.9 | `DemoNetworkView` | I | `Nova.Network.GetAsync / PostAsync / ConnectServer` | 无（在线请求） |
| 2.10 | `DemoProcedureView` | I | 注册与当前流程快照；`ProcedurePlaying.RequestPreloadRestart()` → 原八步预加载 → `ProcedurePlaying` | 重新预加载 |
| 2.11 | `DemoObjectPoolView` | I | `Nova.ObjectPool.CreateSingleGettingObjectPool<T>() / GetObjectPool<T>()` | 无 |
| 2.12 | `DemoPersistView` | I | `Nova.Persist.PlayerPrefs.SetInt / GetInt / HasItem / RemoveItem / Save("Demo")`；默认保存提交整个进程偏好 | 无 |
| 2.13 | `DemoSoundView` | I | `Nova.Sound.PlaySound(group, location, params)` / `StopSound(serialID, 0f)` + `ReleaseAssetBySerialID(serialID)` | mp3 `bgm_main` / `sfx_click` / `sfx_confirm`；xlsx `Demo_Sound` |
| 2.14 | `DemoVibrateView` | I | `Nova.Vibrate.Play(VibrateType.LightImpact) / PlayCustom(...) / PlayEmphasis(...) / StopAll()` | xlsx `Demo_VibrateCustom` / `Demo_VibrateEmphasis` |
| 2.15 | `DemoSDKView` | R | `Nova.SDK.GetAll<ISDKPlugin>() / TryGet<TPlugin>(out p)` | 无 |
| 2.16 | `DemoDebugView` | I | `RuntimeDebugger.Instance.ShowDebugPanel(false) / HideDebugPanel() / IsDebugPanelVisible` | 激活条件满足并初始化后可用 |
| 2.17 | `DemoNativeView` | I | `Nova.Native.GetNotificationPermissionStatusAsync / RequestNotificationPermissionAsync / OpenAppSettingsAsync / OpenNotificationSettingsAsync / RequestInAppReviewAsync`；旧结果不回写重开的页面 | 对应系统能力；取消只结束本页等待，技术返回不证明提示、评价或权限修改完成 |

---

## 3. HybridCLR 热更新解决方案（4 个）

| 编号 | 类名 | R/I | 演示 API | 资源依赖 |
|---|---|---|---|---|
| 3.1 | `DemoHybridClrAotMetadataView` | R | `Util.HybridCLR.LoadAotMetadataAsync(dlls)`（启动期已调，展示快照） | 无 |
| 3.2 | `DemoHybridClrGameDllView` | R | `ProcedureLoadDll → Util.HybridCLR.LoadGameAssemblyAsync(location)`（启动期已调，展示配置快照） | 无 |
| 3.3 | `DemoHybridClrProcedureRegisterView` | R | `ProcedureLoadDll → RegisterAdditionalProcedures(...)` | 无 |
| 3.4 | `DemoHybridClrRuntimeUpdateView` | I | 固定启动 Manifest：`CreateDownloaderByTags → LoadGameAssemblyAsync → Entry → LoadSceneAsync(Additive)` | Running DLL + `DemoRuntimeHotUpdateContent` 场景（tag：`demo_runtime_hotupdate`） |

3.4 页面同时提供独立 Content 加载/卸载：`Nova.Asset.LoadSceneAsync("DemoRuntimeHotUpdateContent", Additive, ct)` → 唯一 `ISceneHandle.UnloadAsync()`。加载中可请求取消，关闭、迟到结果和重入按页面所有权收束；独立场景验证不代表原 Running DLL 热更新验证。

---

## 4. Integration 跨模块联动（5 个）

| 编号 | 类名 | R/I | 演示 API | 资源依赖 |
|---|---|---|---|---|
| 4.1 | `DemoIntegrationUiLocalizationView` | I | `Nova.Localization.SetLanguageAsync + Nova.UI.OpenUIViewAsync<T>` | 复用 `Demo_Localization` |
| 4.2 | `DemoIntegrationUiAssetView` | I | `Nova.Asset.LoadAsync<Sprite>(loc)` 后以文字 userData 打开 `DemoToastView` | 默认 `sprite_icon_tree` + `DemoToastView`；不把资源作为 UI Prefab 注入 |
| 4.3 | `DemoIntegrationEventNetworkView` | I | 本地模拟连接状态，`Nova.Event.Subscribe<DemoPingEventData>` / `Fire` / `Unsubscribe` | 无；不建立真实 WebSocket 连接 |
| 4.4 | `DemoIntegrationConfigHybridClrView` | R | `Nova.Config.Namespace → Util.Assembly.GetAssembly(ns)` | 已加载程序集的快照；命名空间与程序集需按实际配置区分 |
| 4.5 | `DemoIntegrationTableUiView` | I | `Nova.Table.HasTable<TbDemo_Item>() / GetTable<TbDemo_Item>()`，按 `Id` 唯一扫描并刷新详情；重复入口回焦 | `Demo_Item@TableList.xlsx`、新页面 Prefab 和正式 UI 注册；图标地址仅作文字展示 |

4.5 每次打开默认读取明确编号 `1001`，输入 `1002` 可读取另一行；缺编号、重复编号或非整数会清空旧详情并显示原因。`TbDemo_Item` 是 List 表，不能把 `Id` 当行索引。池化重开会重新读表，新增页面的 Prefab 保存、注册导出、编译和实际导航打开仍需各自验收。

---

## 辅助子页面（不计入 35 叶树形导航）

| 类名 | 说明 | 引用方 |
|---|---|---|
| `DemoToastView` | 轻量 Toast 子页面，含自闭定时器 | 2.8 DemoUIView / 4.1 DemoIntegrationUiLocalizationView / 4.2 DemoIntegrationUiAssetView |
| `DemoDialogView` | 带按钮的对话框子页面 | 2.8 DemoUIView |

---

## UI 注册表概要

DemoXxxView 及 2 个辅助 view 的注册源为 `Excels/UIs/UIs.xlsx`（正式 JSON：`Jsons/UIs.json`）；新增页面须同步注册源并通过正式导出更新产物：

- `UIGroupName`：Demo 分组（`Nova.prefab` 中 `m_Depth=1`，`PauseCoveredUIView=false`）
- `AssetLocation`：`DemoXxxView`（当前 `AddressByFileName` 采集，与 prefab 文件名对应）
- 辅助 view `DemoToastView` / `DemoDialogView` 注册在 `UIGroupName=DemoSub`

---

## 关联文档

- [`DESIGN.md`](DESIGN.md) — 当前基类说明与历史设计背景
- [`README.md`](README.md) — MainDemo 工程总览与 Codex 使用入口
- 已安装 Framework 包的 `Docs/INDEX.md`、`Docs/START_HERE.md` 与 `Agents/INDEX.md` — 模块文档与复用技能入口；由实际包解析位置读取，不依赖主仓目录
