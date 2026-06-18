/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureSplash.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   闪屏流程
 *            职责：初始化 LauncherUIController → 展示闪屏画面 →
 *                  等待最短保底时长 → 统一跳转 ProcedureCheckVersion。
 *            Splash 面板跨流程存活，正常启动路径由业务入口决定销毁时机；
 *            若 FSM 关闭（isShutdown == true）则在本流程 OnLeave 兜底销毁。
 ***************************************************************/

using UnityEngine;
using ProcedureOwner = NovaFramework.Runtime.IFsm<NovaFramework.Runtime.IProcedureManager>;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 闪屏流程。
    /// 1. 初始化 LauncherUIController 并显示闪屏面板。
    /// 2. 等满最短保底时长（SplashDuration）。
    /// 3. 保底时长结束后统一进入 ProcedureCheckVersion。
    /// Splash 面板不在本流程 OnLeave 销毁，正常启动路径由业务入口统一回收；
    /// FSM 关闭（isShutdown == true）时在本流程 OnLeave 兜底销毁，避免残留。
    /// </summary>
    public sealed class ProcedureSplash : ProcedureBase
    {
        /// <summary>
        /// 闪屏已持续时间（秒）。
        /// </summary>
        private float m_ElapsedTime;

        /// <summary>
        /// 缓存的流程组件引用（OnInit 一次取得，全流程复用）。
        /// </summary>
        private ProcedureComponent m_ProcedureComponent;

        /// <summary>
        /// 流程初始化时调用；缓存常用组件引用，避免后续每帧 GetComponent。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);

            m_ProcedureComponent = FrameworkComponentsGroup.GetComponent<ProcedureComponent>();
        }

        /// <summary>
        /// 进入流程时调用；重置计时，初始化 UI，显示闪屏。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_ElapsedTime = 0f;

            LauncherUIController.Initialize(m_ProcedureComponent.LauncherSettings);
            LauncherUIController.ShowSplash();
            Log.Debug(LogTag.Procedure, "ProcedureSplash — 闪屏开始，等待保底时长。");
        }

        /// <summary>
        /// 流程轮询时调用；累加时间，保底时长满足后执行跳转。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected internal override void OnUpdate(ProcedureOwner procedureOwner)
        {
            base.OnUpdate(procedureOwner);

            m_ElapsedTime += Time.deltaTime;

            float splashDuration = m_ProcedureComponent.LauncherSettings.SplashDuration;
            if (m_ElapsedTime < splashDuration)
            {
                return;
            }

            ChangeState<ProcedureCheckVersion>(procedureOwner);
        }

        /// <summary>
        /// 离开流程时调用。
        /// 正常跳转时不销毁 Splash（由业务入口负责）；
        /// FSM 关闭（isShutdown == true）时兜底销毁，避免 Splash 残留。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        /// <param name="isShutdown">是否因流程管理器关闭而离开。</param>
        protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);

            if (isShutdown)
            {
                LauncherUIController.DestroySplash();
            }
        }
    }
}
