/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.DataTypeNameHelper.cs
 * author:    taoye
 * created:   2026/4/25
 * descrip:   Luban Excel value type scanner
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Luban
        {
            internal static class DataTypeNameHelper
            {
                internal static IReadOnlyList<string> ScanValueTypes(string filePath, int minHeaderRowCount)
                {
                    if (!Util.SysIO.File.Exists(filePath))
                    {
                        throw new System.IO.FileNotFoundException(
                            $"Luban schema source does not exist: {filePath}",
                            filePath);
                    }

                    try
                    {
                        Dictionary<string, List<IReadOnlyList<string>>> sheets =
                            EditorUtil.Excel.ReadAllSheets(filePath);
                        if (sheets == null)
                        {
                            throw new System.IO.InvalidDataException(
                                $"Luban schema source returned no sheets: {filePath}");
                        }

                        var orderedSheets =
                            new List<KeyValuePair<string, IReadOnlyList<IReadOnlyList<string>>>>(sheets.Count);
                        foreach (KeyValuePair<string, List<IReadOnlyList<string>>> sheet in sheets)
                        {
                            orderedSheets.Add(
                                new KeyValuePair<string, IReadOnlyList<IReadOnlyList<string>>>(
                                    sheet.Key,
                                    sheet.Value));
                        }

                        return ExtractValueTypes(orderedSheets, minHeaderRowCount);
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        throw new System.IO.InvalidDataException(
                            $"Failed to scan Luban schema source '{filePath}': {exception.Message}",
                            exception);
                    }
                }

                internal static IReadOnlyList<string> ExtractValueTypes(
                    IEnumerable<KeyValuePair<string, IReadOnlyList<IReadOnlyList<string>>>> sheets,
                    int minHeaderRowCount)
                {
                    if (sheets == null)
                    {
                        throw new ArgumentNullException(nameof(sheets));
                    }

                    if (minHeaderRowCount < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(minHeaderRowCount));
                    }

                    var valueTypes = new List<string>();
                    foreach (KeyValuePair<string, IReadOnlyList<IReadOnlyList<string>>> sheet in sheets)
                    {
                        if (!string.IsNullOrEmpty(sheet.Key) &&
                            !sheet.Key.StartsWith("#", StringComparison.Ordinal) &&
                            sheet.Value != null &&
                            sheet.Value.Count >= minHeaderRowCount)
                        {
                            valueTypes.Add(sheet.Key);
                        }
                    }

                    return valueTypes;
                }
            }
        }
    }
}
