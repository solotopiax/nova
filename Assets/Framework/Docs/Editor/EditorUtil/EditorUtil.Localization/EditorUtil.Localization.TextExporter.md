# EditorUtil.Localization.TextExporter

**类签名**：`public static class TextExporter`（嵌套于 `EditorUtil.Localization`）
**命名空间**：`NovaFramework.Editor`

本地化文本导出工具，提供全链路导出、仅代码导出、仅数据导出和语言列表独立导出四条路径；文本数据使用 Map 模式，通过 `LocalizationTextExporter` 完成三阶段 PreFilter → Pipeline 编排。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Localization/EditorUtil.Localization.TextExporter.cs` | `EditorUtil.Localization.TextExporter` | 文本导出工具类（含内嵌私有适配类 `CodegenResolvedSettings` / `LangResolvedSettings` / `LubanPathOverrideUnitSetting`） |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Localization (public static partial class)
        └── TextExporter (public static class)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_TargetName` | `const string` | `"localization-text"` | Luban target 名称 |
| `c_ManagerName` | `const string` | `"LocalizationTextTables"` | Luban manager 类名 |
| `c_CodegenTempSubDir` | `const string` | `"_codegen"` | 代码生成临时子目录名称 |
| `c_TempDirName` | `const string` | `"_temp"` | 临时目录根名称 |
| `s_Utf8NoBom` | `static readonly Encoding` | `UTF8Encoding(false)` | UTF-8 无 BOM 编码实例 |

---

## §5 完整公开 API

```csharp
// 全链路导出：DataTypeNameHelper.DoRefreshAllDataTypeNames → LocalizationTextExporter.ExportAll（三阶段）
// textUnitsSettingsProp / serializedObject 可为 null（跳过刷新 DataTypeNames）
// 返回是否成功
public static bool ExportTextAll(
    LocalizationSettings settings,
    string sourceDirPath,
    UnityEditor.SerializedProperty textUnitsSettingsProp,
    UnityEditor.SerializedObject serializedObject,
    string classExportPath,
    string[] customTemplateDirs,
    string supportedLanguagesExportPath);

// 仅导出 C# 类型（Phase A）：PreFilter 取第一种语言列生成简化 Excel → Pipeline.ExportCode
// customTemplateDirs 可为 null
public static bool ExportTextCode(
    LocalizationSettings settings,
    string sourceDirPath,
    string classExportPath,
    string[] customTemplateDirs);

// 仅导出文本数据（Phase B）：按每种语言执行 PreFilter → Pipeline.ExportData
// DatasExportPath 中的 {0} 占位符替换为对应语言名称
public static bool ExportTextData(LocalizationSettings settings, string sourceDirPath);

// 独立导出语言列表 JSON：提取所有语言列 → 排序 → 写入 exportPath
public static bool ExportSupportedLanguages(string sourceDirPath, string exportPath);
```

---

## §9 关键算法

### ExportTextCode 流程（Phase A）

```
ExportTextCode(settings, sourceDirPath, classExportPath, customTemplateDirs):
  1. 参数校验 → 无效时 Log.Warning + return false
  2. codegenTempDir = sourceDirPath/_temp/_codegen
  3. try:
     a. LocalizationExcelPreFilter.FilterForCodeGen(sourceDirPath, codegenTempDir)
        → 取第一种语言列生成 _codegen/{fileName}
     b. 构建 CodegenResolvedSettings（LubanInputPath 覆盖为 _temp/_codegen/{fileName}）
     c. 构建 LubanExportContext（TargetName="localization-text", TopModule=RuntimeProvider.GetNamespace()）
     d. Pipeline.ExportCode(ctx) → success
     e. success → AssetDatabase.Refresh()
  4. finally: CleanupTempDir(sourceDirPath/_temp)
  5. return success
```

### ExportTextData 流程（Phase B，多语言并发）

```
ExportTextData(settings, sourceDirPath):
  1. 参数校验
  2. LocalizationExcelPreFilter.ExtractAllLanguageColumns(sourceDirPath) → allLanguages
  3. allLanguages 空 → Log.Warning + return false
  4. for each languageName in allLanguages:
     a. langTempDir = sourceDirPath/_temp/{languageName}
     b. LocalizationExcelPreFilter.FilterForLanguage(sourceDirPath, langTempDir, languageName)
     c. 构建 LangResolvedSettings（DatasExportPath 的 {0} 替换为 languageName）
     d. Pipeline.ExportData(ctx)
  5. finally: CleanupTempDir(sourceDirPath/_temp)
```

---

## §11 使用示例

```csharp
// LocalizationComponentInspector 中全链路导出
string classExportPath = settings.TextUnitsSettings[0].ClassesExportPath;
string[] templateDirs = EditorUtil.Luban.ExportHelper.GetLubanCustomTemplateDirs("localization-text");
bool ok = EditorUtil.Localization.TextExporter.ExportTextAll(
    settings, sourceDirPath,
    textUnitsSettingsProp, serializedObject,
    classExportPath, templateDirs, supportedLanguagesExportPath);

// Pipify Step 中仅导出文本数据
bool ok = EditorUtil.Localization.TextExporter.ExportTextData(settings, sourceDirPath);

// 独立导出语言列表
bool ok = EditorUtil.Localization.TextExporter.ExportSupportedLanguages(
    sourceDirPath, "Assets/Game/Localization/supported_languages.json");
```

---

## §12 注意事项

- `ExportTextCode` 与 `ExportTextData` 都在 `finally` 块清理 `_temp` 目录，失败后不遗留临时文件
- `ExportTextAll` 委托给 `LocalizationTextExporter.ExportAll` 完成三阶段 Pipeline，本类不重复实现
- `DatasExportPath` 中的 `{0}` 占位符是语言名的插值位，由 `LangResolvedSettings` 内部替换

---

## §13 关联文档

- [LocalizationSettings.md](../../../Runtime/Modules/Localization/LocalizationSettings.md)
- [EditorUtil.Luban.LocalizationTextExporter.md](../EditorUtil.Luban/EditorUtil.Luban.LocalizationTextExporter.md)
- [LocalizationExcelPreFilter.md](../../DataPipeline/Implements/Localizations/LocalizationExcelPreFilter.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
