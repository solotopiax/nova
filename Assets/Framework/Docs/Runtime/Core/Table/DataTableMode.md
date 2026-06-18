# DataTableMode

**类签名**：`public enum DataTableMode`
**命名空间**：`NovaFramework.Runtime`

数据表模式枚举，由 Luban 生成的 `ITable` 实现类通过 `Mode` 属性返回，描述运行时加载结构。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Runtime/Core/Table/DataTableMode.cs` | `DataTableMode` | 枚举定义 |

---

## §5 完整公开 API

```csharp
public enum DataTableMode
{
    [InspectorName("List")]
    List,   // 列表模式，运行时反序列化为 List<T>

    [InspectorName("Map")]
    Map,    // 映射模式，运行时反序列化为 Dictionary<TKey, T>

    [InspectorName("One")]
    One,    // 单例模式，运行时反序列化为单个 T 实例
}
```

---

## §11 使用示例

```csharp
ITable table = /* ... */;
switch (table.Mode)
{
    case DataTableMode.List: break;   // 数据为列表
    case DataTableMode.Map:  break;   // 数据为字典，键由 IndexField 决定
    case DataTableMode.One:  break;   // 数据为单个实例
}
```

---

## §13 关联文档

- [ITable.md](ITable.md) — 含 `Mode` 属性的表格接口
- [IDataTableUnitSetting.md](IDataTableUnitSetting.md) — 含 `Mode` 和 `IndexField` 的单元设置接口
- [../../Modules/Table/TableManager.md](../../Modules/Table/TableManager.md) — 运行时加载器使用此枚举
