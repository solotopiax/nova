# LauncherUIController

`LauncherUIController` 是启动期 UI 的统一控制器。

它管理三类面板：

- Splash
- Progress
- Dialog

外部流程只传“阶段、进度、弹窗类型、回调”，面板加载、实例缓存、层级顺序和销毁都收口在这里。

## 什么时候先看这页

优先看这页的场景：

- 你要排查启动期 UI 为什么重复实例化或没有出现。
- 你要确认 Splash / Progress / Dialog 的层级顺序是谁强制设置的。
- 你要看启动期文本为什么不依赖 `LocalizationManager` 也能工作。

## 依赖与边界

### 它依赖什么

- `LauncherSettings`
- `LauncherLocalization`
- `Resources.Load`
- `LauncherSplashPanel` / `LauncherProgressPanel` / `LauncherDialogPanel`

### 它对外负责什么

- 初始化启动期 UI 配置
- 加载并缓存三类面板实例
- 更新进度与显示弹窗
- 在需要时整体销毁启动期 UI

### 它不负责什么

- 不负责判断业务该显示什么流程
- 不负责资源系统或 LocalizationManager 的初始化
- 不负责面板具体文本内容本身

## 核心流程

### 1. Initialize：先清残留，再初始化轻量本地化

`Initialize(settings)` 会：

1. `DestroyAll()`
2. 缓存 `s_Settings`
3. `LauncherLocalization.Initialize(settings?.LocalizationJsonPathTemplate)`

所以启动期 UI 文本走的是 `LauncherLocalization`，与正式的 `LocalizationManager` 完全解耦。

### 2. 面板按需加载，并统一 `DontDestroyOnLoad`

`ShowSplash()` / `ShowProgress()` / `ShowDialog()` 最终都会走 `LoadPanel<T>()`。

加载逻辑是：

- `Resources.Load<GameObject>(prefabName)`
- `Instantiate(prefab)`
- `DontDestroyOnLoad(instance)`
- 强制设置所有 Canvas 的 `overrideSorting = true`

这意味着启动期 UI 默认跨场景存活。

### 3. 层级顺序由框架强制覆盖

当前排序规则固定为：

- Splash = `0`
- Progress = `10`
- Dialog = `20`

Prefab 自己的 Canvas 配置会被覆盖，最终显示层级不由美术侧自由决定。

### 4. 缺面板时会安全降级

当前实现里：

- `ShowProgress()` 加载失败会输出 error 并直接返回
- `ShowDialog()` 加载失败会输出 error，并直接执行 `onConfirm?.Invoke()`

后者很关键，因为它意味着“弹窗资源缺失”不会把启动链卡死。

### 5. Destroy 系列是幂等的

`DestroySplash()` / `DestroyProgress()` / `DestroyDialog()` / `DestroyAll()` 都会经过统一的 `DestroyPanel(ref panel)`。

无论是正常对象、Unity fake-null，还是已经为空，都会最终归一成真正的 `null`。

## 高价值 API 面

- 初始化：`Initialize(LauncherSettings settings)`
- Splash：`ShowSplash()` / `DestroySplash()`
- Progress：`ShowProgress(...)` / `UpdateProgress(...)` / `HideProgress()` / `DestroyProgress()`
- Dialog：`ShowDialog(...)` / `HideDialog()` / `DestroyDialog()`
- 全局回收：`DestroyAll()`

## 风险点 / 易错点

- `LauncherStage` 现在对 `ShowProgress(stage)` 主要是语义标签，不再负责文本映射。
- 启动期 UI 文本不依赖 `LocalizationManager`，所以不要把两套链路混在一起排查。
- `ShowDialog()` 在面板缺失时会直接执行确认回调，这是一条有意的降级路径。
- 启动期 UI 默认 `DontDestroyOnLoad`，如果业务入口不回收，会出现跨场景残留。

## 继续阅读

关键源码：

- [LauncherUIController.cs](../../../../Scripts/Runtime/Modules/Procedure/LauncherUI/LauncherUIController.cs)
- [LauncherLocalization.cs](../../../../Scripts/Runtime/Modules/Procedure/LauncherUI/LauncherLocalization.cs)

相关文档：

- [LauncherSettings.md](LauncherSettings.md)
- [LauncherStage.md](LauncherStage.md)
- [LauncherDialogType.md](LauncherDialogType.md)
- [LauncherLocalization.md](LauncherLocalization.md)
