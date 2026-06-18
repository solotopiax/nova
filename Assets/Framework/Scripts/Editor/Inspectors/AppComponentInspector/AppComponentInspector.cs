/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppComponentInspector.cs
 * author:    taoye
 * created:   2026/5/16
 * descrip:   App 组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// App 组件编辑器面板定制。
    /// </summary>
    [CustomEditor(typeof(AppComponent))]
    internal sealed partial class AppComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 启用。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            m_CurManagerTypeName = serializedObject.FindProperty("m_CurManagerTypeName");
            m_AppDownloadCheckUrlDebug = serializedObject.FindProperty("m_AppDownloadCheckUrlDebug");
            m_AppDownloadCheckUrlFallbackDebug = serializedObject.FindProperty("m_AppDownloadCheckUrlFallbackDebug");
            m_AppDownloadCheckUrlRelease = serializedObject.FindProperty("m_AppDownloadCheckUrlRelease");
            m_AppDownloadCheckUrlFallbackRelease = serializedObject.FindProperty("m_AppDownloadCheckUrlFallbackRelease");
            m_TimeoutSeconds = serializedObject.FindProperty("m_TimeoutSeconds");
            m_DownloadRoute = serializedObject.FindProperty("m_DownloadRoute");
            m_AndroidStoreUrl = serializedObject.FindProperty("m_AndroidStoreUrl");
            m_AppStoreUrl = serializedObject.FindProperty("m_AppStoreUrl");
            m_PrimaryDownloadUrl = serializedObject.FindProperty("m_PrimaryDownloadUrl");
            m_FallbackDownloadUrl = serializedObject.FindProperty("m_FallbackDownloadUrl");
            m_UseRecommendedDownloadRule = serializedObject.FindProperty("m_UseRecommendedDownloadRule");
            m_UseForcedDownloadRule = serializedObject.FindProperty("m_UseForcedDownloadRule");

            m_AppManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IAppManager)));
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
