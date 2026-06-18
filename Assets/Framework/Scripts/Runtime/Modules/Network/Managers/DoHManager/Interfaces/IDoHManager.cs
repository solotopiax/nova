/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IDoHManager.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   DoH管理器接口
 ***************************************************************/

using System.Collections.Generic;
using System.Net;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DoH 管理器接口，负责 DNS-over-HTTPS 查询与 IP 地址收集。
    /// </summary>
    public interface IDoHManager
    {
        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        void Initialize(DoHManagerConfig config);

        /// <summary>
        /// 遍历所有 NetCmd 中 HTTP 类型的 URL，异步收集各域名的 IP 地址。
        /// </summary>
        /// <param name="netCmdDatas">NetCmd 数据表。</param>
        /// <returns>异步任务。</returns>
        UniTask CollectAllIPAddresses(IEnumerable<string> urls);

        /// <summary>
        /// 对指定 URL 进行 DoH DNS 查询，结果写入内部缓存。
        /// </summary>
        /// <param name="url">目标 URL（支持 http / https）。</param>
        /// <returns>异步任务。</returns>
        UniTask DNSQuery(string url);

        /// <summary>
        /// 从 URL 中提取主机名（域名部分）。
        /// </summary>
        /// <param name="url">完整 URL 字符串。</param>
        /// <returns>主机名字符串，格式非法时返回空字符串。</returns>
        string GetHostName(string url);

        /// <summary>
        /// 通过主机名获取已收集的 IP 地址数组。
        /// </summary>
        /// <param name="hostName">目标主机名。</param>
        /// <returns>IP 地址数组，未收集时返回 null。</returns>
        IPAddress[] GetIPAddresses(string hostName);

        /// <summary>
        /// 清空所有已收集的 IP 地址与 DNS 缓存。
        /// </summary>
        void Clear();

        /// <summary>
        /// 所有已收集的 IP 地址，<原始 URL, 替换 IP 后的 URL 列表>。
        /// </summary>
        IReadOnlyDictionary<string, List<string>> AllCollectedIPAddresses { get; }

        /// <summary>
        /// 所有域名对应的 IP 地址，<主机名, IPAddress 列表>。
        /// </summary>
        IReadOnlyDictionary<string, List<IPAddress>> AllDomainIPAddresses { get; }

        /// <summary>
        /// 最近一次 DNSQuery 返回的 DNS 应答集合。
        /// </summary>
        DNSAnswer[] DNSAnswers { get; }
    }
}
