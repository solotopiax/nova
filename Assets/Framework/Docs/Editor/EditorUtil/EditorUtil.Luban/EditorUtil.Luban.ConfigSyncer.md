# EditorUtil.Luban.ConfigSyncer

**类签名**：`public static class EditorUtil.Luban.ConfigSyncer`
**命名空间**：`NovaFramework.Editor`
**一行描述**：Luban 配置同步器 — 管理 _configs/ 目录，实现 Inspector 与文件双向同步。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.Luban.ConfigSyncer.cs` | `EditorUtil.Luban.ConfigSyncer` | _configs/ 目录管理 + luban.conf 读写 + __tables__.xml 生成 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── Luban (public static partial class)
        └── ConfigSyncer (public static class)
```

> 统一了原 `LubanConfigManager`（Table 模块）和 `LubanConfigConfigManager`（Config 模块）的配置同步逻辑，通过 `IDataTableSettings` / `IDataTableUnitSetting` 接口实现参数化。

---

## §4 关键字段

| 字段 | 类型 | 修饰符 | 说明 |
|------|------|--------|------|
| `s_Utf8NoBom` | `System.Text.UTF8Encoding` | `private static readonly` | UTF-8 无 BOM 编码（避免 Luban CLI 解析 JSON/XML 时因 BOM 出错） |
| `c_ConfigsDirName` | `string` | `private const` | `"_configs"` |
| `c_LubanConfFileName` | `string` | `internal const` | `"luban.conf"` |
| `c_TablesXmlFileName` | `string` | `internal const` | `"__tables__.xml"` |

---

## §5 公开 API

```csharp
/// <summary>
/// 获取 _configs/ 目录完整路径。
/// </summary>
/// <param name="sourceDirPath">数据源目录路径。</param>
/// <returns>_configs/ 目录完整路径。</returns>
public static string GetConfigDirPath(string sourceDirPath)

/// <summary>
/// 获取 Luban 主配置文件（luban.conf）的完整路径。
/// </summary>
/// <param name="sourceDirPath">数据源目录路径。</param>
/// <returns>luban.conf 的完整路径。</returns>
public static string GetConfPath(string sourceDirPath)

/// <summary>
/// 检查 _configs/ 目录是否存在。
/// </summary>
/// <param name="sourceDirPath">数据源目录路径。</param>
/// <returns>是否存在。</returns>
public static bool IsConfigDirExists(string sourceDirPath)

/// <summary>
/// 初始化 _configs/ 目录及默认配置文件。
/// </summary>
/// <param name="sourceDirPath">数据源目录路径。</param>
/// <param name="targetName">Luban target 名称（如 "table" / "config"）。</param>
/// <param name="managerName">Luban manager 类名（如 "TableTables" / "ConfigTables"）。</param>
/// <param name="topModule">顶层命名空间（如 "Game.Runtime"）。</param>
public static void InitializeConfigDir(string sourceDirPath, string targetName, string managerName, string topModule)

/// <summary>
/// 从 Inspector 数据同步到 _configs/ 文件。命名空间通过 RuntimeProvider.GetNamespace() 从 AssetDatabase 读取。
/// </summary>
/// <param name="sourceDirPath">数据源目录路径。</param>
/// <param name="settings">数据表设置（通过 IDataTableSettings 统一消费）。</param>
/// <param name="targetName">Luban target 名称。</param>
/// <param name="managerName">Luban manager 类名。</param>
/// <param name="regionUnits">（可选）按地域区分的单元设置列表，为 null 时回退到 settings.Units。</param>
public static void SyncFromInspector(string sourceDirPath, IDataTableSettings settings, string targetName, string managerName, IReadOnlyList<IDataTableUnitSetting> regionUnits = null)

/// <summary>
/// 清理指定临时目录。
/// </summary>
/// <param name="tempDirPath">临时目录完整路径。</param>
public static void CleanTempDir(string tempDirPath)
```

### 私有方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `WriteDefaultLubanConf` | `void WriteDefaultLubanConf(string path, string targetName, string managerName, string topModule)` | 写入默认 luban.conf（dataDir=".."，schemaFiles 指向 __tables__.xml） |
| `UpdateLubanConfTopModule` | `void UpdateLubanConfTopModule(string confPath, string targetName, string managerName, string topModule)` | 更新 luban.conf 中 targets[0] 的 name/manager/topModule |
| `WriteEmptyTablesXml` | `void WriteEmptyTablesXml(string path)` | 写入空的 `<module/>` XML 文件 |
| `GenerateTablesXml` | `void GenerateTablesXml(string xmlPath, string sourceDirPath, IReadOnlyList<IDataTableUnitSetting> unitSettings)` | 核心方法：从 IDataTableUnitSetting 列表生成完整 __tables__.xml |

---

## §9 关键算法

### GenerateTablesXml

遍历 `unitSettings` 列表，为每个 Excel 文件生成对应的 `<table>` 元素：

1. 跳过 SourcePath 为空、DataTypeNames 为空、或数据源文件不存在的条目
2. 从 `IDataTableUnitSetting` 获取 `LubanInputPath`（Table 模块 = SourcePath，Config 模块 = `"_temp/" + 文件名`）和 `Mode`（转小写字符串）
3. 遍历 `DataTypeNames`，跳过空值和 `#` 开头的忽略列
4. 提取 `sheetName`（取最后一个 `.` 后的部分）
5. 生成 `<table>` 元素：
   - `name="Tb{sheetName}"`
   - `value="Dt{sheetName}"`
   - `input="{sheetName}@{inputPath}"`
   - `mode="{mode}"`
   - Map 模式且 IndexField 非空时追加 `index="{indexField}"`
   - `readSchemaFromFile="true"`
   - `comment="{sheetName}"`

### SyncFromInspector 流程

```
SyncFromInspector
  ├── RuntimeProvider.GetNamespace() 取 topModule（从 AssetDatabase 读取 ConfigRuntimeSO.Common.Namespace）
  ├── _configs/ 不存在 → InitializeConfigDir
  ├── UpdateLubanConfTopModule（更新 luban.conf）
  └── GenerateTablesXml（重新生成 __tables__.xml）
```

---

## §10 常见误区

| 误区 | 说明 |
|------|------|
| luban.conf 的 dataDir | 配置为 `".."`（即 _configs 的父目录 = sourceDirPath），使 __tables__.xml 中的路径相对于 sourceDirPath 解析 |
| 编码问题 | 所有文件写入使用 `s_Utf8NoBom`（UTF-8 无 BOM），避免 Luban CLI 解析失败 |
| 残留配置 | 数据源文件不存在的条目会被 GenerateTablesXml 自动跳过，不会残留到 __tables__.xml |

---

## §11 使用示例

```csharp
// 检查配置目录是否已初始化
if (!EditorUtil.Luban.ConfigSyncer.IsConfigDirExists(sourceDirPath))
{
    EditorUtil.Luban.ConfigSyncer.InitializeConfigDir(sourceDirPath, "table", "TableTables", "Game.Runtime");
}

// Inspector 变更后同步到文件
EditorUtil.Luban.ConfigSyncer.SyncFromInspector(sourceDirPath, tableSettings, "table", "TableTables");

// 获取 luban.conf 路径（供 CliRunner 使用）
string confPath = EditorUtil.Luban.ConfigSyncer.GetConfPath(sourceDirPath);
```

> 通常不直接调用 ConfigSyncer，而是通过 `Pipeline.ExportData` / `Pipeline.ExportCode` / `Pipeline.ExportAll` 间接调用。

---

## §13 关联文档

- [EditorUtil.Luban.Pipeline.md](EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.CliRunner.md](EditorUtil.Luban.CliRunner.md)
- [EditorUtil.Luban.JsonMerger.md](EditorUtil.Luban.JsonMerger.md)
- [EditorUtil.Config.RuntimeProvider.md](../EditorUtil.Config/EditorUtil.Config.RuntimeProvider.md)
- [EditorUtil.md](../EditorUtil.md)
- [IDataTableSettings.md](../../../Runtime/Core/Table/IDataTableSettings.md)
- [IDataTableUnitSetting.md](../../../Runtime/Core/Table/IDataTableUnitSetting.md)
