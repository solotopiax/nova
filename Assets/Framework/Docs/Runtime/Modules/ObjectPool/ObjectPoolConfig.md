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
    public int    Capacity            { get; set; } = int.MaxValue;    // 容量上限
    public float  ExpireTime          { get; set; } = float.MaxValue;  // 对象过期秒数
    public int    Priority            { get; set; } = 0;               // 默认释放排序优先级（值越小越早被默认释放）
}
```

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
