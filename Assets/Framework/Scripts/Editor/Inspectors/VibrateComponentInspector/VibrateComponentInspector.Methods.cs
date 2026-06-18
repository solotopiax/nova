/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class VibrateComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制配置区域：Vibrate 管理器类型选择器 + HelpBox 说明 + 分隔线。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector("Vibrate 管理器", m_ManagerTypeNames, m_CurManagerTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 IVibrateManager 接口后，该类型将自动出现在此列表中。" });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制 Emphasis 振动表格导出区域。
        /// </summary>
        private void DrawEmphasisVibrateExport()
        {
            DrawVibrateSourceDataOperations("Emphasis 振动表格导出", "VibrateEmphasisExport", c_EmphasisTemplateFileName, m_EmphasisSourceDirPath, m_EmphasisUnitsSettings, m_EmphasisFolderFoldoutState);
        }

        /// <summary>
        /// 绘制 Custom 振动表格导出区域。
        /// </summary>
        private void DrawCustomVibrateExport()
        {
            DrawVibrateSourceDataOperations("Custom 振动表格导出", "VibrateCustomExport", c_CustomTemplateFileName, m_CustomSourceDirPath, m_CustomUnitsSettings, m_CustomFolderFoldoutState);
        }

        /// <summary>
        /// 绘制单个振动区域的完整导出区域：Foldout、模板路径提示、表格目录位置、数据源文件树、全局导出按钮。
        /// </summary>
        /// <param name="sectionTitle">区域折叠标题。</param>
        /// <param name="foldoutKey">折叠状态持久化 Key。</param>
        /// <param name="templateFileName">模板文件名。</param>
        /// <param name="sourceDirPathProp">数据源目录路径属性。</param>
        /// <param name="unitsSettingsProp">单元设置列表属性。</param>
        /// <param name="foldoutState">文件树折叠状态字典。</param>
        private void DrawVibrateSourceDataOperations(string sectionTitle, string foldoutKey, string templateFileName, SerializedProperty sourceDirPathProp, SerializedProperty unitsSettingsProp, System.Collections.Generic.Dictionary<string, bool> foldoutState)
        {
            if (sourceDirPathProp == null || unitsSettingsProp == null)
            {
                return;
            }

            if (!EditorUtil.Draw.Foldout(sectionTitle, foldoutKey, true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();

            string sourceDirPath = sourceDirPathProp.stringValue;
            DrawTemplatePathHintReadOnly(templateFileName, "模板文件位置：", sourceDirPath, 105);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("表格目录位置：", false, GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_DirLabelWidth));
                EditorUtil.Draw.TextField(sourceDirPathProp, true);
                EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderDelay("选择表格目录位置", "", "", sourceDirPathProp, onComplete: () =>
                {
                    EditorUtil.FileSystem.RefreshDelayed();
                }), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(sourceDirPathProp.stringValue), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthLarge));
            });

            if (!string.IsNullOrEmpty(sourceDirPath) && Util.SysIO.Directory.Exists(sourceDirPath))
            {
                if (!EditorUtil.Luban.ConfigSyncer.IsConfigDirExists(sourceDirPath))
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "Luban 配置目录 (_configs/) 尚未初始化，首次导出时将自动创建。" });
                }

                bool isEmphasis = foldoutKey == "VibrateEmphasisExport";
                EditorUtil.Draw.SourceFileTree.DrawSourceFilesListWithFolders(sourceDirPath, unitsSettingsProp, foldoutState, customDrawSourceFileRow: (filePath, capturedRelativePath, seq, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty) => DrawVibrateSourceFileRow(filePath, capturedRelativePath, seq, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty, sourceDirPathProp, isEmphasis));
                EditorUtil.Draw.Button("导出所有数据和类型", true, () =>
                {
                    EditorUtil.Luban.DataTypeNameHelper.DoRefreshAllDataTypeNames(sourceDirPath, unitsSettingsProp, serializedObject);
                    DoExportAllDataAndTypes(sourceDirPath, unitsSettingsProp, isEmphasis);
                });
            }

            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 自定义振动数据源文件行绘制：文件名行、数据导出行、类型导出行、Asset 地址行。
        /// </summary>
        /// <param name="filePath">文件完整路径。</param>
        /// <param name="capturedRelativePath">文件相对于根目录的路径。</param>
        /// <param name="seq">序号。</param>
        /// <param name="indentSpace">缩进像素。</param>
        /// <param name="savedIndent">保存的缩进级别。</param>
        /// <param name="detailProp">当前文件的单元设置属性。</param>
        /// <param name="sourceUnitsSettingsProperty">全部单元设置列表属性。</param>
        /// <param name="sourceDirPathProp">当前区域的数据源目录路径属性。</param>
        /// <param name="isEmphasis">是否为 Emphasis 区域。</param>
        private void DrawVibrateSourceFileRow(string filePath, string capturedRelativePath, int seq, float indentSpace, int savedIndent, SerializedProperty detailProp, SerializedProperty sourceUnitsSettingsProperty, SerializedProperty sourceDirPathProp, bool isEmphasis)
        {
            EditorUtil.Draw.SourceFileTree.DrawDefaultFileNameRow(filePath, seq, indentSpace, savedIndent);
            EditorUtil.Draw.SourceFileTree.DrawDataExportRow(filePath, capturedRelativePath, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty, (fp, dataExportPath, dp) => OnExportDataForFile(fp, dataExportPath, dp, sourceDirPathProp, isEmphasis), DoRefreshDataTypeNames);
            EditorUtil.Draw.SourceFileTree.DrawClassExportRow(filePath, capturedRelativePath, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty, (fp, classExportPath, dp) => OnExportClassForFile(fp, classExportPath, dp, sourceDirPathProp, isEmphasis), DoRefreshDataTypeNames);
            EditorUtil.Draw.SourceFileTree.DrawAssetLocationRow(detailProp, indentSpace, savedIndent);
        }

        /// <summary>
        /// 刷新单个文件的 DataTypeNames（委托适配）。
        /// </summary>
        private void DoRefreshDataTypeNames(string filePath, SerializedProperty dataTypeNamesProp)
        {
            EditorUtil.Luban.DataTypeNameHelper.DoRefreshDataTypeNames(filePath, dataTypeNamesProp, serializedObject);
        }
    }
}
