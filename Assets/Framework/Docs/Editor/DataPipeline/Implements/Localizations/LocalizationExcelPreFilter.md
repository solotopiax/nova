# LocalizationExcelPreFilter

**类签名**：`internal static class LocalizationExcelPreFilter`

**命名空间**：`NovaFramework.Editor`

Localization 不能把原始多语言列直接交给 Luban。当前实现由 PreFilter 内部的 `SourceModel` 一次读取并验证配置源，再把内存快照投影为只含 `Name`、`Value` 的临时 CSV；同一轮导出不会重复读取 Excel。

这是 Localization 对多语言表结构的专用解释层。它可以复用 Excel 读取和 Luban 基础设施，但不是 Table、Network 或 Config 的通用预过滤器，也不应承载这些模块的表格规则。

## 输入契约

- 完整导出只读取 `IDataTableSettings.Units` 配置的 `.xlsx`，不递归发现备份文件。
- `SourcePath` 必须位于源目录内；禁止绝对路径、越界、重复物理路径和符号链接路径段。
- 每个有效 Sheet 必须有 `Name`、至少一个已定义 `Language` 列和至少一条有效数据。
- 所有有效 Sheet 的语言集合必须完全一致；翻译单元格允许为空。
- Key 在单个 Sheet 内必须唯一，错误包含源文件、Sheet 与行号。
- 语言表头推荐直接写 `English`、`ChineseSimplified`；历史单个 `#English` 仍兼容，多重 `#` 拒绝。
- `DatasExportPath` 必须包含 `{0}`，所有语言展开后的正式路径不得碰撞。

## 投影 API

```csharp
internal static void ProjectCodeGen(LocalizationExcelPreFilter.SourceModel model, string outputRoot);

internal static void ProjectLanguage(
    LocalizationExcelPreFilter.SourceModel model,
    string outputRoot,
    string languageName);
```

代码生成取按 Ordinal 排序后的第一种语言。相对源路径会保留，例如 `UI/Common/Texts.xlsx` 输出到：

```text
_temp/English/UI/Common/Texts/{Sheet}.csv
```

CSV 的 Luban 标记结构为：

| 行 | 内容 |
|---|---|
| 0 | `##comment, SheetName` |
| 1 | `##var, Name, Value` |
| 2 | `##type, string, string` |
| 3 | `##comment, 键名, 值` |
| 4+ | Key 与当前语言值；空翻译保持空字符串 |

`ExtractAllLanguageColumns` 只为没有 Settings 参数的旧“独立导出支持语言列表”入口保留。完整数据、代码和全量导出均不调用该递归兼容路径。

## 关联文档

- [LocalizationTextExporter.md](LocalizationTextExporter.md)
- [EditorUtil.FileSystem.OutputApplier.md](../../../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Localization.TextExporter.md](../../../EditorUtil/EditorUtil.Localization/EditorUtil.Localization.TextExporter.md)
- [LocalizationSettings.md](../../../../Runtime/Modules/Localization/LocalizationSettings.md)
- [EditorUtil.Luban.Pipeline.md](../../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
