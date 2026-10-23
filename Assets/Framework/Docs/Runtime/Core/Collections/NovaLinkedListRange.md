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

## 行为与边界

这是引用类型，`default(NovaLinkedListRange<T>)` 为 `null`，不是可以直接读取 IsValid 的空结构体。构造仅拒绝空节点和相同节点，不检查同链、仍挂载或 Terminal 可达；IsValid 也只检查这三个条件。Count、Contains 和枚举沿 Next 直到排他 Terminal 或链尾，Count 为线性计算，无节点版本保护。源链删除/复用节点后，旧范围不再代表原值序列。

范围枚举器构造时检查 IsValid，但传 null 会先解引用失败；开始前 Current 为类型默认值，结束后保留最后值，没有额外状态校验。Reset 把游标移回原 First，Dispose 为空，都不释放节点或元素资源；通过接口获取结构体枚举器可能装箱。

## 关联文档

- [Structures](Structures.md)
- [NovaLinkedList](NovaLinkedList.md)
- [NovaMultiDictionary](NovaMultiDictionary.md)
