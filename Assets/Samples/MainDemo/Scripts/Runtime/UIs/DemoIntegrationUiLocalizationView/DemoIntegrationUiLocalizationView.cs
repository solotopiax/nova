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
using System;
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
        /// 当前语言操作版本；关闭、重开或发起新请求后，旧回调不能再刷新此页面。
        /// </summary>
        private int m_LanguageSwitchVersion;

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

            m_LanguageSwitchVersion++;
            RefreshLanguageDropdown();
        }

        /// <summary>
        /// 关闭页面时使本页旧回调失效；不取消全局语言切换。
        /// </summary>
        /// <param name="isShutdown">是否由 UI 管理器关闭触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_LanguageSwitchVersion++;
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 刷新下拉选项列表，读取 Nova.Localization.GetSupportedLanguages。
        /// </summary>
        private void RefreshLanguageDropdown()
        {
            if (m_LanguageDropdown == null || Nova.Localization == null)
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
            int currentIndex = options.IndexOf(currentLang.ToString());
            if (currentIndex >= 0)
            {
                m_LanguageDropdown.SetValueWithoutNotify(currentIndex);
            }

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
        /// 异步切换语言；仅当前页面的最新请求核对实际语言后刷新显示，失败不报成功。
        /// </summary>
        /// <returns>按钮触发的语言切换任务。</returns>
        private async UniTaskVoid SwitchLanguageAsync()
        {
            if (!Available || Nova.Localization == null)
            {
                return;
            }

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
            int requestVersion = ++m_LanguageSwitchVersion;
            AppendFeedback(string.Format("Nova.Localization.SetLanguageAsync({0}) -> ...", target), FeedbackLevel.Info);

            try
            {
                await Nova.Localization.SetLanguageAsync(target);
            }
            catch (Exception exception)
            {
                if (this != null && Available && requestVersion == m_LanguageSwitchVersion)
                {
                    AppendFeedback("切换语言失败：" + exception.Message, FeedbackLevel.Error);
                }
                return;
            }

            if (this == null || !Available || requestVersion != m_LanguageSwitchVersion || Nova.Localization == null)
            {
                return;
            }

            Language actual = Nova.Localization.Language;
            if (m_CurrentLangText != null)
            {
                m_CurrentLangText.text = string.Format("Current: {0}", actual);
            }

            if (actual != target)
            {
                AppendFeedback(string.Format("未切换到 {0}，当前语言为 {1}。", target, actual), FeedbackLevel.Warn);
                return;
            }

            AppendFeedback("语言已切换为 " + actual, FeedbackLevel.Success);
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
