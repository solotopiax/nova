# Fsm / IFsm

**类签名**：`internal sealed class Fsm<T> : IFsm<T> where T : class`
**接口签名**：`public interface IFsm<T> where T : class`
**命名空间**：`NovaFramework.Runtime`

有限状态机实现。通过静态工厂 `Fsm<T>.Create` 创建，持有状态字典和黑板，驱动状态的生命周期切换。Fsm 是纯工具类，不继承 FrameworkManager。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Core/Fsm/IFsm.cs` | `interface IFsm<T>` | FSM 公开接口：Owner / CurrentState / Start / HasState / GetState / ChangeState / 黑板 API |
| `Core/Fsm/Fsm.cs` | `sealed Fsm<T>` | FSM 内部实现：状态字典 / 黑板 / 切换逻辑 / 静态 Create / Update / Shutdown / AddStates |
| `Core/Fsm/Fsm.Visitors.cs` | `sealed Fsm<T>` | 字段与属性（含 m_IsChangingState 重入保护） |
| `Core/Fsm/Fsm.Methods.cs` | `sealed Fsm<T>` | 私有方法（AddStateInternal / ChangeStateInternal） |

---

## § 3 继承关系

```
IFsm<T> (interface)
  └── Fsm<T> (internal sealed)
```

---

## § 4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_Owner` | `T` | 构造传入 | 状态机持有者 |
| `m_States` | `Dictionary<Type, FsmState<T>>` | 空字典 | 所有状态，<状态类型, 状态实例> |
| `m_Blackboard` | `Dictionary<string, object>` | 空字典 | 黑板数据，<键名, 数据值>，用于状态间传递参数 |
| `m_CurrentState` | `FsmState<T>` | `null` | 当前状态 |
| `m_IsDestroyed` | `bool` | `false` | 是否已销毁 |
| `m_IsChangingState` | `bool` | `false` | 重入保护：OnLeave/OnEnter 执行期间为 true，禁止此期间调用 AddStates |

---

## § 5 完整公开 API

```csharp
// --- IFsm<T> 接口 ---
public interface IFsm<T> where T : class
{
    T Owner { get; }
    FsmState<T> CurrentState { get; }
    bool IsDestroyed { get; }
    void Start<TState>() where TState : FsmState<T>
    void Start(Type stateType)
    bool HasState<TState>() where TState : FsmState<T>
    TState GetState<TState>() where TState : FsmState<T>
    FsmState<T> GetState(Type stateType)
    void ChangeState<TState>() where TState : FsmState<T>
    void ChangeState(Type stateType)
    void SetData(string key, object value)
    TData GetData<TData>(string key)
    bool RemoveData(string key)
}

// --- Fsm<T> 内部方法（仅 ProcedureManager 等内部类使用）---
static Fsm<T> Create(T owner, params FsmState<T>[] states)
void AddStates(params FsmState<T>[] states)
void Update()
void Shutdown()
```

---

## § 9 关键算法

### Create 流程

1. 创建 Fsm 实例，遍历 states 数组填充 `m_States` 字典（以 `state.GetType()` 为 key）
2. 检查重复类型，重复则抛异常
3. 遍历所有 state 调用 `state.OnInit(fsm)`
4. 返回 fsm（此时尚未启动，需调用 Start）

### AddStates 流程（运行时动态追加，热更场景使用）

三步原子操作，任一校验失败则不写入任何内容：

1. **全量校验**：检查 `m_IsDestroyed`、`m_IsChangingState`、states 非空、每项非 null、本次批次内无重复、与现有 `m_States` 无重复
2. **正式写入**：调用 `AddStateInternal` 逐个写入 `m_States`（此阶段不再抛异常）
3. **OnInit 回调**：遍历所有新增状态调用 `OnInit(this)`

**守卫说明**：`m_IsChangingState` 在 `ChangeStateInternal` 的 `OnLeave`→`OnEnter` 区间内为 `true`，此时调用 `AddStates` 会抛 `InvalidOperationException`。`ProcedureLoadDll` 在 `OnEnter` 内通过 `await UniTask.Yield()` 主动脱离调用栈，确保 `m_IsChangingState` 已回落后再执行注册。

### ChangeState 流程

泛型重载 `ChangeState<TState>()` 与非泛型重载 `ChangeState(Type stateType)` 逻辑完全一致，区别仅在于前者以 `typeof(TState)` 为查询键，后者直接使用传入的 `stateType`。

1. 检查 `m_CurrentState != null`（状态机已启动）和 `m_IsDestroyed == false`
2. 以 `typeof(TState)` / `stateType` 为键在 `m_States` 字典中查找目标状态，找不到则抛异常
3. `m_CurrentState.OnLeave(this, false)` — 通知旧状态离开
4. `m_CurrentState = m_States[stateType]` — 切换到新状态
5. `m_CurrentState.OnEnter(this)` — 通知新状态进入

### Shutdown 流程

1. 若 `m_CurrentState != null`，先把 `m_IsChangingState = true`，再执行 `m_CurrentState.OnLeave(this, true)`，确保 shutdown 期间不能重入 `ChangeState` / `AddStates`
2. `OnLeave(isShutdown=true)` 返回后再设置 `m_IsDestroyed = true`，因此当前状态在 shutdown 离开阶段仍可读取/移除黑板数据
3. 遍历所有 state 调用 `OnDestroy(this)`
4. 清空 `m_States` 和 `m_Blackboard`
5. `m_CurrentState = null`

---

## § 11 使用示例

```csharp
// 由 ProcedureManager.Initialize 内部使用
Fsm<IProcedureManager> fsm = Fsm<IProcedureManager>.Create(this, procedures);
fsm.Start(entranceProcedureType);

// Update 中驱动
fsm.Update();

// Shutdown 时销毁
fsm.Shutdown();
```

---

## § 12 注意事项

| 场景 | 正确做法 |
|---|---|
| Fsm 是 internal 类 | 业务层不直接创建 Fsm，通过 ProcedureManager 间接使用 |
| 黑板存取 | key 不能为 null 或空字符串，否则抛异常 |
| 重复启动 | Start 只能调用一次，重复调用抛异常 |
| 销毁后调用 | 所有公开方法（Start/Update/ChangeState/SetData/GetData/RemoveData/AddStates）在 `m_IsDestroyed == true` 时抛出 `InvalidOperationException` |
| 未启动时切换状态 | `ChangeState` 在 `m_CurrentState == null` 时抛出明确异常 |
| 在 OnLeave/OnEnter 中调用 AddStates | `m_IsChangingState == true` 时抛出 `InvalidOperationException`；解决方案：通过 `await UniTask.Yield()` 脱离当前调用栈后再调用 |
| AddStates 时批次内类型重复 | 两遍校验中均有检测，任一重复立即抛异常 |

---

## § 13 关联文档

- [FsmState.md](FsmState.md)（状态基类）
- [ProcedureManager.md](../../Modules/Procedure/ProcedureManager.md)
- [Core.md](../Core.md)
