/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.CsvExport.cs
 * author:    taoye
 * created:   2026/1/13
 * descrip:   编辑器CSV导出工具
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// CSV 导出工具。
        /// </summary>
        public static class CsvExporter
        {
            /// <summary>
            /// 导出 CSV 文件。
            /// </summary>
            /// <param name="title">标题。</param>
            /// <param name="header">表头。</param>
            /// <param name="rows">行数据。</param>
            /// <returns>导出路径。</returns>
            public static string Export(string title, string header, IEnumerable<string> rows)
            {
                string path = EditorUtility.SaveFilePanel("导出 CSV 数据", string.Empty, $"{title} {DateTime.Now:yyyy-MM-dd HH-mm-ss}.csv", "csv");

                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                try
                {
                    using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
                    {
                        writer.WriteLine(header);
                        foreach (string row in rows)
                        {
                            writer.WriteLine(row);
                        }
                    }
                    Log.Debug(LogTag.Editor, "导出 CSV 数据到 '{0}' 成功。", path);
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.Editor, "导出 CSV 数据失败：{0}", e.Message);
                }
                
                return path;
            }
            /// <summary>
            /// 按 RFC 4180 规范转义 CSV 字段。
            /// 若字段包含逗号、双引号或换行符，用双引号包裹，并将内部双引号转义为两个双引号。
            /// </summary>
            /// <param name="field">原始字段值。</param>
            /// <returns>转义后的字段。</returns>
            public static string EscapeField(string field)
            {
                if (string.IsNullOrEmpty(field))
                {
                    return field ?? string.Empty;
                }

                if (field.IndexOfAny(s_CsvSpecialChars) >= 0)
                {
                    return "\"" + field.Replace("\"", "\"\"") + "\"";
                }

                return field;
            }

            /// <summary>
            /// CSV 需要转义的特殊字符。
            /// </summary>
            private static readonly char[] s_CsvSpecialChars = { ',', '"', '\n', '\r' };
        }
    }
}
