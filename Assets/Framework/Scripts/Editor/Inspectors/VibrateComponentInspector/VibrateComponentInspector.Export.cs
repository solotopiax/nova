/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateComponentInspector.Export.cs
 * author:    taoye
 * created:   2026/4/25
 * descrip:   振动组件编辑器面板定制 —— Luban 导出流水线方法
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class VibrateComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 对单个数据源文件执行数据导出。
        /// </summary>
        /// <param name="filePath">文件完整路径。</param>
        /// <param name="dataExportPath">数据导出目标路径。</param>
        /// <param name="detailProp">当前文件的单元设置属性。</param>
        /// <param name="sourceDirPathProp">当前区域的数据源目录路径属性。</param>
        /// <param name="isEmphasis">是否为 Emphasis 区域。</param>
        private void OnExportDataForFile(string filePath, string dataExportPath, SerializedProperty detailProp, SerializedProperty sourceDirPathProp, bool isEmphasis)
        {
            VibrateSettings settings = GetVibrateSettings();
            if (settings == null)
            {
                return;
            }

            if (isEmphasis)
            {
                EditorUtil.Vibrate.ExportEmphasisData(filePath, dataExportPath, settings);
            }
            else
            {
                EditorUtil.Vibrate.ExportCustomData(filePath, dataExportPath, settings);
            }
        }

        /// <summary>
        /// 对单个数据源文件执行类型导出。
        /// </summary>
        /// <param name="filePath">文件完整路径。</param>
        /// <param name="classExportPath">类型导出目标路径。</param>
        /// <param name="detailProp">当前文件的单元设置属性。</param>
        /// <param name="sourceDirPathProp">当前区域的数据源目录路径属性。</param>
        /// <param name="isEmphasis">是否为 Emphasis 区域。</param>
        private void OnExportClassForFile(string filePath, string classExportPath, SerializedProperty detailProp, SerializedProperty sourceDirPathProp, bool isEmphasis)
        {
            VibrateSettings settings = GetVibrateSettings();
            if (settings == null)
            {
                return;
            }

            if (isEmphasis)
            {
                EditorUtil.Vibrate.ExportEmphasisCode(filePath, classExportPath, settings);
            }
            else
            {
                EditorUtil.Vibrate.ExportCustomCode(filePath, classExportPath, settings);
            }
        }

        /// <summary>
        /// 导出所有已配置的数据源文件：清理旧导出、通过 Pipeline 同步配置并一次性调用 Luban CLI。
        /// </summary>
        /// <param name="directoryPath">数据源目录完整路径。</param>
        /// <param name="sourceUnitsSettingsProperty">单元设置列表属性。</param>
        /// <param name="isEmphasis">是否为 Emphasis 区域。</param>
        private void DoExportAllDataAndTypes(string directoryPath, SerializedProperty sourceUnitsSettingsProperty, bool isEmphasis)
        {
            if (sourceUnitsSettingsProperty == null)
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();

            VibrateSettings settings = GetVibrateSettings();
            if (settings == null)
            {
                return;
            }

            if (isEmphasis)
            {
                EditorUtil.Vibrate.ExportEmphasisAll(settings);
            }
            else
            {
                EditorUtil.Vibrate.ExportCustomAll(settings);
            }
        }

        /// <summary>
        /// 获取当前的 VibrateSettings 实例。
        /// </summary>
        /// <returns>VibrateSettings 实例，无法获取时返回 null。</returns>
        private VibrateSettings GetVibrateSettings()
        {
            VibrateComponent vibrateComponent = (VibrateComponent)target;
            var field = typeof(VibrateComponent).GetField("m_Settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(vibrateComponent) as VibrateSettings;
        }
    }
}
