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

Sticky/Replay 容器按主线程调用设计，没有集合锁；载荷保留原引用，不深复制、不接管池化对象。Invoke 先更新缓存，再复制 handler 列表逐个调用并记录异常；本轮已经取得的快照不会因 Clear/取消订阅而撤销。Subscribe 的同步补发发生在正式 Add 之前，回调可以立即重入。相同委托允许重复注册，Unsubscribe 只移除首个匹配；句柄 Dispose 只保证该句柄自身幂等。Clear 清缓存和列表，但不会逐个 Dispose 外部持有的句柄，旧句柄仍引用容器。

扩展方法先校验 event/handler/bag，再订阅并把句柄 Add 到 bag；Add 若抛异常，已经生效的订阅不会回滚。普通集合的 Clear 也不会替业务 Dispose 订阅，应在 owner 退出时逐个释放。

## 子类

- [StickyEvent.md](StickyEvent.md)
- [ReplayEvent.md](ReplayEvent.md)
