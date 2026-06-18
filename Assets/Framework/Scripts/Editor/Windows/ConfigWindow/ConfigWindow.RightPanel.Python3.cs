/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.RightPanel.Python3.cs
 * author:    taoye
 * created:   2026/4/30
 * descrip:   ConfigWindow Python3 环境检测面板
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 绘制 Python3 环境检测面板（状态行 + 版本行 + 重新检测按钮，与 Luban 面板风格一致）。
        /// </summary>
        private void DrawPython3Section()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("Python3 环境检测", m_SectionTitleStyle, false);
            });

            EditorUtil.Draw.Space(8f);
            DrawPython3StatusAndButtons();
        }

        /// <summary>
        /// 绘制 Python3 状态区域：
        /// 第一行：图标 + "Python3" + 状态文本；
        /// 最后一行：重新检测按钮靠右对齐。
        /// </summary>
        private void DrawPython3StatusAndButtons()
        {
            // 第一行：状态图标 + 名称 + 状态文本（左缩进 16f）
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);

                bool available = m_Python3CheckResult.IsAvailable;
                GUIStyle iconStyle = available ? m_StatusReadyStyle : m_StatusErrorStyle;
                string icon = available ? "✓" : "✗";
                EditorUtil.Draw.Label(icon, iconStyle, false, GUILayout.Width(16f));
                EditorUtil.Draw.Label("Python3", false, GUILayout.Width(72f));
                EditorUtil.Draw.Label(ResolvePython3StatusText(), m_DescStyle, false, GUILayout.Width(200f));
            });

            // 操作按钮行：右对齐
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.FlexibleSpace();
                EditorUtil.Draw.Button("重新检测", false, () =>
                {
                    m_Python3CheckResult = EditorUtil.Environment.Python3Checker.Recheck();
                    Repaint();
                }, GUILayout.Width(80f));
            });

            EditorUtil.Draw.Space(2f);
        }

        /// <summary>
        /// 获取 Python3 状态描述文本。
        /// </summary>
        /// <returns>状态描述字符串。</returns>
        private string ResolvePython3StatusText()
        {
            if (!m_Python3CheckResult.IsAvailable) return "未找到 python3，请安装 Python 3.x";
            if (string.IsNullOrEmpty(m_Python3CheckResult.Version)) return "就绪";
            return $"就绪（{m_Python3CheckResult.Version}）";
        }
    }
}
