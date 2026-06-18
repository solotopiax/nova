/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugComponentInspector.cs
 * author:    taoye
 * created:   2026/3/27
 * descrip:   Debug 组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Debug 组件编辑器面板定制。
    /// </summary>
    [CustomEditor(typeof(DebugComponent))]
    internal sealed partial class DebugComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 启用。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            m_DebuggerActiveType = serializedObject.FindProperty("m_DebuggerActiveType");
            m_MaximumConsoleEntries = serializedObject.FindProperty("m_MaximumConsoleEntries");
            m_CurManagerTypeName = serializedObject.FindProperty("m_CurManagerTypeName");
            m_ManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IDebugManager)));

            m_DiskCheckingConfigs = serializedObject.FindProperty("m_DiskCheckingConfigs");

            ResetConfigToDefault();
            m_HistoryAABPath = DefaultAABFolder;
            m_HistoryAPKPath = DefaultAPKFolder;
            ReadConfig();
        }

        /// <summary>
        /// 绘制 Inspector。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            DrawConfigs();
            DrawDiskMonitoring();
            EditorUtil.Draw.Space(5);
            DrawAndroidBuild();

            FinalRefreshInspectorGUI();
        }

    }
}
