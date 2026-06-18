# EditorUtil.ScriptingDefineSymbols

## §1 文件头

```csharp
public static partial class EditorUtil { public static class ScriptingDefineSymbols }
namespace NovaFramework.Editor
// Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.ScriptingDefineSymbols/EditorUtil.ScriptingDefineSymbols.cs
```

脚本宏定义（Scripting Define Symbols）增删查工具，支持对所有目标平台批量操作。使用 Unity 现代 `NamedBuildTarget` API（2021.2+）。

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

// 批量添加+移除（原子操作，适合宏组切换）
EditorUtil.ScriptingDefineSymbols.AddOrRemoveScriptingDefineSymbols(
    new[] { "NEW_FEATURE" },
    new[] { "OLD_FEATURE" }
);
```

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md)
- [EnableLogsMenuItems.md](../../Menus/EnableLogsMenuItems.md)
