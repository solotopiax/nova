/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename: EditorUtil.NovaIssues.Play.cs
 * descrip:  归集 Editor Play 阶段的框架运行证据。
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        internal static partial class NovaIssues
        {
            private const string c_PlayAttemptKey = "Nova.Issues.EditorPlay";
            private const double c_PlayCallsIntervalSeconds = 15;
            private static readonly EditorPlayModule[] s_PlayModules =
                (EditorPlayModule[])Enum.GetValues(typeof(EditorPlayModule));
            private static string s_PlayAttempt;
            private static double s_NextPlayCallsReport;

            /// <summary>定时汇总 Play 调用，避免频繁调用的入口逐次写入队列。</summary>
            private static void PollPlayCalls(double editorTime)
            {
                if (editorTime < s_NextPlayCallsReport)
                    return;
                s_NextPlayCallsReport = editorTime + c_PlayCallsIntervalSeconds;
                FlushPlayCalls();
            }

            /// <summary>以本批最后调用时间入队。成功入队后由既有回执保护重试及去重。</summary>
            private static void FlushPlayCalls()
            {
                foreach (var module in s_PlayModules)
                {
                    var (count, observedAt) = EditorPlayEvidence.TakeCalls(module);
                    if (count == 0)
                        continue;
                    string step = module.ToString().ToLowerInvariant();
                    if (!ObserveImplementation("editor-module", "success", step: step, observedAt: observedAt, calls: count))
                        EditorPlayEvidence.AddCalls(module, count, observedAt);
                }
            }

            /// <summary>表注册成功属于准备情况，与业务取表使用不同。</summary>
            private static void ObserveTablesReady()
            {
                ObserveImplementation("table-ready", "success",
                    reportId: EventId("editor-play-table-ready", CurrentPlayAttempt()));
            }

            /// <summary>同一 Play 在脚本重载前后沿用同一会话编号。</summary>
            private static string CurrentPlayAttempt()
            {
                if (s_PlayAttempt != null)
                    return s_PlayAttempt;
                s_PlayAttempt = SessionState.GetString(c_PlayAttemptKey, string.Empty);
                if (s_PlayAttempt.Length == 0)
                {
                    s_PlayAttempt = Guid.NewGuid().ToString("N");
                    SessionState.SetString(c_PlayAttemptKey, s_PlayAttempt);
                }
                return s_PlayAttempt;
            }
        }
    }
}
