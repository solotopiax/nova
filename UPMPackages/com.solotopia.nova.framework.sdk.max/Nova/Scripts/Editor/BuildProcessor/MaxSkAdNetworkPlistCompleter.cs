/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxSkAdNetworkPlistCompleter.cs
 * author:    yingzheng
 * created:   2026/8/6
 * descrip:   iOS 后处理补全 SKAdNetworkItems，离线枚举 MAX 适配器网络，绕开 AppLovin Client.List() 超时
 ***************************************************************/

#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using IOPath = System.IO.Path;
using NovaFramework.Runtime;

using UnityEditor.iOS.Xcode;
using UnityEditor.PackageManager;

using UnityEngine;
using UnityEngine.Networking;

namespace NovaFramework.SDK.MaxAdPlugin.Editor
{
    /// <summary>
    /// iOS SKAdNetworkItems 补全工具。
    /// 背景：AppLovin 的 iOS 后处理靠 <c>GetPackageCollectionSync</c>（内部 <c>Client.List()</c>，硬编码 10s 超时）
    /// 发现已装广告网络，冷启动/慢 registry 时会超时，导致向 skadnetwork_ids 接口请求时缺失 ad_networks 参数，
    /// 只写入基线 SKAdNetwork ID（实测约 152 条，缺各网络专属 ID 约 100 条）。
    /// 本工具改用同步、离线、不会超时的 <see cref="PackageInfo.GetAllRegisteredPackages"/> 重算完整网络列表，
    /// 请求完整 ID 后增量补齐 Info.plist，由 Nova 后处理器在 AppLovin 之后调用（callbackOrder=int.MaxValue）。
    /// 只增不删、幂等，请求失败时原样保留 AppLovin 已写内容，绝不使结果变差。
    /// </summary>
    internal static class MaxSkAdNetworkPlistCompleter
    {
        /// <summary>MAX 广告网络适配器包名前缀。</summary>
        private const string c_AdapterPackagePrefix = "com.applovin.mediation.adapters";

        /// <summary>MAX DSP 包名前缀。</summary>
        private const string c_DspPackagePrefix = "com.applovin.mediation.dsp";

        /// <summary>适配器包 keywords 中标识网络目录名的前缀，例如 "dir:Google"。</summary>
        private const string c_DirKeywordPrefix = "dir:";

        /// <summary>Info.plist 中 SKAdNetwork 列表键名。</summary>
        private const string c_SkAdNetworkItemsKey = "SKAdNetworkItems";

        /// <summary>SKAdNetwork 列表中单条 ID 的键名。</summary>
        private const string c_SkAdNetworkIdentifierKey = "SKAdNetworkIdentifier";

        /// <summary>AppLovin 提供的 SKAdNetwork ID 查询接口。</summary>
        private const string c_SkAdNetworkEndpoint = "https://unity.applovin.com/max/1.0/skadnetwork_ids";

        /// <summary>接口请求超时秒数，给足冷启动/弱网余量。</summary>
        private const int c_RequestTimeoutSeconds = 60;

        /// <summary>
        /// 补全指定 Info.plist 根字典的 SKAdNetworkItems。
        /// 流程：离线枚举已装网络 -> 请求完整 SKAdNetwork ID -> 增量合并进 plist；任一前置步骤失败即安全跳过。
        /// </summary>
        /// <param name="plistDict">Info.plist 根字典（NovaBuildContext.XPlistDict），已包含 AppLovin 写入内容。</param>
        public static void Complete(PlistElementDict plistDict)
        {
            if (plistDict == null)
            {
                Log.Warning(LogTag.Editor, "[MaxSkAdNetworkPlistCompleter] plistDict 为 null，跳过 SKAdNetworkItems 补全。");
                return;
            }

            int before = CountSkAdNetworkItems(plistDict);

            List<string> networks = CollectInstalledNetworks();
            if (networks.Count == 0)
            {
                Log.Warning(LogTag.Editor, "[MaxSkAdNetworkPlistCompleter] 未发现已装 MAX 适配器/DSP 包，跳过 SKAdNetworkItems 补全。");
                return;
            }

            string[] ids = FetchSkAdNetworkIds(networks);
            if (ids == null || ids.Length == 0)
            {
                Log.Warning(LogTag.Editor, "[MaxSkAdNetworkPlistCompleter] 获取 SKAdNetwork ID 失败或为空，保留 AppLovin 已写内容，跳过补全。");
                return;
            }

            int added = MergeIntoPlist(plistDict, ids);
            Log.Debug(LogTag.Editor,
                $"[MaxSkAdNetworkPlistCompleter] SKAdNetworkItems 补全完成：{before} -> {before + added}（新增 {added}，" +
                $"离线网络 {networks.Count} 个，接口返回 {ids.Length} 条）。");
        }

        /// <summary>
        /// 同步、离线枚举当前已安装的 MAX 广告网络目录名（等价 AppLovin 的 dir: 关键字，但不走会超时的 Client.List()）。
        /// 优先取 PackageInfo.keywords；keywords 为空时退回读该包 package.json 的 keywords 兜底。
        /// </summary>
        /// <returns>去重后的网络目录名列表，例如 Google / Facebook / Mintegral。</returns>
        private static List<string> CollectInstalledNetworks()
        {
            List<string> networks = new List<string>();
            HashSet<string> seen = new HashSet<string>();

            // GetAllRegisteredPackages 同步返回已解析的项目包列表，无网络请求、不会超时。
            PackageInfo[] packages = PackageInfo.GetAllRegisteredPackages();
            if (packages == null) return networks;

            foreach (PackageInfo package in packages)
            {
                if (package == null || string.IsNullOrEmpty(package.name)) continue;
                if (!package.name.StartsWith(c_AdapterPackagePrefix, StringComparison.Ordinal) &&
                    !package.name.StartsWith(c_DspPackagePrefix, StringComparison.Ordinal)) continue;

                string[] keywords = package.keywords;
                if (keywords == null || keywords.Length == 0)
                {
                    // 兜底：PackageInfo 未填充 keywords 时，直接读磁盘上的 package.json。
                    keywords = ReadKeywordsFromDisk(package);
                }
                if (keywords == null) continue;

                foreach (string keyword in keywords)
                {
                    if (string.IsNullOrEmpty(keyword) || !keyword.StartsWith(c_DirKeywordPrefix, StringComparison.Ordinal)) continue;

                    string network = keyword.Substring(c_DirKeywordPrefix.Length);
                    if (!string.IsNullOrEmpty(network) && seen.Add(network))
                        networks.Add(network);
                }
            }

            return networks;
        }

        /// <summary>
        /// 兜底读取指定包 resolvedPath 下 package.json 的 keywords 字段。
        /// </summary>
        /// <param name="package">目标包信息。</param>
        /// <returns>keywords 数组；读取或解析失败时返回 null。</returns>
        private static string[] ReadKeywordsFromDisk(PackageInfo package)
        {
            if (package == null || string.IsNullOrEmpty(package.resolvedPath)) return null;

            try
            {
                string manifestPath = IOPath.Combine(package.resolvedPath, "package.json");
                if (!File.Exists(manifestPath)) return null;

                PackageManifestKeywords manifest = JsonUtility.FromJson<PackageManifestKeywords>(File.ReadAllText(manifestPath));
                return manifest != null ? manifest.keywords : null;
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Editor, $"[MaxSkAdNetworkPlistCompleter] 读取 {package.name} 的 package.json keywords 失败：{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 同步请求 skadnetwork_ids 接口，返回给定网络对应的完整 SKAdNetwork ID 列表。
        /// 编辑器内阻塞等待完成（与 AppLovin MaxWebRequest.SendSync 一致），依赖 timeout 兜底不会永久挂起。
        /// </summary>
        /// <param name="networks">已装网络目录名列表，用于拼接 ad_networks 查询参数。</param>
        /// <returns>SKAdNetwork ID 数组；请求或解析失败时返回 null。</returns>
        private static string[] FetchSkAdNetworkIds(IReadOnlyList<string> networks)
        {
            string adNetworks = string.Join(",", networks);
            string url = $"{c_SkAdNetworkEndpoint}?ad_networks={adNetworks}";

            try
            {
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.timeout = c_RequestTimeoutSeconds;

                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    while (!operation.isDone) { } // 编辑器同步后处理中阻塞等待，超时由 request.timeout 保证

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Log.Warning(LogTag.Editor, $"[MaxSkAdNetworkPlistCompleter] 请求 SKAdNetwork ID 失败：{request.error}");
                        return null;
                    }

                    string json = request.downloadHandler != null ? request.downloadHandler.text : null;
                    if (string.IsNullOrEmpty(json)) return null;

                    SkAdNetworkResponse response = JsonUtility.FromJson<SkAdNetworkResponse>(json);
                    return response != null ? response.SkAdNetworkIds : null;
                }
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Editor, $"[MaxSkAdNetworkPlistCompleter] 请求或解析 SKAdNetwork ID 异常：{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 把 SKAdNetwork ID 增量合并进 Info.plist 的 SKAdNetworkItems（只增不删、去重幂等）。
        /// </summary>
        /// <param name="plistDict">Info.plist 根字典。</param>
        /// <param name="ids">待合并的 SKAdNetwork ID 列表。</param>
        /// <returns>本次实际新增的条目数。</returns>
        private static int MergeIntoPlist(PlistElementDict plistDict, IReadOnlyList<string> ids)
        {
            PlistElementArray items = plistDict[c_SkAdNetworkItemsKey] as PlistElementArray;
            if (items == null)
                items = plistDict.CreateArray(c_SkAdNetworkItemsKey);

            HashSet<string> existingIds = new HashSet<string>();
            foreach (PlistElement element in items.values)
            {
                if (element is PlistElementDict entry &&
                    entry.values.TryGetValue(c_SkAdNetworkIdentifierKey, out PlistElement idElement) &&
                    idElement is PlistElementString idString &&
                    !string.IsNullOrEmpty(idString.value))
                {
                    existingIds.Add(idString.value);
                }
            }

            int added = 0;
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id) || !existingIds.Add(id)) continue;

                PlistElementDict entry = items.AddDict();
                entry.SetString(c_SkAdNetworkIdentifierKey, id);
                added++;
            }

            return added;
        }

        /// <summary>
        /// 统计当前 Info.plist 中 SKAdNetworkItems 的条目数，用于补全前后日志对比。
        /// </summary>
        /// <param name="plistDict">Info.plist 根字典。</param>
        /// <returns>SKAdNetworkItems 现有条目数；不存在时返回 0。</returns>
        private static int CountSkAdNetworkItems(PlistElementDict plistDict)
        {
            return plistDict[c_SkAdNetworkItemsKey] is PlistElementArray items ? items.values.Count : 0;
        }

        /// <summary>
        /// skadnetwork_ids 接口响应体，形如 { "SkAdNetworkIds": [ "xxx.skadnetwork", ... ] }。
        /// 字段名须与接口 JSON 精确一致，供 JsonUtility 反序列化。
        /// </summary>
        [Serializable]
        private class SkAdNetworkResponse
        {
            /// <summary>接口返回的全部 SKAdNetwork ID。</summary>
            public string[] SkAdNetworkIds;
        }

        /// <summary>
        /// package.json 中 keywords 字段的最小承载体，仅用于兜底解析网络目录名。
        /// </summary>
        [Serializable]
        private class PackageManifestKeywords
        {
            /// <summary>包关键字数组，MAX 适配器用 "dir:网络名" 标识所属网络目录。</summary>
            public string[] keywords;
        }
    }
}
#endif
