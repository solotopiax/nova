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

## 行为与边界

值重载允许 `null/default(T)` 与重复元素，比较沿默认相等器；节点重载直接使用外部节点，不经过缓存。锚点必须属于本链，插入节点必须尚未挂链，错误沿 BCL 抛出。AddAfter/AddBefore 的值重载会先取缓存节点再验证锚点，插入失败不归还已取出的缓存节点。Remove(value) 仅删除首个匹配，空链 RemoveFirst/Last 抛异常；CopyTo 的空数组、秩、类型、范围与容量交底层校验。

移除和 Clear 把节点 Value 清为默认值并最多缓存 256 个节点，不释放元素关联资源；ClearCachedNodes 只丢缓存，不改变活跃链。First/Last/Find 返回活节点，脱链或清理后可能被复用，持有者不能继续用旧节点代表原元素。容器不自动加锁，SyncRoot 只是同步对象。枚举不是快照，MoveNext 沿底层版本检查；接口形式可能装箱。显式 Reset 把底层结构体枚举器装箱后复位副本，不回写原字段，不应依赖它重置当前枚举器。

## 关联文档

- [Structures](Structures.md)
- [NovaLinkedListRange](NovaLinkedListRange.md)
- [NovaMultiDictionary](NovaMultiDictionary.md)
