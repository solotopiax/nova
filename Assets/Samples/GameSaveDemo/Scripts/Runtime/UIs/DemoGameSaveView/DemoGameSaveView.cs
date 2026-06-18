/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoGameSaveView.cs
 * author:    nova-create-sample
 * created:   2026/06/01
 * descrip:   GameSave Kit 演示 View — 生命周期与公开接口
 *            职责：演示 GameSave 存档读写全套 API（Set/Get/GetFull/SetFull
 *            及其批量重载）。GameSave 依赖已登录身份，先调 Login 取 UID。
 ***************************************************************/

namespace NovaFramework.Kit.Network.GameSave.Samples.Runtime
{
    /// <summary>
    /// GameSave Kit 演示 View，派生自 BaseDemoView，遵循三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// 演示登录取得 UID 后调用 GameSave 存档读写的 6 个公开接口。
    /// </summary>
    public sealed partial class DemoGameSaveView : BaseDemoView
    {
        /// <summary>
        /// 视图初始化钩子，仅在首次创建实例时触发。
        /// 注册登录与 6 个存档读写按钮事件，设置标题与各按钮 API 副标题。
        /// 子类重写须调用 base.OnInit(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("GameSave 存档演示");

            if (m_LoginButton != null)
            {
                m_LoginButton.onClick.AddListener(OnLoginButtonClick);
                SetButtonApiHint(m_LoginButton, "Nova.Network.Kit<Login>().Async(uid, openId, forceNewAccount)");
            }

            if (m_SetButton != null)
            {
                m_SetButton.onClick.AddListener(OnSetButtonClick);
                SetButtonApiHint(m_SetButton, "Nova.Network.Kit<Save>().SetAsync(key, value)");
            }

            if (m_GetButton != null)
            {
                m_GetButton.onClick.AddListener(OnGetButtonClick);
                SetButtonApiHint(m_GetButton, "Nova.Network.Kit<Save>().GetAsync(key)");
            }

            if (m_SetBatchButton != null)
            {
                m_SetBatchButton.onClick.AddListener(OnSetBatchButtonClick);
                SetButtonApiHint(m_SetBatchButton, "Nova.Network.Kit<Save>().SetAsync(keys, values)");
            }

            if (m_GetBatchButton != null)
            {
                m_GetBatchButton.onClick.AddListener(OnGetBatchButtonClick);
                SetButtonApiHint(m_GetBatchButton, "Nova.Network.Kit<Save>().GetAsync(keys)");
            }

            if (m_GetFullButton != null)
            {
                m_GetFullButton.onClick.AddListener(OnGetFullButtonClick);
                SetButtonApiHint(m_GetFullButton, "Nova.Network.Kit<Save>().GetFullAsync()");
            }

            if (m_SetFullButton != null)
            {
                m_SetFullButton.onClick.AddListener(OnSetFullButtonClick);
                SetButtonApiHint(m_SetFullButton, "Nova.Network.Kit<Save>().SetFullAsync(value)");
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

            AppendFeedback("GameSave 存档演示已打开，请先点击登录取得 UID，再演示存档读写。");
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
