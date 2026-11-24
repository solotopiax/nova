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
    /// 支持启动及跨日自动弹出，也支持手动打开。
    /// </summary>
    public sealed partial class CheckUpdateWindow : EditorWindow
    {
        /// <summary>
        /// 窗口启用时清空暂未保存的跳过选择。
        /// </summary>
        private void OnEnable()
        {
            m_SkipDay = default;
        }

        /// <summary>
        /// 窗口关闭或禁用时提交所显示的两个来源列表，按勾选日保存跳过记录。
        /// </summary>
        private void OnDisable()
        {
            if (m_SkipDay != default && (m_ExternalItems?.Count > 0 || m_InternalItems?.Count > 0))
            {
                EditorUtil.CheckUpdate.MarkSkip(m_ExternalItems, m_InternalItems, m_SkipDay);
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

            if (Event.current.type == EventType.Repaint && m_Items.Exists(HasAvailableUpgrade))
            {
                Repaint();
            }
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
