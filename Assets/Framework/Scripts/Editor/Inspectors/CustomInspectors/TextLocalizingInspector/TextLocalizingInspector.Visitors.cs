/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TextLocalizingInspector.Visitors.cs
 * author:    taoye
 * created:   2026/4/24
 * descrip:   文本本地化组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class TextLocalizingInspector : BaseComponentInspector
    {
        /// <summary>
        /// 本地化键名属性。
        /// </summary>
        private SerializedProperty m_LocalizingKeyName;

        /// <summary>
        /// 字体标记属性。
        /// </summary>
        private SerializedProperty m_LocalizingFontMark;

        /// <summary>
        /// 编辑器模式下缓存的当前语言名称（用于预览）。
        /// </summary>
        private string m_PreviewLanguageName;

        /// <summary>
        /// 编辑器模式下缓存的译文（用于预览）。
        /// </summary>
        private string m_PreviewTranslation;

        /// <summary>
        /// 上次预览刷新时的 Key 值（用于检测变化）。
        /// </summary>
        private string m_LastPreviewKey;

        /// <summary>
        /// 编辑器模式下缓存的语言文本数据（键名, 本地化内容）。
        /// </summary>
        private Dictionary<string, string> m_CachedLanguageTexts;

        /// <summary>
        /// 从字体 JSON 中解析出的唯一 Mark 选项列表。
        /// </summary>
        private string[] m_FontMarkOptions;

        /// <summary>
        /// 是否成功加载了字体 Mark 数据。
        /// </summary>
        private bool m_HasFontMarkData;
    }
}
