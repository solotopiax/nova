/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigNormalizeMasterAction.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   ConfigMaster 统一整理的显式受控保存 Action
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using Editing = NovaFramework.Editor.EditorUtil.Config.DesignTimeEditing;
using Panels = NovaFramework.Editor.EditorUtil.Config.PanelEditing;
using Coord = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.Coord;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction("nova.project.config.normalize-master", "整理配置", "config", AgentActionOperationType.Ensure,
        Description = "以明确坐标为归一锚点整理现有 ConfigMaster 的矩阵、掩码及存量覆盖；完整预演后只保存 Master，不修改字段请求、不导出 Runtime。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead |
            AgentActionEffect.UnityWrite | AgentActionEffect.Credential,
        RequiredEvidence = AgentActionEvidence.Static, Idempotency = AgentActionIdempotency.EnsureState,
        RequiresConfirmation = true, RequiresEditMode = true, Locks = new[] { "unity-editor", "configmaster-assets" })]
    internal sealed class ConfigNormalizeMasterAction : AgentActionHandler<ConfigNormalizeMasterAction.Request>
    {
        /// <summary>用户明确选择目标和存量分支锚点；不从当前窗口或相邻格隐式猜测。</summary>
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string masterGuid;
            [AgentActionRequired] public string platform;
            [AgentActionRequired] public string channel;
            [AgentActionRequired] public string developMode;
        }

        /// <summary>整理收据只保存锚点、完整摘要和实际差异地址，不保存业务字段或凭据原值。</summary>
        [Serializable]
        private sealed class Receipt
        {
            public int schemaVersion = 1;
            public Request request;
            public string assetPath;
            public string metaSha256;
            public string plannedAssetSha256;
            public string beforeStateSha256;
            public string expectedStateSha256;
            public string observedAssetSha256;
            public bool executionStarted;
            public Panels.Difference[] physicalChanges;
        }

        /// <summary>整理执行计划仅在当前 Editor 内有效；内部再次单次消费，重载后只能 Verify。</summary>
        private sealed class State
        {
            internal Receipt Receipt;
            internal int Consumed;
        }

        /// <summary>只接受真实 GUID 及大小写精确的有效三维锚点，None 平台和渠道拒绝。</summary>
        protected override bool TryValidateRequest(Request request, out string error) => TryCoordinate(request, out _, out error);

        /// <summary>复用领域 clone 归一与幂等检查；显示全部实际差异，不能把整理偷偷并入普通字段修改。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryCoordinate(request, out Coord coordinate, out string error) ||
                !ConfigPatchPanelsAction.TryLoad(request.masterGuid, true, out ConfigMasterSO master, out string path, out error))
                return Task.FromResult(Blocked(error));
            try
            {
                string fileHash = FileHash(path, context.CancellationToken);
                string metaHash = FileHash(path + ".meta", context.CancellationToken);
                using (Panels.Preview preview = Panels.BuildNormalizationPreview(master, coordinate))
                {
                    if (Panels.StateHash(master) != preview.BeforeHash || fileHash != FileHash(path, context.CancellationToken) ||
                        metaHash != FileHash(path + ".meta", context.CancellationToken) ||
                        EditorUtility.IsDirty(master) || Editing.HasUnsavedDraft(master))
                        return Task.FromResult(Blocked("整理预演期间目标或草稿发生变化，请重新 Plan。"));
                    var receipt = new Receipt
                    {
                        request = new Request { masterGuid = request.masterGuid, platform = request.platform, channel = request.channel, developMode = request.developMode },
                        assetPath = path, metaSha256 = metaHash, plannedAssetSha256 = fileHash,
                        beforeStateSha256 = preview.BeforeHash, expectedStateSha256 = preview.ExpectedHash,
                        physicalChanges = preview.Differences,
                    };
                    return Task.FromResult(new AgentActionHandlerPlan
                    {
                        Status = "ready", Summary = preview.Differences.Length == 0 ? "当前配置已规范，执行将不保存资产。" :
                            "将整理已展示的存量矩阵、掩码和覆盖差异，只保存目标 ConfigMaster。",
                        DataJson = Util.Json.Serialize(receipt), State = new State { Receipt = receipt },
                        RecoveryPayloadJson = Util.Json.Serialize(receipt),
                        WriteSet = preview.Differences.Length == 0 ? Array.Empty<string>() : new[] { path },
                        Evidence = new[]
                        {
                            "仅在独立工作副本上执行现有统一归一，重复归一需保持目标状态不变。",
                            "锚点决定被合并轴的存量保留分支，全部实际差异必须与任务授权一致；不输出凭据值。",
                            "只保存 Master，不切换当前坐标或平台、不导出 Runtime、不生成资源密钥或清理旧迁移文件。",
                        },
                    });
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(Blocked("现有配置无法无歧义地统一整理，未修改资产；请核对重复坐标、分支冲突或资源密钥问题。")); }
        }

        /// <summary>单次执行后重新归一同一锚点并核对精确差异；统一保存链只保存目标，写入失败保留 partial。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen) || frozen.Receipt == null || Interlocked.Exchange(ref frozen.Consumed, 1) != 0)
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "配置整理计划无效或已使用，请重新 Plan。"));
            Receipt receipt = frozen.Receipt;
            bool writeStarted = false;
            try
            {
                if (!TryCoordinate(receipt.request, out Coord coordinate, out string error) ||
                    !ConfigPatchPanelsAction.TryLoad(receipt.request.masterGuid, true, out ConfigMasterSO master, out string path, out error) ||
                    path != receipt.assetPath)
                    return Task.FromResult(BlockedResult(error ?? "目标配置路径发生变化。"));
                AssertFrozen(master, receipt, context.CancellationToken);
                using (Panels.Preview preview = Panels.BuildNormalizationPreview(master, coordinate))
                {
                    if (preview.BeforeHash != receipt.beforeStateSha256 || preview.ExpectedHash != receipt.expectedStateSha256 ||
                        Util.Json.Serialize(preview.Differences) != Util.Json.Serialize(receipt.physicalChanges))
                        return Task.FromResult(BlockedResult("整理目标状态或精确差异已变化，请重新 Plan。"));
                    context.CancellationToken.ThrowIfCancellationRequested();
                    if (preview.Differences.Length > 0)
                    {
                        using (var serialized = new SerializedObject(preview.WorkingCopy))
                            Editing.SaveWorkingCopy(master, preview.WorkingCopy, serialized, preview.SaveCoordinate,
                                new Editing.SaveOptions(), clone =>
                                {
                                    AssertFrozen(master, receipt, context.CancellationToken);
                                    if (Panels.StateHash(clone) != receipt.expectedStateSha256)
                                        throw new InvalidOperationException("整理提交前工作副本发生变化。");
                                });
                        writeStarted = true;
                    }
                    receipt.executionStarted = true;
                    receipt.observedAssetSha256 = FileHash(path, CancellationToken.None);
                    bool matches = !EditorUtility.IsDirty(master) && !Editing.HasUnsavedDraft(master) &&
                        Panels.StateHash(master) == receipt.expectedStateSha256 &&
                        (preview.Differences.Length != 0 || receipt.observedAssetSha256 == receipt.plannedAssetSha256);
                    AgentActionResult result = Result(receipt, matches ? "success" : "partial", matches ?
                        (writeStarted ? "配置整理已保存并复核，可重新计划所需字段修改；运行配置尚未导出。" : "配置已经规范，未保存资产。") :
                        "整理保存后尚未通过完整复核，请使用 Verify。");
                    if (matches && writeStarted) result.Warnings.AddRange(Editing.NotifyMasterSaved(master));
                    return Task.FromResult(result);
                }
            }
            catch (Editing.DesignTimeSaveException exception)
            {
                writeStarted |= exception.WriteStarted;
                return Task.FromResult(Failure(receipt, writeStarted));
            }
            catch (OperationCanceledException) when (!writeStarted) { throw; }
            catch { return Task.FromResult(Failure(receipt, writeStarted)); }
        }

        /// <summary>只读取收据目标并核对完整配置和保存文件；不再次归一、补结构或保存。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try
            {
                Receipt receipt = Util.Json.Deserialize<Receipt>(receiptJson);
                if (receipt == null || receipt.schemaVersion != 1 || !receipt.executionStarted ||
                    !TryCoordinate(receipt.request, out _, out _) || receipt.physicalChanges == null ||
                    !HashValid(receipt.metaSha256) || !HashValid(receipt.expectedStateSha256) || !HashValid(receipt.observedAssetSha256))
                    return Task.FromResult(BlockedResult("整理 Receipt 缺少有效执行或保存证据，不能重放。"));
                if (!ConfigPatchPanelsAction.TryLoad(receipt.request.masterGuid, true, out ConfigMasterSO master, out string path, out string error) ||
                    path != receipt.assetPath || FileHash(path + ".meta", context.CancellationToken) != receipt.metaSha256)
                    return Task.FromResult(BlockedResult(error ?? "收据目标资产身份发生变化。"));
                bool matches = FileHash(path, context.CancellationToken) == receipt.observedAssetSha256 &&
                    Panels.StateHash(master) == receipt.expectedStateSha256 &&
                    (receipt.physicalChanges.Length != 0 || receipt.observedAssetSha256 == receipt.plannedAssetSha256);
                return Task.FromResult(Result(receipt, matches ? "success" : "partial", matches ?
                    "ConfigMaster 整理状态与已保存文件一致；未声明 Runtime、导出或设备验收。" :
                    "当前配置与整理收据不一致，请核对变更后重新 Plan。"));
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(BlockedResult("无法只读核对配置整理 Receipt，未执行写入。")); }
        }

        /// <summary>锚点必须由请求明确给定的有效枚举名称组成，不能以数字、None 或窗口当前值代替。</summary>
        private static bool TryCoordinate(Request request, out Coord coordinate, out string error)
        {
            coordinate = default;
            error = null;
            if (request == null || request.masterGuid == null || request.masterGuid.Length != 32 || !request.masterGuid.All(Uri.IsHexDigit) ||
                !Exact(request.platform, out PlatformType platform) || platform == PlatformType.None ||
                !Exact(request.channel, out ChannelType channel) || channel == ChannelType.None ||
                !Exact(request.developMode, out DevelopMode mode))
            {
                error = "需要有效 masterGuid 和明确、大小写精确、非 None 平台/渠道及有效开发模式。";
                return false;
            }
            coordinate = new Coord(platform, channel, mode);
            return true;
        }

        /// <summary>枚举解析只接受名称原样一致，拒绝数字和宽松大小写转换。</summary>
        private static bool Exact<T>(string value, out T result) where T : struct =>
            Enum.TryParse(value, false, out result) && Enum.IsDefined(typeof(T), result) && result.ToString() == value;

        /// <summary>提交前同时检查整个文件、meta、完整配置和已注册草稿，不能只冻结将变化的字段。</summary>
        private static void AssertFrozen(ConfigMasterSO master, Receipt receipt, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (EditorUtility.IsDirty(master) || Editing.HasUnsavedDraft(master) ||
                FileHash(receipt.assetPath, token) != receipt.plannedAssetSha256 || FileHash(receipt.assetPath + ".meta", token) != receipt.metaSha256 ||
                Panels.StateHash(master) != receipt.beforeStateSha256)
                throw new InvalidOperationException("目标配置或草稿发生变化。");
        }

        /// <summary>已由领域目标加载门验证的项目文件按完整字节计算 SHA256。</summary>
        private static string FileHash(string path, CancellationToken token) => GenerateActionCommon.ComputeFileHash(
            Path.Combine(GenerateActionCommon.ProjectRoot, path), token);

        /// <summary>验证收据中的完整 SHA256 摘要；空值不能表示执行成功。</summary>
        private static bool HashValid(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);

        /// <summary>未进入写入的阻断计划只显示稳定原因，不回显业务值或原始异常。</summary>
        private static AgentActionHandlerPlan Blocked(string message) => new AgentActionHandlerPlan { Status = "blocked", Summary = message };

        /// <summary>未写入的执行或验证失败不携带伪造成功证据。</summary>
        private static AgentActionResult BlockedResult(string message) => AgentActionResult.Create(null, "blocked", message);

        /// <summary>保存开始后记录当前文件供 Verify；失败不自动撤回配置、不自动重新整理。</summary>
        private static AgentActionResult Failure(Receipt receipt, bool started)
        {
            if (!started) return BlockedResult("整理提交前复核失败，未开始写入配置。");
            receipt.executionStarted = true;
            try { receipt.observedAssetSha256 = FileHash(receipt.assetPath, CancellationToken.None); } catch { }
            return Result(receipt, "partial", "配置整理已开始写入但未通过完整核验，请只读 Verify；不得重放 Execute。");
        }

        /// <summary>公开锚点、完整摘要和实际差异；成功只证明目标 Master 已保存，不证明下游导出或运行。</summary>
        private static AgentActionResult Result(Receipt receipt, string status, string message)
        {
            AgentActionResult result = AgentActionResult.Create(null, status, message);
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.DataJson = result.ReceiptJson;
            if (status == "success") result.EvidenceKinds = AgentActionEvidence.Static;
            result.Evidence.Add("已核对整个 ConfigMaster 状态、meta 身份与目标文件摘要；未导出 Runtime 或生成资源密钥。");
            return result;
        }
    }
}
