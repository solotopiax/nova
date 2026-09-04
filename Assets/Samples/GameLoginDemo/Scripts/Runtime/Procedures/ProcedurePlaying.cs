/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedurePlaying.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   游戏进行流程
 *            职责：游戏主逻辑运行阶段。
 ***************************************************************/

using NovaFramework.Runtime;
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

namespace NovaFramework.Kit.Network.GameLogin.Samples.Runtime
{
    /// <summary>
    /// 游戏进行流程。游戏主逻辑运行阶段，OnEnter 直接打开入口 DemoGameLoginView，后续所有子 Demo 沿用此范式（在此直开对应 DemoXXXView）。
    /// </summary>
    public class ProcedurePlaying : ProcedureBase
    {
        private int m_DemoGameLoginViewSerialID = -1;

        /// <summary>
        /// 进入流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            Log.Debug(LogTag.Procedure, "ProcedurePlaying — 进入游戏主循环。");
            Nova.UI.OnOpenUIViewFail += OnOpenUIViewFail;
            m_DemoGameLoginViewSerialID = Nova.UI.OpenUIViewAsync<DemoGameLoginView>();
            if (m_DemoGameLoginViewSerialID < 0)
            {
                Log.Error(LogTag.UI, "ProcedurePlaying — DemoGameLoginView 打开请求失败。");
            }
        }

        private void OnOpenUIViewFail(int serialID, string assetLocation, string errorMessage)
        {
            if (serialID != m_DemoGameLoginViewSerialID)
            {
                return;
            }

            m_DemoGameLoginViewSerialID = -1;
            Log.Error(LogTag.UI, "ProcedurePlaying — DemoGameLoginView 异步打开失败。Asset 地址 '{0}'：{1}", assetLocation, errorMessage);
        }

        /// <summary>
        /// 流程轮询时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnUpdate(ProcedureOwner procedureOwner)
        {
            base.OnUpdate(procedureOwner);
        }

        /// <summary>
        /// 离开流程时调用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        /// <param name="isShutdown">是否因流程管理器关闭而离开。</param>
        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            Nova.UI.OnOpenUIViewFail -= OnOpenUIViewFail;
            if (m_DemoGameLoginViewSerialID >= 0 && Nova.UI.IsLoadingUIView(m_DemoGameLoginViewSerialID))
            {
                Nova.UI.CloseUIView(m_DemoGameLoginViewSerialID);
            }

            m_DemoGameLoginViewSerialID = -1;
            base.OnLeave(procedureOwner, isShutdown);
        }
    }
}
