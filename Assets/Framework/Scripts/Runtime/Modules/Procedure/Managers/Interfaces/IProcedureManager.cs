/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IProcedureManager.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Procedure管理器接口
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Procedure管理器接口。
    /// </summary>
    public interface IProcedureManager
    {
        /// <summary>
        /// 获取当前流程。
        /// </summary>
        ProcedureBase CurrentProcedure { get; }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        void Initialize(ProcedureManagerConfig config);

        /// <summary>
        /// 是否存在指定类型的流程。
        /// </summary>
        /// <typeparam name="T">要检查的流程类型。</typeparam>
        /// <returns>存在返回 true，否则返回 false。</returns>
        bool HasProcedure<T>() where T : ProcedureBase;

        /// <summary>
        /// 获取指定类型的流程实例。
        /// </summary>
        /// <typeparam name="T">要获取的流程类型。</typeparam>
        /// <returns>流程实例。</returns>
        ProcedureBase GetProcedure<T>() where T : ProcedureBase;

        /// <summary>
        /// 获取指定类型的流程实例。
        /// </summary>
        /// <param name="procedureType">要获取的流程类型。</param>
        /// <returns>流程实例。</returns>
        ProcedureBase GetProcedure(Type procedureType);

        /// <summary>
        /// 运行时追加 Procedure 到 FSM。
        /// 用于 HybridCLR 业务 DLL 加载后把业务 Procedure 延迟注册进同一 FSM。
        /// Initialize 必须已执行；追加项不得与现存 Procedure 类型重复；FSM 切换中禁止调用（由 Fsm.AddStates 守卫）。
        /// </summary>
        /// <param name="procedures">待追加的流程实例数组，不允许为空或包含 null。</param>
        void RegisterAdditionalProcedures(ProcedureBase[] procedures);
    }
}
