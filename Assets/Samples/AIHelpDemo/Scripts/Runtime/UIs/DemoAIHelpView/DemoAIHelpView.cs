/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAIHelpView.cs
 * author:    nova-create-sample
 * created:   2026/07/09
 * descrip:   DemoAIHelpView 演示 View — 生命周期与按钮绑定。
 *            每个按钮单独暴露 AIHelpPlugin 一个常用接口，点击后就近打印反馈。
 *            Editor 下 vendor 为空操作（iOS/Android 才有真实 UI），按钮点击后以文案回显即可。
 ***************************************************************/

namespace NovaFramework.Sdk.Aihelp.Samples.Runtime
{
    /// <summary>
    /// DemoAIHelpView 演示 View，派生自 BaseDemoView，遵循三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// 交互区把 AIHelpPlugin 常用接口拆成独立按钮：查看版本与可用状态 / 拉起客服 / 同步登录用户 / 查询未读消息数（含事件回显）/ 关闭页面。
    /// </summary>
    public sealed partial class DemoAIHelpView : BaseDemoView
    {
        /// <summary>
        /// 视图初始化钩子，仅在首次创建实例时触发。
        /// 逐个绑定 AIHelpPlugin 接口按钮并设置就近 API 提示。
        /// 子类重写须调用 base.OnInit(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("AIHelp 演示");

            BindButton(m_InfoButton, OnInfoClick, "GetSDKVersion() / IsAvailable");
            BindButton(m_HelpCenterButton, OnHelpCenterClick, "Show(\"E001\")");
            BindButton(m_CustomerServiceButton, OnCustomerServiceClick, "Show(\"E002\", welcomeMessage)");
            BindButton(m_LoginButton, OnLoginClick, "Login(uid, name, serverId, userTags, customDataJsonString)");
            BindButton(m_FetchUnreadButton, OnFetchUnreadClick, "FetchUnreadMessageCount() / FetchUnreadTaskCount()");
            BindButton(m_CloseAIHelpButton, OnCloseAIHelpClick, "Close()");
        }

        /// <summary>
        /// 视图打开钩子，每次 OpenUIViewAsync 调用时触发。
        /// 子类重写须调用 base.OnOpen(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            // 订阅消息到达 / 未读工单数事件，使「查询未读消息数」按钮触发的异步回调直接回显在反馈区。
            SubscribeAIHelpEvents();

            AppendFeedback("AIHelp 演示已打开，逐个按钮体验各接口。Editor 下 vendor 为空操作，按钮点击后以文案回显。");
        }

        /// <summary>
        /// 视图关闭钩子，关闭时由基类清空反馈区。
        /// 子类重写须调用 base.OnClose(isShutdown, userData)。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            // 退订事件，避免 View 复用 / 关闭后仍持有 plugin 回调造成重复触发或泄漏。
            UnsubscribeAIHelpEvents();

            base.OnClose(isShutdown, userData);
        }
    }
}
