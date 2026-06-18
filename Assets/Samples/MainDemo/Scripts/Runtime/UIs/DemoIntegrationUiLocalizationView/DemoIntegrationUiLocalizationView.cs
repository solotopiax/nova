/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIntegrationUiLocalizationView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Integration 4.1 — UI + Localization 跨模块联动
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Integration Demo 4.1：切语言 -> UI 内文本自动刷新。
    /// API 副标题：Nova.Localization.SetLanguageAsync + Nova.UI.OpenUIViewAsync《T》。
    /// 交互触发型：Language 下拉 + 切换按钮 + 打开 Toast 子 View 演示文本同步。
    /// </summary>
    public sealed class DemoIntegrationUiLocalizationView : BaseDemoView
    {
        /// <summary>
        /// 语言切换下拉组件（TMP_Dropdown）。
        /// </summary>

        [SerializeField] private TMP_Dropdown m_LanguageDropdown;

        /// <summary>
        /// 切换语言按钮。
        /// </summary>

        [SerializeField] private Button m_SwitchLangButton;

        /// <summary>
        /// 打开 Toast 子 View 演示文本同步按钮。
        /// </summary>

        [SerializeField] private Button m_OpenToastButton;

        /// <summary>
        /// 当前语言展示文本组件。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_CurrentLangText;

        /// <summary>
        /// 支持的语言列表缓存，用于下拉映射。
        /// </summary>
        private IReadOnlyList<Language> m_SupportedLanguages;

        /// <summary>
        /// 视图初始化钩子，设置标题、API 副标题并绑定按钮事件。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("UI + Localization");

            if (m_SwitchLangButton != null)
            {
                m_SwitchLangButton.onClick.AddListener(OnSwitchLangButtonClick);
                SetButtonApiHint(m_SwitchLangButton, "Nova.Localization.SetLanguageAsync(lang)");
            }

            if (m_OpenToastButton != null)
            {
                m_OpenToastButton.onClick.AddListener(OnOpenToastButtonClick);
                SetButtonApiHint(m_OpenToastButton, "Nova.UI.OpenUIViewAsync<DemoToastView>()");
            }
        }

        /// <summary>
        /// 视图打开钩子，刷新语言下拉选项与当前语言展示。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            RefreshLanguageDropdown();
        }

        /// <summary>
        /// 刷新下拉选项列表，读取 Nova.Localization.GetSupportedLanguages。
        /// </summary>
        private void RefreshLanguageDropdown()
        {
            if (m_LanguageDropdown == null)
            {
                return;
            }

            m_SupportedLanguages = Nova.Localization.GetSupportedLanguages();
            m_LanguageDropdown.ClearOptions();

            List<string> options = new List<string>();
            for (int i = 0; i < m_SupportedLanguages.Count; i++)
            {
                options.Add(m_SupportedLanguages[i].ToString());
            }

            m_LanguageDropdown.AddOptions(options);

            Language currentLang = Nova.Localization.Language;
            if (m_CurrentLangText != null)
            {
                m_CurrentLangText.text = string.Format("Current: {0}", currentLang);
            }
        }

        /// <summary>
        /// 切换语言按钮点击回调，读取下拉选中项调用 Nova.Localization.SetLanguageAsync。
        /// </summary>
        private void OnSwitchLangButtonClick()
        {
            SwitchLanguageAsync().Forget();
        }

        /// <summary>
        /// 异步切换语言，切换完成后刷新当前语言展示并写反馈日志。
        /// </summary>
        private async UniTaskVoid SwitchLanguageAsync()
        {
            if (m_LanguageDropdown == null || m_SupportedLanguages == null || m_SupportedLanguages.Count == 0)
            {
                AppendFeedback("Nova.Localization.GetSupportedLanguages() -> 0 languages, skip", FeedbackLevel.Warn);
                return;
            }

            int idx = m_LanguageDropdown.value;
            if (idx < 0 || idx >= m_SupportedLanguages.Count)
            {
                AppendFeedback("Nova.Localization.SetLanguageAsync() -> invalid index", FeedbackLevel.Error);
                return;
            }

            Language target = m_SupportedLanguages[idx];
            AppendFeedback(string.Format("Nova.Localization.SetLanguageAsync({0}) -> ...", target), FeedbackLevel.Info);

            await Nova.Localization.SetLanguageAsync(target);

            if (this == null)
            {
                return;
            }

            if (m_CurrentLangText != null)
            {
                m_CurrentLangText.text = string.Format("Current: {0}", target);
            }

            AppendFeedback(string.Format("Nova.Localization.SetLanguageAsync({0}) -> done", target), FeedbackLevel.Success);
        }

        /// <summary>
        /// 打开 Toast 子 View 按钮点击回调，通过 Nova.UI.OpenUIViewAsync 展示本地化文本同步效果。
        /// </summary>
        private void OnOpenToastButtonClick()
        {
            OpenToast();
        }

        /// <summary>
        /// 打开 DemoToastView，toast 文本使用 Nova.Localization.GetText 取词，展示 UI + Localization 联动。
        /// </summary>
        private void OpenToast()
        {
            string toastText = Nova.Localization.GetText("Demo_Toast_Tip");
            AppendFeedback(string.Format("Nova.Localization.GetText(\"Demo_Toast_Tip\") -> \"{0}\"", toastText), FeedbackLevel.Info);

            int serialID = Nova.UI.OpenUIViewAsync<DemoToastView>(toastText);
            AppendFeedback(string.Format("Nova.UI.OpenUIViewAsync<DemoToastView>() -> SerialID={0} / text refreshed", serialID), FeedbackLevel.Success);
        }
    }
}
