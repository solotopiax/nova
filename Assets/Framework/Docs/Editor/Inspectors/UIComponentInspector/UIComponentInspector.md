# UIComponentInspector

**类签名**：`[CustomEditor(typeof(UIComponent))] internal sealed partial class UIComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.UIComponent`

UI 组件的 Inspector 面板，提供管理器类型选择、Luban 表格导出工具链、对象池参数配置、视图分组列表编辑，以及 Play 模式下的运行时 UIGroup / UIView 状态展示。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `UIComponentInspector.cs` | `sealed partial UIComponentInspector` | 主体：`TemplateFileName`/`TemplateLabelWidth` override、`OnEnable`/`OnDisable` 生命周期、`OnInspectorGUI` 绘制入口 |
| `UIComponentInspector.Visitors.cs` | `partial UIComponentInspector` | 字段：颜色/布局常量、`SerializedProperty` 属性引用、GUIStyle、FileWatcher 相关字段 |
| `UIComponentInspector.Methods.cs` | `partial UIComponentInspector` | 私有方法：`DrawConfigs`、`DrawUIExport`、`DrawUISourceFileRow`、运行时视图分组与 DataTypeName 刷新适配 |

---

## § 3 继承关系

```
UnityEditor.Editor
 └── BaseComponentInspector (abstract, NovaFramework.Editor)
      └── UIComponentInspector (sealed partial)
```

---

## § 4 关键字段表

### 布局常量

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `c_InstanceAutoReleaseIntervalMax` | `float` | `3600f` | 实例自动释放间隔上限（秒） |
| `c_InstanceCapacityMax` | `int` | `128` | 实例池容量上限 |
| `c_InstanceExpireTimeMax` | `float` | `3600f` | 实例过期时间上限（秒） |
| `c_InstancePriorityMin` | `int` | `-100` | 实例优先级下限 |
| `c_InstancePriorityMax` | `int` | `100` | 实例优先级上限 |
| `c_UIColSerialID` | `float` | `60f` | UIView 表格 SerialID 列宽（像素） |
| `c_UIColDepth` | `float` | `50f` | UIView 表格 Depth 列宽（像素） |
| `c_UIColPauseCovered` | `float` | `90f` | UIView 表格 PauseCovered 列宽（像素） |

### SerializedProperty 绑定字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_CurUIManagerTypeName` | `SerializedProperty` | 当前 UI 管理器类型名属性引用 |
| `m_CurUIGroupHelperTypeName` | `SerializedProperty` | 当前视图分组辅助器类型名属性引用 |
| `m_SourceDirPath` | `SerializedProperty` | `UISettings.SourceDirPath` 的缓存引用，避免每帧调用 FindPropertyRelative |
| `m_UIUnitsSettings` | `SerializedProperty` | `UISettings.UIUnitsSettings` 的缓存引用，避免每帧调用 FindPropertyRelative |
| `m_ScreenDesignedResolution` | `SerializedProperty` | 屏幕设计分辨率属性引用 |
| `m_ScreenWidthHeightMatchValue` | `SerializedProperty` | 屏幕宽高适配比例阈值属性引用 |
| `m_DestroyMaxNumPerFrame` | `SerializedProperty` | 每帧最多销毁 UI 数量属性引用 |
| `m_GroupDepthFactor` | `SerializedProperty` | 视图分组深度换算系数属性引用 |
| `m_ViewDepthFactor` | `SerializedProperty` | 视图内部深度换算系数属性引用 |
| `m_InstanceAutoReleaseInterval` | `SerializedProperty` | 实例池自动释放间隔属性引用 |
| `m_InstanceCapacity` | `SerializedProperty` | 实例池容量属性引用 |
| `m_InstanceExpireTime` | `SerializedProperty` | 实例过期时间属性引用 |
| `m_InstancePriority` | `SerializedProperty` | 实例优先级属性引用 |
| `m_InstanceRoot` | `SerializedProperty` | 实例挂载根节点属性引用 |
| `m_UIGroups` | `SerializedProperty` | 视图分组配置数组属性引用 |

### 类型名列表字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_UIManagerTypeNames` | `List<string>` | `IUIManager` 全部实现类名列表 |
| `m_UIGroupHelperTypeNames` | `List<string>` | `IUIGroupHelper` 全部实现类名列表 |

### 目录树字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_FolderFoldoutState` | `Dictionary<string, bool>` | 目录树折叠状态；键为文件夹完整路径，值为是否展开 |

### FileWatcher 字段

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_IsLubanConfigExists` | `bool` | `false` | Luban `_configs/` 目录是否存在 |
| `m_LubanFileWatcherCallback` | `Action` | `null` | FileWatcher 变更回调（缓存以便 Unwatch） |
| `m_WatchedConfigDirPath` | `string` | `null` | 已注册 FileWatcher 的 `_configs/` 目录路径（缓存以确保 Unwatch 路径一致） |

---

## § 5 完整公开 API

```csharp
// --- override 属性 ---
protected override string TemplateFileName          // 返回 "UITemplate.xlsx"
protected override float TemplateLabelWidth         // 返回 c_DirLabelWidth (90f)

// --- Unity Inspector 生命周期 ---
protected override void OnEnable()
private void OnDisable()
public override void OnInspectorGUI()
```

---

## § 8 初始化时序

```
OnEnable()
  │
  ├─ base.OnEnable()                                 ← BaseComponentInspector 基础初始化
  │
  ├─ FindProperty("m_CurUIManagerTypeName")          ─┐
  ├─ FindProperty("m_CurUIGroupHelperTypeName")       │
  ├─ FindProperty("m_ScreenDesignedResolution")       │
  ├─ FindProperty("m_ScreenWidthHeightMatchValue")    │  绑定 SerializedProperty
  ├─ FindProperty("m_DestroyMaxNumPerFrame")          │
  ├─ FindProperty("m_GroupDepthFactor")               │
  ├─ FindProperty("m_ViewDepthFactor")                │
  ├─ FindProperty("m_InstanceAutoReleaseInterval")    │
  ├─ FindProperty("m_InstanceCapacity")               │
  ├─ FindProperty("m_InstanceExpireTime")             │
  ├─ FindProperty("m_InstancePriority")               │
  ├─ FindProperty("m_InstanceRoot")                   │
  └─ FindProperty("m_UIGroups")                      ─┘
  │
  ├─ FindProperty("m_UISettings")                    ─┐
  ├─ uiSettings.FindPropertyRelative("SourceDirPath")   → m_SourceDirPath
  ├─ uiSettings.FindPropertyRelative("UIUnitsSettings") → m_UIUnitsSettings
  └─ InitializeTemplatePath(uiSettings."TemplatePath")  ← BaseComponentInspector 模板路径初始化
  │
  ├─ EditorUtil.TypeCache.GetTypeNames(typeof(IUIManager))      → m_UIManagerTypeNames
  └─ EditorUtil.TypeCache.GetTypeNames(typeof(IUIGroupHelper))  → m_UIGroupHelperTypeNames
  │
  ├─ ConfigSyncer.IsConfigDirExists(sourceDirPath) → m_IsLubanConfigExists
  └─ m_IsLubanConfigExists == true 时：
       ├─ ConfigSyncer.GetConfigDirPath() → m_WatchedConfigDirPath
       └─ FileWatcher.Watch(m_WatchedConfigDirPath, OnLubanConfigChanged)

注意：GetTypeNames 通过 EditorUtil.TypeCache 扫描所有程序集，仅在 OnEnable 调用一次；
      编译完成后 Unity 会重新触发 OnEnable，类型列表自动刷新。

OnDisable()
  │
  └─ m_LubanFileWatcherCallback != null && m_WatchedConfigDirPath 非空 时：
       ├─ FileWatcher.Unwatch(m_WatchedConfigDirPath, m_LubanFileWatcherCallback)
       ├─ m_LubanFileWatcherCallback = null
       └─ m_WatchedConfigDirPath = null
```

---

## § 9 关键算法

### DrawConfigs → DrawUIExport → DrawSourceFilesListWithFolders

```
DrawConfigs()
  │
  ├─ TypesSelector("UI 管理器"、"UIGroup 辅助器")
  ├─ HelpBox（接口说明）
  └─ DrawUIExport()
       │
       ├─ Foldout("UI 表格导出") 未展开 → Line() return
       ├─ DrawTemplatePathHintReadOnly(...)
       ├─ 表格目录位置行（TextField + 选择 + 打开文件夹）
       └─ SourceDirPath 非空且目录存在时：
            ├─ !m_IsLubanConfigExists → HelpBox（提示首次导出自动创建）
            ├─ DrawSourceFilesListWithFolders(directoryPath, m_UIUnitsSettings)
            └─ Button("导出所有数据和类型") → DoRefreshAllDataTypeNames + DoExportAllDataAndTypes

DrawSourceFilesListWithFolders()（委托给 EditorUtil.Draw.SourceFileTree）
  │
  调用 EditorUtil.Draw.SourceFileTree.DrawSourceFilesListWithFolders(
      directoryPath, m_UIUnitsSettings, m_FolderFoldoutState,
      customDrawSourceFileRow: DrawUISourceFileRow)
  │
  SourceFileTree 内部：
  ├─ GetFiles → 扫描 xlsx 文件
  ├─ FileFolderTree.BuildTree → 构建目录树
  ├─ RemoveOrphanUnits → 清除磁盘上已不存在的条目
  └─ DrawFolderNode 递归渲染 Foldout + 调用 DrawUISourceFileRow（自定义文件行）
       └─ 文件名行 + 数据导出行 + 类型导出行 + Asset 地址行
```

### DrawUIGroups → DrawRuntimeUIGroups / DrawRuntimeUIGroup

```
DrawUIGroups()
  │
  ├─ EditorApplication.isPlaying → DrawRuntimeUIGroups() → return
  │
  └─ （Edit 模式：序列化配置编辑）

DrawRuntimeUIGroups()
  │
  ├─ Foldout("视图分组列表({UIGroupCount})", key="UIGroups") → 未展开 return
  │
  └─ foreach IUIGroup in GetAllUIGroups()
       └─ DrawRuntimeUIGroup(uiGroup)
            │
            ├─ Foldout("{Name} ({UIViewCount})"，key="RuntimeUIGroup_{Name}")
            │    └─ 未展开 → return
            │
            └─ Layout.Vertical("box")
                 ├─ Label: 名称 / 深度 / 暂停 / 视图数量
                 ├─ Label: 当前视图 "{AssetLocation}  (SerialID:x)"（null 时显示 "—"）
                 └─ TableRow header + foreach IUIView in GetAllUIViews()
                      └─ TableRow: AssetLocation | SerialID | Depth | PauseCovered

DrawUIGroups（配置模式）
  │
  ├─ Foldout("视图分组列表({arraySize})", key="UIGroups") → 未展开 return
  ├─ IntField("数量", arraySize) → 若值变化且 ≥ 0，更新 arraySize
  └─ for i in [0, arraySize)
       ├─ 取 element.m_Name / m_Depth
       ├─ Foldout(header, key="UIGroups_Element_{i}") → 未展开 continue
       └─ Property("名称") + Property("深度")
```

### 单文件导出入口

当前单文件导出不再经过旧的 `GetOrCreateDetailSettingsForFile` / `BuildRelevantFileNames` / `PopulateDataTypeNames` 这套本地拼装链，而是走：

```text
DrawUISourceFileRow
  ├─ DrawDataExportRow  -> OnExportDataForFile
  ├─ DrawClassExportRow -> OnExportClassForFile
  └─ DoRefreshDataTypeNames

OnExportDataForFile  -> EditorUtil.UI.Exporter.ExportDataForFile
OnExportClassForFile -> EditorUtil.UI.Exporter.ExportCodeForFile
DoExportAllDataAndTypes -> EditorUtil.UI.Exporter.ExportAll
```

`DataTypeNames` 的刷新已经委托给 `EditorUtil.Luban.DataTypeNameHelper.DoRefreshDataTypeNames / DoRefreshAllDataTypeNames`，Inspector 只保留 UI 壳层和参数透传。

---

## § 11 使用示例

UIComponentInspector 由 Unity 通过 `[CustomEditor(typeof(UIComponent))]` 自动绑定，无需手动调用。

**在 Edit 模式下 Inspector 展示（简图）：**
```
[UI 管理器]              TypesSelector → IUIManager 实现类
[UIGroup 辅助器]         TypesSelector → IUIGroupHelper 实现类
───────────────────────────
▼ [UI 表格导出]
  模板文件位置：          （只读提示）
  表格目录位置：          TextField  [选择]  [打开文件夹]
  ▼ UIViews (3)
    ▼ Lobby (2)
      [1] LobbyMain.xlsx   [打开] [打开文件夹]
           数据导出位置：  TextField  [选择] [导出] [打开文件夹]
           类型导出位置：  TextField  [选择] [导出] [打开文件夹]
           Asset 地址：    TextField
    [3] Global.xlsx        [打开] [打开文件夹]
  [导出所有数据和类型]
───────────────────────────
[实例根节点]             ObjectField
───────────────────────────
[视图分组深度换算系数]   IntField
[视图内部深度换算系数]   IntField
  HelpBox: sortingOrder = GroupDepth × GroupDepthFactor + DepthInUIGroup × ViewDepthFactor
──────────────────────────
[屏幕设计分辨率]         Vector2Field
[屏幕宽高适配比例阀值]   FloatSlider    0 ~ 1
[每帧最多销毁 UI 数量]   IntField
───────────────────────────
[实例自动释放间隔] FloatSlider  0 ~ 3600s
[实例池容量]       IntSlider    0 ~ 128
[实例过期时间]     FloatSlider  0 ~ 3600s
[实例优先级]       IntSlider   -100 ~ 100
───────────────────────────
[视图分组列表(N)]  可折叠数组，每项含 名称 + 深度
```

**在 Play 模式下额外展示运行时信息：**
```
视图分组数量: 2
加载中视图序列号: 3, 7
───────────────────────────
▼ Default（1）
  ┌──────────────────────────────┐
  │ 名称:    Default             │
  │ 深度:    0                   │
  │ 暂停:    False               │
  │ 视图数量: 1                  │
  │ 当前视图: UITest  (SerialID:1)│
  │   UITest  1  0  False │
  └──────────────────────────────┘
```

---

## § 12 注意事项

| 场景 | 正确做法 |
|---|---|
| 新增 `IUIManager` 实现类后列表未出现 | 等待编译完成，Unity 会触发 `OnEnable` 刷新类型列表 |
| `m_InstanceRoot` 留空 | `UIComponent.Awake()` 会自动创建 `UIViewInstancesRoot` 节点并挂到自身下 |
| 运行时信息不显示 | 需进入 Play 模式；Edit 模式下 `DrawUIGroups()` 走配置编辑分支，不调用 `DrawRuntimeUIGroups()` |
| 视图分组数组调整大小后不生效 | 调整后须点击 Play 重新初始化；运行时不支持动态增减分组 |
| Luban 配置目录未初始化 | 首次点击「导出」按钮时 Pipeline 会自动创建 `_configs/`，之后 `m_IsLubanConfigExists` 变为 true |
| Inspector 关闭后 FileWatcher 仍在监听 | `OnDisable` 已自动 Unwatch；若频繁开关 Inspector 注意日志中有无重复注册警告 |
| GUIStyle 为 null 导致绘制异常 | `EnsureStylesInitialized` 在每次 `DrawSourceFilesListWithFolders` 前调用，在 `OnEnable` 中不初始化（此时 `EditorStyles` 未就绪） |

---

## § 13 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [UIComponent.md](../../../Runtime/Modules/UI/UIComponent.md)
- [UIManager.md](../../../Runtime/Modules/UI/UIManager/UIManager.md)
- [EditorUtil.Draw.SourceFileTree.md](../../EditorUtil/EditorUtil.Draw/EditorUtil.Draw.SourceFileTree.md)
- [EditorUtil.Luban.ExportHelper.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ExportHelper.md)
- [EditorUtil.Luban.DataTypeNameHelper.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.DataTypeNameHelper.md)
