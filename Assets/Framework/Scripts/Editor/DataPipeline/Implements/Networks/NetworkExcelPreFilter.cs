/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkExcelPreFilter.cs
 * author:    taoye
 * created:   2026/4/17
 * descrip:   Network Excel 输入投影：HostKeys 校验环境 Sheet 配对并选择当前模式，Cmds 保持原结构
 * input:     HostKeys/Cmds 源工作簿与 ConfigRuntimeSO.DevelopMode
 * output:    Luban 约定的 _temp/{工作簿名}/{基础 Sheet 名}.csv
 * boundary:  只解释 Network 表格结构，不执行 Luban 导出或发布正式产物
 * failure:   HostKeys 命名或 Debug/Release 配对不完整时先终止，不写任何临时输入
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using NovaFramework.Runtime;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Network 专用 Excel 预处理器。
    /// HostKeys 必须为同一基础名提供 <c>-Debug</c> 与 <c>-Release</c> 两个 Sheet，
    /// 预处理时按当前 DevelopMode 选择一个并去掉后缀；NetCmds 仅搬运有效 Sheet。
    /// </summary>
    internal static class NetworkExcelPreFilter
    {
        private const string c_SearchPattern = "*.xlsx";
        private const string c_ExcludePrefix = "~$";
        private const string c_ConfigsDirName = "_configs";
        private const string c_TempDirName = "_temp";
        private const string c_DebugSuffix = "-Debug";
        private const string c_ReleaseSuffix = "-Release";

        internal delegate Dictionary<string, List<IReadOnlyList<string>>> WorkbookReader(string filePath);
        internal delegate void WorkbookWriter(
            string outputDirPath,
            Dictionary<string, List<IReadOnlyList<string>>> sheets);

        /// <summary>
        /// 校验目录内全部 HostKeys 工作簿的环境 Sheet，并为当前模式生成 Luban 临时输入。
        /// 所有工作簿都验证通过后才开始写入，避免留下部分结果。
        /// </summary>
        internal static void FilterHostKeys(string sourceDirPath, string tempDirPath, DevelopMode mode)
        {
            FilterHostKeys(sourceDirPath, tempDirPath, mode, EditorUtil.Excel.ReadAllSheets, EditorUtil.Excel.Write);
        }

        internal static void FilterHostKeys(
            string sourceDirPath,
            string tempDirPath,
            DevelopMode mode,
            WorkbookReader reader,
            WorkbookWriter writer)
        {
            List<string> files = GetSourceFiles(sourceDirPath);
            var pending = new List<PendingOutput>(files.Count);
            foreach (string filePath in files)
            {
                Dictionary<string, List<IReadOnlyList<string>>> sheets =
                    ProjectHostKeySheets(filePath, reader(filePath), mode);
                pending.Add(new PendingOutput(GetOutputDir(tempDirPath, filePath), sheets));
            }

            WriteAll(pending, writer);
        }

        /// <summary>
        /// 将目录内全部 NetCmds 有效 Sheet 原名写入 Luban 临时输入，不做环境筛选。
        /// </summary>
        internal static void FilterNetCmds(string sourceDirPath, string tempDirPath)
        {
            FilterNetCmds(sourceDirPath, tempDirPath, EditorUtil.Excel.ReadAllSheets, EditorUtil.Excel.Write);
        }

        internal static void FilterNetCmds(
            string sourceDirPath,
            string tempDirPath,
            WorkbookReader reader,
            WorkbookWriter writer)
        {
            List<string> files = GetSourceFiles(sourceDirPath);
            var pending = new List<PendingOutput>(files.Count);
            foreach (string filePath in files)
            {
                Dictionary<string, List<IReadOnlyList<string>>> sheets = GetExportableSheets(reader(filePath));
                if (sheets.Count > 0)
                {
                    pending.Add(new PendingOutput(GetOutputDir(tempDirPath, filePath), sheets));
                }
            }

            WriteAll(pending, writer);
        }

        internal static void FilterHostKeyFile(
            string excelFilePath,
            string tempDirPath,
            DevelopMode mode,
            WorkbookReader reader,
            WorkbookWriter writer)
        {
            Dictionary<string, List<IReadOnlyList<string>>> sheets =
                ProjectHostKeySheets(excelFilePath, reader(excelFilePath), mode);
            writer(GetOutputDir(tempDirPath, excelFilePath), sheets);
        }

        internal static void FilterNetCmdFile(
            string excelFilePath,
            string tempDirPath,
            WorkbookReader reader,
            WorkbookWriter writer)
        {
            Dictionary<string, List<IReadOnlyList<string>>> sheets = GetExportableSheets(reader(excelFilePath));
            if (sheets.Count > 0)
            {
                writer(GetOutputDir(tempDirPath, excelFilePath), sheets);
            }
        }

        private static Dictionary<string, List<IReadOnlyList<string>>> ProjectHostKeySheets(
            string filePath,
            Dictionary<string, List<IReadOnlyList<string>>> allSheets,
            DevelopMode mode)
        {
            Dictionary<string, List<IReadOnlyList<string>>> exportable = GetExportableSheets(allSheets);
            if (exportable.Count == 0)
            {
                throw Error(filePath, "没有可导出的 Sheet；至少需要一组 xxxxx-Debug 与 xxxxx-Release。");
            }

            var pairs = new Dictionary<string, SheetPair>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<IReadOnlyList<string>>> pair in exportable)
            {
                string suffix;
                bool isDebug;
                if (pair.Key.EndsWith(c_DebugSuffix, StringComparison.Ordinal))
                {
                    suffix = c_DebugSuffix;
                    isDebug = true;
                }
                else if (pair.Key.EndsWith(c_ReleaseSuffix, StringComparison.Ordinal))
                {
                    suffix = c_ReleaseSuffix;
                    isDebug = false;
                }
                else
                {
                    throw Error(filePath, $"Sheet '{pair.Key}' 必须以 -Debug 或 -Release 结尾。");
                }

                string baseName = pair.Key.Substring(0, pair.Key.Length - suffix.Length);
                if (string.IsNullOrWhiteSpace(baseName))
                {
                    throw Error(filePath, $"Sheet '{pair.Key}' 的模式后缀前必须包含基础名称。");
                }

                if (!pairs.TryGetValue(baseName, out SheetPair sheetPair))
                {
                    sheetPair = new SheetPair();
                    pairs.Add(baseName, sheetPair);
                }

                if (isDebug)
                {
                    sheetPair.Debug = pair.Value;
                }
                else
                {
                    sheetPair.Release = pair.Value;
                }
            }

            var result = new Dictionary<string, List<IReadOnlyList<string>>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, SheetPair> pair in pairs)
            {
                if (pair.Value.Debug == null)
                {
                    throw Error(filePath, $"缺少配对 Sheet '{pair.Key}-Debug'。");
                }
                if (pair.Value.Release == null)
                {
                    throw Error(filePath, $"缺少配对 Sheet '{pair.Key}-Release'。");
                }

                result.Add(pair.Key, mode == DevelopMode.Debug ? pair.Value.Debug : pair.Value.Release);
            }

            return result;
        }

        private static Dictionary<string, List<IReadOnlyList<string>>> GetExportableSheets(
            Dictionary<string, List<IReadOnlyList<string>>> allSheets)
        {
            var result = new Dictionary<string, List<IReadOnlyList<string>>>(StringComparer.Ordinal);
            if (allSheets == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, List<IReadOnlyList<string>>> pair in allSheets)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) ||
                    pair.Key.StartsWith("#", StringComparison.Ordinal) ||
                    pair.Value == null || pair.Value.Count < 5)
                {
                    continue;
                }

                result.Add(pair.Key, pair.Value);
            }
            return result;
        }

        private static List<string> GetSourceFiles(string sourceDirPath)
        {
            if (string.IsNullOrWhiteSpace(sourceDirPath) || !Directory.Exists(sourceDirPath))
            {
                throw new DirectoryNotFoundException($"Network 数据源目录不存在：{sourceDirPath ?? "null"}");
            }

            string[] files = EditorUtil.FileSystem.GetFiles(sourceDirPath, c_SearchPattern, c_ExcludePrefix);
            var result = new List<string>();
            string configsDirPath = IOPath.Combine(sourceDirPath, c_ConfigsDirName);
            string tempDirPath = IOPath.Combine(sourceDirPath, c_TempDirName);
            if (files == null)
            {
                return result;
            }

            foreach (string filePath in files)
            {
                if (!EditorUtil.FileSystem.IsSubPathOf(filePath, configsDirPath) &&
                    !EditorUtil.FileSystem.IsSubPathOf(filePath, tempDirPath))
                {
                    result.Add(filePath);
                }
            }
            return result;
        }

        private static string GetOutputDir(string tempDirPath, string filePath)
        {
            return IOPath.Combine(tempDirPath, IOPath.GetFileNameWithoutExtension(filePath));
        }

        private static void WriteAll(IReadOnlyList<PendingOutput> outputs, WorkbookWriter writer)
        {
            foreach (PendingOutput output in outputs)
            {
                writer(output.DirectoryPath, output.Sheets);
            }
        }

        private static InvalidDataException Error(string filePath, string message)
        {
            return new InvalidDataException($"Network HostKeys Excel '{filePath}'：{message}");
        }

        private sealed class SheetPair
        {
            internal List<IReadOnlyList<string>> Debug;
            internal List<IReadOnlyList<string>> Release;
        }

        private readonly struct PendingOutput
        {
            internal PendingOutput(string directoryPath, Dictionary<string, List<IReadOnlyList<string>>> sheets)
            {
                DirectoryPath = directoryPath;
                Sheets = sheets;
            }

            internal string DirectoryPath { get; }
            internal Dictionary<string, List<IReadOnlyList<string>>> Sheets { get; }
        }
    }
}
