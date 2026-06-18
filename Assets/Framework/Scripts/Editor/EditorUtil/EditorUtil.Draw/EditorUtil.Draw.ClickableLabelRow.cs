/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.ClickableLabelRow.cs
 * author:    taoye
 * created:   2026/5/7
 * descrip:   编辑器绘制工具-整行可点击文字
 ***************************************************************/

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
            /// 绘制一行整宽可点击文字（无按钮状态机，绕开 GUILayout.Button + EditorStyles.label 派生样式的裁剪与宽度计算问题）。
            /// </summary>
            /// <param name="content">显示文本。</param>
            /// <param name="style">文字样式。</param>
            /// <param name="height">行高（像素）。</param>
            /// <returns>本帧在该行内发生鼠标左键点击时返回 true。</returns>
            public static bool ClickableLabelRow(string content, GUIStyle style, float height = 18f)
            {
                Rect rect = GUILayoutUtility.GetRect(new GUIContent(content), style, GUILayout.ExpandWidth(true), GUILayout.Height(height));
                Event e = Event.current;
                bool clicked = false;
                if (e.type == EventType.Repaint)
                {
                    style.Draw(rect, content, false, false, false, false);
                }
                else if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    clicked = true;
                    e.Use();
                }
                return clicked;
            }
        }
    }
}
