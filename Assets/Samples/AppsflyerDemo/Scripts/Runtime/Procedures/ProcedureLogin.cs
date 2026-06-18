/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureLogin.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   登录流程
 *            职责：打开登录 UI，等待登录成功后跳转 Playing。
 *            当前为模拟实现，延迟后自动跳转。
 ***************************************************************/

using NovaFramework.Runtime;
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

namespace NovaFramework.Sdk.Appsflyer.Samples.Runtime
{
    /// <summary>
    /// 登录流程。显示登录界面，等待登录成功后跳转 ProcedurePlaying。
    /// 当前为模拟实现，延迟 1 秒后自动跳转。
    /// </summary>
    public class ProcedureLogin : ProcedureBase
    {
        /// <summary>
        /// 模拟登录延迟时间（秒）。
        /// </summary>
        private const float c_SimulateDelay = 1f;

        /// <summary>
        /// 已等待时间。
        /// </summary>
        private float m_ElapsedTime;

        /// <summary>
        /// 登录是否完成。
        /// </summary>
        private bool m_LoginComplete;

        /// <summary>
        /// 进入流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_ElapsedTime = 0f;
            m_LoginComplete = false;

            Log.Debug(LogTag.Procedure, "ProcedureLogin — 模拟登录中...");
        }

        /// <summary>
        /// 流程轮询时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnUpdate(ProcedureOwner procedureOwner)
        {
            base.OnUpdate(procedureOwner);

            if (m_LoginComplete)
            {
                ChangeState<ProcedurePlaying>(procedureOwner);
                return;
            }
            m_ElapsedTime += UnityEngine.Time.deltaTime;
            if (m_ElapsedTime >= c_SimulateDelay)
            {
                Log.Debug(LogTag.Procedure, "ProcedureLogin — 模拟登录成功。");
                m_LoginComplete = true;
            }
        }

        /// <summary>
        /// 离开流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        /// <param name="isShutdown">是否因流程管理器关闭而离开。</param>
        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
        }

    }
}
