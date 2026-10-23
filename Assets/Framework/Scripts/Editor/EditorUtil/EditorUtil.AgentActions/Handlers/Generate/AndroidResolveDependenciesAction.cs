/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveDependenciesAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   EDM4U Android 依赖图解析与生成结果验证 Action
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.android.resolve-dependencies",
        "解析 Android 原生依赖",
        "android",
        AgentActionOperationType.Generate,
        Description = "冻结 EDM4U Android 依赖输入后执行受控 Resolve，并核验生成的原生依赖结果。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite |
                  AgentActionEffect.ExternalRead | AgentActionEffect.ExternalWrite |
                  AgentActionEffect.Destructive,
        ContractMajor = 2,
        RequiredEvidence = AgentActionEvidence.PackageResolution | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        Locks = new[]
        {
            "unity-editor", "asset-database", "android-dependency-graph",
            "android-gradle-templates", "generated-local-repo",
        })]
    internal sealed class AndroidResolveDependenciesAction :
        AgentActionHandler<AndroidResolveDependenciesAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string activeBuildTarget;
        }

        private sealed class State
        {
            public AndroidResolveActionCommon.DependencyEntry[] Graph;
            public string GraphSha256;
            public AndroidResolveActionCommon.TemplateResolutionPlan TemplatePlan;
            public AndroidResolveRefreshContract.Snapshot Refresh;
        }

        [Serializable]
        private sealed class Receipt
        {
            public string activeBuildTarget;
            public AndroidResolveActionCommon.DependencyEntry[] expectedGraph;
            public string expectedGraphSha256;
            public bool resolveSucceeded;
            public AndroidResolveActionCommon.ResolutionState resolvedState;
            public AndroidResolveActionCommon.TemplateResolutionPlan templatePlan;
            public AndroidResolveActionCommon.CurrentResolutionInputs afterInputs;
            public string[] failureKinds;
            public AndroidResolveRefreshContract.Snapshot refreshContract;
            public AndroidResolveRefreshContract.Receipt refreshReceipt;
            public GenerateActionCommon.Artifact[] artifacts;
        }

        /// <summary>
        /// 只接受显式 Android Target；Action 不负责切换 BuildTarget。
        /// </summary>
        /// <param name="request">显式 Android 请求；不负责切换 BuildTarget。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (request?.activeBuildTarget != BuildTarget.Android.ToString())
            {
                error = "activeBuildTarget 必须精确为 Android；Action 不切换 BuildTarget。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 核对当前 Target、实际刷新回调与调度后冻结正规解析和非目标保护；未知支路不预读或执行解析。
        /// </summary>
        /// <param name="request">显式 Android 请求；不负责切换 BuildTarget。</param>
        /// <param name="context">Core 提供的执行上下文、取消令牌与证据目标。</param>
        /// <returns>完整自然合同满足时的 ready 计划，或包含具体原因的不可执行结果。</returns>
        public override Task<AgentActionHandlerPlan> PlanAsync(
            Request request,
            AgentActionExecutionContext context)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "blocked",
                    Summary = $"当前 activeBuildTarget={EditorUserBuildSettings.activeBuildTarget}，Action 不自动切换到 Android。",
                });
            }
            if (!HasCompleteTemplateRefreshContract(context.CancellationToken, out string refreshError))
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "blocked",
                    Summary = refreshError,
                });
            }
            if (!AndroidResolveActionCommon.TryCaptureTemplateResolutionPlan(context.CancellationToken,
                    out AndroidResolveActionCommon.TemplateResolutionPlan templatePlan, out string graphError))
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "blocked",
                    Summary = graphError,
                });
            }
            AndroidResolveActionCommon.DependencyEntry[] graph = templatePlan.current.graph;
            if (graph.Length == 0)
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "not_applicable",
                    Summary = "EDM4U 当前公开依赖图为空；为避免把尚未初始化误判为无依赖，Action 不执行清理型 Resolve。",
                });
            }

            if (!AndroidResolveRefreshContract.TryCapturePlan(templatePlan, context.CancellationToken,
                    out AndroidResolveRefreshContract.Snapshot refresh, out string refreshPlanError))
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = refreshPlanError });

            string graphSha256 = AndroidResolveActionCommon.ComputeGraphSha256(graph);
            var receipt = new Receipt
            {
                activeBuildTarget = BuildTarget.Android.ToString(),
                expectedGraph = graph,
                expectedGraphSha256 = graphSha256,
                resolveSucceeded = false,
                resolvedState = null,
                templatePlan = templatePlan,
                refreshContract = refresh,
                artifacts = Array.Empty<GenerateActionCommon.Artifact>(),
            };
            string[] writeSet = templatePlan.writeSet.Concat(AndroidResolveRefreshContract.GetDeclaredWriteSet(refresh)).ToArray();
            string completeReceipt = Util.Json.Serialize(receipt);
            if (!AndroidResolveReceiptCodec.TryRequirePublicDataBudget(completeReceipt, out string encodingError) ||
                !AndroidResolveReceiptCodec.TryEncode(completeReceipt, out string encodedReceipt, out encodingError) ||
                !AndroidResolveReceiptCodec.TryRequireOperationBudget(encodedReceipt, writeSet, out encodingError))
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = encodingError });
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将通过 EDM4U Force Resolve 重建 {graph.Length} 项 Android 依赖的受管输出。",
                DataJson = completeReceipt,
                State = new State { Graph = graph, GraphSha256 = graphSha256, TemplatePlan = templatePlan, Refresh = refresh },
                WriteSet = writeSet,
                Evidence = new[]
                {
                    $"已冻结 EDM4U 当前公开依赖图，共 {graph.Length} 项，SHA-256={graphSha256}。",
                    "当前仅接纳已有模板与本地 SRCAAR/POM 支路；Java 版本为只读检查，下载、SDK 安装、Gradle 执行和未知回调仍具体阻断。已声明 Search 精确 Library 派生缓存/事务和固定内存/UI 缓存维护。",
                    "Force Resolve 与场景回调的 ResolveSync(false, true) 是不同调用；成功 Receipt 不授权重放或把后续回调的状态比较替代为强制解析。",
                    "若 ResolveSync 引发 domain reload，只能使用 recovery token 进入 Verify，不会恢复或重放 Execute。",
                },
                RecoveryPayloadJson = encodedReceipt,
            });
        }

        /// <summary>
        /// 复核依赖图未漂移后调用一次既有 AndroidResolver.Resolve，并冻结 EDM4U 输出证据。
        /// </summary>
        /// <param name="state">本次冻结的解析状态。</param>
        /// <param name="context">Core 提供的执行上下文、取消令牌与证据目标。</param>
        /// <returns>只执行一次正规 Resolve 后的结构化结果；所有写后失败保留 partial。</returns>
        public override async Task<AgentActionResult> ExecuteAsync(
            object state,
            AgentActionExecutionContext context)
        {
            if (!(state is State frozen))
            {
                return AgentActionResult.Create(null, "blocked", "Android 依赖解析冻结状态无效。");
            }
            if (!HasCompleteTemplateRefreshContract(context.CancellationToken, out string refreshError))
            {
                return AgentActionResult.Create(null, "blocked", refreshError);
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                return AgentActionResult.Create(null, "blocked", "activeBuildTarget 已离开 Android，请重新 Plan。");
            }
            if (!AndroidResolveActionCommon.TryCaptureDependencyGraph(
                    out AndroidResolveActionCommon.DependencyEntry[] currentGraph,
                    out string graphError) ||
                !string.Equals(
                    frozen.GraphSha256,
                    AndroidResolveActionCommon.ComputeGraphSha256(currentGraph),
                    StringComparison.OrdinalIgnoreCase))
            {
                return AgentActionResult.Create(
                    null,
                    "blocked",
                    "EDM4U 依赖图已漂移，请重新 Plan：" + graphError);
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            if (!AndroidResolveActionCommon.TryCaptureTemplateResolutionPlan(context.CancellationToken,
                    out AndroidResolveActionCommon.TemplateResolutionPlan currentPlan, out string planError) ||
                frozen.TemplatePlan?.sha256 != currentPlan.sha256)
            {
                return AgentActionResult.Create(null, "blocked",
                    "正规解析设置、仓库、删除集合、模板或工具链漂移，请重新 Plan：" + planError);
            }
            if (!AndroidResolveRefreshContract.TryVerifySnapshot(frozen.Refresh, context.CancellationToken, out string refreshStateError))
                return AgentActionResult.Create(null, "blocked", refreshStateError);
            string[] failures = Array.Empty<string>();
            AndroidResolveRefreshContract.Receipt refreshReceipt = null;
            try
            {
                string invocationFailure = null;
                using (var logs = new AndroidResolveActionCommon.ResolutionLogCapture())
                using (var refreshScope = AndroidResolveRefreshContract.Begin(frozen.Refresh))
                {
                    try { EditorUtil.AndroidResolver.Resolve(); }
                    catch (Exception exception) { invocationFailure = "resolve-exception-" + exception.GetType().Name; }
                    refreshReceipt = await refreshScope.CompleteAsync(context.CancellationToken);
                    failures = invocationFailure == null ? logs.FailureKinds : logs.FailureKinds.Concat(new[] { invocationFailure }).ToArray();
                }
                if (!refreshReceipt.completed)
                    return CreateRefreshPartial(frozen, refreshReceipt, failures,
                        "正规 Resolve 已返回，但刷新/非目标保护验收不完整；不会重放：" + refreshReceipt.failure);
                if (context.CancellationToken.IsCancellationRequested)
                    return CreateRefreshPartial(frozen, refreshReceipt, failures, "正规解析已调用，取消后仅保留 partial，不重放。");

                if (failures.Length > 0)
                {
                    return CreateRefreshPartial(frozen, refreshReceipt, failures,
                        "正规 Resolve 已返回，但本次日志包含解析失败；不会自动重放：" + string.Join(", ", failures));
                }
                if (!AndroidResolveActionCommon.TryCaptureCurrentResolutionInputs(context.CancellationToken,
                        out AndroidResolveActionCommon.CurrentResolutionInputs afterInputs, out string afterError) ||
                    !AndroidResolveActionCommon.TryValidateTemplateResolutionResult(frozen.TemplatePlan, afterInputs,
                        context.CancellationToken, out afterError))
                {
                    return CreateRefreshPartial(frozen, refreshReceipt, failures,
                        "正规 Resolve 已返回，但模板/POM/原生库验收不完整；不会自动重放：" + afterError);
                }

                string packageError = null;
                if (!AndroidResolveActionCommon.TryLoadResolutionState(
                        out AndroidResolveActionCommon.ResolutionState resolvedState,
                        out string stateError) ||
                    !AndroidResolveActionCommon.PackagesEqual(frozen.Graph, resolvedState.packages, out packageError))
                {
                    return CreateRefreshPartial(frozen, refreshReceipt, failures,
                        "EDM4U 已返回，但解析状态无法精确对应冻结依赖图；不会自动重放：" + (stateError ?? packageError));
                }
                if (!AndroidResolveActionCommon.TryCaptureArtifacts(
                        resolvedState,
                        context,
                        out GenerateActionCommon.Artifact[] artifacts,
                        out string artifactError))
                {
                    return CreateRefreshPartial(frozen, refreshReceipt, failures,
                        "EDM4U 已返回，但受管输出证据不完整；不会自动重放：" + artifactError);
                }

                var receipt = new Receipt
                {
                    activeBuildTarget = BuildTarget.Android.ToString(),
                    expectedGraph = frozen.Graph,
                    expectedGraphSha256 = frozen.GraphSha256,
                    resolveSucceeded = true,
                    resolvedState = resolvedState,
                    templatePlan = frozen.TemplatePlan,
                    refreshContract = frozen.Refresh,
                    refreshReceipt = refreshReceipt,
                    afterInputs = afterInputs,
                    failureKinds = failures,
                    artifacts = artifacts,
                };
                AgentActionResult result = AgentActionResult.Create(
                    null,
                    "partial",
                    "EDM4U ResolveSync 已明确成功，等待只读 Verify 核对依赖图、受管文件集合与 SHA-256。");
                string completeReceipt = Util.Json.Serialize(receipt);
                result.ReceiptJson = AndroidResolveReceiptCodec.Encode(completeReceipt);
                AssignPublicData(result, completeReceipt);
                result.Artifacts.AddRange(artifacts.Select(artifact => artifact.path));
                result.Evidence.Add("Execute 只调用现有 EditorUtil.AndroidResolver.Resolve 一次；失败或 domain reload 后不会自动重放。" );
                return result;
            }
            catch (OperationCanceledException)
            { return CreateRefreshPartial(frozen, refreshReceipt, failures, "正规解析已进入一次性执行；取消后保留 partial，不重放。"); }
            catch (Exception exception)
            { return CreateRefreshPartial(frozen, refreshReceipt, failures, "正规解析已进入一次性执行；后验读取失败，保留 partial，不重放：" + exception.GetType().Name); }
        }

        /// <summary>
        /// 正规模板解析先检查实际完整回调集合与调度，只有已审自然支路才继续读取依赖输入。
        /// 未知类型、订阅、模块身份或队列给出具体阻断原因，不改变当前环境来制造安全分支。
        /// </summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool HasCompleteTemplateRefreshContract(CancellationToken token, out string error)
        {
            return AndroidResolveRefreshContract.TryCapturePreflight(token, out _, out _, out error);
        }

        /// <summary>
        /// 正规 Resolve 已返回但验收不足时尝试附完整结构化证据；编码失败仅沿用写前恢复材料并说明缺失，不二次抛出或重放。
        /// </summary>
        /// <param name="state">本次冻结的解析状态。</param>
        /// <param name="refresh">本次正规执行的完整或未完成刷新回执。</param>
        /// <param name="failures">本次窗口观察到的固定失败类别，不包含原日志。</param>
        /// <param name="message">对本次执行结果的说明，不作为后续执行指令。</param>
        /// <returns>附有可编码观察证据或明确编码失败原因且不会重放的 partial 结果。</returns>
        private static AgentActionResult CreateRefreshPartial(State state, AndroidResolveRefreshContract.Receipt refresh,
            string[] failures, string message)
        {
            var receipt = new Receipt { activeBuildTarget = BuildTarget.Android.ToString(), expectedGraph = state.Graph,
                expectedGraphSha256 = state.GraphSha256, resolveSucceeded = true, templatePlan = state.TemplatePlan,
                refreshContract = state.Refresh, refreshReceipt = refresh, failureKinds = failures,
                artifacts = Array.Empty<GenerateActionCommon.Artifact>() };
            AgentActionResult result = AgentActionResult.Create(null, "partial", message);
            string completeReceipt = Util.Json.Serialize(receipt);
            if (AndroidResolveReceiptCodec.TryEncode(completeReceipt, out string encodedReceipt, out string encodingError))
                result.ReceiptJson = encodedReceipt;
            else
            {
                string warning = encodingError + " 未附完整写后回执；仅保留此前已持久化的完整写前 Recovery，写后完整回执未持久化，不会重放。";
                result.Message += " " + warning;
                result.Warnings.Add(warning);
            }
            AssignPublicData(result, completeReceipt);
            return result;
        }

        /// <summary>容量内保持原公开字段；超限准确区分已附回执与缺失回执，不把尚未完成的 Core 持久化冒称成功。</summary>
        /// <param name="result">原本已经是partial的执行或失败结果，不在此提升成功。</param>
        /// <param name="dataJson">完整原展示JSON，编码或回执验证逻辑不依赖该展示字段。</param>
        private static void AssignPublicData(AgentActionResult result, string dataJson)
        {
            if (AndroidResolveReceiptCodec.TryRequirePublicDataBudget(dataJson, out string error)) result.DataJson = dataJson;
            else result.Warnings.Add(error + (string.IsNullOrEmpty(result.ReceiptJson)
                ? " 公开展示未返回，未附完整写后回执；恢复凭据仅保留此前已持久化材料，不表示本次写后材料已保存。"
                : " 公开展示未返回；完整编码回执已附本结果，是否持久化以 Core 实际 Operation 结果为准，未截断或改变原成功标记。"));
        }

        /// <summary>
        /// 只读核对明确成功标记、当前依赖图、EDM4U 状态文件、受管文件集合和全部产物摘要。
        /// </summary>
        /// <param name="receiptJson">需要只读验收的序列化回执；不授权重放执行。</param>
        /// <param name="context">Core 提供的执行上下文、取消令牌与证据目标。</param>
        /// <returns>只读验收结果；缺证据或漂移保持 partial/blocked。</returns>
        public override Task<AgentActionResult> VerifyAsync(
            string receiptJson,
            AgentActionExecutionContext context)
        {
            Receipt receipt;
            try
            {
                if (!AndroidResolveReceiptCodec.TryDecode(receiptJson, out string decodedReceipt, out string decodingError))
                    return Task.FromResult(AgentActionResult.Create(null, "blocked", decodingError));
                receipt = Util.Json.Deserialize<Receipt>(decodedReceipt);
            }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Android Resolve Receipt 无法解析：" + exception.Message));
            }
            if (receipt == null || receipt.activeBuildTarget != BuildTarget.Android.ToString() ||
                receipt.expectedGraph == null || string.IsNullOrWhiteSpace(receipt.expectedGraphSha256) ||
                receipt.artifacts == null || receipt.templatePlan == null)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Android Resolve Receipt 不完整。"));
            }
            if (!string.Equals(
                    receipt.expectedGraphSha256,
                    AndroidResolveActionCommon.ComputeGraphSha256(receipt.expectedGraph),
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Receipt 内冻结依赖图与 SHA-256 不一致。"));
            }
            if (!receipt.resolveSucceeded)
            {
                return Task.FromResult(AgentActionResult.Create(
                    null,
                    "partial",
                    "Recovery Receipt 未包含 ResolveSync 明确成功标记；不会仅凭目录存在推断执行成功，也不会重放。"));
            }
            // 历史成功标记不证明刷新窗口完成；旧回执和跨域回执均不能提升为完整验收。
            if (receipt.refreshContract == null || receipt.refreshReceipt == null)
                return Task.FromResult(AgentActionResult.Create(null, "partial", "现有成功回执缺完整刷新与非目标保护验收；不会重放。"));
            if (!HasCompleteTemplateRefreshContract(context.CancellationToken, out string refreshError))
                return Task.FromResult(AgentActionResult.Create(null, "partial",
                    "现有成功回执仍缺完整刷新与非目标保护验收：" + refreshError));
            if (!AndroidResolveRefreshContract.TryVerifyReceipt(receipt.refreshContract, receipt.refreshReceipt,
                    context.CancellationToken, out string refreshReceiptError))
                return Task.FromResult(AgentActionResult.Create(null, "partial", refreshReceiptError));
            string inputError = null;
            if (receipt.afterInputs == null || receipt.failureKinds == null || receipt.failureKinds.Length != 0 ||
                !AndroidResolveActionCommon.TryCaptureCurrentResolutionInputs(context.CancellationToken,
                    out AndroidResolveActionCommon.CurrentResolutionInputs verifiedInputs, out inputError) ||
                receipt.afterInputs.sha256 != verifiedInputs.sha256 ||
                !AndroidResolveActionCommon.TryValidateTemplateResolutionResult(receipt.templatePlan, verifiedInputs,
                    context.CancellationToken, out inputError))
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial",
                    "正规解析回执缺少完整执行后输入，或源/POM/importer 已漂移：" + inputError));
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "当前 activeBuildTarget 已不是 Android，暂不能完成验证。"));
            }
            if (!AndroidResolveActionCommon.TryCaptureDependencyGraph(
                    out AndroidResolveActionCommon.DependencyEntry[] currentGraph,
                    out string graphError) ||
                !string.Equals(
                    receipt.expectedGraphSha256,
                    AndroidResolveActionCommon.ComputeGraphSha256(currentGraph),
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "当前 EDM4U 依赖图已漂移：" + graphError));
            }
            string packageError = null;
            if (!AndroidResolveActionCommon.TryLoadResolutionState(
                    out AndroidResolveActionCommon.ResolutionState currentState,
                    out string stateError) ||
                !AndroidResolveActionCommon.PackagesEqual(receipt.expectedGraph, currentState.packages, out packageError) ||
                !AndroidResolveActionCommon.StatesEqual(receipt.resolvedState, currentState))
            {
                return Task.FromResult(AgentActionResult.Create(
                    null,
                    "partial",
                    "EDM4U 当前状态与 Execute Receipt 不一致：" + (stateError ?? packageError ?? "受管文件或设置已漂移。")));
            }
            if (!GenerateActionCommon.TryVerifyArtifacts(
                    receipt.artifacts,
                    context.CancellationToken,
                    out GenerateActionCommon.Artifact[] actual,
                    out string artifactError))
            {
                AgentActionResult partial = AgentActionResult.Create(null, "partial", "Android 依赖产物验证未通过：" + artifactError);
                AssignPublicData(partial, Util.Json.Serialize(actual));
                return Task.FromResult(partial);
            }
            if (!AndroidResolveActionCommon.ArtifactsCoverState(currentState, actual, out string coverageError))
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", coverageError));
            }

            AgentActionResult success = AgentActionResult.Create(
                null,
                "success",
                "EDM4U 依赖图、解析状态、GeneratedLocalRepo/受管文件集合与 SHA-256 已只读核对。" );
            success.EvidenceKinds = AgentActionEvidence.PackageResolution | AgentActionEvidence.Artifact;
            string publicState = Util.Json.Serialize(currentState);
            if (!AndroidResolveReceiptCodec.TryRequirePublicDataBudget(publicState, out string publicError))
                return Task.FromResult(AgentActionResult.Create(null, "partial", publicError + " 原完整Execute回执仍由恢复凭据保留，未改写或截断。"));
            success.DataJson = publicState;
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path));
            success.Evidence.Add("Verify 未调用 ResolveSync、AssetDatabase.Refresh 或任何恢复写入。" );
            success.Warnings.Add("该证据不代表 Gradle 构建、Player、设备安装或 SDK 运行时初始化成功。" );
            return Task.FromResult(success);
        }
    }

    /// <summary>
    /// Android Resolve Action 的只读 EDM4U 图快照、状态解析与产物覆盖辅助。
    /// </summary>
    internal static class AndroidResolveActionCommon
    {
        internal const string ResolutionStatePath = "ProjectSettings/AndroidResolverDependencies.xml";
        internal const string LocalRepositoryPath = "Assets/GeneratedLocalRepo";
        internal const string AndroidPluginPath = "Assets/Plugins/Android";

        // EDM4U 1.2.188 的 GetResolutionSettings 完整键集合，禁止把部分设置比较当成无需解析证据。
        private static readonly string[] s_ResolutionSettingNames =
        {
            "installAndroidPackages", "packageDir", "explodeAars", "patchAndroidManifest",
            "patchMainTemplateGradle", "useFullCustomMavenRepoPathWhenExport",
            "useFullCustomMavenRepoPathWhenNotExport", "localMavenRepoDir", "useJetifier",
            "bundleId", "gradleBuildEnabled", "gradleTemplateEnabled",
            "gradlePropertiesTemplateEnabled", "projectExportEnabled", "androidAbis",
        };

        internal static readonly string[] WriteSet =
        {
            ResolutionStatePath,
            LocalRepositoryPath + "/**",
            AndroidPluginPath + "/mainTemplate.gradle",
            AndroidPluginPath + "/settingsTemplate.gradle",
            AndroidPluginPath + "/gradleTemplate.properties",
            AndroidPluginPath + "/AndroidManifest.xml（仅 EDM4U 管理的变量替换）",
            AndroidPluginPath + "/**（仅 EDM4U 标签管理的 AAR/JAR/SRCAAR）",
        };

        [Serializable]
        internal sealed class DependencyEntry
        {
            public string spec;
            public string[] repositorySha256;
            public string[] packageIds;
        }

        [Serializable]
        internal sealed class SettingEntry
        {
            public string name;
            public string value;
        }

        [Serializable]
        internal sealed class ResolutionState
        {
            public string[] packages;
            public string[] files;
            public SettingEntry[] settings;
        }

        /// <summary>通过正规公开读链取得的当前解析输入，供 Action 与构建场景回调共用。</summary>
        [Serializable]
        internal sealed class CurrentResolutionInputs
        {
            public SettingEntry[] preferences;
            public bool useProjectSettings;
            public string persistenceSha256;
            public SettingEntry[] resolutionSettings;
            public string[] labeledAssetPaths;
            public SettingEntry[] templateSha256;
            public DependencyEntry[] graph;
            public string graphSha256;
            public string sha256;
            [NonSerialized] internal Type capturedResolverType;
            [NonSerialized] internal object capturedSettingsObject;
        }

        /// <summary>正规本地 Maven 查询的整仓输入，顺序同时影响同目标复制的优先级。</summary>
        [Serializable]
        internal sealed class LocalMavenInputs
        {
            public string[] roots;
            public string[] orderedRepositorySha256;
            public string[] aarCandidates;
            public GenerateActionCommon.Artifact[] rootArtifacts;
            public SettingEntry[] xmlInputs;
            public string sha256;
        }

        /// <summary>正规构建场景的非强制解析输入；只允许完整状态相同的无资源刷新支路。</summary>
        [Serializable]
        internal sealed class SceneResolutionSnapshot
        {
            public CurrentResolutionInputs current;
            public LocalMavenInputs local;
            public ResolutionState resolvedState;
            public GenerateActionCommon.Artifact[] artifacts;
            public SettingEntry[] toolchain;
            public SettingEntry[] scheduling;
            public SettingEntry[] logging;
            public SceneSettingsPersistenceSnapshot settingsPersistence;
            public int scenesProcessed;
            public string sha256;
        }

        /// <summary>
        /// 默认先正规公开 XML 预读，再冻结全量状态、产物、日志与无弹窗工具链；执行后和 Verify 传 false 时只读。
        /// 只接纳非空依赖图的自然状态相同支路，不调用 Resolve 或私有场景入口。
        /// 现存解析任务不能被构建取消，计数只记录真实值，绝不重置来制造首次场景分支。
        /// </summary>
        /// <param name="cancellationToken">Core或上层正规捕获提供的只读取消令牌；不取消厂商/引擎已有任务。</param>
        /// <param name="snapshot">当前精确函数所声明的完整强类型快照；out仅成功时形成完整证据，普通输入不得跨域继承。</param>
        /// <param name="error">out具体拒绝或不完整原因，成功为null；不能据此自动更改环境或重放Resolve。</param>
        /// <param name="readXmlDependencies">默认true使用唯一公开ReadAll作当前图内存预读；执行后/Verify明确false只读，不重新预读或解析。</param>
        /// <returns>bool；默认先正规公开 XML 预读，再冻结全量状态、产物、日志与无弹窗工具链；执行后和 Verify 传 false 时只读。 只接纳非空依赖图的自然状态相同支路，不调用 Resolve 或私有场景入口。 现存解析任务不能被构建取消，计数只记录真实值，绝不重置来制造首次场景分支。</returns>
        internal static bool TryCaptureSceneResolutionInputs(CancellationToken cancellationToken,
            out SceneResolutionSnapshot snapshot, out string error, bool readXmlDependencies = true)
        {
            snapshot = null;
            error = null;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            { error = "完整场景状态比较仅适用于当前 Android；其它平台须按正规 resolver=null 分支核对。"; return false; }
            LocalMavenInputs local;
            if (!(readXmlDependencies
                    ? TryReadCurrentXmlDependencies(cancellationToken, out local, out error)
                    : TryCaptureLocalMavenInputs(cancellationToken, out local, out error)) ||
                !TryCaptureCurrentResolutionInputs(cancellationToken, out CurrentResolutionInputs current, out error) ||
                !TryLoadResolutionState(out ResolutionState state, out error) ||
                !ResolutionInputsMatchState(current.graph, current.labeledAssetPaths, current.resolutionSettings, state, out error) ||
                !TryCaptureArtifacts(state, new AgentActionExecutionContext(null, cancellationToken),
                    out GenerateActionCommon.Artifact[] artifacts, out error) ||
                !TryCaptureNoPromptToolchain(current, cancellationToken, out SettingEntry[] toolchain, out error)) return false;
            if (!ArtifactsCoverState(state, artifacts, out error)) return false;
            if (UnityEngine.Application.isPlaying || current.graph == null || current.graph.Length == 0 ||
                current.preferences.Single(item => item.name == "GooglePlayServices.AutoResolveOnBuild").value != bool.TrueString)
            { error = "场景合同仅覆盖 EditMode、非空依赖图和当前启用的正规构建自动解析。"; return false; }
            if (!TryCaptureSceneLogging(cancellationToken, out SettingEntry[] logging, out error) ||
                !TryCaptureSceneSettingsPersistence(current, cancellationToken, out SceneSettingsPersistenceSnapshot persistence, out error)) return false;
            try
            {
                Type resolver = FindType("GooglePlayServices.PlayServicesResolver");
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
                FieldInfo jobsField = resolver.GetField("resolutionJobs", flags);
                object jobs = jobsField?.GetValue(null);
                object autoJob = resolver.GetField("autoResolveJobId", flags)?.GetValue(null);
                object resolving = resolver.GetField("autoResolving", flags)?.GetValue(null);
                object counter = resolver.GetField("scenesProcessed", flags)?.GetValue(null);
                object gradle = resolver.GetField("gradleResolverInstance", flags)?.GetValue(null);
                if (!(jobs is ICollection queue) || queue.Count != 0 || !(autoJob is int jobId) || jobId != 0 ||
                    !(resolving is bool busy) || busy || !(counter is int count) || count < 0 || count == int.MaxValue ||
                    gradle?.GetType() != resolver.Assembly.GetType("GooglePlayServices.GradleResolver", false))
                { error = "场景解析已有等待/自动任务、异常计数或未知 resolver；不能取消、重置或替换。"; return false; }
                var preferences = current.preferences.ToDictionary(item => item.name, item => item.value, StringComparer.Ordinal);
                if (preferences["GooglePlayServices.PromptBeforeAutoResolution"] != bool.FalseString)
                { error = "正规场景解析可能进入自动解析确认窗口，当前无弹窗合同不包含该选择。"; return false; }
                if (!TryCaptureSceneScheduler(cancellationToken, out SettingEntry[] scheduler, out error)) return false;
                var scheduling = new[]
                {
                    new SettingEntry { name = "resolutionJobs", value = "0" },
                    new SettingEntry { name = "autoResolveJobId", value = "0" },
                    new SettingEntry { name = "autoResolving", value = bool.FalseString },
                    new SettingEntry { name = "gradleResolverType", value = gradle.GetType().AssemblyQualifiedName },
                    new SettingEntry { name = "lastErrorSha256", value = GenerateActionCommon.ComputeTextHash(
                        resolver.GetField("lastError", flags)?.GetValue(null) as string) },
                }.Concat(scheduler).ToArray();
                snapshot = new SceneResolutionSnapshot
                { current = current, local = local, resolvedState = state, artifacts = artifacts,
                    toolchain = toolchain, scheduling = scheduling, logging = logging, settingsPersistence = persistence, scenesProcessed = count };
                snapshot.sha256 = ComputeSceneResolutionSnapshotSha256(snapshot);
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "冻结正规场景解析输入失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name); return false; }
        }

        /// <summary>只读复核完整冻结输入及日志、resolver 与主线程队列，不调用 ReadAll、Resolve、刷新或恢复写入。</summary>
        /// <param name="before">同一域正规前置捕获的完整输入；用于逐项重算和限定明确正常效果，不能拿执行后状态授权重跑。</param>
        /// <param name="cancellationToken">Core或上层正规捕获提供的只读取消令牌；不取消厂商/引擎已有任务。</param>
        /// <param name="error">out具体拒绝或不完整原因，成功为null；不能据此自动更改环境或重放Resolve。</param>
        /// <returns>bool；只读复核完整冻结输入及日志、resolver 与主线程队列，不调用 ReadAll、Resolve、刷新或恢复写入。</returns>
        internal static bool TryVerifySceneResolutionInputs(SceneResolutionSnapshot before,
            CancellationToken cancellationToken, out string error)
        {
            error = null;
            if (!SceneResolutionSnapshotsMatch(before, before) || before.scenesProcessed < 0)
            { error = "场景回执缺少完整自洽输入。"; return false; }
            if (!TryCaptureSceneResolutionInputs(cancellationToken, out SceneResolutionSnapshot current, out error, false)) return false;
            if (!SceneResolutionInputsMatchAfter(before, current) || current.scenesProcessed < before.scenesProcessed)
            { error = "正规场景输入、产物、日志、工具或调度状态已漂移。"; return false; }
            return true;
        }

        /// <summary>场景计数是正常回调游标，稳定摘要覆盖其余完整字段，由执行凭据单独验证游标与真实日志。</summary>
        /// <param name="snapshot">当前精确函数所声明的完整强类型快照；out仅成功时形成完整证据，普通输入不得跨域继承。</param>
        /// <returns>string；场景计数是正常回调游标，稳定摘要覆盖其余完整字段，由执行凭据单独验证游标与真实日志。</returns>
        internal static string ComputeSceneResolutionSnapshotSha256(SceneResolutionSnapshot snapshot)
        {
            if (snapshot == null) return null;
            return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
            {
                snapshot.current, snapshot.local, snapshot.resolvedState, snapshot.artifacts,
                snapshot.toolchain, snapshot.scheduling, snapshot.logging, snapshot.settingsPersistence,
            }));
        }

        /// <summary>比较完整稳定快照并校验自身摘要，不以摘要相同掩盖嵌套字段、工具或调度漂移。</summary>
        /// <param name="expected">本函数形参类型指明的冻结预期；实际完整状态比较，非用户任意覆盖目标。</param>
        /// <param name="actual">当前从同一正规读链重新捕获的完整强类型状态；缺字段/身份或摘要变化拒绝。</param>
        /// <returns>bool；比较完整稳定快照并校验自身摘要，不以摘要相同掩盖嵌套字段、工具或调度漂移。</returns>
        internal static bool SceneResolutionSnapshotsMatch(SceneResolutionSnapshot expected, SceneResolutionSnapshot actual)
        {
            return expected?.current != null && actual?.current != null && expected.local != null && actual.local != null &&
                expected.resolvedState != null && actual.resolvedState != null && expected.artifacts != null && actual.artifacts != null &&
                expected.toolchain != null && actual.toolchain != null && expected.scheduling != null && actual.scheduling != null &&
                expected.logging != null && actual.logging != null && !string.IsNullOrEmpty(expected.sha256) &&
                expected.sha256 == actual.sha256 && expected.sha256 == ComputeSceneResolutionSnapshotSha256(expected) &&
                actual.sha256 == ComputeSceneResolutionSnapshotSha256(actual);
        }

        /// <summary>正规 Jetifier 同字节同值写回合同，系统同键可物化当前有效 True，项目已有 True 及其他输入完整保持。</summary>
        [Serializable]
        internal sealed class SceneSettingsPersistenceSnapshot
        {
            public string projectPath;
            public string projectSha256;
            public string expectedProjectSha256;
            public string projectEntriesSha256;
            public string expectedProjectEntriesSha256;
            public bool projectKeyExists;
            public bool projectKeyIsTrue;
            public bool systemKeyExists;
            public bool systemKeyIsTrue;
            public string otherEditorPrefsSha256;
            public string environmentSha256;
        }

        /// <summary>只允许已声明的同字节写回及系统单键物化，项目 XML、其他偏好、标签、工具和调度均逐项比较。</summary>
        /// <param name="before">同一域正规前置捕获的完整输入；用于逐项重算和限定明确正常效果，不能拿执行后状态授权重跑。</param>
        /// <param name="after">正常执行之后只读重捕获的完整状态；只接纳显式声明的效果，不形成下一次Execute授权。</param>
        /// <returns>bool；只允许已声明的同字节写回及系统单键物化，项目 XML、其他偏好、标签、工具和调度均逐项比较。</returns>
        internal static bool SceneResolutionInputsMatchAfter(SceneResolutionSnapshot before, SceneResolutionSnapshot after)
        {
            if (before?.settingsPersistence == null || before.scenesProcessed > 0) return SceneResolutionSnapshotsMatch(before, after);
            SceneSettingsPersistenceSnapshot expected = before.settingsPersistence, actual = after?.settingsPersistence;
            if (actual == null || !expected.projectKeyExists || !expected.projectKeyIsTrue ||
                expected.expectedProjectSha256 != expected.projectSha256 || expected.expectedProjectEntriesSha256 != expected.projectEntriesSha256 ||
                expected.systemKeyExists && !expected.systemKeyIsTrue ||
                before.current?.preferences?.Count(item => item.name == "GooglePlayServices.UseJetifier" && item.value == bool.TrueString) != 1 ||
                expected.projectPath != actual.projectPath || expected.expectedProjectSha256 != actual.projectSha256 ||
                expected.expectedProjectSha256 != actual.expectedProjectSha256 || expected.expectedProjectEntriesSha256 != actual.projectEntriesSha256 ||
                expected.expectedProjectEntriesSha256 != actual.expectedProjectEntriesSha256 || !actual.projectKeyExists || !actual.projectKeyIsTrue ||
                !actual.systemKeyExists || !actual.systemKeyIsTrue || expected.otherEditorPrefsSha256 != actual.otherEditorPrefsSha256 ||
                expected.environmentSha256 != actual.environmentSha256 || before.current?.persistenceSha256 != expected.projectSha256 ||
                after.current?.persistenceSha256 != actual.projectSha256 || !SceneResolutionSnapshotsMatch(before, before) ||
                !SceneResolutionSnapshotsMatch(after, after)) return false;
            SceneResolutionSnapshot normalized = Util.Json.Deserialize<SceneResolutionSnapshot>(Util.Json.Serialize(before));
            normalized.settingsPersistence = actual;
            normalized.sha256 = ComputeSceneResolutionSnapshotSha256(normalized);
            return SceneResolutionSnapshotsMatch(normalized, after);
        }

        /// <summary>冻结 Save 完整字典、原字节、内存推导目标及单键 EditorPrefs 效果，不调用 setter、保存或 checkout。</summary>
        /// <param name="current">已完成正式读取的CurrentResolutionInputs，包含18生效偏好、15设置、完整图/模板/gpsr及nonserialized来源。</param>
        /// <param name="token">当前只读捕获/观察的取消令牌，取消只结束自有等待/取证而不消费或清理vendor工作。</param>
        /// <param name="snapshot">当前精确函数所声明的完整强类型快照；out仅成功时形成完整证据，普通输入不得跨域继承。</param>
        /// <param name="error">out具体拒绝或不完整原因，成功为null；不能据此自动更改环境或重放Resolve。</param>
        /// <returns>bool；冻结 Save 完整字典、原字节、内存推导目标及单键 EditorPrefs 效果，不调用 setter、保存或 checkout。</returns>
        private static bool TryCaptureSceneSettingsPersistence(CurrentResolutionInputs current, CancellationToken token,
            out SceneSettingsPersistenceSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            const string key = "GooglePlayServices.UseJetifier";
            if (current.preferences.Single(item => item.name == key).value != bool.TrueString) return true;
            try
            {
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
                const BindingFlags instance = BindingFlags.NonPublic | BindingFlags.Instance;
                Type type = FindType("Google.ProjectSettings");
                object project = type.GetField("projectSettings", flags)?.GetValue(null);
                object system = type.GetField("systemSettings", flags)?.GetValue(null);
                var values = project?.GetType().GetField("settings", instance)?.GetValue(project) as SortedDictionary<string, string>;
                Delegate checkout = type.GetField("checkoutFile", flags)?.GetValue(null) as Delegate;
                Type owner = type.Assembly.GetType("Google.ProjectSettings+<>c", false);
                Type loggerType = type.Assembly.GetType("Google.Logger", false);
                object logger = type.GetField("logger", flags)?.GetValue(null);
                object target = loggerType?.GetField("<Target>k__BackingField", instance)?.GetValue(logger);
                object level = loggerType?.GetField("<Level>k__BackingField", instance)?.GetValue(logger);
                if (project?.GetType() != type.Assembly.GetType("Google.InMemorySettings", false) || values == null ||
                    system?.GetType() != type.Assembly.GetType("Google.EditorSettings", false) ||
                    !ReferenceEquals(type.GetField("loadedSettings", flags)?.GetValue(null), project) ||
                    !(type.GetField("persistenceEnabled", flags)?.GetValue(null) is bool enabled) || !enabled ||
                    checkout?.GetInvocationList().Length != 1 || checkout.Method.DeclaringType != owner ||
                    checkout.Method.Name != "<.cctor>b__46_0" || checkout.Target?.GetType() != owner ||
                    UnityEditor.VersionControl.Provider.enabled || logger?.GetType() != loggerType || target == null || level == null ||
                    loggerType.GetField("<LogFilename>k__BackingField", instance)?.GetValue(logger) != null ||
                    loggerType.GetField("LogMessage", instance)?.GetValue(logger) != null || (Convert.ToInt32(target) & ~6) != 0 || (Convert.ToInt32(target) & 2) == 0 ||
                    Convert.ToInt32(level) < 0 || Convert.ToInt32(level) > 4 ||
                    values.TryGetValue(key, out string raw) && raw != bool.TrueString ||
                    UnityEditor.EditorPrefs.HasKey(key) && !UnityEditor.EditorPrefs.GetBool(key, true))
                { error = "Jetifier 正规同值持久化的字典、现值、日志、checkout 或 VCS 分支尚无完整合同。"; return false; }
                string path = Path.Combine(GenerateActionCommon.ProjectRoot, "ProjectSettings/GvhProjectSettings.xml");
                if (!GenerateActionCommon.TryResolveProjectPath(path, "EDM 项目设置", out path, out error)) return false;
                if (Path.GetFullPath(type.GetField("PROJECT_SETTINGS_FILE", flags)?.GetValue(null) as string ?? "") != path ||
                    !File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                { error = "EDM 正规项目设置路径、存在性或可写状态变化。"; return false; }
                Dictionary<string, string> parsed;
                using (FileStream stream = File.OpenRead(path))
                {
                    try { parsed = ReadSceneSettingsEntries(stream); }
                    catch (InvalidOperationException exception) { error = exception.Message; return false; }
                }
                if (parsed.Count != values.Count || values.Any(pair => !parsed.TryGetValue(pair.Key, out string value) || value != pair.Value))
                { error = "EDM 完整 XML 与已加载字典不同，不能执行隐含加载或覆盖未知项。"; return false; }
                var expected = new SortedDictionary<string, string>(values, values.Comparer) { [key] = bool.TrueString };
                var preferences = current.preferences.Where(item => item.name != key).Select(item => new SettingEntry
                {
                    name = item.name,
                    value = UnityEditor.EditorPrefs.HasKey(item.name) + "|" + GenerateActionCommon.ComputeTextHash(
                        item.name == "GooglePlayServices.PackageDirectory" || item.name == "GooglePlayServices.LocalMavenRepoDir"
                            ? UnityEditor.EditorPrefs.GetString(item.name, "") : UnityEditor.EditorPrefs.GetBool(item.name, false).ToString()),
                }).Concat(new[] { new SettingEntry
                {
                    name = "GooglePlayServices.UseProjectSettings",
                    value = UnityEditor.EditorPrefs.HasKey("GooglePlayServices.UseProjectSettings") + "|" +
                        UnityEditor.EditorPrefs.GetBool("GooglePlayServices.UseProjectSettings", true),
                } }).ToArray();
                snapshot = new SceneSettingsPersistenceSnapshot
                {
                    projectPath = path, projectSha256 = GenerateActionCommon.ComputeFileHash(path, token),
                    expectedProjectSha256 = ComputeSceneSettingsXmlSha256(expected),
                    projectEntriesSha256 = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(values)),
                    expectedProjectEntriesSha256 = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(expected)),
                    projectKeyExists = values.ContainsKey(key), projectKeyIsTrue = values.TryGetValue(key, out string value) && value == bool.TrueString,
                    systemKeyExists = UnityEditor.EditorPrefs.HasKey(key), systemKeyIsTrue = UnityEditor.EditorPrefs.GetBool(key, true),
                    otherEditorPrefsSha256 = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(preferences)),
                    environmentSha256 = GenerateActionCommon.ComputeTextHash("default-checkout|VCS-disabled|persistence-true|" + target + "|" + level),
                };
                if (current.persistenceSha256 != snapshot.projectSha256) { error = "EDM 设置在冻结期间漂移。"; return false; }
                if (!snapshot.projectKeyExists || !snapshot.projectKeyIsTrue || snapshot.expectedProjectSha256 != snapshot.projectSha256 ||
                    snapshot.expectedProjectEntriesSha256 != snapshot.projectEntriesSha256)
                { error = "当前场景仅支持已存在 True 项的正规同字节写回；新增项目键或 XML 规范化仍需独立 Firebase 与警告身份合同。"; return false; }
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "冻结 Jetifier 正规持久化失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>读取完整设置项，仅忽略格式缩进空白；注释、处理指令、实际文本、未知结构和重复键仍拒绝，流由调用方释放。</summary>
        /// <param name="input">caller拥有的完整项目设置XML流；禁DTD/外解析器，解析后不丢未知内容。</param>
        /// <returns>Dictionary&lt;string, string&gt;；读取完整设置项，仅忽略格式缩进空白；注释、处理指令、实际文本、未知结构和重复键仍拒绝，流由调用方释放。</returns>
        internal static Dictionary<string, string> ReadSceneSettingsEntries(Stream input)
        {
            XDocument document;
            using (XmlReader reader = XmlReader.Create(input, new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = true }))
                document = XDocument.Load(reader, LoadOptions.None);
            XElement root = document.Root;
            if (root?.Name != "projectSettings" || document.Nodes().Any(node => node != root) || root.HasAttributes || root.Nodes().Any(node => !(node is XElement)) ||
                root.Elements().Any(entry => entry.Name != "projectSetting" || entry.HasElements || entry.Nodes().Any() ||
                    entry.Attributes().Count() != 2 || string.IsNullOrEmpty((string)entry.Attribute("name")) || string.IsNullOrEmpty((string)entry.Attribute("value"))))
                throw new InvalidOperationException("EDM 设置 XML 含未知结构，不能把正规重写泛化为允许丢弃内容。");
            return root.Elements().ToDictionary(entry => (string)entry.Attribute("name"), entry => (string)entry.Attribute("value"), StringComparer.Ordinal);
        }

        /// <summary>逐项复现已核 Save 的 XmlWriter 布局，仅在内存计算预期完整字节摘要，不写真实配置。</summary>
        /// <param name="values">按该函数强类型声明的完整设置字典或out已读设置数组；必须保留全部键值，不能空集合造完成。</param>
        /// <returns>string；逐项复现已核 Save 的 XmlWriter 布局，仅在内存计算预期完整字节摘要，不写真实配置。</returns>
        internal static string ComputeSceneSettingsXmlSha256(SortedDictionary<string, string> values)
        {
            using (var memory = new MemoryStream())
            {
                using (XmlWriter writer = XmlWriter.Create(memory, new XmlWriterSettings
                { Encoding = new System.Text.UTF8Encoding(false), Indent = true, IndentChars = "  ", NewLineChars = "\n", NewLineHandling = NewLineHandling.None }))
                {
                    writer.WriteStartElement("projectSettings");
                    foreach (KeyValuePair<string, string> pair in values)
                    {
                        writer.WriteStartElement("projectSetting");
                        if (!string.IsNullOrEmpty(pair.Key) && !string.IsNullOrEmpty(pair.Value))
                        { writer.WriteAttributeString("name", pair.Key); writer.WriteAttributeString("value", pair.Value); }
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
                using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(memory.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>冻结正规日志对象与公开尾部事件，排除文件输出和外部委托；过滤的消息由执行窗口自有观察者捕获。</summary>
        /// <param name="cancellationToken">Core或上层正规捕获提供的只读取消令牌；不取消厂商/引擎已有任务。</param>
        /// <param name="logging">out当前已审Google公开日志事件/类型/target/级别及实例指纹，不含消息正文。</param>
        /// <param name="error">out具体拒绝或不完整原因，成功为null；不能据此自动更改环境或重放Resolve。</param>
        /// <returns>bool；冻结正规日志对象与公开尾部事件，排除文件输出和外部委托；过滤的消息由执行窗口自有观察者捕获。</returns>
        private static bool TryCaptureSceneLogging(CancellationToken cancellationToken, out SettingEntry[] logging, out string error)
        {
            logging = null;
            error = null;
            Type loggerType = FindType("Google.Logger");
            if (!TryVerifyEdmType(loggerType, "Google.VersionHandlerImpl.dll",
                    "c3a6d0419b41b0cbc7ad518b0cc2b311af043b5d71ed709ea3baa576b79862ce", cancellationToken, out error)) return false;
            try
            {
                const BindingFlags instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
                Type resolver = FindType("GooglePlayServices.PlayServicesResolver");
                Type support = FindType("Google.JarResolver.PlayServicesSupport");
                if (support?.Assembly != resolver?.Assembly)
                { error = "场景解析依赖日志与 resolver 不属于同一已审程序集。"; return false; }
                object logger = resolver.GetField("logger", flags)?.GetValue(null);
                FieldInfo filename = loggerType.GetField("<LogFilename>k__BackingField", instance);
                FieldInfo callback = loggerType.GetField("LogMessage", instance);
                object target = loggerType.GetField("<Target>k__BackingField", instance)?.GetValue(logger);
                object level = loggerType.GetField("<Level>k__BackingField", instance)?.GetValue(logger);
                EventInfo logEvent = loggerType.GetEvent("LogMessage", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo invoke = logEvent?.EventHandlerType?.GetMethod("Invoke");
                ParameterInfo[] arguments = invoke?.GetParameters();
                Type logLevel = loggerType.Assembly.GetType("Google.LogLevel", false);
                Delegate forward = support.GetField("logger", flags)?.GetValue(null) as Delegate;
                Delegate[] methods = forward?.GetInvocationList();
                bool debug = Environment.CommandLine.ToLowerInvariant().Contains("-gvh_log_debug");
                if (logger?.GetType() != loggerType || filename?.FieldType != typeof(string) || filename.GetValue(logger) != null ||
                    callback == null || callback.GetValue(logger) != null || target == null || level == null ||
                    logEvent?.EventHandlerType?.FullName != "Google.Logger+LogMessageDelegate" || callback.FieldType != logEvent.EventHandlerType ||
                    logEvent.GetAddMethod()?.IsPublic != true || logEvent.GetRemoveMethod()?.IsPublic != true ||
                    logLevel?.IsEnum != true || Enum.GetUnderlyingType(logLevel) != typeof(int) || invoke?.ReturnType != typeof(void) ||
                    arguments?.Length != 2 || arguments[0].ParameterType != typeof(string) || arguments[1].ParameterType != logLevel ||
                    (Convert.ToInt32(target) & ~6) != 0 || (Convert.ToInt32(target) & 2) == 0 ||
                    Convert.ToInt32(level) < 0 || Convert.ToInt32(level) > 4 ||
                    !(support.GetField("verboseLogging", flags)?.GetValue(null) is bool verbose) || verbose ||
                    methods?.Length != 1 || methods[0].Target != null || !methods[0].Method.IsStatic ||
                    methods[0].Method.DeclaringType != resolver || methods[0].Method.Name != "LogDelegate")
                { error = "场景解析日志含文件输出、外部委托、详细输出或未知公开事件签名。"; return false; }
                logging = new[]
                {
                    new SettingEntry { name = "loggerIdentitySha256", value = GenerateActionCommon.ComputeTextHash(AnalyticsIdentity(logger)) },
                    new SettingEntry { name = "loggerEvent", value = "public:LogMessage:void(string,Google.LogLevel):owned-execution-only" },
                    new SettingEntry { name = "loggerTarget", value = Convert.ToInt32(target).ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    new SettingEntry { name = "loggerLevel", value = Convert.ToInt32(level).ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    new SettingEntry { name = "loggerFilename", value = "null" },
                    new SettingEntry { name = "loggerDelegate", value = "null" },
                    new SettingEntry { name = "supportLogger", value = "PlayServicesResolver.LogDelegate:static:single" },
                    new SettingEntry { name = "supportVerbose", value = bool.FalseString },
                    new SettingEntry { name = "debugCommandLine", value = debug.ToString() },
                };
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "冻结场景解析日志失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>
        /// 冻结同步解析会轮询的正规 EDM 主线程队列。允许当前平台条件恒 false 的初始化等待项，
        /// 以及 BuildTarget 已一致的版本检查重新排队；JobId 与时刻是运行游标，不充当稳定输入。
        /// </summary>
        /// <param name="cancellationToken">仅取消当前只读捕获；不调用、消费、取消或清理任何厂商任务。</param>
        /// <param name="scheduling">out已审自然等待项的稳定身份数组；只在所有队列门完整满足时返回有效结果。</param>
        /// <param name="error">out具体拒绝原因；诊断最多列八个排队目标的类型/方法等元数据，不含消息正文或任务参数。</param>
        /// <returns>当前固定DLL的队列与已审自然条件全部满足时为true；不代表任务已完成，不授予执行未知任务的权限。</returns>
        internal static bool TryCaptureSceneScheduler(CancellationToken cancellationToken,
            out SettingEntry[] scheduling, out string error)
        {
            scheduling = Array.Empty<SettingEntry>();
            error = null;
            Type type = FindType("Google.RunOnMainThread");
            if (!TryVerifyEdmType(type, "Google.VersionHandlerImpl.dll",
                    "c3a6d0419b41b0cbc7ad518b0cc2b311af043b5d71ed709ea3baa576b79862ce", cancellationToken, out error)) return false;
            try
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                const BindingFlags instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                object jobs = type.GetField("jobs", flags)?.GetValue(null);
                object completed = type.GetField("completePollingJobs", flags)?.GetValue(null);
                object runningCount = type.GetField("runningExecuteAllCount", flags)?.GetValue(null);
                object running = type.GetField("runningJobs", flags)?.GetValue(null);
                object polling = type.GetField("pollingJobs", flags)?.GetValue(null);
                if (!(jobs is ICollection queued) || queued.Count != 0 || !(completed is ICollection finished) || finished.Count != 0 ||
                    !(runningCount is int executionCount) || executionCount != 0 || !(running is bool busy) || busy || !(polling is IEnumerable entries))
                { error = "EDM 主线程队列已有执行/完成任务，不能在同步场景解析时消费未知工作；queued=" +
                    string.Join(",", (jobs as IEnumerable)?.Cast<object>().Take(8).Select(item => item is Delegate method
                        ? method.Method.DeclaringType?.FullName + "." + method.Method.Name + ":target=" + method.Target?.GetType().FullName
                        : item?.GetType().FullName) ?? Array.Empty<string>()); return false; }
                var result = new List<SettingEntry>();
                int index = 0;
                foreach (object entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!(entry is Delegate callback) || callback.GetInvocationList().Length != 1 || callback.Method.ReturnType != typeof(bool) ||
                        callback.Method.GetParameters().Length != 0 || callback.Target == null)
                    { error = "EDM pollingJobs 包含未知委托签名。"; return false; }
                    Type targetType = callback.Target.GetType();
                    if (targetType == type.Assembly.GetType("Google.EditorInitializer+<>c__DisplayClass0_0", false) &&
                        callback.Method.DeclaringType == targetType && callback.Method.Name == "<InitializeOnMainThread>b__0")
                    {
                        Delegate condition = targetType.GetField("condition", instance)?.GetValue(callback.Target) as Delegate;
                        object execute = targetType.GetField("isExecuteMethodEnabled", instance)?.GetValue(callback.Target);
                        string conditionOwner = condition?.Method.DeclaringType?.FullName;
                        bool iosCondition = conditionOwner == "Google.IOSResolver+<>c" && condition.Method.Name == "<.cctor>b__65_0" ||
                            conditionOwner == "Firebase.Editor.XcodeProjectPatcher+<>c" && condition.Method.Name == "<.cctor>b__11_0";
                        if (!iosCondition || condition.GetInvocationList().Length != 1 || !(execute is bool executeEnabled) || executeEnabled)
                        { error = "EDM 初始化等待项将执行或身份未知，须先完成正规平台初始化后重新 Plan。"; return false; }
                        Type conditionType = condition.Method.DeclaringType;
                        if (conditionOwner == "Google.IOSResolver+<>c")
                        {
                            if (!TryVerifyEdmType(conditionType, "Google.IOSResolver.dll",
                                "ca00858a5f4a657e949480015124f9c783ea80c42bb6000a9c5ec0112ffa5ca5", cancellationToken, out error)) return false;
                        }
                        else if (Path.GetFileName(conditionType.Assembly.Location) != "Firebase.Editor.dll" ||
                            GenerateActionCommon.ComputeFileHash(conditionType.Assembly.Location, cancellationToken) !=
                            "b50eefa93d23c673bc8a97266e3d36dcfba0e9d3e49f5e7cc7850d595c12f1bf")
                        { error = "Firebase 初始化条件的实际 DLL 身份漂移。"; return false; }
                        result.Add(new SettingEntry { name = "polling/" + index++, value = conditionOwner + "." + condition.Method.Name + ":condition=false:executeMethod=false" });
                    }
                    else if (targetType == type.Assembly.GetType("Google.RunOnMainThread+ScheduledJob", false) &&
                        callback.Method.DeclaringType == targetType && callback.Method.Name == "PollUntilExecutionTime")
                    {
                        Delegate job = targetType.GetField("Job", instance)?.GetValue(callback.Target) as Delegate;
                        object delay = targetType.GetField("DelayInMilliseconds", instance)?.GetValue(callback.Target);
                        Type checker = type.Assembly.GetType("Google.VersionHandlerImpl+BuildTargetChecker", false);
                        object knownTarget = checker?.GetField("lastKnownBuildTarget", flags)?.GetValue(null);
                        if (job?.GetInvocationList().Length != 1 || job.Target != null || !job.Method.IsStatic || job.Method.DeclaringType != checker ||
                            job.Method.Name != "CheckBuildTarget" || !(delay is double milliseconds) || milliseconds != 1000 ||
                            !(knownTarget is BuildTarget buildTarget) || buildTarget != EditorUserBuildSettings.activeBuildTarget)
                        { error = "EDM 延迟任务可能触发平台变化或未知工作，当前场景计划不能消费；" +
                            "job=" + (job == null ? "null" : job.Method.DeclaringType?.FullName + "." + job.Method.Name) +
                            ";target=" + (job?.Target?.GetType().FullName ?? "null") + ";module=" + job?.Method.Module.ModuleVersionId + ";static=" + job?.Method.IsStatic +
                            ";invocations=" + job?.GetInvocationList().Length + ";id=" + targetType.GetField("JobId", instance)?.GetValue(callback.Target) +
                            ";delay=" + delay; return false; }
                        result.Add(new SettingEntry { name = "polling/" + index++, value = "BuildTargetChecker.CheckBuildTarget:knownTarget=" + buildTarget + ":delay=1000" });
                    }
                    else
                    { error = "EDM pollingJobs 尚无当前条件合同：" + callback.Method.DeclaringType?.FullName + "." + callback.Method.Name; return false; }
                }
                scheduling = result.ToArray();
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "冻结 EDM 主线程队列失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name); return false; }
        }

        /// <summary>
        /// 冻结正规 Java 版本读取与 Jetifier 检查的输入；已有模板且 JDK 不过旧才能排除窗口和测量分支。
        /// 不运行 Gradle、下载器、SDK 安装，不调用 JavaUtilities 的私有入口。
        /// </summary>
        internal static bool TryCaptureNoPromptToolchain(CurrentResolutionInputs current, CancellationToken cancellationToken,
            out SettingEntry[] toolchain, out string error)
        {
            toolchain = Array.Empty<SettingEntry>();
            error = null;
            try
            {
                if (current?.resolutionSettings == null || current.templateSha256 == null)
                { error = "工具链缺少完整当前解析输入。"; return false; }
                var settings = current.resolutionSettings.ToDictionary(item => item.name, item => item.value, StringComparer.Ordinal);
                if (settings["gradleBuildEnabled"] != bool.TrueString || settings["gradleTemplateEnabled"] != bool.TrueString ||
                    current.templateSha256.Single(item => item.name == AndroidPluginPath + "/mainTemplate.gradle").value == "absent")
                { error = "正规解析会启用或生成 mainTemplate.gradle，当前无弹窗支路不能接纳。"; return false; }
                Type resolver = FindType("GooglePlayServices.PlayServicesResolver");
                Type compat = FindType("GooglePlayServices.UnityCompat");
                string gradle = resolver.GetProperty("GradleVersion", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
                string agp = resolver.GetProperty("AndroidGradlePluginVersion", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
                MethodInfo targetSdk = compat.GetMethod("GetAndroidTargetSDKVersion", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (targetSdk?.ReturnType != typeof(int) || !Version.TryParse(gradle, out _) || !Version.TryParse(agp, out Version agpVersion))
                { error = "正规 Gradle/AGP/目标 SDK 版本无法精确读取。"; return false; }
                int sdk = (int)targetSdk.Invoke(null, null);
                if (settings["useJetifier"] == bool.TrueString && (agpVersion < new Version(3, 2) || sdk < 28))
                { error = "Jetifier 会请求升级 Gradle 或目标 SDK，当前无弹窗合同不能接纳。"; return false; }
                foreach (string name in new[] { "JAVA_TOOL_OPTIONS", "JDK_JAVA_OPTIONS", "_JAVA_OPTIONS" })
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
                    { error = "Java 环境包含额外启动参数，版本读取也可能加载外部代码：" + name; return false; }
                string javaHome = compat.GetProperty("AndroidExternalToolsSettingsJdkRootPath", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
                string playback = resolver.GetProperty("AndroidPlaybackEngineDirectory", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
                bool embedded = EditorPrefs.GetBool("JdkUseEmbedded");
                if (string.IsNullOrEmpty(javaHome) || embedded)
                {
                    string platform = UnityEngine.Application.platform.ToString().Replace("Editor", "").Replace("OSX", "MacOS");
                    string first = string.IsNullOrEmpty(playback) ? null : Path.Combine(playback, "Tools", "OpenJDK", platform);
                    string second = string.IsNullOrEmpty(playback) ? null : Path.Combine(playback, "OpenJDK");
                    if (first != null && Directory.Exists(first)) javaHome = first;
                    else if (second != null && Directory.Exists(second)) javaHome = second;
                }
                if (string.IsNullOrEmpty(javaHome)) javaHome = EditorPrefs.GetString("JdkPath");
                if (string.IsNullOrEmpty(javaHome)) javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (string.IsNullOrEmpty(javaHome))
                { error = "Java 将进入系统 PATH 查找，当前合同需要明确 JDK 根。"; return false; }
                string java = Path.Combine(javaHome, "bin", Path.DirectorySeparatorChar == '\\' ? "java.exe" : "java");
                string release = Path.Combine(javaHome, "release");
                if (!File.Exists(java) || !File.Exists(release))
                { error = "Java 或 JDK release 不存在，不能排除缺失工具窗口。"; return false; }
                Match versionText = Regex.Match(File.ReadAllText(release), "(?m)^JAVA_VERSION=\"([^\"]+)\"\\s*$");
                string versionValue = versionText.Success ? Regex.Replace(versionText.Groups[1].Value, "[^0-9.]", "") : null;
                if (versionValue != null && !versionValue.Contains(".")) versionValue += ".0";
                object minimum = resolver.Assembly.GetType("GooglePlayServices.JavaUtilities", false)?.GetField(
                    "MinimumJdkVersion", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (!(minimum is Version minimumVersion) || !Version.TryParse(versionValue, out Version javaVersion) || javaVersion < minimumVersion)
                { error = "JDK 版本未知或过旧，不能排除测量与错误分支。"; return false; }
                if (Path.GetFullPath(Directory.GetCurrentDirectory()) != GenerateActionCommon.ProjectRoot)
                { error = "正规 Java 版本命令的当前目录不等于项目根。"; return false; }
                toolchain = new[]
                {
                    new SettingEntry { name = "javaHome", value = Path.GetFullPath(javaHome) },
                    new SettingEntry { name = "javaBinary", value = Path.GetFullPath(java) },
                    new SettingEntry { name = "javaSha256", value = GenerateActionCommon.ComputeFileHash(java, cancellationToken) },
                    new SettingEntry { name = "jdkReleaseSha256", value = GenerateActionCommon.ComputeFileHash(release, cancellationToken) },
                    new SettingEntry { name = "javaVersion", value = javaVersion.ToString() },
                    new SettingEntry { name = "minimumJdkVersion", value = minimumVersion.ToString() },
                    new SettingEntry { name = "jdkUseEmbedded", value = embedded.ToString() },
                    new SettingEntry { name = "jdkPathSha256", value = GenerateActionCommon.ComputeTextHash(EditorPrefs.GetString("JdkPath")) },
                    new SettingEntry { name = "environmentJavaHomeSha256", value = GenerateActionCommon.ComputeTextHash(Environment.GetEnvironmentVariable("JAVA_HOME")) },
                    new SettingEntry { name = "gradleVersion", value = gradle },
                    new SettingEntry { name = "androidGradlePluginVersion", value = agp },
                    new SettingEntry { name = "targetSdk", value = sdk.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    new SettingEntry { name = "externalCommand", value = "java -version; cwd=projectRoot; JAVA_HOME=javaHome" },
                };
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "冻结无弹窗工具链失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name); return false; }
        }

        /// <summary>
        /// 正规公开 ReadAll 仅刷新 XML 依赖内存，完整 XML 和设置前后摘要必须保持一致。
        /// 只接纳已审 Console 日志实例和固定静态 LogDelegate，禁止文件日志、外部委托及详细仓库原值输出。
        /// </summary>
        internal static bool TryReadCurrentXmlDependencies(CancellationToken cancellationToken,
            out LocalMavenInputs inputs, out string error)
        {
            inputs = null;
            error = null;
            if (!TryCaptureCurrentResolutionInputs(cancellationToken, out CurrentResolutionInputs current, out error) ||
                !TryCaptureLocalMavenInputs(cancellationToken, out LocalMavenInputs before, out error)) return false;
            Type resolver = FindType("GooglePlayServices.PlayServicesResolver");
            Type support = FindType("Google.JarResolver.PlayServicesSupport");
            Type xmlType = FindType("GooglePlayServices.AndroidXmlDependencies");
            Type loggerType = FindType("Google.Logger");
            object logger = resolver.GetField("logger", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            object xml = resolver.GetField("xmlDependencies", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            try
            {
                const BindingFlags instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                object filename = loggerType?.GetField("<LogFilename>k__BackingField", instance)?.GetValue(logger);
                FieldInfo filenameField = loggerType?.GetField("<LogFilename>k__BackingField", instance);
                FieldInfo callbackField = loggerType?.GetField("LogMessage", instance);
                object target = loggerType?.GetField("<Target>k__BackingField", instance)?.GetValue(logger);
                FieldInfo verboseField = support.GetField("verboseLogging", BindingFlags.NonPublic | BindingFlags.Static);
                Delegate supportLogger = support.GetField("logger", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as Delegate;
                Delegate[] methods = supportLogger?.GetInvocationList();
                if (logger?.GetType() != loggerType || xml?.GetType() != xmlType ||
                    filenameField?.FieldType != typeof(string) || filename != null || callbackField == null ||
                    callbackField.GetValue(logger) != null || target == null || (Convert.ToInt32(target) & ~6) != 0 ||
                    (Convert.ToInt32(target) & 2) == 0 || !(verboseField?.GetValue(null) is bool verbose) || verbose ||
                    methods?.Length != 1 || methods[0].Target != null || !methods[0].Method.IsStatic ||
                    methods[0].Method.DeclaringType != resolver || methods[0].Method.Name != "LogDelegate")
                { error = "EDM 预读日志包含未冻结的写文件、详细仓库输出或外部委托。"; return false; }
                MethodInfo readAll = xmlType.GetMethod("ReadAll", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { loggerType }, null);
                if (readAll?.ReturnType != typeof(bool)) { error = "正规 XML ReadAll 公开签名变化。"; return false; }
                cancellationToken.ThrowIfCancellationRequested();
                object result = readAll.Invoke(xml, new[] { logger });
                cancellationToken.ThrowIfCancellationRequested();
                if (!(result is bool succeeded) || !succeeded)
                { error = "正规 XML 依赖预读明确失败；当前图不能进入执行计划。"; return false; }
                if (!TryCaptureLocalMavenInputs(cancellationToken, out LocalMavenInputs after, out error) ||
                    !TryCaptureCurrentResolutionInputs(cancellationToken, out CurrentResolutionInputs afterCurrent, out error)) return false;
                if (Util.Json.Serialize(before.xmlInputs) != Util.Json.Serialize(after.xmlInputs) ||
                    current.persistenceSha256 != afterCurrent.persistenceSha256 ||
                    Util.Json.Serialize(current.templateSha256) != Util.Json.Serialize(afterCurrent.templateSha256) ||
                    Util.Json.Serialize(current.preferences) != Util.Json.Serialize(afterCurrent.preferences))
                { error = "正规 XML 预读期间文件或设置漂移，须重新 Plan。"; return false; }
                inputs = after;
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "正规 XML 预读失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name); return false; }
        }

        /// <summary>最窄模板解析支路的单个 SRCAAR 复制及正规 POM 包装修正目标。</summary>
        [Serializable]
        internal sealed class TemplateCopyInput
        {
            public string source;
            public string sourceSha256;
            public string sourcePomSha256;
            public string target;
            public string targetSha256;
            public string targetMetaSha256;
            public string targetPomSha256;
            public string expectedTargetPomSha256;
            public string targetPomMetaSha256;
            public bool targetIsDeleted;
            public bool targetPomIsDeleted;
            public bool compatibleWithAnyPlatform;
            public bool compatibleWithAndroid;
        }

        /// <summary>当前正规解析只执行已有模板支路；外部工具是只读 Java 版本检查。</summary>
        [Serializable]
        internal sealed class TemplateResolutionPlan
        {
            public CurrentResolutionInputs current;
            public LocalMavenInputs local;
            public TemplateCopyInput[] copies;
            public string[] deletionSet;
            public SettingEntry[] deletionInputs;
            public SettingEntry[] toolchain;
            public AnalyticsSnapshot analytics;
            public SceneSettingsPersistenceSnapshot settingsPersistence;
            public SettingEntry[] generatedDirectoryInputs;
            public string[] writeSet;
            public string sha256;
        }

        /// <summary>已审版本实际不发送数据的 analytics 自然分支；对象引用只在本域保留，不进入回执。</summary>
        [Serializable]
        internal sealed class AnalyticsSnapshot
        {
            public bool globallyEnabled;
            public bool consentRequested;
            public bool consentRequesting;
            public bool enabled;
            public bool useProjectSettings;
            public string instanceFieldsSha256;
            public string objectIdentitySha256;
            public string settingsSha256;
            public string persistenceSha256;
            public string sha256;
        }

        /// <summary>每个现存对象的域内不透明身份；弱引用避免持有被替换的 vendor 实例。</summary>
        private sealed class AnalyticsObjectIdentity
        {
            internal readonly string value = Guid.NewGuid().ToString("N");
        }

        private static readonly ConditionalWeakTable<object, AnalyticsObjectIdentity> s_AnalyticsIdentities =
            new ConditionalWeakTable<object, AnalyticsObjectIdentity>();

        /// <summary>给现存对象分配本 Action 的弱身份；不初始化、修改或调用该 vendor 对象。</summary>
        private static string AnalyticsIdentity(object value)
        {
            return value == null ? "null" : s_AnalyticsIdentities.GetValue(value, _ => new AnalyticsObjectIdentity()).value;
        }

        /// <summary>仅读取已锁定版本现存实例字段；标量和引用均只输出摘要或不透明身份，不调用字段所指对象。</summary>
        private static string CaptureAnalyticsFieldsSha256(Type type, object analytics)
        {
            const BindingFlags instance = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            var fields = new List<SettingEntry>();
            foreach (FieldInfo field in type.GetFields(instance).OrderBy(field => field.Name, StringComparer.Ordinal))
            {
                object value = field.GetValue(analytics);
                fields.Add(new SettingEntry { name = field.Name,
                    value = field.FieldType == typeof(string) || field.FieldType == typeof(bool)
                        ? GenerateActionCommon.ComputeTextHash(value?.ToString()) : AnalyticsIdentity(value) });
            }
            return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(fields));
        }

        /// <summary>读取已核五项系统偏好的存在状态和生效值；Cookie 仅进入摘要，不保留其原值。</summary>
        private static string CaptureAnalyticsPreferencesSha256(string[] keys)
        {
            var values = keys.Select(key => new SettingEntry { name = key,
                value = GenerateActionCommon.ComputeTextHash(EditorPrefs.HasKey(key) + "\n" +
                    (key.EndsWith("Cookie", StringComparison.Ordinal) ? EditorPrefs.GetString(key, "") :
                        EditorPrefs.GetBool(key, !key.EndsWith("ConsentRequested", StringComparison.Ordinal)).ToString())) }).ToArray();
            return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(values));
        }

        /// <summary>按固定版本实际 Report 控制流接纳自然无弹窗分支；不修改任何同意或启用设置。</summary>
        internal static bool IsAnalyticsBranchReadOnly(bool globallyEnabled, bool consentRequested, bool consentRequesting)
        {
            return !consentRequesting && (!globallyEnabled || consentRequested);
        }

        /// <summary>
        /// 在完整当前输入已读取后冻结既有 analytics 及已加载设置；不调用 Report、Prompt、Cookie 或 setter。
        /// 已审 DLL 的 Report 回调只读 Enabled 后返回，没有 HTTP、Cookie 写入或持久上报队列。
        /// </summary>
        internal static bool TryCaptureAnalyticsSnapshot(CurrentResolutionInputs current, CancellationToken cancellationToken,
            out AnalyticsSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            if (current?.preferences == null || string.IsNullOrEmpty(current.persistenceSha256) ||
                current.capturedResolverType == null || current.capturedSettingsObject == null)
            { error = "analytics 缺少已完成读取的当前解析输入。"; return false; }
            Type resolver = FindType("GooglePlayServices.PlayServicesResolver");
            Type type = FindType("Google.EditorMeasurement");
            if (!TryVerifyEdmType(resolver, "Google.JarResolver.dll",
                    "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0", cancellationToken, out error) ||
                !TryVerifyEdmType(type, "Google.VersionHandlerImpl.dll",
                    "c3a6d0419b41b0cbc7ad518b0cc2b311af043b5d71ed709ea3baa576b79862ce", cancellationToken, out error) ||
                current.capturedResolverType != resolver)
            { error = error ?? "analytics 解析器类型不是当前正规读取链实例。"; return false; }
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            const BindingFlags instance = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            try
            {
                object analytics = resolver.GetField("analytics", flags)?.GetValue(null);
                object settings = type.GetField("settings", instance)?.GetValue(analytics);
                Type settingsType = type.Assembly.GetType("Google.ProjectSettings", false);
                object project = settingsType?.GetField("projectSettings", flags)?.GetValue(null);
                object system = settingsType?.GetField("systemSettings", flags)?.GetValue(null);
                object loaded = settingsType?.GetField("loadedSettings", flags)?.GetValue(null);
                if (analytics?.GetType() != type || settings?.GetType() != settingsType ||
                    project?.GetType() != type.Assembly.GetType("Google.InMemorySettings", false) ||
                    system?.GetType() != type.Assembly.GetType("Google.EditorSettings", false) || !ReferenceEquals(loaded, project))
                { error = "analytics 或设置尚未初始化、被替换或包含未审实现。"; return false; }
                Type settingsDialog = resolver.Assembly.GetType("GooglePlayServices.SettingsDialog", false);
                if (!ReferenceEquals(settingsDialog?.GetField("projectSettings", flags)?.GetValue(null), settings) ||
                    !ReferenceEquals(current.capturedSettingsObject, settings))
                { error = "analytics 与当前解析输入并非同一设置归属。"; return false; }
                if (!Equals(settingsType.GetField("moduleName", instance)?.GetValue(settings), "GooglePlayServices."))
                { error = "analytics 设置归属不在当前已审模块。"; return false; }
                const string prefix = "com.google.external-dependency-manager";
                foreach (string name in new[] { "enabledPreferencesKey", "consentRequestedPreferencesKey", "cookiePreferencesKey", "systemCookiePreferencesKey" })
                {
                    string suffix = name == "enabledPreferencesKey" ? "AnalyticsEnabled" :
                        name == "consentRequestedPreferencesKey" ? "AnalyticsConsentRequested" :
                        name == "cookiePreferencesKey" ? "AnalyticsCookie" : "AnalyticsSystemCookie";
                    if (!Equals(type.GetField(name, instance)?.GetValue(analytics), prefix + suffix))
                    { error = "analytics 偏好键与已审实现不一致。"; return false; }
                }
                if (!(type.GetField("GloballyEnabled", flags)?.GetValue(null) is bool global) ||
                    !(type.GetField("ConsentRequesting", instance)?.GetValue(analytics) is bool requesting))
                { error = "analytics 控制字段签名变化。"; return false; }
                string[] keys = { prefix + "AnalyticsEnabled", prefix + "AnalyticsConsentRequested", prefix + "AnalyticsCookie",
                    prefix + "AnalyticsSystemCookie", "GooglePlayServices.UseProjectSettings" };
                string preferencesBefore = CaptureAnalyticsPreferencesSha256(keys);
                string fieldsBefore = CaptureAnalyticsFieldsSha256(type, analytics);
                object dictionary = project.GetType().GetField("settings", instance)?.GetValue(project);
                if (!(dictionary is SortedDictionary<string, string> values))
                { error = "analytics 当前项目设置集合结构变化。"; return false; }
                string projectBefore = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(values));
                bool consent = EditorPrefs.GetBool(prefix + "AnalyticsConsentRequested", false);
                if (!IsAnalyticsBranchReadOnly(global, consent, requesting))
                { error = "analytics 正在请求或尚未请求同意，正规 Report 可能进入窗口；保持当前设置并先完成该用户选择。"; return false; }
                if (!TryReadGoogleSettings(settings,
                        new[] { new GoogleSettingRequest { key = prefix + "AnalyticsEnabled", defaultBoolean = true } },
                        cancellationToken, out SettingEntry[] enabledSettings, out string persistence, out error)) return false;
                bool enabled = bool.Parse(enabledSettings.Single().value);
                bool useProject = (bool)settingsType.GetProperty("UseProjectSettings", BindingFlags.Public | BindingFlags.Instance).GetValue(settings);
                string preferencesAfter = CaptureAnalyticsPreferencesSha256(keys);
                string fieldsAfter = CaptureAnalyticsFieldsSha256(type, analytics);
                string projectAfter = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(values));
                if (preferencesBefore != preferencesAfter || fieldsBefore != fieldsAfter || projectBefore != projectAfter || persistence != current.persistenceSha256 ||
                    !ReferenceEquals(resolver.GetField("analytics", flags).GetValue(null), analytics) ||
                    !ReferenceEquals(settingsType.GetField("loadedSettings", flags).GetValue(null), project) ||
                    !ReferenceEquals(type.GetField("settings", instance).GetValue(analytics), settings) ||
                    !ReferenceEquals(settingsType.GetField("systemSettings", flags).GetValue(null), system) ||
                    !ReferenceEquals(settingsType.GetField("projectSettings", flags).GetValue(null), project) ||
                    !Equals(type.GetField("GloballyEnabled", flags).GetValue(null), global) ||
                    !Equals(type.GetField("ConsentRequesting", instance).GetValue(analytics), requesting))
                { error = "analytics 偏好、持久配置或实例在读取期间漂移。"; return false; }
                snapshot = new AnalyticsSnapshot
                { globallyEnabled = global, consentRequested = consent, consentRequesting = requesting, enabled = enabled,
                    useProjectSettings = useProject, instanceFieldsSha256 = fieldsAfter,
                    objectIdentitySha256 = GenerateActionCommon.ComputeTextHash(string.Join("\n", new[]
                    { AnalyticsIdentity(analytics), AnalyticsIdentity(settings), AnalyticsIdentity(project), AnalyticsIdentity(system) })),
                    settingsSha256 = GenerateActionCommon.ComputeTextHash(projectAfter + preferencesAfter), persistenceSha256 = persistence };
                snapshot.sha256 = ComputeAnalyticsSnapshotSha256(snapshot);
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "analytics 只读冻结失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>只按完整强类型状态计算摘要，排除 sha256 自身，供序列化回执自洽检查和当前状态比较。</summary>
        internal static string ComputeAnalyticsSnapshotSha256(AnalyticsSnapshot snapshot)
        {
            if (snapshot == null) return null;
            return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
            {
                snapshot.globallyEnabled, snapshot.consentRequested, snapshot.consentRequesting, snapshot.enabled,
                snapshot.useProjectSettings, snapshot.instanceFieldsSha256, snapshot.objectIdentitySha256,
                snapshot.settingsSha256, snapshot.persistenceSha256,
            }));
        }

        /// <summary>核对同域不透明对象身份和完整摘要；序列化回执可验证，域重载会生成新身份而不继承证据。</summary>
        internal static bool AnalyticsSnapshotsMatch(AnalyticsSnapshot expected, AnalyticsSnapshot actual)
        {
            return expected != null && actual != null && !string.IsNullOrEmpty(expected.objectIdentitySha256) &&
                   expected.objectIdentitySha256 == actual.objectIdentitySha256 &&
                   !string.IsNullOrEmpty(expected.sha256) && expected.sha256 == actual.sha256 &&
                   expected.sha256 == ComputeAnalyticsSnapshotSha256(expected) && actual.sha256 == ComputeAnalyticsSnapshotSha256(actual);
        }

        /// <summary>捕获正规解析失败类别与可选场景事件，不保留 vendor 原日志中的仓库或凭据内容。</summary>
        internal sealed class ResolutionLogCapture : IDisposable
        {
            private readonly HashSet<string> failures = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<string> sceneEvents = new List<string>();
            private readonly bool sceneNoResolution;
            private object vendorLogger;
            private EventInfo vendorEvent;
            private FieldInfo vendorEventField;
            private Delegate originalDelegate;
            private Delegate ownedDelegate;
            private bool vendorObserverAttached;
            private volatile bool disposed;
            /// <summary>按实际接收顺序返回固定场景标记，不返回 vendor 原日志。</summary>
            /// <returns>string[]；按实际接收顺序返回固定场景标记，不返回 vendor 原日志。</returns>
            internal string[] SceneEvents { get { lock (failures) return sceneEvents.ToArray(); } }
            /// <summary>线程安全返回固定失败类别，vendor 事件可能在日志来源线程直接调用观察者。</summary>
            /// <returns>string[]；线程安全返回固定失败类别，vendor 事件可能在日志来源线程直接调用观察者。</returns>
            internal string[] FailureKinds { get { lock (failures) return failures.OrderBy(value => value, StringComparer.Ordinal).ToArray(); } }

            /// <summary>绑定一次执行窗口；场景模式才把自然跳过视为成功事件，Force Resolve 保留旧失败语义。</summary>
            /// <param name="sceneNoResolution">默认false保留Force日志观察；场景构建显式true时额外收集无解析分支的精确有序事件。</param>
            internal ResolutionLogCapture(bool sceneNoResolution = false)
            {
                this.sceneNoResolution = sceneNoResolution;
                UnityEngine.Application.logMessageReceived += Capture;
            }

            /// <summary>写前核对冻结日志合同，仅临时订阅公开事件；不调用日志器或场景回调，不改变级别与目标。</summary>
            /// <param name="expected">TryCaptureSceneLogging已冻结的完整logger事件/target/level/身份条目；订阅前重捕获必须全等。</param>
            /// <param name="token">当前只读捕获/观察的取消令牌，取消只结束自有等待/取证而不消费或清理vendor工作。</param>
            internal void AttachSceneLogger(SettingEntry[] expected, CancellationToken token)
            {
                string error = null;
                if (!sceneNoResolution || disposed || ownedDelegate != null ||
                    !TryCaptureSceneLogging(token, out SettingEntry[] current, out error) ||
                    Util.Json.Serialize(expected) != Util.Json.Serialize(current))
                    throw new InvalidOperationException("EDM 日志观察写前合同不匹配：" + error);
                Type resolver = FindType("GooglePlayServices.PlayServicesResolver");
                vendorLogger = resolver.GetField("logger", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                Type loggerType = vendorLogger?.GetType();
                vendorEvent = loggerType.GetEvent("LogMessage", BindingFlags.Public | BindingFlags.Instance);
                vendorEventField = loggerType.GetField("LogMessage", BindingFlags.NonPublic | BindingFlags.Instance);
                originalDelegate = vendorEventField.GetValue(vendorLogger) as Delegate;
                if (originalDelegate != null || expected.Single(item => item.name == "loggerIdentitySha256").value !=
                    GenerateActionCommon.ComputeTextHash(AnalyticsIdentity(vendorLogger)))
                    throw new InvalidOperationException("EDM 日志对象或原事件在订阅前漂移。");
                Type level = vendorEvent.EventHandlerType.GetMethod("Invoke").GetParameters()[1].ParameterType;
                MethodInfo bridge = GetType().GetMethod(nameof(CaptureVendor), BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(level);
                ownedDelegate = Delegate.CreateDelegate(vendorEvent.EventHandlerType, this, bridge);
                try
                {
                    vendorEvent.AddEventHandler(vendorLogger, ownedDelegate);
                    if (!Equals(vendorEventField.GetValue(vendorLogger), ownedDelegate))
                        throw new InvalidOperationException("EDM 日志事件出现非本次观察者，不能覆盖或继承。");
                    vendorObserverAttached = true;
                }
                catch { Dispose(); throw; }
            }

            /// <summary>接收 vendor 公开事件的精确泛型委托签名，只记录固定事件与失败类别，不调用 Unity 日志造成递归。</summary>
            /// <param name="message">真实日志字符串；仅与固定标记/前缀比较，不保存原文，不作为后续指令。</param>
            /// <param name="level">已绑定Google.LogLevel的真实值；只接受0..4并映射固定失败类别，不改日志级别。</param>
            private void CaptureVendor<TLevel>(string message, TLevel level) where TLevel : struct
            {
                if (disposed) return;
                int value = Convert.ToInt32(level, System.Globalization.CultureInfo.InvariantCulture);
                if (value < 0 || value > 4)
                { lock (failures) { if (!disposed) failures.Add("scene-logger-level-drift"); } return; }
                CaptureMessage(message, value == 4 ? UnityEngine.LogType.Error : value == 3 ? UnityEngine.LogType.Warning : UnityEngine.LogType.Log, true);
            }

            /// <summary>Unity 通道仍捕获其他日志器的失败；已订阅 vendor 时场景事件只取直接事件，避免同一消息双计数。</summary>
            /// <param name="message">真实日志字符串；仅与固定标记/前缀比较，不保存原文，不作为后续指令。</param>
            /// <param name="stack">Unity真实日志栈，本合同不保存或转发该字符串，仅维持真实事件签名。</param>
            /// <param name="type">Application.logMessageReceived真实UnityEngine.LogType严重级别，用于Error/Exception/Assert固定失败分类。</param>
            private void Capture(string message, string stack, UnityEngine.LogType type)
            {
                CaptureMessage(message, type, !vendorObserverAttached);
            }

            /// <summary>识别失败类别与场景有序事件，只保留固定标记；真实重复事件保留并由严格顺序合同拒绝。</summary>
            /// <param name="message">真实日志字符串；仅与固定标记/前缀比较，不保存原文，不作为后续指令。</param>
            /// <param name="type">Application.logMessageReceived真实UnityEngine.LogType严重级别，用于Error/Exception/Assert固定失败分类。</param>
            /// <param name="includeSceneEvents">内部由是否已订阅vendor公开事件推导，防同一事件从Unity+vendor两个通道重复计数。</param>
            private void CaptureMessage(string message, UnityEngine.LogType type, bool includeSceneEvents)
            {
                lock (failures)
                {
                    if (disposed) return;
                    if (type == UnityEngine.LogType.Error || type == UnityEngine.LogType.Exception || type == UnityEngine.LogType.Assert)
                        failures.Add("editor-error");
                    if (message == null) return;
                    if (sceneNoResolution)
                    {
                        if (includeSceneEvents) switch (message)
                        {
                            case "Starting auto-resolution before scene build...": sceneEvents.Add("starting"); break;
                            case "Checking for changes from previous resolution...": sceneEvents.Add("checking"); break;
                            case "No changes found, resolution skipped.": sceneEvents.Add("skipped"); break;
                            case "Android resolution succeeded.": sceneEvents.Add("succeeded"); break;
                            case "Android resolution failed.": sceneEvents.Add("failed"); break;
                        }
                        if (message == "No changes found, resolution skipped.") return;
                        foreach (string prefix in new[] { "Forcing resolution...", "Android dependencies changed from:",
                            "Failed to parse previous resolution state", "Resolving the following dependencies:",
                            "No dependencies found.", "Stale dependencies exist. Deleting assets..." })
                            if (message.StartsWith(prefix, StringComparison.Ordinal)) failures.Add("scene-resolution-branch");
                    }
                    foreach (string prefix in new[] { "Failed to ", "Unable to ", "Cannot label ", "Cannot patch POM ",
                                 "No .*", "No changes found, resolution skipped.", "Android resolution failed.",
                                 "Failed while parsing XML file ", "Ignoring invalid package specification ",
                                 "Error reading Android dependencies", "Could not find " })
                    {
                        if (message.StartsWith(prefix, StringComparison.Ordinal)) failures.Add("resolver-" + prefix.Trim());
                    }
                }
            }

            /// <summary>精确移除本次观察者并核对原事件恢复；保留并报告外部漂移，不覆盖其他委托，重复清理无副作用。</summary>
            public void Dispose()
            {
                lock (failures)
                {
                    if (disposed) return;
                    disposed = true;
                    UnityEngine.Application.logMessageReceived -= Capture;
                    if (ownedDelegate == null) return;
                    try
                    {
                        if (!Equals(vendorEventField.GetValue(vendorLogger), Delegate.Combine(originalDelegate, ownedDelegate)))
                            failures.Add("scene-logger-delegate-drift");
                        vendorEvent.RemoveEventHandler(vendorLogger, ownedDelegate);
                        if (!Equals(vendorEventField.GetValue(vendorLogger), originalDelegate)) failures.Add("scene-logger-not-restored");
                        Type resolver = FindType("GooglePlayServices.PlayServicesResolver");
                        if (!ReferenceEquals(resolver.GetField("logger", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null), vendorLogger))
                            failures.Add("scene-logger-instance-drift");
                    }
                    catch (Exception) { failures.Add("scene-logger-unsubscribe-failed"); }
                }
            }
        }

        /// <summary>只读验证正规源不变、完整状态一致、复制内容、完整预期 POM 字节及 importer 语义。</summary>
        /// <param name="before">同域执行前冻结的完整状态。</param>
        /// <param name="after">当前重新捕获的执行后状态。</param>
        /// <param name="cancellationToken">本次读取取消令牌，不消费厂商任务。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool TryValidateTemplateResolutionResult(TemplateResolutionPlan before, CurrentResolutionInputs after,
            CancellationToken cancellationToken, out string error)
        {
            error = null;
            if (before?.current == null || before.local == null || before.copies == null || after == null ||
                before.current.graphSha256 != after.graphSha256 || before.current.persistenceSha256 != after.persistenceSha256 ||
                before.current.useProjectSettings != after.useProjectSettings ||
                Util.Json.Serialize(before.current.preferences) != Util.Json.Serialize(after.preferences) ||
                Util.Json.Serialize(before.current.resolutionSettings) != Util.Json.Serialize(after.resolutionSettings))
            { error = "正规依赖图、偏好或当前解析设置发生未计划变化。"; return false; }
            try
            {
                if (!TryCaptureSceneSettingsPersistence(after, cancellationToken, out SceneSettingsPersistenceSnapshot persistence, out error) ||
                    !ForceSettingsPersistenceMatchesAfter(before.settingsPersistence, persistence))
                { error = error ?? "Force Resolve 正规同值设置回写不满足冻结预期。"; return false; }
                if (!TryCaptureAnalyticsSnapshot(after, cancellationToken, out AnalyticsSnapshot analytics, out error) ||
                    !AnalyticsSnapshotsMatch(before.analytics, analytics))
                { error = error ?? "analytics 对象、同意或偏好发生未计划变化；重载后不能继承旧对象证据。"; return false; }
                if (!GenerateActionCommon.TryVerifyArtifacts(before.local.rootArtifacts, cancellationToken, out _, out error) &&
                    before.local.rootArtifacts.Length != 0) return false;
                if (!TryCaptureLocalMavenInputs(cancellationToken, out LocalMavenInputs local, out error)) return false;
                if (Util.Json.Serialize(before.local.xmlInputs) != Util.Json.Serialize(local.xmlInputs) ||
                    Util.Json.Serialize(before.local.orderedRepositorySha256) != Util.Json.Serialize(local.orderedRepositorySha256) ||
                    Util.Json.Serialize(before.local.aarCandidates) != Util.Json.Serialize(local.aarCandidates))
                { error = "正规 XML、Maven 优先级或整仓候选已漂移。"; return false; }
                foreach (TemplateCopyInput copy in before.copies)
                {
                    string target = Path.Combine(GenerateActionCommon.ProjectRoot, copy.target);
                    string expectedSha = copy.targetIsDeleted || copy.targetSha256 == "absent" ? copy.sourceSha256 : copy.targetSha256;
                    if (!File.Exists(target) || GenerateActionCommon.ComputeFileHash(target, cancellationToken) != expectedSha)
                    { error = "正规 AAR 复制内容与冻结源不一致：" + copy.target; return false; }
                    PluginImporter importer = AssetImporter.GetAtPath(copy.target) as PluginImporter;
                    bool expectedAny = copy.targetIsDeleted || copy.targetSha256 == "absent" ? false : copy.compatibleWithAnyPlatform;
                    bool expectedAndroid = copy.targetIsDeleted || copy.targetSha256 == "absent" ? false : copy.compatibleWithAndroid;
                    if (importer == null || importer.GetCompatibleWithAnyPlatform() != expectedAny ||
                        importer.GetCompatibleWithPlatform(BuildTarget.Android) != expectedAndroid ||
                        !AssetDatabase.GetLabels(importer).Contains("gpsr", StringComparer.Ordinal))
                    { error = "正规 AAR importer 或 gpsr 标签与复制语义不一致：" + copy.target; return false; }
                    string pomPath = Path.ChangeExtension(copy.target, ".pom");
                    string pomAbsolute = Path.Combine(GenerateActionCommon.ProjectRoot, pomPath);
                    if (string.IsNullOrEmpty(copy.expectedTargetPomSha256) || !File.Exists(pomAbsolute) ||
                        GenerateActionCommon.ComputeFileHash(pomAbsolute, cancellationToken) != copy.expectedTargetPomSha256)
                    { error = "正规目标 POM 的完整字节与冻结修补结果不一致：" + pomPath; return false; }
                    AssetImporter pomImporter = AssetImporter.GetAtPath(pomPath);
                    if (pomImporter == null || !AssetDatabase.GetLabels(pomImporter).Contains("gpsr", StringComparer.Ordinal))
                    { error = "正规 POM 缺少 importer 或 gpsr 标签。"; return false; }
                }
                if (!TryLoadResolutionState(out ResolutionState state, out error) ||
                    !ResolutionInputsMatchState(after.graph, after.labeledAssetPaths, after.resolutionSettings, state, out error)) return false;
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "正规模板解析验收失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>
        /// 按当前生效配置冻结正规 Force Resolve 的模板支路，不自动调整设置来满足条件。
        /// 下载器、SDK 安装、模板启用、Jetifier 选择或网络 VCS checkout 分支需要额外合同，按具体条件阻断。
        /// </summary>
        /// <param name="cancellationToken">本次读取取消令牌，不消费厂商任务。</param>
        /// <param name="plan">已冻结的正规本地模板解析计划。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool TryCaptureTemplateResolutionPlan(CancellationToken cancellationToken,
            out TemplateResolutionPlan plan, out string error)
        {
            plan = null;
            error = null;
            if (!TryReadCurrentXmlDependencies(cancellationToken, out LocalMavenInputs local, out error) ||
                !TryCaptureCurrentResolutionInputs(cancellationToken, out CurrentResolutionInputs current, out error)) return false;
            var settings = current.resolutionSettings.ToDictionary(item => item.name, item => item.value, StringComparer.Ordinal);
            if (settings["gradleBuildEnabled"] != bool.TrueString || settings["gradleTemplateEnabled"] != bool.TrueString ||
                settings["patchMainTemplateGradle"] != bool.TrueString || settings["gradlePropertiesTemplateEnabled"] != bool.TrueString ||
                current.templateSha256.Any(item => item.value == "absent" && item.name != AndroidPluginPath + "/AndroidManifest.xml"))
            { error = "当前配置会进入模板启用或 Gradle 下载支路，缺少该支路精确外部目标合同。"; return false; }
            if (!File.ReadAllText(Path.Combine(GenerateActionCommon.ProjectRoot, AndroidPluginPath, "mainTemplate.gradle")).Contains("**DEPS**"))
            { error = "mainTemplate.gradle 缺少正规 DEPS 注入点，不能将 resolver 返回 true 当成完成。"; return false; }
            try
            {
                if (!TryCaptureAnalyticsSnapshot(current, cancellationToken, out AnalyticsSnapshot analytics, out error) ||
                    !TryCaptureSceneSettingsPersistence(current, cancellationToken, out SceneSettingsPersistenceSnapshot persistence, out error)) return false;
                // 场景回调和 Force Resolve 复用同一校验，防止后者遗漏 JDK release、版本和注入参数。
                if (!TryCaptureNoPromptToolchain(current, cancellationToken, out SettingEntry[] noPromptTools, out error)) return false;
                Version gradleVersion = Version.Parse(noPromptTools.Single(item => item.name == "gradleVersion").value);
                // 新版 Gradle 会按 ABI 直接生成排除项；避免旧版 ListZip 产生未冻结 jar 工具调用。
                if (gradleVersion < new Version(4, 1))
                { error = "旧 Gradle 会调用本地 ZIP/JAR 工具，当前模板支路工具合同不足。"; return false; }
                if (UnityEditor.VersionControl.Provider.enabled || UnityEditor.VersionControl.Provider.isActive)
                { error = "当前 VCS 可能对 POM/模板执行外部 checkout，须单独冻结该外部目标。"; return false; }
                string java = noPromptTools.Single(item => item.name == "javaBinary").value;
                SettingEntry[] toolchain = noPromptTools.Concat(new[]
                {
                    new SettingEntry { name = "vcsEnabled", value = bool.FalseString },
                    new SettingEntry { name = "vcsIsActive", value = bool.FalseString },
                }).ToArray();
                if (!TryCaptureTemplateCopyInputs(current, local, cancellationToken, out TemplateCopyInput[] copies,
                        out string[] deletes, out SettingEntry[] deletionInputs, out error)) return false;
                // 所有即将删除的本地依赖均须在同轮正规复制目标中重建；不清除尚未初始化的 SDK 输出。
                string[] targets = copies.SelectMany(copy => new[] { copy.target, Path.ChangeExtension(copy.target, ".pom") }).ToArray();
                if (current.labeledAssetPaths.Any(path => !targets.Contains(path, StringComparer.Ordinal)))
                { error = "正规 Force 会删除无法由当前源重建的 gpsr 文件，必须先补齐相应依赖来源。"; return false; }
                string[] writeSet = deletes.Concat(targets.SelectMany(path => new[] { path, path + ".meta" }))
                    .Concat(new[] { ResolutionStatePath, AndroidPluginPath + "/mainTemplate.gradle", AndroidPluginPath + "/settingsTemplate.gradle",
                        AndroidPluginPath + "/gradleTemplate.properties", "外部只读进程：" + java + " -version" })
                    .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                var directoryInputs = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (string target in targets)
                {
                    string parent = Path.GetDirectoryName(target)?.Replace('\\', '/');
                    while (!string.IsNullOrEmpty(parent) && parent != "Assets")
                    {
                        directoryInputs[parent] = Directory.Exists(Path.Combine(GenerateActionCommon.ProjectRoot, parent)) ? "directory" : "absent";
                        directoryInputs[parent + ".meta"] = HashProjectPathOrAbsent(parent + ".meta", cancellationToken);
                        parent = Path.GetDirectoryName(parent)?.Replace('\\', '/');
                    }
                }
                writeSet = writeSet.Concat(directoryInputs.Keys).Concat(persistence == null ? Array.Empty<string>() :
                    new[] { "ProjectSettings/GvhProjectSettings.xml（正规同字节写回）", "Unity EditorPrefs:GooglePlayServices.UseJetifier（已True或从缺失物化True）" })
                    .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                plan = new TemplateResolutionPlan
                { current = current, local = local, copies = copies, deletionSet = deletes, analytics = analytics, settingsPersistence = persistence,
                    deletionInputs = deletionInputs, toolchain = toolchain,
                    generatedDirectoryInputs = directoryInputs.Select(pair => new SettingEntry { name = pair.Key, value = pair.Value }).ToArray(), writeSet = writeSet };
                plan.sha256 = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(plan));
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "冻结正规模板解析计划失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name); return false; }
        }

        /// <summary>正规 Force 使用同一已审 Jetifier 成功前置，仅允许同字节项目写回及系统单键 True 物化。</summary>
        /// <param name="before">由正规设置读取冻结的执行前输入；未启用 Jetifier 时为 null。</param>
        /// <param name="after">由相同只读函数捕获的执行后输入。</param>
        /// <returns>除已明确允许的系统缺失键物化为 True 外，完整 DTO 均相同时返回 true。</returns>
        internal static bool ForceSettingsPersistenceMatchesAfter(SceneSettingsPersistenceSnapshot before, SceneSettingsPersistenceSnapshot after)
        {
            if (before == null || after == null) return before == null && after == null;
            if (!before.projectKeyExists || !before.projectKeyIsTrue || before.expectedProjectSha256 != before.projectSha256 ||
                before.expectedProjectEntriesSha256 != before.projectEntriesSha256 || before.systemKeyExists && !before.systemKeyIsTrue ||
                !after.systemKeyExists || !after.systemKeyIsTrue) return false;
            SceneSettingsPersistenceSnapshot expected = Util.Json.Deserialize<SceneSettingsPersistenceSnapshot>(Util.Json.Serialize(before));
            expected.systemKeyExists = true;
            expected.systemKeyIsTrue = true;
            return Util.Json.Serialize(expected) == Util.Json.Serialize(after);
        }

        /// <summary>冻结模板支路复制输入和精确文件/目录删除闭包，不调用复制、删除或标签 API。</summary>
        internal static bool TryCaptureTemplateCopyInputs(CurrentResolutionInputs current, LocalMavenInputs local,
            CancellationToken cancellationToken, out TemplateCopyInput[] copies, out string[] deletionSet,
            out SettingEntry[] deletionInputs, out string error)
        {
            copies = Array.Empty<TemplateCopyInput>();
            deletionSet = Array.Empty<string>();
            deletionInputs = Array.Empty<SettingEntry>();
            error = null;
            if (current?.labeledAssetPaths == null || local?.aarCandidates == null)
            { error = "模板复制输入缺少正规标签或仓库快照。"; return false; }
            // 完整 POM 预期模型只对应此已审 DLL；不能跨版本继续使用相同 XML 写出假设。
            Type localMaven = FindType("GooglePlayServices.LocalMavenRepository");
            if (!TryVerifyEdmType(localMaven, "Google.JarResolver.dll",
                    "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0", cancellationToken, out error)) return false;
            MethodInfo patchPom = localMaven.GetMethod("PatchPomFile", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(string), typeof(string) }, null);
            if (patchPom?.DeclaringType != localMaven || patchPom.ReturnType != typeof(bool))
            { error = "已审 POM 修补入口身份变化，不能使用旧版本预期模型。"; return false; }
            Type fileUtils = FindType("Google.FileUtils");
            if (!TryVerifyEdmType(fileUtils, "Google.VersionHandlerImpl.dll",
                    "c3a6d0419b41b0cbc7ad518b0cc2b311af043b5d71ed709ea3baa576b79862ce", cancellationToken, out error)) return false;
            MethodInfo replace = fileUtils.GetMethod("ReplaceBaseAssetsOrPackagesFolder", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(string), typeof(string) }, null);
            if (replace?.ReturnType != typeof(string)) { error = "正规本地 Maven 目标路径入口变化。"; return false; }
            try
            {
                var deletes = new SortedSet<string>(StringComparer.Ordinal);
                var frozen = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string labeled in current.labeledAssetPaths)
                {
                    if (!labeled.StartsWith(LocalRepositoryPath + "/", StringComparison.Ordinal) &&
                        !labeled.StartsWith(AndroidPluginPath + "/", StringComparison.Ordinal))
                    { error = "gpsr 标签包含固定受管根以外的目标，不能进入最窄模板支路。"; return false; }
                    if (!GenerateActionCommon.TryResolveProjectPath(labeled, "gpsr 删除目标", out string absolute, out error)) return false;
                    if (Directory.Exists(absolute))
                    { error = "gpsr 标签目录需要单独审查其全部删除内容，最窄支路只接受明确文件。"; return false; }
                    if (!File.Exists(absolute)) { error = "gpsr 标签包含不存在的文件，需重新查询。"; return false; }
                    deletes.Add(labeled);
                    deletes.Add(labeled + ".meta");
                    frozen[labeled] = GenerateActionCommon.ComputeFileHash(absolute, cancellationToken);
                    frozen[labeled + ".meta"] = HashProjectPathOrAbsent(labeled + ".meta", cancellationToken);
                    string parent = Path.GetDirectoryName(labeled)?.Replace('\\', '/');
                    while (!string.IsNullOrEmpty(parent) && parent != "Assets" && !Path.IsPathRooted(parent))
                    {
                        if (!GenerateActionCommon.TryResolveProjectPath(parent, "gpsr 空目录闭包", out string parentAbsolute, out error)) return false;
                        string[] entries = Directory.GetFileSystemEntries(parentAbsolute)
                            .Select(path => Path.GetFileName(path)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                        frozen[parent + "/<entries>"] = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(entries));
                        frozen[parent + ".meta"] = HashProjectPathOrAbsent(parent + ".meta", cancellationToken);
                        // EDM 只会删除已经为空的祖先，具体目录及 meta 均提前公开；不会扩展为目录通配写权限。
                        deletes.Add(parent + "/（仅删除后为空时移除目录）");
                        deletes.Add(parent + ".meta（仅移除空目录时删除）");
                        parent = Path.GetDirectoryName(parent)?.Replace('\\', '/');
                    }
                }
                var result = new List<TemplateCopyInput>();
                foreach (string source in local.aarCandidates)
                {
                    if (Path.GetExtension(source) != ".srcaar") continue;
                    if (!GenerateActionCommon.TryResolveProjectPath(source, "SRCAAR 输入", out string absolute, out error)) return false;
                    string sourceDirectory = Path.GetDirectoryName(source);
                    string targetDirectory = replace.Invoke(null, new object[] { sourceDirectory, LocalRepositoryPath }) as string;
                    if (targetDirectory == null) { error = "正规目标目录转换返回未知结构。"; return false; }
                    if (!targetDirectory.StartsWith(LocalRepositoryPath, StringComparison.Ordinal))
                        targetDirectory = Regex.Replace(sourceDirectory, @"^(.*[/\\])([^/\\]+[/\\]m2repository.*)$",
                            LocalRepositoryPath + "/$2");
                    string target = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(absolute) + ".aar").Replace('\\', '/');
                    if (!target.StartsWith(LocalRepositoryPath + "/", StringComparison.Ordinal) ||
                        !GenerateActionCommon.TryResolveProjectPath(target, "模板复制目标", out string targetAbsolute, out error))
                    { error = error ?? "SRCAAR 目标越出受管本地仓库。"; return false; }
                    if (result.Any(item => item.target == target))
                    { error = "两个 SRCAAR 竞争同一目标，需要显式选择优先级后重新 Plan。"; return false; }
                    string sourcePom = Path.ChangeExtension(source, ".pom");
                    string targetPom = Path.ChangeExtension(target, ".pom");
                    if (!GenerateActionCommon.TryResolveProjectPath(sourcePom, "SRCAAR POM", out string sourcePomAbsolute, out error) ||
                        !TryLoadXmlWithoutDtd(sourcePomAbsolute, out _, out error)) return false;
                    if (File.Exists(Path.ChangeExtension(targetAbsolute, ".pom")) &&
                        !TryLoadXmlWithoutDtd(Path.ChangeExtension(targetAbsolute, ".pom"), out _, out error)) return false;
                    bool deleted = current.labeledAssetPaths.Contains(target, StringComparer.Ordinal);
                    bool pomDeleted = current.labeledAssetPaths.Contains(targetPom, StringComparer.Ordinal);
                    string sourcePomSha256 = GenerateActionCommon.ComputeFileHash(sourcePomAbsolute, cancellationToken);
                    string targetPomSha256 = HashProjectPathOrAbsent(targetPom, cancellationToken);
                    // PatchPomFile 仅在目标缺失时复制源 POM；未被删除的既有目标必须保留其其它内容。
                    string pomBase = pomDeleted || targetPomSha256 == "absent" ? sourcePomAbsolute : Path.ChangeExtension(targetAbsolute, ".pom");
                    byte[] pomBytes = File.ReadAllBytes(pomBase);
                    string pomBaseSha256 = pomDeleted || targetPomSha256 == "absent" ? sourcePomSha256 : targetPomSha256;
                    if (ComputeByteHash(pomBytes) != pomBaseSha256)
                    { error = "冻结 POM 内容时文件漂移，须重新 Plan。"; return false; }
                    if (!TryComputePatchedPomSha256(pomBytes, out string expectedPomSha256, out error)) return false;
                    bool any = false;
                    bool android = false;
                    if (File.Exists(targetAbsolute) && !deleted)
                    {
                        PluginImporter importer = AssetImporter.GetAtPath(target) as PluginImporter;
                        if (importer == null) { error = "已存在 AAR 目标缺少正规 PluginImporter。"; return false; }
                        any = importer.GetCompatibleWithAnyPlatform();
                        android = importer.GetCompatibleWithPlatform(BuildTarget.Android);
                    }
                    result.Add(new TemplateCopyInput
                    {
                        source = source.Replace('\\', '/'),
                        sourceSha256 = GenerateActionCommon.ComputeFileHash(absolute, cancellationToken),
                        sourcePomSha256 = sourcePomSha256,
                        target = target,
                        targetSha256 = HashProjectPathOrAbsent(target, cancellationToken),
                        targetMetaSha256 = HashProjectPathOrAbsent(target + ".meta", cancellationToken),
                        targetPomSha256 = targetPomSha256,
                        expectedTargetPomSha256 = expectedPomSha256,
                        targetPomMetaSha256 = HashProjectPathOrAbsent(targetPom + ".meta", cancellationToken),
                        targetIsDeleted = deleted,
                        targetPomIsDeleted = pomDeleted,
                        compatibleWithAnyPlatform = any,
                        compatibleWithAndroid = android,
                    });
                }
                copies = result.ToArray();
                deletionSet = deletes.ToArray();
                deletionInputs = frozen.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new SettingEntry { name = pair.Key, value = pair.Value }).ToArray();
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "冻结模板复制闭包失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name); return false; }
        }

        /// <summary>
        /// 在内存中按已核 EDM4U 1.2.188 PatchPomFile 的读取、packaging 修改及写出格式计算完整预期摘要。
        /// 生产调用必须先核严格 Google.JarResolver.dll 指纹；无 packaging 或内容已为 aar 时保留原字节。
        /// 禁止 DTD，不调用 vendor、AssetDatabase 或文件写入，不形成第二套修补执行入口。
        /// </summary>
        internal static bool TryComputePatchedPomSha256(byte[] originalBytes, out string sha256, out string error)
        {
            sha256 = null;
            error = null;
            if (originalBytes == null) { error = "POM 原始字节缺失。"; return false; }
            try
            {
                var document = new XmlDocument { XmlResolver = null };
                using (var input = new MemoryStream(originalBytes, false))
                using (var text = new StreamReader(input))
                using (XmlReader reader = XmlReader.Create(text,
                           new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    document.Load(reader);
                bool changed = false;
                foreach (XmlNode packaging in document.GetElementsByTagName("packaging"))
                {
                    if (packaging.InnerText == "aar") continue;
                    packaging.InnerText = "aar";
                    changed = true;
                }
                if (!changed) { sha256 = ComputeByteHash(originalBytes); return true; }
                using (var output = new MemoryStream())
                {
                    var writerSettings = new XmlWriterSettings
                    { Indent = true, IndentChars = "  ", NewLineChars = "\n", NewLineHandling = NewLineHandling.Replace };
                    using (XmlWriter writer = XmlWriter.Create(output, writerSettings)) document.Save(writer);
                    sha256 = ComputeByteHash(output.ToArray());
                }
                return true;
            }
            catch (Exception exception)
            { error = "POM 完整预期结果无法安全计算：" + exception.GetType().Name; return false; }
        }

        /// <summary>计算完整原始字节的 SHA-256；只用于 POM 内存模型，不把 XML 或私有地址写入回执。</summary>
        private static string ComputeByteHash(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>冻结项目内路径的实际文件摘要或缺失状态，拒绝重解析点及目录冒充文件。</summary>
        private static string HashProjectPathOrAbsent(string path, CancellationToken cancellationToken)
        {
            if (!GenerateActionCommon.TryResolveProjectPath(path, "解析冻结路径", out string absolute, out string error))
                throw new InvalidOperationException(error);
            if (Directory.Exists(absolute)) throw new InvalidOperationException("解析冻结文件实际为目录。");
            return File.Exists(absolute) ? GenerateActionCommon.ComputeFileHash(absolute, cancellationToken) : "absent";
        }

        /// <summary>
        /// 使用 EDM 的公开本地仓库枚举入口取得真实候选，读取整仓文件摘要后才执行递归查询。
        /// XML 集合使用已加载正规 AndroidXmlDependencies 的纯 Regex 谓词，不调用私有 Read/FindFiles。
        /// </summary>
        internal static bool TryCaptureLocalMavenInputs(CancellationToken cancellationToken,
            out LocalMavenInputs inputs, out string error)
        {
            inputs = null;
            error = null;
            Type support = FindType("Google.JarResolver.PlayServicesSupport");
            Type local = FindType("GooglePlayServices.LocalMavenRepository");
            Type resolver = FindType("GooglePlayServices.PlayServicesResolver");
            const string resolverHash = "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0";
            if (!TryVerifyEdmType(support, "Google.JarResolver.dll", resolverHash, cancellationToken, out error) ||
                local?.Assembly != support.Assembly || resolver?.Assembly != support.Assembly)
            { error = error ?? "EDM 本地仓库公开实现身份不匹配。"; return false; }
            try
            {
                MethodInfo all = support.GetMethod("GetAllDependencies", BindingFlags.Public | BindingFlags.Static,
                    null, Type.EmptyTypes, null);
                object graph = all?.Invoke(null, null);
                object dependencies = graph?.GetType().GetProperty("Values")?.GetValue(graph);
                Type dependencyType = support.Assembly.GetType("Google.JarResolver.Dependency", false);
                Type collectionType = dependencyType == null ? null : typeof(ICollection<>).MakeGenericType(dependencyType);
                if (collectionType == null || !collectionType.IsInstanceOfType(dependencies))
                { error = "EDM 公开依赖集合结构变化。"; return false; }
                MethodInfo findRoots = local.GetMethod("FindLocalRepos", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { collectionType }, null);
                MethodInfo findAars = local.GetMethod("FindAarsInLocalRepos", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { collectionType }, null);
                if (findRoots == null || findAars == null) { error = "EDM 本地仓库公开查询入口变化。"; return false; }
                string[] roots = (findRoots.Invoke(null, new[] { dependencies }) as IEnumerable<string>)?.ToArray();
                if (roots == null || roots.Any(string.IsNullOrWhiteSpace))
                { error = "EDM 本地仓库集合包含未知路径。"; return false; }
                var artifacts = new List<GenerateActionCommon.Artifact>();
                foreach (string root in roots)
                {
                    if (!GenerateActionCommon.TryResolveProjectPath(root, "本地 Maven 仓库", out string absolute, out error)) return false;
                    // EDM 会遍历整个根。完整捕获同时拒绝任何子层重解析点，空根也需显式处理。
                    if (!Directory.Exists(absolute)) { error = "本地 Maven 仓库尚不存在，不能冻结正规枚举输入。"; return false; }
                    artifacts.Add(GenerateActionCommon.CaptureDirectory(absolute, "*", cancellationToken));
                }
                string[] aars = (findAars.Invoke(null, new[] { dependencies }) as IEnumerable<string>)?.ToArray();
                if (aars == null || aars.Any(string.IsNullOrWhiteSpace))
                { error = "EDM 本地 AAR/SRCAAR 候选结构变化。"; return false; }
                foreach (string aar in aars)
                {
                    if (!GenerateActionCommon.TryResolveProjectPath(aar, "本地原生库", out string absolute, out error)) return false;
                    if (!artifacts.Any(root => IsStrictDescendant(absolute, root.path)))
                    { error = "正规本地原生库查询越出已冻结整仓范围。"; return false; }
                }
                MethodInfo getRepos = resolver.GetMethod("GetRepos", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(IEnumerable<>).MakeGenericType(dependencyType) }, null);
                if (!(getRepos?.Invoke(null, new[] { dependencies }) is IEnumerable repositoryPairs))
                { error = "正规完整 Maven 仓库优先级入口变化。"; return false; }
                var repositoryHashes = new List<string>();
                foreach (object pair in repositoryPairs)
                {
                    string uri = pair?.GetType().GetProperty("Key")?.GetValue(pair) as string;
                    if (string.IsNullOrWhiteSpace(uri)) { error = "正规 Maven 仓库包含未知地址。"; return false; }
                    repositoryHashes.Add(GenerateActionCommon.ComputeTextHash(uri));
                }

                object xml = resolver.GetField("xmlDependencies", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                Type xmlType = support.Assembly.GetType("GooglePlayServices.AndroidXmlDependencies", false);
                Type baseType = support.Assembly.GetType("GooglePlayServices.XmlDependencies", false);
                object patternCollection = baseType?.GetField("fileRegularExpressions", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(xml);
                if (xml?.GetType() != xmlType || !(patternCollection is IEnumerable patterns))
                { error = "EDM Android XML 正规查询谓词身份变化。"; return false; }
                var regularExpressions = new List<Regex>();
                foreach (object pattern in patterns)
                {
                    if (pattern?.GetType() != typeof(Regex)) { error = "依赖 XML 查询包含未审自定义 Regex。"; return false; }
                    regularExpressions.Add((Regex)pattern);
                }
                if (regularExpressions.Count == 0) { error = "依赖 XML 查询谓词尚未初始化。"; return false; }
                var xmlInputs = new List<SettingEntry>();
                string[] searchRoots = new[] { "Assets", "Packages" }.Where(Directory.Exists).ToArray();
                string[] query = searchRoots.Length == 0 ? AssetDatabase.FindAssets("Dependencies t:TextAsset")
                    : AssetDatabase.FindAssets("Dependencies t:TextAsset", searchRoots);
                var xmlSet = new HashSet<string>();
                foreach (string guid in query)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    // 忠实采用 EDM SearchAssetDatabase 的实际路径存在筛选，不能替 vendor 补读 UPM 虚路径。
                    if ((File.Exists(path) || Directory.Exists(path)) && regularExpressions.Any(pattern => pattern.IsMatch(path)))
                        xmlSet.Add(path);
                }
                string[] xmlPaths = xmlSet.ToArray();
                foreach (string path in xmlPaths)
                {
                    if (!GenerateActionCommon.TryResolveProjectPath(path, "依赖 XML", out string absolute, out error)) return false;
                    if (!TryLoadXmlWithoutDtd(absolute, out _, out error)) return false;
                    xmlInputs.Add(new SettingEntry { name = path, value = GenerateActionCommon.ComputeFileHash(absolute, cancellationToken) });
                }
                xmlInputs.AddRange(regularExpressions.Select((pattern, index) => new SettingEntry
                { name = "queryPattern/" + index, value = GenerateActionCommon.ComputeTextHash(pattern.ToString() + "\n" + pattern.Options) }));
                inputs = new LocalMavenInputs
                { roots = roots, orderedRepositorySha256 = repositoryHashes.ToArray(), aarCandidates = aars,
                    rootArtifacts = artifacts.ToArray(), xmlInputs = xmlInputs.ToArray() };
                inputs.sha256 = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(inputs));
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "冻结本地 Maven 输入失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name); return false; }
        }

        /// <summary>以禁用 DTD 和外部解析器的标准 XML 读取检验模板解析输入，不执行 vendor 解析器。</summary>
        private static bool TryLoadXmlWithoutDtd(string path, out XmlDocument document, out string error)
        {
            document = null;
            error = null;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (XmlReader reader = XmlReader.Create(path, settings))
                {
                    document = new XmlDocument { XmlResolver = null };
                    document.Load(reader);
                }
                return true;
            }
            catch (Exception exception)
            { error = "依赖 XML/POM 无法安全读取：" + exception.GetType().Name; return false; }
        }

        /// <summary>
        /// 自动读取 EDM4U 的 18 项生效偏好、15 项状态比较输入、模板和 gpsr 集合。
        /// 只读私有实例身份与固定字段；设置通过公开 Google.ProjectSettings，构建状态通过公开 Current 读取。
        /// </summary>
        internal static bool TryCaptureCurrentResolutionInputs(CancellationToken cancellationToken,
            out CurrentResolutionInputs inputs, out string error)
        {
            inputs = null;
            error = null;
            Type dialogType = FindType("GooglePlayServices.SettingsDialog");
            Type resolverType = FindType("GooglePlayServices.PlayServicesResolver");
            const string resolverHash = "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0";
            if (!TryVerifyEdmType(dialogType, "Google.JarResolver.dll", resolverHash, cancellationToken, out error) ||
                resolverType?.Assembly != dialogType.Assembly)
            { error = error ?? "EDM 设置与解析器不属于同一正式实现。"; return false; }
            try
            {
                object settings = dialogType.GetField("projectSettings", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                var requests = new[]
                {
                    new GoogleSettingRequest { key = "GooglePlayServices.AutoResolverEnabled", defaultBoolean = true },
                    new GoogleSettingRequest { key = "GooglePlayServices.AutoResolveOnBuild", defaultBoolean = true },
                    new GoogleSettingRequest { key = "GooglePlayServices.AndroidPackageInstallationEnabled", defaultBoolean = true },
                    new GoogleSettingRequest { key = "GooglePlayServices.PackageDirectory", defaultString = AndroidPluginPath },
                    new GoogleSettingRequest { key = "GooglePlayServices.ExplodeAars", defaultBoolean = true },
                    new GoogleSettingRequest { key = "GooglePlayServices.PatchAndroidManifest", defaultBoolean = true },
                    new GoogleSettingRequest { key = "GooglePlayServices.PatchMainTemplateGradle", defaultBoolean = true },
                    new GoogleSettingRequest { key = "GooglePlayServices.PatchPropertiesTemplateGradle", defaultBoolean = true },
                    new GoogleSettingRequest { key = "GooglePlayServices.PatchSettingsTemplateGradle", defaultBoolean = true },
                    new GoogleSettingRequest { key = "GooglePlayServices.UseFullCustomMavenRepoPathWhenExport", defaultBoolean = true },
                    new GoogleSettingRequest { key = "GooglePlayServices.UseFullCustomMavenRepoPathWhenNotExport", defaultBoolean = false },
                    new GoogleSettingRequest { key = "GooglePlayServices.LocalMavenRepoDir", defaultString = LocalRepositoryPath },
                    new GoogleSettingRequest { key = "GooglePlayServices.UseJetifier", defaultBoolean = true },
                    new GoogleSettingRequest { key = "GooglePlayServices.VerboseLogging", defaultBoolean = false },
                    new GoogleSettingRequest { key = "GooglePlayServices.AutoResolutionDisabledWarning", defaultBoolean = true },
                    new GoogleSettingRequest { key = "GooglePlayServices.PromptBeforeAutoResolution", defaultBoolean = true },
                    new GoogleSettingRequest { key = "GooglePlayServices.UseGradleDaemon", defaultBoolean = false },
                    new GoogleSettingRequest { key = "GooglePlayServices.UserRejectedGradleUpgrade", defaultBoolean = false },
                };
                if (!TryReadGoogleSettings(settings, requests, cancellationToken, out SettingEntry[] preferences,
                        out string persistenceSha256, out error)) return false;
                PropertyInfo useProject = settings.GetType().GetProperty("UseProjectSettings", BindingFlags.Public | BindingFlags.Instance);
                if (useProject?.PropertyType != typeof(bool))
                { error = "Google.ProjectSettings.UseProjectSettings 公开签名变化。"; return false; }
                bool useProjectSettings = (bool)useProject.GetValue(settings);
                // 当前 EDM 将 PackageDir 固定到常量。拒绝未知可配置分支及校正型目录输入，不调用私有 Validate 方法。
                object configurable = dialogType.GetField("ConfigurablePackageDir", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                object defaultPackage = dialogType.GetField("DefaultPackageDir", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                if (!(configurable is bool configurablePackage) || configurablePackage || !Equals(defaultPackage, AndroidPluginPath) ||
                    preferences.Single(item => item.name == "GooglePlayServices.LocalMavenRepoDir").value != LocalRepositoryPath)
                { error = "当前包目录或本地 Maven 目录需要正规校正，不能进入固定目录合同。"; return false; }

                Type buildType = resolverType.GetNestedType("AndroidBuildSystemSettings", BindingFlags.NonPublic | BindingFlags.Public);
                PropertyInfo current = buildType?.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                if (current?.PropertyType != buildType) { error = "EDM 公开 Android 构建状态入口变化。"; return false; }
                object build = current.GetValue(null);
                Type abisType = FindType("GooglePlayServices.AndroidAbis");
                PropertyInfo abisCurrent = abisType?.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                if (abisType?.Assembly != resolverType.Assembly || abisCurrent?.PropertyType != abisType)
                { error = "EDM 公开 AndroidAbis.Current 入口变化。"; return false; }
                var resolution = new List<SettingEntry>();
                string[][] mappings =
                {
                    new[] { "installAndroidPackages", "GooglePlayServices.AndroidPackageInstallationEnabled" },
                    new[] { "explodeAars", "GooglePlayServices.ExplodeAars" },
                    new[] { "patchAndroidManifest", "GooglePlayServices.PatchAndroidManifest" },
                    new[] { "patchMainTemplateGradle", "GooglePlayServices.PatchMainTemplateGradle" },
                    new[] { "useFullCustomMavenRepoPathWhenExport", "GooglePlayServices.UseFullCustomMavenRepoPathWhenExport" },
                    new[] { "useFullCustomMavenRepoPathWhenNotExport", "GooglePlayServices.UseFullCustomMavenRepoPathWhenNotExport" },
                    new[] { "useJetifier", "GooglePlayServices.UseJetifier" },
                };
                resolution.AddRange(mappings.Select(pair => new SettingEntry
                { name = pair[0], value = preferences.Single(item => item.name == pair[1]).value }));
                resolution.Add(new SettingEntry { name = "packageDir", value = AndroidPluginPath });
                resolution.Add(new SettingEntry { name = "localMavenRepoDir", value = LocalRepositoryPath });
                resolution.Add(new SettingEntry { name = "bundleId", value = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android) });
                string[][] buildMappings =
                {
                    new[] { "gradleBuildEnabled", "GradleBuildEnabled" },
                    new[] { "gradleTemplateEnabled", "GradleTemplateEnabled" },
                    new[] { "gradlePropertiesTemplateEnabled", "GradlePropertiesTemplateEnabled" },
                    new[] { "projectExportEnabled", "ProjectExportEnabled" },
                };
                foreach (string[] pair in buildMappings)
                {
                    PropertyInfo property = buildType.GetProperty(pair[1], BindingFlags.Public | BindingFlags.Instance);
                    if (property?.PropertyType != typeof(bool)) { error = "EDM 构建状态公开属性变化。"; return false; }
                    resolution.Add(new SettingEntry { name = pair[0], value = property.GetValue(build).ToString() });
                }
                resolution.Add(new SettingEntry { name = "androidAbis", value = abisCurrent.GetValue(null).ToString() });
                var templates = new List<SettingEntry>();
                foreach (string file in new[] { "mainTemplate.gradle", "settingsTemplate.gradle", "gradleTemplate.properties", "AndroidManifest.xml" })
                {
                    string relative = AndroidPluginPath + "/" + file;
                    if (!GenerateActionCommon.TryResolveProjectPath(relative, "EDM 模板", out string absolute, out error)) return false;
                    templates.Add(new SettingEntry { name = relative, value = File.Exists(absolute)
                        ? GenerateActionCommon.ComputeFileHash(absolute, cancellationToken) : "absent" });
                }
                string[] labels = AssetDatabase.FindAssets("l:gpsr").Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(path => path, StringComparer.Ordinal).ToArray();
                if (labels.Any(string.IsNullOrWhiteSpace) || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
                { error = "gpsr 查询存在未知或重复路径。"; return false; }
                if (!TryCaptureDependencyGraph(out DependencyEntry[] graph, out error)) return false;
                inputs = new CurrentResolutionInputs
                {
                    preferences = preferences,
                    useProjectSettings = useProjectSettings,
                    persistenceSha256 = persistenceSha256,
                    resolutionSettings = resolution.OrderBy(item => item.name, StringComparer.Ordinal).ToArray(),
                    labeledAssetPaths = labels,
                    templateSha256 = templates.ToArray(),
                    graph = graph,
                    graphSha256 = ComputeGraphSha256(graph),
                    capturedResolverType = resolverType,
                    capturedSettingsObject = settings,
                };
                inputs.sha256 = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(inputs));
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "冻结正规 EDM 输入失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name); return false; }
        }

        /// <summary>正式 Google 设置读取规格；未指定字符串默认值时按布尔设置读取。</summary>
        internal sealed class GoogleSettingRequest
        {
            public string key;
            public bool defaultBoolean;
            public string defaultString;
        }

        /// <summary>
        /// 通过已审 Google.ProjectSettings 公开 GetBool/GetString 读取生效值，不调用设置窗口私有 getter。
        /// 正规读链可能加载 XML 到内存；磁盘设置摘要必须在读取前后相同，禁止 setter 或保存。
        /// </summary>
        internal static bool TryReadGoogleSettings(
            object projectSettings,
            GoogleSettingRequest[] requests,
            CancellationToken cancellationToken,
            out SettingEntry[] values,
            out string persistenceSha256,
            out string error)
        {
            values = Array.Empty<SettingEntry>();
            persistenceSha256 = null;
            error = null;
            Type type = projectSettings?.GetType();
            const string settingsAssemblyHash = "c3a6d0419b41b0cbc7ad518b0cc2b311af043b5d71ed709ea3baa576b79862ce";
            if (type?.FullName != "Google.ProjectSettings" || requests == null ||
                requests.Any(item => string.IsNullOrWhiteSpace(item?.key)) ||
                requests.Select(item => item.key).Distinct(StringComparer.Ordinal).Count() != requests.Length ||
                !TryVerifyEdmType(type, "Google.VersionHandlerImpl.dll", settingsAssemblyHash, cancellationToken, out error))
            {
                error = error ?? "Google 设置读取规格或正式实现身份不匹配。";
                return false;
            }
            MethodInfo readBoolean = type.GetMethod("GetBool", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(string), typeof(bool) }, null);
            MethodInfo readString = type.GetMethod("GetString", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(string), typeof(string) }, null);
            if (readBoolean?.ReturnType != typeof(bool) || readString?.ReturnType != typeof(string))
            { error = "Google.ProjectSettings 公开读取签名已变化。"; return false; }
            // 公开读取会分派 ISettings；只接受当前已审的项目内存与 EditorPrefs 实现。
            object project = type.GetField("projectSettings", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            object system = type.GetField("systemSettings", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            if (project?.GetType() != type.Assembly.GetType("Google.InMemorySettings", false) ||
                system?.GetType() != type.Assembly.GetType("Google.EditorSettings", false))
            { error = "Google 设置包含未审查的自定义 ISettings 实现。"; return false; }
            string path = Path.Combine(GenerateActionCommon.ProjectRoot, "ProjectSettings/GvhProjectSettings.xml");
            try
            {
                string before = File.Exists(path) ? GenerateActionCommon.ComputeFileHash(path, cancellationToken) : "absent";
                var result = new List<SettingEntry>();
                foreach (GoogleSettingRequest request in requests)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    object value = request.defaultString == null
                        ? readBoolean.Invoke(projectSettings, new object[] { request.key, request.defaultBoolean })
                        : readString.Invoke(projectSettings, new object[] { request.key, request.defaultString });
                    result.Add(new SettingEntry { name = request.key, value = value?.ToString() });
                }
                persistenceSha256 = File.Exists(path) ? GenerateActionCommon.ComputeFileHash(path, cancellationToken) : "absent";
                if (!string.Equals(before, persistenceSha256, StringComparison.Ordinal))
                { error = "正规 Google 设置读取期间持久化文件发生变化，计划不可复用。"; return false; }
                values = result.OrderBy(item => item.name, StringComparer.Ordinal).ToArray();
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "读取正式 Google 设置失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name); return false; }
        }

        /// <summary>核对当前 EDM 包、加载 DLL 路径及源码摘要，未知版本不能调用正规反射读取入口。</summary>
        internal static bool TryVerifyEdmType(Type type, string fileName, string sha256,
            CancellationToken cancellationToken, out string error)
        {
            error = null;
            if (type == null) { error = "EDM 类型不存在。"; return false; }
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly);
            string path = package == null ? null : Path.GetFullPath(Path.Combine(package.resolvedPath,
                "ExternalDependencyManager/Editor/1.2.188", fileName));
            if (package?.name != "com.google.external-dependency-manager" || package.version != "1.2.188" ||
                string.IsNullOrEmpty(path) || Path.GetFullPath(type.Assembly.Location) != path || !File.Exists(path) ||
                !string.Equals(GenerateActionCommon.ComputeFileHash(path, cancellationToken), sha256, StringComparison.OrdinalIgnoreCase))
            { error = "EDM 当前包、加载路径或 DLL 摘要不在已审契约内。"; return false; }
            return true;
        }

        /// <summary>
        /// 通过 EDM4U 的公开 PlayServicesSupport.GetAllDependencies 冻结当前完整依赖图。
        /// </summary>
        /// <param name="graph">完整DependencyEntry序列；spec+packageIds排序但Maven repositorySha256保留原优先顺序。</param>
        /// <param name="error">out具体拒绝或不完整原因，成功为null；不能据此自动更改环境或重放Resolve。</param>
        /// <returns>bool；通过 EDM4U 的公开 PlayServicesSupport.GetAllDependencies 冻结当前完整依赖图。</returns>
        internal static bool TryCaptureDependencyGraph(out DependencyEntry[] graph, out string error)
        {
            graph = Array.Empty<DependencyEntry>();
            error = null;
            Type supportType = FindType("Google.JarResolver.PlayServicesSupport");
            MethodInfo getAll = supportType?.GetMethod(
                "GetAllDependencies",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            if (getAll == null)
            {
                error = "当前项目未提供可用的 EDM4U PlayServicesSupport.GetAllDependencies。";
                return false;
            }

            object value;
            try
            {
                value = getAll.Invoke(null, null);
            }
            catch (Exception exception)
            {
                error = "读取 EDM4U 公开依赖图失败：" + (exception.InnerException?.Message ?? exception.Message);
                return false;
            }
            if (!(value is IEnumerable entries))
            {
                error = "EDM4U 公开依赖图返回了未知结构。";
                return false;
            }

            var result = new List<DependencyEntry>();
            foreach (object pair in entries)
            {
                object dependency = pair?.GetType().GetProperty("Value")?.GetValue(pair);
                string spec = dependency?.GetType().GetProperty("Key")?.GetValue(dependency) as string;
                if (string.IsNullOrWhiteSpace(spec))
                {
                    error = "EDM4U 依赖图包含无法识别的依赖项。";
                    return false;
                }
                result.Add(new DependencyEntry
                {
                    spec = spec,
                    repositorySha256 = ReadStringArray(dependency, "Repositories", false)
                        .Select(GenerateActionCommon.ComputeTextHash)
                        .ToArray(),
                    packageIds = ReadStringArray(dependency, "PackageIds", true),
                });
            }
            graph = result.OrderBy(item => item.spec, StringComparer.Ordinal).ToArray();
            return true;
        }

        /// <summary>
        /// 对排序后的依赖规格和 Android SDK 包标识生成稳定摘要，保留 Maven 仓库优先级顺序。
        /// </summary>
        /// <param name="graph">完整DependencyEntry序列；spec+packageIds排序但Maven repositorySha256保留原优先顺序。</param>
        /// <returns>string；对排序后的依赖规格和 Android SDK 包标识生成稳定摘要，保留 Maven 仓库优先级顺序。</returns>
        internal static string ComputeGraphSha256(IEnumerable<DependencyEntry> graph)
        {
            DependencyEntry[] canonical = (graph ?? Array.Empty<DependencyEntry>())
                .Select(item => new DependencyEntry
                {
                    spec = item?.spec,
                    repositorySha256 = (item?.repositorySha256 ?? Array.Empty<string>())
                        .ToArray(),
                    packageIds = (item?.packageIds ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                })
                .OrderBy(item => item.spec, StringComparer.Ordinal)
                .ToArray();
            return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(canonical));
        }

        /// <summary>
        /// 从项目中的 EDM4U 状态文件读取本次解析后的包、受管文件和设置快照。
        /// </summary>
        internal static bool TryLoadResolutionState(out ResolutionState state, out string error)
        {
            state = null;
            error = null;
            string absolute = Path.Combine(GenerateActionCommon.ProjectRoot, ResolutionStatePath);
            if (!File.Exists(absolute))
            {
                error = ResolutionStatePath + " 不存在。";
                return false;
            }
            try
            {
                return TryParseResolutionStateXml(File.ReadAllText(absolute), out state, out error);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                error = "读取 EDM4U 状态文件失败：" + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 严格解析 EDM4U 状态 XML，拒绝重复项、重复设置与项目外受管路径。
        /// </summary>
        internal static bool TryParseResolutionStateXml(string xml, out ResolutionState state, out string error)
        {
            state = null;
            error = null;
            XDocument document;
            try
            {
                using (var reader = XmlReader.Create(new StringReader(xml ?? string.Empty), new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                }))
                {
                    document = XDocument.Load(reader, LoadOptions.None);
                }
            }
            catch (Exception exception) when (exception is XmlException || exception is InvalidOperationException)
            {
                error = "EDM4U 状态 XML 无法解析：" + exception.Message;
                return false;
            }
            XElement root = document.Root;
            if (root?.Name.LocalName != "dependencies")
            {
                error = "EDM4U 状态 XML 缺少 dependencies 根节点。";
                return false;
            }

            string[] packages = root.Element("packages")?.Elements("package")
                .Select(element => element.Value?.Trim()).ToArray() ?? Array.Empty<string>();
            string[] files = root.Element("files")?.Elements("file")
                .Select(element => (element.Value ?? string.Empty).Trim().Replace('\\', '/')).ToArray() ?? Array.Empty<string>();
            SettingEntry[] settings = root.Element("settings")?.Elements("setting")
                .Select(element => new SettingEntry
                {
                    name = (string)element.Attribute("name"),
                    value = (string)element.Attribute("value"),
                }).ToArray() ?? Array.Empty<SettingEntry>();
            if (packages.Any(string.IsNullOrWhiteSpace) || files.Any(string.IsNullOrWhiteSpace) ||
                settings.Any(item => string.IsNullOrWhiteSpace(item.name) || item.value == null) ||
                packages.Distinct(StringComparer.Ordinal).Count() != packages.Length ||
                files.Distinct(StringComparer.Ordinal).Count() != files.Length ||
                settings.Select(item => item.name).Distinct(StringComparer.Ordinal).Count() != settings.Length)
            {
                error = "EDM4U 状态 XML 包含空值或重复项。";
                return false;
            }
            foreach (string file in files)
            {
                if (!GenerateActionCommon.TryResolveProjectPath(
                        file,
                        "EDM4U 受管文件",
                        out string absoluteFile,
                        out error)) return false;
                string localRepositoryRoot = Path.Combine(GenerateActionCommon.ProjectRoot, LocalRepositoryPath);
                string androidPluginRoot = Path.Combine(GenerateActionCommon.ProjectRoot, AndroidPluginPath);
                if (!IsStrictDescendant(absoluteFile, localRepositoryRoot) &&
                    !IsStrictDescendant(absoluteFile, androidPluginRoot))
                {
                    error = "EDM4U 受管文件必须严格位于 Assets/GeneratedLocalRepo/** 或 Assets/Plugins/Android/**：" + file;
                    return false;
                }
            }

            state = new ResolutionState
            {
                packages = packages.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                files = files.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                settings = settings.OrderBy(item => item.name, StringComparer.Ordinal).ToArray(),
            };
            if (!HasRequiredSetting(state, "localMavenRepoDir", LocalRepositoryPath) ||
                !HasRequiredSetting(state, "packageDir", AndroidPluginPath))
            {
                error = "EDM4U 的 localMavenRepoDir/packageDir 不符合 Nova 固定写入边界。";
                state = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 核对状态包集合与冻结图的 Maven 规格完全一致。
        /// </summary>
        internal static bool PackagesEqual(DependencyEntry[] graph, string[] packages, out string error)
        {
            string[] expected = (graph ?? Array.Empty<DependencyEntry>()).Select(item => item.spec)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] actual = (packages ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            bool equal = expected.SequenceEqual(actual, StringComparer.Ordinal);
            error = equal ? null : "AndroidResolverDependencies.xml 的 package 集合与冻结依赖图不一致。";
            return equal;
        }

        /// <summary>
        /// 纯输入核对正规场景回调比较的包、gpsr 标签路径与当前完整设置。
        /// 本方法不调用私有 resolver，不解析或修改标签；一致性仍须配合产物 SHA-256 验证。
        /// </summary>
        internal static bool ResolutionInputsMatchState(
            DependencyEntry[] graph,
            string[] labeledAssetPaths,
            SettingEntry[] currentSettings,
            ResolutionState state,
            out string error)
        {
            error = null;
            if (graph == null || graph.Length == 0 || labeledAssetPaths == null || currentSettings == null ||
                state?.packages == null || state.files == null || state.settings == null ||
                graph.Any(item => string.IsNullOrWhiteSpace(item?.spec)) ||
                graph.Select(item => item.spec).Distinct(StringComparer.Ordinal).Count() != graph.Length ||
                labeledAssetPaths.Any(string.IsNullOrWhiteSpace) ||
                labeledAssetPaths.Distinct(StringComparer.Ordinal).Count() != labeledAssetPaths.Length ||
                currentSettings.Any(item => string.IsNullOrWhiteSpace(item?.name) || item.value == null) ||
                currentSettings.Select(item => item.name).Distinct(StringComparer.Ordinal).Count() != currentSettings.Length ||
                !currentSettings.Select(item => item.name).OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(s_ResolutionSettingNames.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
            {
                error = "场景依赖输入必须包含非空唯一依赖图、完整 gpsr 路径集合与无重复的完整设置。";
                return false;
            }
            if (!PackagesEqual(graph, state.packages, out error)) return false;
            if (!labeledAssetPaths.OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(state.files.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
            {
                error = "当前 gpsr 标签路径集合与 EDM4U 解析状态不一致；不能允许隐式重新解析。";
                return false;
            }
            if (state.settings.Any(item => string.IsNullOrWhiteSpace(item?.name) || item.value == null) ||
                !currentSettings.OrderBy(item => item.name, StringComparer.Ordinal).Select(SettingKey)
                    .SequenceEqual(state.settings.OrderBy(item => item.name, StringComparer.Ordinal).Select(SettingKey), StringComparer.Ordinal))
            {
                error = "当前完整解析设置与 EDM4U 状态不一致；不能允许隐式重新解析。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 捕获 EDM4U 状态、全部受管文件、固定本地仓库和存在的 Android 模板摘要。
        /// </summary>
        internal static bool TryCaptureArtifacts(
            ResolutionState state,
            AgentActionExecutionContext context,
            out GenerateActionCommon.Artifact[] artifacts,
            out string error)
        {
            var result = new List<GenerateActionCommon.Artifact>();
            error = null;
            try
            {
                result.Add(GenerateActionCommon.CaptureFile(
                    Path.Combine(GenerateActionCommon.ProjectRoot, ResolutionStatePath),
                    context.CancellationToken));
                foreach (string file in state?.files ?? Array.Empty<string>())
                {
                    string absolute = Path.Combine(GenerateActionCommon.ProjectRoot, file);
                    result.Add(GenerateActionCommon.CaptureFile(absolute, context.CancellationToken));
                    if (File.Exists(absolute + ".meta"))
                    {
                        result.Add(GenerateActionCommon.CaptureFile(absolute + ".meta", context.CancellationToken));
                    }
                }

                string repository = Path.Combine(GenerateActionCommon.ProjectRoot, LocalRepositoryPath);
                bool repositoryHasManagedFiles = (state?.files ?? Array.Empty<string>()).Any(file =>
                    file.StartsWith(LocalRepositoryPath + "/", StringComparison.Ordinal));
                if (repositoryHasManagedFiles)
                {
                    result.Add(GenerateActionCommon.CaptureDirectory(repository, "*", context.CancellationToken));
                }
                foreach (string relative in new[]
                         {
                             AndroidPluginPath + "/mainTemplate.gradle",
                             AndroidPluginPath + "/settingsTemplate.gradle",
                             AndroidPluginPath + "/gradleTemplate.properties",
                             AndroidPluginPath + "/AndroidManifest.xml",
                         })
                {
                    string absolute = Path.Combine(GenerateActionCommon.ProjectRoot, relative);
                    if (File.Exists(absolute)) result.Add(GenerateActionCommon.CaptureFile(absolute, context.CancellationToken));
                }
                artifacts = result.GroupBy(item => item.path, StringComparer.Ordinal)
                    .Select(group => group.First()).OrderBy(item => item.path, StringComparer.Ordinal).ToArray();
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                artifacts = result.ToArray();
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 核对 Execute 与 Verify 读取的包、文件和设置快照完全一致。
        /// </summary>
        internal static bool StatesEqual(ResolutionState expected, ResolutionState actual)
        {
            if (expected == null || actual == null) return false;
            return (expected.packages ?? Array.Empty<string>()).SequenceEqual(actual.packages ?? Array.Empty<string>(), StringComparer.Ordinal) &&
                   (expected.files ?? Array.Empty<string>()).SequenceEqual(actual.files ?? Array.Empty<string>(), StringComparer.Ordinal) &&
                   (expected.settings ?? Array.Empty<SettingEntry>()).Select(SettingKey)
                   .SequenceEqual((actual.settings ?? Array.Empty<SettingEntry>()).Select(SettingKey), StringComparer.Ordinal);
        }

        /// <summary>
        /// 确保状态文件自身、每个 EDM4U 受管文件以及有受管本地文件时的仓库目录均进入证据集合。
        /// </summary>
        internal static bool ArtifactsCoverState(
            ResolutionState state,
            IEnumerable<GenerateActionCommon.Artifact> artifacts,
            out string error)
        {
            error = null;
            var paths = new HashSet<string>((artifacts ?? Array.Empty<GenerateActionCommon.Artifact>())
                .Select(item => item.path), StringComparer.Ordinal);
            string statePath = Path.Combine(GenerateActionCommon.ProjectRoot, ResolutionStatePath);
            if (!paths.Contains(statePath))
            {
                error = "产物证据未覆盖 EDM4U 状态文件。";
                return false;
            }
            foreach (string file in state?.files ?? Array.Empty<string>())
            {
                string absolute = Path.GetFullPath(Path.Combine(GenerateActionCommon.ProjectRoot, file));
                if (!paths.Contains(absolute))
                {
                    error = "产物证据未覆盖 EDM4U 受管文件：" + file;
                    return false;
                }
            }
            if ((state?.files ?? Array.Empty<string>()).Any(file =>
                    file.StartsWith(LocalRepositoryPath + "/", StringComparison.Ordinal)))
            {
                string repository = Path.Combine(GenerateActionCommon.ProjectRoot, LocalRepositoryPath);
                if (!paths.Contains(repository))
                {
                    error = "产物证据未覆盖 GeneratedLocalRepo 完整目录。";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 在已加载程序集里查找指定 EDM4U 类型，避免 Framework asmdef 硬依赖其裸 DLL。
        /// </summary>
        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        /// <summary>
        /// 读取 EDM4U Dependency 的公开字符串数组；仓库保留优先级顺序，集合型包标识可排序。
        /// </summary>
        private static string[] ReadStringArray(object target, string propertyName, bool sort)
        {
            IEnumerable<string> values = (target?.GetType().GetProperty(propertyName)?.GetValue(target) as IEnumerable<string> ??
                    Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value));
            return (sort ? values.OrderBy(value => value, StringComparer.Ordinal) : values).ToArray();
        }

        /// <summary>
        /// 核对 EDM4U 状态中的固定路径设置。
        /// </summary>
        private static bool HasRequiredSetting(ResolutionState state, string name, string expected)
        {
            return state.settings.Any(item => item.name == name &&
                                               string.Equals(item.value?.Replace('\\', '/'), expected, StringComparison.Ordinal));
        }

        /// <summary>
        /// 判断文件是否为指定受管根的真实后代；根目录自身不属于合法文件目标。
        /// </summary>
        private static bool IsStrictDescendant(string absoluteFile, string absoluteRoot)
        {
            string file = Path.GetFullPath(absoluteFile);
            string root = Path.GetFullPath(absoluteRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return file.StartsWith(root + Path.DirectorySeparatorChar, comparison);
        }

        /// <summary>
        /// 将设置项转换为稳定比较键。
        /// </summary>
        private static string SettingKey(SettingEntry item)
        {
            return (item?.name ?? string.Empty) + "\n" + (item?.value ?? string.Empty);
        }
    }
}
