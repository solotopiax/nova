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

                bool lastState = m_OpenedItems.Contains(listName);
                bool currentState = EditorGUILayout.Foldout(lastState, displayName, true, EditorStyles.foldout);

                if (currentState != lastState)
                {
                    if (currentState)
                    {
                        m_OpenedItems.Add(listName);
                    }
                    else
                    {
                        m_OpenedItems.Remove(listName);
                    }
                }

                return currentState;
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
                foldout = EditorGUILayout.Foldout(foldout, displayName, toggleOnLabelClick);
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
