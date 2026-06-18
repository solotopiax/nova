# LauncherSettings

`LauncherSettings` 是启动期 UI 的序列化配置对象。

它集中描述的是：

- 闪屏最短展示时长
- 三类启动面板的 Resources 路径
- 启动期轻量本地化 JSON 路径模板

## 什么时候先看这页

优先看这页的场景：

- 你要改启动期面板 Prefab 路径。
- 你要排查 Splash 为什么展示时间不对。
- 你要看启动期弹窗和进度条文本从哪个 JSON 路径模板读取。

## 配置语义

### 1. `SplashDuration`

- 类型：`float`
- 默认值：`2f`

语义是“闪屏最短展示时间”，由 `ProcedureSplash` 消费。

### 2. 面板 Prefab 路径

- `SplashPanelPrefab`
- `ProgressPanelPrefab`
- `DialogPanelPrefab`

它们都是相对 `Resources/` 的路径，最终由 `LauncherUIController.LoadPanel<T>()` 通过 `Resources.Load` 加载。

### 3. `LocalizationJsonPathTemplate`

- 默认值：`BuiltIn/Jsons/LocalizationTexts_{0}`

这个模板不是给正式 `LocalizationManager` 用的，而是给 `LauncherLocalization` 用的轻量启动期文本链路。

## 调用方可依赖的边界

- 这个对象在 `ProcedureComponent` Inspector 上序列化。
- 它服务的是 Splash 到 LoadDll 之间这段启动 UI 链路，不是正式游戏内 UI 配置。

## 风险点 / 易错点

- 面板路径相对于 `Resources/`，不是 AssetBundle 地址。
- 改动 `LocalizationJsonPathTemplate` 会直接影响启动期 UI 文本是否能命中。
- `SplashDuration` 只控制最短时间，不保证后续流程已经准备好。

## 继续阅读

关键源码：

- [LauncherSettings.cs](../../../../Scripts/Runtime/Modules/Procedure/Definitions/LauncherSettings.cs)

相关文档：

- [ProcedureComponent.md](ProcedureComponent.md)
- [ProcedureSplash.md](ProcedureSplash.md)
- [LauncherUIController.md](LauncherUIController.md)
- [LauncherLocalization.md](LauncherLocalization.md)
