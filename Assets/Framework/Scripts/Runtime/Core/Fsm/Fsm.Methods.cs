/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Fsm.Methods.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   有限状态机——私有方法
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    internal sealed partial class Fsm<T> : IFsm<T> where T : class
    {
        /// <summary>
        /// 将单个状态插入 m_States 字典，供 Create 与 AddStates 复用。
        /// </summary>
        /// <param name="state">待插入的状态实例。</param>
        /// <param name="isNew">插入成功（字典中原本不存在该类型）时为 true，否则为 false。</param>
        private void AddStateInternal(FsmState<T> state, out bool isNew)
        {
            Type stateType = state.GetType();
            if (m_States.ContainsKey(stateType))
            {
                isNew = false;
                return;
            }

            m_States.Add(stateType, state);
            isNew = true;
        }

        /// <summary>
        /// 内部状态切换实现，两个公开重载的共享逻辑。
        /// </summary>
        /// <param name="stateType">目标状态类型。</param>
        private void ChangeStateInternal(Type stateType)
        {
            if (m_CurrentState == null)
            {
                throw new InvalidOperationException("状态机尚未启动，不可切换状态。");
            }

            if (m_IsDestroyed)
            {
                throw new InvalidOperationException("状态机已销毁，不可切换状态。");
            }

            if (m_IsChangingState)
            {
                throw new InvalidOperationException(Txt.Format("状态机正在切换状态中，禁止在 OnLeave/OnEnter 中重入调用 ChangeState。当前目标：{0}。", stateType.FullName));
            }

            if (!m_States.TryGetValue(stateType, out FsmState<T> state))
            {
                throw new ArgumentException(Txt.Format("状态机中不存在状态 {0}。", stateType.FullName));
            }

            m_IsChangingState = true;
            try
            {
                m_CurrentState.OnLeave(this, false);
                m_CurrentState = state;
                m_CurrentState.OnEnter(this);
            }
            finally
            {
                m_IsChangingState = false;
            }
        }
    }
}
