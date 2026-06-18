# ProcedureSplash

`ProcedureSplash` 是启动链的第一个可见流程。

它只负责三件事：

- 初始化 `LauncherUIController`
- 显示闪屏
- 等保底时长到点后，统一进入 `ProcedureCheckVersion`

## 什么时候先看这页

优先看这页的场景：

- 你要排查启动为什么没有经过版本检查。
- 你要确认闪屏什么时候出现、什么时候离开。
- 你要看 Splash 为什么在切流程后没有立刻销毁。

## 输入 / 输出

### 输入

- `ProcedureComponent.LauncherSettings`

### 输出

- `LauncherUIController` 已初始化
- Splash 面板已显示
- 启动链进入版本检查流程

## 主链路

### 1. OnInit：缓存组件引用

初始化阶段会缓存：

- `ProcedureComponent`
- `AssetComponent`

这样后续每帧不需要重复取组件。

### 2. OnEnter：重置时间并显示闪屏

进入流程时会：

- `m_ElapsedTime = 0`
- `LauncherUIController.Initialize(m_ProcedureComponent.LauncherSettings)`
- `LauncherUIController.ShowSplash()`

也就是说，启动期 UI 控制器是在这里正式起步的。

### 3. OnUpdate：只等最短展示时长

每帧只做两件事：

- `m_ElapsedTime += Time.deltaTime`
- 和 `LauncherSettings.SplashDuration` 比较

一旦达到保底时长：

- 统一切到 `ProcedureCheckVersion`

这里没有额外异步等待，也没有“超时上限”机制。

### 4. OnLeave：正常切换时不销毁 Splash

`OnLeave(isShutdown)` 的行为是：

- `isShutdown == true`：`LauncherUIController.DestroySplash()`
- 正常切换：不销毁

这意味着 Splash 面板默认允许跨流程存活，后续由业务入口或其他启动阶段统一回收。

## 风险点 / 易错点

- `SplashDuration` 是最短展示时长，不是最长等待上限。
- `ProcedureSplash` 不再关心 `EnableHotfix`；大版本检测是否执行不受热更开关影响。
- 正常离开流程时不销毁 Splash 是刻意设计，不是遗留资源泄漏。

## 继续阅读

关键源码：

- [ProcedureSplash.cs](../../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureSplash.cs)

相关文档：

- [ProcedureBase.md](ProcedureBase.md)
- [ProcedureCheckVersion.md](ProcedureCheckVersion.md)
- [Procedures/ProcedureLoadDll.md](Procedures/ProcedureLoadDll.md)
- [LauncherUIController.md](LauncherUIController.md)
- [LauncherSettings.md](LauncherSettings.md)
