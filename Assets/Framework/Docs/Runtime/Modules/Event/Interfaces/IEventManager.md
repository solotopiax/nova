# IEventManager

`IEventManager` 定义的是事件系统的运行时契约。

调用方真正应该依赖的不是接口里有多少个重载，而是这些稳定语义：

- 订阅和取消订阅按事件类型编号生效
- 泛型版本只是 `EventTypeID` 的语法糖
- `Fire()` 与 `FireNow()` 是两种不同分发模型

## 契约定位

直接依赖它的通常是：

- `EventComponent`
- 框架内部需要统一发事件的模块
- 需要做事件诊断或统计的调试逻辑

它描述的是“事件系统对外保证什么”，不是“底层队列如何实现”。

## 调用方可依赖的语义

### 1. 查询、订阅、取消订阅都围绕事件类型 ID

- `GetCountByID(int id)`
- `Check(int id, handler)`
- `Subscribe(int id, handler)`
- `Unsubscribe(int id, handler)`

泛型版本只是把 `T : EventData` 映射为 `EventTypeID.Get<T>()` 后再委托到 `int id` 版本。

### 2. 发布分成排队版和立即版

- `Fire(sender, e)`：线程安全，下一帧主线程分发
- `FireNow(sender, e)`：立即同步分发，不提供线程安全保证

这条边界是事件系统最核心的契约之一。

### 3. 默认处理器是“兜底”，不是普通订阅

- `SetDefaultHandler(handler)` 只在事件没有匹配 handler 时触发
- 它不替代正常订阅关系

### 4. 诊断接口不改变分发语义

- `GetRegisteredEventIDs()`
- `GetHandlers(int id)` / `GetHandlers<T>()`

这些接口只提供观察能力，不承诺可以安全驱动业务逻辑分支。

## 最小 API 面

- 配置：`Initialize(EventManagerConfig config)`
- 发布：`Fire(...)` / `FireNow(...)`
- 订阅：`Subscribe(...)` / `Unsubscribe(...)`
- 查询：`GetCountByID(...)` / `Check(...)`
- 兜底：`SetDefaultHandler(...)`
- 诊断：`GetRegisteredEventIDs()` / `GetHandlers(...)`

## 变更影响面

如果这里的契约变化，会直接影响：

- [EventComponent.md](../EventComponent.md)
- [EventManager.md](../EventManager.md)
- 所有依赖 `Nova.Event` 的运行时代码

尤其高风险的是：

- `Fire()` 是否仍保证线程安全入队
- 泛型重载是否仍映射到同一套 `EventTypeID`
- 立即分发和队列分发的边界是否变化
- 默认处理器是否仍保持“只在无匹配 handler 时触发”

## 相关实现

关键源码：

- [IEventManager.cs](../../../../../Scripts/Runtime/Modules/Event/Managers/Interfaces/IEventManager.cs)

相关文档：

- [EventComponent.md](../EventComponent.md)
- [EventManager.md](../EventManager.md)
- [EventData.md](../Definitions/EventData.md)
- [EventManagerConfig.md](../Definitions/EventManagerConfig.md)
- [EventPool.md](../Implements/EventPools/EventPool.md)
