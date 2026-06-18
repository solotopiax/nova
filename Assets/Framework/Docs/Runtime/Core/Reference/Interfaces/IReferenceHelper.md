# IReferenceHelper

**类签名**：`public interface IReferenceHelper`
**命名空间**：`NovaFramework.Runtime`

引用助手接口，定义了引用池的核心操作契约：获取、归还、追加、移除引用以及查询引用池信息。`ReferencePool` 静态门面通过该接口委托实际的引用管理逻辑，支持替换底层实现。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Interfaces/IReferenceHelper.cs` | 接口定义 |

## 公开 API

```csharp
void Initialize(bool strictCheck);
void ClearAll();
int GetPoolCount();

// 获取与归还
T Get<T>() where T : class, IReference, new();
IReference Get(Type referenceType);
void Put(IReference reference);

// 追加
void Add<T>(int count) where T : class, IReference, new();
void Add(Type referenceType, int count);

// 移除
void Remove<T>(int count) where T : class, IReference;
void Remove(Type referenceType, int count);
void RemoveAll<T>() where T : class, IReference;
void RemoveAll(Type referenceType);

// 信息查询
IReadOnlyList<ReferencePoolInfo> GetAllReferencePoolInfos();
```

## 关联文档

- [ReferencePool](../ReferencePool.md)
- [IReference](../IReference.md)
- [ReferenceHelper](../Implements/ReferenceHelper.md)
