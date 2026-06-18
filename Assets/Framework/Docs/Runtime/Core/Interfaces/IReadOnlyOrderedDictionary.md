# IReadOnlyOrderedDictionary\<TKey, TValue\>

**类签名**：`public interface IReadOnlyOrderedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>`
**命名空间**：`NovaFramework.Runtime`

只读型序列字典接口，提供对 `NovaOrderedDictionary` 的只读访问视图。通过 `NovaOrderedDictionary.AsReadOnly()` 获取该接口实例，防止外部修改字典内容。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `IReadOnlyOrderedDictionary.cs` | 接口定义 |

## 公开 API

```csharp
TValue this[TKey key] { get; }
IEnumerable<TKey> Keys { get; }
IEnumerable<TValue> Values { get; }
int Count { get; }
bool ContainsKey(TKey key);
bool TryGetValue(TKey key, out TValue value);
```

## 关联文档

- [Interfaces](Interfaces.md)
- [NovaOrderedDictionary](../Collections/NovaOrderedDictionary.md)
