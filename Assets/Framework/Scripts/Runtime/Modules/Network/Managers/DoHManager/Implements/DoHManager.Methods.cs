/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DoHManager.Methods.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   DoH管理器 —— 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DoH 管理器。
    /// </summary>
    internal sealed partial class DoHManager : DoHManagerBase
    {
        /// <summary>
        /// 获取或创建指定主机名的 DoH 查询器。
        /// </summary>
        /// <param name="hostName">主机名。</param>
        /// <returns>对应的 DoHClient 实例。</returns>
        private DoHClient GetDoHClient(string hostName)
        {
            string normalizedHostName = NormalizeHostName(hostName);
            if (string.IsNullOrEmpty(normalizedHostName))
            {
                return null;
            }

            if (!m_DoHClients.TryGetValue(normalizedHostName, out DoHClient client))
            {
                client = new DoHClient(normalizedHostName);
                m_DoHClients[normalizedHostName] = client;
            }

            return client;
        }

        /// <summary>
        /// 对指定 URL 执行 DoH DNS 查询，返回应答数组（供并行收集调用）。
        /// </summary>
        /// <param name="url">目标 URL。</param>
        /// <returns>DNS 应答数组，查询失败时返回 null。</returns>
        private async UniTask<DNSAnswer[]> QueryDNSResultAsync(string url)
        {
            string hostName = GetHostName(url);
            if (string.IsNullOrEmpty(hostName))
            {
                return null;
            }

            return await QueryHostAnswersAsync(hostName);
        }

        /// <summary>
        /// 对指定主机名执行 DoH DNS 查询，返回应答数组。
        /// </summary>
        /// <param name="hostName">目标主机名。</param>
        /// <returns>DNS 应答数组，查询失败时返回 null。</returns>
        private async UniTask<DNSAnswer[]> QueryHostAnswersAsync(string hostName)
        {
            string normalizedHostName = NormalizeHostName(hostName);
            if (!m_UseDoH || string.IsNullOrEmpty(normalizedHostName) || IPAddress.TryParse(normalizedHostName, out _))
            {
                return null;
            }

            try
            {
                DoHClient client = GetDoHClient(normalizedHostName);
                return client != null ? await client.QueryAsync(m_DNSTimeout) : null;
            }
            catch (Exception e)
            {
                if (m_ConfigManager?.DevelopMode == DevelopMode.Debug)
                {
                    Log.Error(LogTag.DoH, "QueryDNSResultAsync 异常，host：{0}，异常信息：{1}。", normalizedHostName, e);
                }

                return null;
            }
        }

        /// <summary>
        /// 将 DoH 查询结果写入缓存：域名 -> IP 列表，以及原始 URL -> 替换后 URL 列表。
        /// </summary>
        /// <param name="url">原始请求 URL。</param>
        /// <param name="answers">DoH 应答数组。</param>
        private async UniTask CacheDnsAnswersAsync(string url, DNSAnswer[] answers, DoHResolutionSource source)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            string hostName = GetHostName(url);
            if (string.IsNullOrEmpty(hostName))
            {
                return;
            }

            DoHResolutionNode root = GetOrCreateResolutionRoot(hostName, source);
            root.Addresses.Clear();
            root.Children.Clear();
            root.FailureReason = null;

            List<IPAddress> resolvedIPs = await ResolveIPAddressesAsync(
                hostName,
                answers,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                root);
            ApplyResolutionResult(root, resolvedIPs);
            if (resolvedIPs.Count == 0)
            {
                return;
            }

            MergeCachedIPs(hostName, resolvedIPs);
            if (m_AllDomainIPAddresses.TryGetValue(hostName, out List<IPAddress> cachedIPs))
            {
                CacheCollectedUrls(url, cachedIPs);
            }
        }

        /// <summary>
        /// 从 DNS 应答中解析最终可用的 IP 地址；若命中 CNAME，则递归查询别名目标。
        /// </summary>
        /// <param name="hostName">当前主机名。</param>
        /// <param name="answers">DNS 应答数组。</param>
        /// <param name="visitedHosts">已访问主机名集合，避免循环解析。</param>
        /// <returns>解析出的 IP 地址列表。</returns>
        private async UniTask<List<IPAddress>> ResolveIPAddressesAsync(
            string hostName,
            DNSAnswer[] answers,
            HashSet<string> visitedHosts,
            DoHResolutionNode root)
        {
            List<IPAddress> resolvedIPs = new List<IPAddress>();
            if (answers == null || answers.Length == 0)
            {
                return resolvedIPs;
            }

            string normalizedHostName = NormalizeHostName(hostName);
            if (string.IsNullOrEmpty(normalizedHostName) || !visitedHosts.Add(normalizedHostName))
            {
                return resolvedIPs;
            }

            HashSet<string> seenIPs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> cnameHosts = new List<string>();

            foreach (DNSAnswer answer in answers)
            {
                if (IsAnswerForHost(answer, normalizedHostName) && TryParseIPAddress(answer, out IPAddress parsedIP))
                {
                    if (seenIPs.Add(parsedIP.ToString()))
                    {
                        resolvedIPs.Add(parsedIP);
                        root.Addresses.Add(parsedIP);
                    }

                    continue;
                }

                if (answer?.RecordType == ResourceRecordType.CNAME && IsAnswerForHost(answer, normalizedHostName))
                {
                    string cnameHost = NormalizeHostName(answer.Data);
                    if (!string.IsNullOrEmpty(cnameHost) && !visitedHosts.Contains(cnameHost) && !cnameHosts.Contains(cnameHost))
                    {
                        cnameHosts.Add(cnameHost);
                    }
                }
            }

            for (int i = 0; i < cnameHosts.Count; i++)
            {
                string cnameHost = cnameHosts[i];
                DoHResolutionNode child = new DoHResolutionNode(cnameHost, root.Source);
                root.Children.Add(child);
                DNSAnswer[] cnameAnswers = await QueryHostAnswersAsync(cnameHost);
                List<IPAddress> cnameIPs = await ResolveIPAddressesAsync(cnameHost, cnameAnswers, visitedHosts, child);
                ApplyResolutionResult(child, cnameIPs);
                if (cnameIPs.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < cnameIPs.Count; j++)
                {
                    IPAddress cnameIP = cnameIPs[j];
                    if (seenIPs.Add(cnameIP.ToString()))
                    {
                        resolvedIPs.Add(cnameIP);
                    }
                }
            }

            return resolvedIPs;
        }

        /// <summary>
        /// 获取或创建原始业务域名的诊断树根节点。
        /// </summary>
        private DoHResolutionNode GetOrCreateResolutionRoot(string hostName, DoHResolutionSource source)
        {
            string normalizedHostName = NormalizeHostName(hostName);
            if (!m_ResolutionRoots.TryGetValue(normalizedHostName, out DoHResolutionNode root))
            {
                root = new DoHResolutionNode(normalizedHostName, source);
                m_ResolutionRoots[normalizedHostName] = root;
            }
            else if (source == DoHResolutionSource.HostKeyPrewarm)
            {
                root.Source = DoHResolutionSource.HostKeyPrewarm;
            }

            return root;
        }

        /// <summary>
        /// 将一次查询的最终结果应用到诊断节点，失败节点也会被保留。
        /// </summary>
        private static void ApplyResolutionResult(DoHResolutionNode node, List<IPAddress> resolvedIPs)
        {
            node.FailureReason = resolvedIPs == null || resolvedIPs.Count == 0 ? "未获取 IP" : null;
        }

        /// <summary>
        /// 判断应答记录是否属于当前解析层级的域名。
        /// </summary>
        private static bool IsAnswerForHost(DNSAnswer answer, string hostName)
        {
            return answer != null && string.Equals(
                NormalizeHostName(answer.Name),
                hostName,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将解析出的 IP 地址合并进域名缓存，保持首次成功顺序不变。
        /// </summary>
        /// <param name="hostName">目标主机名。</param>
        /// <param name="resolvedIPs">解析出的 IP 地址列表。</param>
        private void MergeCachedIPs(string hostName, List<IPAddress> resolvedIPs)
        {
            string normalizedHostName = NormalizeHostName(hostName);
            if (string.IsNullOrEmpty(normalizedHostName) || resolvedIPs == null || resolvedIPs.Count == 0)
            {
                return;
            }

            if (!m_AllDomainIPAddresses.TryGetValue(normalizedHostName, out List<IPAddress> cachedIPs))
            {
                cachedIPs = new List<IPAddress>();
                m_AllDomainIPAddresses[normalizedHostName] = cachedIPs;
            }

            HashSet<string> existingIPs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < cachedIPs.Count; i++)
            {
                existingIPs.Add(cachedIPs[i].ToString());
            }

            for (int i = 0; i < resolvedIPs.Count; i++)
            {
                IPAddress resolvedIP = resolvedIPs[i];
                if (existingIPs.Add(resolvedIP.ToString()))
                {
                    cachedIPs.Add(resolvedIP);
                }
            }
        }

        /// <summary>
        /// 根据当前域名缓存刷新指定原始 URL 的 IP 替换候选列表。
        /// </summary>
        /// <param name="url">原始请求 URL。</param>
        /// <param name="cachedIPs">该 URL 对应主机名的已缓存 IP 列表。</param>
        private void CacheCollectedUrls(string url, List<IPAddress> cachedIPs)
        {
            if (string.IsNullOrEmpty(url) || cachedIPs == null || cachedIPs.Count == 0)
            {
                return;
            }

            if (!m_AllCollectedIPAddresses.TryGetValue(url, out List<string> collectedUrls))
            {
                collectedUrls = new List<string>();
                m_AllCollectedIPAddresses[url] = collectedUrls;
            }
            else
            {
                collectedUrls.Clear();
            }

            for (int i = 0; i < cachedIPs.Count; i++)
            {
                if (DoHRequestPlanner.TryBuildUrlWithIPAddress(url, cachedIPs[i], out string resolvedUrl) && !collectedUrls.Contains(resolvedUrl))
                {
                    collectedUrls.Add(resolvedUrl);
                }
            }
        }

        /// <summary>
        /// 判断 DNS 应答是否包含可用的 IPv4 / IPv6 地址。
        /// </summary>
        /// <param name="answer">DNS 应答。</param>
        /// <param name="parsedIP">解析出的 IP 地址。</param>
        /// <returns>是否成功解析出可用 IP。</returns>
        private static bool TryParseIPAddress(DNSAnswer answer, out IPAddress parsedIP)
        {
            parsedIP = null;
            if (answer == null || (answer.RecordType != ResourceRecordType.A && answer.RecordType != ResourceRecordType.AAAA))
            {
                return false;
            }

            return IPAddress.TryParse(answer.Data, out parsedIP) &&
                   (parsedIP.AddressFamily == AddressFamily.InterNetwork || parsedIP.AddressFamily == AddressFamily.InterNetworkV6);
        }

        /// <summary>
        /// 归一化主机名：裁掉空白、方括号和结尾的点。
        /// </summary>
        /// <param name="hostName">原始主机名。</param>
        /// <returns>归一化后的主机名。</returns>
        private static string NormalizeHostName(string hostName)
        {
            if (string.IsNullOrWhiteSpace(hostName))
            {
                return string.Empty;
            }

            return hostName.Trim().Trim('[', ']').TrimEnd('.');
        }
    }
}
