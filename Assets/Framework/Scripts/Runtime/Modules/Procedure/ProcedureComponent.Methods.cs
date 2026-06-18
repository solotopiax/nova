/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureComponent.Methods.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Procedure组件 —— 私有方法
 ***************************************************************/

#if UNITY_EDITOR
using UnityEngine;
#endif

namespace NovaFramework.Runtime
{
    public sealed partial class ProcedureComponent : FrameworkComponent
    {
#if UNITY_EDITOR
        /// <summary>
        /// 记录一次 Procedure 切换事件。
        /// 相同 curTypeFullName 时直接返回；切换时关闭上一条记录，按容量上限裁剪后开启新记录。
        /// </summary>
        /// <param name="curTypeFullName">当前 Procedure 的类型全名，可为 null。</param>
        private void RecordProcedureTransition(string curTypeFullName)
        {
            if (curTypeFullName == m_LastProcedureTypeFullName)
            {
                return;
            }

            if (m_RunHistory.Count > 0)
            {
                ProcedureRunInfo last = m_RunHistory[m_RunHistory.Count - 1];
                if (!last.Finished)
                {
                    last.LeaveRealtime = Time.realtimeSinceStartup;
                    last.Finished = true;
                }
            }

            if (curTypeFullName != null)
            {
                if (m_RunHistory.Count >= c_RunHistoryCapacity)
                {
                    m_RunHistory.RemoveAt(0);
                }
                m_RunHistory.Add(new ProcedureRunInfo
                {
                    TypeFullName = curTypeFullName,
                    EnterRealtime = Time.realtimeSinceStartup,
                    Finished = false,
                });
            }

            m_LastProcedureTypeFullName = curTypeFullName;
        }
#endif
    }
}
