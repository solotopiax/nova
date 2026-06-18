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
        /// DNS 查询超时时间（毫秒），0 表示不限制。
        /// </summary>
        private int m_DNSTimeout;

        /// <summary>
        /// 所有已收集的 IP 地址，<原始 URL, 替换 IP 后的 URL 列表>，由 CollectAllIPAddresses 填充。
        /// </summary>
        private Dictionary<string, List<string>> m_AllCollectedIPAddresses;
        public override IReadOnlyDictionary<string, List<string>> AllCollectedIPAddresses => m_AllCollectedIPAddresses;

        /// <summary>
        /// 所有域名对应的 IPAddress 列表，<主机名, IPAddress 列表>，由 CollectAllIPAddresses 填充。
        /// </summary>
        private Dictionary<string, List<IPAddress>> m_AllDomainIPAddresses;
        public override IReadOnlyDictionary<string, List<IPAddress>> AllDomainIPAddresses => m_AllDomainIPAddresses;

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
