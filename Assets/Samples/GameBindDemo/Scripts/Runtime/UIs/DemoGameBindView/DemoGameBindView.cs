/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoGameBindView.cs
 * author:    taoye
 * created:   2026/07/02
 * descrip:   GameBind Kit 演示 View — 生命周期与公开接口
 *            职责：演示 Nova.Network.Kit<Bind>().QueryBindingAsync / BindAsync /
 *            QueryConflictAsync / ResolveAsync，并附登录（绑定前提）入口。
 ***************************************************************/

namespace NovaFramework.Kit.Network.GameBind.Samples.Runtime
{
    /// <summary>
    /// GameBind Kit 演示 View，展示绑定状态查询、账号绑定、冲突查询、裁决 API 的调用方式。
    /// 派生自 BaseDemoView，遵循三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// </summary>
    public sealed partial class DemoGameBindView : BaseDemoView
    {
        /// <summary>
        /// 视图初始化钩子，仅在首次创建实例时触发。
        /// 注册登录、绑定、冲突查询、裁决按钮事件并设置标题与 API 副标题。
        /// 子类重写须调用 base.OnInit(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("GameBind 账号绑定");

            if (m_LoginButton != null)
            {
                m_LoginButton.onClick.AddListener(OnLoginButtonClick);
                SetButtonApiHint(m_LoginButton, "Nova.Network.Kit<Login>().Async(uid, openId, forceNewAccount)");
            }

            if (m_BindButton != null)
            {
                m_BindButton.onClick.AddListener(OnBindButtonClick);
                SetButtonApiHint(m_BindButton, "Nova.Network.Kit<Bind>().BindAsync(ThirdLoginProvider.Google, openId)");
            }

            if (m_QueryBindingButton != null)
            {
                m_QueryBindingButton.onClick.AddListener(OnQueryBindingButtonClick);
                SetButtonApiHint(m_QueryBindingButton, "Nova.Network.Kit<Bind>().QueryBindingAsync(openId)");
            }

            if (m_UploadSaveButton != null)
            {
                m_UploadSaveButton.onClick.AddListener(OnUploadSaveButtonClick);
                SetButtonApiHint(m_UploadSaveButton, "Nova.Network.Kit<Save>().SetFullAsync(json)");
            }

            if (m_GetSaveButton != null)
            {
                m_GetSaveButton.onClick.AddListener(OnGetSaveButtonClick);
                SetButtonApiHint(m_GetSaveButton, "Nova.Network.Kit<Save>().GetFullAsync()");
            }

            if (m_QueryConflictButton != null)
            {
                m_QueryConflictButton.onClick.AddListener(OnQueryConflictButtonClick);
                SetButtonApiHint(m_QueryConflictButton, "Nova.Network.Kit<Bind>().QueryConflictAsync(openId)");
            }

            if (m_QuerySaveByUidButton != null)
            {
                m_QuerySaveByUidButton.onClick.AddListener(OnQuerySaveByUidButtonClick);
                SetButtonApiHint(m_QuerySaveByUidButton, "Nova.Network.Kit<Save>().GetFullAsync(targetUid)");
            }

            if (m_ResolveButton != null)
            {
                m_ResolveButton.onClick.AddListener(OnResolveButtonClick);
                SetButtonApiHint(m_ResolveButton, "Nova.Network.Kit<Bind>().ResolveAsync(openId, choice, verifyCode)");
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

            AppendFeedback("可先查询 OpenID 绑定状态；绑定前需登录，冲突时按冲突查询→二选一→裁决流程操作。");
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
