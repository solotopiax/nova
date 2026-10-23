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

只读指通过此接口不能写入，不表示不可变快照或线程安全。当前 `NovaOrderedDictionary.AsReadOnly()` 返回原字典的实时包装：`Keys` 沿原插入顺序，`Values` 直接返回底层字典值集合，不能用二者的位置配对；需要有序键值对时枚举接口自身。索引器、`ContainsKey` 和 `TryGetValue` 都转发原字典；缺失键的索引访问会抛异常，尝试读取失败时返回 `false` 并给出类型默认值。空键和比较器错误沿底层字典传播；这些具体行为来自默认包装实现，接口声明本身没有新增守卫。

## 关联文档

- [Interfaces](Interfaces.md)
- [NovaOrderedDictionary](../Collections/NovaOrderedDictionary.md)
