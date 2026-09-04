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
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

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
                Interrupted,
            }

            /// <summary>
            /// Pipify 外部任务状态快照。
            /// </summary>
            public sealed class BatchJobSnapshot
            {
                [JsonProperty]
                public string JobId { get; internal set; }
                [JsonProperty]
                public string BatchName { get; internal set; }
                [JsonProperty]
                public BatchJobState State { get; internal set; }
                [JsonIgnore]
                public string StateName => State.ToString();
                [JsonProperty]
                public string Error { get; internal set; }
                [JsonProperty]
                public string QueuedAtUtc { get; internal set; }
                [JsonProperty]
                public string StartedAtUtc { get; internal set; }
                [JsonProperty]
                public string CompletedAtUtc { get; internal set; }
            }

            private static readonly Dictionary<string, BatchJobSnapshot> s_BatchJobs =
                new Dictionary<string, BatchJobSnapshot>(StringComparer.Ordinal);

            private static string s_ActiveBatchJobId;

            private static readonly string s_JobDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory(),
                "Library/Nova/Pipify/Jobs");

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
                if (s_BatchJobs.TryGetValue(jobId, out BatchJobSnapshot snapshot)) return snapshot;
                return TryLoadPersistedJob(jobId, out snapshot) ? snapshot : null;
            }

            internal static string StartBatchJob(
                Batch batch,
                IReadOnlyDictionary<string, string> overrides,
                Action<Action> scheduler,
                Func<Batch, IReadOnlyDictionary<string, string>, UniTask> runner)
            {
                return StartBatchJob(batch, overrides, scheduler, runner, null);
            }

            /// <summary>
            /// 启动可在真实运行期持有外部资源租约的 Pipify 任务，供受控 Action 适配器使用。
            /// </summary>
            internal static string StartBatchJob(
                Batch batch,
                IReadOnlyDictionary<string, string> overrides,
                Action<Action> scheduler,
                Func<Batch, IReadOnlyDictionary<string, string>, UniTask> runner,
                Func<IDisposable> leaseFactory)
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
                    QueuedAtUtc = DateTime.UtcNow.ToString("O"),
                };
                s_BatchJobs.Add(jobId, snapshot);
                s_ActiveBatchJobId = jobId;
                PersistJob(snapshot);

                try
                {
                    scheduler(() => ExecuteBatchJobAsync(snapshot, batch, overrides, runner, leaseFactory).Forget());
                }
                catch (Exception ex)
                {
                    snapshot.State = BatchJobState.Failed;
                    snapshot.Error = ex.ToString();
                    snapshot.CompletedAtUtc = DateTime.UtcNow.ToString("O");
                    PersistJob(snapshot, false);
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
                Func<Batch, IReadOnlyDictionary<string, string>, UniTask> runner,
                Func<IDisposable> leaseFactory)
            {
                snapshot.State = BatchJobState.Running;
                snapshot.StartedAtUtc = DateTime.UtcNow.ToString("O");
                PersistJob(snapshot);
                IDisposable lease = null;
                try
                {
                    lease = leaseFactory?.Invoke();
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
                    lease?.Dispose();
                    snapshot.CompletedAtUtc = DateTime.UtcNow.ToString("O");
                    PersistJob(snapshot, snapshot.State != BatchJobState.Failed);
                    if (string.Equals(s_ActiveBatchJobId, snapshot.JobId, StringComparison.Ordinal))
                    {
                        s_ActiveBatchJobId = null;
                    }
                }
            }

            /// <summary>
            /// 将任务状态原子写入 Library，供 MCP 断线或 domain reload 后只读核验。
            /// 失败详情默认不持久化，避免把 Step 参数中的凭据落入任务文件。
            /// </summary>
            private static void PersistJob(BatchJobSnapshot snapshot, bool includeError = true)
            {
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.JobId)) return;
                Directory.CreateDirectory(s_JobDirectory);
                string path = GetJobPath(snapshot.JobId);
                string temporaryPath = path + ".tmp";
                var persisted = new BatchJobSnapshot
                {
                    JobId = snapshot.JobId,
                    BatchName = snapshot.BatchName,
                    State = snapshot.State,
                    Error = includeError ? snapshot.Error : "Batch 执行失败；详情请查看 Unity Console。",
                    QueuedAtUtc = snapshot.QueuedAtUtc,
                    StartedAtUtc = snapshot.StartedAtUtc,
                    CompletedAtUtc = snapshot.CompletedAtUtc,
                };
                File.WriteAllText(temporaryPath, Util.Json.Serialize(persisted));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporaryPath, path);
            }

            /// <summary>
            /// 从 Library 加载任务快照；reload 前未终止的任务会标记为 Interrupted，且不会重新执行。
            /// </summary>
            private static bool TryLoadPersistedJob(string jobId, out BatchJobSnapshot snapshot)
            {
                snapshot = null;
                if (!Guid.TryParseExact(jobId, "N", out _)) return false;
                string path = GetJobPath(jobId);
                if (!File.Exists(path)) return false;
                try
                {
                    snapshot = Util.Json.Deserialize<BatchJobSnapshot>(File.ReadAllText(path));
                    if (snapshot == null || !string.Equals(snapshot.JobId, jobId, StringComparison.Ordinal))
                    {
                        snapshot = null;
                        return false;
                    }
                    if (snapshot.State == BatchJobState.Waiting || snapshot.State == BatchJobState.Running)
                    {
                        snapshot.State = BatchJobState.Interrupted;
                        snapshot.Error = "Unity domain reload 或进程中断导致任务状态丢失；不会自动重放。";
                        snapshot.CompletedAtUtc = DateTime.UtcNow.ToString("O");
                        PersistJob(snapshot);
                    }
                    s_BatchJobs[jobId] = snapshot;
                    return true;
                }
                catch (Exception exception)
                {
                    Log.Warning(LogTag.Editor, "{0} 无法读取 Pipify Job {1}：{2}", c_LogPrefix, jobId, exception.Message);
                    snapshot = null;
                    return false;
                }
            }

            /// <summary>
            /// 返回经过格式校验的任务状态文件路径，避免外部任务编号参与任意路径拼接。
            /// </summary>
            private static string GetJobPath(string jobId)
            {
                return Path.Combine(s_JobDirectory, jobId + ".json");
            }
        }
    }
}
