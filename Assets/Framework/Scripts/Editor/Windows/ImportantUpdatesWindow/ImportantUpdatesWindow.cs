/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ImportantUpdatesWindow.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   升级后按包及正式版本展示的重要内容强制确认窗口
 ***************************************************************/

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>重要声明的展示与人工确认壳层；不提供菜单，不在关闭或失焦回调中写入已读状态。</summary>
    internal sealed class ImportantUpdatesWindow : EditorWindow
    {
        private EditorUtil.ImportantUpdates.Presentation m_Presentation;
        private Vector2 m_Scroll;
        private bool m_Read;
        private string m_Error;
        private GUIStyle m_TitleStyle;
        private GUIStyle m_ContentStyle;
        private GUIStyle m_ContentAreaStyle;
        private GUIStyle m_ScrollStyle;
        private GUIStyle m_VersionStyle;
        private GUIStyle m_SummaryStyle;
        private GUIStyle m_SeparatorStyle;
        private GUIStyle m_ConfirmationStyle;

        /// <summary>由稳定 Editor 的调度器打开模态窗口，退出模态后由本地记录决定是否继续提醒。</summary>
        internal static void Show(EditorUtil.ImportantUpdates.Presentation presentation)
        {
            if (presentation == null || presentation.Notices.Count == 0) return;
            ImportantUpdatesWindow window = CreateInstance<ImportantUpdatesWindow>();
            window.m_Presentation = presentation;
            window.titleContent = new GUIContent("Nova · ImportantUpdates");
            window.minSize = new Vector2(640, 440);
            window.position = new Rect(150, 100, 780, 620);
            window.ShowModal();
        }

        /// <summary>绘制默认全部展开的声明和确认区；正文保留可复制的原始 Markdown，不折叠重要内容。</summary>
        private void OnGUI()
        {
            if (m_Presentation == null)
            {
                Close();
                return;
            }
            EnsureStyles();
            EditorUtil.Draw.Space(14);
            EditorUtil.Draw.Label("重要升级说明", m_TitleStyle, false);
            EditorUtil.Draw.Space(10);
            EditorUtil.Draw.Space(10);
            DrawFullWidthSeparator(true);
            int packageCount = m_Presentation.Notices.Select(item => item.PackageName).Distinct().Count();
            EditorUtil.Draw.Layout.Vertical(m_ContentAreaStyle, () =>
                EditorUtil.Draw.Label($"本次涉及 {packageCount} 个包、{m_Presentation.Notices.Count} 个版本的重要内容，请阅读影响和操作要求。", m_SummaryStyle, false));
            EditorUtil.Draw.Space(6);
            DrawFullWidthSeparator();

            // Draw 暂无 ScrollView 原语，沿用 CheckUpdateWindow 的直接滚动容器，其余业务绘制统一复用 Draw。
            Color previousBackground = GUI.backgroundColor;
            try
            {
                GUI.backgroundColor = new Color(0.14f, 0.14f, 0.14f, 1);
                m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll, m_ScrollStyle);
            }
            finally
            {
                GUI.backgroundColor = previousBackground;
            }
            EditorUtil.Draw.Layout.Vertical(m_ContentAreaStyle, () =>
            {
                string previousPackage = null;
                foreach (EditorUtil.ImportantUpdates.Notice notice in m_Presentation.Notices)
                {
                    if (previousPackage != notice.PackageName)
                    {
                        if (previousPackage != null)
                        {
                            EditorUtil.Draw.Space(16);
                            EditorUtil.Draw.Label(string.Empty, m_SeparatorStyle, false, GUILayout.Height(4));
                        }
                        EditorUtil.Draw.Space(12);
                        string transition = string.IsNullOrEmpty(notice.FromVersion) ? "首次本地检查，当前 " + notice.InstalledVersion :
                            notice.FromVersion == notice.InstalledVersion ? "当前 " + notice.InstalledVersion : notice.FromVersion + " → " + notice.InstalledVersion;
                        EditorUtil.Draw.Label(notice.DisplayName + " (" + notice.PackageName + ")    " + transition, EditorStyles.boldLabel, false);
                        previousPackage = notice.PackageName;
                    }
                    else
                    {
                        EditorUtil.Draw.Space(10);
                        EditorUtil.Draw.Label(string.Empty, m_SeparatorStyle, false, GUILayout.Height(1));
                    }
                    EditorUtil.Draw.Space(10);
                    EditorUtil.Draw.Label("版本 " + notice.Version, m_VersionStyle, false);
                    float height = m_ContentStyle.CalcHeight(new GUIContent(notice.Body), Mathf.Max(160, position.width - 70));
                    EditorUtil.Draw.SelectableLabel(notice.Body, m_ContentStyle, false, GUILayout.MinHeight(height + 6));
                }
            });
            EditorGUILayout.EndScrollView();

            DrawFullWidthSeparator();
            EditorUtil.Draw.Layout.Vertical(m_ConfirmationStyle, () =>
            {
                EditorUtil.Draw.Label("阅读确认", EditorStyles.boldLabel, false);
                EditorUtil.Draw.Space(6);
                if (!string.IsNullOrEmpty(m_Error)) EditorUtil.Draw.HelpBox(MessageType.Error, new[] { m_Error }, false);
                m_Read = EditorUtil.Draw.ToggleLeft("我已阅读上述重要升级说明", m_Read, false);
                EditorUtil.Draw.Space(10);
                EditorUtil.Draw.DisabledGroup(!m_Read, () =>
                {
                    // 模态循环内不调用 ExitGUI；仅成功保存后关闭，原生关闭按钮不会触发此路径。
                    EditorUtil.Draw.Button("我已阅读并知晓", false, false, Confirm, GUILayout.Height(30));
                });
            });
            EditorUtil.Draw.Space(6);
        }

        /// <summary>按窗口完整宽度绘制功能区分隔线，不继承布局标签的左右留白。</summary>
        private void DrawFullWidthSeparator(bool header = false)
        {
            EditorUtil.Draw.Space(2);
            Rect rect = GUILayoutUtility.GetLastRect();
            Color previousColor = GUI.color;
            try
            {
                if (header) GUI.color = Color.black;
                EditorUtil.Draw.Label(new Rect(0, rect.y, position.width, 2), string.Empty, m_SeparatorStyle, false);
            }
            finally
            {
                GUI.color = previousColor;
            }
        }

        /// <summary>懒初始化 GUIStyle，避免在 OnEnable 时访问尚未就绪的 EditorStyles。</summary>
        private void EnsureStyles()
        {
            if (m_TitleStyle != null && m_ScrollStyle != null) return;
            m_TitleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            int indent = Mathf.RoundToInt(EditorStyles.label.CalcSize(new GUIContent("汉")).x);
            // 左侧对齐确认区的文字，右侧补偿滚动条占用的宽度。
            m_ContentAreaStyle = new GUIStyle { padding = new RectOffset(20, 8, 0, 0) };
            m_ScrollStyle = new GUIStyle { normal = { background = Texture2D.whiteTexture } };
            m_VersionStyle = new GUIStyle(EditorStyles.boldLabel) { padding = new RectOffset(2 + indent, 2, 0, 0) };
            m_ContentStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { richText = false, padding = new RectOffset(2 + indent * 2, 2, 6, 6) };
            m_SummaryStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 11, wordWrap = false, normal = { textColor = Color.white } };
            m_SeparatorStyle = new GUIStyle { normal = { background = Texture2D.grayTexture }, margin = new RectOffset(8, 8, 0, 0) };
            m_ConfirmationStyle = new GUIStyle { padding = new RectOffset(12, 12, 10, 12), margin = new RectOffset(8, 8, 10, 0) };
        }

        /// <summary>用户明确确认后委托工具层保存，失败时保留内容并显示可重试提示。</summary>
        private void Confirm()
        {
            if (!m_Read) return;
            if (EditorUtil.ImportantUpdates.TryAcknowledge(m_Presentation, out m_Error)) Close();
        }
    }
}
