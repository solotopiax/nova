# PersistComponentInspector

**类签名**：`[CustomEditor(typeof(PersistComponent))] internal sealed partial class PersistComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.PersistComponent`

Persist 组件的 Inspector 面板，分两区绘制：上方为三种存储实现（PlayerPrefs / FileFragment / SQLite）的管理器实现类选择器，下方为各存储实现的 AES 加密开关及 SQLite Cipher 密码配置。

Editor 模式下，Inspector 通过 `EditorUtil.Config.WorkspaceActive.Get()` 定位当前 ConfigMaster，并按其 `CurrentPlatform / CurrentChannel / CurrentDevelopMode` 合法坐标读取 `PrivacyConfigs`，再将 Key/IV 显式传给 AES 接口。其中 `CurrentPlatform` 实时映射 Unity `activeBuildTarget`，因此 Inspector 不提供与当前 Unity 构建目标不同的平台坐标。Play 模式下改读该 Master 的 `ExportTarget`（ConfigRuntimeSO）隐私快照。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `PersistComponentInspector.cs` | `sealed partial PersistComponentInspector` | 主体：`OnEnable` 绑定 7 个序列化属性及 3 个类型名列表，`OnInspectorGUI` 调度绘制入口 |
| `PersistComponentInspector.Visitors.cs` | `partial PersistComponentInspector` | 字段：全部 `SerializedProperty` 引用与 `List<string>` 类型名列表 |
| `PersistComponentInspector.Methods.cs` | `partial PersistComponentInspector` | 私有方法：`DrawConfigs`（配置区总入口）、`DrawManagerSection`（管理器区）、`DrawEncryptSection`（加密区）、`DrawAutoSaveSection`（自动保存间隔区）、`DrawDataSection` / `DrawGlobalSearchBar` / `DrawBackendSearchBar` / `DrawRuntimeData` / `DrawEditorData`（数据区）、`DrawItemRow`（条目行共用绘制）、`MatchSearch` / `ContainsIgnoreCase`（全局与各存储实现搜索匹配）、`TryReadFragmentFile` / `WriteFragmentFile` / `DecodeForDisplay` / `EncodeForStorage`（FF 文件与 AES 双向处理）、`DrawClassifyFoldout` |
| `PersistComponentInspector.PlayerPrefs.cs` | `partial PersistComponentInspector` | PlayerPrefs 存储实现特定绘制与交互逻辑 |
| `PersistComponentInspector.FileFragment.cs` | `partial PersistComponentInspector` | FileFragment 存储实现特定绘制与交互逻辑（含 `EditorUtil.FileWatcher` 目录监控） |
| `PersistComponentInspector.SQLite.cs` | `partial PersistComponentInspector` | SQLite 存储实现特定绘制与交互逻辑（含 `EditorUtil.FileSystem.DeletePath` 清理导出路径） |

---

## § 3 继承关系

```
UnityEditor.Editor
  └── BaseComponentInspector (abstract, NovaFramework.Editor)
       └── PersistComponentInspector (sealed partial)
```

---

## § 4 关键字段表

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_CurPlayerPrefsManagerTypeName` | `SerializedProperty` | 绑定 `m_CurPlayerPrefsManagerTypeName` |
| `m_PlayerPrefsManagerTypeNames` | `List<string>` | `IPlayerPrefsManager` 全部实现类名列表 |
| `m_UseAESForPlayerPrefs` | `SerializedProperty` | 绑定 `m_UseAESForPlayerPrefs` |
| `m_AutoSaveIntervalPlayerPrefs` | `SerializedProperty` | PlayerPrefs 自动保存间隔（秒，0/负数禁用） |
| `m_CurFileFragmentManagerTypeName` | `SerializedProperty` | 绑定 `m_CurFileFragmentManagerTypeName` |
| `m_FileFragmentManagerTypeNames` | `List<string>` | `IFileFragmentManager` 全部实现类名列表 |
| `m_UseAESForFileFragment` | `SerializedProperty` | 绑定 `m_UseAESForFileFragment` |
| `m_AutoSaveIntervalFileFragment` | `SerializedProperty` | FileFragment 自动保存间隔（秒，0/负数禁用） |
| `m_CurSQLiteManagerTypeName` | `SerializedProperty` | 绑定 `m_CurSQLiteManagerTypeName` |
| `m_SQLiteManagerTypeNames` | `List<string>` | `ISQLiteManager` 全部实现类名列表 |
| `m_UseAESForSQLite` | `SerializedProperty` | 绑定 `m_UseAESForSQLite` |
| `m_AutoSaveIntervalSQLite` | `SerializedProperty` | SQLite 自动保存间隔（秒，0/负数禁用） |
| `m_SQLiteCipherPassword` | `SerializedProperty` | 绑定 `m_SQLiteCipherPassword` |
| `m_TmpSQLiteCipherPassword` | `string` | Cipher 密码临时输入缓冲；与 `m_SQLiteCipherPassword` 不同步时 "存档转换" 按钮可点 |
| `m_GlobalSearchText` / `m_PPSearchText` / `m_FFSearchText` / `m_SQLSearchText` | `string` | 全局搜索关键词 + 各存储实现独立搜索关键词（不区分大小写，作用于 classify/item/value） |
| `m_PP_Values` / `m_PP_EditBuffers` / `m_PP_EditStates` | `SortedDictionary<...>` | Editor 模式 PlayerPrefs 分类→条目 的值/编辑缓冲/编辑态 |
| `m_FF_Values` / `m_FF_EditBuffers` / `m_FF_EditStates` | `Dictionary<...>` | Editor 模式 FileFragment 分类→条目 的值/编辑缓冲/编辑态 |
| `m_SQL_Values` / `m_SQL_EditBuffers` / `m_SQL_EditStates` | `SortedDictionary<...>` | Editor 模式 SQLite 表→条目 的值/编辑缓冲/编辑态（非 WebGL） |
| `m_SQLiteConnection` | `SqlCipher4Unity3D.SQLiteConnection` | Editor 模式 SQLite 连接；Play 进入前关闭、退出后重建（非 WebGL） |

---

## § 5 完整公开 API

```csharp
// --- Unity Inspector 生命周期 ---
protected override void OnEnable()
public override void OnInspectorGUI()
```

---

## § 8 初始化时序

```
OnEnable()
  │
  ├─ base.OnEnable()
  │
  ├─ FindProperty("m_CurPlayerPrefsManagerTypeName")  → m_CurPlayerPrefsManagerTypeName
  ├─ FindProperty("m_UseAESForPlayerPrefs")            → m_UseAESForPlayerPrefs
  ├─ FindProperty("m_CurFileFragmentManagerTypeName") → m_CurFileFragmentManagerTypeName
  ├─ FindProperty("m_UseAESForFileFragment")           → m_UseAESForFileFragment
  ├─ FindProperty("m_CurSQLiteManagerTypeName")        → m_CurSQLiteManagerTypeName
  ├─ FindProperty("m_UseAESForSQLite")                 → m_UseAESForSQLite
  ├─ FindProperty("m_SQLiteCipherPassword")            → m_SQLiteCipherPassword
  │
  ├─ Util.Assembly.GetTypeNames(IPlayerPrefsManager)  → m_PlayerPrefsManagerTypeNames
  ├─ Util.Assembly.GetTypeNames(IFileFragmentManager) → m_FileFragmentManagerTypeNames
  └─ Util.Assembly.GetTypeNames(ISQLiteManager)       → m_SQLiteManagerTypeNames
```

---

## § 11 使用示例

`PersistComponentInspector` 由 Unity 通过 `[CustomEditor(typeof(PersistComponent))]` 自动绑定，无需手动调用。

**Inspector 布局：**

```
[管理器区]
PlayerPrefs 管理器   TypesSelector → IPlayerPrefsManager 实现类
FileFragment 管理器  TypesSelector → IFileFragmentManager 实现类
SQLite 管理器        TypesSelector → ISQLiteManager 实现类
HelpBox(Info)       自定义扩展说明 + WebGL 平台限制说明
─────────────────────────────────────────────────────────
[加密配置区]
PlayerPrefs 启用 AES 加密   Toggle（切换时立即批量迁移所有已有条目）
FileFragment 启用 AES 加密  Toggle（切换时以新加密状态重写所有 .dat 文件）
SQLite 启用 AES 加密        Toggle（切换时立即批量迁移所有已有条目）
SQLite Cipher 密码          TextField（临时缓冲，与序列化值不同步时激活"存档转换"）
存档转换                    Button（用新密码删旧库、按原数据建新库，AES 条目状态保持不变）
HelpBox(Info)              AES 条目级加密说明 + Cipher 数据库级加密说明
─────────────────────────────────────────────────────────
[自动保存配置区]
PlayerPrefs 自动保存间隔(秒)   Property
FileFragment 自动保存间隔(秒)  Property
SQLite 自动保存间隔(秒)        Property
HelpBox(Info)                 0 或负数禁用自动保存，仅 Shutdown / 手动 Save 落盘
─────────────────────────────────────────────────────────
[数据区]
全局搜索                    TextField + ×（作用于 classify / item / value）
[清除全部持久化数据]        DangerButton（Play 模式下隐藏；同时清除三种存储实现数据，二次确认）

Editor 模式（非 Play）：
  [PlayerPrefs（Editor）(n) ▼]   分类折叠 + 存储项搜索 + 编辑/保存/清除 + 清除全部
  [FileFragment（Editor）(n) ▼]  分类折叠 + 存储项搜索 + 打开文件夹 + 编辑/保存/清除 + 清除全部
  [SQLite（Editor）(n) ▼]        分类折叠 + 存储项搜索 + 打开文件夹 + 编辑/保存/清除 + 清除全部
                                 ├─ Windows：可视化工具 / 应用预览
                                 └─ 连接失败时提供"删除数据库"DangerButton

Runtime 模式（Play 中）：
  [PlayerPrefs（Runtime）(n) ▼]  只读列表（IPlayerPrefsManager 读取）
  [FileFragment（Runtime）(n) ▼] 只读列表（IFileFragmentManager 读取）
  [SQLite（Runtime）(n) ▼]       只读列表（ISQLiteManager 读取）
```

---

## § 12 注意事项

**SQLite 密码缓冲与存档转换**：`m_TmpSQLiteCipherPassword` 是临时文本框，不会立即写回序列化字段。当它 ≠ `m_SQLiteCipherPassword.stringValue` 时，"存档转换" 按钮解除灰显；点击后 Inspector 删除旧库（含 `-shm` / `-wal` / `-journal` 附属文件）、按当前内存数据原样重建新库并应用新 Cipher 密码，AES 条目状态保持不变。

**Play 模式 SQLite 连接切换**：进入 Play 前通过 `EditorApplication.playModeStateChanged` 关闭 Editor 侧 SQLite 连接，退出 Play 后经 `delayCall` 在 Inspector 仍有效时重建连接；避免 Play 期间 Editor 侧与 Runtime 侧同时持有同一数据库句柄。

**AES 迁移立即执行**：切换任一存储实现的 AES Toggle 时，立即对该存储实现已存数据做明文 ↔ 密文批量迁移，保证开关状态与实际存储格式始终一致；Inspector 始终以明文展示。

**WebGL 平台**：SQLite 存储实现在 WebGL 上以静默空操作运行，Initialize 输出警告，Get 返回默认值，Set 被忽略；Editor 面板中 SQLite 相关区块整体被 `#if !UNITY_WEBGL` 裁剪。

**Editor 模式删除数据库**：当 SQLite 文件存在但连接建立失败（如密码错误）时，Editor 面板提供"删除数据库"DangerButton，便于清理后重新开始。

**Windows 预览工作流**：`#if UNITY_EDITOR_WIN` 下提供 "可视化工具" 与 "应用预览" 按钮。无 Cipher 密码时直接打开原 DB；有 Cipher 密码时先在同目录导出 `game_preview.db` 明文副本（条目值按当前 AES 状态原样保留），用 SQLiteStudio 打开副本编辑，再点 "应用预览" 将修改回写原加密库并删除副本。

---

## § 13 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [PersistComponent.md](../../../Runtime/Modules/Persist/PersistComponent.md)
- [EditorUtil.FileWatcher.md](../../EditorUtil/EditorUtil.FileWatcher/EditorUtil.FileWatcher.md)
- [EditorUtil.FileSystem.md](../../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.md)
