# EditorUtil.Vibrate.Exporter

**类签名**：Exporter 方法直接定义在 `EditorUtil.Vibrate` partial class 中（非独立嵌套类）
**命名空间**：`NovaFramework.Editor`

Vibrate 模块 Luban 导出工具，Emphasis 与 Custom 双轨独立，每轨提供单文件数据、单文件类型、全量三种导出操作。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Vibrate/EditorUtil.Vibrate.Exporter.cs` | `EditorUtil.Vibrate` | Vibrate 导出方法（直接挂在 partial class 上） |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Vibrate (public static partial class)
        └── 导出方法直接定义（无独立嵌套类）
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_EmphasisTargetName` | `const string` | `"vibrate-emphasis"` | Emphasis 区域 Luban target 名称 |
| `c_CustomTargetName` | `const string` | `"vibrate-custom"` | Custom 区域 Luban target 名称 |
| `c_EmphasisManagerName` | `const string` | `"VibrateEmphasisTables"` | Emphasis Luban manager 类名 |
| `c_CustomManagerName` | `const string` | `"VibrateCustomTables"` | Custom Luban manager 类名 |

---

## §5 完整公开 API

```csharp
// Emphasis 区域 — 单文件数据导出
public static void ExportEmphasisData(string filePath, string dataExportPath, VibrateSettings settings);

// Emphasis 区域 — 单文件类型导出
public static void ExportEmphasisCode(string filePath, string classExportPath, VibrateSettings settings);

// Emphasis 区域 — 全量导出（数据 + 类型）
public static void ExportEmphasisAll(VibrateSettings settings);

// Custom 区域 — 单文件数据导出
public static void ExportCustomData(string filePath, string dataExportPath, VibrateSettings settings);

// Custom 区域 — 单文件类型导出
public static void ExportCustomCode(string filePath, string classExportPath, VibrateSettings settings);

// Custom 区域 — 全量导出（数据 + 类型）
public static void ExportCustomAll(VibrateSettings settings);
```

---

## §9 关键算法

### 双轨路由

所有 6 个公开方法均委托给三个私有方法，通过 `isEmphasis` 布尔参数路由：

```
ExportEmphasisData/Code/All → private ExportData/Code/All(isEmphasis: true)
ExportCustomData/Code/All   → private ExportData/Code/All(isEmphasis: false)
```

私有方法根据 `isEmphasis` 选择：
- `sourceDirPath`：`settings.EmphasisSourceDirPath` 或 `settings.CustomSourceDirPath`
- `units`：`settings.EmphasisUnitsSettings` 或 `settings.CustomUnitsSettings`
- `targetName` / `managerName`：对应常量

### 单文件导出（ExportData / ExportCode）

```
ExportData(filePath, dataExportPath, settings, isEmphasis):
  1. dataExportPath 空或 settings == null → return
  2. 取 sourceDirPath / units / targetName / managerName（按 isEmphasis）
  3. relativePath = GetRelativePath(sourceDirPath, filePath)
  4. unitSetting = units.Find(u => u.SourcePath == relativePath)
  5. unitSetting == null → Log.Error + return
  6. areaSettings = settings.GetEmphasisAsSettings() 或 GetCustomAsSettings()
  7. BuildExportContext(sourceDirPath, areaSettings, targetName, managerName) → ctx
  8. ctx.TargetUnit = unitSetting → Pipeline.ExportData(ctx)
```

### ExportAll（全量导出）

```
ExportAll(settings, isEmphasis):
  1. settings == null → return
  2. 收集所有 DatasExportPath / ClassesExportPath → 去重后 FileSystem.DeletePath
  3. classExportPath = 首个非空 ClassesExportPath（多路径时 Log.Warning）
  4. areaSettings → BuildExportContext → ctx.OutputCodeDir = classExportPath
  5. Pipeline.ExportAll(ctx)
```

---

## §11 使用示例

```csharp
// VibrateComponentInspector 中 Emphasis 全量导出
EditorUtil.Vibrate.ExportEmphasisAll(settings);

// Pipify Step 中按单元逐个导出 Emphasis 数据
string sourceDirPath = settings.EmphasisSourceDirPath;
foreach (VibrateUnitSetting unit in settings.EmphasisUnitsSettings)
{
    string filePath = sourceDirPath.TrimEnd('/', '\\') + "/" + unit.SourcePath;
    EditorUtil.Vibrate.ExportEmphasisData(filePath, unit.DatasExportPath, settings);
}

// Pipify Step 中按单元逐个导出 Custom 类型
foreach (VibrateUnitSetting unit in settings.CustomUnitsSettings)
{
    string filePath = settings.CustomSourceDirPath.TrimEnd('/', '\\') + "/" + unit.SourcePath;
    EditorUtil.Vibrate.ExportCustomCode(filePath, unit.ClassesExportPath, settings);
}
```

---

## §12 注意事项

- 与 Sound.Exporter 不同，Vibrate 的 Exporter 方法不封装在嵌套 `Exporter` 类内，直接挂在 `EditorUtil.Vibrate` partial class 上，调用时无 `.Exporter` 层级
- 单文件导出前会先用 `GetRelativePath` 把绝对路径换算为相对路径，再与 `unit.SourcePath` 匹配；路径分隔符须保持一致

---

## §13 关联文档

- [VibrateSettings.md](../../../Runtime/Modules/Vibrate/VibrateSettings.md)
- [VibrateUnitSetting.md](../../../Runtime/Modules/Vibrate/VibrateUnitSetting.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
