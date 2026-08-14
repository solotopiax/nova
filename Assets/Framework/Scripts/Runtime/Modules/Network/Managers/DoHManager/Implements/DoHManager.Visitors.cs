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
        public override bool IsEnabled => m_UseDoH;

        /// <summary>
        /// 单个原始域名完整 DoH 解析链的超时时间（毫秒）；配置秒数小于等于 0 时保存 Timeout.Infinite。
        /// A 查询与后续全部 CNAME 层共用由该值创建的唯一截止时间。
        /// </summary>
        private int m_DNSTimeout;

        /// <summary>
        /// 每个域名最多写入缓存的 IPv4 数量；小于等于 0 表示不限制。
        /// </summary>
        private int m_MaxIPAddressesPerHost = 3;

        /// <summary>
        /// 当前 DoH 查询代次；Clear / Shutdown 会递增该值，使清理前发起的异步查询失效。
        /// </summary>
        private int m_QueryGeneration;

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
