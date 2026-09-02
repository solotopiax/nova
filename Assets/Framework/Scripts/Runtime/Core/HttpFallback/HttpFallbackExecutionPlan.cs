/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpFallbackExecutionPlan.cs
 * author:    taoye
 * created:   2026/9/2
 * descrip:   HTTP 主备候选不可变执行计划
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 保存去重并按偏好排序的单轮候选，以及 C×R×(K+1) 执行规模。
    /// </summary>
    internal sealed class HttpFallbackExecutionPlan
    {
        private readonly HttpFallbackCandidate[] m_Candidates;

        /// <summary>
        /// 创建不可变执行计划；仅由共享规划器调用。
        /// </summary>
        internal HttpFallbackExecutionPlan(HttpFallbackCandidate[] candidates, HttpFallbackPolicy policy)
        {
            m_Candidates = candidates ?? System.Array.Empty<HttpFallbackCandidate>();
            Policy = policy;
            PlannedPhysicalSendCount = checked((long)m_Candidates.Length * policy.RoundCount *
                                               (policy.AdditionalRetryCount + 1L));
        }

        public IReadOnlyList<HttpFallbackCandidate> Candidates => m_Candidates;
        public HttpFallbackPolicy Policy { get; }
        public int CandidateCount => m_Candidates.Length;
        public int RoundCount => Policy.RoundCount;
        public int AdditionalRetryCount => Policy.AdditionalRetryCount;
        public int RetryCycleCount => Policy.AdditionalRetryCount + 1;
        public long PlannedPhysicalSendCount { get; }

        /// <summary>
        /// 为本次逻辑请求创建独立游标；计划本身可被安全并发读取。
        /// </summary>
        public HttpFallbackExecutionCursor CreateCursor()
        {
            return new HttpFallbackExecutionCursor(this);
        }
    }
}
