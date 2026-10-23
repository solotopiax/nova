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

`m_CurrentFiringNodes` 和 `m_TempNodes` 的存在，用于处理：

- 当前正在遍历 handler 链时
- 某个 handler 里又执行了 `Unsubscribe(...)`
- 尝试前移下一节点

指针修正只比较委托而未核对事件 ID；跨 ID 复用同一委托可能影响另一条分发中的指针，且修正发生在最终 Remove 之前，失败注销也可能已改变指针。

### 4. 无处理器、默认处理器、异常隔离都有明确语义

- 有匹配 handler：依次执行
- 没有匹配 handler 但存在默认处理器：走默认处理器
- 没有匹配 handler 且 `AllowNoHandler` 未开启：抛异常
- 单个 handler 抛异常：记录日志，但不阻止后续 handler

### 5. 事件数据会在分发结束后归还引用池

`HandleEvent(...)` 结束时会 `ReferencePool.Put(e)`。

正常分发、默认处理器分发，以及“无 handler 且不允许”的拒绝分支都会调用归还；拒绝分支先 `Put(e)` 再抛错。单个 handler 异常通常会被记录并继续。

事件数据没有总 `finally` 回收保证：日志、遍历或 `Put` 本身的异常仍可能中断这一步。排队派发中，`Update()` 的 `finally` 只归还包装 `Event`，包装的 `Clear()` 只置空引用，不替事件数据归还。

## 调用方可依赖的语义

- `Subscribe` / `Unsubscribe` 不是线程安全操作，应在主线程调用
- `Fire()` 仅对队列入队加锁，包装创建发生在锁外
- `FireNow()` 是立即同步处理，不经过队列
- `GetHandlers(...)` / `GetRegisteredEventIDs()` 只是诊断接口

## 风险点 / 易错点

- `EventCount` 只反映 `m_Events` 当前待入处理队列的数量，不等于总积压规模或 handler 数量。
- 帧限流只会延后分发，不会丢事件。
- 事件对象在池内会被复用，handler 外持有引用是危险操作。
- `AllowDuplicateHandler` 没开时，重复订阅会直接抛异常。
- `Clear()` 只清 `m_Events` 入队队列，不清 `m_ProcessingEvents` 的限流积压，也不逐项归还包装节点或事件数据；后续 `Update()` 仍可处理已有积压。
- `Shutdown()` 会进一步清处理队列、订阅、遍历指针和默认处理器，但同样不逐项归还未派发对象，也没有“已销毁”调用守卫。

## 委托、重入和包装

Subscribe/Check/Unsubscribe 对空委托抛 ArgumentNullException。已有 ID 先检查是否允许多处理器，再检查重复；只开 AllowDuplicateHandler 并不能绕过禁多处理器。Unsubscribe 移除一项匹配绑定，不存在或重复注销会抛，不自动跟随业务对象销毁。默认委托可显式 null 清除，仅在无普通订阅时使用，不计入订阅数量。

入队保存 sender 与 e 原引用；接受后发布方不得修改、再次提交或手动 Put。回调期间 Fire 的新节点等待下次搬运，FireNow 则绕过队列和限额。分发指针以 e 为键，没有同 e 重入保护；递归复用可覆盖指针或重复归还。内部 Event.Create 取引用池包装并保存两引用，Clear 只置空，不克隆、Clear 或归还 EventData，也不销毁 sender。

正常与禁止无处理器分支会归还数据；handler/default 异常只在正常记录日志的条件下继续。Update catch 还会读取载荷 ID，数据可能已被归还；日志或 Put 异常仍可中断轮询。只有已出队包装受 finally 保障，不能据此推导 payload 在所有异常下都已回池。启用正数限流且处理队列剩余大于 100 才 warning。

## 继续阅读

关键源码：

- [EventPool.cs](../../../../../../Scripts/Runtime/Modules/Event/Managers/Implements/EventPools/EventPool.cs)
- [EventPool.Event.cs](../../../../../../Scripts/Runtime/Modules/Event/Managers/Implements/EventPools/EventPool.Event.cs)

相关文档：

- [EventPoolMode.md](EventPoolMode.md)
- [EventManager.md](../../EventManager.md)
- [EventManagerBase.md](../EventManagerBase.md)
- [EventData.md](../../Definitions/EventData.md)
