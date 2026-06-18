/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyWindow.Visitors.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 窗口字段声明
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class PipifyWindow : EditorWindow
    {
        /// <summary>
        /// 窗口标题。
        /// </summary>
        private const string c_WindowTitle = "Nova · Pipify";

        /// <summary>
        /// 窗口最小宽度。
        /// </summary>
        private const float c_WindowMinWidth = 1100f;

        /// <summary>
        /// 窗口最小高度。
        /// </summary>
        private const float c_WindowMinHeight = 640f;

        /// <summary>
        /// 日志标签前缀，用于标识 Pipify 窗口相关日志来源。
        /// </summary>
        private const string c_LogTag = "[PipifyWindow]";

        /// <summary>
        /// 当前加载的 PipifySettingsSO 资产引用。
        /// </summary>
        private PipifySettingsSO m_Settings;

        /// <summary>
        /// 与 m_Settings 绑定的 SerializedObject。
        /// </summary>
        private SerializedObject m_SettingsSO;

        /// <summary>
        /// 面板是否有未保存的改动（窗口关闭即丢弃，不持久化）。
        /// </summary>
        private bool m_IsDirty;

        /// <summary>
        /// 左侧列表当前选中的 Batch 索引；-1 表示无选中项。
        /// </summary>
        private int m_SelectedBatchIndex = -1;

        /// <summary>
        /// 存档文件行标签列宽。
        /// </summary>
        private const float c_TopBarLabelWidth = 100f;

        /// <summary>
        /// 存档文件行普通按钮宽度。
        /// </summary>
        private const float c_TopBarButtonWidth = 64f;

        /// <summary>
        /// 存档文件行宽按钮宽度（如"打开文件夹"）。
        /// </summary>
        private const float c_TopBarWideButtonWidth = 90f;

        /// <summary>
        /// 主标题样式（窗口顶部居中粗体大标题）。
        /// </summary>
        private GUIStyle m_MainTitleStyle;

        /// <summary>
        /// 左侧面板固定宽度。
        /// </summary>
        private const float c_LeftPanelWidth = 260f;

        /// <summary>
        /// 左侧列表每行行高。
        /// </summary>
        private const float c_LeftRowHeight = 22f;

        /// <summary>
        /// 搜索关键词，用于过滤 Batch 列表显示项。
        /// </summary>
        private string m_Filter = string.Empty;

        /// <summary>
        /// 左侧 Batch 列表 ScrollView 的当前滚动位置。
        /// </summary>
        private Vector2 m_LeftScroll;

        /// <summary>
        /// 左侧 Batch 行级可拖拽列表绘制器；行渲染、手柄列、单击/双击/右键、拖拽预览全部由它统一封装。
        /// </summary>
        private EditorUtil.Draw.ReorderableRowList m_LeftRowList;

        /// <summary>
        /// 右侧面板 ScrollView 当前滚动位置。
        /// </summary>
        private Vector2 m_RightScroll;

        /// <summary>
        /// Item 行高（折叠态）。
        /// </summary>
        private const float c_ItemRowHeight = 20f;

        /// <summary>
        /// 参数字段行高。
        /// </summary>
        private const float c_ParamFieldHeight = 18f;

        /// <summary>
        /// 参数区左缩进量（单位像素）。
        /// </summary>
        private const float c_ParamsInset = 16f;

        /// <summary>
        /// 当前绑定 ReorderableList 的 Batch 索引；-1 表示未绑定，切换时触发重建。
        /// </summary>
        private int m_ItemsListBoundBatchIndex = -1;

        /// <summary>
        /// 绑定当前选中 Batch.m_Items 的 ReorderableList；批次切换时重建。
        /// </summary>
        private ReorderableList m_ItemsList;

        /// <summary>
        /// 已展开参数区的 Item 索引集合。
        /// </summary>
        private readonly HashSet<int> m_ExpandedItemIndices = new HashSet<int>();

        /// <summary>
        /// Item 参数对象缓存；key = Item 索引，value = 反序列化的参数实例（或 null 表示无参）。
        /// </summary>
        private readonly Dictionary<int, object> m_ParamsCache = new Dictionary<int, object>();

        /// <summary>
        /// PipifyDropdown 下拉显示项缓存；<接口类型, 显示文本数组（首项为"未配置"）>。
        /// </summary>
        private readonly Dictionary<System.Type, string[]> m_DropdownDisplaysCache = new Dictionary<System.Type, string[]>();

        /// <summary>
        /// PipifyDropdown 下拉对应 FullName 缓存；<接口类型, 与显示项一一对应的 FullName 数组（首项为空串）>。
        /// </summary>
        private readonly Dictionary<System.Type, string[]> m_DropdownFullNamesCache = new Dictionary<System.Type, string[]>();

        /// <summary>
        /// 占位文本绘制样式（PipifyDynamicDefault 字段为空时显示），延迟初始化。
        /// </summary>
        private GUIStyle m_PlaceholderStyle;
    }
}
