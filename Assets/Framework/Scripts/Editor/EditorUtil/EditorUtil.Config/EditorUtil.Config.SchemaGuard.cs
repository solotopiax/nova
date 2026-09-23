/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.SchemaGuard.cs
 * author:    taoye
 * created:   2026/7/24
 * descrip:   ConfigMasterSO 结构版本保护入口
 ***************************************************************/

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// ConfigMasterSO 结构版本保护入口；只接受当前版本，不再迁移历史结构。
            /// </summary>
            public static class SchemaGuard
            {
                private static bool s_IsRunning;

                /// <summary>
                /// 脚本重载后延迟检查 ConfigMasterSO 版本，避开程序集加载与 AssetDatabase 刷新阶段。
                /// </summary>
                [UnityEditor.InitializeOnLoadMethod]
                private static void ScheduleAutomaticValidation()
                {
                    UnityEditor.EditorApplication.delayCall -= ValidateAllAssets;
                    UnityEditor.EditorApplication.delayCall += ValidateAllAssets;
                }

                /// <summary>
                /// 验证指定 ConfigMasterSO 是否使用当前结构版本。
                /// </summary>
                /// <param name="master">待验证的设计态配置资产。</param>
                /// <param name="error">失败时返回原因；成功时为 null。</param>
                /// <returns>资产存在且结构版本与当前代码一致时返回 true。</returns>
                public static bool Validate(ConfigMasterSO master, out string error)
                {
                    error = null;
                    if (master == null)
                    {
                        error = "ConfigMasterSO 不能为空。";
                        return false;
                    }

                    if (master.ConfigSchemaVersion != ConfigMasterSO.CurrentConfigSchemaVersion)
                    {
                        error = $"不支持的 ConfigMasterSO 结构版本：{master.ConfigSchemaVersion}；当前版本为 {ConfigMasterSO.CurrentConfigSchemaVersion}。";
                        return false;
                    }

                    return true;
                }

                /// <summary>
                /// 扫描工程内全部 ConfigMasterSO，对版本不一致的资产报错但不改写。
                /// </summary>
                private static void ValidateAllAssets()
                {
                    if (s_IsRunning || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        return;
                    }

                    s_IsRunning = true;
                    try
                    {
                        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ConfigMasterSO");
                        System.Array.Sort(guids, System.StringComparer.Ordinal);
                        for (int i = 0; i < guids.Length; i++)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                            ConfigMasterSO master = UnityEditor.AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(path);
                            if (!Validate(master, out string error))
                            {
                                UnityEngine.Debug.LogError($"Config 结构版本检查失败：{path}\n{error}");
                            }
                        }
                    }
                    finally
                    {
                        s_IsRunning = false;
                    }
                }
            }
        }
    }
}
