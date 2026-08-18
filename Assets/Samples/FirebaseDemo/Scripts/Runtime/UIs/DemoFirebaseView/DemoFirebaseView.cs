/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoFirebaseView.cs
 * author:    nova-create-sample
 * created:   2026/06/02
 * descrip:   DemoFirebaseView 演示 View — 生命周期与公开接口
 ***************************************************************/

using System.Collections.Generic;
using TMPro;

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

            InitializePushTaskControls();
            RefreshEventParamsPreview();
        }

        // 初始化 push task 测试控件的固定选项和事件监听。
        private void InitializePushTaskControls()
        {
            if (m_PushTaskKeyDropdown != null)
            {
                m_PushTaskKeyDropdown.ClearOptions();
                m_PushTaskKeyDropdown.AddOptions(new List<TMP_Dropdown.OptionData>
                {
                    new TMP_Dropdown.OptionData("demo_push_task_1"),
                    new TMP_Dropdown.OptionData("demo_push_task_2"),
                    new TMP_Dropdown.OptionData("demo_push_task_3"),
                    new TMP_Dropdown.OptionData("demo_push_task_4"),
                });
                m_PushTaskKeyDropdown.value = 0;
                m_PushTaskKeyDropdown.RefreshShownValue();
                m_PushTaskKeyDropdown.onValueChanged.AddListener(_ => RefreshPushTaskPreview());
            }

            if (m_PushTaskTriggerTimeDropdown != null)
            {
                m_PushTaskTriggerTimeDropdown.ClearOptions();
                m_PushTaskTriggerTimeDropdown.AddOptions(new List<TMP_Dropdown.OptionData>
                {
                    new TMP_Dropdown.OptionData("UTC+0 当前时间"),
                    new TMP_Dropdown.OptionData("UTC+0 1 分钟后"),
                    new TMP_Dropdown.OptionData("UTC+0 5 分钟后"),
                    new TMP_Dropdown.OptionData("UTC+0 10 分钟后"),
                    new TMP_Dropdown.OptionData("UTC+0 1 小时后"),
                    new TMP_Dropdown.OptionData("UTC+0 3 小时后"),
                    new TMP_Dropdown.OptionData("UTC+0 12 小时后"),
                    new TMP_Dropdown.OptionData("UTC+0 24 小时后"),
                });
                m_PushTaskTriggerTimeDropdown.value = 0;
                m_PushTaskTriggerTimeDropdown.RefreshShownValue();
                m_PushTaskTriggerTimeDropdown.onValueChanged.AddListener(_ => RefreshPushTaskPreview());
            }

            if (m_PushTaskTemplateIdDropdown != null)
            {
                m_PushTaskTemplateIdDropdown.ClearOptions();
                m_PushTaskTemplateIdDropdown.AddOptions(new List<TMP_Dropdown.OptionData>
                {
                    new TMP_Dropdown.OptionData("1"),
                    new TMP_Dropdown.OptionData("2"),
                    new TMP_Dropdown.OptionData("3"),
                    new TMP_Dropdown.OptionData("4"),
                });
                m_PushTaskTemplateIdDropdown.value = 0;
                m_PushTaskTemplateIdDropdown.RefreshShownValue();
                m_PushTaskTemplateIdDropdown.onValueChanged.AddListener(_ => RefreshPushTaskPreview());
            }

            if (m_PushTaskCancelToggle != null)
            {
                m_PushTaskCancelToggle.isOn = false;
                m_PushTaskCancelToggle.onValueChanged.AddListener(_ => RefreshPushTaskPreview());
            }

            if (m_SendPushTaskButton != null)
            {
                m_SendPushTaskButton.onClick.AddListener(OnSendPushTaskButtonClick);
                SetPushTaskSendButtonInteractable(false);
                SetButtonApiHint(m_SendPushTaskButton, "IFirebasePushTaskPlugin.QueuePushTaskAsync(FirebasePushTask)");
            }

            RefreshPushTaskPreview();
        }

        private void SetPushTaskSendButtonInteractable(bool interactable)
        {
            if (m_SendPushTaskButton == null)
            {
                return;
            }

            m_SendPushTaskButton.interactable = interactable;
        }

        /// <summary>
        /// 视图打开钩子，每次 OpenUIViewAsync 调用时触发。
        /// 子类重写须调用 base.OnOpen(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            AppendFeedback("Firebase 演示已打开，可获取 Instance ID、Token，编辑事件属性、发送打点或执行登录；Push Task 需先登录后才可发送。");
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
