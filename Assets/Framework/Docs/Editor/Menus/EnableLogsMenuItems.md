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
| `c_Priority*` | `private const int` | `10000–10006` |
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

这些菜单沿无目标参数的宏工具处理 `Standalone / iOS / Android / WindowsStoreApps / WebGL` 五个目标，不只修改当前活动平台。`EnableAllLogs` 是先清日志宏再加总开关 `ENABLE_LOG`，不是把全部日志宏都加上；非日志宏保留。

`SetAboveLogSymbol` 的空值或未知宏完全不修改。`SetSpecifyLogSymbols` 的 null、空数组或全未知项也不修改，不能用空数组代表关闭；首次合法项才触发清理，之后逐项添加。先清后加是多次 PlayerSettings 写入，可能触发编译/重载，中途异常没有跨目标事务或回滚。

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
