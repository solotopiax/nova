/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.RightPanel.Privacy.cs
 * author:    Codex
 * created:   2026/10/21
 * descrip:   隐私配置的通用分区标题与分隔线
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>沿用隐私页内容边界绘制功能区域标题，不改变配置字段和保存路径。</summary>
        /// <param name="title">使用者可直接理解的功能区域名称。</param>
        private void DrawPrivacySectionTitle(string title)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label(title, m_SectionTitleStyle, false);
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>在相邻独立功能区域之间绘制统一横线与留白，保持内容左右边界一致。</summary>
        private static void DrawPrivacySectionSeparator()
        {
            EditorUtil.Draw.Space(12f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Line();
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(12f);
        }

    }
}
