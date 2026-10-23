/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * filename:  TracksGenerateRegistryAction.cs
 * author:    Codex
 * created:   2026/10/13
 * descrip:   真实登记源冻结后的受控 Tracks 汇总生成
 ***************************************************************/
using System;
using System.IO;
using IOPath = System.IO.Path;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using Registry = NovaFramework.Editor.EditorUtil.TrackRegistry;

namespace NovaFramework.Editor
{
    /// <summary>计划只读提供真实源快照；执行只调用既有汇总器，验证独立核对内容和样式。</summary>
    [AgentAction("nova.project.tracks.generate-registry", "生成 Tracks 汇总", "tracks", AgentActionOperationType.Generate,
        Description = "冻结全部正式登记源及包元数据，生成并核验本地 Tracks 汇总。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput, RequiresConfirmation = true, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "tracks-registry", "table-source" })]
    internal sealed class TracksGenerateRegistryAction : AgentActionHandler<TracksGenerateRegistryAction.Request>
    {
        private const string c_ActionId = "nova.project.tracks.generate-registry";
        private const string c_Output = "Library/Nova/Tracks/Tracks.generated.xlsx";

        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string scope;
        }

        [Serializable]
        internal sealed class Receipt
        {
            public Registry.AgentRegistrySnapshot registry;
            public Registry.AgentSheetState[] sheets;
            public Registry.AgentFileState beforeOutput;
            public GenerateActionCommon.Artifact output;
            public string backupPath;
            public bool executionStarted;
            public bool completed;
        }

        /// <summary>只接受 all 范围；汇总器保留其现有全登记源语义，不伪造选择性生成。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = request?.scope == "all" ? null : "scope 必须是 all；来源由当前真实登记规则确定。";
            return error == null;
        }

        /// <summary>只读冻结全部源/meta/包信息、页内容和既有输出；DataJson 中 registry 可直接供结构 CLI 使用。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            try
            {
                if (!TryValidateRequest(request, out string error)) return PlanResult("blocked", error);
                string root = GenerateActionCommon.ProjectRoot;
                if (!GenerateActionCommon.TryResolveProjectPath(c_Output, "Tracks 输出", out string output, out error))
                    return PlanResult("blocked", error);
                var receipt = new Receipt
                {
                    registry = Registry.CaptureAgentSnapshot(root, context.CancellationToken),
                    sheets = Registry.CaptureAgentSheets(root), beforeOutput = Registry.CaptureAgentFile(output, context.CancellationToken),
                };
                if (receipt.beforeOutput.exists) receipt.backupPath = "Library/Nova/AgentActions/TracksBackups/" + Guid.NewGuid().ToString("N") + ".xlsx";
                string json = Util.Json.Serialize(receipt);
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "ready", Summary = $"将按 {receipt.registry.sources.Length} 个正式源生成 {receipt.sheets.Length} 个 Tracks 页。",
                    State = receipt, DataJson = json, RecoveryPayloadJson = json,
                    WriteSet = receipt.beforeOutput.exists ? new[] { c_Output, receipt.backupPath } : new[] { c_Output },
                    Evidence = new[] { "Plan 未写入源或汇总，已冻结全部整源/meta、清单、解析包元数据、页序与输出基线。" },
                });
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                return PlanResult("blocked", "Tracks 正式来源或输出无法安全冻结：" + exception.Message);
            }
        }

        /// <summary>重验全部快照和输出基线后备份既有派生输出；提交开始后取消或失败均保留 Receipt，不重放。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is Receipt receipt)) return Result("blocked", "缺少 Tracks 冻结状态。");
            string ownedBackup = null;
            try
            {
                RequireSameSnapshot(receipt, context);
                Registry.AgentFileState current = Registry.CaptureAgentFile(receipt.beforeOutput.path, context.CancellationToken);
                if (Util.Json.Serialize(current) != Util.Json.Serialize(receipt.beforeOutput))
                    return Result("blocked", "既有汇总已漂移，请重新 Plan。");
                context.CancellationToken.ThrowIfCancellationRequested();
                if (current.exists)
                {
                    if (!GenerateActionCommon.TryResolveProjectPath(receipt.backupPath, "Tracks 备份", out string backup, out string error))
                        return Result("blocked", error);
                    Directory.CreateDirectory(IOPath.GetDirectoryName(backup));
                    using (var destination = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        // 一旦独占创建就记录归属；拷贝中断可能留下截断备份，必须返回 partial 并保留现场。
                        ownedBackup = backup;
                        using (var input = new FileStream(current.path, FileMode.Open, FileAccess.Read, FileShare.Read)) input.CopyTo(destination);
                        destination.Flush(true);
                    }
                    if (GenerateActionCommon.ComputeFileHash(backup, context.CancellationToken) != current.sha256)
                        throw new InvalidDataException("既有汇总备份摘要不匹配。");
                }
                RequireSameSnapshot(receipt, context);
                if (Util.Json.Serialize(Registry.CaptureAgentFile(receipt.beforeOutput.path, context.CancellationToken)) != Util.Json.Serialize(receipt.beforeOutput))
                    throw new InvalidDataException("原子生成前既有输出基线已漂移。");
                receipt.executionStarted = true;
                Registry.Generate(GenerateActionCommon.ProjectRoot);
                receipt.output = GenerateActionCommon.CaptureFile(c_Output, context.CancellationToken);
                Registry.VerifyAgentOutput(receipt.output.path, receipt.sheets);
                RequireSameSnapshot(receipt, context);
                receipt.completed = true;
                return ReceiptResult(receipt, "partial", "Tracks 已生成，等待只读 Verify。");
            }
            catch (Exception exception)
            {
                // 已进入生成后不得把取消包装成可重试 blocked，也不自动恢复可能被用户继续修改的输出。
                if (receipt.executionStarted) return ReceiptResult(receipt, "partial", "Tracks 提交已开始但取证未完成；保留现场和 Receipt，只 Verify，不重放。" );
                if (ownedBackup != null)
                {
                    try
                    {
                        // 仅清理本次成功创建且仍等于基线的绝对备份，不能依赖进程 CWD 或覆盖后续现场。
                        if (GenerateActionCommon.ComputeFileHash(ownedBackup, System.Threading.CancellationToken.None) != receipt.beforeOutput.sha256)
                            return ReceiptResult(receipt, "partial", "生成前备份摘要已变化，保留现场和 Receipt，不能自动清理。");
                        File.Delete(ownedBackup);
                    }
                    catch (Exception)
                    {
                        return ReceiptResult(receipt, "partial", "生成前备份无法安全清理，保留绝对路径和 Receipt。");
                    }
                }
                if (exception is OperationCanceledException) throw;
                return Result("blocked", "Tracks 尚未生成：" + exception.Message);
            }
        }

        /// <summary>独立只读核对全来源、meta、实际产物摘要、源行和样式；Plan 恢复载荷不算执行证据。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            try { receipt = Util.Json.Deserialize<Receipt>(receiptJson); }
            catch (Exception) { return Result("blocked", "Tracks Receipt 无法解析。"); }
            if (receipt?.registry == null || receipt.sheets == null || receipt.beforeOutput == null ||
                !receipt.executionStarted || !receipt.completed || receipt.output == null)
                return Result("partial", "Tracks Receipt 尚无完整执行证据，不会重放 Generate。");
            try
            {
                if (!GenerateActionCommon.TryResolveProjectPath(c_Output, "Tracks 输出", out string output, out string error) ||
                    receipt.output.path != output || receipt.output.kind != "file")
                    return Result("blocked", error ?? "Receipt 输出不属于固定 Tracks 产物。");
                RequireSameSnapshot(receipt, context);
                if (!GenerateActionCommon.TryVerifyArtifacts(new[] { receipt.output }, context.CancellationToken, out _, out error))
                    return Result("partial", error);
                Registry.VerifyAgentOutput(output, receipt.sheets);
                if (receipt.beforeOutput.exists)
                {
                    if (string.IsNullOrEmpty(receipt.backupPath) || !receipt.backupPath.StartsWith("Library/Nova/AgentActions/TracksBackups/", StringComparison.Ordinal) ||
                        !GenerateActionCommon.TryResolveProjectPath(receipt.backupPath, "Tracks 备份", out string backup, out error) ||
                        GenerateActionCommon.ComputeFileHash(backup, context.CancellationToken) != receipt.beforeOutput.sha256)
                        return Result("partial", "Tracks 备份不存在或摘要已变化。");
                }
                AgentActionResult result = AgentActionResult.Create(c_ActionId, "success", "Tracks 全登记来源、汇总内容、单色样式与 SHA-256 已核对。");
                result.EvidenceKinds = AgentActionEvidence.Static | AgentActionEvidence.Artifact;
                result.DataJson = Util.Json.Serialize(receipt);
                result.Artifacts.Add(c_Output);
                result.Evidence.Add("Excel 登记与本地汇总通过；业务发送及厂商后台接收须另外验收。");
                return Task.FromResult(result);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                return Result("partial", "Tracks 来源、产物或样式已变化：" + exception.Message);
            }
        }

        /// <summary>比较真实全来源和页数据，任何包解析、增删源、meta 或源内容漂移均停止提交或验证。</summary>
        private static void RequireSameSnapshot(Receipt receipt, AgentActionExecutionContext context)
        {
            if (Util.Json.Serialize(Registry.CaptureAgentSnapshot(GenerateActionCommon.ProjectRoot, context.CancellationToken)) != Util.Json.Serialize(receipt.registry) ||
                Util.Json.Serialize(Registry.CaptureAgentSheets(GenerateActionCommon.ProjectRoot)) != Util.Json.Serialize(receipt.sheets))
                throw new InvalidDataException("Tracks 完整登记源或包解析元数据已漂移。");
        }

        /// <summary>附带可恢复验证的领域 Receipt，失败后也保留一次执行边界。</summary>
        private static Task<AgentActionResult> ReceiptResult(Receipt receipt, string status, string message)
        {
            AgentActionResult result = AgentActionResult.Create(c_ActionId, status, message);
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.DataJson = result.ReceiptJson;
            if (receipt.output != null) result.Artifacts.Add(c_Output);
            if (!string.IsNullOrEmpty(receipt.backupPath)) result.Artifacts.Add(receipt.backupPath);
            return Task.FromResult(result);
        }

        /// <summary>返回没有写入承诺的计划状态。</summary>
        private static Task<AgentActionHandlerPlan> PlanResult(string status, string message) =>
            Task.FromResult(new AgentActionHandlerPlan { Status = status, Summary = message });

        /// <summary>返回精确执行或验证状态，不虚构产物证据。</summary>
        private static Task<AgentActionResult> Result(string status, string message) =>
            Task.FromResult(AgentActionResult.Create(c_ActionId, status, message));
    }
}
