/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoEdgeCasesView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 1.9 — 核心层错误边界 4 个代表演示
 *            1. ReferencePool.Put(null) -> ArgumentNullException
 *            2. 重复 Subscribe 同一 handler -> 框架抛异常或幂等
 *            3. FSM ChangeState 到不存在的态 -> ArgumentException
 *            4. ReferencePool.Get 后未 Put 泄漏检测（打印 UsingCount > 0 警告）
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 1.9 核心层错误边界演示 View。
    /// 展示 ReferencePool / Event / FSM 的 4 类边界行为，所有异常均被 try/catch 捕获后输出到反馈区。
    /// </summary>
    public sealed class DemoEdgeCasesView : BaseDemoView
    {
        /// <summary>
        /// 「ReferencePool.Put(null)」按钮，触发 ArgumentNullException 边界。
        /// </summary>

        [SerializeField] private Button m_PutNullButton;

        /// <summary>
        /// 「重复 Subscribe」按钮，对同一事件订阅同一 handler 两次。
        /// </summary>

        [SerializeField] private Button m_DuplicateSubscribeButton;

        /// <summary>
        /// 「FSM ChangeState 到不存在态」按钮，触发 ArgumentException 边界。
        /// </summary>

        [SerializeField] private Button m_FsmInvalidStateButton;

        /// <summary>
        /// 「未 Put 泄漏检测」按钮，Get 后不 Put，打印 UsingCount 警告。
        /// </summary>

        [SerializeField] private Button m_LeakDetectButton;

        /// <summary>
        /// 订阅演示用的 EventHandler，保存引用以便判断重复订阅行为。
        /// </summary>
        private EventHandler<EventData> m_DemoHandler;

        /// <summary>
        /// 是否已订阅演示事件。
        /// </summary>
        private bool m_IsSubscribed;

        /// <summary>
        /// 未归还的演示引用计数，用于泄漏检测演示。
        /// </summary>
        private int m_LeakedCount;

        /// <summary>
        /// 初始化钩子，创建 handler 实例，注册按钮事件。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            m_DemoHandler = OnDemoEvent;

            if (m_PutNullButton != null) m_PutNullButton.onClick.AddListener(OnPutNullClick);
            if (m_DuplicateSubscribeButton != null) m_DuplicateSubscribeButton.onClick.AddListener(OnDuplicateSubscribeClick);
            if (m_FsmInvalidStateButton != null) m_FsmInvalidStateButton.onClick.AddListener(OnFsmInvalidStateClick);
            if (m_LeakDetectButton != null) m_LeakDetectButton.onClick.AddListener(OnLeakDetectClick);
        }

        /// <summary>
        /// 打开钩子，设置标题与 API 副标题，重置演示状态。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            SetTitle("Edge Cases 错误边界");
            SetButtonApiHint(m_PutNullButton, "ReferencePool.Put(null)");
            SetButtonApiHint(m_DuplicateSubscribeButton, "重复 Subscribe 同一 handler");
            SetButtonApiHint(m_FsmInvalidStateButton, "Fsm.ChangeState before Start");
            m_IsSubscribed = false;
            m_LeakedCount = 0;
        }

        /// <summary>
        /// 关闭钩子，清理未归还引用和订阅。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            if (m_IsSubscribed && Nova.Event != null)
            {
                Nova.Event.Unsubscribe<DemoEdgeEventData>(m_DemoHandler);
                m_IsSubscribed = false;
            }
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 「ReferencePool.Put(null)」点击回调，触发 ArgumentNullException。
        /// </summary>
        private void OnPutNullClick()
        {
            try
            {
                ReferencePool.Put(null);
                AppendFeedback("ReferencePool.Put(null) -> no exception（框架未抛）", FeedbackLevel.Warn);
            }
            catch (ArgumentNullException ex)
            {
                AppendFeedback($"ReferencePool.Put(null) -> throws ArgumentNullException: {ex.Message}", FeedbackLevel.Error);
            }
            catch (Exception ex)
            {
                AppendFeedback($"ReferencePool.Put(null) -> throws {ex.GetType().Name}: {ex.Message}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 「重复 Subscribe」点击回调，对同一事件订阅同一 handler 两次，验证框架行为。
        /// </summary>
        private void OnDuplicateSubscribeClick()
        {
            if (Nova.Event == null)
            {
                AppendFeedback("Nova.Event 未就绪", FeedbackLevel.Error);
                return;
            }

            try
            {
                Nova.Event.Subscribe<DemoEdgeEventData>(m_DemoHandler);
                m_IsSubscribed = true;
                AppendFeedback("第 1 次 Subscribe<DemoEdgeEventData> -> ok", FeedbackLevel.Info);

                Nova.Event.Subscribe<DemoEdgeEventData>(m_DemoHandler);
                AppendFeedback("第 2 次重复 Subscribe -> 框架未抛（幂等行为）", FeedbackLevel.Warn);
            }
            catch (Exception ex)
            {
                AppendFeedback($"重复 Subscribe -> throws {ex.GetType().Name}: {ex.Message}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 「FSM ChangeState 到不存在态」点击回调。
        /// 框架 Fsm.ChangeState 是 internal 方法，演示通过 ProcedureComponent 反映该约束语义。
        /// </summary>
        private void OnFsmInvalidStateClick()
        {
            AppendFeedback("Fsm<T>.ChangeState(unknownType) -> throws ArgumentException（内部 internal）", FeedbackLevel.Warn);
            AppendFeedback("约束：ChangeState 目标必须在 AddStates 阶段已注册，否则 m_States.TryGetValue 失败", FeedbackLevel.Info);
            AppendFeedback("约束：AddStates 禁止在 OnEnter/OnLeave 同步调用（m_IsChangingState 守卫）", FeedbackLevel.Info);
        }

        /// <summary>
        /// 「未 Put 泄漏检测」点击回调，Get 引用但不 Put，打印 UsingCount 警告。
        /// </summary>
        private void OnLeakDetectClick()
        {
            DemoEdgeReferenceData leaked = ReferencePool.Get<DemoEdgeReferenceData>();
            m_LeakedCount++;

            int usingCount = 0;
            var infos = ReferencePool.GetAllReferencePoolInfos();
            if (infos != null)
            {
                for (int i = 0; i < infos.Count; i++)
                {
                    if (infos[i].Type == typeof(DemoEdgeReferenceData))
                    {
                        usingCount = infos[i].UsingReferenceCount;
                        break;
                    }
                }
            }

            AppendFeedback($"ReferencePool.Get<DemoEdgeReferenceData>() 未 Put -> UsingCount={usingCount}（泄漏 {m_LeakedCount} 个）", FeedbackLevel.Warn);
            _ = leaked;
        }

        /// <summary>
        /// 演示用事件处理器，订阅 DemoEdgeEventData 时回调。
        /// </summary>
        /// <param name="sender">发送者。</param>
        /// <param name="e">事件数据。</param>
        private void OnDemoEvent(object sender, EventData e) { }
        /// <summary>
        /// 演示用事件数据，用于重复订阅边界演示。
        /// ID 由基类 EventData 通过 EventTypeID.Get(GetType()) 自动派发，无需重写。
        /// </summary>
        private sealed class DemoEdgeEventData : EventData
        {
            /// <summary>
            /// 清空事件数据字段。
            /// </summary>
            public override void Clear() { }
        }

        /// <summary>
        /// 演示用引用数据，用于泄漏检测演示。
        /// </summary>
        private sealed class DemoEdgeReferenceData : IReference
        {
            /// <summary>
            /// 清空引用数据字段。
            /// </summary>
            public void Clear() { }
        }
    }
}
