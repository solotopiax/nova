/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Pipify.AsyncJob.cs
 * author:    taoye
 * created:   2026/8/14
 * descrip:   Pipify 外部异步任务启动与状态查询
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Pipify
        {
            /// <summary>
            /// Pipify 外部任务状态。
            /// </summary>
            public enum BatchJobState
            {
                Waiting,
                Running,
                Succeeded,
                Failed,
            }

            /// <summary>
            /// Pipify 外部任务状态快照。
            /// </summary>
            public sealed class BatchJobSnapshot
            {
                public string JobId { get; internal set; }
                public string BatchName { get; internal set; }
                public BatchJobState State { get; internal set; }
                public string StateName => State.ToString();
                public string Error { get; internal set; }
            }

            private static readonly Dictionary<string, BatchJobSnapshot> s_BatchJobs =
                new Dictionary<string, BatchJobSnapshot>(StringComparer.Ordinal);

            private static string s_ActiveBatchJobId;

            /// <summary>
            /// 启动供 Editor 外部调用的 Pipify 任务。
            /// 本方法只登记任务并立即返回任务编号，Batch 会在下一次 Editor update 中开始执行。
            /// </summary>
            /// <param name="batch">待执行 Batch。</param>
            /// <param name="overrides">参数覆盖字典；null 表示不覆盖。</param>
            /// <returns>用于查询状态的任务编号。</returns>
            public static string StartBatchJob(
                Batch batch,
                IReadOnlyDictionary<string, string> overrides)
            {
                return StartBatchJob(batch, overrides, ScheduleOnNextEditorUpdate, RunBatchForCliAsync);
            }

            /// <summary>
            /// 查询 Pipify 外部任务状态；任务编号不存在时返回 null。
            /// </summary>
            public static BatchJobSnapshot GetBatchJob(string jobId)
            {
                if (string.IsNullOrEmpty(jobId)) return null;
                return s_BatchJobs.TryGetValue(jobId, out BatchJobSnapshot snapshot) ? snapshot : null;
            }

            internal static string StartBatchJob(
                Batch batch,
                IReadOnlyDictionary<string, string> overrides,
                Action<Action> scheduler,
                Func<Batch, IReadOnlyDictionary<string, string>, UniTask> runner)
            {
                if (batch == null) throw new ArgumentNullException(nameof(batch));
                if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
                if (runner == null) throw new ArgumentNullException(nameof(runner));

                if (!string.IsNullOrEmpty(s_ActiveBatchJobId)
                    && s_BatchJobs.TryGetValue(s_ActiveBatchJobId, out BatchJobSnapshot active)
                    && (active.State == BatchJobState.Waiting || active.State == BatchJobState.Running))
                {
                    throw new InvalidOperationException(string.Format(
                        "{0} 已有 Pipify 任务正在等待或执行：{1}",
                        c_LogPrefix,
                        s_ActiveBatchJobId));
                }

                string jobId = Guid.NewGuid().ToString("N");
                var snapshot = new BatchJobSnapshot
                {
                    JobId = jobId,
                    BatchName = batch.Name,
                    State = BatchJobState.Waiting,
                    Error = null,
                };
                s_BatchJobs.Add(jobId, snapshot);
                s_ActiveBatchJobId = jobId;

                try
                {
                    scheduler(() => ExecuteBatchJobAsync(snapshot, batch, overrides, runner).Forget());
                }
                catch (Exception ex)
                {
                    snapshot.State = BatchJobState.Failed;
                    snapshot.Error = ex.ToString();
                    s_ActiveBatchJobId = null;
                    throw;
                }

                return jobId;
            }

            private static void ScheduleOnNextEditorUpdate(Action callback)
            {
                EditorApplication.CallbackFunction scheduled = null;
                scheduled = () =>
                {
                    EditorApplication.update -= scheduled;
                    callback();
                };
                EditorApplication.update += scheduled;
            }

            private static async UniTask ExecuteBatchJobAsync(
                BatchJobSnapshot snapshot,
                Batch batch,
                IReadOnlyDictionary<string, string> overrides,
                Func<Batch, IReadOnlyDictionary<string, string>, UniTask> runner)
            {
                snapshot.State = BatchJobState.Running;
                try
                {
                    await runner(batch, overrides);
                    snapshot.State = BatchJobState.Succeeded;
                }
                catch (Exception ex)
                {
                    snapshot.State = BatchJobState.Failed;
                    snapshot.Error = ex.ToString();
                    Log.Error(
                        LogTag.Editor,
                        "{0}[Job {1}] Batch {2} 执行失败：{3}",
                        c_LogPrefix,
                        snapshot.JobId,
                        snapshot.BatchName,
                        ex);
                }
                finally
                {
                    if (string.Equals(s_ActiveBatchJobId, snapshot.JobId, StringComparison.Ordinal))
                    {
                        s_ActiveBatchJobId = null;
                    }
                }
            }
        }
    }
}
