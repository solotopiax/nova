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
using System.Text;
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

                    ShowPlayBlockedDialog(report);
                }
            }

            private static bool ShouldBlockPlayForDiagnostics(NovaGuardReport report)
            {
                return report != null && report.HasErrors;
            }

            /// <summary>
            /// 显示 Play 阻断弹窗；配置错误时可直接打开对应 ConfigWindow 配置来源。
            /// </summary>
            private static void ShowPlayBlockedDialog(NovaGuardReport report)
            {
                bool hasConfigErrors = report.Issues.Any(issue =>
                    issue.Severity == NovaGuardSeverity.Error &&
                    issue.RuleId.StartsWith("NOVA-CONFIG", System.StringComparison.Ordinal));
                if (!hasConfigErrors)
                {
                    EditorUtility.DisplayDialog(
                        "Nova 启动检查未通过",
                        BuildPlayBlockedDialogMessage(report),
                        "知道了");
                    return;
                }

                bool openConfig = EditorUtility.DisplayDialog(
                    "Nova 启动配置未就绪",
                    BuildPlayBlockedDialogMessage(report),
                    "打开 Config",
                    "取消启动");
                if (openConfig)
                {
                    OpenLastConfigSource();
                }
            }

            /// <summary>
            /// 构建 Play 阻断弹窗文本，仅保留字段异常和配置入口；完整诊断由 Console 与 Editor.log 输出。
            /// </summary>
            private static string BuildPlayBlockedDialogMessage(NovaGuardReport report)
            {
                var builder = new StringBuilder("已阻止进入 Play Mode。请修正以下错误并重新导出后再启动：\n");
                foreach (NovaGuardIssue issue in report?.Issues.Where(item =>
                             item.Severity == NovaGuardSeverity.Error) ?? Enumerable.Empty<NovaGuardIssue>())
                {
                    string summary = string.Join("\n", issue.Message.Split('\n').Take(2));
                    builder.Append("\n[").Append(issue.RuleId).Append("] ")
                        .Append(summary)
                        .Append('\n');
                }
                return builder.ToString();
            }

            /// <summary>
            /// 测试入口：构建 Play 阻断弹窗文本，不显示真实弹窗。
            /// </summary>
            private static string BuildPlayBlockedDialogMessageForDiagnostics(NovaGuardReport report)
                => BuildPlayBlockedDialogMessage(report);
        }
    }
}
