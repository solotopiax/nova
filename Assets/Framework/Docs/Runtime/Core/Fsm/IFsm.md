# IFsm\<T\>

**类签名**：`public interface IFsm<T> where T : class`
**命名空间**：`NovaFramework.Runtime`

有限状态机接口，定义了状态机的核心操作契约。提供状态的启动、查询、切换功能，以及基于字符串键的黑板数据读写机制，用于状态间的数据共享。泛型参数 `T` 表示状态机的持有者类型。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `IFsm.cs` | 接口定义 |

## 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Owner` | `T` | 状态机持有者 |
| `CurrentState` | `FsmState<T>` | 当前状态 |
| `IsDestroyed` | `bool` | 状态机是否已销毁 |

## 公开 API

```csharp
// 启动
void Start<TState>() where TState : FsmState<T>;
void Start(Type stateType);

// 状态查询
bool HasState<TState>() where TState : FsmState<T>;
TState GetState<TState>() where TState : FsmState<T>;
FsmState<T> GetState(Type stateType);

// 状态切换
void ChangeState<TState>() where TState : FsmState<T>;
void ChangeState(Type stateType);

// 黑板数据
void SetData(string key, object value);
TData GetData<TData>(string key);
bool RemoveData(string key);
```

## 关联文档

- [Fsm](Fsm.md)
- [FsmState](FsmState.md)
