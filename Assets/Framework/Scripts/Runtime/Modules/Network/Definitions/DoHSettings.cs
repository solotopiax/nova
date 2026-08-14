/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DoHSettings.cs
 * author:    taoye
 * created:   2026/3/11
 * descrip:   DoH 管理器配置
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DoH（DNS-over-HTTPS）管理器配置。
    /// </summary>
    [Serializable]
    public class DoHSettings
    {
        /// <summary>
        /// 是否启用 DoH DNS 解析。
        /// </summary>
        public bool UseDoH;

        /// <summary>
        /// 单个原始域名的完整 DoH 解析链超时时间（秒），默认 5 秒；小于等于 0 表示无限等待。
        /// A 查询与后续全部 CNAME 层共用同一截止时间；是否启用 DoH 仅由 UseDoH 控制。
        /// </summary>
        public int DnsTimeoutSeconds = 5;

        /// <summary>
        /// 每个域名最多保留的 DoH IPv4 数量，默认 3；小于等于 0 表示保留全部。
        /// </summary>
        public int MaxIPAddressesPerHost = 3;
    }
}
