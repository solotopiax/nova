/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableComponentInspector.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   Table 官方 Luban Project Inspector
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 配置 Table Luban Project、Runtime Bindings 和运行态诊断。
    /// </summary>
    [CustomEditor(typeof(TableComponent))]
    internal sealed partial class TableComponentInspector : BaseComponentInspector
    {
        protected override string TemplateFileName => "TableListTemplate.xlsx";

        /// <summary>
        /// 绑定与 TableSettings 声明顺序一致的 Manager、Project 和 Runtime Bindings 属性。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            m_CurManagerTypeName = serializedObject.FindProperty("m_CurManagerTypeName");
            m_Setting = serializedObject.FindProperty("m_Setting");
            m_Project = m_Setting?.FindPropertyRelative("Project");
            m_Runtime = m_Setting?.FindPropertyRelative("Runtime");
            m_ManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(ITableManager)));
        }

        /// <summary>
        /// 按配置、Luban Project 导出和运行时诊断顺序绘制 Inspector。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            DrawTableExport();
            DrawRuntimeInfos();
            FinalRefreshInspectorGUI();
        }
    }
}
