/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.BinaryPackager.cs
 * author:    taoye
 * created:   2026/7/29
 * descrip:   Luban Binary 单表产物聚合器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NovaFramework.Runtime;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Luban
        {
            /// <summary>
            /// 把 Luban 原生单表 Binary 产物按 SchemaManifest Unit 聚合为 Nova 数据包。
            /// 包内表负载保持 Luban 原始字节不变。
            /// </summary>
            internal static class BinaryPackager
            {
                private static readonly byte[] s_Magic = Encoding.ASCII.GetBytes("NLBP");
                private const byte c_Version = 1;

                /// <summary>
                /// 聚合 Manifest 中全部有数据输出的 Unit。
                /// </summary>
                /// <param name="lubanOutputDir">Luban 原生 Binary 输出目录。</param>
                /// <param name="manifest">本次导出的 SchemaManifest。</param>
                /// <param name="deferredResults">输出路径到表数量的可选结果字典。</param>
                /// <returns>全部 Unit 都成功时返回 true。</returns>
                internal static bool PackageAll(
                    string lubanOutputDir,
                    LubanSchemaManifest manifest,
                    Dictionary<string, int> deferredResults = null)
                {
                    if (manifest == null)
                    {
                        throw new ArgumentNullException(nameof(manifest));
                    }

                    try
                    {
                        foreach (LubanSchemaUnit unit in manifest.Units)
                        {
                            if (string.IsNullOrWhiteSpace(unit.DatasExportPath) || unit.Tables.Count == 0)
                            {
                                continue;
                            }

                            PackageUnit(lubanOutputDir, unit);
                            if (deferredResults != null)
                            {
                                deferredResults[unit.DatasExportPath] = unit.Tables.Count;
                            }
                        }
                        return true;
                    }
                    catch (Exception exception)
                    {
                        Log.Error(LogTag.Editor, "Binary 数据聚合失败：{0}", exception.Message);
                        return false;
                    }
                }

                internal static bool PackageForUnit(
                    string lubanOutputDir,
                    LubanSchemaUnit unit,
                    Dictionary<string, int> deferredResults = null)
                {
                    if (unit == null)
                    {
                        throw new ArgumentNullException(nameof(unit));
                    }

                    try
                    {
                        PackageUnit(lubanOutputDir, unit);
                        if (deferredResults != null)
                        {
                            deferredResults[unit.DatasExportPath] = unit.Tables.Count;
                        }
                        return true;
                    }
                    catch (Exception exception)
                    {
                        Log.Error(LogTag.Editor, "Binary 数据聚合失败：{0}", exception.Message);
                        return false;
                    }
                }

                private static void PackageUnit(string lubanOutputDir, LubanSchemaUnit unit)
                {
                    var entries = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
                    foreach (LubanSchemaTable table in unit.Tables)
                    {
                        string dataFile = table.Name.ToLowerInvariant();
                        string sourcePath = IOPath.Combine(lubanOutputDir, dataFile + ".bytes");
                        if (!File.Exists(sourcePath))
                        {
                            throw new FileNotFoundException($"未找到 Luban Binary 导出文件：{sourcePath}", sourcePath);
                        }
                        entries.Add(dataFile, File.ReadAllBytes(sourcePath));
                    }

                    string outputDirectory = IOPath.GetDirectoryName(unit.DatasExportPath);
                    if (!string.IsNullOrEmpty(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }

                    using var stream = new MemoryStream();
                    using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), true))
                    {
                        writer.Write(s_Magic);
                        writer.Write(c_Version);
                        writer.Write(entries.Count);
                        foreach (KeyValuePair<string, byte[]> entry in entries)
                        {
                            byte[] nameBytes = Encoding.UTF8.GetBytes(entry.Key);
                            writer.Write(nameBytes.Length);
                            writer.Write(nameBytes);
                            writer.Write(entry.Value.Length);
                            writer.Write(entry.Value);
                        }
                    }
                    File.WriteAllBytes(unit.DatasExportPath, stream.ToArray());
                }
            }
        }
    }
}
