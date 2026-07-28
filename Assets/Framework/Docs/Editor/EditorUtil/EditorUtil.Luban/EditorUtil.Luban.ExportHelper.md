# EditorUtil.Luban.ExportHelper

**类签名**：`public static class ExportHelper`（`EditorUtil.Luban` 嵌套静态类）
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.Luban.ExportHelper`

Luban 导出辅助工具：根据模块 Profile 构建导出上下文、生成关联文件名、查找单元设置。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil.Luban.ExportHelper.cs` | `EditorUtil.Luban.ExportHelper` | 全部方法：构建导出上下文、生成关联文件名集合、查找单元设置、辅助路径方法 |
| `EditorUtil.Luban.ExportProfile.cs` | `LubanExportProfile` / `LubanExportProfiles` | Nova 内置模块的 target、manager 与模板键单一真相源 |

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
// Internal：从本次 manifest 的目标 unit 构建生成代码文件名集合
internal static HashSet<string> BuildRelevantFileNames(
    LubanSchemaManifest manifest,
    string sourcePath,
    string managerName)

// 在单元设置列表中查找与指定相对路径匹配的 UnitSetting
// 未找到时返回 null
public static IDataTableUnitSetting FindUnitSetting(
    IReadOnlyList<IDataTableUnitSetting> units,
    string relativePath)

// 获取预过滤器临时目录路径（regionDirPath/_temp/）
// Config / Network 模块在导出前使用 PreFilter 将过滤后的文件写入此目录
public static string GetPreFilterTempDirPath(string regionDirPath)

// 获取 Luban 自定义模板目录列表
// 优先检查 Packages/com.solotopia.nova.framework/Templates/Luban，回退到 Assets/Framework/Templates/Luban
// 不存在时返回 null
public static string[] GetLubanCustomTemplateDirs(string targetName)
```

Nova Framework 内部通过 Profile 重载构建上下文：

```csharp
BuildExportContext(sourceDirPath, settings, LubanExportProfiles.Sound)
```

---

## §9 关键算法

### BuildExportContext 组装逻辑

```
BuildExportContext(sourceDirPath, settings, profile)
  ├── ConfigSyncer.GetConfigDirPath(sourceDirPath) → configDir
  ├── Path.Combine(configDir, c_LubanConfFileName)   → confPath
  ├── Path.Combine(configDir, c_TablesXmlFileName)   → tablesXmlPath
  ├── RuntimeProvider.GetNamespace()                   → topModule（从 AssetDatabase 读取 ConfigRuntimeSO.Namespace）
  ├── profile.TargetName / ManagerName               → 固定 Luban 身份
  ├── GetLubanCustomTemplateDirs(profile.TemplateKey) → customTemplateDirs
  └── new LubanExportContext { … }
```

### BuildRelevantFileNames 文件名生成规则

```
BuildRelevantFileNames(manifest, sourcePath, managerName)
  ├── manifest.ResolveUnit(sourcePath) → unit
  ├── foreach table in unit.Tables：
  │     fileNames.Add(table.ValueType + ".cs")
  │     fileNames.Add(table.Name + ".cs")
  └── fileNames.Add(managerName + ".cs")
```

---

## §11 使用示例

```csharp
// Inspector 导出全量数据和类型时构建上下文
LubanExportContext ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(
    m_SourceDirPath.stringValue,
    GetTableSettings(),
    EditorUtil.Luban.LubanExportProfiles.Sound);

EditorUtil.Luban.Pipeline.ExportAll(ctx);

// 单文件导出时构建关联文件名（用于日志过滤）
HashSet<string> fileNames = EditorUtil.Luban.ExportHelper.BuildRelevantFileNames(
    ctx.SchemaManifest,
    unitSetting.SourcePath,
    EditorUtil.Luban.LubanExportProfiles.Sound.ManagerName);
```

---

## §13 关联文档

- [EditorUtil.Luban.Pipeline.md](EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.ConfigSyncer.md](EditorUtil.Luban.ConfigSyncer.md)
- [EditorUtil.Config.RuntimeProvider.md](../EditorUtil.Config/EditorUtil.Config.RuntimeProvider.md)
- [EditorUtil.Draw.SourceFileTree.md](../EditorUtil.Draw/EditorUtil.Draw.SourceFileTree.md)
- [EditorUtil.Luban.DataTypeNameHelper.md](EditorUtil.Luban.DataTypeNameHelper.md)
- [EditorUtil.Luban.SchemaManifest.md](EditorUtil.Luban.SchemaManifest.md)
- [EditorUtil.Luban.ExportProfile.md](EditorUtil.Luban.ExportProfile.md)
- [IDataTableSettings.md](../../../Runtime/Core/Table/IDataTableSettings.md)
- [IDataTableUnitSetting.md](../../../Runtime/Core/Table/IDataTableUnitSetting.md)
