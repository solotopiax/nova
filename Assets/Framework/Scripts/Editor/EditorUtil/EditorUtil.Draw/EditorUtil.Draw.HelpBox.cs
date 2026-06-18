/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.HelpBox.cs
 * author:    taoye
 * created:   2026/1/15
 * descrip:   编辑器绘制工具-提示框
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
            /// 边缘距离。
            /// </summary>
            private const float c_Padding = 6f;

            /// <summary>
            /// 图标尺寸。
            /// </summary>
            private const float c_IconSize = 10f;

            /// <summary>
            /// 图标文本间距。
            /// </summary>
            private const float c_IconTextSpacing = 1f;

            /// <summary>
            /// 边框宽度。
            /// </summary>
            private const float c_BorderWidth = 1f;

            /// <summary>
            /// 字体大小。
            /// </summary>
            private const int c_FontSize = 10;

            /// <summary>
            /// 首行图标占位缩进（左侧让位给图标）。
            /// </summary>
            private const float c_FirstLineIndent = c_IconSize + c_IconTextSpacing;

            /// <summary>
            /// 首行文本样式（按 MessageType 缓存）。
            /// </summary>
            private static readonly GUIStyle[] s_FirstLineStyles = new GUIStyle[4];

            /// <summary>
            /// 正文文本样式（按 MessageType 缓存）。
            /// </summary>
            private static readonly GUIStyle[] s_RestStyles = new GUIStyle[4];

            /// <summary>
            /// 绘制提示框（GUILayout 自动布局版）。
            /// 使用独立的 GUILayout.Label 承载每行文本，让 Unity 自行按真实宽度 wordwrap，
            /// 不做任何预估宽度/高度；避免 Layout 与 Repaint 阶段宽度不一致导致末行被 "..." 截断。
            /// </summary>
            /// <param name="messageType">消息类型。</param>
            /// <param name="messages">提示内容。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">额外 GUILayout 选项。</param>
            public static void HelpBox(MessageType messageType, string[] messages, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                if (messages == null || messages.Length == 0)
                {
                    return;
                }

                string titleText = GetTextTitle(messageType);
                GUIStyle firstLineStyle = GetFirstLineStyle(messageType);
                GUIStyle restStyle = GetRestStyle(messageType);

                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);

                int baseCount = options?.Length ?? 0;
                GUILayoutOption[] finalOptions = new GUILayoutOption[baseCount + 1];
                if (options != null)
                {
                    Array.Copy(options, finalOptions, baseCount);
                }
                finalOptions[baseCount] = GUILayout.ExpandWidth(true);

                Rect containerRect = EditorGUILayout.BeginVertical(GUIStyle.none, finalOptions);

                if (Event.current.type == EventType.Repaint)
                {
                    DrawBackground(containerRect, messageType);
                    DrawBorder(containerRect);
                }

                GUILayout.Space(c_Padding);
                GUILayout.Label(titleText, firstLineStyle);
                foreach (string msg in messages)
                {
                    if (string.IsNullOrEmpty(msg))
                    {
                        continue;
                    }
                    GUILayout.Label(msg, restStyle);
                }
                GUILayout.Space(c_Padding);

                EditorGUILayout.EndVertical();

                if (Event.current.type == EventType.Repaint)
                {
                    DrawIcon(containerRect, messageType, firstLineStyle);
                }

                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 在首行位置叠加绘制图标。
            /// </summary>
            /// <param name="rect">容器 rect。</param>
            /// <param name="messageType">消息类型。</param>
            /// <param name="firstLineStyle">首行样式（取 lineHeight）。</param>
            private static void DrawIcon(Rect rect, MessageType messageType, GUIStyle firstLineStyle)
            {
                Texture2D icon = GetIcon(messageType);
                if (icon == null)
                {
                    return;
                }

                float lineHeight = GetLineHeight(firstLineStyle);
                float iconY = rect.y + c_Padding + (lineHeight - c_IconSize) * 0.5f;
                Rect iconRect = new Rect(rect.x + c_Padding + 1f, iconY - 1f, c_IconSize, c_IconSize);

                Color oldColor = GUI.color;
                GUI.color = GetIconColor(messageType);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                GUI.color = oldColor;
            }

            /// <summary>
            /// 获取行高。
            /// </summary>
            /// <param name="style">文本样式。</param>
            /// <returns>行高。</returns>
            private static float GetLineHeight(GUIStyle style)
            {
                return style.lineHeight > 0 ? style.lineHeight : EditorGUIUtility.singleLineHeight;
            }

            /// <summary>
            /// 获取首行样式（左侧 padding 为图标留位）。
            /// </summary>
            /// <param name="type">消息类型。</param>
            /// <returns>首行样式。</returns>
            private static GUIStyle GetFirstLineStyle(MessageType type)
            {
                int idx = (int)type & 3;
                if (s_FirstLineStyles[idx] == null)
                {
                    s_FirstLineStyles[idx] = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = true,
                        richText = false,
                        clipping = TextClipping.Overflow,
                        alignment = TextAnchor.UpperLeft,
                        fontSize = c_FontSize,
                        padding = new RectOffset((int)(c_Padding + c_FirstLineIndent), (int)c_Padding, 0, 0),
                        margin = new RectOffset(0, 0, 0, 0)
                    };
                    Color c = GetTextColor(type);
                    s_FirstLineStyles[idx].normal.textColor = c;
                    s_FirstLineStyles[idx].focused.textColor = c;
                    s_FirstLineStyles[idx].hover.textColor = c;
                    s_FirstLineStyles[idx].active.textColor = c;
                }
                return s_FirstLineStyles[idx];
            }

            /// <summary>
            /// 获取正文样式。
            /// </summary>
            /// <param name="type">消息类型。</param>
            /// <returns>正文样式。</returns>
            private static GUIStyle GetRestStyle(MessageType type)
            {
                int idx = (int)type & 3;
                if (s_RestStyles[idx] == null)
                {
                    s_RestStyles[idx] = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = true,
                        richText = false,
                        clipping = TextClipping.Overflow,
                        alignment = TextAnchor.UpperLeft,
                        fontSize = c_FontSize,
                        padding = new RectOffset((int)c_Padding, (int)c_Padding, 0, 0),
                        margin = new RectOffset(0, 0, 0, 0)
                    };
                    Color c = GetTextColor(type);
                    s_RestStyles[idx].normal.textColor = c;
                    s_RestStyles[idx].focused.textColor = c;
                    s_RestStyles[idx].hover.textColor = c;
                    s_RestStyles[idx].active.textColor = c;
                }
                return s_RestStyles[idx];
            }

            /// <summary>
            /// 获取图标。
            /// </summary>
            /// <param name="type">消息类型。</param>
            /// <returns>图标纹理。</returns>
            private static Texture2D GetIcon(MessageType type)
            {
                string iconName = type switch
                {
                    MessageType.Info => "console.infoicon.sml",
                    MessageType.Warning => "console.warnicon.sml",
                    MessageType.Error => "console.erroricon.sml",
                    _ => null
                };
                return iconName != null ? EditorGUIUtility.FindTexture(iconName) : null;
            }

            /// <summary>
            /// 获取图标颜色。
            /// </summary>
            /// <param name="type">消息类型。</param>
            /// <returns>图标颜色。</returns>
            private static Color GetIconColor(MessageType type)
            {
                return type switch
                {
                    MessageType.Info => new Color(18 / 255f, 150 / 255f, 220 / 255f, 1f),
                    MessageType.Warning => new Color(1f, 190 / 255f, 7 / 255f, 1f),
                    MessageType.Error => new Color(255 / 255f, 110 / 255f, 64 / 255f, 1f),
                    _ => Color.white
                };
            }

            /// <summary>
            /// 获取文本颜色。
            /// </summary>
            /// <param name="type">消息类型。</param>
            /// <returns>文本颜色。</returns>
            private static Color GetTextColor(MessageType type)
            {
                return type switch
                {
                    MessageType.Info => new Color(100 / 255f, 210 / 255f, 210 / 255f, 1f),
                    MessageType.Warning => new Color(1f, 233 / 255f, 169 / 255f, 1f),
                    MessageType.Error => new Color(1f, 157 / 255f, 125 / 255f, 1f),
                    _ => Color.white
                };
            }

            /// <summary>
            /// 获取文本标题。
            /// </summary>
            /// <param name="type">消息类型。</param>
            /// <returns>标题文本。</returns>
            private static string GetTextTitle(MessageType type)
            {
                return type switch
                {
                    MessageType.Info => "提示：",
                    MessageType.Warning => "警告：",
                    MessageType.Error => "错误：",
                    _ => string.Empty
                };
            }

            /// <summary>
            /// 绘制背景。
            /// </summary>
            /// <param name="rect">绘制区域。</param>
            /// <param name="type">消息类型。</param>
            private static void DrawBackground(Rect rect, MessageType type)
            {
                EditorGUI.DrawRect(rect, type switch
                {
                    MessageType.Info => new Color(0.22f, 0.25f, 0.30f, 1f),
                    MessageType.Warning => new Color(0.32f, 0.26f, 0.12f, 1f),
                    MessageType.Error => new Color(0.32f, 0.18f, 0.18f, 1f),
                    _ => Color.gray
                });
            }

            /// <summary>
            /// 绘制边框。
            /// </summary>
            /// <param name="rect">绘制区域。</param>
            private static void DrawBorder(Rect rect)
            {
                Color border = new Color(0f, 0f, 0f, 0.35f);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, c_BorderWidth), border);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - c_BorderWidth, rect.width, c_BorderWidth), border);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, c_BorderWidth, rect.height), border);
                EditorGUI.DrawRect(new Rect(rect.xMax - c_BorderWidth, rect.y, c_BorderWidth, rect.height), border);
            }

            /// <summary>
            /// 绘制提示框（Rect 版，PropertyDrawer 专用）。
            /// 视觉与 GUILayout 版完全一致：同字号、同边距、同颜色、同图标、同边框。
            /// 渲染顺序：背景 → 边框 → 标题（含图标留位）→ 正文 → 图标。
            /// </summary>
            /// <param name="rect">绘制区域。</param>
            /// <param name="messageType">消息类型。</param>
            /// <param name="message">单条提示内容。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            public static void HelpBox(Rect rect, MessageType messageType, string message, bool disableOnPlaying = true)
            {
                if (string.IsNullOrEmpty(message))
                {
                    return;
                }
                HelpBox(rect, messageType, new[] { message }, disableOnPlaying);
            }

            /// <summary>
            /// 绘制提示框（Rect 多条文本版，PropertyDrawer 专用）。
            /// 视觉与 GUILayout 版完全一致：同字号、同边距、同颜色、同图标、同边框。
            /// 渲染顺序：背景 → 边框 → 标题（含图标留位）→ 正文各条 → 图标。
            /// </summary>
            /// <param name="rect">绘制区域。</param>
            /// <param name="messageType">消息类型。</param>
            /// <param name="messages">提示内容（每条作为独立段落渲染）。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            public static void HelpBox(Rect rect, MessageType messageType, string[] messages, bool disableOnPlaying = true)
            {
                if (messages == null || messages.Length == 0)
                {
                    return;
                }

                bool disabled = disableOnPlaying && EditorApplication.isPlaying;

                using (new EditorGUI.DisabledScope(disabled))
                {
                    if (Event.current.type == EventType.Repaint)
                    {
                        DrawBackground(rect, messageType);
                        DrawBorder(rect);
                    }

                    string titleText = GetTextTitle(messageType);
                    GUIStyle firstLineStyle = GetFirstLineStyle(messageType);
                    GUIStyle restStyle = GetRestStyle(messageType);

                    float titleH = c_FontSize + 2f;
                    float textX = rect.x + c_Padding;
                    float textWidth = rect.width - c_Padding * 2f;
                    float y = rect.y + c_Padding;

                    // 标题行（含图标留位）
                    Rect titleRect = new Rect(textX, y, textWidth, titleH);
                    GUI.Label(titleRect, titleText, firstLineStyle);
                    y += titleH;

                    // 正文各条
                    foreach (string msg in messages)
                    {
                        if (string.IsNullOrEmpty(msg))
                        {
                            continue;
                        }
                        float msgH = restStyle.CalcHeight(new GUIContent(msg), textWidth);
                        Rect msgRect = new Rect(textX, y, textWidth, msgH);
                        GUI.Label(msgRect, msg, restStyle);
                        y += msgH;
                    }
                }

                // 图标在 DisabledScope 外绘制，与 GUILayout 版行为一致：
                // 避免 DisabledScope 对 GUI.color 的灰化叠加影响图标着色。
                if (Event.current.type == EventType.Repaint)
                {
                    DrawIcon(rect, messageType, firstLineStyle: GetFirstLineStyle(messageType));
                }
            }

            /// <summary>
            /// 计算单条文本提示框所需像素高度（供 PropertyDrawer.GetPropertyHeight 使用）。
            /// </summary>
            /// <param name="messageType">消息类型。</param>
            /// <param name="message">正文内容。</param>
            /// <param name="availableWidth">可用宽度（像素）。</param>
            /// <returns>HelpBox 所需总高度（像素）。</returns>
            public static float CalcHelpBoxHeight(MessageType messageType, string message, float availableWidth)
            {
                if (string.IsNullOrEmpty(message))
                {
                    return 0f;
                }
                return CalcHelpBoxHeight(messageType, new[] { message }, availableWidth);
            }

            /// <summary>
            /// 计算多条文本提示框所需像素高度（供 PropertyDrawer.GetPropertyHeight 使用）。
            /// </summary>
            /// <param name="messageType">消息类型。</param>
            /// <param name="messages">提示内容数组。</param>
            /// <param name="availableWidth">可用宽度（像素）。</param>
            /// <returns>HelpBox 所需总高度（像素）。</returns>
            public static float CalcHelpBoxHeight(MessageType messageType, string[] messages, float availableWidth)
            {
                if (messages == null || messages.Length == 0)
                {
                    return 0f;
                }

                GUIStyle restStyle = GetRestStyle(messageType);
                float contentWidth = availableWidth - c_Padding * 2f;
                float titleH = c_FontSize + 2f;
                float totalH = c_Padding + titleH;

                foreach (string msg in messages)
                {
                    if (string.IsNullOrEmpty(msg))
                    {
                        continue;
                    }
                    totalH += restStyle.CalcHeight(new GUIContent(msg), contentWidth);
                }

                totalH += c_Padding;
                return totalH;
            }

        }
    }
}
