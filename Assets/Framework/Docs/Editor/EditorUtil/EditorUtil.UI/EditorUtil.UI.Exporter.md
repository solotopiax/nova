# EditorUtil.UI.Exporter

**类签名**：`public static class Exporter`（嵌套于 `EditorUtil.UI`）
**命名空间**：`NovaFramework.Editor`

UI 模块 Luban 导出入口，提供全量、仅代码、仅数据及单文件四种导出路径；与 Inspector 共用同一套 Pipeline。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.UI/EditorUtil.UI.Exporter.cs` | `EditorUtil.UI.Exporter` | UI 导出工具类 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.UI (public static partial class)
        └── Exporter (public static class)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_ExportTargetName` | `const string` | `"ui"` | Luban target 名称 |
| `c_ExportManagerName` | `const string` | `"UITables"` | Luban manager 类名 |

---

## §5 完整公开 API

```csharp
// 全量导出（代码 + 数据）：清理旧目录后一次性生成
// settings 为 null / UIUnitsSettings 为空时静默返回
public static void ExportAll(UISettings settings, string sourceDirPath);

// 全量仅导出代码（类型定义），不清理旧目录
// settings 为 null 或 sourceDirPath 为空时静默返回
public static void ExportCode(UISettings settings, string sourceDirPath);

// 全量仅导出数据（JSON），不清理旧目录
// settings 为 null 或 sourceDirPath 为空时静默返回
public static void ExportData(UISettings settings, string sourceDirPath);

// 单文件代码导出：为指定文件生成 Luban 代码；classExportPath 空时静默返回
public static void ExportCodeForFile(UISettings settings, string sourceDirPath, string filePath, string classExportPath);

// 单文件数据导出：为指定文件执行 Luban 数据导出；未找到对应 UIUnitSetting 时 Log.Error 并返回
public static void ExportDataForFile(UISettings settings, string sourceDirPath, string filePath);
```

---

## §9 关键算法

### ExportAll 流程

```
ExportAll(settings, sourceDirPath):
  1. settings == null || UIUnitsSettings 为空 → return
  2. ClearExportPaths(settings)：去重 DatasExportPath / ClassesExportPath → DeletePath
  3. CollectFirstClassExportPath(settings) → classExportPath（多路径时 Log.Warning）
  4. BuildExportContext(sourceDirPath, settings, "ui", "UITables") → ctx
  5. ctx.OutputCodeDir = classExportPath
  6. Pipeline.ExportAll(ctx)
```

### ExportCodeForFile / ExportDataForFile

- `ExportCodeForFile`：计算文件相对路径，查找对应 UIUnitSetting（可为 null），构建 ctx 时附加 `RelevantFileNames` 与 `TargetUnit`，调用 `Pipeline.ExportCode`
- `ExportDataForFile`：与上类似，但若 UIUnitSetting 未找到则 `Log.Error` 并直接返回；找到后设置 `ctx.TargetUnit` 调用 `Pipeline.ExportData`

---

## §11 使用示例

```csharp
// UIComponentInspector 中全量导出
EditorUtil.UI.Exporter.ExportAll(settings, sourceDirPath);

// Pipify Step 中仅导出数据
EditorUtil.UI.Exporter.ExportData(settings, sourceDirPath);

// 文件树监听到单文件变化时仅重新导该文件代码
EditorUtil.UI.Exporter.ExportCodeForFile(settings, sourceDirPath, changedFilePath, classExportPath);
```

---

## §13 关联文档

- [UISettings.md](../../../Runtime/Modules/UI/UIManager/Definitions/UISettings.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.ExportHelper.md](../EditorUtil.Luban/EditorUtil.Luban.ExportHelper.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
