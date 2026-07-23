/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.TrackRegistry.cs
 * author:    taoye
 * created:   2026/7/21
 * descrip:   打点 Excel 汇总工具
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using NovaFramework.Runtime;
using UnityEngine;
using IOPath = System.IO.Path;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 打点 Excel 汇总工具。
        /// </summary>
        public static partial class TrackRegistry
        {
            /// <summary>
            /// Framework UPM 包的固定包名。
            /// </summary>
            private const string c_FrameworkPackageName = "com.solotopia.nova.framework";

            /// <summary>
            /// 自动生成的本地打点汇总表相对工程根目录的路径。
            /// </summary>
            private const string c_OutputRelativePath = "Library/Nova/Tracks/Tracks.generated.xlsx";

            /// <summary>
            /// 本地 Framework 公共打点表相对工程根目录的路径。
            /// </summary>
            private const string c_AssetFrameworkTracksRelativePath = "Assets/Framework/Tracks/Tracks.xlsx";

            /// <summary>
            /// Framework 包内公共打点表相对包根目录的路径。
            /// </summary>
            private const string c_FrameworkPackageTracksRelativePath = "Tracks/Tracks.xlsx";

            /// <summary>
            /// 模块包内打点表相对包根目录的路径。
            /// </summary>
            private const string c_ModulePackageTracksRelativePath = "Nova/Tracks/Tracks.xlsx";

            /// <summary>
            /// 本地 UPM 工作区相对工程根目录的路径。
            /// </summary>
            private const string c_UpmPackageRootRelativePath = "UPMPackages";

            /// <summary>
            /// Unity Packages 相对工程根目录的路径。
            /// </summary>
            private const string c_PackageRootRelativePath = "Packages";

            /// <summary>
            /// 打点表扫描规则，数组顺序就是生成表追加 Sheet 的优先级顺序。
            /// </summary>
            private static readonly TrackWorkbookScanRule[] s_WorkbookScanRules =
            {
                new TrackWorkbookScanRule(TrackWorkbookSource.ProjectFile, c_AssetFrameworkTracksRelativePath, isFrameworkSheet: true),
                new TrackWorkbookScanRule(TrackWorkbookSource.FrameworkPackage, c_FrameworkPackageTracksRelativePath, isFrameworkSheet: true),
                new TrackWorkbookScanRule(TrackWorkbookSource.UpmPackages, c_ModulePackageTracksRelativePath, isFrameworkSheet: false),
                new TrackWorkbookScanRule(TrackWorkbookSource.Packages, c_ModulePackageTracksRelativePath, isFrameworkSheet: false),
            };

            /// <summary>
            /// 生成当前工程的打点汇总表。
            /// </summary>
            /// <param name="projectRoot">工程根目录。</param>
            /// <returns>生成的 xlsx 绝对路径。</returns>
            public static string Generate(string projectRoot)
            {
                if (string.IsNullOrEmpty(projectRoot))
                {
                    throw new ArgumentException("工程根目录为空。", nameof(projectRoot));
                }

                string normalizedRoot = IOPath.GetFullPath(projectRoot);
                List<TrackSheet> sheets = BuildSheets(normalizedRoot);
                if (sheets.Count == 0)
                {
                    throw new InvalidOperationException("未找到任何打点 Excel。");
                }

                string outputPath = IOPath.Combine(normalizedRoot, c_OutputRelativePath);
                Directory.CreateDirectory(IOPath.GetDirectoryName(outputPath));
                XlsxWriter.Write(outputPath, sheets);
                return outputPath;
            }

            /// <summary>
            /// 构建待写入汇总表的 Sheet 列表，先追加 Framework 公共 Sheet，再追加各模块的打点 Sheet。
            /// </summary>
            /// <param name="projectRoot">工程根目录的绝对路径。</param>
            /// <returns>按生成顺序排列的 Sheet 数据。</returns>
            private static List<TrackSheet> BuildSheets(string projectRoot)
            {
                var sheets = new List<TrackSheet>();
                var sourcesBySheetName = new Dictionary<string, string>(StringComparer.Ordinal);
                var registeredWorkbookPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                TrackWorkbookScanContext scanContext = CreateScanContext(projectRoot);

                foreach (TrackWorkbookScanRule rule in s_WorkbookScanRules)
                {
                    foreach (string workbookPath in EnumerateTrackWorkbooks(scanContext, rule))
                    {
                        if (!TryRegisterWorkbookPath(registeredWorkbookPaths, workbookPath))
                        {
                            continue;
                        }

                        AppendWorkbookSheets(sheets, sourcesBySheetName, workbookPath, rule.IsFrameworkSheet);
                    }
                }

                return sheets;
            }

            /// <summary>
            /// 尝试登记本次生成已经处理过的工作簿路径，避免同一物理表从多个扫描来源重复追加。
            /// </summary>
            /// <param name="registeredWorkbookPaths">已登记的规范化工作簿路径集合。</param>
            /// <param name="workbookPath">待登记的工作簿路径。</param>
            /// <returns>首次登记时返回 true；同一路径已存在时返回 false。</returns>
            private static bool TryRegisterWorkbookPath(HashSet<string> registeredWorkbookPaths, string workbookPath)
            {
                return registeredWorkbookPaths.Add(NormalizeWorkbookPath(workbookPath));
            }

            /// <summary>
            /// 规范化工作簿路径，用于跨扫描来源识别同一份打点表。
            /// </summary>
            /// <param name="workbookPath">待规范化的工作簿路径。</param>
            /// <returns>完整路径形式的工作簿路径。</returns>
            private static string NormalizeWorkbookPath(string workbookPath)
            {
                return IOPath.GetFullPath(workbookPath)
                    .TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
            }

            /// <summary>
            /// 创建单次生成过程中共享的打点表扫描上下文。
            /// </summary>
            /// <param name="projectRoot">工程根目录的绝对路径。</param>
            /// <returns>包含工程根目录和已缓存包根目录的扫描上下文。</returns>
            private static TrackWorkbookScanContext CreateScanContext(string projectRoot)
            {
                return new TrackWorkbookScanContext(
                    projectRoot,
                    new List<string>(EnumeratePhysicalPackageRoots(projectRoot, c_UpmPackageRootRelativePath)),
                    new List<string>(EnumeratePackageRoots(projectRoot)));
            }

            /// <summary>
            /// 根据扫描规则枚举对应来源下存在的打点工作簿。
            /// </summary>
            /// <param name="context">当前生成过程共享的扫描上下文。</param>
            /// <param name="rule">当前执行的扫描规则。</param>
            /// <returns>命中规则的打点工作簿绝对路径。</returns>
            private static IEnumerable<string> EnumerateTrackWorkbooks(TrackWorkbookScanContext context, TrackWorkbookScanRule rule)
            {
                switch (rule.Source)
                {
                    case TrackWorkbookSource.ProjectFile:
                        foreach (string workbookPath in EnumerateProjectFileWorkbook(context.ProjectRoot, rule.RelativePath))
                        {
                            yield return workbookPath;
                        }
                        break;
                    case TrackWorkbookSource.FrameworkPackage:
                        foreach (string workbookPath in EnumerateFrameworkPackageWorkbooks(context.PackageRoots, rule.RelativePath))
                        {
                            yield return workbookPath;
                        }
                        break;
                    case TrackWorkbookSource.UpmPackages:
                        foreach (string workbookPath in EnumeratePackageTrackWorkbooks(context.UpmPackageRoots, rule.RelativePath))
                        {
                            yield return workbookPath;
                        }
                        break;
                    case TrackWorkbookSource.Packages:
                        foreach (string workbookPath in EnumeratePackageTrackWorkbooks(context.PackageRoots, rule.RelativePath))
                        {
                            yield return workbookPath;
                        }
                        break;
                    default:
                        throw new InvalidOperationException(Txt.Format("未知的打点表扫描来源：{0}", rule.Source));
                }
            }

            /// <summary>
            /// 读取单个工作簿中的所有 Sheet，并追加到汇总列表中。
            /// </summary>
            /// <param name="sheets">已收集的汇总 Sheet 列表。</param>
            /// <param name="sourcesBySheetName">用于检查 Sheet 重名的来源路径索引。</param>
            /// <param name="workbookPath">待读取的工作簿绝对路径。</param>
            /// <param name="frameworkSheet">当前工作簿是否来自 Framework 公共打点表。</param>
            private static void AppendWorkbookSheets(
                List<TrackSheet> sheets,
                Dictionary<string, string> sourcesBySheetName,
                string workbookPath,
                bool frameworkSheet)
            {
                Dictionary<string, List<IReadOnlyList<string>>> rowsBySheet = EditorUtil.Excel.ReadAllSheets(workbookPath);
                foreach (KeyValuePair<string, List<IReadOnlyList<string>>> kvp in rowsBySheet)
                {
                    string sheetName = kvp.Key;
                    if (sourcesBySheetName.TryGetValue(sheetName, out string existingSource))
                    {
                        throw new InvalidOperationException(
                            Txt.Format("打点 Sheet 名重复：{0}\n来源 A：{1}\n来源 B：{2}", sheetName, existingSource, workbookPath));
                    }

                    sourcesBySheetName.Add(sheetName, workbookPath);
                    sheets.Add(new TrackSheet(sheetName, NormalizeRows(kvp.Value), workbookPath, frameworkSheet));
                }
            }

            /// <summary>
            /// 规范化 Excel 读取结果，移除每行末尾空单元格并把空引用转为空字符串。
            /// </summary>
            /// <param name="rows">从 Excel Sheet 读取到的原始行数据。</param>
            /// <returns>可直接写入生成表的行数据。</returns>
            private static List<IReadOnlyList<string>> NormalizeRows(List<IReadOnlyList<string>> rows)
            {
                var result = new List<IReadOnlyList<string>>(rows.Count);
                foreach (IReadOnlyList<string> row in rows)
                {
                    int count = row.Count;
                    while (count > 0 && string.IsNullOrEmpty(row[count - 1]))
                    {
                        count--;
                    }

                    var normalized = new List<string>(count);
                    for (int i = 0; i < count; i++)
                    {
                        normalized.Add(row[i] ?? string.Empty);
                    }

                    result.Add(normalized);
                }

                return result;
            }

            /// <summary>
            /// 枚举工程固定路径下的单个打点工作簿。
            /// </summary>
            /// <param name="projectRoot">工程根目录的绝对路径。</param>
            /// <param name="relativePath">工作簿相对工程根目录的路径。</param>
            /// <returns>存在时返回该工作簿绝对路径，否则返回空序列。</returns>
            private static IEnumerable<string> EnumerateProjectFileWorkbook(string projectRoot, string relativePath)
            {
                string workbookPath = IOPath.Combine(projectRoot, relativePath);
                if (File.Exists(workbookPath))
                {
                    yield return workbookPath;
                }
            }

            /// <summary>
            /// 枚举 Framework 包根目录下的公共打点工作簿。
            /// </summary>
            /// <param name="packageRoots">候选包根目录列表。</param>
            /// <param name="relativePath">工作簿相对包根目录的路径。</param>
            /// <returns>Framework 包内存在的公共打点工作簿绝对路径。</returns>
            private static IEnumerable<string> EnumerateFrameworkPackageWorkbooks(IEnumerable<string> packageRoots, string relativePath)
            {
                foreach (string packageRoot in packageRoots)
                {
                    if (!IsFrameworkPackageRoot(packageRoot))
                    {
                        continue;
                    }

                    string workbookPath = IOPath.Combine(packageRoot, relativePath);
                    if (File.Exists(workbookPath))
                    {
                        yield return workbookPath;
                    }
                }
            }

            /// <summary>
            /// 枚举一组包根目录下的模块打点工作簿。
            /// </summary>
            /// <param name="packageRoots">候选包根目录列表。</param>
            /// <param name="relativePath">工作簿相对包根目录的路径。</param>
            /// <returns>包内存在的模块打点工作簿绝对路径。</returns>
            private static IEnumerable<string> EnumeratePackageTrackWorkbooks(IEnumerable<string> packageRoots, string relativePath)
            {
                foreach (string packageRoot in packageRoots)
                {
                    string workbookPath = IOPath.Combine(packageRoot, relativePath);
                    if (File.Exists(workbookPath))
                    {
                        yield return workbookPath;
                    }
                }
            }

            /// <summary>
            /// 枚举 Packages 包根目录，先读工程内物理 Packages 目录，再读取 Unity 已解析包路径。
            /// </summary>
            /// <param name="projectRoot">工程根目录的绝对路径。</param>
            /// <returns>Packages 中的包根目录绝对路径。</returns>
            private static IEnumerable<string> EnumeratePackageRoots(string projectRoot)
            {
                var emitted = new HashSet<string>(StringComparer.Ordinal);

                foreach (string packageRoot in EnumeratePhysicalPackageRoots(projectRoot, c_PackageRootRelativePath))
                {
                    if (emitted.Add(packageRoot))
                    {
                        yield return packageRoot;
                    }
                }

                if (!IsCurrentUnityProject(projectRoot))
                {
                    yield break;
                }

                PackageInfo[] registeredPackages = PackageInfo.GetAllRegisteredPackages();
                Array.Sort(registeredPackages, (left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));
                foreach (PackageInfo packageInfo in registeredPackages)
                {
                    if (string.IsNullOrEmpty(packageInfo.assetPath) ||
                        !packageInfo.assetPath.StartsWith(c_PackageRootRelativePath + "/", StringComparison.Ordinal) ||
                        string.IsNullOrEmpty(packageInfo.resolvedPath) ||
                        !Directory.Exists(packageInfo.resolvedPath))
                    {
                        continue;
                    }

                    if (emitted.Add(packageInfo.resolvedPath))
                    {
                        yield return packageInfo.resolvedPath;
                    }
                }
            }

            /// <summary>
            /// 枚举指定工程相对根目录下的一级包目录。
            /// </summary>
            /// <param name="projectRoot">工程根目录的绝对路径。</param>
            /// <param name="rootRelativePath">包根目录相对工程根目录的路径。</param>
            /// <returns>按目录名排序后的一级包目录绝对路径。</returns>
            private static IEnumerable<string> EnumeratePhysicalPackageRoots(string projectRoot, string rootRelativePath)
            {
                string moduleRoot = IOPath.Combine(projectRoot, rootRelativePath);
                if (!Directory.Exists(moduleRoot))
                {
                    yield break;
                }

                string[] packageRoots = Directory.GetDirectories(moduleRoot);
                Array.Sort(packageRoots, StringComparer.Ordinal);
                foreach (string packageRoot in packageRoots)
                {
                    yield return packageRoot;
                }
            }

            /// <summary>
            /// 判断传入工程根目录是否为当前 Unity Editor 打开的工程。
            /// </summary>
            /// <param name="projectRoot">工程根目录的绝对路径。</param>
            /// <returns>与 Application.dataPath 所属工程一致时返回 true。</returns>
            private static bool IsCurrentUnityProject(string projectRoot)
            {
                string currentProjectRoot = IOPath.GetFullPath(IOPath.Combine(Application.dataPath, ".."));
                return string.Equals(
                    TrimDirectorySeparator(IOPath.GetFullPath(projectRoot)),
                    TrimDirectorySeparator(currentProjectRoot),
                    StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// 移除路径末尾的目录分隔符，便于不同来源的工程根目录做等值比较。
            /// </summary>
            /// <param name="path">待处理路径。</param>
            /// <returns>不含末尾目录分隔符的路径。</returns>
            private static string TrimDirectorySeparator(string path)
            {
                string root = IOPath.GetPathRoot(path);
                return path.Length > root.Length
                    ? path.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar)
                    : path;
            }

            /// <summary>
            /// 判断包根目录是否为 Framework 包，兼容 PackageCache 中带 hash 后缀的目录名。
            /// </summary>
            /// <param name="packageRoot">包根目录绝对路径。</param>
            /// <returns>是否为 `com.solotopia.nova.framework` 包目录。</returns>
            private static bool IsFrameworkPackageRoot(string packageRoot)
            {
                string packageDirectoryName = new DirectoryInfo(packageRoot).Name;
                int versionSeparatorIndex = packageDirectoryName.IndexOf('@');
                if (versionSeparatorIndex >= 0)
                {
                    packageDirectoryName = packageDirectoryName.Substring(0, versionSeparatorIndex);
                }

                return string.Equals(packageDirectoryName, c_FrameworkPackageName, StringComparison.Ordinal);
            }

            /// <summary>
            /// 打点工作簿的扫描来源类型。
            /// </summary>
            private enum TrackWorkbookSource
            {
                /// <summary>
                /// 工程根目录下的固定文件。
                /// </summary>
                ProjectFile,

                /// <summary>
                /// 已解析的 Framework 包目录。
                /// </summary>
                FrameworkPackage,

                /// <summary>
                /// 本地 UPMPackages 工作区中的包目录。
                /// </summary>
                UpmPackages,

                /// <summary>
                /// Unity Packages 中的包目录。
                /// </summary>
                Packages,
            }

            /// <summary>
            /// 表示一次打点表生成过程中的扫描上下文。
            /// </summary>
            private readonly struct TrackWorkbookScanContext
            {
                /// <summary>
                /// 创建打点表扫描上下文。
                /// </summary>
                /// <param name="projectRoot">工程根目录的绝对路径。</param>
                /// <param name="upmPackageRoots">已缓存的 UPMPackages 包根目录列表。</param>
                /// <param name="packageRoots">已缓存的 Packages 包根目录列表。</param>
                public TrackWorkbookScanContext(string projectRoot, IReadOnlyList<string> upmPackageRoots, IReadOnlyList<string> packageRoots)
                {
                    ProjectRoot = projectRoot;
                    UpmPackageRoots = upmPackageRoots;
                    PackageRoots = packageRoots;
                }

                /// <summary>
                /// 工程根目录的绝对路径。
                /// </summary>
                public string ProjectRoot { get; }

                /// <summary>
                /// 已缓存的 UPMPackages 包根目录列表。
                /// </summary>
                public IReadOnlyList<string> UpmPackageRoots { get; }

                /// <summary>
                /// 已缓存的 Packages 包根目录列表。
                /// </summary>
                public IReadOnlyList<string> PackageRoots { get; }
            }

            /// <summary>
            /// 表示一条打点工作簿扫描规则。
            /// </summary>
            private readonly struct TrackWorkbookScanRule
            {
                /// <summary>
                /// 创建打点工作簿扫描规则。
                /// </summary>
                /// <param name="source">扫描来源。</param>
                /// <param name="relativePath">工作簿相对来源根目录的路径。</param>
                /// <param name="isFrameworkSheet">命中的工作簿是否按 Framework 公共表处理。</param>
                public TrackWorkbookScanRule(TrackWorkbookSource source, string relativePath, bool isFrameworkSheet)
                {
                    Source = source;
                    RelativePath = relativePath;
                    IsFrameworkSheet = isFrameworkSheet;
                }

                /// <summary>
                /// 扫描来源。
                /// </summary>
                public TrackWorkbookSource Source { get; }

                /// <summary>
                /// 工作簿相对来源根目录的路径。
                /// </summary>
                public string RelativePath { get; }

                /// <summary>
                /// 命中的工作簿是否按 Framework 公共表处理。
                /// </summary>
                public bool IsFrameworkSheet { get; }
            }

            /// <summary>
            /// 表示一个待写入生成工作簿的 Sheet 及其来源信息。
            /// </summary>
            private readonly struct TrackSheet
            {
                /// <summary>
                /// 创建待写入的 Sheet 数据对象。
                /// </summary>
                /// <param name="name">Sheet 名称。</param>
                /// <param name="rows">Sheet 的行数据。</param>
                /// <param name="sourcePath">该 Sheet 所属源工作簿的绝对路径。</param>
                /// <param name="isFrameworkSheet">该 Sheet 是否来自 Framework 公共打点表。</param>
                public TrackSheet(string name, List<IReadOnlyList<string>> rows, string sourcePath, bool isFrameworkSheet)
                {
                    Name = name;
                    Rows = rows;
                    SourcePath = sourcePath;
                    IsFrameworkSheet = isFrameworkSheet;
                }

                /// <summary>
                /// Sheet 名称。
                /// </summary>
                public string Name { get; }

                /// <summary>
                /// Sheet 行数据。
                /// </summary>
                public List<IReadOnlyList<string>> Rows { get; }

                /// <summary>
                /// 源工作簿绝对路径。
                /// </summary>
                public string SourcePath { get; }

                /// <summary>
                /// 是否为 Framework 公共打点表中的 Sheet。
                /// </summary>
                public bool IsFrameworkSheet { get; }
            }
        }
    }
}
