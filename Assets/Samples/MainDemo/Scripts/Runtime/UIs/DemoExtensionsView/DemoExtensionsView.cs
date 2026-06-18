/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoExtensionsView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 1.8 — 扩展方法 5 个代表演示
 *            覆盖框架已有扩展：
 *            string.ToTitleCase / string.UppercaseFirst /
 *            string.RemoveExtraSpaces / string.GetChineseNum /
 *            float.FloatToTimeString
 ***************************************************************/

using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 1.8 扩展方法代表演示 View。
    /// 展示 5 个框架扩展方法：ToTitleCase / UppercaseFirst /
    /// RemoveExtraSpaces / GetChineseNum / FloatToTimeString。
    /// </summary>
    public sealed class DemoExtensionsView : BaseDemoView
    {
        /// <summary>
        /// 公共输入框，多卡片共用（每次触发按钮时读取当前文本）。
        /// </summary>

        [SerializeField] private TMP_InputField m_TextInput;

        /// <summary>
        /// 浮点秒数输入框，供 FloatToTimeString 使用。
        /// </summary>

        [SerializeField] private TMP_InputField m_FloatInput;

        /// <summary>
        /// 「ToTitleCase」按钮，将输入字符串标题化。
        /// </summary>

        [SerializeField] private Button m_TitleCaseButton;

        /// <summary>
        /// 「UppercaseFirst」按钮，将输入字符串首字母大写。
        /// </summary>

        [SerializeField] private Button m_UppercaseFirstButton;

        /// <summary>
        /// 「RemoveExtraSpaces」按钮，压缩输入字符串多余空格。
        /// </summary>

        [SerializeField] private Button m_RemoveSpacesButton;

        /// <summary>
        /// 「GetChineseNum」按钮，统计输入字符串中的汉字数量。
        /// </summary>

        [SerializeField] private Button m_ChineseNumButton;

        /// <summary>
        /// 「FloatToTimeString」按钮，将浮点秒数格式化为时间字符串。
        /// </summary>

        [SerializeField] private Button m_FloatToTimeButton;

        /// <summary>
        /// 初始化钩子，注册所有按钮事件。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            if (m_TitleCaseButton != null)
            {
                m_TitleCaseButton.onClick.AddListener(OnTitleCaseClick);
                SetButtonApiHint(m_TitleCaseButton, "string.ToTitleCase()");
            }

            if (m_UppercaseFirstButton != null)
            {
                m_UppercaseFirstButton.onClick.AddListener(OnUppercaseFirstClick);
                SetButtonApiHint(m_UppercaseFirstButton, "string.UppercaseFirst()");
            }

            if (m_RemoveSpacesButton != null)
            {
                m_RemoveSpacesButton.onClick.AddListener(OnRemoveSpacesClick);
                SetButtonApiHint(m_RemoveSpacesButton, "string.RemoveExtraSpaces()");
            }

            if (m_ChineseNumButton != null)
            {
                m_ChineseNumButton.onClick.AddListener(OnChineseNumClick);
                SetButtonApiHint(m_ChineseNumButton, "string.GetChineseNum()");
            }

            if (m_FloatToTimeButton != null)
            {
                m_FloatToTimeButton.onClick.AddListener(OnFloatToTimeClick);
                SetButtonApiHint(m_FloatToTimeButton, "float.FloatToTimeString()");
            }
        }

        /// <summary>
        /// 打开钩子，设置标题与 API 副标题，并预填示例输入。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            SetTitle("扩展方法代表");

            if (m_TextInput != null && string.IsNullOrEmpty(m_TextInput.text))
            {
                m_TextInput.text = "hello nova 你好世界";
            }
            if (m_FloatInput != null && string.IsNullOrEmpty(m_FloatInput.text))
            {
                m_FloatInput.text = "3661.5";
            }
        }

        /// <summary>
        /// 「ToTitleCase」点击回调。
        /// </summary>
        private void OnTitleCaseClick()
        {
            string input = GetTextInput();
            string result = input.ToTitleCase();
            AppendFeedback($"\"{input}\".ToTitleCase() -> \"{result}\"", FeedbackLevel.Success);
        }

        /// <summary>
        /// 「UppercaseFirst」点击回调。
        /// </summary>
        private void OnUppercaseFirstClick()
        {
            string input = GetTextInput();
            string result = input.UppercaseFirst();
            AppendFeedback($"\"{input}\".UppercaseFirst() -> \"{result}\"", FeedbackLevel.Success);
        }

        /// <summary>
        /// 「RemoveExtraSpaces」点击回调。
        /// </summary>
        private void OnRemoveSpacesClick()
        {
            string input = GetTextInput();
            string result = input.RemoveExtraSpaces();
            AppendFeedback($"\"{input}\".RemoveExtraSpaces() -> \"{result}\"", FeedbackLevel.Success);
        }

        /// <summary>
        /// 「GetChineseNum」点击回调。
        /// </summary>
        private void OnChineseNumClick()
        {
            string input = GetTextInput();
            int count = input.GetChineseNum();
            AppendFeedback($"\"{input}\".GetChineseNum() -> {count} 个汉字", FeedbackLevel.Success);
        }

        /// <summary>
        /// 「FloatToTimeString」点击回调，将浮点秒数格式化为 mm:ss 字符串。
        /// </summary>
        private void OnFloatToTimeClick()
        {
            string raw = m_FloatInput != null ? m_FloatInput.text : "0";
            if (!float.TryParse(raw, out float seconds))
            {
                AppendFeedback($"输入 \"{raw}\" 不是有效浮点数", FeedbackLevel.Warn);
                return;
            }

            string result = seconds.FloatToTimeString(displayHours: true, displayMinutes: true, displaySeconds: true);
            AppendFeedback($"{seconds}f.FloatToTimeString() -> \"{result}\"", FeedbackLevel.Success);
        }

        /// <summary>
        /// 获取文本输入框的当前内容，输入为空时返回默认值。
        /// </summary>
        /// <returns>输入文本。</returns>
        private string GetTextInput()
        {
            return m_TextInput != null && !string.IsNullOrEmpty(m_TextInput.text)
                ? m_TextInput.text
                : "hello nova";
        }
    }
}
