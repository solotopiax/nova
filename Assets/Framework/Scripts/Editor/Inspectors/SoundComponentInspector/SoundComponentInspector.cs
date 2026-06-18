/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundComponentInspector.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   音效组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 音效组件编辑器面板定制。
    /// </summary>
    [CustomEditor(typeof(SoundComponent))]
    internal sealed partial class SoundComponentInspector : BaseComponentInspector
    {
        /// <inheritdoc />
        protected override string TemplateFileName => "SoundTemplate.xlsx";

        /// <inheritdoc />
        protected override float TemplateLabelWidth => EditorUtil.Draw.SourceFileTree.c_DirLabelWidth;

        /// <summary>
        /// 启用时绑定 SerializedProperty、收集管理器类型名称列表并注册 Luban 文件监控。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            m_CurManagerTypeName = serializedObject.FindProperty("m_CurManagerTypeName");
            m_AudioMixer = serializedObject.FindProperty("m_AudioMixer");
            m_SoundGroupShells = serializedObject.FindProperty("m_SoundGroupShells");

            m_ManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(ISoundManager)));

            m_Settings = serializedObject.FindProperty("m_Settings");
            m_SourceDirPath = m_Settings?.FindPropertyRelative("SourceDirPath");
            m_SoundUnitsSettings = m_Settings?.FindPropertyRelative("SoundUnitsSettings");

            InitializeTemplatePath(m_Settings?.FindPropertyRelative("TemplatePath"));

            string sourcePath = m_SourceDirPath?.stringValue;
            if (!string.IsNullOrEmpty(sourcePath))
            {
                m_IsLubanConfigExists = EditorUtil.Luban.ConfigSyncer.IsConfigDirExists(sourcePath);
                if (m_IsLubanConfigExists)
                {
                    m_WatchedConfigDirPath = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(sourcePath);
                    m_LubanFileWatcherCallback = OnLubanConfigChanged;
                    EditorUtil.FileWatcher.Watch(m_WatchedConfigDirPath, m_LubanFileWatcherCallback);
                }
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
        /// 绘制 Inspector：依次绘制音效设置、运行时声音组，并执行最终刷新。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawSoundSettings();
            DrawRuntimeSoundGroups();
            FinalRefreshInspectorGUI();
        }

    }
}
