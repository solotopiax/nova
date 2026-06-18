/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPView.cs
 * author:    nova-create-sample
 * created:   2026/06/05
 * descrip:   DemoIAPView 演示 View — 生命周期与公开接口
 ***************************************************************/

using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// DemoIAPView 演示 View，派生自 BaseDemoView，遵循三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// 交互区初始仅有一个登录按钮，点击登录后置灰并展开 4 个支付按钮（2 普通 + 2 订阅）。
    /// </summary>
    public sealed partial class DemoIAPView : BaseDemoView
    {
        /// <summary>
        /// 视图初始化钩子，仅在首次创建实例时触发。
        /// 把示例按钮复用为登录按钮：设置文本为「登录」并注册点击回调。
        /// 子类重写须调用 base.OnInit(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("IAP 演示");
            EnsureIAPBridge();

            if (m_SampleButton != null)
            {
                SetButtonLabel(m_SampleButton, "登录");
                m_SampleButton.onClick.AddListener(OnLoginClick);
                SetButtonApiHint(m_SampleButton, string.Empty);
            }
        }

        /// <summary>
        /// 确保 IAP 胶水层可用；缓存 View 重新打开时会按需重建。
        /// </summary>
        private void EnsureIAPBridge()
        {
            if (m_IapBridge == null)
            {
                m_IapBridge = new DemoIAPBridge(AppendFeedback, UpdatePayButtonText, SetPayButtonsInteractable);
            }
        }

        /// <summary>
        /// 视图打开钩子，每次 OpenUIViewAsync 调用时触发。
        /// 子类重写须调用 base.OnOpen(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            EnsureIAPBridge();

            if (m_IapBridge != null && m_IapBridge.TryInitialize())
            {
                AppendFeedback("IAP 演示已打开，点击「登录」按钮展开支付列表。");
            }
            else
            {
                AppendFeedback("IAP 演示已打开，但 IAP 插件暂不可用。", FeedbackLevel.Warn);
            }
        }

        /// <summary>
        /// 视图关闭钩子，关闭时由基类清空反馈区。
        /// 子类重写须调用 base.OnClose(isShutdown, userData)。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            if (m_IapBridge != null)
            {
                m_IapBridge.Dispose();
                m_IapBridge = null;
            }

            ClearPayButtons();
            m_LoggedIn = false;
            m_LoginInProgress = false;

            if (m_SampleButton != null)
            {
                m_SampleButton.interactable = true;

                Image image = m_SampleButton.GetComponent<Image>();
                if (image != null)
                {
                    image.color = Color.white;
                }
            }

            base.OnClose(isShutdown, userData);
        }
    }
}
