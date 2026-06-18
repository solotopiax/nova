# LauncherSplashPanel

`LauncherSplashPanel` 是启动期闪屏面板组件。

当前它的定位非常简单：

- 只是 Splash Prefab 上的展示容器

## 当前语义边界

- 组件上序列化了背景图和 Logo 图引用
- 当前源码没有公开方法，也没有播放动画或自驱动逻辑
- 生命周期由 `LauncherUIController` 和 `ProcedureSplash` 控制

## 风险点 / 易错点

- 不要把它当成“闪屏流程控制器”；它只是展示层。
- 闪屏什么时候显示、什么时候销毁，不在这个类里决定。

## 继续阅读

关键源码：

- [LauncherSplashPanel.cs](../../../../Scripts/Runtime/Modules/Procedure/LauncherUI/LauncherSplashPanel.cs)

相关文档：

- [ProcedureSplash.md](ProcedureSplash.md)
- [LauncherUIController.md](LauncherUIController.md)
- [LauncherSettings.md](LauncherSettings.md)
