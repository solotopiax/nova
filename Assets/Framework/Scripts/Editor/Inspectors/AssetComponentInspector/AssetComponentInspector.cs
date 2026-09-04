/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetComponentInspector.cs
 * author:    taoye
 * created:   2026/1/12
 * descrip:   Asset组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Asset 组件编辑器面板定制。
    /// </summary>
    [CustomEditor(typeof(AssetComponent))]
    internal sealed partial class AssetComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 启用。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            // ① Manager 选择
            m_CurAssetManagerTypeName = serializedObject.FindProperty("m_CurAssetManagerTypeName");
            m_AssetManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IAssetManager)));

            // ② 加载模式
            m_EditorPlayMode = serializedObject.FindProperty("m_EditorPlayMode");
            m_RuntimePlayMode = serializedObject.FindProperty("m_RuntimePlayMode");

            // ③ 资源包配置
            m_Packages = serializedObject.FindProperty("m_Packages");
            // 仅首次选中时默认展开列表，之后尊重用户的折叠操作（不在 OnInspectorGUI 内每帧强制，否则折叠箭头点击无效）
            m_Packages.isExpanded = true;
            m_DefaultPackageName = serializedObject.FindProperty("m_DefaultPackageName");
            m_AutoCleanupOnSceneUnload = serializedObject.FindProperty("m_AutoCleanupOnSceneUnload");

            // ④ 热更配置（开关 + 服务器分发 + 下载行为）
            m_EnableHotfix = serializedObject.FindProperty("m_EnableHotfix");
            m_EnableStartupWhitelist = serializedObject.FindProperty("m_EnableStartupWhitelist");
            m_StartupWhitelistUrlDebug = serializedObject.FindProperty("m_StartupWhitelistUrlDebug");
            m_StartupWhitelistUrlFallbackDebug = serializedObject.FindProperty("m_StartupWhitelistUrlFallbackDebug");
            m_StartupWhitelistUrlRelease = serializedObject.FindProperty("m_StartupWhitelistUrlRelease");
            m_StartupWhitelistUrlFallbackRelease = serializedObject.FindProperty("m_StartupWhitelistUrlFallbackRelease");
            m_StartupWhitelistMetadataRootUrlDebug = serializedObject.FindProperty("m_StartupWhitelistMetadataRootUrlDebug");
            m_StartupWhitelistMetadataRootUrlFallbackDebug = serializedObject.FindProperty("m_StartupWhitelistMetadataRootUrlFallbackDebug");
            m_StartupWhitelistMetadataRootUrlRelease = serializedObject.FindProperty("m_StartupWhitelistMetadataRootUrlRelease");
            m_StartupWhitelistMetadataRootUrlFallbackRelease = serializedObject.FindProperty("m_StartupWhitelistMetadataRootUrlFallbackRelease");
            m_StartupWhitelistFallbackRoundCount = serializedObject.FindProperty("m_StartupWhitelistFallbackRoundCount");
            m_StartupWhitelistRetryRequestCount = serializedObject.FindProperty("m_StartupWhitelistRetryRequestCount");
            m_StartupWhitelistPreferLastSuccessfulHost = serializedObject.FindProperty("m_StartupWhitelistPreferLastSuccessfulHost");
            m_StartupWhitelistEnableUWRTracks = serializedObject.FindProperty("m_StartupWhitelistEnableUWRTracks");
            m_StartupWhitelistCheckTimeout = serializedObject.FindProperty("m_StartupWhitelistCheckTimeout");
            m_HostServerUrlDebug = serializedObject.FindProperty("m_HostServerUrlDebug");
            m_HostServerUrlFallbackDebug = serializedObject.FindProperty("m_HostServerUrlFallbackDebug");
            m_HostServerUrlRelease = serializedObject.FindProperty("m_HostServerUrlRelease");
            m_HostServerUrlFallbackRelease = serializedObject.FindProperty("m_HostServerUrlFallbackRelease");
            m_AutoHotfix = serializedObject.FindProperty("m_AutoHotfix");
            m_QuitOnFailedOrCancel = serializedObject.FindProperty("m_QuitOnFailedOrCancel");
            m_MaxDownloadConcurrency = serializedObject.FindProperty("m_MaxDownloadConcurrency");
            m_FallbackRoundCount = serializedObject.FindProperty("m_FallbackRoundCount");
            m_RetryDownloadCount = serializedObject.FindProperty("m_RetryDownloadCount");
            m_PreferLastSuccessfulHost = serializedObject.FindProperty("m_PreferLastSuccessfulHost");
            m_EnableUWRTracks = serializedObject.FindProperty("m_EnableUWRTracks");
            m_CheckTimeout = serializedObject.FindProperty("m_CheckTimeout");
            m_ManifestRequestTimeout = serializedObject.FindProperty("m_ManifestRequestTimeout");
            m_WebGLBundleRequestTimeout = serializedObject.FindProperty("m_WebGLBundleRequestTimeout");
            m_IdleTimeout = serializedObject.FindProperty("m_IdleTimeout");
            m_LaunchHotfixTags = serializedObject.FindProperty("m_LaunchHotfixTags");
            m_AutoClearUnusedCacheOnHotfix = serializedObject.FindProperty("m_AutoClearUnusedCacheOnHotfix");
        }

        /// <summary>
        /// 绘制。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            FinalRefreshInspectorGUI();
        }
    }
}
