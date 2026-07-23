/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundComponentInspector.Export.cs
 * author:    taoye
 * created:   2026/4/24
 * descrip:   音效组件编辑器面板定制 —— Luban 导出流水线方法
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class SoundComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 对单个数据源文件执行数据导出。
        /// </summary>
        private void OnExportDataForFile(string filePath, string dataExportPath, SerializedProperty detailProp)
        {
            if (string.IsNullOrEmpty(dataExportPath))
            {
                return;
            }

            string sourceDirPath = m_SourceDirPath.stringValue;
            SoundSettings settings = GetSoundSettings();
            if (settings == null)
            {
                return;
            }

            string relativePath = Util.SysIO.Path.GetRelativePath(sourceDirPath.TrimEnd('/', '\\'), filePath);
            SoundUnitSetting unitSetting = settings.SoundUnitsSettings?.Find(u => u.SourcePath == relativePath);
            if (unitSetting == null)
            {
                Log.Error(LogTag.Editor, "未找到文件 {0} 对应的 SoundUnitSetting。", relativePath);
                return;
            }

            EditorUtil.Sound.Exporter.ExportData(sourceDirPath, settings, unitSetting);
        }

        /// <summary>
        /// 对单个数据源文件执行类型导出。
        /// </summary>
        private void OnExportClassForFile(string filePath, string classExportPath, SerializedProperty detailProp)
        {
            if (string.IsNullOrEmpty(classExportPath))
            {
                return;
            }

            string sourceDirPath = m_SourceDirPath.stringValue;
            SoundSettings settings = GetSoundSettings();
            if (settings == null)
            {
                return;
            }

            string relativePath = Util.SysIO.Path.GetRelativePath(sourceDirPath.TrimEnd('/', '\\'), filePath);
            SoundUnitSetting unitSetting = settings.SoundUnitsSettings?.Find(u => u.SourcePath == relativePath);
            EditorUtil.Sound.Exporter.ExportCode(sourceDirPath, settings, unitSetting, classExportPath, null);
        }

        /// <summary>
        /// 导出所有已配置的数据源文件：清理旧导出、通过 Pipeline 同步配置并一次性调用 Luban CLI。
        /// </summary>
        /// <param name="directoryPath">数据源目录完整路径。</param>
        /// <param name="sourceUnitsSettingsProperty">SoundSettings.SoundUnitsSettings 列表属性。</param>
        private void DoExportAllDataAndTypes(string directoryPath, SerializedProperty sourceUnitsSettingsProperty)
        {
            if (sourceUnitsSettingsProperty == null)
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();

            string sourceDirPath = m_SourceDirPath.stringValue;
            SoundSettings settings = GetSoundSettings();
            if (settings == null)
            {
                return;
            }

            EditorUtil.Sound.Exporter.ExportAll(sourceDirPath, settings);
        }

        /// <summary>
        /// 获取当前 SoundSettings 实例。
        /// </summary>
        /// <returns>SoundSettings 实例，无法获取时返回 null。</returns>
        private SoundSettings GetSoundSettings()
        {
            SoundComponent soundComponent = (SoundComponent)target;
            var field = typeof(SoundComponent).GetField("m_Settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(soundComponent) as SoundSettings;
        }
    }
}
