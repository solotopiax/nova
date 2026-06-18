/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationComponentInspector.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   本地化组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// LocalizationComponent 的 CustomEditor，负责本地化管理器配置、Luban 导出和数据源文件树的绘制。
    /// </summary>
    [CustomEditor(typeof(LocalizationComponent))]
    internal sealed partial class LocalizationComponentInspector : BaseComponentInspector
    {
        /// <inheritdoc />
        protected override string TemplateFileName => "LocalizationTextTemplate.xlsx";

        /// <summary>
        /// 字体数据模板文件名。
        /// </summary>
        private const string c_FontTemplateFileName = "LocalizationFontTemplate.xlsx";

        /// <inheritdoc />
        protected override float TemplateLabelWidth => c_LabelWidth;

        /// <summary>
        /// 启用时绑定 SerializedProperty 并收集管理器类型名称列表。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            m_CurLocalizationManagerTypeName = serializedObject.FindProperty("m_CurLocalizationManagerTypeName");
            m_EditorLanguage = serializedObject.FindProperty("m_EditorLanguage");
            m_RuntimeLanguagePrefer = serializedObject.FindProperty("m_RuntimeLanguagePrefer");
            m_FallbackLanguage = serializedObject.FindProperty("m_FallbackLanguage");
            m_AutoFontAdapt = serializedObject.FindProperty("m_AutoFontAdapt");

            m_LocalizationSettings = serializedObject.FindProperty("m_LocalizationSettings");
            m_TextSourceDirPath = m_LocalizationSettings?.FindPropertyRelative("TextSourceDirPath");
            m_FontSourceDirPath = m_LocalizationSettings?.FindPropertyRelative("FontSourceDirPath");
            m_TextUnitsSettings = m_LocalizationSettings?.FindPropertyRelative("TextUnitsSettings");
            m_FontUnitsSettings = m_LocalizationSettings?.FindPropertyRelative("FontUnitsSettings");

            InitializeTemplatePath(serializedObject.FindProperty("m_TextTemplatePath"));

            m_FontTemplatePath = serializedObject.FindProperty("m_FontTemplatePath");
            if (m_FontTemplatePath != null && string.IsNullOrEmpty(m_FontTemplatePath.stringValue))
            {
                m_FontTemplatePath.stringValue = EditorUtil.FileSystem.ResolveTemplatePath(c_FontTemplateFileName);
                m_FontTemplatePath.serializedObject.ApplyModifiedProperties();
            }

            m_SupportedLanguagesJsonExportPath = serializedObject.FindProperty("m_SupportedLanguagesJsonExportPath");
            m_SupportedLanguagesAssetLocation = serializedObject.FindProperty("m_SupportedLanguagesAssetLocation");

            m_LocalizationManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(ILocalizationManager)));
        }

        /// <summary>
        /// 绘制 Inspector：依次绘制配置区域并执行最终刷新。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            DrawTextExportSection();
            DrawFontExportSection();
            FinalRefreshInspectorGUI();
        }
    }
}
