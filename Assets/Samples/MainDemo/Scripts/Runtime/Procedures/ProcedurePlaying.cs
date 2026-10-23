/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedurePlaying.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   游戏进行流程
 *            职责：游戏主逻辑运行阶段。
 *            当前为模拟实现，仅输出日志标记进入主循环。
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEngine;
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// 游戏进行流程。游戏主逻辑运行阶段。
    /// </summary>
    public class ProcedurePlaying : ProcedureBase
    {
        /// <summary>
        /// 本次进入拥有的导航视图序列编号，用于失败与退出时精确清理。
        /// </summary>
        private int m_DemoNavTreeViewSerialID = -1;

        /// <summary>
        /// 当前进入是否已请求重新预加载，由下一次流程轮询消费。
        /// </summary>
        private bool m_PreloadRestartRequested;

        /// <summary>
        /// 进入流程时订阅打开失败事件，并打开本次拥有的导航视图。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            m_PreloadRestartRequested = false;

            Log.Debug(LogTag.Procedure, "ProcedurePlaying — 进入游戏主循环。");

            if (Nova.UI == null)
            {
                Log.Error(LogTag.UI, "ProcedurePlaying — UI 组件不可用，无法打开导航。");
                return;
            }

            Nova.UI.OnOpenUIViewFail += OnOpenUIViewFail;
            m_DemoNavTreeViewSerialID = Nova.UI.OpenUIViewAsync<DemoNavTreeView>();
            if (m_DemoNavTreeViewSerialID < 0)
            {
                Log.Error(LogTag.UI, "ProcedurePlaying — DemoNavTreeView 打开请求失败。");
            }
        }

        /// <summary>
        /// 请求从当前游戏主流程重新执行预加载，状态转换由下一次轮询统一触发。
        /// </summary>
        /// <returns>当前仍为此流程且没有待处理请求时返回 true；过期或重复请求返回 false。</returns>
        public bool RequestPreloadRestart()
        {
            if (Nova.Procedure == null || !ReferenceEquals(Nova.Procedure.CurrentProcedure, this)
                || m_PreloadRestartRequested)
            {
                return false;
            }

            m_PreloadRestartRequested = true;
            return true;
        }

        /// <summary>
        /// 只处理本次导航请求的异步打开失败，释放本流程保存的序列编号。
        /// </summary>
        /// <param name="serialID">失败请求的视图序列编号。</param>
        /// <param name="assetLocation">失败请求的资源地址。</param>
        /// <param name="errorMessage">打开失败的原因。</param>
        private void OnOpenUIViewFail(int serialID, string assetLocation, string errorMessage)
        {
            if (serialID != m_DemoNavTreeViewSerialID)
            {
                return;
            }

            m_DemoNavTreeViewSerialID = -1;
            Log.Error(LogTag.UI, "ProcedurePlaying — DemoNavTreeView 异步打开失败。Asset 地址 '{0}'：{1}", assetLocation, errorMessage);
        }

        /// <summary>
        /// 流程轮询时消费唯一重预加载请求，并在末尾切换到现有预加载流程。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnUpdate(ProcedureOwner procedureOwner)
        {
            base.OnUpdate(procedureOwner);
            if (!m_PreloadRestartRequested)
            {
                return;
            }

            m_PreloadRestartRequested = false;
            ChangeState<ProcedurePreload>(procedureOwner);
        }

        /// <summary>
        /// 离开流程时退订事件并关闭本次导航，兼顾加载中、已打开与门面销毁。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        /// <param name="isShutdown">是否因流程管理器关闭而离开。</param>
        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            try
            {
                if (Nova.UI != null)
                {
                    Nova.UI.OnOpenUIViewFail -= OnOpenUIViewFail;
                    if (m_DemoNavTreeViewSerialID >= 0
                        && (Nova.UI.IsLoadingUIView(m_DemoNavTreeViewSerialID)
                            || Nova.UI.GetUIView(m_DemoNavTreeViewSerialID) != null))
                    {
                        Nova.UI.CloseUIView(m_DemoNavTreeViewSerialID);
                    }
                }
            }
            finally
            {
                m_DemoNavTreeViewSerialID = -1;
                m_PreloadRestartRequested = false;
                base.OnLeave(procedureOwner, isShutdown);
            }
        }
    }
}
