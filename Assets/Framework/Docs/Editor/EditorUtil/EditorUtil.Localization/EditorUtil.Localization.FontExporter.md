# EditorUtil.Localization.FontExporter

**类签名**：`public static class FontExporter`（嵌套于 `EditorUtil.Localization`）

**命名空间**：`NovaFramework.Editor`

本地化字体 Excel 使用 Luban 原生表结构，不需要文本导出的多语言 PreFilter；但它与其他非 Table 模块一样，先在源目录的 `_temp/_publish` 完成生成和验证，再一次性发布正式数据与 C#。

## 公开 API

```csharp
public static bool ExportFontAll(
    LocalizationSettings settings,
    string sourceDirPath,
    string classExportPath);

public static bool ExportFontCode(
    LocalizationSettings settings,
    string sourceDirPath,
    string classExportPath);

public static bool ExportFontData(
    LocalizationSettings settings,
    string sourceDirPath);
```

公开签名和 Inspector/Pipify 调用方式未变化。`ExportFontAll` 在 `classExportPath` 为空时只导出数据。

## 导出流程

```text
校验 FontUnitsSettings、源目录和输出路径
  -> 获取源目录 _temp 工作区租约并清理旧临时内容
  -> 克隆字体 Settings，把数据/C# 输出改写到 _temp/_publish
  -> 仅代码导出时复制正式数据，供 Map 属性生成读取
  -> Luban Pipeline 生成数据、类型和 SchemaManifest
  -> 验证全部预期数据/C#
  -> Luban.GeneratedOutput 写入第一行单行所有权标记
  -> 登记同名反格式数据及其 .meta 删除项
  -> FileSystem.OutputApplier 一次发布全部正式产物
  -> finally 清理 _temp 并刷新 AssetDatabase
```

Pipeline、产物验证或应用任一步失败时，正式产物保持不变或回滚。全量代码清理只删除 Profile、Source 和正文 Hash 均匹配的过期文件；无标记、跨归属或人工修改文件保留并报警。

字体数据采用 JSON/Binary 二选一发布。成功生成 `.json` 时，同一事务删除同名 `.bytes` 及其 `.meta`；成功生成 `.bytes` 时反向删除同名 `.json` 及其 `.meta`。仅代码导出不改变正式数据文件。

## 关联文档

- [EditorUtil.Localization.TextExporter.md](EditorUtil.Localization.TextExporter.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.GeneratedOutput.md](../EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [EditorUtil.FileSystem.OutputApplier.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
