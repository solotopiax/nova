/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DNSCacheEntry.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   DNS缓存条目
 ***************************************************************/

using System;
using System.Linq;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DoHClient 内部使用的 DNS 查询结果缓存条目，包含过期时间与应答集合。
    /// </summary>
    internal class DNSCacheEntry
    {
        /// <summary>
        /// 缓存过期时间（基于各应答 TTL 的最小值计算）。
        /// </summary>
        public readonly DateTime ExpireTime;

        /// <summary>
        /// 缓存的 DNS 应答集合。
        /// </summary>
        public readonly DNSAnswer[] Answers;

        /// <summary>
        /// 构造 DNS 缓存条目。
        /// </summary>
        /// <param name="answers">DNS 应答数组，用于计算过期时间。</param>
        public DNSCacheEntry(DNSAnswer[] answers)
        {
            Answers = answers;
            if (answers != null && answers.Any())
            {
                ExpireTime = DateTime.Now + new TimeSpan(0, 0, answers.Min(a => a.TTL));
            }
            else
            {
                ExpireTime = DateTime.Now;
            }
        }
    }
}
