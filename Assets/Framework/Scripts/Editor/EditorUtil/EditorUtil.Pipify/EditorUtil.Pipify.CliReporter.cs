/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Pipify.CliReporter.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   CLI 宿主进度 Reporter：纯日志输出实现
 ***************************************************************/

using System;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Pipify
        {
            /// <summary>
            /// CLI 宿主进度 Reporter：纯日志输出，ReportStep 恒返回 false。
            /// 供 Jenkins 等 batchmode 流水线使用。
            /// </summary>
            internal sealed class CliReporter : IPipifyProgressReporter
            {
                /// <summary>
                /// 当前 Batch 名。
                /// </summary>
                private string m_BatchName;

                /// <summary>
                /// 当前 Batch 步骤总数。
                /// </summary>
                private int m_TotalSteps;

                /// <summary>
                /// 最近一次 ReportStep 传入的步骤展示名，供 EndStep 日志输出。
                /// </summary>
                private string m_LastStepName;

                /// <summary>
                /// 通知 Batch 开始，记录名称与总步骤数，并打印 Debug 日志。
                /// </summary>
                /// <param name="batchName">Batch 名称。</param>
                /// <param name="totalSteps">步骤总数。</param>
                public void BeginBatch(string batchName, int totalSteps)
                {
                    m_BatchName = batchName;
                    m_TotalSteps = totalSteps;
                    Log.Debug(LogTag.Editor, "{0} [CLI] Batch 开始：{1}（{2} 步）", c_LogPrefix, batchName, totalSteps);
                }

                /// <summary>
                /// 打印当前 Step 进度日志；CLI 模式不支持交互取消，恒返回 false。
                /// </summary>
                /// <param name="index">Step 索引（从 0 起）。</param>
                /// <param name="stepDisplayName">Step 展示名。</param>
                /// <param name="innerProgress">Step 内进度（0~1）。</param>
                /// <returns>恒返回 false（CLI 不支持交互取消）。</returns>
                public bool ReportStep(int index, string stepDisplayName, float innerProgress)
                {
                    m_LastStepName = stepDisplayName;
                    Log.Debug(LogTag.Editor, "{0} [CLI] 第 {1}/{2} 步：{3}（进度 {4:P0}）", c_LogPrefix, index + 1, m_TotalSteps, stepDisplayName, innerProgress);
                    return false;
                }

                /// <summary>
                /// Step 结束回调：成功打 Debug 日志，失败打 Error 日志。
                /// </summary>
                /// <param name="index">Step 索引。</param>
                /// <param name="success">是否成功。</param>
                /// <param name="elapsed">耗时。</param>
                /// <param name="error">异常（失败时非 null）。</param>
                public void EndStep(int index, bool success, TimeSpan elapsed, Exception error)
                {
                    if (success)
                    {
                        Log.Debug(LogTag.Editor, "{0} [CLI] 第 {1} 步完成（耗时 {2:0.00}s）", c_LogPrefix, index + 1, elapsed.TotalSeconds);
                    }
                    else
                    {
                        Log.Error(LogTag.Editor, "{0} [CLI] 第 {1} 步「{2}」失败：{3}", c_LogPrefix, index + 1, m_LastStepName, error);
                    }
                }

                /// <summary>
                /// Batch 结束回调：打 Debug 或 Error 日志（CLI 无进度条可清除）。
                /// </summary>
                /// <param name="success">整个 Batch 是否全部成功。</param>
                /// <param name="totalElapsed">总耗时。</param>
                public void EndBatch(bool success, TimeSpan totalElapsed)
                {
                    if (success)
                    {
                        Log.Debug(LogTag.Editor, "{0} [CLI] Batch 完成：{1}（总耗时 {2:0.00}s）", c_LogPrefix, m_BatchName, totalElapsed.TotalSeconds);
                    }
                    else
                    {
                        Log.Error(LogTag.Editor, "{0} [CLI] Batch 失败：{1}（总耗时 {2:0.00}s）", c_LogPrefix, m_BatchName, totalElapsed.TotalSeconds);
                    }
                }
            }
        }
    }
}
