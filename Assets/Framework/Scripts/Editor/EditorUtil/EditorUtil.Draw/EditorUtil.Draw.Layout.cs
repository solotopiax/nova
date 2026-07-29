/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.Layout.cs
 * author:    taoye
 * created:   2026/1/27
 * descrip:   编辑器绘制工具-布局相关
 ***************************************************************/

using System;
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
            /// 布局工具。
            /// </summary>
            public static class Layout
            {
                /// <summary>
                /// Unity Inspector 中一级树形缩进的标准宽度，视觉上约等于一个汉字。
                /// </summary>
                internal const float c_IndentPixelsPerLevel = 11f;

                /// <summary>
                /// 固定零边距的缩进布局样式，阻止展开内容的 Box margin 反向传播到父级布局。
                /// </summary>
                private static GUIStyle s_IndentedGroupStyle;

                /// <summary>
                /// 保留 Box 背景但移除左侧外边距与内边距的样式，用于树形条目箭头对齐。
                /// </summary>
                private static GUIStyle s_TreeItemBoxStyle;

                /// <summary>
                /// 获取固定零边距的缩进布局样式。
                /// </summary>
                private static GUIStyle IndentedGroupStyle
                {
                    get
                    {
                        if (s_IndentedGroupStyle == null)
                        {
                            s_IndentedGroupStyle = new GUIStyle(GUIStyle.none)
                            {
                                margin = new RectOffset(),
                            };
                        }
                        return s_IndentedGroupStyle;
                    }
                }

                /// <summary>
                /// 获取不会额外推移树形标题箭头的 Box 样式。
                /// </summary>
                private static GUIStyle TreeItemBoxStyle
                {
                    get
                    {
                        if (s_TreeItemBoxStyle == null)
                        {
                            GUIStyle source = GUI.skin.box;
                            s_TreeItemBoxStyle = new GUIStyle(source)
                            {
                                margin = new RectOffset(
                                    0, source.margin.right, source.margin.top, source.margin.bottom),
                                padding = new RectOffset(
                                    0, source.padding.right, source.padding.top, source.padding.bottom),
                            };
                        }
                        return s_TreeItemBoxStyle;
                    }
                }

                /// <summary>
                /// 以当前 Editor 字体的一个汉字宽度为一级，整体缩进内部所有字段、按钮和子布局。
                /// </summary>
                /// <param name="drawAction">绘制回调。</param>
                /// <param name="levels">缩进级数。</param>
                public static void Indented(Action drawAction, int levels = 1)
                {
                    if (drawAction == null)
                    {
                        return;
                    }

                    float indentWidth = c_IndentPixelsPerLevel * Math.Max(0, levels);
                    Horizontal(() =>
                    {
                        GUILayout.Space(indentWidth);
                        Vertical(IndentedGroupStyle, drawAction, GUILayout.ExpandWidth(true));
                    }, GUILayout.ExpandWidth(true));
                }

                /// <summary>
                /// 绘制水平布局。
                /// </summary>
                /// <param name="drawAction">绘制回调。</param>
                /// <param name="options">布局选项。</param>
                public static void Horizontal(Action drawAction, params GUILayoutOption[] options)
                {
                    if (drawAction == null)
                    {
                        return;
                    }

                    EditorGUILayout.BeginHorizontal(options);
                    try
                    {
                        drawAction.Invoke();
                    }
                    finally
                    {
                        EditorGUILayout.EndHorizontal();
                    }
                }

                /// <summary>
                /// 绘制水平布局（带样式）。
                /// </summary>
                /// <param name="style">GUI样式。</param>
                /// <param name="drawAction">绘制回调。</param>
                /// <param name="options">布局选项。</param>
                public static void Horizontal(GUIStyle style, Action drawAction, params GUILayoutOption[] options)
                {
                    if (drawAction == null)
                    {
                        return;
                    }

                    EditorGUILayout.BeginHorizontal(style, options);
                    try
                    {
                        drawAction.Invoke();
                    }
                    finally
                    {
                        EditorGUILayout.EndHorizontal();
                    }
                }

                /// <summary>
                /// 绘制水平布局（带样式名称）。
                /// </summary>
                /// <param name="styleName">样式名称。</param>
                /// <param name="drawAction">绘制回调。</param>
                /// <param name="options">布局选项。</param>
                public static void Horizontal(string styleName, Action drawAction, params GUILayoutOption[] options)
                {
                    if (drawAction == null)
                    {
                        return;
                    }

                    EditorGUILayout.BeginHorizontal(styleName, options);
                    try
                    {
                        drawAction.Invoke();
                    }
                    finally
                    {
                        EditorGUILayout.EndHorizontal();
                    }
                }

                /// <summary>
                /// 绘制垂直布局。
                /// </summary>
                /// <param name="drawAction">绘制回调。</param>
                /// <param name="options">布局选项。</param>
                public static void Vertical(Action drawAction, params GUILayoutOption[] options)
                {
                    if (drawAction == null)
                    {
                        return;
                    }

                    EditorGUILayout.BeginVertical(options);
                    try
                    {
                        drawAction.Invoke();
                    }
                    finally
                    {
                        EditorGUILayout.EndVertical();
                    }
                }

                /// <summary>
                /// 绘制垂直布局（带样式）。
                /// </summary>
                /// <param name="style">GUI样式。</param>
                /// <param name="drawAction">绘制回调。</param>
                /// <param name="options">布局选项。</param>
                public static void Vertical(GUIStyle style, Action drawAction, params GUILayoutOption[] options)
                {
                    if (drawAction == null)
                    {
                        return;
                    }

                    EditorGUILayout.BeginVertical(style, options);
                    try
                    {
                        drawAction.Invoke();
                    }
                    finally
                    {
                        EditorGUILayout.EndVertical();
                    }
                }

                /// <summary>
                /// 绘制垂直布局（带样式名称）。
                /// </summary>
                /// <param name="styleName">样式名称。</param>
                /// <param name="drawAction">绘制回调。</param>
                /// <param name="options">布局选项。</param>
                public static void Vertical(string styleName, Action drawAction, params GUILayoutOption[] options)
                {
                    if (drawAction == null)
                    {
                        return;
                    }

                    EditorGUILayout.BeginVertical(styleName, options);
                    try
                    {
                        drawAction.Invoke();
                    }
                    finally
                    {
                        EditorGUILayout.EndVertical();
                    }
                }

                /// <summary>
                /// 绘制树形条目 Box；保留背景样式，但不额外推移左侧箭头。
                /// </summary>
                /// <param name="drawAction">绘制回调。</param>
                /// <param name="options">布局选项。</param>
                public static void TreeItemBox(Action drawAction, params GUILayoutOption[] options)
                {
                    Vertical(TreeItemBoxStyle, drawAction, options);
                }

                /// <summary>
                /// 绘制 ScrollView 区块；闭包内部的所有绘制都受 scroll 偏移控制。
                /// </summary>
                /// <param name="scroll">入参/出参滚动位置（按引用传，闭包内不可改本地副本）。</param>
                /// <param name="drawAction">绘制回调。</param>
                /// <param name="options">布局选项。</param>
                /// <returns>本帧用户交互后的滚动位置（调用方需写回原字段）。</returns>
                public static Vector2 ScrollView(Vector2 scroll, Action drawAction, params GUILayoutOption[] options)
                {
                    if (drawAction == null)
                    {
                        return scroll;
                    }
                    Vector2 next = EditorGUILayout.BeginScrollView(scroll, options);
                    try
                    {
                        drawAction.Invoke();
                    }
                    finally
                    {
                        EditorGUILayout.EndScrollView();
                    }
                    return next;
                }
            }
        }
    }
}
