---
id: GLO-15
title: Fsm 有限状态机与 Procedure
type: glossary
status: active
date: 2026-08-05
summary: Fsm 驱动 Procedure 状态流转
category: runtime
source: docs-and-source-verification
aliases:
  - GLO-15-fsm-state-machine
  - Fsm
  - IFsm
  - FsmState
keywords: [GLO-15, Fsm, IFsm, FsmState, Procedure, ProcedureBase, ProcedureOwner, ChangeState, 状态机]
tags: [glossary, nova, terminology, fsm, procedure, runtime]
related:
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
  - "[[MOC-Procedure|MOC-Procedure]]"
  - "[[MOC-HybridCLR|MOC-HybridCLR]]"
---

# GLO-15：Fsm 有限状态机与 Procedure

## 定义

Fsm 是 Nova Core 的通用有限状态机（`Assets/Framework/Scripts/Runtime/Core/Fsm/`）：`IFsm<T>` 持有一组 `FsmState<T>` 并按 `ChangeState` 切换。Nova 的启动/热更流程（Procedure）就是 Fsm 的应用：`ProcedureOwner` 即 `IFsm<IProcedureManager>`，每个 `ProcedureBase` 是一个 `FsmState`。

## 边界

- 状态生命周期回调固定五段：`OnInit -> OnEnter -> OnUpdate -> OnLeave -> OnDestroy`。
- 流程推进用 `ChangeState<TState>(procedureOwner)`，且必须在 `OnUpdate` 最后一行调用；调用后会同步触发新状态 `OnEnter`，之后不得再执行逻辑（`ProcedureBase.cs` 顶部约定）。
- FSM 切换过程中禁止追加 Procedure（`IProcedureManager` 注释：由 `Fsm.AddStates` 守卫）。
- 当前 Fsm 的唯一正式消费者是 Procedure 模块；不要为新业务自建平行状态机框架。

## 易混淆项

- Fsm ≠ UniTask：Fsm 管“阶段与流转顺序”，UniTask 管“阶段内异步等待”（GLO-12），两者配合而非替代。
- `ProcedureOwner` 不是 Manager 本身，而是 `IFsm<IProcedureManager>`；访问 Manager 要经 `procedureOwner.Owner`。
- `ChangeState` 是受保护的 `FsmState` 辅助方法，业务 Procedure 用它；外部不得直接操作 `IFsm.ChangeState`。

## 示例

```csharp
protected internal override void OnUpdate(ProcedureOwner procedureOwner)
{
    // ...阶段逻辑...
    ChangeState<ProcedureLoadDll>(procedureOwner); // 必须是最后一行
}
```

## 来源与验证

- `Assets/Framework/Scripts/Runtime/Core/Fsm/`：`IFsm<T>`（`ChangeState<TState>`）、`FsmState<T>`（五段回调与受保护 `ChangeState`）。
- `Assets/Framework/Scripts/Runtime/Modules/Procedure/Definitions/ProcedureBase.cs`：`ProcedureOwner = IFsm<IProcedureManager>` 别名与 ChangeState 调用约定。
- `ProcedureCheckVersion.cs` 等：`ChangeState<ProcedureLoadDll>` 实际调用点。
