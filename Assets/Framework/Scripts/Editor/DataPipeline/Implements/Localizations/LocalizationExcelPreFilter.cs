/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationExcelPreFilter.cs
 * author:    taoye
 * created:   2026/4/19
 * descrip:   Localization 模块 Excel 预过滤器
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Localization 模块 Excel 预过滤器。
    /// 读取多语言文本 Excel，为每种语言生成只含 Name + Value 两列的临时 Luban 格式 Excel，
    /// 供后续 Luban Pipeline 标准导出使用。
    /// </summary>
    internal static class LocalizationExcelPreFilter
    {
        /// <summary>
        /// 源文件搜索模式。
        /// </summary>
        private const string c_SearchPattern = "*.xlsx";

        /// <summary>
        /// 需要排除的临时文件前缀。
        /// </summary>
        private const string c_ExcludePrefix = "~$";

        /// <summary>
        /// _configs 子目录名称（需跳过）。
        /// </summary>
        private const string c_ConfigsDirName = "_configs";

        /// <summary>
        /// _temp 临时目录名称（需跳过）。
        /// </summary>
        private const string c_TempDirName = "_temp";

        /// <summary>
        /// Luban 4 行标记格式中变量名行索引（##var）。
        /// </summary>
        private const int c_VarRow = 1;

        /// <summary>
        /// Luban 4 行标记格式中数据行起始索引。
        /// </summary>
        private const int c_DataStartRow = 4;

        /// <summary>
        /// Sheet 最少行数（4 行表头 + 至少 1 行数据）。
        /// </summary>
        private const int c_MinRowCount = 5;

        /// <summary>
        /// Key 列名称。
        /// </summary>
        private const string c_KeyColumnName = "Name";

        /// <summary>
        /// 描述列名称。
        /// </summary>
        private const string c_DescColumnName = "Desc";

        /// <summary>
        /// 标记列名称前缀。
        /// </summary>
        private const string c_MarkerColumnPrefix = "#";

        /// <summary>
        /// 为代码生成准备临时 Excel：取第一种语言列的值作为 Value。
        /// 扫描 sourceDirPath 下所有 Excel（排除 _configs/ 和 _temp/），
        /// 对每个 Sheet 取第一种语言列，输出 Name + Value 两列 Luban 格式到 codegenTempDir。
        /// </summary>
        /// <param name="sourceDirPath">文本数据源目录路径。</param>
        /// <param name="codegenTempDir">代码生成临时输出目录。</param>
        public static void FilterForCodeGen(string sourceDirPath, string codegenTempDir)
        {
            string[] files = GetFilterableFiles(sourceDirPath);
            if (files == null || files.Length == 0)
            {
                return;
            }

            foreach (string filePath in files)
            {
                if (IsExcludedPath(filePath, sourceDirPath))
                {
                    continue;
                }

                FilterFileForCodeGen(filePath, codegenTempDir);
            }
        }

        /// <summary>
        /// 为指定语言准备临时 Excel：取该语言列的值作为 Value。
        /// 扫描 sourceDirPath 下所有 Excel（排除 _configs/ 和 _temp/），
        /// 对每个 Sheet 取指定语言列，输出 Name + Value 两列 Luban 格式到 langTempDir。
        /// </summary>
        /// <param name="sourceDirPath">文本数据源目录路径。</param>
        /// <param name="langTempDir">语言临时输出目录。</param>
        /// <param name="languageName">目标语言名称（如 "ChineseSimplified"）。</param>
        public static void FilterForLanguage(string sourceDirPath, string langTempDir, string languageName)
        {
            if (string.IsNullOrEmpty(languageName))
            {
                Log.Warning(LogTag.Editor, "Localization 预过滤：语言名称为空，已跳过。");
                return;
            }

            string[] files = GetFilterableFiles(sourceDirPath);
            if (files == null || files.Length == 0)
            {
                return;
            }

            foreach (string filePath in files)
            {
                if (IsExcludedPath(filePath, sourceDirPath))
                {
                    continue;
                }

                FilterFileForLanguage(filePath, langTempDir, languageName);
            }
        }

        /// <summary>
        /// 从目录下所有 Excel 文件中提取所有语言列名称。
        /// 扫描所有 Excel 的 ##var 行，收集能通过 Language 枚举解析的列名。
        /// </summary>
        /// <param name="sourceDirPath">文本数据源目录路径。</param>
        /// <returns>语言列名称集合，无有效语言时返回空集合。</returns>
        public static HashSet<string> ExtractAllLanguageColumns(string sourceDirPath)
        {
            HashSet<string> result = new HashSet<string>();

            string[] files = GetFilterableFiles(sourceDirPath);
            if (files == null || files.Length == 0)
            {
                return result;
            }

            foreach (string filePath in files)
            {
                if (IsExcludedPath(filePath, sourceDirPath))
                {
                    continue;
                }

                ExtractLanguageColumnsFromFile(filePath, result);
            }

            return result;
        }

        /// <summary>
        /// 获取目录下可过滤的 Excel 文件列表。
        /// </summary>
        /// <param name="sourceDirPath">数据源目录路径。</param>
        /// <returns>文件路径数组，目录无效时返回 null。</returns>
        private static string[] GetFilterableFiles(string sourceDirPath)
        {
            if (string.IsNullOrEmpty(sourceDirPath) || !Util.SysIO.Directory.Exists(sourceDirPath))
            {
                Log.Warning(LogTag.Editor, "Localization 预过滤：数据源目录不存在或为空：{0}", sourceDirPath ?? "null");
                return null;
            }

            string[] files = EditorUtil.FileSystem.GetFiles(sourceDirPath, c_SearchPattern, c_ExcludePrefix);
            if (files == null || files.Length == 0)
            {
                Log.Debug(LogTag.Editor, "Localization 预过滤：数据源目录下未找到 .xlsx 文件：{0}", sourceDirPath);
                return null;
            }

            return files;
        }

        /// <summary>
        /// 判断文件路径是否在 _configs/ 或 _temp/ 子目录中（需跳过）。
        /// </summary>
        /// <param name="filePath">文件完整路径。</param>
        /// <param name="sourceDirPath">数据源目录路径。</param>
        /// <returns>是否应排除。</returns>
        private static bool IsExcludedPath(string filePath, string sourceDirPath)
        {
            string normalizedPath = filePath.Replace('\\', '/');
            string configsDirPath = (Util.SysIO.Path.Combine(sourceDirPath, c_ConfigsDirName) + Util.SysIO.Path.DirectorySeparatorChar).Replace('\\', '/');
            string tempDirPath = (Util.SysIO.Path.Combine(sourceDirPath, c_TempDirName) + Util.SysIO.Path.DirectorySeparatorChar).Replace('\\', '/');

            return normalizedPath.StartsWith(configsDirPath) || normalizedPath.StartsWith(tempDirPath);
        }

        /// <summary>
        /// 对单个 Excel 文件执行代码生成预过滤：取第一种语言列的值作为 Value。
        /// </summary>
        /// <param name="excelFilePath">Excel 文件完整路径。</param>
        /// <param name="codegenTempDir">代码生成临时输出目录。</param>
        private static void FilterFileForCodeGen(string excelFilePath, string codegenTempDir)
        {
            Dictionary<string, List<IReadOnlyList<string>>> allSheets = ReadValidSheets(excelFilePath);
            if (allSheets == null || allSheets.Count == 0)
            {
                return;
            }

            var outputSheets = new Dictionary<string, List<IReadOnlyList<string>>>();

            foreach (var kvp in allSheets)
            {
                string sheetName = kvp.Key;
                List<IReadOnlyList<string>> rows = kvp.Value;

                ColumnLayout layout = ParseColumnLayout(rows[c_VarRow]);
                if (layout.KeyIndex < 0 || layout.LanguageColumns.Count == 0)
                {
                    continue;
                }

                int firstLangIndex = layout.LanguageColumns[0].Index;
                List<IReadOnlyList<string>> filtered = BuildFilteredSheet(sheetName, rows, layout.KeyIndex, firstLangIndex);
                if (filtered != null)
                {
                    outputSheets[sheetName] = filtered;
                }
            }

            WriteOutputExcel(excelFilePath, codegenTempDir, outputSheets);
        }

        /// <summary>
        /// 对单个 Excel 文件执行语言预过滤：取指定语言列的值作为 Value。
        /// </summary>
        /// <param name="excelFilePath">Excel 文件完整路径。</param>
        /// <param name="langTempDir">语言临时输出目录。</param>
        /// <param name="languageName">目标语言名称。</param>
        private static void FilterFileForLanguage(string excelFilePath, string langTempDir, string languageName)
        {
            Dictionary<string, List<IReadOnlyList<string>>> allSheets = ReadValidSheets(excelFilePath);
            if (allSheets == null || allSheets.Count == 0)
            {
                return;
            }

            var outputSheets = new Dictionary<string, List<IReadOnlyList<string>>>();

            foreach (var kvp in allSheets)
            {
                string sheetName = kvp.Key;
                List<IReadOnlyList<string>> rows = kvp.Value;

                ColumnLayout layout = ParseColumnLayout(rows[c_VarRow]);
                if (layout.KeyIndex < 0)
                {
                    continue;
                }

                int langIndex = FindLanguageColumnIndex(layout.LanguageColumns, languageName);
                if (langIndex < 0)
                {
                    continue;
                }

                List<IReadOnlyList<string>> filtered = BuildFilteredSheet(sheetName, rows, layout.KeyIndex, langIndex);
                if (filtered != null)
                {
                    outputSheets[sheetName] = filtered;
                }
            }

            WriteOutputExcel(excelFilePath, langTempDir, outputSheets);
        }

        /// <summary>
        /// 读取 Excel 文件中所有有效的 Sheet（跳过 # 开头和行数不足的 Sheet）。
        /// </summary>
        /// <param name="excelFilePath">Excel 文件完整路径。</param>
        /// <returns>有效 Sheet 字典，读取失败时返回 null。</returns>
        private static Dictionary<string, List<IReadOnlyList<string>>> ReadValidSheets(string excelFilePath)
        {
            if (string.IsNullOrEmpty(excelFilePath) || !Util.SysIO.File.Exists(excelFilePath))
            {
                return null;
            }

            Dictionary<string, List<IReadOnlyList<string>>> allSheets;
            try
            {
                allSheets = EditorUtil.Excel.ReadAllSheets(excelFilePath);
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Editor, "Localization 预过滤：读取 Excel 失败：{0}，异常：{1}", excelFilePath, e.Message);
                return null;
            }

            if (allSheets == null || allSheets.Count == 0)
            {
                return null;
            }

            var validSheets = new Dictionary<string, List<IReadOnlyList<string>>>();
            foreach (var kvp in allSheets)
            {
                if (kvp.Key.StartsWith(c_MarkerColumnPrefix))
                {
                    continue;
                }

                if (kvp.Value == null || kvp.Value.Count < c_MinRowCount)
                {
                    continue;
                }

                validSheets[kvp.Key] = kvp.Value;
            }

            return validSheets.Count > 0 ? validSheets : null;
        }

        /// <summary>
        /// Excel 列布局信息。
        /// </summary>
        private struct ColumnLayout
        {
            /// <summary>
            /// Name（Key）列索引，未找到时为 -1。
            /// </summary>
            public int KeyIndex;

            /// <summary>
            /// 语言列索引与名称列表。
            /// </summary>
            public List<(int Index, string Name)> LanguageColumns;
        }

        /// <summary>
        /// 从 ##var 行解析列布局，识别 Key 列和所有语言列。
        /// </summary>
        /// <param name="varRow">##var 行数据。</param>
        /// <returns>列布局信息。</returns>
        private static ColumnLayout ParseColumnLayout(IReadOnlyList<string> varRow)
        {
            var layout = new ColumnLayout { KeyIndex = -1, LanguageColumns = new List<(int, string)>() };

            if (varRow == null || varRow.Count < 2)
            {
                return layout;
            }

            for (int col = 0; col < varRow.Count; col++)
            {
                string columnName = varRow[col]?.Trim();
                if (string.IsNullOrEmpty(columnName))
                {
                    continue;
                }

                // 去掉 Luban 的 # 前缀（如 #ChineseSimplified → ChineseSimplified）
                string pureName = columnName.TrimStart('#');

                if (pureName == c_KeyColumnName)
                {
                    layout.KeyIndex = col;
                }
                else if (pureName == c_DescColumnName)
                {
                    continue;
                }
                else if (Enum.TryParse<Language>(pureName, out _))
                {
                    // LanguageColumns 存纯名称，Index 指向原列位置
                    layout.LanguageColumns.Add((col, pureName));
                }
            }

            return layout;
        }

        /// <summary>
        /// 在语言列列表中查找指定语言名称对应的列索引。
        /// </summary>
        /// <param name="languageColumns">语言列列表。</param>
        /// <param name="languageName">目标语言名称。</param>
        /// <returns>列索引，未找到时返回 -1。</returns>
        private static int FindLanguageColumnIndex(List<(int Index, string Name)> languageColumns, string languageName)
        {
            for (int i = 0; i < languageColumns.Count; i++)
            {
                if (languageColumns[i].Name == languageName)
                {
                    return languageColumns[i].Index;
                }
            }

            return -1;
        }

        /// <summary>
        /// 构建过滤后的 Sheet 数据：4 行 Luban 标记表头 + Name/Value 数据行。
        /// </summary>
        /// <param name="sheetName">Sheet 名称。</param>
        /// <param name="rows">原始行数据。</param>
        /// <param name="keyIndex">Name 列索引。</param>
        /// <param name="valueIndex">Value 来源列索引（语言列）。</param>
        /// <returns>过滤后的行数据，无有效数据时返回 null。</returns>
        private static List<IReadOnlyList<string>> BuildFilteredSheet(string sheetName, List<IReadOnlyList<string>> rows, int keyIndex, int valueIndex)
        {
            var dataRows = new List<IReadOnlyList<string>>();

            for (int i = c_DataStartRow; i < rows.Count; i++)
            {
                IReadOnlyList<string> row = rows[i];
                if (row == null || row.Count == 0)
                {
                    continue;
                }

                if (row.Count > 0 && row[0] == c_MarkerColumnPrefix)
                {
                    continue;
                }

                if (keyIndex >= row.Count)
                {
                    continue;
                }

                string name = row[keyIndex]?.Trim();
                if (string.IsNullOrEmpty(name) || name.StartsWith(c_MarkerColumnPrefix))
                {
                    continue;
                }

                string value = valueIndex < row.Count ? row[valueIndex] ?? string.Empty : string.Empty;
                dataRows.Add(new List<string> { "", name, value });
            }

            if (dataRows.Count == 0)
            {
                return null;
            }

            var outputRows = new List<IReadOnlyList<string>>
            {
                new List<string> { "##comment", sheetName },
                new List<string> { "##var", "Name", "Value" },
                new List<string> { "##type", "string", "string" },
                new List<string> { "##comment", "键名", "值" }
            };
            outputRows.AddRange(dataRows);

            return outputRows;
        }

        /// <summary>
        /// 将过滤后的 Sheet 数据写入临时 CSV 文件目录。
        /// </summary>
        /// <param name="sourceFilePath">原始 Excel 文件路径（用于提取文件名）。</param>
        /// <param name="tempDir">临时输出目录。</param>
        /// <param name="outputSheets">过滤后的 Sheet 数据。</param>
        private static void WriteOutputExcel(string sourceFilePath, string tempDir, Dictionary<string, List<IReadOnlyList<string>>> outputSheets)
        {
            if (outputSheets == null || outputSheets.Count == 0)
            {
                return;
            }

            if (!Util.SysIO.Directory.Exists(tempDir))
            {
                Util.SysIO.Directory.CreateIfNotExist(tempDir);
            }

            string fileNameWithoutExt = Util.SysIO.Path.GetFileNameWithoutExtension(sourceFilePath);
            string outputDir = Util.SysIO.Path.Combine(tempDir, fileNameWithoutExt);
            EditorUtil.Excel.Write(outputDir, outputSheets);
        }

        /// <summary>
        /// 从单个 Excel 文件中提取所有语言列名称到结果集合。
        /// </summary>
        /// <param name="excelFilePath">Excel 文件完整路径。</param>
        /// <param name="result">语言名称结果集合（追加写入）。</param>
        private static void ExtractLanguageColumnsFromFile(string excelFilePath, HashSet<string> result)
        {
            Dictionary<string, List<IReadOnlyList<string>>> allSheets = ReadValidSheets(excelFilePath);
            if (allSheets == null)
            {
                return;
            }

            foreach (var kvp in allSheets)
            {
                List<IReadOnlyList<string>> rows = kvp.Value;
                if (rows.Count < c_MinRowCount)
                {
                    continue;
                }

                ColumnLayout layout = ParseColumnLayout(rows[c_VarRow]);
                for (int i = 0; i < layout.LanguageColumns.Count; i++)
                {
                    result.Add(layout.LanguageColumns[i].Name);
                }
            }
        }
    }
}
