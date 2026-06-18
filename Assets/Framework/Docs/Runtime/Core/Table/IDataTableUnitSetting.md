# IDataTableUnitSetting

**类签名**：`public interface IDataTableUnitSetting`
**命名空间**：`NovaFramework.Runtime`

数据表单元设置接口，每个数据源文件对应一个单元，供 Luban 导出流水线统一消费。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Core/Table/IDataTableUnitSetting.cs` | `IDataTableUnitSetting` | 接口定义 |

---

## §5 完整公开 API

```csharp
public interface IDataTableUnitSetting
{
#if UNITY_EDITOR
    string SourcePath { get; }        // 相对数据源目录的文件路径
    string DatasExportPath { get; }   // 导出数据文件的目标路径
    string ClassesExportPath { get; } // 导出类型定义文件的目标路径
    string LubanInputPath { get; }    // Luban __tables__.xml input 路径；Table=SourcePath，Config="_temp/"+文件名
#endif
    string AssetLocation { get; }     // 资源的 Asset 地址
    DataTableMode Mode { get; }       // 数据表模式（List / Map / One）
    string IndexField { get; }        // 映射模式索引字段名（仅 Map 模式使用）
    IReadOnlyList<string> DataTypeNames { get; }  // 数据类型短名称列表（不含命名空间）
}
```

---

## §11 使用示例

```csharp
// Editor 流水线中逐单元导出
foreach (IDataTableUnitSetting unit in settings.Units)
{
#if UNITY_EDITOR
    string inputPath = unit.LubanInputPath;
    string outputPath = unit.DatasExportPath;
#endif
    string assetLocation = unit.AssetLocation;    // 运行时加载用
}
```

---

## §13 关联文档

- [IDataTableSettings.md](IDataTableSettings.md)
