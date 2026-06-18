/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IPipifyProgressReporter.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 执行进度汇报接口
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 执行进度汇报接口。
    /// Runner 通过它向宿主汇报状态；Window 实现为模态进度条，CLI 实现为纯日志。
    /// </summary>
    public interface IPipifyProgressReporter
    {
        /// <summary>
        /// Batch 开始。
        /// </summary>
        /// <param name="batchName">Batch 名称。</param>
        /// <param name="totalSteps">步骤总数。</param>
        void BeginBatch(string batchName, int totalSteps);

        /// <summary>
        /// Step 进度汇报。
        /// </summary>
        /// <param name="index">Step 索引（从 0 起）。</param>
        /// <param name="stepDisplayName">Step 展示名。</param>
        /// <param name="innerProgress">Step 内进度（0~1）。</param>
        /// <returns>true 表示请求取消（Window 点取消）；CLI 恒返回 false。</returns>
        bool ReportStep(int index, string stepDisplayName, float innerProgress);

        /// <summary>
        /// Step 结束。
        /// </summary>
        /// <param name="index">Step 索引。</param>
        /// <param name="success">是否成功。</param>
        /// <param name="elapsed">耗时。</param>
        /// <param name="error">异常（失败时），否则 null。</param>
        void EndStep(int index, bool success, TimeSpan elapsed, Exception error);

        /// <summary>
        /// Batch 结束。
        /// </summary>
        /// <param name="success">整个 Batch 是否全部成功。</param>
        /// <param name="totalElapsed">总耗时。</param>
        void EndBatch(bool success, TimeSpan totalElapsed);
    }
}
