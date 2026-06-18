/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.DataTypeNameHelper.cs
 * author:    taoye
 * created:   2026/4/25
 * descrip:   数据类型名称刷新工具：读取数据源文件提取有效 Sheet 名称并填充 DataTypeNames
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Luban
        {
            /// <summary>
            /// 数据类型名称刷新工具：读取数据源文件提取有效 Sheet 名称并填充 DataTypeNames。
            /// </summary>
            public static class DataTypeNameHelper
            {
                /// <summary>
                /// 读取单个数据源文件，提取有效 Sheet 名称并填充对应的 DataTypeNames 列表。
                /// 默认实现使用 EditorUtil.Excel.ReadAllSheets（多文件合并模式）。
                /// </summary>
                /// <param name="filePath">数据源文件的完整路径。</param>
                /// <param name="dataTypeNamesProp">目标 DataTypeNames 的 SerializedProperty。</param>
                /// <param name="serializedObject">序列化对象，用于 ApplyModifiedProperties。</param>
                /// <param name="minHeaderRowCount">有效 Sheet 的最小表头行数，默认 5。</param>
                public static void DoRefreshDataTypeNames(string filePath, SerializedProperty dataTypeNamesProp, SerializedObject serializedObject, int minHeaderRowCount = 5)
                {
                    try
                    {
                        Dictionary<string, List<IReadOnlyList<string>>> sheets = EditorUtil.Excel.ReadAllSheets(filePath);
                        if (sheets == null)
                        {
                            return;
                        }

                        PopulateDataTypeNames(sheets, dataTypeNamesProp, minHeaderRowCount);
                        serializedObject.ApplyModifiedProperties();
                    }
                    catch (Exception e)
                    {
                        Log.Error(LogTag.Editor, "刷新类型名失败 ({0}): {1}", filePath, e.Message);
                    }
                }

                /// <summary>
                /// 遍历所有已配置的数据源文件，依次读取并刷新每个条目的 DataTypeNames。
                /// </summary>
                /// <param name="directoryPath">数据源目录完整路径。</param>
                /// <param name="sourceUnitsSettingsProperty">UnitSettings 列表属性。</param>
                /// <param name="serializedObject">序列化对象，用于 ApplyModifiedProperties。</param>
                /// <param name="minHeaderRowCount">有效 Sheet 的最小表头行数，默认 5。</param>
                /// <param name="doRefreshSingle">单文件刷新委托（fullPath, dataTypeNamesProp）；为 null 时使用默认实现。</param>
                public static void DoRefreshAllDataTypeNames(string directoryPath, SerializedProperty sourceUnitsSettingsProperty, SerializedObject serializedObject, int minHeaderRowCount = 5, Action<string, SerializedProperty> doRefreshSingle = null)
                {
                    if (sourceUnitsSettingsProperty == null)
                    {
                        return;
                    }

                    string rootDir = directoryPath.Replace('\\', '/').TrimEnd('/');

                    for (int i = 0; i < sourceUnitsSettingsProperty.arraySize; i++)
                    {
                        SerializedProperty elem = sourceUnitsSettingsProperty.GetArrayElementAtIndex(i);
                        SerializedProperty sourcePathProp = elem.FindPropertyRelative("SourcePath");
                        SerializedProperty dataTypeNamesProp = elem.FindPropertyRelative("DataTypeNames");

                        if (sourcePathProp == null || string.IsNullOrEmpty(sourcePathProp.stringValue) || dataTypeNamesProp == null)
                        {
                            continue;
                        }

                        string fullPath = Util.SysIO.Path.Combine(rootDir, sourcePathProp.stringValue);
                        if (!Util.SysIO.File.Exists(fullPath))
                        {
                            continue;
                        }

                        if (doRefreshSingle != null)
                        {
                            doRefreshSingle(fullPath, dataTypeNamesProp);
                        }
                        else
                        {
                            DoRefreshSingleDataTypeNames(fullPath, dataTypeNamesProp, minHeaderRowCount);
                        }
                    }

                    serializedObject.ApplyModifiedProperties();
                }

                /// <summary>
                /// 刷新单个文件的 DataTypeNames（供 DoRefreshAllDataTypeNames 循环内调用，不额外 ApplyModifiedProperties）。
                /// 默认实现使用 EditorUtil.Excel.ReadAllSheets。
                /// </summary>
                /// <param name="fullPath">文件完整路径。</param>
                /// <param name="dataTypeNamesProp">DataTypeNames 属性。</param>
                /// <param name="minHeaderRowCount">有效 Sheet 的最小表头行数，默认 5。</param>
                public static void DoRefreshSingleDataTypeNames(string fullPath, SerializedProperty dataTypeNamesProp, int minHeaderRowCount = 5)
                {
                    try
                    {
                        Dictionary<string, List<IReadOnlyList<string>>> sheets = EditorUtil.Excel.ReadAllSheets(fullPath);
                        if (sheets == null)
                        {
                            return;
                        }

                        PopulateDataTypeNames(sheets, dataTypeNamesProp, minHeaderRowCount);
                    }
                    catch (Exception e)
                    {
                        Log.Error(LogTag.Editor, "刷新类型名失败 ({0}): {1}", fullPath, e.Message);
                    }
                }

                /// <summary>
                /// 根据 Sheet 字典填充 DataTypeNames 属性：筛选非 # 开头且行数 >= minHeaderRowCount 的有效 Sheet。
                /// </summary>
                /// <param name="sheets">从数据源读取的 Sheet 字典（Value 为 List 类型）。</param>
                /// <param name="dataTypeNamesProp">目标 DataTypeNames 的 SerializedProperty。</param>
                /// <param name="minHeaderRowCount">有效 Sheet 的最小表头行数。</param>
                public static void PopulateDataTypeNames(Dictionary<string, List<IReadOnlyList<string>>> sheets, SerializedProperty dataTypeNamesProp, int minHeaderRowCount)
                {
                    var typeNames = new List<string>();
                    foreach (var kvp in sheets)
                    {
                        if (!kvp.Key.StartsWith("#") && kvp.Value != null && kvp.Value.Count >= minHeaderRowCount)
                        {
                            typeNames.Add(kvp.Key);
                        }
                    }

                    dataTypeNamesProp.ClearArray();
                    for (int i = 0; i < typeNames.Count; i++)
                    {
                        dataTypeNamesProp.InsertArrayElementAtIndex(i);
                        dataTypeNamesProp.GetArrayElementAtIndex(i).stringValue = typeNames[i];
                    }
                }

                /// <summary>
                /// 根据 Sheet 字典（IReadOnlyList 版本）填充 DataTypeNames 属性。
                /// Table 模块通过 EditorUtil.Excel 读取后的 RawContent 为此类型。
                /// </summary>
                /// <param name="sheets">从数据源读取的 Sheet 字典。</param>
                /// <param name="dataTypeNamesProp">目标 DataTypeNames 的 SerializedProperty。</param>
                /// <param name="minHeaderRowCount">有效 Sheet 的最小表头行数。</param>
                public static void PopulateDataTypeNames(Dictionary<string, IReadOnlyList<IReadOnlyList<string>>> sheets, SerializedProperty dataTypeNamesProp, int minHeaderRowCount)
                {
                    var typeNames = new List<string>();
                    foreach (var kvp in sheets)
                    {
                        if (!kvp.Key.StartsWith("#") && kvp.Value != null && kvp.Value.Count >= minHeaderRowCount)
                        {
                            typeNames.Add(kvp.Key);
                        }
                    }

                    dataTypeNamesProp.ClearArray();
                    for (int i = 0; i < typeNames.Count; i++)
                    {
                        dataTypeNamesProp.InsertArrayElementAtIndex(i);
                        dataTypeNamesProp.GetArrayElementAtIndex(i).stringValue = typeNames[i];
                    }
                }
            }
        }
    }
}
