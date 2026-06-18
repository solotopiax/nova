# EventPool\<T\>

`EventPool<T>` 是事件系统真正的队列与分发层。

`EventManager` 只是持有它并转发调用；事件的排队、限流、默认处理器、异常隔离和引用池归还都发生在这里。

## 什么时候先看这页

优先看这页的场景：

- 你要排查为什么 `Fire()` 不是立即触发。
- 你要确认每帧限流和积压 warning 的具体行为。
- 你要看“派发中取消订阅”为什么不会打断遍历。
- 你要确认事件对象是什么时候被归还引用池的。

## 核心机制

### 1. `Fire()` 先入队，`Update()` 再分发

`Fire(sender, e)` 会：

- 把事件包装成内部 `Event` 节点
- `lock (m_Events)` 后入队

真正的处理发生在下一次 `Update()`。

### 2. `Update()` 先搬运，再按帧限流分发

`Update()` 的步骤是：

1. 把 `m_Events` 全部转移到 `m_ProcessingEvents`
2. 逐个取出并执行 `HandleEvent(...)`
3. 如果达到 `m_MaxDispatchPerFrame`，本帧停止，剩余事件下帧继续

积压超过阈值时会输出 warning。当前阈值是 `100`。

### 3. 派发中取消订阅是被显式处理过的

`m_CurrentFiringNodes` 和 `m_TempNodes` 的存在，就是为了保证：

- 当前正在遍历 handler 链时
- 某个 handler 里又执行了 `Unsubscribe(...)`
- 遍历指针仍然能安全前移

这不是 incidental 行为，而是这个池专门处理过的边界。

### 4. 无处理器、默认处理器、异常隔离都有明确语义

- 有匹配 handler：依次执行
- 没有匹配 handler 但存在默认处理器：走默认处理器
- 没有匹配 handler 且 `AllowNoHandler` 未开启：抛异常
- 单个 handler 抛异常：记录日志，但不阻止后续 handler

### 5. 事件数据会在分发结束后归还引用池

`HandleEvent(...)` 结束时会 `ReferencePool.Put(e)`。

无论是正常分发、默认处理器分发，还是“无 handler 且不允许”的异常路径，事件对象都会被回收。

## 调用方可依赖的语义

- `Subscribe` / `Unsubscribe` 不是线程安全操作，应在主线程调用
- `Fire()` 是线程安全入队
- `FireNow()` 是立即同步处理，不经过队列
- `GetHandlers(...)` / `GetRegisteredEventIDs()` 只是诊断接口

## 风险点 / 易错点

- `EventCount` 只反映 `m_Events` 当前待入处理队列的数量，不等于总积压规模或 handler 数量。
- 帧限流只会延后分发，不会丢事件。
- 事件对象在池内会被复用，handler 外持有引用是危险操作。
- `AllowDuplicateHandler` 没开时，重复订阅会直接抛异常。

## 继续阅读

关键源码：

- [EventPool.cs](../../../../../../Scripts/Runtime/Modules/Event/Managers/Implements/EventPools/EventPool.cs)
- [EventPool.Event.cs](../../../../../../Scripts/Runtime/Modules/Event/Managers/Implements/EventPools/EventPool.Event.cs)

相关文档：

- [EventPoolMode.md](EventPoolMode.md)
- [EventManager.md](../../EventManager.md)
- [EventManagerBase.md](../EventManagerBase.md)
- [EventData.md](../../Definitions/EventData.md)
