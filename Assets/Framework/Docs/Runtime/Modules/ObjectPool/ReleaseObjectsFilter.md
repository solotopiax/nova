# ReleaseObjectsFilter\<T\>

**类签名**：`public delegate List<T> ReleaseObjectsFilter<T>(List<T> candidateObjects, int toReleaseCount, DateTime expireTime) where T : ObjectBase`
**命名空间**：`NovaFramework.Runtime`

释放对象筛选器委托，在对象池执行释放操作时从候选对象列表中筛选出实际需要释放的对象。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Managers/Definitions/ReleaseObjectsFilter.cs` | `ReleaseObjectsFilter<T>` | 委托定义 |

---

## §5 完整公开 API

```csharp
/// <typeparam name="T">对象类型。</typeparam>
/// <param name="candidateObjects">要筛选的对象集合。</param>
/// <param name="toReleaseCount">需要释放的对象数量。</param>
/// <param name="expireTime">对象过期参考时间。</param>
/// <returns>经筛选需要释放的对象集合。</returns>
public delegate List<T> ReleaseObjectsFilter<T>(
    List<T> candidateObjects,
    int toReleaseCount,
    DateTime expireTime)
    where T : ObjectBase;
```

---

## §11 使用示例

```csharp
// 使用自定义筛选器释放对象池
IObjectPool<MySoundObject> pool = Nova.ObjectPool.GetObjectPool<MySoundObject>();

pool.Release((candidates, count, expireTime) =>
{
    // 优先释放静音状态的对象
    var result = candidates
        .Where(o => o.IsMuted)
        .Take(count)
        .ToList();
    return result;
});
```

---

## §13 关联文档

- [IObjectPoolManager.md](IObjectPoolManager.md)
- [ObjectPool.md](ObjectPool.md)
