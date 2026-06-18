# EditorUtil.Luban.Pipeline

**类签名**：`public class EditorUtil.Luban.LubanExportContext` + `public static class EditorUtil.Luban.Pipeline`
**命名空间**：`NovaFramework.Editor`
**一行描述**：Luban 导出流水线 — 统一编排 Sync / CLI / Merge / MapPropGen 四阶段导出流程。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.Luban.Pipeline.cs` | `EditorUtil.Luban.LubanExportContext` | 导出上下文，封装单次导出操作所需的全部参数 |
| `EditorUtil.Luban.Pipeline.cs` | `EditorUtil.Luban.Pipeline` | 流水线入口，编排 ConfigSyncer → CliRunner → JsonMerger → MapPropGen |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── Luban (public static partial class)
        ├── LubanExportContext (public class)
        └── Pipeline (public static class)
```

---

## §4 关键字段

### LubanExportContext 字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `SourceDirPath` | `string` | 数据源根目录路径 |
| `ConfPath` | `string` | luban.conf 文件路径 |
| `TargetName` | `string` | Luban target 名称（如 `"table"` / `"config"`） |
| `ManagerName` | `string` | Luban manager 类名（如 `"TableTables"` / `"ConfigTables"`） |
| `TopModule` | `string` | 顶层命名空间（如 `"Game.Runtime"`） |
| `OutputCodeDir` | `string` | 代码输出目录 |
| `OutputDataDir` | `string` | 数据输出目录（Luban 临时输出，非最终导出路径） |
| `CustomTemplateDirs` | `string[]` | 自定义模板目录列表（可为 null，使用内置模板）。按优先级排序，Luban 依次查找 |
| `TablesXmlPath` | `string` | __tables__.xml 文件路径 |
| `Settings` | `IDataTableSettings` | 数据表设置（通过接口统一消费 Table/Config 模块） |
| `RelevantFileNames` | `HashSet<string>` | 仅打印这些文件名的日志（单文件导出时使用，为 null 时打印全部） |
| `RegionUnits` | `IReadOnlyList<IDataTableUnitSetting>` | 当前地域的单元设置列表（按地域区分时由调用方填入，为 null 时回退到 Settings.Units） |
| `TargetUnit` | `IDataTableUnitSetting` | 目标单元设置（单文件导出时指定，为 null 时处理全部） |
| `EffectiveUnits` | `IReadOnlyList<IDataTableUnitSetting>` | （只读属性）优先使用 RegionUnits，回退到 Settings?.Units |

---

## §5 公开 API

```csharp
/// <summary>
/// 导出数据（单文件或全部）：同步配置 -> 调用 Luban CLI 导出数据 -> 合并 JSON -> 生成 Map 属性。
/// </summary>
/// <param name="ctx">导出上下文。</param>
/// <returns>是否成功。</returns>
public static bool ExportData(LubanExportContext ctx)

/// <summary>
/// 导出代码（单文件或全部）：同步配置 -> 调用 Luban CLI 生成代码 -> 生成 Map 属性。
/// </summary>
/// <param name="ctx">导出上下文。</param>
/// <returns>是否成功。</returns>
public static bool ExportCode(LubanExportContext ctx)

/// <summary>
/// 导出全部（代码 + 数据）：同步配置 -> 调用 Luban CLI -> 合并 JSON -> 生成 Map 属性。
/// </summary>
/// <param name="ctx">导出上下文。</param>
/// <returns>是否成功。</returns>
public static bool ExportAll(LubanExportContext ctx)
```

---

## §9 关键算法

### ExportData 流程

```
ExportData(ctx)
  ├── Environment.LubanChecker.Check() → 未就绪时 ConfigWindow.OpenLubanSection(envResult) + return false
  ├── ConfigSyncer.SyncFromInspector（同步 _configs/）
  ├── 创建临时目录 _luban_temp_{guid8}
  ├── CliRunner.RunDataExport → 临时目录
  ├── ctx.TargetUnit != null?
  │     ├── 是 → JsonMerger.MergeForUnit + MapPropGen.GenerateForUnit（单文件）
  │     └── 否 → JsonMerger.MergeAll + MapPropGen.GenerateAll（全部）
  ├── finally: 删除临时目录
  └── finally: AssetDatabase.Refresh()
```

### ExportCode 流程

```
ExportCode(ctx)
  ├── Environment.LubanChecker.Check() → 未就绪时 ConfigWindow.OpenLubanSection(envResult) + return false
  ├── ConfigSyncer.SyncFromInspector（同步 _configs/）
  ├── CliRunner.RunCodeGen → ctx.OutputCodeDir（直接输出到目标目录，无临时目录）
  ├── ctx.TargetUnit != null?
  │     ├── 是 → MapPropGen.GenerateForUnit（单文件）
  │     └── 否 → MapPropGen.GenerateAll（全部）
  └── AssetDatabase.Refresh()
```

### ExportAll 流程

```
ExportAll(ctx)
  ├── Environment.LubanChecker.Check() → 未就绪时 ConfigWindow.OpenLubanSection(envResult) + return false
  ├── ConfigSyncer.SyncFromInspector（同步 _configs/）
  ├── 创建临时目录 _luban_temp_{guid8}
  ├── ctx.OutputCodeDir 非空?
  │     ├── 是 → CliRunner.RunAll（代码输出到 OutputCodeDir，数据输出到临时目录）
  │     └── 否 → CliRunner.RunDataExport（仅数据，输出到临时目录）
  ├── JsonMerger.MergeAll（全部合并）
  ├── MapPropGen.GenerateAll（全部生成属性）
  ├── finally: 删除临时目录
  └── finally: AssetDatabase.Refresh()
```

### 临时目录管理

- 数据导出使用临时目录 `_luban_temp_{guid8}`（位于 SourceDirPath 下）
- 临时目录在 `finally` 块中无条件删除，确保即使导出失败也不残留
- 代码导出不需要临时目录（直接输出到最终目标目录）

---

## §11 使用示例

```csharp
// Inspector 中构建导出上下文并调用 Pipeline
var ctx = new EditorUtil.Luban.LubanExportContext
{
    SourceDirPath = tableSettings.SourceDirPath,
    ConfPath = Path.Combine(
        EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(tableSettings.SourceDirPath),
        EditorUtil.Luban.ConfigSyncer.c_LubanConfFileName
    ),
    TargetName = "table",
    ManagerName = "TableTables",
    TopModule = tableSettings.Namespaces[0],
    OutputCodeDir = classesExportPath,
    OutputDataDir = null,
    CustomTemplateDir = customTemplateDir,
    TablesXmlPath = Path.Combine(
        EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(tableSettings.SourceDirPath),
        EditorUtil.Luban.ConfigSyncer.c_TablesXmlFileName
    ),
    Settings = tableSettings,
    RelevantFileNames = null,
    TargetUnit = null,
};

// 导出全部（代码 + 数据）
bool success = EditorUtil.Luban.Pipeline.ExportAll(ctx);

// 单文件导出数据
ctx.TargetUnit = tableSettings.Units[0];
ctx.RelevantFileNames = new HashSet<string> { "TbHero.cs", "TbHeroSkill.cs" };
bool dataSuccess = EditorUtil.Luban.Pipeline.ExportData(ctx);
```

---

## §12 注意事项

| 场景 | 说明 |
|------|------|
| 环境检查 guard | `ExportData` / `ExportCode` / `ExportAll` 三个入口均在首步执行 `EditorUtil.Environment.LubanChecker.Check()`，未就绪时自动弹出 `ConfigWindow` 并提前返回 false |
| 临时目录残留 | 正常情况下 `finally` 块会清理。若 Unity 崩溃可能残留 `_luban_temp_*` 目录，需手动删除 |
| AssetDatabase.Refresh | 每个导出方法结束时自动调用 `AssetDatabase.Refresh()`，无需外部再次调用 |
| ExportAll 的 OutputCodeDir | 为 null 时退化为纯数据导出（仅调用 `RunDataExport`），不生成代码 |
| RegionUnits vs Settings.Units | 当需要按地域区分单元配置时传入 `RegionUnits`；不区分地域时留 null，Pipeline 自动从 `Settings.Units` 取 |

---

## §13 关联文档

- [EditorUtil.Luban.CliRunner.md](EditorUtil.Luban.CliRunner.md)
- [EditorUtil.Environment.LubanChecker.md](../EditorUtil.Environment/EditorUtil.Environment.LubanChecker.md)
- [EditorUtil.Luban.ConfigSyncer.md](EditorUtil.Luban.ConfigSyncer.md)
- [EditorUtil.Luban.JsonMerger.md](EditorUtil.Luban.JsonMerger.md)
- [EditorUtil.Luban.MapPropGen.md](EditorUtil.Luban.MapPropGen.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)
- [EditorUtil.md](../EditorUtil.md)
- [IDataTableSettings.md](../../../Runtime/Core/Table/IDataTableSettings.md)
- [IDataTableUnitSetting.md](../../../Runtime/Core/Table/IDataTableUnitSetting.md)
- [TableComponentInspector.md](../../Inspectors/TableComponentInspector/TableComponentInspector.md)
- [ConfigComponentInspector.md](../../Inspectors/ConfigComponentInspector/ConfigComponentInspector.md)
