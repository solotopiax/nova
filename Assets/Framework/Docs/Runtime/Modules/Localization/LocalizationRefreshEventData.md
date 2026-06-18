# LocalizationRefreshEventData

`LocalizationRefreshEventData` 是语言切换成功后的刷新事件数据。

它只承载两件事：

- 切换前语言 `OldLanguage`
- 切换后语言 `NewLanguage`

事件由 `LocalizationManager` 在语言切换完成后，通过 `IEventManager.FireNow(...)` 同步广播。

## 什么时候先看这页

优先看这页的场景：

- 你要监听语言切换并刷新 UI。
- 你要确认语言切换事件是不是立即触发。
- 你要排查为什么事件对象不能在异步逻辑里长期持有。

## 语义要点

### 1. 这是“切换成功后”的事件

只有 `LocalizationManager` 完成文本缓存切换、保存持久化之后，才会创建并广播这个事件。

它不是“准备切换”事件，也不是“切换失败”事件。

### 2. 事件是同步立即广播

当前实现里：

- `LocalizationManager.ApplyLanguageSwitchResult(...)`
- `FireRefreshEvent(oldLanguage, newLanguage)`
- `m_EventManager.FireNow(this, eventData)`

所以监听方会在同一条语言切换调用链里收到通知。

### 3. 事件对象来自引用池

- `Create(...)` 通过 `ReferencePool.Get<LocalizationRefreshEventData>()` 获取对象
- `Clear()` 会把两个语言字段重置为 `Language.Unspecified`

因此 handler 返回后，不能继续持有这个对象引用。

## 调用方可依赖的语义

- `OldLanguage` 一定是切换前语言
- `NewLanguage` 一定是切换后语言
- 收到事件时，当前语言缓存已经更新完成
- 事件对象会参与引用池复用

## 风险点 / 易错点

- 这是同步广播；handler 里做重逻辑会直接拖慢切语言链路。
- 不要在异步任务里直接捕获事件对象本身；需要长期使用时应自行复制值。
- `OldLanguage` 可能是 `Language.Unspecified`，例如第一次初始化当前语言时。

## 继续阅读

关键源码：

- [LocalizationRefreshEventData.cs](../../../../Scripts/Runtime/Modules/Localization/Managers/Definitions/LocalizationRefreshEventData.cs)

相关文档：

- [LocalizationManager.md](LocalizationManager.md)
- [TextLocalizing.md](TextLocalizing.md)
- [EventManager.md](../Event/EventManager.md)
- [EventData.md](../Event/Definitions/EventData.md)
