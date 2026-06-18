# StickyEvent

**类签名**：`public sealed class StickyEvent<T> : ObservableEvent<T>`  
**命名空间**：`NovaFramework.Runtime`

保留最近一次值的新订阅可回放事件。适合“只关心最新状态”的场景。

## 关键语义

- `Subscribe()` 时如果已有缓存值，会立即同步回放一次。
- `Invoke()` 会覆盖旧缓存并通知所有订阅者。
- `Clear()` 会清空缓存和值订阅者列表。

## 关联文档

- [ObservableEvent.md](ObservableEvent.md)
- [ReplayEvent.md](ReplayEvent.md)
