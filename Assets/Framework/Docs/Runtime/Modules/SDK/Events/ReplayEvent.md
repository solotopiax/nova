# ReplayEvent

**类签名**：`public sealed class ReplayEvent<T> : ObservableEvent<T>`  
**命名空间**：`NovaFramework.Runtime`

保留固定容量历史队列的可回放事件。适合“每条历史记录都有意义”的场景。

## 关键语义

- 构造参数 `capacity` 决定回放缓冲容量，默认 `4`。
- 新订阅者会按顺序收到当前缓冲队列中的全部历史值。
- `Invoke()` 在队满时淘汰最老一条再写入新值。
- 容量必须大于零；有限历史不是可靠业务持久化。Subscribe 直接枚举当前队列同步补发，回调若重入 Invoke/Clear 修改队列，枚举自身可能抛异常；逐 handler 的异常捕获不覆盖枚举器推进。

## 关联文档

- [ObservableEvent.md](ObservableEvent.md)
- [StickyEvent.md](StickyEvent.md)
