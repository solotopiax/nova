/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.JsonMerger.cs
 * author:    taoye
 * created:   2026/4/16
 * descrip:   Luban JSON 合并器（通用版）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Xml;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Luban
        {
            /// <summary>
            /// Luban JSON 合并器，将 Luban 按表导出的 JSON 文件合并为按 Excel 文件的 JSON（Nova 格式）。
            /// <para>统一支持 Table/Config 模块：通过 IDataTableUnitSetting.LubanInputPath 匹配 __tables__.xml 中的 input 路径。</para>
            /// </summary>
            public static class JsonMerger
            {
                /// <summary>
                /// 合并指定 Excel 文件的 Luban 导出 JSON。
                /// </summary>
                /// <param name="lubanOutputDir">Luban 临时输出目录。</param>
                /// <param name="tablesXmlPath">__tables__.xml 文件路径。</param>
                /// <param name="unitSetting">目标 Excel 文件的单元设置。</param>
                /// <param name="topModule">Luban topModule（用于定位输出文件名前缀）。</param>
                /// <param name="deferredResults">若非 null，将结果（输出路径 → 表数量）存入此字典而不立即打印日志。</param>
                /// <returns>是否成功。</returns>
                public static bool MergeForUnit(string lubanOutputDir, string tablesXmlPath, IDataTableUnitSetting unitSetting, string topModule, Dictionary<string, int> deferredResults = null)
                {
                    try
                    {
                        Dictionary<string, string> tableToSheet = ParseTablesXmlForUnit(tablesXmlPath, unitSetting.LubanInputPath);
                        if (tableToSheet.Count == 0)
                        {
                            return true;
                        }

                        JObject mergedJson = BuildMergedJson(lubanOutputDir, tableToSheet, topModule);
                        string outputPath = unitSetting.DatasExportPath;
                        string outputDir = Util.SysIO.Path.GetDirectoryName(outputPath);
                        if (!string.IsNullOrEmpty(outputDir))
                        {
                            Util.SysIO.Directory.CreateIfNotExist(outputDir);
                        }
                        Util.SysIO.File.WriteAllTextSync(outputPath, mergedJson.ToString(Newtonsoft.Json.Formatting.Indented), System.Text.Encoding.UTF8);

                        if (deferredResults != null)
                        {
                            deferredResults[outputPath] = tableToSheet.Count;
                        }
                        else
                        {
                            Log.Debug(LogTag.Editor, "数据导出完成：{0}（{1} 个表）", outputPath, tableToSheet.Count);
                        }
                        return true;
                    }
                    catch (Exception e)
                    {
                        Log.Error(LogTag.Editor, "数据合并失败：{0}", e.Message);
                        return false;
                    }
                }

                /// <summary>
                /// 批量合并所有 Excel 文件的 Luban 导出 JSON。
                /// </summary>
                /// <param name="lubanOutputDir">Luban 临时输出目录。</param>
                /// <param name="tablesXmlPath">__tables__.xml 文件路径。</param>
                /// <param name="unitSettings">全部单元设置列表。</param>
                /// <param name="topModule">Luban topModule。</param>
                /// <param name="deferredResults">若非 null，将结果存入此字典而不立即打印日志。</param>
                /// <returns>是否全部成功。</returns>
                public static bool MergeAll(string lubanOutputDir, string tablesXmlPath, IReadOnlyList<IDataTableUnitSetting> unitSettings, string topModule, Dictionary<string, int> deferredResults = null)
                {
                    bool allSuccess = true;
                    foreach (IDataTableUnitSetting unit in unitSettings)
                    {
                        if (string.IsNullOrEmpty(unit.DatasExportPath) || string.IsNullOrEmpty(unit.SourcePath) || unit.DataTypeNames == null || unit.DataTypeNames.Count == 0)
                        {
                            continue;
                        }

                        if (!MergeForUnit(lubanOutputDir, tablesXmlPath, unit, topModule, deferredResults))
                        {
                            allSuccess = false;
                        }
                    }
                    return allSuccess;
                }

                /// <summary>
                /// 解析 __tables__.xml，提取指定单元关联的表名到原始 Sheet 名映射。
                /// <para>匹配逻辑：将 input 属性的 @ 后部分与 unitLubanInputPath 按文件名比较。</para>
                /// <para>Table 模块的 LubanInputPath 等于 SourcePath（完整相对路径），</para>
                /// <para>Config 模块的 LubanInputPath 带 _temp/ 前缀（如 "_temp/Configs_Global.xlsx"），</para>
                /// <para>统一用文件名匹配可同时兼容两种格式。</para>
                /// </summary>
                /// <param name="tablesXmlPath">__tables__.xml 路径。</param>
                /// <param name="unitLubanInputPath">单元的 Luban 输入路径（IDataTableUnitSetting.LubanInputPath）。</param>
                /// <returns>表名（如 "TbTestAAA"）到原始 Sheet 名（如 "TestAAA"）的映射。</returns>
                private static Dictionary<string, string> ParseTablesXmlForUnit(string tablesXmlPath, string unitLubanInputPath)
                {
                    Dictionary<string, string> result = new Dictionary<string, string>();
                    if (!Util.SysIO.File.Exists(tablesXmlPath))
                    {
                        return result;
                    }

                    XmlDocument doc = new XmlDocument();
                    doc.Load(tablesXmlPath);
                    XmlNodeList tables = doc.SelectNodes("//table");
                    if (tables == null)
                    {
                        return result;
                    }

                    string normalizedInputPath = NormalizePath(unitLubanInputPath);

                    foreach (XmlNode node in tables)
                    {
                        string input = node.Attributes?["input"]?.Value ?? "";
                        int atIndex = input.IndexOf('@');
                        string sheetName = atIndex >= 0 ? input.Substring(0, atIndex) : "";
                        string filePart = atIndex >= 0 ? input.Substring(atIndex + 1) : input;

                        if (NormalizePath(filePart) == normalizedInputPath)
                        {
                            string tableName = node.Attributes?["name"]?.Value ?? "";
                            if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(sheetName))
                            {
                                result[tableName] = sheetName;
                            }
                        }
                    }

                    return result;
                }

                /// <summary>
                /// 从 Luban 输出目录读取各表 JSON 并合并为单个 JObject。
                /// </summary>
                /// <param name="lubanOutputDir">Luban 输出目录。</param>
                /// <param name="tableToSheet">表名到 Sheet 名映射。</param>
                /// <param name="topModule">topModule（用于构建文件名前缀）。</param>
                /// <returns>合并后的 JObject，key 为 Sheet 原名（保持大小写），value 为 JArray。</returns>
                private static JObject BuildMergedJson(string lubanOutputDir, Dictionary<string, string> tableToSheet, string topModule)
                {
                    JObject merged = new JObject();

                    foreach (var kvp in tableToSheet)
                    {
                        string tableName = kvp.Key;
                        string sheetName = kvp.Value;
                        string fileName = tableName.ToLower() + ".json";
                        string filePath = Util.SysIO.Path.Combine(lubanOutputDir, fileName);

                        if (!Util.SysIO.File.Exists(filePath))
                        {
                            Log.Warning(LogTag.Editor, "未找到 Luban 导出文件：{0}", filePath);
                            continue;
                        }

                        string content = Util.SysIO.File.ReadAllTextSync(filePath, System.Text.Encoding.UTF8);
                        JToken token = JToken.Parse(content);
                        merged[sheetName] = token;
                    }

                    return merged;
                }

                /// <summary>
                /// 规范化路径（统一正斜杠、去除前后空格）。
                /// </summary>
                /// <param name="path">原始路径。</param>
                /// <returns>规范化后的路径。</returns>
                private static string NormalizePath(string path)
                {
                    return path?.Trim().Replace('\\', '/') ?? "";
                }
            }
        }
    }
}
