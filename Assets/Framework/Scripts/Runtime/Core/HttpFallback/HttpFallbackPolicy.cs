/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpFallbackPolicy.cs
 * author:    taoye
 * created:   2026/9/2
 * descrip:   HTTP 主备候选执行策略
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 定义一条逻辑请求内的完整候选轮数、额外重试次数与最近成功域名偏好。
    /// </summary>
    internal readonly struct HttpFallbackPolicy
    {
        /// <summary>
        /// 创建 HTTP 主备候选执行策略。
        /// </summary>
        /// <param name="roundCount">每个重试周期执行的完整候选轮数，必须大于等于 1。</param>
        /// <param name="additionalRetryCount">首个周期耗尽后的额外重试次数，必须大于等于 0。</param>
        /// <param name="preferLastSuccessfulHost">是否将最近成功域名放在单轮首位。</param>
        public HttpFallbackPolicy(int roundCount, int additionalRetryCount, bool preferLastSuccessfulHost)
        {
            if (roundCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(roundCount), roundCount, "Round count must be at least 1.");
            }

            if (additionalRetryCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(additionalRetryCount), additionalRetryCount,
                    "Additional retry count cannot be negative.");
            }

            RoundCount = roundCount;
            AdditionalRetryCount = additionalRetryCount;
            PreferLastSuccessfulHost = preferLastSuccessfulHost;
        }

        /// <summary>
        /// 获取每个重试周期执行的完整候选轮数。
        /// </summary>
        public int RoundCount { get; }

        /// <summary>
        /// 获取首个周期耗尽后的额外重试次数。
        /// </summary>
        public int AdditionalRetryCount { get; }

        /// <summary>
        /// 获取是否优先使用最近成功域名。
        /// </summary>
        public bool PreferLastSuccessfulHost { get; }
    }
}
