/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.LeftTree.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   ConfigWindow 左侧树面板（环境检测 / 通用配置 / Kit 配置 / SDK 配置 四组嵌套）
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 左树组标题 GUIStyle（粗体，选中组加 accent 色）。
        /// </summary>
        private GUIStyle m_TreeGroupLabelStyle;

        /// <summary>
        /// 左树组标题 GUIStyle（选中态，带底色 accent）。
        /// </summary>
        private GUIStyle m_TreeGroupLabelSelectedStyle;

        /// <summary>
        /// 左树二级节点 GUIStyle（普通态）。
        /// </summary>
        private GUIStyle m_TreeItemStyle;

        /// <summary>
        /// 左树二级节点 GUIStyle（选中态）。
        /// </summary>
        private GUIStyle m_TreeItemSelectedStyle;

        /// <summary>
        /// 绘制左侧树：环境检测、通用配置、SDK 配置三个一级组（可折叠）；未绑 Master 时仅显示环境检测组并提示创建。
        /// </summary>
        private void DrawLeftTree()
        {
            EnsureTreeStyles();
            EditorUtil.Draw.Layout.Vertical(EditorStyles.helpBox, () =>
            {
                m_LeftScrollPos = EditorGUILayout.BeginScrollView(m_LeftScrollPos);

                DrawLeftTreeGroup(LeftTreeGroup.Environment, "环境检测", ref m_GroupExpandedEnvironment, DrawEnvironmentGroupItems);

                if (m_Master == null)
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "请创建或选择 ConfigMaster.asset。" }, false);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                DrawLeftTreeGroup(LeftTreeGroup.Common, "通用配置", ref m_GroupExpandedCommon, DrawCommonGroupItems);

                DrawLeftTreeGroup(LeftTreeGroup.Kit, "Kit 配置", ref m_GroupExpandedKit, DrawKitGroupItems);

                DrawLeftTreeGroup(LeftTreeGroup.SDK, "SDK 配置", ref m_GroupExpandedSDK, DrawSDKGroupItems);

                EditorGUILayout.EndScrollView();
            }, GUILayout.Width(c_LeftTreeWidth));
        }

        /// <summary>
        /// 延迟初始化左树相关 GUIStyle。每个 style 独立判空，防止域重载后 native 对象失效但引用非 null 的情况。
        /// </summary>
        private void EnsureTreeStyles()
        {
            if (m_TreeGroupLabelStyle == null)
            {
                m_TreeGroupLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    padding = new RectOffset(4, 4, 3, 3),
                };
            }

            // m_TreeGroupLabelSelectedStyle 派生自 m_TreeGroupLabelStyle，必须在其初始化后再初始化
            if (m_TreeGroupLabelSelectedStyle == null)
            {
                m_TreeGroupLabelSelectedStyle = new GUIStyle(m_TreeGroupLabelStyle)
                {
                    normal = { textColor = new Color(0.4f, 0.8f, 1f) },
                };
            }

            if (m_TreeItemStyle == null)
            {
                // GUILayout.Button 使用 EditorStyles.label 派生样式时，按钮状态机会吞掉 label 原本 textColor；
                // 必须显式给 normal/hover/active/focused 四态赋色，否则按钮矩形可见但文字不可见。
                Color normalText = new Color(0.85f, 0.85f, 0.85f);
                Color hoverText = Color.white;
                m_TreeItemStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(4, 4, 2, 2),
                    normal = { textColor = normalText },
                    hover = { textColor = hoverText },
                    active = { textColor = hoverText },
                    focused = { textColor = normalText },
                };
            }

            // m_TreeItemSelectedStyle 派生自 m_TreeItemStyle，必须在其初始化后再初始化
            if (m_TreeItemSelectedStyle == null)
            {
                Color accent = new Color(0.4f, 0.8f, 1f);
                m_TreeItemSelectedStyle = new GUIStyle(m_TreeItemStyle)
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = accent },
                    hover = { textColor = accent },
                    active = { textColor = accent },
                    focused = { textColor = accent },
                };
            }
        }

        /// <summary>
        /// 判断指定一级组内是否有子项被选中（用于组标题高亮）。
        /// </summary>
        /// <param name="group">一级组枚举值。</param>
        /// <returns>组内有选中项时返回 true。</returns>
        private bool IsGroupActive(LeftTreeGroup group)
        {
            switch (group)
            {
                case LeftTreeGroup.Environment:
                    return m_SelectedItem == LeftTreeItem.LubanEnv || m_SelectedItem == LeftTreeItem.Python3Env || m_SelectedItem == LeftTreeItem.HybridCLREnv;
                case LeftTreeGroup.Common:
                    return m_SelectedItem == LeftTreeItem.AppConfig || m_SelectedItem == LeftTreeItem.NamespaceConfig || m_SelectedItem == LeftTreeItem.HybridCLRConfig || m_SelectedItem == LeftTreeItem.YooAssetConfig;
                case LeftTreeGroup.SDK:
                    return m_SelectedItem == LeftTreeItem.SDKNode;
                case LeftTreeGroup.Kit:
                    return m_SelectedItem == LeftTreeItem.KitNode;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 绘制一个可折叠的一级组，组内条目由 drawItems 委托负责。
        /// 选中态：组内有选中项时组标题以 accent 色渲染。
        /// </summary>
        /// <param name="group">一级组枚举值。</param>
        /// <param name="label">组显示文本。</param>
        /// <param name="expanded">折叠状态 ref。</param>
        /// <param name="drawItems">绘制组内二级条目的委托。</param>
        private void DrawLeftTreeGroup(LeftTreeGroup group, string label, ref bool expanded, Action drawItems)
        {
            GUIStyle groupStyle = IsGroupActive(group) ? m_TreeGroupLabelSelectedStyle : m_TreeGroupLabelStyle;
            expanded = EditorUtil.Draw.Foldout(ref expanded, label, true, groupStyle);
            if (!expanded) return;

            // 二级节点缩进：左侧固定 14px（一个汉字宽）
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(14f);
                EditorUtil.Draw.Layout.Vertical(() => drawItems());
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 绘制"环境检测"组内二级条目（Luban 环境检测 + Python3 环境检测 + HybridCLR 环境检测）。
        /// </summary>
        private void DrawEnvironmentGroupItems()
        {
            DrawLeftTreeItem("Luban 环境检测", LeftTreeItem.LubanEnv, null);
            DrawLeftTreeItem("Python3 环境检测", LeftTreeItem.Python3Env, null);
            DrawLeftTreeItem("HybridCLR 环境检测", LeftTreeItem.HybridCLREnv, null);
        }

        /// <summary>
        /// 绘制"通用配置"组内二级条目（应用配置 + 名字空间配置 + HybridCLR 配置 + YooAsset 配置）。
        /// </summary>
        private void DrawCommonGroupItems()
        {
            DrawLeftTreeItem("应用配置", LeftTreeItem.AppConfig, null);
            DrawLeftTreeItem("名字空间配置", LeftTreeItem.NamespaceConfig, null);
            DrawLeftTreeItem("HybridCLR 配置", LeftTreeItem.HybridCLRConfig, null);
            DrawLeftTreeItem("YooAsset 配置", LeftTreeItem.YooAssetConfig, null);
        }

        /// <summary>
        /// 绘制"SDK 配置"组内二级条目（每个 SDK Plugin 一项）。
        /// </summary>
        private void DrawSDKGroupItems()
        {
            for (int i = 0; i < m_PluginTypeCache.Count; i++)
            {
                DrawSDKTreeItem(m_PluginTypeCache[i]);
            }
        }

        /// <summary>
        /// 绘制"Kit 配置"组内二级条目（每个 Kit Config 一项）。
        /// </summary>
        private void DrawKitGroupItems()
        {
            for (int i = 0; i < m_KitTypeCache.Count; i++)
            {
                DrawKitTreeItem(m_KitTypeCache[i]);
            }
        }

        /// <summary>
        /// 尝试切换左树选中项；仅当目标与当前不同才写入并强制清除键盘焦点，
        /// 避免旧面板上正在编辑的 TextField 把输入缓冲带入新面板同位置的控件，
        /// 导致 Namespace / DevelopMode 等字段显示上一个字段编辑态的值。
        /// </summary>
        /// <param name="target">目标选中项。</param>
        /// <param name="pluginType">目标关联的 Plugin 类型；静态节点传 null。</param>
        private void TryChangeSelection(LeftTreeItem target, Type pluginType)
        {
            if (m_SelectedItem == target && m_SelectedPluginType == pluginType)
            {
                return;
            }
            m_SelectedItem = target;
            m_SelectedPluginType = pluginType;
            // 清除键盘焦点，让 IMGUI 放弃旧控件的 editingTextField 缓冲。
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
            Repaint();
        }

        /// <summary>
        /// 绘制静态左树二级节点：行首绘制灰色圆点，被选中时用选中态样式高亮整行。
        /// </summary>
        /// <param name="label">节点显示文本。</param>
        /// <param name="target">节点对应的枚举值。</param>
        /// <param name="pluginType">关联的 Plugin 类型；静态节点传 null。</param>
        private void DrawLeftTreeItem(string label, LeftTreeItem target, Type pluginType)
        {
            bool selected = m_SelectedItem == target && m_SelectedPluginType == pluginType;
            GUIStyle labelStyle = selected ? m_TreeItemSelectedStyle : m_TreeItemStyle;
            if (EditorUtil.Draw.ClickableLabelRow("• " + label, labelStyle))
            {
                TryChangeSelection(target, pluginType);
            }
        }

        /// <summary>
        /// 绘制 SDK Plugin 左树节点：圆点 + Toggle 控制 EnabledSDKs，未勾选时名字置灰但数据保留。
        /// 名称显示 DisplayName（中文）。
        /// </summary>
        /// <param name="entry">要绘制的 Plugin Config 条目（类型 + 展示名）。</param>
        private void DrawSDKTreeItem(EditorUtil.Config.SDKPluginScanner.PluginConfigEntry entry)
        {
            Type pluginType = entry.ConfigType;
            string typeName = pluginType.FullName;
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            bool enabled = workingSrc.EnabledSDKs.Contains(typeName);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                bool newEnabled = EditorUtil.Draw.Toggle(enabled, GUILayout.Width(18f));
                if (newEnabled != enabled)
                {
                    if (newEnabled)
                    {
                        workingSrc.EnabledSDKs.Add(typeName);
                        IReadOnlyList<PlatformChannelEntry> allEntries = workingSrc.GetAllEntries();
                        DevelopMode[] allModes = (DevelopMode[])System.Enum.GetValues(typeof(DevelopMode));
                        for (int i = 0; i < allEntries.Count; i++)
                        {
                            for (int m = 0; m < allModes.Length; m++)
                            {
                                EditorUtil.Config.SDKPluginScanner.EnsureInstance(allEntries[i], allModes[m], pluginType);
                            }
                        }
                    }
                    else
                    {
                        workingSrc.EnabledSDKs.Remove(typeName);
                    }
                    m_IsDirty = true;
                }

                bool itemSelected = m_SelectedItem == LeftTreeItem.SDKNode && m_SelectedPluginType == pluginType;
                GUIStyle style = itemSelected ? m_TreeItemSelectedStyle : m_TreeItemStyle;
                Color original = GUI.color;
                if (!enabled) GUI.color = Color.gray;
                if (EditorUtil.Draw.ClickableLabelRow("• " + entry.DisplayName, style))
                {
                    TryChangeSelection(LeftTreeItem.SDKNode, pluginType);
                }
                GUI.color = original;
            });
        }

        /// <summary>
        /// 绘制 Kit Config 左树节点：Toggle 控制 EnabledKits，未勾选时名字置灰但数据保留。
        /// 勾选时遍历 allEntries × allModes 给每格补实例（对称 SDK 树项多格补全逻辑）。
        /// 名称显示 DisplayName（中文）。
        /// </summary>
        /// <param name="entry">要绘制的 Kit Config 条目（类型 + 展示名）。</param>
        private void DrawKitTreeItem(EditorUtil.Config.KitConfigScanner.KitConfigEntry entry)
        {
            System.Type kitType = entry.ConfigType;
            string typeName = kitType.FullName;
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            bool enabled = workingSrc.EnabledKits.Contains(typeName);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                bool newEnabled = EditorUtil.Draw.Toggle(enabled, GUILayout.Width(18f));
                if (newEnabled != enabled)
                {
                    if (newEnabled)
                    {
                        workingSrc.EnabledKits.Add(typeName);
                        IReadOnlyList<PlatformChannelEntry> allEntries = workingSrc.GetAllEntries();
                        DevelopMode[] allModes = (DevelopMode[])System.Enum.GetValues(typeof(DevelopMode));
                        for (int i = 0; i < allEntries.Count; i++)
                        {
                            for (int m = 0; m < allModes.Length; m++)
                            {
                                EditorUtil.Config.KitConfigScanner.EnsureInstance(allEntries[i], allModes[m], kitType);
                            }
                        }
                    }
                    else
                    {
                        workingSrc.EnabledKits.Remove(typeName);
                    }
                    m_IsDirty = true;
                }

                bool itemSelected = m_SelectedItem == LeftTreeItem.KitNode && m_SelectedPluginType == kitType;
                GUIStyle style = itemSelected ? m_TreeItemSelectedStyle : m_TreeItemStyle;
                Color original = GUI.color;
                if (!enabled) GUI.color = Color.gray;
                if (EditorUtil.Draw.ClickableLabelRow("• " + entry.DisplayName, style))
                {
                    TryChangeSelection(LeftTreeItem.KitNode, kitType);
                }
                GUI.color = original;
            });
        }
    }
}
