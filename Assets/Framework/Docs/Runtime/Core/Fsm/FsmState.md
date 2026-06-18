# FsmState

**类签名**：`public abstract class FsmState<T> where T : class`
**命名空间**：`NovaFramework.Runtime`

有限状态机状态基类，定义状态的 5 个生命周期回调和状态切换方法。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Core/Fsm/FsmState.cs` | `abstract FsmState<T>` | 泛型状态基类，5 个生命周期 + ChangeState |

---

## § 3 继承关系

```
FsmState<T> (abstract)
  └── ProcedureBase (abstract) : FsmState<IProcedureManager>
        └── ProcedureSplash / ProcedureCheckVersion / ProcedureHotfix / ProcedureAppDownload / ProcedureLoadDll
        └── Game 层自定义 Procedure（如业务自己的 ProcedurePreload）
```

---

## § 4 关键字段表

无实例字段。FsmState 是无状态基类，所有数据通过 IFsm 黑板传递。

---

## § 5 完整公开 API

```csharp
// --- 生命周期（子类重写）---
protected internal virtual void OnInit(IFsm<T> fsm)
protected internal virtual void OnEnter(IFsm<T> fsm)
protected internal virtual void OnUpdate(IFsm<T> fsm)
protected internal virtual void OnLeave(IFsm<T> fsm, bool isShutdown)
protected internal virtual void OnDestroy(IFsm<T> fsm)

// --- 状态切换 ---
protected void ChangeState<TState>(IFsm<T> fsm) where TState : FsmState<T>
protected void ChangeState(Type stateType, IFsm<T> fsm)
```

---

## § 11 使用示例

```csharp
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

public class MyProcedure : ProcedureBase
{
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        // 进入流程逻辑
    }

    protected internal override void OnUpdate(ProcedureOwner procedureOwner)
    {
        base.OnUpdate(procedureOwner);
        // 条件满足时切换
        if (条件)
        {
            ChangeState<NextProcedure>(procedureOwner);
        }
    }
}
```

---

## § 13 关联文档

- [Fsm.md](Fsm.md)（FSM 实现 + IFsm 接口）
- [ProcedureBase.md](../../Modules/Procedure/ProcedureBase.md)
- [Core.md](../Core.md)
