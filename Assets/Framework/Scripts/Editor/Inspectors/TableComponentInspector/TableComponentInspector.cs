/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableComponentInspector.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   表格组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// TableComponent 的 CustomEditor，负责配置、表格导出、设置与运行时信息的绘制。
    /// </summary>
    [CustomEditor(typeof(TableComponent))]
    internal sealed partial class TableComponentInspector : BaseComponentInspector
    {
        /// <inheritdoc />
        protected override string TemplateFileName => "TableListTemplate.xlsx";

        /// <summary>
        /// 映射格式模板文件名。
        /// </summary>
        private const string c_TemplateMapFileName = "TableMapTemplate.xlsx";

        /// <summary>
        /// 单例格式模板文件名。
        /// </summary>
        private const string c_TemplateOneFileName = "TableOneTemplate.xlsx";

        /// <inheritdoc />
        protected override float TemplateLabelWidth => EditorUtil.Draw.SourceFileTree.c_DirLabelWidth;

        /// <summary>
        /// 启用时绑定 SerializedProperty、收集管理器类型名称列表并注册 Luban 文件监控。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            m_CurManagerTypeName = serializedObject.FindProperty("m_CurManagerTypeName");
            m_Setting = serializedObject.FindProperty("m_Setting");
            m_SourceDirPath = m_Setting?.FindPropertyRelative("SourceDirPath");
            m_TableUnitsSettings = m_Setting?.FindPropertyRelative("TableUnitsSettings");

            m_ManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(ITableManager)));

            string sourceDirPath = m_SourceDirPath?.stringValue;
            m_IsLubanConfigExists = !string.IsNullOrEmpty(sourceDirPath) && EditorUtil.Luban.ConfigSyncer.IsConfigDirExists(sourceDirPath);

            if (m_IsLubanConfigExists)
            {
                m_WatchedConfigDirPath = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(sourceDirPath);
                m_LubanFileWatcherCallback = OnLubanConfigChanged;
                EditorUtil.FileWatcher.Watch(m_WatchedConfigDirPath, m_LubanFileWatcherCallback);
            }
        }

        /// <summary>
        /// 禁用时取消 Luban 文件监控。
        /// </summary>
        private void OnDisable()
        {
            if (m_LubanFileWatcherCallback != null && !string.IsNullOrEmpty(m_WatchedConfigDirPath))
            {
                EditorUtil.FileWatcher.Unwatch(m_WatchedConfigDirPath, m_LubanFileWatcherCallback);
                m_LubanFileWatcherCallback = null;
                m_WatchedConfigDirPath = null;
            }
        }

        /// <summary>
        /// 绘制 Inspector：依次绘制配置、表格导出、运行时信息，并执行最终刷新。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            DrawTableExport();
            DrawRuntimeInfos();
            FinalRefreshInspectorGUI();
        }

    }
}
