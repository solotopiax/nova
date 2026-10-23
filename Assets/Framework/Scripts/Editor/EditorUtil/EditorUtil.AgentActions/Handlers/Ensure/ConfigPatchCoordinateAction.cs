/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigPatchCoordinateAction.cs
 * author:    taoye
 * created:   2026/10/12
 * descrip:   ConfigMaster 固定字段批量编辑与互斥坐标选择受控 Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using Path = System.IO.Path;
using Patch = NovaFramework.Editor.EditorUtil.Config.CoordinatePatchService;
using Editing = NovaFramework.Editor.EditorUtil.Config.DesignTimeEditing;
using Coord = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.Coord;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.config.patch-coordinate", "批量编辑 Config 坐标", "config", AgentActionOperationType.Ensure,
        Description = "按明确坐标编辑固定字段，或互斥切换并保存渠道/开发模式；平台核对 Active BuildTarget，复用统一保存，不导出运行配置。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite | AgentActionEffect.Credential,
        RequiredEvidence = AgentActionEvidence.Static,
        Idempotency = AgentActionIdempotency.EnsureState,
        RequiresConfirmation = true, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "configmaster-assets" })]
    internal sealed class ConfigPatchCoordinateAction : AgentActionHandler<ConfigPatchCoordinateAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string masterGuid;
            public CoordinatePatch[] patches;
            /// <summary>省略时保持原固定字段请求；coordinate-switch 表示互斥的当前坐标切换。</summary>
            public string operation;
            public CoordinateSelection coordinate;
        }

        /// <summary>坐标切换的完整目标；三个精确枚举名称均必须明示，平台仅校验、不写入。</summary>
        [Serializable]
        public sealed class CoordinateSelection
        {
            [AgentActionRequired] public string platform;
            [AgentActionRequired] public string channel;
            [AgentActionRequired] public string developMode;
        }

        /// <summary>非 null 包装表示明确修改；value="" 表示清空，省略字段则保留。</summary>
        [Serializable]
        public sealed class StringValue
        {
            [AgentActionRequired] public string value;
        }

        [Serializable]
        public sealed class AppPatch
        {
            public StringValue AppID;
            public StringValue AppAesKey;
            public StringValue AppAesIV;
            public StringValue CustomConfigCmdName;
            public StringValue CustomName;
        }

        [Serializable]
        public sealed class CoordinatePatch
        {
            [AgentActionRequired] public string platform;
            [AgentActionRequired] public string channel;
            [AgentActionRequired] public string developMode;
            public StringValue Namespace;
            public AppPatch AppConfigs;
        }

        /// <summary>跨重载只读验证载荷；只含总体摘要与精确字段地址，不包含输入值。</summary>
        [Serializable]
        private sealed class Receipt
        {
            public int schemaVersion = 1;
            public string masterGuid;
            public string assetPath;
            public string plannedAssetSha256;
            public string beforeStateSha256;
            public string expectedStateSha256;
            public string observedAssetSha256;
            public bool executionStarted;
            public Patch.PhysicalChange[] physicalChanges;
            public string operation;
            public CoordinateSelection coordinate;
            public string activeBuildTarget;
            public string[] coordinateChanges;
        }

        private sealed class State
        {
            public Receipt Receipt;
            public Patch.Edit[] Edits;
            public int Consumed;
        }

        private sealed class CoordinateState
        {
            public Receipt Receipt;
            public Coord Coordinate;
            public int Consumed;
        }

        private const string c_CoordinateSwitch = "coordinate-switch";

        /// <summary>验证互斥请求；固定字段可指定其它有效平台，坐标选择只能匹配当前活动平台。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (request?.operation == c_CoordinateSwitch)
                return TryBuildCoordinateSwitch(request, out _, out error);
            return TryBuildEdits(request, out _, out error);
        }

        /// <summary>只读加载实际资产；全部领域 Getter、校验、广播与保存预演只作用于 clone。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (request?.operation == c_CoordinateSwitch) return PlanCoordinateSwitch(request, context);
            if (!TryBuildEdits(request, out Patch.Edit[] edits, out string error) ||
                !TryLoadCleanMaster(request.masterGuid, out ConfigMasterSO master, out string path, out error))
                return Task.FromResult(BlockedPlan(error));
            try
            {
                string assetHash = GenerateActionCommon.ComputeFileHash(Absolute(path), context.CancellationToken);
                using (Patch.Preview preview = Patch.BuildPreview(master, edits))
                {
                    if (!string.Equals(assetHash, GenerateActionCommon.ComputeFileHash(Absolute(path), context.CancellationToken), StringComparison.Ordinal))
                        return Task.FromResult(BlockedPlan("预演期间 ConfigMaster 文件发生变化，请重新 Plan。"));
                    var receipt = new Receipt
                    {
                        masterGuid = request.masterGuid, assetPath = path, plannedAssetSha256 = assetHash,
                        beforeStateSha256 = preview.BeforeStateHash, expectedStateSha256 = preview.ExpectedStateHash,
                        physicalChanges = preview.Changes,
                    };
                    var view = new
                    {
                        receipt.masterGuid, receipt.assetPath, receipt.plannedAssetSha256,
                        receipt.beforeStateSha256, receipt.expectedStateSha256,
                        requestedPatchCount = request.patches.Length,
                        changedPhysicalFieldCount = preview.Changes.Length,
                        physicalChanges = preview.Changes,
                        remainingIssues = preview.RemainingIssues,
                        configurationErrorsRemain = preview.RemainingIssues.Any(issue => issue.severity == "Error"),
                    };
                    return Task.FromResult(new AgentActionHandlerPlan
                    {
                        Status = "ready",
                        Summary = preview.Changes.Length == 0 ? "目标字段已满足，无需保存。" :
                            $"将修改 {preview.Changes.Length} 个物理坐标字段；只保存 ConfigMaster，运行配置需另行导出。",
                        DataJson = Util.Json.Serialize(view),
                        State = new State { Receipt = receipt, Edits = preview.Edits },
                        RecoveryPayloadJson = Util.Json.Serialize(receipt),
                        WriteSet = preview.Changes.Length == 0 ? Array.Empty<string>() : new[] { path },
                        Evidence = new[]
                        {
                            "只读冻结资产 GUID、文件摘要、完整序列化状态和精确物理字段写集；不输出字段值或逐项密钥摘要。",
                            "全部字段编辑、同组广播、保存归一及校验只在工作副本预演；不修改 activeBuildTarget、当前坐标或导出目标。",
                            "缺少其它必填值允许逐步补齐；剩余问题不表示服务器或真机配置已经可用。",
                        },
                    });
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Editing.DesignTimeSaveException)
            {
                return Task.FromResult(BlockedPlan("配置预演归一或资源密钥校验失败，请先在 ConfigWindow 整理并保存。"));
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult(BlockedPlan("配置预演未通过结构、同组冲突或精确写集检查；请核对逻辑组内请求值并在 ConfigWindow 整理保存。"));
            }
            catch { return Task.FromResult(BlockedPlan("无法安全完成配置预演，未修改实际 ConfigMaster。")); }
        }

        /// <summary>内部执行态单次消费，复核完整漂移和草稿后调用共享保存；写入开始后的错误返回 partial。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (state is CoordinateState coordinateState) return ExecuteCoordinateSwitch(coordinateState, context);
            if (!(state is State frozen) || frozen.Receipt == null || frozen.Edits == null ||
                Interlocked.Exchange(ref frozen.Consumed, 1) != 0)
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "配置编辑计划无效或已使用，请重新 Plan。"));
            Receipt receipt = frozen.Receipt;
            if (!TryLoadCleanMaster(receipt.masterGuid, out ConfigMasterSO master, out string path, out string error) ||
                path != receipt.assetPath)
                return Task.FromResult(AgentActionResult.Create(null, "blocked", error ?? "ConfigMaster 路径已漂移。"));
            bool writeStarted = false;
            try
            {
                if (GenerateActionCommon.ComputeFileHash(Absolute(path), context.CancellationToken) != receipt.plannedAssetSha256 ||
                    Patch.StateHash(master) != receipt.beforeStateSha256)
                    return Task.FromResult(AgentActionResult.Create(null, "blocked", "ConfigMaster 文件或序列化状态已漂移，请重新 Plan。"));
                using (Patch.Preview preview = Patch.BuildPreview(master, frozen.Edits))
                {
                    if (preview.BeforeStateHash != receipt.beforeStateSha256 || preview.ExpectedStateHash != receipt.expectedStateSha256 ||
                        Util.Json.Serialize(preview.Changes) != Util.Json.Serialize(receipt.physicalChanges))
                        return Task.FromResult(AgentActionResult.Create(null, "blocked", "配置精确写集或目标状态已漂移，请重新 Plan。"));
                    context.CancellationToken.ThrowIfCancellationRequested();
                    receipt.executionStarted = true;
                    if (preview.Changes.Length > 0)
                    {
                        using (var so = new SerializedObject(preview.WorkingCopy))
                        {
                            Editing.SaveWorkingCopy(master, preview.WorkingCopy, so, frozen.Edits[0].Coordinate,
                                new Editing.SaveOptions(), workingCopy =>
                                {
                                    context.CancellationToken.ThrowIfCancellationRequested();
                                    if (EditorUtility.IsDirty(master) || Editing.HasUnsavedDraft(master) ||
                                        GenerateActionCommon.ComputeFileHash(Absolute(path), context.CancellationToken) != receipt.plannedAssetSha256 ||
                                        Patch.StateHash(master) != receipt.beforeStateSha256 ||
                                        Patch.StateHash(workingCopy) != receipt.expectedStateSha256)
                                        throw new InvalidOperationException("提交前配置状态、草稿或预期状态发生变化。");
                                });
                            writeStarted = true;
                        }
                    }
                    receipt.observedAssetSha256 = GenerateActionCommon.ComputeFileHash(Absolute(path), CancellationToken.None);
                    bool matches = !EditorUtility.IsDirty(master) && Patch.StateHash(master) == receipt.expectedStateSha256 &&
                        (preview.Changes.Length == 0 || receipt.observedAssetSha256 != receipt.plannedAssetSha256);
                    AgentActionResult result = BuildResult(receipt, matches ? "success" : "partial", matches ?
                        (preview.Changes.Length == 0 ? "目标字段已满足，未保存资产。" : "已保存配置批量修改；运行配置需要另行导出。") :
                        "保存已执行，但目标文件或完整状态未通过复核，请使用 Verify 检查。", preview.RemainingIssues);
                    if (matches && preview.Changes.Length > 0) result.Warnings.AddRange(Editing.NotifyMasterSaved(master));
                    return Task.FromResult(result);
                }
            }
            catch (Editing.DesignTimeSaveException exception)
            {
                writeStarted |= exception.WriteStarted;
                return Task.FromResult(FailureResult(receipt, writeStarted));
            }
            catch (OperationCanceledException) when (!writeStarted) { throw; }
            catch { return Task.FromResult(FailureResult(receipt, writeStarted)); }
        }

        /// <summary>仅重新加载并核对整体状态/文件摘要及精确字段地址，不执行保存、校验 getter 或恢复重放。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            try { receipt = Util.Json.Deserialize<Receipt>(receiptJson); }
            catch { return Task.FromResult(AgentActionResult.Create(null, "blocked", "配置编辑 Receipt 无法解析。")); }
            if (!ValidReceipt(receipt) || !receipt.executionStarted || !IsHash(receipt.observedAssetSha256))
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Receipt 缺少有效执行证据，不能重放配置编辑。"));
            if (!TryLoadCleanMaster(receipt.masterGuid, out ConfigMasterSO master, out string path, out string error) || path != receipt.assetPath)
                return Task.FromResult(AgentActionResult.Create(null, "blocked", error ?? "Receipt 的资产路径已漂移。"));
            try
            {
                string fileHash = GenerateActionCommon.ComputeFileHash(Absolute(path), context.CancellationToken);
                bool matches = fileHash == receipt.observedAssetSha256 && Patch.StateHash(master) == receipt.expectedStateSha256;
                if (receipt.operation == c_CoordinateSwitch)
                    matches &= MatchesFrozenBuildTarget(receipt) &&
                               receipt.coordinate.channel == master.CurrentChannel.ToString() &&
                               receipt.coordinate.developMode == master.CurrentDevelopMode.ToString();
                return Task.FromResult(BuildResult(receipt, matches ? "success" : "partial", matches ?
                    "ConfigMaster 的完整目标状态与已保存文件摘要一致；这不代表运行配置已导出或游戏已运行。" :
                    "ConfigMaster 文件或目标状态与 Receipt 不一致，请查明变化后重新 Plan。", null));
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(BuildResult(receipt, "blocked", "无法读取 Receipt 目标资产，未执行写入。", null)); }
        }

        /// <summary>验证互斥的完整坐标请求；平台只接受当前 Active BuildTarget 映射，绝不隐式切平台。</summary>
        /// <param name="request">唯一 Master、coordinate-switch 及完整 coordinate；patches 必须省略或 null。</param>
        /// <param name="coordinate">成功时返回明确目标；失败时仅返回默认值。</param>
        /// <param name="error">不含配置值的失败原因。</param>
        /// <returns>输入及活动平台全部匹配时返回 true。</returns>
        private static bool TryBuildCoordinateSwitch(Request request, out Coord coordinate, out string error)
        {
            coordinate = default;
            error = null;
            if (request == null || request.operation != c_CoordinateSwitch || !IsGuid(request.masterGuid) ||
                request.patches != null || request.coordinate == null ||
                !TryParseExact(request.coordinate.platform, out PlatformType platform) || platform == PlatformType.None ||
                !TryParseExact(request.coordinate.channel, out ChannelType channel) || channel == ChannelType.None ||
                !TryParseExact(request.coordinate.developMode, out DevelopMode mode))
            {
                error = "coordinate-switch 须提供有效 masterGuid 和完整精确 coordinate，不能混用 patches。";
                return false;
            }
            if (platform != EditorUtil.Config.ActivePlatform.Current)
            {
                error = "坐标平台必须等于 Unity 当前 Active BuildTarget 的 Nova 平台；请先明确切换构建目标，不能由本 Action 隐式切平台。";
                return false;
            }
            coordinate = new Coord(platform, channel, mode);
            return true;
        }

        /// <summary>在 clone 上冻结当前渠道/模式选择、完整资产和精确两叶子写集；不预演字段广播。</summary>
        /// <param name="request">已经明确授权的独立坐标切换请求。</param>
        /// <param name="context">本次取消令牌及执行上下文；不触发 Unity BuildTarget 切换。</param>
        /// <returns>只读 ready 计划及跨重载 Verify 载荷，无法精确保存则 blocked。</returns>
        private static Task<AgentActionHandlerPlan> PlanCoordinateSwitch(Request request, AgentActionExecutionContext context)
        {
            if (!TryBuildCoordinateSwitch(request, out Coord coordinate, out string error) ||
                !TryLoadCleanMaster(request.masterGuid, out ConfigMasterSO master, out string path, out error))
                return Task.FromResult(BlockedPlan(error));
            try
            {
                string target = EditorUserBuildSettings.activeBuildTarget.ToString();
                string assetHash = GenerateActionCommon.ComputeFileHash(Absolute(path), context.CancellationToken);
                using (Patch.Preview preview = Patch.BuildCoordinateSwitchPreview(master, coordinate))
                {
                    var receipt = new Receipt
                    {
                        masterGuid = request.masterGuid, assetPath = path, plannedAssetSha256 = assetHash,
                        beforeStateSha256 = preview.BeforeStateHash, expectedStateSha256 = preview.ExpectedStateHash,
                        physicalChanges = Array.Empty<Patch.PhysicalChange>(), operation = c_CoordinateSwitch,
                        coordinate = new CoordinateSelection
                        {
                            platform = coordinate.Platform.ToString(), channel = coordinate.Channel.ToString(),
                            developMode = coordinate.Mode.ToString(),
                        },
                        activeBuildTarget = target, coordinateChanges = preview.CoordinateChanges,
                    };
                    if (!MatchesFrozenBuildTarget(receipt) || EditorUtility.IsDirty(master) || Editing.HasUnsavedDraft(master) ||
                        Patch.StateHash(master) != receipt.beforeStateSha256 ||
                        assetHash != GenerateActionCommon.ComputeFileHash(Absolute(path), context.CancellationToken))
                        return Task.FromResult(BlockedPlan("坐标预演期间资产、草稿或活动构建目标发生变化，请重新 Plan。"));
                    return Task.FromResult(new AgentActionHandlerPlan
                    {
                        Status = "ready",
                        Summary = preview.CoordinateChanges.Length == 0 ? "当前渠道与开发模式已满足，无需保存。" :
                            "将保存明确渠道与开发模式；只写 ConfigMaster，不切 Unity 构建平台、不导出运行配置。",
                        DataJson = Util.Json.Serialize(new
                        {
                            receipt.operation, receipt.masterGuid, receipt.assetPath, receipt.plannedAssetSha256,
                            receipt.beforeStateSha256, receipt.expectedStateSha256, receipt.activeBuildTarget, receipt.coordinate,
                            changedPhysicalFieldCount = preview.CoordinateChanges.Length, physicalChanges = preview.CoordinateChanges,
                        }),
                        State = new CoordinateState { Receipt = receipt, Coordinate = coordinate },
                        RecoveryPayloadJson = Util.Json.Serialize(receipt),
                        WriteSet = preview.CoordinateChanges.Length == 0 ? Array.Empty<string>() : new[] { path },
                        Evidence = new[]
                        {
                            "只读冻结完整资产及序列化状态、活动 BuildTarget 和 CurrentChannel/CurrentDevelopMode 的精确叶子写集。",
                            "坐标外配置值、矩阵、掩码和导出目标保持；归一需要改其他字段时必须先独立归一保存，再重新 Plan。",
                        },
                    });
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(BlockedPlan("无法精确预演坐标切换；请核对目标，并先独立归一保存非规范 Master。")); }
        }

        /// <summary>单次消费坐标计划，复核文件/状态/草稿/BuildTarget后复用单 Master 保存；写后失败保 partial。</summary>
        /// <param name="frozen">Plan 冻结的完整目标及精确写集，只能消费一次。</param>
        /// <param name="context">本次取消令牌；提交前可取消，正式写后不能重放。</param>
        /// <returns>成功或无变化结果；提交前失败 blocked，正式写入后不完整返回 partial 与 Receipt。</returns>
        private static Task<AgentActionResult> ExecuteCoordinateSwitch(CoordinateState frozen, AgentActionExecutionContext context)
        {
            if (frozen.Receipt == null || frozen.Receipt.operation != c_CoordinateSwitch ||
                Interlocked.Exchange(ref frozen.Consumed, 1) != 0)
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "坐标切换计划无效或已使用，请重新 Plan。"));
            Receipt receipt = frozen.Receipt;
            if (!TryLoadCleanMaster(receipt.masterGuid, out ConfigMasterSO master, out string path, out string error) ||
                path != receipt.assetPath || !MatchesFrozenBuildTarget(receipt))
                return Task.FromResult(AgentActionResult.Create(null, "blocked", error ?? "目标资产或活动构建平台已漂移，请重新 Plan。"));
            bool writeStarted = false;
            try
            {
                if (GenerateActionCommon.ComputeFileHash(Absolute(path), context.CancellationToken) != receipt.plannedAssetSha256 ||
                    Patch.StateHash(master) != receipt.beforeStateSha256)
                    return Task.FromResult(AgentActionResult.Create(null, "blocked", "ConfigMaster 文件或完整状态已漂移，请重新 Plan。"));
                using (Patch.Preview preview = Patch.BuildCoordinateSwitchPreview(master, frozen.Coordinate))
                {
                    if (preview.BeforeStateHash != receipt.beforeStateSha256 || preview.ExpectedStateHash != receipt.expectedStateSha256 ||
                        !preview.CoordinateChanges.SequenceEqual(receipt.coordinateChanges, StringComparer.Ordinal))
                        return Task.FromResult(AgentActionResult.Create(null, "blocked", "坐标切换的精确写集或目标状态已漂移，请重新 Plan。"));
                    context.CancellationToken.ThrowIfCancellationRequested();
                    receipt.executionStarted = true;
                    if (preview.CoordinateChanges.Length > 0)
                    {
                        using (var so = new SerializedObject(preview.WorkingCopy))
                            Editing.SaveWorkingCopy(master, preview.WorkingCopy, so, frozen.Coordinate,
                                new Editing.SaveOptions(), workingCopy =>
                                {
                                    context.CancellationToken.ThrowIfCancellationRequested();
                                    if (!MatchesFrozenBuildTarget(receipt) || EditorUtility.IsDirty(master) || Editing.HasUnsavedDraft(master) ||
                                        GenerateActionCommon.ComputeFileHash(Absolute(path), context.CancellationToken) != receipt.plannedAssetSha256 ||
                                        Patch.StateHash(master) != receipt.beforeStateSha256 ||
                                        Patch.StateHash(workingCopy) != receipt.expectedStateSha256)
                                        throw new InvalidOperationException("提交前坐标、文件、草稿或活动构建平台发生变化。");
                                });
                        writeStarted = true;
                    }
                    receipt.observedAssetSha256 = GenerateActionCommon.ComputeFileHash(Absolute(path), CancellationToken.None);
                    bool matches = MatchesFrozenBuildTarget(receipt) && !EditorUtility.IsDirty(master) &&
                                   Patch.StateHash(master) == receipt.expectedStateSha256 &&
                                   (preview.CoordinateChanges.Length == 0 || receipt.observedAssetSha256 != receipt.plannedAssetSha256);
                    AgentActionResult result = BuildResult(receipt, matches ? "success" : "partial", matches ?
                        (preview.CoordinateChanges.Length == 0 ? "渠道与开发模式已满足，未保存资产。" :
                            "已保存渠道与开发模式；运行配置需另行导出。") : "坐标保存已执行，完整状态尚未通过复核，请使用 Verify。", null);
                    if (matches && preview.CoordinateChanges.Length > 0) result.Warnings.AddRange(Editing.NotifyMasterSaved(master));
                    return Task.FromResult(result);
                }
            }
            catch (Editing.DesignTimeSaveException exception)
            {
                writeStarted |= exception.WriteStarted;
                return Task.FromResult(FailureResult(receipt, writeStarted));
            }
            catch (OperationCanceledException) when (!writeStarted) { throw; }
            catch { return Task.FromResult(FailureResult(receipt, writeStarted)); }
        }

        /// <summary>只读核对冻结的 Unity 构建目标与 Nova 平台映射；不执行平台切换或写配置。</summary>
        /// <param name="receipt">当前坐标分支的冻结 BuildTarget 与明确平台。</param>
        /// <returns>构建目标和平台均与 Plan 一致时返回 true。</returns>
        private static bool MatchesFrozenBuildTarget(Receipt receipt)
        {
            return receipt.activeBuildTarget == EditorUserBuildSettings.activeBuildTarget.ToString() &&
                   receipt.coordinate?.platform == EditorUtil.Config.ActivePlatform.Current.ToString();
        }

        /// <summary>将明确 DTO 转为固定字段 Edit，不接受任意字段路径或代码；每个坐标项必须包含至少一个修改。</summary>
        private static bool TryBuildEdits(Request request, out Patch.Edit[] edits, out string error)
        {
            edits = null;
            error = null;
            if (request != null && (request.operation != null || request.coordinate != null))
            {
                error = "固定字段请求须省略 operation/coordinate；坐标切换使用独立 coordinate-switch 分支。";
                return false;
            }
            if (request == null || !IsGuid(request.masterGuid) || request.patches == null || request.patches.Length == 0 || request.patches.Length > 64)
            {
                error = "masterGuid 必须是 32 位十六进制 Unity GUID，patches 必须包含 1 至 64 个坐标项。";
                return false;
            }
            var result = new List<Patch.Edit>();
            foreach (CoordinatePatch patch in request.patches)
            {
                if (patch == null || !TryParseExact(patch.platform, out PlatformType platform) || platform == PlatformType.None ||
                    !TryParseExact(patch.channel, out ChannelType channel) || channel == ChannelType.None ||
                    !TryParseExact(patch.developMode, out DevelopMode mode))
                {
                    error = "每个坐标项必须提供大小写精确、非 None 的 platform/channel 和有效 developMode。";
                    return false;
                }
                Coord coordinate = new Coord(platform, channel, mode);
                int start = result.Count;
                AddEdit(result, coordinate, Patch.Field.Namespace, patch.Namespace);
                if (patch.AppConfigs != null)
                {
                    AddEdit(result, coordinate, Patch.Field.AppID, patch.AppConfigs.AppID);
                    AddEdit(result, coordinate, Patch.Field.AppAesKey, patch.AppConfigs.AppAesKey);
                    AddEdit(result, coordinate, Patch.Field.AppAesIV, patch.AppConfigs.AppAesIV);
                    AddEdit(result, coordinate, Patch.Field.CustomConfigCmdName, patch.AppConfigs.CustomConfigCmdName);
                    AddEdit(result, coordinate, Patch.Field.CustomName, patch.AppConfigs.CustomName);
                }
                if (start == result.Count)
                {
                    error = "每个坐标项必须明确提供至少一个待修改字段。";
                    return false;
                }
            }
            foreach (Patch.Edit edit in result)
                if (!Patch.TryValidateEdit(edit, out error)) return false;
            edits = result.ToArray();
            return true;
        }

        /// <summary>包装缺失表示保留；包装存在则冻结字符串值，由领域校验拒绝 null。</summary>
        private static void AddEdit(List<Patch.Edit> edits, Coord coordinate, Patch.Field field, StringValue value)
        {
            if (value != null) edits.Add(new Patch.Edit(coordinate, field, value.value));
        }

        /// <summary>只接受当前枚举精确名称，拒绝数字字符串、None 由调用者进一步限定。</summary>
        private static bool TryParseExact<T>(string value, out T parsed) where T : struct
        {
            return Enum.TryParse(value, false, out parsed) && Enum.IsDefined(typeof(T), parsed) && parsed.ToString() == value;
        }

        /// <summary>只读定位 Assets 内真实可写配置及文件边界，并检查资产脏态和注册的窗口草稿。</summary>
        private static bool TryLoadCleanMaster(string guid, out ConfigMasterSO master, out string path, out string error)
        {
            master = null;
            path = null;
            error = null;
            try
            {
                if (!IsGuid(guid)) { error = "ConfigMaster GUID 无效。"; return false; }
                path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !GenerateActionCommon.TryResolveProjectPath(path, "ConfigMaster", out string absolute, out _) || !File.Exists(absolute))
                {
                    error = "ConfigMaster 必须是 Assets 内存在、无符号链接的项目资产。";
                    return false;
                }
                master = AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(path);
                if (master == null || !EditorUtility.IsPersistent(master) ||
                    !string.Equals(AssetDatabase.AssetPathToGUID(path), guid, StringComparison.OrdinalIgnoreCase) ||
                    (File.GetAttributes(absolute) & FileAttributes.ReadOnly) != 0)
                {
                    error = "未找到可写且 GUID 一致的 ConfigMaster 资产。";
                    return false;
                }
                if (EditorUtility.IsDirty(master) || Editing.HasUnsavedDraft(master))
                {
                    error = "ConfigMaster 或 ConfigWindow 存在未保存草稿，请先保存或放弃草稿。";
                    return false;
                }
                return true;
            }
            catch
            {
                master = null;
                error = "无法安全读取目标 ConfigMaster 资产及草稿状态。";
                return false;
            }
        }

        /// <summary>校验旧固定字段或互斥坐标选择载荷的版本、整体摘要及精确地址；只供验证，不解释为执行请求。</summary>
        private static bool ValidReceipt(Receipt receipt)
        {
            if (receipt == null || receipt.schemaVersion != 1 || !IsGuid(receipt.masterGuid) ||
                string.IsNullOrEmpty(receipt.assetPath) || !receipt.assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !IsHash(receipt.plannedAssetSha256) || !IsHash(receipt.beforeStateSha256) || !IsHash(receipt.expectedStateSha256) ||
                receipt.physicalChanges == null || receipt.physicalChanges.Length > 100000) return false;
            if (receipt.operation != null)
            {
                if (receipt.operation != c_CoordinateSwitch || receipt.physicalChanges.Length != 0 ||
                    receipt.coordinate == null || receipt.coordinateChanges == null || receipt.coordinateChanges.Length > 2 ||
                    !TryParseExact(receipt.coordinate.platform, out PlatformType platform) || platform == PlatformType.None ||
                    !TryParseExact(receipt.coordinate.channel, out ChannelType channel) || channel == ChannelType.None ||
                    !TryParseExact(receipt.coordinate.developMode, out DevelopMode _) ||
                    !TryParseExact(receipt.activeBuildTarget, out BuildTarget target) ||
                    EditorUtil.Config.ActivePlatform.FromBuildTarget(target) != platform) return false;
                return receipt.coordinateChanges.Distinct(StringComparer.Ordinal).Count() == receipt.coordinateChanges.Length &&
                       receipt.coordinateChanges.All(field => field == nameof(ConfigMasterSO.CurrentChannel) ||
                                                             field == nameof(ConfigMasterSO.CurrentDevelopMode));
            }
            if (receipt.coordinate != null || receipt.activeBuildTarget != null || receipt.coordinateChanges != null) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Patch.PhysicalChange change in receipt.physicalChanges)
            {
                if (change == null || !TryParseExact(change.platform, out PlatformType p) || p == PlatformType.None ||
                    !TryParseExact(change.channel, out ChannelType c) || c == ChannelType.None ||
                    !TryParseExact(change.developMode, out DevelopMode _) || !TryParseExact(change.field, out Patch.Field _)) return false;
                if (!seen.Add(change.platform + "|" + change.channel + "|" + change.developMode + "|" + change.field)) return false;
            }
            return true;
        }

        /// <summary>GUID 仅接受恰好 32 位十六进制；资产匹配时忽略十六进制字母大小写。</summary>
        private static bool IsGuid(string value) => value != null && value.Length == 32 && value.All(Uri.IsHexDigit);

        /// <summary>完整 SHA-256 文本必须是 64 位十六进制。</summary>
        private static bool IsHash(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);

        /// <summary>组合已经由项目路径门检查的目标资产路径，不接受外部路径输入。</summary>
        private static string Absolute(string path) => Path.Combine(GenerateActionCommon.ProjectRoot, path);

        /// <summary>生成无执行态、无写集的阻塞计划。</summary>
        private static AgentActionHandlerPlan BlockedPlan(string reason) => new AgentActionHandlerPlan { Status = "blocked", Summary = reason };

        /// <summary>结果只包含总体摘要、精确写集和无值校验路径；不序列化内部 Edit 或请求 DTO。</summary>
        private static AgentActionResult BuildResult(Receipt receipt, string status, string message, Patch.RemainingIssue[] issues)
        {
            AgentActionResult result = AgentActionResult.Create(null, status, message);
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.DataJson = receipt.operation == c_CoordinateSwitch ? Util.Json.Serialize(new
            {
                receipt.operation, receipt.assetPath, receipt.observedAssetSha256, receipt.expectedStateSha256,
                receipt.activeBuildTarget, receipt.coordinate,
                changedPhysicalFieldCount = receipt.coordinateChanges.Length,
                physicalChanges = receipt.coordinateChanges,
            }) : Util.Json.Serialize(new
            {
                receipt.assetPath, receipt.observedAssetSha256, receipt.expectedStateSha256,
                changedPhysicalFieldCount = receipt.physicalChanges.Length,
                physicalChanges = receipt.physicalChanges, remainingIssues = issues,
            });
            result.EvidenceKinds = AgentActionEvidence.Static;
            result.Evidence.Add("仅配置资产与完整状态摘要证据；未导出 ConfigRuntime、未运行游戏、未验证服务器或真机。");
            return result;
        }

        /// <summary>正式写入开始后保留只读恢复载荷并返回 partial；不尝试回滚或重放保存。</summary>
        private static AgentActionResult FailureResult(Receipt receipt, bool writeStarted)
        {
            if (!writeStarted) return AgentActionResult.Create(null, "blocked", "配置提交前检查失败，未修改实际 ConfigMaster，请重新 Plan。");
            receipt.executionStarted = true;
            try { receipt.observedAssetSha256 = GenerateActionCommon.ComputeFileHash(Absolute(receipt.assetPath), CancellationToken.None); }
            catch { receipt.observedAssetSha256 = null; }
            return BuildResult(receipt, "partial", "配置写入已开始但保存或复核未完成；请保留 Receipt 并使用 Verify 检查，不要重放 Execute。", null);
        }
    }
}
