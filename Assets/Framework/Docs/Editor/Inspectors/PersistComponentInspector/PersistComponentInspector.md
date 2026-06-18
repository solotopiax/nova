# PersistComponentInspector

**类签名**：`[CustomEditor(typeof(PersistComponent))] internal sealed partial class PersistComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.PersistComponent`

Persist 组件的 Inspector 面板，分两区绘制：上方为三个后端管理器（PlayerPrefs / FileFragment / SQLite）的实现类选择器，下方为各后端 AES 加密开关及 SQLite Cipher 密码配置。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `PersistComponentInspector.cs` | `sealed partial PersistComponentInspector` | 主体：`OnEnable` 绑定 7 个序列化属性及 3 个类型名列表，`OnInspectorGUI` 调度绘制入口 |
| `PersistComponentInspector.Visitors.cs` | `partial PersistComponentInspector` | 字段：全部 `SerializedProperty` 引用与 `List<string>` 类型名列表 |
| `PersistComponentInspector.Methods.cs` | `partial PersistComponentInspector` | 私有方法：`DrawConfigs`（总入口）、`DrawManagerSection`（管理器区）、`DrawEncryptSection`（加密配置区） |
| `PersistComponentInspector.PlayerPrefs.cs` | `partial PersistComponentInspector` | PlayerPrefs 后端特定绘制与交互逻辑 |
| `PersistComponentInspector.FileFragment.cs` | `partial PersistComponentInspector` | FileFragment 后端特定绘制与交互逻辑（含 `EditorUtil.FileWatcher` 目录监控） |
| `PersistComponentInspector.SQLite.cs` | `partial PersistComponentInspector` | SQLite 后端特定绘制与交互逻辑（含 `EditorUtil.FileSystem.DeletePath` 清理导出路径） |

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
| `m_CurFileFragmentManagerTypeName` | `SerializedProperty` | 绑定 `m_CurFileFragmentManagerTypeName` |
| `m_FileFragmentManagerTypeNames` | `List<string>` | `IFileFragmentManager` 全部实现类名列表 |
| `m_UseAESForFileFragment` | `SerializedProperty` | 绑定 `m_UseAESForFileFragment` |
| `m_CurSQLiteManagerTypeName` | `SerializedProperty` | 绑定 `m_CurSQLiteManagerTypeName` |
| `m_SQLiteManagerTypeNames` | `List<string>` | `ISQLiteManager` 全部实现类名列表 |
| `m_UseAESForSQLite` | `SerializedProperty` | 绑定 `m_UseAESForSQLite` |
| `m_SQLiteCipherPassword` | `SerializedProperty` | 绑定 `m_SQLiteCipherPassword` |

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
PlayerPrefs 启用 AES 加密   Toggle
FileFragment 启用 AES 加密  Toggle
SQLite 启用 AES 加密        Toggle
SQLite Cipher 密码          TextField
HelpBox(Info)              AES 条目级加密说明 + Cipher 数据库级加密说明
─────────────────────────────────────────────────────────
```

---

## § 13 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [PersistComponent.md](../../../Runtime/Modules/Persist/PersistComponent.md)
- [EditorUtil.FileWatcher.md](../../EditorUtil/EditorUtil.FileWatcher/EditorUtil.FileWatcher.md)
- [EditorUtil.FileSystem.md](../../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.md)
