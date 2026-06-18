# EventComponent

`EventComponent` 是事件系统的场景入口，也是 `Nova.Event` 对应的组件门面。

它本身不实现事件池或分发逻辑，职责只有两件事：

- 反射创建 `IEventManager`
- 把 Inspector 上的事件池配置下发给 Manager

真正的订阅、入队、主线程分发和回收，都在 `EventManager` / `EventPool`。

## 什么时候先看这页

优先看这页的场景：

- 你要确认事件池模式和每帧分发上限从哪里配置。
- 你要判断某个 API 是组件门面，还是 Manager 的真实实现。
- 你要排查为什么场景里改了配置，但事件行为没有按预期变化。

如果你已经在看事件积压、线程安全或分发顺序，继续看 [EventManager.md](EventManager.md)。

## 依赖与边界

### 它依赖什么

- `IEventManager`
- `EventManagerConfig`
- `Util.TypeCreator`

### 它对外暴露什么

- 订阅 / 取消订阅门面
- 线程安全入队 `Fire(...)`
- 立即同步分发 `FireNow(...)`
- 诊断入口：`GetRegisteredEventIDs()` / `GetHandlers(...)`

### 它不负责什么

- 不负责事件队列管理
- 不负责主线程分发节流
- 不负责 `EventData` 的引用池归还
- 不负责默认处理器的实际执行

## 核心流程

### Awake：先创建 Manager

`Awake()` 会：

1. `base.Awake()`
2. `Util.TypeCreator.Create<IEventManager>(m_CurManagerTypeName)`

类型名无效时会直接抛 `InvalidOperationException`。

### Start：注入事件池配置

`Start()` 只会向 Manager 下发：

- `PoolMode`
- `MaxDispatchPerFrame`

这里不会主动分发任何事件，也不会做额外的预热逻辑。

### 运行期 API 都是薄透传

这些公开方法本质上都只是委托：

- `Subscribe(...)`
- `Unsubscribe(...)`
- `SetDefaultHandler(...)`
- `Fire(...)`
- `FireNow(...)`
- `GetHandlers(...)`

真正的行为语义由 `EventManager` 和 `EventPool` 决定。

### OnDestroy 只清空引用

`OnDestroy()` 这里只把 `m_EventManager` 置空，不是事件系统真正的 shutdown 入口。

## 高价值配置面

- `m_CurManagerTypeName`：决定创建哪种 `IEventManager`
- `m_EventPoolMode`：控制无处理器 / 多处理器 / 重复处理器策略
- `m_MaxDispatchPerFrame`：控制每帧最大分发数，`0` 表示不限制

## 风险点 / 易错点

- `Fire()` 和 `FireNow()` 语义完全不同，不能只按“是否立即”去理解。
- 组件层不做任何线程保护或引用池兜底，全部依赖底层 Manager。
- `GetHandlers(...)` 和 `GetRegisteredEventIDs()` 属于诊断接口，不应被业务逻辑当作核心运行依赖。
- `OnDestroy()` 只清空组件引用；不要把它误读成事件队列已经被完整关闭。

## 继续阅读

关键源码：

- [EventComponent.cs](../../../../Scripts/Runtime/Modules/Event/EventComponent.cs)
- [EventComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Event/EventComponent.Visitors.cs)

相关文档：

- [EventManager.md](EventManager.md)
- [IEventManager.md](Interfaces/IEventManager.md)
- [EventData.md](Definitions/EventData.md)
- [EventPool.md](Implements/EventPools/EventPool.md)
- [EventPoolMode.md](Implements/EventPools/EventPoolMode.md)
