/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AgentCapabilitiesWindow.cs
 * author:    taoye
 * created:   2026/8/25
 * descrip:   Nova Agent 能力总览窗口
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 只读展示 Nova Skills、C# Actions 与 MCP 开放状态。
    /// </summary>
    public sealed partial class AgentCapabilitiesWindow : EditorWindow
    {
        /// <summary>
        /// 打开或聚焦 Agent Capabilities 能力总览窗口。
        /// </summary>
        [MenuItem(c_MenuPath, false, 1001)]
        public static void Open()
        {
            AgentCapabilitiesWindow window = GetWindow<AgentCapabilitiesWindow>(false, c_WindowTitle, true);
            window.minSize = new Vector2(c_WindowMinWidth, c_WindowMinHeight);
            window.ScheduleRefresh();
        }

        private void OnEnable()
        {
            EditorUtil.AgentSkills.ProjectionChanged -= ScheduleRefresh;
            EditorUtil.AgentSkills.ProjectionChanged += ScheduleRefresh;
            ScheduleRefresh();
        }

        private void OnDisable()
        {
            EditorUtil.AgentSkills.ProjectionChanged -= ScheduleRefresh;
            EditorApplication.delayCall -= RunScheduledRefresh;
            m_RefreshScheduled = false;
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawMainTitle();
            DrawSummary();
            DrawToolbar();
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                DrawNavigation();
                DrawCapabilityList();
                DrawDetails();
            });
        }

        private void RefreshSnapshot()
        {
            try
            {
                m_Snapshot = EditorUtil.AgentCapabilities.CreateSnapshot();
                m_LoadError = null;
                RebuildItems();
            }
            catch (Exception exception)
            {
                m_Snapshot = null;
                m_Items.Clear();
                m_LoadError = exception.Message;
            }
            Repaint();
        }

        private void ScheduleRefresh()
        {
            if (m_RefreshScheduled)
                return;

            m_RefreshScheduled = true;
            EditorApplication.delayCall += RunScheduledRefresh;
        }

        private void RunScheduledRefresh()
        {
            EditorApplication.delayCall -= RunScheduledRefresh;
            m_RefreshScheduled = false;
            if (this != null)
                RefreshSnapshot();
        }

        private void RebuildItems()
        {
            string selectedKey = m_SelectedItem?.Key;
            m_Items.Clear();
            if (m_Snapshot == null)
                return;

            foreach (EditorUtil.AgentCapabilities.SkillCapability skill in m_Snapshot.Skills)
            {
                m_Items.Add(CapabilityItem.FromSkill(skill));
            }
            foreach (EditorUtil.AgentCapabilities.ActionCapability action in m_Snapshot.Actions)
            {
                m_Items.Add(CapabilityItem.FromAction(action));
            }
            foreach (EditorUtil.AgentCapabilities.CapabilityIssue issue in m_Snapshot.Issues)
            {
                m_Items.Add(CapabilityItem.FromIssue(issue));
            }

            ApplyFilter();
            m_SelectedItem = m_FilteredItems.FirstOrDefault(item => item.Key == selectedKey)
                             ?? m_FilteredItems.FirstOrDefault();
        }

        private void ApplyFilter()
        {
            IEnumerable<CapabilityItem> query = m_Items;
            switch (m_Filter)
            {
                case CapabilityFilter.All:
                    query = query.Where(item => item.Kind != CapabilityItemKind.Issue);
                    break;
                case CapabilityFilter.Skills:
                    query = query.Where(item => item.Kind == CapabilityItemKind.Skill);
                    break;
                case CapabilityFilter.Actions:
                    query = query.Where(item => item.Kind == CapabilityItemKind.Action);
                    break;
                case CapabilityFilter.McpAvailable:
                    query = query.Where(item => item.Action?.IsMcpExposed == true);
                    break;
                case CapabilityFilter.NotExposed:
                    query = query.Where(item => item.Action != null && !item.Action.IsInMcpPolicy);
                    break;
                case CapabilityFilter.Issues:
                    query = query.Where(item => item.Kind == CapabilityItemKind.Issue);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(m_SelectedGroup))
            {
                query = query.Where(item => item.Skill != null && item.Skill.Groups.Contains(m_SelectedGroup));
            }
            if (!string.IsNullOrWhiteSpace(m_SearchText))
            {
                query = query.Where(item => item.SearchText.IndexOf(
                    m_SearchText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            }

            m_FilteredItems = query
                .OrderBy(item => item.Kind)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (m_SelectedItem != null && !m_FilteredItems.Contains(m_SelectedItem))
                m_SelectedItem = m_FilteredItems.FirstOrDefault();
            Repaint();
        }
    }
}
