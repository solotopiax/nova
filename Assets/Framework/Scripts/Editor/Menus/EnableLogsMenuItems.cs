/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EnableLogsMenuItems.cs
 * author:    taoye
 * created:   2026/4/1
 * descrip:   日志脚本宏定义相关菜单项集合
 ***************************************************************/

using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 日志脚本宏定义相关菜单项集合。
    /// </summary>
    public static class EnableLogsMenuItems
    {
        /// <summary>
        /// 禁用所有日志宏菜单路径。
        /// </summary>
        private const string c_MenuDisableAllLogs = "Nova/Enable Logs/Disable All Logs";

        /// <summary>
        /// 开启所有日志宏菜单路径。
        /// </summary>
        private const string c_MenuEnableAllLogs = "Nova/Enable Logs/Enable All Logs";

        /// <summary>
        /// 开启 Debug 及以上级别日志宏菜单路径。
        /// </summary>
        private const string c_MenuEnableDebugAndAboveLogs = "Nova/Enable Logs/Enable Debug And Above Logs";

        /// <summary>
        /// 开启 Info 及以上级别日志宏菜单路径。
        /// </summary>
        private const string c_MenuEnableInfoAndAboveLogs = "Nova/Enable Logs/Enable Info And Above Logs";

        /// <summary>
        /// 开启 Warning 及以上级别日志宏菜单路径。
        /// </summary>
        private const string c_MenuEnableWarningAndAboveLogs = "Nova/Enable Logs/Enable Warning And Above Logs";

        /// <summary>
        /// 开启 Error 及以上级别日志宏菜单路径。
        /// </summary>
        private const string c_MenuEnableErrorAndAboveLogs = "Nova/Enable Logs/Enable Error And Above Logs";

        /// <summary>
        /// 开启 Fatal 及以上级别日志宏菜单路径。
        /// </summary>
        private const string c_MenuEnableFatalAndAboveLogs = "Nova/Enable Logs/Enable Fatal And Above Logs";

        /// <summary>
        /// 禁用所有日志宏菜单排序优先级。
        /// </summary>
        private const int c_PriorityDisableAllLogs = 1040;

        /// <summary>
        /// 开启所有日志宏菜单排序优先级。
        /// </summary>
        private const int c_PriorityEnableAllLogs = 1041;

        /// <summary>
        /// 开启 Debug 及以上级别日志宏菜单排序优先级。
        /// </summary>
        private const int c_PriorityEnableDebugAndAboveLogs = 1042;

        /// <summary>
        /// 开启 Info 及以上级别日志宏菜单排序优先级。
        /// </summary>
        private const int c_PriorityEnableInfoAndAboveLogs = 1043;

        /// <summary>
        /// 开启 Warning 及以上级别日志宏菜单排序优先级。
        /// </summary>
        private const int c_PriorityEnableWarningAndAboveLogs = 1044;

        /// <summary>
        /// 开启 Error 及以上级别日志宏菜单排序优先级。
        /// </summary>
        private const int c_PriorityEnableErrorAndAboveLogs = 1045;

        /// <summary>
        /// 开启 Fatal 及以上级别日志宏菜单排序优先级。
        /// </summary>
        private const int c_PriorityEnableFatalAndAboveLogs = 1046;

        /// <summary>
        /// 全局日志总开关宏。
        /// </summary>
        private const string c_EnableLog = "ENABLE_LOG";

        /// <summary>
        /// Debug 及以上级别日志宏。
        /// </summary>
        private const string c_EnableDebugAndAboveLog = "ENABLE_DEBUG_AND_ABOVE_LOG";

        /// <summary>
        /// Info 及以上级别日志宏。
        /// </summary>
        private const string c_EnableInfoAndAboveLog = "ENABLE_INFO_AND_ABOVE_LOG";

        /// <summary>
        /// Warning 及以上级别日志宏。
        /// </summary>
        private const string c_EnableWarningAndAboveLog = "ENABLE_WARNING_AND_ABOVE_LOG";

        /// <summary>
        /// Error 及以上级别日志宏。
        /// </summary>
        private const string c_EnableErrorAndAboveLog = "ENABLE_ERROR_AND_ABOVE_LOG";

        /// <summary>
        /// Fatal 及以上级别日志宏。
        /// </summary>
        private const string c_EnableFatalAndAboveLog = "ENABLE_FATAL_AND_ABOVE_LOG";

        /// <summary>
        /// 仅 Debug 级别日志宏。
        /// </summary>
        private const string c_EnableDebugLog = "ENABLE_DEBUG_LOG";

        /// <summary>
        /// 仅 Info 级别日志宏。
        /// </summary>
        private const string c_EnableInfoLog = "ENABLE_INFO_LOG";

        /// <summary>
        /// 仅 Warning 级别日志宏。
        /// </summary>
        private const string c_EnableWarningLog = "ENABLE_WARNING_LOG";

        /// <summary>
        /// 仅 Error 级别日志宏。
        /// </summary>
        private const string c_EnableErrorLog = "ENABLE_ERROR_LOG";

        /// <summary>
        /// 仅 Fatal 级别日志宏。
        /// </summary>
        private const string c_EnableFatalLog = "ENABLE_FATAL_LOG";

        /// <summary>
        /// 所有 AboveLog 级别宏的集合，用于互斥切换。
        /// </summary>
        private static readonly string[] s_AboveLogSymbols = new string[]
        {
            c_EnableDebugAndAboveLog,
            c_EnableInfoAndAboveLog,
            c_EnableWarningAndAboveLog,
            c_EnableErrorAndAboveLog,
            c_EnableFatalAndAboveLog
        };

        /// <summary>
        /// 所有 SpecifyLog 精确级别宏的集合。
        /// </summary>
        private static readonly string[] s_SpecifyLogSymbols = new string[]
        {
            c_EnableDebugLog,
            c_EnableInfoLog,
            c_EnableWarningLog,
            c_EnableErrorLog,
            c_EnableFatalLog
        };

        /// <summary>
        /// 禁用所有日志脚本宏定义。
        /// </summary>
        [MenuItem(c_MenuDisableAllLogs, false, c_PriorityDisableAllLogs)]
        public static void DisableAllLogs()
        {
            EditorUtil.ScriptingDefineSymbols.RemoveScriptingDefineSymbol(c_EnableLog);
            foreach (string symbol in s_SpecifyLogSymbols)
            {
                EditorUtil.ScriptingDefineSymbols.RemoveScriptingDefineSymbol(symbol);
            }
            foreach (string symbol in s_AboveLogSymbols)
            {
                EditorUtil.ScriptingDefineSymbols.RemoveScriptingDefineSymbol(symbol);
            }
        }

        /// <summary>
        /// 开启所有日志脚本宏定义（仅保留总开关 ENABLE_LOG）。
        /// </summary>
        [MenuItem(c_MenuEnableAllLogs, false, c_PriorityEnableAllLogs)]
        public static void EnableAllLogs()
        {
            DisableAllLogs();
            EditorUtil.ScriptingDefineSymbols.AddScriptingDefineSymbol(c_EnableLog);
        }

        /// <summary>
        /// 开启 Debug 及以上级别的日志脚本宏定义。
        /// </summary>
        [MenuItem(c_MenuEnableDebugAndAboveLogs, false, c_PriorityEnableDebugAndAboveLogs)]
        public static void EnableDebugAndAboveLogs()
        {
            SetAboveLogSymbol(c_EnableDebugAndAboveLog);
        }

        /// <summary>
        /// 开启 Info 及以上级别的日志脚本宏定义。
        /// </summary>
        [MenuItem(c_MenuEnableInfoAndAboveLogs, false, c_PriorityEnableInfoAndAboveLogs)]
        public static void EnableInfoAndAboveLogs()
        {
            SetAboveLogSymbol(c_EnableInfoAndAboveLog);
        }

        /// <summary>
        /// 开启 Warning 及以上级别的日志脚本宏定义。
        /// </summary>
        [MenuItem(c_MenuEnableWarningAndAboveLogs, false, c_PriorityEnableWarningAndAboveLogs)]
        public static void EnableWarningAndAboveLogs()
        {
            SetAboveLogSymbol(c_EnableWarningAndAboveLog);
        }

        /// <summary>
        /// 开启 Error 及以上级别的日志脚本宏定义。
        /// </summary>
        [MenuItem(c_MenuEnableErrorAndAboveLogs, false, c_PriorityEnableErrorAndAboveLogs)]
        public static void EnableErrorAndAboveLogs()
        {
            SetAboveLogSymbol(c_EnableErrorAndAboveLog);
        }

        /// <summary>
        /// 开启 Fatal 及以上级别的日志脚本宏定义。
        /// </summary>
        [MenuItem(c_MenuEnableFatalAndAboveLogs, false, c_PriorityEnableFatalAndAboveLogs)]
        public static void EnableFatalAndAboveLogs()
        {
            SetAboveLogSymbol(c_EnableFatalAndAboveLog);
        }

        /// <summary>
        /// 激活指定的 AboveLog 级别宏（先清除所有宏，再添加目标宏）。
        /// </summary>
        /// <param name="aboveLogSymbol">要激活的 AboveLog 宏定义字符串。</param>
        public static void SetAboveLogSymbol(string aboveLogSymbol)
        {
            if (string.IsNullOrEmpty(aboveLogSymbol))
            {
                return;
            }

            foreach (string symbol in s_AboveLogSymbols)
            {
                if (symbol == aboveLogSymbol)
                {
                    DisableAllLogs();
                    EditorUtil.ScriptingDefineSymbols.AddScriptingDefineSymbol(aboveLogSymbol);
                    return;
                }
            }
        }

        /// <summary>
        /// 激活一组 SpecifyLog 精确级别宏（先清除所有宏，再逐一添加有效目标宏）。
        /// </summary>
        /// <param name="specifyLogSymbols">要激活的 SpecifyLog 宏定义数组。</param>
        public static void SetSpecifyLogSymbols(string[] specifyLogSymbols)
        {
            if (specifyLogSymbols == null || specifyLogSymbols.Length == 0)
            {
                return;
            }

            bool removed = false;
            foreach (string specifySymbol in specifyLogSymbols)
            {
                if (string.IsNullOrEmpty(specifySymbol))
                {
                    continue;
                }

                foreach (string symbol in s_SpecifyLogSymbols)
                {
                    if (symbol == specifySymbol)
                    {
                        if (!removed)
                        {
                            removed = true;
                            DisableAllLogs();
                        }
                        EditorUtil.ScriptingDefineSymbols.AddScriptingDefineSymbol(specifySymbol);
                        break;
                    }
                }
            }
        }
    }
}
