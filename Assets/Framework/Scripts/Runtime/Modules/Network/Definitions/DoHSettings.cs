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
        /// DNS 查询超时时间（秒），0 表示不限制。
        /// </summary>
        public int DnsTimeoutSeconds;
    }
}
