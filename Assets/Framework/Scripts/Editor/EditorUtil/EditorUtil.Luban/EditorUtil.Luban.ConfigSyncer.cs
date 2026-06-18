/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.ConfigSyncer.cs
 * author:    taoye
 * created:   2026/4/16
 * descrip:   Luban 配置同步器（通用版）
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
            /// Luban 配置同步器，管理 _configs/ 目录，实现 Inspector 与文件双向同步。
            /// <para>统一支持 Table/Config 模块：通过 IDataTableSettings/IDataTableUnitSetting 接口实现参数化。</para>
            /// </summary>
            public static class ConfigSyncer
            {
                /// <summary>
                /// UTF-8 无 BOM 编码（避免 Luban CLI 解析 JSON/XML 时因 BOM 出错）。
                /// </summary>
                private static readonly System.Text.Encoding s_Utf8NoBom = new System.Text.UTF8Encoding(false);

                /// <summary>
                /// _configs 子目录名称。
                /// </summary>
                private const string c_ConfigsDirName = "_configs";

                /// <summary>
                /// Luban 主配置文件名。
                /// </summary>
                internal const string c_LubanConfFileName = "luban.conf";

                /// <summary>
                /// 表定义文件名。
                /// </summary>
                internal const string c_TablesXmlFileName = "__tables__.xml";

                /// <summary>
                /// 获取 _configs/ 目录完整路径。
                /// </summary>
                /// <param name="sourceDirPath">数据源目录路径。</param>
                /// <returns>_configs/ 目录完整路径。</returns>
                public static string GetConfigDirPath(string sourceDirPath)
                {
                    return Util.SysIO.Path.Combine(sourceDirPath, c_ConfigsDirName);
                }

                /// <summary>
                /// 获取 Luban 主配置文件（luban.conf）的完整路径。
                /// 等价于 GetConfigDirPath(sourceDirPath) + c_LubanConfFileName，封装为 public 供外部调用方使用。
                /// </summary>
                /// <param name="sourceDirPath">数据源目录路径。</param>
                /// <returns>luban.conf 的完整路径。</returns>
                public static string GetConfPath(string sourceDirPath)
                {
                    return Util.SysIO.Path.Combine(GetConfigDirPath(sourceDirPath), c_LubanConfFileName);
                }

                /// <summary>
                /// 检查 _configs/ 目录是否存在。
                /// </summary>
                /// <param name="sourceDirPath">数据源目录路径。</param>
                /// <returns>是否存在。</returns>
                public static bool IsConfigDirExists(string sourceDirPath)
                {
                    return Util.SysIO.Directory.Exists(GetConfigDirPath(sourceDirPath));
                }

                /// <summary>
                /// 初始化 _configs/ 目录及默认配置文件。
                /// </summary>
                /// <param name="sourceDirPath">数据源目录路径。</param>
                /// <param name="targetName">Luban target 名称（如 "table" / "config"）。</param>
                /// <param name="managerName">Luban manager 类名（如 "TableTables" / "ConfigTables"）。</param>
                /// <param name="topModule">顶层命名空间（如 "Game.Runtime"）。</param>
                public static void InitializeConfigDir(string sourceDirPath, string targetName, string managerName, string topModule)
                {
                    string configDir = GetConfigDirPath(sourceDirPath);
                    if (!Util.SysIO.Directory.Exists(configDir))
                    {
                        Util.SysIO.Directory.CreateIfNotExist(configDir);
                    }

                    string confPath = Util.SysIO.Path.Combine(configDir, c_LubanConfFileName);
                    if (!Util.SysIO.File.Exists(confPath))
                    {
                        WriteDefaultLubanConf(confPath, targetName, managerName, topModule);
                    }

                    string xmlPath = Util.SysIO.Path.Combine(configDir, c_TablesXmlFileName);
                    if (!Util.SysIO.File.Exists(xmlPath))
                    {
                        WriteEmptyTablesXml(xmlPath);
                    }
                }

                /// <summary>
                /// 从 Inspector 数据同步到 _configs/ 文件。
                /// </summary>
                /// <param name="sourceDirPath">数据源目录路径。</param>
                /// <param name="settings">数据表设置（通过 IDataTableSettings 统一消费）。</param>
                /// <param name="targetName">Luban target 名称。</param>
                /// <param name="managerName">Luban manager 类名。</param>
                public static void SyncFromInspector(string sourceDirPath, IDataTableSettings settings, string targetName, string managerName, IReadOnlyList<IDataTableUnitSetting> regionUnits = null)
                {
                    string topModule = EditorUtil.Config.RuntimeProvider.GetNamespace();

                    string configDir = GetConfigDirPath(sourceDirPath);
                    if (!Util.SysIO.Directory.Exists(configDir))
                    {
                        InitializeConfigDir(sourceDirPath, targetName, managerName, topModule);
                    }

                    string confPath = Util.SysIO.Path.Combine(configDir, c_LubanConfFileName);
                    UpdateLubanConfTopModule(confPath, targetName, managerName, topModule);

                    string xmlPath = Util.SysIO.Path.Combine(configDir, c_TablesXmlFileName);
                    GenerateTablesXml(xmlPath, sourceDirPath, regionUnits ?? settings.Units);
                }

                /// <summary>
                /// 清理指定临时目录。
                /// </summary>
                /// <param name="tempDirPath">临时目录完整路径。</param>
                public static void CleanTempDir(string tempDirPath)
                {
                    if (Util.SysIO.Directory.Exists(tempDirPath))
                    {
                        try
                        {
                            Util.SysIO.Directory.Delete(tempDirPath, true);
                        }
                        catch (Exception e)
                        {
                            Log.Warning(LogTag.Editor, "清理临时目录失败：{0}", e.Message);
                        }
                    }
                }

                /// <summary>
                /// 写入默认 luban.conf 文件。
                /// <para>dataDir 配置为 ".."（即 _configs 的父目录 = sourceDirPath），</para>
                /// <para>使得 __tables__.xml 中的路径相对于 sourceDirPath 解析。</para>
                /// </summary>
                /// <param name="path">luban.conf 文件路径。</param>
                /// <param name="targetName">Luban target 名称。</param>
                /// <param name="managerName">Luban manager 类名。</param>
                /// <param name="topModule">顶层命名空间。</param>
                private static void WriteDefaultLubanConf(string path, string targetName, string managerName, string topModule)
                {
                    var conf = new JObject
                    {
                        ["dataDir"] = "..",
                        ["groups"] = new JArray(new JObject { ["names"] = new JArray("c"), ["default"] = true }),
                        ["schemaFiles"] = new JArray(new JObject { ["fileName"] = c_TablesXmlFileName, ["type"] = "" }),
                        ["targets"] = new JArray(new JObject
                        {
                            ["name"] = targetName,
                            ["manager"] = managerName,
                            ["groups"] = new JArray("c"),
                            ["topModule"] = topModule,
                        })
                    };
                    Util.SysIO.File.WriteAllTextSync(path, conf.ToString(Newtonsoft.Json.Formatting.Indented), s_Utf8NoBom);
                }

                /// <summary>
                /// 更新 luban.conf 中的 topModule、targetName、managerName。
                /// </summary>
                /// <param name="confPath">luban.conf 文件路径。</param>
                /// <param name="targetName">Luban target 名称。</param>
                /// <param name="managerName">Luban manager 类名。</param>
                /// <param name="topModule">顶层命名空间。</param>
                private static void UpdateLubanConfTopModule(string confPath, string targetName, string managerName, string topModule)
                {
                    if (!Util.SysIO.File.Exists(confPath))
                    {
                        WriteDefaultLubanConf(confPath, targetName, managerName, topModule);
                        return;
                    }

                    try
                    {
                        JObject conf = JObject.Parse(Util.SysIO.File.ReadAllTextSync(confPath, s_Utf8NoBom));
                        JArray targets = conf["targets"] as JArray;
                        if (targets != null && targets.Count > 0)
                        {
                            JObject target = targets[0] as JObject;
                            if (target != null)
                            {
                                target["name"] = targetName;
                                target["manager"] = managerName;
                                target["topModule"] = topModule;
                            }
                        }
                        Util.SysIO.File.WriteAllTextSync(confPath, conf.ToString(Newtonsoft.Json.Formatting.Indented), s_Utf8NoBom);
                    }
                    catch (Exception e)
                    {
                        Log.Error(LogTag.Editor, "更新 luban.conf 失败：{0}", e.Message);
                    }
                }

                /// <summary>
                /// 写入空的 __tables__.xml 文件。
                /// </summary>
                /// <param name="path">__tables__.xml 文件路径。</param>
                private static void WriteEmptyTablesXml(string path)
                {
                    XmlWriterSettings xmlSettings = new XmlWriterSettings { Indent = true, Encoding = s_Utf8NoBom };
                    using XmlWriter writer = XmlWriter.Create(path, xmlSettings);
                    writer.WriteStartDocument();
                    writer.WriteStartElement("module");
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }

                /// <summary>
                /// 从 IDataTableUnitSetting 列表生成 __tables__.xml。
                /// <para>对每个 Excel 文件扫描 DataTypeNames（过滤 # 开头），为每个条目生成一个 table 元素。</para>
                /// <para>通过 IDataTableUnitSetting.Mode/IndexField/LubanInputPath 实现 Table/Config 差异化。</para>
                /// <para>跳过数据源文件不存在的条目，避免残留配置污染 Luban 输入。</para>
                /// <para>重写时保留已有的 enum 等非 table 元素。</para>
                /// </summary>
                /// <param name="xmlPath">__tables__.xml 文件路径。</param>
                /// <param name="sourceDirPath">数据源目录路径。</param>
                /// <param name="unitSettings">数据表单元设置列表。</param>
                private static void GenerateTablesXml(string xmlPath, string sourceDirPath, IReadOnlyList<IDataTableUnitSetting> unitSettings)
                {
                    string rootDir = sourceDirPath.TrimEnd('/', '\\');

                    List<XmlElement> preservedElements = CollectNonTableElements(xmlPath);

                    XmlWriterSettings xmlSettings = new XmlWriterSettings { Indent = true, Encoding = s_Utf8NoBom };
                    using XmlWriter writer = XmlWriter.Create(xmlPath, xmlSettings);
                    writer.WriteStartDocument();
                    writer.WriteStartElement("module");

                    foreach (XmlElement element in preservedElements)
                    {
                        element.WriteTo(writer);
                    }

                    if (unitSettings != null)
                    {
                        foreach (IDataTableUnitSetting unit in unitSettings)
                        {
                            if (string.IsNullOrEmpty(unit.SourcePath) || unit.DataTypeNames == null || unit.DataTypeNames.Count == 0)
                            {
                                continue;
                            }

                            string fullPath = Util.SysIO.Path.Combine(rootDir, unit.SourcePath);
                            if (!Util.SysIO.File.Exists(fullPath))
                            {
                                continue;
                            }

                            string inputPath = unit.LubanInputPath;
                            string mode = unit.Mode.ToString().ToLower();

                            foreach (string typeName in unit.DataTypeNames)
                            {
                                if (string.IsNullOrEmpty(typeName) || typeName.StartsWith("#"))
                                {
                                    continue;
                                }

                                string sheetName = typeName.Contains(".") ? typeName.Substring(typeName.LastIndexOf('.') + 1) : typeName;

                                // Excel 源文件：input = {sheetName}@{filePath}
                                // CSV 临时目录：input = {dir}/{sheetName}.csv
                                string ext = Util.SysIO.Path.GetExtension(inputPath);
                                string input = (ext == ".xlsx" || ext == ".xls")
                                    ? sheetName + "@" + inputPath
                                    : inputPath + "/" + sheetName + ".csv";

                                writer.WriteStartElement("table");
                                writer.WriteAttributeString("name", "Tb" + sheetName);
                                writer.WriteAttributeString("value", sheetName);
                                writer.WriteAttributeString("input", input);
                                writer.WriteAttributeString("mode", mode);
                                if (unit.Mode == DataTableMode.Map && !string.IsNullOrEmpty(unit.IndexField))
                                {
                                    writer.WriteAttributeString("index", unit.IndexField);
                                }
                                writer.WriteAttributeString("readSchemaFromFile", "true");
                                writer.WriteAttributeString("comment", sheetName);
                                writer.WriteEndElement();
                            }
                        }
                    }

                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }

                /// <summary>
                /// 从现有 __tables__.xml 中收集非 table 元素（如 enum、bean 等手写定义）。
                /// </summary>
                /// <param name="xmlPath">__tables__.xml 文件路径。</param>
                /// <returns>需要保留的非 table 元素列表，文件不存在或无非 table 元素时返回空列表。</returns>
                private static List<XmlElement> CollectNonTableElements(string xmlPath)
                {
                    List<XmlElement> result = new List<XmlElement>();
                    if (!Util.SysIO.File.Exists(xmlPath))
                    {
                        return result;
                    }

                    try
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.Load(xmlPath);
                        XmlElement root = doc.DocumentElement;
                        if (root == null)
                        {
                            return result;
                        }

                        foreach (XmlNode node in root.ChildNodes)
                        {
                            if (node is XmlElement element && element.Name != "table")
                            {
                                result.Add(element);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warning(LogTag.Editor, "读取 __tables__.xml 保留元素失败：{0}", e.Message);
                    }

                    return result;
                }
            }
        }
    }
}
