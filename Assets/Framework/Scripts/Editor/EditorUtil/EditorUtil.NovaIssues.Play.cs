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
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        internal static partial class NovaIssues
        {
            private const string c_PlayAttemptKey = "Nova.Issues.EditorPlay";
            private const string c_PlaySequenceKey = "Nova.Issues.EditorPlaySequence";
            private static readonly string[] s_PreloadSteps =
                { "persist", "localization", "network", "sdk", "table", "ui", "sound", "vibration" };
            private static string s_PlayAttempt;
            private static int s_StartupSequence;
            private static string s_StartupStage;
            private static double s_StartupStageStarted;
            private static bool s_StartupWaitReported;

            /// <summary>从实际的资源、流程和数据表入口记录本次 Play 使用。</summary>
            private static void ObservePlayModule(EditorPlayModule module)
            {
                string step = module.ToString().ToLowerInvariant();
                ObserveImplementation("editor-module", "success", step: step,
                    reportId: EventId("editor-play-module", CurrentPlayAttempt(), step));
            }

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
                s_StartupSequence = SessionState.GetInt(c_PlaySequenceKey, 0);
                return s_PlayAttempt;
            }

            /// <summary>用框架流程的真实切换记录已推进的阶段。</summary>
            private static void ObserveProcedureEntry(string phase)
            {
                FinishStartupStage("advanced");
                if (phase != null)
                    BeginStartupStage(phase);
            }

            private static void ObserveProcedureFailure(string phase)
            {
                if (phase != null && phase == s_StartupStage)
                    FinishStartupStage("failed");
            }

            /// <summary>兼容消费工程已经导入的 MainDemo 预加载流程。</summary>
            private static void ObservePreloadStage(string message, LogType type)
            {
                int marker = message.IndexOf("[Procedure] Preload Step ", StringComparison.Ordinal);
                if (marker >= 0)
                {
                    int digit = marker + "[Procedure] Preload Step ".Length;
                    if (message.Length <= digit + 3 || message[digit] < '1' || message[digit] > '8' ||
                        string.CompareOrdinal(message, digit + 1, "/8 ", 0, 3) != 0)
                        return;
                    int step = message[digit] - '1';
                    FinishStartupStage(step > 0 && s_StartupStage == s_PreloadSteps[step - 1] ? "success" : "advanced");
                    BeginStartupStage(s_PreloadSteps[step]);
                }
                else if (s_StartupStage == "vibration" && message.Contains("[Procedure] 预加载全部完成。"))
                {
                    FinishStartupStage("success");
                }
                else if (type == LogType.Error && Array.IndexOf(s_PreloadSteps, s_StartupStage) >= 0 &&
                         message.Contains("[Procedure] 预加载异常:"))
                {
                    FinishStartupStage("failed");
                }
            }

            private static void BeginStartupStage(string phase)
            {
                CurrentPlayAttempt();
                s_StartupSequence++;
                SessionState.SetInt(c_PlaySequenceKey, s_StartupSequence);
                s_StartupStage = phase;
                s_StartupStageStarted = EditorApplication.timeSinceStartup;
                s_StartupWaitReported = false;
                ReportStartupStage("running", 0);
            }

            private static void PollStartupStage(double stamp)
            {
                if (s_StartupStage == null || s_StartupWaitReported || stamp - s_StartupStageStarted < 15)
                    return;
                s_StartupWaitReported = true;
                ReportStartupStage("waiting", stamp - s_StartupStageStarted);
            }

            private static void FinishStartupStage(string outcome)
            {
                if (s_StartupStage == null)
                    return;
                ReportStartupStage(outcome, EditorApplication.timeSinceStartup - s_StartupStageStarted);
                s_StartupStage = null;
            }

            /// <summary>上报当前启动尝试和阶段耗时，按 Play 会话关联推进结果。</summary>
            private static void ReportStartupStage(string outcome, double seconds)
            {
                ObserveImplementation("startup-stage", outcome, step: s_StartupStage,
                    elapsed: TimeSpan.FromSeconds(seconds), attempt: s_PlayAttempt,
                    sequence: s_StartupSequence);
            }
        }
    }
}
