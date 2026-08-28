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
    /// 为不依赖正式本地化加载状态的 TMP 文本应用语言方向和渲染预处理。
    /// </summary>
    internal static class LocalizationTextRenderer
    {
        /// <summary>
        /// 以独立语言上下文接管 TMP 文本渲染，并禁用同节点依赖正式本地化状态的组件。
        /// 原始文本保持 Unicode 逻辑顺序，仅在 TMP 生成网格前执行 RTL 转换。
        /// </summary>
        /// <param name="text">目标 TMP 文本。</param>
        /// <param name="language">本次渲染使用的明确语言。</param>
        /// <param name="value">保持逻辑顺序的原始文本。</param>
        internal static void ApplyIndependentText(TMP_Text text, Language language, string value)
        {
            if (text == null)
            {
                return;
            }

            // Launcher 有独立语言上下文，不能继续接收正式 LocalizationManager 的刷新。
            TextLocalizing textLocalizing = text.GetComponent<TextLocalizing>();
            if (textLocalizing != null)
            {
                textLocalizing.RelinquishTextRendering();
            }

            LocalizationTextPreprocessor preprocessor = text.textPreprocessor as LocalizationTextPreprocessor;
            if (preprocessor == null)
            {
                preprocessor = new LocalizationTextPreprocessor(text.textPreprocessor);
                text.textPreprocessor = preprocessor;
            }

            bool isRightToLeft = LanguageMetadata.IsRightToLeft(language);
            bool renderingStateChanged = preprocessor.Language != language || text.isRightToLeftText != isRightToLeft;
            preprocessor.Language = language;
            text.isRightToLeftText = isRightToLeft;
            text.text = value ?? string.Empty;

            if (renderingStateChanged)
            {
                text.SetVerticesDirty();
                text.SetLayoutDirty();
            }
        }
    }

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
