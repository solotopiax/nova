/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TextLocalizingAutoMount.cs
 * author:    taoye
 * created:   2026/4/11
 * descrip:   TextLocalizing 自动挂载钩子
 ***************************************************************/

using NovaFramework.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// TextLocalizing 自动挂载钩子。
    /// 监听编辑器中 TextMeshProUGUI 组件的添加，自动挂载 TextLocalizing 并设置默认字体标记。
    /// </summary>
    internal static class TextLocalizingAutoMount
    {
        /// <summary>
        /// 默认字体标记。
        /// </summary>
        private const string c_DefaultFontMark = "Main";

        /// <summary>
        /// 注册组件添加回调。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Register()
        {
            ObjectFactory.componentWasAdded -= OnComponentAdded;
            ObjectFactory.componentWasAdded += OnComponentAdded;
        }

        /// <summary>
        /// 组件添加回调。当添加 TextMeshProUGUI 时自动挂载 TextLocalizing。
        /// </summary>
        /// <param name="component">新添加的组件。</param>
        private static void OnComponentAdded(Component component)
        {
            if (!(component is TextMeshProUGUI))
            {
                return;
            }

            if (component.GetComponent<TextLocalizing>() != null)
            {
                return;
            }

            TextLocalizing textLocalizing = Undo.AddComponent<TextLocalizing>(component.gameObject);
            SerializedObject so = new SerializedObject(textLocalizing);
            SerializedProperty fontMarkProp = so.FindProperty("m_LocalizingFontMark");
            if (fontMarkProp != null && string.IsNullOrEmpty(fontMarkProp.stringValue))
            {
                fontMarkProp.stringValue = c_DefaultFontMark;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
