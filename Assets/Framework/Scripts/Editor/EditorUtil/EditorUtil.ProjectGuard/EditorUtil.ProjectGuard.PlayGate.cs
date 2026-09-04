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
                    "打开配置",
                    "取消启动");
                if (openConfig)
                {
                    OpenLastConfigSource();
                }
            }

            /// <summary>
            /// 构建面向普通项目成员的 Play 阻断提示；字段名、类型全名与资产路径只保留在 Console。
            /// </summary>
            private static string BuildPlayBlockedDialogMessage(NovaGuardReport report)
            {
                bool hasConfigErrors = report?.Issues.Any(item =>
                    item.Severity == NovaGuardSeverity.Error &&
                    item.RuleId.StartsWith("NOVA-CONFIG", System.StringComparison.Ordinal)) == true;
                var builder = new StringBuilder(hasConfigErrors
                    ? "游戏暂时无法启动，因为配置还没有准备好。\n\n需要处理：\n"
                    : "游戏暂时无法启动，因为启动检查发现以下问题：\n\n");
                foreach (NovaGuardIssue issue in report?.Issues.Where(item =>
                             item.Severity == NovaGuardSeverity.Error) ?? Enumerable.Empty<NovaGuardIssue>())
                {
                    builder.Append("• ").Append(BuildUserFacingIssueSummary(issue)).Append('\n');
                }
                if (hasConfigErrors)
                    builder.Append("\n点击“打开配置”，确认对应页面后依次点击“保存”和“导出”，再重新启动游戏。");
                else
                    builder.Append("\n请修正以上问题后重新启动游戏。");
                builder.Append("\n如需查看技术详情，请打开 Console。");
                return builder.ToString();
            }

            /// <summary>
            /// 把 Guard 技术诊断收敛为用户可执行的问题摘要。
            /// </summary>
            private static string BuildUserFacingIssueSummary(NovaGuardIssue issue)
            {
                string message = issue?.Message ?? string.Empty;
                string explicitSummary = message.Split('\n')
                    .FirstOrDefault(line => line.StartsWith("用户提示：", System.StringComparison.Ordinal));
                if (!string.IsNullOrEmpty(explicitSummary))
                    return explicitSummary.Substring("用户提示：".Length);
                if (message.Contains("EnabledSDKConfigs"))
                    return "已启用的 SDK 配置还没有同步到游戏。";
                if (message.Contains("EnabledKitConfigs"))
                    return "已启用的功能配置还没有同步到游戏。";
                if (message.Contains("PrivacyConfigs"))
                    return "隐私配置已修改，但还没有同步到游戏。";
                if (message.Contains("AppConfigs"))
                    return "应用配置已修改，但还没有同步到游戏。";
                if (message.Contains("Namespace"))
                    return "代码命名空间配置不完整或尚未同步。";

                string firstLine = message.Split('\n').FirstOrDefault();
                return string.IsNullOrWhiteSpace(firstLine)
                    ? "存在一项尚未完成的启动配置。"
                    : firstLine.Replace("启动配置未准备好：", string.Empty)
                        .Replace("配置异常：", string.Empty);
            }

            /// <summary>
            /// 测试入口：构建 Play 阻断弹窗文本，不显示真实弹窗。
            /// </summary>
            private static string BuildPlayBlockedDialogMessageForDiagnostics(NovaGuardReport report)
                => BuildPlayBlockedDialogMessage(report);
        }
    }
}
