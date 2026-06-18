/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureManager.Visitors.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Procedure管理器 —— 属性与字段
 ***************************************************************/

namespace NovaFramework.Runtime
{
    internal sealed partial class ProcedureManager : ProcedureManagerBase
    {
        /// <summary>
        /// 流程状态机。
        /// </summary>
        private Fsm<IProcedureManager> m_ProcedureFsm;
    }
}
