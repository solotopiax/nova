# EditorUtil.Luban.LocalizationTextExporter

**类签名**：`public static class EditorUtil.Luban.LocalizationTextExporter`
**命名空间**：`NovaFramework.Editor`

本地化文本导出器，编排 PreFilter + Luban Pipeline 完成三阶段全链路导出。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `EditorUtil.Luban.LocalizationTextExporter.cs` | `static class LocalizationTextExporter` | 三阶段 Pipeline 编排器 |
| `EditorUtil.Luban.LocalizationTextExporter.cs` | `class LanguageResolvedSettings` | 按语言解析后的 IDataTableSettings wrapper |
| `EditorUtil.Luban.LocalizationTextExporter.cs` | `class LanguageResolvedUnitSetting` | 按语言解析后的 IDataTableUnitSetting wrapper |

---

## 设计说明

文本数据与标准 Luban 数据（Config/Table）的区别在于：一个 Excel 文件中每一列对应一种语言，需要按语言拆分后分别走 Luban Pipeline。

**核心思路**：PreFilter 为每种语言生成只含 Name + Value 两列的临时 Luban 格式 Excel，然后每种语言各走一次标准 Luban Pipeline（ConfigSyncer -> CliRunner -> JsonMerger -> MapPropGen）。

### 三阶段导出流程

```
Phase A -- C# 类型生成（一次）
  topModule = RuntimeProvider.GetNamespace()（从 AssetDatabase 读取 ConfigRuntimeSO.Common.Namespace）
  PreFilter.FilterForCodeGen() -> 取第一种语言值 -> _temp/_codegen/
  Pipeline.ExportCode(ctx)    -> Luban CLI 生成 C# 类型

Phase B -- 按语言数据导出（每种语言一次）
  for each language:
    PreFilter.FilterForLanguage() -> 取该语言值 -> _temp/{language}/
    LanguageResolvedSettings 替换 DatasExportPath 中的 {0} 为语言名
    Pipeline.ExportData(ctx)      -> Luban CLI + JsonMerger 输出 JSON

Phase C -- MapPropGen + 语言列表
  MapPropGen.GenerateAll()    -> 为 TbXxx 追加 Map 属性
  ExportSupportedLanguagesJson() -> 输出语言列表 JSON
```

### LanguageResolvedSettings

`LanguageResolvedSettings` 和 `LanguageResolvedUnitSetting` 是 `IDataTableSettings` / `IDataTableUnitSetting` 的 wrapper 类，在 Phase B 中将原始 Settings 的 `DatasExportPath` 中的 `{0}` 替换为当前语言名称，确保每种语言输出到不同文件。

---

## 完整公开 API

```csharp
// 三阶段全链路导出：PreFilter + Luban Pipeline
public static bool ExportAll(string sourceDirPath, IDataTableSettings settings, string classExportPath, string customTemplateDir, string supportedLanguagesExportPath)
```

---

## 关键内部方法

| 方法 | 说明 |
|------|------|
| `ExportPhaseA(...)` | C# 类型生成：PreFilter 取第一种语言 -> Pipeline.ExportCode |
| `ExportPhaseB(...)` | 按语言数据导出：每种语言 PreFilter -> LanguageResolvedSettings -> Pipeline.ExportData |
| `ExportPhaseC(...)` | MapPropGen.GenerateAll + 导出语言列表 JSON |
| `ExportSupportedLanguagesJson(...)` | 将语言名称集合序列化为 JSON 数组输出 |
| `CleanupTempDir(...)` | 清理临时目录 |

---

## Excel 格式要求

必须使用 Luban 4 行标记格式：

| 行号 | 标记 | 内容 |
|------|------|------|
| 0 | `##comment` | 注释行（可选） |
| 1 | `##var` | 变量名行（Name, Desc, English, ChineseSimplified, ...） |
| 2 | `##type` | 类型行 |
| 3 | `##comment` | 注释行（可选） |
| 4+ | 数据 | 实际文本数据 |

特殊列：
- `Name`：Key 列（必须）
- `Desc`：描述列（跳过）
- `#`：标记列（跳过）
- 其他列名须匹配 `Language` 枚举名称（如 `English`、`ChineseSimplified`）

---

## 使用示例

```csharp
// 在 LocalizationComponentInspector 中调用
TextDataTableSettingsAdapter adapter = new TextDataTableSettingsAdapter(textDirPath, settings.Namespaces, settings.TextUnitsSettings);
EditorUtil.Luban.LocalizationTextExporter.ExportAll(textDirPath, adapter, classExportPath, customTemplateDir, supportedLanguagesExportPath);
```

---

## 关联文档

- [LocalizationExcelPreFilter.md](../../DataPipeline/Implements/Localizations/LocalizationExcelPreFilter.md)
- [EditorUtil.Luban.Pipeline.md](EditorUtil.Luban.Pipeline.md)
- [LocalizationSettings.md](../../../Runtime/Modules/Localization/LocalizationSettings.md)
- [LocalizationComponentInspector.md](../../Inspectors/LocalizationComponentInspector/LocalizationComponentInspector.md)
