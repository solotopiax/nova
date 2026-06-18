/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoTGAView.cs
 * author:    nova-create-sample
 * created:   2026/06/01
 * descrip:   DemoTGAView 演示 View — 生命周期与公开接口
 ***************************************************************/

namespace NovaFramework.Sdk.Tga.Samples.Runtime
{
    /// <summary>
    /// DemoTGAView 演示 View，派生自 BaseDemoView，遵循三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// 提供登录、身份信息、打点、用户属性和公共属性的 TGA SDK 测试入口。
    /// </summary>
    public sealed partial class DemoTGAView : BaseDemoView
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

            SetTitle("TGA 演示");

            if (m_LoginButton != null)
            {
                m_LoginButton.onClick.AddListener(OnLoginButtonClick);
                SetButtonApiHint(m_LoginButton, "Nova.Network.Kit<Login>().Async(...) / Nova.SDK.Login(uid)");
            }

            if (m_GetDeviceIdButton != null)
            {
                m_GetDeviceIdButton.onClick.AddListener(OnGetDeviceIdButtonClick);
                SetButtonApiHint(m_GetDeviceIdButton, "TGAPlugin.GetDeviceId()");
            }

            if (m_GetDistinctIdButton != null)
            {
                m_GetDistinctIdButton.onClick.AddListener(OnGetDistinctIdButtonClick);
                SetButtonApiHint(m_GetDistinctIdButton, "TGAPlugin.GetDistinctId()");
            }

            if (m_GetPresetPropertiesButton != null)
            {
                m_GetPresetPropertiesButton.onClick.AddListener(OnGetPresetPropertiesButtonClick);
                SetButtonApiHint(m_GetPresetPropertiesButton, "TGAPlugin.GetPresetProperties()");
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
                SetButtonApiHint(m_SendEventButton, "TGAPlugin.TrackEvent(eventName, parameters)");
            }

            if (m_TimeEventButton != null)
            {
                m_TimeEventButton.onClick.AddListener(OnTimeEventButtonClick);
                SetButtonApiHint(m_TimeEventButton, "TGAPlugin.TimeEvent(eventName)");
            }

            if (m_FlushButton != null)
            {
                m_FlushButton.onClick.AddListener(OnFlushButtonClick);
                SetButtonApiHint(m_FlushButton, "TGAPlugin.Flush()");
            }

            if (m_UserSetButton != null)
            {
                m_UserSetButton.onClick.AddListener(OnUserSetButtonClick);
                SetButtonApiHint(m_UserSetButton, "TGAPlugin.UserSet(key, value)");
            }

            if (m_UserSetOnceButton != null)
            {
                m_UserSetOnceButton.onClick.AddListener(OnUserSetOnceButtonClick);
                SetButtonApiHint(m_UserSetOnceButton, "TGAPlugin.UserSetOnce(key, value)");
            }

            if (m_UserAddButton != null)
            {
                m_UserAddButton.onClick.AddListener(OnUserAddButtonClick);
                SetButtonApiHint(m_UserAddButton, "TGAPlugin.UserAdd(key, numericValue)");
            }

            if (m_UserAppendButton != null)
            {
                m_UserAppendButton.onClick.AddListener(OnUserAppendButtonClick);
                SetButtonApiHint(m_UserAppendButton, "TGAPlugin.UserAppend(key, values)");
            }

            if (m_UserUnsetButton != null)
            {
                m_UserUnsetButton.onClick.AddListener(OnUserUnsetButtonClick);
                SetButtonApiHint(m_UserUnsetButton, "TGAPlugin.UserUnset(key)");
            }

            if (m_UserDeleteButton != null)
            {
                m_UserDeleteButton.onClick.AddListener(OnUserDeleteButtonClick);
                SetButtonApiHint(m_UserDeleteButton, "TGAPlugin.UserDelete()");
            }

            if (m_SetSuperPropertyButton != null)
            {
                m_SetSuperPropertyButton.onClick.AddListener(OnSetSuperPropertyButtonClick);
                SetButtonApiHint(m_SetSuperPropertyButton, "TGAPlugin.SetSuperProperty(key, value)");
            }

            if (m_ClearSuperPropertiesButton != null)
            {
                m_ClearSuperPropertiesButton.onClick.AddListener(OnClearSuperPropertiesButtonClick);
                SetButtonApiHint(m_ClearSuperPropertiesButton, "TGAPlugin.ClearSuperProperties()");
            }

            if (m_SetDynamicSuperPropertyButton != null)
            {
                m_SetDynamicSuperPropertyButton.onClick.AddListener(OnSetDynamicSuperPropertyButtonClick);
                SetButtonApiHint(m_SetDynamicSuperPropertyButton, "TGAPlugin.SetDynamicSuperProperty(key, value)");
            }

            if (m_GetDynamicSuperPropertiesButton != null)
            {
                m_GetDynamicSuperPropertiesButton.onClick.AddListener(OnGetDynamicSuperPropertiesButtonClick);
                SetButtonApiHint(m_GetDynamicSuperPropertiesButton, "TGAPlugin.GetDynamicSuperProperties()");
            }

            if (m_ClearDynamicSuperPropertiesButton != null)
            {
                m_ClearDynamicSuperPropertiesButton.onClick.AddListener(OnClearDynamicSuperPropertiesButtonClick);
                SetButtonApiHint(m_ClearDynamicSuperPropertiesButton, "TGAPlugin.ClearDynamicSuperProperties()");
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

            AppendFeedback("TGA 演示已打开，可登录、获取设备/访客信息、发送打点、管理用户属性与公共属性。");
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
