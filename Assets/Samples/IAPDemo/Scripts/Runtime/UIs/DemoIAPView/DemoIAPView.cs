/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPView.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   根据已安装可选支付包动态展示商店 Tab 的 IAP 演示 View
 ***************************************************************/

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// 在 BaseDemoView 三段式骨架内编排登录和当前工程可用的支付商店 Panel。
    /// </summary>
    public sealed partial class DemoIAPView : BaseDemoView
    {
        /// <summary>
        /// 初始化登录、通用 Panel 壳、可选商店模块与 IAP Core 桥接层。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            SetTitle("IAP 统一支付演示");
            EnsureIAPBridge();
            InitializeStoreModules();
            BindStaticButtons();
            ShowFirstAvailableTab();
            SetAuthenticatedContentActive(false);
        }

        /// <summary>
        /// 打开 View 时恢复模块发现结果、登录状态并连接基础 IAP 插件。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            EnsureIAPBridge();
            InitializeStoreModules();
            SetAuthenticatedContentActive(m_LoggedIn);
            ShowFirstAvailableTab();
            bool initialized = m_IapBridge != null && m_IapBridge.TryInitialize();
            AppendFeedback(initialized
                ? "IAP 插件已连接，请先登录查看当前已安装的支付商店。"
                : "IAP 插件暂不可用。", initialized ? FeedbackLevel.Info : FeedbackLevel.Warn);
        }

        /// <summary>
        /// 关闭 View 时取消异步任务并清理全部已发现商店模块的运行时内容。
        /// </summary>
        /// <param name="isShutdown">是否因视图系统关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            ClearStoreModules();
            m_IapBridge?.Dispose();
            m_IapBridge = null;
            m_LoggedIn = false;
            m_LoginInProgress = false;
            SetAuthenticatedContentActive(false);
            if (m_LoginButton != null)
            {
                m_LoginButton.interactable = true;
                SetButtonLabel(m_LoginButton, "登录");
            }

            base.OnClose(isShutdown, userData);
        }
    }
}
