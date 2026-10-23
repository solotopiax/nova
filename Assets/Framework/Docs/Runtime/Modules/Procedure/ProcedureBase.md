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

- `OnEnter()`：释放上一次进入保留的源，再新建 `CancellationTokenSource`
- `OnLeave()`：只 `Cancel()`
- `OnDestroy()`：`Dispose()` 并清空

向子类暴露 `protected CancellationToken CancellationToken`。

这意味着：

- 流程中的异步任务应默认绑定这个 token
- 流程切走时，异步逻辑应响应取消而不是继续跑完

### 2. `OnLeave()` 只取消，不释放

当前实现明确区分了：

- `OnLeave()`：只负责让异步 continuation 观察到取消
- 下一次 `OnEnter()` 或最终 `OnDestroy()`：释放保留的 CTS 资源

这是为了让流程刚离开时，尚未返回的异步 continuation 观察取消。业务任务必须在本次进入时固定 token；不要在 await 返回后重新读取属性，否则重入时可能取得新任务的 token。代际校验用于阻止旧结果与回调修改新进入的状态，取消也不保证底层操作立即中止。

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
- 不要把 `OnLeave()` 里的取消理解成资源已完全释放；保留源在下一次进入或最终销毁时释放。

## 参数与生命周期边界

ProcedureOwner 的准确类型是 IFsm<IProcedureManager>，isShutdown 表示 FSM 关闭离开而非资源对象池释放。token 属性在未进入/已清理时返回 default，不创建 CTS；OnDestroy 只 Dispose，不隐含 Cancel。Cancel 回调异常可中断 OnLeave 后续 base/派生清理，业务资源仍由各流程承担。

## 继续阅读

关键源码：

- [ProcedureBase.cs](../../../../Scripts/Runtime/Modules/Procedure/Definitions/ProcedureBase.cs)

相关文档：

- [ProcedureManager.md](ProcedureManager.md)
- [ProcedureComponent.md](ProcedureComponent.md)
- [BuiltInProcedures.md](BuiltInProcedures.md)
- [FsmState.md](../../Core/Fsm/FsmState.md)
