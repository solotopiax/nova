/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpFallbackExecutionCursor.cs
 * author:    taoye
 * created:   2026/9/2
 * descrip:   HTTP 主备候选执行游标
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 一条逻辑请求的候选执行状态。
    /// </summary>
    internal enum HttpFallbackExecutionState
    {
        NotStarted,
        CandidateInFlight,
        CandidateRejected,
        Completed,
        Exhausted,
        Cancelled,
    }

    /// <summary>
    /// 以 RetryCycle → Round → Candidate 顺序惰性推进物理发送，不负责执行网络请求。
    /// </summary>
    internal sealed class HttpFallbackExecutionCursor
    {
        private readonly HttpFallbackExecutionPlan m_Plan;
        private long m_NextPhysicalSendIndex;

        /// <summary>
        /// 创建指定计划的独立执行游标。
        /// </summary>
        internal HttpFallbackExecutionCursor(HttpFallbackExecutionPlan plan)
        {
            m_Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            State = HttpFallbackExecutionState.NotStarted;
        }

        public HttpFallbackExecutionState State { get; private set; }
        public HttpFallbackStep Current { get; private set; }

        /// <summary>
        /// 开始下一次物理发送；上一候选必须已明确拒绝，且不会隐式改变请求结果。
        /// </summary>
        /// <param name="step">下一次物理发送坐标。</param>
        /// <returns>存在下一候选时返回 true；计划耗尽或已终止时返回 false。</returns>
        public bool TryBeginNext(out HttpFallbackStep step)
        {
            if (State == HttpFallbackExecutionState.CandidateInFlight)
            {
                throw new InvalidOperationException("The current fallback candidate has not completed.");
            }

            if (State == HttpFallbackExecutionState.Completed ||
                State == HttpFallbackExecutionState.Cancelled ||
                State == HttpFallbackExecutionState.Exhausted)
            {
                step = default;
                return false;
            }

            if (m_NextPhysicalSendIndex >= m_Plan.PlannedPhysicalSendCount || m_Plan.CandidateCount == 0)
            {
                State = HttpFallbackExecutionState.Exhausted;
                step = default;
                return false;
            }

            long flatRoundIndex = m_NextPhysicalSendIndex / m_Plan.CandidateCount;
            int retryCycleIndex = (int)(flatRoundIndex / m_Plan.RoundCount);
            int roundIndex = (int)(flatRoundIndex % m_Plan.RoundCount);
            int candidateIndex = (int)(m_NextPhysicalSendIndex % m_Plan.CandidateCount);
            Current = new HttpFallbackStep(
                m_Plan.Candidates[candidateIndex],
                retryCycleIndex,
                roundIndex,
                candidateIndex,
                m_Plan.CandidateCount,
                m_NextPhysicalSendIndex);
            State = HttpFallbackExecutionState.CandidateInFlight;
            step = Current;
            return true;
        }

        /// <summary>
        /// 将当前物理发送标记为可继续的失败，并推进到下一个候选坐标。
        /// </summary>
        public void RejectCurrent()
        {
            EnsureCandidateInFlight();
            m_NextPhysicalSendIndex++;
            State = m_NextPhysicalSendIndex >= m_Plan.PlannedPhysicalSendCount
                ? HttpFallbackExecutionState.Exhausted
                : HttpFallbackExecutionState.CandidateRejected;
        }

        /// <summary>
        /// 将当前物理发送标记为逻辑链终态；终态既可以是成功，也可以是正式 HTTP 错误。
        /// </summary>
        public void CompleteCurrent()
        {
            EnsureCandidateInFlight();
            State = HttpFallbackExecutionState.Completed;
        }

        /// <summary>
        /// 取消整条逻辑请求；取消后不会再产生候选。
        /// </summary>
        public void Cancel()
        {
            if (State == HttpFallbackExecutionState.Completed || State == HttpFallbackExecutionState.Exhausted)
            {
                return;
            }

            State = HttpFallbackExecutionState.Cancelled;
        }

        /// <summary>
        /// 校验当前确实存在正在执行的物理发送。
        /// </summary>
        private void EnsureCandidateInFlight()
        {
            if (State != HttpFallbackExecutionState.CandidateInFlight)
            {
                throw new InvalidOperationException("There is no in-flight fallback candidate.");
            }
        }
    }
}
