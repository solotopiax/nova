# EventManagerBase

`EventManagerBase` 是事件管理器的抽象中间层。

它的作用不是承载复杂实现，而是固定两件事：

- 事件系统这条 Manager 分支的统一优先级
- `IEventManager` 与 `FrameworkManager` 的组合骨架

## 什么时候先看这页

优先看这页的场景：

- 你要确认事件系统在 Manager 调度里的优先级。
- 你要排查旧文档里关于 `Priority` 的错误事实。
- 你要扩展新的事件管理器实现并保持现有契约。

## 语义要点

### 1. Priority 固定为 1

`EventManagerBase.Priority => 1`。

这表示事件系统在 Manager 更新顺序里非常靠前，不是旧文档里写的 `17`。

### 2. 泛型重载在这一层完成映射

`GetCountByID<T>()`、`Check<T>()`、`Subscribe<T>()`、`Unsubscribe<T>()`、`GetHandlers<T>()` 都在这里通过 `EventTypeID.Get<T>()` 委托到 `int id` 版本。

也就是说，具体实现类不需要重复实现泛型映射逻辑。

### 3. 具体事件池行为不在这里

这里声明的是：

- 初始化
- 更新
- 关闭
- 订阅 / 取消订阅
- 发布
- 诊断

真正的事件池重建和分发行为在 [EventManager.md](../EventManager.md) 与 [EventPool.md](EventPools/EventPool.md)。

## 变更影响面

如果这里的优先级或泛型映射方式变化，会直接影响：

- [EventManager.md](../EventManager.md)
- [IEventManager.md](../Interfaces/IEventManager.md)
- 所有依赖泛型事件接口的调用方

## 继续阅读

关键源码：

- [EventManagerBase.cs](../../../../../Scripts/Runtime/Modules/Event/Managers/Implements/EventManagerBase.cs)

相关文档：

- [IEventManager.md](../Interfaces/IEventManager.md)
- [EventManager.md](../EventManager.md)
- [EventPool.md](EventPools/EventPool.md)
