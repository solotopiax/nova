/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   表格组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using System;
using System.Collections;
using System.Reflection;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class TableComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制配置区域：Table 管理器类型选择器与命名空间列表。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector("Table 管理器", m_ManagerTypeNames, m_CurManagerTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 ITableManager 接口后，该类型将自动出现在此列表中。" });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制表格导出区域：表格目录位置（选择/打开文件夹）、若目录有效则绘制可折叠的数据源文件树及每文件的导出数据/类型路径与操作按钮、以及「导出所有数据和类型」按钮。
        /// </summary>
        private void DrawTableExport()
        {
            if (m_SourceDirPath == null || m_TableUnitsSettings == null)
            {
                return;
            }

            if (!EditorUtil.Draw.Foldout("表格导出", "TableExport", true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();

            DrawTemplatePathHintReadOnly(m_SourceDirPath, TemplateFileName, "List 模板位置：");
            DrawTemplatePathHintReadOnly(m_SourceDirPath, c_TemplateMapFileName, "Map 模板位置：");
            DrawTemplatePathHintReadOnly(m_SourceDirPath, c_TemplateOneFileName, "One 模板位置：");

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("表格目录位置：", false, GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_DirLabelWidth));
                EditorUtil.Draw.TextField(m_SourceDirPath, true);
                EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderDelay("选择表格目录位置", "", "", m_SourceDirPath), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(m_SourceDirPath.stringValue), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthLarge));
            });

            string directoryPath = m_SourceDirPath.stringValue;
            if (!string.IsNullOrEmpty(directoryPath) && Util.SysIO.Directory.Exists(directoryPath))
            {
                if (!m_IsLubanConfigExists)
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "Luban 配置目录 (_configs/) 尚未初始化，首次导出时将自动创建。" });
                }

                EditorUtil.Draw.SourceFileTree.DrawSourceFilesListWithFolders(directoryPath, m_TableUnitsSettings, m_FolderFoldoutState, minHeaderRowCount: 4, customDrawSourceFileRow: DrawTableSourceFileRow);
                EditorUtil.Draw.Button("导出所有数据和类型", true, () =>
                {
                    EditorUtil.Luban.DataTypeNameHelper.DoRefreshAllDataTypeNames(directoryPath, m_TableUnitsSettings, serializedObject, minHeaderRowCount: 4, doRefreshSingle: DoRefreshSingleDataTypeNames);
                    DoExportAllDataAndTypes(directoryPath, m_TableUnitsSettings);
                });
            }

            EditorUtil.Draw.DecreaseIndentLevel();

            EditorUtil.Draw.Line();
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
        /// 仅在运行态下绘制运行时信息：以可折叠列表显示已加载数据表数量及各表条目数，并绘制分隔线。
        /// 计数仅统计实际有数据填充的表格。
        /// </summary>
        private void DrawRuntimeInfos()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            TableComponent t = (TableComponent)target;
            int loadedWithDataCount = 0;

            if (m_TableUnitsSettings != null)
            {
                for (int i = 0; i < m_TableUnitsSettings.arraySize; i++)
                {
                    SerializedProperty elem = m_TableUnitsSettings.GetArrayElementAtIndex(i);
                    SerializedProperty dataTypeNamesProp = elem.FindPropertyRelative("DataTypeNames");
                    if (dataTypeNamesProp == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < dataTypeNamesProp.arraySize; j++)
                    {
                        string sheetName = dataTypeNamesProp.GetArrayElementAtIndex(j).stringValue;
                        if (string.IsNullOrEmpty(sheetName))
                        {
                            continue;
                        }

                        Type type = ResolveTableType("Tb" + sheetName);
                        if (type == null || !t.HasTable(type))
                        {
                            continue;
                        }

                        if (GetTableDataCount(t.GetTable(type)) > 0)
                        {
                            loadedWithDataCount++;
                        }
                    }
                }
            }

            string loadStatus = t.IsLoadOver ? "已全部加载" : "未全部加载";
            string foldoutLabel = $"已加载数据表 ({loadedWithDataCount}) [{loadStatus}]";
            m_RuntimeTablesFoldout = EditorUtil.Draw.Foldout(ref m_RuntimeTablesFoldout, foldoutLabel, false);

            if (m_RuntimeTablesFoldout && m_TableUnitsSettings != null)
            {
                EditorUtil.Draw.IncreaseIndentLevel();

                for (int i = 0; i < m_TableUnitsSettings.arraySize; i++)
                {
                    SerializedProperty elem = m_TableUnitsSettings.GetArrayElementAtIndex(i);
                    SerializedProperty dataTypeNamesProp = elem.FindPropertyRelative("DataTypeNames");
                    if (dataTypeNamesProp == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < dataTypeNamesProp.arraySize; j++)
                    {
                        string sheetName = dataTypeNamesProp.GetArrayElementAtIndex(j).stringValue;
                        if (string.IsNullOrEmpty(sheetName))
                        {
                            continue;
                        }

                        string tableName = "Tb" + sheetName;
                        Type type = ResolveTableType(tableName);
                        if (type == null)
                        {
                            continue;
                        }

                        if (!t.HasTable(type))
                        {
                            continue;
                        }

                        int dataCount = GetTableDataCount(t.GetTable(type));
                        if (dataCount == 0)
                        {
                            continue;
                        }

                        EditorUtil.Draw.Label(tableName, "Loaded", false);
                    }
                }

                EditorUtil.Draw.DecreaseIndentLevel();
            }

            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 从 IConfigManager 现取命名空间并解析表格类型。
        /// 仅在 Play Mode 下调用（由 DrawRuntimeInfos 守门），此时 ConfigManager 已就绪。
        /// </summary>
        /// <param name="tableName">表格类型名称（如 TbXxx）。</param>
        /// <returns>解析到的类型，未找到时返回 null。</returns>
        private Type ResolveTableType(string tableName)
        {
            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null)
            {
                return null;
            }

            string ns = configManager.Namespace;
            if (string.IsNullOrEmpty(ns))
            {
                return null;
            }

            return Util.Assembly.GetType(string.Concat(ns, ".", tableName));
        }

        /// <summary>
        /// 通过反射获取 ITable 实例的数据行数量（读取 DataList.Count）。
        /// </summary>
        /// <param name="tableObj">ITable 实例对象。</param>
        /// <returns>数据行数量，无法读取时返回 0。</returns>
        private static int GetTableDataCount(object tableObj)
        {
            if (tableObj == null)
            {
                return 0;
            }

            try
            {
                PropertyInfo dataListProp = tableObj.GetType().GetProperty("DataList", BindingFlags.Public | BindingFlags.Instance);
                if (dataListProp != null)
                {
                    object dataList = dataListProp.GetValue(tableObj);
                    if (dataList is ICollection collection)
                    {
                        return collection.Count;
                    }
                }
            }
            catch
            {
                // 反射失败时静默返回 0
            }

            return 0;
        }

        /// <summary>
        /// 自定义表格数据源文件行绘制：文件名行（含 DataTableMode 选择）、数据导出行、类型导出行、Asset 地址行。
        /// </summary>
        private void DrawTableSourceFileRow(string filePath, string capturedRelativePath, int seq, float indentSpace, int savedIndent, SerializedProperty detailProp, SerializedProperty sourceUnitsSettingsProperty)
        {
            DrawTableFileNameRow(filePath, seq, indentSpace, savedIndent, detailProp);
            EditorUtil.Draw.SourceFileTree.DrawDataExportRow(filePath, capturedRelativePath, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty, OnExportDataForFile, DoRefreshDataTypeNames);
            EditorUtil.Draw.SourceFileTree.DrawClassExportRow(filePath, capturedRelativePath, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty, OnExportClassForFile, DoRefreshDataTypeNames);
            EditorUtil.Draw.SourceFileTree.DrawAssetLocationRow(detailProp, indentSpace, savedIndent);
        }

        /// <summary>
        /// 绘制表格文件名行：文件名 + Map 模式索引字段 + DataTableMode 枚举下拉 + 打开按钮。
        /// </summary>
        private void DrawTableFileNameRow(string filePath, int seq, float indentSpace, int savedIndent, SerializedProperty detailProp)
        {
            string fileName = Util.SysIO.Path.GetFileName(filePath);

            SerializedProperty exportModeProp = detailProp?.FindPropertyRelative("TableMode");
            EditorUtil.Draw.SetIndentLevel(0);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(indentSpace);
                EditorUtil.Draw.Label($"[{seq}] {fileName}", EditorUtil.Draw.SourceFileTree.SourceFileNameStyle, false, GUILayout.ExpandWidth(true));
                if (exportModeProp != null && (DataTableMode)exportModeProp.enumValueIndex == DataTableMode.Map)
                {
                    SerializedProperty indexFieldProp = detailProp?.FindPropertyRelative("IndexField");
                    if (indexFieldProp != null)
                    {
                        EditorUtil.Draw.Label("索引：", false, GUILayout.Width(40));
                        EditorUtil.Draw.TextField(indexFieldProp, true, null, GUILayout.Width(50));
                    }
                }
                EditorUtil.Draw.EnumPopup<DataTableMode>(exportModeProp, EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall, false);
                EditorUtil.Draw.Button("打开", false, () => EditorUtil.FileSystem.OpenFile(filePath), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(filePath), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthMedium));
            });
            EditorUtil.Draw.RestoreIndentLevel(savedIndent);
        }

        /// <summary>
        /// 刷新单个文件的 DataTypeNames（委托适配，使用 minHeaderRowCount: 4）。
        /// </summary>
        private void DoRefreshDataTypeNames(string filePath, SerializedProperty dataTypeNamesProp)
        {
            EditorUtil.Luban.DataTypeNameHelper.DoRefreshDataTypeNames(filePath, dataTypeNamesProp, serializedObject, minHeaderRowCount: 4);
        }

    }
}
