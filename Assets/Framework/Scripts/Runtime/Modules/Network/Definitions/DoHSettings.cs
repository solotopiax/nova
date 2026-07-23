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
        /// 单个域名的一次 DoH 查询超时时间（秒），默认 3 秒；0 表示跳过 DoH 查询。
        /// 查询期间的所有候选地址共用该超时时间。
        /// </summary>
        public int DnsTimeoutSeconds = 3;
    }
}
