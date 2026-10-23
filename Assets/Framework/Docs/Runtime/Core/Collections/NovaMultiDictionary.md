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

## 行为与边界

同键值允许重复，也允许空/default 值；Contains/Remove(value) 用默认相等器，Remove 只删首个匹配。每组以 default 值哨兵结束，删除最后值时同时删除哨兵和主键；RemoveAll 连哨兵一起移除，Clear 使所有旧范围失效。Count 是主键数，Keys 与取得的范围都是实时引用；缺键索引器返回 null，TryGetValue 返回 false 且 out 为 null，不能直接读 IsValid。空键和比较器错误沿字典传播，分配/登记失败无事务回滚，删除不释放值资源。

主键枚举包装底层字典，无主键排序承诺，修改字典可触发版本异常；每个值范围则没有版本检查，节点复用会影响旧范围。接口枚举可能装箱，显式 Reset 只复位底层结构体副本，不回写原字段。

## 关联文档

- [Structures](Structures.md)
- [NovaLinkedList](NovaLinkedList.md)
- [NovaLinkedListRange](NovaLinkedListRange.md)
