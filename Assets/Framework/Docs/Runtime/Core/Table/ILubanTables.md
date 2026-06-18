# ILubanTables

**类签名**：`public interface ILubanTables`
**命名空间**：`NovaFramework.Runtime`

Luban Tables 总管理器接口，框架层通过此接口操作游戏层 Luban 代码生成的 Tables 类。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Core/Table/ILubanTables.cs` | `ILubanTables` | 接口定义 |

---

## §5 完整公开 API

```csharp
public interface ILubanTables
{
    /// <summary>解析所有跨表引用。</summary>
    void ResolveRef();

    /// <summary>获取所有 ITable 实例。</summary>
    IReadOnlyList<ITable> GetAllTables();

    /// <summary>获取指定类型的 Luban 表实例，不存在时返回 null。</summary>
    T GetTable<T>() where T : class, ITable;

    /// <summary>根据类型获取 Luban 表实例，不存在时返回 null。</summary>
    ITable GetTable(Type type);
}
```

---

## §11 使用示例

```csharp
// 游戏层实现（Luban 代码生成）
public partial class TableTables : ILubanTables
{
    public TableTables(Func<string, JArray> loader)
    {
        TbHero = new TbHero(loader("tbhero"));
    }

    public void ResolveRef() => TbHero.ResolveRef(this);

    public IReadOnlyList<ITable> GetAllTables() => new ITable[] { TbHero };

    public T GetTable<T>() where T : class, ITable
        => m_Tables.TryGetValue(typeof(T), out var t) ? t as T : null;

    public ITable GetTable(Type type)
        => m_Tables.TryGetValue(type, out var t) ? t : null;
}
```

---

## §13 关联文档

- [ITable.md](ITable.md) — `GetAllTables()` 返回的元素类型
- [LubanTablesLoader.md](LubanTablesLoader.md) — 调用此接口的共享加载器
- [DataTableMode.md](DataTableMode.md) — 数据表模式枚举
- [../../Modules/Table/TableManager.md](../../Modules/Table/TableManager.md) — Table 模块消费者
- [../../Modules/Config/ConfigManager.md](../../Modules/Config/ConfigManager.md) — Config 模块消费者
