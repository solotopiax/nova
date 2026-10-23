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

单类型引用的实际容器，使用 `Queue<IReference>` 缓存空闲引用，并始终分配按引用身份比较的 `HashSet<IReference>` 检测重复归还；关闭 StrictCheck 也不关闭身份查重。比较器使用 `ReferenceEquals` 和运行时身份哈希，不调用业务对象的值相等重载。StrictCheck 额外检查类型；集合在创建时捕获该策略，再调用 Helper.Initialize 不会重配已有集合。类型检查拒绝空、抽象或非 IReference 类，但不提前验证公开无参构造器。

泛型 Helper.Get/Add 转发 Type 重载，当前公开路径最终用 `Activator.CreateInstance`，并非直接调用容器的泛型 `new T()`。Get 在构造前先累计借出、获取与新增计数，构造失败不回滚；预热 Add 也先累计数量，再逐项构造，失败会留下已加入的对象。Add/Remove 没有非负校验，负参数不循环却会降低相应累计计数；Remove 只把上限夹到空闲数量，不影响在用对象。

Put 按对象实际类型取池，锁内先加入去重集合，再检查类型、调用 Clear、入队和更新计数；类型或 Clear 异常不会撤销先前的 HashSet 写入。它不证明对象此前从此池借出，外部新对象归还可能使借出统计为负。Remove/RemoveAll 只丢弃空闲引用，不再次调用 Clear、不强取回借出对象；单类型 RemoveAll 保留类型索引，Helper.ClearAll 才清索引，且不冻结已经持有旧集合的并发操作。

队列修改与统计递增使用集合锁，类型表另用字典锁；除空闲数量外的统计 getter 没有统一读取锁。GetAllReferencePoolInfos 返回新数组中的值快照，各计数可能采自不同时刻，不能视为跨池或单池的原子一致快照。ReferencePoolInfo 构造器仅保存 Type 和六个计数，不校验空类型、负数或一致性；默认结构体 Type 为 null。

## 关联文档

- [ReferencePool](../ReferencePool.md)
- [IReferenceHelper](../Interfaces/IReferenceHelper.md)
- [IReference](../IReference.md)
- [ReferencePoolInfo](../ReferencePoolInfo.md)
