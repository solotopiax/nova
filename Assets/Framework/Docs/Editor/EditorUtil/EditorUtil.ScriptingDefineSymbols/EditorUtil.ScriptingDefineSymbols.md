# EditorUtil.ScriptingDefineSymbols

## §1 文件头

```csharp
public static partial class EditorUtil { public static class ScriptingDefineSymbols }
namespace NovaFramework.Editor
// Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.ScriptingDefineSymbols/EditorUtil.ScriptingDefineSymbols.cs
```

脚本宏定义（Scripting Define Symbols）增删查工具，支持对固定目标列表批量操作，使用 `NamedBuildTarget` API。目标列表见下表，不代表覆盖 Unity 的全部目标。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `EditorUtil.ScriptingDefineSymbols.cs` | `EditorUtil.ScriptingDefineSymbols` | 唯一文件 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── ScriptingDefineSymbols (public static class)
```

---

## §4 关键字段表

| 字段 | 类型 | 说明 |
|------|------|------|
| `s_BuildTargets` | `private static readonly NamedBuildTarget[]` | 批量操作的平台列表：Standalone / iOS / Android / WindowsStoreApps / WebGL |

---

## §5 完整公开 API

```csharp
// 单平台操作
public static bool     HasScriptingDefineSymbol(NamedBuildTarget buildTarget, string symbol)
public static void     AddScriptingDefineSymbol(NamedBuildTarget buildTarget, string symbol)
public static void     RemoveScriptingDefineSymbol(NamedBuildTarget buildTarget, string symbol)
public static string[] GetScriptingDefineSymbols(NamedBuildTarget buildTarget)
public static void     SetScriptingDefineSymbols(NamedBuildTarget buildTarget, string[] symbols)
public static void     AddOrRemoveScriptingDefineSymbols(NamedBuildTarget buildTarget, string[] addSymbols, string[] removeSymbols)

// 所有平台批量操作（遍历 s_BuildTargets）
public static void AddScriptingDefineSymbol(string symbol)
public static void RemoveScriptingDefineSymbol(string symbol)
public static void AddOrRemoveScriptingDefineSymbols(string[] addSymbols, string[] removeSymbols)
```

---

## §11 使用示例

```csharp
// 为所有平台添加宏
EditorUtil.ScriptingDefineSymbols.AddScriptingDefineSymbol("MY_CUSTOM_DEFINE");

// 为所有平台移除宏
EditorUtil.ScriptingDefineSymbols.RemoveScriptingDefineSymbol("MY_CUSTOM_DEFINE");

// 仅检查当前 Standalone 平台
bool has = EditorUtil.ScriptingDefineSymbols.HasScriptingDefineSymbol(
    NamedBuildTarget.Standalone, "MY_DEFINE");

// 批量添加+移除（每个目标组单独写入，适合宏组切换）
EditorUtil.ScriptingDefineSymbols.AddOrRemoveScriptingDefineSymbols(
    new[] { "NEW_FEATURE" },
    new[] { "OLD_FEATURE" }
);
```

---

## 项目组受控编辑

`nova.project.settings.patch-build-defines` 复用 `SetScriptingDefineSymbols(NamedBuildTarget, string[])`，设置当前 Active BuildTarget 对应目标的完整最终宏集合。请求为 `namedBuildTarget` 与 `symbols` 数组；目标精确名称只允许 `Standalone`、`iOS`、`Android`、`WindowsStoreApps`、`WebGL`。`symbols: []` 明确清空，省略数组、null、空元素、重复、分号或非法标识均拒绝。不会改写其他目标，也不会自动切平台；Unity 实际序列化键由 `NamedBuildTarget.TargetName` 取得（iOS 为 `iPhone`，WindowsStoreApps 为 `Windows Store Apps`）。

Plan 只读展示全部原宏、最终集合及新增/移除，冻结真实 PlayerSettings、目标文件 SHA、其他第一层 Unity ProjectSettings 文件和精确平台。Project Settings 窗口、PlayerSettings Inspector、任何未保存窗口或 PlayerSettings 脏态均阻断，避免覆盖编辑草稿。Execute 提交前再次复核，单次调用 public Set；此 API 本身可能立即持久化并触发编译，不把后续保存当作第一次写入。只保存该原生 PlayerSettings 对象，核对最终集合、其他平台宏及全部其他字段不变。写入后的失败为 partial，不重放；本次编译与脚本重载后只读 Verify 核对收据及保存文件。原集合已满足时不写入、不申请编译，可按静态和产物验收。

`PlayerSettings` 是原生项目设置，Action 不以 `AssetDatabase.SaveAssetIfDirty` 作为其文件已落盘的证据。它用公有 `Object.Instantiate` 创建唯一 PlayerSettings 的非持久副本，完整快照相等后用公有 `UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget` 保存副本；随后独立加载正式文件核完整状态，在 finally 销毁副本与加载对象。文件 SHA 必须真实改变，完整预期快照与其他 ProjectSettings 均核对通过后才清除提交时的脏标志。Verify 不保存或清脏，允许重载重新标脏但必须完整快照及文件精确匹配。不得全局 SaveAssets 或用 JSON 覆写原生 PlayerSettings。Unity 宏 Set 还会同步活动 Build Profile 的覆盖设置，因此 Action 在 `BuildProfile.GetActiveBuildProfile()` 非 null 时阻断，不切 Profile，也不扩大写入范围。

这里只维护项目显式脚本宏，不代替 asmdef `versionDefines/defineConstraints`、包依赖或 SDK/Kit 接入。移除宏会改变编译分支；不能为了消除编译报错盲目清空。长期 [SettingsPatchBuildDefinesActionTests](../../../../../Tests/Editor/AgentActions/SettingsPatchBuildDefinesActionTests.cs) 覆盖严格请求、空集合、真实目标键、范围外字段保护、顺序与未知序列化结构；测试不清空生产宏。真实写入与恢复由已授权项目验收单独执行。

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md)
- [EnableLogsMenuItems.md](../../Menus/EnableLogsMenuItems.md)

## 原始工具的集合语义

单目标 API 使用传入的 NamedBuildTarget，不自动选择当前目标。Get 按分号拆分并去空项，不 Trim 或去重；Has 和增删按非空字符串精确匹配。单项 Add 仅在不存在时追加，单项 Remove 删除全部精确重复项。批量 AddOrRemove 先加后删，重叠项以删除为准，但每个 remove 元素只移除一次，旧重复项可能留下；它最终仍 Set 并记录日志。

Set 直接连接传入数组覆盖目标的完整宏集合，不负责标识合法性、保留旧项、去重或授权。无目标参数的重载依次处理 Standalone、iOS、Android、WindowsStoreApps、WebGL，部分目标写入后失败不回滚；写入可触发编译，返回不代表编译完成。下面的项目组 Action 另有严格输入和保存范围门禁，不能反推原始工具也有这些保护。
