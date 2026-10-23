/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyConfigureBatchAction.cs
 * author:    Codex
 * created:   2026/10/13
 * descrip:   Pipify 具名 Batch 受控配置入口
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using Editing = NovaFramework.Editor.EditorUtil.Pipify.ConfigurationEditing;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction("nova.project.pipify.configure-batch", "配置 Pipify Batch", "pipify", AgentActionOperationType.Ensure,
        Description = "创建、克隆或编辑具名 Batch、排列已注册 Steps 并配置真实基础字段；只保存不运行。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite | AgentActionEffect.Credential,
        RequiredEvidence = AgentActionEvidence.Static, Idempotency = AgentActionIdempotency.EnsureState,
        RequiresConfirmation = true, RequiresEditMode = true, Locks = new[] { "unity-editor", "pipify-batch", "pipify-settings-assets" })]
    internal sealed class PipifyConfigureBatchAction : AgentActionHandler<PipifyConfigureBatchAction.Request>
    {
        /// <summary>固定 DTO 复用领域契约，客户端不可选择参数类型、方法或任意 C#。</summary>
        [Serializable]
        public sealed class Request : Editing.BatchChange { }

        /// <summary>恢复载荷只含总体摘要与固定目标身份，不携带字段值、默认参数或逐值摘要。</summary>
        [Serializable]
        private sealed class Receipt
        {
            public int schemaVersion = 1;
            public string settingsGuid;
            public string assetPath;
            public string batchName;
            public string plannedAssetSha256;
            public string beforeStateSha256;
            public string expectedStateSha256;
            public string modelSha256;
            public string observedAssetSha256;
            public string buildTarget;
            public bool exportAndroidProject;
            public bool changed;
            public int targetBatchIndex;
            public string[] targetStepIds;
            public string[] requestedFieldAddresses;
            public bool executionStarted;
        }

        private sealed class State
        {
            internal Receipt Receipt;
            internal Editing.BatchChange Change;
            internal Dictionary<int, string> Defaults;
            internal int Consumed;
        }

        /// <summary>按共享领域规则验证形状，GUID 仅接受 32 位十六进制。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (request == null || !IsGuid(request.settingsGuid)) { error = "PipifySettings GUID 无效。"; return false; }
            return Editing.TryValidate(request, out error);
        }

        /// <summary>冻结全资产、参数模型、构建环境及已生成默认值；所有修改只在副本预演。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryValidateRequest(request, out string error) || !TryLoad(request.settingsGuid, true, out PipifySettingsSO settings, out string path, out error))
                return Task.FromResult(Blocked(error));
            try
            {
                string fileHash = FileHash(path, context.CancellationToken);
                using var preview = Editing.BuildPreview(settings, request);
                if (FileHash(path, context.CancellationToken) != fileHash || Editing.StateHash(settings) != preview.BeforeHash || Editing.HasUnsavedDraft(settings) || EditorUtility.IsDirty(settings))
                    return Task.FromResult(Blocked("Pipify 资产或草稿在预演时变化，请重新 Plan。"));
                var receipt = new Receipt { settingsGuid = request.settingsGuid, assetPath = path, batchName = request.batchName,
                    plannedAssetSha256 = fileHash, beforeStateSha256 = preview.BeforeHash, expectedStateSha256 = preview.ExpectedHash,
                    modelSha256 = preview.ModelHash, buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    exportAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject, changed = preview.Changed,
                    targetBatchIndex = preview.TargetBatchIndex, targetStepIds = preview.TargetStepIds, requestedFieldAddresses = preview.RequestedFieldAddresses };
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "ready", Summary = preview.Changed ? "将保存目标 Pipify Batch 配置；不会运行 Step。" : "目标 Batch 已满足，保持资产文件不变。",
                    DataJson = Util.Json.Serialize(new { receipt.settingsGuid, receipt.assetPath, receipt.batchName, receipt.changed,
                        receipt.beforeStateSha256, receipt.expectedStateSha256, receipt.modelSha256, receipt.buildTarget,
                        receipt.targetBatchIndex, receipt.targetStepIds, receipt.requestedFieldAddresses,
                        completeStepsReplacement = request.steps != null,
                        sourceBatchName = request.sourceBatchName,
                        androidDevelopmentAabSigningRisk = preview.AndroidSigningRisk,
                        warning = preview.AndroidSigningRisk ? "首次进入 Android DevelopmentBuild + AAB 组合，实际构建时需核对上传签名。" : null }),
                    State = new State { Receipt = receipt, Change = preview.FrozenChange, Defaults = new Dictionary<int, string>(preview.NewDefaults) },
                    RecoveryPayloadJson = Util.Json.Serialize(receipt), WriteSet = preview.Changed ? new[] { path } : Array.Empty<string>(),
                    Evidence = new[] { "仅真实 Registry ID 与基础参数字段；草稿保护、全状态冻结、完整目标顺序和单资产保存。", "默认值只在预演创建并冻结；密码与输入字段值不出现在公开 Plan 或 Receipt。", "不运行 Step、导出、构建、发送通知或写外部服务。" },
                });
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(Blocked("Pipify 预演未通过字段、候选、草稿或参数结构检查；实际资产未修改。")); }
        }

        /// <summary>单次消费计划，复核资产和环境后只保存目标；写后失败返回 partial，不自动重放。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen) || frozen.Receipt == null || Interlocked.Exchange(ref frozen.Consumed, 1) != 0)
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Pipify 计划无效或已使用，请重新 Plan。"));
            Receipt receipt = frozen.Receipt;
            bool writeStarted = false;
            try
            {
                if (!TryLoad(receipt.settingsGuid, true, out PipifySettingsSO settings, out string path, out string error) || path != receipt.assetPath)
                    return Task.FromResult(AgentActionResult.Create(null, "blocked", error ?? "Pipify 资产路径漂移。"));
                EnsureFrozen(settings, receipt, context.CancellationToken);
                using var preview = Editing.BuildPreview(settings, frozen.Change, frozen.Defaults);
                if (preview.BeforeHash != receipt.beforeStateSha256 || preview.ExpectedHash != receipt.expectedStateSha256 || preview.ModelHash != receipt.modelSha256 || preview.Changed != receipt.changed ||
                    preview.TargetBatchIndex != receipt.targetBatchIndex || !preview.TargetStepIds.SequenceEqual(receipt.targetStepIds) ||
                    !preview.RequestedFieldAddresses.SequenceEqual(receipt.requestedFieldAddresses))
                    return Task.FromResult(AgentActionResult.Create(null, "blocked", "Pipify 目标状态、参数模型或写集漂移，请重新 Plan。"));
                context.CancellationToken.ThrowIfCancellationRequested();
                Editing.SavePreview(settings, preview, () =>
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    EnsureFrozen(settings, receipt, context.CancellationToken);
                    if (Editing.StateHash(preview.WorkingCopy) != receipt.expectedStateSha256) throw new InvalidOperationException("工作副本状态漂移。");
                });
                writeStarted = receipt.changed;
                receipt.executionStarted = true;
                receipt.observedAssetSha256 = FileHash(path, CancellationToken.None);
                bool matches = !EditorUtility.IsDirty(settings) && Editing.StateHash(settings) == receipt.expectedStateSha256 &&
                    (receipt.changed ? receipt.observedAssetSha256 != receipt.plannedAssetSha256 : receipt.observedAssetSha256 == receipt.plannedAssetSha256);
                AgentActionResult result = Result(receipt, matches ? "success" : "partial", matches ? (receipt.changed ? "Pipify Batch 配置已保存；尚未运行。" : "目标配置已满足，未保存资产。") : "保存后状态尚未通过复核，请使用 Verify。");
                if (matches && receipt.changed) result.Warnings.AddRange(Editing.NotifySaved(settings));
                return Task.FromResult(result);
            }
            catch (Editing.SaveException exception) { return Task.FromResult(Failure(receipt, writeStarted || exception.WriteStarted)); }
            catch (OperationCanceledException) when (!writeStarted) { throw; }
            catch { return Task.FromResult(Failure(receipt, writeStarted)); }
        }

        /// <summary>跨重载只读核对整体文件和状态，不构造参数、不调用默认提供者、不保存或重放。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            try { receipt = Util.Json.Deserialize<Receipt>(receiptJson); }
            catch { return Task.FromResult(AgentActionResult.Create(null, "blocked", "Pipify Receipt 无法解析。")); }
            if (receipt == null || receipt.schemaVersion != 1 || !receipt.executionStarted || !IsGuid(receipt.settingsGuid) ||
                !IsHash(receipt.plannedAssetSha256) || !IsHash(receipt.beforeStateSha256) || !IsHash(receipt.modelSha256) ||
                !IsHash(receipt.expectedStateSha256) || !IsHash(receipt.observedAssetSha256) || receipt.targetBatchIndex < 0 ||
                receipt.targetStepIds == null || receipt.targetStepIds.Length > 512 || receipt.requestedFieldAddresses == null)
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Pipify Receipt 缺少有效执行证据；不能重放。"));
            try
            {
                if (!TryLoad(receipt.settingsGuid, false, out PipifySettingsSO settings, out string path, out string error) || path != receipt.assetPath)
                    return Task.FromResult(AgentActionResult.Create(null, "blocked", error ?? "Pipify Receipt 资产漂移。"));
                bool matches = !EditorUtility.IsDirty(settings) && !Editing.HasUnsavedDraft(settings) &&
                    FileHash(path, context.CancellationToken) == receipt.observedAssetSha256 && Editing.StateHash(settings) == receipt.expectedStateSha256;
                return Task.FromResult(Result(receipt, matches ? "success" : "partial", matches ? "Pipify 保存后的完整资产状态已核对；未运行任何 Step。" : "Pipify 状态变化或保存未完成，请先检查资产与窗口。"));
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(Result(receipt, "partial", "无法只读核对 Pipify 保存状态。")); }
        }

        /// <summary>仅定位 Assets 内真实、非子资产、无符号链接目标；写前保护原脏资产和窗口草稿。</summary>
        internal static bool TryLoad(string guid, bool requireClean, out PipifySettingsSO settings, out string path, out string error)
        {
            settings = null; path = null; error = null;
            try
            {
                if (!IsGuid(guid)) { error = "PipifySettings GUID 无效。"; return false; }
                path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !GenerateActionCommon.TryResolveProjectPath(path, "PipifySettings", out string absolute, out _) || !File.Exists(absolute))
                { error = "PipifySettings 必须是 Assets 内现存且无符号链接的资产。"; return false; }
                settings = AssetDatabase.LoadAssetAtPath<PipifySettingsSO>(path);
                if (settings == null || !EditorUtility.IsPersistent(settings) || !AssetDatabase.IsMainAsset(settings) ||
                    !string.Equals(AssetDatabase.AssetPathToGUID(path), guid, StringComparison.OrdinalIgnoreCase) ||
                    requireClean && ((File.GetAttributes(absolute) & FileAttributes.ReadOnly) != 0 || EditorUtility.IsDirty(settings) || Editing.HasUnsavedDraft(settings)))
                { error = "PipifySettings 不可写或存在未保存资产/窗口草稿，请先保存或放弃草稿。"; return false; }
                return true;
            }
            catch { error = "无法安全读取 PipifySettings 资产。"; settings = null; return false; }
        }

        /// <summary>执行前和提交前均复核实际草稿、全状态、Registry 与构建环境。</summary>
        private static void EnsureFrozen(PipifySettingsSO settings, Receipt receipt, CancellationToken cancellation)
        {
            if (EditorUtility.IsDirty(settings) || Editing.HasUnsavedDraft(settings) || Editing.StateHash(settings) != receipt.beforeStateSha256 ||
                FileHash(receipt.assetPath, cancellation) != receipt.plannedAssetSha256 || Editing.ModelHash() != receipt.modelSha256 ||
                EditorUserBuildSettings.activeBuildTarget.ToString() != receipt.buildTarget || EditorUserBuildSettings.exportAsGoogleAndroidProject != receipt.exportAndroidProject)
                throw new InvalidOperationException("Pipify 资产、草稿、字段模型或构建环境漂移。");
        }

        /// <summary>对已通过项目路径门的目标文件取整体摘要。</summary>
        private static string FileHash(string path, CancellationToken cancellation) => GenerateActionCommon.ComputeFileHash(Path.Combine(GenerateActionCommon.ProjectRoot, path), cancellation);
        /// <summary>精确校验 GUID 长度与字符。</summary>
        private static bool IsGuid(string value) => value != null && value.Length == 32 && value.All(Uri.IsHexDigit);
        /// <summary>精确校验整体 SHA-256 文本。</summary>
        private static bool IsHash(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
        /// <summary>阻塞计划不携带执行态或写集。</summary>
        private static AgentActionHandlerPlan Blocked(string error) => new AgentActionHandlerPlan { Status = "blocked", Summary = error };

        /// <summary>公开结果只含固定目标、整体摘要及证据，不携带请求值。</summary>
        private static AgentActionResult Result(Receipt receipt, string status, string message)
        {
            AgentActionResult result = AgentActionResult.Create(null, status, message);
            result.DataJson = Util.Json.Serialize(new { receipt.assetPath, receipt.batchName, receipt.changed, receipt.targetBatchIndex,
                receipt.targetStepIds, receipt.requestedFieldAddresses, receipt.expectedStateSha256, receipt.observedAssetSha256 });
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.EvidenceKinds = AgentActionEvidence.Static;
            result.Evidence.Add("仅目标 PipifySettings 文件及完整序列化状态证据；没有执行 Step 或验证构建/外部系统。");
            return result;
        }

        /// <summary>写开始后返回 partial 收据；失败前不声称已保存，也不重放。</summary>
        private static AgentActionResult Failure(Receipt receipt, bool started)
        {
            if (!started) return AgentActionResult.Create(null, "blocked", "Pipify 提交前检查失败，实际资产未修改，请重新 Plan。");
            receipt.executionStarted = true;
            try { receipt.observedAssetSha256 = FileHash(receipt.assetPath, CancellationToken.None); } catch { receipt.observedAssetSha256 = null; }
            return Result(receipt, "partial", "Pipify 写入已开始，保存或复核未完成；请使用 Verify，不要重放 Execute。");
        }
    }
}
