/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureManager.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Procedure管理器
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Procedure管理器。
    /// </summary>
    internal sealed partial class ProcedureManager : ProcedureManagerBase
    {
        /// <summary>
        /// 初始化Procedure管理器的新实例。
        /// </summary>
        public ProcedureManager()
        {
        }

        /// <summary>
        /// 获取当前流程。
        /// </summary>
        public override ProcedureBase CurrentProcedure
        {
            get
            {
                return m_ProcedureFsm != null ? (ProcedureBase)m_ProcedureFsm.CurrentState : null;
            }
        }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override void Initialize(ProcedureManagerConfig config)
        {
            if (config.Procedures == null || config.Procedures.Length == 0)
            {
                throw new ArgumentException("流程集合不能为空。", nameof(config));
            }

            if (config.EntranceProcedureType == null)
            {
                throw new ArgumentNullException(nameof(config), "入口流程类型无效。");
            }

            ProcedureBase[] procedures = new ProcedureBase[config.Procedures.Length];
            Array.Copy(config.Procedures, procedures, procedures.Length);
            m_ProcedureFsm = Fsm<IProcedureManager>.Create(this, procedures);
            m_ProcedureFsm.Start(config.EntranceProcedureType);
        }

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public override void Update()
        {
            m_ProcedureFsm?.Update();
        }

        /// <summary>
        /// 关闭并清理管理器。
        /// 先销毁启动阶段 UI 面板，再关闭 FSM，确保面板不残留。
        /// </summary>
        public override void Shutdown()
        {
            LauncherUIController.DestroyAll();

            if (m_ProcedureFsm != null)
            {
                m_ProcedureFsm.Shutdown();
                m_ProcedureFsm = null;
            }
        }

        /// <summary>
        /// 是否存在指定类型的流程。
        /// </summary>
        /// <typeparam name="T">要检查的流程类型。</typeparam>
        /// <returns>存在返回 true，否则返回 false。</returns>
        public override bool HasProcedure<T>()
        {
            if (m_ProcedureFsm == null)
            {
                throw new InvalidOperationException("ProcedureManager 尚未初始化。");
            }

            return m_ProcedureFsm.HasState<T>();
        }

        /// <summary>
        /// 获取指定类型的流程实例。
        /// </summary>
        /// <typeparam name="T">要获取的流程类型。</typeparam>
        /// <returns>流程实例。</returns>
        public override ProcedureBase GetProcedure<T>()
        {
            if (m_ProcedureFsm == null)
            {
                throw new InvalidOperationException("ProcedureManager 尚未初始化。");
            }

            return (ProcedureBase)m_ProcedureFsm.GetState<T>();
        }

        /// <summary>
        /// 获取指定类型的流程实例。
        /// </summary>
        /// <param name="procedureType">要获取的流程类型。</param>
        /// <returns>流程实例。</returns>
        public override ProcedureBase GetProcedure(Type procedureType)
        {
            if (m_ProcedureFsm == null)
            {
                throw new InvalidOperationException("ProcedureManager 尚未初始化。");
            }

            return (ProcedureBase)m_ProcedureFsm.GetState(procedureType);
        }

        /// <summary>
        /// 运行时追加 Procedure 到 FSM。
        /// 用于 HybridCLR 业务 DLL 加载后把业务 Procedure 延迟注册进同一 FSM。
        /// Initialize 必须已执行；追加项不得与现存 Procedure 类型重复；FSM 切换中禁止调用（由 Fsm.AddStates 守卫）。
        /// </summary>
        /// <param name="procedures">待追加的流程实例数组，不允许为空或包含 null。</param>
        public override void RegisterAdditionalProcedures(ProcedureBase[] procedures)
        {
            if (m_ProcedureFsm == null)
            {
                throw new InvalidOperationException("[ProcedureManager] RegisterAdditionalProcedures 在 Initialize 之前调用。");
            }

            if (procedures == null || procedures.Length == 0)
            {
                throw new ArgumentNullException(nameof(procedures), "待追加的流程集合不能为空。");
            }

            // HasProcedure/GetProcedure 均委托 m_ProcedureFsm，无独立容器需同步；
            // Fsm.AddStates 内部负责 null 检查、重复类型检查、切换中检查及 OnInit 回调。
            m_ProcedureFsm.AddStates(procedures);
        }
    }
}
