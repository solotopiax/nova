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
                /// 普通列的最小显示宽度。
                /// </summary>
                private const double c_MinColumnWidth = 10d;

                /// <summary>
                /// 普通列的最大显示宽度。
                /// </summary>
                private const double c_MaxColumnWidth = 55d;

                /// <summary>
                /// Framework 属性说明 Sheet 的最大显示宽度。
                /// </summary>
                private const double c_FrameworkPropertyMaxColumnWidth = 120d;

                /// <summary>
                /// Framework 属性说明 Sheet 第一列的统一显示宽度。
                /// </summary>
                private const double c_FrameworkPropertyFirstColumnWidth = 32d;

                /// <summary>
                /// Framework 说明 Sheet 首行各列的固定宽度。
                /// </summary>
                private const double c_FrameworkFirstRowWidth = 32d;

                /// <summary>
                /// Framework 说明 Sheet 首行的固定高度。
                /// </summary>
                private const double c_FrameworkFirstRowHeight = 48d;

                /// <summary>
                /// 模块打点 Sheet 首行的统一显示高度。
                /// </summary>
                private const double c_PackageFirstRowHeight = 48d;

                /// <summary>
                /// 注意事项 Sheet 的 B 列宽度相对内容宽度的放大倍数。
                /// </summary>
                private const double c_NotesColumnBWidthScale = 1.5d;

                /// <summary>
                /// 注意事项 Sheet 的 B 列最大显示宽度。
                /// </summary>
                private const double c_NotesColumnBMaxWidth = 200d;

                /// <summary>
                /// 模块打点 Sheet 中事件数据开始参与分组渲染的行索引。
                /// </summary>
                private const int c_PackageTrackStartRowIndex = 1;

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
                    for (int rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
                    {
                        IReadOnlyList<string> row = sheet.Rows[rowIndex];
                        var cells = new List<string>(row.Count);
                        string rowAttr = Txt.Format(" r=\"{0}\"", rowIndex + 1);
                        double height = RowHeight(sheet, rowIndex, row, widths);
                        if (height > 18d)
                        {
                            rowAttr += Txt.Format(" ht=\"{0}\" customHeight=\"1\"", height.ToString("0.##", CultureInfo.InvariantCulture));
                        }

                        for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
                        {
                            int style = CellStyle(sheet, rowIndex, columnIndex, row);
                            string styleAttr = style > 0 ? Txt.Format(" s=\"{0}\"", style) : string.Empty;
                            string reference = ColumnName(columnIndex + 1) + (rowIndex + 1).ToString(CultureInfo.InvariantCulture);
                            cells.Add(Txt.Format("<c r=\"{0}\"{1} t=\"inlineStr\"><is><t>{2}</t></is></c>", reference, styleAttr, Escape(row[columnIndex])));
                        }

                        rowsXml.Add(Txt.Format("<row{0}>{1}</row>", rowAttr, string.Concat(cells)));
                    }

                    return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                           "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                           BuildColsXml(widths) +
                           Txt.Format("<sheetData>{0}</sheetData>", string.Concat(rowsXml)) +
                           BuildMergeCellsXml(mergeRanges) +
                           "</worksheet>";
                }

                /// <summary>
                /// 根据 Sheet 内容和 Framework 特殊说明页规则计算每列宽度。
                /// </summary>
                /// <param name="sheet">待计算列宽的 Sheet 数据。</param>
                /// <returns>按列顺序排列的宽度列表。</returns>
                private static List<double> ColumnWidths(TrackSheet sheet)
                {
                    int maxColumns = 1;
                    foreach (IReadOnlyList<string> row in sheet.Rows)
                    {
                        if (row.Count > maxColumns)
                        {
                            maxColumns = row.Count;
                        }
                    }

                    var widths = new List<double>(maxColumns);
                    var contentWidths = new List<double>(maxColumns);
                    for (int columnIndex = 0; columnIndex < maxColumns; columnIndex++)
                    {
                        int maxWidth = 8;
                        foreach (IReadOnlyList<string> row in sheet.Rows)
                        {
                            if (columnIndex < row.Count)
                            {
                                maxWidth = System.Math.Max(maxWidth, DisplayWidth(row[columnIndex]));
                            }
                        }

                        double contentWidth = System.Math.Max(maxWidth + 2, c_MinColumnWidth);
                        double maxColumnWidth = IsFrameworkPropertySheet(sheet.Name) ? c_FrameworkPropertyMaxColumnWidth : c_MaxColumnWidth;
                        widths.Add(System.Math.Min(contentWidth, maxColumnWidth));
                        contentWidths.Add(contentWidth);
                    }

                    if (sheet.IsFrameworkSheet && !IsFrameworkPropertySheet(sheet.Name) && sheet.Rows.Count > 0)
                    {
                        for (int i = 0; i < sheet.Rows[0].Count && i < widths.Count; i++)
                        {
                            widths[i] = c_FrameworkFirstRowWidth;
                        }
                    }

                    if (sheet.IsFrameworkSheet && IsFrameworkPropertySheet(sheet.Name) && widths.Count > 0)
                    {
                        widths[0] = c_FrameworkPropertyFirstColumnWidth;
                    }

                    if (sheet.IsFrameworkSheet && IsNotesSheet(sheet.Name) && widths.Count > 1)
                    {
                        widths[1] = System.Math.Min(contentWidths[1] * c_NotesColumnBWidthScale, c_NotesColumnBMaxWidth);
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
                /// 根据单元格内容长度和换行数量估算行高。
                /// </summary>
                /// <param name="sheet">当前 Sheet 数据。</param>
                /// <param name="rowIndex">当前行索引。</param>
                /// <param name="row">当前行内容。</param>
                /// <param name="widths">按列顺序排列的宽度列表。</param>
                /// <returns>写入 OpenXML 的行高。</returns>
                private static double RowHeight(TrackSheet sheet, int rowIndex, IReadOnlyList<string> row, IReadOnlyList<double> widths)
                {
                    if (sheet.IsFrameworkSheet && rowIndex == 0)
                    {
                        return c_FrameworkFirstRowHeight;
                    }

                    if (!sheet.IsFrameworkSheet && rowIndex == 0)
                    {
                        return c_PackageFirstRowHeight;
                    }

                    int lineCount = 1;
                    for (int i = 0; i < row.Count; i++)
                    {
                        double width = i < widths.Count ? widths[i] : 20d;
                        string value = row[i] ?? string.Empty;
                        int explicitLines = value.Split('\n').Length;
                        int wrappedLines = System.Math.Max(1, (int)System.Math.Ceiling(DisplayWidth(value) / System.Math.Max(width, 1d)));
                        lineCount = System.Math.Max(lineCount, System.Math.Max(explicitLines, wrappedLines));
                    }

                    return System.Math.Min(System.Math.Max(lineCount * 18d, 18d), 150d);
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
                    if (sheet.IsFrameworkSheet && IsFrameworkPropertySheet(sheet.Name))
                    {
                        return FrameworkPropertyCellStyle(rowIndex);
                    }

                    if (sheet.IsFrameworkSheet && !IsFrameworkDescriptionSheet(sheet.Name))
                    {
                        if (rowIndex == 0)
                        {
                            return 11;
                        }

                        return row.Count > 0 && !string.IsNullOrEmpty(row[0]) ? 3 : 0;
                    }

                    if (sheet.IsFrameworkSheet && rowIndex == 0 && columnIndex == 0)
                    {
                        return 5;
                    }

                    if (!sheet.IsFrameworkSheet && rowIndex >= c_PackageTrackStartRowIndex)
                    {
                        int groupIndex = PackageTrackGroupIndex(sheet, rowIndex);
                        bool evenGroup = groupIndex % 2 == 0;
                        if (IsPackageMergedColumn(sheet, columnIndex))
                        {
                            return evenGroup ? 6 : 8;
                        }

                        return evenGroup ? 7 : 9;
                    }

                    return BaseCellStyle(rowIndex, row);
                }

                /// <summary>
                /// 选择 Framework 属性说明页的单元格样式。
                /// </summary>
                /// <param name="rowIndex">当前行索引。</param>
                /// <returns>styles.xml 中的 cellXfs 样式索引。</returns>
                private static int FrameworkPropertyCellStyle(int rowIndex)
                {
                    if (rowIndex == 0)
                    {
                        return 5;
                    }

                    if (rowIndex == 1)
                    {
                        return 10;
                    }

                    return 4;
                }

                /// <summary>
                /// 选择非模块分组场景下的基础单元格样式。
                /// </summary>
                /// <param name="rowIndex">当前行索引。</param>
                /// <param name="row">当前行内容。</param>
                /// <returns>styles.xml 中的 cellXfs 样式索引。</returns>
                private static int BaseCellStyle(int rowIndex, IReadOnlyList<string> row)
                {
                    if (rowIndex == 0)
                    {
                        return 1;
                    }

                    if (rowIndex == 1)
                    {
                        return 2;
                    }

                    if (row.Count > 0 && !string.IsNullOrEmpty(row[0]))
                    {
                        return 3;
                    }

                    return 0;
                }

                /// <summary>
                /// 构建模块打点 Sheet 中需要纵向合并的单元格范围。
                /// </summary>
                /// <param name="sheet">当前 Sheet 数据。</param>
                /// <returns>Excel 单元格范围列表，例如 A2:A4。</returns>
                private static List<string> BuildMergeRanges(TrackSheet sheet)
                {
                    var ranges = new List<string>();
                    if (sheet.IsFrameworkSheet)
                    {
                        return ranges;
                    }

                    foreach (int columnIndex in PackageMergedColumnIndexes(sheet))
                    {
                        int rowIndex = c_PackageTrackStartRowIndex;
                        while (rowIndex < sheet.Rows.Count)
                        {
                            if (string.IsNullOrEmpty(CellValue(sheet.Rows[rowIndex], columnIndex)))
                            {
                                rowIndex++;
                                continue;
                            }

                            int endRowIndex = rowIndex;
                            while (endRowIndex + 1 < sheet.Rows.Count && string.IsNullOrEmpty(CellValue(sheet.Rows[endRowIndex + 1], columnIndex)))
                            {
                                endRowIndex++;
                            }

                            if (endRowIndex > rowIndex)
                            {
                                string columnName = ColumnName(columnIndex + 1);
                                ranges.Add(Txt.Format("{0}{1}:{0}{2}", columnName, rowIndex + 1, endRowIndex + 1));
                            }

                            rowIndex = endRowIndex + 1;
                        }
                    }

                    return ranges;
                }

                /// <summary>
                /// 获取模块打点 Sheet 中需要按事件分组纵向合并的列索引。
                /// </summary>
                /// <param name="sheet">当前 Sheet 数据。</param>
                /// <returns>需要合并的列索引列表。</returns>
                private static List<int> PackageMergedColumnIndexes(TrackSheet sheet)
                {
                    int maxColumnCount = MaxColumnCount(sheet.Rows);
                    var indexes = new List<int>();
                    for (int i = 0; i < maxColumnCount; i++)
                    {
                        if (i <= 2 || i >= maxColumnCount - 3)
                        {
                            indexes.Add(i);
                        }
                    }

                    return indexes;
                }

                /// <summary>
                /// 判断指定列是否属于模块打点 Sheet 的分组合并列。
                /// </summary>
                /// <param name="sheet">当前 Sheet 数据。</param>
                /// <param name="columnIndex">待判断的列索引。</param>
                /// <returns>指定列是否需要使用分组合并样式。</returns>
                private static bool IsPackageMergedColumn(TrackSheet sheet, int columnIndex)
                {
                    int maxColumnCount = MaxColumnCount(sheet.Rows);
                    return columnIndex <= 2 || columnIndex >= maxColumnCount - 3;
                }

                /// <summary>
                /// 根据首列非空事件名计算当前行所属的事件分组序号。
                /// </summary>
                /// <param name="sheet">当前 Sheet 数据。</param>
                /// <param name="rowIndex">当前行索引。</param>
                /// <returns>从 0 开始的事件分组序号。</returns>
                private static int PackageTrackGroupIndex(TrackSheet sheet, int rowIndex)
                {
                    int groupIndex = -1;
                    for (int i = c_PackageTrackStartRowIndex; i <= rowIndex && i < sheet.Rows.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(CellValue(sheet.Rows[i], 0)))
                        {
                            groupIndex++;
                        }
                    }

                    return System.Math.Max(groupIndex, 0);
                }

                /// <summary>
                /// 判断 Sheet 名是否为注意事项说明页。
                /// </summary>
                /// <param name="sheetName">Sheet 名称。</param>
                /// <returns>是否为注意事项说明页。</returns>
                private static bool IsNotesSheet(string sheetName)
                {
                    return string.Equals(sheetName, "$注意事项", System.StringComparison.Ordinal) ||
                           string.Equals(sheetName, "$娉ㄦ剰浜嬮」", System.StringComparison.Ordinal);
                }

                /// <summary>
                /// 判断 Sheet 名是否为 Framework 属性说明页。
                /// </summary>
                /// <param name="sheetName">Sheet 名称。</param>
                /// <returns>是否为默认用户属性或公共事件属性说明页。</returns>
                private static bool IsFrameworkPropertySheet(string sheetName)
                {
                    return string.Equals(sheetName, "$默认用户属性说明", System.StringComparison.Ordinal) ||
                           string.Equals(sheetName, "$公共事件属性说明", System.StringComparison.Ordinal);
                }

                /// <summary>
                /// 判断 Framework Sheet 是否为带标题行和二级表头的说明页。
                /// </summary>
                private static bool IsFrameworkDescriptionSheet(string sheetName)
                {
                    return !string.IsNullOrEmpty(sheetName) && sheetName[0] == '$';
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
                /// 估算文本展示宽度，中文字符按两个英文字符宽度计算。
                /// </summary>
                /// <param name="value">待估算的文本。</param>
                /// <returns>估算后的展示宽度。</returns>
                private static int DisplayWidth(string value)
                {
                    int width = 0;
                    foreach (char ch in value ?? string.Empty)
                    {
                        width += ch >= '\u4e00' && ch <= '\u9fff' ? 2 : 1;
                    }

                    return width;
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
                /// 构建生成工作簿使用的样式表 OpenXML 文本。
                /// </summary>
                /// <returns>styles xml 内容。</returns>
                private static string StylesXml()
                {
                    return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                           "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                           "<fonts count=\"4\">" +
                           "<font><sz val=\"11\"/><color theme=\"1\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                           "<font><b/><sz val=\"13\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                           "<font><b/><sz val=\"11\"/><color rgb=\"FF17324D\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                           "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                           "</fonts>" +
                           "<fills count=\"6\">" +
                           "<fill><patternFill patternType=\"none\"/></fill>" +
                           "<fill><patternFill patternType=\"gray125\"/></fill>" +
                           "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF17324D\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                           "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9EAF7\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                           "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFEAF4EC\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                           "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFF4D8\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                           "</fills>" +
                           "<borders count=\"2\">" +
                           "<border><left/><right/><top/><bottom/><diagonal/></border>" +
                           "<border><left style=\"thin\"><color rgb=\"FFB7C7D6\"/></left><right style=\"thin\"><color rgb=\"FFB7C7D6\"/></right><top style=\"thin\"><color rgb=\"FFB7C7D6\"/></top><bottom style=\"thin\"><color rgb=\"FFB7C7D6\"/></bottom><diagonal/></border>" +
                           "</borders>" +
                           "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                           "<cellXfs count=\"12\">" +
                           "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                           "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                           "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                           "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                           "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                           "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                           "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                           "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                           "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"5\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                           "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"5\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                           "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                           "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                           "</cellXfs>" +
                           "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
                           "<dxfs count=\"0\"/><tableStyles count=\"0\" defaultTableStyle=\"TableStyleMedium2\" defaultPivotStyle=\"TableStyleLight16\"/>" +
                           "</styleSheet>";
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
