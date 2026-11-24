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
using System.Globalization;
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
            /// 为启动或跨日的自动检查查询一次更新，把结果交给安全展示队列。
            /// 等待展示及域重载复用已取得的列表，跳过配置和本地安装版本在真正展示前重新读取。
            /// </summary>
            internal static async Task CheckOnStartupAsync()
            {
                if (Application.isBatchMode || s_AutomaticStopping || s_AutomaticQueryRunning) return;
                s_AutomaticQueryRunning = true;
                SetAutomaticCheckState("querying", "正在查询可用更新；查询完成后仍需等待安全展示时机。");
                try
                {
                    List<UpdateInfo> externalUpdates = await CheckExternalAsync();
                    List<UpdateInfo> internalUpdates = await CheckInternalAsync();
                    if (s_AutomaticStopping) return;
                    if ((externalUpdates == null || externalUpdates.Count == 0) && (internalUpdates == null || internalUpdates.Count == 0))
                    {
                        CompleteAutomaticPresentation(DateTime.Today);
                        return;
                    }
                    QueueAutomaticPresentation(externalUpdates, internalUpdates);
                }
                catch (Exception e)
                {
                    FinishAutomaticCheckForDay(DateTime.Today);
                    SetAutomaticCheckState("blocked", "本轮自动更新查询未完成；保留已有跳过记录，可从菜单手动检查。");
                    Log.Warning(LogTag.Editor, "CheckUpdate.CheckOnStartupAsync 检查更新失败: {0}", e.Message);
                }
                finally
                {
                    s_AutomaticQueryRunning = false;
                }
            }

            /// <summary>
            /// 暂停指定外网包的当前最新版本的自动提醒，有效期为本地当天。
            /// </summary>
            /// <param name="items">今天要跳过的外网仓库更新列表。</param>
            public static void MarkSkip(IEnumerable<UpdateInfo> items)
            {
                MarkSkip(items, null, DateTime.Today);
            }

            /// <summary>
            /// 一次保存两个仓库的当天选择。窗口跨日后关闭时，昨天的选择已经过期。
            /// </summary>
            /// <param name="externalItems">今天要跳过的外网仓库更新列表。</param>
            /// <param name="internalItems">今天要跳过的内部云仓库更新列表。</param>
            /// <param name="selectedDay">勾选时的本地自然日。</param>
            internal static void MarkSkip(IEnumerable<UpdateInfo> externalItems, IEnumerable<UpdateInfo> internalItems, DateTime selectedDay)
            {
                if (selectedDay != DateTime.Today) return;

                SkipConfig config = LoadConfigForDay(selectedDay);
                foreach (UpdateInfo info in externalItems ?? Array.Empty<UpdateInfo>())
                {
                    config.SkipVersions[BuildSkipKey(info.PackageName, false)] = info.LatestVersion;
                }
                foreach (UpdateInfo info in internalItems ?? Array.Empty<UpdateInfo>())
                {
                    config.SkipVersions[BuildSkipKey(info.PackageName, true)] = info.LatestVersion;
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
                if (string.IsNullOrEmpty(registryUrl))
                {
                    return new List<UpdateInfo>();
                }

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
            /// 读取指定本地自然日的跳过配置；缺少日期、日期不同或记录无效时返回当天的空配置。
            /// </summary>
            /// <param name="day">要读取的本地自然日。</param>
            /// <returns>可应用于该日的跳过配置。</returns>
            private static SkipConfig LoadConfigForDay(DateTime day)
            {
                string localDate = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string fullPath = GetConfigFullPath();
                try
                {
                    if (Util.SysIO.File.Exists(fullPath))
                    {
                        string json = Util.SysIO.File.ReadAllTextSync(fullPath);
                        SkipConfig config = Util.Json.Deserialize<SkipConfig>(json);
                        if (config?.LocalDate == localDate && config.SkipVersions != null) return config;
                    }
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "CheckUpdate.LoadConfigForDay 读取配置失败: {0}", e.Message);
                }

                return new SkipConfig { LocalDate = localDate };
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
