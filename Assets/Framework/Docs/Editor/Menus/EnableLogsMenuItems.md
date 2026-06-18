# EnableLogsMenuItems

## §1 文件头

```csharp
public static class EnableLogsMenuItems
namespace NovaFramework.Editor
// Assets/Framework/Scripts/Editor/Menus/EnableLogsMenuItems.cs
```

日志脚本宏定义（Scripting Define Symbols）的管理菜单项集合，支持一键切换日志等级。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `EnableLogsMenuItems.cs` | `EnableLogsMenuItems` | 唯一文件 |

---

## §3 继承关系

```
(无继承，public static class)
```

---

## §4 关键字段表

| 常量/字段 | 类型 | 值 / 说明 |
|-----------|------|----------|
| `c_MenuDisableAllLogs` | `private const string` | `"Nova/Enable Logs/Disable All Logs"` |
| `c_MenuEnable*` | `private const string` | 各级别菜单路径（见代码） |
| `c_Priority*` | `private const int` | `1040–1046` |
| `c_EnableLog` | `private const string` | `"ENABLE_LOG"` — 总开关 |
| `c_EnableDebugAndAboveLog` | `private const string` | `"ENABLE_DEBUG_AND_ABOVE_LOG"` |
| `c_EnableInfoAndAboveLog` | `private const string` | `"ENABLE_INFO_AND_ABOVE_LOG"` |
| `c_EnableWarningAndAboveLog` | `private const string` | `"ENABLE_WARNING_AND_ABOVE_LOG"` |
| `c_EnableErrorAndAboveLog` | `private const string` | `"ENABLE_ERROR_AND_ABOVE_LOG"` |
| `c_EnableFatalAndAboveLog` | `private const string` | `"ENABLE_FATAL_AND_ABOVE_LOG"` |
| `c_Enable*Log` | `private const string` | 5 个精确级别宏 |
| `s_AboveLogSymbols` | `private static readonly string[]` | 5 个 AboveLog 宏的互斥集合 |
| `s_SpecifyLogSymbols` | `private static readonly string[]` | 5 个精确级别宏的集合 |

---

## §5 完整公开 API

```csharp
[MenuItem("Nova/Enable Logs/Disable All Logs")]              public static void DisableAllLogs()
[MenuItem("Nova/Enable Logs/Enable All Logs")]               public static void EnableAllLogs()
[MenuItem("Nova/Enable Logs/Enable Debug And Above Logs")]   public static void EnableDebugAndAboveLogs()
[MenuItem("Nova/Enable Logs/Enable Info And Above Logs")]    public static void EnableInfoAndAboveLogs()
[MenuItem("Nova/Enable Logs/Enable Warning And Above Logs")] public static void EnableWarningAndAboveLogs()
[MenuItem("Nova/Enable Logs/Enable Error And Above Logs")]   public static void EnableErrorAndAboveLogs()
[MenuItem("Nova/Enable Logs/Enable Fatal And Above Logs")]   public static void EnableFatalAndAboveLogs()

public static void SetAboveLogSymbol(string aboveLogSymbol)
public static void SetSpecifyLogSymbols(string[] specifyLogSymbols)
```

---

## §9 关键算法

**互斥切换逻辑：** `SetAboveLogSymbol` 先调用 `DisableAllLogs`（清除所有宏），再添加目标宏，确保同一时刻只有一个 AboveLog 级别宏生效。

`DisableAllLogs` 移除顺序：总开关 → SpecifyLog 宏 → AboveLog 宏。

---

## §11 使用示例

```csharp
// 在 CI 或自定义编辑器工具中切换日志等级
EnableLogsMenuItems.EnableWarningAndAboveLogs();

// 批量激活精确级别宏
EnableLogsMenuItems.SetSpecifyLogSymbols(new[] { "ENABLE_ERROR_LOG", "ENABLE_FATAL_LOG" });
```

---

## §13 关联文档

- [Menus.md](Menus.md)
- [EditorUtil.ScriptingDefineSymbols.md](../EditorUtil/EditorUtil.ScriptingDefineSymbols/EditorUtil.ScriptingDefineSymbols.md)
