/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.CheckUpdate.Methods.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   CheckUpdate 工具 —— 私有实现方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class CheckUpdate
        {
            /// <summary>
            /// 异步拉取所有有更新的包信息列表（latest > current）。
            /// </summary>
            /// <returns>有可用更新的 UpdateInfo 列表，网络失败时返回空列表。</returns>
            public static async Task<List<UpdateInfo>> CheckAsync()
            {
                List<UpdateInfo> externalUpdates = await CheckExternalAsync();
                List<UpdateInfo> internalUpdates = await CheckInternalAsync();
                return MergeAndSortUpdateInfos(externalUpdates, internalUpdates);
            }

            /// <summary>
            /// Editor 启动时异步检查更新，过滤已跳过的版本后弹窗通知。
            /// 所有异常均静默处理，不影响 Editor 正常启动。
            /// </summary>
            internal static async Task CheckOnStartupAsync()
            {
                try
                {
                    List<UpdateInfo> externalUpdates = await CheckExternalAsync();
                    List<UpdateInfo> internalUpdates = await CheckInternalAsync();
                    if ((externalUpdates == null || externalUpdates.Count == 0) && (internalUpdates == null || internalUpdates.Count == 0))
                    {
                        return;
                    }

                    SkipConfig config = LoadConfig();
                    List<UpdateInfo> filteredExternal = FilterSkippedUpdates(externalUpdates, false, config);
                    List<UpdateInfo> filteredInternal = FilterSkippedUpdates(internalUpdates, true, config);
                    if (filteredExternal.Count > 0 || filteredInternal.Count > 0)
                    {
                        CheckUpdateWindow.Open(filteredExternal, filteredInternal);
                    }
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "CheckUpdate.CheckOnStartupAsync 检查更新失败: {0}", e.Message);
                }
            }

            /// <summary>
            /// 将指定包的当前最新版本标记为"跳过"，写入持久化配置。
            /// </summary>
            /// <param name="items">要跳过的更新列表。</param>
            public static void MarkSkip(IEnumerable<UpdateInfo> items)
            {
                MarkSkip(items, false);
            }

            /// <summary>
            /// 将指定仓库的当前最新版本标记为"跳过"，写入持久化配置。
            /// </summary>
            internal static void MarkSkip(IEnumerable<UpdateInfo> items, bool isInternal)
            {
                SkipConfig config = LoadConfig();
                foreach (UpdateInfo info in items)
                {
                    config.SkipVersions[BuildSkipKey(info.PackageName, isInternal)] = info.LatestVersion;
                }

                SaveConfig(config);
            }

            /// <summary>
            /// 检查外网仓库更新。
            /// </summary>
            internal static async Task<List<UpdateInfo>> CheckExternalAsync()
            {
                PlugPals.RegistriesConfig registries = PlugPals.LoadRegistries();
                return await CheckRepoAsync(registries.externalUrl);
            }

            /// <summary>
            /// 检查内部云仓库更新。内部云地址未配置时跳过，返回空列表。
            /// </summary>
            internal static async Task<List<UpdateInfo>> CheckInternalAsync()
            {
                PlugPals.RegistriesConfig registries = PlugPals.LoadRegistries();
                if (string.IsNullOrEmpty(registries.internalUrl))
                {
                    return new List<UpdateInfo>();
                }

                return await CheckRepoAsync(registries.internalUrl);
            }

            /// <summary>
            /// 按仓库地址检查单个仓库更新。
            /// </summary>
            private static async Task<List<UpdateInfo>> CheckRepoAsync(string registryUrl)
            {
                try
                {
                    PlugPals.VerdaccioPackageInfo[] remotePackages = await PlugPals.FetchRemotePackagesAsync(registryUrl, PlugPals.c_RegistryApiPath, CancellationToken.None);
                    if (remotePackages == null)
                    {
                        return new List<UpdateInfo>();
                    }

                    Dictionary<string, string> installedVersions = PlugPals.ReadInstalledVersions();
                    if (installedVersions == null)
                    {
                        return new List<UpdateInfo>();
                    }

                    var result = new List<UpdateInfo>();
                    for (int i = 0; i < remotePackages.Length; i++)
                    {
                        PlugPals.VerdaccioPackageInfo remote = remotePackages[i];
                        if (string.IsNullOrEmpty(remote.Name) || string.IsNullOrEmpty(remote.Version))
                        {
                            continue;
                        }

                        if (!installedVersions.TryGetValue(remote.Name, out string currentVersion))
                        {
                            continue;
                        }

                        if (PlugPals.CompareSemVer(remote.Version, currentVersion) > 0)
                        {
                            result.Add(new UpdateInfo(remote.Name, currentVersion, remote.Version));
                        }
                    }

                    result.Sort((a, b) => CompareUpdateInfos(a, b));
                    return result;
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "CheckUpdate.CheckRepoAsync 检查失败 [{0}]: {1}", registryUrl, e.Message);
                    return new List<UpdateInfo>();
                }
            }

            /// <summary>
            /// 合并外网与内部仓库更新列表，并统一按包名排序。
            /// </summary>
            internal static List<UpdateInfo> MergeAndSortUpdateInfos(IReadOnlyList<UpdateInfo> externalUpdates, IReadOnlyList<UpdateInfo> internalUpdates)
            {
                int capacity = (externalUpdates?.Count ?? 0) + (internalUpdates?.Count ?? 0);
                var merged = new List<UpdateInfo>(capacity);
                if (externalUpdates != null)
                {
                    merged.AddRange(externalUpdates);
                }

                if (internalUpdates != null)
                {
                    merged.AddRange(internalUpdates);
                }

                var internalItemSet = internalUpdates != null ? new HashSet<UpdateInfo>(internalUpdates) : null;
                merged.Sort((a, b) => CompareUpdateInfos(a, b, internalItemSet));
                return merged;
            }

            /// <summary>
            /// 过滤已跳过的更新项，skip key 带仓库维度。
            /// </summary>
            private static List<UpdateInfo> FilterSkippedUpdates(IReadOnlyList<UpdateInfo> updates, bool isInternal, SkipConfig config)
            {
                if (updates == null || updates.Count == 0)
                {
                    return new List<UpdateInfo>();
                }

                var filtered = new List<UpdateInfo>(updates.Count);
                for (int i = 0; i < updates.Count; i++)
                {
                    UpdateInfo info = updates[i];
                    if (config.SkipVersions.TryGetValue(BuildSkipKey(info.PackageName, isInternal), out string skippedVersion) &&
                        skippedVersion == info.LatestVersion)
                    {
                        continue;
                    }

                    filtered.Add(info);
                }

                return filtered;
            }

            /// <summary>
            /// 构建带仓库前缀的 skip key。
            /// </summary>
            internal static string BuildSkipKey(string packageName, bool isInternal)
            {
                return (isInternal ? "internal:" : "external:") + packageName;
            }

            /// <summary>
            /// 更新项统一排序：按包名、最新版本、当前版本升序。
            /// </summary>
            private static int CompareUpdateInfos(UpdateInfo a, UpdateInfo b, ISet<UpdateInfo> internalItems = null)
            {
                int byName = string.Compare(a?.PackageName, b?.PackageName, StringComparison.OrdinalIgnoreCase);
                if (byName != 0)
                {
                    return byName;
                }

                int byLatest = string.Compare(a?.LatestVersion, b?.LatestVersion, StringComparison.OrdinalIgnoreCase);
                if (byLatest != 0)
                {
                    return byLatest;
                }

                int byCurrent = string.Compare(a?.CurrentVersion, b?.CurrentVersion, StringComparison.OrdinalIgnoreCase);
                if (byCurrent != 0)
                {
                    return byCurrent;
                }

                bool aIsInternal = internalItems != null && a != null && internalItems.Contains(a);
                bool bIsInternal = internalItems != null && b != null && internalItems.Contains(b);
                if (aIsInternal != bIsInternal)
                {
                    return aIsInternal ? 1 : -1;
                }

                return 0;
            }

            /// <summary>
            /// 清空所有跳过记录。
            /// </summary>
            public static void ClearSkip()
            {
                SaveConfig(new SkipConfig());
            }

            /// <summary>
            /// 从磁盘读取跳过配置；文件不存在时返回空配置不报错。
            /// </summary>
            /// <returns>跳过配置实例。</returns>
            private static SkipConfig LoadConfig()
            {
                string fullPath = GetConfigFullPath();
                if (!Util.SysIO.File.Exists(fullPath))
                {
                    return new SkipConfig();
                }

                try
                {
                    string json = Util.SysIO.File.ReadAllTextSync(fullPath);
                    SkipConfig config = Util.Json.Deserialize<SkipConfig>(json);
                    return config ?? new SkipConfig();
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "CheckUpdate.LoadConfig 读取配置失败: {0}", e.Message);
                    return new SkipConfig();
                }
            }

            /// <summary>
            /// 将跳过配置写入磁盘；目录不存在时自动创建。
            /// </summary>
            /// <param name="config">要持久化的配置。</param>
            private static void SaveConfig(SkipConfig config)
            {
                try
                {
                    string fullPath = GetConfigFullPath();
                    string dir = Util.SysIO.Path.GetDirectoryName(fullPath);
                    if (!Util.SysIO.Directory.Exists(dir))
                    {
                        Util.SysIO.Directory.Create(dir);
                    }

                    string json = Util.Json.Serialize(config);
                    Util.SysIO.File.WriteAllTextSync(fullPath, json);
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "CheckUpdate.SaveConfig 写入配置失败: {0}", e.Message);
                }
            }

            /// <summary>
            /// 获取跳过配置文件绝对路径。
            /// </summary>
            /// <returns>配置文件绝对路径。</returns>
            private static string GetConfigFullPath()
            {
                return Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(Application.dataPath, "../", c_ConfigPath));
            }
        }
    }
}
