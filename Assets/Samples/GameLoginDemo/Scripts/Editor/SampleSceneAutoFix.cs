/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SampleSceneAutoFix.cs
 * author:    taoye
 * created:   2026/5/28
 * descrip:   切换到 LoginDemo scene 时自动写 Globals.json + 注入 YooAsset
 ***************************************************************/

using NovaFramework.Editor;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace NovaFramework.Kit.Network.GameLogin.Samples.Editor
{
    /// <summary>
    /// LoginDemo sample scene 切换守护：以 Single 模式打开本 sample 主场景时，
    /// 写入 Globals.json 锁定本 sample 的 ConfigMaster，并注入 YooAsset 设置。
    /// </summary>
    [InitializeOnLoad]
    public static class SampleSceneAutoFix
    {
        static SampleSceneAutoFix()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (mode != OpenSceneMode.Single) return;
            if (string.IsNullOrEmpty(scene.path)) return;

            // 利用 WorkspaceActive.Get() 内部 TryInferFromOpenedSampleScene 上溯推断，
            // 天然兼容 UPM 嵌套路径 Assets/Samples/{Pkg}/{Ver}/{Sample}/...
            ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
            if (master != null)
                EditorUtil.Config.YooAssetInjector.Inject(master);
        }
    }
}
