/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyInputDialog.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 通用单行文本输入弹窗（模态 EditorWindow）
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 通用单行文本输入弹窗。
    /// 使用内部状态机实现同步返回：Show() 打开窗口并阻塞直到用户确认或取消。
    /// 用户点击 OK / 按 Enter 时返回输入值；点击取消 / 按 Escape / 关闭窗口时返回 null。
    /// </summary>
    internal sealed class PipifyInputDialog : EditorWindow
    {
        /// <summary>
        /// 窗口固定宽度。
        /// </summary>
        private const float c_WindowWidth = 360f;

        /// <summary>
        /// 窗口固定高度。
        /// </summary>
        private const float c_WindowHeight = 96f;

        /// <summary>
        /// 输入框标签文本。
        /// </summary>
        private string m_Label;

        /// <summary>
        /// 当前输入框内容。
        /// </summary>
        private string m_Value = string.Empty;

        /// <summary>
        /// 用户操作后的结果；null 表示取消，非 null 表示确认内容（含空字符串）。
        /// </summary>
        private static string s_Result;

        /// <summary>
        /// 是否已收到用户操作（确认或取消）。
        /// </summary>
        private static bool s_Closed;

        /// <summary>
        /// 弹出输入对话框并同步等待用户操作，返回用户输入结果。
        /// </summary>
        /// <param name="title">窗口标题。</param>
        /// <param name="label">输入框前的提示文字。</param>
        /// <param name="initialValue">输入框初始值。</param>
        /// <returns>用户确认时返回输入字符串；取消或关闭时返回 null。</returns>
        public static string Show(string title, string label, string initialValue = "")
        {
            s_Result = null;
            s_Closed = false;

            PipifyInputDialog dialog = CreateInstance<PipifyInputDialog>();
            dialog.titleContent = new GUIContent(title);
            dialog.m_Label = label;
            dialog.m_Value = initialValue ?? string.Empty;

            // 计算居中位置
            Vector2 center = new Vector2(Screen.currentResolution.width * 0.5f, Screen.currentResolution.height * 0.5f);
            Rect windowRect = new Rect(center.x - c_WindowWidth * 0.5f, center.y - c_WindowHeight * 0.5f, c_WindowWidth, c_WindowHeight);
            dialog.minSize = new Vector2(c_WindowWidth, c_WindowHeight);
            dialog.maxSize = new Vector2(c_WindowWidth, c_WindowHeight);
            dialog.position = windowRect;

            // ShowModal 阻塞调用直到窗口关闭（Unity Editor 内部消息泵继续工作）
            dialog.ShowModal();

            return s_Result;
        }

        /// <summary>
        /// 绘制对话框内容：输入框 + 确认 / 取消按钮行。
        /// </summary>
        private void OnGUI()
        {
            EditorUtil.Draw.Space(12f);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(12f);
                EditorUtil.Draw.Layout.Vertical(() =>
                {
                    // 标签 + 输入框同行
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Label(m_Label, false, GUILayout.Width(80f));
                        GUI.SetNextControlName("PipifyInputField");
                        m_Value = EditorUtil.Draw.TextField(m_Value, false, GUILayout.ExpandWidth(true));
                    });

                    EditorUtil.Draw.Space(10f);

                    // 按钮行：右对齐
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.FlexibleSpace();
                        // Button(label, disableOnPlaying, exitGUI, onClick, options)
                        // exitGUI=false：避免在 ShowModal 内部调用 ExitGUI 导致异常
                        EditorUtil.Draw.Button("取消", false, false, () =>
                        {
                            s_Result = null;
                            s_Closed = true;
                            Close();
                        }, GUILayout.Width(64f));
                        EditorUtil.Draw.Space(6f);
                        EditorUtil.Draw.Button("确认", false, false, () =>
                        {
                            s_Result = m_Value;
                            s_Closed = true;
                            Close();
                        }, GUILayout.Width(64f));
                    });
                });
                EditorUtil.Draw.Space(12f);
            });

            // 自动聚焦输入框（首帧）
            if (Event.current.type == EventType.Layout)
            {
                EditorGUI.FocusTextInControl("PipifyInputField");
            }

            // 键盘快捷键：Enter 确认，Escape 取消
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    s_Result = m_Value;
                    s_Closed = true;
                    Event.current.Use();
                    Close();
                }
                else if (Event.current.keyCode == KeyCode.Escape)
                {
                    s_Result = null;
                    s_Closed = true;
                    Event.current.Use();
                    Close();
                }
            }
        }

        /// <summary>
        /// 窗口被用户直接点 × 关闭时视为取消。
        /// </summary>
        private void OnDestroy()
        {
            if (!s_Closed)
            {
                s_Result = null;
                s_Closed = true;
            }
        }
    }
}
