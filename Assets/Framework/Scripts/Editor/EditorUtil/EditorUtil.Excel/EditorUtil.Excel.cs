/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Excel.cs
 * author:    taoye
 * created:   2026/4/15
 * descrip:   编辑器 Excel 读写工具
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using ExcelDataReader;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Excel 读写工具（CSV 写入 + ExcelDataReader 读取）。
        /// </summary>
        public static class Excel
        {
            /// <summary>
            /// 有效的 Excel 文件扩展名集合。
            /// </summary>
            private static readonly HashSet<string> s_ValidExtensions = new(StringComparer.OrdinalIgnoreCase) { ".xlsx", ".xls" };

            /// <summary>
            /// Excel 源文件搜索模式。
            /// </summary>
            public const string c_SearchPattern = "*.xlsx";

            /// <summary>
            /// 需要排除的 Excel 临时文件前缀。
            /// </summary>
            public const string c_ExcludePrefix = "~$";

            /// <summary>
            /// 读取 Excel 文件所有 Sheet 数据。
            /// </summary>
            /// <param name="filePath">Excel 文件绝对路径。</param>
            /// <returns>字典：Sheet 名 → 行列数据。</returns>
            public static Dictionary<string, List<IReadOnlyList<string>>> ReadAllSheets(string filePath)
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentException("Excel 文件路径为空。");
                }

                if (!Util.SysIO.File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Excel 文件不存在：{filePath}");
                }

                var result = new Dictionary<string, List<IReadOnlyList<string>>>();

                using var stream = Util.SysIO.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = false
                    }
                });

                foreach (DataTable table in dataSet.Tables)
                {
                    if (table == null || table.Rows.Count == 0 || string.IsNullOrWhiteSpace(table.TableName))
                    {
                        continue;
                    }

                    result[table.TableName] = ConvertTableToRows(table);
                }

                return result;
            }

            /// <summary>
            /// 读取 Excel 文件指定 Sheet 数据。
            /// </summary>
            /// <param name="filePath">Excel 文件绝对路径。</param>
            /// <param name="sheetName">Sheet 名称。</param>
            /// <returns>行列数据。</returns>
            public static List<IReadOnlyList<string>> ReadSheet(string filePath, string sheetName)
            {
                if (string.IsNullOrEmpty(sheetName))
                {
                    throw new ArgumentException("Sheet 名称为空。");
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentException("Excel 文件路径为空。");
                }

                if (!Util.SysIO.File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Excel 文件不存在：{filePath}");
                }

                using var stream = Util.SysIO.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                do
                {
                    if (string.Equals(reader.Name, sheetName, StringComparison.Ordinal))
                    {
                        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                        {
                            FilterSheet = (tableReader, sheetIndex) => string.Equals(tableReader.Name, sheetName, StringComparison.Ordinal),
                            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
                        });

                        DataTable table = dataSet.Tables[sheetName];
                        if (table != null && table.Rows.Count > 0)
                        {
                            return ConvertTableToRows(table);
                        }

                        throw new KeyNotFoundException($"Sheet '{sheetName}' 在文件 '{filePath}' 中无数据。");
                    }
                } while (reader.NextResult());

                throw new KeyNotFoundException($"Sheet '{sheetName}' 在文件 '{filePath}' 中不存在。");
            }

            /// <summary>
            /// 获取 Excel 文件的所有 Sheet 名称。
            /// </summary>
            /// <param name="filePath">Excel 文件绝对路径。</param>
            /// <returns>Sheet 名称列表。</returns>
            public static List<string> GetSheetNames(string filePath)
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentException("Excel 文件路径为空。");
                }

                if (!Util.SysIO.File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Excel 文件不存在：{filePath}");
                }

                var names = new List<string>();

                using var stream = Util.SysIO.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                do
                {
                    if (!string.IsNullOrWhiteSpace(reader.Name))
                    {
                        names.Add(reader.Name);
                    }
                } while (reader.NextResult());

                return names;
            }

            /// <summary>
            /// 将多个 Sheet 数据分别写入 CSV 文件到指定目录。
            /// 每个 Sheet 生成一个 {dirPath}/{sheetName}.csv 文件。
            /// </summary>
            /// <param name="dirPath">输出目录绝对路径。</param>
            /// <param name="sheets">字典：Sheet 名 → 行列数据。</param>
            public static void Write(string dirPath, Dictionary<string, List<IReadOnlyList<string>>> sheets)
            {
                if (string.IsNullOrEmpty(dirPath))
                {
                    throw new ArgumentException("输出目录路径为空。");
                }

                if (sheets == null || sheets.Count == 0)
                {
                    throw new ArgumentException("写入数据为空。");
                }

                Util.SysIO.Directory.CreateIfNotExist(dirPath);

                foreach (var kvp in sheets)
                {
                    string csvPath = Util.SysIO.Path.Combine(dirPath, kvp.Key + ".csv");
                    WriteCsvFile(csvPath, kvp.Value);
                }
            }

            /// <summary>
            /// 将单个 Sheet 数据写入 CSV 文件到指定目录。
            /// </summary>
            /// <param name="dirPath">输出目录绝对路径。</param>
            /// <param name="sheetName">Sheet 名称（用作文件名）。</param>
            /// <param name="rows">行列数据。</param>
            public static void Write(string dirPath, string sheetName, List<IReadOnlyList<string>> rows)
            {
                if (string.IsNullOrEmpty(sheetName))
                {
                    throw new ArgumentException("Sheet 名称为空。");
                }

                Write(dirPath, new Dictionary<string, List<IReadOnlyList<string>>>
                {
                    { sheetName, rows }
                });
            }

            /// <summary>
            /// 检查文件是否为有效 Excel 格式（.xlsx/.xls）。
            /// </summary>
            /// <param name="filePath">文件路径。</param>
            /// <returns>是有效 Excel 文件返回 true，否则返回 false。</returns>
            public static bool IsExcelFile(string filePath)
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return false;
                }

                string extension = Util.SysIO.Path.GetExtension(filePath);
                return s_ValidExtensions.Contains(extension);
            }

            /// <summary>
            /// 将 DataTable 转换为行列字符串数据。
            /// </summary>
            /// <param name="table">数据表。</param>
            /// <returns>行列数据。</returns>
            private static List<IReadOnlyList<string>> ConvertTableToRows(DataTable table)
            {
                var rows = new List<IReadOnlyList<string>>(table.Rows.Count);

                foreach (DataRow row in table.Rows)
                {
                    var columns = new List<string>(table.Columns.Count);

                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        var cell = row[i];
                        columns.Add(cell?.ToString() ?? string.Empty);
                    }

                    rows.Add(columns);
                }

                return rows;
            }

            /// <summary>
            /// 将行列数据写入单个 CSV 文件（RFC 4180 标准）。
            /// </summary>
            /// <param name="filePath">CSV 文件绝对路径。</param>
            /// <param name="rows">行列数据。</param>
            private static void WriteCsvFile(string filePath, List<IReadOnlyList<string>> rows)
            {
                var sb = new StringBuilder();

                foreach (var row in rows)
                {
                    for (int i = 0; i < row.Count; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append(',');
                        }

                        string cell = row[i] ?? string.Empty;

                        if (cell.Contains(',') || cell.Contains('"') || cell.Contains('\n') || cell.Contains('\r'))
                        {
                            sb.Append('"');
                            sb.Append(cell.Replace("\"", "\"\""));
                            sb.Append('"');
                        }
                        else
                        {
                            sb.Append(cell);
                        }
                    }

                    sb.AppendLine();
                }

                Util.SysIO.File.WriteAllTextSync(filePath, sb.ToString(), Encoding.UTF8);
            }
        }
    }
}
