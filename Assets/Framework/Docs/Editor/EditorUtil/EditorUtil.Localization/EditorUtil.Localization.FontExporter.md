# EditorUtil.Localization.FontExporter

**类签名**：`public static class FontExporter`（嵌套于 `EditorUtil.Localization`）
**命名空间**：`NovaFramework.Editor`

本地化字体导出工具，提供全链路（代码+数据）、仅代码、仅数据三条路径；通过标准 Luban Pipeline 实现，无需 PreFilter 多语言处理。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Localization/EditorUtil.Localization.FontExporter.cs` | `EditorUtil.Localization.FontExporter` | 字体导出工具类 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Localization (public static partial class)
        └── FontExporter (public static class)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_TargetName` | `const string` | `"localization-font"` | Luban target 名称 |
| `c_ManagerName` | `const string` | `"LocalizationFontTables"` | Luban manager 类名 |

---

## §5 完整公开 API

```csharp
// 全链路导出：刷新 DataTypeNames → 构建上下文
//   classExportPath 非空 → Pipeline.ExportAll（代码+数据）
//   classExportPath 为空 → Pipeline.ExportData（仅数据）
// fontUnitsSettingsProp / serializedObject 可为 null（跳过刷新）
public static bool ExportFontAll(
    LocalizationSettings settings,
    string sourceDirPath,
    UnityEditor.SerializedProperty fontUnitsSettingsProp,
    UnityEditor.SerializedObject serializedObject,
    string classExportPath);

// 仅导出字体 C# 类型：构建上下文 → Pipeline.ExportCode
public static bool ExportFontCode(
    LocalizationSettings settings,
    string sourceDirPath,
    string classExportPath);

// 仅导出字体数据：构建上下文 → Pipeline.ExportData
public static bool ExportFontData(
    LocalizationSettings settings,
    string sourceDirPath);
```

---

## §9 关键算法

### ExportFontAll 流程

```
ExportFontAll(settings, sourceDirPath, fontUnitsSettingsProp, serializedObject, classExportPath):
  1. 参数校验 → 无效时 Log.Warning + return false
  2. fontUnitsSettingsProp != null && serializedObject != null
     → DataTypeNameHelper.DoRefreshAllDataTypeNames(...)
  3. FontUnitsSettings 空 → Log.Warning + return false
  4. 构建 DataTableSettingsAdapter<LocalizationFontUnitSetting>(sourceDirPath, settings.FontUnitsSettings)
  5. BuildExportContext(sourceDirPath, adapter, "localization-font", "LocalizationFontTables") → ctx
  6. ctx.OutputCodeDir = classExportPath
  7. classExportPath 非空 → Pipeline.ExportAll(ctx)
     否则 → Pipeline.ExportData(ctx)
  8. success → AssetDatabase.Refresh()
```

---

## §11 使用示例

```csharp
// LocalizationComponentInspector 中字体全量导出
bool ok = EditorUtil.Localization.FontExporter.ExportFontAll(
    settings, settings.FontSourceDirPath,
    fontUnitsSettingsProp, serializedObject, classExportPath);

// Pipify Step 中仅导出字体数据
bool ok = EditorUtil.Localization.FontExporter.ExportFontData(settings, settings.FontSourceDirPath);

// Pipify Step 中仅导出字体类型
bool ok = EditorUtil.Localization.FontExporter.ExportFontCode(settings, settings.FontSourceDirPath, classExportPath);
```

---

## §13 关联文档

- [LocalizationSettings.md](../../../Runtime/Modules/Localization/LocalizationSettings.md)
- [EditorUtil.Localization.TextExporter.md](./EditorUtil.Localization.TextExporter.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
