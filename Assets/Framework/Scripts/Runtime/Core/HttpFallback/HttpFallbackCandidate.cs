/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpFallbackCandidate.cs
 * author:    taoye
 * created:   2026/9/2
 * descrip:   HTTP 主备候选与物理发送坐标
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 候选在原始主备配置中的稳定角色。
    /// </summary>
    internal enum HttpFallbackRouteRole
    {
        Primary,
        Fallback,
        Other,
    }

    /// <summary>
    /// 表示一轮中的单个完整 URL 候选。
    /// </summary>
    internal readonly struct HttpFallbackCandidate
    {
        /// <summary>
        /// 创建单个 HTTP 候选。
        /// </summary>
        /// <param name="url">本次物理发送使用的完整 URL。</param>
        /// <param name="endpointId">规范化后的 scheme、host 与 port。</param>
        /// <param name="routeRole">候选在原配置中的主备角色。</param>
        /// <param name="originalIndex">候选在去重前配置中的索引。</param>
        public HttpFallbackCandidate(
            string url,
            string endpointId,
            HttpFallbackRouteRole routeRole,
            int originalIndex)
        {
            Url = url;
            EndpointId = endpointId;
            RouteRole = routeRole;
            OriginalIndex = originalIndex;
        }

        public string Url { get; }
        public string EndpointId { get; }
        public HttpFallbackRouteRole RouteRole { get; }
        public int OriginalIndex { get; }
    }

    /// <summary>
    /// 表示一条逻辑请求中的一次物理发送及其完整计数坐标。
    /// </summary>
    internal readonly struct HttpFallbackStep
    {
        /// <summary>
        /// 创建一次物理发送坐标。
        /// </summary>
        public HttpFallbackStep(
            HttpFallbackCandidate candidate,
            int retryCycleIndex,
            int roundIndex,
            int candidateIndex,
            int candidateCount,
            long physicalSendIndex)
        {
            Candidate = candidate;
            RetryCycleIndex = retryCycleIndex;
            RoundIndex = roundIndex;
            CandidateIndex = candidateIndex;
            CandidateCount = candidateCount;
            PhysicalSendIndex = physicalSendIndex;
        }

        public HttpFallbackCandidate Candidate { get; }
        public int RetryCycleIndex { get; }
        public int AdditionalRetriesUsed => RetryCycleIndex;
        public int RoundIndex { get; }
        public int CandidateIndex { get; }
        public int CandidateCount { get; }
        public long PhysicalSendIndex { get; }
    }
}
