/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.Assist.cs
 * author:    taoye
 * created:   2026/1/15
 * descrip:   编辑器绘制工具-辅助相关
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
            /// 绘制禁用区。
            /// </summary>
            /// <param name="disabled">是否禁用。</param>
            /// <param name="drawAction">绘制回调。</param>
            public static void DisabledGroup(bool disabled, Action drawAction)
            {
                if (drawAction == null)
                {
                    return;
                }

                EditorGUI.BeginDisabledGroup(disabled);
                try
                {
                    drawAction.Invoke();
                }
                finally
                {
                    EditorGUI.EndDisabledGroup();
                }
            }

            /// <summary>
            /// 绘制分割线。
            /// </summary>
            public static void Line()
            {
                GUILayout.Space(10f);
                Rect rect = GUILayoutUtility.GetLastRect();
                Color oldColor = GUI.color;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(8f, rect.yMin + 6f, EditorGUIUtility.currentViewWidth - 8f, 2f), EditorGUIUtility.whiteTexture);
                GUI.color = oldColor;
            }

            /// <summary>
            /// 绘制空白间距。
            /// </summary>
            /// <param name="pixels">像素值。</param>
            public static void Space(float pixels)
            {
                GUILayout.Space(pixels);
            }

            /// <summary>
            /// 绘制空白间距（带扩展选项）。
            /// </summary>
            /// <param name="pixels">像素值。</param>
            /// <param name="expand">是否扩展。</param>
            public static void Space(float pixels, bool expand)
            {
                EditorGUILayout.Space(pixels, expand);
            }

            /// <summary>
            /// 绘制分隔符。
            /// </summary>
            public static void Separator()
            {
                EditorGUILayout.Separator();
            }

            /// <summary>
            /// 绘制弹性空白区（占满剩余水平空间）。
            /// </summary>
            public static void FlexibleSpace()
            {
                GUILayout.FlexibleSpace();
            }

            /// <summary>
            /// 保存当前缩进级别。
            /// </summary>
            /// <returns>当前缩进级别。</returns>
            public static int SaveIndentLevel()
            {
                return EditorGUI.indentLevel;
            }

            /// <summary>
            /// 恢复缩进级别到指定值。
            /// </summary>
            /// <param name="indentLevel">要恢复的缩进级别。</param>
            public static void RestoreIndentLevel(int indentLevel)
            {
                EditorGUI.indentLevel = indentLevel;
            }

            /// <summary>
            /// 设置缩进级别。
            /// </summary>
            /// <param name="indentLevel">缩进级别。</param>
            public static void SetIndentLevel(int indentLevel)
            {
                EditorGUI.indentLevel = indentLevel;
            }

            /// <summary>
            /// 增加缩进级别。
            /// </summary>
            public static void IncreaseIndentLevel()
            {
                EditorGUI.indentLevel++;
            }

            /// <summary>
            /// 减少缩进级别。
            /// </summary>
            public static void DecreaseIndentLevel()
            {
                EditorGUI.indentLevel--;
            }
        }   
    }
}
