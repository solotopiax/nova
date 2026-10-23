# NovaOrderedDictionary\<TKey, TValue\>

**类签名**：`public sealed class NovaOrderedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>`
**命名空间**：`NovaFramework.Runtime`

保持插入顺序的泛型字典，内部组合 `List<TKey>` 与 `Dictionary<TKey,TValue>`。遍历按键列表顺序返回元素；`AsReadOnly()` 首次创建并缓存 `IReadOnlyOrderedDictionary<TKey, TValue>` 实时包装。

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
| `Keys` | `IReadOnlyList<TKey>` | 底层键列表的实时视图 |
| `Values` | `Dictionary<TKey,TValue>.ValueCollection` | 底层字典值集合，不按键列表重新排序 |

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
Enumerator GetEnumerator();
```

Add 重复键抛异常，setter 允许覆盖且不改旧键顺序；新键才追加。缺键 getter 抛 KeyNotFoundException，TryGetValue 缺失返回 false/default，空键与比较器错误交字典处理。Add 先写字典后加键列表，setter 新键先加列表后写字典，任一步失败都没有回滚。Remove/Clear 不释放值资源。

CopyKeysTo/CopyValuesTo 拒绝 null，先清目标再复制，后者按键列表查值。不要把经类型转换取得的内部 Keys List 再作为 CopyKeysTo 目标，否则会清掉源键列表；只读包装不防止其他引用修改源。枚举器以实时列表索引查字典，没有版本保护，枚举时修改可能跳项、重复或提前结束；开始前/结束后读取 Current 会越界，接口形式可能装箱。需要稳定快照时由调用方在合适生命周期复制。

## 关联文档

- [Structures](Structures.md)
- [IReadOnlyOrderedDictionary](../Interfaces/IReadOnlyOrderedDictionary.md)
