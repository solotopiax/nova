# ObservableEvent

**类签名**：`public abstract class ObservableEvent<T>`  
**命名空间**：`NovaFramework.Runtime`

SDK 事件容器抽象基类。新订阅者注册时会收到缓冲补发，具体缓冲策略由子类决定。

## 当前公开 API

```csharp
public abstract class ObservableEvent<T>
{
    public abstract IDisposable Subscribe(Action<T> handler);
    public abstract void Invoke(T value);
    public abstract void Clear();
}
```

同文件还提供 `ObservableEventExtensions.Subscribe(..., ICollection<IDisposable> bag)` 扩展，用于把订阅句柄纳入生命周期容器。

## 子类

- [StickyEvent.md](StickyEvent.md)
- [ReplayEvent.md](ReplayEvent.md)
