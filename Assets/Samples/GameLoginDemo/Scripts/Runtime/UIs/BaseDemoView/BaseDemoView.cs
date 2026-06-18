/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BaseDemoView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 基础 View 基类 — 生命周期与公开接口
 *            职责：为所有 DemoXxxView 提供三段式骨架（TitleBar 64px / InteractionArea flex /
 *            FeedbackArea 200px），统一反馈日志、关闭行为与 API 副标题展示。
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameLogin.Samples.Runtime
{
    /// <summary>
    /// Demo 基础 View 基类，三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// 子类继承后重写 OnInit/OnOpen/OnClose，禁止覆盖 Unity 生命周期方法（Awake/Start 由基类 UIView 托管）。
    /// </summary>
    public partial class BaseDemoView : UIView
    {
        /// <summary>
        /// 视图初始化钩子，仅在首次创建实例时触发。
        /// 基类注册关闭按钮与清空反馈按钮事件；子类重写时须调用 base.OnInit(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            if (m_CloseButton != null)
            {
                m_CloseButton.onClick.AddListener(OnCloseButtonClick);
            }

            if (m_ClearFeedbackButton != null)
            {
                m_ClearFeedbackButton.onClick.AddListener(OnClearFeedbackButtonClick);
            }

            if (m_FeedbackLineTemplate != null)
            {
                m_FeedbackLineTemplate.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 视图打开钩子，每次 OpenUIViewAsync 调用时触发。
        /// 子类重写时须调用 base.OnOpen(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
        }

        /// <summary>
        /// 视图关闭钩子，关闭时清空反馈区。
        /// 子类重写时须调用 base.OnClose(isShutdown, userData)。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            ClearFeedback();
            base.OnClose(isShutdown, userData);
        }
    }
}
