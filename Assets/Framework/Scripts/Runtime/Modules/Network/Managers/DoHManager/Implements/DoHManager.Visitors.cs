/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DoHManager.Visitors.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   DoH管理器 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using System.Net;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DoH 管理器。
    /// </summary>
    internal sealed partial class DoHManager : DoHManagerBase
    {
        /// <summary>
        /// DoH 查询器实例缓存，<主机名, DoHClient>。
        /// </summary>
        private Dictionary<string, DoHClient> m_DoHClients;

        /// <summary>
        /// 是否启用 DoH 解析。
        /// </summary>
        private bool m_UseDoH;

        /// <summary>
        /// 单个域名的一次 DoH 查询超时时间（毫秒），0 表示跳过 DoH 查询。
        /// 查询期间的所有候选地址共用该超时时间。
        /// </summary>
        private int m_DNSTimeout;

        /// <summary>
        /// 所有已收集的 IP 地址，<原始 URL, 替换 IP 后的 URL 列表>。
        /// </summary>
        private Dictionary<string, List<string>> m_AllCollectedIPAddresses;
        public override IReadOnlyDictionary<string, List<string>> AllCollectedIPAddresses => m_AllCollectedIPAddresses;

        /// <summary>
        /// 原始业务域名对应的最终 IPAddress 列表；CNAME 中间域名不会单独写入。
        /// </summary>
        private Dictionary<string, List<IPAddress>> m_AllDomainIPAddresses;
        public override IReadOnlyDictionary<string, List<IPAddress>> AllDomainIPAddresses => m_AllDomainIPAddresses;

        /// <summary>
        /// 按原始业务域名保存的 DoH 解析诊断树；CNAME 域名只存在于根节点的 Children 中。
        /// </summary>
        private Dictionary<string, DoHResolutionNode> m_ResolutionRoots;
        public override IReadOnlyDictionary<string, DoHResolutionNode> ResolutionRoots => m_ResolutionRoots;

        /// <summary>
        /// 最近一次 DNSQuery 返回的 DNS 应答集合。
        /// </summary>
        private DNSAnswer[] m_DNSAnswers;
        public override DNSAnswer[] DNSAnswers => m_DNSAnswers;

        /// <summary>
        /// 配置管理器引用，用于判断开发模式以控制异常日志输出。
        /// 由 Initialize 阶段通过 FrameworkManagersGroup 注入，禁止业务聚合器穿透访问。
        /// </summary>
        private IConfigManager m_ConfigManager;
    }
}
