/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrivacyAesMaintenanceGuard.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   Editor默认AES维护启动与构建门禁
 ***************************************************************/

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>跨重载的默认 AES 维护门禁；仅检查 Runtime 约定的当前项目固定标记。</summary>
    [InitializeOnLoad]
    internal sealed class PrivacyAesMaintenanceGuard : IPreprocessBuildWithReport
    {
        /// <summary>重新加载 Editor 后重新绑定 Play 门禁；不改变用户的数据或维护日志。</summary>
        static PrivacyAesMaintenanceGuard() => EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        /// <summary>在其它构建写入之前检查未完成维护，当前门禁无 SDK 注入副作用。</summary>
        public int callbackOrder => int.MinValue;

        /// <summary>有未完成维护即阻断构建，异常只显示稳定提示，不回显日志内容。</summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            try { NovaFramework.Runtime.PrivacyAesMaintenanceGuard.AssertReady(); }
            catch { throw new BuildFailedException("存档密钥维护尚未完成，请先检查并恢复该操作，再构建游戏。"); }
        }

        /// <summary>进入 Play 前阻止未完成维护，Domain Reload 关闭时 Config/Persist 仍须再次检查 Runtime 门禁。</summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try { NovaFramework.Runtime.PrivacyAesMaintenanceGuard.AssertReady(); }
            catch
            {
                EditorApplication.isPlaying = false;
                Debug.LogError("存档密钥维护尚未完成，请先通过 Codex 检查并恢复该操作，再启动游戏。");
            }
        }
    }
}
