/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DoHManagerConfig.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   DoH管理器配置
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DoH 管理器配置。
    /// </summary>
    public class DoHManagerConfig
    {
        /// <summary>
        /// 是否启用 DoH（DNS-over-HTTPS）解析。
        /// </summary>
        public bool UseDoH;

        /// <summary>
        /// 单个域名的一次 DoH 查询超时时间（秒），默认 3 秒；0 表示跳过 DoH 查询。
        /// 查询期间的所有候选地址共用该超时时间。
        /// </summary>
        public int DnsTimeoutSeconds = 3;

    }
}
