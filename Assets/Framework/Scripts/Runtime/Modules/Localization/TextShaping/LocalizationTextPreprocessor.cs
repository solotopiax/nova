/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationTextPreprocessor.cs
 * author:    taoye
 * created:   2026/8/27
 * descrip:   本地化文本渲染预处理器
 ***************************************************************/

using TMPro;
using NovaFramework.Runtime.Internal;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 在 TMP 渲染前为 RTL 语言执行字形连接与双向文本整理。
    /// 原始本地化文本和 TMP.text 始终保持 Unicode 逻辑顺序。
    /// </summary>
    internal sealed class LocalizationTextPreprocessor : ITextPreprocessor
    {
        private readonly ITextPreprocessor m_PreviousPreprocessor;
        private readonly FastStringBuilder m_ShapedText = new FastStringBuilder(RTLSupport.DefaultBufferSize);

        public LocalizationTextPreprocessor(ITextPreprocessor previousPreprocessor)
        {
            m_PreviousPreprocessor = previousPreprocessor;
        }

        public Language Language { get; set; }

        public string PreprocessText(string text)
        {
            string source = m_PreviousPreprocessor?.PreprocessText(text) ?? text;
            if (string.IsNullOrEmpty(source) || !LanguageMetadata.IsRightToLeft(Language))
            {
                return source;
            }

            m_ShapedText.Clear();
            RTLSupport.FixRTL(
                source,
                m_ShapedText,
                farsi: Language == Language.Persian,
                fixTextTags: true,
                preserveNumbers: true);

            // TMP 的 RTL 布局接收逻辑顺序；RTLSupport 先生成视觉顺序，因此这里反转回来。
            m_ShapedText.Reverse();
            return m_ShapedText.ToString();
        }
    }
}
