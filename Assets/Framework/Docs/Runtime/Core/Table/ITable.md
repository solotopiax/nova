# ITable

**类签名**：`public interface ITable`
**命名空间**：`NovaFramework.Runtime`

表格容器接口，由 Luban 代码生成工具生成的 TbXxx 类实现。框架通过此接口统一持有所有表格实例。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Core/Table/ITable.cs` | `ITable` | 基础接口（仅 `Mode` 属性） |
| | `ITable<TData>` | 带强类型数据行的表格接口（List / Map 通用基接口） |
| | `ITableMap<TKey, TData>` | 映射模式表格接口，支持按键查询 |

---

## §5 完整公开 API

```csharp
public interface ITable
{
    /// <summary>
    /// 表格模式（List / Map / One）。
    /// </summary>
    DataTableMode Mode { get; }
}

public interface ITable<out TData> : ITable
{
    /// <summary>
    /// 全部数据行列表。
    /// </summary>
    IReadOnlyList<TData> DataList { get; }

    /// <summary>
    /// 按下标获取数据行。
    /// </summary>
    TData this[int index] { get; }
}

public interface ITableMap<TKey, TData> : ITable<TData>
{
    /// <summary>
    /// 全部键值对映射。
    /// </summary>
    IReadOnlyDictionary<TKey, TData> DataMap { get; }

    /// <summary>
    /// 按键获取数据行，键不存在时抛异常。
    /// </summary>
    TData Get(TKey key);

    /// <summary>
    /// 按键获取数据行，键不存在时返回 default。
    /// </summary>
    TData GetOrDefault(TKey key);

    /// <summary>
    /// 按键获取数据行。
    /// </summary>
    TData this[TKey key] { get; }
}
```

> `ITable<out TData>` 声明 `TData` 为协变（out），允许 `ITable<DerivedRow>` 安全转型为 `ITable<BaseRow>`，消除反射。

---

## §11 使用示例

```csharp
// 获取表格并检查其模式（一般不直接操作 ITable，而是通过 GetTable<T> 获取具体类型）
ITable table = Nova.Table.GetTable(typeof(TbHero));
if (table != null)
{
    Log.Debug(LogTag.Table, "TbHero 模式：{0}", table.Mode);
}

// 典型用法：通过泛型 GetTable 直接获取具体类型
TbHero heroTable = Nova.Table.GetTable<TbHero>();

// 使用 ITableMap 接口按键查询
ITableMap<int, DtHero> mapTable = heroTable as ITableMap<int, DtHero>;
DtHero hero = mapTable.Get(1001);
```

---

## §13 关联文档

- [ILubanTables.md](ILubanTables.md)（`GetAllTables()` 返回 `IReadOnlyList<ITable>`）
- [DataTableMode.md](DataTableMode.md)（`DataTableMode` 枚举定义）
- [LubanTablesLoader.md](LubanTablesLoader.md)（反射构造 Tables 并提取 ITable）
- [ITableManager.md](../../Modules/Table/Interfaces/ITableManager.md)（泛型约束 `where T : class, ITable`）
- [TableManager.md](../../Modules/Table/TableManager.md)（持有 `Dictionary<Type, ITable>`）
