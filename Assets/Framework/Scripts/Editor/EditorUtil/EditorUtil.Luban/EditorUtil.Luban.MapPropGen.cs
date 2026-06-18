/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.MapPropGen.cs
 * author:    taoye
 * created:   2026/4/16
 * descrip:   Luban Map 模式属性生成器（通用版）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Luban
        {
            /// <summary>
            /// Luban Map 模式属性生成器。
            /// <para>读取导出的 JSON 数据，提取 Map 模式表格的所有键值，
            /// 在对应 TbXxx.cs 文件末尾追加 partial class，为每个键生成只读属性。</para>
            /// <para>统一支持 Table/Config 模块：通过 IDataTableUnitSetting.Mode/IndexField 实现参数化。</para>
            /// </summary>
            public static class MapPropGen
            {
                /// <summary>
                /// 自动生成代码块的起始标记。
                /// </summary>
                private const string c_RegionBegin = "// --- AUTO-GENERATED MAP PROPERTIES BEGIN ---";

                /// <summary>
                /// 自动生成代码块的结束标记。
                /// </summary>
                private const string c_RegionEnd = "// --- AUTO-GENERATED MAP PROPERTIES END ---";

                /// <summary>
                /// UTF-8 无 BOM 编码。
                /// </summary>
                private static readonly Encoding s_Utf8NoBom = new UTF8Encoding(false);

                /// <summary>
                /// 为所有 Map 模式的单元批量生成属性访问器。
                /// </summary>
                /// <param name="unitSettings">全部单元设置列表。</param>
                /// <param name="topModule">顶层命名空间（如 "Game.Runtime"）。</param>
                /// <returns>生成结果（文件名 → 属性数量），供调用方合并日志使用。</returns>
                public static Dictionary<string, int> GenerateAll(IReadOnlyList<IDataTableUnitSetting> unitSettings, string topModule)
                {
                    Dictionary<string, int> result = new Dictionary<string, int>();
                    if (unitSettings == null)
                    {
                        return result;
                    }

                    foreach (IDataTableUnitSetting unit in unitSettings)
                    {
                        if (unit.Mode != DataTableMode.Map)
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(unit.DatasExportPath) || string.IsNullOrEmpty(unit.ClassesExportPath) || unit.DataTypeNames == null)
                        {
                            continue;
                        }

                        Dictionary<string, int> unitResult = GenerateForUnit(unit, topModule);
                        foreach (var kvp in unitResult)
                        {
                            result[kvp.Key] = kvp.Value;
                        }
                    }

                    return result;
                }

                /// <summary>
                /// 为单个 Map 模式的单元生成属性访问器。
                /// </summary>
                /// <param name="unitSetting">单元设置。</param>
                /// <param name="topModule">顶层命名空间。</param>
                /// <returns>生成结果（文件名 → 属性数量），供调用方合并日志使用。</returns>
                public static Dictionary<string, int> GenerateForUnit(IDataTableUnitSetting unitSetting, string topModule)
                {
                    Dictionary<string, int> result = new Dictionary<string, int>();

                    if (unitSetting.Mode != DataTableMode.Map)
                    {
                        return result;
                    }

                    string jsonPath = unitSetting.DatasExportPath;
                    if (!Util.SysIO.File.Exists(jsonPath))
                    {
                        Log.Warning(LogTag.Editor, "Map 属性生成跳过：JSON 文件不存在 {0}", jsonPath);
                        return result;
                    }

                    string indexField = string.IsNullOrEmpty(unitSetting.IndexField) ? "ID" : unitSetting.IndexField;

                    JObject rootJson;
                    try
                    {
                        string content = Util.SysIO.File.ReadAllTextSync(jsonPath, Encoding.UTF8);
                        rootJson = JObject.Parse(content);
                    }
                    catch (Exception e)
                    {
                        Log.Error(LogTag.Editor, "Map 属性生成失败：解析 JSON 出错 {0}: {1}", jsonPath, e.Message);
                        return result;
                    }

                    foreach (string typeName in unitSetting.DataTypeNames)
                    {
                        if (string.IsNullOrEmpty(typeName) || typeName.StartsWith("#"))
                        {
                            continue;
                        }

                        string sheetName = typeName.Contains(".") ? typeName.Substring(typeName.LastIndexOf('.') + 1) : typeName;
                        string tableName = "Tb" + sheetName;
                        string dataTypeName = sheetName;

                        JToken sheetToken = rootJson[sheetName];
                        if (sheetToken is not JArray dataArray || dataArray.Count == 0)
                        {
                            continue;
                        }

                        List<MapKeyEntry> keys = ExtractMapKeys(dataArray, indexField);
                        if (keys.Count == 0)
                        {
                            continue;
                        }

                        string classExportDir = unitSetting.ClassesExportPath;
                        string tbFilePath = Util.SysIO.Path.Combine(classExportDir, tableName + ".cs");
                        if (!Util.SysIO.File.Exists(tbFilePath))
                        {
                            Log.Warning(LogTag.Editor, "Map 属性生成跳过：TbXxx 文件不存在 {0}", tbFilePath);
                            continue;
                        }

                        AppendMapProperties(tbFilePath, tableName, dataTypeName, topModule, keys);
                        string fileName = tableName + ".cs";
                        result[fileName] = keys.Count;
                    }

                    return result;
                }

                /// <summary>
                /// 从 JSON 数据数组中提取所有 Map 键及其注释。
                /// </summary>
                /// <param name="dataArray">JSON 数据数组。</param>
                /// <param name="indexField">索引字段名。</param>
                /// <returns>键条目列表。</returns>
                private static List<MapKeyEntry> ExtractMapKeys(JArray dataArray, string indexField)
                {
                    List<MapKeyEntry> keys = new List<MapKeyEntry>();
                    HashSet<string> seen = new HashSet<string>();

                    foreach (JToken item in dataArray)
                    {
                        if (item is not JObject obj)
                        {
                            continue;
                        }

                        string keyValue = obj[indexField]?.ToString();
                        if (string.IsNullOrEmpty(keyValue) || !seen.Add(keyValue))
                        {
                            continue;
                        }

                        string desc = obj["Desc"]?.ToString() ?? "";
                        keys.Add(new MapKeyEntry(keyValue, desc));
                    }

                    return keys;
                }

                /// <summary>
                /// 在 TbXxx.cs 文件末尾追加 partial class Map 属性块。
                /// <para>若文件已包含自动生成的属性块，先移除旧块再重新追加。</para>
                /// </summary>
                /// <param name="filePath">TbXxx.cs 文件路径。</param>
                /// <param name="tableName">表名（如 "TbSystemConfig"）。</param>
                /// <param name="dataTypeName">数据类型名（如 "SystemConfig"）。</param>
                /// <param name="topModule">命名空间。</param>
                /// <param name="keys">Map 键条目列表。</param>
                private static void AppendMapProperties(string filePath, string tableName, string dataTypeName, string topModule, List<MapKeyEntry> keys)
                {
                    string existingContent = Util.SysIO.File.ReadAllTextSync(filePath, s_Utf8NoBom);
                    existingContent = RemoveOldGeneratedBlock(existingContent);

                    StringBuilder sb = new StringBuilder();
                    sb.Append('\n');
                    sb.Append(c_RegionBegin).Append('\n');
                    sb.Append("#pragma warning disable\n");
                    sb.Append('\n');

                    if (!string.IsNullOrEmpty(topModule))
                    {
                        sb.Append($"namespace {topModule}").Append('\n');
                        sb.Append('{').Append('\n');
                    }

                    sb.Append($"public partial class {tableName}").Append('\n');
                    sb.Append('{').Append('\n');

                    foreach (MapKeyEntry entry in keys)
                    {
                        if (!IsValidCSharpIdentifier(entry.Key))
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(entry.Desc))
                        {
                            sb.Append("    /// <summary>").Append('\n');
                            sb.Append($"    /// {entry.Desc}").Append('\n');
                            sb.Append("    /// </summary>").Append('\n');
                        }
                        sb.Append($"    public {dataTypeName} {entry.Key} => GetOrDefault(\"{entry.Key}\");").Append('\n');
                        sb.Append('\n');
                    }

                    sb.Append('}').Append('\n');

                    if (!string.IsNullOrEmpty(topModule))
                    {
                        sb.Append('}').Append('\n');
                    }

                    sb.Append("#pragma warning restore\n");
                    sb.Append('\n');
                    sb.Append(c_RegionEnd);

                    string finalContent = existingContent.TrimEnd() + "\n" + sb.ToString() + "\n";
                    Util.SysIO.File.WriteAllTextSync(filePath, finalContent, s_Utf8NoBom);
                }

                /// <summary>
                /// 移除文件内容中已存在的自动生成属性块。
                /// </summary>
                /// <param name="content">文件内容。</param>
                /// <returns>移除后的内容。</returns>
                private static string RemoveOldGeneratedBlock(string content)
                {
                    int beginIndex = content.IndexOf(c_RegionBegin, StringComparison.Ordinal);
                    if (beginIndex < 0)
                    {
                        return content;
                    }

                    int endIndex = content.IndexOf(c_RegionEnd, beginIndex, StringComparison.Ordinal);
                    if (endIndex < 0)
                    {
                        return content.Substring(0, beginIndex);
                    }

                    return content.Substring(0, beginIndex) + content.Substring(endIndex + c_RegionEnd.Length);
                }

                /// <summary>
                /// 检查字符串是否为合法的 C# 标识符。
                /// </summary>
                /// <param name="name">待检查的字符串。</param>
                /// <returns>是否合法。</returns>
                private static bool IsValidCSharpIdentifier(string name)
                {
                    if (string.IsNullOrEmpty(name))
                    {
                        return false;
                    }

                    if (!char.IsLetter(name[0]) && name[0] != '_')
                    {
                        return false;
                    }

                    for (int i = 1; i < name.Length; i++)
                    {
                        if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                        {
                            return false;
                        }
                    }

                    return true;
                }

                /// <summary>
                /// Map 键条目（键值 + 注释描述）。
                /// </summary>
                private readonly struct MapKeyEntry
                {
                    /// <summary>
                    /// 键值（将成为 C# 属性名）。
                    /// </summary>
                    public readonly string Key;

                    /// <summary>
                    /// 描述注释。
                    /// </summary>
                    public readonly string Desc;

                    /// <summary>
                    /// 构造 Map 键条目。
                    /// </summary>
                    /// <param name="key">键值。</param>
                    /// <param name="desc">描述。</param>
                    public MapKeyEntry(string key, string desc)
                    {
                        Key = key;
                        Desc = desc;
                    }
                }
            }
        }
    }
}
