/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureBase.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   流程基类
 ***************************************************************/

using System.Threading;
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 流程基类。所有流程（框架内置和 Game 层自定义）均继承此类。
    /// <para>
    /// 重要约定：ChangeState 应在 OnUpdate 方法的最后一行调用，
    /// 调用后不要再执行任何逻辑，因为 ChangeState 会同步触发新状态的 OnEnter。
    /// </para>
    /// </summary>
    public abstract class ProcedureBase : FsmState<IProcedureManager>
    {
        /// <summary>
        /// 当前流程的取消令牌源。OnEnter 时创建，OnLeave 时取消并释放。
        /// </summary>
        private CancellationTokenSource m_Cts;

        /// <summary>
        /// 当前流程的取消令牌，子类在 async 方法中使用以响应流程退出。
        /// </summary>
        protected CancellationToken CancellationToken => m_Cts?.Token ?? default;

        /// <summary>
        /// 流程初始化时调用（FSM 创建时，每个流程调用一次）。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);
        }

        /// <summary>
        /// 进入流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            m_Cts?.Dispose();
            m_Cts = new CancellationTokenSource();
        }

        /// <summary>
        /// 流程轮询时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnUpdate(ProcedureOwner procedureOwner)
        {
            base.OnUpdate(procedureOwner);
        }

        /// <summary>
        /// 离开流程时调用。仅 Cancel 令牌，不 Dispose（异步 continuation 仍需观察取消状态）。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        /// <param name="isShutdown">是否因流程管理器关闭而离开。</param>
        protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            m_Cts?.Cancel();
            base.OnLeave(procedureOwner, isShutdown);
        }

        /// <summary>
        /// 流程销毁时调用（FSM 销毁时，每个流程调用一次）。释放 CTS 资源。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnDestroy(ProcedureOwner procedureOwner)
        {
            if (m_Cts != null)
            {
                m_Cts.Dispose();
                m_Cts = null;
            }

            base.OnDestroy(procedureOwner);
        }
    }
}
