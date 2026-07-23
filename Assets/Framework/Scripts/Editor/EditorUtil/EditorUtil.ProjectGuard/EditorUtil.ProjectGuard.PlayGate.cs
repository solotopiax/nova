/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ProjectGuard.PlayGate.cs
 * author:    taoye
 * created:   2026/7/15
 * descrip:   Nova 项目规范守卫 Play 门禁
 ***************************************************************/

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class ProjectGuard
        {
            [InitializeOnLoad]
            private static class PlayGate
            {
                static PlayGate()
                {
                    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                }

                private static void OnPlayModeStateChanged(PlayModeStateChange state)
                {
                    if (state != PlayModeStateChange.ExitingEditMode)
                        return;

                    NovaGuardReport report = ValidatePlay();
                    if (!ShouldBlockPlayForDiagnostics(report))
                        return;

                    EditorApplication.isPlaying = false;
                    foreach (NovaGuardIssue issue in report.Issues.Where(issue =>
                                 issue.Severity == NovaGuardSeverity.Error))
                    {
                        Debug.LogError($"[{issue.RuleId}] {issue.Message} {issue.AssetPath}");
                    }
                }
            }

            private static bool ShouldBlockPlayForDiagnostics(NovaGuardReport report)
            {
                return report != null && report.HasErrors;
            }
        }
    }
}
