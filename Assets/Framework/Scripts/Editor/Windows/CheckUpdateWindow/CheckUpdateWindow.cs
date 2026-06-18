/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CheckUpdateWindow.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   Nova 包版本更新提示窗口
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Nova 包版本更新提示窗口，展示所有已安装包中存在新版本的条目。
    /// 支持启动时自动弹出（由 EditorUtil.CheckUpdate 钩子触发）和手动打开两种入口。
    /// </summary>
    public sealed partial class CheckUpdateWindow : EditorWindow
    {
        /// <summary>
        /// 窗口启用时重置 DontShowAgain 状态。
        /// </summary>
        private void OnEnable()
        {
            m_DontShowAgain = false;
        }

        /// <summary>
        /// 窗口关闭时若已勾选"启动时不再提示"，将当前列表写入跳过配置。
        /// </summary>
        private void OnDisable()
        {
            if (m_DontShowAgain)
            {
                if (m_ExternalItems != null && m_ExternalItems.Count > 0)
                {
                    EditorUtil.CheckUpdate.MarkSkip(m_ExternalItems, false);
                }

                if (m_InternalItems != null && m_InternalItems.Count > 0)
                {
                    EditorUtil.CheckUpdate.MarkSkip(m_InternalItems, true);
                }
            }
        }

        /// <summary>
        /// 绘制窗口界面。
        /// </summary>
        private void OnGUI()
        {
            EnsureStyles();
            DrawHeader();

            if (m_IsChecking)
            {
                // 拉取中空态
                GUILayout.FlexibleSpace();
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    GUILayout.FlexibleSpace();
                    EditorUtil.Draw.Label("正在检查更新…", m_EmptyStyle, false);
                    GUILayout.FlexibleSpace();
                });
                GUILayout.FlexibleSpace();
                Repaint();
                DrawFooter();
                return;
            }

            if (m_Items == null || m_Items.Count == 0)
            {
                // 无更新空态
                GUILayout.FlexibleSpace();
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    GUILayout.FlexibleSpace();
                    EditorUtil.Draw.Label("所有插件包均已是最新版本 ✓", m_EmptyStyle, false);
                    GUILayout.FlexibleSpace();
                });
                GUILayout.FlexibleSpace();
                DrawFooter();
                return;
            }

            DrawTableHeader();

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            for (int i = 0; i < m_Items.Count; i++)
            {
                DrawRow(m_Items[i], i);
            }
            EditorUtil.Draw.Space(4f);
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        /// <summary>
        /// 窗口销毁时清除单例引用。
        /// </summary>
        private void OnDestroy()
        {
            s_Instance = null;
        }
    }
}
