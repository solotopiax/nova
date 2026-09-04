/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyRunBatchAction.cs
 * author:    taoye
 * created:   2026/9/4
 * descrip:   按已冻结 PipifySettings 与 Batch 启动流水线的 Project Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.pipify.run-batch",
        "运行 Pipify Batch",
        "pipify",
        AgentActionOperationType.Build,
        Description = "按 PipifySettings GUID 与 Batch 名称运行项目已配置的完整流水线，并通过异步任务状态核验结果。",
        Effects = AgentActionEffect.WorkspaceRead |
                  AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead |
                  AgentActionEffect.UnityWrite |
                  AgentActionEffect.ExternalRead |
                  AgentActionEffect.ExternalWrite |
                  AgentActionEffect.BuildArtifact |
                  AgentActionEffect.Destructive |
                  AgentActionEffect.Credential,
        RequiredEvidence = AgentActionEvidence.Static,
        Idempotency = AgentActionIdempotency.SubmitOnce,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        Locks = new[] { "unity-editor", "pipify-batch", "Assets", "Packages", "ProjectSettings", "Build" },
        VerifyLocks = new string[0])]
    internal sealed class PipifyRunBatchAction : AgentActionHandler<PipifyRunBatchAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string pipifySettingsGuid;
            [AgentActionRequired] public string batchName;
        }

        private sealed class State
        {
            public string SettingsGuid;
            public string SettingsPath;
            public string SettingsHash;
            public string ActiveBuildTarget;
            public Batch Batch;
            public BatchSnapshot Snapshot;
        }

        [Serializable]
        private sealed class BatchSnapshot
        {
            public string pipifySettingsGuid;
            public string pipifySettingsPath;
            public string pipifySettingsHash;
            public string batchName;
            public string batchDescription;
            public string activeBuildTarget;
            public StepSnapshot[] steps;
        }

        [Serializable]
        private sealed class StepSnapshot
        {
            public int index;
            public string stepId;
            public string paramsJson;
        }

        [Serializable]
        private sealed class Receipt
        {
            public string jobId;
            public BatchSnapshot batch;
        }

        /// <summary>
        /// 校验 PipifySettings GUID 与 Batch 名称的基础传输格式，领域存在性在 Plan 中核对。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (request == null || !Guid.TryParseExact(request.pipifySettingsGuid, "N", out _))
            {
                error = "pipifySettingsGuid 必须是 32 位 Unity Asset GUID。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(request.batchName) || request.batchName.Length > 256)
            {
                error = "batchName 必须是 1 至 256 个字符的非空名称。";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 只读冻结 PipifySettings 文件、Batch 步骤参数、活动构建目标与可见写入摘要。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryCaptureState(request.pipifySettingsGuid, request.batchName, out State state, out string error))
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = error });
            }

            var receipt = new Receipt { batch = state.Snapshot };
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将启动 Pipify Batch“{state.Batch.Name}”，共 {state.Batch.Items.Count} 个步骤。",
                State = state,
                DataJson = Util.Json.Serialize(state.Snapshot),
                WriteSet = BuildWriteSet(state),
                Evidence = new[]
                {
                    $"已冻结 PipifySettings={state.SettingsPath}（SHA-256={state.SettingsHash}）。",
                    $"已冻结 activeBuildTarget={state.ActiveBuildTarget} 与 {state.Batch.Items.Count} 个 StepId/ParamsJson。",
                    "具体领域写入与外部副作用由 Batch 中的已注册 Step 定义；Runner 任一步失败都会终止后续步骤。",
                },
                RecoveryPayloadJson = Util.Json.Serialize(receipt),
            });
        }

        /// <summary>
        /// 复核冻结快照后登记异步 Pipify 任务；不在 MCP 请求内阻塞等待完整构建。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Pipify Batch 冻结状态无效。"));
            }
            context.CancellationToken.ThrowIfCancellationRequested();

            if (!TryCaptureState(frozen.SettingsGuid, frozen.Batch.Name, out State current, out string error) ||
                !SnapshotsEqual(frozen, current))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Pipify Batch 或执行环境已变化，请重新 Plan：" + error));
            }

            string jobId = EditorUtil.Pipify.StartBatchJob(
                current.Batch,
                null,
                ScheduleOnNextEditorUpdate,
                EditorUtil.Pipify.RunBatchForCliAsync,
                AcquireBatchLease);
            var receipt = new Receipt { jobId = jobId, batch = current.Snapshot };
            AgentActionResult result = AgentActionResult.Create(
                null,
                "partial",
                $"Pipify Batch“{current.Batch.Name}”已登记为异步任务 {jobId}；请使用 recovery_token 轮询 Verify。" );
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.DataJson = result.ReceiptJson;
            result.Evidence.Add("Execute 仅登记一次异步任务；失败、断线或 domain reload 后不会自动重放 Batch。" );
            return Task.FromResult(result);
        }

        /// <summary>
        /// 只读查询异步任务状态；成功仅证明 Runner 完整结束，具体产物仍由各 Step 或后续检查核验。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            try
            {
                receipt = Util.Json.Deserialize<Receipt>(receiptJson);
            }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Pipify Batch Receipt 无法解析：" + exception.Message));
            }

            if (receipt?.batch == null)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Pipify Batch Receipt 不完整。"));
            }
            if (string.IsNullOrWhiteSpace(receipt.jobId))
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "Batch 尚未确认启动，且 Verify 不会恢复或重放 Execute。"));
            }

            EditorUtil.Pipify.BatchJobSnapshot job = EditorUtil.Pipify.GetBatchJob(receipt.jobId);
            if (job == null)
            {
                return Task.FromResult(AgentActionResult.Create(
                    null,
                    "partial",
                    "当前 AppDomain 中没有该 Pipify 任务状态；可能发生过 domain reload，Verify 不会重放 Batch。"));
            }
            if (job.State == EditorUtil.Pipify.BatchJobState.Waiting ||
                job.State == EditorUtil.Pipify.BatchJobState.Running)
            {
                AgentActionResult running = AgentActionResult.Create(null, "partial", $"Pipify Batch 当前状态：{job.StateName}。" );
                running.DataJson = Util.Json.Serialize(new { receipt.jobId, state = job.StateName, receipt.batch });
                return Task.FromResult(running);
            }
            if (job.State == EditorUtil.Pipify.BatchJobState.Failed)
            {
                AgentActionResult failed = AgentActionResult.Create(null, "partial", "Pipify Batch 执行失败；不会自动重放：" + job.Error);
                failed.DataJson = Util.Json.Serialize(new { receipt.jobId, state = job.StateName, receipt.batch });
                return Task.FromResult(failed);
            }
            if (job.State == EditorUtil.Pipify.BatchJobState.Interrupted)
            {
                AgentActionResult interrupted = AgentActionResult.Create(null, "partial", job.Error);
                interrupted.DataJson = Util.Json.Serialize(new { receipt.jobId, state = job.StateName, receipt.batch });
                return Task.FromResult(interrupted);
            }

            AgentActionResult success = AgentActionResult.Create(null, "success", $"Pipify Batch“{job.BatchName}”已完整执行。" );
            success.EvidenceKinds = AgentActionEvidence.Static;
            success.DataJson = Util.Json.Serialize(new { receipt.jobId, state = job.StateName, receipt.batch });
            success.Evidence.Add("异步任务状态为 Succeeded，Runner 已按顺序完成全部步骤且没有捕获异常。" );
            success.Warnings.Add("该通用证据不替代具体 Player、Bundle、设备、CDN 或浏览器运行验证。" );
            return Task.FromResult(success);
        }

        /// <summary>
        /// 将任务安排到下一次 Editor update，并在调用前先注销回调，确保后台 MCP 调用也能可靠启动一次。
        /// </summary>
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

        /// <summary>
        /// 从指定 GUID 精确解析当前活动 PipifySettings 与唯一同名 Batch，并生成不可变快照。
        /// </summary>
        private static bool TryCaptureState(string settingsGuid, string batchName, out State state, out string error)
        {
            state = null;
            error = null;
            string settingsPath = AssetDatabase.GUIDToAssetPath(settingsGuid);
            PipifySettingsSO settings = string.IsNullOrWhiteSpace(settingsPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<PipifySettingsSO>(settingsPath);
            if (settings == null)
            {
                error = "pipifySettingsGuid 未解析到 PipifySettingsSO。";
                return false;
            }
            if (!EditorUtil.Config.WorkspaceActive.TryGetPersistedPipifySettings(
                    out PipifySettingsSO activeSettings, out string activeGuid, out _, out string workspaceError))
            {
                error = workspaceError;
                return false;
            }
            if (!ReferenceEquals(settings, activeSettings) ||
                !string.Equals(settingsGuid, activeGuid, StringComparison.OrdinalIgnoreCase))
            {
                error = "请求的 PipifySettings 不是 Globals 当前活动工作区。";
                return false;
            }
            if (!EditorUtil.Config.WorkspaceActive.TryGetPersistedConfigMaster(out _, out _, out _, out string masterError))
            {
                error = masterError;
                return false;
            }

            Batch[] matches = settings.Batches
                .Where(item => item != null && string.Equals(item.Name, batchName, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                error = matches.Length == 0
                    ? $"PipifySettings 中不存在 Batch：{batchName}。"
                    : $"PipifySettings 中存在多个同名 Batch：{batchName}。";
                return false;
            }

            Batch batch = matches[0];
            var steps = new StepSnapshot[batch.Items.Count];
            for (int index = 0; index < batch.Items.Count; index++)
            {
                BatchItem item = batch.Items[index];
                PipifyStepInfo stepInfo = item == null || string.IsNullOrWhiteSpace(item.StepId)
                    ? null
                    : EditorUtil.Pipify.Registry.FindById(item.StepId);
                if (stepInfo == null)
                {
                    error = $"Batch 第 {index + 1} 步未配置有效的已注册 StepId。";
                    return false;
                }
                steps[index] = new StepSnapshot
                {
                    index = index,
                    stepId = item.StepId,
                    paramsJson = RedactParamsJson(stepInfo, item.ParamsJson),
                };
            }

            string settingsHash;
            try
            {
                settingsHash = ComputeFileSha256(Path.GetFullPath(settingsPath));
            }
            catch (Exception exception)
            {
                error = "无法计算 PipifySettings SHA-256：" + exception.Message;
                return false;
            }

            state = new State
            {
                SettingsGuid = settingsGuid.ToLowerInvariant(),
                SettingsPath = settingsPath,
                SettingsHash = settingsHash,
                ActiveBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                Batch = batch,
                Snapshot = new BatchSnapshot
                {
                    pipifySettingsGuid = settingsGuid.ToLowerInvariant(),
                    pipifySettingsPath = settingsPath,
                    pipifySettingsHash = settingsHash,
                    batchName = batch.Name,
                    batchDescription = batch.Description ?? string.Empty,
                    activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    steps = steps,
                },
            };
            return true;
        }

        /// <summary>
        /// 按 Step 参数类型上的 PipifyPassword 标记脱敏公开快照，避免凭据进入 Plan、Receipt 与日志。
        /// 原始参数仍保留在 PipifySettings 中，并由文件哈希负责执行前漂移校验。
        /// </summary>
        internal static string RedactParamsJson(PipifyStepInfo stepInfo, string paramsJson)
        {
            string source = paramsJson ?? string.Empty;
            if (stepInfo?.ParamsType == null || string.IsNullOrWhiteSpace(source)) return source;

            FieldInfo[] passwordFields = stepInfo.ParamsType
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => field.GetCustomAttribute<PipifyPasswordAttribute>() != null)
                .ToArray();
            if (passwordFields.Length == 0) return source;

            try
            {
                JObject jsonObject = JObject.Parse(source);
                foreach (FieldInfo field in passwordFields)
                {
                    JProperty property = jsonObject.Property(field.Name, StringComparison.Ordinal);
                    if (property != null) property.Value = "***";
                }
                return jsonObject.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch
            {
                return "{\"redacted\":true}";
            }
        }

        /// <summary>
        /// 比较 Plan 与 Execute 两次捕获的设置文件、构建目标和 Batch 步骤快照。
        /// </summary>
        private static bool SnapshotsEqual(State left, State right)
        {
            return left != null && right != null &&
                   string.Equals(left.SettingsGuid, right.SettingsGuid, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(left.SettingsPath, right.SettingsPath, StringComparison.Ordinal) &&
                   string.Equals(left.SettingsHash, right.SettingsHash, StringComparison.Ordinal) &&
                   string.Equals(left.ActiveBuildTarget, right.ActiveBuildTarget, StringComparison.Ordinal) &&
                   string.Equals(Util.Json.Serialize(left.Snapshot), Util.Json.Serialize(right.Snapshot), StringComparison.Ordinal);
        }

        /// <summary>
        /// 生成可审阅的保守写入摘要；精确领域路径仍由每个已配置 Step 的参数契约决定。
        /// </summary>
        private static string[] BuildWriteSet(State state)
        {
            var writeSet = new List<string>
            {
                state.SettingsPath + " (read-only configuration)",
                "Pipify configured workspace/build/external outputs",
            };
            writeSet.AddRange(state.Snapshot.steps.Select(step => $"Step[{step.index}] {step.stepId}"));
            return writeSet.ToArray();
        }

        /// <summary>
        /// 在异步 Batch 的真实运行期重新取得资源锁，避免 Execute 返回后其它 Project Action 并发写入。
        /// </summary>
        private static IDisposable AcquireBatchLease()
        {
            string[] locks = { "unity-editor", "pipify-batch", "Assets", "Packages", "ProjectSettings", "Build" };
            if (!AgentActionLockManager.TryAcquire(locks, out IDisposable lease))
            {
                throw new InvalidOperationException("Pipify Batch 所需资源正被其它 Action 使用。" );
            }
            return lease;
        }

        /// <summary>
        /// 计算指定设置资产文件的 SHA-256，防止 Plan 后配置被静默替换。
        /// </summary>
        private static string ComputeFileSha256(string fullPath)
        {
            using (FileStream stream = File.OpenRead(fullPath))
            using (SHA256 sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(stream).Select(item => item.ToString("x2")));
            }
        }
    }
}
