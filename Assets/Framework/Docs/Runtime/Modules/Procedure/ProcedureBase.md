# ProcedureBase

`ProcedureBase` 是所有流程的共同基类。

它的核心作用不是提供业务流程模板代码，而是统一两件事：

- 把流程状态固定到 `FsmState<IProcedureManager>` 这条 FSM 体系里
- 给每个流程提供一条随生命周期创建、取消、释放的 `CancellationToken`

## 什么时候先看这页

优先看这页的场景：

- 你要新增一个内置或业务 `Procedure`。
- 你要确认异步任务该绑定哪个 token。
- 你要排查为什么流程离开后异步逻辑应该立刻停止。

## 核心语义

### 1. 每个流程自带生命周期 token

`ProcedureBase` 内部维护 `m_Cts`，并通过：

- `OnEnter()`：新建 `CancellationTokenSource`
- `OnLeave()`：只 `Cancel()`
- `OnDestroy()`：`Dispose()` 并清空

向子类暴露 `protected CancellationToken CancellationToken`。

这意味着：

- 流程中的异步任务应默认绑定这个 token
- 流程切走时，异步逻辑应响应取消而不是继续跑完

### 2. `OnLeave()` 只取消，不释放

当前实现明确区分了：

- `OnLeave()`：只负责让异步 continuation 观察到取消
- `OnDestroy()`：才真正释放 CTS 资源

这是为了避免流程刚离开时，尚未返回的异步 continuation 访问到一个已经被过早释放的源。

### 3. `ChangeState` 仍然是同步切换

`ProcedureBase` 继承自 `FsmState<IProcedureManager>`，流程切换仍沿用 FSM 的同步切换语义。

因此已有约定仍成立：

- 在 `OnUpdate()` 里决定跳转最稳妥
- `ChangeState(...)` 后不要再继续写依赖旧状态的逻辑

## 调用方可依赖的边界

- 所有内置和业务流程都应该从这里继承
- 异步流程统一使用 `CancellationToken`
- 这里不提供“下一流程自动推导”之类额外机制

旧文档里提到的 `GetNextProcedureType()` / `ChangeToNext()` 并不存在于当前源码。

## 风险点 / 易错点

- 重写 `OnEnter()` / `OnLeave()` / `OnDestroy()` 时如果不调用 `base`，会直接破坏 token 生命周期。
- 异步方法里拿到 `CancellationToken` 却不在关键 await 后检查取消，会造成流程切走后逻辑继续运行。
- 不要把 `OnLeave()` 里的取消理解成资源已完全释放；真正释放发生在 `OnDestroy()`。

## 继续阅读

关键源码：

- [ProcedureBase.cs](../../../../Scripts/Runtime/Modules/Procedure/Definitions/ProcedureBase.cs)

相关文档：

- [ProcedureManager.md](ProcedureManager.md)
- [ProcedureComponent.md](ProcedureComponent.md)
- [BuiltInProcedures.md](BuiltInProcedures.md)
- [FsmState.md](../../Core/Fsm/FsmState.md)
