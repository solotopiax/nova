/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DoHRequestPlanner.cs
 * author:    taoye
 * created:   2026/7/16
 * descrip:   DoH 请求候选规划器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Net;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 根据 DoH 缓存与即时查询结果构造请求候选 URL。
    /// </summary>
    internal static class DoHRequestPlanner
    {
        /// <summary>
        /// 构造请求候选 URL。
        /// </summary>
        /// <param name="originalUrl">原始请求 URL。</param>
        /// <param name="useDoH">是否启用 DoH。</param>
        /// <param name="canUseIpCandidate">是否允许生成 IP 直连候选。</param>
        /// <param name="getCachedAddresses">按主机名读取缓存地址的委托。</param>
        /// <param name="queryOnCacheMissAsync">缓存未命中时执行查询的委托。</param>
        /// <returns>按 IP 候选、原始 URL 顺序排列的候选列表。</returns>
        internal static async UniTask<IReadOnlyList<string>> BuildCandidatesAsync(
            string originalUrl,
            bool useDoH,
            bool canUseIpCandidate,
            Func<string, IPAddress[]> getCachedAddresses,
            Func<string, UniTask> queryOnCacheMissAsync)
        {
            List<string> candidates = new List<string>();
            HashSet<string> uniqueCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string hostName = GetHostName(originalUrl);

            if (useDoH &&
                !string.IsNullOrEmpty(hostName) &&
                !string.Equals(hostName, "localhost", StringComparison.OrdinalIgnoreCase) &&
                !IPAddress.TryParse(hostName, out _))
            {
                IPAddress[] cachedAddresses = getCachedAddresses(hostName);
                if (cachedAddresses == null || cachedAddresses.Length == 0)
                {
                    try
                    {
                        await queryOnCacheMissAsync(originalUrl);
                    }
                    catch (Exception)
                    {
                        // 查询失败时保留原始 URL 作为兜底。
                    }

                    cachedAddresses = getCachedAddresses(hostName);
                }

                if (canUseIpCandidate && cachedAddresses != null)
                {
                    for (int i = 0; i < cachedAddresses.Length; i++)
                    {
                        if (TryBuildUrlWithIPAddress(originalUrl, cachedAddresses[i], out string candidateUrl) &&
                            uniqueCandidates.Add(candidateUrl))
                        {
                            candidates.Add(candidateUrl);
                        }
                    }
                }
            }

            if (uniqueCandidates.Add(originalUrl))
            {
                candidates.Add(originalUrl);
            }

            return candidates;
        }

        /// <summary>
        /// 从 HTTP / HTTPS / WS / WSS URL 中提取主机名。
        /// </summary>
        internal static string GetHostName(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri) &&
                (uri.Scheme == Uri.UriSchemeHttp ||
                 uri.Scheme == Uri.UriSchemeHttps ||
                 string.Equals(uri.Scheme, "ws", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase)))
            {
                return uri.Host.Trim().Trim('[', ']').TrimEnd('.');
            }

            return string.Empty;
        }

        /// <summary>
        /// 用指定 IP 替换 URL 的 host 部分，保留协议、端口、路径和查询字符串不变。
        /// </summary>
        internal static bool TryBuildUrlWithIPAddress(string url, IPAddress ipAddress, out string resolvedUrl)
        {
            resolvedUrl = null;
            if (ipAddress == null || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            UriBuilder builder = new UriBuilder(uri)
            {
                Host = ipAddress.ToString()
            };

            resolvedUrl = builder.Uri.AbsoluteUri;
            return true;
        }
    }
}
