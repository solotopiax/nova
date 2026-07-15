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

using System.Globalization;
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
        /// PlayCustom 强度输入，范围为 0-1。
        /// </summary>
        [SerializeField] private TMP_InputField m_CustomIntensityInput;

        /// <summary>
        /// PlayCustom 锐度输入，范围为 0-1。
        /// </summary>
        [SerializeField] private TMP_InputField m_CustomSharpnessInput;

        /// <summary>
        /// PlayCustom 前置延迟输入（秒）。
        /// </summary>
        [SerializeField] private TMP_InputField m_CustomPreDurationInput;

        /// <summary>
        /// PlayCustom 持续时间输入（秒）。
        /// </summary>
        [SerializeField] private TMP_InputField m_CustomDurationInput;

        /// <summary>
        /// PlayCustom 按钮，调用 PlayCustom(intensity, sharpness, preDuration, duration)。
        /// </summary>

        [SerializeField] private Button m_PlayCustomButton;

        /// <summary>
        /// PlayCustomByName 按钮，调用 PlayCustom(name) 按名称查表。
        /// </summary>

        [SerializeField] private Button m_PlayCustomByNameButton;

        /// <summary>
        /// PlayEmphasis 幅度输入，范围为 0-1。
        /// </summary>
        [SerializeField] private TMP_InputField m_EmphasisAmplitudeInput;

        /// <summary>
        /// PlayEmphasis 频率输入，范围为 0-1。
        /// </summary>
        [SerializeField] private TMP_InputField m_EmphasisFrequencyInput;

        /// <summary>
        /// PlayEmphasis 前置延迟输入（秒）。
        /// </summary>
        [SerializeField] private TMP_InputField m_EmphasisPreDurationInput;

        /// <summary>
        /// PlayEmphasis 间隔输入（秒）。
        /// </summary>
        [SerializeField] private TMP_InputField m_EmphasisIntervalInput;

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
        /// PlayCustom 按钮点击：读取配置并调用 PlayCustom(intensity, sharpness, preDuration, duration)。
        /// </summary>
        private void OnPlayCustomButtonClick()
        {
            if (Nova.Vibrate == null)
            {
                AppendFeedback("Nova.Vibrate 不可用", FeedbackLevel.Error);
                return;
            }

            if (!TryReadParameter(m_CustomIntensityInput, "intensity", true, out float intensity) ||
                !TryReadParameter(m_CustomSharpnessInput, "sharpness", true, out float sharpness) ||
                !TryReadParameter(m_CustomPreDurationInput, "preDuration", false, out float preDuration) ||
                !TryReadParameter(m_CustomDurationInput, "duration", false, out float duration))
            {
                return;
            }

            Nova.Vibrate.PlayCustom(intensity, sharpness, preDuration, duration);
            bool supported = Nova.Vibrate.IsSupported;
            AppendFeedback($"Nova.Vibrate.PlayCustom(intensity={intensity}, sharpness={sharpness}, pre={preDuration}, dur={duration}) -> ok / supported={supported}", supported ? FeedbackLevel.Success : FeedbackLevel.Warn);
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
        /// PlayEmphasis 按钮点击：读取配置并调用 PlayEmphasis(amplitude, frequency, preDuration, interval)。
        /// </summary>
        private void OnPlayEmphasisButtonClick()
        {
            if (Nova.Vibrate == null)
            {
                AppendFeedback("Nova.Vibrate 不可用", FeedbackLevel.Error);
                return;
            }

            if (!TryReadParameter(m_EmphasisAmplitudeInput, "amplitude", true, out float amplitude) ||
                !TryReadParameter(m_EmphasisFrequencyInput, "frequency", true, out float frequency) ||
                !TryReadParameter(m_EmphasisPreDurationInput, "preDuration", false, out float preDuration) ||
                !TryReadParameter(m_EmphasisIntervalInput, "interval", false, out float interval))
            {
                return;
            }

            Nova.Vibrate.PlayEmphasis(amplitude, frequency, preDuration, interval);
            bool supported = Nova.Vibrate.IsSupported;
            AppendFeedback($"Nova.Vibrate.PlayEmphasis(amp={amplitude}, freq={frequency}, pre={preDuration}, interval={interval}) -> ok / supported={supported}", supported ? FeedbackLevel.Success : FeedbackLevel.Warn);
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
        /// 读取单个振动参数；归一化参数会限制到 0-1 并回写输入框。
        /// </summary>
        private bool TryReadParameter(TMP_InputField input, string parameterName, bool normalized, out float value)
        {
            string raw = input != null ? input.text : null;
            if (!TryParseParameter(raw, normalized, out value))
            {
                AppendFeedback($"{parameterName} 输入 \"{raw}\" 不是有效浮点数", FeedbackLevel.Error);
                return false;
            }

            if (normalized)
            {
                input.SetTextWithoutNotify(value.ToString("G9", CultureInfo.InvariantCulture));
            }

            return true;
        }

        /// <summary>
        /// 按固定小数点格式解析振动参数，并按需限制到 0-1。
        /// </summary>
        private static bool TryParseParameter(string raw, bool normalized, out float value)
        {
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                float.IsNaN(value) ||
                float.IsInfinity(value))
            {
                value = 0f;
                return false;
            }

            if (normalized)
            {
                value = Mathf.Clamp01(value);
            }

            return true;
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
