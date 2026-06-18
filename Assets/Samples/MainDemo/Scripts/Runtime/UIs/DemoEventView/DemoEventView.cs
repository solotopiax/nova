/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoEventView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.5 — Event 订阅/发布/取消演示 View（交互型）
 *            职责：演示 Nova.Event.Subscribe<T> / Fire / Unsubscribe，
 *            展示当前订阅数并将收到的事件内容追加到反馈区。
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Modules 2.5 Event 演示 View（交互型）。
    /// 演示 Nova.Event.Subscribe / Fire / Unsubscribe 三步操作，
    /// 并将收到的 DemoPingEventData 内容实时追加到反馈区。
    /// </summary>
    public sealed class DemoEventView : BaseDemoView
    {
        /// <summary>
        /// 订阅按钮，调用 Nova.Event.Subscribe 注册 DemoPingEventData 处理器。
        /// </summary>

        [SerializeField] private Button m_SubscribeButton;

        /// <summary>
        /// 取消订阅按钮，调用 Nova.Event.Unsubscribe 注销 DemoPingEventData 处理器。
        /// </summary>

        [SerializeField] private Button m_UnsubscribeButton;

        /// <summary>
        /// 发布事件按钮，调用 Nova.Event.Fire 发布一次 DemoPingEventData。
        /// </summary>

        [SerializeField] private Button m_FireButton;

        /// <summary>
        /// 当前订阅数文本展示组件。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_SubscribeCountText;

        /// <summary>
        /// 是否已注册 DemoPingEventData 处理器，防止重复订阅。
        /// </summary>
        private bool m_IsSubscribed;

        /// <summary>
        /// DemoPingEventData 事件处理器缓存，用于订阅与取消订阅传同一实例。
        /// </summary>
        private EventHandler<EventData> m_PingHandler;

        /// <summary>
        /// 视图初始化钩子，注册按钮事件，创建处理器实例，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Event");

            m_PingHandler = OnDemoPingReceived;

            if (m_SubscribeButton != null)
            {
                m_SubscribeButton.onClick.AddListener(OnSubscribeButtonClick);
                SetButtonApiHint(m_SubscribeButton, "Nova.Event.Subscribe<T>(handler)");
            }

            if (m_UnsubscribeButton != null)
            {
                m_UnsubscribeButton.onClick.AddListener(OnUnsubscribeButtonClick);
                SetButtonApiHint(m_UnsubscribeButton, "Nova.Event.Unsubscribe<T>(handler)");
            }

            if (m_FireButton != null)
            {
                m_FireButton.onClick.AddListener(OnFireButtonClick);
                SetButtonApiHint(m_FireButton, "Nova.Event.Fire(sender, e)");
            }
        }

        /// <summary>
        /// 视图打开钩子，刷新订阅数文本。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            RefreshSubscribeCountText();
        }

        /// <summary>
        /// 视图关闭钩子，确保在关闭时取消订阅，防止悬空 handler。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            UnsubscribeInternal();
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 订阅按钮点击回调，注册 DemoPingEventData 处理器。
        /// </summary>
        private void OnSubscribeButtonClick()
        {
            if (m_IsSubscribed)
            {
                AppendFeedback("Nova.Event.Subscribe<DemoPingEventData> -> 已订阅，无需重复", FeedbackLevel.Warn);
                return;
            }

            if (Nova.Event == null)
            {
                AppendFeedback("Nova.Event.Subscribe -> EventComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            Nova.Event.Subscribe<DemoPingEventData>(m_PingHandler);
            m_IsSubscribed = true;
            RefreshSubscribeCountText();
            AppendFeedback("Nova.Event.Subscribe<DemoPingEventData>(handler) -> handlers=" + Nova.Event.GetCountByID<DemoPingEventData>(), FeedbackLevel.Success);
        }

        /// <summary>
        /// 取消订阅按钮点击回调，注销 DemoPingEventData 处理器。
        /// </summary>
        private void OnUnsubscribeButtonClick()
        {
            if (!m_IsSubscribed)
            {
                AppendFeedback("Nova.Event.Unsubscribe<DemoPingEventData> -> 未订阅，跳过", FeedbackLevel.Warn);
                return;
            }

            UnsubscribeInternal();
            RefreshSubscribeCountText();
            AppendFeedback("Nova.Event.Unsubscribe<DemoPingEventData>(handler) -> handlers=" + (Nova.Event != null ? Nova.Event.GetCountByID<DemoPingEventData>() : 0), FeedbackLevel.Success);
        }

        /// <summary>
        /// 发布事件按钮点击回调，发布一次 DemoPingEventData 到事件总线。
        /// </summary>
        private void OnFireButtonClick()
        {
            if (Nova.Event == null)
            {
                AppendFeedback("Nova.Event.Fire -> EventComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            DemoPingEventData eventData = DemoPingEventData.Create("Demo Ping from DemoEventView");
            Nova.Event.Fire(this, eventData);
            AppendFeedback("Nova.Event.Fire(this, DemoPingEventData) -> handlers=" + Nova.Event.GetCountByID<DemoPingEventData>(), FeedbackLevel.Success);
        }

        /// <summary>
        /// 内部取消订阅逻辑，确保幂等。
        /// </summary>
        private void UnsubscribeInternal()
        {
            if (!m_IsSubscribed || Nova.Event == null)
            {
                m_IsSubscribed = false;
                return;
            }

            Nova.Event.Unsubscribe<DemoPingEventData>(m_PingHandler);
            m_IsSubscribed = false;
        }

        /// <summary>
        /// 收到 DemoPingEventData 事件的处理器，将消息内容追加到反馈区。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件数据基类实例，实际为 DemoPingEventData。</param>
        private void OnDemoPingReceived(object sender, EventData e)
        {
            if (e is DemoPingEventData ping)
            {
                AppendFeedback("收到 DemoPingEventData -> Message=\"" + ping.Message + "\"", FeedbackLevel.Info);
            }
        }

        /// <summary>
        /// 刷新当前订阅数文本显示。
        /// </summary>
        private void RefreshSubscribeCountText()
        {
            if (m_SubscribeCountText == null)
            {
                return;
            }

            int count = (Nova.Event != null) ? Nova.Event.GetCountByID<DemoPingEventData>() : 0;
            m_SubscribeCountText.text = "当前订阅数：" + count + (m_IsSubscribed ? "（本 View 已订阅）" : "");
        }
    }
}
