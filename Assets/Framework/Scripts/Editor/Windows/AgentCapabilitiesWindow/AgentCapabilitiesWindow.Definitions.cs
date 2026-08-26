/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AgentCapabilitiesWindow.Definitions.cs
 * author:    taoye
 * created:   2026/8/25
 * descrip:   AgentCapabilitiesWindow 状态与展示模型
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public sealed partial class AgentCapabilitiesWindow : EditorWindow
    {
        private const string c_MenuPath = "Nova/Open Agent Capabilities View";
        private const string c_WindowTitle = "Nova · Agent Capabilities";
        private const string c_MainTitle = "Agent Capabilities 能力中心";
        private const float c_WindowMinWidth = 1100f;
        private const float c_WindowMinHeight = 620f;
        private const float c_NavigationWidth = 190f;
        private const float c_ListWidth = 390f;

        private EditorUtil.AgentCapabilities.Snapshot m_Snapshot;
        private readonly List<CapabilityItem> m_Items = new List<CapabilityItem>();
        private List<CapabilityItem> m_FilteredItems = new List<CapabilityItem>();
        private CapabilityItem m_SelectedItem;
        private CapabilityFilter m_Filter;
        private string m_SelectedGroup;
        private string m_SearchText = string.Empty;
        private string m_LoadError;
        private bool m_RefreshScheduled;
        private Vector2 m_NavigationScroll;
        private Vector2 m_ListScroll;
        private Vector2 m_DetailScroll;
        private GUIStyle m_MainTitleStyle;
        private GUIStyle m_SectionTitleStyle;
        private GUIStyle m_SelectedRowStyle;
        private GUIStyle m_NormalRowStyle;
        private GUIStyle m_MutedStyle;
        private GUIStyle m_WrappedStyle;
        private GUIStyle m_PanelStyle;

        private enum CapabilityFilter
        {
            All,
            Skills,
            Actions,
            McpAvailable,
            NotExposed,
            Issues,
        }

        private enum CapabilityItemKind
        {
            Skill,
            Action,
            Issue,
        }

        private sealed class CapabilityItem
        {
            public CapabilityItemKind Kind;
            public string Key;
            public string Title;
            public string Subtitle;
            public string Status;
            public string SearchText;
            public EditorUtil.AgentCapabilities.SkillCapability Skill;
            public EditorUtil.AgentCapabilities.ActionCapability Action;
            public EditorUtil.AgentCapabilities.CapabilityIssue Issue;

            public static CapabilityItem FromSkill(EditorUtil.AgentCapabilities.SkillCapability skill)
            {
                return new CapabilityItem
                {
                    Kind = CapabilityItemKind.Skill,
                    Key = "skill:" + skill.Id,
                    Title = skill.Id,
                    Subtitle = skill.Description,
                    Status = ProjectionLabel(skill.Projection),
                    SearchText = string.Join(" ", skill.Id, skill.Description, skill.Kind,
                        string.Join(" ", skill.Groups), string.Join(" ", skill.Journeys)),
                    Skill = skill,
                };
            }

            public static CapabilityItem FromAction(EditorUtil.AgentCapabilities.ActionCapability action)
            {
                AgentActionDescriptor descriptor = action.Descriptor;
                return new CapabilityItem
                {
                    Kind = CapabilityItemKind.Action,
                    Key = "action:" + descriptor.Id,
                    Title = descriptor.DisplayName,
                    Subtitle = descriptor.Id,
                    Status = action.IsMcpExposed
                        ? "MCP 可调用"
                        : action.IsInMcpPolicy ? "开放策略异常" : "暂未向 MCP 开放",
                    SearchText = string.Join(" ", descriptor.Id, descriptor.DisplayName,
                        descriptor.Description, descriptor.Domain, descriptor.OperationType.ToString()),
                    Action = action,
                };
            }

            public static CapabilityItem FromIssue(EditorUtil.AgentCapabilities.CapabilityIssue issue)
            {
                return new CapabilityItem
                {
                    Kind = CapabilityItemKind.Issue,
                    Key = "issue:" + issue.Source + ":" + issue.Message,
                    Title = issue.Source,
                    Subtitle = issue.Message,
                    Status = "需要处理",
                    SearchText = issue.Source + " " + issue.Message,
                    Issue = issue,
                };
            }

            private static string ProjectionLabel(EditorUtil.AgentCapabilities.ProjectionStatus status)
            {
                switch (status)
                {
                    case EditorUtil.AgentCapabilities.ProjectionStatus.Current: return "Agent 可发现";
                    case EditorUtil.AgentCapabilities.ProjectionStatus.Missing: return "未投影";
                    case EditorUtil.AgentCapabilities.ProjectionStatus.UpdateAvailable: return "待更新";
                    case EditorUtil.AgentCapabilities.ProjectionStatus.Conflict: return "投影冲突";
                    default: return "状态未知";
                }
            }
        }
    }
}
