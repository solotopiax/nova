/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Pipify.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 自动化流水线执行引擎入口（Editor-only）
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Pipify 自动化流水线执行引擎入口（Editor-only）。
        /// 对外暴露 Batch 执行方法；UI 与 CLI 共享同一 Runner。
        /// </summary>
        public static partial class Pipify
        {
            /// <summary>
            /// 记录当前工作区实际激活的 PipifySettings。
            /// 由 EditorUtil.SceneRoute 在 Single 场景打开后调用，避免自身重复订阅 sceneOpened。
            /// </summary>
            internal static void LogActiveSettings()
            {
                PipifySettingsSO settings = Config.WorkspaceActive.GetPipifySettings();
                if (settings == null) return;

                string assetPath = AssetDatabase.GetAssetPath(settings);
                if (string.IsNullOrEmpty(assetPath)) return;

                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                Log.Debug(LogTag.Editor, "{0} 已激活 PipifySettings：{1}（{2}）", c_LogPrefix, assetPath, guid);
            }

            /// <summary>
            /// 按当前 active scene 所属 sample 推断配对的 PipifySettings.asset。
            /// </summary>
            internal static PipifySettingsSO FindSettingsForActiveScene()
            {
                return FindSettingsForScene(EditorSceneManager.GetActiveScene().path);
            }

            /// <summary>
            /// 按 scene 路径逐级向上推断配对的 PipifySettings.asset；非 sample scene 返回 null。
            /// 支持开发态扁平结构与 UPM 导入态嵌套结构。
            /// </summary>
            internal static PipifySettingsSO FindSettingsForScene(string scenePath)
            {
                if (string.IsNullOrEmpty(scenePath)) return null;

                string normalizedScenePath = scenePath.Replace('\\', '/');
                if (!normalizedScenePath.StartsWith("Assets/Samples/")) return null;

                string dir = System.IO.Path.GetDirectoryName(normalizedScenePath)?.Replace('\\', '/');
                while (!string.IsNullOrEmpty(dir) && dir.StartsWith("Assets/Samples/"))
                {
                    string candidate = $"{dir}/Editor/PipifySettings.asset";
                    PipifySettingsSO settings = AssetDatabase.LoadAssetAtPath<PipifySettingsSO>(candidate);
                    if (settings != null) return settings;
                    dir = System.IO.Path.GetDirectoryName(dir)?.Replace('\\', '/');
                }

                return null;
            }

            /// <summary>
            /// 定位实际持有指定 Batch 实例的 PipifySettingsSO，供旧参数首次执行时精确保存原存档。
            /// </summary>
            /// <param name="batch">待定位归属的 Batch 实例。</param>
            /// <returns>持有该实例的 PipifySettingsSO；未找到时返回 null。</returns>
            internal static PipifySettingsSO FindSettingsContaining(Batch batch)
            {
                if (batch == null) return null;
                string[] guids = AssetDatabase.FindAssets("t:" + nameof(PipifySettingsSO));
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    PipifySettingsSO settings = AssetDatabase.LoadAssetAtPath<PipifySettingsSO>(path);
                    if (settings != null && settings.Batches.Contains(batch))
                    {
                        return settings;
                    }
                }
                return null;
            }

            /// <summary>
            /// UI 宿主入口：使用模态进度条 Reporter 执行 Batch。
            /// 由调用方传入宿主 EditorWindow，Batch 结束后 WindowReporter 通过宿主弹 ShowNotification。
            /// </summary>
            /// <param name="batch">待执行 Batch。</param>
            /// <param name="host">承载 ShowNotification 的宿主窗口；null 时仅写日志不弹通知。</param>
            /// <returns>UniTask 句柄。</returns>
            public static UniTask RunBatchAsync(Batch batch, EditorWindow host)
            {
                ValidateBatchWorkspace(batch);
                return Runner.RunBatchAsync(batch, new WindowReporter(host), null, CancellationToken.None);
            }

            /// <summary>
            /// CLI 宿主入口：使用纯日志 Reporter 执行 Batch，支持参数覆盖。
            /// </summary>
            /// <param name="batch">待执行 Batch。</param>
            /// <param name="overrides">参数覆盖字典；null 表示不覆盖。</param>
            /// <returns>UniTask 句柄。</returns>
            public static UniTask RunBatchForCliAsync(Batch batch, IReadOnlyDictionary<string, string> overrides)
            {
                ValidateBatchWorkspace(batch);
                return Runner.RunBatchAsync(batch, new CliReporter(), overrides, CancellationToken.None);
            }

            /// <summary>
            /// 在执行前验证 Batch 所属 PipifySettings 与 Globals 当前工作区一致，且存在有效 ConfigMaster。
            /// 该门禁阻止窗口、CLI 或旧调用方把某个 Sample 的 Batch 与另一套 Master 静默混用。
            /// </summary>
            /// <param name="batch">待执行的 Batch。</param>
            private static void ValidateBatchWorkspace(Batch batch)
            {
                if (batch == null) throw new System.ArgumentNullException(nameof(batch));
                PipifySettingsSO owner = FindSettingsContaining(batch);
                if (owner == null)
                {
                    throw new System.InvalidOperationException($"{c_LogPrefix} 无法确定 Batch 所属 PipifySettingsSO。");
                }

                if (!Config.WorkspaceActive.TryGetPersistedPipifySettings(
                        out PipifySettingsSO activeSettings, out _, out _, out string pipifyError))
                {
                    throw new System.InvalidOperationException($"{c_LogPrefix} {pipifyError}");
                }
                if (!ReferenceEquals(owner, activeSettings))
                {
                    throw new System.InvalidOperationException(
                        $"{c_LogPrefix} Batch 所属 PipifySettings 与 Globals 当前绑定不一致，已拒绝执行。");
                }
                if (!Config.WorkspaceActive.TryGetPersistedConfigMaster(out _, out _, out _, out string masterError))
                {
                    throw new System.InvalidOperationException($"{c_LogPrefix} {masterError}");
                }
            }
        }
    }
}
