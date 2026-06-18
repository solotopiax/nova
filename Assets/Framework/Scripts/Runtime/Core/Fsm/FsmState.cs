/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FsmState.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   有限状态机状态基类
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 有限状态机状态基类。
    /// </summary>
    /// <typeparam name="T">状态机持有者类型。</typeparam>
    public abstract class FsmState<T> where T : class
    {
        /// <summary>
        /// 状态初始化时调用（FSM 创建时，每个 State 调用一次）。
        /// </summary>
        /// <param name="fsm">所属状态机引用。</param>
        protected internal virtual void OnInit(IFsm<T> fsm)
        {
        }

        /// <summary>
        /// 进入状态时调用。
        /// </summary>
        /// <param name="fsm">所属状态机引用。</param>
        protected internal virtual void OnEnter(IFsm<T> fsm)
        {
        }

        /// <summary>
        /// 状态轮询时调用（每帧）。
        /// </summary>
        /// <param name="fsm">所属状态机引用。</param>
        protected internal virtual void OnUpdate(IFsm<T> fsm)
        {
        }

        /// <summary>
        /// 离开状态时调用。
        /// </summary>
        /// <param name="fsm">所属状态机引用。</param>
        /// <param name="isShutdown">是否因 FSM 关闭而离开。</param>
        protected internal virtual void OnLeave(IFsm<T> fsm, bool isShutdown)
        {
        }

        /// <summary>
        /// 状态销毁时调用（FSM 销毁时，每个 State 调用一次）。
        /// </summary>
        /// <param name="fsm">所属状态机引用。</param>
        protected internal virtual void OnDestroy(IFsm<T> fsm)
        {
        }

        /// <summary>
        /// 切换到指定状态。
        /// </summary>
        /// <typeparam name="TState">目标状态类型。</typeparam>
        /// <param name="fsm">所属状态机引用。</param>
        protected void ChangeState<TState>(IFsm<T> fsm) where TState : FsmState<T>
        {
            fsm.ChangeState<TState>();
        }

        /// <summary>
        /// 以 Type 方式切换到指定状态。
        /// </summary>
        /// <param name="fsm">所属状态机引用。</param>
        /// <param name="stateType">目标状态类型。</param>
        protected void ChangeState(IFsm<T> fsm, System.Type stateType)
        {
            fsm.ChangeState(stateType);
        }
    }
}
