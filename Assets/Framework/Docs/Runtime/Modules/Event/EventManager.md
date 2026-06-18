# EventManager

`EventManager` 是 Nova 事件系统的真实运行核心，但它自身仍然是一个很薄的封装层。

它主要负责：

- 持有 `EventPool<EventData>`
- 接收 `EventComponent` 下发的池配置
- 把所有订阅、发布、查询动作委托给事件池

真正的排队、分发、默认处理器、积压告警和引用池归还，落在 `EventPool<EventData>`。

## 什么时候先看这页

优先看这页的场景：

- 你要排查为什么事件总在下一帧才触发。
- 你要确认 `Priority`、每帧限流和积压 warning 的事实。
- 你要区分 `Fire()`、`FireNow()` 和默认处理器的行为边界。

## 依赖与边界

### 它依赖什么

- `EventPool<EventData>`
- `EventManagerConfig`
- `EventTypeID`
- `ReferencePool`

### 它对外负责什么

- 初始化或重建事件池
- 暴露 handler 数量与待分发数量
- 提供统一的发布 / 订阅 / 查询入口

### 它不负责什么

- 不负责场景组件生命周期
- 不负责具体事件类型的数据内容
- 不负责业务层何时订阅和取消订阅

## 核心流程

### 1. Priority 固定为 1

`EventManagerBase.Priority => 1`。

这意味着它属于非常靠前的 Manager 更新顺序，不是旧文档里写的 `17`。

### 2. Initialize：重建事件池

`Initialize(config)` 会：

1. `m_EventPool.Shutdown()`
2. 按 `config.PoolMode` 新建一个新的 `EventPool<EventData>`
3. 设置 `MaxDispatchPerFrame`

因此切换配置时生效点在初始化，而不是运行中动态热改。

### 3. Update：由事件池统一分发

`Update()` 只是调用 `m_EventPool.Update()`。

事件池内部会：

- 把 `m_Events` 搬到 `m_ProcessingEvents`
- 按 `m_MaxDispatchPerFrame` 逐个分发
- 超过阈值时输出积压 warning

积压 warning 的阈值当前是 `100`。

### 4. Fire 与 FireNow 是两条完全不同的路径

- `Fire(sender, e)`：线程安全入队，下一次 `Update()` 分发
- `FireNow(sender, e)`：立即同步分发，不是线程安全调用

调用方如果需要跨线程安全，只能走 `Fire()`。

### 5. EventData 在分发后会被归还引用池

无论是排队分发还是立即分发，`EventPool` 在处理完成后都会 `ReferencePool.Put(e)`。

这也是为什么事件对象不能在 handler 外长期持有。

## 高价值 API 面

- 配置与状态：`Initialize(...)` / `HandlerCount` / `Count`
- 发布：`Fire(...)` / `FireNow(...)`
- 订阅：`Subscribe(...)` / `Unsubscribe(...)`
- 查询：`GetCountByID(...)` / `Check(...)`
- 兜底：`SetDefaultHandler(...)`
- 诊断：`GetRegisteredEventIDs()` / `GetHandlers(...)`

## 风险点 / 易错点

- `FireNow()` 不会经过队列，也不提供线程安全保证。
- `EventData.Clone()` 默认不支持；如果异步逻辑要跨 handler 生命周期持有数据，需要子类自己实现深拷贝。
- 当 `EventPoolMode` 不允许无处理器时，未命中 handler 的事件会抛异常，而不是静默吞掉。
- `Count` 反映的是待分发事件数量，不是订阅者数量。

## 继续阅读

关键源码：

- [EventManager.cs](../../../../Scripts/Runtime/Modules/Event/Managers/Implements/EventManager.cs)
- [EventManagerBase.cs](../../../../Scripts/Runtime/Modules/Event/Managers/Implements/EventManagerBase.cs)
- [EventPool.cs](../../../../Scripts/Runtime/Modules/Event/Managers/Implements/EventPools/EventPool.cs)
- [EventData.cs](../../../../Scripts/Runtime/Modules/Event/Managers/Definitions/EventData.cs)

相关文档：

- [EventComponent.md](EventComponent.md)
- [IEventManager.md](Interfaces/IEventManager.md)
- [EventData.md](Definitions/EventData.md)
- [EventPool.md](Implements/EventPools/EventPool.md)
- [EventPoolMode.md](Implements/EventPools/EventPoolMode.md)
