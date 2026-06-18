# NovaOrderedDictionary\<TKey, TValue\>

**类签名**：`public sealed class NovaOrderedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>`
**命名空间**：`NovaFramework.Runtime`

保持插入顺序的泛型字典，内部封装 `OrderedDictionary`。遍历时按照键值对的插入顺序返回元素。支持通过 `AsReadOnly()` 转换为 `IReadOnlyOrderedDictionary<TKey, TValue>` 只读视图。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `NovaOrderedDictionary.cs` | 序列字典类主体及内部 `ReadOnlyWrapper` |

## 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Count` | `int` | 字典中键值对的数量 |
| `this[TKey key]` | `TValue` | 按键获取或设置值 |
| `Keys` | `ICollection<TKey>` | 所有键的集合 |
| `Values` | `ICollection<TValue>` | 所有值的集合 |

## 公开 API

```csharp
// 增删
void Add(TKey key, TValue value);
bool Remove(TKey key);
void Clear();

// 查询
bool ContainsKey(TKey key);
bool TryGetValue(TKey key, out TValue value);

// 零分配复制（性能敏感场景使用，调用方复用 List）
void CopyKeysTo(List<TKey> result);
void CopyValuesTo(List<TValue> result);

// 转换
IReadOnlyOrderedDictionary<TKey, TValue> AsReadOnly();

// 枚举
IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator();
```

## 关联文档

- [Structures](Structures.md)
- [IReadOnlyOrderedDictionary](../Interfaces/IReadOnlyOrderedDictionary.md)
