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
using System.Threading;
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
        /// 在一个独立的完整链路截止时间内解析原始 URL，并构造尚未写入共享缓存的本地结果。
        /// </summary>
        /// <param name="url">原始请求 URL。</param>
        /// <param name="source">本次解析的来源。</param>
        /// <param name="queryGeneration">发起查询时的 DoH 查询代次。</param>
        /// <returns>完整解析结果；URL 无效或查询代次失效时返回 null。</returns>
        private async UniTask<HostResolutionResult> ResolveUrlAsync(
            string url,
            DoHResolutionSource source,
            int queryGeneration)
        {
            string hostName = NormalizeHostName(GetHostName(url));
            if (string.IsNullOrEmpty(hostName) || !IsCurrentQueryGeneration(queryGeneration))
            {
                return null;
            }

            DateTime? deadlineUtc = CreateQueryDeadlineUtc(m_DNSTimeout);
            DNSAnswer[] answers = await QueryHostAnswersAsync(hostName, deadlineUtc, queryGeneration);
            if (!IsCurrentQueryGeneration(queryGeneration))
            {
                return null;
            }

            DoHResolutionNode root = new DoHResolutionNode(hostName, source);
            List<IPAddress> resolvedIPs = await ResolveIPAddressesAsync(
                hostName,
                answers,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                root,
                deadlineUtc,
                queryGeneration);
            if (!IsCurrentQueryGeneration(queryGeneration))
            {
                return null;
            }

            ApplyResolutionResult(root, resolvedIPs);
            return new HostResolutionResult(url, hostName, answers, root, resolvedIPs);
        }

        /// <summary>
        /// 对指定主机名查询 A 记录；响应中的 CNAME 由上层继续沿链解析。
        /// </summary>
        /// <param name="hostName">目标主机名。</param>
        /// <param name="deadlineUtc">原始域名完整解析链的 UTC 截止时间；null 表示无限等待。</param>
        /// <param name="queryGeneration">发起查询时的 DoH 查询代次。</param>
        /// <returns>DNS 应答数组，查询失败时返回 null。</returns>
        private async UniTask<DNSAnswer[]> QueryHostAnswersAsync(
            string hostName,
            DateTime? deadlineUtc,
            int queryGeneration)
        {
            string normalizedHostName = NormalizeHostName(hostName);
            if (!m_UseDoH || !IsCurrentQueryGeneration(queryGeneration) ||
                string.IsNullOrEmpty(normalizedHostName) || IPAddress.TryParse(normalizedHostName, out _))
            {
                return null;
            }

            try
            {
                DoHClient client = GetDoHClient(normalizedHostName);
                if (client == null)
                {
                    return null;
                }

                DNSAnswer[] answers = await client.QueryAsync(ResourceRecordType.A, deadlineUtc);
                return IsCurrentQueryGeneration(queryGeneration) ? answers : null;
            }
            catch (Exception e)
            {
                if (m_ConfigManager?.DevelopMode == DevelopMode.Debug)
                {
                    Log.Error(LogTag.DoH, "QueryHostAnswersAsync 异常，host：{0}，异常信息：{1}。", normalizedHostName, e);
                }

                return null;
            }
        }

        /// <summary>
        /// 将单个原始域名已经完整解析好的本地结果串行写入共享缓存。
        /// </summary>
        /// <param name="result">完整解析结果。</param>
        /// <param name="queryGeneration">发起查询时的 DoH 查询代次。</param>
        private void CommitResolutionResult(HostResolutionResult result, int queryGeneration)
        {
            if (result == null || !IsCurrentQueryGeneration(queryGeneration))
            {
                return;
            }

            if (m_ResolutionRoots.TryGetValue(result.HostName, out DoHResolutionNode existingRoot) &&
                existingRoot.Source == DoHResolutionSource.HostKeyPrewarm)
            {
                result.Root.Source = DoHResolutionSource.HostKeyPrewarm;
            }

            m_ResolutionRoots[result.HostName] = result.Root;
            if (result.IPAddresses == null || result.IPAddresses.Count == 0)
            {
                ClearCachedHost(result.HostName);
                return;
            }

            ReplaceCachedIPs(result.HostName, result.IPAddresses);
            string normalizedHostName = NormalizeHostName(result.HostName);
            if (m_AllDomainIPAddresses.TryGetValue(normalizedHostName, out List<IPAddress> cachedIPs))
            {
                RefreshCachedUrlsForHost(result.Url, normalizedHostName, cachedIPs);
            }
        }

        /// <summary>
        /// 从 DNS 应答中解析最终可用的 IP 地址；若命中 CNAME，则沿用原始域名的截止时间递归查询。
        /// </summary>
        /// <param name="hostName">当前主机名。</param>
        /// <param name="answers">DNS 应答数组。</param>
        /// <param name="visitedHosts">已访问主机名集合，避免循环解析。</param>
        /// <param name="root">当前解析层级对应的诊断节点。</param>
        /// <param name="deadlineUtc">原始域名完整解析链的 UTC 截止时间；null 表示无限等待。</param>
        /// <param name="queryGeneration">发起查询时的 DoH 查询代次。</param>
        /// <returns>解析出的 IP 地址列表。</returns>
        private async UniTask<List<IPAddress>> ResolveIPAddressesAsync(
            string hostName,
            DNSAnswer[] answers,
            HashSet<string> visitedHosts,
            DoHResolutionNode root,
            DateTime? deadlineUtc,
            int queryGeneration)
        {
            List<IPAddress> resolvedIPs = new List<IPAddress>();
            if (!IsCurrentQueryGeneration(queryGeneration) || answers == null || answers.Length == 0)
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
                if (!IsCurrentQueryGeneration(queryGeneration))
                {
                    return resolvedIPs;
                }

                string cnameHost = cnameHosts[i];
                DoHResolutionNode child = new DoHResolutionNode(cnameHost, root.Source);
                root.Children.Add(child);
                DNSAnswer[] cnameAnswers = await QueryHostAnswersAsync(cnameHost, deadlineUtc, queryGeneration);
                List<IPAddress> cnameIPs = await ResolveIPAddressesAsync(
                    cnameHost,
                    cnameAnswers,
                    visitedHosts,
                    child,
                    deadlineUtc,
                    queryGeneration);
                if (!IsCurrentQueryGeneration(queryGeneration))
                {
                    return resolvedIPs;
                }

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
        /// 判断异步查询是否仍属于当前有效代次，防止 Clear / Shutdown 后旧结果重新写入缓存。
        /// </summary>
        /// <param name="queryGeneration">发起查询时记录的代次。</param>
        /// <returns>代次仍有效时返回 true。</returns>
        private bool IsCurrentQueryGeneration(int queryGeneration)
        {
            return queryGeneration == Volatile.Read(ref m_QueryGeneration);
        }

        /// <summary>
        /// 根据配置的毫秒数为一个原始域名创建唯一的绝对截止时间。
        /// </summary>
        /// <param name="timeout">完整解析链超时时间（毫秒）；小于等于 0 表示无限等待。</param>
        /// <returns>UTC 截止时间；无限等待时返回 null。</returns>
        private static DateTime? CreateQueryDeadlineUtc(int timeout)
        {
            return timeout > 0 ? DateTime.UtcNow.AddMilliseconds(timeout) : (DateTime?)null;
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
        /// 使用本次非空解析结果整体替换域名缓存，并在限制数量前按应答顺序去重。
        /// </summary>
        /// <param name="hostName">目标主机名。</param>
        /// <param name="resolvedIPs">解析出的 IP 地址列表。</param>
        private void ReplaceCachedIPs(string hostName, List<IPAddress> resolvedIPs)
        {
            string normalizedHostName = NormalizeHostName(hostName);
            if (string.IsNullOrEmpty(normalizedHostName) || resolvedIPs == null || resolvedIPs.Count == 0)
            {
                return;
            }

            var cachedIPs = new List<IPAddress>();
            var uniqueIPs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < resolvedIPs.Count; i++)
            {
                if (m_MaxIPAddressesPerHost > 0 && cachedIPs.Count >= m_MaxIPAddressesPerHost)
                {
                    break;
                }

                IPAddress resolvedIP = resolvedIPs[i];
                if (uniqueIPs.Add(resolvedIP.ToString()))
                {
                    cachedIPs.Add(resolvedIP);
                }
            }

            m_AllDomainIPAddresses[normalizedHostName] = cachedIPs;
        }

        /// <summary>
        /// 删除指定主机名的域名 IP 缓存及全部 URL 快照，避免刷新失败后继续使用旧 IP。
        /// </summary>
        /// <param name="hostName">需要清理的主机名。</param>
        private void ClearCachedHost(string hostName)
        {
            string normalizedHostName = NormalizeHostName(hostName);
            if (string.IsNullOrEmpty(normalizedHostName))
            {
                return;
            }

            m_AllDomainIPAddresses.Remove(normalizedHostName);
            List<string> affectedUrls = GetCachedUrlsForHost(normalizedHostName);
            for (int i = 0; i < affectedUrls.Count; i++)
            {
                m_AllCollectedIPAddresses.Remove(affectedUrls[i]);
            }
        }

        /// <summary>
        /// 使用指定主机名的新 IP 列表同步刷新该主机名下全部 URL 快照。
        /// </summary>
        /// <param name="resolvedUrl">本次解析直接对应的原始 URL。</param>
        /// <param name="normalizedHostName">已归一化的主机名。</param>
        /// <param name="cachedIPs">本次解析后保留的 IP 列表。</param>
        private void RefreshCachedUrlsForHost(
            string resolvedUrl,
            string normalizedHostName,
            List<IPAddress> cachedIPs)
        {
            List<string> affectedUrls = GetCachedUrlsForHost(normalizedHostName);
            if (!string.IsNullOrEmpty(resolvedUrl) && !m_AllCollectedIPAddresses.ContainsKey(resolvedUrl))
            {
                affectedUrls.Add(resolvedUrl);
            }

            for (int i = 0; i < affectedUrls.Count; i++)
            {
                CacheCollectedUrls(affectedUrls[i], cachedIPs);
            }
        }

        /// <summary>
        /// 收集当前 URL 快照中属于指定主机名的全部 URL，调用方可在返回后安全修改缓存字典。
        /// </summary>
        /// <param name="normalizedHostName">已归一化的主机名。</param>
        /// <returns>同一主机名下的 URL 快照键列表。</returns>
        private List<string> GetCachedUrlsForHost(string normalizedHostName)
        {
            var urls = new List<string>();
            if (string.IsNullOrEmpty(normalizedHostName))
            {
                return urls;
            }

            foreach (string url in m_AllCollectedIPAddresses.Keys)
            {
                string cachedHostName = NormalizeHostName(GetHostName(url));
                if (string.Equals(cachedHostName, normalizedHostName, StringComparison.OrdinalIgnoreCase))
                {
                    urls.Add(url);
                }
            }

            return urls;
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
        /// 判断 DNS 应答是否包含可用的 IPv4 地址。
        /// </summary>
        /// <param name="answer">DNS 应答。</param>
        /// <param name="parsedIP">解析出的 IP 地址。</param>
        /// <returns>是否成功解析出可用 IP。</returns>
        private static bool TryParseIPAddress(DNSAnswer answer, out IPAddress parsedIP)
        {
            parsedIP = null;
            if (answer == null || answer.RecordType != ResourceRecordType.A)
            {
                return false;
            }

            return IPAddress.TryParse(answer.Data, out parsedIP) &&
                   parsedIP.AddressFamily == AddressFamily.InterNetwork;
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

        /// <summary>
        /// 单个原始 URL 的本地 DoH 完整解析结果；完成整条 CNAME 链后才会写入共享缓存。
        /// </summary>
        private sealed class HostResolutionResult
        {
            /// <summary>
            /// 初始化单个原始 URL 的完整解析结果。
            /// </summary>
            /// <param name="url">原始请求 URL。</param>
            /// <param name="hostName">归一化后的原始域名。</param>
            /// <param name="answers">根域名的 A 查询应答，可能同时包含 CNAME 记录。</param>
            /// <param name="root">包含 CNAME 子树的诊断根节点。</param>
            /// <param name="ipAddresses">整条解析链得到的 IP 地址。</param>
            public HostResolutionResult(
                string url,
                string hostName,
                DNSAnswer[] answers,
                DoHResolutionNode root,
                List<IPAddress> ipAddresses)
            {
                Url = url;
                HostName = hostName;
                Answers = answers;
                Root = root;
                IPAddresses = ipAddresses;
            }

            /// <summary>
            /// 原始请求 URL。
            /// </summary>
            public string Url { get; }

            /// <summary>
            /// 归一化后的原始域名。
            /// </summary>
            public string HostName { get; }

            /// <summary>
            /// 根域名的 A 查询应答，可能同时包含 CNAME 记录。
            /// </summary>
            public DNSAnswer[] Answers { get; }

            /// <summary>
            /// 包含 CNAME 子树的诊断根节点。
            /// </summary>
            public DoHResolutionNode Root { get; }

            /// <summary>
            /// 整条解析链得到的 IP 地址。
            /// </summary>
            public List<IPAddress> IPAddresses { get; }
        }
    }
}
