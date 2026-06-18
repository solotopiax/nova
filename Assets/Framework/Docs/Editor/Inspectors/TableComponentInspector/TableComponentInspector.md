# TableComponentInspector

**类签名**：`[CustomEditor(typeof(TableComponent))] internal sealed partial class TableComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.TableComponent`

表格组件 Inspector，提供 Manager 类型选择、命名空间配置、Luban 导出流程（代码生成 + 数据导出 + JSON 合并），以及运行时已加载数据表展示。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `TableComponentInspector.cs` | `sealed partial TableComponentInspector` | `OnEnable`（绑定属性、收集类型名、注册 Luban 文件监控）、`OnDisable`（取消监控）、`OnInspectorGUI` |
| `TableComponentInspector.Visitors.cs` | `partial TableComponentInspector` | 所有 `SerializedProperty` 字段、类型名列表、样式常量、折叠状态字典、Luban 监控相关字段 |
| `TableComponentInspector.Methods.cs` | `partial TableComponentInspector` | 私有方法：`DrawConfigs`、`DrawTableExport`、`DrawSourceFilesListWithFolders`、`DrawFolderNode`、`DrawSourceFileRow`、`DrawRuntimeInfos`、`DoExportDataForFile`、`DoExportClassForFile`、`DoExportAllDataAndTypes` 等 |

---

## §3 继承关系

```
UnityEditor.Editor
 └── BaseComponentInspector (abstract)
      └── TableComponentInspector (sealed partial)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_CurManagerTypeName` | `SerializedProperty` | — | 绑定 `TableComponent.m_CurManagerTypeName` |
| `m_Setting` | `SerializedProperty` | — | 绑定 `TableComponent.m_Setting`（`TableSettings`） |
| `m_SourceDirPath` | `SerializedProperty` | — | `m_Setting.SourceDirPath` 缓存 |
| `m_TableUnitsSettings` | `SerializedProperty` | — | `m_Setting.TableUnitsSettings` 缓存 |
| `m_ManagerTypeNames` | `List<string>` | — | 反射收集所有 `ITableManager` 实现类名称 |
| `m_FolderFoldoutState` | `Dictionary<string, bool>` | `{}` | 目录树折叠状态（键为完整路径） |
| `m_RuntimeTablesFoldout` | `bool` | `false` | 运行时数据表列表折叠状态 |
| `m_IsLubanConfigExists` | `bool` | `false` | `_configs/` 目录是否存在 |
| `m_LubanFileWatcherCallback` | `Action` | `null` | `EditorUtil.FileWatcher` 变更回调缓存（用于 `Unwatch`） |
| `m_WatchedConfigDirPath` | `string` | `null` | 已注册监控的 `_configs/` 目录路径缓存 |
| `DirLabelWidth` | `const float` | `90f` | 表格目录位置标签宽度（像素） |
| `c_TemplateMapFileName` | `const string` | `"TableMapTemplate.xlsx"` | 映射格式模板文件名 |
| `c_TemplateOneFileName` | `const string` | `"TableOneTemplate.xlsx"` | 单例格式模板文件名 |

---

## §5 完整公开 API

继承自 `BaseComponentInspector`，无额外 public 方法。

```csharp
// 生命周期（override）
protected override void OnEnable();   // 绑定属性、收集管理器类型名列表、注册 FileWatcher
private void OnDisable();             // 取消 FileWatcher 监控
public override void OnInspectorGUI(); // DrawConfigs → DrawTableExport → DrawRuntimeInfos → FinalRefreshInspectorGUI

// 模板文件名（override）
protected override string TemplateFileName => "TableListTemplate.xlsx"
protected override float TemplateLabelWidth => DirLabelWidth
```

---

## §9 关键算法

### DrawConfigs 绘制流程

```
DrawConfigs()
  ├── TypesSelector（Table 管理器类型选择）
  └── 分割线
```

与旧版相比，移除了 DataPipeline 六个类型选择器（读取器、解析器、序列化器、导出器、代码生成器、文件转换器）。

### DrawTableExport 绘制流程

```
DrawTableExport()
  ├── DrawTemplatePathHintReadOnly(List 模板位置)  // 只读，由 ResolveTemplatePath 动态计算
  ├── DrawTemplatePathHintReadOnly(Map 模板位置)   // 不走序列化
  ├── DrawTemplatePathHintReadOnly(One 模板位置)
  ├── 表格目录位置（Label + TextField + [选择] + [打开文件夹]）
  ├── 若 SourceDirPath 有效：
  │     ├── 若 _configs/ 不存在 → HelpBox 提示"首次导出将自动创建"
  │     ├── DrawSourceFilesListWithFolders(...)
  │     └── [导出所有数据和类型] 按钮
  │           → DoRefreshAllDataTypeNames() + DoExportAllDataAndTypes()
  └── 分割线
```

### DrawSourceFilesListWithFolders 目录树算法

1. 扫描 `SourceDirPath` 下所有 `.xlsx`（排除 `~` 开头的临时文件）
2. `FileFolderTree.BuildTree` 构建树结构
3. 递归 `DrawFolderNode`：根节点 Foldout → 子文件夹 Foldout → `DrawSourceFileRow`

文件行依次绘制：文件名 + `DataTableMode` 枚举下拉 + （Map 模式时）索引字段输入框 + [打开] + [打开文件夹]，以及数据导出位置行（含 [选择] [导出] [打开文件夹]）、类型导出位置行（含 [选择] [导出] [打开文件夹]）、Asset 地址行（`FindPropertyRelative("AssetLocation")`）。

### DoExportAllDataAndTypes Luban 导出流程

```
DoExportAllDataAndTypes(directoryPath, sourceUnitsSettingsProperty)
  ├── EditorUtil.Luban.ConfigSyncer.SyncFromInspector(...)
  │     ├── 若 _configs/ 不存在 → InitializeConfigDir（生成 luban.conf + __tables__.xml）
  │     ├── UpdateLubanConfTopModule(...)
  │     └── GenerateTablesXml(...)（从 TableUnitSetting 列表重新生成 __tables__.xml）
  ├── 收集并清空旧 DatasExportPath / ClassesExportPath
  ├── 若有 classExportPath → EditorUtil.Luban.CliRunner.RunAll(confPath, targetName, classExportPath, tempDir, customTemplateDir)
  │   否则              → EditorUtil.Luban.CliRunner.RunDataExport(confPath, targetName, tempDir)
  ├── EditorUtil.Luban.JsonMerger.MergeAll(tempDir, tablesXmlPath, unitSettings, topModule)
  │     为每个 Excel 文件将 Luban per-table JSON 合并为 Nova per-Excel JSON
  └── 删除临时目录 tempDir
```

### DrawRuntimeInfos 运行时面板

非 `isPlaying` 直接返回。`isPlaying` 时展示：
- 折叠标题：`已加载数据表 ({t.Count}) [已全部加载 | 未全部加载]`
- 展开后遍历 `TableUnitsSettings` 中每个 `DataTypeNames`，拼接 `"Tb" + sheetName` 构造表容器类名，通过反射解析类型后调用 `HasTable(type)`，显示 `TbXxx  Loaded` 或 `TbXxx  Not Loaded`

---

## §11 使用示例

Inspector 自动挂载，无需手动调用。

```
[编辑器 Inspector 面板]
Table 管理器:           [TableManager              ▼]
──────────────────────────────────────
List 模板位置：  Assets/.../TableListTemplate.xlsx
Map 模板位置：   Assets/.../TableMapTemplate.xlsx
One 模板位置：   Assets/.../TableOneTemplate.xlsx
表格目录位置：  Assets/...  [选择] [打开文件夹]
  ▼ TableData (3)
    ▼ Hero (1)
      [1] Hero/HeroData.xlsx   List ▼  [打开] [打开文件夹]
           数据导出位置：  [Assets/.../HeroData.json]  [选择] [导出] [打开文件夹]
           类型导出位置：  [Assets/.../Scripts/]       [选择] [导出] [打开文件夹]
           Asset 地址：    hero_data
    ▼ Config (1)
      [2] Config/GameConfig.xlsx   Map ▼  索引：ID  [打开] [打开文件夹]
           ...
              [导出所有数据和类型]
──────────────────────────────────────

[运行时 Inspector 面板]
▼ 已加载数据表 (3) [已全部加载]
    TbHeroData    Loaded
    TbItemData    Loaded
    TbGameConfig  Not Loaded
──────────────────────────────────────
```

---

## §12 注意事项

- `_configs/` 目录由 `EditorUtil.Luban.ConfigSyncer` 管理，首次导出时自动创建，其内容（`luban.conf`、`__tables__.xml`）由 `SyncFromInspector` 维护，**不要手动修改**
- 映射格式常量 `c_TemplateMapFileName` 已从旧版 `"TableDictTemplate.xlsx"` 更名为 `"TableMapTemplate.xlsx"`，旧文件名在文档中不再使用
- `DoExportAllDataAndTypes` 使用随机后缀临时目录存放 Luban 输出，导出完成后立即删除，若中途报错 `finally` 块确保清理

---

## §13 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [TableComponent.md](../../../Runtime/Modules/Table/TableComponent.md)
- [TableManager.md](../../../Runtime/Modules/Table/TableManager.md)
- [TableSettings.md](../../../Runtime/Modules/Table/Definitions/TableSettings.md)
- [EditorUtil.Luban.ConfigSyncer.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ConfigSyncer.md)
- [EditorUtil.Luban.CliRunner.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.CliRunner.md)
- [EditorUtil.Luban.JsonMerger.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.JsonMerger.md)
- [EditorUtil.FileWatcher.md](../../EditorUtil/EditorUtil.FileWatcher/EditorUtil.FileWatcher.md)
- [FileFolderTree.md](../../Definitions/FileFolderTree.md)
- [EditorUtil.Draw.SourceFileTree.md](../../EditorUtil/EditorUtil.Draw/EditorUtil.Draw.SourceFileTree.md)
- [EditorUtil.Luban.ExportHelper.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ExportHelper.md)
- [EditorUtil.Luban.DataTypeNameHelper.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.DataTypeNameHelper.md)
