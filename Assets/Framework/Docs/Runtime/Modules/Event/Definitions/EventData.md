# EventData

`EventData` 是所有事件数据的共同基类。

它给事件系统提供三件基础能力：

- 统一事件类型 ID
- 引用池回收入口
- 异步安全持有时的可选深拷贝约定

## 什么时候先看这页

优先看这页的场景：

- 你要新增一个事件类型。
- 你要确认事件 ID 是怎么生成的。
- 你要排查为什么事件对象在 handler 外不能继续使用。
- 你要在异步逻辑里安全保留事件数据。

## 语义要点

### 1. `ID` 由运行时类型决定

`ID => EventTypeID.Get(GetType())`

也就是说，子类不需要自己维护事件编号；事件类型 ID 直接取决于具体运行时类型。

### 2. `Clear()` 是引用池回收协议

所有子类都必须实现 `Clear()`，把自己的状态恢复到默认值。

否则对象回到 `ReferencePool` 后再次复用，会把脏数据带给下一次事件。

### 3. `Clone()` 默认不支持

`Clone()` 的默认实现会抛 `NotSupportedException`。

只有当某个事件类型确实需要在 handler 生命周期之外被安全持有时，子类才应该自己 override 深拷贝逻辑。

## 调用方可依赖的语义

- 每个事件实例都能通过 `ID` 映射回自己的事件类型
- 事件对象可能被引用池复用
- 没有 override `Clone()` 的事件，不应在异步流程里直接长期持有

## 风险点 / 易错点

- “handler 返回后仍使用事件对象”是错误用法，因为分发完成后对象会被归还引用池。
- 只复制引用而不 override `Clone()`，不能解决异步安全问题。
- `Clear()` 漏掉字段会造成跨事件污染。

## 继续阅读

关键源码：

- [EventData.cs](../../../../../Scripts/Runtime/Modules/Event/Managers/Definitions/EventData.cs)

相关文档：

- [EventTypeID.md](EventTypeID.md)
- [../EventManager.md](../EventManager.md)
- [../Interfaces/IEventManager.md](../Interfaces/IEventManager.md)
- [EventManagerConfig.md](EventManagerConfig.md)
