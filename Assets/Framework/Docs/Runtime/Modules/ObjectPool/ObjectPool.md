# ObjectPool\<T\>

**类签名**：`internal sealed partial class ObjectPool<T> : ObjectPoolBase, IObjectPool<T> where T : ObjectBase`
**命名空间**：`NovaFramework.Runtime`

对象池核心实现类。同时继承 `ObjectPoolBase`（提供非泛型统一管理入口）和实现 `IObjectPool<T>`（提供强类型业务操作接口）。内部使用双索引表管理对象：按名称索引（`NovaMultiDictionary`，支持同名多实例）和按 Target 索引（`Dictionary`，用于回收/释放时快速定位）。内置可插拔的释放策略，支持定时自动释放、容量溢出触发释放和手动释放。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Managers/Implements/ObjectPools/ObjectPool.cs` | 对象池核心实现 |
| `Managers/Implements/ObjectPools/ObjectPool.Methods.cs` | 候选过滤与默认释放排序 |
| `Managers/Implements/ObjectPools/ObjectPool.Visitors.cs` | 字段与属性实现 |

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
| `m_Capacity` | `int` | 对象池保留容量；setter 拒绝负数，值实际改变后触发 `Release()`；在用或受保护对象可能使 `Count` 继续超出容量 |
| `m_ExpireTime` | `float` | 对象过期秒数；setter 拒绝小于零的值，值实际改变后触发 `Release()` |
| `m_Priority` | `int` | 池优先级；setter 直接保存，Manager 全局释放按此值升序遍历池，不改变池内对象的 `Priority` |

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
public override void Shutdown()   // 逐项调用 Release(true)，全部完成后清空集合
```

## 释放触发路径

```
定时自动：Update() -> AutoReleaseTimeCounter 到期 -> Release()
容量溢出：Register() 后 Count > Capacity -> Release()
容量溢出：Put() 后 Count > Capacity 且本次归还对象 RefCount <= 0 -> Release()
容量变更：Capacity.set 接受不同的新值 -> Release()
过期变更：ExpireTime.set 接受不同的新值 -> Release()
手动调用：业务代码直接调用 Release()/Release(count)/Release(filter)
```

以上批量筛选路径汇聚到 `Release(int toReleaseCount, ReleaseObjectsFilter<T> releaseObjectsFilter)`。`ReleaseObject`、`ReleaseAllUnused` 和 `Shutdown` 有各自入口：前两者仍执行释放保护检查，`Shutdown` 则直接调用每个包装器的 `Release(true)`。

业务侧通常不直接 `new ObjectPool<T>`，而是通过 `IObjectPoolManager.CreateSingleGettingObjectPool* / CreateMultiGettingObjectPool*` 创建对象池，让名称、容量和自动释放策略统一走 `ObjectPoolConfig`。

## 候选对象过滤条件（GetCanReleaseObjects）

对象必须同时满足以下条件才能进入释放候选列表：
1. 未被使用（`IsInUse == false`）
2. 未被锁定（`Locked == false`）
3. 自定义标记允许释放（`CustomCanReleaseFlag == true`）

`CanReleaseCount` 使用相同三个条件计数，不检查对象是否过期。`Locked` 和 `CustomCanReleaseFlag` 只参与释放保护，不阻止 `Get`。

## 逐重载与参数语义

以下方法的参数均没有可省略的默认值；无参重载的委托行为单独列出。

| 方法 / 参数 | 实际语义与失败边界 |
|---|---|
| `ObjectPool(bool allowMultiGet, ObjectPoolConfig config)` | `allowMultiGet` 决定单取 / 多取模式，构造后不变；`config` 的名称和四个数值在构造时复制，后续修改原配置对象不影响池。内部构造不处理 `config == null`，也不经过属性 setter 校验。Manager 的创建入口会为 `null` 配置补 `new ObjectPoolConfig()`。 |
| `Register(T obj, bool got)` | `obj` 是带 `Name` / `Target` 的池化对象，`null` 抛 `ArgumentNullException`。`got == true` 从引用计数 1 开始，并在写索引前调用 `OnGet()`；`false` 从 0 开始。先写名称索引，再写 Target 索引；重复 Target 的字典添加抛异常时，名称索引已写入，不回滚。容量释放发生在两次索引写入之后。 |
| `CanGet()` / `Get()` | 分别委托 `CanGet(string.Empty)` / `Get(string.Empty)`，只查空名称对象，不遍历所有名称。 |
| `CanGet(string name)` / `Get(string name)` | `name` 按对象名称查找，`null` 抛 `ArgumentException`。只选该名称下第一个满足“多取模式或当前未在用”的对象；找不到分别返回 `false` / `null`。`CanGet` 只做当次检查，不改变计数，也不保留候选；随后 `Get` 会重新查找，检查成功不保证下一次仍能取到同一对象。`Get` 先加计数、更新 UTC `LastUseTime`，再调用 `OnGet()`，回调抛异常时计数不回滚。 |
| `Put(T obj)` / `Put(object target)` | `obj` 重载按 `obj.Target` 定位；`obj` / `target` 为 `null` 抛 `ArgumentNullException`，未登记 Target 抛 `InvalidOperationException`。包装器先调用 `OnPut()`，成功后更新 UTC `LastUseTime` 并减计数；计数减为负数后抛 `InvalidOperationException`，不回滚。多取对象需要为每次取得配对归还。 |
| `SetLocked(T obj, bool locked)` / `SetLocked(object target, bool locked)` | 按 Target 找到已登记对象并立即写入释放锁；`obj` 重载仍使用 `obj.Target`，不按 `T` 实例身份查找。空参数抛 `ArgumentNullException`，未登记 Target 抛 `InvalidOperationException`。`locked` 没有默认值，也不会触发释放。 |
| `SetPriority(T obj, int priority)` / `SetPriority(object target, int priority)` | 参数定位和失败语义同 `SetLocked`；`priority` 立即写入对象的 `ObjectBase.Priority`，任意整数均可，不触发释放。默认筛选中数值更小的对象先被选中；它不修改池的 `Priority`。 |
| `ReleaseObject(T obj)` / `ReleaseObject(object target)` | `obj` 重载按 `obj.Target` 定位。空参数抛 `ArgumentNullException`；未登记、正在使用、锁定或自定义禁止释放时返回 `false`。满足条件时先移除双索引，再调用包装器的 `Release(false)` 并归还包装器；全部完成才返回 `true`。释放回调抛异常时索引移除不回滚。 |
| `Release()` / `Release(ReleaseObjectsFilter<T> releaseObjectsFilter)` | 尝试数量取 `Count - Capacity`；无参版使用默认筛选器，带筛选器版使用传入策略。 |
| `Release(int toReleaseCount)` / `Release(int toReleaseCount, ReleaseObjectsFilter<T> releaseObjectsFilter)` | `toReleaseCount < 0` 被修正为 0；它是筛选输入，不是释放数量上限。只带数量时使用默认策略；显式筛选器为 `null` 抛 `ArgumentNullException`。先生成过期参考时间，再清零自动释放计时器、收集候选并调用筛选器；返回 `null` / 空列表时不释放。结果逐项交给 `ReleaseObject`，仍检查保护条件。 |
| `ReleaseAllUnused()` | 清零自动释放计时器并逐个释放所有当前候选；不按容量、过期时间或目标数量筛选，仍保护在用、锁定及自定义禁止释放的对象。 |
| `Shutdown()` | 不检查上述三个保护条件，逐项执行包装器 `Release(true)` 并归还包装器，全部成功后清空索引和缓存。任一释放回调抛异常会中断循环，后续对象与清空步骤不会完成。 |

`AutoReleaseInterval` 的 getter 返回当前秒数，setter 直接保存，不验证数值、不清零计时器、不立即释放。`Update()` 由 Manager 轮询，使用 `Time.unscaledDeltaTime` 累加计时器，到达该间隔后调用 `Release()`。其他可写属性的 getter 均返回当前保存值，setter 行为见字段表。

## 默认筛选器的数量与时间规则

`DefaultReleaseObjectsFilter(candidateObjects, toReleaseCount, expireTime)` 接收当前可释放对象快照，不修改候选列表，返回池内复用的结果列表：

1. `expireTime > DateTime.MinValue` 时，先选中**全部** `LastUseTime <= expireTime` 的过期候选。即使目标数量为 0，也会选中过期对象，数量可以超过 `toReleaseCount`。
2. 过期对象数量不足目标数量时，从剩余候选按 `ObjectBase.Priority` 升序、同优先级按 `LastUseTime` 从旧到新补足；候选不足时只选实际可选对象。

过期参考时间由 `Release` 根据池的 `ExpireTime` 计算：`ExpireTime < float.MaxValue` 时为 `DateTime.UtcNow.AddSeconds(-ExpireTime)`，否则为 `DateTime.MinValue`；默认配置使用 `float.MaxValue`，因此默认筛选不按过期时间选对象。自定义筛选器收到相同候选、非负目标数量和时间参数，自行决定结果；最终释放仍由 `ReleaseObject` 执行。

## 参数与所有权边界

Target 字典使用默认 object 相等/哈希，不强制 ReferenceEquals；传入另一 T 包装的重载也仅按其 Target 定位，不核对包装同一性。Count 读 Target 索引，GetAllObjectInfos 却遍历 Name 索引；注册失败可能留下孤立 Name 项，两者不保证仍一致。Name 查询 null 拒绝，而 Manager 的池名 null 归 empty，这两类名称不能混用。

构造直接抄数值，负 Capacity/ExpireTime、NaN 等可以绕过运行 setter 的检查。Capacity/ExpireTime setter 先写新值再 Release，释放异常不回滚；AutoReleaseInterval 直接存值并保留累计时间，0/负间隔每次 Update 都会检查释放，NaN 比较也不会被预检。ExpireTime<MaxValue 才计算 UTC AddSeconds，过大时长可抛；NaN 不进入计算。主 Release 仅在筛选器与日期计算通过后重置 counter，即使没有候选也已重置。

默认候选、选择与诊断列表均在池内复用，不是线程安全或可重入的稳定快照。自定义 filter 当次同步执行，不保留订阅，返回 null/empty 无动作；没有独立验证返回重复项或归属，最终逐项 ReleaseObject 再查 Target 与保护门，回调或重入可能使后续项失效。排序仅比较对象 Priority/LastUseTime，同值不承诺稳定；排除已过期项的 Contains 另有线性扫描成本。

ReleaseObject 已移索引后才业务 Release 和两层引用池归还，无总 finally；Shutdown 逐 Target 强制 Release(true)，包括在用、锁定及 Custom 禁止对象，回调异常会留下部分清理且没有 destroyed 守卫。CanReleaseCount 仅读当时条件且可执行业务 getter，不等于即将释放数量；原池引用不延长 Target 生命周期。

## 关联文档

- [ObjectPoolBase](ObjectPoolBase.md) -- 非泛型基类
- [IObjectPool\<T\>](IObjectPool.md) -- 泛型接口
- [Object\<T\>](Object.md) -- 内部引用计数包装器
- [ObjectBase](ObjectBase.md) -- 池化对象基类
- [ReleaseObjectsFilter\<T\>](ReleaseObjectsFilter.md) -- 释放筛选器委托
