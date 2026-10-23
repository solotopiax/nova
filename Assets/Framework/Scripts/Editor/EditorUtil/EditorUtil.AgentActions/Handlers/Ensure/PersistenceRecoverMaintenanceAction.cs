/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistenceRecoverMaintenanceAction.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   按规范操作标识独立计划存档维护恢复
 ***************************************************************/

using System;
using System.Threading.Tasks;

namespace NovaFramework.Editor
{
    [AgentAction("nova.project.persistence.recover-maintenance", "恢复存档维护", "persistence", AgentActionOperationType.Ensure,
        Description = "独立冻结规范 operationId 的 journal、原字节备份与当前数据/配置；只恢复本操作原或新字节，不覆盖第三方变更，不重放迁移。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead |
            AgentActionEffect.UnityWrite | AgentActionEffect.Credential | AgentActionEffect.Destructive,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact, Idempotency = AgentActionIdempotency.EnsureState,
        RequiresConfirmation = true, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "serialized-target-assets", "persistence-data", "config-master" })]
    internal sealed class PersistenceRecoverMaintenanceAction : AgentActionHandler<PersistenceRecoverMaintenanceAction.Request>
    {
        /// <summary>仅接受同项目维护操作标识，不允许外部路径、数据值或凭据。</summary>
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string operationId;
            [AgentActionRequired] public string migrationRecoveryToken;
        }

        /// <summary>操作标识必须是规范 N 格式 GUID。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = request != null && Guid.TryParseExact(request.operationId, "N", out _) &&
                request.migrationRecoveryToken != null && request.migrationRecoveryToken.StartsWith(AgentActionOperationStore.RecoveryTokenPrefix, StringComparison.Ordinal) &&
                Guid.TryParseExact(request.migrationRecoveryToken.Substring(AgentActionOperationStore.RecoveryTokenPrefix.Length), "N", out _) ? null :
                "须提供维护收据的 operationId 和原 migrate-encryption 的 migrationRecoveryToken。";
            return error == null;
        }

        /// <summary>恢复独立预演原配置并验证完整备份及本操作字节，未开始还原。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            try
            {
                if (PersistenceMaintenance.HasPrivacyJournal(request.operationId))
                {
                    var privacy = PersistenceMaintenance.BuildPrivacyRecovery(request.operationId, request.migrationRecoveryToken, context.CancellationToken);
                    var privacyResult = PersistenceMaintenance.PrivacyResult(privacy.Journal, "ready", "已冻结本操作三存储、Privacy Master/Runtime 与独立恢复材料。");
                    return Task.FromResult(new AgentActionHandlerPlan { Status = "ready", Summary = privacyResult.Message, State = privacy,
                        DataJson = privacyResult.DataJson, RecoveryPayloadJson = privacyResult.ReceiptJson,
                        WriteSet = new[] { "current-editor-privacy-aes-data", "config-master", "config-runtime-export",
                            "Library/Nova/AgentActions/PersistenceMaintenance/" + request.operationId },
                        Evidence = new[] { "只恢复完整本操作原/新状态；原token、Keychain、备份或第三方状态不满足即阻断。" } });
                }
                if (PersistenceMaintenance.HasCipherJournal(request.operationId))
                {
                    var cipher = PersistenceMaintenance.BuildCipherRecovery(request.operationId, request.migrationRecoveryToken, context.CancellationToken);
                    var cipherResult = PersistenceMaintenance.CipherResult(cipher.Journal, "ready", "已冻结本操作旧/新 Cipher、独立加密备份与完整资产状态；恢复使用新的同库事务。");
                    return Task.FromResult(new AgentActionHandlerPlan { Status = "ready", Summary = cipherResult.Message, State = cipher,
                        DataJson = cipherResult.DataJson, RecoveryPayloadJson = cipherResult.ReceiptJson,
                        WriteSet = new[] { cipher.Journal.sourceRoot, cipher.Journal.beforeSettings.assetPath,
                            "Library/Nova/AgentActions/PersistenceMaintenance/" + request.operationId },
                        Evidence = new[] { "跨 domain reload 凭据从项目 Keychain 读取；备份保护独立于新旧 Cipher 和 Privacy。",
                            "只接受原操作完整数据/资产状态，不覆盖第三方变化，不替换活库。" } });
                }
                if (PersistenceMaintenance.HasValueJournal(request.operationId))
                {
                    var values = PersistenceMaintenance.BuildValueRecovery(request.operationId, request.migrationRecoveryToken, context.CancellationToken);
                    var valueResult = PersistenceMaintenance.ValueResult(values.Journal, "ready", "已冻结完整原/新值范围；恢复以新事务还原值和唯一原字段。");
                    return Task.FromResult(new AgentActionHandlerPlan { Status = "ready", Summary = valueResult.Message, State = values,
                        DataJson = valueResult.DataJson, RecoveryPayloadJson = valueResult.ReceiptJson,
                        WriteSet = new[] { values.Journal.origin.sourceRoot, values.Journal.origin.settings.assetPath,
                            "Library/Nova/AgentActions/PersistenceMaintenance/" + request.operationId },
                        Evidence = new[] { "只接受原操作完整身份/原新值；PlayerPrefs 无关全局 Save 副作用不能恢复，SQLite 使用新的同库恢复事务。" } });
                }
                var plan = PersistenceMaintenance.BuildRecovery(request.operationId, request.migrationRecoveryToken, context.CancellationToken);
                var result = PersistenceMaintenance.Result(plan.Journal, "ready", "已冻结本操作恢复范围；第三方字节或资产改动将阻断。");
                return Task.FromResult(new AgentActionHandlerPlan { Status = "ready", Summary = result.Message, State = plan,
                    DataJson = result.DataJson, RecoveryPayloadJson = result.ReceiptJson,
                    WriteSet = new[] { plan.Journal.sourceRoot, plan.Journal.settings.assetPath,
                        "Library/Nova/AgentActions/PersistenceMaintenance/" + request.operationId },
                    Evidence = new[] { "只读验证原字节备份、规范日志和当前原/新全集；恢复需独立确认计划，不自动重放迁移。" } });
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "恢复日志、数据、配置、备份或占用不满足，未开始还原。" }); }
        }

        /// <summary>单次恢复完整原字节与原字段，提交后失败留下 partial 及恢复日志。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context) =>
            Task.FromResult(state is PersistenceMaintenance.PrivacyPlan privacy ? PersistenceMaintenance.RecoverPrivacyRotation(privacy, context.CancellationToken) :
                state is PersistenceMaintenance.CipherPlan cipher ? PersistenceMaintenance.RecoverCipher(cipher, context.CancellationToken) :
                state is PersistenceMaintenance.ValuePlan values ? PersistenceMaintenance.RecoverValues(values, context.CancellationToken) :
                PersistenceMaintenance.Recover(state as PersistenceMaintenance.Plan, context.CancellationToken));

        /// <summary>只读重核恢复结果，不再次写入或抢占活跃数据锁。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context) =>
            Task.FromResult(Newtonsoft.Json.Linq.JObject.Parse(receiptJson).Value<int>("schemaVersion") == 4 ?
                PersistenceMaintenance.VerifyPrivacyRotation(receiptJson, context.CancellationToken) :
                Newtonsoft.Json.Linq.JObject.Parse(receiptJson).Value<int>("schemaVersion") == 3 ?
                PersistenceMaintenance.VerifyCipher(receiptJson, context.CancellationToken) :
                Newtonsoft.Json.Linq.JObject.Parse(receiptJson).Value<int>("schemaVersion") == 2 ?
                PersistenceMaintenance.VerifyValues(receiptJson, context.CancellationToken) : PersistenceMaintenance.Verify(receiptJson, context.CancellationToken));
    }
}
