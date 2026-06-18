# NovaMultiDictionary\<TKey, TValue\>

**类签名**：`public sealed class NovaMultiDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, NovaLinkedListRange<TValue>>>, IEnumerable`
**命名空间**：`NovaFramework.Runtime`

多值字典，允许同一主键对应多个值。内部使用 `NovaLinkedList<TValue>` 存储所有值，使用 `Dictionary<TKey, NovaLinkedListRange<TValue>>` 维护每个主键到其值范围的映射。适用于事件订阅、分组数据等一对多场景。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `NovaMultiDictionary.cs` | 多值字典类主体及内部 `Enumerator` 结构体 |

## 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Count` | `int` | 字典中主键的数量 |
| `Keys` | `IReadOnlyCollection<TKey>` | 所有主键的只读集合（来自内部 `Dictionary.Keys`） |
| `this[TKey key]` | `NovaLinkedListRange<TValue>` | 按主键获取对应的值范围（不存在时返回 null） |

## 公开 API

```csharp
// 属性
int Count { get; }
IReadOnlyCollection<TKey> Keys { get; }
NovaLinkedListRange<TValue> this[TKey key] { get; }

// 增删
void Add(TKey key, TValue value);
bool Remove(TKey key, TValue value);
bool RemoveAll(TKey key);
void Clear();

// 查询
bool Contains(TKey key);
bool Contains(TKey key, TValue value);
bool TryGetValue(TKey key, out NovaLinkedListRange<TValue> range);

// 枚举
Enumerator GetEnumerator();
```

## 关联文档

- [Structures](Structures.md)
- [NovaLinkedList](NovaLinkedList.md)
- [NovaLinkedListRange](NovaLinkedListRange.md)
