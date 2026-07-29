/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoConfigView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.4 — Config 运行态快照与应用配置拉取演示 View
 *            职责：展示 Nova.Config 的 DevelopMode/Platform/Channel/Namespace 四张卡片，
 *            以及 Common 字段（AppID/AppAesKey）和 EnabledSDKs 列表。
 ***************************************************************/

using System.Text;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Modules 2.4 Config 演示 View。
    /// 打开时自动读取 Nova.Config 运行态快照并展示到 UI 卡片中，
    /// 提供本地快照刷新与远端 Custom 配置拉取入口。
    /// </summary>
    public sealed class DemoConfigView : BaseDemoView
    {
        /// <summary>
        /// DevelopMode 卡片文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_DevelopModeText;

        /// <summary>
        /// Platform 卡片文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_PlatformText;

        /// <summary>
        /// Channel 卡片文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_ChannelText;

        /// <summary>
        /// Namespace 卡片文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_NamespaceText;

        /// <summary>
        /// Common 字段区域文本（AppID / AppAesKey 等）。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_CommonText;

        /// <summary>
        /// EnabledSDKs 列表文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_SdkListText;

        /// <summary>
        /// 刷新按钮，重新读取并展示当前 Config 快照。
        /// </summary>

        [SerializeField] private Button m_RefreshButton;

        /// <summary>
        /// 应用配置手动拉取按钮。
        /// </summary>

        [SerializeField] private Button m_AppConfigButton;

        /// <summary>
        /// 视图已关闭标志，防止异步拉取完成后写入已关闭页面。
        /// </summary>
        private bool m_IsClosed;

        /// <summary>
        /// 视图初始化钩子，注册刷新按钮事件，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Config");

            if (m_RefreshButton != null)
            {
                m_RefreshButton.onClick.AddListener(OnRefreshButtonClick);
                SetButtonApiHint(m_RefreshButton, "Nova.Config.AppConfigs / .Namespace / GetSDKPluginConfig<T>()");
            }

            if (m_AppConfigButton != null)
            {
                m_AppConfigButton.onClick.AddListener(OnAppConfigButtonClick);
                SetButtonApiHint(m_AppConfigButton, "Nova.Config.RefreshAppConfigAsync()");
            }
        }

        /// <summary>
        /// 视图打开钩子，自动读取 Config 快照并刷新 UI。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            m_IsClosed = false;
            RefreshSnapshot();
        }

        /// <summary>
        /// 视图关闭时阻止未完成的远端拉取继续写入反馈区。
        /// </summary>
        /// <param name="isShutdown">是否因框架关闭而触发。</param>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_IsClosed = true;
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 刷新按钮点击回调，重新读取并展示当前 Config 快照。
        /// </summary>
        private void OnRefreshButtonClick()
        {
            ClearFeedback();
            RefreshSnapshot();
        }

        /// <summary>
        /// 应用配置按钮点击回调，显式拉取 GM 后台 Custom 配置。
        /// </summary>
        private void OnAppConfigButtonClick()
        {
            ExecuteAppConfigRefreshAsync().Forget();
        }

        /// <summary>
        /// 拉取应用配置并展示 Config 中已配置的 ThirdIAPOpen 与 ThirdIAPIOSOpenCountry。
        /// 失败时仍通过 Nova.Config.Custom 显示当前缓存或 ConfigRuntime 本地默认值。
        /// </summary>
        private async UniTaskVoid ExecuteAppConfigRefreshAsync()
        {
            if (Nova.Config == null)
            {
                AppendFeedback("Nova.Config 不可用", FeedbackLevel.Error);
                return;
            }

            ClearFeedback();
            AppendFeedback("Nova.Config.RefreshAppConfigAsync() -> 拉取中...", FeedbackLevel.Info);
            bool succeeded = await Nova.Config.RefreshAppConfigAsync();
            if (this == null || m_IsClosed)
            {
                return;
            }

            CustomConfig custom = Nova.Config.Custom;
            bool thirdIapOpen = custom?.GetBool("ThirdIAPOpen", false) ?? false;
            string iosCountries = custom?.GetString("ThirdIAPIOSOpenCountry", "[]") ?? "[]";
            AppendFeedback(
                succeeded ? "Nova.Config.RefreshAppConfigAsync() -> 成功" : "Nova.Config.RefreshAppConfigAsync() -> 失败，已保留当前值",
                succeeded ? FeedbackLevel.Success : FeedbackLevel.Error);
            AppendFeedback($"Nova.Config.Custom.GetBool(\"ThirdIAPOpen\") -> {thirdIapOpen}", FeedbackLevel.Info);
            AppendFeedback($"Nova.Config.Custom.GetString(\"ThirdIAPIOSOpenCountry\") -> {iosCountries}", FeedbackLevel.Info);
        }

        /// <summary>
        /// 读取 Nova.Config 各属性并更新所有卡片文本及反馈区。
        /// </summary>
        private void RefreshSnapshot()
        {
            if (Nova.Config == null)
            {
                AppendFeedback("Nova.Config -> ConfigComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            if (m_DevelopModeText != null)
            {
                m_DevelopModeText.text = "DevelopMode: " + Nova.Config.DevelopMode;
            }

            if (m_PlatformText != null)
            {
                m_PlatformText.text = "Platform: " + Nova.Config.Platform;
            }

            if (m_ChannelText != null)
            {
                m_ChannelText.text = "Channel: " + Nova.Config.Channel;
            }

            string ns = Nova.Config.Namespace ?? "(null)";
            if (m_NamespaceText != null)
            {
                m_NamespaceText.text = "Namespace: " + ns;
            }

            AppConfigs appConfigs = Nova.Config.AppConfigs;
            if (m_CommonText != null)
            {
                if (appConfigs != null)
                {
                    m_CommonText.text = "AppID: " + appConfigs.AppID + "\nAppAesKey: " + (string.IsNullOrEmpty(appConfigs.AppAesKey) ? "(空)" : "******");
                }
                else
                {
                    m_CommonText.text = "AppConfigs: (null)";
                }
            }

            int sdkCount = 0;
            StringBuilder sbSdk = new StringBuilder();
            foreach (ISDKPluginConfig cfg in Nova.Config.AllPluginConfigs)
            {
                sbSdk.AppendLine("- " + cfg.GetType().Name);
                sdkCount++;
            }

            if (m_SdkListText != null)
            {
                m_SdkListText.text = sdkCount > 0 ? sbSdk.ToString().TrimEnd() : "EnabledSDKs: (无)";
            }

            AppendFeedback("Nova.Config.ConfigManager.Namespace -> \"" + ns + "\"", FeedbackLevel.Success);
        }
    }
}
