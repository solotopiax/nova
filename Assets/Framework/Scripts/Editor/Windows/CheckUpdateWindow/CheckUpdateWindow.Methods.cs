/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CheckUpdateWindow.Methods.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   CheckUpdate 窗口私有方法
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public sealed partial class CheckUpdateWindow : EditorWindow
    {
        /// <summary>
        /// 手动打开：窗口自行触发一次 CheckAsync，展示拉取中状态。
        /// </summary>
        public static void Open()
        {
            s_Instance = GetWindow<CheckUpdateWindow>(true, c_WindowTitle, true);
            s_Instance.minSize = new Vector2(c_MinWidth, c_MinHeight);
            if (s_Instance.m_IsChecking) { s_Instance.Focus(); return; }
            s_Instance.m_Items = null;
            s_Instance.m_ExternalItems = null;
            s_Instance.m_InternalItems = null;
            s_Instance.m_IsChecking = true;
            s_Instance.Repaint();
            s_Instance.StartCheckAsync();
        }

        /// <summary>
        /// 带参数打开：启动钩子传入已知更新列表，直接展示无需再次拉取。
        /// </summary>
        /// <param name="items">待展示的更新列表。</param>
        public static void Open(List<EditorUtil.CheckUpdate.UpdateInfo> items)
        {
            Open(items, new List<EditorUtil.CheckUpdate.UpdateInfo>());
        }

        /// <summary>
        /// 带参数打开：启动钩子传入已知更新列表，直接展示无需再次拉取。
        /// </summary>
        public static void Open(List<EditorUtil.CheckUpdate.UpdateInfo> externalItems, List<EditorUtil.CheckUpdate.UpdateInfo> internalItems)
        {
            s_Instance = GetWindow<CheckUpdateWindow>(true, c_WindowTitle, true);
            s_Instance.minSize = new Vector2(c_MinWidth, c_MinHeight);
            s_Instance.m_ExternalItems = externalItems ?? new List<EditorUtil.CheckUpdate.UpdateInfo>();
            s_Instance.m_InternalItems = internalItems ?? new List<EditorUtil.CheckUpdate.UpdateInfo>();
            s_Instance.m_Items = MergeItems(s_Instance.m_ExternalItems, s_Instance.m_InternalItems);
            s_Instance.m_IsChecking = false;
            s_Instance.m_DontShowAgain = false;
            s_Instance.Repaint();
        }

        /// <summary>
        /// 异步执行版本检查并在主线程刷新窗口。
        /// </summary>
        private async void StartCheckAsync()
        {
            try
            {
                List<EditorUtil.CheckUpdate.UpdateInfo> externalItems = await EditorUtil.CheckUpdate.CheckExternalAsync();
                List<EditorUtil.CheckUpdate.UpdateInfo> internalItems = await EditorUtil.CheckUpdate.CheckInternalAsync();
                if (s_Instance == null)
                {
                    return;
                }

                s_Instance.m_ExternalItems = externalItems ?? new List<EditorUtil.CheckUpdate.UpdateInfo>();
                s_Instance.m_InternalItems = internalItems ?? new List<EditorUtil.CheckUpdate.UpdateInfo>();
                s_Instance.m_Items = MergeItems(s_Instance.m_ExternalItems, s_Instance.m_InternalItems);
                s_Instance.m_DontShowAgain = false;
            }
            catch (System.Exception e)
            {
                Log.Warning(LogTag.Editor, "CheckUpdateWindow.StartCheckAsync 检查失败: {0}", e.Message);
                if (s_Instance != null)
                {
                    s_Instance.m_ExternalItems = new List<EditorUtil.CheckUpdate.UpdateInfo>();
                    s_Instance.m_InternalItems = new List<EditorUtil.CheckUpdate.UpdateInfo>();
                    s_Instance.m_Items = new List<EditorUtil.CheckUpdate.UpdateInfo>();
                }
            }
            finally
            {
                if (s_Instance != null)
                {
                    s_Instance.m_IsChecking = false;
                    s_Instance.Repaint();
                }
            }
        }

        /// <summary>
        /// 延迟初始化 GUIStyle，避免 EditorStyles 未就绪时创建。
        /// </summary>
        private void EnsureStyles()
        {
            if (m_HeaderStyle != null)
            {
                return;
            }

            m_HeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };

            m_LatestStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = s_LatestColor }
            };

            m_EmptyStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
            };

            m_OpenPlugPalsStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = s_OpenPlugPalsTextColor },
                hover = { textColor = s_OpenPlugPalsTextColor },
                active = { textColor = s_OpenPlugPalsTextColor },
                focused = { textColor = s_OpenPlugPalsTextColor },
            };

            m_RightAlignedStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleRight,
            };

            m_RightAlignedLatestStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = s_LatestColor },
            };
        }

        /// <summary>
        /// 绘制顶部标题区域（稍大字号 + 分隔线；有可用更新时右侧显示 Open PlugPals 按钮）。
        /// </summary>
        private void DrawHeader()
        {
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(8f);
                EditorUtil.Draw.Label("可用版本更新", m_HeaderStyle, false, GUILayout.ExpandWidth(false));

                if (!m_IsChecking && m_Items != null && m_Items.Count > 0)
                {
                    EditorUtil.Draw.FlexibleSpace();
                    DrawOpenPlugPalsButton();
                    EditorUtil.Draw.Space(8f);
                }
            });
            EditorUtil.Draw.Space(6f);
            DrawHorizontalLine();
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 绘制绿色 Open PlugPals 按钮（绿色背景 + 绿色粗体文字），点击后打开 PlugPalsWindow。
        /// </summary>
        private void DrawOpenPlugPalsButton()
        {
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = s_OpenPlugPalsColor;
            try
            {
                if (GUILayout.Button(c_OpenPlugPalsLabel, m_OpenPlugPalsStyle, GUILayout.Width(c_OpenPlugPalsWidth)))
                {
                    PlugPalsWindow.Open(showInstalledOnly: true);
                    GUIUtility.ExitGUI();
                }
            }
            finally
            {
                GUI.backgroundColor = prev;
            }
        }

        /// <summary>
        /// 绘制列表表头一行（包名 / 当前版本 / 最新版本 / 更新日志）。
        /// 包名列左对齐；当前版本、最新版本、更新日志列整体右对齐到窗口右侧。
        /// </summary>
        private void DrawTableHeader()
        {
            float rowHeight = EditorGUIUtility.singleLineHeight;
            Rect rowRect = GUILayoutUtility.GetRect(1f, rowHeight, GUILayout.ExpandWidth(true));

            GUIStyle rightBold = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight };

            Rect packageRect, currentRect, latestRect, changelogRect;
            ComputeColumnRects(rowRect, out packageRect, out currentRect, out latestRect, out changelogRect);

            EditorGUI.LabelField(packageRect, "包名", EditorStyles.boldLabel);
            EditorGUI.LabelField(currentRect, "当前版本", rightBold);
            EditorGUI.LabelField(latestRect, "最新版本", rightBold);
            // 第四列（更新日志按钮位置）表头不显示文字，留空。

            EditorUtil.Draw.Space(2f);
        }

        /// <summary>
        /// 计算四列（包名/当前版本/最新版本/更新日志）的子矩形布局：
        /// 包名列从左侧 padding 开始；当前版本、最新版本、更新日志按钮三列以右边缘为锚点
        /// 按 c_ColStep 等距排列（相邻两列右边缘间距相等），各列内容右对齐锚定在自身右边缘。
        /// </summary>
        /// <param name="rowRect">行外矩形。</param>
        /// <param name="packageRect">包名列矩形。</param>
        /// <param name="currentRect">当前版本列矩形。</param>
        /// <param name="latestRect">最新版本列矩形。</param>
        /// <param name="changelogRect">更新日志按钮列矩形。</param>
        private void ComputeColumnRects(Rect rowRect, out Rect packageRect, out Rect currentRect, out Rect latestRect, out Rect changelogRect)
        {
            const float leftPad = 8f;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float contentY = rowRect.y + (rowRect.height - lineHeight) * 0.5f;

            // 三列右边缘按 c_ColStep 等距排列，与各列自身宽度无关。
            float changelogRight = rowRect.xMax - c_RightPadding;
            float latestRight = changelogRight - c_ColStep;
            float currentRight = latestRight - c_ColStep;
            float changelogLeft = changelogRight - c_ColChangelogBtnWidth;
            float latestLeft = latestRight - c_ColVersionWidth;
            float currentLeft = currentRight - c_ColVersionWidth;

            float packageLeft = rowRect.x + leftPad;
            float packageWidth = Mathf.Max(0f, currentLeft - packageLeft - 4f);

            packageRect = new Rect(packageLeft, contentY, packageWidth, lineHeight);
            currentRect = new Rect(currentLeft, contentY, c_ColVersionWidth, lineHeight);
            latestRect = new Rect(latestLeft, contentY, c_ColVersionWidth, lineHeight);
            changelogRect = new Rect(changelogLeft, contentY, c_ColChangelogBtnWidth, lineHeight);
        }

        /// <summary>
        /// 绘制单条包更新行（斑马纹背景 + Latest 列绿色高亮 + 更新日志按钮）。
        /// 使用 GUILayoutUtility.GetRect 取得行矩形，直接 EditorGUI.LabelField 以固定子矩形绘制，
        /// 避免在 ScrollView 内使用 GUILayout.BeginArea 导致内容错位不可见。
        /// </summary>
        /// <param name="info">更新信息。</param>
        /// <param name="index">行索引（用于斑马纹判断）。</param>
        private void DrawRow(EditorUtil.CheckUpdate.UpdateInfo info, int index)
        {
            float rowHeight = EditorGUIUtility.singleLineHeight + c_RowSpacing;
            Rect rowRect = GUILayoutUtility.GetRect(1f, rowHeight, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint && index % 2 == 0)
            {
                EditorGUI.DrawRect(rowRect, s_RowEvenColor);
            }

            Rect packageRect, currentRect, latestRect, changelogRect;
            ComputeColumnRects(rowRect, out packageRect, out currentRect, out latestRect, out changelogRect);

            Color prevColor = GUI.contentColor;
            GUI.contentColor = IsInternalItem(info) ? s_InternalPackageNameColor : Color.white;
            EditorGUI.LabelField(packageRect, info.PackageName);
            GUI.contentColor = prevColor;
            EditorGUI.LabelField(currentRect, info.CurrentVersion ?? "—", m_RightAlignedStyle);
            EditorGUI.LabelField(latestRect, info.LatestVersion, m_RightAlignedLatestStyle);

            if (GUI.Button(changelogRect, c_ChangelogLabel))
            {
                OpenChangelogAsync(info);
            }
        }

        /// <summary>
        /// 异步获取并用系统默认程序打开指定包最新版本的更新日志。
        /// 失败或包内无 CHANGELOG.md 时用 DisplayDialog 提示，不污染窗口内容区。
        /// </summary>
        /// <param name="info">更新信息。</param>
        private async void OpenChangelogAsync(EditorUtil.CheckUpdate.UpdateInfo info)
        {
            if (m_IsFetchingChangelog) return;
            m_IsFetchingChangelog = true;
            try
            {
                string path = await EditorUtil.PlugPals.FetchChangelogAsync(GetRegistryUrl(info), info.PackageName, info.LatestVersion, CancellationToken.None);
                if (path == null)
                {
                    EditorUtility.DisplayDialog("更新日志", "该包未发布更新日志。", "确定");
                    return;
                }
                EditorUtility.OpenWithDefaultApp(path);
            }
            finally
            {
                m_IsFetchingChangelog = false;
            }
        }

        /// <summary>
        /// 绘制底部 Footer（启动时不再提示勾选框，右对齐）。
        /// </summary>
        private void DrawFooter()
        {
            DrawHorizontalLine();
            EditorUtil.Draw.Layout.Horizontal(EditorStyles.helpBox, () =>
            {
                // 左侧弹性空白，将勾选框推到右侧
                EditorUtil.Draw.FlexibleSpace();

                // 精确计算 label 宽度 + 勾选框像素，避免 ExpandWidth 留白
                float toggleWidth = EditorStyles.label.CalcSize(new GUIContent(c_FooterToggleLabel)).x + c_FooterToggleExtra;
                m_DontShowAgain = EditorUtil.Draw.ToggleLeft(c_FooterToggleLabel, m_DontShowAgain, false, GUILayout.Width(toggleWidth));
                EditorUtil.Draw.Space(4f);
            });
        }

        /// <summary>
        /// 绘制一条全宽水平分隔线。
        /// </summary>
        private void DrawHorizontalLine()
        {
            Rect lineRect = GUILayoutUtility.GetRect(position.width, 1f);
            EditorGUI.DrawRect(lineRect, new Color(1f, 1f, 1f, 0.12f));
        }

        /// <summary>
        /// 合并外网与内部仓库更新项，统一按包名排序。
        /// </summary>
        private static List<EditorUtil.CheckUpdate.UpdateInfo> MergeItems(
            IReadOnlyList<EditorUtil.CheckUpdate.UpdateInfo> externalItems,
            IReadOnlyList<EditorUtil.CheckUpdate.UpdateInfo> internalItems)
        {
            return EditorUtil.CheckUpdate.MergeAndSortUpdateInfos(externalItems, internalItems);
        }

        /// <summary>
        /// 判定更新项是否来自内部云仓库列表。
        /// </summary>
        private bool IsInternalItem(EditorUtil.CheckUpdate.UpdateInfo info)
        {
            if (info == null || m_InternalItems == null)
            {
                return false;
            }

            for (int i = 0; i < m_InternalItems.Count; i++)
            {
                if (ReferenceEquals(m_InternalItems[i], info))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取更新项对应的仓库地址。
        /// </summary>
        private string GetRegistryUrl(EditorUtil.CheckUpdate.UpdateInfo info)
        {
            EditorUtil.PlugPals.RegistriesConfig registries = EditorUtil.PlugPals.LoadRegistries();
            return IsInternalItem(info) ? registries.internalUrl : registries.externalUrl;
        }
    }
}
