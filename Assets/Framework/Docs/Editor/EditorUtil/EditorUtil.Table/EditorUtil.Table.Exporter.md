# EditorUtil.Table.Exporter

**类签名**：`public static class Exporter`（嵌套于 `EditorUtil.Table`）
**命名空间**：`NovaFramework.Editor`

表格模块 Luban 导出入口，封装清理旧文件、构建上下文、调用 Pipeline 三步流程。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.cs` | `EditorUtil.Table.Exporter` | Table 导出工具类 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Table (public static partial class)
        └── Exporter (public static class)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_ExportTargetName` | `const string` | `"table"` | Luban target 名称 |
| `c_ExportManagerName` | `const string` | `"TableTables"` | Luban manager 类名 |

---

## §5 完整公开 API

```csharp
// 导出全部（代码 + 数据）：清理旧导出文件 → 构建上下文 → Pipeline.ExportAll
// settings 为 null 或 sourceDirPath 为空时返回 false
public static bool ExportAll(TableSettings settings, string sourceDirPath);

// 仅导出代码（类型定义）：构建上下文 → Pipeline.ExportCode
// settings 为 null 或 sourceDirPath 为空时返回 false
public static bool ExportCode(TableSettings settings, string sourceDirPath);

// 仅导出数据（JSON）：构建上下文 → Pipeline.ExportData
// settings 为 null 或 sourceDirPath 为空时返回 false
public static bool ExportData(TableSettings settings, string sourceDirPath);
```

---

## §9 关键算法

### ExportAll 流程

```
ExportAll(settings, sourceDirPath):
  1. settings == null || sourceDirPath 空 → return false
  2. ClearExportPaths(settings)：收集所有 DatasExportPath / ClassesExportPath → 去重后逐目录 DeletePath
  3. CollectFirstClassExportPath(settings) → classExportPath（多路径时 Log.Warning）
  4. BuildExportContext(sourceDirPath, settings, "table", "TableTables") → ctx
  5. ctx.OutputCodeDir = classExportPath
  6. Pipeline.ExportAll(ctx) → return result
```

### ExportCode / ExportData 流程

- `ExportCode`：跳过 ClearExportPaths，直接 BuildExportContext → `ctx.OutputCodeDir = classExportPath` → `Pipeline.ExportCode(ctx)`
- `ExportData`：跳过 ClearExportPaths 与 classExportPath 解析，直接 BuildExportContext → `Pipeline.ExportData(ctx)`

---

## §11 使用示例

```csharp
// TableComponentInspector 中导出全部
bool ok = EditorUtil.Table.Exporter.ExportAll(settings, settings.SourceDirPath);

// Pipify Step 中仅导出数据（不重新生成代码）
bool ok = EditorUtil.Table.Exporter.ExportData(settings, settings.SourceDirPath);

// Pipify Step 中仅导出类型（不重新导数据）
bool ok = EditorUtil.Table.Exporter.ExportCode(settings, settings.SourceDirPath);
```

---

## §13 关联文档

- [TableSettings.md](../../../Runtime/Modules/Table/Definitions/TableSettings.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.ExportHelper.md](../EditorUtil.Luban/EditorUtil.Luban.ExportHelper.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
