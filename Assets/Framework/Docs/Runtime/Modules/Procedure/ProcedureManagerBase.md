# ProcedureManagerBase

`ProcedureManagerBase` 是流程管理器的抽象中间层。

它的价值不在于提供实现，而在于固定两件事：

- `ProcedureManager` 这一支 Manager 的统一优先级
- `IProcedureManager` 与 `FrameworkManager` 的组合骨架

## 什么时候先看这页

优先看这页的场景：

- 你要确认流程系统在 Manager 调度里的优先级。
- 你要扩展新的流程管理器实现，并希望保持同一套契约。
- 你要排查旧文档里关于 `Priority` 的错误事实。

## 语义要点

### 1. Priority 固定为 6

`ProcedureManagerBase.Priority => 6`。

这代表流程系统在 Framework Manager 调度中的相对顺序，不是旧文档里写的 `0`。

### 2. 它只定义骨架，不提供流程逻辑

这里声明的是：

- `CurrentProcedure`
- `Initialize(...)`
- `Update()`
- `Shutdown()`
- 查询接口
- `RegisterAdditionalProcedures(...)`

真正的 FSM 创建、启动、关闭和追加流程逻辑在 [ProcedureManager.md](ProcedureManager.md)。

### 3. 它保证“流程系统 = FrameworkManager + IProcedureManager”

这意味着任何新的流程管理器实现，只要继承这层，就要同时满足：

- Framework 统一的生命周期调度
- 流程系统既有的运行时契约

## 变更影响面

如果这里的优先级或抽象面发生变化，会直接影响：

- [ProcedureManager.md](ProcedureManager.md)
- [IProcedureManager.md](IProcedureManager.md)
- Framework Manager 的整体调度顺序

## 继续阅读

关键源码：

- [ProcedureManagerBase.cs](../../../../Scripts/Runtime/Modules/Procedure/Managers/Implements/ProcedureManagerBase.cs)

相关文档：

- [ProcedureManager.md](ProcedureManager.md)
- [IProcedureManager.md](IProcedureManager.md)
- [ProcedureComponent.md](ProcedureComponent.md)
- [FrameworkManager.md](../FrameworkManager.md)
