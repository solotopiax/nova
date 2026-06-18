# EventManagerConfig

`EventManagerConfig` 是事件系统的初始化配置。

它只描述两件事：

- 事件池行为模式
- 每帧最大分发数量

## 什么时候先看这页

优先看这页的场景：

- 你要确认 `EventComponent.Start()` 实际传给 Manager 的配置是什么。
- 你要排查为什么某些事件在无 handler 时会抛错或静默通过。
- 你要看事件积压是否会被每帧限流压住。

## 配置语义

### 1. `PoolMode`

- 类型：`EventPoolMode`
- 默认值：`AllowNoHandler | AllowMultiHandler`

它决定：

- 是否允许事件没有 handler
- 是否允许一个事件有多个 handler
- 是否允许重复 handler

### 2. `MaxDispatchPerFrame`

- 类型：`int`
- 默认值：`0`

语义是：

- `0` 表示不限制
- `> 0` 表示每帧最多分发指定数量，剩余事件延后到下一帧

## 风险点 / 易错点

- 这里的限流只影响分发节奏，不会丢事件。
- `PoolMode` 配错时，问题通常不是“事件没发出去”，而是订阅或分发路径直接抛异常。
- 这只是初始化配置，不是运行中自动热更新的参数容器。

## 继续阅读

关键源码：

- [EventManagerConfig.cs](../../../../../Scripts/Runtime/Modules/Event/Managers/Definitions/EventManagerConfig.cs)

相关文档：

- [../Interfaces/IEventManager.md](../Interfaces/IEventManager.md)
- [../EventManager.md](../EventManager.md)
- [../Implements/EventPools/EventPool.md](../Implements/EventPools/EventPool.md)
- [EventPoolMode.md](../Implements/EventPools/EventPoolMode.md)
