/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   音效组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class SoundComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 目录树折叠状态：键为文件夹完整路径，值为该层 Foldout 是否展开。
        /// </summary>
        private readonly Dictionary<string, bool> m_FolderFoldoutState = new Dictionary<string, bool>();

        /// <summary>
        /// 当前管理器类型名称。
        /// </summary>
        private SerializedProperty m_CurManagerTypeName;

        /// <summary>
        /// 声音设置。
        /// </summary>
        private SerializedProperty m_Settings;

        /// <summary>
        /// 数据源目录路径属性缓存。
        /// </summary>
        private SerializedProperty m_SourceDirPath;

        /// <summary>
        /// SoundUnitsSettings 列表属性。
        /// </summary>
        private SerializedProperty m_SoundUnitsSettings;

        /// <summary>
        /// AudioMixer 引用。
        /// </summary>
        private SerializedProperty m_AudioMixer;

        /// <summary>
        /// 声音分组壳数组。
        /// </summary>
        private SerializedProperty m_SoundGroupShells;

        /// <summary>
        /// 管理器所有类型名称。
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
        /// 运行时信息区域：声音组列表的折叠状态，默认收起。
        /// </summary>
        private bool m_RuntimeSoundGroupsFoldout;

    }
}
