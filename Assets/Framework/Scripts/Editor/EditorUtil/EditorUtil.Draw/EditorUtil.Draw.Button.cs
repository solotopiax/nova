/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.Button.cs
 * author:    taoye
 * created:   2026/1/27
 * descrip:   编辑器绘制工具-按钮相关
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
            /// 危险按钮启用色。
            /// </summary>
            private static readonly Color s_DangerEnabledColor = new Color(0.75f, 0.25f, 0.25f);

            /// <summary>
            /// 危险按钮禁用色。
            /// </summary>
            private static readonly Color s_DangerDisabledColor = new Color(0.45f, 0.2f, 0.2f);

            /// <summary>
            /// 成功按钮启用色。
            /// </summary>
            private static readonly Color s_SuccessEnabledColor = new Color(0.3f, 0.7f, 0.35f);

            /// <summary>
            /// 成功按钮禁用色。
            /// </summary>
            private static readonly Color s_SuccessDisabledColor = new Color(0.2f, 0.4f, 0.22f);

            /// <summary>
            /// 警告按钮启用色。
            /// </summary>
            private static readonly Color s_WarningEnabledColor = new Color(0.8f, 0.7f, 0.25f);

            /// <summary>
            /// 警告按钮禁用色。
            /// </summary>
            private static readonly Color s_WarningDisabledColor = new Color(0.5f, 0.43f, 0.18f);

            /// <summary>
            /// 绘制按钮。
            /// </summary>
            /// <param name="label">按钮文本。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onClick">点击回调。</param>
            /// <param name="options">布局选项。</param>
            public static void Button(string label, bool disableOnPlaying = true, Action onClick = null, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                try
                {
                    if (GUILayout.Button(label, options))
                    {
                        onClick?.Invoke();
                        GUIUtility.ExitGUI();
                    }
                }
                finally
                {
                    EditorGUI.EndDisabledGroup();
                }
            }

            /// <summary>
            /// 绘制按钮，可控制是否在点击后调用 ExitGUI。
            /// 适用于按钮点击后需要在同一调用栈内继续执行后续 GUI 逻辑的场景（如同步弹窗选择文件后更新字段）。
            /// </summary>
            /// <param name="label">按钮文本。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="exitGUI">点击后是否调用 GUIUtility.ExitGUI。</param>
            /// <param name="onClick">点击回调。</param>
            /// <param name="options">布局选项。</param>
            public static void Button(string label, bool disableOnPlaying, bool exitGUI, Action onClick, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                try
                {
                    if (GUILayout.Button(label, options))
                    {
                        onClick?.Invoke();
                        if (exitGUI)
                        {
                            GUIUtility.ExitGUI();
                        }
                    }
                }
                finally
                {
                    EditorGUI.EndDisabledGroup();
                }
            }

            /// <summary>
            /// 绘制按钮（Rect 手动布局版，供 PropertyDrawer 等自定义布局使用）。
            /// </summary>
            /// <param name="rect">按钮绘制矩形。</param>
            /// <param name="label">按钮文本。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用；为 true 时运行期间灰显不可点击。</param>
            /// <returns>按钮是否被点击。</returns>
            public static bool Button(Rect rect, string label, bool disableOnPlaying = true)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                try
                {
                    return GUI.Button(rect, label);
                }
                finally
                {
                    EditorGUI.EndDisabledGroup();
                }
            }

            /// <summary>
            /// 绘制按钮（带宽度控制）。
            /// </summary>
            /// <param name="label">按钮文本。</param>
            /// <param name="width">按钮宽度。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onClick">点击回调。</param>
            public static void Button(string label, float width, bool disableOnPlaying = true, Action onClick = null, params GUILayoutOption[] options)
            {
                GUILayoutOption[] finalOptions;
                if (options == null || options.Length == 0)
                {
                    finalOptions = new GUILayoutOption[] { GUILayout.Width(width) };
                }
                else
                {
                    finalOptions = new GUILayoutOption[options.Length + 1];
                    Array.Copy(options, finalOptions, options.Length);
                    finalOptions[options.Length] = GUILayout.Width(width);
                }

                Button(label, disableOnPlaying, onClick, finalOptions);
            }

            /// <summary>
            /// 绘制危险按钮（红色），点击后执行 onClick 并调用 GUIUtility.ExitGUI。
            /// </summary>
            /// <param name="label">按钮文本。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onClick">点击回调。</param>
            /// <param name="options">布局选项。</param>
            public static void DangerButton(string label, bool disableOnPlaying = true, Action onClick = null, params GUILayoutOption[] options)
            {
                ColoredButton(label, s_DangerEnabledColor, s_DangerDisabledColor, disableOnPlaying, onClick, options);
            }

            /// <summary>
            /// 绘制危险按钮（红色，带宽度控制），点击后执行 onClick 并调用 GUIUtility.ExitGUI。
            /// </summary>
            /// <param name="label">按钮文本。</param>
            /// <param name="width">按钮宽度。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onClick">点击回调。</param>
            public static void DangerButton(string label, float width, bool disableOnPlaying = true, Action onClick = null)
            {
                DangerButton(label, disableOnPlaying, onClick, GUILayout.Width(width));
            }

            /// <summary>
            /// 绘制成功按钮（绿色），点击后执行 onClick 并调用 GUIUtility.ExitGUI。
            /// </summary>
            /// <param name="label">按钮文本。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onClick">点击回调。</param>
            /// <param name="options">布局选项。</param>
            public static void SuccessButton(string label, bool disableOnPlaying = true, Action onClick = null, params GUILayoutOption[] options)
            {
                ColoredButton(label, s_SuccessEnabledColor, s_SuccessDisabledColor, disableOnPlaying, onClick, options);
            }

            /// <summary>
            /// 绘制成功按钮（绿色，带宽度控制），点击后执行 onClick 并调用 GUIUtility.ExitGUI。
            /// </summary>
            /// <param name="label">按钮文本。</param>
            /// <param name="width">按钮宽度。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onClick">点击回调。</param>
            public static void SuccessButton(string label, float width, bool disableOnPlaying = true, Action onClick = null)
            {
                SuccessButton(label, disableOnPlaying, onClick, GUILayout.Width(width));
            }

            /// <summary>
            /// 绘制警告按钮（黄色），点击后执行 onClick 并调用 GUIUtility.ExitGUI。
            /// </summary>
            /// <param name="label">按钮文本。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onClick">点击回调。</param>
            /// <param name="options">布局选项。</param>
            public static void WarningButton(string label, bool disableOnPlaying = true, Action onClick = null, params GUILayoutOption[] options)
            {
                ColoredButton(label, s_WarningEnabledColor, s_WarningDisabledColor, disableOnPlaying, onClick, options);
            }

            /// <summary>
            /// 绘制警告按钮（黄色，带宽度控制），点击后执行 onClick 并调用 GUIUtility.ExitGUI。
            /// </summary>
            /// <param name="label">按钮文本。</param>
            /// <param name="width">按钮宽度。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onClick">点击回调。</param>
            public static void WarningButton(string label, float width, bool disableOnPlaying = true, Action onClick = null)
            {
                WarningButton(label, disableOnPlaying, onClick, GUILayout.Width(width));
            }

            /// <summary>
            /// 着色按钮内部实现，统一 DisabledGroup + 颜色恢复的 try/finally 保护。
            /// </summary>
            /// <param name="label">按钮文本。</param>
            /// <param name="enabledColor">启用时颜色。</param>
            /// <param name="disabledColor">禁用时颜色。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onClick">点击回调。</param>
            /// <param name="options">布局选项。</param>
            private static void ColoredButton(string label, Color enabledColor, Color disabledColor, bool disableOnPlaying, Action onClick, params GUILayoutOption[] options)
            {
                Color prev = GUI.color;
                GUI.color = GUI.enabled ? enabledColor : disabledColor;
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                try
                {
                    bool clicked = GUILayout.Button(label, options);
                    if (clicked)
                    {
                        onClick?.Invoke();
                        GUIUtility.ExitGUI();
                    }
                }
                finally
                {
                    EditorGUI.EndDisabledGroup();
                    GUI.color = prev;
                }
            }
        }
    }
}
