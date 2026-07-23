/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationExcelPreFilter.cs
 * author:    taoye
 * created:   2026/4/19
 * descrip:   加载并校验 Settings 指定的 Localization Excel，投影 Luban 输入 CSV
 * input:     Localization 源目录、IDataTableUnitSetting 与目标语言
 * output:    调用方指定的 _temp 目录下 Name/Value CSV
 * reason:    Excel 的多语言列结构不能直接作为单语言 Luban 输入
 * boundary:  不调用 Luban，不选择正式输出路径，不发布或删除正式产物
 * failure:   校验失败立即终止；本文件只写临时投影，不修改正式产物
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovaFramework.Runtime;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 一次加载并验证 Settings 指定的 Excel，再按目标语言投影为 Luban 使用的 Name/Value CSV。
    /// 只操作调用方提供的临时输出目录，不调用 Luban，也不修改正式导出产物。
    /// </summary>
    internal static class LocalizationExcelPreFilter
    {
        private const int c_DataStartRow = 4;

        internal static void ProjectCodeGen(SourceModel model, string outputRoot)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model.Languages == null || model.Languages.Count == 0)
            {
                throw new InvalidDataException("Localization source model contains no languages.");
            }

            ProjectLanguage(model, outputRoot, model.Languages[0]);
        }

        internal static void ProjectLanguage(
            SourceModel model,
            string outputRoot,
            string languageName)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (string.IsNullOrEmpty(outputRoot))
            {
                throw new ArgumentException(
                    "Localization projection output root cannot be empty.",
                    nameof(outputRoot));
            }

            bool containsLanguage = false;
            foreach (string language in model.Languages)
            {
                if (string.Equals(language, languageName, StringComparison.Ordinal))
                {
                    containsLanguage = true;
                    break;
                }
            }

            if (!containsLanguage)
            {
                throw new InvalidDataException(
                    $"Localization source model does not contain language '{languageName}'.");
            }

            foreach (SourceUnit unit in model.Units)
            {
                var outputSheets = new Dictionary<string, List<IReadOnlyList<string>>>();
                foreach (SourceSheet sheet in unit.Sheets)
                {
                    outputSheets.Add(sheet.Name, BuildProjectedSheet(sheet, languageName));
                }

                EditorUtil.Excel.Write(
                    Util.SysIO.Path.Combine(outputRoot, unit.RelativeStem),
                    outputSheets);
            }
        }

        /// <summary>
        /// 兼容无 Settings 参数的独立语言列表入口。完整数据/代码导出不调用此递归扫描。
        /// </summary>
        internal static HashSet<string> ExtractAllLanguageColumns(string sourceDirPath)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(sourceDirPath) || !Directory.Exists(sourceDirPath))
            {
                return result;
            }

            string root = IOPath.GetFullPath(sourceDirPath);
            foreach (string filePath in Directory.GetFiles(root, "*.xlsx", SearchOption.AllDirectories))
            {
                string relativePath = IOPath.GetRelativePath(root, filePath).Replace('\\', '/');
                if (relativePath.StartsWith("_configs/", StringComparison.OrdinalIgnoreCase) ||
                    relativePath.StartsWith("_temp/", StringComparison.OrdinalIgnoreCase) ||
                    IOPath.GetFileName(filePath).StartsWith("~$", StringComparison.Ordinal))
                {
                    continue;
                }

                Dictionary<string, List<IReadOnlyList<string>>> sheets =
                    EditorUtil.Excel.ReadAllSheets(filePath);
                if (sheets == null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, List<IReadOnlyList<string>>> pair in sheets)
                {
                    if (pair.Key.StartsWith("#", StringComparison.Ordinal) ||
                        pair.Value == null ||
                        pair.Value.Count < 2 ||
                        pair.Value[1] == null)
                    {
                        continue;
                    }

                    foreach (string rawHeader in pair.Value[1])
                    {
                        string header = rawHeader?.Trim();
                        if (!string.IsNullOrEmpty(header) && header[0] == '#')
                        {
                            header = header.Substring(1);
                        }

                        if (Enum.TryParse(header, false, out Language language) &&
                            Enum.IsDefined(typeof(Language), language) &&
                            string.Equals(
                                Enum.GetName(typeof(Language), language),
                                header,
                                StringComparison.Ordinal))
                        {
                            result.Add(header);
                        }
                    }
                }
            }

            return result;
        }

        private static List<IReadOnlyList<string>> BuildProjectedSheet(
            SourceSheet sheet,
            string languageName)
        {
            if (!sheet.LanguageColumnIndexes.TryGetValue(languageName, out int languageColumnIndex))
            {
                throw new InvalidDataException(
                    $"Localization sheet '{sheet.Name}' does not contain language '{languageName}'.");
            }

            var outputRows = new List<IReadOnlyList<string>>
            {
                new List<string> { "##comment", sheet.Name },
                new List<string> { "##var", "Name", "Value" },
                new List<string> { "##type", "string", "string" },
                new List<string> { "##comment", "键名", "值" },
            };

            for (int rowIndex = c_DataStartRow; rowIndex < sheet.Rows.Count; rowIndex++)
            {
                IReadOnlyList<string> row = sheet.Rows[rowIndex];
                if (row == null || sheet.NameColumnIndex >= row.Count)
                {
                    continue;
                }

                string key = row[sheet.NameColumnIndex]?.Trim();
                if (string.IsNullOrEmpty(key) || key.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string value = languageColumnIndex < row.Count
                    ? row[languageColumnIndex] ?? string.Empty
                    : string.Empty;
                outputRows.Add(new List<string> { string.Empty, key, value });
            }

            return outputRows;
        }

        internal sealed class SourceModel
        {
            private const int c_VarRowIndex = 1;
            private const int c_MinRowCount = 5;
            private const string c_NameColumnName = "Name";
            private const string c_DescColumnName = "Desc";

            private SourceModel(
                IReadOnlyList<SourceUnit> units,
                IReadOnlyList<string> languages)
            {
                Units = units;
                Languages = languages;
            }

            internal IReadOnlyList<SourceUnit> Units { get; }
            internal IReadOnlyList<string> Languages { get; }

            internal static SourceModel Load(
                string sourceDirPath,
                IReadOnlyList<IDataTableUnitSetting> units,
                Func<string, Dictionary<string, List<IReadOnlyList<string>>>> readAllSheets = null)
            {
                if (string.IsNullOrWhiteSpace(sourceDirPath))
                {
                    throw new InvalidDataException("Localization source directory cannot be empty.");
                }

                if (units == null)
                {
                    throw new InvalidDataException("Localization source units cannot be null.");
                }

                string rootPath = IOPath.GetFullPath(sourceDirPath);
                string fileSystemRoot = IOPath.GetPathRoot(rootPath);
                if (rootPath.Length > fileSystemRoot.Length)
                {
                    rootPath = rootPath.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
                }

                string rootPrefix = rootPath.EndsWith(IOPath.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? rootPath
                    : rootPath + IOPath.DirectorySeparatorChar;
                StringComparison pathComparison = IOPath.DirectorySeparatorChar == '\\'
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var validatedUnits = new List<ValidatedUnit>(units.Count);

                foreach (IDataTableUnitSetting setting in units)
                {
                    if (setting == null)
                    {
                        throw new InvalidDataException("Localization source unit cannot be null.");
                    }

                    string sourcePath = NormalizeSourcePath(setting.SourcePath);
                    string platformPath = sourcePath.Replace('/', IOPath.DirectorySeparatorChar);
                    string fullPath = IOPath.GetFullPath(IOPath.Combine(rootPath, platformPath));
                    if (!fullPath.StartsWith(rootPrefix, pathComparison))
                    {
                        throw new InvalidDataException(
                            $"Localization source path must stay inside the source directory: {setting.SourcePath}.");
                    }

                    string canonicalSourcePath = IOPath.GetRelativePath(rootPath, fullPath).Replace('\\', '/');
                    if (!string.Equals(IOPath.GetExtension(canonicalSourcePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"Localization source path must use the .xlsx extension: {setting.SourcePath}.");
                    }

                    ValidateNoReparsePoints(rootPath, canonicalSourcePath);
                    if (!sourcePaths.Add(canonicalSourcePath))
                    {
                        throw new InvalidDataException($"Duplicate Localization source path: {canonicalSourcePath}.");
                    }

                    validatedUnits.Add(new ValidatedUnit(setting, canonicalSourcePath, fullPath));
                }

                readAllSheets ??= EditorUtil.Excel.ReadAllSheets;
                var loadedUnits = new List<SourceUnit>(validatedUnits.Count);
                foreach (ValidatedUnit validated in validatedUnits.OrderBy(unit => unit.SourcePath, StringComparer.Ordinal))
                {
                    Dictionary<string, List<IReadOnlyList<string>>> workbook = readAllSheets(validated.FullPath);
                    var sheets = new List<SourceSheet>();
                    if (workbook != null)
                    {
                        foreach (KeyValuePair<string, List<IReadOnlyList<string>>> entry in workbook)
                        {
                            if (entry.Key.StartsWith("#", StringComparison.Ordinal) ||
                                entry.Value == null || entry.Value.Count < c_MinRowCount)
                            {
                                continue;
                            }

                            SourceSheet sheet = ParseSheet(
                                validated.SourcePath,
                                entry.Key,
                                entry.Value);
                            sheets.Add(sheet);
                        }
                    }

                    if (sheets.Count == 0)
                    {
                        throw new InvalidDataException(
                            $"Localization source '{validated.SourcePath}' contains no valid sheets.");
                    }

                    loadedUnits.Add(new SourceUnit(
                        validated.Setting,
                        validated.SourcePath,
                        GetRelativeStem(validated.SourcePath),
                        sheets));
                }

                IReadOnlyList<string> languages = ValidateGlobalContract(loadedUnits);
                ValidateExportPaths(loadedUnits, languages);
                return new SourceModel(loadedUnits, languages);
            }

            private static string NormalizeSourcePath(string sourcePath)
            {
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    throw new InvalidDataException("Localization source path cannot be empty.");
                }

                string normalized = sourcePath.Replace('\\', '/');
                bool hasDrivePrefix = normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':';
                if (IOPath.IsPathRooted(normalized) || normalized.StartsWith("//", StringComparison.Ordinal) || hasDrivePrefix)
                {
                    throw new InvalidDataException($"Localization source path must be relative: {sourcePath}.");
                }

                return normalized;
            }

            private static SourceSheet ParseSheet(
                string sourcePath,
                string sheetName,
                IReadOnlyList<IReadOnlyList<string>> rows)
            {
                IReadOnlyList<string> varRow = rows[c_VarRowIndex];
                if (varRow == null)
                {
                    throw new InvalidDataException(
                        $"Localization source '{sourcePath}', sheet '{sheetName}' has no ##var row.");
                }

                int nameColumnIndex = -1;
                int descColumnIndex = -1;
                var languageColumnIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 1; i < varRow.Count; i++)
                {
                    string columnName = varRow[i]?.Trim();
                    if (!string.IsNullOrEmpty(columnName) && columnName[0] == '#')
                    {
                        columnName = columnName.Substring(1);
                        if (columnName.StartsWith("#", StringComparison.Ordinal))
                        {
                            throw new InvalidDataException(
                                $"Localization source '{sourcePath}', sheet '{sheetName}' column {i + 1} " +
                                $"has multiple # prefixes: '{varRow[i]}'.");
                        }
                    }

                    if (string.Equals(columnName, c_NameColumnName, StringComparison.Ordinal))
                    {
                        nameColumnIndex = i;
                    }
                    else if (string.Equals(columnName, c_DescColumnName, StringComparison.Ordinal))
                    {
                        descColumnIndex = i;
                    }
                    else if (IsDefinedLanguage(columnName))
                    {
                        if (languageColumnIndexes.ContainsKey(columnName))
                        {
                            throw new InvalidDataException(
                                $"Localization source '{sourcePath}', sheet '{sheetName}' " +
                                $"contains duplicate language column '{columnName}'.");
                        }

                        languageColumnIndexes.Add(columnName, i);
                    }
                }

                if (nameColumnIndex < 0)
                {
                    throw new InvalidDataException(
                        $"Localization source '{sourcePath}', sheet '{sheetName}' is missing the Name column.");
                }

                if (languageColumnIndexes.Count == 0)
                {
                    throw new InvalidDataException(
                        $"Localization source '{sourcePath}', sheet '{sheetName}' has no defined language columns.");
                }

                var keys = new HashSet<string>(StringComparer.Ordinal);
                int validRowCount = 0;
                for (int rowIndex = c_MinRowCount - 1; rowIndex < rows.Count; rowIndex++)
                {
                    IReadOnlyList<string> row = rows[rowIndex];
                    if (row == null || nameColumnIndex >= row.Count)
                    {
                        continue;
                    }

                    string key = row[nameColumnIndex]?.Trim();
                    if (string.IsNullOrEmpty(key) || key.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!keys.Add(key))
                    {
                        throw new InvalidDataException(
                            $"Localization source '{sourcePath}', sheet '{sheetName}' row {rowIndex + 1} " +
                            $"contains duplicate key '{key}'.");
                    }

                    validRowCount++;
                }

                if (validRowCount == 0)
                {
                    throw new InvalidDataException(
                        $"Localization source '{sourcePath}', sheet '{sheetName}' has no valid data rows.");
                }

                return new SourceSheet(
                    sheetName,
                    nameColumnIndex,
                    descColumnIndex,
                    languageColumnIndexes,
                    rows);
            }

            private static bool IsDefinedLanguage(string value)
            {
                if (string.IsNullOrEmpty(value) ||
                    !Enum.TryParse(value, false, out Language parsed) ||
                    !Enum.IsDefined(typeof(Language), parsed))
                {
                    return false;
                }

                return string.Equals(Enum.GetName(typeof(Language), parsed), value, StringComparison.Ordinal);
            }

            private static IReadOnlyList<string> ValidateGlobalContract(
                IReadOnlyList<SourceUnit> units)
            {
                HashSet<string> expected = null;
                foreach (SourceUnit unit in units)
                {
                    foreach (SourceSheet sheet in unit.Sheets)
                    {
                        var actual = new HashSet<string>(
                            sheet.LanguageColumnIndexes.Keys,
                            StringComparer.Ordinal);
                        if (expected == null)
                        {
                            expected = actual;
                            continue;
                        }

                        if (expected.SetEquals(actual))
                        {
                            continue;
                        }

                        string missing = string.Join(", ", expected.Except(actual).OrderBy(x => x, StringComparer.Ordinal));
                        string extra = string.Join(", ", actual.Except(expected).OrderBy(x => x, StringComparer.Ordinal));
                        throw new InvalidDataException(
                            $"Localization source '{unit.SourcePath}', sheet '{sheet.Name}' has inconsistent " +
                            $"language columns. Missing: [{missing}]. Extra: [{extra}].");
                    }
                }

                if (expected == null || expected.Count == 0)
                {
                    throw new InvalidDataException("Localization sources contain no defined languages.");
                }

                return expected.OrderBy(language => language, StringComparer.Ordinal).ToList();
            }

            private static void ValidateExportPaths(
                IReadOnlyList<SourceUnit> units,
                IReadOnlyList<string> languages)
            {
                var expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (SourceUnit unit in units)
                {
                    string template = unit.Setting.DatasExportPath;
                    if (string.IsNullOrEmpty(template) ||
                        template.IndexOf("{0}", StringComparison.Ordinal) < 0)
                    {
                        throw new InvalidDataException(
                            $"Localization source '{unit.SourcePath}' DatasExportPath must contain '{{0}}'.");
                    }

                    foreach (string language in languages)
                    {
                        string expanded = template.Replace("{0}", language);
                        string canonical = IOPath.GetFullPath(expanded).Replace('\\', '/');
                        if (!expandedPaths.Add(canonical))
                        {
                            throw new InvalidDataException(
                                $"Localization data export path collision: '{expanded}'.");
                        }
                    }
                }
            }

            private static string GetRelativeStem(string sourcePath)
            {
                string extension = IOPath.GetExtension(sourcePath);
                return sourcePath.Substring(0, sourcePath.Length - extension.Length);
            }

            private static void ValidateNoReparsePoints(string rootPath, string canonicalSourcePath)
            {
                string currentPath = rootPath;
                string[] segments = canonicalSourcePath.Split('/');
                foreach (string segment in segments)
                {
                    currentPath = IOPath.Combine(currentPath, segment);
                    try
                    {
                        if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
                        {
                            throw new InvalidDataException(
                                $"Localization source path cannot contain a symbolic link or reparse point: {canonicalSourcePath}.");
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        break;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        break;
                    }
                }
            }

            private sealed class ValidatedUnit
            {
                internal ValidatedUnit(IDataTableUnitSetting setting, string sourcePath, string fullPath)
                {
                    Setting = setting;
                    SourcePath = sourcePath;
                    FullPath = fullPath;
                }

                internal IDataTableUnitSetting Setting { get; }
                internal string SourcePath { get; }
                internal string FullPath { get; }
            }
        }

        /// <summary>
        /// 单个配置源文件快照。
        /// </summary>
        internal sealed class SourceUnit
        {
            internal SourceUnit(
                IDataTableUnitSetting setting,
                string sourcePath,
                string relativeStem,
                IReadOnlyList<SourceSheet> sheets)
            {
                Setting = setting;
                SourcePath = sourcePath;
                RelativeStem = relativeStem;
                Sheets = sheets;
            }

            internal IDataTableUnitSetting Setting { get; }
            internal string SourcePath { get; }
            internal string RelativeStem { get; }
            internal IReadOnlyList<SourceSheet> Sheets { get; }
        }

        /// <summary>
        /// 单个 Localization Sheet 快照。
        /// </summary>
        internal sealed class SourceSheet
        {
            internal SourceSheet(
                string name,
                int nameColumnIndex,
                int descColumnIndex,
                IReadOnlyDictionary<string, int> languageColumnIndexes,
                IReadOnlyList<IReadOnlyList<string>> rows)
            {
                Name = name;
                NameColumnIndex = nameColumnIndex;
                DescColumnIndex = descColumnIndex;
                LanguageColumnIndexes = languageColumnIndexes;
                Rows = rows;
            }

            internal string Name { get; }
            internal int NameColumnIndex { get; }
            internal int DescColumnIndex { get; }
            internal IReadOnlyDictionary<string, int> LanguageColumnIndexes { get; }
            internal IReadOnlyList<IReadOnlyList<string>> Rows { get; }
        }
    }
}
