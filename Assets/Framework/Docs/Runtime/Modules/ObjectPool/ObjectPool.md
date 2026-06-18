# ObjectPool\<T\>

**类签名**：`internal sealed class ObjectPool<T> : ObjectPoolBase, IObjectPool<T> where T : ObjectBase`
**命名空间**：`NovaFramework.Runtime`

对象池核心实现类。同时继承 `ObjectPoolBase`（提供非泛型统一管理入口）和实现 `IObjectPool<T>`（提供强类型业务操作接口）。内部使用双索引表管理对象：按名称索引（`NovaMultiDictionary`，支持同名多实例）和按 Target 索引（`Dictionary`，用于回收/释放时快速定位）。内置可插拔的释放策略，支持定时自动释放、容量溢出触发释放和手动释放。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Managers/Implements/ObjectPools/ObjectPool.cs` | 对象池核心实现 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_Objects` | `NovaMultiDictionary<string, Object<T>>` | 按 Name 索引的对象集合（支持同名多实例） |
| `m_ObjectMap` | `Dictionary<object, Object<T>>` | 按 Target 索引的对象映射表 |
| `m_DefaultReleaseObjectsFilter` | `ReleaseObjectsFilter<T>` | 默认释放对象筛选器 |
| `m_CachedCanReleaseObjects` | `List<T>` | 缓存的可释放对象集合 |
| `m_CachedToReleaseObjects` | `List<T>` | 缓存的待释放对象集合 |
| `s_ReleaseObjectComparer` | `Comparison<T>` | 静态释放对象比较器（O(n log n) 排序，按优先级升序、同优先级按最久未使用排序） |
| `m_AllowMultiGet` | `bool` | 是否允许对象被多次获取 |
| `m_AutoReleaseInterval` | `float` | 自动释放间隔秒数 |
| `m_AutoReleaseTimeCounter` | `float` | 自动释放时间计数器 |
| `m_Capacity` | `int` | 对象池容量（设置时触发 Release） |
| `m_ExpireTime` | `float` | 对象过期秒数（设置时触发 Release） |
| `m_Priority` | `int` | 对象池优先级 |

## 公开 API

```csharp
// 构造
public ObjectPool(bool allowMultiGet, ObjectPoolConfig config)

// 注册对象（超容量时自动触发释放）
public void Register(T obj, bool got)

// 检查可获取性
public bool CanGet()
public bool CanGet(string name)

// 获取对象
public T Get()
public T Get(string name)

// 回收对象（超容量时自动触发释放）
public void Put(T obj)
public void Put(object target)

// 设置元数据
public void SetLocked(T obj, bool locked)
public void SetLocked(object target, bool locked)
public void SetPriority(T obj, int priority)
public void SetPriority(object target, int priority)

// 释放单个对象
public bool ReleaseObject(T obj)
public bool ReleaseObject(object target)

// 批量释放
public override void Release()
public override void Release(int toReleaseCount)
public void Release(ReleaseObjectsFilter<T> releaseObjectsFilter)
public void Release(int toReleaseCount, ReleaseObjectsFilter<T> releaseObjectsFilter)
public override void ReleaseAllUnused()

// 信息查询
public override ObjectInfo[] GetAllObjectInfos()

// 生命周期
public override void Update()     // 累加计时器，到期触发 Release
public override void Shutdown()   // 释放所有对象并清空集合
```

## 释放触发路径

```
定时自动：Update() -> AutoReleaseTimeCounter 到期 -> Release()
容量溢出：Register()/Put() 后 Count > Capacity -> Release()
容量变更：Capacity.set -> Release()
过期变更：ExpireTime.set -> Release()
手动调用：业务代码直接调用 Release()/Release(count)/Release(filter)
```

所有路径最终汇聚到 `Release(int toReleaseCount, ReleaseObjectsFilter<T> filter)` 统一入口。

业务侧通常不直接 `new ObjectPool<T>`，而是通过 `IObjectPoolManager.CreateSingleGettingObjectPool* / CreateMultiGettingObjectPool*` 创建对象池，让名称、容量和自动释放策略统一走 `ObjectPoolConfig`。

## 候选对象过滤条件（GetCanReleaseObjects）

对象必须同时满足以下条件才能进入释放候选列表：
1. 未被使用（`IsInUse == false`）
2. 未被锁定（`Locked == false`）
3. 自定义标记允许释放（`CustomCanReleaseFlag == true`）

## 关联文档

- [ObjectPoolBase](ObjectPoolBase.md) -- 非泛型基类
- [IObjectPool\<T\>](IObjectPool.md) -- 泛型接口
- [Object\<T\>](Object.md) -- 内部引用计数包装器
- [ObjectBase](ObjectBase.md) -- 池化对象基类
- [ReleaseObjectsFilter\<T\>](ReleaseObjectsFilter.md) -- 释放筛选器委托
