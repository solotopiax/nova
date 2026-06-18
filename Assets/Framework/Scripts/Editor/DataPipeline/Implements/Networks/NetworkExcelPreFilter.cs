/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkExcelPreFilter.cs
 * author:    taoye
 * created:   2026/4/17
 * descrip:   Network 模块 Excel 预过滤器（纯拷贝）
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Network 模块 Excel 预过滤器（纯拷贝）。
    /// 将源目录下的 xlsx 文件原样搬运到 _temp/ 目录，供 Luban CLI 读取。
    /// 源表已删除环境维度列（Platform/Channel/DevelopValue/PublishValue），
    /// 过滤/合并逻辑随之整体删除；环境差异上移到 Config 三维矩阵承载（ADR-054 决策 8）。
    /// </summary>
    internal static class NetworkExcelPreFilter
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
        /// 对指定目录下所有 Network Excel 文件执行预处理（纯拷贝）。
        /// 排除 _configs/ 和 _temp/ 子目录下的文件，排除 ~$ 开头的临时文件。
        /// </summary>
        /// <param name="sourceDirPath">数据源目录路径（含 .xlsx 文件的目录）。</param>
        /// <param name="tempDirPath">临时输出目录路径（_temp/）。</param>
        public static void FilterAll(string sourceDirPath, string tempDirPath)
        {
            if (string.IsNullOrEmpty(sourceDirPath) || !Util.SysIO.Directory.Exists(sourceDirPath))
            {
                Log.Warning(LogTag.Editor, "Network 预处理：数据源目录不存在或为空：{0}", sourceDirPath ?? "null");
                return;
            }

            string[] files = EditorUtil.FileSystem.GetFiles(sourceDirPath, c_SearchPattern, c_ExcludePrefix);
            if (files == null || files.Length == 0)
            {
                Log.Debug(LogTag.Editor, "Network 预处理：数据源目录下未找到 .xlsx 文件：{0}", sourceDirPath);
                return;
            }

            string configsDirPath = Util.SysIO.Path.Combine(sourceDirPath, c_ConfigsDirName);
            string tempDirFullPath = Util.SysIO.Path.Combine(sourceDirPath, c_TempDirName);

            foreach (string filePath in files)
            {
                if (EditorUtil.FileSystem.IsSubPathOf(filePath, configsDirPath) || EditorUtil.FileSystem.IsSubPathOf(filePath, tempDirFullPath))
                {
                    continue;
                }

                FilterFile(filePath, tempDirPath);
            }
        }

        /// <summary>
        /// 对单个 Network Excel 文件执行预处理（纯拷贝）。
        /// 读取所有 Sheet，跳过 # 开头的 Sheet 与不足 5 行的 Sheet，其余 Sheet 原样写入 _temp。
        /// Luban 读取 schema 时会自行跳过 # 前缀隐藏列，无需在此剥列。
        /// </summary>
        /// <param name="excelFilePath">原始 Excel 文件绝对路径。</param>
        /// <param name="tempDirPath">临时输出目录路径。</param>
        public static void FilterFile(string excelFilePath, string tempDirPath)
        {
            if (string.IsNullOrEmpty(excelFilePath) || !Util.SysIO.File.Exists(excelFilePath))
            {
                Log.Warning(LogTag.Editor, "Network 预处理：Excel 文件不存在：{0}", excelFilePath ?? "null");
                return;
            }

            Dictionary<string, List<IReadOnlyList<string>>> allSheets = EditorUtil.Excel.ReadAllSheets(excelFilePath);

            if (allSheets == null || allSheets.Count == 0)
            {
                Log.Debug(LogTag.Editor, "Network 预处理：Excel 文件无有效 Sheet：{0}", excelFilePath);
                return;
            }

            var outputSheets = new Dictionary<string, List<IReadOnlyList<string>>>();

            foreach (var kvp in allSheets)
            {
                string sheetName = kvp.Key;
                List<IReadOnlyList<string>> rows = kvp.Value;

                if (sheetName.StartsWith("#") || rows == null || rows.Count < 5)
                {
                    continue;
                }

                outputSheets[sheetName] = rows;
            }

            if (outputSheets.Count == 0)
            {
                Log.Debug(LogTag.Editor, "Network 预处理：过滤后无有效数据，跳过输出：{0}", excelFilePath);
                return;
            }

            if (!Util.SysIO.Directory.Exists(tempDirPath))
            {
                Util.SysIO.Directory.CreateIfNotExist(tempDirPath);
            }

            string fileNameWithoutExt = Util.SysIO.Path.GetFileNameWithoutExtension(excelFilePath);
            string outputDir = Util.SysIO.Path.Combine(tempDirPath, fileNameWithoutExt);
            EditorUtil.Excel.Write(outputDir, outputSheets);
        }
    }
}
