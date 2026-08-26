/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AgentCapabilitiesWindow.Methods.cs
 * author:    taoye
 * created:   2026/8/25
 * descrip:   AgentCapabilitiesWindow 绘制实现
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public sealed partial class AgentCapabilitiesWindow : EditorWindow
    {
        private void EnsureStyles()
        {
            if (m_MainTitleStyle != null)
                return;

            m_MainTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
            };
            m_SectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
            };
            m_SelectedRowStyle = new GUIStyle("SelectionRect")
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 5, 5),
            };
            m_NormalRowStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 5, 5),
            };
            m_MutedStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                normal = { textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.67f, 0.67f, 0.67f)
                    : new Color(0.35f, 0.35f, 0.35f) },
            };
            m_WrappedStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            m_PanelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(4, 4, 4, 4),
            };
        }

        private void DrawMainTitle()
        {
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Label(c_MainTitle, m_MainTitleStyle, false, GUILayout.ExpandWidth(true));
            EditorUtil.Draw.Space(2f);
            EditorUtil.Draw.Label(
                "集中查看项目组 Skills、固定 C# Actions 与 Nova MCP 开放状态",
                m_MutedStyle,
                false,
                GUILayout.ExpandWidth(true));
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Line();
        }

        private void DrawSummary()
        {
            if (!string.IsNullOrEmpty(m_LoadError))
            {
                EditorUtil.Draw.HelpBox(MessageType.Error, new[] { m_LoadError }, false);
                return;
            }
            if (m_Snapshot == null)
                return;

            int notExposed = m_Snapshot.Actions.Count(action => !action.IsInMcpPolicy);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                DrawSummaryCard("Framework", string.IsNullOrEmpty(m_Snapshot.FrameworkVersion) ? "未知" : m_Snapshot.FrameworkVersion);
                DrawSummaryCard("Skills", m_Snapshot.Skills.Count.ToString());
                DrawSummaryCard("Actions", m_Snapshot.Actions.Count.ToString());
                DrawSummaryCard("MCP 可调用", m_Snapshot.McpExposedActionCount.ToString());
                DrawSummaryCard("暂未开放", notExposed.ToString());
                DrawSummaryCard("Issues", m_Snapshot.Issues.Count.ToString());
            });

            if (!m_Snapshot.McpAvailable)
            {
                EditorUtil.Draw.HelpBox(MessageType.Warning,
                    new[] { "Nova MCP 开放策略当前不可用；窗口仍会展示 Skills 与已注册 Actions。" }, false);
            }
        }

        private void DrawSummaryCard(string label, string value)
        {
            EditorGUILayout.BeginVertical(m_PanelStyle, GUILayout.MinWidth(110f), GUILayout.Height(48f));
            GUILayout.Label(label, m_MutedStyle);
            GUILayout.Label(value, EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(8f);
                EditorUtil.Draw.Label("搜索", EditorStyles.boldLabel, false, GUILayout.Width(40f));
                string search = EditorUtil.Draw.TextField(m_SearchText ?? string.Empty, false, GUILayout.Width(320f));
                if (!string.Equals(search, m_SearchText, StringComparison.Ordinal))
                {
                    m_SearchText = search;
                    ApplyFilter();
                }
                GUILayout.FlexibleSpace();
                EditorUtil.Draw.Label("只读能力视图", m_MutedStyle, false);
                EditorUtil.Draw.Space(8f);
                EditorUtil.Draw.Button("刷新", 90f, false, ScheduleRefresh);
                EditorUtil.Draw.Space(8f);
            });
            EditorUtil.Draw.Space(4f);
            EditorUtil.Draw.Line();
        }

        private void DrawNavigation()
        {
            EditorGUILayout.BeginVertical(m_PanelStyle, GUILayout.Width(c_NavigationWidth), GUILayout.ExpandHeight(true));
            GUILayout.Label("能力分类", m_SectionTitleStyle);
            m_NavigationScroll = EditorGUILayout.BeginScrollView(m_NavigationScroll);
            DrawFilterButton(CapabilityFilter.All, "全部能力",
                (m_Snapshot?.Skills.Count ?? 0) + (m_Snapshot?.Actions.Count ?? 0));
            DrawFilterButton(CapabilityFilter.Skills, "Skills", m_Snapshot?.Skills.Count ?? 0);
            DrawFilterButton(CapabilityFilter.Actions, "Actions", m_Snapshot?.Actions.Count ?? 0);
            DrawFilterButton(CapabilityFilter.McpAvailable, "MCP 可调用", m_Snapshot?.McpExposedActionCount ?? 0);
            DrawFilterButton(CapabilityFilter.NotExposed, "暂未开放",
                m_Snapshot?.Actions.Count(action => !action.IsInMcpPolicy) ?? 0);
            DrawFilterButton(CapabilityFilter.Issues, "配置异常", m_Snapshot?.Issues.Count ?? 0);

            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Line();
            GUILayout.Label("Skill 业务模块", m_SectionTitleStyle);
            if (GUILayout.Toggle(string.IsNullOrEmpty(m_SelectedGroup), "全部模块", "Button"))
                SetGroup(null);
            if (m_Snapshot != null)
            {
                foreach (string group in m_Snapshot.Skills.SelectMany(skill => skill.Groups)
                             .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
                {
                    int count = m_Snapshot.Skills.Count(skill => skill.Groups.Contains(group));
                    if (GUILayout.Toggle(string.Equals(m_SelectedGroup, group, StringComparison.Ordinal),
                            $"{group}  ({count})", "Button"))
                        SetGroup(group);
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawFilterButton(CapabilityFilter filter, string label, int count)
        {
            if (GUILayout.Toggle(m_Filter == filter, $"{label}  ({count})", "Button"))
            {
                if (m_Filter != filter)
                {
                    m_Filter = filter;
                    m_SelectedGroup = null;
                    ApplyFilter();
                }
            }
        }

        private void SetGroup(string group)
        {
            if (string.Equals(m_SelectedGroup, group, StringComparison.Ordinal))
                return;
            m_SelectedGroup = group;
            m_Filter = CapabilityFilter.Skills;
            ApplyFilter();
        }

        private void DrawCapabilityList()
        {
            EditorGUILayout.BeginVertical(m_PanelStyle, GUILayout.Width(c_ListWidth), GUILayout.ExpandHeight(true));
            GUILayout.Label($"能力列表（{m_FilteredItems.Count}）", m_SectionTitleStyle);
            m_ListScroll = EditorGUILayout.BeginScrollView(m_ListScroll);
            foreach (CapabilityItem item in m_FilteredItems)
            {
                bool selected = ReferenceEquals(item, m_SelectedItem);
                GUIStyle style = selected ? m_SelectedRowStyle : m_NormalRowStyle;
                if (GUILayout.Button(new GUIContent(item.Title, item.Subtitle), style, GUILayout.Height(28f)))
                    m_SelectedItem = item;
                GUILayout.Label($"{KindLabel(item.Kind)} · {item.Status}", m_MutedStyle);
                EditorUtil.Draw.Line();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetails()
        {
            EditorGUILayout.BeginVertical(m_PanelStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label("详细说明", m_SectionTitleStyle);
            m_DetailScroll = EditorGUILayout.BeginScrollView(m_DetailScroll);
            if (m_SelectedItem == null)
            {
                GUILayout.Label("请选择左侧能力条目。", m_MutedStyle);
            }
            else if (m_SelectedItem.Skill != null)
            {
                DrawSkillDetails(m_SelectedItem.Skill);
            }
            else if (m_SelectedItem.Action != null)
            {
                DrawActionDetails(m_SelectedItem.Action);
            }
            else if (m_SelectedItem.Issue != null)
            {
                DrawIssueDetails(m_SelectedItem.Issue);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSkillDetails(EditorUtil.AgentCapabilities.SkillCapability skill)
        {
            GUILayout.Label(skill.Id, m_SectionTitleStyle);
            GUILayout.Label(skill.Description, m_WrappedStyle);
            EditorUtil.Draw.Space(8f);
            DrawKeyValue("类型", skill.Kind);
            DrawKeyValue("发布状态", skill.Status);
            DrawKeyValue("Agent 状态", skill.ProjectionMessage);
            DrawKeyValue("业务模块", Join(skill.Groups));
            DrawKeyValue("任务场景", Join(skill.Journeys));
            DrawKeyValue("影响范围", Join(skill.Effects));
            DrawKeyValue("最低验证", skill.MinimumEvidence);
            DrawSection("输入", skill.Inputs);
            DrawSection("验收证据", skill.Evidence);
            DrawSection("确认规则", new[] { skill.ConfirmationRule });

            GUILayout.Label("执行适配器", m_SectionTitleStyle);
            foreach (EditorUtil.AgentCapabilities.ActionAdapter adapter in skill.Adapters)
            {
                EditorGUILayout.BeginVertical(m_PanelStyle);
                DrawKeyValue("类型", adapter.Kind);
                DrawKeyValue("入口", adapter.Entry);
                DrawKeyValue("用途", adapter.When);
                EditorGUILayout.EndVertical();
            }

            EditorUtil.Draw.Space(6f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Button("定位 SKILL.md", 120f, false,
                    () => EditorUtility.RevealInFinder(skill.SkillFilePath));
                EditorUtil.Draw.Button("定位 contract.json", 140f, false,
                    () => EditorUtility.RevealInFinder(skill.ContractFilePath));
            });
        }

        private void DrawActionDetails(EditorUtil.AgentCapabilities.ActionCapability action)
        {
            AgentActionDescriptor descriptor = action.Descriptor;
            GUILayout.Label(descriptor.DisplayName, m_SectionTitleStyle);
            GUILayout.Label(descriptor.Description, m_WrappedStyle);
            EditorUtil.Draw.Space(8f);
            DrawKeyValue("Action ID", descriptor.Id);
            DrawKeyValue("业务模块", descriptor.Domain);
            DrawKeyValue("操作类型", descriptor.OperationType.ToString());
            DrawKeyValue("MCP 状态", action.IsMcpExposed
                ? "已开放，可由 Agent 调用"
                : action.IsInMcpPolicy ? "已列入开放策略，但当前校验失败" : "尚未向 MCP 开放");
            DrawKeyValue("副作用", descriptor.Effects.ToString());
            DrawKeyValue("幂等语义", descriptor.Idempotency.ToString());
            DrawKeyValue("需要确认", descriptor.RequiresConfirmation ? "是" : "否");
            DrawKeyValue("需要稳定 Editor", descriptor.RequiresStableEditor ? "是" : "否");
            DrawKeyValue("只允许 Edit Mode", descriptor.RequiresEditMode ? "是" : "否");
            DrawKeyValue("契约版本", descriptor.ContractMajor.ToString());
            DrawKeyValue("验证证据", descriptor.RequiredEvidence.ToString());
            DrawSection("资源锁", descriptor.Locks);
            DrawSection("关联 Skills", action.SkillIds);
            DrawSection("等待开放的 Skills", action.BlockedSkillIds);

            GUILayout.Label("请求参数 Schema", m_SectionTitleStyle);
            EditorGUILayout.TextArea(descriptor.RequestSchemaJson ?? string.Empty, m_WrappedStyle,
                GUILayout.MinHeight(120f));
        }

        private void DrawIssueDetails(EditorUtil.AgentCapabilities.CapabilityIssue issue)
        {
            GUILayout.Label(issue.Source, m_SectionTitleStyle);
            EditorGUILayout.HelpBox(issue.Message, MessageType.Error);
        }

        private void DrawKeyValue(string label, string value)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(110f));
                GUILayout.Label(string.IsNullOrWhiteSpace(value) ? "—" : value, m_WrappedStyle);
            });
        }

        private void DrawSection(string title, IEnumerable<string> values)
        {
            string[] items = (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            GUILayout.Label(title, m_SectionTitleStyle);
            if (items.Length == 0)
            {
                GUILayout.Label("—", m_MutedStyle);
                return;
            }
            foreach (string item in items)
                GUILayout.Label("• " + item, m_WrappedStyle);
        }

        private static string Join(IEnumerable<string> values)
        {
            return string.Join("、", values ?? Array.Empty<string>());
        }

        private static string KindLabel(CapabilityItemKind kind)
        {
            switch (kind)
            {
                case CapabilityItemKind.Skill: return "Skill";
                case CapabilityItemKind.Action: return "Action";
                default: return "Issue";
            }
        }
    }
}
