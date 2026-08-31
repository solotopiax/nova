/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.CDN.LatestVersion.cs
 * author:    Codex
 * created:   2026/8/7
 * descrip:   YooAsset 本地构建版本目录识别与最新版本解析
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using YooAsset;
using YooAsset.Editor;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class CDN
        {
            /// <summary>
            /// 从指定 ConfigMaster 当前坐标解析 YooAsset PackageFilePrefix 模板。
            /// </summary>
            internal static string ResolvePackageFilePrefix(
                ConfigMasterSO master,
                PlatformType platform,
                ChannelType channel,
                DevelopMode mode,
                string packageName,
                string appVersion,
                DateTime time)
            {
                if (master == null) throw new ArgumentNullException(nameof(master));
                Config.DimensionalResolver.YooAssetResult yooAsset =
                    Config.DimensionalResolver.ResolveYooAsset(master, platform, channel, mode);
                if (!string.IsNullOrEmpty(yooAsset.PackageFilePrefix)
                    && yooAsset.PackageFilePrefix.Contains("{Time}", StringComparison.Ordinal))
                {
                    YooAssetSettings settings = AssetDatabase.LoadAssetAtPath<YooAssetSettings>(
                        yooAsset.YooAssetSettingsPath);
                    if (settings == null)
                    {
                        throw new InvalidOperationException(
                            $"PackageFilePrefix 包含 {{Time}}，但无法读取构建时实际 YooAssetSettings：{yooAsset.YooAssetSettingsPath}");
                    }

                    // {Time} 在构建导出时已固化；部署必须复用实际产物前缀，不能按部署时间重新计算。
                    return settings.PackageFilePrefix ?? string.Empty;
                }

                PlaceholderContext context = EditorUtil.Placeholder.FromConfigMaster(
                    master,
                    platform,
                    channel,
                    packageName,
                    appVersion,
                    time);
                return Util.Placeholder.Resolve(yooAsset.PackageFilePrefix, context) ?? string.Empty;
            }

            /// <summary>
            /// 校验手工指定的项目内 CDN 部署目录。
            /// </summary>
            internal static bool TryValidateManualLocalDirectory(
                string configuredRelativePath,
                string projectRoot,
                out string error)
            {
                error = string.Empty;
                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    error = "无法解析 Unity 项目根目录。";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(configuredRelativePath))
                {
                    error = "热更资源本地目录位置不能为空。";
                    return false;
                }

                try
                {
                    string normalizedRoot = IOPath.GetFullPath(projectRoot);
                    string fullPath = IOPath.GetFullPath(IOPath.Combine(normalizedRoot, configuredRelativePath));
                    if (!IsPathInsideRoot(fullPath, normalizedRoot))
                    {
                        error = $"热更资源本地目录必须位于 Unity 项目根目录内：{fullPath}";
                        return false;
                    }
                    if (!Directory.Exists(fullPath))
                    {
                        error = $"热更资源本地目录不存在：{fullPath}";
                        return false;
                    }
                    if (TryFindReparsePointInTree(fullPath, normalizedRoot, out string reparsePoint))
                    {
                        error = $"热更资源本地目录不允许包含符号链接或 junction：{reparsePoint}";
                        return false;
                    }
                    return true;
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is NotSupportedException ||
                    exception is IOException ||
                    exception is UnauthorizedAccessException)
                {
                    error = $"热更资源本地目录路径无效：{configuredRelativePath}。{exception.Message}";
                    return false;
                }
            }

            /// <summary>
            /// 校验手工指定的项目内 YooAsset 版本文件。
            /// </summary>
            internal static bool TryValidateManualLocalFile(
                string configuredRelativePath,
                string projectRoot,
                string expectedExtension,
                out string error)
            {
                error = string.Empty;
                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    error = "无法解析 Unity 项目根目录。";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(configuredRelativePath))
                {
                    error = $"版本文件({expectedExtension})本地文件位置不能为空。";
                    return false;
                }

                try
                {
                    string normalizedRoot = IOPath.GetFullPath(projectRoot);
                    string fullPath = IOPath.GetFullPath(IOPath.Combine(normalizedRoot, configuredRelativePath));
                    if (!IsPathInsideRoot(fullPath, normalizedRoot))
                    {
                        error = $"版本文件({expectedExtension})必须位于 Unity 项目根目录内：{fullPath}";
                        return false;
                    }
                    if (!File.Exists(fullPath))
                    {
                        error = $"版本文件({expectedExtension})不存在：{fullPath}";
                        return false;
                    }
                    if (TryFindReparsePointInPath(fullPath, normalizedRoot, out string reparsePoint))
                    {
                        error = $"版本文件({expectedExtension})不允许包含符号链接或 junction：{reparsePoint}";
                        return false;
                    }
                    if (!string.Equals(
                            IOPath.GetExtension(fullPath),
                            expectedExtension,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"版本文件扩展名必须为 {expectedExtension}：{fullPath}";
                        return false;
                    }
                    return true;
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is NotSupportedException ||
                    exception is IOException ||
                    exception is UnauthorizedAccessException)
                {
                    error = $"版本文件({expectedExtension})路径无效：{configuredRelativePath}。{exception.Message}";
                    return false;
                }
            }

            /// <summary>
            /// 从配置目录或其父目录中选择最后生成的完整 YooAsset 版本目录。
            /// PackageVersion 是任意字符串，因此只按版本指针文件最后写入时间排序。
            /// </summary>
            /// <param name="configuredRelativePath">项目根相对的包根目录或具体版本目录。</param>
            /// <param name="projectRoot">Unity 项目根绝对路径。</param>
            /// <param name="packageName">YooAsset 资源包名。</param>
            /// <param name="resolvedRelativePath">成功时返回项目根相对的最新版本目录。</param>
            /// <param name="error">失败时返回包含实际扫描路径的错误。</param>
            /// <returns>是否找到完整版本目录。</returns>
            internal static bool TryResolveLatestPackageDirectory(
                string configuredRelativePath,
                string projectRoot,
                string packageName,
                out string resolvedRelativePath,
                out string error)
            {
                return TryResolveLatestPackageDirectory(
                    configuredRelativePath,
                    projectRoot,
                    packageName,
                    string.Empty,
                    out resolvedRelativePath,
                    out error);
            }

            /// <summary>
            /// 使用调用方显式提供的 PackageFilePrefix 解析最新完整版本目录。
            /// </summary>
            internal static bool TryResolveLatestPackageDirectory(
                string configuredRelativePath,
                string projectRoot,
                string packageName,
                string packageFilePrefix,
                out string resolvedRelativePath,
                out string error)
            {
                resolvedRelativePath = string.Empty;
                error = string.Empty;

                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    error = "无法解析 Unity 项目根目录。";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(packageName))
                {
                    error = "自动关联最新版本失败：YooAsset 资源包名为空。";
                    return false;
                }

                string normalizedRoot = IOPath.GetFullPath(projectRoot);
                string configuredFullPath = IOPath.GetFullPath(
                    IOPath.Combine(normalizedRoot, configuredRelativePath ?? string.Empty));
                if (!IsPathInsideRoot(configuredFullPath, normalizedRoot))
                {
                    error = $"自动关联最新版本失败，本地目录必须位于 Unity 项目根目录内：{configuredFullPath}";
                    return false;
                }
                if (TryFindReparsePointInPath(configuredFullPath, normalizedRoot, out string configuredLink))
                {
                    error = $"自动关联最新版本失败，本地目录不允许包含符号链接或 junction：{configuredLink}";
                    return false;
                }

                try
                {
                    IReadOnlyList<VersionDirectoryCandidate> candidates = FindVersionDirectories(
                        configuredFullPath,
                        packageName,
                        packageFilePrefix);
                    string packageRoot = configuredFullPath;

                    if (candidates.Count == 0 && ShouldSearchParent(
                            configuredFullPath,
                            packageName,
                            packageFilePrefix))
                    {
                        string parent = IOPath.GetDirectoryName(configuredFullPath);
                        if (!string.IsNullOrEmpty(parent) && IsPathInsideRoot(parent, normalizedRoot))
                        {
                            packageRoot = parent;
                            candidates = FindVersionDirectories(packageRoot, packageName, packageFilePrefix);
                        }
                    }

                    if (candidates.Count == 0)
                    {
                        error = "自动关联最新版本失败，当前目录下没有有效的YooAsset版本目录，请检查。";
                        return false;
                    }

                    DateTime latestWriteTime = candidates.Max(candidate => candidate.VersionFileWriteTimeUtc);
                    VersionDirectoryCandidate[] latestCandidates = candidates
                        .Where(candidate => candidate.VersionFileWriteTimeUtc == latestWriteTime)
                        .ToArray();
                    if (latestCandidates.Length > 1)
                    {
                        string names = string.Join(", ", latestCandidates
                            .Select(candidate => candidate.DirectoryName)
                            .OrderBy(name => name, StringComparer.Ordinal));
                        error = $"自动关联最新版本失败，多个有效版本的 .version 写入时间相同，无法判断最新版本：{names}。目录：{packageRoot}";
                        return false;
                    }

                    VersionDirectoryCandidate latest = latestCandidates[0];
                    resolvedRelativePath = IOPath.GetRelativePath(normalizedRoot, latest.FullPath).Replace('\\', '/');
                    return true;
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    error = $"自动关联最新版本失败，无法读取目录：{configuredFullPath}。{exception.Message}";
                    return false;
                }
            }

            /// <summary>
            /// 以已配置的 .bytes 文件父目录为锚点，解析最新完整 YooAsset 版本对应的三个元数据文件。
            /// </summary>
            internal static bool TryResolveLatestAssetCheckVersionFiles(
                string configuredBytesRelativePath,
                string projectRoot,
                string packageName,
                out string bytesRelativePath,
                out string hashRelativePath,
                out string versionRelativePath,
                out string error)
            {
                return TryResolveLatestAssetCheckVersionFiles(
                    configuredBytesRelativePath,
                    projectRoot,
                    packageName,
                    string.Empty,
                    out bytesRelativePath,
                    out hashRelativePath,
                    out versionRelativePath,
                    out error);
            }

            /// <summary>
            /// 使用调用方显式提供的 PackageFilePrefix 解析最新版本的三个元数据文件。
            /// </summary>
            internal static bool TryResolveLatestAssetCheckVersionFiles(
                string configuredBytesRelativePath,
                string projectRoot,
                string packageName,
                string packageFilePrefix,
                out string bytesRelativePath,
                out string hashRelativePath,
                out string versionRelativePath,
                out string error)
            {
                bytesRelativePath = string.Empty;
                hashRelativePath = string.Empty;
                versionRelativePath = string.Empty;
                error = string.Empty;

                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    error = "白名单自动关联最新版本失败：无法解析 Unity 项目根目录。";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(configuredBytesRelativePath))
                {
                    error = "白名单自动关联最新版本失败：版本文件(.bytes)本地文件位置为空。";
                    return false;
                }

                string normalizedRoot = IOPath.GetFullPath(projectRoot);
                string configuredBytesFullPath = IOPath.GetFullPath(
                    IOPath.Combine(normalizedRoot, configuredBytesRelativePath));
                if (!IsPathInsideRoot(configuredBytesFullPath, normalizedRoot))
                {
                    error = $"白名单自动关联最新版本失败，.bytes 文件必须位于 Unity 项目根目录内：{configuredBytesFullPath}";
                    return false;
                }

                string configuredDirectory = IOPath.GetDirectoryName(configuredBytesFullPath);
                if (string.IsNullOrEmpty(configuredDirectory))
                {
                    error = $"白名单自动关联最新版本失败，无法解析 .bytes 文件所在目录：{configuredBytesFullPath}";
                    return false;
                }
                string configuredDirectoryRelativePath = IOPath.GetRelativePath(
                    normalizedRoot,
                    configuredDirectory);
                if (!TryResolveLatestPackageDirectory(
                        configuredDirectoryRelativePath,
                        normalizedRoot,
                        packageName,
                        packageFilePrefix,
                        out string latestDirectoryRelativePath,
                        out error))
                {
                    return false;
                }

                string latestDirectoryFullPath = IOPath.GetFullPath(
                    IOPath.Combine(normalizedRoot, latestDirectoryRelativePath));
                string packageVersion = IOPath.GetFileName(latestDirectoryFullPath.TrimEnd(
                    IOPath.DirectorySeparatorChar,
                    IOPath.AltDirectorySeparatorChar));
                string bytesFullPath = IOPath.Combine(
                    latestDirectoryFullPath,
                    GetManifestBinaryFileName(packageName, packageVersion, packageFilePrefix));
                string hashFullPath = IOPath.Combine(
                    latestDirectoryFullPath,
                    GetPackageHashFileName(packageName, packageVersion, packageFilePrefix));
                string versionFullPath = IOPath.Combine(
                    latestDirectoryFullPath,
                    GetPackageVersionFileName(packageName, packageFilePrefix));
                if (!File.Exists(bytesFullPath) || !File.Exists(hashFullPath) || !File.Exists(versionFullPath))
                {
                    error = $"白名单自动关联最新版本失败，最新版本的 YooAsset 元数据文件不完整：{latestDirectoryFullPath}";
                    return false;
                }

                bytesRelativePath = IOPath.GetRelativePath(normalizedRoot, bytesFullPath).Replace('\\', '/');
                hashRelativePath = IOPath.GetRelativePath(normalizedRoot, hashFullPath).Replace('\\', '/');
                versionRelativePath = IOPath.GetRelativePath(normalizedRoot, versionFullPath).Replace('\\', '/');
                return true;
            }

            private static bool ShouldSearchParent(
                string configuredFullPath,
                string packageName,
                string packageFilePrefix)
            {
                if (!Directory.Exists(configuredFullPath)) return true;
                if (IsCompleteVersionDirectory(
                        configuredFullPath,
                        packageName,
                        packageFilePrefix,
                        out _)) return true;
                string versionFile = IOPath.Combine(
                    configuredFullPath,
                    GetPackageVersionFileName(packageName, packageFilePrefix));
                return File.Exists(versionFile);
            }

            private static IReadOnlyList<VersionDirectoryCandidate> FindVersionDirectories(
                string packageRoot,
                string packageName,
                string packageFilePrefix)
            {
                if (!Directory.Exists(packageRoot)) return Array.Empty<VersionDirectoryCandidate>();

                var result = new List<VersionDirectoryCandidate>();
                foreach (string directory in Directory.GetDirectories(packageRoot))
                {
                    if (IsCompleteVersionDirectory(
                            directory,
                            packageName,
                            packageFilePrefix,
                            out VersionDirectoryCandidate candidate))
                        result.Add(candidate);
                }
                return result;
            }

            private static bool IsCompleteVersionDirectory(
                string directory,
                string packageName,
                string packageFilePrefix,
                out VersionDirectoryCandidate candidate)
            {
                candidate = default;
                if (!Directory.Exists(directory)) return false;
                if (IsReparsePoint(directory)) return false;

                string version = IOPath.GetFileName(directory.TrimEnd(
                    IOPath.DirectorySeparatorChar,
                    IOPath.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(version)) return false;

                string versionFile = IOPath.Combine(
                    directory,
                    GetPackageVersionFileName(packageName, packageFilePrefix));
                if (!File.Exists(versionFile)) return false;
                if (!string.Equals(File.ReadAllText(versionFile).Trim(), version, StringComparison.Ordinal)) return false;

                string manifestFile = IOPath.Combine(
                    directory,
                    GetManifestBinaryFileName(packageName, version, packageFilePrefix));
                string hashFile = IOPath.Combine(
                    directory,
                    GetPackageHashFileName(packageName, version, packageFilePrefix));
                string reportFile = IOPath.Combine(
                    directory,
                    GetBuildReportFileName(packageName, version, packageFilePrefix));
                if (!File.Exists(manifestFile) || !File.Exists(hashFile) || !File.Exists(reportFile)) return false;

                BuildReport report;
                try
                {
                    report = BuildReport.Deserialize(File.ReadAllText(reportFile));
                }
                catch (ArgumentException)
                {
                    return false;
                }
                if (report?.Summary == null || report.BundleInfos == null) return false;
                if (!string.Equals(report.Summary.BuildPackageName, packageName, StringComparison.Ordinal) ||
                    !string.Equals(report.Summary.BuildPackageVersion, version, StringComparison.Ordinal))
                {
                    return false;
                }
                foreach (ReportBundleInfo bundle in report.BundleInfos)
                {
                    if (bundle == null || string.IsNullOrWhiteSpace(bundle.FileName)) return false;
                    string bundlePath = IOPath.GetFullPath(IOPath.Combine(directory, bundle.FileName));
                    if (!IsPathInsideRoot(bundlePath, directory) || !File.Exists(bundlePath)) return false;
                    if (TryFindReparsePointInPath(bundlePath, directory, out _)) return false;
                }

                candidate = new VersionDirectoryCandidate(
                    directory,
                    version,
                    File.GetLastWriteTimeUtc(versionFile));
                return true;
            }

            /// <summary>
            /// 从完整 YooAsset 版本目录生成运行时上传白名单。
            /// 未发现匹配的 version 文件时视为普通目录；发现但目录不完整时返回明确错误。
            /// </summary>
            private static bool TryGetYooAssetRuntimeUploadFiles(
                string directory,
                string packageName,
                string packageFilePrefix,
                out string[] files,
                out string error)
            {
                files = Array.Empty<string>();
                error = string.Empty;
                if (string.IsNullOrWhiteSpace(packageName)) return false;

                string versionFile = IOPath.Combine(
                    directory,
                    GetPackageVersionFileName(packageName, packageFilePrefix));
                if (!File.Exists(versionFile)) return false;
                if (!IsCompleteVersionDirectory(
                        directory,
                        packageName,
                        packageFilePrefix,
                        out VersionDirectoryCandidate candidate))
                {
                    error = $"YooAsset 版本目录不完整或无效，请检查：{directory}";
                    return false;
                }

                string reportFile = IOPath.Combine(
                    directory,
                    GetBuildReportFileName(packageName, candidate.DirectoryName, packageFilePrefix));
                BuildReport report = BuildReport.Deserialize(File.ReadAllText(reportFile));
                var runtimeFiles = new List<string>(report.BundleInfos.Count + 3)
                {
                    versionFile,
                    IOPath.Combine(
                        directory,
                        GetPackageHashFileName(packageName, candidate.DirectoryName, packageFilePrefix)),
                    IOPath.Combine(
                        directory,
                        GetManifestBinaryFileName(packageName, candidate.DirectoryName, packageFilePrefix)),
                };
                runtimeFiles.AddRange(report.BundleInfos.Select(bundle =>
                    IOPath.GetFullPath(IOPath.Combine(directory, bundle.FileName))));
                files = runtimeFiles.Distinct(StringComparer.Ordinal).ToArray();
                return true;
            }

            private static string GetPackageVersionFileName(string packageName, string packageFilePrefix)
            {
                return $"{GetPackageFilePrefix(packageFilePrefix)}{packageName}.version";
            }

            private static string GetManifestBinaryFileName(
                string packageName,
                string packageVersion,
                string packageFilePrefix)
            {
                return $"{GetPackageFilePrefix(packageFilePrefix)}{packageName}_{packageVersion}.bytes";
            }

            private static string GetPackageHashFileName(
                string packageName,
                string packageVersion,
                string packageFilePrefix)
            {
                return $"{GetPackageFilePrefix(packageFilePrefix)}{packageName}_{packageVersion}.hash";
            }

            private static string GetBuildReportFileName(
                string packageName,
                string packageVersion,
                string packageFilePrefix)
            {
                return $"{GetPackageFilePrefix(packageFilePrefix)}{packageName}_{packageVersion}.report";
            }

            private static string GetPackageFilePrefix(string packageFilePrefix)
            {
                return string.IsNullOrEmpty(packageFilePrefix) ? string.Empty : packageFilePrefix + "_";
            }

            /// <summary>
            /// 检查部署目录及其内容，拒绝可能把访问范围引到项目根外的符号链接或 junction。
            /// </summary>
            internal static bool TryFindReparsePointInTree(
                string localRoot,
                string projectRoot,
                out string reparsePoint)
            {
                reparsePoint = string.Empty;
                if (TryFindReparsePointInPath(localRoot, projectRoot, out reparsePoint)) return true;
                if (!Directory.Exists(localRoot)) return false;

                var pending = new Stack<string>();
                pending.Push(localRoot);
                while (pending.Count > 0)
                {
                    string current = pending.Pop();
                    foreach (FileSystemInfo entry in new DirectoryInfo(current).GetFileSystemInfos())
                    {
                        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            reparsePoint = entry.FullName;
                            return true;
                        }
                        if (entry is DirectoryInfo)
                            pending.Push(entry.FullName);
                    }
                }
                return false;
            }

            private static bool TryFindReparsePointInPath(
                string path,
                string root,
                out string reparsePoint)
            {
                reparsePoint = string.Empty;
                string normalizedRoot = IOPath.GetFullPath(root);
                string current = IOPath.GetFullPath(path);
                while (IsPathInsideRoot(current, normalizedRoot))
                {
                    if ((File.Exists(current) || Directory.Exists(current)) && IsReparsePoint(current))
                    {
                        reparsePoint = current;
                        return true;
                    }
                    if (string.Equals(current, normalizedRoot, StringComparison.Ordinal)) break;
                    current = IOPath.GetDirectoryName(current);
                    if (string.IsNullOrEmpty(current)) break;
                }
                return false;
            }

            private static bool IsReparsePoint(string path)
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }

            private readonly struct VersionDirectoryCandidate
            {
                internal VersionDirectoryCandidate(
                    string fullPath,
                    string directoryName,
                    DateTime versionFileWriteTimeUtc)
                {
                    FullPath = fullPath;
                    DirectoryName = directoryName;
                    VersionFileWriteTimeUtc = versionFileWriteTimeUtc;
                }

                internal string FullPath { get; }
                internal string DirectoryName { get; }
                internal DateTime VersionFileWriteTimeUtc { get; }
            }
        }
    }
}
