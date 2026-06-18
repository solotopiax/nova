# EventTypeID

`EventTypeID` 是事件类型到整数 ID 的静态注册表。

它解决的是一个很具体的问题：

- 为每个 `EventData` 子类分配稳定且唯一的运行时 ID
- 避免直接依赖 `Type.GetHashCode()` 这类可能冲突的方案

## 什么时候先看这页

优先看这页的场景：

- 你要确认泛型事件接口最后是怎么映射到 `int id` 的。
- 你要新增诊断或调试能力，需要按类型反查事件编号。
- 你要确认事件 ID 是否线程安全、是否全局唯一。

## 核心机制

### 1. ID 通过全局自增分配

- `s_Counter` 从 `0` 开始
- 首次注册时用 `Interlocked.Increment(ref s_Counter)` 分配 ID

因此：

- `0` 被保留为无效值
- 实际事件 ID 从 `1` 开始

### 2. 类型映射缓存是并发安全的

`s_TypeToID` 使用 `ConcurrentDictionary<Type, int>`。

这保证了首次并发访问某个事件类型时，映射仍然是安全且一致的。

### 3. 泛型版本走静态泛型缓存

`Get<T>()` 最终返回 `TypeCache<T>.ID`。

这意味着同一个泛型类型的后续读取不需要再走字典查找。

## 调用方可依赖的语义

- 同一事件类型始终对应同一个 ID
- 不同事件类型会拿到不同 ID
- 泛型接口和非泛型接口最终映射到同一套编号体系

## 风险点 / 易错点

- 这是运行时注册表，不应把具体 ID 数值写死进业务逻辑或配置文件。
- 如果你通过 `Type` 反射拿事件 ID，得到的编号与 `EventData.ID` / 泛型订阅使用的是同一套体系。

## 继续阅读

关键源码：

- [EventTypeID.cs](../../../../../Scripts/Runtime/Modules/Event/Managers/Definitions/EventTypeID.cs)

相关文档：

- [EventData.md](EventData.md)
- [../EventManager.md](../EventManager.md)
- [../Interfaces/IEventManager.md](../Interfaces/IEventManager.md)
