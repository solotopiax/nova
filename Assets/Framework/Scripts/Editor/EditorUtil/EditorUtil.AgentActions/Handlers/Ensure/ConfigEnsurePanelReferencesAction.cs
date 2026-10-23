/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigEnsurePanelReferencesAction.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   已登记内嵌配置引用的精确幂等创建与保存
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using NovaFramework.Runtime;
using Editing = NovaFramework.Editor.EditorUtil.Config.DesignTimeEditing;
using Panels = NovaFramework.Editor.EditorUtil.Config.PanelEditing;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction("nova.project.config.ensure-panel-references", "补齐已登记内嵌配置", "config", AgentActionOperationType.Ensure,
        Description = "仅在请求面板的同一逻辑组补缺失白名单引用；首版支持 IAP Mobile/ThirdPay/Voucher Store，不覆盖旧值、不启用顶层 SDK、不导出 Runtime。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead |
            AgentActionEffect.UnityWrite | AgentActionEffect.Credential,
        RequiredEvidence = AgentActionEvidence.Static, Idempotency = AgentActionIdempotency.EnsureState,
        RequiresConfirmation = true, RequiresEditMode = true, Locks = new[] { "unity-editor", "configmaster-assets" })]
    internal sealed class ConfigEnsurePanelReferencesAction : AgentActionHandler<ConfigEnsurePanelReferencesAction.Request>
    {
        /// <summary>路径从 inspect-panel 获取但仍经领域白名单校验；只表达补实例，不接收初始值或动态类型元数据。</summary>
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string masterGuid;
            [AgentActionRequired] public Panels.Target target;
            [AgentActionRequired] public string collectionPath;
            [AgentActionRequired] public string[] typeFullNames;
        }

        /// <summary>恢复载荷只有明确请求、稳定来源、整份状态与脱敏地址；不保存用户原字段或新实例正文。</summary>
        [Serializable]
        private sealed class Receipt
        {
            public int schemaVersion = 1;
            public Request request;
            public string assetPath;
            public string plannedAssetSha256;
            public string metaSha256;
            public string beforeStateSha256;
            public string expectedStateSha256;
            public string observedAssetSha256;
            public bool executionStarted;
            public Panels.ReferenceTypeIdentity[] typeIdentities;
            public Panels.ReferenceAddition[] additions;
            public Panels.Difference[] physicalChanges;
        }

        /// <summary>一次性执行状态独立于恢复载荷；Verify 不能重放补齐。</summary>
        private sealed class State
        {
            internal Receipt Receipt;
            internal int Consumed;
        }

        /// <summary>格式与领域白名单在读取资产之前拒绝任意类型、未声明路径、重复或空补项。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            try
            {
                if (request == null || !GuidValid(request.masterGuid)) throw new InvalidOperationException();
                Panels.ValidateReferenceRequest(request.target, request.collectionPath, request.typeFullNames);
                return true;
            }
            catch { error = "补引用请求需要有效 Master、精确 SDK 坐标、已登记列表路径与无重复类型。"; return false; }
        }

        /// <summary>冻结原资产/全部内存状态/已安装源码；创建仅发生在可销毁副本，计划不保存真实资产。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryValidateRequest(request, out string error) || !ConfigPatchPanelsAction.TryLoad(request.masterGuid, true,
                out ConfigMasterSO master, out string path, out error)) return Task.FromResult(Blocked(error));
            try
            {
                Request frozen = Util.Json.Deserialize<Request>(Util.Json.Serialize(request));
                string fileHash = FileHash(path, context.CancellationToken);
                string metaHash = FileHash(path + ".meta", context.CancellationToken);
                Panels.ReferenceTypeIdentity[] identities = Panels.CaptureReferenceTypes(frozen.typeFullNames, context.CancellationToken);
                using (Panels.ReferencePreview referencePreview = Panels.BuildReferencePreview(master, frozen.target, frozen.collectionPath, frozen.typeFullNames))
                {
                    Panels.Preview preview = referencePreview.PanelPreview;
                    if (fileHash != FileHash(path, context.CancellationToken) || metaHash != FileHash(path + ".meta", context.CancellationToken) ||
                        Panels.StateHash(master) != preview.BeforeHash || EditorUtility.IsDirty(master) || Editing.HasUnsavedDraft(master) ||
                        !Panels.ReferenceTypesMatch(identities, frozen.typeFullNames, true, context.CancellationToken))
                        return Task.FromResult(Blocked("预演期间资产、草稿或已安装类型来源漂移，请重新 Plan。"));
                    var receipt = new Receipt { request = frozen, assetPath = path, plannedAssetSha256 = fileHash, metaSha256 = metaHash,
                        beforeStateSha256 = preview.BeforeHash, expectedStateSha256 = preview.ExpectedHash, typeIdentities = identities,
                        additions = referencePreview.Additions, physicalChanges = preview.Differences };
                    return Task.FromResult(new AgentActionHandlerPlan
                    {
                        Status = "ready", Summary = referencePreview.Additions.Length == 0 ? "已登记引用已满足，保持文件不变。" :
                            "将仅补齐已展示逻辑组的缺失 Store，保留全部旧配置；尚未导出 Runtime。",
                        DataJson = Util.Json.Serialize(new { frozen.masterGuid, assetPath = path, frozen.target, frozen.collectionPath,
                            frozen.typeFullNames, receipt.beforeStateSha256, receipt.expectedStateSha256, receipt.additions,
                            receipt.physicalChanges, receipt.typeIdentities, saveOnly = true }),
                        State = new State { Receipt = receipt }, RecoveryPayloadJson = Util.Json.Serialize(receipt),
                        WriteSet = preview.Differences.Length == 0 ? Array.Empty<string>() : new[] { path },
                        Evidence = new[] { "仅已安装包内三种精确登记类型，程序集/MVID/完整 Runtime 源与包版本已冻结。",
                            "预演只构造独立副本；全部旧项字段、列表顺序和请求外状态保持，实际共享组按真实掩码展开。",
                            "只保存目标 Master，不启用 SDK、不改维度或平台、不导出 Runtime、不发起支付。" },
                    });
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(Blocked("已登记引用预演未通过类型来源、既有结构、旧值保留或精确写集检查。")); }
        }

        /// <summary>一次提交前重新预演和完整复核；只保存目标 Master，已开始写入的失败保留 partial 且不重放。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen) || frozen.Receipt == null || Interlocked.Exchange(ref frozen.Consumed, 1) != 0)
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "补引用计划无效或已消费，请重新 Plan。"));
            Receipt receipt = frozen.Receipt;
            bool writeStarted = false;
            try
            {
                if (!ConfigPatchPanelsAction.TryLoad(receipt.request.masterGuid, true, out ConfigMasterSO master, out string path, out string error) ||
                    path != receipt.assetPath) return Task.FromResult(AgentActionResult.Create(null, "blocked", error ?? "Master 身份漂移。"));
                AssertFrozen(master, receipt, context.CancellationToken);
                using (Panels.ReferencePreview referencePreview = Panels.BuildReferencePreview(master, receipt.request.target,
                    receipt.request.collectionPath, receipt.request.typeFullNames))
                {
                    Panels.Preview preview = referencePreview.PanelPreview;
                    if (preview.BeforeHash != receipt.beforeStateSha256 || preview.ExpectedHash != receipt.expectedStateSha256 ||
                        Util.Json.Serialize(referencePreview.Additions) != Util.Json.Serialize(receipt.additions) ||
                        Util.Json.Serialize(preview.Differences) != Util.Json.Serialize(receipt.physicalChanges))
                        return Task.FromResult(AgentActionResult.Create(null, "blocked", "补引用目标状态或物理写集漂移。"));
                    if (preview.Differences.Length > 0)
                    {
                        using (var so = new SerializedObject(preview.WorkingCopy))
                            Editing.SaveWorkingCopy(master, preview.WorkingCopy, so, preview.SaveCoordinate, new Editing.SaveOptions(), clone =>
                            {
                                AssertFrozen(master, receipt, context.CancellationToken);
                                if (Panels.StateHash(clone) != receipt.expectedStateSha256) throw new InvalidOperationException("提交前副本漂移。");
                            });
                        writeStarted = true;
                    }
                    receipt.executionStarted = true;
                    receipt.observedAssetSha256 = FileHash(path, CancellationToken.None);
                    bool matches = !EditorUtility.IsDirty(master) && !Editing.HasUnsavedDraft(master) &&
                        Panels.StateHash(master) == receipt.expectedStateSha256 && FileHash(path + ".meta", CancellationToken.None) == receipt.metaSha256;
                    AgentActionResult result = Result(receipt, matches ? "success" : "partial", matches ?
                        "缺失 Store 已保存并复核；后续字段配置及 Runtime 导出仍独立执行。" : "补引用保存后证据尚未完整，请只读 Verify。");
                    if (matches && writeStarted) result.Warnings.AddRange(Editing.NotifyMasterSaved(master));
                    return Task.FromResult(result);
                }
            }
            catch (Editing.DesignTimeSaveException exception) { writeStarted |= exception.WriteStarted; return Task.FromResult(Failure(receipt, writeStarted)); }
            catch (OperationCanceledException) when (!writeStarted) { throw; }
            catch { return Task.FromResult(Failure(receipt, writeStarted)); }
        }

        /// <summary>跨重载只读验证保存文件、完整字段状态和稳定类型来源，不创建实例、不归一、不保存。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try
            {
                Receipt receipt = Util.Json.Deserialize<Receipt>(receiptJson);
                if (receipt == null || receipt.schemaVersion != 1 || !receipt.executionStarted ||
                    !TryValidateRequest(receipt.request, out _) || !HashValid(receipt.plannedAssetSha256) || !HashValid(receipt.metaSha256) ||
                    !HashValid(receipt.beforeStateSha256) || !HashValid(receipt.expectedStateSha256) || !HashValid(receipt.observedAssetSha256) ||
                    receipt.physicalChanges == null || receipt.additions == null || receipt.typeIdentities == null)
                    return Task.FromResult(AgentActionResult.Create(null, "blocked", "补引用 Receipt 缺有效执行证据，不能重放。"));
                if (!ConfigPatchPanelsAction.TryLoad(receipt.request.masterGuid, true, out ConfigMasterSO master, out string path, out string error) ||
                    path != receipt.assetPath) return Task.FromResult(AgentActionResult.Create(null, "blocked", error ?? "Receipt 目标路径漂移。"));
                bool matches = FileHash(path, context.CancellationToken) == receipt.observedAssetSha256 &&
                    FileHash(path + ".meta", context.CancellationToken) == receipt.metaSha256 && Panels.StateHash(master) == receipt.expectedStateSha256 &&
                    Panels.ReferenceTypesMatch(receipt.typeIdentities, receipt.request.typeFullNames, false, context.CancellationToken);
                return Task.FromResult(Result(receipt, matches ? "success" : "partial", matches ?
                    "Master 整份状态及 Store 来源一致；未推断支付、Runtime 或设备验收。" : "保存状态或类型来源与 Receipt 不一致，请检查后重新 Plan。"));
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(AgentActionResult.Create(null, "blocked", "无法只读核验补引用 Receipt。")); }
        }

        /// <summary>提交前同时核对磁盘、完整内存语义、草稿与同次类型模块；任何漂移都拒绝。</summary>
        private static void AssertFrozen(ConfigMasterSO master, Receipt receipt, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (EditorUtility.IsDirty(master) || Editing.HasUnsavedDraft(master) || FileHash(receipt.assetPath, token) != receipt.plannedAssetSha256 ||
                FileHash(receipt.assetPath + ".meta", token) != receipt.metaSha256 || Panels.StateHash(master) != receipt.beforeStateSha256 ||
                !Panels.ReferenceTypesMatch(receipt.typeIdentities, receipt.request.typeFullNames, true, token))
                throw new InvalidOperationException("补引用原状态、源码或草稿漂移。");
        }

        /// <summary>文件已经通过 Master 路径门；摘要读取不能改变资产。</summary>
        private static string FileHash(string path, CancellationToken token) => GenerateActionCommon.ComputeFileHash(
            Path.Combine(GenerateActionCommon.ProjectRoot, path), token);

        /// <summary>只接受 Unity 32 位十六进制 GUID。</summary>
        private static bool GuidValid(string value) => value != null && value.Length == 32 && value.All(Uri.IsHexDigit);

        /// <summary>只接受完整 SHA-256 文本。</summary>
        private static bool HashValid(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);

        /// <summary>阻塞摘要不回显配置值或底层异常内容。</summary>
        private static AgentActionHandlerPlan Blocked(string message) => new AgentActionHandlerPlan { Status = "blocked", Summary = message };

        /// <summary>写入开始后保留已观察文件证据，不假称回滚或提供重新 Execute 能力。</summary>
        private static AgentActionResult Failure(Receipt receipt, bool started)
        {
            if (!started) return AgentActionResult.Create(null, "blocked", "补引用提交前复核失败，未开始写入。");
            receipt.executionStarted = true;
            try { receipt.observedAssetSha256 = FileHash(receipt.assetPath, CancellationToken.None); } catch { }
            return Result(receipt, "partial", "补引用写入已开始但核验未完成，请只读 Verify；不得重放 Execute。");
        }

        /// <summary>仅报告整份指纹和创建地址；补实例不等同字段配置、运行导出或支付成功。</summary>
        private static AgentActionResult Result(Receipt receipt, string status, string message)
        {
            AgentActionResult result = AgentActionResult.Create(null, status, message);
            result.DataJson = Util.Json.Serialize(new { receipt.request.masterGuid, receipt.assetPath, receipt.expectedStateSha256,
                receipt.observedAssetSha256, receipt.additions, receipt.physicalChanges, saveOnly = true });
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.EvidenceKinds = AgentActionEvidence.Static;
            result.Evidence.Add("仅核对目标 Master 完整序列化状态、保存文件和登记类型来源。");
            return result;
        }
    }
}
