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
        }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override void Initialize(DoHManagerConfig config)
        {
            m_UseDoH = config.UseDoH;
            m_DNSTimeout = Math.Max(0, config.DnsTimeoutSeconds * 1000);
            ServicePointManager.DefaultConnectionLimit = 10;
            m_ConfigManager = FrameworkManagersGroup.GetManager<IConfigManager>();
        }

        /// <summary>
        /// 遍历给定的 URL 列表，并行异步收集各域名 IP 地址。
        /// DNS 查询阶段并行执行，结果写入字典阶段串行处理以避免竞态。
        /// </summary>
        /// <param name="urls">目标 URL 集合（由 NetworkManager.GetAllNetCmdUrls() 提供）。</param>
        /// <returns>异步任务。</returns>
        public override async UniTask CollectAllIPAddresses(IEnumerable<string> urls)
        {
            if (!m_UseDoH || urls == null)
            {
                return;
            }

            List<string> validUrls = new List<string>();
            List<UniTask<DNSAnswer[]>> queryTasks = new List<UniTask<DNSAnswer[]>>();
            HashSet<string> uniqueUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string url in urls)
            {
                if (string.IsNullOrEmpty(url) || !uniqueUrls.Add(url))
                {
                    continue;
                }

                validUrls.Add(url);
                queryTasks.Add(QueryDNSResultAsync(url));
            }

            if (queryTasks.Count == 0)
            {
                return;
            }

            DNSAnswer[][] allResults = await UniTask.WhenAll(queryTasks);

            for (int i = 0; i < validUrls.Count; i++)
            {
                await CacheDnsAnswersAsync(validUrls[i], allResults[i]);
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

            string hostName = GetHostName(url);
            if (string.IsNullOrEmpty(hostName))
            {
                m_DNSAnswers = null;
                return;
            }

            try
            {
                m_DNSAnswers = await QueryHostAnswersAsync(hostName);
                await CacheDnsAnswersAsync(url, m_DNSAnswers);
            }
            catch (Exception e)
            {
                if (m_ConfigManager?.DevelopMode == DevelopMode.Debug)
                {
                    Log.Error(LogTag.DoH, "DNSQuery 异常，host：{0}，异常信息：{1}。", hostName, e);
                }
            }
        }

        /// <summary>
        /// 从 URL 中提取主机名（域名部分）。
        /// </summary>
        /// <param name="url">完整 URL 字符串。</param>
        /// <returns>主机名字符串，格式非法时返回空字符串。</returns>
        public override string GetHostName(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return NormalizeHostName(uri.Host);
            }

            return string.Empty;
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
        /// 清空所有已收集的 IP 地址与 DNS 缓存。
        /// </summary>
        public override void Clear()
        {
            foreach (var kvp in m_DoHClients)
            {
                kvp.Value?.Dispose();
            }

            m_DoHClients.Clear();
            m_DNSAnswers = null;
            m_AllCollectedIPAddresses.Clear();
            m_AllDomainIPAddresses.Clear();
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
