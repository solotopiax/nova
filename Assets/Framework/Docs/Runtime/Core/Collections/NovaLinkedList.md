# NovaLinkedList\<T\>

**类签名**：`public sealed class NovaLinkedList<T> : ICollection<T>, IEnumerable<T>, ICollection, IEnumerable`
**命名空间**：`NovaFramework.Runtime`

带节点缓存的泛型链表类。内部封装 `LinkedList<T>`，在节点移除时将其回收至缓存队列，下次添加时优先复用已缓存节点，从而减少 GC 分配开销。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `NovaLinkedList.cs` | 链表类主体及内部 `Enumerator` 结构体 |

## 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Count` | `int` | 链表中实际包含的节点数量 |
| `CachedNodeCount` | `int` | 节点缓存池中的节点数量（Clear 时上限 256，超出直接丢弃） |
| `First` | `LinkedListNode<T>` | 链表第一个节点 |
| `Last` | `LinkedListNode<T>` | 链表最后一个节点 |
| `IsReadOnly` | `bool` | 是否为只读集合 |
| `IsSynchronized` | `bool` | 是否线程安全 |
| `SyncRoot` | `object` | 同步访问对象 |

## 公开 API

```csharp
// 添加
void Add(T value);
LinkedListNode<T> AddAfter(LinkedListNode<T> node, T value);
void AddAfter(LinkedListNode<T> node, LinkedListNode<T> newNode);
LinkedListNode<T> AddBefore(LinkedListNode<T> node, T value);
void AddBefore(LinkedListNode<T> node, LinkedListNode<T> newNode);
LinkedListNode<T> AddFirst(T value);
void AddFirst(LinkedListNode<T> node);
LinkedListNode<T> AddLast(T value);
void AddLast(LinkedListNode<T> node);

// 移除
bool Remove(T value);
void Remove(LinkedListNode<T> node);
void RemoveFirst();
void RemoveLast();

// 查找
bool Contains(T value);
LinkedListNode<T> Find(T value);
LinkedListNode<T> FindLast(T value);

// 清理
void Clear();
void ClearCachedNodes();

// 复制
void CopyTo(T[] array, int index);
void CopyTo(Array array, int index);

// 枚举
Enumerator GetEnumerator();
```

## 关联文档

- [Structures](Structures.md)
- [NovaLinkedListRange](NovaLinkedListRange.md)
- [NovaMultiDictionary](NovaMultiDictionary.md)
