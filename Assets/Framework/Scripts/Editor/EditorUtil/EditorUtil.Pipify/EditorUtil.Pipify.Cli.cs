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
            ///       [-configMasterGuid "..."] [-pipifySettingsGuid "..."]
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

                        PipifySettingsSO so = ResolveWorkspaceFromArgs();

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
                /// 解析 CLI 工作区。
                /// 两个 GUID 参数必须成对出现；显式传入时先持久化该工作区，未传时只使用 Globals 当前绑定，
                /// 不再扫描并选择第一份 PipifySettings。
                /// </summary>
                /// <returns>确定绑定的 PipifySettings。</returns>
                private static PipifySettingsSO ResolveWorkspaceFromArgs()
                {
                    string masterGuid = ReadArg("-configMasterGuid");
                    string pipifyGuid = ReadArg("-pipifySettingsGuid");
                    bool hasMasterArg = !string.IsNullOrWhiteSpace(masterGuid);
                    bool hasPipifyArg = !string.IsNullOrWhiteSpace(pipifyGuid);
                    if (hasMasterArg != hasPipifyArg)
                    {
                        throw new InvalidOperationException(
                            string.Format("{0}[CLI] -configMasterGuid 与 -pipifySettingsGuid 必须成对提供。", c_LogPrefix));
                    }

                    if (hasMasterArg)
                    {
                        ConfigMasterSO master = LoadAssetByGuid<ConfigMasterSO>(masterGuid, "ConfigMaster");
                        PipifySettingsSO settings = LoadAssetByGuid<PipifySettingsSO>(pipifyGuid, "PipifySettings");
                        if (!Config.WorkspaceActive.TrySetExplicitWorkspace(master, settings, out string workspaceError))
                        {
                            throw new InvalidOperationException(
                                string.Format("{0}[CLI] 显式工作区无效：{1}", c_LogPrefix, workspaceError));
                        }
                    }

                    if (!Config.WorkspaceActive.TryGetPersistedConfigMaster(out _, out _, out _, out string masterError))
                    {
                        throw new InvalidOperationException(string.Format("{0}[CLI] {1}", c_LogPrefix, masterError));
                    }
                    if (!Config.WorkspaceActive.TryGetPersistedPipifySettings(
                            out PipifySettingsSO activeSettings, out _, out _, out string pipifyError))
                    {
                        throw new InvalidOperationException(string.Format("{0}[CLI] {1}", c_LogPrefix, pipifyError));
                    }
                    return activeSettings;
                }

                /// <summary>
                /// 按 GUID 加载 CLI 显式指定的资产，并验证类型。
                /// </summary>
                /// <typeparam name="T">目标 Unity 资产类型。</typeparam>
                /// <param name="guid">命令行传入的资产 GUID。</param>
                /// <param name="label">错误信息中的资产名称。</param>
                /// <returns>加载成功的资产。</returns>
                private static T LoadAssetByGuid<T>(string guid, string label) where T : UnityEngine.Object
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    T asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset == null)
                    {
                        throw new InvalidOperationException(
                            string.Format("{0}[CLI] {1} GUID 无效或类型不匹配：{2}", c_LogPrefix, label, guid));
                    }
                    return asset;
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
