# LocalizationExcelPreFilter

**类签名**：`internal static class LocalizationExcelPreFilter`
**命名空间**：`NovaFramework.Editor`

Localization 模块 Excel 预过滤器，从多语言 Excel 生成按语言拆分的临时 Luban 格式 Excel。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `LocalizationExcelPreFilter.cs` | `static class LocalizationExcelPreFilter` | 预过滤器：代码生成 / 按语言过滤 / 提取语言列 |

---

## 设计说明

Localization 文本 Excel 的特殊性在于一个文件包含多种语言列（Name, Desc, ChineseSimplified, English, ...）。标准 Luban CLI 无法处理这种列方向 Pivot，因此 PreFilter 在 Luban Pipeline 之前介入，为每种语言生成只含 Name + Value 两列的临时 Excel，让后续 Pipeline 正常消费。

**三种过滤模式**：
- `FilterForCodeGen`：取第一种语言的值作为 Value，用于 C# 类型生成（只需一次）
- `FilterForLanguage`：取指定语言列的值作为 Value，用于按语言数据导出
- `ExtractAllLanguageColumns`：扫描所有 Excel 提取语言列名称集合

---

## 关键字段表

| 字段 | 类型 | 值 | 说明 |
|------|------|----|------|
| `c_VarRow` | `int` | `1` | Luban 4 行标记中 ##var 行索引 |
| `c_DataStartRow` | `int` | `4` | 数据行起始索引 |
| `c_MinRowCount` | `int` | `5` | Sheet 最少行数（4 行表头 + 1 行数据） |
| `c_KeyColumnName` | `string` | `"Name"` | Key 列名称 |
| `c_DescColumnName` | `string` | `"Desc"` | 描述列名称（跳过） |

---

## 完整公开 API

```csharp
// 为代码生成准备临时 Excel：取第一种语言列的值作为 Value
public static void FilterForCodeGen(string sourceDirPath, string codegenTempDir)

// 为指定语言准备临时 Excel：取该语言列的值作为 Value
public static void FilterForLanguage(string sourceDirPath, string langTempDir, string languageName)

// 从目录下所有 Excel 中提取所有语言列名称
public static HashSet<string> ExtractAllLanguageColumns(string sourceDirPath)
```

---

## 关键内部方法

| 方法 | 说明 |
|------|------|
| `ParseColumnLayout(varRow)` | 从 ##var 行解析列布局，识别 Key 列和所有语言列 |
| `BuildFilteredSheet(sheetName, rows, keyIndex, valueIndex)` | 构建 4 行 Luban 标记表头 + Name/Value 数据行 |
| `WriteOutputExcel(sourceFilePath, tempDir, outputSheets)` | 写入临时 Excel 文件 |
| `ReadValidSheets(excelFilePath)` | 读取有效 Sheet（跳过 # 开头和行数不足的） |
| `IsExcludedPath(filePath, sourceDirPath)` | 排除 _configs/ 和 _temp/ 子目录 |

---

## 输出 Excel 格式

PreFilter 生成的临时 Excel 统一为标准 Luban 格式：

| 行号 | 内容 |
|------|------|
| 0 | `##comment`, SheetName |
| 1 | `##var`, Name, Value |
| 2 | `##type`, string, string |
| 3 | `##comment`, 键名, 值 |
| 4+ | 数据行（空串, name, value） |

---

## 使用示例

```csharp
// 在 LocalizationTextExporter.ExportPhaseA 中使用
LocalizationExcelPreFilter.FilterForCodeGen(sourceDirPath, codegenTempDir);

// 在 LocalizationTextExporter.ExportPhaseB 中使用
foreach (string language in allLanguages)
{
    string langTempDir = Path.Combine(tempDir, language);
    LocalizationExcelPreFilter.FilterForLanguage(sourceDirPath, langTempDir, language);
}

// 提取所有语言列
HashSet<string> languages = LocalizationExcelPreFilter.ExtractAllLanguageColumns(sourceDirPath);
```

---

## 关联文档

- [EditorUtil.Luban.LocalizationTextExporter.md](../../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.LocalizationTextExporter.md)
- [LocalizationSettings.md](../../../../Runtime/Modules/Localization/LocalizationSettings.md)
- [EditorUtil.Luban.Pipeline.md](../../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
