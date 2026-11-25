/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.CheckUpdate.Supplemental.cs
 * author:    taoye
 * created:   2026/11/25
 * descrip:   活跃期间额外检查与新版本提示去重
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using Log = NovaFramework.Runtime.Log;
using LogTag = NovaFramework.Runtime.LogTag;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class CheckUpdate
        {
            private const int c_SupplementalIntervalSeconds = 3600;
            private const int c_ForegroundFreshnessSeconds = 1800;
            private const string c_PresentedVersionsSessionKey = "Nova.CheckUpdate.Automatic.PresentedVersions.v1";
            private static readonly int s_SupplementalJitterSeconds = new System.Random().Next(301);
            private static bool s_ForegroundCheckRequested;
            private static double s_NextSupplementalPoll;
            private static DateTime s_PresentedVersionDay;
            private static Dictionary<string, string> s_PresentedVersions = new(StringComparer.Ordinal);

            /// <summary>回到前台时重算仓库新鲜度；实际查询仍由更新队列和门禁调度。</summary>
            private static void OnAutomaticFocusChanged(bool focused)
            {
                s_ForegroundCheckRequested = focused;
                if (focused) s_NextSupplementalPoll = 0;
            }

            /// <summary>先完成原有当天检查，再按前台状态挑选到期或可重试的仓库。</summary>
            private static void TryRunSupplementalCheck(double now)
            {
                if (now < s_NextSupplementalPoll) return;
                s_NextSupplementalPoll = now + c_AutomaticDayPollIntervalSeconds;
                if (s_LastAutomaticCheckDay != DateTime.Today || !InternalEditorUtility.isApplicationActive ||
                    !string.IsNullOrEmpty(AutomaticWindowGate.ReadWaitReason(true))) return;

                long freshness = s_ForegroundCheckRequested
                    ? c_ForegroundFreshnessSeconds : c_SupplementalIntervalSeconds + s_SupplementalJitterSeconds;
                s_ForegroundCheckRequested = false;
                long utcNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                PlugPals.RegistriesConfig registries = PlugPals.LoadRegistries();
                string external = PlugPals.IsRegistryCatalogDue(registries.externalUrl, utcNow, freshness) ? registries.externalUrl : null;
                string internalUrl = PlugPals.IsRegistryCatalogDue(registries.internalUrl, utcNow, freshness) ? registries.internalUrl : null;
                if (external != null || internalUrl != null) _ = CheckSupplementalAsync(external, internalUrl);
            }

            /// <summary>额外检查只提供可显示的候选列表，不写入当天启动或跨日检查的完成状态。</summary>
            private static async Task CheckSupplementalAsync(string externalUrl, string internalUrl)
            {
                s_AutomaticQueryRunning = true;
                try
                {
                    var (external, internalItems) = await CheckRegistriesAsync(externalUrl, internalUrl);
                    if (s_AutomaticStopping || s_LastAutomaticCheckDay != DateTime.Today ||
                        (external.Count == 0 && internalItems.Count == 0)) return;
                    s_AutomaticSnapshot = new AutomaticUpdateSnapshot
                    {
                        External = external, Internal = internalItems, Supplemental = true,
                        ExternalUrl = externalUrl, InternalUrl = internalUrl
                    };
                    s_AutomaticCheckQueued = true;
                    s_NextSupplementalPoll = 0;
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "额外更新检查失败：{0}", e.Message);
                }
                finally
                {
                    s_AutomaticQueryRunning = false;
                }
            }

            /// <summary>额外提示等待前台及旧提示关闭，等待期间限制本地包状态的重复读取。</summary>
            private static bool CanAttemptSupplementalPresentation()
            {
                double now = EditorApplication.timeSinceStartup;
                if (now < s_NextSupplementalPoll) return false;
                s_NextSupplementalPoll = now + 2;
                return InternalEditorUtility.isApplicationActive && UnityEngine.Resources.FindObjectsOfTypeAll<CheckUpdateWindow>().Length == 0;
            }

            /// <summary>丢弃跨日、失效或已处理的额外结果，让原有日期调度继续运行。</summary>
            private static void ClearSupplementalSnapshot()
            {
                s_AutomaticSnapshot = null;
                s_AutomaticCheckQueued = false;
            }

            /// <summary>仓库配置与当前安装资格都一致时，才从额外检查中提示尚未展示的更高版本。</summary>
            private static List<UpdateInfo> FilterSupplementalUpdates(List<UpdateInfo> updates, bool isInternal,
                string queriedUrl, string currentUrl, IReadOnlyDictionary<string, string> presented)
            {
                if (updates.Count == 0 || !string.Equals(queriedUrl, currentUrl, StringComparison.Ordinal)) return new List<UpdateInfo>();
                List<UpdateInfo> candidates = FilterUnpresentedUpdates(updates, isInternal, presented);
                if (candidates.Count == 0) return candidates;
                var remote = candidates.Select(item => new PlugPals.VerdaccioPackageInfo { Name = item.PackageName, Version = item.LatestVersion }).ToArray();
                return PlugPals.BuildDisplayEntries(remote, queriedUrl)
                    .Where(item => item.Status == PlugPals.PackageStatus.Upgradeable)
                    .Select(item => new UpdateInfo(item.Name, item.LocalVersion, item.LatestVersion)).ToList();
            }

            /// <summary>额外提示只保留高于本日已展示目标版本的条目，公网和内部云独立。</summary>
            private static List<UpdateInfo> FilterUnpresentedUpdates(IEnumerable<UpdateInfo> updates, bool isInternal,
                IReadOnlyDictionary<string, string> presented)
            {
                return updates.Where(item => !presented.TryGetValue(BuildSkipKey(item.PackageName, isInternal), out string version) ||
                    PlugPals.CompareSemVer(item.LatestVersion, version) > 0).ToList();
            }

            /// <summary>按本地自然日恢复已经弹出的目标版本，仅供额外提示过滤。</summary>
            private static Dictionary<string, string> PresentedVersionsForDay(DateTime day)
            {
                if (s_PresentedVersionDay == day) return s_PresentedVersions;
                s_PresentedVersionDay = day;
                s_PresentedVersions = new Dictionary<string, string>(StringComparer.Ordinal);
                try
                {
                    JObject saved = JObject.Parse(SessionState.GetString(c_PresentedVersionsSessionKey, "{}"));
                    if ((string)saved["day"] == day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                        s_PresentedVersions = saved["versions"]?.ToObject<Dictionary<string, string>>() ?? s_PresentedVersions;
                }
                catch (Exception)
                {
                    SessionState.EraseString(c_PresentedVersionsSessionKey);
                }
                return s_PresentedVersions;
            }

            /// <summary>只在现有窗口实际弹出后记录目标版本，不改用户选择的当天跳过设置。</summary>
            private static void RememberAutomaticPresentation(DateTime day, List<UpdateInfo> external, List<UpdateInfo> internalItems)
            {
                Dictionary<string, string> presented = PresentedVersionsForDay(day);
                void Remember(IEnumerable<UpdateInfo> items, bool isInternal)
                {
                    foreach (UpdateInfo item in FilterUnpresentedUpdates(items, isInternal, presented))
                        presented[BuildSkipKey(item.PackageName, isInternal)] = item.LatestVersion;
                }
                Remember(external, false);
                Remember(internalItems, true);
                try
                {
                    var saved = new JObject { ["day"] = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["versions"] = JObject.FromObject(presented) };
                    SessionState.SetString(c_PresentedVersionsSessionKey, saved.ToString(Newtonsoft.Json.Formatting.None));
                }
                catch (Exception)
                {
                    // 已显示的窗口和本域的防重复记录不依赖会话缓存写入。
                }
            }
        }
    }
}
