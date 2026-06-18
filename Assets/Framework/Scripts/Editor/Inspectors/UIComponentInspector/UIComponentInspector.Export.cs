/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIComponentInspector.Export.cs
 * author:    taoye
 * created:   2026/4/24
 * descrip:   UI 组件编辑器面板定制 —— Luban 导出流水线方法
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class UIComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 对单个数据源文件执行数据导出：委托给 EditorUtil.UI.Exporter.ExportDataForFile。
        /// </summary>
        private void OnExportDataForFile(string filePath, string dataExportPath, SerializedProperty detailProp)
        {
            if (string.IsNullOrEmpty(dataExportPath) || m_SourceDirPath == null)
            {
                return;
            }

            string sourceDirPath = m_SourceDirPath.stringValue;
            UISettings settings = GetUISettings();
            if (settings == null)
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtil.UI.Exporter.ExportDataForFile(settings, sourceDirPath, filePath);
        }

        /// <summary>
        /// 对单个数据源文件执行类型导出：委托给 EditorUtil.UI.Exporter.ExportCodeForFile。
        /// </summary>
        private void OnExportClassForFile(string filePath, string classExportPath, SerializedProperty detailProp)
        {
            if (string.IsNullOrEmpty(classExportPath) || m_SourceDirPath == null)
            {
                return;
            }

            string sourceDirPath = m_SourceDirPath.stringValue;
            UISettings settings = GetUISettings();
            if (settings == null)
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtil.UI.Exporter.ExportCodeForFile(settings, sourceDirPath, filePath, classExportPath);
        }

        /// <summary>
        /// 导出所有已配置数据路径与类型路径的数据源文件：委托给 EditorUtil.UI.Exporter.ExportAll。
        /// </summary>
        /// <param name="directoryPath">UI 表格根目录的完整路径。</param>
        /// <param name="sourceUnitsSettingsProperty">UISettings.UIUnitsSettings 列表属性。</param>
        private void DoExportAllDataAndTypes(string directoryPath, SerializedProperty sourceUnitsSettingsProperty)
        {
            if (sourceUnitsSettingsProperty == null || m_SourceDirPath == null)
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();

            string sourceDirPath = m_SourceDirPath.stringValue;
            UISettings settings = GetUISettings();
            if (settings == null)
            {
                return;
            }

            EditorUtil.UI.Exporter.ExportAll(settings, sourceDirPath);
        }

        /// <summary>
        /// 获取当前 UIComponent 的 UISettings 实例。
        /// </summary>
        /// <returns>UISettings 实例，无法获取时返回 null。</returns>
        private UISettings GetUISettings()
        {
            UIComponent uiComponent = (UIComponent)target;
            var field = typeof(UIComponent).GetField("m_UISettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(uiComponent) as UISettings;
        }
    }
}
