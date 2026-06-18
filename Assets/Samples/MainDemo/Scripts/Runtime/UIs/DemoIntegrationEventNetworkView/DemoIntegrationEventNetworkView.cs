/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIntegrationEventNetworkView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Integration 4.4 — Event + Network 桥接演示
 *            fake Nova.Event.Fire 模拟收消息，演示网络消息桥接到事件总线。
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Integration Demo 4.4：网络消息桥接到事件总线演示（mock）。
    /// API 副标题：Nova.Network.OnWebSocketReceiveMessage += h -> Nova.Event.Fire(this, e)。
    /// 交互触发型：Connect Mock / Send Mock Message / Subscribe Bridged Event 三按钮 + 收到消息卡片。
    /// 注意：本 demo 使用 fake Fire 模拟，不实际建立 WebSocket 连接（无外部依赖）。
    /// </summary>
    public sealed class DemoIntegrationEventNetworkView : BaseDemoView
    {
        /// <summary>
        /// 模拟连接按钮。
        /// </summary>

        [SerializeField] private Button m_ConnectMockButton;

        /// <summary>
        /// 发送模拟消息按钮。
        /// </summary>

        [SerializeField] private Button m_SendMockButton;

        /// <summary>
        /// 订阅桥接事件按钮。
        /// </summary>

        [SerializeField] private Button m_SubscribeButton;

        /// <summary>
        /// 取消订阅桥接事件按钮。
        /// </summary>

        [SerializeField] private Button m_UnsubscribeButton;

        /// <summary>
        /// 是否已模拟连接状态。
        /// </summary>
        private bool m_IsMockConnected;

        /// <summary>
        /// 当前桥接事件订阅句柄，用于取消订阅时精确匹配。
        /// </summary>
        private EventHandler<EventData> m_BridgedHandler;

        /// <summary>
        /// 视图初始化钩子，设置标题、API 副标题并绑定按钮事件。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Event + Network");

            if (m_ConnectMockButton != null)
            {
                m_ConnectMockButton.onClick.AddListener(OnConnectMockButtonClick);
                SetButtonApiHint(m_ConnectMockButton, "Nova.Network.ConnectServer(mock)");
            }

            if (m_SendMockButton != null)
            {
                m_SendMockButton.onClick.AddListener(OnSendMockButtonClick);
                SetButtonApiHint(m_SendMockButton, "Nova.Event.Fire(this, e)");
            }

            if (m_SubscribeButton != null)
            {
                m_SubscribeButton.onClick.AddListener(OnSubscribeButtonClick);
                SetButtonApiHint(m_SubscribeButton, "Nova.Event.Subscribe<DemoPingEventData>(h)");
            }

            if (m_UnsubscribeButton != null)
            {
                m_UnsubscribeButton.onClick.AddListener(OnUnsubscribeButtonClick);
                SetButtonApiHint(m_UnsubscribeButton, "Nova.Event.Unsubscribe<DemoPingEventData>(h)");
            }
        }

        /// <summary>
        /// 视图关闭钩子，取消所有事件订阅防止回调泄漏。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            UnsubscribeBridgedEvent();
            m_IsMockConnected = false;
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 模拟 WebSocket 连接（不实际建立连接，仅切换状态与打印日志）。
        /// </summary>
        private void OnConnectMockButtonClick()
        {
            m_IsMockConnected = !m_IsMockConnected;
            if (m_IsMockConnected)
            {
                AppendFeedback("Nova.Network.ConnectServer(mock) -> mock connected (fake)", FeedbackLevel.Success);
            }
            else
            {
                AppendFeedback("Nova.Network.Disconnect(mock) -> mock disconnected (fake)", FeedbackLevel.Warn);
            }
        }

        /// <summary>
        /// 发送模拟消息，直接 fake Nova.Event.Fire，不经过真实 WebSocket。
        /// </summary>
        private void OnSendMockButtonClick()
        {
            if (!m_IsMockConnected)
            {
                AppendFeedback("Send Mock -> not connected, click Connect Mock first", FeedbackLevel.Warn);
                return;
            }

            string mockPayload = string.Format("mock_payload_{0}", Time.frameCount);
            AppendFeedback(string.Format("WS receive(mock) -> payload=\"{0}\"", mockPayload), FeedbackLevel.Info);

            DemoPingEventData eventData = DemoPingEventData.Create(mockPayload);
            Nova.Event.Fire(this, eventData);
            AppendFeedback(string.Format("Nova.Event.Fire(this, DemoPingEventData) -> handlers={0}", Nova.Event.GetCountByID<DemoPingEventData>()), FeedbackLevel.Success);
        }

        /// <summary>
        /// 订阅桥接事件，监听 DemoPingEventData 并打印反馈。
        /// </summary>
        private void OnSubscribeButtonClick()
        {
            if (m_BridgedHandler != null)
            {
                AppendFeedback("Nova.Event.Subscribe<DemoPingEventData> -> already subscribed", FeedbackLevel.Warn);
                return;
            }

            m_BridgedHandler = OnBridgedPingEvent;
            Nova.Event.Subscribe<DemoPingEventData>(m_BridgedHandler);
            AppendFeedback(string.Format("Nova.Event.Subscribe<DemoPingEventData>(h) -> handlers={0}", Nova.Event.GetCountByID<DemoPingEventData>()), FeedbackLevel.Success);
        }

        /// <summary>
        /// 取消订阅桥接事件。
        /// </summary>
        private void OnUnsubscribeButtonClick()
        {
            UnsubscribeBridgedEvent();
            AppendFeedback(string.Format("Nova.Event.Unsubscribe<DemoPingEventData>(h) -> handlers={0}", Nova.Event.GetCountByID<DemoPingEventData>()), FeedbackLevel.Info);
        }

        /// <summary>
        /// 内部统一取消订阅，避免重复注销。
        /// </summary>
        private void UnsubscribeBridgedEvent()
        {
            if (m_BridgedHandler == null || Nova.Event == null)
            {
                m_BridgedHandler = null;
                return;
            }

            Nova.Event.Unsubscribe<DemoPingEventData>(m_BridgedHandler);
            m_BridgedHandler = null;
        }

        /// <summary>
        /// DemoPingEventData 事件桥接回调，在反馈区打印收到的消息。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件数据，预期为 DemoPingEventData。</param>
        private void OnBridgedPingEvent(object sender, EventData e)
        {
            if (e is DemoPingEventData ping)
            {
                AppendFeedback(string.Format("OnBridgedPingEvent -> Message=\"{0}\"", ping.Message), FeedbackLevel.Success);
            }
        }
    }
}
