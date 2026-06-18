/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableComponentInspector.Export.cs
 * author:    taoye
 * created:   2026/4/24
 * descrip:   表格组件编辑器面板定制 —— Luban 导出流水线方法
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class TableComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// Luban target 名称。
        /// </summary>
        private const string c_ExportTargetName = "table";

        /// <summary>
        /// Luban manager 类名。
        /// </summary>
        private const string c_ExportManagerName = "TableTables";

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
            TableSettings settings = GetTableSettings();
            if (settings == null)
            {
                return;
            }

            string relativePath = Util.SysIO.Path.GetRelativePath(sourceDirPath.TrimEnd('/', '\\'), filePath);
            TableUnitSetting unitSetting = settings.TableUnitsSettings.Find(u => u.SourcePath == relativePath);
            if (unitSetting == null)
            {
                Log.Error(LogTag.Editor, "未找到文件 {0} 对应的 TableUnitSetting。", relativePath);
                return;
            }

            var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, c_ExportTargetName, c_ExportManagerName);
            ctx.TargetUnit = unitSetting;
            EditorUtil.Luban.Pipeline.ExportData(ctx);
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
            TableSettings settings = GetTableSettings();
            if (settings == null)
            {
                return;
            }

            string relativePath = Util.SysIO.Path.GetRelativePath(sourceDirPath.TrimEnd('/', '\\'), filePath);
            TableUnitSetting unitSetting = settings.TableUnitsSettings?.Find(u => u.SourcePath == relativePath);

            var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, c_ExportTargetName, c_ExportManagerName);
            ctx.OutputCodeDir = classExportPath;
            ctx.RelevantFileNames = EditorUtil.Luban.ExportHelper.BuildRelevantFileNames(filePath, sourceDirPath, ((IDataTableSettings)settings).Units, ctx.ManagerName);
            ctx.TargetUnit = unitSetting;
            EditorUtil.Luban.Pipeline.ExportCode(ctx);
        }

        /// <summary>
        /// 导出所有已配置了数据路径与类型路径的数据源文件：清理旧导出文件、通过 Pipeline 同步配置并一次性调用 Luban CLI 生成代码和数据、合并 JSON。
        /// </summary>
        /// <param name="directoryPath">表格根目录的完整路径。</param>
        /// <param name="sourceUnitsSettingsProperty">TableSettings.TableUnitsSettings 列表属性。</param>
        private void DoExportAllDataAndTypes(string directoryPath, SerializedProperty sourceUnitsSettingsProperty)
        {
            if (sourceUnitsSettingsProperty == null)
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();

            string sourceDirPath = m_SourceDirPath.stringValue;
            TableSettings settings = GetTableSettings();
            if (settings == null)
            {
                return;
            }

            EditorUtil.Table.Exporter.ExportAll(settings, sourceDirPath);
        }

        /// <summary>
        /// 刷新单个文件的 DataTypeNames（供 DoRefreshAllDataTypeNames 循环内调用，不额外 ApplyModifiedProperties）。
        /// </summary>
        private void DoRefreshSingleDataTypeNames(string fullPath, SerializedProperty dataTypeNamesProp)
        {
            EditorUtil.Luban.DataTypeNameHelper.DoRefreshSingleDataTypeNames(fullPath, dataTypeNamesProp, minHeaderRowCount: 4);
        }

        /// <summary>
        /// 获取当前 TableComponent 的 TableSettings 实例。
        /// </summary>
        /// <returns>TableSettings 实例，无法获取时返回 null。</returns>
        private TableSettings GetTableSettings()
        {
            TableComponent tableComponent = (TableComponent)target;
            var field = typeof(TableComponent).GetField("m_Setting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(tableComponent) as TableSettings;
        }
    }
}
