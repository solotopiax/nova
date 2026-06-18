# NovaLinkedListRange\<T\>

**类签名**：`public sealed class NovaLinkedListRange<T> : IEnumerable<T>, IEnumerable`
**命名空间**：`NovaFramework.Runtime`

表示链表中一段连续节点的范围，由起始节点（First）和终结标记节点（Terminal）界定。终结标记节点本身不包含在范围内。常与 `NovaMultiDictionary` 搭配使用，用于标识同一主键下的值序列。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `NovaLinkedListRange.cs` | 链表范围类主体及内部 `Enumerator` 结构体 |

## 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `IsValid` | `bool` | 范围是否有效（First 和 Terminal 非空且不相同） |
| `First` | `LinkedListNode<T>` | 范围的起始节点 |
| `Terminal` | `LinkedListNode<T>` | 范围的终结标记节点（不包含在范围内） |
| `Count` | `int` | 范围内的节点数量 |

## 公开 API

```csharp
// 构造
NovaLinkedListRange(LinkedListNode<T> first, LinkedListNode<T> terminal);

// 查询
bool Contains(T value);

// 枚举
Enumerator GetEnumerator();
```

## 关联文档

- [Structures](Structures.md)
- [NovaLinkedList](NovaLinkedList.md)
- [NovaMultiDictionary](NovaMultiDictionary.md)
