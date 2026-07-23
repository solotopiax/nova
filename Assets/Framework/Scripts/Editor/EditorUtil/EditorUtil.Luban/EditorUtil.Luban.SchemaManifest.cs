/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.SchemaManifest.cs
 * author:    taoye
 * created:   2026/7/16
 * descrip:   Luban Excel schema manifest model, validation, storage, and builder
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Luban
        {
            [Serializable]
            internal sealed class LubanSchemaManifest
            {
                internal const int CurrentSchemaVersion = 1;

                [JsonProperty("schemaVersion", Required = Required.Always)]
                public int SchemaVersion = CurrentSchemaVersion;

                [JsonProperty("profileId", Required = Required.Always)]
                public string ProfileId;

                [JsonProperty("units", Required = Required.Always)]
                public List<LubanSchemaUnit> Units = new List<LubanSchemaUnit>();

                internal LubanSchemaUnit ResolveUnit(string sourcePath)
                {
                    string normalizedSourcePath = LubanSchemaManifestValidator.NormalizeRelativeSourcePath(sourcePath);
                    LubanSchemaUnit unit = Units?.Find(candidate =>
                        string.Equals(candidate.SourcePath, normalizedSourcePath, StringComparison.OrdinalIgnoreCase));
                    if (unit == null)
                    {
                        throw new InvalidDataException(
                            $"Luban schema manifest does not contain sourcePath '{normalizedSourcePath}'.");
                    }

                    return unit;
                }
            }

            [Serializable]
            internal sealed class LubanSchemaUnit
            {
                [JsonProperty("sourcePath", Required = Required.Always)]
                public string SourcePath;

                [JsonProperty("lubanInputPath", Required = Required.Always)]
                public string LubanInputPath;

                [JsonProperty("datasExportPath", Required = Required.Always)]
                public string DatasExportPath;

                [JsonProperty("classesExportPath", Required = Required.Always)]
                public string ClassesExportPath;

                [JsonProperty("mode", Required = Required.Always)]
                public string Mode;

                [JsonProperty("indexField", Required = Required.Always)]
                public string IndexField;

                [JsonProperty("tables", Required = Required.Always)]
                public List<LubanSchemaTable> Tables = new List<LubanSchemaTable>();
            }

            [Serializable]
            internal sealed class LubanSchemaTable
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name;

                [JsonProperty("valueType", Required = Required.Always)]
                public string ValueType;
            }

            internal static class LubanSchemaManifestValidator
            {
                internal static void ValidateAndNormalize(LubanSchemaManifest manifest)
                {
                    if (manifest == null)
                    {
                        throw new InvalidDataException("Luban schema manifest cannot be null.");
                    }

                    if (manifest.SchemaVersion != LubanSchemaManifest.CurrentSchemaVersion)
                    {
                        throw new InvalidDataException(
                            $"Unsupported Luban schema manifest version: {manifest.SchemaVersion}.");
                    }

                    RequireValue(manifest.ProfileId, "profileId");
                    if (manifest.Units == null)
                    {
                        throw new InvalidDataException("Luban schema manifest units cannot be null.");
                    }

                    var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var tableNames = new HashSet<string>(StringComparer.Ordinal);

                    foreach (LubanSchemaUnit unit in manifest.Units)
                    {
                        if (unit == null)
                        {
                            throw new InvalidDataException("Luban schema manifest unit cannot be null.");
                        }

                        unit.SourcePath = NormalizeRelativeSourcePath(unit.SourcePath);
                        unit.LubanInputPath = NormalizeRequiredPath(unit.LubanInputPath, "lubanInputPath");
                        unit.DatasExportPath = NormalizeOptionalPath(unit.DatasExportPath);
                        unit.ClassesExportPath = NormalizeOptionalPath(unit.ClassesExportPath);
                        RequireValue(unit.Mode, "mode");
                        unit.IndexField ??= string.Empty;
                        if (unit.Mode != "list" && unit.Mode != "map" && unit.Mode != "one")
                        {
                            throw new InvalidDataException($"Unsupported Luban table mode: {unit.Mode}.");
                        }
                        if (unit.Mode == "map" && string.IsNullOrWhiteSpace(unit.IndexField))
                        {
                            throw new InvalidDataException(
                                $"Luban map unit '{unit.SourcePath}' requires an indexField.");
                        }

                        if (!sourcePaths.Add(unit.SourcePath))
                        {
                            throw new InvalidDataException($"Duplicate Luban sourcePath: {unit.SourcePath}.");
                        }

                        if (unit.Tables == null)
                        {
                            throw new InvalidDataException(
                                $"Luban schema manifest tables cannot be null for sourcePath '{unit.SourcePath}'.");
                        }

                        foreach (LubanSchemaTable table in unit.Tables)
                        {
                            if (table == null)
                            {
                                throw new InvalidDataException(
                                    $"Luban schema table cannot be null for sourcePath '{unit.SourcePath}'.");
                            }

                            RequireValue(table.Name, "table.name");
                            RequireValue(table.ValueType, "table.valueType");
                            if (!tableNames.Add(table.Name))
                            {
                                throw new InvalidDataException($"Duplicate Luban table name: {table.Name}.");
                            }
                        }

                        unit.Tables.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
                    }

                    manifest.Units.Sort((left, right) => string.CompareOrdinal(left.SourcePath, right.SourcePath));
                }

                internal static string NormalizePath(string value)
                {
                    if (value == null)
                    {
                        return null;
                    }

                    string normalized = value.Replace('\\', '/');
                    while (normalized.Contains("//"))
                    {
                        normalized = normalized.Replace("//", "/");
                    }

                    return normalized.TrimEnd('/');
                }

                internal static string NormalizeRelativeSourcePath(string value)
                {
                    string normalized = NormalizePath(value);
                    ValidateRelativeSourcePath(normalized);
                    return normalized;
                }

                private static string NormalizeRequiredPath(string value, string fieldName)
                {
                    string normalized = NormalizePath(value);
                    RequireValue(normalized, fieldName);
                    return normalized;
                }

                private static string NormalizeOptionalPath(string value)
                {
                    return NormalizePath(value) ?? string.Empty;
                }

                private static void ValidateRelativeSourcePath(string sourcePath)
                {
                    RequireValue(sourcePath, "sourcePath");
                    bool hasDrivePrefix = sourcePath.Length >= 2 && char.IsLetter(sourcePath[0]) && sourcePath[1] == ':';
                    if (sourcePath.StartsWith("/", StringComparison.Ordinal) || hasDrivePrefix || System.IO.Path.IsPathRooted(sourcePath))
                    {
                        throw new InvalidDataException($"Luban sourcePath must be relative: {sourcePath}.");
                    }

                    if (sourcePath.Split('/').Any(segment => segment == ".."))
                    {
                        throw new InvalidDataException($"Luban sourcePath cannot contain traversal: {sourcePath}.");
                    }
                }

                private static void RequireValue(string value, string fieldName)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new InvalidDataException($"Luban schema manifest field '{fieldName}' cannot be empty.");
                    }
                }
            }

            internal static class LubanSchemaManifestStore
            {
                internal const string FileName = "nova-export-manifest.json";
                private static readonly Encoding s_Utf8NoBom = new UTF8Encoding(false);

                internal static string GetPath(string sourceDirPath)
                {
                    return System.IO.Path.Combine(ConfigSyncer.GetConfigDirPath(sourceDirPath), FileName);
                }

                internal static LubanSchemaManifest Load(string sourceDirPath)
                {
                    string json = File.ReadAllText(GetPath(sourceDirPath), s_Utf8NoBom);
                    try
                    {
                        LubanSchemaManifest manifest = Util.Json.Deserialize<LubanSchemaManifest>(json);
                        LubanSchemaManifestValidator.ValidateAndNormalize(manifest);
                        return manifest;
                    }
                    catch (JsonException exception)
                    {
                        throw new InvalidDataException("Invalid Luban schema manifest JSON.", exception);
                    }
                }

                internal static void Save(
                    string sourceDirPath,
                    LubanSchemaManifest manifest,
                    Action<string, string> replaceExisting = null)
                {
                    LubanSchemaManifestValidator.ValidateAndNormalize(manifest);
                    string json = Util.Json.Serialize(manifest, Formatting.Indented);
                    string destinationPath = GetPath(sourceDirPath);
                    string directoryPath = System.IO.Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    string temporaryPath = destinationPath + ".tmp";
                    try
                    {
                        File.WriteAllText(temporaryPath, json, s_Utf8NoBom);
                        if (File.Exists(destinationPath))
                        {
                            (replaceExisting ?? ReplaceFile)(temporaryPath, destinationPath);
                        }
                        else
                        {
                            File.Move(temporaryPath, destinationPath);
                        }
                    }
                    catch
                    {
                        try
                        {
                            if (File.Exists(temporaryPath))
                            {
                                File.Delete(temporaryPath);
                            }
                        }
                        catch (Exception cleanupException)
                        {
                            Log.Warning(LogTag.Editor, "清理 Luban manifest 临时文件失败：{0}", cleanupException.Message);
                        }

                        throw;
                    }
                }

                private static void ReplaceFile(string sourcePath, string destinationPath)
                {
                    File.Replace(sourcePath, destinationPath, null);
                }
            }

            internal static class LubanSchemaManifestBuilder
            {
                internal static LubanSchemaManifest Build(
                    string sourceDirPath,
                    string profileId,
                    IReadOnlyList<IDataTableUnitSetting> effectiveUnits,
                    int minHeaderRowCount,
                    Func<string, int, IReadOnlyList<string>> scanValueTypes = null)
                {
                    if (effectiveUnits == null)
                    {
                        throw new InvalidDataException("Effective Luban units cannot be null.");
                    }

                    var manifest = new LubanSchemaManifest { ProfileId = profileId };
                    foreach (IDataTableUnitSetting unit in effectiveUnits)
                    {
                        if (unit == null)
                        {
                            throw new InvalidDataException("Effective Luban unit cannot be null.");
                        }

                        string sourcePath = LubanSchemaManifestValidator.NormalizeRelativeSourcePath(unit.SourcePath);
                        string fullPath = System.IO.Path.Combine(sourceDirPath, sourcePath ?? string.Empty);
                        IReadOnlyList<string> valueTypes = (scanValueTypes ?? DataTypeNameHelper.ScanValueTypes)(
                            fullPath,
                            minHeaderRowCount);

                        var schemaUnit = new LubanSchemaUnit
                        {
                            SourcePath = sourcePath,
                            LubanInputPath = unit.LubanInputPath,
                            DatasExportPath = unit.DatasExportPath,
                            ClassesExportPath = unit.ClassesExportPath,
                            Mode = unit.Mode.ToString().ToLowerInvariant(),
                            IndexField = unit.Mode == DataTableMode.Map ? unit.IndexField ?? string.Empty : string.Empty,
                        };

                        if (valueTypes == null)
                        {
                            throw new InvalidDataException($"Luban schema scan returned null for '{fullPath}'.");
                        }

                        foreach (string valueType in valueTypes)
                        {
                            string shortValueType = valueType;
                            int namespaceSeparatorIndex = valueType?.LastIndexOf('.') ?? -1;
                            if (namespaceSeparatorIndex >= 0)
                            {
                                shortValueType = valueType.Substring(namespaceSeparatorIndex + 1);
                            }

                            schemaUnit.Tables.Add(new LubanSchemaTable
                            {
                                Name = "Tb" + shortValueType,
                                ValueType = shortValueType,
                            });
                        }

                        manifest.Units.Add(schemaUnit);
                    }

                    LubanSchemaManifestValidator.ValidateAndNormalize(manifest);
                    return manifest;
                }
            }
        }
    }
}
