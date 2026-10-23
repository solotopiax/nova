# ObjectPoolConfig

**类签名**：`public class ObjectPoolConfig`
**命名空间**：`NovaFramework.Runtime`

对象池创建配置类，封装创建对象池时的全部参数，替代多参数组合重载。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Managers/Definitions/ObjectPoolConfig.cs` | `ObjectPoolConfig` | 配置类定义 |

---

## §5 完整公开 API

```csharp
public class ObjectPoolConfig
{
    public string Name                { get; set; } = string.Empty;    // 对象池名称
    public float  AutoReleaseInterval { get; set; } = float.MaxValue;  // 自动释放间隔（秒）
    public int    Capacity            { get; set; } = int.MaxValue;    // 保留容量，受保护对象可超出
    public float  ExpireTime          { get; set; } = float.MaxValue;  // 对象过期秒数
    public int    Priority            { get; set; } = 0;               // 池优先级，全局释放按升序遍历池
}
```

这些属性是无校验的自动属性。Manager 创建入口的 `config = null` 使用上面的默认配置；创建时复制各项值，后续修改配置对象不会同步到已创建池，运行时应修改 `IObjectPool<T>` 的对应属性。

`Priority` 控制 Manager 的 `Release()` / `ReleaseAllUnused()` 遍历多个池的先后顺序，不控制池内对象顺序。池内默认释放排序使用 `ObjectBase.Priority`，可通过 `SetPriority(obj, priority)` / `SetPriority(target, priority)` 修改，数值较小的对象先被选中。

---

## §11 使用示例

```csharp
// 最简创建（全部使用默认值）
IObjectPool<MySoundObject> pool =
    Nova.ObjectPool.CreateSingleGettingObjectPool<MySoundObject>();

// 指定名称和容量
IObjectPool<MySoundObject> pool =
    Nova.ObjectPool.CreateSingleGettingObjectPool<MySoundObject>(new ObjectPoolConfig
    {
        Name = "SFX",
        Capacity = 32,
        AutoReleaseInterval = 60f,
        ExpireTime = 120f,
    });
```

---

## §13 关联文档

- [IObjectPoolManager.md](IObjectPoolManager.md)
- [ObjectPoolComponent.md](ObjectPoolComponent.md)

## 参数与所有权边界

所有 getter/setter 只存 DTO 值，不验证范围；负数/NaN 可进入构造，已有池不受后续改 DTO 影响。Name 为 null 建池时归 empty；AutoReleaseInterval/ExpireTime 以秒计，float.MaxValue 为默认，不能把 0 理解为禁用。运行时 Capacity/ExpireTime setter 的非负检查及立即 Release 不适用于构造阶段。
