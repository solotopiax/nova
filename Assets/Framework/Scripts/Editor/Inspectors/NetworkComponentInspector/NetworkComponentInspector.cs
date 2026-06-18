/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponentInspector.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Network 组件编辑器面板定制。
    /// </summary>
    [CustomEditor(typeof(NetworkComponent))]
    internal sealed partial class NetworkComponentInspector : BaseComponentInspector
    {
        /// <inheritdoc />
        protected override string TemplateFileName => "NetworkTemplate.xlsx";

        /// <summary>
        /// 启用时绑定 SerializedProperty、收集管理器类型名称列表并注册 Luban 文件监控。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            // 管理器类型名
            m_CurNetworkManagerTypeName = serializedObject.FindProperty("m_CurNetworkManagerTypeName");
            m_CurHttpManagerTypeName = serializedObject.FindProperty("m_CurHttpManagerTypeName");
            m_CurDoHManagerTypeName = serializedObject.FindProperty("m_CurDoHManagerTypeName");
            m_CurWebSocketManagerTypeName = serializedObject.FindProperty("m_CurWebSocketManagerTypeName");

            // 业务上下文
            m_Settings = serializedObject.FindProperty("m_Settings");

            // HostKey 属性绑定
            SerializedProperty hostKeySetting = m_Settings?.FindPropertyRelative("HostKeySettings");
            m_HostKeySourceDirPath = hostKeySetting?.FindPropertyRelative("SourceDirPath");
            m_HostKeyUnitsSettings = hostKeySetting?.FindPropertyRelative("HostKeyUnits");

            // NetCmd 属性绑定
            SerializedProperty netCmdSetting = m_Settings?.FindPropertyRelative("NetCmdSettings");
            m_NetCmdSourceDirPath = netCmdSetting?.FindPropertyRelative("SourceDirPath");
            m_NetCmdUnitsSettings = netCmdSetting?.FindPropertyRelative("NetCmdUnits");

            // HostKey FileWatcher
            string hostKeyBasePath = m_HostKeySourceDirPath?.stringValue;
            if (!string.IsNullOrEmpty(hostKeyBasePath))
            {
                m_IsHostKeyLubanConfigExists = EditorUtil.Luban.ConfigSyncer.IsConfigDirExists(hostKeyBasePath);
                if (m_IsHostKeyLubanConfigExists)
                {
                    m_WatchedHostKeyConfigDirPath = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(hostKeyBasePath);
                    m_HostKeyLubanFileWatcherCallback = OnHostKeyLubanConfigChanged;
                    EditorUtil.FileWatcher.Watch(m_WatchedHostKeyConfigDirPath, m_HostKeyLubanFileWatcherCallback);
                }
            }

            // NetCmd FileWatcher
            string netCmdBasePath = m_NetCmdSourceDirPath?.stringValue;
            if (!string.IsNullOrEmpty(netCmdBasePath))
            {
                m_IsNetCmdLubanConfigExists = EditorUtil.Luban.ConfigSyncer.IsConfigDirExists(netCmdBasePath);
                if (m_IsNetCmdLubanConfigExists)
                {
                    m_WatchedNetCmdConfigDirPath = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(netCmdBasePath);
                    m_NetCmdLubanFileWatcherCallback = OnNetCmdLubanConfigChanged;
                    EditorUtil.FileWatcher.Watch(m_WatchedNetCmdConfigDirPath, m_NetCmdLubanFileWatcherCallback);
                }
            }

            // 管理器配置对象
            m_DoHSettings = serializedObject.FindProperty("m_DoHSettings");
            m_HttpSettings = serializedObject.FindProperty("m_HttpSettings");
            m_WebSocketSettings = serializedObject.FindProperty("m_WebSocketSettings");

            // 实现类列表 — 运行时管理器
            m_NetworkManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(INetworkManager)));
            m_HttpManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IHttpManager)));
            m_DoHManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IDoHManager)));
            m_WebSocketManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IWebSocketManager)));

            // Proto 设置
            m_ProtoSettings = serializedObject.FindProperty("m_ProtoSettings");
            if (m_ProtoSettings != null)
            {
                m_ProtoSourceDirPath = m_ProtoSettings.FindPropertyRelative("ProtoSourceDirPath");
                m_ProtoUnitsSettings = m_ProtoSettings.FindPropertyRelative("ProtoUnits");
            }
        }

        /// <summary>
        /// 禁用时取消所有 FileWatcher 监控，防止回调指向已销毁的 Inspector 实例。
        /// </summary>
        private void OnDisable()
        {
            // HostKey
            if (m_HostKeyLubanFileWatcherCallback != null && !string.IsNullOrEmpty(m_WatchedHostKeyConfigDirPath))
            {
                EditorUtil.FileWatcher.Unwatch(m_WatchedHostKeyConfigDirPath, m_HostKeyLubanFileWatcherCallback);
                m_HostKeyLubanFileWatcherCallback = null;
                m_WatchedHostKeyConfigDirPath = null;
            }

            // NetCmd
            if (m_NetCmdLubanFileWatcherCallback != null && !string.IsNullOrEmpty(m_WatchedNetCmdConfigDirPath))
            {
                EditorUtil.FileWatcher.Unwatch(m_WatchedNetCmdConfigDirPath, m_NetCmdLubanFileWatcherCallback);
                m_NetCmdLubanFileWatcherCallback = null;
                m_WatchedNetCmdConfigDirPath = null;
            }
        }

        /// <summary>
        /// 绘制 Inspector：依次绘制管理器选择、域名表导出、指令表导出、Proto 管理、HTTP/DoH/WebSocket 设置，并执行最终刷新。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawManagerSelectors();
            DrawHostKeyExport();
            DrawNetCmdExport();
            DrawProtoManagement();
            DrawHttpSettings();
            DrawDoHSettings();
            DrawWebSocketSettings();
            FinalRefreshInspectorGUI();
        }
    }
}
