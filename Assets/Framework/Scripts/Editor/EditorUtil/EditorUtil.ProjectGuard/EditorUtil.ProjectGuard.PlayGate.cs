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
