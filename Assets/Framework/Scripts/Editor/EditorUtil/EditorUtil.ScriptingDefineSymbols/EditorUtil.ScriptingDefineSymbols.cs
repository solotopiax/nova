/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ScriptingDefineSymbols.cs
 * author:    taoye
 * created:   2026/4/1
 * descrip:   脚本宏定义增删查工具（跨平台批量操作）
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 脚本宏定义增删查工具，支持对所有目标平台批量操作。
        /// </summary>
        public static class ScriptingDefineSymbols
        {
            /// <summary>
            /// 需要同步操作的目标平台列表。
            /// </summary>
            private static readonly NamedBuildTarget[] s_BuildTargets = new NamedBuildTarget[]
            {
                NamedBuildTarget.Standalone,
                NamedBuildTarget.iOS,
                NamedBuildTarget.Android,
                NamedBuildTarget.WindowsStoreApps,
                NamedBuildTarget.WebGL
            };

            /// <summary>
            /// 检查指定平台是否存在指定的脚本宏定义。
            /// </summary>
            /// <param name="buildTarget">要检查的目标平台。</param>
            /// <param name="symbol">要检查的宏定义字符串。</param>
            /// <returns>指定平台是否存在该宏定义。</returns>
            public static bool HasScriptingDefineSymbol(NamedBuildTarget buildTarget, string symbol)
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    return false;
                }

                string[] symbols = GetScriptingDefineSymbols(buildTarget);
                foreach (string s in symbols)
                {
                    if (s == symbol)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// 为指定平台添加脚本宏定义（已存在则跳过）。
            /// </summary>
            /// <param name="buildTarget">要操作的目标平台。</param>
            /// <param name="symbol">要添加的宏定义字符串。</param>
            public static void AddScriptingDefineSymbol(NamedBuildTarget buildTarget, string symbol)
            {
                if (string.IsNullOrEmpty(symbol) || HasScriptingDefineSymbol(buildTarget, symbol))
                {
                    return;
                }

                List<string> symbols = new List<string>(GetScriptingDefineSymbols(buildTarget)) { symbol };
                SetScriptingDefineSymbols(buildTarget, symbols.ToArray());
            }

            /// <summary>
            /// 为指定平台移除脚本宏定义（不存在则跳过）。
            /// </summary>
            /// <param name="buildTarget">要操作的目标平台。</param>
            /// <param name="symbol">要移除的宏定义字符串。</param>
            public static void RemoveScriptingDefineSymbol(NamedBuildTarget buildTarget, string symbol)
            {
                if (string.IsNullOrEmpty(symbol) || !HasScriptingDefineSymbol(buildTarget, symbol))
                {
                    return;
                }

                List<string> symbols = new List<string>(GetScriptingDefineSymbols(buildTarget));
                symbols.RemoveAll(s => s == symbol);
                SetScriptingDefineSymbols(buildTarget, symbols.ToArray());
            }

            /// <summary>
            /// 为所有目标平台添加脚本宏定义。
            /// </summary>
            /// <param name="symbol">要添加的宏定义字符串。</param>
            public static void AddScriptingDefineSymbol(string symbol)
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    return;
                }

                foreach (NamedBuildTarget buildTarget in s_BuildTargets)
                {
                    AddScriptingDefineSymbol(buildTarget, symbol);
                }
            }

            /// <summary>
            /// 为所有目标平台移除脚本宏定义。
            /// </summary>
            /// <param name="symbol">要移除的宏定义字符串。</param>
            public static void RemoveScriptingDefineSymbol(string symbol)
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    return;
                }

                foreach (NamedBuildTarget buildTarget in s_BuildTargets)
                {
                    RemoveScriptingDefineSymbol(buildTarget, symbol);
                }
            }

            /// <summary>
            /// 为所有目标平台批量添加与移除脚本宏定义。
            /// </summary>
            /// <param name="addSymbols">要添加的宏定义数组。</param>
            /// <param name="removeSymbols">要移除的宏定义数组。</param>
            public static void AddOrRemoveScriptingDefineSymbols(string[] addSymbols, string[] removeSymbols)
            {
                foreach (NamedBuildTarget buildTarget in s_BuildTargets)
                {
                    AddOrRemoveScriptingDefineSymbols(buildTarget, addSymbols, removeSymbols);
                }
            }

            /// <summary>
            /// 为指定平台批量添加与移除脚本宏定义。
            /// </summary>
            /// <param name="buildTarget">要操作的目标平台。</param>
            /// <param name="addSymbols">要添加的宏定义数组。</param>
            /// <param name="removeSymbols">要移除的宏定义数组。</param>
            public static void AddOrRemoveScriptingDefineSymbols(NamedBuildTarget buildTarget, string[] addSymbols, string[] removeSymbols)
            {
                List<string> symbols = new List<string>(GetScriptingDefineSymbols(buildTarget));

                if (addSymbols != null && addSymbols.Length > 0)
                {
                    foreach (string s in addSymbols)
                    {
                        if (!string.IsNullOrEmpty(s) && !symbols.Contains(s))
                        {
                            symbols.Add(s);
                        }
                    }
                }

                if (removeSymbols != null && removeSymbols.Length > 0)
                {
                    foreach (string s in removeSymbols)
                    {
                        if (!string.IsNullOrEmpty(s))
                        {
                            symbols.Remove(s);
                        }
                    }
                }

                Log.Debug(LogTag.Editor, "EditorUtil.ScriptingDefineSymbols buildTarget={0} symbols={1}", buildTarget, string.Join(",", symbols));
                SetScriptingDefineSymbols(buildTarget, symbols.ToArray());
            }

            /// <summary>
            /// 获取指定平台的当前脚本宏定义数组。
            /// </summary>
            /// <param name="buildTarget">要查询的目标平台。</param>
            /// <returns>当前所有宏定义字符串数组。</returns>
            public static string[] GetScriptingDefineSymbols(NamedBuildTarget buildTarget)
            {
                return PlayerSettings.GetScriptingDefineSymbols(buildTarget).Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);
            }

            /// <summary>
            /// 设置指定平台的脚本宏定义（覆盖式）。
            /// </summary>
            /// <param name="buildTarget">要操作的目标平台。</param>
            /// <param name="symbols">要设置的宏定义字符串数组。</param>
            public static void SetScriptingDefineSymbols(NamedBuildTarget buildTarget, string[] symbols)
            {
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.Join(";", symbols));
            }
        }
    }
}
