/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoFsmView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 1.3 — FSM 三态切换概念可视化
 *            展示 FSM 核心 API 说明（AddStates / Start / ChangeState），
 *            并读取当前 ProcedureManager 持有的 FSM 状态作为运行时快照。
 *            注：Fsm<T> 是框架内部 internal 类型，Sample 层通过 IFsm 接口访问，
 *            不直接实例化 Fsm<T>。
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEngine.UI;
using UnityEngine;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 1.3 FSM 三态切换概念可视化 View。
    /// 展示 Fsm 核心 API 语义说明，并从 ProcedureComponent 读取当前运行态作为快照。
    /// </summary>
    public sealed class DemoFsmView : BaseDemoView
    {
        /// <summary>
        /// 「读取当前 FSM 快照」按钮，从 Nova.Procedure 获取当前状态输出到反馈区。
        /// </summary>

        [SerializeField] private Button m_SnapshotButton;

        /// <summary>
        /// 初始化钩子，注册按钮事件。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            if (m_SnapshotButton != null)
            {
                m_SnapshotButton.onClick.AddListener(OnSnapshotClick);
            }
        }

        /// <summary>
        /// 打开钩子，设置标题与 API 副标题，并输出 API 语义说明卡片。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            SetTitle("FSM 有限状态机");
            SetButtonApiHint(m_SnapshotButton, "Fsm<T>.Start<TState>() / ChangeState / AddStates");
            AppendApiSummary();
        }

        /// <summary>
        /// 追加 FSM 三步 API 语义说明到反馈区。
        /// </summary>
        private void AppendApiSummary()
        {
            AppendFeedback("AddStates(params FsmState<T>[]): 注册所有状态实例", FeedbackLevel.Info);
            AppendFeedback("Start<TState>(): 启动状态机并进入初始态，触发 OnEnter", FeedbackLevel.Info);
            AppendFeedback("ChangeState<TState>(): 离开当前态 OnLeave -> 进入目标态 OnEnter", FeedbackLevel.Info);
            AppendFeedback("约束：AddStates 禁止在 OnEnter/OnLeave 同步调用（m_IsChangingState 守卫）", FeedbackLevel.Warn);
        }

        /// <summary>
        /// 「读取当前 FSM 快照」点击回调，从 ProcedureComponent 读取当前 Procedure 名作为快照。
        /// </summary>
        private void OnSnapshotClick()
        {
            if (Nova.Procedure == null)
            {
                AppendFeedback("Nova.Procedure 未就绪", FeedbackLevel.Error);
                return;
            }

            string currentProcedure = Nova.Procedure.CurrentProcedure != null
                ? Nova.Procedure.CurrentProcedure.GetType().Name
                : "(null)";

            AppendFeedback($"Fsm<ProcedureBase>.CurrentState -> {currentProcedure}", FeedbackLevel.Success);
        }
    }
}
