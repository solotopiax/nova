# NovaLinkedSet\<T\>

**类签名**：`public sealed class NovaLinkedSet<T> : IEnumerable<T>`
**命名空间**：`NovaFramework.Runtime`

有序去重集合，内部使用 `LinkedList<T>` 维护插入顺序，同时使用 `Dictionary<T, LinkedListNode<T>>` 保证元素唯一性和 O(1) 查找/删除性能。适用于需要保持插入顺序且不允许重复元素的场景。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `NovaLinkedSet.cs` | Set 容器类主体 |

## 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Count` | `int` | 集合中的元素数量 |

## 公开 API

```csharp
// 构造
NovaLinkedSet();
NovaLinkedSet(IEqualityComparer<T> comparer);

// 增删
bool Add(T t);
bool Remove(T t);
void Clear();

// 查询
bool Contains(T t);

// 枚举
IEnumerator<T> GetEnumerator();
```

## 关联文档

- [Structures](Structures.md)
