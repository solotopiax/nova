# TableSettings / DataTableMode / TableUnitSetting

**命名空间**：`NovaFramework.Runtime`

本文件定义了表格系统的三个配置类型：`DataTableMode` 枚举、`TableSettings` 主设置类和 `TableUnitSetting` 单表格单元设置类。它们共同描述表格的 Excel 来源路径、导出目标、AB 资源路径及数据表加载模式。命名空间由 `IConfigManager.Namespace` 统一提供，不再在 `TableSettings` 中存储。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Managers/Definitions/TableSettings.cs` | 枚举、设置类、单元设置类定义 |

---

## DataTableMode

**类签名**：`public enum DataTableMode`

数据表模式枚举，决定 JSON 数据的根结构。

| 枚举值 | InspectorName | 说明 |
|--------|--------------|------|
| `List` | `List` | JSON 根值为数组 |
| `Map` | `Map` | JSON 根值为对象（以 IndexField 字段值为 key） |
| `One` | `One` | JSON 根值为单个对象 |

---

## TableSettings

**类签名**：`[Serializable] public class TableSettings`

表格整体设置，包含编辑器 Excel 目录路径和所有单表格单元设置列表。由 `TableComponent` 在 Inspector 中序列化持有。

### 关键字段/属性

| 字段 | 类型 | 条件 | 说明 |
|------|------|------|------|
| `SourceDirPath` | `string` | `UNITY_EDITOR` | 数据源目录路径（`[FormerlySerializedAs("ExcelDirPath")]`） |
| `TableUnitsSettings` | `List<TableUnitSetting>` | — | 所有单表格单元设置列表 |

> 模板路径不再序列化存储，由 Inspector 通过 `EditorUtil.FileSystem.ResolveTemplatePath` 动态计算（自动适配消费者 UPM 模式与开发者模式）。

---

## TableUnitSetting

**类签名**：`[Serializable] public class TableUnitSetting`

单个 Excel 表格的单元设置，描述该表格的导出路径、AB 资源路径、Luban 加载模式、映射索引字段及数据类型列表。

### 关键字段/属性

| 字段 | 类型 | 默认值 | 条件 | 说明 |
|------|------|--------|------|------|
| `SourcePath` | `string` | `""` | `UNITY_EDITOR` | 相对表格目录的数据源文件相对路径（`[FormerlySerializedAs("ExcelPath")]`） |
| `DatasExportPath` | `string` | `""` | `UNITY_EDITOR` | 该数据源的导出数据路径 |
| `ClassesExportPath` | `string` | `""` | `UNITY_EDITOR` | 该数据源的导出类型定义路径 |
| `TableMode` | `DataTableMode` | `DataTableMode.List` | — | 表格模式（列表 / 映射 / 单例），`[FormerlySerializedAs("ExportMode")]` |
| `IndexField` | `string` | `"ID"` | — | 映射模式的索引字段名（仅 Map 模式使用） |
| `AssetLocation` | `string` | `""` | — | 表格资源的 Asset 地址 |
| `DataTypeNames` | `List<string>` | `new List<string>()` | — | 表格数据类型短名列表（如 `"HeroData"`，即 Excel Sheet 名），一个 JSON 可包含多个类型。`LubanConfigManager` 生成 `__tables__.xml` 时自动为 Bean 类名添加 `Dt` 前缀（如 `"DtHeroData"`），Table 容器类名添加 `Tb` 前缀（如 `"TbHeroData"`） |

## 公开 API

```csharp
public enum DataTableMode
{
    [InspectorName("List")]  List,
    [InspectorName("Map")]   Map,
    [InspectorName("One")]   One,
}

[Serializable]
public class TableSettings : IDataTableSettings
{
    // UNITY_EDITOR only
    [FormerlySerializedAs("ExcelDirPath")]
    public string SourceDirPath;
    string IDataTableSettings.SourceDirPath => SourceDirPath;

    public List<TableUnitSetting> TableUnitsSettings = new List<TableUnitSetting>();
    IReadOnlyList<IDataTableUnitSetting> IDataTableSettings.Units => TableUnitsSettings;
}

[Serializable]
public class TableUnitSetting
{
    // UNITY_EDITOR only
    public string SourcePath;
    public string DatasExportPath;
    public string ClassesExportPath;

    [FormerlySerializedAs("ExportMode")]
    public DataTableMode TableMode = DataTableMode.List;
    public string IndexField = "ID";
    public string AssetLocation;
    public List<string> DataTypeNames = new List<string>();
}
```

## 关联文档

- [TableManagerConfig.md](TableManagerConfig.md)（持有 `UnitSettings`）
- [TableComponent.md](../TableComponent.md)（Inspector 中序列化 `TableSettings`）
- [TableManager.md](../TableManager.md)（运行时根据设置加载数据）
- [ILubanTables.md](../../../Core/Table/ILubanTables.md)（`TableTables` 容器接口）
