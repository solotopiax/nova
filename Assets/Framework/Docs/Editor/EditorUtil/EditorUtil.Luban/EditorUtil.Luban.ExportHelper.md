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

// Internal：把 Assets/Framework 或 Packages 下的框架模板逻辑路径
// 解析为当前安装形态的真实物理目录；项目自定义路径保持原值
internal static string ResolveCustomTemplateDirectory(string configuredDirectory)
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
  ├── RuntimeProvider.GetNamespace()                   → topModule（Globals 锚定的激活 ConfigMasterSO 顶层 Namespace）
  ├── profile.TargetName / ManagerName               → 固定 Luban 身份
  ├── GetLubanCustomTemplateDirs(profile.TemplateKey) → customTemplateDirs
  └── new LubanExportContext { … }
```

`RuntimeProvider.GetNamespace()` 当前调用 `WorkspaceActive.Get()`，按 `ProjectSettings/Nova/Globals.json` 的 `configMasterGuid` 加载激活 `ConfigMasterSO`，返回 `master.Namespace ?? string.Empty`；无可加载 Master 时同样返回空字符串。它不读取配对 `ConfigRuntimeSO.Namespace`，不应用 `NamespaceMask` / `NamespaceOverrides` 或 `DimensionalResolver.ResolveNamespace`，也不按场景或候选排序选择 Master。`ConfigSyncer.SyncFromInspector()` 会再次读取这一来源并写入 `luban.conf.targets[0].topModule`，因此仅冻结表设置不足以证明生成命名空间不漂移。

受控代码导出的 Action 必须在 Plan 冻结激活 Master 的 GUID、路径和顶层 Namespace，在 Execute 写入前复核，并核对生成类型全名与冻结预期及既有消费者。只读身份核验可复用 `WorkspaceActive.TryGetPersistedConfigMaster(...)`；它检查 Globals 的 GUID / pathHint / 实际资产一致性，不修复或写入。未绑定 Master 应阻断；已绑定且明确空 Namespace 的项目可生成 global namespace，不能把空字符串一概视为缺失配置。当前 Action 是否提供这些防护须以 live 契约和已发布实现为准，本节不构成运行验收证据。

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
- [EditorUtil.Config.WorkspaceActive.md](../EditorUtil.Config/EditorUtil.Config.WorkspaceActive.md)
- [EditorUtil.Draw.SourceFileTree.md](../EditorUtil.Draw/EditorUtil.Draw.SourceFileTree.md)
- [EditorUtil.Luban.DataTypeNameHelper.md](EditorUtil.Luban.DataTypeNameHelper.md)
- [EditorUtil.Luban.SchemaManifest.md](EditorUtil.Luban.SchemaManifest.md)
- [EditorUtil.Luban.ExportProfile.md](EditorUtil.Luban.ExportProfile.md)
- [IDataTableSettings.md](../../../Runtime/Core/Table/IDataTableSettings.md)
- [IDataTableUnitSetting.md](../../../Runtime/Core/Table/IDataTableUnitSetting.md)

## 模板输入与生成身份

模板的 bean/table/tables 分别承担行类型、表包装及聚合管理/绑定；JSON 与 Binary 读取链不同，存在模板不证明正式类型已生成或编译。领域行接口依模板键固定为 ILocalizationTextRow、ILocalizationFontRow、ILocalizationSupportedLanguageRow、INetworkCmdRow、INetworkHostKeyRow、ISoundRow、IUIViewRow、IVibrateEmphasisRow、IVibrateCustomRow；通用 table、table-protobuf/default 不据目录名附加这些领域接口。Binary 模板经 ByteBuf，绑定使用 ILubanTables/ITable 及适用的 ILubanTableBinding；Protobuf adapter 有独立模板。

框架逻辑模板路径可解析实际包 resolvedPath，自定义目录原文保留；不解析导航、链接或核全目录结构。Profile 的 target/manager/templateKey 是内部名称，不等于 UPM 包、程序集或 Agent Action；All 返回底层数组，不能当不可变列表。DataTypeNameHelper 只按 Sheet 名和行数阈值筛选，不证明表头与数据语义。完整模板与工具冻结由受控 Action 承担。
