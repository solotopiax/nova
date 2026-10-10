/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.TrackRegistry.XlsxWriter.cs
 * author:    taoye
 * created:   2026/7/21
 * descrip:   打点 Excel 汇总表 OpenXML 写入器
 ***************************************************************/

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class TrackRegistry
        {
            /// <summary>
            /// 负责把汇总后的 Sheet 数据写成最小 xlsx 包结构。
            /// </summary>
            private static class XlsxWriter
            {
                /// <summary>
                /// 写入完整的 xlsx 文件。
                /// </summary>
                /// <param name="outputPath">输出 xlsx 的绝对路径。</param>
                /// <param name="sheets">需要写入工作簿的 Sheet 数据。</param>
                public static void Write(string outputPath, IReadOnlyList<TrackSheet> sheets)
                {
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }

                    using ZipArchive archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
                    WriteEntry(archive, "_rels/.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                        "</Relationships>");

                    var workbookSheets = new List<string>(sheets.Count);
                    var rels = new List<string>(sheets.Count + 1);
                    var contentOverrides = new List<string>(sheets.Count + 2)
                    {
                        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>",
                        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
                    };

                    for (int i = 0; i < sheets.Count; i++)
                    {
                        int sheetId = i + 1;
                        workbookSheets.Add(Txt.Format("<sheet name=\"{0}\" sheetId=\"{1}\" r:id=\"rId{1}\"/>", Escape(sheets[i].Name), sheetId));
                        rels.Add(Txt.Format("<Relationship Id=\"rId{0}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{0}.xml\"/>", sheetId));
                        contentOverrides.Add(Txt.Format("<Override PartName=\"/xl/worksheets/sheet{0}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>", sheetId));
                        WriteEntry(archive, Txt.Format("xl/worksheets/sheet{0}.xml", sheetId), BuildSheetXml(sheets[i]));
                    }

                    string styleRelId = Txt.Format("rId{0}", sheets.Count + 1);
                    WriteEntry(archive, "[Content_Types].xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                        string.Concat(contentOverrides) +
                        "</Types>");
                    WriteEntry(archive, "xl/workbook.xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                        Txt.Format("<sheets>{0}</sheets></workbook>", string.Concat(workbookSheets)));
                    WriteEntry(archive, "xl/_rels/workbook.xml.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        string.Concat(rels) +
                        Txt.Format("<Relationship Id=\"{0}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>", styleRelId) +
                        "</Relationships>");
                    WriteEntry(archive, "xl/styles.xml", StylesXml());
                }

                /// <summary>
                /// 构建单个 Sheet 的 OpenXML 文本。
                /// </summary>
                /// <param name="sheet">待写入的 Sheet 数据。</param>
                /// <returns>worksheet xml 内容。</returns>
                private static string BuildSheetXml(TrackSheet sheet)
                {
                    List<double> widths = ColumnWidths(sheet);
                    var rowsXml = new List<string>(sheet.Rows.Count);
                    List<string> mergeRanges = BuildMergeRanges(sheet);
                    bool eventSheet = sheet.Rows.Count > 0 && CellValue(sheet.Rows[0], 0) == "事件名";
                    bool titleRow = !eventSheet && sheet.Rows.Count > 0 && string.IsNullOrEmpty(CellValue(sheet.Rows[0], 1));
                    int frozenRows = titleRow ? 2 : 1;
                    for (int rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
                    {
                        IReadOnlyList<string> row = sheet.Rows[rowIndex];
                        var cells = new List<string>(widths.Count);
                        string rowAttr = Txt.Format(" r=\"{0}\"", rowIndex + 1);
                        double height = RowHeight(sheet, rowIndex, row, widths);
                        if (height != 16.8d)
                        {
                            rowAttr += Txt.Format(" ht=\"{0}\" customHeight=\"1\"", height.ToString("0.##", CultureInfo.InvariantCulture));
                        }

                        for (int columnIndex = 0; columnIndex < widths.Count; columnIndex++)
                        {
                            int style = CellStyle(sheet, rowIndex, columnIndex, row);
                            string styleAttr = style > 0 ? Txt.Format(" s=\"{0}\"", style) : string.Empty;
                            string reference = ColumnName(columnIndex + 1) + (rowIndex + 1).ToString(CultureInfo.InvariantCulture);
                            cells.Add(Txt.Format("<c r=\"{0}\"{1} t=\"inlineStr\"><is><t>{2}</t></is></c>", reference, styleAttr, Escape(CellValue(row, columnIndex))));
                        }

                        rowsXml.Add(Txt.Format("<row{0}>{1}</row>", rowAttr, string.Concat(cells)));
                    }

                    return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                           "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                           Txt.Format("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"{0}\" topLeftCell=\"A{1}\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>", frozenRows, frozenRows + 1) +
                           "<sheetFormatPr baseColWidth=\"8\" defaultColWidth=\"9\" defaultRowHeight=\"16.8\"/>" +
                           BuildColsXml(widths) +
                           Txt.Format("<sheetData>{0}</sheetData>", string.Concat(rowsXml)) +
                           Txt.Format("<autoFilter ref=\"A{0}:{1}{2}\"/>", frozenRows, ColumnName(widths.Count), sheet.Rows.Count) +
                           BuildMergeCellsXml(mergeRanges) +
                           "</worksheet>";
                }

                /// <summary>
                /// 按 Network 模板设置事件列宽，说明页保留其字段结构。
                /// </summary>
                /// <param name="sheet">待计算列宽的 Sheet 数据。</param>
                /// <returns>按列顺序排列的宽度列表。</returns>
                private static List<double> ColumnWidths(TrackSheet sheet)
                {
                    int count = MaxColumnCount(sheet.Rows);
                    bool eventSheet = sheet.Rows.Count > 0 && CellValue(sheet.Rows[0], 0) == "事件名";
                    double[] networkWidths = { 28d, 18d, 36d, 40d, 14d, 58d, 32d, 16d, 48d };
                    var widths = new List<double>(count);
                    for (int columnIndex = 0; columnIndex < count; columnIndex++)
                    {
                        widths.Add(eventSheet && columnIndex < networkWidths.Length
                            ? networkWidths[columnIndex]
                            : columnIndex == count - 1 ? 58d : 32d);
                    }

                    return widths;
                }

                /// <summary>
                /// 构建列宽定义的 OpenXML 文本。
                /// </summary>
                /// <param name="widths">按列顺序排列的宽度列表。</param>
                /// <returns>cols xml 内容。</returns>
                private static string BuildColsXml(IReadOnlyList<double> widths)
                {
                    var cols = new List<string>(widths.Count);
                    for (int i = 0; i < widths.Count; i++)
                    {
                        string width = widths[i].ToString("0.##", CultureInfo.InvariantCulture);
                        cols.Add(Txt.Format("<col min=\"{0}\" max=\"{0}\" width=\"{1}\" customWidth=\"1\"/>", i + 1, width));
                    }

                    return Txt.Format("<cols>{0}</cols>", string.Concat(cols));
                }

                /// <summary>
                /// 沿用 Network 模板的首行与默认数据行高度。
                /// </summary>
                /// <param name="sheet">当前 Sheet 数据。</param>
                /// <param name="rowIndex">当前行索引。</param>
                /// <param name="row">当前行内容。</param>
                /// <param name="widths">按列顺序排列的宽度列表。</param>
                /// <returns>写入 OpenXML 的行高。</returns>
                private static double RowHeight(TrackSheet sheet, int rowIndex, IReadOnlyList<string> row, IReadOnlyList<double> widths)
                {
                    return rowIndex == 0 ? 17d : 16.8d;
                }

                /// <summary>
                /// 选择当前单元格使用的样式索引。
                /// </summary>
                /// <param name="sheet">当前 Sheet 数据。</param>
                /// <param name="rowIndex">当前行索引。</param>
                /// <param name="columnIndex">当前列索引。</param>
                /// <param name="row">当前行内容。</param>
                /// <returns>styles.xml 中的 cellXfs 样式索引。</returns>
                private static int CellStyle(TrackSheet sheet, int rowIndex, int columnIndex, IReadOnlyList<string> row)
                {
                    bool eventSheet = CellValue(sheet.Rows[0], 0) == "事件名";
                    bool titleRow = !eventSheet && string.IsNullOrEmpty(CellValue(sheet.Rows[0], 1));
                    if (rowIndex == 0 || (titleRow && rowIndex == 1))
                    {
                        return 2;
                    }

                    if (eventSheet && columnIndex < 3 && !string.IsNullOrEmpty(CellValue(row, columnIndex)))
                    {
                        return columnIndex == 2 ? 4 : 3;
                    }

                    return 1;
                }

                /// <summary>
                /// 按 Network 模板构建事件页 A～C 列的分组合并范围。
                /// </summary>
                /// <param name="sheet">当前 Sheet 数据。</param>
                /// <returns>Excel 单元格范围列表，例如 A2:A4。</returns>
                private static List<string> BuildMergeRanges(TrackSheet sheet)
                {
                    var ranges = new List<string>();
                    if (sheet.Rows.Count == 0 || CellValue(sheet.Rows[0], 0) != "事件名")
                    {
                        return ranges;
                    }

                    // 以事件为边界，只合并 A～C；G～I 保持逐行单元格，避免跨事件吞并说明。
                    for (int start = 1; start < sheet.Rows.Count;)
                    {
                        int end = start + 1;
                        while (end < sheet.Rows.Count && string.IsNullOrEmpty(CellValue(sheet.Rows[end], 0)))
                        {
                            end++;
                        }

                        for (int columnIndex = 0; columnIndex < 3; columnIndex++)
                        {
                            int anchor = start;
                            for (int rowIndex = start + 1; rowIndex <= end; rowIndex++)
                            {
                                if (rowIndex < end && string.IsNullOrEmpty(CellValue(sheet.Rows[rowIndex], columnIndex)))
                                {
                                    continue;
                                }

                                if (rowIndex - anchor > 1)
                                {
                                    string column = ColumnName(columnIndex + 1);
                                    ranges.Add(Txt.Format("{0}{1}:{0}{2}", column, anchor + 1, rowIndex));
                                }

                                anchor = rowIndex;
                            }
                        }

                        start = end;
                    }

                    return ranges;
                }

                /// <summary>
                /// 计算所有行中的最大列数。
                /// </summary>
                /// <param name="rows">Sheet 行数据。</param>
                /// <returns>最大列数。</returns>
                private static int MaxColumnCount(IReadOnlyList<IReadOnlyList<string>> rows)
                {
                    int max = 0;
                    foreach (IReadOnlyList<string> row in rows)
                    {
                        if (row.Count > max)
                        {
                            max = row.Count;
                        }
                    }

                    return max;
                }

                /// <summary>
                /// 读取指定单元格文本，越界或空引用时返回空字符串。
                /// </summary>
                /// <param name="row">当前行内容。</param>
                /// <param name="columnIndex">列索引。</param>
                /// <returns>单元格文本。</returns>
                private static string CellValue(IReadOnlyList<string> row, int columnIndex)
                {
                    return columnIndex < row.Count ? row[columnIndex] ?? string.Empty : string.Empty;
                }

                /// <summary>
                /// 构建合并单元格声明的 OpenXML 文本。
                /// </summary>
                /// <param name="ranges">需要合并的 Excel 单元格范围。</param>
                /// <returns>mergeCells xml 内容，没有合并范围时返回空字符串。</returns>
                private static string BuildMergeCellsXml(IReadOnlyList<string> ranges)
                {
                    if (ranges == null || ranges.Count == 0)
                    {
                        return string.Empty;
                    }

                    var mergeCells = new List<string>(ranges.Count);
                    foreach (string range in ranges)
                    {
                        mergeCells.Add(Txt.Format("<mergeCell ref=\"{0}\"/>", range));
                    }

                    return Txt.Format("<mergeCells count=\"{0}\">{1}</mergeCells>", ranges.Count, string.Concat(mergeCells));
                }

                /// <summary>
                /// 把从 1 开始的列序号转换为 Excel 列名。
                /// </summary>
                /// <param name="index">从 1 开始的列序号。</param>
                /// <returns>Excel 列名。</returns>
                private static string ColumnName(int index)
                {
                    string result = string.Empty;
                    while (index > 0)
                    {
                        index--;
                        result = (char)('A' + index % 26) + result;
                        index /= 26;
                    }

                    return result;
                }

                /// <summary>
                /// 构建与 Network 模板一致的单色数据区样式表；四种样式依次为数据、表头、事件标识与事件说明。
                /// </summary>
                /// <returns>styles xml 内容。</returns>
                private static string StylesXml()
                {
                    return "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                           + "<numFmts count=\"0\" />"
                           + "<fonts count=\"4\">"
                           + "<font>"
                           + "<name val=\"Calibri\" />"
                           + "<family val=\"2\" />"
                           + "<color theme=\"1\" />"
                           + "<sz val=\"11\" />"
                           + "<scheme val=\"minor\" />"
                           + "</font>"
                           + "<font>"
                           + "<name val=\"Calibri\" />"
                           + "<charset val=\"134\" />"
                           + "<color theme=\"1\" />"
                           + "<sz val=\"11\" />"
                           + "</font>"
                           + "<font>"
                           + "<name val=\"Calibri\" />"
                           + "<charset val=\"134\" />"
                           + "<b val=\"1\" />"
                           + "<color rgb=\"FFFFFFFF\" />"
                           + "<sz val=\"11\" />"
                           + "</font>"
                           + "<font>"
                           + "<name val=\"宋体-简\" />"
                           + "<charset val=\"134\" />"
                           + "<color theme=\"1\" />"
                           + "<sz val=\"11\" />"
                           + "</font>"
                           + "</fonts>"
                           + "<fills count=\"4\">"
                           + "<fill>"
                           + "<patternFill />"
                           + "</fill>"
                           + "<fill>"
                           + "<patternFill patternType=\"gray125\" />"
                           + "</fill>"
                           + "<fill>"
                           + "<patternFill patternType=\"solid\">"
                           + "<fgColor rgb=\"FFEAF4EC\" />"
                           + "<bgColor indexed=\"64\" />"
                           + "</patternFill>"
                           + "</fill>"
                           + "<fill>"
                           + "<patternFill patternType=\"solid\">"
                           + "<fgColor rgb=\"FF1F4E78\" />"
                           + "<bgColor indexed=\"64\" />"
                           + "</patternFill>"
                           + "</fill>"
                           + "</fills>"
                           + "<borders count=\"3\">"
                           + "<border>"
                           + "<left />"
                           + "<right />"
                           + "<top />"
                           + "<bottom />"
                           + "<diagonal />"
                           + "</border>"
                           + "<border>"
                           + "<left style=\"thin\">"
                           + "<color rgb=\"FFB7C7D6\" />"
                           + "</left>"
                           + "<right style=\"thin\">"
                           + "<color rgb=\"FFB7C7D6\" />"
                           + "</right>"
                           + "<top style=\"thin\">"
                           + "<color rgb=\"FFB7C7D6\" />"
                           + "</top>"
                           + "<bottom style=\"thin\">"
                           + "<color rgb=\"FFB7C7D6\" />"
                           + "</bottom>"
                           + "<diagonal />"
                           + "</border>"
                           + "<border>"
                           + "<left />"
                           + "<right />"
                           + "<top />"
                           + "<bottom style=\"thin\">"
                           + "<color rgb=\"FFD9E2F3\" />"
                           + "</bottom>"
                           + "<diagonal />"
                           + "</border>"
                           + "</borders>"
                           + "<cellStyleXfs count=\"1\">"
                           + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" />"
                           + "</cellStyleXfs>"
                           + "<cellXfs count=\"5\">"
                           + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" pivotButton=\"0\" quotePrefix=\"0\" xfId=\"0\" />"
                           + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" pivotButton=\"0\" quotePrefix=\"0\" xfId=\"0\" />"
                           + "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"2\" applyAlignment=\"1\" pivotButton=\"0\" quotePrefix=\"0\" xfId=\"0\">"
                           + "<alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\" />"
                           + "</xf>"
                           + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" applyAlignment=\"1\" pivotButton=\"0\" quotePrefix=\"0\" xfId=\"0\">"
                           + "<alignment horizontal=\"left\" vertical=\"top\" />"
                           + "</xf>"
                           + "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"2\" borderId=\"1\" applyAlignment=\"1\" pivotButton=\"0\" quotePrefix=\"0\" xfId=\"0\">"
                           + "<alignment horizontal=\"left\" vertical=\"top\" wrapText=\"1\" />"
                           + "</xf>"
                           + "</cellXfs>"
                           + "<cellStyles count=\"1\">"
                           + "<cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\" hidden=\"0\" />"
                           + "</cellStyles>"
                           + "<tableStyles count=\"0\" defaultTableStyle=\"TableStyleMedium9\" defaultPivotStyle=\"PivotStyleLight16\" />"
                           + "<colors>"
                           + "<indexedColors>"
                           + "<rgbColor rgb=\"00000000\" />"
                           + "<rgbColor rgb=\"00FFFFFF\" />"
                           + "<rgbColor rgb=\"00FF0000\" />"
                           + "<rgbColor rgb=\"0000FF00\" />"
                           + "<rgbColor rgb=\"000000FF\" />"
                           + "<rgbColor rgb=\"00FFFF00\" />"
                           + "<rgbColor rgb=\"00FF00FF\" />"
                           + "<rgbColor rgb=\"0000FFFF\" />"
                           + "<rgbColor rgb=\"00000000\" />"
                           + "<rgbColor rgb=\"00FFFFFF\" />"
                           + "<rgbColor rgb=\"00FF0000\" />"
                           + "<rgbColor rgb=\"0000FF00\" />"
                           + "<rgbColor rgb=\"000000FF\" />"
                           + "<rgbColor rgb=\"00FFFF00\" />"
                           + "<rgbColor rgb=\"00FF00FF\" />"
                           + "<rgbColor rgb=\"0000FFFF\" />"
                           + "<rgbColor rgb=\"00800000\" />"
                           + "<rgbColor rgb=\"00008000\" />"
                           + "<rgbColor rgb=\"00000080\" />"
                           + "<rgbColor rgb=\"00808000\" />"
                           + "<rgbColor rgb=\"00800080\" />"
                           + "<rgbColor rgb=\"00008080\" />"
                           + "<rgbColor rgb=\"00C0C0C0\" />"
                           + "<rgbColor rgb=\"00808080\" />"
                           + "<rgbColor rgb=\"009999FF\" />"
                           + "<rgbColor rgb=\"00993366\" />"
                           + "<rgbColor rgb=\"00FFFFCC\" />"
                           + "<rgbColor rgb=\"00CCFFFF\" />"
                           + "<rgbColor rgb=\"00660066\" />"
                           + "<rgbColor rgb=\"00FF8080\" />"
                           + "<rgbColor rgb=\"000066CC\" />"
                           + "<rgbColor rgb=\"00CCCCFF\" />"
                           + "<rgbColor rgb=\"00000080\" />"
                           + "<rgbColor rgb=\"00FF00FF\" />"
                           + "<rgbColor rgb=\"00FFFF00\" />"
                           + "<rgbColor rgb=\"0000FFFF\" />"
                           + "<rgbColor rgb=\"00800080\" />"
                           + "<rgbColor rgb=\"00800000\" />"
                           + "<rgbColor rgb=\"00008080\" />"
                           + "<rgbColor rgb=\"000000FF\" />"
                           + "<rgbColor rgb=\"0000CCFF\" />"
                           + "<rgbColor rgb=\"00CCFFFF\" />"
                           + "<rgbColor rgb=\"00CCFFCC\" />"
                           + "<rgbColor rgb=\"00FFFF99\" />"
                           + "<rgbColor rgb=\"0099CCFF\" />"
                           + "<rgbColor rgb=\"00FF99CC\" />"
                           + "<rgbColor rgb=\"00CC99FF\" />"
                           + "<rgbColor rgb=\"00FFCC99\" />"
                           + "<rgbColor rgb=\"003366FF\" />"
                           + "<rgbColor rgb=\"0033CCCC\" />"
                           + "<rgbColor rgb=\"0099CC00\" />"
                           + "<rgbColor rgb=\"00FFCC00\" />"
                           + "<rgbColor rgb=\"00FF9900\" />"
                           + "<rgbColor rgb=\"00FF6600\" />"
                           + "<rgbColor rgb=\"00666699\" />"
                           + "<rgbColor rgb=\"00969696\" />"
                           + "<rgbColor rgb=\"00003366\" />"
                           + "<rgbColor rgb=\"00339966\" />"
                           + "<rgbColor rgb=\"00003300\" />"
                           + "<rgbColor rgb=\"00333300\" />"
                           + "<rgbColor rgb=\"00993300\" />"
                           + "<rgbColor rgb=\"00993366\" />"
                           + "<rgbColor rgb=\"00333399\" />"
                           + "<rgbColor rgb=\"00333333\" />"
                           + "</indexedColors>"
                           + "</colors>"
                           + "</styleSheet>";
                }

                /// <summary>
                /// 转义写入 XML 文本节点或属性的字符串。
                /// </summary>
                /// <param name="value">原始字符串。</param>
                /// <returns>XML 转义后的字符串。</returns>
                private static string Escape(string value)
                {
                    return SecurityElement.Escape(value ?? string.Empty);
                }

                /// <summary>
                /// 向 xlsx 压缩包写入一个文件条目。
                /// </summary>
                /// <param name="archive">目标 xlsx 压缩包。</param>
                /// <param name="name">压缩包内条目路径。</param>
                /// <param name="content">条目文本内容。</param>
                private static void WriteEntry(ZipArchive archive, string name, string content)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write(content);
                }
            }
        }
    }
}
