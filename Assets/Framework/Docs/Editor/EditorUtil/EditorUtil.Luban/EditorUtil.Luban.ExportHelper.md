# EditorUtil.Luban.ExportHelper

**类签名**：`public static class ExportHelper`（`EditorUtil.Luban` 嵌套静态类）
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.Luban.ExportHelper`

Luban 导出辅助工具：构建导出上下文、生成关联文件名、查找单元设置。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil.Luban.ExportHelper.cs` | `EditorUtil.Luban.ExportHelper` | 全部方法：构建导出上下文、生成关联文件名集合、查找单元设置、辅助路径方法 |

---

## §3 继承关系

```
EditorUtil (static partial)
  └── EditorUtil.Luban (static partial)
        └── EditorUtil.Luban.ExportHelper (static)
```

---

## §4 关键字段表

此类仅包含静态方法，无字段。

---

## §5 完整公开 API

```csharp
// 构建标准 Luban 导出上下文
// targetName：Luban target 名称（如 "table" / "config" / "sound" / "ui" / "hostkey" / "network"）
// managerName：Luban manager 类名（如 "TableTables" / "ConfigTables"）
public static LubanExportContext BuildExportContext(
    string sourceDirPath,
    IDataTableSettings settings,
    string targetName,
    string managerName)

// 根据数据源文件构建其对应生成代码文件名集合（用于单文件导出时过滤日志）
// 从 unitSetting.DataTypeNames 提取 SheetName.cs 和 TbSheetName.cs，并附加 managerName.cs
public static HashSet<string> BuildRelevantFileNames(
    string filePath,
    string sourceDirPath,
    IReadOnlyList<IDataTableUnitSetting> units,
    string managerName)

// 在单元设置列表中查找与指定相对路径匹配的 UnitSetting
// 未找到时返回 null
public static IDataTableUnitSetting FindUnitSetting(
    IReadOnlyList<IDataTableUnitSetting> units,
    string relativePath)

// 获取预过滤器临时目录路径（regionDirPath/_temp/）
// Config / Network 模块在导出前使用 PreFilter 将过滤后的文件写入此目录
public static string GetPreFilterTempDirPath(string regionDirPath)

// 获取 Luban 自定义模板目录路径
// 优先检查 Packages/com.solotopia.nova.framework/Templates/Luban，回退到 Assets/Framework/Templates/Luban
// 不存在时返回 null
public static string GetLubanCustomTemplateDir()
```

---

## §9 关键算法

### BuildExportContext 组装逻辑

```
BuildExportContext(sourceDirPath, settings, targetName, managerName)
  ├── ConfigSyncer.GetConfigDirPath(sourceDirPath) → configDir
  ├── Path.Combine(configDir, c_LubanConfFileName)   → confPath
  ├── Path.Combine(configDir, c_TablesXmlFileName)   → tablesXmlPath
  ├── RuntimeProvider.GetNamespace()                   → topModule（从 AssetDatabase 读取 ConfigRuntimeSO.Common.Namespace）
  ├── GetLubanCustomTemplateDir()                    → customTemplateDir
  └── new LubanExportContext { … }
```

### BuildRelevantFileNames 文件名生成规则

```
BuildRelevantFileNames(filePath, sourceDirPath, units, managerName)
  ├── GetRelativePath(sourceDirPath, filePath) → relativePath
  ├── FindUnitSetting(units, relativePath) → unitSetting
  ├── foreach typeName in unitSetting.DataTypeNames：
  │     跳过空值或 # 开头的条目
  │     sheetName = typeName 截取最后一个 '.' 后的部分（无 '.' 时取全名）
  │     fileNames.Add(sheetName + ".cs")
  │     fileNames.Add("Tb" + sheetName + ".cs")
  └── fileNames.Add(managerName + ".cs")
```

---

## §11 使用示例

```csharp
// Inspector 导出全量数据和类型时构建上下文
LubanExportContext ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(
    m_SourceDirPath.stringValue,
    GetTableSettings(),   // 返回实现了 IDataTableSettings 的设置对象
    "table",
    "TableTables");

EditorUtil.Luban.Pipeline.ExportAll(ctx);

// 单文件导出时构建关联文件名（用于日志过滤）
IReadOnlyList<IDataTableUnitSetting> units = GetCurrentUnitSettings();
HashSet<string> fileNames = EditorUtil.Luban.ExportHelper.BuildRelevantFileNames(
    filePath,
    m_SourceDirPath.stringValue,
    units,
    "TableTables");
```

---

## §13 关联文档

- [EditorUtil.Luban.Pipeline.md](EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.ConfigSyncer.md](EditorUtil.Luban.ConfigSyncer.md)
- [EditorUtil.Config.RuntimeProvider.md](../EditorUtil.Config/EditorUtil.Config.RuntimeProvider.md)
- [EditorUtil.Draw.SourceFileTree.md](../EditorUtil.Draw/EditorUtil.Draw.SourceFileTree.md)
- [EditorUtil.Luban.DataTypeNameHelper.md](EditorUtil.Luban.DataTypeNameHelper.md)
- [IDataTableSettings.md](../../../Runtime/Core/Table/IDataTableSettings.md)
- [IDataTableUnitSetting.md](../../../Runtime/Core/Table/IDataTableUnitSetting.md)
