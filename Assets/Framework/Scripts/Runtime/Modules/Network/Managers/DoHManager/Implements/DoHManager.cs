/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DoHManager.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   DoH管理器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DoH 管理器，负责 DNS-over-HTTPS 查询与 IP 地址收集。
    /// </summary>
    internal sealed partial class DoHManager : DoHManagerBase
    {
        /// <summary>
        /// 初始化 DoHManager 的新实例。
        /// </summary>
        public DoHManager()
        {
            m_DoHClients = new Dictionary<string, DoHClient>(StringComparer.OrdinalIgnoreCase);
            m_AllCollectedIPAddresses = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            m_AllDomainIPAddresses = new Dictionary<string, List<IPAddress>>(StringComparer.OrdinalIgnoreCase);
            m_ResolutionRoots = new Dictionary<string, DoHResolutionNode>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override void Initialize(DoHManagerConfig config)
        {
            m_UseDoH = config.UseDoH;
            m_DNSTimeout = config.DnsTimeoutSeconds <= 0
                ? System.Threading.Timeout.Infinite
                : (int)Math.Min(int.MaxValue, (long)config.DnsTimeoutSeconds * 1000L);
            m_MaxIPAddressesPerHost = config.MaxIPAddressesPerHost;
            ServicePointManager.DefaultConnectionLimit = 10;
            m_ConfigManager = FrameworkManagersGroup.GetManager<IConfigManager>();
        }

        /// <summary>
        /// 在当前进程内禁用 DoH 并清空已解析结果，后续请求统一使用系统 DNS。
        /// </summary>
        public override void DisableForRuntime()
        {
            if (!m_UseDoH)
            {
                return;
            }

            m_UseDoH = false;
            Clear();
        }

        /// <summary>
        /// 遍历给定的 URL 列表，并行完成各原始域名的 A 与 CNAME 全链解析。
        /// 每个域名先形成本地结果，全部完成后再串行写入共享缓存。
        /// </summary>
        /// <param name="urls">目标 URL 集合（由 NetworkManager.GetAllHostKeyUrls() 提供）。</param>
        /// <returns>异步任务。</returns>
        public override async UniTask CollectAllIPAddresses(IEnumerable<string> urls)
        {
            if (!m_UseDoH || urls == null)
            {
                return;
            }

            int queryGeneration = Volatile.Read(ref m_QueryGeneration);
            List<UniTask<HostResolutionResult>> queryTasks = new List<UniTask<HostResolutionResult>>();
            HashSet<string> uniqueUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string url in urls)
            {
                if (string.IsNullOrEmpty(url) || !uniqueUrls.Add(url))
                {
                    continue;
                }

                queryTasks.Add(ResolveUrlSafelyAsync(url, DoHResolutionSource.HostKeyPrewarm, queryGeneration));
            }

            if (queryTasks.Count == 0)
            {
                return;
            }

            HostResolutionResult[] allResults = await UniTask.WhenAll(queryTasks);
            if (!IsCurrentQueryGeneration(queryGeneration))
            {
                return;
            }

            for (int i = 0; i < allResults.Length; i++)
            {
                CommitResolutionResult(allResults[i], queryGeneration);
                if (!IsCurrentQueryGeneration(queryGeneration))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// 隔离单个预热域名的意外异常，使其他域名仍能完成提交，并让失败域名清除旧缓存。
        /// </summary>
        /// <param name="url">需要解析的原始 URL。</param>
        /// <param name="source">本次解析的来源。</param>
        /// <param name="queryGeneration">发起查询时的 DoH 查询代次。</param>
        /// <returns>正常结果或用于清除旧缓存的空结果；URL 无效或代次失效时返回 null。</returns>
        private async UniTask<HostResolutionResult> ResolveUrlSafelyAsync(
            string url,
            DoHResolutionSource source,
            int queryGeneration)
        {
            string hostName = NormalizeHostName(GetHostName(url));
            try
            {
                return await ResolveUrlAsync(url, source, queryGeneration);
            }
            catch (Exception exception)
            {
                if (m_ConfigManager?.DevelopMode == DevelopMode.Debug)
                {
                    Log.Error(LogTag.DoH, "DoH 预热异常，host：{0}，已清除旧缓存。异常信息：{1}。", hostName, exception);
                }

                if (!IsCurrentQueryGeneration(queryGeneration) || string.IsNullOrEmpty(hostName))
                {
                    return null;
                }

                var root = new DoHResolutionNode(hostName, source);
                var emptyIPs = new List<IPAddress>();
                ApplyResolutionResult(root, emptyIPs);
                return new HostResolutionResult(url, hostName, null, root, emptyIPs);
            }
        }

        /// <summary>
        /// 对指定 URL 执行 DoH DNS 查询，结果写入 DNSAnswers。
        /// </summary>
        /// <param name="url">目标 URL。</param>
        /// <returns>异步任务。</returns>
        public override async UniTask DNSQuery(string url)
        {
            if (!m_UseDoH)
            {
                m_DNSAnswers = null;
                return;
            }

            int queryGeneration = Volatile.Read(ref m_QueryGeneration);
            string hostName = GetHostName(url);
            if (string.IsNullOrEmpty(hostName))
            {
                m_DNSAnswers = null;
                return;
            }

            try
            {
                HostResolutionResult result = await ResolveUrlAsync(
                    url,
                    DoHResolutionSource.RuntimeDiscovered,
                    queryGeneration);
                if (!IsCurrentQueryGeneration(queryGeneration))
                {
                    return;
                }

                m_DNSAnswers = result?.Answers;
                CommitResolutionResult(result, queryGeneration);
            }
            catch (Exception e)
            {
                m_DNSAnswers = null;
                if (IsCurrentQueryGeneration(queryGeneration))
                {
                    ClearCachedHost(hostName);
                }

                if (m_ConfigManager?.DevelopMode == DevelopMode.Debug)
                {
                    Log.Error(LogTag.DoH, "DNSQuery 异常，host：{0}，已清除旧缓存。异常信息：{1}。", hostName, e);
                }
            }
        }

        /// <summary>
        /// 根据 DoH 缓存与即时查询结果构造请求候选 URL。
        /// </summary>
        /// <param name="originalUrl">原始请求 URL。</param>
        /// <param name="canUseIpCandidate">是否允许生成 IP 直连候选。</param>
        /// <returns>按 IP 候选、原始 URL 顺序排列的候选列表。</returns>
        public override async UniTask<IReadOnlyList<string>> BuildRequestUrlCandidatesAsync(string originalUrl, bool canUseIpCandidate)
        {
            return await DoHRequestPlanner.BuildCandidatesAsync(
                originalUrl,
                m_UseDoH,
                canUseIpCandidate,
                GetIPAddresses,
                DNSQuery);
        }

        /// <summary>
        /// 从 URL 中提取主机名（域名部分）。
        /// </summary>
        /// <param name="url">完整 URL 字符串。</param>
        /// <returns>主机名字符串，格式非法时返回空字符串。</returns>
        public override string GetHostName(string url)
        {
            return DoHRequestPlanner.GetHostName(url);
        }

        /// <summary>
        /// 通过主机名获取已收集的 IP 地址数组。
        /// </summary>
        /// <param name="hostName">目标主机名。</param>
        /// <returns>IP 地址数组，未收集时返回 null。</returns>
        public override IPAddress[] GetIPAddresses(string hostName)
        {
            string normalizedHostName = NormalizeHostName(hostName);
            if (string.IsNullOrEmpty(normalizedHostName))
            {
                return null;
            }

            if (m_AllDomainIPAddresses.TryGetValue(normalizedHostName, out List<IPAddress> list) && list.Count > 0)
            {
                return list.ToArray();
            }

            return null;
        }

        /// <summary>
        /// 清空所有已收集的 IP 地址与 DNS 缓存，并使清理前发起的异步查询失效。
        /// </summary>
        public override void Clear()
        {
            Interlocked.Increment(ref m_QueryGeneration);
            foreach (var kvp in m_DoHClients)
            {
                kvp.Value?.Dispose();
            }

            m_DoHClients.Clear();
            m_DNSAnswers = null;
            m_AllCollectedIPAddresses.Clear();
            m_AllDomainIPAddresses.Clear();
            m_ResolutionRoots.Clear();
        }

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public override void Update()
        {
        }

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public override void Shutdown()
        {
            Clear();
        }
    }
}
