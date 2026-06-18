/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateComponentInspector.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 振动组件编辑器面板定制。
    /// </summary>
    [CustomEditor(typeof(VibrateComponent))]
    internal sealed partial class VibrateComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 启用时绑定 SerializedProperty、收集管理器类型名称列表。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            m_CurManagerTypeName = serializedObject.FindProperty("m_CurManagerTypeName");
            m_Settings = serializedObject.FindProperty("m_Settings");

            m_ManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IVibrateManager)));

            m_EmphasisSourceDirPath = m_Settings?.FindPropertyRelative("EmphasisSourceDirPath");
            m_CustomSourceDirPath = m_Settings?.FindPropertyRelative("CustomSourceDirPath");
            m_EmphasisUnitsSettings = m_Settings?.FindPropertyRelative("EmphasisUnitsSettings");
            m_CustomUnitsSettings = m_Settings?.FindPropertyRelative("CustomUnitsSettings");
        }

        /// <summary>
        /// 绘制 Inspector：依次绘制配置、Emphasis 导出区域、Custom 导出区域，并执行最终刷新。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            DrawEmphasisVibrateExport();
            DrawCustomVibrateExport();
            FinalRefreshInspectorGUI();
        }
    }
}
