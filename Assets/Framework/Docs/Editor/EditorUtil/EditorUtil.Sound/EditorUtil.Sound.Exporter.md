# EditorUtil.Sound.Exporter

**类签名**：`public static class Exporter`（嵌套于 `EditorUtil.Sound`）
**命名空间**：`NovaFramework.Editor`

Sound 模块 Luban 导出流水线薄封装，提供全量、单文件数据、单文件类型三种导出操作；Inspector 通过此类下沉业务导出逻辑，自身仅负责参数组装与序列化对象读取。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Sound/EditorUtil.Sound.Exporter.cs` | `EditorUtil.Sound.Exporter` | Sound 导出工具类 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Sound (public static partial class)
        └── Exporter (public static class)
```

---

## §4 关键字段表

无字段（静态工具类，常量由 Pipify Step 外部传入 `targetName` / `managerName`）。

---

## §5 完整公开 API

```csharp
// 全量导出（数据 + 类型）：清理旧导出路径后一次性生成代码与数据
// settings 为 null 或 SoundUnitsSettings 为 null 时静默返回
// 多单元类型路径不同时 Log.Warning，使用首个非空路径
public static void ExportAll(string sourceDirPath, SoundSettings settings, string targetName, string managerName);

// 单文件数据导出：导出指定 unit 的 JSON 数据
// unitSetting 为 null 时静默返回
public static void ExportData(string sourceDirPath, SoundSettings settings, SoundUnitSetting unitSetting, string targetName, string managerName);

// 单文件类型代码导出：为指定文件生成 C# 类型
// unitSetting 为 null 时仅按文件名过滤生成（全量代码，不限定单元）
// relevantFileNames 可为 null
public static void ExportCode(string sourceDirPath, SoundSettings settings, SoundUnitSetting unitSetting, string classExportPath, HashSet<string> relevantFileNames, string targetName, string managerName);
```

---

## §9 关键算法

### ExportAll 流程

```
ExportAll(sourceDirPath, settings, targetName, managerName):
  1. 参数校验：settings == null || sourceDirPath 空 || SoundUnitsSettings == null → return
  2. 收集所有 DatasExportPath / ClassesExportPath → 去重后逐目录 FileSystem.DeletePath
  3. 收集 classExportPath（首个非空 ClassesExportPath，多路径时 Log.Warning）
  4. BuildExportContext(sourceDirPath, settings, targetName, managerName) → ctx
  5. ctx.OutputCodeDir = classExportPath
  6. Pipeline.ExportAll(ctx)
```

### ExportData 与 ExportCode

- `ExportData`：设置 `ctx.TargetUnit = unitSetting`，调用 `Pipeline.ExportData(ctx)`
- `ExportCode`：设置 `ctx.OutputCodeDir`、`ctx.RelevantFileNames`、`ctx.TargetUnit`，调用 `Pipeline.ExportCode(ctx)`

---

## §11 使用示例

```csharp
// SoundComponentInspector 中全量导出（targetName/managerName 由 Inspector 持有常量）
EditorUtil.Sound.Exporter.ExportAll(sourceDirPath, settings, "sound", "SoundTables");

// Pipify Step 中按单元逐个导出数据
foreach (SoundUnitSetting unit in settings.SoundUnitsSettings)
{
    EditorUtil.Sound.Exporter.ExportData(sourceDirPath, settings, unit, "sound", "SoundTables");
}

// Pipify Step 中全量导出类型（unitSetting 传 null = 全量代码生成）
EditorUtil.Sound.Exporter.ExportCode(sourceDirPath, settings, null, classExportPath, null, "sound", "SoundTables");
```

---

## §12 注意事项

- `ExportData` 的 `unitSetting` 为 `null` 时**静默返回**，不执行导出；与 `ExportCode` 的 null 语义不同——后者 null 表示全量代码生成
- `targetName` 和 `managerName` 由调用方（Inspector 或 Pipify Step）传入，约定为 `"sound"` / `"SoundTables"`

---

## §13 关联文档

- [SoundSettings.md](../../../Runtime/Modules/Sound/SoundSettings.md)
- [SoundUnitSetting.md](../../../Runtime/Modules/Sound/SoundUnitSetting.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
