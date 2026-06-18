/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Pipify.Cli.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify CLI 入口（Jenkins / CI batchmode 调用）
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Pipify
        {
            /// <summary>
            /// 命令行入口（Jenkins / CI）。
            /// 用法：unity -batchmode -executeMethod NovaFramework.Editor.EditorUtil+Pipify+Cli.Run
            ///       -batchName "xxx" [-params '{"step.字段":"值"}']
            /// </summary>
            public static class Cli
            {
                /// <summary>
                /// CLI 主入口。读取 -batchName / -params 命令行参数，定位 PipifySettingsSO，执行目标 Batch。
                /// 成功退出码 0，失败退出码 1。
                /// </summary>
                public static void Run()
                {
                    try
                    {
                        string batchName = ReadArg("-batchName");
                        if (string.IsNullOrEmpty(batchName))
                        {
                            throw new InvalidOperationException(string.Format("{0}[CLI] 缺少 -batchName 参数", c_LogPrefix));
                        }

                        string paramsJson = ReadArg("-params");
                        IReadOnlyDictionary<string, string> overrides = ParseOverrides(paramsJson);

                        PipifySettingsSO so = FindSettings();
                        if (so == null)
                        {
                            throw new InvalidOperationException(string.Format("{0}[CLI] 工程中未找到 PipifySettingsSO", c_LogPrefix));
                        }

                        Batch batch = so.Batches.Find(b => b.Name == batchName);
                        if (batch == null)
                        {
                            throw new InvalidOperationException(string.Format("{0}[CLI] 未找到 Batch：{1}", c_LogPrefix, batchName));
                        }

                        RunBatchForCliAsync(batch, overrides).GetAwaiter().GetResult();
                        EditorApplication.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(LogTag.Editor, "{0}[CLI] 执行失败：{1}", c_LogPrefix, ex);
                        EditorApplication.Exit(1);
                    }
                }

                /// <summary>
                /// 从 Environment.GetCommandLineArgs 读取命名参数。命中 name 则返回下一项为值。
                /// </summary>
                /// <param name="name">参数名（含前导 `-`）。</param>
                /// <returns>参数值；未命中返回 null。</returns>
                private static string ReadArg(string name)
                {
                    string[] args = System.Environment.GetCommandLineArgs();
                    for (int i = 0; i < args.Length - 1; i++)
                    {
                        if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
                    }
                    return null;
                }

                /// <summary>
                /// 查找工程中的 PipifySettingsSO。存在多份时取首个并 Warning。
                /// </summary>
                /// <returns>找到的 SO；未找到返回 null。</returns>
                private static PipifySettingsSO FindSettings()
                {
                    string[] guids = AssetDatabase.FindAssets("t:" + nameof(PipifySettingsSO));
                    if (guids == null || guids.Length == 0) return null;
                    if (guids.Length > 1)
                    {
                        Log.Warning(LogTag.Editor, "{0}[CLI] 工程中存在 {1} 份 PipifySettingsSO，取首个。", c_LogPrefix, guids.Length);
                    }
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    return AssetDatabase.LoadAssetAtPath<PipifySettingsSO>(path);
                }

                /// <summary>
                /// 解析 -params JSON 为覆盖字典；空字符串返回 null。
                /// </summary>
                /// <param name="json">键值对 JSON 字符串。</param>
                /// <returns>覆盖字典或 null。</returns>
                private static IReadOnlyDictionary<string, string> ParseOverrides(string json)
                {
                    if (string.IsNullOrEmpty(json)) return null;
                    return Util.Json.Deserialize<Dictionary<string, string>>(json);
                }
            }
        }
    }
}
