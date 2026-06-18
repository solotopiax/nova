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
