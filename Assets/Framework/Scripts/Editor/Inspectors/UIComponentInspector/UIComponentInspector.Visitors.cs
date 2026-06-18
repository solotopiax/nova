/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   UI 组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class UIComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 目录树折叠状态：键为文件夹完整路径，值为该层 Foldout 是否展开。
        /// </summary>
        private readonly Dictionary<string, bool> m_FolderFoldoutState = new Dictionary<string, bool>();

        /// <summary>
        /// 实例自动释放间隔最大值（秒）。
        /// </summary>
        private const float c_InstanceAutoReleaseIntervalMax = 3600f;

        /// <summary>
        /// 实例池容量上限。
        /// </summary>
        private const int c_InstanceCapacityMax = 128;

        /// <summary>
        /// 实例过期时间最大值（秒）。
        /// </summary>
        private const float c_InstanceExpireTimeMax = 3600f;

        /// <summary>
        /// 实例优先级最小值。
        /// </summary>
        private const int c_InstancePriorityMin = -100;

        /// <summary>
        /// 实例优先级最大值。
        /// </summary>
        private const int c_InstancePriorityMax = 100;

        /// <summary>
        /// UIView 信息表格 SerialID 列宽度（像素）。
        /// </summary>
        private const float c_UIColSerialID = 60f;

        /// <summary>
        /// UIView 信息表格 Depth 列宽度（像素）。
        /// </summary>
        private const float c_UIColDepth = 50f;

        /// <summary>
        /// UIView 信息表格 PauseCovered 列宽度（像素）。
        /// </summary>
        private const float c_UIColPauseCovered = 90f;

        /// <summary>
        /// 当前 UI 管理器类型名称属性。
        /// </summary>
        private SerializedProperty m_CurUIManagerTypeName;

        /// <summary>
        /// 当前视图分组辅助器类型名称属性。
        /// </summary>
        private SerializedProperty m_CurUIGroupHelperTypeName;

        /// <summary>
        /// UISettings.SourceDirPath 的缓存属性，避免每帧 FindPropertyRelative。
        /// </summary>
        private SerializedProperty m_SourceDirPath;

        /// <summary>
        /// UISettings.UIUnitsSettings 的缓存属性，避免每帧 FindPropertyRelative。
        /// </summary>
        private SerializedProperty m_UIUnitsSettings;

        /// <summary>
        /// 屏幕设计分辨率属性。
        /// </summary>
        private SerializedProperty m_ScreenDesignedResolution;

        /// <summary>
        /// 屏幕宽高适配比例阈值属性。
        /// </summary>
        private SerializedProperty m_ScreenWidthHeightMatchValue;

        /// <summary>
        /// 每帧最多销毁 UI 数量属性。
        /// </summary>
        private SerializedProperty m_DestroyMaxNumPerFrame;

        /// <summary>
        /// 实例自动释放间隔属性。
        /// </summary>
        private SerializedProperty m_InstanceAutoReleaseInterval;

        /// <summary>
        /// 实例池容量属性。
        /// </summary>
        private SerializedProperty m_InstanceCapacity;

        /// <summary>
        /// 实例过期时间属性。
        /// </summary>
        private SerializedProperty m_InstanceExpireTime;

        /// <summary>
        /// 实例优先级属性。
        /// </summary>
        private SerializedProperty m_InstancePriority;

        /// <summary>
        /// 实例挂载根节点属性。
        /// </summary>
        private SerializedProperty m_InstanceRoot;

        /// <summary>
        /// 视图分组深度换算系数属性。
        /// </summary>
        private SerializedProperty m_GroupDepthFactor;

        /// <summary>
        /// 视图内部深度换算系数属性。
        /// </summary>
        private SerializedProperty m_ViewDepthFactor;

        /// <summary>
        /// Inspector 配置的视图分组列表属性。
        /// </summary>
        private SerializedProperty m_UIGroups;

        /// <summary>
        /// UI 管理器所有可用类型名称列表。
        /// </summary>
        private List<string> m_UIManagerTypeNames;

        /// <summary>
        /// 视图分组辅助器所有可用类型名称列表。
        /// </summary>
        private List<string> m_UIGroupHelperTypeNames;

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

    }
}
