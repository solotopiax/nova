/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIComponentInspector.cs
 * author:    taoye
 * created:   2026/02/28
 * descrip:   UI 组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// UIComponent 的 CustomEditor，负责配置、UI 表格导出、设置与运行时信息的绘制。
    /// </summary>
    [CustomEditor(typeof(UIComponent))]
    internal sealed partial class UIComponentInspector : BaseComponentInspector
    {
        /// <inheritdoc />
        protected override string TemplateFileName => "UITemplate.xlsx";

        /// <inheritdoc />
        protected override float TemplateLabelWidth => EditorUtil.Draw.SourceFileTree.c_DirLabelWidth;

        /// <summary>
        /// 启用时绑定 SerializedProperty、收集管理器类型名称列表并注册 Luban 文件监控。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            m_CurUIManagerTypeName = serializedObject.FindProperty("m_CurUIManagerTypeName");
            m_CurUIGroupHelperTypeName = serializedObject.FindProperty("m_CurUIGroupHelperTypeName");
            m_ScreenDesignedResolution = serializedObject.FindProperty("m_ScreenDesignedResolution");
            m_ScreenWidthHeightMatchValue = serializedObject.FindProperty("m_ScreenWidthHeightMatchValue");
            m_DestroyMaxNumPerFrame = serializedObject.FindProperty("m_DestroyMaxNumPerFrame");
            m_InstanceAutoReleaseInterval = serializedObject.FindProperty("m_InstanceAutoReleaseInterval");
            m_InstanceCapacity = serializedObject.FindProperty("m_InstanceCapacity");
            m_InstanceExpireTime = serializedObject.FindProperty("m_InstanceExpireTime");
            m_InstancePriority = serializedObject.FindProperty("m_InstancePriority");
            m_InstanceRoot = serializedObject.FindProperty("m_InstanceRoot");
            m_GroupDepthFactor = serializedObject.FindProperty("m_GroupDepthFactor");
            m_ViewDepthFactor = serializedObject.FindProperty("m_ViewDepthFactor");
            m_UIGroups = serializedObject.FindProperty("m_UIGroups");

            SerializedProperty uiSettings = serializedObject.FindProperty("m_UISettings");
            m_SourceDirPath = uiSettings?.FindPropertyRelative("SourceDirPath");
            m_UIUnitsSettings = uiSettings?.FindPropertyRelative("UIUnitsSettings");
            InitializeTemplatePath(uiSettings?.FindPropertyRelative("TemplatePath"));

            m_UIManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IUIManager)));
            m_UIGroupHelperTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IUIGroupHelper)));

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
        /// 绘制 Inspector：依次绘制配置、运行时信息，并执行最终刷新。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            FinalRefreshInspectorGUI();
        }

    }
}
