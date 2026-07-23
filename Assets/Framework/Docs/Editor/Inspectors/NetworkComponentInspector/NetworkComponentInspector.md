# NetworkComponentInspector

**类签名**：`[CustomEditor(typeof(NetworkComponent))] internal sealed partial class NetworkComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.NetworkComponent`

Network 组件的 Inspector 面板，提供四个管理器实现类选择器、HostKey/NetCmd 双导出区，以及 Proto 协议管理区（`.proto` 目录树、单元预建、批量导出）。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `NetworkComponentInspector.cs` | `sealed partial NetworkComponentInspector` | 主体：`OnEnable` 绑定 SerializedProperty、`OnDisable` 清理 FileWatcher、`OnInspectorGUI` 调度绘制入口 |
| `NetworkComponentInspector.Visitors.cs` | `partial NetworkComponentInspector` | 字段：全部 `SerializedProperty` 引用、类型名列表、Proto/Luban 相关字段与缓存、FileWatcher 回调 |
| `NetworkComponentInspector.Methods.cs` | `partial NetworkComponentInspector` | 私有方法：`DrawManagerSelectors`、`DrawHostKeyExport`、`DrawNetCmdExport`、`DrawProtoManagement`、`DrawHttpSettings`、`DrawDoHSettings`、`DrawWebSocketSettings` 与 FileWatcher 初始化 |

---

## § 3 继承关系

```
UnityEditor.Editor
  └── BaseComponentInspector (abstract, NovaFramework.Editor)
       └── NetworkComponentInspector (sealed partial)
```

---

## § 4 关键字段表

### 管理器选择 & 配置

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_CurNetworkManagerTypeName` | `SerializedProperty` | 绑定 `m_CurNetworkManagerTypeName` |
| `m_NetworkManagerTypeNames` | `List<string>` | `INetworkManager` 全部实现类名列表 |
| `m_CurHttpManagerTypeName` | `SerializedProperty` | 绑定 `m_CurHttpManagerTypeName` |
| `m_HttpManagerTypeNames` | `List<string>` | `IHttpManager` 全部实现类名列表 |
| `m_CurDoHManagerTypeName` | `SerializedProperty` | 绑定 `m_CurDoHManagerTypeName` |
| `m_DoHManagerTypeNames` | `List<string>` | `IDoHManager` 全部实现类名列表 |
| `m_CurWebSocketManagerTypeName` | `SerializedProperty` | 绑定 `m_CurWebSocketManagerTypeName` |
| `m_WebSocketManagerTypeNames` | `List<string>` | `IWebSocketManager` 全部实现类名列表 |
| `m_DoHSettings` | `SerializedProperty` | 绑定 `m_DoHSettings`（DoH 配置对象节点） |
| `m_HttpSettings` | `SerializedProperty` | 绑定 `m_HttpSettings`（Http 配置对象节点） |
| `m_WebSocketSettings` | `SerializedProperty` | 绑定 `m_WebSocketSettings`（WebSocket 配置对象节点） |

### 业务上下文

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_Settings` | `SerializedProperty` | 绑定 `m_Settings`（NetworkSettings 配置对象） |

### HostKey 管理区

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_HostKeySourceDirPath` | `SerializedProperty` | 绑定 `HostKeySettings.SourceDirPath`（数据源目录路径属性） |
| `m_HostKeyUnitsSettings` | `SerializedProperty` | `HostKeySettings.HostKeyUnits` 列表 |
| `m_HostKeyFolderFoldoutState` | `Dictionary<string, bool>` | HostKey 目录树各节点折叠状态缓存 |
| `m_IsHostKeyLubanConfigExists` | `bool` | HostKey 目录下 `_configs/` 是否存在 |
| `m_HostKeyLubanFileWatcherCallback` | `System.Action` | HostKey `_configs/` FileWatcher 回调缓存（供 OnDisable Unwatch） |
| `m_WatchedHostKeyConfigDirPath` | `string` | 已注册 HostKey FileWatcher 的 `_configs/` 目录路径缓存 |
| `c_HostKeyTemplateFileName` | `const string` | 域名表模板文件名（`"NetworkHostKeysTemplate.xlsx"`） |

### NetCmd 管理区

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_NetCmdSourceDirPath` | `SerializedProperty` | 绑定 `NetCmdSettings.SourceDirPath`（数据源目录路径属性） |
| `m_NetCmdUnitsSettings` | `SerializedProperty` | `NetCmdSettings.NetCmdUnits` 列表 |
| `m_NetCmdFolderFoldoutState` | `Dictionary<string, bool>` | NetCmd 目录树各节点折叠状态缓存 |
| `m_IsNetCmdLubanConfigExists` | `bool` | NetCmd 目录下 `_configs/` 是否存在 |
| `m_NetCmdLubanFileWatcherCallback` | `System.Action` | NetCmd `_configs/` FileWatcher 回调缓存（供 OnDisable Unwatch） |
| `m_WatchedNetCmdConfigDirPath` | `string` | 已注册 NetCmd FileWatcher 的 `_configs/` 目录路径缓存 |
| `c_NetCmdTemplateFileName` | `const string` | 指令表模板文件名（`"NetworkCmdsTemplate.xlsx"`） |

### Proto 管理区

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_ProtoSettings` | `SerializedProperty` | 绑定 `m_ProtoSettings`（ProtoSettings 对象节点） |
| `m_ProtoSourceDirPath` | `SerializedProperty` | 绑定 `m_ProtoSettings.ProtoSourceDirPath`（.proto 文件根目录） |
| `m_ProtoUnitsSettings` | `SerializedProperty` | 绑定 `m_ProtoSettings.ProtoUnits`（.proto 单元设置列表） |
| `m_ProtoFileFolderFoldoutState` | `Dictionary<string, bool>` | .proto 文件目录树各节点的 Foldout 折叠状态缓存 |
| `m_CachedProtoFiles` | `string[]` | .proto 文件列表缓存（Layout 事件刷新，同步预建 ProtoUnitSetting 条目） |
| `s_ProtoFileNameStyle` | `GUIStyle` | .proto 文件名标签样式（静态，浅青色） |


---

## § 5 完整公开 API

```csharp
// Unity Inspector 生命周期
protected override void OnEnable()
private void OnDisable()
public override void OnInspectorGUI()

// 绘制方法（私有）
private void DrawManagerSelectors()
private void DrawHostKeyExport()
private void DrawNetCmdExport()
private void DrawDoHSettings()
private void DrawHttpSettings()
private void DrawWebSocketSettings()
private void DrawProtoManagement()

// 辅助绘制（私有）
private void DrawSourceDirRow(SerializedProperty sourceDirPath, string label, string dialogTitle)
private void DrawNetworkSourceFilesListWithFolders(string directoryPath, SerializedProperty unitsSettingsProp, Dictionary<string, bool> foldoutState, string settingsPropertyName)
private void DrawNetworkSourceFileRow(...)
private void DrawRuntimeWebSocketChannels(NetworkComponent t)
private void DrawRuntimeDoHAddresses(NetworkComponent t)
private void DrawProtoFilesTree(string protoDir)
private void DrawProtoFolderNode(FileFolderTree.TreeNode node, string rootPathNorm, SerializedProperty currentProtoUnits)
private void DrawProtoFileRow(string filePath, string rootPathNorm, int seq, float indentSpace, int savedIndent, SerializedProperty currentProtoUnits)
private void DrawProtoExportButton()

// 导出执行（私有）
private void DoExportDataForFile(string filePath, string dataExportPath, string regionDirPath, string settingsPropertyName)
private void DoExportClassForFile(string filePath, string classExportPath, string regionDirPath, string settingsPropertyName)
private void DoExportAllDataAndTypes(SerializedProperty unitsSettingsProp, string settingsPropertyName)

// FileWatcher 回调（私有）
private void OnHostKeyLubanConfigChanged()
private void OnNetCmdLubanConfigChanged()

// 工具方法（私有）
private IDataTableSettings GetDataTableSettings(string settingsPropertyName)
private IReadOnlyList<IDataTableUnitSetting> GetCurrentRegionUnitSettings(string settingsPropertyName)
private SerializedProperty GetOrCreateProtoUnitSetting(string sourceRelativePath, SerializedProperty currentProtoUnits)
private static void EnsureProtoFileStylesInitialized()
```

---

## § 8 初始化时序

```
OnEnable()
  │
  ├─ base.OnEnable()
  │
  ├─ FindProperty("m_CurNetworkManagerTypeName")     → m_CurNetworkManagerTypeName
  ├─ FindProperty("m_CurHttpManagerTypeName")        → m_CurHttpManagerTypeName
  ├─ FindProperty("m_CurDoHManagerTypeName")         → m_CurDoHManagerTypeName
  ├─ FindProperty("m_CurWebSocketManagerTypeName")   → m_CurWebSocketManagerTypeName
  │
  ├─ FindProperty("m_Settings")                      → m_Settings
  │
  ├─ m_Settings.HostKeySettings.FindPropertyRelative("SourceDirPath") → m_HostKeySourceDirPath
  ├─ m_Settings.HostKeySettings.FindPropertyRelative("HostKeyUnits")  → m_HostKeyUnitsSettings
  ├─ ConfigSyncer.IsConfigDirExists(hostKeyBasePath) → m_IsHostKeyLubanConfigExists
  └─ FileWatcher.Watch(hostKeyConfigDir, ...)        → 监控 HostKey _configs/ 目录（若存在）
  │
  ├─ m_Settings.NetCmdSettings.FindPropertyRelative("SourceDirPath")  → m_NetCmdSourceDirPath
  ├─ m_Settings.NetCmdSettings.FindPropertyRelative("NetCmdUnits")    → m_NetCmdUnitsSettings
  ├─ ConfigSyncer.IsConfigDirExists(netCmdBasePath)  → m_IsNetCmdLubanConfigExists
  └─ FileWatcher.Watch(netCmdConfigDir, ...)         → 监控 NetCmd _configs/ 目录（若存在）
  │
  ├─ FindProperty("m_DoHSettings/m_HttpSettings/m_WebSocketSettings") → 三个设置对象节点
  │
  ├─ TypeCache.GetTypeNames × 4                      → 运行时管理器类型列表
  │
  └─ FindProperty("m_ProtoSettings")                 → m_ProtoSettings
      ├─ FindPropertyRelative("ProtoSourceDirPath")  → m_ProtoSourceDirPath
      └─ FindPropertyRelative("ProtoUnits")          → m_ProtoUnitsSettings

OnDisable()
  ├─ FileWatcher.Unwatch(m_WatchedHostKeyConfigDirPath, m_HostKeyLubanFileWatcherCallback) → HostKey
  └─ FileWatcher.Unwatch(m_WatchedNetCmdConfigDirPath, m_NetCmdLubanFileWatcherCallback)  → NetCmd
```

---

## § 9 关键算法

### Proto 批量导出管线（DrawProtoExportButton）

点击"导出所有协议和类型"按钮时执行：

```
1. 扫描 Proto 根目录下的 `.proto` 文件
   — Layout 阶段会确保每个文件都有对应的 `ProtoUnitSetting`

2. serializedObject.ApplyModifiedProperties()
   — 将单元预建或路径调整持久化到 ScriptableObject

3. GetProtoSettings()（反射获取 ProtoSettings 实例）

4. EditorUtil.Network.ProtoExporter.ExportAllProtos(networkComponent.ProtoSettings)
   — 由导出器统一完成 `.proto` -> C# 的批量编译链

5. AssetDatabase.Refresh()
```

### `.proto` 文件树预建 ProtoUnitSetting（Layout 事件）

仅在 `EventType.Layout` 时执行，确保 Repaint 路径控件数量不变：

```
1. GetFiles(protoDir) → m_CachedProtoFiles（刷新文件列表）

2. 遍历所有 `.proto` 文件：
   — 计算相对路径 relativePath
   — 在 m_ProtoUnitsSettings 中搜索匹配的 SourcePath
   — 若不存在则追加新 ProtoUnitSetting 条目，写入 SourcePath

3. 若有新增条目，serializedObject.ApplyModifiedProperties()
```

### HostKey/NetCmd 全量导出管线（DrawHostKeyExport / DrawNetCmdExport）

点击"导出所有域名表/指令表数据和类型"按钮时执行：

```
1. serializedObject.ApplyModifiedProperties()

2. EditorUtil.Network.HostKeyExporter / NetCmdExporter
   — 公共门面把全量请求交给 NetworkExporter

3. NetworkExporter
   — HostKeys 校验 Debug/Release 配对并按 ConfigRuntime.DevelopMode 选择 Sheet；NetCmds 保持原结构
   — Luban 只写入 _temp/_publish，完整验证 JSON/C# 后再通过 FileSystem.OutputApplier 批量发布

4. finally 清理 _temp 并刷新 AssetDatabase
```

文件树中的单文件数据/类型按钮也调用同一 `NetworkExporter`，不再绕过暂存发布直接写正式产物。

---

## § 11 使用示例

`NetworkComponentInspector` 由 Unity 通过 `[CustomEditor(typeof(NetworkComponent))]` 自动绑定，无需手动调用。

**Inspector 布局：**

```
[Network 管理器]       TypesSelector → INetworkManager 实现类
[HTTP 管理器]          TypesSelector → IHttpManager 实现类
[DoH 管理器]           TypesSelector → IDoHManager 实现类
[WebSocket 管理器]     TypesSelector → IWebSocketManager 实现类
HelpBox 说明
─────────────────────────────────────────────────
域名表目录位置         TextField + 选择 + 打开文件夹
指令表目录位置         TextField + 选择 + 打开文件夹
协议目录位置           TextField + 选择 + 打开文件夹
─────────────────────────────────────────────────
[域名表 (HostKey) ▼]  Foldout
  模板文件位置           只读灰色提示（c_HostKeyTemplateFileName 在当前地域目录）
  表格目录位置           TextField + 选择 + 打开文件夹
  （_configs/ 未初始化时显示 HelpBox 提示）
  [文件树]              可折叠目录树，每行：文件名 + 打开/打开文件夹 + 数据/类型导出行 + AB/Asset
  导出所有域名表数据和类型  Button
─────────────────────────────────────────────────
[指令表 (NetCmd) ▼]   Foldout
  模板文件位置           只读灰色提示（c_NetCmdTemplateFileName 在当前地域目录）
  表格目录位置           TextField + 选择 + 打开文件夹
  （_configs/ 未初始化时显示 HelpBox 提示）
  [文件树]              可折叠目录树，每行：文件名 + 打开/打开文件夹 + 数据/类型导出行 + AB/Asset
  导出所有指令表数据和类型  Button
─────────────────────────────────────────────────
  [Proto 协议导出 ▼]    Foldout
  Proto 根目录            TextField + 选择 + 打开文件夹
  [.proto 文件树]         可折叠目录树，Layout 阶段自动预建 ProtoUnitSetting
  每个 .proto 行含：打开 / 打开文件夹 / 类型导出位置（CSharpExportPath）/ 单文件 [导出] 按钮
  导出所有协议类型      Button（委托 ProtoExporter 批量编译，Proto 目录未配置时 DisabledGroup 灰显）
─────────────────────────────────────────────────
HTTP 设置             Foldout（连接/请求超时）
─────────────────────────────────────────────────
DoH 设置              UseDoH Toggle / 单次 DoH 查询超时 / 候选地址说明 / 运行时解析诊断树
  单次 DoH 查询超时时间（秒）
  HelpBox：
    每个域名的一次 DoH 查询独立计时，默认 3 秒，0 表示跳过 DoH 查询。
    查询期间的所有候选地址：
    1. https://1.1.1.1/dns-query
    2. https://1.0.0.1/dns-query
    3. https://cloudflare-dns.com/dns-query
    将共用该超时时间。
  [Play 模式附加] DoH 解析列表
    HostKey 预热             原始 HostKey 域名根节点
      CNAME 中间域名         作为父域名子节点；IP 在对应域名下一级
    运行时按需发现           HostKey 范围外由请求链路触发的域名
    未获取 IP 的节点         红色“[未获取 IP]”标注，失败根节点不会被隐藏
─────────────────────────────────────────────────
WebSocket 设置        超时/心跳/重连参数 / 运行时通道列表
  启用自动重连 Toggle（关闭时下方重连最大次数/间隔/失败 UI 整段 DisabledGroup 灰显）
  WS 自动重连失败 UI  Foldout（嵌套于启用自动重连子区，配置 Asset 地址）
  [Play 模式附加] 运行时 WebSocket 通道列表（绿/红/黄 标识 IsConnected / IsDisconnectedSubjectively / 其他）
```

---

## § 12 注意事项

**FileWatcher 必须在 OnDisable 中清理**：Inspector 被销毁或重新选中时，`OnDisable` 会调用 `FileWatcher.Unwatch` 注销两路监控（HostKey + NetCmd 的 `_configs/`）。若未清理，FileWatcher 将持有已销毁的 Inspector 实例引用，回调时会引发 `MissingReferenceException`。

**两路 FileWatcher 监控**：
- `m_WatchedHostKeyConfigDirPath` — 监控 HostKey 地域目录下 `_configs/`，变更时将 `m_IsHostKeyLubanConfigExists` 置为 `true` 并 `Repaint`
- `m_WatchedNetCmdConfigDirPath` — 监控 NetCmd 地域目录下 `_configs/`，变更时将 `m_IsNetCmdLubanConfigExists` 置为 `true` 并 `Repaint`

**Proto 文件树预建逻辑只在 Layout 事件执行**：Repaint 路径不修改 SerializedProperty，确保两次 IMGUI 事件间控件数量一致，避免 Unity 布局断言错误。

---

## § 13 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [NetworkComponent.md](../../../Runtime/Modules/Network/NetworkComponent.md)
- [ProtoSettings.md](../../../Runtime/Modules/Network/Definitions/ProtoSettings.md)
- [EditorUtil.Proto.CliRunner.md](../../EditorUtil/EditorUtil.Proto/EditorUtil.Proto.CliRunner.md)
- [EditorUtil.Luban.ConfigSyncer.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ConfigSyncer.md)
- [EditorUtil.FileWatcher.md](../../EditorUtil/EditorUtil.FileWatcher/EditorUtil.FileWatcher.md)
- [EditorUtil.Draw.SourceFileTree.md](../../EditorUtil/EditorUtil.Draw/EditorUtil.Draw.SourceFileTree.md)
- [EditorUtil.Luban.ExportHelper.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ExportHelper.md)
- [EditorUtil.Luban.DataTypeNameHelper.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.DataTypeNameHelper.md)
