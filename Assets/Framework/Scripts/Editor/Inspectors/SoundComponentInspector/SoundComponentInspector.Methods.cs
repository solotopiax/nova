/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   音效组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class SoundComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制音效配置信息：Sound 管理器类型选择器、命名空间列表、导出区域、AudioMixer、声音分组壳。
        /// </summary>
        private void DrawSoundSettings()
        {
            EditorUtil.Draw.TypesSelector("Sound 管理器", m_ManagerTypeNames, m_CurManagerTypeName, true, null, GUILayout.Width(175));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 ISoundManager 接口后，该类型将自动出现在此列表中。" });
            EditorUtil.Draw.Line();

            DrawSourceDataOperations();

            EditorUtil.Draw.Property("声音混音器：", m_AudioMixer, true, GUILayout.Width(175));

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("声音分组壳：", true, GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_DirLabelWidth));
                EditorUtil.Draw.Button("+", true, () =>
                {
                    m_SoundGroupShells.InsertArrayElementAtIndex(m_SoundGroupShells.arraySize);
                    m_SoundGroupShells.serializedObject.ApplyModifiedProperties();
                }, GUILayout.Width(24));
            });

            for (int i = 0; i < m_SoundGroupShells.arraySize; i++)
            {
                int index = i;
                SerializedProperty shell = m_SoundGroupShells.GetArrayElementAtIndex(index);
                EditorUtil.Draw.Layout.Vertical("box", () =>
                {
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Property("名称：", shell.FindPropertyRelative("m_Name"), true, GUILayout.ExpandWidth(true));
                        EditorUtil.Draw.Button("-", true, () =>
                        {
                            m_SoundGroupShells.DeleteArrayElementAtIndex(index);
                            m_SoundGroupShells.serializedObject.ApplyModifiedProperties();
                        }, GUILayout.Width(24));
                    });
                    EditorUtil.Draw.Property("避免同优先级替换：", shell.FindPropertyRelative("m_AvoidBeingReplacedBySamePriority"), true);
                    EditorUtil.Draw.Property("静音：", shell.FindPropertyRelative("m_Mute"), true);
                    EditorUtil.Draw.Property("音量：", shell.FindPropertyRelative("m_Volume"), true);
                    EditorUtil.Draw.Property("代理数量：", shell.FindPropertyRelative("m_AgentCount"), true);
                });
            }

            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制声音表格导出区域：模板路径提示、表格目录位置、数据源文件树及每文件的导出设置、全局导出按钮。
        /// </summary>
        private void DrawSourceDataOperations()
        {
            if (m_SourceDirPath == null || m_SoundUnitsSettings == null)
            {
                return;
            }

            if (!EditorUtil.Draw.Foldout("声音表格导出", "SoundExport", true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();

            string sourceDirPath = m_SourceDirPath.stringValue;
            DrawTemplatePathHintReadOnly(TemplateFileName, "模板文件位置：", sourceDirPath);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("表格目录位置：", false, GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_DirLabelWidth));
                EditorUtil.Draw.TextField(m_SourceDirPath, true);
                EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderDelay("选择表格目录位置", "", "", m_SourceDirPath, onComplete: () =>
                {
                    EditorUtil.FileSystem.RefreshDelayed();
                }), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(m_SourceDirPath.stringValue), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthLarge));
            });

            if (!string.IsNullOrEmpty(sourceDirPath) && Util.SysIO.Directory.Exists(sourceDirPath))
            {
                if (!m_IsLubanConfigExists)
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "Luban 配置目录 (_configs/) 尚未初始化，首次导出时将自动创建。" });
                }

                EditorUtil.Draw.SourceFileTree.DrawSourceFilesListWithFolders(sourceDirPath, m_SoundUnitsSettings, m_FolderFoldoutState, customDrawSourceFileRow: DrawSoundSourceFileRow);
                EditorUtil.Draw.Button("导出所有数据和类型", true, () =>
                {
                    EditorUtil.Luban.DataTypeNameHelper.DoRefreshAllDataTypeNames(sourceDirPath, m_SoundUnitsSettings, serializedObject);
                    DoExportAllDataAndTypes(sourceDirPath, m_SoundUnitsSettings);
                });
            }

            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 自定义声音数据源文件行绘制：文件名行、数据导出行、类型导出行、Asset 地址行。
        /// </summary>
        private void DrawSoundSourceFileRow(string filePath, string capturedRelativePath, int seq, float indentSpace, int savedIndent, SerializedProperty detailProp, SerializedProperty sourceUnitsSettingsProperty)
        {
            EditorUtil.Draw.SourceFileTree.DrawDefaultFileNameRow(filePath, seq, indentSpace, savedIndent);
            EditorUtil.Draw.SourceFileTree.DrawDataExportRow(filePath, capturedRelativePath, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty, OnExportDataForFile, DoRefreshDataTypeNames);
            EditorUtil.Draw.SourceFileTree.DrawClassExportRow(filePath, capturedRelativePath, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty, OnExportClassForFile, DoRefreshDataTypeNames);
            EditorUtil.Draw.SourceFileTree.DrawAssetLocationRow(detailProp, indentSpace, savedIndent);
        }

        /// <summary>
        /// 刷新单个文件的 DataTypeNames（委托适配）。
        /// </summary>
        private void DoRefreshDataTypeNames(string filePath, SerializedProperty dataTypeNamesProp)
        {
            EditorUtil.Luban.DataTypeNameHelper.DoRefreshDataTypeNames(filePath, dataTypeNamesProp, serializedObject);
        }

        /// <summary>
        /// Luban _configs/ 文件变更回调（由 FileWatcher 在主线程触发）。
        /// </summary>
        private void OnLubanConfigChanged()
        {
            m_IsLubanConfigExists = true;
            Repaint();
        }

        /// <summary>
        /// 仅在运行态下绘制运行时信息：以可折叠列表显示声音组数量，展开后绘制各声音组详情。
        /// </summary>
        private void DrawRuntimeSoundGroups()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            SoundComponent t = (SoundComponent)target;
            string foldoutLabel = $"声音组 ({t.SoundGroupCount})";
            m_RuntimeSoundGroupsFoldout = EditorUtil.Draw.Foldout(ref m_RuntimeSoundGroupsFoldout, foldoutLabel, false);

            if (m_RuntimeSoundGroupsFoldout)
            {
                EditorUtil.Draw.Label("Enable：", t.enabled.ToString(), false);
                EditorUtil.Draw.Label("SoundGroupCount：", t.SoundGroupCount.ToString(), false);
            }

            EditorUtil.Draw.Line();
        }

    }
}
