/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureRunInfo.cs
 * author:    taoye
 * created:   2026/5/8
 * descrip:   单条 Procedure 执行记录（仅 UNITY_EDITOR 编译）
 ***************************************************************/

#if UNITY_EDITOR
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 单条 Procedure 执行记录。
    /// 由 ProcedureComponent 在运行时采集，Editor Inspector 直接读取展示。
    /// 仅 UNITY_EDITOR 编译，出包时整个类型不存在。
    /// </summary>
    public sealed class ProcedureRunInfo
    {
        /// <summary>
        /// Procedure 类型全名。
        /// </summary>
        public string TypeFullName;

        /// <summary>
        /// 进入时刻（Time.realtimeSinceStartup）。
        /// </summary>
        public float EnterRealtime;

        /// <summary>
        /// 离开时刻（Time.realtimeSinceStartup）；Finished 为 false 时无意义。
        /// </summary>
        public float LeaveRealtime;

        /// <summary>
        /// 是否已结束（已切换到下一个 Procedure）。
        /// </summary>
        public bool Finished;

        /// <summary>
        /// 停留时长（秒）；活跃条目实时累积，已结束条目返回固定值。
        /// </summary>
        public float Elapsed => (Finished ? LeaveRealtime : Time.realtimeSinceStartup) - EnterRealtime;
    }
}
#endif
