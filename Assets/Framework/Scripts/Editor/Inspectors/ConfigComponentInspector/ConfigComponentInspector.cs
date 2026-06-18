/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigComponentInspector.cs
 * author:    taoye
 * created:   2026/1/21
 * descrip:   配置组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 配置组件编辑器面板定制。
    /// </summary>
    [CustomEditor(typeof(ConfigComponent))]
    internal sealed partial class ConfigComponentInspector : BaseComponentInspector
    {
        /// <inheritdoc />
        protected override string TemplateFileName => "ConfigTemplate.xlsx";

        /// <inheritdoc />
        protected override float TemplateLabelWidth => EditorUtil.Draw.SourceFileTree.c_DirLabelWidth;

        /// <summary>
        /// 启用时绑定 SerializedProperty 并收集管理器类型名称列表。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            m_CurManagerTypeName = serializedObject.FindProperty("m_CurManagerTypeName");
            m_AssetLocationSP = serializedObject.FindProperty("m_AssetLocation");

            m_ManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IConfigManager)));
        }

        /// <summary>
        /// 禁用。
        /// </summary>
        private void OnDisable()
        {
        }

        /// <summary>
        /// 绘制 Inspector：依次绘制配置并执行最终刷新。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            FinalRefreshInspectorGUI();
        }

    }
}
