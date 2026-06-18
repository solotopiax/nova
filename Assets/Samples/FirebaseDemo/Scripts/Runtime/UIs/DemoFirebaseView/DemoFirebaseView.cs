/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoFirebaseView.cs
 * author:    nova-create-sample
 * created:   2026/06/02
 * descrip:   DemoFirebaseView 演示 View — 生命周期与公开接口
 ***************************************************************/

namespace NovaFramework.Sdk.Firebase.Samples.Runtime
{
    /// <summary>
    /// DemoFirebaseView 演示 View，派生自 BaseDemoView，遵循三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// 自带一个示例交互按钮，业务侧替换为真实 Kit API 调用即可。
    /// </summary>
    public sealed partial class DemoFirebaseView : BaseDemoView
    {
        /// <summary>
        /// 视图初始化钩子，仅在首次创建实例时触发。
        /// 注册示例按钮事件并设置标题与 API 副标题。
        /// 子类重写须调用 base.OnInit(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Firebase 演示");

            if (m_GetInstanceIdButton != null)
            {
                m_GetInstanceIdButton.onClick.AddListener(OnGetInstanceIdButtonClick);
                SetButtonApiHint(m_GetInstanceIdButton, "FirebasePlugin.GetAnalyticsInstanceId()");
            }

            if (m_GetTokenButton != null)
            {
                m_GetTokenButton.onClick.AddListener(OnGetTokenButtonClick);
                SetButtonApiHint(m_GetTokenButton, "FirebasePlugin.GetToken()");
            }

            if (m_AddEventParamButton != null)
            {
                m_AddEventParamButton.onClick.AddListener(OnAddEventParamButtonClick);
            }

            if (m_ClearEventParamsButton != null)
            {
                m_ClearEventParamsButton.onClick.AddListener(OnClearEventParamsButtonClick);
            }

            if (m_SendEventButton != null)
            {
                m_SendEventButton.onClick.AddListener(OnSendEventButtonClick);
                SetButtonApiHint(m_SendEventButton, "FirebasePlugin.TrackEvent(eventName, parameters)");
            }

            if (m_LoginButton != null)
            {
                m_LoginButton.onClick.AddListener(OnLoginButtonClick);
                SetButtonApiHint(m_LoginButton, "Nova.Network.Kit<Login>().Async(...) / Nova.SDK.Login(uid)");
            }

            RefreshEventParamsPreview();
        }

        /// <summary>
        /// 视图打开钩子，每次 OpenUIViewAsync 调用时触发。
        /// 子类重写须调用 base.OnOpen(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            AppendFeedback("Firebase 演示已打开，可获取 Instance ID、Token，编辑事件属性、发送打点或执行登录。");
        }

        /// <summary>
        /// 视图关闭钩子，关闭时由基类清空反馈区。
        /// 子类重写须调用 base.OnClose(isShutdown, userData)。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);
        }
    }
}
