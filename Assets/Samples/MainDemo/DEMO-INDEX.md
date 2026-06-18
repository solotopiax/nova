# MainDemo — UIs 33 Demo 索引

> 本文档是 33 个演示子页面的完整索引表。每行含：编号 / 类名 / 变体（R=只读快照 / I=交互触发）/ 演示 API / 资源依赖 / 实现状态。
>
> 设计稿全文见 [`DESIGN.md`](DESIGN.md)。

---

## BaseDemoView 基类

| 项 | 说明 |
|---|---|
| 路径 | `BaseDemoView/` |
| 说明 | 三段式骨架（TitleBar 64px + InteractionArea flex + FeedbackArea 200px）+ `AppendFeedback` + `ClearFeedback` |
| 模板方法 | `SetTitle(string)` / `SetApiHint(string)` / `AppendFeedback(string, FeedbackLevel)` / `ClearFeedback()` |

---

## 1. Core 核心库（9 个，全 R 只读快照型）

| 编号 | 类名 | R/I | 演示 API | 资源依赖 |
|---|---|---|---|---|
| 1.1 | `DemoFrameworkComponentView` | R | `FrameworkComponent.Awake() / Start() / OnDestroy()` | 无 |
| 1.2 | `DemoFrameworkManagerView` | R | `Util.TypeCreator.Create<I{Xxx}Manager>() / Manager.Priority` | 无 |
| 1.3 | `DemoFsmView` | R | `Fsm<T>.Start<TState>() / ChangeState / AddStates` | 无 |
| 1.4 | `DemoReferenceView` | R | `ReferencePool.Acquire<T>() / Release(reference)` | 无 |
| 1.5 | `DemoLogView` | R | `Log.Debug / Log.Warning / Log.Error` | 无 |
| 1.6 | `DemoUtilView` | R | `Util.Json.ToJson(obj) / Util.Encrypt.AESEncrypt(text, key)` | 无 |
| 1.7 | `DemoCollectionsView` | R | `new NovaLinkedSet<int>() / .Add(x) / .Contains(x)` | 无 |
| 1.8 | `DemoExtensionsView` | R | `"hello".ToTitleCase() / list.IsNullOrEmpty()` | 无 |
| 1.9 | `DemoEdgeCasesView` | R | `ReferencePool.Release(null) / Fsm.ChangeState before Start` | 无 |

---

## 2. Modules 模块库（16 个）

| 编号 | 类名 | R/I | 演示 API | 资源依赖 |
|---|---|---|---|---|
| 2.1 | `DemoAppView` | I | `Nova.App.CheckAsync() / DownloadAsync() / OpenStoreAsync()` | 无 |
| 2.2 | `DemoAssetView` | I | `Nova.Asset.LoadAsync<Sprite>(location, ct)` / `Nova.Asset.RefreshManifestAsync()` / `CreateDownloaderByTags(tags)` / `IAssetDownloader.RunAsync(ct)` / `CreateDownloaderByLocations(locations)` | sprite `icon_coin`（DemoAtlas） |
| 2.3 | `DemoPrefabView` | I | `Nova.Prefab.InstantiateAsync(location) / Destroy(go)` | prefab `DemoPrefabBlock` |
| 2.4 | `DemoConfigView` | R | `Nova.Config.ConfigManager.Common / .Namespace / GetSDKPluginConfig<T>()` | 无 |
| 2.5 | `DemoEventView` | I | `Nova.Event.Subscribe<T>(h) / Fire(sender, e) / Unsubscribe<T>(h)` | 无 |
| 2.6 | `DemoTableView` | R | `Nova.Table.GetTable<TbDemoItem>() / HasTable<T>()` | xlsx `Demo_Item`（10 行） |
| 2.7 | `DemoLocalizationView` | I | `Nova.Localization.SetLanguageAsync(lang) / GetText(name)` | xlsx `Demo_Localization`（5 key × 3 语言） |
| 2.8 | `DemoUIView` | I | `Nova.UI.OpenUIViewAsync<T>(userData) / CloseUIView(serialID)` | prefab `DemoToastView` / `DemoDialogView` |
| 2.9 | `DemoNetworkView` | I | `Nova.Network.GetAsync(url) / PostAsync(url, body) / ConnectServer(...)` | 无（在线请求） |
| 2.10 | `DemoProcedureView` | R | `Nova.Procedure.GetProcedure<T>() / HasProcedure<T>()` | 无 |
| 2.11 | `DemoObjectPoolView` | I | `Nova.ObjectPool.CreateSingleGettingObjectPool<T>() / GetObjectPool<T>()` | 无 |
| 2.12 | `DemoPersistView` | I | `Nova.Persist.PlayerPrefs.SetInt(k, v) / GetInt(k)` | 无 |
| 2.13 | `DemoSoundView` | I | `Nova.Sound.PlaySound(group, location, params) / StopSound(serialID)` | ogg/wav `bgm_main` / `sfx_click` / `sfx_confirm`；xlsx `Demo_Sound` |
| 2.14 | `DemoVibrateView` | I | `Nova.Vibrate.Play(VibrateType.Light) / PlayCustom(...) / StopAll()` | xlsx `Demo_VibrateCustom` / `Demo_VibrateEmphasis` |
| 2.15 | `DemoSDKView` | R | `Nova.SDK.GetAll<ISDKPlugin>() / TryGet<TPlugin>(out p)` | 无 |
| 2.16 | `DemoDebugView` | I | `Nova.Debug.IsActiveWindow / Active / Deactive`（与 DebugComponent 对齐） | 无 |

---

## 3. HybridCLR 运行时热更新（3 个，全 R 只读快照型）

| 编号 | 类名 | R/I | 演示 API | 资源依赖 |
|---|---|---|---|---|
| 3.1 | `DemoHybridClrAotMetadataView` | R | `Util.HybridCLR.LoadAotMetadataAsync(dlls)`（启动期已调，展示快照） | 无 |
| 3.2 | `DemoHybridClrGameDllView` | R | `Util.HybridCLR.LoadGameAssemblyAsync(dlls)` | 无 |
| 3.3 | `DemoHybridClrProcedureRegisterView` | R | `ProcedureLoadDll → RegisterAdditionalProcedures(...)` | 无 |

---

## 4. Integration 跨模块联动（5 个）

| 编号 | 类名 | R/I | 演示 API | 资源依赖 |
|---|---|---|---|---|
| 4.1 | `DemoIntegrationUiLocalizationView` | I | `Nova.Localization.SetLanguageAsync + Nova.UI.OpenUIViewAsync<T>` | 复用 `Demo_Localization` |
| 4.2 | `DemoIntegrationUiAssetView` | I | `Nova.Asset.LoadAsync<GameObject>(loc) → Nova.UI.OpenUIViewAsync<T>(go)` | prefab `DemoSubPanel` |
| 4.3 | `DemoIntegrationProcedureAssetView` | R | `ProcedureCheckVersion → ProcedureHotfix → ProcedureLoadDll` | 无 |
| 4.4 | `DemoIntegrationEventNetworkView` | I | `Nova.Network.OnWebSocketReceiveMessage += h → Nova.Event.Fire(this, e)` | 无（mock echo） |
| 4.5 | `DemoIntegrationConfigHybridClrView` | R | `Nova.Config.ConfigManager.Namespace → Util.Assembly.GetAssembly(ns)` | 无 |

---

## 辅助子页面（不计入 33 叶树形导航）

| 类名 | 说明 | 引用方 |
|---|---|---|
| `DemoToastView` | 轻量 Toast 子页面，含自闭定时器 | 2.8 DemoUIView / 4.1 DemoIntegrationUiLocalizationView / 4.2 DemoIntegrationUiAssetView |
| `DemoDialogView` | 带按钮的对话框子页面 | 2.8 DemoUIView |

---

## UI 注册表概要

所有 33 个 DemoXxxView 及 2 个辅助 view 均注册在 `Excels/UIs/UIs.xlsx`（JSON 副本：`Jsons/UIs.json`）：

- `UIGroupName`：Demo 分组（Depth=50，`PauseCoveredUIView=false`）
- `AssetLocation`：`UIs/DemoXxxView`（与 prefab 同名）
- 辅助 view `DemoToastView` / `DemoDialogView` 注册在 `UIGroupName=DemoSub`

---

## 关联文档

- [`DESIGN.md`](DESIGN.md) — 设计稿全文（§1 BaseDemoView / §2 8维矩阵 / §3 33叶细化 / §4 资源清单 / §5 源数据 / §6 UI注册表）
- [`../../README.md`](../../README.md) — MainDemo 工程总览
- [`../../../../Framework/Docs/INDEX.md`](../../../../Framework/Docs/INDEX.md) — Framework L2 文档索引
