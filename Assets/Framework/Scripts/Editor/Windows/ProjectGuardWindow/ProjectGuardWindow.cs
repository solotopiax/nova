/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProjectGuardWindow.cs
 * author:    taoye
 * created:   2026/7/15
 * descrip:   Nova 项目规范守卫窗口
 ***************************************************************/

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
        private const string c_Title = "Nova · ProjectGuard";
        private const string c_DisplayName = "项目规范守卫";
        private const string c_MenuPath = "Nova/Open ProjectGuard";
        private VisualElement m_Results;

        [MenuItem(c_MenuPath)]
        private static void Open()
        {
            GetWindow<ProjectGuardWindow>(false, c_Title, true);
        }

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
            actions.Add(new Button(() => ShowReport(EditorUtil.ProjectGuard.ValidateQuick()))
            {
                text = "Quick Validate",
            });
            actions.Add(new Button(() => ShowReport(EditorUtil.ProjectGuard.ValidateBuild(
                EditorUserBuildSettings.activeBuildTarget)))
            {
                text = "Build Validate",
            });
            toolbar.Add(actions);
            rootVisualElement.Add(toolbar);

            m_Results = new ScrollView();
            rootVisualElement.Add(m_Results);
            ShowReport(EditorUtil.ProjectGuard.ValidateQuick());
        }

        private void ShowReport(EditorUtil.ProjectGuard.NovaGuardReport report)
        {
            m_Results?.Clear();
            if (m_Results == null)
                return;

            if (report.Issues.Count == 0)
            {
                m_Results.Add(new HelpBox("当前范围未发现 Nova 结构问题。", HelpBoxMessageType.Info));
                return;
            }

            foreach (EditorUtil.ProjectGuard.NovaGuardIssue issue in report.Issues)
            {
                HelpBoxMessageType type = issue.Severity switch
                {
                    EditorUtil.ProjectGuard.NovaGuardSeverity.Error => HelpBoxMessageType.Error,
                    EditorUtil.ProjectGuard.NovaGuardSeverity.Warning => HelpBoxMessageType.Warning,
                    _ => HelpBoxMessageType.Info,
                };
                string path = string.IsNullOrEmpty(issue.AssetPath) ? string.Empty : $"\n{issue.AssetPath}";
                m_Results.Add(new HelpBox($"[{issue.RuleId}] {issue.Message}{path}", type));
            }
        }
    }
}
