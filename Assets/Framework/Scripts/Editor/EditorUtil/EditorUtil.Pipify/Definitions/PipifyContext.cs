/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyContext.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Runner 下行给每个 Step 的运行时上下文
 ***************************************************************/

using System.Threading;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Runner 在调用每个 Step 前构造并下发的运行时上下文。
    /// Step 只读取、不修改；承载环境信息（Batch 名 / 步索引 / 进度汇报 / 取消令牌）。
    /// 属性 setter 全部 internal，仅 Runner 所在程序集可写。
    /// </summary>
    public sealed class PipifyContext
    {
        /// <summary>
        /// 当前执行的 Batch 名称。
        /// </summary>
        public string BatchName { get; internal set; }

        /// <summary>
        /// 当前 Step 在 Batch 中的索引（从 0 起）。
        /// </summary>
        public int CurrentStepIndex { get; internal set; }

        /// <summary>
        /// 当前 Batch 的步骤总数。
        /// </summary>
        public int TotalSteps { get; internal set; }

        /// <summary>
        /// 进度汇报接口（Window/CLI 各一实现）。
        /// </summary>
        public IPipifyProgressReporter Reporter { get; internal set; }

        /// <summary>
        /// 取消令牌，供长耗时 Step 内部响应取消。
        /// </summary>
        public CancellationToken CancellationToken { get; internal set; }
    }
}
