# ConfigWindow

**类签名**：`internal sealed partial class ConfigWindow : EditorWindow`
**命名空间**：`NovaFramework.Editor`
**菜单路径**：`Nova/Open Config`（`[MenuItem("Nova/Open Config")]`）

Nova 全局配置窗口，三段式布局（顶栏 + 左树 + 右面板），集中管理 ConfigMasterSO 的 Platform×Channel 矩阵编辑、SDK Plugin 配置、CommonConfig 参数填写、HybridCLR DLL 配置及 Luban/Python3 环境检查。支持一键导出 ConfigRuntimeSO。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/Windows/ConfigWindow/ConfigWindow.cs` | `ConfigWindow` | public 开口：`Open`、`OpenLubanSection` |
| `Editor/Windows/ConfigWindow/ConfigWindow.Visitors.cs` | `ConfigWindow` | 字段：常量 + 运行时状态字段 + `LeftTreeGroup` + `LeftTreeItem` 枚举；新增延迟切坐标守卫字段：`m_HasPendingCoordSwitch`、`m_PendingPlatform`、`m_PendingChannel`、`m_PendingDevelopMode` |
| `Editor/Windows/ConfigWindow/ConfigWindow.Methods.cs` | `ConfigWindow` | 总调度：`OnEnable`、`OnDisable`、`OnGUI`、`DrawBody`、`DrawMainTitle`、`ApplyPendingCoordSwitch`（延迟切坐标，DrawRightPanel 后调用）、`PollChannelChangeForRepaint`、`RefreshPluginCache`、`RunLubanCheck`、`RunPython3Check`、`RestoreExportTargetFromPrefs`、`SaveExportTargetToPrefs`；`EnsureStyles`（GUIStyle 懒初始化） |
| `Editor/Windows/ConfigWindow/ConfigWindow.TopBar.cs` | `ConfigWindow` | 顶栏：`DrawTopBar`、`OnClickSelectExportAsset`、`OnClickSave`、`RebindMaster`、`CreateMasterInteractive`、`PickMasterInteractive`、`RevealMasterInFinder`、`TryApplyPlatformChannel`（延迟写坐标，见 PAT-22 升级）、`TryApplyDevelopMode`（延迟写坐标，见 PAT-22 升级）、`OnClickExport`（导出成功后追加场景 `DevelopMode` 快照回写） |
| `Editor/Windows/ConfigWindow/ConfigWindow.LeftTree.cs` | `ConfigWindow` | 左树：`DrawLeftTree`、`DrawLeftTreeItem`、`DrawSDKTreeItem`、`DrawKitGroupItems`、`DrawKitTreeItem`；SDK/Kit 勾选写 WorkingCopy（`workingSrc.EnabledSDKs/EnabledKits`）+ `m_IsDirty=true`，不直写 `m_Master`，延迟保存机制对齐；`TryChangeSelection` 清除键盘焦点（`GUI.FocusControl(null)` + `EditorGUIUtility.editingTextField=false`）后更新选中状态 |
| `Editor/Windows/ConfigWindow/ConfigWindow.RightPanel.cs` | `ConfigWindow` | 右面板：`DrawRightPanel`、`DrawVerticalSeparator`、`DrawNamespacePanel`、`DrawCommonPanel`、`DrawSDKPanel`、`DrawKitPanel`；标题+掩码内联行：`DrawPanelTitleWithMask`（从 mask 字段读掩码后委托 `DrawTitleWithMaskCore` 绘制标题+三 toggle 行，HelpBox 留本方法；供 Common/Namespace/SDK/Kit/HybridCLR 五面板统一调用）、`DrawTitleWithMaskCore`（矩阵五面板与 YooAsset 面板共用的「标题+三 toggle」核心渲染，toggle 间统一无分隔符，渲染微差由 `titleTrailingSpace` 参数吸收（矩阵 30f / YooAsset 0f），HelpBox 由调用方各自绘制）；Namespace 提交：`CommitNamespaceValue`（按 IsGlobal 双分支写 Namespace，IsGlobal 写 SerializedProperty，否则调 `SetNamespaceAtCoord` 写 Override 并刷新 SerializedObject）；`DrawNamespacePanel` 使用普通 `TextField` 实时提交，按键有变化即触发 `CommitNamespaceValue`（Bug 1 修复后已无 DelayedTextField） |
| `Editor/Windows/ConfigWindow/ConfigWindow.RightPanel.Luban.cs` | `ConfigWindow` | Luban 面板：`DrawLubanSection`、`DrawLubanStatusAndButtons`、`DrawLubanWindowsExportWarning`、`DrawLubanInstallGuide`、`ResolveDotnetStatusText`、`ResolveLubanDllStatusText`、`IsDotnetReady`、`GetLubanWindowsExportWarningText` |
| `Editor/Windows/ConfigWindow/ConfigWindow.RightPanel.Python3.cs` | `ConfigWindow` | Python3 面板：`DrawPython3Section`、`DrawPython3StatusAndButtons`、`ResolvePython3StatusText` |
| `Editor/Windows/ConfigWindow/ConfigWindow.RightPanel.HybridCLR.cs` | `ConfigWindow` | HybridCLR 面板：`DrawHybridCLRPanel`、`DrawHybridCLREntranceSection`、`DrawHybridCLRAotMetadataSection`、`DrawHybridCLRGameDllSection`；ReorderableList 辅助：`EnsureHybridCLRAotMetadataDllsList`、`EnsureHybridCLRGameDllsList`、`DrawHybridCLRDllEntryElementCore`（三字段：源位置 / 目标位置 / Asset 地址）、`OnAddHybridCLRAotMetadataDllEntry`、`OnAddHybridCLRGameDllEntry` |
| `Editor/Windows/ConfigWindow/ConfigWindow.RightPanel.YooAsset.cs` | `ConfigWindow` | YooAsset 配置面板：`DrawRightPanelYooAsset`、`DrawYooAssetSettingsPathRow`、`DrawBundleCollectorSettingPathRow`、`BrowseYooAssetSettingsPath`、`BrowseBundleCollectorSettingPath`、`ToProjectRelativePath`（绝对路径 → 项目根相对）；内联标题行：`DrawYooAssetTitleWithMask`（标题+三 toggle 行委托 `DrawTitleWithMaskCore` 渲染，HelpBox 留本方法；toggle 回调作用于真实资产 m_Master，改完即时 SetDirty + SaveAssetIfDirty + `ReInjectYooAsset`）；`ReInjectYooAsset`（调 `DimensionalResolver.ResolveYooAsset` + `YooAssetInjector.InjectByPath`）；`SyncYooAssetDimensionToWorkingCopy`（维度 toggle 直写 m_Master 后将 YooAssetMask/YooAssetOverrides 补同步到 WorkingCopy） |
| `Editor/Windows/ConfigWindow/ConfigWindow.RightPanel.BindGuide.cs` | `ConfigWindow` | 绑定引导面板（m_Master 为 null 时显示）：`DrawBindGuide`、`BrowseAndBindConfigMaster`、`CreateAndBindConfigMaster`、`BindMaster` |
| `Editor/Windows/ConfigWindow/ConfigWindow.Dialogs.cs` | `ConfigWindow` | 弹框：`ConfirmDiscardDirty`、`HasAnyError`、`ShowValidationDialog`、`PromptMissingRefsIfAny`（启动时检测并可清理 SDK / Kit 的缺失 `SerializeReference`） |

---

## §3 继承关系

```
UnityEditor.EditorWindow
  └── ConfigWindow (internal sealed partial)
```

---

## §4 关键字段表

常量（Visitors.cs）：

| 字段 | 类型 | 值 | 说明 |
|------|------|-----|------|
| `c_MenuPath` | `const string` | `"Nova/Open Config"` | 菜单路径 |
| `c_WindowTitle` | `const string` | `"Nova · Config"` | 窗口标题 |
| `c_WindowMinWidth` | `const float` | `1000f` | 窗口最小宽度 |
| `c_WindowMinHeight` | `const float` | `650f` | 窗口最小高度 |
| `c_TopBarHeight` | `const float` | `60f` | 顶部工具栏高度 |
| `c_LeftTreeWidth` | `const float` | `260f` | 左侧树宽度 |
| `c_InstallCmdMac` | `const string` | `"curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version " + c_MaxDotnetVersion`（展开后：`"...--version 10.0.203"`） | macOS 精确锁版本安装命令（dotnet-install.sh，推荐安装兼容区间上限版本，拼接 `LubanChecker.c_MaxDotnetVersion`） |
| `c_InstallCmdWin` | `const string` | `"&([scriptblock]::Create((irm https://dot.net/v1/dotnet-install.ps1))) -Version " + c_MaxDotnetVersion`（展开后：`"...-Version 10.0.203"`） | Windows 精确锁版本安装命令（dotnet-install.ps1 官方脚本，推荐安装兼容区间上限版本，拼接 `LubanChecker.c_MaxDotnetVersion`） |
| `c_InstallCmdLinux` | `const string` | `"curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version " + c_MaxDotnetVersion`（展开后：`"...--version 10.0.203"`） | Linux 精确锁版本安装命令（dotnet-install.sh，推荐安装兼容区间上限版本，拼接 `LubanChecker.c_MaxDotnetVersion`） |
| `c_DotnetDownloadUrl` | `const string` | `"https://dotnet.microsoft.com/download/dotnet/10.0"` | .NET 官方下载页（上限版本所属主版本 .NET 10） |

运行时状态字段（Visitors.cs）：

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_ScrollPosition` | `Vector2` | `default` | 右侧面板滚动位置 |
| `m_LeftScrollPos` | `Vector2` | `default` | 左侧树滚动位置 |
| `m_ExportTargetAsset` | `ConfigRuntimeSO` | `null` | （已废弃字段，原用于跨会话 EditorPrefs 恢复；现导出目标改由 `m_Master.ExportTarget` 持久化，此字段不再使用） |
| `m_GroupExpandedEnvironment` | `bool` | `true` | 左侧一级组"环境检测"折叠状态 |
| `m_GroupExpandedCommon` | `bool` | `true` | 左侧一级组"通用配置"折叠状态 |
| `m_IsDirty` | `bool` | `false` | 右侧配置面板是否有未保存改动；`DrawRightPanel` 的 `EditorGUI.ChangeCheck` 置 `true`；以下 2 处重置为 `false`：`OnClickSave`、`RebindMaster`；**切换导出目标（ObjectField 变更回调 / `OnClickSelectExportAsset`）不清零** |
| `m_GroupExpandedSDK` | `bool` | `true` | 左侧一级组"SDK 配置"折叠状态 |
| `m_GroupExpandedKit` | `bool` | `true` | 左侧一级组"Kit 配置"折叠状态 |
| `m_LubanCheckResult` | `EnvironmentCheckResult` | `default` | Luban 环境检查结果缓存 |
| `m_Python3CheckResult` | `Python3CheckResult` | `default` | Python3 环境检查结果缓存 |
| `m_MainTitleStyle` | `GUIStyle` | `null` | 窗口顶部居中粗体大标题样式（懒初始化） |
| `m_TitleStyle` | `GUIStyle` | `null` | 大标题样式（懒初始化） |
| `m_SectionTitleStyle` | `GUIStyle` | `null` | 小节标题样式（懒初始化） |
| `m_StatusReadyStyle` | `GUIStyle` | `null` | 就绪状态标签样式（懒初始化） |
| `m_StatusErrorStyle` | `GUIStyle` | `null` | 失败状态标签样式（懒初始化） |
| `m_CodeStyle` | `GUIStyle` | `null` | 命令行代码样式（懒初始化） |
| `m_DescStyle` | `GUIStyle` | `null` | 普通说明文字样式（懒初始化） |
| `m_Master` | `ConfigMasterSO` | `null` | 当前选中的 ConfigMasterSO |
| `m_MasterSO` | `SerializedObject` | `null` | 与 m_Master 绑定的 SerializedObject |
| `m_SelectedItem` | `LeftTreeItem` | `LeftTreeItem.LubanEnv` | 左树当前选中项 |
| `m_SelectedPluginType` | `Type` | `null` | 左树选中的 SDK Plugin 类型（SDKNode 时有效） |
| `m_PluginTypeCache` | `List<EditorUtil.Config.SDKPluginScanner.PluginConfigEntry>` | `new()` | 扫描到的 Plugin 类型缓存（含类型 + 显示名） |
| `m_KitTypeCache` | `List<EditorUtil.Config.KitConfigScanner.KitConfigEntry>` | `new()` | 扫描到的 Kit Config 类型缓存（含类型 + 显示名） |
| `m_HybridCLRAotMetadataDllsList` | `ReorderableList` | `null` | HybridCLR 面板：AOT 元数据 DLL 列表控件，懒初始化 |
| `m_HybridCLRGameDllsList` | `ReorderableList` | `null` | HybridCLR 面板：业务 DLL 列表控件，懒初始化 |
| `m_LastKnownChannel` | `ChannelType` | `default` | 上次 Channel 值，用于 Repaint 轮询对比 |
| `m_HasPendingCoordSwitch` | `bool` | `false` | 延迟切坐标标志；TryApply 置 true，ApplyPendingCoordSwitch 消费并置 false（PAT-22 升级）|
| `m_PendingPlatform` | `PlatformType` | `default` | 待应用的目标平台（延迟切坐标） |
| `m_PendingChannel` | `ChannelType` | `default` | 待应用的目标渠道（延迟切坐标） |
| `m_PendingDevelopMode` | `DevelopMode` | `default` | 待应用的目标开发模式（延迟切坐标） |

`LeftTreeGroup` 嵌套枚举（Visitors.cs）— 左侧树一级组：

| 值 | 说明 |
|----|------|
| `Environment` | 环境检测组 |
| `Common` | 通用配置组 |
| `SDK` | SDK 配置组 |
| `Kit` | Kit 配置组 |

`LeftTreeItem` 嵌套枚举（Visitors.cs）— 左侧树二级节点：

| 值 | 说明 |
|----|------|
| `LubanEnv` | Luban 环境检测面板（环境检测组下） |
| `Python3Env` | Python3 环境检测面板（环境检测组下） |
| `HybridCLREnv` | HybridCLR 环境检测面板（环境检测组下） |
| `AppConfig` | 应用配置面板（通用配置组下） |
| `NamespaceConfig` | 名字空间配置面板（通用配置组下） |
| `HybridCLRConfig` | HybridCLR 配置面板（通用配置组下）：业务入口 Procedure 相对名 + AOT 元数据 DLL 列表 + 业务 DLL 列表 |
| `YooAssetConfig` | YooAsset 配置面板（通用配置组下）：YooAssetSettingsPath / BundleCollectorSettingPath 路径编辑与浏览 |
| `SDKNode` | SDK Plugin 节点面板 |
| `KitNode` | Kit 配置节点面板 |

---

## §5 完整公开 API

```csharp
// 菜单入口：打开窗口（[MenuItem("Nova/Open Config")]）
[MenuItem("Nova/Open Config")]
public static void Open();

// Pipeline 调用入口：打开窗口并展开 Luban 面板
public static void OpenLubanSection(EnvironmentCheckResult result);
```

---

## §8 初始化时序

```
OnEnable()
  ├─ EditorUtil.Config.WorkspaceActive.Get() → m_Master（四段回退；找不到则 null）
  ├─ m_Master != null:
  │    ├─ new SerializedObject(m_Master) → m_MasterSO
  │    ├─ StructureGuard.SyncEnumGrid(m_Master)    补齐矩阵
  │    ├─ RefreshPluginCache()   扫 ISDKPluginConfig 实现类型
  │    ├─ m_LastKnownChannel = m_Master.CurrentChannel
  │    └─ YooAssetInjector.Inject(m_Master)    注入 YooAssetConfiguration
  ├─ EditorSceneManager.sceneOpened += OnSceneOpenedRefresh
  ├─ RunLubanCheck()    跑 Luban 环境检测
  ├─ RunPython3Check()  跑 Python3 环境检测（读 SessionState 缓存，不阻塞）
  └─ PromptMissingRefsIfAny()   检测并弹框处理 SDK / Kit 配置里的 null 占位

OnDisable()
  ├─ EditorSceneManager.sceneOpened -= OnSceneOpenedRefresh
  └─ （无持久化操作；导出目标由 m_Master.ExportTarget 随 SO 保存持久化）

OnSceneOpenedRefresh(scene, mode)   仅响应 Single 加载模式
  ├─ WorkspaceActive.Get() → fresh
  ├─ fresh == m_Master → return（无变化）
  └─ fresh != m_Master → m_Master = fresh; new SerializedObject; YooAssetInjector.Inject; Repaint

OpenLubanSection(result)
  ├─ GetWindow<ConfigWindow>() → 打开或聚焦窗口
  └─ window.m_LubanCheckResult = result
```

---

## §9 关键算法

### 布局流

```
OnGUI()
  └─ EnsureStyles()（懒初始化 GUIStyle）
     └─ DisabledScope(isPlayingOrWillChangePlaymode)
           └─ DrawBody()
                ├─ DrawMainTitle()   居中粗体大标题行
                ├─ DrawTopBar()      顶部工具栏（SO 选择 + Platform/Channel/DevelopMode + 导出）
                ├─ HorizontalScope:
                │    ├─ DrawLeftTree()   左树（三组：环境检测 / 通用配置 / SDK）
                │    └─ DrawRightPanel() 右面板（分发到具体 Panel）
                ├─ ApplyPendingCoordSwitch()   延迟切坐标：消费 TryApply 记录的 pending，让数字字段在旧坐标格子下完成失焦提交后再换坐标（PAT-22 升级）
                └─ PollChannelChangeForRepaint()
```

### 顶栏布局（DrawTopBar）

```
第一行（SO 选择 + 保存，"helpBox" 容器）：
  Label("编辑器存档文件：", 100f) + ObjectField<ConfigMasterSO>
  + Button("创建", 64f) + Button("选择", 64f)
  + DisabledScope(!m_IsDirty) → SuccessButton("保存", 64f)   ← m_IsDirty=false 时禁用
  + Button("打开文件夹", 90f)

第二行（仅 m_Master != null 时，labelWidth=80f）：
  Label("平台类型：", 64f) + EnumPopup<PlatformType>(120f) + Space(24f)
  Label("渠道类型：", 64f) + EnumPopup<ChannelType>(120f) + Space(24f)
  Label("开发模式：", 64f) + EnumPopup<DevelopMode>(120f)
  FlexibleSpace →
  Label("导出文件：", 60f) + ObjectField<ConfigRuntimeSO>(200f，绑定 m_Master.ExportTarget) + Button("选择", 64f)
  SuccessButton("导出", 64f)
  DisabledScope(m_Master.ExportTarget==null) → Button("打开文件夹", 90f)
```

### 导出附加动作

`OnClickExport()` 在 `EditorUtil.Config.Exporter.Export(...)` 成功后，还会调用：

- `EditorUtil.Config.SceneDevelopModeWriter.WriteActiveScene(m_Master.CurrentDevelopMode)`

作用是把当前激活场景中的 `Nova + 所有 FrameworkComponent` 的 `m_DevelopMode` 序列化快照更新为这次导出选中的 `DevelopMode`，供启动早期模块在 `Config.LoadAsync()` 之前直接读取。

### 右面板分发（DrawRightPanel）

```
DrawRightPanel()
  DrawVerticalSeparator()（1px 竖分割线）
  VerticalScope(EditorStyles.helpBox):
    m_Master == null → DrawBindGuide() → return（引导卡片，跳过 ChangeCheck）
    EditorGUI.BeginChangeCheck()
    switch (m_SelectedItem):
      LubanEnv        → DrawLubanSection()
      Python3Env      → DrawPython3Section()
      HybridCLREnv    → DrawHybridCLREnvSection()
      AppConfig       → DrawCommonPanel()       ← m_MasterSO != null 时有效
      NamespaceConfig → DrawNamespacePanel()    ← m_MasterSO != null 时有效
      HybridCLRConfig → DrawHybridCLRPanel()    ← m_MasterSO != null 时有效
      YooAssetConfig  → DrawRightPanelYooAsset() ← m_Master != null 时有效
      SDKNode         → DrawSDKPanel()           ← m_SelectedPluginType != null 时有效
      KitNode         → DrawKitPanel()           ← m_SelectedPluginType != null 时有效
      default         → HelpBox("请在左侧选择一项开始编辑。")
    EndChangeCheck() → true → m_IsDirty = true
```

### HybridCLR 面板布局（DrawHybridCLRPanel）

```
DrawHybridCLRPanel()
  DrawHybridCLREntranceSection()
    标题：Label("业务入口 Procedure（相对名）", m_SectionTitleStyle)
    说明文本：不含 namespace 的类名，如 "ProcedurePreload"
    TextField（labelWidth=100f）← 绑定 master.GameEntranceProcedureName

  DrawHybridCLRAotMetadataSection()
    标题：Label("AOT 元数据 DLL 列表", m_SectionTitleStyle)
    EnsureHybridCLRAotMetadataDllsList() → 懒初始化 m_HybridCLRAotMetadataDllsList
    m_HybridCLRAotMetadataDllsList.DoLayoutList()
      每条目：DrawHybridCLRDllEntryElementCore（三行：源位置 / 目标位置 / Asset 地址，labelWidth=80f，elementHeight=singleLineHeight*3+10f）

  DrawHybridCLRGameDllSection()
    标题：Label("业务 DLL 列表", m_SectionTitleStyle)
    EnsureHybridCLRGameDllsList() → 懒初始化 m_HybridCLRGameDllsList
    m_HybridCLRGameDllsList.DoLayoutList()
      每条目：DrawHybridCLRDllEntryElementCore（三行：源位置 / 目标位置 / Asset 地址，布局同上）
```

### Luban 面板布局（DrawLubanSection）

```
标题行：Label("Luban 环境检测", m_SectionTitleStyle)
Space(8f)
DrawLubanStatusAndButtons():
  状态行：Space(8f) + [dotnet 图标+状态文本] + "|" 分隔 + [Luban.dll 图标+状态文本]
  按钮行（右对齐）：FlexibleSpace + Button("重新检测", 80f) + Space(2f) + Button("打开官网", 80f)
DrawLubanWindowsExportWarning():
  触发条件：Application.platform == WindowsEditor
  在状态区与安装指南之间插入 HelpBox
  文案说明：Win11 可能在 Luban 导出时提示“应用程序控制策略已阻止此文件”，根因是操作系统智能应用控制限制，并给出关闭路径：
    设置 -> 隐私和安全性 -> Windows安全中心 -> 应用和浏览器控制 -> 智能应用控制设置 -> 关闭
DrawLubanInstallGuide():
  触发条件：Issue == DotnetNotFound 或 DotnetVersionTooLow 或 DotnetVersionTooHigh（三种情况均显示）
  版本区间说明：兼容区间为 [8.0.127, 10.0.203] 闭区间，过低/过高均为硬阻断
  分平台精确锁版本安装命令（对应常量 c_InstallCmdMac / c_InstallCmdWin / c_InstallCmdLinux，均拼接 LubanChecker.c_MaxDotnetVersion，当前上限 10.0.203）：
    macOS:   curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version 10.0.203
    Linux:   curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version 10.0.203
    Windows: &([scriptblock]::Create((irm https://dot.net/v1/dotnet-install.ps1))) -Version 10.0.203
  含安装命令代码块（平台名称 + 命令 + 复制按钮）+ 官网链接（c_DotnetDownloadUrl）
```

### Python3 面板布局（DrawPython3Section）

```
标题行：Label("Python3 环境检测", m_SectionTitleStyle)
Space(8f)
DrawPython3StatusAndButtons():
  状态行：Space(8f) + [图标(✓/✗)] + Label("Python3", 72f) + Label(ResolvePython3StatusText(), 200f)
          注意：状态文本仅包含就绪状态和版本号，不展示 Python3 可执行路径
  按钮行（独立行，右对齐）：FlexibleSpace + Button("重新检测", 80f)
  Space(2f)
```

### 导出流（OnClickExport）

```
OnClickExport()
  1. Validator.Validate(master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode)
  2. HasAnyError(issues) → ShowValidationDialog → return（阻断）
  3. master.ExportTarget == null → DisplayDialog("未设置导出文件") → return
  4. assetPath = AssetDatabase.GetAssetPath(master.ExportTarget)
  5. Exporter.Export(master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode, assetPath)
  6. master.ExportTarget 为 null 时用导出结果写回
  7. DisplayDialog("导出成功")
```

> **注意**：导出目标不再通过 `EditorPrefs` GUID 存储，改由 `ConfigMasterSO.ExportTarget`（`[SerializeField]`）直接持久化到 SO 资产中；`RestoreExportTargetFromPrefs` / `SaveExportTargetToPrefs` / `OnDisable` 中的 GUID 持久化链路已删除。

---

## §10 常见误区

| 误区 | 正确理解 |
|------|---------|
| Play Mode 下编辑 | 窗口整体 DisabledScope，顶部 HelpBox 提示"请退出 Play Mode 后编辑" |
| 直接切换 Master 丢失改动 | `RebindMaster` 调 `ConfirmDiscardDirty` 弹三选一对话框（保存/取消/丢弃） |
| 导出前未填 Common 字段 | `Validator.Validate` 检查 CommonConfig 必填字段，有 Error 时阻断导出 |
| 导出目标 SO 重启后丢失 | 导出目标改由 `ConfigMasterSO.ExportTarget`（`[SerializeField]`）持久化，SO 保存后跨会话自动恢复；无需 EditorPrefs |
| `m_SelectedItem` 默认值 | 默认为 `LeftTreeItem.LubanEnv`，不是 `Environment` |
| "保存"按钮灰色点不了 | `m_IsDirty == false` 时 DisabledScope 禁用按钮；在右侧面板编辑任意字段后 ChangeCheck 自动置 `true` |
| 切换导出目标后保存按钮仍然灰色 | 正常现象。切换导出目标（ObjectField 回调 / `OnClickSelectExportAsset`）不清零 `m_IsDirty`，也不置位；导出目标切换不影响 ConfigMasterSO 的脏状态 |
| 改完字段直接关窗口 | 右侧 ChangeCheck 只负责置位 `m_IsDirty`，实际持久化依赖"保存"按钮触发 `AssetDatabase.SaveAssets`；关窗口前须点保存 |

---

## §11 使用示例

```csharp
// 菜单打开（普通情况）
// Nova → Open Config

// Pipeline 中 Luban 环境检测失败时自动弹出并展开 Luban 面板
EditorUtil.Environment.LubanChecker.EnvironmentCheckResult envResult =
    EditorUtil.Environment.LubanChecker.Check();
if (!envResult.IsReady)
{
    ConfigWindow.OpenLubanSection(envResult);
    return false;
}
```

---

## §12 注意事项

| 场景 | 说明 |
|------|------|
| 扩展新 Section | 在 `LeftTreeItem` 枚举增值，`DrawLeftTree` 增菜单项，`DrawRightPanel` 增 case，新建 `ConfigWindow.RightPanel.Xxx.cs` |
| GUIStyle 懒初始化 | 所有样式在 `ConfigWindow.Methods.cs` 的 `EnsureStyles` 中延迟初始化，避免序列化阶段访问 `EditorStyles`；每个样式独立 null 判断，防止域重载后 native 层失效 |
| `internal` 可见性 | 窗口不对外部包公开，仅供框架内部 Pipeline/菜单调用 |
| `DrawCommonPanel` 无 Foldout | CommonConfig 子字段直接遍历展开绘制，不包裹外层"公共参数"折叠容器 |
| `DrawNamespacePanel` 标题标签 | `DrawPanelTitleWithMask` 传入标题为"命名空间配置" |

---

## §13 关联文档

- [ConfigMasterSO.md](../../Runtime/Modules/Config/ConfigMasterSO.md)
- [ConfigRuntimeSO.md](../../Runtime/Modules/Config/ConfigRuntimeSO.md)
- [EditorUtil.Asset.Operator.md](../EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Operator.md)
- [EditorUtil.Config.StructureGuard.md](../EditorUtil/EditorUtil.Config/EditorUtil.Config.StructureGuard.md)
- [EditorUtil.Config.SDKPluginScanner.md](../EditorUtil/EditorUtil.Config/EditorUtil.Config.SDKPluginScanner.md)
- [EditorUtil.Config.KitConfigScanner.md](../EditorUtil/EditorUtil.Config/EditorUtil.Config.KitConfigScanner.md)
- [EditorUtil.Config.Validator.md](../EditorUtil/EditorUtil.Config/EditorUtil.Config.Validator.md)
- [EditorUtil.Config.Exporter.md](../EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.md)
- [EditorUtil.Config.WorkspaceActive.md](../EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.md)（OnEnable / OnSceneOpenedRefresh 读取激活 Master）
- [EditorUtil.Config.YooAssetInjector.md](../EditorUtil/EditorUtil.Config/EditorUtil.Config.YooAssetInjector.md)（OnEnable / OnSceneOpenedRefresh / DrawRightPanelYooAsset 注入调用）
- [EditorUtil.Config.DimensionProjector.md](../EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.md)（`DrawDimensionMaskRow` 触发三操作）
- [EditorUtil.Config.DimensionalResolver.md](../EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md)（`DrawYooAssetTitleWithMask` / `ReInjectYooAsset` 取数）
- [EditorUtil.Environment.LubanChecker.md](../EditorUtil/EditorUtil.Environment/EditorUtil.Environment.LubanChecker.md)
- [EditorUtil.Environment.Python3.md](../EditorUtil/EditorUtil.Environment/EditorUtil.Environment.Python3.md)
