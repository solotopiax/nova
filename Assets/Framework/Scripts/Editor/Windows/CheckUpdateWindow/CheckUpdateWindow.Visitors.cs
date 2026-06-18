/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CheckUpdateWindow.Visitors.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   CheckUpdate 窗口字段声明
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public sealed partial class CheckUpdateWindow : EditorWindow
    {
        /// <summary>
        /// 窗口标题。
        /// </summary>
        private const string c_WindowTitle = "Nova · CheckUpdate";

        /// <summary>
        /// 窗口最小宽度。
        /// </summary>
        private const float c_MinWidth = 760f;

        /// <summary>
        /// 窗口最小高度。
        /// </summary>
        private const float c_MinHeight = 320f;

        /// <summary>
        /// 行间距（singleLineHeight + 4）。
        /// </summary>
        private const float c_RowSpacing = 4f;

        /// <summary>
        /// Package 列宽。
        /// </summary>
        private const float c_ColPackageWidth = 480f;

        /// <summary>
        /// Current/Latest 列宽。
        /// </summary>
        private const float c_ColVersionWidth = 110f;

        /// <summary>
        /// 标题栏 Open PlugPals 按钮文案。
        /// </summary>
        private const string c_OpenPlugPalsLabel = "Open PlugPals";

        /// <summary>
        /// 标题栏 Open PlugPals 按钮宽度。
        /// </summary>
        private const float c_OpenPlugPalsWidth = 130f;

        /// <summary>
        /// 标题栏 Open PlugPals 按钮背景绿色。
        /// </summary>
        private static readonly Color s_OpenPlugPalsColor = new Color(0.35f, 0.8f, 0.4f);

        /// <summary>
        /// 标题栏 Open PlugPals 按钮文字绿色。
        /// </summary>
        private static readonly Color s_OpenPlugPalsTextColor = new Color(0.2f, 0.85f, 0.3f);

        /// <summary>
        /// 右侧列距窗口右边缘的内边距。
        /// </summary>
        private const float c_RightPadding = 16f;

        /// <summary>
        /// Footer 勾选项文案。
        /// </summary>
        private const string c_FooterToggleLabel = "启动时不再提示这些版本";

        /// <summary>
        /// Footer 勾选项文字宽度之外的额外像素（勾选框 + 内边距）。
        /// </summary>
        private const float c_FooterToggleExtra = 24f;

        /// <summary>
        /// 斑马纹偶数行背景色（浅灰）。
        /// </summary>
        private static readonly Color s_RowEvenColor = new Color(0.22f, 0.22f, 0.22f, 0.4f);

        /// <summary>
        /// Latest 列高亮色（绿色）。
        /// </summary>
        private static readonly Color s_LatestColor = new Color(0.6f, 0.9f, 0.4f);

        /// <summary>
        /// 内部云仓库包名色（浅橙色）。
        /// </summary>
        private static readonly Color s_InternalPackageNameColor = new Color(1f, 0.82f, 0.62f);

        /// <summary>
        /// 窗口单例。
        /// </summary>
        private static CheckUpdateWindow s_Instance;

        /// <summary>
        /// 当前展示的更新列表。
        /// </summary>
        private List<EditorUtil.CheckUpdate.UpdateInfo> m_Items;

        /// <summary>
        /// 外网仓库更新列表。
        /// </summary>
        private List<EditorUtil.CheckUpdate.UpdateInfo> m_ExternalItems;

        /// <summary>
        /// 内部云仓库更新列表。
        /// </summary>
        private List<EditorUtil.CheckUpdate.UpdateInfo> m_InternalItems;

        /// <summary>
        /// 是否正在异步拉取中。
        /// </summary>
        private bool m_IsChecking;

        /// <summary>
        /// 滚动位置。
        /// </summary>
        private Vector2 m_Scroll;

        /// <summary>
        /// 是否勾选"启动时不再提示这些版本"。
        /// </summary>
        private bool m_DontShowAgain;

        /// <summary>
        /// 表头/行样式缓存（避免每帧重建）。
        /// </summary>
        private GUIStyle m_HeaderStyle;

        /// <summary>
        /// Latest 版本列文本样式（绿色）。
        /// </summary>
        private GUIStyle m_LatestStyle;

        /// <summary>
        /// 空态提示文本样式（居中灰色）。
        /// </summary>
        private GUIStyle m_EmptyStyle;

        /// <summary>
        /// Open PlugPals 按钮样式（绿色文字 + 粗体）。
        /// </summary>
        private GUIStyle m_OpenPlugPalsStyle;

        /// <summary>
        /// Current/Latest 列右对齐样式。
        /// </summary>
        private GUIStyle m_RightAlignedStyle;

        /// <summary>
        /// Latest 列右对齐绿色样式。
        /// </summary>
        private GUIStyle m_RightAlignedLatestStyle;

        /// <summary>
        /// 当前版本 / 最新版本 / 更新日志按钮三列右边缘之间的统一步长（相邻两列右边缘的距离）。
        /// 以右边缘为锚点等距排列，保证三列视觉上均匀分布，与各列自身宽度无关。
        /// </summary>
        private const float c_ColStep = 120f;

        /// <summary>
        /// 更新日志按钮列宽（文案缩短为"日志"后缩减至 56）。
        /// </summary>
        private const float c_ColChangelogBtnWidth = 56f;

        /// <summary>
        /// 更新日志按钮文案（仅用于 DrawRow 按钮，表头第四列不显示文字）。
        /// </summary>
        private const string c_ChangelogLabel = "日志";

        /// <summary>
        /// 更新日志拉取互斥（防止快速双击并发下载同一 tarball）。
        /// </summary>
        private bool m_IsFetchingChangelog;
    }
}
