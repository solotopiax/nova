/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistenceInspectDataAction.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   复用入口只读检查分片及其他存储元数据
 ***************************************************************/

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NovaFramework.Editor
{
    [AgentAction("nova.project.persistence.inspect-data", "检查存档维护前置", "persistence", AgentActionOperationType.Inspect,
        Description = "FileFragment 严格全集解码；PlayerPrefs 默认范围检查托管索引元数据，managed-indexed-data 范围内部读取完整已索引字符串值并仅输出计数和摘要；SQLite 默认范围只检查固定主库/伴随文件元数据，不开库。PrivacyAES 检查的 Plan/Execute/Verify 均重读源；SQLite AES 已启用且主库存在时打开既有库并执行 BEGIN IMMEDIATE/ROLLBACK，取得写事务锁且可能改变 WAL/SHM。不转换或提交玩家值、不保存配置、不初始化 Manager 或打开 Inspector，不输出玩家值和凭据。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.UnityRead | AgentActionEffect.Credential,
        RequiredEvidence = AgentActionEvidence.Static, Idempotency = AgentActionIdempotency.ReadOnly, RequiresConfirmation = true, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "serialized-target-assets", "persistence-data", "config-master" })]
    internal sealed class PersistenceInspectDataAction : AgentActionHandler<PersistenceMaintenance.Request>
    {
        /// <summary>检查三种精确存储和实际资产身份，不接受加密状态、源摘要或其他写意图。</summary>
        protected override bool TryValidateRequest(PersistenceMaintenance.Request request, out string error)
            => PersistenceMaintenance.ValidateInspection(request, out error);

        /// <summary>冻结检查身份；元数据缺项仍允许只读执行，其他前置失败阻断，不执行恢复或缓存初始化。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(PersistenceMaintenance.Request request, AgentActionExecutionContext context)
        {
            AgentActionResult result = Read(request, context);
            return Task.FromResult(new AgentActionHandlerPlan { Status = result.Status == "success" || result.Status == "partial" ? "ready" :
                    result.Status == "not_applicable" ? "not_applicable" : "blocked",
                Summary = result.Message, DataJson = result.DataJson, State = request, RecoveryPayloadJson = JsonConvert.SerializeObject(request),
                Evidence = new[] { "按 storage 执行严格分片检查或限定索引/文件元数据检查；无恢复、隔离或玩家值输出。",
                    "PlayerPrefs 未证明全量键或落盘；SQLite 未证明原子快照、逻辑完整性或连接独占。" } });
        }

        /// <summary>重新读取最新数据，之前 Plan 的摘要不替代当前事实。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context) =>
            Task.FromResult(Read(state as PersistenceMaintenance.Request, context));

        /// <summary>重载后恢复请求身份重新只读检查，不执行迁移，旧元数据不替代当前观察。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try { return Task.FromResult(Read(JsonConvert.DeserializeObject<PersistenceMaintenance.Request>(receiptJson), context)); }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(AgentActionResult.Create(null, "blocked", "检查收据无法解析。")); }
        }

        /// <summary>按存储和固定范围选择严格分片、元数据或完整已索引字符串值检查；错误不回显玩家值、凭据或底层异常。</summary>
        private static AgentActionResult Read(PersistenceMaintenance.Request request, AgentActionExecutionContext context)
        {
            try
            {
                if (!PersistenceMaintenance.ValidateInspection(request, out _)) throw new InvalidOperationException();
                if (request.operation == "inspect-privacy-aes")
                {
                    AgentActionResult privacy = PersistenceMaintenance.InspectPrivacyRotation(request, context.CancellationToken);
                    privacy.ReceiptJson = JsonConvert.SerializeObject(request);
                    return privacy;
                }
                if (request.storage == "PlayerPrefs" && request.dataScope == "current-editor-managed-indexed-data")
                    return PersistenceMaintenance.InspectPlayerPrefsValues(request, context.CancellationToken);
                if (request.storage != "FileFragment") return PersistenceMaintenance.InspectMetadata(request, context.CancellationToken);
                var plan = PersistenceMaintenance.Build(request, false, context.CancellationToken);
                var result = PersistenceMaintenance.Result(plan.Journal, "success", "现存分片全集已严格只读检查；没有恢复、备份或写入。");
                result.EvidenceKinds = AgentActionEvidence.Static;
                result.ReceiptJson = JsonConvert.SerializeObject(request);
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch { return AgentActionResult.Create(null, "blocked", "目标、占用、格式、范围、容量或检查期间稳定性不满足只读检查；未写入数据或配置。"); }
        }
    }
}
