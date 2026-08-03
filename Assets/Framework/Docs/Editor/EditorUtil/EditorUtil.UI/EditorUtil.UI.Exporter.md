# EditorUtil.UI.Exporter

**类签名**：`public static class Exporter`（嵌套于 `EditorUtil.UI`）

**命名空间**：`NovaFramework.Editor`

`EditorUtil.UI.Exporter` 是 UI 模块导出的公共门面。Inspector、Pipify 和其他 Editor 调用方只依赖这里的稳定 API；Excel 校验、Luban 流程、暂存发布与失败回滚统一交给 DataPipeline 中的 UI 专用实现。

## 调用关系

```text
Inspector / Pipify
  -> EditorUtil.UI.Exporter（公共门面）
  -> UIExporter（内部校验与导出编排）
  -> Luban.GeneratedOutput（C# 所有权与安全清理）
  -> FileSystem.OutputApplier（正式产物应用与失败回滚）
```

公共门面不保存状态，也不解释 UI Excel 结构。

## 公开 API

```csharp
public static bool ExportAll(UISettings settings, string sourceDirPath);
public static bool ExportCode(UISettings settings, string sourceDirPath);
public static bool ExportData(UISettings settings, string sourceDirPath);
public static bool ExportCodeForFile(
    UISettings settings,
    string sourceDirPath,
    string filePath,
    string classExportPath);
public static bool ExportDataForFile(
    UISettings settings,
    string sourceDirPath,
    string filePath);
```

- `ExportAll`：导出代码和数据。
- `ExportCode`：仅更新代码；正式表格数据只作为 Map 生成输入，不会被修改。
- `ExportData`：仅更新所选 JSON/Binary 数据。

格式来自 `UISettings.DataFormat`，默认 JSON。Binary 使用 `.bytes`；数据发布时会在同一事务中删除同名反格式文件及其 `.meta`。
- `ExportCodeForFile` / `ExportDataForFile`：只发布精确匹配 Unit 的产物，不退化为全量发布。
- 所有方法返回是否成功；Pipify 收到 `false` 时中止当前流水线。

## 使用示例

```csharp
EditorUtil.UI.Exporter.ExportAll(settings, sourceDirPath);
EditorUtil.UI.Exporter.ExportData(settings, sourceDirPath);
EditorUtil.UI.Exporter.ExportCodeForFile(
    settings,
    sourceDirPath,
    changedFilePath,
    classExportPath);
```

## 边界

- UI 表结构与路径校验参见 `UIExporter`。
- `_temp/_publish`、正式文件替换和回滚参见 `FileSystem.OutputApplier`。
- UI 导出是模块深度定制流程，不作为 Table 通用表格导出的变体，也不要求其他模块接入相同分层。

## 关联文档

- [UIExporter.md](../../DataPipeline/Implements/UIs/UIExporter.md)
- [EditorUtil.FileSystem.OutputApplier.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [UISettings.md](../../../Runtime/Modules/UI/UIManager/Definitions/UISettings.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
