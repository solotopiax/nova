/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.SceneRoute.cs
 * author:    taoye
 * created:   2026/8/19
 * descrip:   Sample 场景打开时的 Editor 配置路由摘要与统一编排
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Sample 场景打开时的 Editor 配置路由编排。
        /// 统一输出场景、ConfigMaster、YooAssetSettings、PipifySettings 四个阶段，
        /// 避免多个独立 sceneOpened 监听器造成重复或不稳定的日志。
        /// </summary>
        internal static class SceneRoute
        {
            /// <summary>
            /// 域重载时注册场景打开监听；先解绑再绑定，避免重复订阅。
            /// </summary>
            [InitializeOnLoadMethod]
            private static void HookSceneOpened()
            {
                EditorSceneManager.sceneOpened -= OnSceneOpened;
                EditorSceneManager.sceneOpened += OnSceneOpened;
            }

            /// <summary>
            /// 单场景打开后按固定顺序解析并记录 Editor 配置路由。
            /// Additive 场景不切换激活配置，直接忽略。
            /// </summary>
            /// <param name="scene">新打开的场景。</param>
            /// <param name="mode">场景打开模式。</param>
            private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
            {
                if (mode != OpenSceneMode.Single) return;

                Log.Debug(LogTag.Editor, "[SceneRoute] 已打开场景：{0}", scene.path);
                if (!Config.WorkspaceActive.ReconcileScene(scene.path))
                {
                    Log.Warning(LogTag.Editor, "[SceneRoute] 工作区切换失败，已停止后续配置注入：{0}", scene.path);
                    return;
                }
                ConfigMasterSO master = Config.WorkspaceActive.Get();
                LogActiveConfigMaster(master);
                Config.YooAssetInjector.Inject(master);
                Pipify.LogActiveSettings();
            }

            /// <summary>
            /// 输出当前路由实际使用的 ConfigMaster；缺少资产时保持静默，沿用注入层的失败语义。
            /// </summary>
            /// <param name="master">当前激活的 ConfigMaster。</param>
            private static void LogActiveConfigMaster(ConfigMasterSO master)
            {
                if (master == null) return;

                string assetPath = AssetDatabase.GetAssetPath(master);
                if (string.IsNullOrEmpty(assetPath)) return;

                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                Log.Debug(LogTag.Editor, "[WorkspaceActive] 已激活 ConfigMaster：{0}（{1}）", assetPath, guid);
            }
        }
    }
}
