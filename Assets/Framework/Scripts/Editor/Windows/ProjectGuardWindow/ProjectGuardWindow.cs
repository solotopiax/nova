/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProjectGuardWindow.cs
 * author:    taoye
 * created:   2026/7/15
 * descrip:   Nova 项目检查窗口
 ***************************************************************/

using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaFramework.Editor
{
    /// <summary>
    /// ProjectGuard 的只读展示窗口。全部规则均由 EditorUtil.ProjectGuard 持有。
    /// </summary>
    public sealed class ProjectGuardWindow : EditorWindow
    {
        private const string c_Title = "Nova · 项目检查";
        private const string c_DisplayName = "项目检查";
        private const string c_MenuPath = "Nova/Open ProjectGuard";
        private VisualElement m_Results;

        [MenuItem(c_MenuPath)]
        private static void Open()
        {
            GetWindow<ProjectGuardWindow>(false, c_Title, true);
        }

        /// <summary>
        /// 创建项目检查窗口，并默认展示当前场景的只读检查结果。
        /// </summary>
        public void CreateGUI()
        {
            var toolbar = new VisualElement
            {
                name = "project-guard-toolbar",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 6,
                    paddingBottom = 6,
                },
            };
            toolbar.Add(new Label(c_DisplayName)
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft,
                },
            });
            var actions = new VisualElement
            {
                name = "project-guard-actions",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginLeft = new StyleLength(StyleKeyword.Auto),
                },
            };
            actions.Add(new Button(() => RunCheck(
                EditorUtil.ProjectGuard.ValidateQuick(), "当前编辑场景及其所在目录下的资源"))
            {
                text = "检查当前场景",
                tooltip = "检查当前正在编辑的场景，以及该场景所在目录下的资源。",
            });
            actions.Add(new Button(() => RunCheck(EditorUtil.ProjectGuard.ValidateBuild(
                EditorUserBuildSettings.activeBuildTarget), "已启用构建场景及其所在目录下的资源"))
            {
                text = "检查构建场景",
                tooltip = "检查 Build Settings 中已启用的场景，以及这些场景所在目录下的资源；不会实际构建项目。",
            });
            toolbar.Add(actions);
            rootVisualElement.Add(toolbar);

            m_Results = new ScrollView();
            rootVisualElement.Add(m_Results);
            ShowReport(EditorUtil.ProjectGuard.ValidateQuick(), "当前编辑场景及其所在目录下的资源");
        }

        /// <summary>
        /// 执行用户主动发起的检查，并把完整技术诊断写入 Console 与 Editor.log。
        /// </summary>
        /// <param name="report">本次检查报告。</param>
        /// <param name="scopeName">面板展示的检查范围名称。</param>
        private void RunCheck(EditorUtil.ProjectGuard.NovaGuardReport report, string scopeName)
        {
            ShowReport(report, scopeName);
            LogFullReport(report);
        }

        /// <summary>
        /// 以面向项目成员的摘要形式展示检查结果，不在窗口中堆叠原始技术诊断。
        /// </summary>
        /// <param name="report">本次检查报告。</param>
        /// <param name="scopeName">面板展示的检查范围名称。</param>
        private void ShowReport(EditorUtil.ProjectGuard.NovaGuardReport report, string scopeName)
        {
            m_Results?.Clear();
            if (m_Results == null)
                return;

            m_Results.Add(new HelpBox(
                "(1) 检查 Nova 的启动配置、场景结构和 Resources 资源。\n" +
                "(2) 此窗口只显示方便处理的摘要，不会修改项目。\n" +
                "(3) 点击检查按钮后，完整技术诊断会输出到 Console / Editor.log。",
                HelpBoxMessageType.Info));

            if (report == null)
            {
                m_Results.Add(new HelpBox(
                    "(1) 状态：检查未完成。\n(2) 请主动点击检查按钮后，查看 Console / Editor.log 获取完整技术诊断。",
                    HelpBoxMessageType.Error));
                return;
            }

            int errorCount = 0;
            int warningCount = 0;
            foreach (EditorUtil.ProjectGuard.NovaGuardIssue issue in report.Issues)
            {
                switch (issue.Severity)
                {
                    case EditorUtil.ProjectGuard.NovaGuardSeverity.Error:
                        errorCount++;
                        break;
                    case EditorUtil.ProjectGuard.NovaGuardSeverity.Warning:
                        warningCount++;
                        break;
                }
            }

            if (report.Issues.Count == 0)
            {
                m_Results.Add(new HelpBox($"(1) 检查范围：{scopeName}\n(2) 检查完成：未发现需要处理的问题。",
                    HelpBoxMessageType.Info));
                return;
            }

            HelpBoxMessageType summaryType = errorCount > 0
                ? HelpBoxMessageType.Error
                : warningCount > 0 ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
            m_Results.Add(new HelpBox($"(1) 检查范围：{scopeName}\n(2) {BuildReportSummary(errorCount, warningCount)}", summaryType));

            foreach (EditorUtil.ProjectGuard.NovaGuardIssue issue in report.Issues)
            {
                HelpBoxMessageType type = issue.Severity switch
                {
                    EditorUtil.ProjectGuard.NovaGuardSeverity.Error => HelpBoxMessageType.Error,
                    EditorUtil.ProjectGuard.NovaGuardSeverity.Warning => HelpBoxMessageType.Warning,
                    _ => HelpBoxMessageType.Info,
                };
                m_Results.Add(new HelpBox(BuildIssueDisplayMessage(issue), type));
            }
        }

        /// <summary>
        /// 将用户主动触发检查得到的完整报告写入 Console，以便在 Editor.log 中查看完整诊断。
        /// </summary>
        /// <param name="report">本次检查报告。</param>
        private static void LogFullReport(EditorUtil.ProjectGuard.NovaGuardReport report)
        {
            if (report == null || report.Issues.Count == 0)
                return;

            foreach (EditorUtil.ProjectGuard.NovaGuardIssue issue in report.Issues)
            {
                string location = string.IsNullOrEmpty(issue.AssetPath) ? string.Empty : $"\n检查位置：{issue.AssetPath}";
                string message = $"[ProjectGuard][{issue.RuleId}] {issue.Message}{location}";
                switch (issue.Severity)
                {
                    case EditorUtil.ProjectGuard.NovaGuardSeverity.Error:
                        Debug.LogError(message);
                        break;
                    case EditorUtil.ProjectGuard.NovaGuardSeverity.Warning:
                        Debug.LogWarning(message);
                        break;
                    default:
                        Debug.Log(message);
                        break;
                }
            }
        }

        /// <summary>
        /// 根据错误与提醒数量生成项目成员可直接理解的检查摘要。
        /// </summary>
        /// <param name="errorCount">需要处理的错误数量。</param>
        /// <param name="warningCount">需要确认的提醒数量。</param>
        /// <returns>检查摘要。</returns>
        private static string BuildReportSummary(int errorCount, int warningCount)
        {
            if (errorCount > 0 && warningCount > 0)
                return $"发现 {errorCount} 个需要处理的问题，{warningCount} 个需要确认的提醒。请先处理“需要处理”的项目，再确认“请确认”的项目。";

            if (errorCount > 0)
                return $"发现 {errorCount} 个需要处理的问题。请按下方说明逐项处理。";

            if (warningCount > 0)
                return $"没有会阻止启动的问题，但有 {warningCount} 个提醒需要确认。";

            return "检查完成：只有提示信息，不需要处理。";
        }

        /// <summary>
        /// 将规则提供的前两条用户说明编号展示；完整技术诊断只保留在日志中。
        /// </summary>
        /// <param name="issue">当前规则问题。</param>
        /// <returns>面板展示文本。</returns>
        private static string BuildIssueDisplayMessage(EditorUtil.ProjectGuard.NovaGuardIssue issue)
        {
            string[] messageLines = GetFirstTwoMessageLines(issue.Message);
            string problem = string.IsNullOrEmpty(messageLines[0]) ? "未提供问题说明。" : messageLines[0];
            string action = string.IsNullOrEmpty(messageLines[1])
                ? "请查看 Console / Editor.log 获取完整技术诊断。"
                : messageLines[1];
            var builder = new StringBuilder();
            builder.Append("(1) 状态：")
                .Append(GetSeverityText(issue.Severity))
                .Append('（')
                .Append(issue.RuleId)
                .Append("）\n(2) ")
                .Append(problem)
                .Append("\n(3) ")
                .Append(action);

            string location = GetIssueLocation(issue.RuleId, issue.AssetPath);
            if (!string.IsNullOrEmpty(location))
                builder.Append("\n(4) ").Append(location);
            return builder.ToString();
        }

        /// <summary>
        /// 从规则消息中取前两条有效说明，避免窗口层解释或匹配规则内部诊断。
        /// </summary>
        /// <param name="message">规则提供的完整消息。</param>
        /// <returns>最多两条用户说明；未提供的位置为空字符串。</returns>
        private static string[] GetFirstTwoMessageLines(string message)
        {
            var result = new string[2];
            if (string.IsNullOrWhiteSpace(message))
                return result;

            string[] lines = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int resultIndex = 0;
            for (int i = 0; i < lines.Length && resultIndex < result.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                result[resultIndex++] = line;
            }

            return result;
        }

        /// <summary>
        /// 返回规则问题在面板中需要展示的可操作位置；配置导出物路径仅保留在日志中。
        /// </summary>
        /// <param name="ruleId">规则编号。</param>
        /// <param name="assetPath">规则记录的原始资产路径。</param>
        /// <returns>带语义标签的位置文本。</returns>
        private static string GetIssueLocation(string ruleId, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || ruleId.StartsWith("NOVA-CONFIG", StringComparison.Ordinal))
                return string.Empty;

            if (ruleId.StartsWith("NOVA-SCENE", StringComparison.Ordinal))
                return $"场景：{assetPath}";

            if (string.Equals(ruleId, "NOVA-RES-001", StringComparison.Ordinal))
                return $"资源：{assetPath}";

            return $"检查位置：{assetPath}";
        }

        /// <summary>
        /// 将规则严重性转成面向项目成员的状态文本。
        /// </summary>
        /// <param name="severity">规则严重性。</param>
        /// <returns>状态文本。</returns>
        private static string GetSeverityText(EditorUtil.ProjectGuard.NovaGuardSeverity severity)
        {
            switch (severity)
            {
                case EditorUtil.ProjectGuard.NovaGuardSeverity.Error: return "需要处理";
                case EditorUtil.ProjectGuard.NovaGuardSeverity.Warning: return "请确认";
                default: return "提示";
            }
        }
    }
}
