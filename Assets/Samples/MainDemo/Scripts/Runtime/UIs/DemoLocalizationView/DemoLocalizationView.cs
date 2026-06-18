/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoLocalizationView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.7 — Localization 切换语言与取词演示 View（交互型）
 *            职责：演示 Nova.Localization.SetLanguageAsync / GetSupportedLanguages / GetText，
 *            展示当前支持的语言列表，并用 5 个 Demo key 展示切语言后的即时刷新。
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Modules 2.7 Localization 演示 View（交互型）。
    /// 演示 Nova.Localization.GetSupportedLanguages / SetLanguageAsync / GetText 三段操作，
    /// 提供语言切换下拉框和 Demo 本地化 key 展示区。
    /// </summary>
    public sealed class DemoLocalizationView : BaseDemoView
    {
        /// <summary>
        /// 语言切换下拉框，选项由 GetSupportedLanguages 动态填充。
        /// </summary>

        [SerializeField] private TMP_Dropdown m_LanguageDropdown;

        /// <summary>
        /// 切换语言按钮，触发 Nova.Localization.SetLanguageAsync。
        /// </summary>

        [SerializeField] private Button m_SwitchButton;

        /// <summary>
        /// Demo_Greet 本地化 key 文本展示。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_GreetText;

        /// <summary>
        /// Demo_Confirm 本地化 key 文本展示。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_ConfirmText;

        /// <summary>
        /// Demo_Cancel 本地化 key 文本展示。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_CancelText;

        /// <summary>
        /// Demo_Toast_Tip 本地化 key 文本展示。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_ToastTipText;

        /// <summary>
        /// Demo_Switch_Lang 本地化 key 文本展示。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_SwitchLangText;

        /// <summary>
        /// 当前支持的语言列表，由 GetSupportedLanguages 填充，与下拉框索引对应。
        /// </summary>
        private readonly List<Language> m_SupportedLanguages = new List<Language>();

        /// <summary>
        /// 当前语言切换任务的取消令牌源，View 关闭时取消。
        /// </summary>
        private CancellationTokenSource m_Cts;

        /// <summary>
        /// 视图初始化钩子，注册按钮事件，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Localization");

            if (m_SwitchButton != null)
            {
                m_SwitchButton.onClick.AddListener(OnSwitchButtonClick);
                SetButtonApiHint(m_SwitchButton, "Nova.Localization.SetLanguageAsync(lang)");
            }
        }

        /// <summary>
        /// 视图打开钩子，填充语言下拉框并刷新 Demo key 展示区。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = new CancellationTokenSource();

            SetFieldApiHint(m_GreetText, "Nova.Localization.GetText(name)");
            RefreshLanguageDropdown();
            RefreshDemoTexts();
        }

        /// <summary>
        /// 视图关闭钩子，取消进行中的语言切换任务。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = null;
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 切换语言按钮点击回调，异步切换到下拉框选中的语言并刷新展示区。
        /// </summary>
        private void OnSwitchButtonClick()
        {
            SwitchLanguageAsync().Forget();
        }

        /// <summary>
        /// 异步切换语言，成功后刷新 Demo key 展示区并写入反馈。
        /// </summary>
        private async UniTaskVoid SwitchLanguageAsync()
        {
            if (Nova.Localization == null)
            {
                AppendFeedback("Nova.Localization.SetLanguageAsync -> LocalizationComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            if (m_LanguageDropdown == null || m_SupportedLanguages.Count == 0)
            {
                AppendFeedback("Nova.Localization.SetLanguageAsync -> 无可用语言列表", FeedbackLevel.Warn);
                return;
            }

            int selectedIndex = m_LanguageDropdown.value;
            if (selectedIndex < 0 || selectedIndex >= m_SupportedLanguages.Count)
            {
                AppendFeedback("Nova.Localization.SetLanguageAsync -> 下拉框索引越界", FeedbackLevel.Warn);
                return;
            }

            Language target = m_SupportedLanguages[selectedIndex];
            await Nova.Localization.SetLanguageAsync(target);

            if (m_Cts == null || m_Cts.IsCancellationRequested)
            {
                return;
            }

            RefreshDemoTexts();
            AppendFeedback("Nova.Localization.SetLanguageAsync(" + target + ") -> done", FeedbackLevel.Success);
        }

        /// <summary>
        /// 从 GetSupportedLanguages 填充语言下拉框，并将当前语言设为默认选项。
        /// </summary>
        private void RefreshLanguageDropdown()
        {
            m_SupportedLanguages.Clear();

            if (m_LanguageDropdown == null || Nova.Localization == null)
            {
                return;
            }

            IReadOnlyList<Language> langs = Nova.Localization.GetSupportedLanguages();
            m_LanguageDropdown.ClearOptions();

            Language current = Nova.Localization.Language;
            int defaultIndex = 0;

            for (int i = 0; i < langs.Count; i++)
            {
                m_SupportedLanguages.Add(langs[i]);
                m_LanguageDropdown.options.Add(new TMP_Dropdown.OptionData(langs[i].ToString()));

                if (langs[i] == current)
                {
                    defaultIndex = i;
                }
            }

            m_LanguageDropdown.value = defaultIndex;
            m_LanguageDropdown.RefreshShownValue();
        }

        /// <summary>
        /// 调用 Nova.Localization.GetText 刷新所有 Demo 本地化 key 展示文本。
        /// </summary>
        private void RefreshDemoTexts()
        {
            if (Nova.Localization == null)
            {
                return;
            }

            SetTextIfNotNull(m_GreetText, "Demo_Greet");
            SetTextIfNotNull(m_ConfirmText, "Demo_Confirm");
            SetTextIfNotNull(m_CancelText, "Demo_Cancel");
            SetTextIfNotNull(m_ToastTipText, "Demo_Toast_Tip");
            SetTextIfNotNull(m_SwitchLangText, "Demo_Switch_Lang");
        }

        /// <summary>
        /// 向指定文本组件设置本地化 key 对应的文本值；key 不存在时展示 key 本身加提示。
        /// </summary>
        /// <param name="label">目标文本组件。</param>
        /// <param name="key">本地化 key 名称。</param>
        private void SetTextIfNotNull(TextMeshProUGUI label, string key)
        {
            if (label == null)
            {
                return;
            }

            string value = Nova.Localization.GetText(key);
            label.text = string.IsNullOrEmpty(value) ? key + " (缺失)" : value;
        }
    }
}
