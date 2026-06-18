/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureManagerConfig.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Procedure管理器配置
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Procedure管理器配置。
    /// </summary>
    public class ProcedureManagerConfig
    {
        /// <summary>
        /// 所有流程实例。
        /// </summary>
        public ProcedureBase[] Procedures { get; set; }

        /// <summary>
        /// 入口流程类型。
        /// </summary>
        public Type EntranceProcedureType { get; set; }
    }
}
