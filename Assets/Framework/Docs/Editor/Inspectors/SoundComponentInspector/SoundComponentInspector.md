# SoundComponentInspector

**类签名**：`[CustomEditor(typeof(SoundComponent))] internal sealed partial class SoundComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.SoundComponent`

声音组件的 Editor Inspector，提供声音 Manager 类型选择、Luban Pipeline 目录树导出（per-file 独立设置）、声音分组壳配置，以及运行时声音组状态展示。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `SoundComponentInspector.cs` | `sealed partial SoundComponentInspector` | 主体：`OnEnable` 绑定属性、注册 FileWatcher；`OnDisable` 注销 FileWatcher；`OnInspectorGUI` 绘制入口 |
| `SoundComponentInspector.Visitors.cs` | `partial SoundComponentInspector` | 字段：`SerializedProperty` 引用、常量、样式、折叠状态、FileWatcher 缓存 |
| `SoundComponentInspector.Methods.cs` | `partial SoundComponentInspector` | 私有方法：`DrawSoundSettings`、`DrawSourceDataOperations`、`DrawSoundSourceFileRow`、运行时声音组展示、导出/刷新逻辑 |

---

## §3 继承关系

```
UnityEditor.Editor
 └── BaseComponentInspector (abstract)
      └── SoundComponentInspector (sealed partial)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_CurManagerTypeName` | `SerializedProperty` | — | 绑定 `SoundComponent.m_CurManagerTypeName` |
| `m_Settings` | `SerializedProperty` | — | 绑定 `SoundComponent.m_Settings`（`SoundSettings`） |
| `m_SourceDirPath` | `SerializedProperty` | — | `m_Settings.SourceDirPath`，数据源目录路径 |
| `m_SoundUnitsSettings` | `SerializedProperty` | — | `m_Settings.SoundUnitsSettings`，per-file 单元设置列表 |
| `m_AudioMixer` | `SerializedProperty` | — | 绑定 `SoundComponent.m_AudioMixer`（AudioMixer 资产引用） |
| `m_SoundGroupShells` | `SerializedProperty` | — | 绑定 `SoundComponent.m_SoundGroupShells`（声音分组壳数组） |
| `m_ManagerTypeNames` | `List<string>` | — | 反射收集所有 `ISoundManager` 实现类名称 |
| `m_FolderFoldoutState` | `Dictionary<string, bool>` | 空字典 | 目录树各节点折叠状态，键为文件夹完整路径 |
| `m_IsLubanConfigExists` | `bool` | `false` | Luban `_configs/` 目录是否已存在 |
| `m_LubanFileWatcherCallback` | `Action` | `null` | FileWatcher 变更回调缓存（用于 Unwatch） |
| `m_WatchedConfigDirPath` | `string` | `null` | 已注册 FileWatcher 的 `_configs/` 目录路径 |
| `m_RuntimeSoundGroupsFoldout` | `bool` | `false` | 运行时声音组列表折叠状态，默认收起 |

---

## §5 完整公开 API

继承自 `BaseComponentInspector`，无额外 public 方法。

```csharp
// 生命周期（override）
protected override void OnEnable()
// 绑定 SerializedProperty，收集 ISoundManager 类型名，
// 初始化模板路径，注册 Luban FileWatcher

private void OnDisable()
// 注销 Luban FileWatcher

public override void OnInspectorGUI()
// 绘制：base.OnInspectorGUI → DrawSoundSettings → DrawRuntimeSoundGroups → FinalRefreshInspectorGUI

// 模板文件名（override）
protected override string TemplateFileName => "SoundTemplate.xlsx"
protected override float TemplateLabelWidth => c_DirLabelWidth
```

---

## §9 关键算法

### DrawSoundSettings 绘制流程

```
DrawSoundSettings()
  ├── TypesSelector（Sound 管理器）
  ├── HelpBox（支持自定义类型说明）
  ├── 分割线
  ├── DrawSourceDataOperations()     ── Luban Pipeline 导出区域
  ├── AudioMixer Property 绑定
  └── 声音分组壳列表（+ 按钮添加，每个 shell 含 box：名称/避免同优先级替换/静音/音量/代理数量）
```

### DrawSourceDataOperations 绘制流程

```
DrawSourceDataOperations()
  ├── Foldout "声音表格导出"（折叠时直接 return）
  ├── DrawTemplatePathHintReadOnly（模板文件位置）
  ├── 表格目录位置（TextField + 选择 + 打开文件夹）
  ├── 若目录存在：
  │   ├── HelpBox（_configs/ 未初始化时提示）
  │   ├── DrawSourceFilesListWithFolders()   ── 目录树 + per-file 导出设置
  │   └── [导出所有数据和类型] 按钮
  │         → DoRefreshAllDataTypeNames + DoExportAllDataAndTypes
  └── 分割线
```

### 文件树与单文件导出

调用 `EditorUtil.Draw.SourceFileTree.DrawSourceFilesListWithFolders`，传入自定义 `DrawSoundSourceFileRow` 委托。`SourceFileTree` 内部负责：
1. `EditorUtil.FileSystem.GetFiles` 扫描目录
2. `FileFolderTree.BuildTree` 构建目录树
3. 清除孤儿条目、递归渲染 Foldout 与文件行

### DrawSourceFileRow 单文件设置行

每个文件绘制四行（均受目录层级缩进偏移控制）：

| 行 | 内容 |
|----|------|
| 文件名行 | `[序号] 文件名` + 打开 + 打开文件夹 |
| 数据导出位置行 | TextField + 选择 + 导出 + 打开文件夹 |
| 类型导出位置行 | TextField + 选择 + 导出 + 打开文件夹 |
| Asset 地址行 | TextField（`FindPropertyRelative("AssetLocation")`） |

"导出"按钮触发：`DoRefreshDataTypeNames`（委托给 `EditorUtil.Luban.DataTypeNameHelper`）→ `OnExportDataForFile` / `OnExportClassForFile`。

当前单文件和全量导出的真实入口为：

```text
DrawSoundSourceFileRow
  ├─ OnExportDataForFile  -> EditorUtil.Sound.Exporter.ExportData
  ├─ OnExportClassForFile -> EditorUtil.Sound.Exporter.ExportCode
  └─ DoExportAllDataAndTypes -> EditorUtil.Sound.Exporter.ExportAll
```

文档里旧的 `DrawSoundExport`、`GetOrCreateDetailSettingsForFile` 等方法名已经不再是当前实现事实，不应继续引用。

### DrawRuntimeSoundGroups 运行时面板

非 `isPlaying` 状态直接返回。`isPlaying` 时展示可折叠面板：
- 折叠标题：`声音组 ({t.SoundGroupCount})`
- 展开后：`Enable` 状态 + `SoundGroupCount` 数量

---

## §11 使用示例

Inspector 自动挂载，无需手动调用。

```
[编辑器 Inspector 面板]
Sound 管理器：  [SoundManager ▼]
──────────────────────────────────────
▼ 声音表格导出
  模板文件位置：Assets/.../SoundTemplate.xlsx
  表格目录位置：Assets/...  [选择] [打开文件夹]
  ▼ SoundData (3)
    ▼ BGM (1)
        [1] BGM_Main.xlsx  [打开] [打开文件夹]
            数据导出位置：[____________]  [选择] [导出] [打开文件夹]
            类型导出位置：[____________]  [选择] [导出] [打开文件夹]
            Asset 地址：  [____________]
    ▼ SFX (2)
        [2] SFX_UI.xlsx    [打开] [打开文件夹]
            ...
        [3] SFX_Game.xlsx  [打开] [打开文件夹]
            ...
  [导出所有数据和类型]
──────────────────────────────────────
声音混音器：   [None (AudioMixer)]
声音分组壳：   [+]
  ┌─ 名称：BGM                      [-]
  │  避免同优先级替换：  □
  │  静音：             □
  │  音量：             1
  └─ 代理数量：         1
```

---

## §13 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [SoundComponent.md](../../../Runtime/Modules/Sound/SoundComponent.md)
- [SoundSettings.md](../../../Runtime/Modules/Sound/SoundSettings.md)
- [SoundUnitSetting.md](../../../Runtime/Modules/Sound/SoundUnitSetting.md)
- [SoundManager.md](../../../Runtime/Modules/Sound/SoundManager.md)
- [FileFolderTree.md](../../Definitions/FileFolderTree.md)
- [EditorUtil.Luban.Pipeline.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Draw.SourceFileTree.md](../../EditorUtil/EditorUtil.Draw/EditorUtil.Draw.SourceFileTree.md)
- [EditorUtil.Luban.ExportHelper.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ExportHelper.md)
- [EditorUtil.Luban.DataTypeNameHelper.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.DataTypeNameHelper.md)
