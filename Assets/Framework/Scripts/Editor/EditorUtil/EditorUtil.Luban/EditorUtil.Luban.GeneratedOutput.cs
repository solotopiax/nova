/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.GeneratedOutput.cs
 * author:    taoye
 * created:   2026/7/21
 * descrip:   Luban 生成代码所有权标记、正文完整性校验与安全发布辅助
 * input:     生成文件、Profile、数据源、SchemaManifest 与正式代码目录
 * output:    第一行单行所有权标记，以及经校验的替换和精确删除登记
 * boundary:  不解释模块业务，也不决定模块应导出哪些数据或何时发布
 * failure:   归属不明或正文被修改的旧文件只警告并保留，不自动删除
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NovaFramework.Runtime;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Luban
        {
            /// <summary>
            /// 为 Luban 生成代码写入可自证的单行第一行标记，并校验正式文件是否仍属于原生成批次。
            /// </summary>
            internal static class GeneratedOutput
            {
                private const string c_MarkerPrefix = "// <nova-generated ";
                private static readonly Encoding s_Utf8NoBom = new UTF8Encoding(false);
                private static readonly Regex s_MarkerRegex = new Regex(
                    "^// <nova-generated profile=\"(?<profile>[^\"]*)\" source=\"(?<source>[^\"]*)\" artifact=\"(?<artifact>[^\"]*)\" content-hash=\"sha256:(?<hash>[0-9a-f]{64})\" />$",
                    RegexOptions.CultureInvariant);

                internal static void StampFile(
                    string filePath,
                    string profileId,
                    string source,
                    string artifact)
                {
                    RequireFile(filePath);
                    string body = ReadNormalizedBody(filePath, out _);
                    string normalizedSource = NormalizeSource(source);
                    string marker = c_MarkerPrefix +
                        "profile=\"" + Escape(profileId, nameof(profileId)) + "\" " +
                        "source=\"" + Escape(normalizedSource, nameof(source)) + "\" " +
                        "artifact=\"" + Escape(artifact, nameof(artifact)) + "\" " +
                        "content-hash=\"sha256:" + ComputeHash(body) + "\" />";
                    File.WriteAllText(filePath, marker + "\n" + body, s_Utf8NoBom);
                }

                internal static bool IsOwnedAndUnmodified(
                    string filePath,
                    string profileId,
                    string source)
                {
                    if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    {
                        return false;
                    }

                    string body = ReadNormalizedBody(filePath, out Ownership ownership);
                    return ownership != null &&
                           string.Equals(ownership.ProfileId, profileId, StringComparison.Ordinal) &&
                           string.Equals(ownership.Source, NormalizeSource(source), StringComparison.Ordinal) &&
                           string.Equals(ownership.Hash, ComputeHash(body), StringComparison.Ordinal);
                }

                /// <summary>
                /// 根据本次 SchemaManifest 验证并登记生成代码；全量发布时只删除归属和 Hash 均自证有效的过期文件。
                /// </summary>
                internal static void RegisterCodeOutputs(
                    EditorUtil.FileSystem.OutputApplier applier,
                    LubanExportContext context,
                    string stagedCodeDir,
                    string formalCodeDir,
                    bool deleteStaleFiles)
                {
                    if (applier == null)
                    {
                        throw new ArgumentNullException(nameof(applier));
                    }
                    if (context?.SchemaManifest == null)
                    {
                        throw new InvalidDataException("Luban code publication requires a SchemaManifest.");
                    }
                    if (string.IsNullOrWhiteSpace(stagedCodeDir) || !Directory.Exists(stagedCodeDir) ||
                        string.IsNullOrWhiteSpace(formalCodeDir))
                    {
                        throw new InvalidDataException("Luban staged or formal code directory is invalid.");
                    }

                    string profileId = context.SchemaManifest.ProfileId;
                    if (string.IsNullOrWhiteSpace(profileId))
                    {
                        throw new InvalidDataException("Luban SchemaManifest ProfileId is empty.");
                    }
                    string source = EditorUtil.FileSystem.GetProjectRelativePath(context.SourceDirPath);
                    HashSet<string> expectedFileNames = BuildExpectedFileNames(context);
                    foreach (string fileName in expectedFileNames)
                    {
                        string stagedPath = IOPath.Combine(stagedCodeDir, fileName);
                        if (!File.Exists(stagedPath))
                        {
                            throw new FileNotFoundException($"Luban staged code file is missing: {stagedPath}", stagedPath);
                        }

                        StampFile(stagedPath, profileId, source, fileName);
                        applier.AddReplacement(stagedPath, IOPath.Combine(formalCodeDir, fileName));
                    }

                    if (context.TargetUnit == null)
                    {
                        RegisterCache(applier, context, formalCodeDir, source, profileId, expectedFileNames);
                    }

                    if (!deleteStaleFiles || !Directory.Exists(formalCodeDir))
                    {
                        return;
                    }

                    foreach (string formalPath in Directory.GetFiles(formalCodeDir, "*.cs", SearchOption.TopDirectoryOnly))
                    {
                        if (expectedFileNames.Contains(IOPath.GetFileName(formalPath)))
                        {
                            continue;
                        }
                        if (!IsOwnedAndUnmodified(formalPath, profileId, source))
                        {
                            Log.Warning(
                                LogTag.Editor,
                                "无法安全删除 Luban 过期类型文件，已保留（标记缺失、归属不一致或正文已修改）：{0}",
                                formalPath);
                            continue;
                        }

                        applier.AddDeletion(formalPath);
                        if (File.Exists(formalPath + ".meta"))
                        {
                            applier.AddDeletion(formalPath + ".meta");
                        }
                    }
                }

                private static HashSet<string> BuildExpectedFileNames(LubanExportContext context)
                {
                    var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        context.ManagerName + ".cs",
                    };
                    if (context.TargetUnit != null)
                    {
                        AddUnitFileNames(context.SchemaManifest.ResolveUnit(context.TargetUnit.SourcePath), result);
                    }
                    else
                    {
                        foreach (LubanSchemaUnit unit in context.SchemaManifest.Units)
                        {
                            AddUnitFileNames(unit, result);
                        }
                    }
                    return result;
                }

                private static void RegisterCache(
                    EditorUtil.FileSystem.OutputApplier applier,
                    LubanExportContext context,
                    string formalCodeDir,
                    string source,
                    string profileId,
                    HashSet<string> expectedFileNames)
                {
                    var files = new List<string>(expectedFileNames);
                    files.Sort(StringComparer.OrdinalIgnoreCase);
                    var cache = new OutputManifestCache
                    {
                        ProfileId = profileId,
                        Source = source,
                        CodeOutputDirectory = EditorUtil.FileSystem.GetProjectRelativePath(formalCodeDir),
                        Files = files,
                    };
                    string stagedPath = IOPath.Combine(
                        applier.StagingRoot,
                        "metadata",
                        "output-manifests",
                        profileId + ".json");
                    Directory.CreateDirectory(IOPath.GetDirectoryName(stagedPath));
                    File.WriteAllText(stagedPath, Util.Json.Serialize(cache), s_Utf8NoBom);

                    string targetPath = IOPath.Combine(
                        ConfigSyncer.GetConfigDirPath(context.SourceDirPath),
                        "output-manifests",
                        profileId + ".json");
                    applier.AddReplacement(stagedPath, targetPath);
                }

                private static void AddUnitFileNames(LubanSchemaUnit unit, ISet<string> fileNames)
                {
                    foreach (LubanSchemaTable table in unit.Tables)
                    {
                        fileNames.Add(table.Name + ".cs");
                        fileNames.Add(table.ValueType + ".cs");
                    }
                }

                private static string ReadNormalizedBody(string filePath, out Ownership ownership)
                {
                    string content = NormalizeLineEndings(File.ReadAllText(filePath, s_Utf8NoBom));
                    int lineEnd = content.IndexOf('\n');
                    string firstLine = lineEnd >= 0 ? content.Substring(0, lineEnd) : content;
                    ownership = Parse(firstLine);
                    return ownership == null
                        ? content
                        : lineEnd >= 0 ? content.Substring(lineEnd + 1) : string.Empty;
                }

                private static Ownership Parse(string firstLine)
                {
                    if (string.IsNullOrEmpty(firstLine) ||
                        !firstLine.StartsWith(c_MarkerPrefix, StringComparison.Ordinal))
                    {
                        return null;
                    }

                    Match match = s_MarkerRegex.Match(firstLine);
                    if (!match.Success)
                    {
                        return null;
                    }

                    return new Ownership(
                        Unescape(match.Groups["profile"].Value),
                        Unescape(match.Groups["source"].Value),
                        match.Groups["hash"].Value);
                }

                private static string ComputeHash(string body)
                {
                    using SHA256 sha256 = SHA256.Create();
                    byte[] bytes = s_Utf8NoBom.GetBytes(NormalizeLineEndings(body));
                    byte[] hash = sha256.ComputeHash(bytes);
                    var result = new StringBuilder(hash.Length * 2);
                    foreach (byte value in hash)
                    {
                        result.Append(value.ToString("x2"));
                    }
                    return result.ToString();
                }

                private static string NormalizeLineEndings(string value)
                {
                    return (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
                }

                private static string NormalizeSource(string source)
                {
                    if (string.IsNullOrWhiteSpace(source))
                    {
                        throw new ArgumentException("Generated output source cannot be empty.", nameof(source));
                    }
                    return source.Trim().Replace('\\', '/').TrimEnd('/');
                }

                private static string Escape(string value, string parameterName)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new ArgumentException("Generated output metadata cannot be empty.", parameterName);
                    }
                    return SecurityElement.Escape(value.Trim());
                }

                private static string Unescape(string value)
                {
                    return value.Replace("&quot;", "\"")
                        .Replace("&apos;", "'")
                        .Replace("&gt;", ">")
                        .Replace("&lt;", "<")
                        .Replace("&amp;", "&");
                }

                private static void RequireFile(string filePath)
                {
                    if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    {
                        throw new FileNotFoundException("Generated output file does not exist.", filePath);
                    }
                }

                private sealed class Ownership
                {
                    internal Ownership(string profileId, string source, string hash)
                    {
                        ProfileId = profileId;
                        Source = source;
                        Hash = hash;
                    }

                    internal string ProfileId { get; }
                    internal string Source { get; }
                    internal string Hash { get; }
                }

                [Serializable]
                private sealed class OutputManifestCache
                {
                    public string ProfileId;
                    public string Source;
                    public string CodeOutputDirectory;
                    public List<string> Files;
                }
            }
        }
    }
}
