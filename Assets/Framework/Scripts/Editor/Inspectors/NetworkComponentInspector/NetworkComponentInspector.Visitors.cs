/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class NetworkComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 标签宽度。
        /// </summary>
        private const float c_LabelWidth = 102f;

        /// <summary>
        /// 域名表模板文件名。
        /// </summary>
        private const string c_HostKeyTemplateFileName = "NetworkHostKeysTemplate.xlsx";

        /// <summary>
        /// 指令表模板文件名。
        /// </summary>
        private const string c_NetCmdTemplateFileName = "NetworkCmdsTemplate.xlsx";

        /// <summary>
        /// 网络设置属性（包含 HostKey 和 NetCmd 两套构建单元配置）。
        /// </summary>
        private SerializedProperty m_Settings;

        /// <summary>
        /// Network 管理器实现类名属性。
        /// </summary>
        private SerializedProperty m_CurNetworkManagerTypeName;

        /// <summary>
        /// Network 管理器所有实现类名列表。
        /// </summary>
        private List<string> m_NetworkManagerTypeNames;

        /// <summary>
        /// HTTP 管理器实现类名属性。
        /// </summary>
        private SerializedProperty m_CurHttpManagerTypeName;

        /// <summary>
        /// HTTP 管理器所有实现类名列表。
        /// </summary>
        private List<string> m_HttpManagerTypeNames;

        /// <summary>
        /// DoH 管理器实现类名属性。
        /// </summary>
        private SerializedProperty m_CurDoHManagerTypeName;

        /// <summary>
        /// DoH 管理器所有实现类名列表。
        /// </summary>
        private List<string> m_DoHManagerTypeNames;

        /// <summary>
        /// WebSocket 管理器实现类名属性。
        /// </summary>
        private SerializedProperty m_CurWebSocketManagerTypeName;

        /// <summary>
        /// WebSocket 管理器所有实现类名列表。
        /// </summary>
        private List<string> m_WebSocketManagerTypeNames;

        /// <summary>
        /// HostKey 数据源目录路径属性（HostKeySettings.SourceDirPath）。
        /// </summary>
        private SerializedProperty m_HostKeySourceDirPath;

        /// <summary>
        /// HostKeySettings.HostKeyUnits 列表属性。
        /// </summary>
        private SerializedProperty m_HostKeyUnitsSettings;

        /// <summary>
        /// HostKey 目录树折叠状态。
        /// </summary>
        private readonly Dictionary<string, bool> m_HostKeyFolderFoldoutState = new Dictionary<string, bool>();

        /// <summary>
        /// HostKey Luban _configs/ 目录是否存在。
        /// </summary>
        private bool m_IsHostKeyLubanConfigExists;

        /// <summary>
        /// HostKey Luban FileWatcher 变更回调。
        /// </summary>
        private System.Action m_HostKeyLubanFileWatcherCallback;

        /// <summary>
        /// HostKey 已注册 FileWatcher 的 _configs/ 目录路径。
        /// </summary>
        private string m_WatchedHostKeyConfigDirPath;

        /// <summary>
        /// NetCmd 数据源目录路径属性（NetCmdSettings.SourceDirPath）。
        /// </summary>
        private SerializedProperty m_NetCmdSourceDirPath;

        /// <summary>
        /// NetCmdSettings.NetCmdUnits 列表属性。
        /// </summary>
        private SerializedProperty m_NetCmdUnitsSettings;

        /// <summary>
        /// NetCmd 目录树折叠状态。
        /// </summary>
        private readonly Dictionary<string, bool> m_NetCmdFolderFoldoutState = new Dictionary<string, bool>();

        /// <summary>
        /// NetCmd Luban _configs/ 目录是否存在。
        /// </summary>
        private bool m_IsNetCmdLubanConfigExists;

        /// <summary>
        /// NetCmd Luban FileWatcher 变更回调。
        /// </summary>
        private System.Action m_NetCmdLubanFileWatcherCallback;

        /// <summary>
        /// NetCmd 已注册 FileWatcher 的 _configs/ 目录路径。
        /// </summary>
        private string m_WatchedNetCmdConfigDirPath;

        /// <summary>
        /// DoH 管理器配置对象属性。
        /// </summary>
        private SerializedProperty m_DoHSettings;

        /// <summary>
        /// HTTP 管理器配置对象属性。
        /// </summary>
        private SerializedProperty m_HttpSettings;

        /// <summary>
        /// WebSocket 管理器配置对象属性。
        /// </summary>
        private SerializedProperty m_WebSocketSettings;

        /// <summary>
        /// Protobuf 编辑器设置属性。
        /// </summary>
        private SerializedProperty m_ProtoSettings;

        /// <summary>
        /// Proto 文件根目录路径属性（ProtoSettings.ProtoSourceDirPath）。
        /// </summary>
        private SerializedProperty m_ProtoSourceDirPath;

        /// <summary>
        /// ProtoSettings.ProtoUnits 列表属性。
        /// </summary>
        private SerializedProperty m_ProtoUnitsSettings;

        /// <summary>
        /// Proto 文件树折叠状态字典。
        /// </summary>
        private readonly Dictionary<string, bool> m_ProtoFileFolderFoldoutState = new Dictionary<string, bool>();

        /// <summary>
        /// Proto 文件列表缓存（Layout 事件时刷新）。
        /// </summary>
        private string[] m_CachedProtoFiles;

        /// <summary>
        /// Proto 文件名标签样式。
        /// </summary>
        private static GUIStyle s_ProtoFileNameStyle;

    }
}
