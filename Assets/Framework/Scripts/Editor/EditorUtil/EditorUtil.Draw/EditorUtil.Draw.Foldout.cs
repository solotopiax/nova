/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.Foldout.cs
 * author:    taoye
 * created:   2026/1/15
 * descrip:   编辑器绘制工具-折叠页
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 绘制工具。
        /// </summary>
        public static partial class Draw
        {
            /// <summary>
            /// Item 状态缓存（只记录展开的对象）。
            /// </summary>
            private static readonly HashSet<string> m_OpenedItems = new HashSet<string>();

            /// <summary>
            /// 已初始化默认展开状态的 Item 集合，防止 defaultOpen=true 时每帧重复添加。
            /// </summary>
            private static readonly HashSet<string> m_DefaultOpenInitialized = new HashSet<string>();

            /// <summary>
            /// Foldout 箭头槽宽度；仅保留图标所需区域，避免箭头与后续内容之间形成额外汉字宽度。
            /// </summary>
            private const float c_FoldoutArrowWidth = 5f;

            /// <summary>
            /// Foldout 各槽位之间不额外留白，由控件自身视觉边界保持紧凑间距。
            /// </summary>
            private const float c_FoldoutSpacing = 0f;

            /// <summary>
            /// Foldout 启用 Toggle 槽宽度。
            /// </summary>
            private const float c_FoldoutToggleWidth = 16f;

            /// <summary>
            /// 统一绘制左对齐 Foldout 行；展开状态只改变箭头方向，不改变任何槽位坐标。
            /// </summary>
            /// <param name="displayName">标题。</param>
            /// <param name="expanded">当前展开状态。</param>
            /// <param name="color">箭头与标题颜色。</param>
            /// <param name="toggleValue">可选启用 Toggle；null 表示不绘制。</param>
            /// <param name="newToggleValue">交互后的 Toggle 值。</param>
            /// <param name="toggleOnTitleClick">是否点击标题切换展开状态。</param>
            /// <param name="drawTrailingContent">右侧操作区。</param>
            /// <returns>交互后的展开状态。</returns>
            private static bool DrawFoldoutRow(
                string displayName,
                bool expanded,
                Color color,
                bool? toggleValue,
                out bool newToggleValue,
                bool toggleOnTitleClick,
                Action drawTrailingContent)
            {
                bool nextExpanded = expanded;
                bool nextToggleValue = toggleValue ?? false;
                Layout.Horizontal(() =>
                {
                    Rect row = EditorGUILayout.GetControlRect(
                        GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                    float hierarchyIndent = EditorGUI.indentLevel * Layout.c_IndentPixelsPerLevel;
                    float cursorX = row.x + hierarchyIndent;
                    Rect arrowRect = new Rect(cursorX, row.y, c_FoldoutArrowWidth, row.height);
                    cursorX += c_FoldoutArrowWidth + c_FoldoutSpacing;
                    Rect toggleRect = default;
                    if (toggleValue.HasValue)
                    {
                        toggleRect = new Rect(cursorX, row.y, c_FoldoutToggleWidth, row.height);
                        cursorX += c_FoldoutToggleWidth + c_FoldoutSpacing;
                    }
                    Rect titleRect = new Rect(cursorX, row.y, Mathf.Max(0f, row.xMax - cursorX), row.height);

                    int previousIndent = EditorGUI.indentLevel;
                    Color previousColor = GUI.contentColor;
                    EditorGUI.indentLevel = 0;
                    try
                    {
                        GUI.contentColor = color;
                        nextExpanded = EditorGUI.Foldout(
                            arrowRect, expanded, GUIContent.none, false, EditorStyles.foldout);
                        GUI.Label(titleRect, displayName, EditorStyles.label);
                        if (toggleOnTitleClick && GUI.Button(titleRect, GUIContent.none, GUIStyle.none))
                        {
                            nextExpanded = !expanded;
                        }

                        if (toggleValue.HasValue)
                        {
                            GUI.contentColor = previousColor;
                            nextToggleValue = GUI.Toggle(
                                toggleRect, nextToggleValue, GUIContent.none, GUI.skin.toggle);
                        }
                    }
                    finally
                    {
                        GUI.contentColor = previousColor;
                        EditorGUI.indentLevel = previousIndent;
                    }
                    drawTrailingContent?.Invoke();
                });
                newToggleValue = nextToggleValue;
                return nextExpanded;
            }

            /// <summary>
            /// 写回内部 Foldout 缓存并返回新状态。
            /// </summary>
            /// <param name="listName">折叠状态唯一标识。</param>
            /// <param name="expanded">新状态。</param>
            /// <returns>新状态。</returns>
            private static bool SetCachedFoldoutState(string listName, bool expanded)
            {
                if (expanded)
                {
                    m_OpenedItems.Add(listName);
                }
                else
                {
                    m_OpenedItems.Remove(listName);
                }
                return expanded;
            }

            /// <summary>
            /// 展示伸缩列表。
            /// </summary>
            /// <param name="displayName">列表展示名称。</param>
            /// <param name="listName">列表名称（作为当前列表的唯一标识，填写 listName 的情况下，displayName 可以有多个，但是 listName 只能有一个）。</param>
            /// <param name="defaultOpen">首次出现时是否默认展开，false 为默认收起。</param>
            /// <returns>是否展开。</returns>
            public static bool Foldout(string displayName, string listName = null, bool defaultOpen = false)
            {
                listName ??= displayName;

                if (defaultOpen && m_DefaultOpenInitialized.Add(listName))
                {
                    m_OpenedItems.Add(listName);
                }

                bool currentState = DrawFoldoutRow(
                    displayName, m_OpenedItems.Contains(listName), GUI.contentColor,
                    null, out _, true, null);
                return SetCachedFoldoutState(listName, currentState);
            }

            /// <summary>
            /// 使用指定状态色绘制带内部缓存的可折叠项。
            /// </summary>
            /// <param name="displayName">列表展示名称。</param>
            /// <param name="listName">折叠状态唯一标识。</param>
            /// <param name="color">Foldout 箭头与文字状态色。</param>
            /// <param name="defaultOpen">首次出现时是否默认展开。</param>
            /// <returns>是否展开。</returns>
            public static bool ColoredFoldout(string displayName, string listName, Color color, bool defaultOpen = false)
            {
                listName ??= displayName;
                if (defaultOpen && m_DefaultOpenInitialized.Add(listName))
                {
                    m_OpenedItems.Add(listName);
                }
                bool currentState = DrawFoldoutRow(
                    displayName, m_OpenedItems.Contains(listName), color,
                    null, out _, true, null);
                return SetCachedFoldoutState(listName, currentState);
            }

            /// <summary>
            /// 绘制左侧占满剩余宽度、右侧带独立操作区的普通折叠标题行。
            /// </summary>
            /// <param name="displayName">列表展示名称。</param>
            /// <param name="listName">折叠状态唯一标识。</param>
            /// <param name="drawTrailingContent">绘制右侧独立操作控件。</param>
            /// <param name="defaultOpen">首次出现时是否默认展开。</param>
            /// <returns>是否展开。</returns>
            public static bool FoldoutHeader(
                string displayName,
                string listName,
                Action drawTrailingContent,
                bool defaultOpen = false)
            {
                return ColoredFoldoutHeader(
                    displayName, listName, GUI.contentColor, drawTrailingContent, defaultOpen);
            }

            /// <summary>
            /// 绘制左侧占满剩余宽度、右侧带独立操作区的彩色折叠标题行。
            /// </summary>
            /// <param name="displayName">列表展示名称。</param>
            /// <param name="listName">折叠状态唯一标识。</param>
            /// <param name="color">Foldout 箭头与文字状态色。</param>
            /// <param name="drawTrailingContent">绘制右侧独立操作控件。</param>
            /// <param name="defaultOpen">首次出现时是否默认展开。</param>
            /// <returns>是否展开。</returns>
            public static bool ColoredFoldoutHeader(
                string displayName,
                string listName,
                Color color,
                Action drawTrailingContent,
                bool defaultOpen = false)
            {
                listName ??= displayName;
                if (defaultOpen && m_DefaultOpenInitialized.Add(listName))
                {
                    m_OpenedItems.Add(listName);
                }

                bool currentState = DrawFoldoutRow(
                    displayName, m_OpenedItems.Contains(listName), color,
                    null, out _, true, drawTrailingContent);
                return SetCachedFoldoutState(listName, currentState);
            }

            /// <summary>
            /// 绘制“展开箭头、启用复选框、标题、右侧操作区”结构的彩色折叠标题行。
            /// </summary>
            /// <param name="displayName">列表展示名称。</param>
            /// <param name="listName">折叠状态唯一标识。</param>
            /// <param name="color">Foldout 箭头与标题状态色。</param>
            /// <param name="toggleValue">当前复选框值。</param>
            /// <param name="newToggleValue">用户操作后的复选框值。</param>
            /// <param name="drawTrailingContent">绘制右侧独立操作控件。</param>
            /// <param name="defaultOpen">首次出现时是否默认展开。</param>
            /// <returns>是否展开。</returns>
            public static bool ColoredToggleFoldoutHeader(
                string displayName,
                string listName,
                Color color,
                bool toggleValue,
                out bool newToggleValue,
                Action drawTrailingContent,
                bool defaultOpen = false)
            {
                listName ??= displayName;
                if (defaultOpen && m_DefaultOpenInitialized.Add(listName))
                {
                    m_OpenedItems.Add(listName);
                }

                bool currentState = DrawFoldoutRow(
                    displayName, m_OpenedItems.Contains(listName), color,
                    toggleValue, out newToggleValue, true, drawTrailingContent);
                return SetCachedFoldoutState(listName, currentState);
            }

            /// <summary>
            /// 绘制可折叠项（外部管理状态，不使用内部缓存）。
            /// </summary>
            /// <param name="foldout">当前折叠状态（传入引用，会被更新）。</param>
            /// <param name="displayName">显示名称。</param>
            /// <param name="toggleOnLabelClick">是否点击标签也可切换展开/折叠。</param>
            /// <returns>更新后的折叠状态。</returns>
            public static bool Foldout(ref bool foldout, string displayName, bool toggleOnLabelClick = true)
            {
                foldout = DrawFoldoutRow(
                    displayName, foldout, GUI.contentColor,
                    null, out _, toggleOnLabelClick, null);
                return foldout;
            }

            /// <summary>
            /// 绘制可折叠项（外部管理状态，支持自定义样式和布局选项）。
            /// </summary>
            /// <param name="foldout">当前折叠状态（传入引用，会被更新）。</param>
            /// <param name="displayName">显示名称。</param>
            /// <param name="toggleOnLabelClick">是否点击标签也可切换展开/折叠。</param>
            /// <param name="style">自定义 GUIStyle。</param>
            /// <param name="options">布局选项（如 GUILayout.Width）。</param>
            /// <returns>更新后的折叠状态。</returns>
            public static bool Foldout(ref bool foldout, string displayName, bool toggleOnLabelClick, GUIStyle style, params GUILayoutOption[] options)
            {
                Rect rect = EditorGUILayout.GetControlRect(options);
                foldout = EditorGUI.Foldout(rect, foldout, displayName, toggleOnLabelClick, style);
                return foldout;
            }

            /// <summary>
            /// 绘制可折叠项（外部管理状态，支持布局选项）。
            /// </summary>
            /// <param name="foldout">当前折叠状态（传入引用，会被更新）。</param>
            /// <param name="displayName">显示名称。</param>
            /// <param name="toggleOnLabelClick">是否点击标签也可切换展开/折叠。</param>
            /// <param name="options">布局选项（如 GUILayout.Width）。</param>
            /// <returns>更新后的折叠状态。</returns>
            public static bool Foldout(ref bool foldout, string displayName, bool toggleOnLabelClick, params GUILayoutOption[] options)
            {
                return Foldout(ref foldout, displayName, toggleOnLabelClick, EditorStyles.foldout, options);
            }

            /// <summary>
            /// 清理伸缩列表缓存。
            /// </summary>
            /// <param name="listName">列表名称。</param>
            public static void CleanFoldout(string listName)
            {
                if (!string.IsNullOrEmpty(listName))
                {
                    m_OpenedItems.Remove(listName);
                    m_DefaultOpenInitialized.Remove(listName);
                }
            }

            /// <summary>
            /// 清除所有折叠状态缓存，适用于 EditorWindow 关闭或需要重置全部折叠状态的场景。
            /// </summary>
            public static void ClearAllFoldoutCache()
            {
                m_OpenedItems.Clear();
                m_DefaultOpenInitialized.Clear();
            }

            /// <summary>
            /// 绘制可折叠项（外部管理状态，手动 Rect 布局）。
            /// </summary>
            /// <param name="rect">绘制区域。</param>
            /// <param name="foldout">当前折叠状态（传入引用，会被更新）。</param>
            /// <param name="displayName">显示名称。</param>
            /// <param name="toggleOnLabelClick">是否点击标签也可切换展开/折叠。</param>
            /// <param name="style">自定义 GUIStyle。</param>
            /// <returns>更新后的折叠状态。</returns>
            public static bool Foldout(Rect rect, ref bool foldout, string displayName, bool toggleOnLabelClick, GUIStyle style)
            {
                foldout = EditorGUI.Foldout(rect, foldout, displayName, toggleOnLabelClick, style);
                return foldout;
            }

        }
    }
}
