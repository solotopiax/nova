/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoGameLoginView.cs
 * author:    taoye
 * created:   2026/06/01
 * descrip:   Login Kit 演示 View — 生命周期与公开接口
 *            职责：演示 Nova.Network.Kit<Login>().Async(openId, forceNewAccount)、
 *            Clear() 与 DeleteAsync() 三个核心 API。
 ***************************************************************/

namespace NovaFramework.Kit.Network.GameLogin.Samples.Runtime
{
    /// <summary>
    /// Login Kit 演示 View，展示登录、登出与删除账号 API 的调用方式。
    /// 派生自 BaseDemoView，遵循三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// </summary>
    public sealed partial class DemoGameLoginView : BaseDemoView
    {
        /// <summary>
        /// 视图初始化钩子，仅在首次创建实例时触发。
        /// 注册登录、登出按钮事件并设置标题与 API 副标题。
        /// 子类重写须调用 base.OnInit(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Login 登录");

            if (m_LoginButton != null)
            {
                m_LoginButton.onClick.AddListener(OnLoginButtonClick);
                SetButtonApiHint(m_LoginButton, "Nova.Network.Kit<Login>().Async(openId, forceNewAccount)");
            }

            if (m_ClearButton != null)
            {
                m_ClearButton.onClick.AddListener(OnClearButtonClick);
                SetButtonApiHint(m_ClearButton, "Nova.Network.Kit<Login>().Clear()");
            }

            if (m_DeleteButton != null)
            {
                m_DeleteButton.onClick.AddListener(OnDeleteButtonClick);
                SetButtonApiHint(m_DeleteButton, "Nova.Network.Kit<Login>().DeleteAsync()");
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
