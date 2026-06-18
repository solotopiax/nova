/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.PlugPals.cs
 * author:    taoye
 * created:   2026/4/21
 * descrip:   PlugPals 工具 —— 公有接口
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// PlugPals 私有 Verdaccio 仓库 UPM 包管理工具。
        /// 提供远程包列表拉取、本地版本读取、manifest 读写以及包安装/卸载流程。
        /// </summary>
        public static partial class PlugPals
        {
            /// <summary>
            /// 异步从 Verdaccio 仓库拉取远程包信息列表。
            /// 使用 HttpClient 避免 UnityWebRequest 的 HTTP 不安全连接限制。
            /// </summary>
            /// <param name="registryUrl">Verdaccio 仓库根地址。</param>
            /// <param name="apiPath">包列表 API 路径（如 /-/verdaccio/data/packages）。</param>
            /// <param name="token">取消令牌。</param>
            /// <returns>远程包信息数组，解析失败返回 null。</returns>
            public static async Task<VerdaccioPackageInfo[]> FetchRemotePackagesAsync(string registryUrl, string apiPath, CancellationToken token)
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, registryUrl + apiPath);
                HttpResponseMessage response = await s_HttpClient.SendAsync(request, token);
                response.EnsureSuccessStatusCode();
                token.ThrowIfCancellationRequested();
                string json = await response.Content.ReadAsStringAsync();
                token.ThrowIfCancellationRequested();

                VerdaccioPackageInfo[] packages = Util.Json.Deserialize<VerdaccioPackageInfo[]>(json);
                return packages;
            }

            /// <summary>
            /// 从 packages-lock.json 读取所有已安装包的实际版本号（含直接和传递依赖）。
            /// 对于 file: 引用读取对应 package.json；对于 git 引用从 Library/PackageCache 中读取实际版本；
            /// 对于 registry 引用直接使用 lock 文件中的版本号。
            /// </summary>
            /// <returns>包名到版本号的字典，读取失败返回 null。</returns>
            public static Dictionary<string, string> ReadInstalledVersions()
            {
                string lockPath = Util.SysIO.Path.Combine(Application.dataPath, "../", "Packages/packages-lock.json");
                string fullLockPath = Util.SysIO.Path.GetFullPath(lockPath);

                if (!Util.SysIO.File.Exists(fullLockPath))
                {
                    return null;
                }

                PackagesLockData lockData;
                try
                {
                    string json = Util.SysIO.File.ReadAllTextSync(fullLockPath);
                    lockData = Util.Json.Deserialize<PackagesLockData>(json);
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "PlugPals.ReadInstalledVersions 读取 packages-lock.json 失败: {0}", e.Message);
                    return null;
                }

                if (lockData?.dependencies == null)
                {
                    return null;
                }

                var versions = new Dictionary<string, string>(lockData.dependencies.Count);
                foreach (var kvp in lockData.dependencies)
                {
                    string packageName = kvp.Key;
                    PackagesLockEntry entry = kvp.Value;
                    if (entry == null || string.IsNullOrEmpty(entry.version))
                    {
                        continue;
                    }

                    string version = ResolveEntryVersion(packageName, entry);
                    if (!string.IsNullOrEmpty(version))
                    {
                        versions[packageName] = version;
                    }
                }

                return versions;
            }

            /// <summary>
            /// 根据远程包信息和本地已安装版本构建显示条目列表。
            /// </summary>
            /// <param name="remotePackages">Verdaccio 返回的远程包信息数组。</param>
            /// <param name="registryUrl">Verdaccio 仓库地址，用于判断非仓库引用。</param>
            /// <returns>按显示名称排序的包条目列表。</returns>
            public static List<PackageDisplayEntry> BuildDisplayEntries(VerdaccioPackageInfo[] remotePackages, string registryUrl)
            {
                Dictionary<string, string> installedVersions = ReadInstalledVersions();
                ManifestData manifest = ReadManifest(Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(Application.dataPath, "../", c_ManifestRelativePath)));

                var entries = new List<PackageDisplayEntry>(remotePackages.Length);
                for (int i = 0; i < remotePackages.Length; i++)
                {
                    VerdaccioPackageInfo remote = remotePackages[i];
                    if (string.IsNullOrEmpty(remote.Name) || string.IsNullOrEmpty(remote.Version))
                    {
                        continue;
                    }

                    string localVersion = null;
                    installedVersions?.TryGetValue(remote.Name, out localVersion);
                    bool isNonRegistry = IsNonRegistryReference(remote.Name, manifest);
                    PackageStatus status = CompareVersions(localVersion, remote.Version, isNonRegistry);

                    entries.Add(new PackageDisplayEntry
                    {
                        Name = remote.Name,
                        DisplayName = string.IsNullOrEmpty(remote.DisplayName) ? remote.Name : remote.DisplayName,
                        Description = remote.Description,
                        LocalVersion = localVersion,
                        LatestVersion = remote.Version,
                        Status = status,
                        Category = CategorizePackage(remote.Name),
                        CoreVersion = remote.CoreVersion,
                        Dependencies = remote.Dependencies,
                        Nova = remote.Nova
                    });
                }

                entries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
                return entries;
            }

            /// <summary>
            /// 比较本地版本与远程版本，确定包安装状态。
            /// </summary>
            /// <param name="local">本地版本（null 表示未安装）。</param>
            /// <param name="remote">远程最新版本。</param>
            /// <param name="isNonRegistry">是否为非仓库引用（git/file:）。</param>
            /// <returns>包安装状态。</returns>
            public static PackageStatus CompareVersions(string local, string remote, bool isNonRegistry)
            {
                if (local == null)
                {
                    return isNonRegistry ? PackageStatus.NonRegistry : PackageStatus.NotInstalled;
                }

                if (isNonRegistry)
                {
                    return PackageStatus.NonRegistry;
                }

                if (CompareSemVer(remote, local) > 0)
                {
                    return PackageStatus.Upgradeable;
                }

                return PackageStatus.Installed;
            }

            /// <summary>
            /// 根据包名前缀判定包分类。
            /// sdk 开头 → SDK；kit 开头 → Kit；framework 开头且不含 .sdk. 和 .kit. → Framework；其余 → Other。
            /// </summary>
            /// <param name="packageName">UPM 包名。</param>
            /// <returns>对应的 PackageCategory 枚举值。</returns>
            public static PackageCategory CategorizePackage(string packageName)
            {
                if (string.IsNullOrEmpty(packageName))
                {
                    return PackageCategory.Other;
                }

                if (packageName.StartsWith("com.solotopia.nova.framework.sdk", StringComparison.Ordinal))
                {
                    return PackageCategory.SDK;
                }

                if (packageName.StartsWith("com.solotopia.nova.framework.kit", StringComparison.Ordinal))
                {
                    return PackageCategory.Kit;
                }

                if (packageName.StartsWith("com.solotopia.nova.framework", StringComparison.Ordinal))
                {
                    return PackageCategory.Framework;
                }

                return PackageCategory.Other;
            }

            /// <summary>
            /// 读取 manifest.json 并反序列化为 ManifestData（仅解析 scopedRegistries 和 dependencies）。
            /// </summary>
            /// <param name="manifestPath">manifest.json 绝对路径。</param>
            /// <returns>manifest 数据，读取失败返回 null。</returns>
            public static ManifestData ReadManifest(string manifestPath)
            {
                if (!Util.SysIO.File.Exists(manifestPath))
                {
                    Log.Error(LogTag.Editor, "PlugPals.ReadManifest manifest.json 不存在: {0}", manifestPath);
                    return null;
                }

                try
                {
                    string json = Util.SysIO.File.ReadAllTextSync(manifestPath);
                    return Util.Json.Deserialize<ManifestData>(json);
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.Editor, "PlugPals.ReadManifest 读取或解析 manifest.json 失败: {0}", e.Message);
                    return null;
                }
            }

            /// <summary>
            /// 将 ManifestData 的变更合并回原始 manifest.json（保留未建模的字段如 testables 等）。
            /// 使用 JObject 局部更新策略，避免丢失未知字段。
            /// </summary>
            /// <param name="manifestPath">manifest.json 绝对路径。</param>
            /// <param name="manifest">manifest 数据。</param>
            public static void SaveManifest(string manifestPath, ManifestData manifest)
            {
                try
                {
                    JObject root;
                    if (Util.SysIO.File.Exists(manifestPath))
                    {
                        string existingJson = Util.SysIO.File.ReadAllTextSync(manifestPath);
                        root = JObject.Parse(existingJson);
                    }
                    else
                    {
                        root = new JObject();
                    }

                    if (manifest.scopedRegistries != null && manifest.scopedRegistries.Count > 0)
                    {
                        // 确保 scopedRegistries 出现在 dependencies 之前
                        if (root.Property("scopedRegistries") == null && root.Property("dependencies") != null)
                        {
                            root.Property("dependencies").AddBeforeSelf(new JProperty("scopedRegistries", JToken.FromObject(manifest.scopedRegistries)));
                        }
                        else
                        {
                            root["scopedRegistries"] = JToken.FromObject(manifest.scopedRegistries);
                        }
                    }
                    else
                    {
                        root.Remove("scopedRegistries");
                    }

                    if (manifest.dependencies != null)
                    {
                        root["dependencies"] = JToken.FromObject(manifest.dependencies);
                    }

                    string json = root.ToString(Newtonsoft.Json.Formatting.Indented);
                    Util.SysIO.File.WriteAllTextSync(manifestPath, json);
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.Editor, "PlugPals.SaveManifest 保存 manifest.json 失败: {0}", e.Message);
                    throw;
                }
            }

            /// <summary>
            /// 安装指定包：先按 dependencies + registry 命中检测缺库，缺失则引导并中止；
            /// 全命中则为命中依赖自动配 scope 并写 manifest 触发 UPM 解析安装。
            /// </summary>
            public static bool InstallPackage(string manifestPath, string registryUrl, string registryName, PackageDisplayEntry entry, IReadOnlyDictionary<string, RegistrySource> knownRegistryPackages)
            {
                ManifestData manifest = ReadManifest(manifestPath);
                if (manifest == null)
                {
                    return false;
                }

                manifest.dependencies ??= new Dictionary<string, string>();

                var installedPackageNames = new HashSet<string>(StringComparer.Ordinal);
                Dictionary<string, string> installedVersions = ReadInstalledVersions();
                if (installedVersions != null)
                {
                    foreach (string name in installedVersions.Keys)
                    {
                        installedPackageNames.Add(name);
                    }
                }
                foreach (string name in manifest.dependencies.Keys)
                {
                    installedPackageNames.Add(name);
                }

                DependencyCheckResult check = CheckDependencies(
                    entry.Dependencies,
                    installedPackageNames,
                    knownRegistryPackages,
                    entry.Nova,
                    entry.Name,
                    entry.DisplayName);

                if (check.Missing.Count > 0)
                {
                    PlugPalsMissingRequiredLibrariesWindow.Open(check.Missing);
                    return false;
                }

                foreach (KeyValuePair<string, RegistrySource> hit in check.ToAutoScope)
                {
                    EnsureScopedRegistry(manifest, hit.Value.Url, hit.Value.Name, hit.Key);
                }

                EnsureScopedRegistry(manifest, registryUrl, registryName, entry.Name);

                if (entry.Nova?.scopedRegistries != null)
                {
                    foreach (ScopedRegistry declared in entry.Nova.scopedRegistries)
                    {
                        UpsertDeclaredScopedRegistry(manifest, declared);
                    }
                }

                manifest.dependencies[entry.Name] = entry.LatestVersion;

                // 被声明 registry scope 覆盖的依赖显式写入顶层，确保 UPM 作为直接依赖解析安装
                // （仅作主包传递依赖时 UPM 不保证拉取 scoped-registry 包）。
                foreach (KeyValuePair<string, string> declaredDep in CollectDeclaredRegistryDependencies(entry))
                {
                    manifest.dependencies[declaredDep.Key] = declaredDep.Value;
                }

                SaveManifest(manifestPath, manifest);
                ResolvePackages();

                entry.LocalVersion = entry.LatestVersion;
                entry.Status = PackageStatus.Installed;
                return true;
            }

            /// <summary>
            /// 从 manifest.json 中卸载指定包并触发 UPM 解析。
            /// </summary>
            /// <param name="manifestPath">manifest.json 绝对路径。</param>
            /// <param name="registryUrl">Verdaccio 仓库地址（用于清理 ScopedRegistry）。</param>
            /// <param name="entry">要卸载的包条目。</param>
            /// <param name="registryUrlsNeededByOthers">仍被其它已安装包声明共用的 registry url 集合；命中则保留该私有仓库不删。</param>
            public static void UninstallPackage(string manifestPath, string registryUrl, PackageDisplayEntry entry, ISet<string> registryUrlsNeededByOthers)
            {
                ManifestData manifest = ReadManifest(manifestPath);
                if (manifest == null)
                {
                    return;
                }

                if (manifest.dependencies != null)
                {
                    manifest.dependencies.Remove(entry.Name);

                    // 移除安装时随主包写入顶层的、被声明 registry scope 覆盖的依赖。
                    foreach (string declaredDepName in CollectDeclaredRegistryDependencies(entry).Keys)
                    {
                        manifest.dependencies.Remove(declaredDepName);
                    }
                }

                CleanupScopedRegistry(manifest, registryUrl);

                if (entry.Nova?.scopedRegistries != null)
                {
                    foreach (ScopedRegistry declared in entry.Nova.scopedRegistries)
                    {
                        if (declared == null || string.IsNullOrEmpty(declared.url))
                        {
                            continue;
                        }

                        if (registryUrlsNeededByOthers != null && registryUrlsNeededByOthers.Contains(declared.url))
                        {
                            continue;
                        }

                        RemoveDeclaredScopedRegistryByUrl(manifest, declared.url);
                    }
                }

                SaveManifest(manifestPath, manifest);
                ResolvePackages();

                entry.LocalVersion = null;
                entry.Status = PackageStatus.NotInstalled;
            }

            /// <summary>
            /// 延迟触发 UPM 包解析，优先使用 Client.Resolve（Unity 2020.1+），不可用时回退 AssetDatabase.Refresh。
            /// </summary>
            public static void ResolvePackages()
            {
                if (s_IsResolvePackagesQueued)
                {
                    return;
                }

                s_IsResolvePackagesQueued = true;
                EditorApplication.delayCall += ExecuteResolvePackages;
            }

            /// <summary>
            /// 执行已排队的 UPM 包解析。
            /// </summary>
            private static void ExecuteResolvePackages()
            {
                s_IsResolvePackagesQueued = false;

                // Client.Resolve 是 Unity 2020.1+ API，通过反射调用以兼容低版本
                Type clientType = typeof(UnityEditor.PackageManager.Client);
                System.Reflection.MethodInfo resolveMethod = clientType.GetMethod("Resolve", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, Type.EmptyTypes, null);
                if (resolveMethod != null)
                {
                    resolveMethod.Invoke(null, null);
                }
                else
                {
                    AssetDatabase.Refresh();
                }
            }

            /// <summary>
            /// 异步获取指定包版本的更新日志，优先命中本地缓存（永久缓存）；
            /// 缓存缺失时从 Verdaccio 下载 tarball 解出 CHANGELOG.md 并缓存。
            /// </summary>
            /// <param name="registryUrl">Verdaccio 仓库根地址。</param>
            /// <param name="packageName">UPM 包名。</param>
            /// <param name="version">目标版本号。</param>
            /// <param name="token">取消令牌。</param>
            /// <returns>缓存后的 CHANGELOG.md 绝对路径；失败返回 null。</returns>
            public static async Task<string> FetchChangelogAsync(string registryUrl, string packageName, string version, CancellationToken token)
            {
                string projectRoot = Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(Application.dataPath, ".."));
                string cacheDir = Util.SysIO.Path.Combine(projectRoot, c_ChangelogCacheRelDir, packageName);
                string cachePath = Util.SysIO.Path.Combine(cacheDir, version + ".md");

                if (Util.SysIO.File.Exists(cachePath))
                {
                    return cachePath;
                }

                string pkgBaseName = packageName.Contains("/") ? packageName.Substring(packageName.LastIndexOf('/') + 1) : packageName;
                string tarballUrl = registryUrl + "/" + packageName + "/-/" + pkgBaseName + "-" + version + ".tgz";
                string tarballsDir = Util.SysIO.Path.Combine(projectRoot, c_TarballCacheRelDir);
                string tarballPath = Util.SysIO.Path.Combine(tarballsDir, pkgBaseName + "-" + version + ".tgz");
                string extractDir = Util.SysIO.Path.Combine(tarballsDir, "extract-" + pkgBaseName + "-" + version);

                try
                {
                    Util.SysIO.Directory.CreateIfNotExist(tarballsDir);

                    HttpResponseMessage response = await s_HttpClient.GetAsync(tarballUrl, token);
                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Warning(LogTag.Editor, "PlugPals.FetchChangelogAsync 下载 tarball 失败 [{0}]: {1}", (int)response.StatusCode, tarballUrl);
                        return null;
                    }

                    byte[] tarballBytes = await response.Content.ReadAsByteArrayAsync();
                    token.ThrowIfCancellationRequested();
                    Util.SysIO.File.WriteAllBytesSync(tarballPath, tarballBytes);

                    Util.SysIO.Directory.CreateIfNotExist(extractDir);

                    bool tarSuccess = RunTarExtract(tarballPath, extractDir, c_ChangelogTarEntry);
                    if (!tarSuccess)
                    {
                        Log.Warning(LogTag.Editor, "PlugPals.FetchChangelogAsync tar 解压失败或包内无 CHANGELOG.md: {0}@{1}", packageName, version);
                        return null;
                    }

                    string extractedChangelog = Util.SysIO.Path.Combine(extractDir, "package", "CHANGELOG.md");
                    if (!Util.SysIO.File.Exists(extractedChangelog))
                    {
                        Log.Warning(LogTag.Editor, "PlugPals.FetchChangelogAsync 解压后未找到 CHANGELOG.md: {0}@{1}", packageName, version);
                        return null;
                    }

                    Util.SysIO.Directory.CreateIfNotExist(cacheDir);
                    Util.SysIO.File.Move(extractedChangelog, cachePath);
                    return cachePath;
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "PlugPals.FetchChangelogAsync 异常: {0}@{1} - {2}", packageName, version, e.Message);
                    return null;
                }
                finally
                {
                    if (Util.SysIO.File.Exists(tarballPath))
                    {
                        Util.SysIO.File.Delete(tarballPath);
                    }
                    if (Util.SysIO.Directory.Exists(extractDir))
                    {
                        Util.SysIO.Directory.Delete(extractDir, true);
                    }
                }
            }

            /// <summary>
            /// 读取已安装包的 Samples 列表（基于 UPM 标准 Sample.FindByPackage）。
            /// 只看 package.json 的 samples 声明，不校验磁盘 resolvedPath 是否真实存在；
            /// 源目录缺失的检测交给 Import 调用方在用户点击时给出明确提示。
            /// </summary>
            /// <param name="packageName">包名（如 com.solotopia.nova.framework）。</param>
            /// <param name="version">本地已安装版本号；空或包未安装时返回空数组。</param>
            /// <returns>Samples 列表，无声明时返回空数组（不返回 null）。</returns>
            public static IReadOnlyList<Sample> GetPackageSamples(string packageName, string version)
            {
                if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(version))
                {
                    return Array.Empty<Sample>();
                }

                try
                {
                    IEnumerable<Sample> samples = Sample.FindByPackage(packageName, version);
                    if (samples == null)
                    {
                        return Array.Empty<Sample>();
                    }
                    return new List<Sample>(samples);
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "PlugPals.GetPackageSamples 读取 {0}@{1} 的 samples 失败: {2}", packageName, version, e.Message);
                    return Array.Empty<Sample>();
                }
            }
        }
    }
}
