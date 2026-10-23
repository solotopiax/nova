# Object\<T\>

**类签名**：`internal sealed class Object<T> : IReference where T : ObjectBase`
**命名空间**：`NovaFramework.Runtime`

内部引用计数包装器。为 `ObjectPool<T>` 提供对 `ObjectBase` 子类实例的引用计数管理。引用计数是池的内部机制，封装在 `Object<T>` 内部，对业务层不可见。实例通过 `ReferencePool` 进行创建和回收，避免频繁 GC。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Managers/Implements/ObjectPools/Object.cs` | 内部包装器定义 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_Object` | `T` | 被包装的 ObjectBase 子类实例 |
| `m_RefCount` | `int` | 引用计数 |
| `Name` | `string` | 转发 `m_Object.Name` |
| `Locked` | `bool` | 转发 `m_Object.Locked`（可读写） |
| `Priority` | `int` | 转发 `m_Object.Priority`（可读写） |
| `CustomCanReleaseFlag` | `bool` | 转发 `m_Object.CustomCanReleaseFlag` |
| `LastUseTime` | `DateTime` | 转发 `m_Object.LastUseTime` |
| `IsInUse` | `bool` | `m_RefCount > 0` |
| `RefCount` | `int` | 当前引用计数 |

## 内部 API

```csharp
// 工厂方法：从 ReferencePool 获取实例并初始化
public static Object<T> Create(T obj, bool got)

// IReference 实现
public void Clear()

// 查看对象（不增加引用计数）
public T Peek()

// 获取对象（引用计数 +1，更新 LastUseTime，回调 OnGet）
public T Get()

// 归还对象（回调 OnPut，更新 LastUseTime，引用计数 -1，负数则抛异常）
public void Put()

// 释放对象（调用 ObjectBase.Release，再将 ObjectBase 还回 ReferencePool）
public void Release(bool isShutdown)
```

## 参数与所有权边界

Create 拒 null obj，取引用池包装后先设 RefCount=got?1:0，got=true 随即 OnGet，但不额外更新 LastUseTime；此回调失败不会自动归还已取得包装。Get 则先计数++、UTC 更新时间、再 OnGet。Put 顺序为 OnPut → UTC 更新时间 → 计数-- → 负数检查，回调失败计数未减，重复归还则已改状态后抛。Peek 不计数。Release 先业务释放，再把 T 回 ReferencePool，最后断 m_Object；它不归还自身包装，异常也无 finally 保证。Clear 仅断引用/归零，元数据 getter/setter 直接转发 m_Object，清理后读取可失败。

## 关联文档

- [ObjectBase](ObjectBase.md) -- 被包装的对象基类
- [ObjectPool\<T\>](ObjectPool.md) -- 持有 `Object<T>` 集合的对象池
