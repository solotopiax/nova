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
        /// DNS 查询超时时间（秒），0 表示不限制超时。
        /// </summary>
        public int DnsTimeoutSeconds;

    }
}
