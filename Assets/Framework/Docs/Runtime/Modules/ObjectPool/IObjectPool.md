# IObjectPool\<T\>

**类签名**：`public interface IObjectPool<T> where T : ObjectBase`
**命名空间**：`NovaFramework.Runtime`

泛型对象池接口。弥补 `ObjectPoolBase` 没有泛型方法的空缺，为业务层提供强类型的对象注册、获取、回收、释放操作。业务代码通过 `IObjectPoolManager.CreateSingleGettingObjectPool<T>()` 或 `CreateMultiGettingObjectPool<T>()` 获取该接口实例，无需关心内部实现细节。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Managers/Definitions/IObjectPool.cs` | 接口定义 |

## 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Name` | `string` | 对象池名称 |
| `FullName` | `string` | 对象池完整名称（类型+名称） |
| `ObjectType` | `Type` | 对象池对象类型 |
| `Count` | `int` | 对象池中对象数量 |
| `CanReleaseCount` | `int` | 可被释放的对象数量 |
| `AllowMultiGet` | `bool` | 是否允许对象被多次获取 |
| `AutoReleaseInterval` | `float` | 自动释放间隔秒数 |
| `Capacity` | `int` | 对象池容量 |
| `ExpireTime` | `float` | 对象过期秒数 |
| `Priority` | `int` | 对象池优先级；默认释放策略下会先释放数值更小的对象 |

## 公开 API

```csharp
// 注册对象
void Register(T obj, bool got)

// 检查可获取性
bool CanGet()
bool CanGet(string name)

// 获取对象
T Get()
T Get(string name)

// 回收对象
void Put(T obj)
void Put(object target)

// 设置元数据
void SetLocked(T obj, bool locked)
void SetLocked(object target, bool locked)
void SetPriority(T obj, int priority)
void SetPriority(object target, int priority)

// 释放单个对象
bool ReleaseObject(T obj)
bool ReleaseObject(object target)

// 批量释放
void Release()
void Release(int toReleaseCount)
void Release(ReleaseObjectsFilter<T> releaseObjectsFilter)
void Release(int toReleaseCount, ReleaseObjectsFilter<T> releaseObjectsFilter)
void ReleaseAllUnused()
```

## 关联文档

- [ObjectPoolBase](ObjectPoolBase.md) -- 非泛型抽象基类
- [ObjectPool\<T\>](ObjectPool.md) -- 具体实现类（同时继承 ObjectPoolBase 并实现本接口）
- [ObjectBase](ObjectBase.md) -- 泛型约束基类
- [ReleaseObjectsFilter\<T\>](ReleaseObjectsFilter.md) -- 释放筛选器委托
