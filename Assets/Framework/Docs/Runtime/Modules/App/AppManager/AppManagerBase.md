# AppManagerBase

`AppManagerBase` 是 App 管理器的抽象中间层。

它的价值主要有两点：

- 固定 App 模块这条 Manager 分支的优先级
- 把 `IAppManager` 契约和 `FrameworkManager` 生命周期组合到一起

## 什么时候先看这页

优先看这页的场景：

- 你要确认 App 模块在 Manager 调度中的顺序。
- 你要扩展新的 AppManager 实现，同时保持现有契约。

## 语义要点

### 1. Priority 固定为 11

`AppManagerBase.Priority => 11`。

这定义了它在 Framework Manager 调度里的相对顺序。

### 2. 它只定义骨架，不提供业务逻辑

这里声明的是：

- 初始化
- Update / Shutdown
- 版本检查
- 下载 / 跳商店
- 检查结果状态属性

真正的 HTTP 请求、JSON 解析和 URL 解析逻辑都在 [AppManager.md](AppManager.md)。

## 继续阅读

关键源码：

- [AppManagerBase.cs](../../../../../Scripts/Runtime/Modules/App/Managers/AppManager/Implements/AppManagerBase.cs)

相关文档：

- [IAppManager.md](IAppManager.md)
- [AppManager.md](AppManager.md)
