/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoVibrateView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.14 — Vibrate 模块演示视图（交互触发型）。
 *            演示振动类型预设、自定义振动（参数/name）、
 *            强调振动（参数/name）与停止全部。
 *            API：Nova.Vibrate.Play / PlayCustom / PlayEmphasis / StopAll
 ***************************************************************/

using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Vibrate 模块演示视图，演示振动类型预设、自定义参数/name、强调振动参数/name 与全停。
    /// 继承 BaseDemoView 三段式骨架，交互区含类型下拉与多个操作按钮，并显示 IsSupported 状态。
    /// </summary>
    public sealed class DemoVibrateView : BaseDemoView
    {
        /// <summary>
        /// 振动类型下拉（对应 VibrateType 枚举值名称）。
        /// </summary>

        [SerializeField] private TMP_Dropdown m_TypeDropdown;

        /// <summary>
        /// Play 按钮，调用 Play(VibrateType)。
        /// </summary>

        [SerializeField] private Button m_PlayButton;

        /// <summary>
        /// PlayCustom 按钮，调用 PlayCustom(intensity, sharpness, preDuration, duration)。
        /// </summary>

        [SerializeField] private Button m_PlayCustomButton;

        /// <summary>
        /// PlayCustomByName 按钮，调用 PlayCustom(name) 按名称查表。
        /// </summary>

        [SerializeField] private Button m_PlayCustomByNameButton;

        /// <summary>
        /// PlayEmphasis 按钮，调用 PlayEmphasis(amplitude, frequency, preDuration, interval)。
        /// </summary>

        [SerializeField] private Button m_PlayEmphasisButton;

        /// <summary>
        /// PlayEmphasisByName 按钮，调用 PlayEmphasis(name) 按名称查表。
        /// </summary>

        [SerializeField] private Button m_PlayEmphasisByNameButton;

        /// <summary>
        /// StopAll 按钮，调用 StopAll()。
        /// </summary>

        [SerializeField] private Button m_StopAllButton;

        /// <summary>
        /// IsSupported 状态文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_IsSupportedText;

        /// <summary>
        /// PlayCustom 演示用强度参数。
        /// </summary>
        private const float c_CustomIntensity = 0.5f;

        /// <summary>
        /// PlayCustom 演示用锐度参数。
        /// </summary>
        private const float c_CustomSharpness = 0.5f;

        /// <summary>
        /// PlayCustom 演示用前置延迟参数（秒）。
        /// </summary>
        private const float c_CustomPreDuration = 0f;

        /// <summary>
        /// PlayCustom 演示用持续时间参数（秒）。
        /// </summary>
        private const float c_CustomDuration = 0.3f;

        /// <summary>
        /// PlayEmphasis 演示用幅度参数。
        /// </summary>
        private const float c_EmphasisAmplitude = 0.8f;

        /// <summary>
        /// PlayEmphasis 演示用频率参数。
        /// </summary>
        private const float c_EmphasisFrequency = 0.5f;

        /// <summary>
        /// PlayEmphasis 演示用前置延迟参数（秒）。
        /// </summary>
        private const float c_EmphasisPreDuration = 0f;

        /// <summary>
        /// PlayEmphasis 演示用间隔参数（秒）。
        /// </summary>
        private const float c_EmphasisInterval = 0.1f;

        /// <summary>
        /// PlayCustom(name) 演示固定 name（来自 Demo_VibrateCustom 数据表首行）。
        /// </summary>
        private const string c_CustomDemoName = "Demo_Light";

        /// <summary>
        /// PlayEmphasis(name) 演示固定 name（来自 Demo_VibrateEmphasis 数据表首行）。
        /// </summary>
        private const string c_EmphasisDemoName = "Demo_Tap";

        /// <summary>
        /// 视图初始化：注册按钮事件，设置标题，填充下拉选项。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Vibrate 演示");

            BuildTypeDropdown();

            if (m_PlayButton != null)
            {
                m_PlayButton.onClick.AddListener(OnPlayButtonClick);
                SetButtonApiHint(m_PlayButton, "Nova.Vibrate.Play(VibrateType)");
            }

            if (m_PlayCustomButton != null)
            {
                m_PlayCustomButton.onClick.AddListener(OnPlayCustomButtonClick);
                SetButtonApiHint(m_PlayCustomButton, "Nova.Vibrate.PlayCustom(intensity, sharpness, pre, dur)");
            }

            if (m_PlayCustomByNameButton != null)
            {
                m_PlayCustomByNameButton.onClick.AddListener(OnPlayCustomByNameButtonClick);
                SetButtonApiHint(m_PlayCustomByNameButton, "Nova.Vibrate.PlayCustom(name)");
            }

            if (m_PlayEmphasisButton != null)
            {
                m_PlayEmphasisButton.onClick.AddListener(OnPlayEmphasisButtonClick);
                SetButtonApiHint(m_PlayEmphasisButton, "Nova.Vibrate.PlayEmphasis(amp, freq, pre, interval)");
            }

            if (m_PlayEmphasisByNameButton != null)
            {
                m_PlayEmphasisByNameButton.onClick.AddListener(OnPlayEmphasisByNameButtonClick);
                SetButtonApiHint(m_PlayEmphasisByNameButton, "Nova.Vibrate.PlayEmphasis(name)");
            }

            if (m_StopAllButton != null)
            {
                m_StopAllButton.onClick.AddListener(OnStopAllButtonClick);
                SetButtonApiHint(m_StopAllButton, "Nova.Vibrate.StopAll()");
            }
        }

        /// <summary>
        /// 视图打开：刷新 IsSupported 状态卡片。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            RefreshIsSupportedText();
        }

        /// <summary>
        /// Play 按钮点击：根据下拉选中的 VibrateType 调用 Play。
        /// </summary>
        private void OnPlayButtonClick()
        {
            if (Nova.Vibrate == null)
            {
                AppendFeedback("Nova.Vibrate 不可用", FeedbackLevel.Error);
                return;
            }

            VibrateType type = GetSelectedVibrateType();
            Nova.Vibrate.Play(type);
            bool supported = Nova.Vibrate.IsSupported;
            AppendFeedback($"Nova.Vibrate.Play({type}) -> ok / supported={supported}", supported ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// PlayCustom 按钮点击：调用 PlayCustom(intensity, sharpness, preDuration, duration) 固定参数。
        /// </summary>
        private void OnPlayCustomButtonClick()
        {
            if (Nova.Vibrate == null)
            {
                AppendFeedback("Nova.Vibrate 不可用", FeedbackLevel.Error);
                return;
            }

            Nova.Vibrate.PlayCustom(c_CustomIntensity, c_CustomSharpness, c_CustomPreDuration, c_CustomDuration);
            bool supported = Nova.Vibrate.IsSupported;
            AppendFeedback($"Nova.Vibrate.PlayCustom(intensity={c_CustomIntensity}, sharpness={c_CustomSharpness}, pre={c_CustomPreDuration}, dur={c_CustomDuration}) -> ok / supported={supported}", supported ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// PlayCustom(name) 按钮点击：按固定 name 查 VibrateCustom 数据表并播放。
        /// </summary>
        private void OnPlayCustomByNameButtonClick()
        {
            if (Nova.Vibrate == null)
            {
                AppendFeedback("Nova.Vibrate 不可用", FeedbackLevel.Error);
                return;
            }

            Nova.Vibrate.PlayCustom(c_CustomDemoName);
            bool supported = Nova.Vibrate.IsSupported;
            AppendFeedback($"Nova.Vibrate.PlayCustom(\"{c_CustomDemoName}\") -> ok / supported={supported}", supported ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// PlayEmphasis 按钮点击：调用 PlayEmphasis(amplitude, frequency, preDuration, interval) 固定参数。
        /// </summary>
        private void OnPlayEmphasisButtonClick()
        {
            if (Nova.Vibrate == null)
            {
                AppendFeedback("Nova.Vibrate 不可用", FeedbackLevel.Error);
                return;
            }

            Nova.Vibrate.PlayEmphasis(c_EmphasisAmplitude, c_EmphasisFrequency, c_EmphasisPreDuration, c_EmphasisInterval);
            bool supported = Nova.Vibrate.IsSupported;
            AppendFeedback($"Nova.Vibrate.PlayEmphasis(amp={c_EmphasisAmplitude}, freq={c_EmphasisFrequency}, pre={c_EmphasisPreDuration}, interval={c_EmphasisInterval}) -> ok / supported={supported}", supported ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// PlayEmphasis(name) 按钮点击：按固定 name 查 VibrateEmphasis 数据表并播放。
        /// </summary>
        private void OnPlayEmphasisByNameButtonClick()
        {
            if (Nova.Vibrate == null)
            {
                AppendFeedback("Nova.Vibrate 不可用", FeedbackLevel.Error);
                return;
            }

            Nova.Vibrate.PlayEmphasis(c_EmphasisDemoName);
            bool supported = Nova.Vibrate.IsSupported;
            AppendFeedback($"Nova.Vibrate.PlayEmphasis(\"{c_EmphasisDemoName}\") -> ok / supported={supported}", supported ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// StopAll 按钮点击：停止全部振动。
        /// </summary>
        private void OnStopAllButtonClick()
        {
            if (Nova.Vibrate == null)
            {
                AppendFeedback("Nova.Vibrate 不可用", FeedbackLevel.Error);
                return;
            }

            Nova.Vibrate.StopAll();
            AppendFeedback("Nova.Vibrate.StopAll() -> ok", FeedbackLevel.Success);
        }

        /// <summary>
        /// 向 Dropdown 填充 VibrateType 枚举值（跳过 None）。
        /// </summary>
        private void BuildTypeDropdown()
        {
            if (m_TypeDropdown == null)
            {
                return;
            }

            m_TypeDropdown.ClearOptions();
            System.Array values = System.Enum.GetValues(typeof(VibrateType));
            System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>();
            for (int i = 0; i < values.Length; i++)
            {
                VibrateType t = (VibrateType)values.GetValue(i);
                if (t == VibrateType.None)
                {
                    continue;
                }

                options.Add(t.ToString());
            }

            m_TypeDropdown.AddOptions(options);
        }

        /// <summary>
        /// 从 Dropdown 当前选项解析出对应的 VibrateType 枚举值。
        /// </summary>
        /// <returns>选中的 VibrateType，解析失败时返回 LightImpact。</returns>
        private VibrateType GetSelectedVibrateType()
        {
            if (m_TypeDropdown == null)
            {
                return VibrateType.LightImpact;
            }

            string selectedText = m_TypeDropdown.options.Count > m_TypeDropdown.value ? m_TypeDropdown.options[m_TypeDropdown.value].text : null;
            if (!string.IsNullOrEmpty(selectedText) && System.Enum.TryParse(selectedText, out VibrateType result))
            {
                return result;
            }

            return VibrateType.LightImpact;
        }

        /// <summary>
        /// 刷新振动硬件支持状态文本。
        /// </summary>
        private void RefreshIsSupportedText()
        {
            if (m_IsSupportedText == null || Nova.Vibrate == null)
            {
                return;
            }

            bool supported = Nova.Vibrate.IsSupported;
            m_IsSupportedText.text = $"IsSupported：{supported}";
            m_IsSupportedText.color = supported ? new Color32(0x4C, 0xAF, 0x50, 0xFF) : new Color32(0xFF, 0xB3, 0x00, 0xFF);
        }
    }
}
