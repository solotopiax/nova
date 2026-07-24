/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.SchemaMigration.cs
 * author:    taoye
 * created:   2026/7/24
 * descrip:   ConfigMasterSO 结构版本迁移入口
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// ConfigMasterSO 结构版本迁移入口；迁移行为仅存在于 Editor 程序集。
            /// </summary>
            public static class SchemaMigration
            {
                private static bool s_IsRunning;

                /// <summary>
                /// 脚本重载后延迟扫描旧版 ConfigMasterSO，避开程序集加载与 AssetDatabase 刷新阶段。
                /// </summary>
                [UnityEditor.InitializeOnLoadMethod]
                private static void ScheduleAutomaticMigration()
                {
                    UnityEditor.EditorApplication.delayCall -= RunAutomaticMigration;
                    UnityEditor.EditorApplication.delayCall += RunAutomaticMigration;
                }

                /// <summary>
                /// 将指定 ConfigMasterSO 升级到当前结构版本；重复调用不会覆盖已迁移的新数据。
                /// </summary>
                /// <param name="master">待迁移的设计态配置资产。</param>
                /// <param name="changed">成功时返回本次是否实际修改了结构版本。</param>
                /// <param name="error">失败时返回原因；成功时为 null。</param>
                /// <returns>迁移成功或无需迁移时返回 true；输入无效或数据损坏时返回 false。</returns>
                public static bool TryMigrate(ConfigMasterSO master, out bool changed, out string error)
                {
                    changed = false;
                    error = null;
                    if (master == null)
                    {
                        error = "ConfigMasterSO 不能为空。";
                        return false;
                    }

                    return master.TryMigrateLegacyData(out changed, out error);
                }

                /// <summary>
                /// 迁移单个 ConfigMasterSO，并在其绑定了导出目标时按当前三维坐标重建 Runtime 快照。
                /// 导出前先验证目标路径与矩阵行，避免前置条件失败后仍推进结构版本。
                /// </summary>
                /// <param name="master">待迁移的设计态配置资产。</param>
                /// <param name="changed">成功时返回 Master 是否发生结构迁移。</param>
                /// <param name="exported">成功时返回是否重新导出了 Runtime 快照。</param>
                /// <param name="error">失败时返回原因；成功时为 null。</param>
                /// <returns>迁移及必要的重导出全部成功时返回 true。</returns>
                public static bool TryMigrateAndReexport(ConfigMasterSO master, out bool changed, out bool exported, out string error)
                {
                    changed = false;
                    exported = false;
                    error = null;

                    if (master == null)
                    {
                        error = "ConfigMasterSO 不能为空。";
                        return false;
                    }

                    string exportPath = null;
                    if (master.ConfigSchemaVersion != ConfigMasterSO.CurrentConfigSchemaVersion && master.ExportTarget != null)
                    {
                        exportPath = UnityEditor.AssetDatabase.GetAssetPath(master.ExportTarget);
                        if (string.IsNullOrEmpty(exportPath))
                        {
                            error = "ConfigMasterSO.ExportTarget 尚未保存为资产，无法在迁移后安全重导出。";
                            return false;
                        }

                        if (!master.TryGetEntry(master.CurrentPlatform, master.CurrentChannel, out _))
                        {
                            error = $"找不到当前导出坐标：{master.CurrentPlatform}/{master.CurrentChannel}/{master.CurrentDevelopMode}。";
                            return false;
                        }
                    }

                    if (!TryMigrate(master, out changed, out error))
                    {
                        return false;
                    }

                    if (!changed)
                    {
                        return true;
                    }

                    UpdateEditorClassIdentifier(master);
                    UnityEditor.EditorUtility.SetDirty(master);
                    if (master.ExportTarget == null)
                    {
                        UnityEditor.AssetDatabase.SaveAssets();
                        return true;
                    }

                    ConfigRuntimeSO runtime = Exporter.Export(
                        master,
                        master.CurrentPlatform,
                        master.CurrentChannel,
                        master.CurrentDevelopMode,
                        exportPath);
                    if (runtime == null)
                    {
                        error = "结构迁移完成，但 Runtime 快照重导出失败。";
                        return false;
                    }

                    exported = true;
                    return true;
                }

                /// <summary>
                /// 将旧资产残留的 Runtime EditorClassIdentifier 更新为 ConfigMasterSO 当前程序集与完整类型名。
                /// </summary>
                /// <param name="master">已完成结构迁移的 ConfigMasterSO。</param>
                private static void UpdateEditorClassIdentifier(ConfigMasterSO master)
                {
                    var serialized = new UnityEditor.SerializedObject(master);
                    UnityEditor.SerializedProperty identifier = serialized.FindProperty("m_EditorClassIdentifier");
                    if (identifier == null)
                    {
                        return;
                    }

                    string expected = master.GetType().Assembly.GetName().Name + "::" + master.GetType().FullName;
                    if (identifier.stringValue == expected)
                    {
                        return;
                    }

                    identifier.stringValue = expected;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                /// <summary>
                /// 脚本重载后的自动迁移回调；仅在非播放态执行，成功时保持静默。
                /// </summary>
                private static void RunAutomaticMigration()
                {
                    if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        return;
                    }

                    MigrateAllAssets();
                }

                /// <summary>
                /// 扫描工程内全部 ConfigMasterSO，逐个执行幂等迁移、保存与必要的 Runtime 重导出。
                /// </summary>
                private static void MigrateAllAssets()
                {
                    if (s_IsRunning)
                    {
                        return;
                    }

                    s_IsRunning = true;
                    try
                    {
                        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ConfigMasterSO");
                        System.Array.Sort(guids, System.StringComparer.Ordinal);
                        bool hasChanges = false;

                        for (int i = 0; i < guids.Length; i++)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                            ConfigMasterSO master = UnityEditor.AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(path);
                            if (!TryMigrateAndReexport(master, out bool changed, out _, out string error))
                            {
                                UnityEngine.Debug.LogError($"Config 结构迁移失败：{path}\n{error}");
                                continue;
                            }

                            hasChanges |= changed;
                        }

                        if (hasChanges)
                        {
                            UnityEditor.AssetDatabase.SaveAssets();
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
