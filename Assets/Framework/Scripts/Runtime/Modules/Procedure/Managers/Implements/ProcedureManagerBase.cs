/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureManagerBase.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Procedure管理器基类
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Procedure管理器基类。
    /// </summary>
    internal abstract class ProcedureManagerBase : FrameworkManager, IProcedureManager
    {
        /// <summary>
        /// 管理器优先级（值越小越先 Update、越后 Shutdown）。
        /// </summary>
        /// <remarks>值越小优先级越高，越先 Update、越后 Shutdown。</remarks>
        public override int Priority => 6;

        /// <summary>
        /// 获取当前流程。
        /// </summary>
        public abstract ProcedureBase CurrentProcedure { get; }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public abstract void Initialize(ProcedureManagerConfig config);

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public abstract override void Shutdown();

        /// <summary>
        /// 是否存在指定类型的流程。
        /// </summary>
        /// <typeparam name="T">要检查的流程类型。</typeparam>
        /// <returns>存在返回 true，否则返回 false。</returns>
        public abstract bool HasProcedure<T>() where T : ProcedureBase;

        /// <summary>
        /// 获取指定类型的流程实例。
        /// </summary>
        /// <typeparam name="T">要获取的流程类型。</typeparam>
        /// <returns>流程实例。</returns>
        public abstract ProcedureBase GetProcedure<T>() where T : ProcedureBase;

        /// <summary>
        /// 获取指定类型的流程实例。
        /// </summary>
        /// <param name="procedureType">要获取的流程类型。</param>
        /// <returns>流程实例。</returns>
        public abstract ProcedureBase GetProcedure(Type procedureType);

        /// <summary>
        /// 运行时追加 Procedure 到 FSM。
        /// 用于 HybridCLR 业务 DLL 加载后把业务 Procedure 延迟注册进同一 FSM。
        /// Initialize 必须已执行；追加项不得与现存 Procedure 类型重复；FSM 切换中禁止调用（由 Fsm.AddStates 守卫）。
        /// </summary>
        /// <param name="procedures">待追加的流程实例数组，不允许为空或包含 null。</param>
        public abstract void RegisterAdditionalProcedures(ProcedureBase[] procedures);
    }
}
