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

- `Fire(sender, e)`：锁内入队，后续 `Update()` 搬运并按限额分发
- `FireNow(sender, e)`：立即同步分发，不是线程安全调用

入队锁不保护事件对象内容、引用池 helper 或其它 API；跨线程发布仍须确保载荷独占及实际 helper 的线程条件。

### 5. EventData 在分发后会被归还引用池

正常派发、默认处理器派发，以及不允许无处理器的拒绝分支，都会调用 `ReferencePool.Put(e)`；拒绝分支先归还再抛错。排队派发另由 `Update()` 的 `finally` 归还包装节点。

事件数据本身没有总 `finally` 回收保证：日志、遍历或 `Put` 的异常仍可能中断回收。`Clear()` / `Shutdown()` 直接清队列，不逐项归还尚未派发的包装节点或事件数据。因此业务既不能在 handler 外长期持有已交池对象，也不能把关闭队列当作完整引用池回收。

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
- `Count` 仅反映 incoming 队列数量，不含处理队列积压；HandlerCount 是事件 ID 组数，不是委托总数。

## 初始化、诊断和归属

构造器先建默认空池，Component Awake 创建 Manager、Start 才复制配置。Initialize 先 Shutdown 旧池，之后才读取 config，新配置为 null/创建失败时旧订阅及队列可能已清，不回滚，也不热绑定 DTO。配置负上限不拒绝，Update 仅对正数限流；mode 的未知位不预检。Priority=1 规定较早 Update、较晚 Shutdown，不保证模块 Start 顺序。

GetHandlers 返回新的委托列表或未知 ID 的 null，修改列表不修改订阅，但元素仍持原目标引用。GetRegisteredEventIDs 返回当前池 Keys 实时只读视图；换池后旧视图不跟随新池。Component 的 EventCount/EventHandlerCount 对缺 Manager 返回 0，int GetHandlers/ID 集合查询返回 null；其余多数 API（包括泛型 GetHandlers）直接解引用，不能把这些兜底值当成已就绪证据。Component OnDestroy 只断 Manager 引用，全局组承担 Shutdown；池没有 destroyed 守卫，关闭后对象仍可再次调用。

接口与 Base 抽象声明不执行这些行为；Base 的泛型包装先取得类型 ID 再转发整数重载，即使后续空委托或缺订阅失败也可能已登记类型。ID、sender、委托均按调用方值传递，不从配置表自动绑定业务生命周期。

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
