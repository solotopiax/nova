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

namespace NovaFramework.Editor
{
    /// <summary>
    /// ProjectGuard 的只读展示窗口。全部规则均由 EditorUtil.ProjectGuard 持有。
    /// </summary>
    public sealed class ProjectGuardWindow : EditorWindow
    {
        private const string c_Title = "Nova · 项目检查";
        private const string c_DisplayName = "项目检查";
        private const string c_MenuPath = "Nova/Open Project Guard";
        private EditorUtil.ProjectGuard.NovaGuardReport m_Report;
        private string m_ScopeName;
        private Vector2 m_ScrollPosition;
        private GUIStyle m_MainTitleStyle;

        [MenuItem(c_MenuPath, false, 1000)]
        private static void Open()
        {
            ProjectGuardWindow window = GetWindow<ProjectGuardWindow>(false, c_Title, true);
            window.minSize = new Vector2(700f, 400f);
        }

        /// <summary>
        /// 窗口启用时默认读取当前场景的只读检查结果。
        /// </summary>
        private void OnEnable()
        {
            ShowReport(EditorUtil.ProjectGuard.ValidateQuick(), "当前编辑场景及其所在目录下的资源");
        }

        /// <summary>
        /// 使用 Nova 统一 IMGUI 绘制，避免 UI Toolkit 长中文 HelpBox 随机缺字。
        /// </summary>
        private void OnGUI()
        {
            EnsureStyles();
            DrawMainTitle();
            DrawToolbar();
            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
            DrawReport();
            EditorGUILayout.EndScrollView();
        }

        private void EnsureStyles()
        {
            if (m_MainTitleStyle != null)
                return;

            m_MainTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
            };
        }

        private void DrawMainTitle()
        {
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Label(c_DisplayName, m_MainTitleStyle, false, GUILayout.ExpandWidth(true));
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Line();
        }

        private void DrawToolbar()
        {
            EditorUtil.Draw.Space(6f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                GUILayout.FlexibleSpace();
                EditorUtil.Draw.Button("检查当前场景", 140f, false, () => RunCheck(
                    EditorUtil.ProjectGuard.ValidateQuick(), "当前编辑场景及其所在目录下的资源"));
                EditorUtil.Draw.Button("检查构建场景", 140f, false, () => RunCheck(
                    EditorUtil.ProjectGuard.ValidateBuild(EditorUserBuildSettings.activeBuildTarget),
                    "已启用构建场景及其所在目录下的资源"));
                EditorUtil.Draw.Space(8f);
            });
            EditorUtil.Draw.Space(6f);
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
            m_Report = report;
            m_ScopeName = scopeName;
            Repaint();
        }

        private void DrawReport()
        {
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1) 检查 Nova 的启动配置、场景结构和 Resources 资源。",
                "(2) 此窗口只显示方便处理的摘要，不会修改项目。",
                "(3) 点击检查按钮后，完整技术诊断会输出到 Console / Editor.log。",
            }, false, GUILayout.ExpandWidth(true));

            if (m_Report == null)
            {
                EditorUtil.Draw.HelpBox(MessageType.Error, new[]
                {
                    "(1) 状态：检查未完成。",
                    "(2) 请主动点击检查按钮后，查看 Console / Editor.log 获取完整技术诊断。",
                }, false, GUILayout.ExpandWidth(true));
                return;
            }

            int errorCount = 0;
            int warningCount = 0;
            foreach (EditorUtil.ProjectGuard.NovaGuardIssue issue in m_Report.Issues)
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

            if (m_Report.Issues.Count == 0)
            {
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    $"(1) 检查范围：{m_ScopeName}",
                    "(2) 检查完成：未发现需要处理的问题。",
                }, false, GUILayout.ExpandWidth(true));
                return;
            }

            MessageType summaryType = errorCount > 0
                ? MessageType.Error
                : warningCount > 0 ? MessageType.Warning : MessageType.Info;
            EditorUtil.Draw.HelpBox(summaryType, new[]
            {
                $"(1) 检查范围：{m_ScopeName}",
                $"(2) {BuildReportSummary(errorCount, warningCount)}",
            }, false, GUILayout.ExpandWidth(true));

            foreach (EditorUtil.ProjectGuard.NovaGuardIssue issue in m_Report.Issues)
            {
                MessageType type = issue.Severity switch
                {
                    EditorUtil.ProjectGuard.NovaGuardSeverity.Error => MessageType.Error,
                    EditorUtil.ProjectGuard.NovaGuardSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info,
                };
                EditorUtil.Draw.HelpBox(type, BuildIssueDisplayMessage(issue)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), false,
                    GUILayout.ExpandWidth(true));
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
