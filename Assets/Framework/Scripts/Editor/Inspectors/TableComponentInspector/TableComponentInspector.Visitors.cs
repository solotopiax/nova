/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   表格组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class TableComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 目录树折叠状态：键为文件夹完整路径，值为该层 Foldout 是否展开。
        /// </summary>
        private readonly Dictionary<string, bool> m_FolderFoldoutState = new Dictionary<string, bool>();

        /// <summary>
        /// 当前选中的 Table 管理器类型名称。
        /// </summary>
        private SerializedProperty m_CurManagerTypeName;

        /// <summary>
        /// TableSettings 序列化对象（SourceDirPath、TableUnitsSettings 等）。
        /// </summary>
        private SerializedProperty m_Setting;

        /// <summary>
        /// TableSettings.SourceDirPath 的缓存属性，避免每帧 FindPropertyRelative。
        /// </summary>
        private SerializedProperty m_SourceDirPath;

        /// <summary>
        /// TableSettings.TableUnitsSettings 的缓存属性，避免每帧 FindPropertyRelative。
        /// </summary>
        private SerializedProperty m_TableUnitsSettings;

        /// <summary>
        /// 程序集中所有实现 ITableManager 的类型名称列表，用于下拉选择。
        /// </summary>
        private List<string> m_ManagerTypeNames;

        /// <summary>
        /// Luban _configs/ 目录是否存在。
        /// </summary>
        private bool m_IsLubanConfigExists;

        /// <summary>
        /// FileWatcher 变更回调（缓存以便 Unwatch）。
        /// </summary>
        private Action m_LubanFileWatcherCallback;

        /// <summary>
        /// 已注册 FileWatcher 的 _configs/ 目录路径（缓存以确保 Unwatch 路径一致）。
        /// </summary>
        private string m_WatchedConfigDirPath;

        /// <summary>
        /// 运行时信息区域：已加载数据表列表的折叠状态，默认收起。
        /// </summary>
        private bool m_RuntimeTablesFoldout = false;

    }
}
