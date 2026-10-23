/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistenceMigrateEncryptionAction.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   复用存档维护服务的强类型加密切换
 ***************************************************************/

using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NovaFramework.Editor
{
    [AgentAction("nova.project.persistence.migrate-encryption", "受控切换存档加密", "persistence", AgentActionOperationType.Ensure,
        Description = "受控切换 FileFragment 全集、PlayerPrefs 已索引值或 SQLite 标准值 AES；PlayerPrefs 显式接受全局 Save，SQLite 维护门与同库事务；macOS Editor 安全封存后可同库轮换非空 Cipher。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead |
            AgentActionEffect.UnityWrite | AgentActionEffect.Credential | AgentActionEffect.Destructive,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact, Idempotency = AgentActionIdempotency.EnsureState,
        RequiresConfirmation = true, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "serialized-target-assets", "persistence-data", "config-master" })]
    internal sealed class PersistenceMigrateEncryptionAction : AgentActionHandler<PersistenceMaintenance.Request>
    {
        /// <summary>只接受固定存储和唯一资产标识及显式布尔状态；拒绝路径、密钥或任意方法。</summary>
        protected override bool TryValidateRequest(PersistenceMaintenance.Request request, out string error) => PersistenceMaintenance.Validate(request, true, out error);

        /// <summary>Plan 严格核对源和待应用字段缓存，不提交玩家值、备份或阶段文件；SQLite 事务读取可能改变伴随文件。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(PersistenceMaintenance.Request request, AgentActionExecutionContext context)
        {
            try
            {
                if (request.operation == "rotate-privacy-aes")
                {
                    var privacy = PersistenceMaintenance.BuildPrivacyRotation(request, true, context.CancellationToken);
                    var privacyResult = PersistenceMaintenance.PrivacyResult(privacy.Journal, "ready", "已冻结当前 Editor 三存储与 Privacy 配置范围；执行前封存独立恢复凭据。");
                    return Task.FromResult(new AgentActionHandlerPlan { Status = "ready", Summary = privacyResult.Message, State = privacy,
                        DataJson = privacyResult.DataJson, RecoveryPayloadJson = privacyResult.ReceiptJson,
                        WriteSet = new[] { "current-editor-privacy-aes-data", "config-master", "config-runtime-export",
                            "Library/Nova/AgentActions/PersistenceMaintenance/" + privacy.Journal.operationId, "project-keychain-credential" },
                        Evidence = new[] { "仅当前 Editor 数据与已冻结默认 AES 消费者范围，PlayerPrefs Save 会提交全进程缓存。",
                            "跨存储/配置不是原子事务；独立备份、持久阶段和恢复门禁覆盖中断，真机与外部消费者另验。" } });
                }
                if (request.operation == "rotate-sqlite-cipher")
                {
                    var cipher = PersistenceMaintenance.BuildCipher(request, context.CancellationToken);
                    var cipherResult = PersistenceMaintenance.CipherResult(cipher.Journal, "ready", "已冻结 Cipher 轮换策略与原库逻辑；Execute 将先封存恢复凭据再写库。");
                    return Task.FromResult(new AgentActionHandlerPlan { Status = "ready", Summary = cipherResult.Message, State = cipher,
                        DataJson = cipherResult.DataJson, RecoveryPayloadJson = cipherResult.ReceiptJson,
                        WriteSet = new[] { cipher.Journal.sourceRoot, cipher.Journal.beforeSettings.assetPath,
                            "Library/Nova/AgentActions/PersistenceMaintenance/" + cipher.Journal.operationId, "project-keychain-credential" },
                        Evidence = new[] { "Plan 不生成新密码、不创建 Keychain 项或备份；事务读可能改变伴随文件。",
                            "Execute 先安全封存旧/新/独立备份秘密，再同库非空→非空 rekey；不删除或替换活库。",
                            "macOS 是 Editor 凭据宿主边界，设备目标平台与耐久性另行验证。" } });
                }
                if (request.storage != "FileFragment")
                {
                    var values = PersistenceMaintenance.BuildValues(request, context.CancellationToken);
                    var valueResult = PersistenceMaintenance.ValueResult(values.Journal, "ready", "已冻结值转换、受保护备份和唯一字段；本轮未执行玩家值维护。");
                    return Task.FromResult(new AgentActionHandlerPlan { Status = "ready", Summary = valueResult.Message,
                        DataJson = valueResult.DataJson, State = values, RecoveryPayloadJson = valueResult.ReceiptJson,
                        WriteSet = values.Journal.origin.beforeAES == values.Journal.origin.afterAES ? Array.Empty<string>() : new[] {
                            values.Journal.origin.sourceRoot, values.Journal.origin.settings.assetPath,
                            "Library/Nova/AgentActions/PersistenceMaintenance/" + values.Journal.origin.operationId },
                        Evidence = new[] { "PlayerPrefs 仅完整 indexed strings；Save 将提交整个进程缓存，无关脏值副作用无法恢复。",
                            "SQLite Plan 使用真实写事务验证现有标准 schema/值，可能改变伴随文件；执行同库事务，不删除或替换主库。",
                            "值 AES 与 Cipher 轮换为独立 typed operation；Config AES Key/IV 轮换另需完整多存储契约；设备重启耐久性另验。" } });
                }
                var plan = PersistenceMaintenance.Build(request, true, context.CancellationToken);
                var journal = plan.Journal;
                var result = PersistenceMaintenance.Result(journal, "ready", "计划已冻结当前数据与配置；执行将保留整集原字节恢复材料。");
                return Task.FromResult(new AgentActionHandlerPlan { Status = "ready", Summary = result.Message,
                    DataJson = result.DataJson, State = plan, RecoveryPayloadJson = result.ReceiptJson,
                    WriteSet = journal.beforeAES == journal.afterAES ? Array.Empty<string>() : journal.entries.Select(entry =>
                            System.IO.Path.Combine(journal.sourceRoot, entry.name)).Concat(new[] { journal.settings.assetPath,
                            "Library/Nova/AgentActions/PersistenceMaintenance/" + journal.operationId, FileFragmentMaintenance.StagePath(journal.sourceRoot, journal.operationId) }).ToArray(),
                    Evidence = new[] { "严格完整解码与全体文件摘要；Stage、备份和 journal 尚未创建。",
                        "只支持已有 FileFragment AES 开关，非密钥轮换/PlayerPrefs/SQLite；真实设备耐久性另行验证。" } });
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "目标、当前配置、草稿、严格数据或维护容量不满足；未写入。" }); }
        }

        /// <summary>单次委托共享维护核心执行；提交后错误保留真实 partial 与恢复标识，不重放。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context) =>
            Task.FromResult(state is PersistenceMaintenance.PrivacyPlan privacy ? PersistenceMaintenance.ExecutePrivacyRotation(privacy, context.CancellationToken) :
                state is PersistenceMaintenance.CipherPlan cipher ? PersistenceMaintenance.ExecuteCipher(cipher, context.CancellationToken) :
                state is PersistenceMaintenance.ValuePlan values ? PersistenceMaintenance.ExecuteValues(values, context.CancellationToken) :
                PersistenceMaintenance.Execute(state as PersistenceMaintenance.Plan, context.CancellationToken));

        /// <summary>跨重载仅从持久 journal 只读验证；恢复是新的独立强类型 Action。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context) =>
            Task.FromResult(Newtonsoft.Json.Linq.JObject.Parse(receiptJson).Value<int>("schemaVersion") == 4 ?
                PersistenceMaintenance.VerifyPrivacyRotation(receiptJson, context.CancellationToken) :
                Newtonsoft.Json.Linq.JObject.Parse(receiptJson).Value<int>("schemaVersion") == 3 ?
                PersistenceMaintenance.VerifyCipher(receiptJson, context.CancellationToken) :
                Newtonsoft.Json.Linq.JObject.Parse(receiptJson).Value<int>("schemaVersion") == 2 ?
                PersistenceMaintenance.VerifyValues(receiptJson, context.CancellationToken) : PersistenceMaintenance.Verify(receiptJson, context.CancellationToken));
    }
}
