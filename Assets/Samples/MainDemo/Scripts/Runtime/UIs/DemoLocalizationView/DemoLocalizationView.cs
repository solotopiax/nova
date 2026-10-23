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
        /// 本次视图打开周期的取消令牌源，仅控制语言切换后的视图刷新，不取消全局语言切换。
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
        /// 视图打开钩子，重建本次打开周期的取消令牌源，填充语言下拉框并刷新 Demo key 展示区。
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
        /// 视图关闭钩子，取消旧打开周期请求的后续视图刷新；全局语言切换仍由本地化 Manager 完成。
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
        /// 异步切换全局语言，仅在请求所属的视图打开周期仍有效时刷新 Demo key 展示区并写入反馈。
        /// 关闭视图只抑制此方法后续的视图刷新，不取消或回滚本地化 Manager 的全局语言切换。
        /// </summary>
        /// <returns>按钮触发的异步操作。</returns>
        private async UniTaskVoid SwitchLanguageAsync()
        {
            CancellationTokenSource viewCts = m_Cts;
            if (viewCts == null)
            {
                return;
            }

            CancellationToken viewToken = viewCts.Token;
            if (viewToken.IsCancellationRequested)
            {
                return;
            }

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
            // 公开 API 不接收视图 Token；固定旧周期归属，避免关闭并重开后旧请求刷新新视图。
            await Nova.Localization.SetLanguageAsync(target);

            if (viewToken.IsCancellationRequested || !ReferenceEquals(m_Cts, viewCts))
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
        /// 刷新 Demo 文本展示区：已绑定的文本由 TextLocalizing 刷新，未绑定的文本走 GetText 回退。
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
        /// 未绑定 TextLocalizing.KeyName 时手动设置译文；非空绑定由组件刷新，不覆盖其配置或译文。
        /// 手动回退的 key 不存在时展示 key 本身加提示，不以预期 key 掩盖错误的非空组件绑定。
        /// </summary>
        /// <param name="label">目标文本组件。</param>
        /// <param name="key">本地化 key 名称。</param>
        private void SetTextIfNotNull(TextMeshProUGUI label, string key)
        {
            if (label == null)
            {
                return;
            }

            TextLocalizing localizing = label.GetComponent<TextLocalizing>();
            if (localizing != null && !string.IsNullOrEmpty(localizing.KeyName))
            {
                return;
            }

            string value = Nova.Localization.GetText(key);
            label.text = string.IsNullOrEmpty(value) ? key + " (缺失)" : value;
        }
    }
}
