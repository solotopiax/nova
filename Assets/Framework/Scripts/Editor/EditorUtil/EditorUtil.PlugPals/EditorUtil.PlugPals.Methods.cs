/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.PlugPals.Methods.cs
 * author:    taoye
 * created:   2026/4/21
 * descrip:   PlugPals 工具 —— 私有实现方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class PlugPals
        {
            /// <summary>
            /// 根据 packages-lock.json 条目的来源类型解析实际版本号。
            /// </summary>
            /// <param name="packageName">包名。</param>
            /// <param name="entry">lock 文件条目。</param>
            /// <returns>实际版本号字符串，无法解析返回 null。</returns>
            private static string ResolveEntryVersion(string packageName, PackagesLockEntry entry)
            {
                if (entry.source == "local" && entry.version.StartsWith("file:"))
                {
                    string relativePath = entry.version.Substring(5);
                    string packagesDir = Util.SysIO.Path.Combine(Application.dataPath, "../", "Packages");
                    string packageJsonPath = Util.SysIO.Path.Combine(packagesDir, relativePath, "package.json");
                    string fullPath = Util.SysIO.Path.GetFullPath(packageJsonPath);

                    if (Util.SysIO.File.Exists(fullPath))
                    {
                        try
                        {
                            string json = Util.SysIO.File.ReadAllTextSync(fullPath);
                            LocalPackageJson pkgJson = Util.Json.Deserialize<LocalPackageJson>(json);
                            return pkgJson?.version;
                        }
                        catch (Exception e)
                        {
                            Log.Warning(LogTag.Editor, "PlugPals.ResolveEntryVersion 读取 file: 引用包版本失败: {0}", e.Message);
                            return null;
                        }
                    }

                    return null;
                }

                if (entry.source == "git")
                {
                    return GetCachedPackageVersion(packageName);
                }

                // registry / builtin / embedded 等直接使用版本号
                return entry.version;
            }

            /// <summary>
            /// 从 Library/PackageCache 中读取指定包的实际安装版本（适用于 git/http 引用）。
            /// </summary>
            /// <param name="packageName">包名。</param>
            /// <returns>实际版本号，未找到返回 null。</returns>
            private static string GetCachedPackageVersion(string packageName)
            {
                string cacheDir = Util.SysIO.Path.Combine(Application.dataPath, "../", "Library/PackageCache");
                string fullCacheDir = Util.SysIO.Path.GetFullPath(cacheDir);

                if (!Util.SysIO.Directory.Exists(fullCacheDir))
                {
                    return null;
                }

                try
                {
                    string[] dirs = Util.SysIO.Directory.GetDirectories(fullCacheDir, packageName + "@*", System.IO.SearchOption.TopDirectoryOnly);
                    if (dirs.Length == 0)
                    {
                        return null;
                    }

                    string packageJsonPath = Util.SysIO.Path.Combine(dirs[0], "package.json");
                    if (Util.SysIO.File.Exists(packageJsonPath))
                    {
                        string json = Util.SysIO.File.ReadAllTextSync(packageJsonPath);
                        LocalPackageJson pkgJson = Util.Json.Deserialize<LocalPackageJson>(json);
                        return pkgJson?.version;
                    }
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "PlugPals.GetCachedPackageVersion 读取缓存包版本失败: {0}", e.Message);
                }

                return null;
            }

            /// <summary>
            /// 判断指定包在 manifest.json 中是否为非仓库引用（git/http/file:）。
            /// </summary>
            /// <param name="packageName">包名。</param>
            /// <param name="manifest">manifest 数据。</param>
            /// <returns>若为 git/http/file: 引用返回 true。</returns>
            private static bool IsNonRegistryReference(string packageName, ManifestData manifest)
            {
                if (manifest?.dependencies == null || !manifest.dependencies.TryGetValue(packageName, out string value))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(value))
                {
                    return false;
                }

                return value.StartsWith("file:") || value.StartsWith("http") || value.StartsWith("git");
            }

            /// <summary>
            /// 比较两个 SemVer 版本号，返回正数表示 a > b，0 表示相等，负数表示 a < b。
            /// pre-release 后缀（如 -rc1）视为小于无后缀的同版本号；解析失败走字符串 Ordinal 兜底。
            /// </summary>
            /// <param name="a">版本号 a。</param>
            /// <param name="b">版本号 b。</param>
            /// <returns>比较结果（正数/0/负数）。</returns>
            internal static int CompareSemVer(string a, string b)
            {
                if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
                {
                    return 0;
                }

                if (string.IsNullOrEmpty(a))
                {
                    return -1;
                }

                if (string.IsNullOrEmpty(b))
                {
                    return 1;
                }

                string coreA = StripPreRelease(a, out bool hasPreA);
                string coreB = StripPreRelease(b, out bool hasPreB);

                int[] partsA = ParseVersionParts(coreA);
                int[] partsB = ParseVersionParts(coreB);

                if (partsA == null || partsB == null)
                {
                    return string.Compare(a, b, StringComparison.Ordinal);
                }

                int len = Math.Max(partsA.Length, partsB.Length);
                for (int i = 0; i < len; i++)
                {
                    int va = i < partsA.Length ? partsA[i] : 0;
                    int vb = i < partsB.Length ? partsB[i] : 0;
                    if (va != vb)
                    {
                        return va.CompareTo(vb);
                    }
                }

                if (hasPreA && !hasPreB)
                {
                    return -1;
                }

                if (!hasPreA && hasPreB)
                {
                    return 1;
                }

                return 0;
            }

            /// <summary>
            /// 从版本字符串中剥离 pre-release 后缀，返回核心版本号。
            /// </summary>
            /// <param name="version">原始版本字符串。</param>
            /// <param name="hasPreRelease">是否包含 pre-release 后缀。</param>
            /// <returns>核心版本号字符串（major.minor.patch）。</returns>
            private static string StripPreRelease(string version, out bool hasPreRelease)
            {
                int idx = version.IndexOf('-');
                if (idx >= 0)
                {
                    hasPreRelease = true;
                    return version.Substring(0, idx);
                }

                hasPreRelease = false;
                return version;
            }

            /// <summary>
            /// 将 major.minor.patch 格式字符串解析为整数数组。
            /// </summary>
            /// <param name="core">核心版本字符串。</param>
            /// <returns>整数数组，解析失败返回 null。</returns>
            private static int[] ParseVersionParts(string core)
            {
                string[] segments = core.Split('.');
                int[] parts = new int[segments.Length];
                for (int i = 0; i < segments.Length; i++)
                {
                    if (!int.TryParse(segments[i], out parts[i]))
                    {
                        return null;
                    }
                }

                return parts;
            }

            /// <summary>
            /// 调用系统 tar 解压 tarball，仅提取指定单个文件路径（如 package/CHANGELOG.md）。
            /// 超时 15 秒；tar 退非零或目标文件不存在均返回 false。
            /// </summary>
            /// <param name="tarballPath">tarball 绝对路径。</param>
            /// <param name="extractDir">解压输出目录绝对路径。</param>
            /// <param name="entryPath">tarball 内目标文件路径（如 package/CHANGELOG.md）。</param>
            /// <returns>解压成功且目标文件存在返回 true，否则 false。</returns>
            private static bool RunTarExtract(string tarballPath, string extractDir, string entryPath)
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "tar",
                    Arguments = "-xzf \"" + tarballPath + "\" -C \"" + extractDir + "\" " + entryPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                try
                {
                    using (Process process = Process.Start(psi))
                    {
                        string stderr = process.StandardError.ReadToEnd();
                        bool exited = process.WaitForExit(15000);
                        if (!exited)
                        {
                            try { process.Kill(); } catch (InvalidOperationException) { }
                            Log.Warning(LogTag.Editor, "PlugPals.RunTarExtract 超时 Kill: {0}", tarballPath);
                            return false;
                        }

                        if (process.ExitCode != 0)
                        {
                            if (!string.IsNullOrEmpty(stderr))
                            {
                                Log.Warning(LogTag.Editor, "PlugPals.RunTarExtract tar stderr: {0}", stderr);
                            }
                            return false;
                        }
                    }

                    return true;
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "PlugPals.RunTarExtract 启动 tar 进程失败: {0}", e.Message);
                    return false;
                }
            }

            /// <summary>
            /// 从包名中提取作用域（前两段），例如 com.solotopia.nova.framework 返回 com.solotopia。
            /// </summary>
            /// <param name="packageName">完整包名。</param>
            /// <returns>作用域字符串。</returns>
            private static string ExtractScope(string packageName)
            {
                if (string.IsNullOrEmpty(packageName))
                {
                    return packageName;
                }

                string[] parts = packageName.Split('.');
                if (parts.Length >= 2)
                {
                    return parts[0] + "." + parts[1];
                }

                return packageName;
            }

            /// <summary>
            /// 确保 manifest 中存在当前包所需的 ScopedRegistry 配置。
            /// </summary>
            /// <param name="manifest">manifest 数据。</param>
            /// <param name="registryUrl">仓库地址。</param>
            /// <param name="registryName">仓库名称。</param>
            /// <param name="packageName">包名。</param>
            private static void EnsureScopedRegistry(ManifestData manifest, string registryUrl, string registryName, string packageName)
            {
                if (manifest.scopedRegistries == null)
                {
                    manifest.scopedRegistries = new List<ScopedRegistry>();
                }

                ScopedRegistry registry = manifest.scopedRegistries.FirstOrDefault(r => r.url == registryUrl);
                if (registry == null)
                {
                    registry = new ScopedRegistry
                    {
                        name = registryName,
                        url = registryUrl,
                        scopes = new List<string>()
                    };
                    manifest.scopedRegistries.Add(registry);
                }

                if (registry.scopes == null)
                {
                    registry.scopes = new List<string>();
                }

                string scope = ExtractScope(packageName);
                if (!registry.scopes.Contains(scope))
                {
                    registry.scopes.Add(scope);
                }
            }

            /// <summary>
            /// 清理 ScopedRegistry 中不再使用的 scope，移除空的 registry。
            /// </summary>
            /// <param name="manifest">manifest 数据。</param>
            /// <param name="registryUrl">仓库地址。</param>
            private static void CleanupScopedRegistry(ManifestData manifest, string registryUrl)
            {
                if (manifest.scopedRegistries == null)
                {
                    return;
                }

                ScopedRegistry registry = manifest.scopedRegistries.FirstOrDefault(r => r.url == registryUrl);
                if (registry == null)
                {
                    return;
                }

                var usedScopes = new HashSet<string>();
                if (manifest.dependencies != null)
                {
                    foreach (string packageName in manifest.dependencies.Keys)
                    {
                        usedScopes.Add(ExtractScope(packageName));
                    }
                }

                if (registry.scopes != null)
                {
                    registry.scopes.RemoveAll(scope => !usedScopes.Contains(scope));
                }

                if (registry.scopes == null || registry.scopes.Count == 0)
                {
                    manifest.scopedRegistries.Remove(registry);
                }

                if (manifest.scopedRegistries.Count == 0)
                {
                    manifest.scopedRegistries = null;
                }
            }

            /// <summary>
            /// 按 url 将包自带声明的 ScopedRegistry upsert 到 manifest：
            /// 不存在则整条新增（scopes 深拷贝避免与声明源共享引用）；
            /// 已存在则仅替换该条 name 与 scopes（完全覆盖），不触碰其它 registry。
            /// </summary>
            /// <param name="manifest">manifest 数据。</param>
            /// <param name="declared">包 nova.scopedRegistries 中声明的仓库条目。</param>
            internal static void UpsertDeclaredScopedRegistry(ManifestData manifest, ScopedRegistry declared)
            {
                if (manifest == null || declared == null || string.IsNullOrEmpty(declared.url))
                {
                    return;
                }

                if (manifest.scopedRegistries == null)
                {
                    manifest.scopedRegistries = new List<ScopedRegistry>();
                }

                List<string> scopesCopy = declared.scopes != null ? new List<string>(declared.scopes) : new List<string>();

                ScopedRegistry existing = manifest.scopedRegistries.FirstOrDefault(r => r.url == declared.url);
                if (existing == null)
                {
                    manifest.scopedRegistries.Add(new ScopedRegistry
                    {
                        name = declared.name,
                        url = declared.url,
                        scopes = scopesCopy
                    });
                    return;
                }

                existing.name = declared.name;
                existing.scopes = scopesCopy;
            }

            /// <summary>
            /// 按 url 从 manifest 移除整条 ScopedRegistry；移除后列表为空则置 null。
            /// </summary>
            /// <param name="manifest">manifest 数据。</param>
            /// <param name="url">要移除的仓库 url。</param>
            internal static void RemoveDeclaredScopedRegistryByUrl(ManifestData manifest, string url)
            {
                if (manifest?.scopedRegistries == null || string.IsNullOrEmpty(url))
                {
                    return;
                }

                manifest.scopedRegistries.RemoveAll(r => r.url == url);

                if (manifest.scopedRegistries.Count == 0)
                {
                    manifest.scopedRegistries = null;
                }
            }

            /// <summary>
            /// 收集「除待卸载包外、其它已安装包」通过 nova.scopedRegistries 声明的仓库 url 集合。
            /// 卸载时若待移除的 registry url 命中此集合，说明仍有其它已装包共用该私有仓库，应保留不删，避免误伤。
            /// 仅统计已安装条目（LocalVersion 非空）；待卸载包自身及其同名副本（外网/内部云）一律排除。
            /// </summary>
            /// <param name="allEntries">全部远程包条目（外网 + 内部云），含本地安装态与 nova 声明。</param>
            /// <param name="uninstallingPackageName">正在卸载的包名。</param>
            /// <returns>仍被其它已安装包需要的 registry url 集合。</returns>
            internal static HashSet<string> CollectRegistryUrlsDeclaredByOtherInstalled(
                IEnumerable<PackageDisplayEntry> allEntries,
                string uninstallingPackageName)
            {
                var urls = new HashSet<string>(StringComparer.Ordinal);
                if (allEntries == null)
                {
                    return urls;
                }

                foreach (PackageDisplayEntry entry in allEntries)
                {
                    if (entry == null ||
                        entry.Name == uninstallingPackageName ||
                        string.IsNullOrEmpty(entry.LocalVersion) ||
                        entry.Nova?.scopedRegistries == null)
                    {
                        continue;
                    }

                    foreach (ScopedRegistry registry in entry.Nova.scopedRegistries)
                    {
                        if (registry != null && !string.IsNullOrEmpty(registry.url))
                        {
                            urls.Add(registry.url);
                        }
                    }
                }

                return urls;
            }

            /// <summary>
            /// 收集待装包 dependencies 中被其自身 nova.scopedRegistries 声明 scope 覆盖的依赖（依赖名 -> 版本）。
            /// 这些依赖由声明的私有仓库提供，安装时随主包显式写入项目 manifest.dependencies 顶层，
            /// 确保 UPM 作为直接依赖解析安装（仅作传递依赖时 UPM 不保证拉取）；卸载时一并移除。
            /// </summary>
            /// <param name="entry">待装/待卸载包条目。</param>
            /// <returns>被声明 scope 覆盖的依赖（依赖名 -> 版本）。</returns>
            internal static Dictionary<string, string> CollectDeclaredRegistryDependencies(PackageDisplayEntry entry)
            {
                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                if (entry?.Dependencies == null || entry.Nova?.scopedRegistries == null)
                {
                    return result;
                }

                foreach (KeyValuePair<string, string> dependency in entry.Dependencies)
                {
                    if (!string.IsNullOrEmpty(dependency.Key) && IsCoveredByDeclaredRegistries(dependency.Key, entry.Nova))
                    {
                        result[dependency.Key] = dependency.Value;
                    }
                }

                return result;
            }
        }
    }
}
