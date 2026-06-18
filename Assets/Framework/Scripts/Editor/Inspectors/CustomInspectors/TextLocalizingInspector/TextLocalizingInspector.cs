/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TextLocalizingInspector.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   文本本地化组件编辑器面板定制
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 文本本地化组件编辑器面板定制。
    /// </summary>
    [CustomEditor(typeof(TextLocalizing))]
    [CanEditMultipleObjects]
    internal sealed partial class TextLocalizingInspector : BaseComponentInspector
    {
        /// <summary>
        /// 启用。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            m_LocalizingKeyName = serializedObject.FindProperty("m_LocalizingKeyName");
            m_LocalizingFontMark = serializedObject.FindProperty("m_LocalizingFontMark");

            m_PreviewLanguageName = string.Empty;
            m_PreviewTranslation = string.Empty;
            m_LastPreviewKey = string.Empty;
            m_CachedLanguageTexts = null;

            TryLoadPreviewData();
            TryLoadFontMarkOptions();
        }

        /// <summary>
        /// 绘制。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorUtil.Draw.Property("本地化 Key", m_LocalizingKeyName, true, GUILayout.Width(175));
            DrawFontMarkField();
            EditorUtil.Draw.Line();

            DrawPreview();

            FinalRefreshInspectorGUI();
        }
    }
}
