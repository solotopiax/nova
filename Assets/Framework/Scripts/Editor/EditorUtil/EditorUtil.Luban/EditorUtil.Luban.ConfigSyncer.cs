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
                internal static void InitializeConfigDir(string sourceDirPath, string targetName, string managerName, string topModule)
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

                internal static LubanSchemaManifest SyncFromInspector(
                    string sourceDirPath,
                    IDataTableSettings settings,
                    LubanExportProfile profile,
                    IReadOnlyList<IDataTableUnitSetting> regionUnits = null,
                    int? minHeaderRowCount = null,
                    Func<string, int, IReadOnlyList<string>> scanValueTypes = null)
                {
                    string topModule = EditorUtil.Config.RuntimeProvider.GetNamespace();

                    string configDir = GetConfigDirPath(sourceDirPath);
                    if (!Util.SysIO.Directory.Exists(configDir))
                    {
                        InitializeConfigDir(sourceDirPath, profile.TargetName, profile.ManagerName, topModule);
                    }

                    string confPath = Util.SysIO.Path.Combine(configDir, c_LubanConfFileName);
                    UpdateLubanConfTopModule(confPath, profile.TargetName, profile.ManagerName, topModule);

                    LubanSchemaManifest manifest = LubanSchemaManifestBuilder.Build(
                        sourceDirPath,
                        profile.Id,
                        regionUnits ?? settings.Units,
                        minHeaderRowCount ?? profile.MinHeaderRowCount,
                        scanValueTypes);
                    string xmlPath = Util.SysIO.Path.Combine(configDir, c_TablesXmlFileName);
                    byte[] previousXml = System.IO.File.Exists(xmlPath)
                        ? System.IO.File.ReadAllBytes(xmlPath)
                        : null;
                    GenerateTablesXml(xmlPath, manifest);
                    try
                    {
                        LubanSchemaManifestStore.Save(sourceDirPath, manifest);
                    }
                    catch (Exception saveException)
                    {
                        try
                        {
                            RestoreTablesXml(xmlPath, previousXml);
                        }
                        catch (Exception rollbackException)
                        {
                            throw new AggregateException(
                                "保存 Luban manifest 失败，且 __tables__.xml 回滚失败。",
                                saveException,
                                rollbackException);
                        }

                        throw;
                    }
                    return manifest;
                }

                private static void RestoreTablesXml(string xmlPath, byte[] previousContent)
                {
                    if (previousContent == null)
                    {
                        if (System.IO.File.Exists(xmlPath))
                        {
                            System.IO.File.Delete(xmlPath);
                        }
                        return;
                    }

                    string rollbackPath = xmlPath + ".rollback";
                    try
                    {
                        System.IO.File.WriteAllBytes(rollbackPath, previousContent);
                        if (System.IO.File.Exists(xmlPath))
                        {
                            System.IO.File.Replace(rollbackPath, xmlPath, null);
                        }
                        else
                        {
                            System.IO.File.Move(rollbackPath, xmlPath);
                        }
                    }
                    finally
                    {
                        if (System.IO.File.Exists(rollbackPath))
                        {
                            System.IO.File.Delete(rollbackPath);
                        }
                    }
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
                /// 从已验证的 schema manifest 生成 __tables__.xml。
                /// <para>每个 manifest table 对应一个 Luban table 元素。</para>
                /// <para>输出完全由 manifest 重建，不继承缓存文件中的额外节点。</para>
                /// </summary>
                /// <param name="xmlPath">__tables__.xml 文件路径。</param>
                /// <param name="sourceDirPath">数据源目录路径。</param>
                /// <param name="unitSettings">数据表单元设置列表。</param>
                internal static void GenerateTablesXml(string xmlPath, LubanSchemaManifest manifest)
                {
                    LubanSchemaManifestValidator.ValidateAndNormalize(manifest);
                    string temporaryPath = xmlPath + ".tmp";
                    try
                    {
                        XmlWriterSettings xmlSettings = new XmlWriterSettings { Indent = true, Encoding = s_Utf8NoBom };
                        using (XmlWriter writer = XmlWriter.Create(temporaryPath, xmlSettings))
                        {
                            writer.WriteStartDocument();
                            writer.WriteStartElement("module");

                            foreach (LubanSchemaUnit unit in manifest.Units)
                            {
                                string extension = Util.SysIO.Path.GetExtension(unit.LubanInputPath);
                                foreach (LubanSchemaTable table in unit.Tables)
                                {
                                    string input = extension == ".xlsx" || extension == ".xls"
                                        ? table.ValueType + "@" + unit.LubanInputPath
                                        : unit.LubanInputPath + "/" + table.ValueType + ".csv";

                                    writer.WriteStartElement("table");
                                    writer.WriteAttributeString("name", table.Name);
                                    writer.WriteAttributeString("value", table.ValueType);
                                    writer.WriteAttributeString("input", input);
                                    writer.WriteAttributeString("mode", unit.Mode);
                                    if (unit.Mode == "map")
                                    {
                                        writer.WriteAttributeString("index", unit.IndexField);
                                    }
                                    writer.WriteAttributeString("readSchemaFromFile", "true");
                                    writer.WriteAttributeString("comment", table.ValueType);
                                    writer.WriteEndElement();
                                }
                            }

                            writer.WriteEndElement();
                            writer.WriteEndDocument();
                        }

                        if (Util.SysIO.File.Exists(xmlPath))
                        {
                            System.IO.File.Replace(temporaryPath, xmlPath, null);
                        }
                        else
                        {
                            System.IO.File.Move(temporaryPath, xmlPath);
                        }
                    }
                    catch
                    {
                        try
                        {
                            if (Util.SysIO.File.Exists(temporaryPath))
                            {
                                System.IO.File.Delete(temporaryPath);
                            }
                        }
                        catch (Exception cleanupException)
                        {
                            Log.Warning(LogTag.Editor, "清理 Luban XML 临时文件失败：{0}", cleanupException.Message);
                        }
                        throw;
                    }
                }

            }
        }
    }
}
