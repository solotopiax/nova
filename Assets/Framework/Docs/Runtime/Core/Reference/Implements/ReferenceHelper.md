# ReferenceHelper

**类签名**：`internal sealed partial class ReferenceHelper : IReferenceHelper`
**命名空间**：`NovaFramework.Runtime`

引用助手实现类，实现 `IReferenceHelper` 接口，负责引用池的具体管理逻辑。内部维护 `Dictionary<Type, ReferenceCollection>` 按类型管理引用集合，支持线程安全的获取、归还、追加和移除操作。包含内部类 `ReferenceCollection` 作为单类型引用的实际容器。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Implements/ReferenceHelper.cs` | 引用助手主体 |
| `Implements/ReferenceHelper.ReferenceCollection.cs` | 内部引用集合容器 |

## 关键字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_ReferencePools` | `Dictionary<Type, ReferenceCollection>` | 按类型索引的引用池集合 |
| `m_StrictCheck` | `bool` | 是否开启强制检查 |

## 公开 API

```csharp
void Initialize(bool strictCheck);
void ClearAll();
int GetPoolCount();

T Get<T>() where T : class, IReference, new();
IReference Get(Type referenceType);
void Put(IReference reference);

void Add<T>(int count) where T : class, IReference, new();
void Add(Type referenceType, int count);

void Remove<T>(int count) where T : class, IReference;
void Remove(Type referenceType, int count);
void RemoveAll<T>() where T : class, IReference;
void RemoveAll(Type referenceType);

IReadOnlyList<ReferencePoolInfo> GetAllReferencePoolInfos();
```

## 内部类：ReferenceCollection

单类型引用的实际容器，使用 `Queue<IReference>` 缓存未使用的引用实例，支持线程安全的获取、归还、追加和移除操作，并跟踪累积统计数据。所有计数器操作均在 `lock(m_References)` 内部执行，确保多线程下统计数据的原子性。StrictCheck 模式下使用 `HashSet<IReference>` 辅助去重（O(1) 查重），非 StrictCheck 模式下 HashSet 为 null，零开销。`GetPoolCount()` 已加锁保护，与其他方法一致。

## 关联文档

- [ReferencePool](../ReferencePool.md)
- [IReferenceHelper](../Interfaces/IReferenceHelper.md)
- [IReference](../IReference.md)
- [ReferencePoolInfo](../ReferencePoolInfo.md)
