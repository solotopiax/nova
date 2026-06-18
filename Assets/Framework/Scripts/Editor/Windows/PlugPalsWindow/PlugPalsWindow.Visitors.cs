/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlugPalsWindow.Visitors.cs
 * author:    taoye
 * created:   2026/4/8
 * descrip:   PlugPals 窗口字段声明
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public sealed partial class PlugPalsWindow : EditorWindow
    {
        /// <summary>
        /// 公网 registry 根地址，OnEnable 由 EditorUtil.PlugPals.LoadRegistries() 填充。
        /// </summary>
        private string m_ExternalUrl = "";

        /// <summary>
        /// 公网 registry 名称。
        /// </summary>
        private string m_ExternalName = "";

        /// <summary>
        /// 内部云 registry 根地址；为空表示未配置内部云。
        /// </summary>
        private string m_InternalUrl = "";

        /// <summary>
        /// 内部云 registry 名称。
        /// </summary>
        private string m_InternalName = "";

        /// <summary>
        /// 菜单路径。
        /// </summary>
        private const string c_MenuPath = "Nova/Open PlugPals";

        /// <summary>
        /// 窗口标题。
        /// </summary>
        private const string c_WindowTitle = "Nova · PlugPals";

        /// <summary>
        /// manifest.json 相对工程根目录路径。
        /// </summary>
        private const string c_ManifestPath = "Packages/manifest.json";

        /// <summary>
        /// 卡片内标签列宽度。
        /// </summary>
        private const float c_LabelWidth = 120f;

        /// <summary>
        /// 列拖拽手柄热区半宽。
        /// </summary>
        private const float c_DragHandleHalfWidth = 4f;

        /// <summary>
        /// 安装/升级、卸载按钮宽度。
        /// </summary>
        private const float c_ColBtnWidth = 60f;

        /// <summary>
        /// UPM 按钮宽度。
        /// </summary>
        private const float c_ColBtnUpmWidth = 50f;

        /// <summary>
        /// 卡片内 Samples 导入按钮宽度（含版本号文案 "导入 Samples (vX.X.X)"）。
        /// </summary>
        private const float c_ColSamplesBtnWidth = 170f;

        /// <summary>
        /// Nova Framework 体系包名前缀，仅此前缀的包在已安装时展示 Samples 导入入口。
        /// </summary>
        private const string c_NovaFrameworkPackagePrefix = "com.solotopia.nova.framework";

        /// <summary>
        /// 展开卡片内"更新日志"按钮宽度。
        /// </summary>
        private const float c_ColChangelogBtnWidth = 90f;

        /// <summary>
        /// 行高度。
        /// </summary>
        private const float c_RowHeight = 22f;

        /// <summary>
        /// 详情按钮行统一高度。安装/卸载/UPM/导入 Samples/更新日志 5 个按钮共享。
        /// </summary>
        private const float c_RowBtnHeight = 22f;

        /// <summary>
        /// 分隔线颜色。
        /// </summary>
        private static readonly Color s_SeparatorColor = new Color(1f, 1f, 1f, 0.12f);

        /// <summary>
        /// 内部云仓库条目名称色（浅橙色）。
        /// </summary>
        private static readonly Color s_InternalPackageNameColor = new Color(1f, 0.82f, 0.62f);

        /// <summary>
        /// 五条分隔线的 X 坐标（从左到右：描述左边缘、本地版本左边缘、最新版本左边缘、版本操作左边缘、窗口右边缘）。
        /// 索引 0-3 可拖拽，索引 4 固定在窗口右边缘。
        /// </summary>
        private float[] m_ColBorders;

        /// <summary>
        /// 当前正在拖拽的分隔线索引（-1 表示未拖拽）。
        /// </summary>
        private int m_DraggingCol = -1;

        /// <summary>
        /// 拖拽起始鼠标 X 坐标。
        /// </summary>
        private float m_DragStartMouseX;

        /// <summary>
        /// 拖拽起始时的分隔线 X 坐标。
        /// </summary>
        private float m_DragStartBorderX;

        /// <summary>
        /// 窗口单例。
        /// </summary>
        private static PlugPalsWindow s_Instance;

        /// <summary>
        /// 外网仓库包条目列表。
        /// </summary>
        private List<EditorUtil.PlugPals.PackageDisplayEntry> m_ExternalPackages;

        /// <summary>
        /// 内部云仓库包条目列表。
        /// </summary>
        private List<EditorUtil.PlugPals.PackageDisplayEntry> m_InternalPackages;

        /// <summary>
        /// 经搜索过滤后的包条目列表。
        /// </summary>
        private List<EditorUtil.PlugPals.PackageDisplayEntry> m_FilteredPackages;

        /// <summary>
        /// 滚动位置。
        /// </summary>
        private Vector2 m_ScrollPos;

        /// <summary>
        /// 搜索框文本。
        /// </summary>
        private string m_SearchText = "";

        /// <summary>
        /// 是否正在请求远程包列表。
        /// </summary>
        private bool m_IsFetching;

        /// <summary>
        /// 错误信息（非空表示上次请求失败）。
        /// </summary>
        private string m_ErrorMessage;

        /// <summary>
        /// 外网仓库请求失败信息（可部分降级显示）。
        /// </summary>
        private string m_ExternalErrorMessage;

        /// <summary>
        /// 内部云仓库请求失败信息（可部分降级显示）。
        /// </summary>
        private string m_InternalErrorMessage;

        /// <summary>
        /// 用于取消进行中请求的令牌源。
        /// </summary>
        private CancellationTokenSource m_CancellationTokenSource;

        /// <summary>
        /// 是否有包正在执行安装/卸载/升级操作（防止重复操作）。
        /// </summary>
        private bool m_IsOperating;

        /// <summary>
        /// 当前选中的分类标签。
        /// </summary>
        private EditorUtil.PlugPals.PackageCategory m_SelectedCategory = EditorUtil.PlugPals.PackageCategory.All;

        /// <summary>
        /// 是否仅显示已安装模块（右侧独立页签，开启后忽略分类过滤）。
        /// </summary>
        private bool m_ShowInstalledOnly;

        /// <summary>
        /// 是否仅显示内部云仓库页签。
        /// </summary>
        private bool m_ShowInternalOnly;

        /// <summary>
        /// 窗口主标题样式（居中加粗大字）。
        /// </summary>
        private GUIStyle m_MainTitleStyle;

        /// <summary>
        /// 分类标签按钮样式（选中态）。
        /// </summary>
        private GUIStyle m_CategoryActiveStyle;

        /// <summary>
        /// 分类标签按钮样式（未选中态）。
        /// </summary>
        private GUIStyle m_CategoryNormalStyle;

        /// <summary>
        /// 卡片折叠标题样式。
        /// </summary>
        private GUIStyle m_TitleStyle;

        /// <summary>
        /// 卡片内标签样式（浅蓝色粗体）。
        /// </summary>
        private GUIStyle m_LabelStyle;

        /// <summary>
        /// 卡片内值样式。
        /// </summary>
        private GUIStyle m_ValueStyle;

        /// <summary>
        /// 描述文本样式（灰色自动换行）。
        /// </summary>
        private GUIStyle m_DescStyle;

        /// <summary>
        /// 版本列表头样式（居中大号白字）。
        /// </summary>
        private GUIStyle m_VersionHeaderStyle;

        /// <summary>
        /// 折叠行描述样式（小字灰色，单行截断）。
        /// </summary>
        private GUIStyle m_RowDescStyle;

        /// <summary>
        /// 折叠行版本号样式（居中对齐）。
        /// </summary>
        private GUIStyle m_RowVersionStyle;

        /// <summary>
        /// 包展开/折叠状态（包名 -> 是否展开）。
        /// </summary>
        private Dictionary<string, bool> m_FoldoutStates = new Dictionary<string, bool>();

        /// <summary>
        /// 更新日志拉取互斥（防止快速双击并发下载同一 tarball）。
        /// </summary>
        private bool m_IsFetchingChangelog;
    }
}
