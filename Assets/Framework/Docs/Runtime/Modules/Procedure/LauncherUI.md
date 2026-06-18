# LauncherUI（启动阶段 UI）

**命名空间**：`NovaFramework.Runtime`
**资源目录**：`Resources/BuiltIn/Prefabs/`

启动阶段固化 UI：Splash / Progress / Dialog 三类 Prefab 放在 Resources 中，不参与热更新，保证热更前即可展示。`ProcedureSplash` 通过 `LauncherUIController.Initialize(Nova.Procedure.LauncherSettings)` 注入配置；之后各内置 Procedure 只调用 `LauncherUIController` 的静态 API，不直接 `Resources.Load`。

文本内容由各面板的 `LauncherLocalizedText` / `LauncherDialogLocalizedText` 数组在 Inspector 中配置 Key 驱动，底层通过 `LauncherLocalization`（只走 Resources 通道）解析，与 LocalizationManager 完全解耦。

---

## 文件表

| 文件 | 类 | 访问修饰符 | 说明 |
|---|---|---|---|
| `LauncherUI/LauncherUIController.cs` | `LauncherUIController` | `public static` | 统一入口：初始化、Show/Update/Hide/Destroy |
| `LauncherUI/LauncherStage.cs` | `LauncherStage` | `public enum` | 进度 UI 阶段：`CheckVersion` / `Hotfix` / `Preload` |
| `LauncherUI/LauncherDialogType.cs` | `LauncherDialogType` | `public enum` | 弹窗类型：`ForcedDownload` / `RecommendedDownload` / `HotfixFailed` 等 |
| `LauncherUI/LauncherSplashPanel.cs` | `LauncherSplashPanel` | `public sealed` | 闪屏面板（Background + Logo） |
| `LauncherUI/LauncherProgressPanel.cs` | `LauncherProgressPanel` | `public sealed` | 进度面板（Slider + ProgressText + 多语言文本数组） |
| `LauncherUI/LauncherDialogPanel.cs` | `LauncherDialogPanel` | `public sealed` | 通用弹窗（Confirm/Cancel 按钮 + 多语言文本数组） |
| `LauncherUI/LauncherLocalization.cs` | `LauncherLocalization` | `public static` | 启动期本地化解析器（Resources 通道，与 LocalizationManager 解耦） |
| `LauncherUI/LauncherLocalizedText.cs` | `LauncherLocalizedText` | `public sealed` | 普通文本本地化绑定条目（TMP_Text + Key） |
| `LauncherUI/LauncherDialogLocalizedText.cs` | `LauncherDialogLocalizedText` | `public sealed` | 弹窗文本本地化绑定条目（TMP_Text + Key + LauncherDialogType） |

---

## LauncherUIController

`public static` 控制器：持有 `LauncherSettings` 中的 Prefab 名，从 `Resources` 加载并 `DontDestroyOnLoad`。初始化时调 `LauncherLocalization.Initialize`；文本内容完全由各面板自身的 `m_LocalizedTexts` 数组驱动，控制器不传文本。

```csharp
static void Initialize(LauncherSettings settings)

static void ShowSplash()
static void DestroySplash()

static void ShowProgress(LauncherStage stage)   // stage 为保留参数，不用于取文本
static void UpdateProgress(float progress)
static void HideProgress()
static void DestroyProgress()

static void ShowDialog(LauncherDialogType dialogType, Action onConfirm, Action onCancel = null)
static void HideDialog()
static void DestroyDialog()

static void DestroyAll()
```

`DestroySplash` / `DestroyProgress` / `DestroyDialog` / `DestroyAll` 均为幂等调用，可安全用于业务 `ProcedurePreload` 或首屏入口的重复进入/离开收尾。

**sortingOrder 强制规则**（ADR-015）：

| 面板 | sortingOrder |
|---|---|
| Splash | `0` |
| Progress | `10` |
| Dialog | `20` |

---

## LauncherSplashPanel

闪屏面板，纯展示无交互。

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_Background` | `Image` | 全屏背景图 |
| `m_Logo` | `Image` | Logo 图 |

**使用者**：`ProcedureSplash`（OnEnter `ShowSplash`；正常启动路径由业务入口统一回收）

---

## LauncherProgressPanel

进度面板，供版本检查、热更下载、预加载复用。

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_ProgressBar` | `Slider` | 进度条 |
| `m_ProgressText` | `TMP_Text` | 百分比文本（整数，如"42%"） |
| `m_LocalizedTexts` | `LauncherLocalizedText[]` | 本地化文本条目数组，Inspector 配置 |

```csharp
void SetProgress(float progress)   // 0 ~ 1，内部 Clamp；百分比整数格式
```

---

## LauncherDialogPanel

通用弹窗，单 Prefab 覆盖多种启动阶段弹窗。

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_ConfirmButton` | `Button` | 确认按钮 |
| `m_CancelButton` | `Button` | 取消按钮 |
| `m_LocalizedTexts` | `LauncherDialogLocalizedText[]` | 弹窗本地化文本条目数组，Inspector 配置 |

```csharp
void Show(LauncherDialogType type, Action onConfirm, Action onCancel = null)
void Hide()
```

- `onCancel == null` 时隐藏取消按钮
- `Show` 时清理按钮监听，避免重复订阅
- 文本由 `LauncherDialogLocalizedText.ApplyByType` 按 `type` 激活/隐藏对应行

---

## LauncherLocalization

启动期本地化解析器，只走 `Resources.Load` 通道，不依赖 LocalizationManager / IAssetManager / EventManager。

- JSON 根节点：`Launcher_Localization`
- 语言优先级：持久化（IPlayerPrefsManager）> `Application.systemLanguage` 映射 > English 回退
- `Initialize` 幂等；`GetText` miss 时返回 key 本身

---

## Prefab 创建指南

| Prefab | 结构 | 挂载脚本 |
|---|---|---|
| `LauncherSplashPanel.prefab` | Canvas > Background(Image) + Logo(Image) | `LauncherSplashPanel` |
| `LauncherProgressPanel.prefab` | Canvas > Slider + ProgressText(TMP) + m_LocalizedTexts 拖拽绑定 | `LauncherProgressPanel` |
| `LauncherDialogPanel.prefab` | Canvas > ConfirmBtn(Button) + CancelBtn(Button) + m_LocalizedTexts 拖拽绑定 | `LauncherDialogPanel` |

默认路径见 `LauncherSettings` 字段默认值。

---

## 调用方概要

| 流程 | 行为 |
|---|---|
| `ProcedureSplash` | `Initialize(LauncherSettings)` + `ShowSplash` |
| `ProcedureCheckVersion` | 只做大版本检查、Manifest 加载、补丁判断与流程分流；当前不直接操作 Progress UI |
| `ProcedureHotfix` | `ShowProgress(Hotfix)` → `UpdateProgress`；失败时 `ShowDialog(HotfixFailed, ...)`，正常启动路径不主动 `HideProgress` |
| `ProcedureAppDownload` | `ShowDialog(ForcedDownload/RecommendedDownload, ...)`（正常跳转到业务层时不主动销毁 Dialog） |
| `ProcedureLoadDll` | 加载业务 DLL 并跳转业务入口；不主动销毁 `Splash/Progress/Dialog` |
| 业务入口（如 `ProcedurePreload`） | 按首屏衔接需要调用 `DestroySplash` / `DestroyProgress` / `DestroyAll` |

---

## 关联文档

- [BuiltInProcedures.md](BuiltInProcedures.md)
- [LauncherUIController.md](LauncherUIController.md)
- [LauncherSettings.md](LauncherSettings.md)
- [LauncherLocalization.md](LauncherLocalization.md)
- [LauncherLocalizedText.md](LauncherLocalizedText.md)
- [LauncherDialogLocalizedText.md](LauncherDialogLocalizedText.md)
- [LauncherStage.md](LauncherStage.md)
- [LauncherDialogType.md](LauncherDialogType.md)
- [LauncherSplashPanel.md](LauncherSplashPanel.md)
- [LauncherProgressPanel.md](LauncherProgressPanel.md)
- [LauncherDialogPanel.md](LauncherDialogPanel.md)
- [ProcedureComponent.md](ProcedureComponent.md)
