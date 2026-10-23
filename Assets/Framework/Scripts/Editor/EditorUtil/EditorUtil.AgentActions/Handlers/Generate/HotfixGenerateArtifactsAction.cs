/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HotfixGenerateArtifactsAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   当前 Target 的 HybridCLR GenerateAll + link.xml 校验 Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using HybridCLR.Editor;
using HybridCLR.Editor.Installer;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.hotfix.generate-artifacts",
        "生成 HybridCLR 预构建产物",
        "hotfix",
        AgentActionOperationType.Generate,
        Description = "为当前构建目标生成 HybridCLR bridge、AOT generic、Il2CppDef、link.xml 等预构建产物。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite |
                  AgentActionEffect.ExternalWrite |
                  AgentActionEffect.BuildArtifact | AgentActionEffect.Destructive,
        RequiredEvidence = AgentActionEvidence.Compile | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        Locks = new[]
        {
            "unity-editor", "asset-database", "build-settings", "active-config-master",
            "hybridclr-generated-output", "hybridclr-strip-output", "link-xml-target",
            "unity-build-cache", "Library/Bee", "Library/PlayerDataCache", "android-build-inputs",
        })]
    internal sealed class HotfixGenerateArtifactsAction : AgentActionHandler<HotfixGenerateArtifactsAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string masterGuid;
            [AgentActionRequired] public string platform;
            [AgentActionRequired] public string channel;
            [AgentActionRequired] public string developMode;
            [AgentActionRequired] public string activeBuildTarget;
            public bool developmentBuild;
        }

        private sealed class State : IDisposable
        {
            private HybridClrGenerateReloadLease m_ReloadLease;
            private bool m_Disposed;
            public Request Request;
            public BuildTarget Target;
            public PlatformType Platform;
            public ChannelType Channel;
            public DevelopMode Mode;
            public string MasterPath;
            public string MasterHash;
            public string SettingsHash;
            public string[] Scenes;
            public string[] AotAssemblies;
            public OutputSet Outputs;
            public BuildActionCommon.BuildCallbackSnapshot Callbacks;
            public BuildActionCommon.PlayerSettingsSnapshot PlayerSettings;
            public HybridClrStripBuildContract.Snapshot StripBuild;
            public PlayerBuildAction.GeneratePlan WebglGenerate;
            public PlayerBuildAction.AndroidGeneratePlan AndroidGenerate;
            public bool BuildScriptsOnly;
            public bool ExportAndroidProject;
            public string BuildLocation;

            /// <summary>只在Execute真实输入复核后取得本状态唯一租约；Plan/过期清理从不获取。</summary>
            /// <param name="token">原Action取消令牌。</param>
            /// <returns>本次30分钟协作令牌，不对同步Build承诺硬中断。</returns>
            internal System.Threading.CancellationToken AcquireReloadLease(System.Threading.CancellationToken token)
            {
                if (Target != BuildTarget.WebGL && Target != BuildTarget.Android) throw new InvalidOperationException("Generate 重载租约只允许本次真实WebGL/Android typed分支。");
                if (m_Disposed || m_ReloadLease != null) throw new InvalidOperationException("Generate State 重载租约已释放或已获取，不能重复进入。");
                m_ReloadLease = HybridClrGenerateReloadLease.Acquire(token);
                return m_ReloadLease.Token;
            }

            /// <summary>由原Dispatcher在Operation持久化尝试后的finally释放；未执行的Plan没有原生副作用。</summary>
            /// <returns>无返回值；只释放本状态自己的锁，重复清理无操作。</returns>
            public void Dispose()
            {
                if (m_Disposed) return;
                m_ReloadLease?.Dispose();
                m_ReloadLease = null; m_Disposed = true;
            }
        }

        [Serializable]
        private sealed class OutputSet
        {
            public string hotUpdateDirectory;
            public string strippedAotDirectory;
            public string generatedCppDirectory;
            public string methodBridgeFile;
            public string unityVersionFile;
            public string assemblyManifestFile;
            public string aotGenericReferenceFile;
            public string linkXmlFile;
            public string generatedLinkXmlFile;
            public string strippedTempProject;
            public string il2CppBuildCache;
            public string[] sharedBuildCaches;
            public string[] androidBuildInputs;
        }

        [Serializable]
        private sealed class Receipt
        {
            public bool finalized;
            public string masterGuid;
            public string masterPath;
            public string masterHash;
            public string settingsHash;
            public string platform;
            public string channel;
            public string developMode;
            public string activeBuildTarget;
            public bool developmentBuild;
            public string[] enabledScenes;
            public string[] aotAssemblies;
            public OutputSet outputs;
            public GenerateActionCommon.Artifact[] artifacts;
            public BuildActionCommon.BuildCallbackSnapshot callbacks;
            public BuildActionCommon.PlayerSettingsSnapshot playerSettings;
            public HybridClrStripBuildContract.Snapshot stripBuildBefore;
            public HybridClrStripBuildContract.Snapshot stripBuildAfter;
            public HybridClrStripGradleScope.Observation stripBuildObservation;
            [NonSerialized, Newtonsoft.Json.JsonIgnore] public PlayerBuildAction.GeneratePlan webglGeneratePlan;
            [NonSerialized, Newtonsoft.Json.JsonIgnore] public PlayerBuildAction.GenerateEvidence webglGenerate;
            public HybridClrGenerateEvidenceStore.Reference webglEvidenceReference;
            [NonSerialized, Newtonsoft.Json.JsonIgnore] public PlayerBuildAction.AndroidGenerateEvidence androidGenerate;
            public HybridClrGenerateEvidenceStore.Reference androidEvidenceReference;
            public bool buildScriptsOnly;
            public bool exportAndroidProject;
            public string buildLocation;
            public string compilationToken;
            public string compilationPayloadHash;
        }

        /// <summary>验证生成请求的 Master GUID、配置坐标和活动构建目标文本。</summary>
        /// <param name="request">由 Action 框架绑定的生成请求；空请求会在 GUID 校验时被拒绝。</param>
        /// <param name="error">首个未通过的参数校验说明；成功时沿用解析器的输出。</param>
        /// <returns>所有必需参数均可解析时返回 true，否则返回 false；不装载 Master 或切换构建目标。</returns>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!GenerateActionCommon.TryValidateGuid(request?.masterGuid, out error)) return false;
            if (!GenerateActionCommon.TryParseCoordinate(
                    request.platform, request.channel, request.developMode,
                    out _, out _, out _, out error)) return false;
            return GenerateActionCommon.TryParseActiveBuildTarget(request.activeBuildTarget, out _, out error);
        }

        /// <summary>捕获本次 GenerateAll 的冻结输入、可能写域与恢复凭据，供执行前审阅。</summary>
        /// <param name="request">已经通过请求校验的 Master、配置坐标、活动目标和开发构建选项。</param>
        /// <param name="context">本次 Action 的执行上下文，用于传递只读捕获的取消令牌。</param>
        /// <returns>已完成的计划任务；前提齐全时为 ready，失败时保留状态、原因和可用的未完成恢复凭据。</returns>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryBuildFrozenState(request, context.CancellationToken, out State state, out string status, out string error))
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = status, Summary = error,
                    DataJson = state == null ? null : state.AndroidGenerate == null ? Util.Json.Serialize(CreateReceipt(state, Array.Empty<GenerateActionCommon.Artifact>())) :
                        AndroidHybridClrGenerateEvidenceStore.Preview(Util.Json.Serialize(CreateReceipt(state, Array.Empty<GenerateActionCommon.Artifact>())), state.AndroidGenerate),
                });
            }

            Receipt receipt = CreateReceipt(state, Array.Empty<GenerateActionCommon.Artifact>());
            string[] writes = GetWriteSet(state.Outputs).Concat(state.WebglGenerate == null ? Array.Empty<string>() : PlayerBuildAction.GetGenerateCallbackWriteSet(state.WebglGenerate)).Concat(state.Target == BuildTarget.WebGL ? new[] { "EditorApplication reload lock (one owned LockReloadAssemblies/UnlockReloadAssemblies pair; release after Operation persistence attempt)" } : Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            if (state.AndroidGenerate != null) writes = writes.Concat(new[] { new AndroidHybridClrGenerateEvidenceStore(BuildActionCommon.ProjectRoot).DirectoryPath(receipt.androidEvidenceReference),
                "EditorApplication reload lock (one owned LockReloadAssemblies/UnlockReloadAssemblies pair; release after Operation persistence attempt)",
                "UNITY_IL2CPP_PATH + _CL_ (official typed persistent transition)" }).Distinct(StringComparer.Ordinal).ToArray();
            string recovery = Util.Json.Serialize(receipt);
            string previewData;
            try
            {
                if (state.Target == BuildTarget.WebGL || state.Target == BuildTarget.Android) HybridClrGenerateEvidenceStore.RequireOperationBudget(recovery, writes);
                previewData = state.Target == BuildTarget.WebGL ? HybridClrGenerateEvidenceStore.CreatePlanPreviewData(recovery, state.WebglGenerate) : state.Target == BuildTarget.Android ? AndroidHybridClrGenerateEvidenceStore.Preview(recovery, state.AndroidGenerate) : Util.Json.Serialize(receipt);
            }
            catch (Exception budgetError) { return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = budgetError.Message }); }
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将在 {state.Target} / DevelopmentBuild={request.developmentBuild} 生成 HybridCLR 全量预构建产物，再校验 {GenerateActionCommon.ToProjectRelative(state.Outputs.linkXmlFile)}。",
                DataJson = previewData,
                State = state,
                WriteSet = writes,
                Evidence = new[]
                {
                    $"已只读冻结 Master={state.MasterPath}，坐标={state.Platform}/{state.Channel}/{state.Mode}。",
                    $"已冻结 {state.Scenes.Length} 个启用场景、activeBuildTarget={state.Target}、DevelopmentBuild={request.developmentBuild}。",
                    "Action 不切换 BuildTarget，也不复制 AOT/Game DLL、构建最终 Player 或 Bundle。",
                    "Android/WebGL StripAOT 显式 options、debug/profiler/deepProfiling 与当前设置已冻结；scripts-only 全局值不冒充显式 BuildOptions。",
                    "当前真实 WebGL 复用 Player 准确回调与完整 typed before/after/正式报告；输出拥有域只排除精确生成子树和 AOTReference 所属派生编译文件。",
                    "WebGL Execute 复核后取得一个公开域重载租约；正常 Refresh、编译和报告等待保持。完整final与Schedule/Receipt返回后，由Dispatcher持久化尝试后的State.Dispose释放自己的一个锁；30分钟仅协作取消，同步Build不能强杀。",
                    "HybridCLR 正常设置进程 UNITY_IL2CPP_PATH 与 _CL_；Receipt 绑定原值摘要与预期后值摘要，执行后核对正常转换，不盲目恢复。",
                    "WriteSet 覆盖 GenerateAll 可能写入或失效的路径；Verify 只对 Receipt 中明列的目录与文件出具产物证据。",
                },
                RecoveryPayloadJson = recovery,
            });
        }

        /// <summary>按冻结合同执行一次 GenerateAll；WebGL 先完成真实 typed 后验及官方报告等待，再建立绑定本次编译的产物凭据。</summary>
        /// <param name="state">本次 Plan 返回的完整冻结状态；类型不符或输入漂移时不进入生成。</param>
        /// <param name="context">本次操作的执行上下文，提供取消令牌以及由运行器持有的锁和恢复设施。</param>
        /// <returns>正常生成后返回等待正式编译/重载的partial；正式final写后、finalized/Schedule前再次核协作令牌。State租约留到Dispatcher持久化尝试后释放；失败保存实际材料，不重放旧操作，同步Build只能返回后观察取消。</returns>
        public override async Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen)) return AgentActionResult.Create(null, "blocked", "HybridCLR Generate 冻结状态无效。");
            bool captureSucceeded = TryBuildFrozenState(frozen.Request, context.CancellationToken, out State current, out _, out string error);
            if (!captureSucceeded || !FrozenStatesEqual(frozen, current))
            {
                string detail = captureSucceeded && string.IsNullOrEmpty(error) ? DescribeFrozenStateMismatch(frozen, current) : error;
                return AgentActionResult.Create(null, "blocked", "HybridCLR Generate 上下文已漂移，请重新 Plan：" + detail);
            }

            PlayerBuildAction.GenerateEvidence webglEvidence = null;
            PlayerBuildAction.AndroidGenerateEvidence androidEvidence = null;
            bool finalWriteAttempted = false;
            HybridClrGenerateEvidenceStore.Reference webglReference = null;
            HybridClrGenerateEvidenceStore evidenceStore = frozen.Target == BuildTarget.WebGL ? new HybridClrGenerateEvidenceStore(BuildActionCommon.ProjectRoot) : null;
            AndroidHybridClrGenerateEvidenceStore androidStore = frozen.Target == BuildTarget.Android ? new AndroidHybridClrGenerateEvidenceStore(BuildActionCommon.ProjectRoot) : null;
            HybridClrGenerateEvidenceStore.Reference androidReference = null;
            System.Threading.CancellationToken executionToken = context.CancellationToken;
            try
            {
                if (frozen.Target == BuildTarget.WebGL || frozen.Target == BuildTarget.Android) executionToken = frozen.AcquireReloadLease(context.CancellationToken);
                executionToken.ThrowIfCancellationRequested();
                HybridClrStripGradleScope.Observation stripObservation = null;
                if (frozen.Target == BuildTarget.WebGL)
                {
                    webglReference = evidenceStore.WriteBefore(frozen.WebglGenerate, evidenceStore.Plan(frozen.WebglGenerate));
                    webglEvidence = await PlayerBuildAction.ExecuteHybridClrGenerateAsync(frozen.WebglGenerate, frozen.StripBuild.explicitBuildOptions, executionToken);
                    if (!webglEvidence.complete || webglEvidence.error != null || webglEvidence.cancelled || !webglEvidence.failureStateValid || string.IsNullOrEmpty(webglEvidence.preCompilationCertificate)) throw new InvalidOperationException(webglEvidence.error ?? "WebGL Generate adapter 未完整完成。");
                }
                else if (frozen.Target == BuildTarget.Android)
                {
                    androidReference = androidStore.WriteBefore(frozen.AndroidGenerate, androidStore.Plan(frozen.AndroidGenerate));
                    androidEvidence = await PlayerBuildAction.ExecuteAndroidHybridClrGenerateAsync(frozen.AndroidGenerate, executionToken);
                    if (!androidEvidence.complete || androidEvidence.error != null || androidEvidence.cancelled || string.IsNullOrEmpty(androidEvidence.certificate))
                        throw new InvalidOperationException(androidEvidence.error ?? "Android Generate真实typed执行未完成。");
                    stripObservation = androidEvidence.gradle;
                }
                else EditorUtil.HybridCLR.GenerateAll();
                executionToken.ThrowIfCancellationRequested();
                if (frozen.Target != BuildTarget.WebGL && frozen.Target != BuildTarget.Android) EditorUtil.HybridCLR.ValidateLinkXml();

                if (!TryBuildFrozenState(frozen.Request, executionToken, out State after, out _, out error, frozen.Target != BuildTarget.WebGL && frozen.Target != BuildTarget.Android) ||
                    !FrozenStatesEqual(frozen, after, true))
                {
                    throw new InvalidOperationException("GenerateAll 执行后上下文或临时构建设置未保持冻结值：" + error);
                }

                GenerateActionCommon.Artifact[] artifacts = CaptureArtifacts(frozen.Outputs, new AgentActionExecutionContext(context.ConfirmationToken, executionToken));
                ValidateMethodBridge(frozen.Outputs.methodBridgeFile, frozen.Request.developmentBuild);
                ValidateLinkXml(frozen.Outputs.linkXmlFile, frozen.AotAssemblies);
                ValidateLinkXml(frozen.Outputs.generatedLinkXmlFile, Array.Empty<string>());
                Receipt receipt = CreateReceipt(frozen, artifacts);
                receipt.stripBuildAfter = after.StripBuild;
                receipt.stripBuildObservation = stripObservation;
                receipt.webglGenerate = webglEvidence;
                receipt.androidGenerate = androidEvidence;
                if (frozen.Target == BuildTarget.WebGL)
                {
                    finalWriteAttempted = true;
                    if (!evidenceStore.TryWriteFinal(webglReference, webglEvidence, out HybridClrGenerateEvidenceStore.Reference saved, out string saveError)) throw new InvalidOperationException(saveError);
                    webglReference = saved;
                    receipt.webglEvidenceReference = saved;
                }
                if (frozen.Target == BuildTarget.Android)
                {
                    finalWriteAttempted = true;
                    if (!androidStore.TryWriteFinal(androidReference, androidEvidence, out HybridClrGenerateEvidenceStore.Reference saved, out string saveError)) throw new InvalidOperationException(saveError);
                    androidReference = saved; receipt.androidEvidenceReference = saved;
                }
                if (frozen.Target == BuildTarget.WebGL || frozen.Target == BuildTarget.Android) executionToken.ThrowIfCancellationRequested();
                receipt.finalized = true;
                receipt.compilationToken = Guid.NewGuid().ToString("N");
                receipt.compilationPayloadHash = ComputeCompilationPayloadHash(receipt);
                GeneratedExportCompilationState.Schedule(receipt.compilationToken, receipt.compilationPayloadHash);
                AgentActionResult result = AgentActionResult.Create(null, "partial", "HybridCLR generate -> validate 已执行，等待 Unity 稳定后只读 Verify。" );
                result.ReceiptJson = Util.Json.Serialize(receipt);
                result.DataJson = result.ReceiptJson;
                result.Artifacts.AddRange(artifacts.Select(artifact => artifact.path));
                if (webglReference != null) result.Artifacts.AddRange(evidenceStore.Artifacts(webglReference));
                if (androidReference != null) result.Artifacts.AddRange(androidStore.Artifacts(androidReference));
                result.Evidence.Add("Execute 仅一次调用 GenerateAll 后调用 ValidateLinkXml；异常后不会自动重放。" );
                return result;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException) || frozen.Target == BuildTarget.WebGL || frozen.Target == BuildTarget.Android)
            {
                bool maySaveFailureFinal = PrepareFailureFinalEvidence(finalWriteAttempted, webglEvidence, exception);
                string phase = frozen.Target == BuildTarget.WebGL && webglReference == null ? "HybridCLR 写前证据未保存且尚未进入生成：" : frozen.Target == BuildTarget.WebGL && webglEvidence?.chainEntered != true ? "HybridCLR 已保存写前证据但尚未进入生成，准备未完成：" : "HybridCLR 已进入生成，后续未完成：";
                AgentActionResult failed = AgentActionResult.Create(null, "partial", phase + exception.Message);
                Receipt partialReceipt = CreateReceipt(frozen, Array.Empty<GenerateActionCommon.Artifact>());
                partialReceipt.webglGenerate = webglEvidence;
                partialReceipt.androidGenerate = androidEvidence;
                if (androidReference != null)
                {
                    partialReceipt.androidEvidenceReference = androidReference;
                    if (!finalWriteAttempted)
                    {
                        androidEvidence = androidEvidence ?? new PlayerBuildAction.AndroidGenerateEvidence { before = frozen.AndroidGenerate };
                        androidEvidence.complete = false; androidEvidence.certificate = null; androidEvidence.error = exception.ToString();
                        finalWriteAttempted = true;
                        if (androidStore.TryWriteFinal(androidReference, androidEvidence, out HybridClrGenerateEvidenceStore.Reference saved, out string saveError)) partialReceipt.androidEvidenceReference = saved;
                        else failed.Warnings.Add(saveError);
                    }
                    failed.Artifacts.AddRange(androidStore.Artifacts(partialReceipt.androidEvidenceReference));
                }
                if (webglReference != null)
                {
                    partialReceipt.webglEvidenceReference = webglReference;
                    if (maySaveFailureFinal)
                    {
                        finalWriteAttempted = true;
                        if (evidenceStore.TryWriteFinal(webglReference, webglEvidence, out HybridClrGenerateEvidenceStore.Reference saved, out string saveError)) partialReceipt.webglEvidenceReference = saved;
                        else { failed.Warnings.Add(saveError); failed.Message += DescribeFinalEvidenceSaveFailure(saveError); }
                    }
                    else if (finalWriteAttempted)
                        failed.Warnings.Add(webglReference.finalWritten ? "完整final已保存为不可变的编译前历史；本次外层收尾未完成，partial Receipt不升级finalized，不改写该文件。" : "本次final保存已尝试但未完成；保留原before，不再次写final或重放生成。");
                    failed.Artifacts.AddRange(evidenceStore.Artifacts(partialReceipt.webglEvidenceReference));
                }
                failed.ReceiptJson = Util.Json.Serialize(partialReceipt);
                failed.DataJson = failed.ReceiptJson;
                failed.Artifacts.AddRange(GetWriteSet(frozen.Outputs));
                if (frozen.Target == BuildTarget.WebGL && executionToken.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
                    failed.Warnings.Add("本次30分钟协作期限已取消；同步原生Build只能返回后观察，未强杀或由计时器提前解锁。");
                failed.Warnings.Add("保留可能已写入的生成物与缓存供归属核查；不会自动重放、删除已有目录或将旧产物升级为成功。");
                return failed;
            }
            finally
            {
                // Android/其它原同步分支保留旧恢复；WebGL 的同步 vendor finally 已恢复，跨 await 不覆盖用户后续设置。
                // 域已卸载时不能依靠本执行栈 finally；恢复凭据仍只能只读验证，不能重放或假称恢复已发生。
                if (frozen.Target != BuildTarget.WebGL && frozen.Target != BuildTarget.Android)
                {
                    EditorUserBuildSettings.buildScriptsOnly = frozen.BuildScriptsOnly;
                    EditorUserBuildSettings.exportAsGoogleAndroidProject = frozen.ExportAndroidProject;
                    EditorUserBuildSettings.SetBuildLocation(frozen.Target, frozen.BuildLocation);
                }
            }
        }

        /// <summary>仅尚未尝试final时准备一次实际失败材料；已有保存尝试的编译前历史完整保持，不改写或重试。</summary>
        /// <param name="finalWriteAttempted">本次是否已进入过TryWriteFinal，须在第一次调用前置true，与是否已成功保存不同。</param>
        /// <param name="evidence">本次真实执行材料；无材料时不补造。纯fixture不代表Unity观察。</param>
        /// <param name="failure">外层捕获的真实收尾/取消/保存/调度异常。</param>
        /// <returns>仅第一次可将失败材料写入final时为true；已有尝试或无材料为false且不更改旧材料。</returns>
        internal static bool PrepareFailureFinalEvidence(bool finalWriteAttempted, PlayerBuildAction.GenerateEvidence evidence, Exception failure)
        {
            if (failure == null) throw new ArgumentNullException(nameof(failure));
            if (finalWriteAttempted || evidence == null) return false;
            evidence.complete = false; evidence.preCompilationCertificate = null;
            evidence.cancelled |= failure is OperationCanceledException || failure is AggregateException aggregate && aggregate.Flatten().InnerExceptions.Any(error => error is OperationCanceledException);
            evidence.error = evidence.error == null ? failure.ToString() : evidence.error + "\n外层收尾失败：" + failure;
            return true;
        }

        /// <summary>把同一次实际final保存失败原因作为有界诊断保留到原Operation消息；不覆盖原生成失败，也不重试保存或序列化执行材料。</summary>
        /// <param name="saveError">唯一TryWriteFinal实际返回的同一个错误；null明确缺失，绝不补造原因。</param>
        /// <returns>至多4KiB UTF8附加消息；完整合法错误保原文及完整长度/SHA，超限整份省略正文并明确标识，格式异常保安全不完整标记。</returns>
        private static string DescribeFinalEvidenceSaveFailure(string saveError)
        {
            const string prefix = "\nGenerate final 证据保存失败诊断：";
            try
            {
                var utf8 = new System.Text.UTF8Encoding(false, true);
                var diagnostic = new Newtonsoft.Json.Linq.JObject { ["diagnosticOnly"] = true, ["finalWriteFailed"] = true, ["errorMissing"] = saveError == null,
                    ["rawUtf8Bytes"] = saveError == null ? (Newtonsoft.Json.Linq.JToken)Newtonsoft.Json.Linq.JValue.CreateNull() : utf8.GetByteCount(saveError),
                    ["rawSha256"] = saveError == null ? null : GenerateActionCommon.ComputeTextHash(saveError), ["errorOmitted"] = false, ["error"] = saveError };
                string output = prefix + diagnostic.ToString(Newtonsoft.Json.Formatting.None);
                if (utf8.GetByteCount(output) <= 4 * 1024) return output;
                diagnostic["error"] = null; diagnostic["errorOmitted"] = true;
                return prefix + diagnostic.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (Exception error) when (error is ArgumentException || error is Newtonsoft.Json.JsonException)
            {
                return prefix + new Newtonsoft.Json.Linq.JObject { ["diagnosticOnly"] = true, ["finalWriteFailed"] = true, ["errorOmitted"] = true, ["diagnosticIncomplete"] = true, ["diagnosticErrorType"] = error.GetType().FullName }.ToString(Newtonsoft.Json.Formatting.None);
            }
        }

        /// <summary>核对正式凭据的编译绑定、冻结上下文、精确产物和生成语义，不重新执行生成。</summary>
        /// <param name="receiptJson">本次 Execute 保存的正式凭据 JSON；未完成或漂移的凭据不能升级为成功。</param>
        /// <param name="context">当前只读验证上下文，向重新捕获和产物校验传递取消令牌。</param>
        /// <returns>已完成的验证结果任务；结构无效时为 blocked，证据不足时为 partial，全部检查通过时仅对明列产物返回 success。</returns>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            try { receipt = Util.Json.Deserialize<Receipt>(receiptJson); }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "HybridCLR Generate Receipt 无法解析：" + exception.Message));
            }
            if (receipt == null || receipt.outputs == null || receipt.artifacts == null)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "HybridCLR Generate Receipt 不完整。"));
            }
            if (!receipt.finalized || receipt.activeBuildTarget != BuildTarget.WebGL.ToString() && receipt.activeBuildTarget != BuildTarget.Android.ToString() && receipt.callbacks == null || receipt.playerSettings == null ||
                string.IsNullOrEmpty(receipt.masterHash) || string.IsNullOrEmpty(receipt.settingsHash) ||
                string.IsNullOrEmpty(receipt.outputs.generatedLinkXmlFile) ||
                receipt.activeBuildTarget == BuildTarget.WebGL.ToString() && (receipt.webglEvidenceReference?.finalWritten != true || receipt.stripBuildBefore == null || receipt.stripBuildAfter == null) ||
                receipt.activeBuildTarget == BuildTarget.Android.ToString() && (receipt.stripBuildBefore == null || receipt.stripBuildAfter == null ||
                    receipt.androidEvidenceReference?.finalWritten != true ||
                    !HybridClrStripGradleScope.IsComplete(receipt.stripBuildObservation, receipt.outputs.strippedTempProject)))
                return Task.FromResult(AgentActionResult.Create(null, "partial", "HybridCLR Generate Receipt 尚无完整执行后上下文和产物证据。"));
            if (receipt.compilationPayloadHash != ComputeCompilationPayloadHash(receipt))
                return Task.FromResult(AgentActionResult.Create(null, "partial", "HybridCLR Receipt 的编译载荷已漂移。"));
            if (!GeneratedExportCompilationState.TryVerify(receipt.compilationToken, receipt.compilationPayloadHash, true, out string compileError))
                return Task.FromResult(AgentActionResult.Create(null, "partial", "HybridCLR 生成后的本次编译与重载证据不足：" + compileError));
            if (receipt.activeBuildTarget == BuildTarget.WebGL.ToString())
            {
                var store = new HybridClrGenerateEvidenceStore(BuildActionCommon.ProjectRoot);
                if (!store.TryRead(receipt.webglEvidenceReference, out receipt.webglGenerate, out string storedError))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", storedError));
                receipt.webglGeneratePlan = receipt.webglGenerate.before;
            }
            if (receipt.activeBuildTarget == BuildTarget.Android.ToString())
            {
                var store = new AndroidHybridClrGenerateEvidenceStore(BuildActionCommon.ProjectRoot);
                if (!store.TryRead(receipt.androidEvidenceReference, out receipt.androidGenerate, out string storedError))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", storedError));
            }
            var request = new Request
            {
                masterGuid = receipt.masterGuid, platform = receipt.platform, channel = receipt.channel,
                developMode = receipt.developMode, activeBuildTarget = receipt.activeBuildTarget, developmentBuild = receipt.developmentBuild,
            };
            if (!TryBuildFrozenState(request, context.CancellationToken, out State current, out _, out string stateError, receipt.activeBuildTarget != BuildTarget.WebGL.ToString() && receipt.activeBuildTarget != BuildTarget.Android.ToString()) ||
                current.MasterPath != receipt.masterPath || current.MasterHash != receipt.masterHash || current.SettingsHash != receipt.settingsHash ||
                Util.Json.Serialize(current.Callbacks) != Util.Json.Serialize(receipt.callbacks) ||
                !BuildActionCommon.PlayerSettingsEqual(receipt.playerSettings, current.PlayerSettings) ||
                (current.Target == BuildTarget.Android || current.Target == BuildTarget.WebGL) && (!HybridClrStripBuildContract.AfterEqual(receipt.stripBuildBefore, current.StripBuild) ||
                    !HybridClrStripBuildContract.BeforeEqual(receipt.stripBuildAfter, current.StripBuild)) ||
                current.BuildScriptsOnly != receipt.buildScriptsOnly || current.ExportAndroidProject != receipt.exportAndroidProject ||
                current.BuildLocation != receipt.buildLocation || !current.Scenes.SequenceEqual(receipt.enabledScenes ?? Array.Empty<string>(), StringComparer.Ordinal) ||
                !current.AotAssemblies.SequenceEqual(receipt.aotAssemblies ?? Array.Empty<string>(), StringComparer.Ordinal) ||
                Util.Json.Serialize(current.Outputs) != Util.Json.Serialize(receipt.outputs))
                return Task.FromResult(AgentActionResult.Create(null, "partial", "HybridCLR 执行后冻结上下文、回调或临时设置已漂移：" + stateError));
            GenerateSceneVisibilityContract.CompilationObservation sceneVisibilityCompilation = null;
            if (current.Target == BuildTarget.Android && !PlayerBuildAction.VerifyAndroidHybridClrGenerate(receipt.androidGenerate, context.CancellationToken, receipt.compilationToken, receipt.compilationPayloadHash, out string androidError))
                return Task.FromResult(AgentActionResult.Create(null, "partial", androidError));
            if (current.Target == BuildTarget.WebGL && (!PlayerBuildAction.VerifyHybridClrGenerateObserved(receipt.webglGenerate, receipt.stripBuildBefore.explicitBuildOptions, context.CancellationToken, out string adapterError, receipt.compilationToken, receipt.compilationPayloadHash, out sceneVisibilityCompilation) ||
                Util.Json.Serialize(receipt.webglGeneratePlan) != Util.Json.Serialize(receipt.webglGenerate.before)))
            {
                AgentActionResult partial = AgentActionResult.Create(null, "partial", adapterError ?? "WebGL Generate 的正式前态未绑定当前回执。");
                if (sceneVisibilityCompilation != null)
                {
                    try { partial.Evidence.Add("nova-generate-svs-postcompile-v1:" + GenerateSceneVisibilityContract.EncodeCompilationObservation(sceneVisibilityCompilation, receipt.webglEvidenceReference)); }
                    catch (Exception exception) { partial.Warnings.Add("本次SVS完整后验未能返回：" + exception.Message); }
                }
                return Task.FromResult(partial);
            }
            if (!GenerateActionCommon.TryVerifyArtifacts(receipt.artifacts, context.CancellationToken, out GenerateActionCommon.Artifact[] actual, out string artifactError))
            {
                AgentActionResult partial = AgentActionResult.Create(null, "partial", "HybridCLR 生成产物验证未通过：" + artifactError);
                partial.DataJson = Util.Json.Serialize(actual);
                return Task.FromResult(partial);
            }
            string[] exactOutputs = GetExactOutputs(receipt.outputs);
            if (actual.Length != exactOutputs.Length || actual.Select(artifact => artifact.path).Distinct(StringComparer.Ordinal).Count() != exactOutputs.Length)
                return Task.FromResult(AgentActionResult.Create(null, "partial", "HybridCLR Receipt 的产物集合不是精确唯一输出集合。"));
            foreach (string output in exactOutputs)
            {
                if (!GenerateActionCommon.TryResolveProjectPath(output, "Receipt output", out string fullOutput, out string outputError) ||
                    !actual.Any(artifact => string.Equals(artifact.path, fullOutput, StringComparison.Ordinal)))
                {
                    return Task.FromResult(AgentActionResult.Create(null, "partial", outputError ?? "Receipt 产物集合未覆盖精确输出：" + output));
                }
            }

            try
            {
                ValidateMethodBridge(receipt.outputs.methodBridgeFile, receipt.developmentBuild);
                ValidateLinkXml(receipt.outputs.linkXmlFile, receipt.aotAssemblies ?? Array.Empty<string>());
                ValidateLinkXml(receipt.outputs.generatedLinkXmlFile, Array.Empty<string>());
            }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "HybridCLR 生成语义验证未通过：" + exception.Message));
            }

            AgentActionResult success = AgentActionResult.Create(null, "success", "HybridCLR Receipt 明列的精确唯一目录/文件、SHA-256、MethodBridge DEVELOPMENT、生成 link.xml 与 Master AOT 条目已只读核对。" );
            success.EvidenceKinds = AgentActionEvidence.Compile | AgentActionEvidence.Artifact;
            success.DataJson = Util.Json.Serialize(actual);
            if (current.Target == BuildTarget.WebGL)
            {
                try { success.Evidence.Add("nova-generate-svs-postcompile-v1:" + GenerateSceneVisibilityContract.EncodeCompilationObservation(sceneVisibilityCompilation, receipt.webglEvidenceReference)); }
                catch (Exception exception) { return Task.FromResult(AgentActionResult.Create(null, "partial", "Generate SVS 完整编译后证据不能返回：" + exception.Message)); }
            }
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path));
            if (receipt.webglEvidenceReference != null) success.Artifacts.AddRange(new HybridClrGenerateEvidenceStore(BuildActionCommon.ProjectRoot).Artifacts(receipt.webglEvidenceReference));
            success.Evidence.Add("Verify 只读；不会重新调用 GenerateAll、ValidateLinkXml 或切换 BuildTarget。" );
            success.Warnings.Add("generatedCppDirectory 仅核验 MethodBridge.cpp、UnityVersion.h 与 AssemblyManifest.cpp；未宣称整个目录完整。" );
            success.Warnings.Add("il2CppBuildCache 仅因 GenerateAll 可删除/失效而纳入 WriteSet，不做存在性或完整性核验。" );
            success.Warnings.Add("该证据不代表最终 Player、Bundle、运行时或设备验证成功。" );
            return Task.FromResult(success);
        }

        /// <summary>捕获 Master、场景、输出角色及真实 StripAOT 输入，并检查生成前提。</summary>
        /// <param name="request">已经过入口校验的 Master、配置坐标、活动目标和开发构建请求。</param>
        /// <param name="cancellationToken">传递给源码、程序集和资产摘要捕获的本次取消令牌。</param>
        /// <param name="state">可冻结的输入和输出快照；失败时可能保留诊断材料，不可据此执行。</param>
        /// <param name="status">失败前提的结果类别；成功时由调用方另行建立 ready 计划。</param>
        /// <param name="error">首个不满足的前提说明；无法捕获输入时保留具体原因。</param>
        /// <param name="captureGenerateCallbacks">Plan 和执行前默认为 true；WebGL 执行后和 Verify 使用已绑定真实回执的独立 typed 验证，不重建写前计划。</param>
        /// <returns>全部前提均可冻结时返回 true，否则返回 false；共享读取过程的异常可能继续传播。</returns>
        private static bool TryBuildFrozenState(
            Request request,
            System.Threading.CancellationToken cancellationToken,
            out State state,
            out string status,
            out string error,
            bool captureGenerateCallbacks = true)
        {
            state = null;
            status = "blocked";
            error = null;
            if (GeneratedExportCompilationState.IsPending())
            {
                error = "已有代码导出/生成编译尚未完成；请等待稳定后重新 Plan。";
                return false;
            }
            GenerateActionCommon.TryParseCoordinate(request.platform, request.channel, request.developMode,
                out PlatformType platform, out ChannelType channel, out DevelopMode mode, out _);
            GenerateActionCommon.TryParseActiveBuildTarget(request.activeBuildTarget, out BuildTarget target, out _);
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                error = $"请求 Target={target} 与 activeBuildTarget={EditorUserBuildSettings.activeBuildTarget} 不一致；Action 不切换 Target。";
                return false;
            }
            if (EditorUserBuildSettings.development != request.developmentBuild)
            {
                error = $"请求 DevelopmentBuild={request.developmentBuild} 与 EditorUserBuildSettings.development={EditorUserBuildSettings.development} 不一致。";
                return false;
            }
            UnityEditor.Build.NamedBuildTarget namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target) ||
                PlayerSettings.GetScriptingBackend(namedTarget) != ScriptingImplementation.IL2CPP)
            {
                error = "目标平台模块必须已安装且 ScriptingBackend 已是 IL2CPP；Action 不自动修改 PlayerSettings。";
                return false;
            }
            if (!SettingsUtil.Enable || !new InstallerController().HasInstalledHybridCLR())
            {
                status = "not_applicable";
                error = "当前项目未启用或未初始化 HybridCLR。";
                return false;
            }

            ConfigMasterSO master = GenerateActionCommon.ResolveMaster(request.masterGuid, out string masterPath);
            if (master == null)
            {
                error = "masterGuid 未解析到 ConfigMasterSO。";
                return false;
            }
            if (!GenerateActionCommon.TryValidateActiveMasterBinding(request.masterGuid, masterPath, out error)) return false;
            if (!GenerateActionCommon.CoordinateMatches(master, platform, channel, mode))
            {
                error = $"请求坐标与激活 ConfigMaster 当前坐标 {master.CurrentPlatform}/{master.CurrentChannel}/{master.CurrentDevelopMode} 不一致。";
                return false;
            }

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0 || scenes.Any(path => string.IsNullOrWhiteSpace(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null))
            {
                error = "Build Settings 没有完整的启用场景闭包，GenerateAll 会无法诚实验证。";
                return false;
            }

            EditorUtil.Config.DimensionalResolver.HybridCLRResult hybrid =
                EditorUtil.Config.DimensionalResolver.ResolveHybridCLR(master, platform, channel, mode);
            string configLink = string.IsNullOrEmpty(hybrid.LinkXmlTargetPath) ? "Assets/link.xml" : hybrid.LinkXmlTargetPath;
            if (!GenerateActionCommon.TryResolveProjectPath(configLink, "LinkXmlTargetPath", out string configLinkFull, out error)) return false;
            string settingsLinkRaw = Path.Combine(Application.dataPath, SettingsUtil.HybridCLRSettings.outputLinkFile ?? string.Empty);
            if (!GenerateActionCommon.TryResolveProjectPath(settingsLinkRaw, "HybridCLR outputLinkFile", out string settingsLinkFull, out error)) return false;
            // HybridCLR 生成引用保留表，Framework 随后在 Master 路径补全 AOT 条目；两份文件可合法独立。
            if (!TryResolveOutputs(target, configLinkFull, settingsLinkFull, out OutputSet outputs, out error)) return false;
            string[] aotAssemblies = (hybrid.AotMetadataDlls ?? new List<DllMasterAssetEntry>())
                .Select(entry => StripDllSuffix(entry.AssetLocation)).Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string masterHash = GenerateActionCommon.ComputeFileHash(Path.Combine(GenerateActionCommon.ProjectRoot, masterPath), cancellationToken);
            HybridClrStripBuildContract.Snapshot stripBuild = null;
            if (target == BuildTarget.Android || target == BuildTarget.WebGL)
            {
                try
                {
                    stripBuild = target == BuildTarget.Android ? HybridClrStripBuildContract.CaptureAndroid() : HybridClrStripBuildContract.CaptureWebGL();
                    if (stripBuild.methodBridgeCheckDisabled)
                    {
                        error = "HybridCLR 检查开关已有 true 状态；官方 finally 固定 false，不能覆盖其他操作状态。";
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    error = "StripAOT 输入合同无法冻结：" + exception.Message;
                    return false;
                }
            }
            PlayerBuildAction.GeneratePlan webglGenerate = null;
            PlayerBuildAction.AndroidGeneratePlan androidGenerate = null;
            if (target == BuildTarget.WebGL && captureGenerateCallbacks)
            {
                try { webglGenerate = PlayerBuildAction.CaptureForHybridClrGenerate(masterPath, outputs.linkXmlFile, scenes, cancellationToken); }
                catch (Exception exception) when (!(exception is OperationCanceledException)) { error = "WebGL Generate 输入与回调无法完整冻结：" + exception.Message; return false; }
            }
            if (target == BuildTarget.Android && captureGenerateCallbacks)
            {
                try { androidGenerate = PlayerBuildAction.CaptureForAndroidHybridClrGenerate(masterPath, outputs.linkXmlFile, scenes, cancellationToken); }
                catch (Exception exception) when (!(exception is OperationCanceledException)) { error = "Android Generate输入、工厂或provider无法完整冻结：" + exception.Message; return false; }
            }
            state = new State
            {
                Request = request,
                Target = target,
                Platform = platform,
                Channel = channel,
                Mode = mode,
                MasterPath = masterPath,
                MasterHash = masterHash,
                SettingsHash = ComputeSettingsHash(target, outputs),
                Scenes = scenes,
                AotAssemblies = aotAssemblies,
                Outputs = outputs,
                Callbacks = target == BuildTarget.WebGL || target == BuildTarget.Android ? null : BuildActionCommon.CaptureBuildCallbacks(true, cancellationToken),
                PlayerSettings = BuildActionCommon.CapturePlayerSettings(target, request.developmentBuild, cancellationToken),
                StripBuild = stripBuild,
                WebglGenerate = webglGenerate,
                AndroidGenerate = androidGenerate,
                BuildScriptsOnly = EditorUserBuildSettings.buildScriptsOnly,
                ExportAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject,
                BuildLocation = EditorUserBuildSettings.GetBuildLocation(target),
            };
            if (androidGenerate?.unboundContracts?.Length > 0)
            {
                error = "Android Generate真实执行合同尚未闭合，不能执行：" + string.Join("; ", androidGenerate.unboundContracts);
                return false;
            }
            if (state.Callbacks != null && state.Callbacks.unsupported.Length > 0)
            {
                error = "GenerateAll 的 Player 回调尚无确定写入契约，不能执行：" + string.Join("; ", state.Callbacks.unsupported);
                return false;
            }
            if (File.Exists(YooAssetRuntimeSettingsStaging.GetMarkerFilePath()))
            {
                error = "YooAsset staging 已有旧 marker，内部构建后处理可能清理原残留；请先按原凭据恢复，再重新 Plan。";
                return false;
            }
            if (!BuildActionCommon.TryValidateNoDirtyPersistentAssets(out error)) return false;
            return true;
        }

        /// <summary>分别解析 Master AOT 保留表和 HybridCLR 引用保留表，冻结生成输出与缓存写域。</summary>
        /// <param name="target">已经核对为当前活动目标的构建平台。</param>
        /// <param name="linkXml">Master 配置解析后的 AOT 保留表完整路径。</param>
        /// <param name="generatedLinkXml">调用方已通过项目路径校验的 HybridCLR 引用保留表完整路径。</param>
        /// <param name="outputs">成功时得到的输出角色与可能失效缓存集合；路径解析失败时为空。</param>
        /// <param name="error">首个项目路径解析失败的具体说明。</param>
        /// <returns>主要输出路径均可解析到项目内时返回 true；本函数不创建目录或要求两份保留表路径相同。</returns>
        private static bool TryResolveOutputs(BuildTarget target, string linkXml, string generatedLinkXml, out OutputSet outputs, out string error)
        {
            outputs = null;
            error = null;
            string genericRaw = Path.Combine(Application.dataPath, SettingsUtil.HybridCLRSettings.outputAOTGenericReferenceFile ?? string.Empty);
            string[] values =
            {
                SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target), SettingsUtil.GetAssembliesPostIl2CppStripDir(target),
                SettingsUtil.GeneratedCppDir, genericRaw, linkXml,
                Path.Combine(SettingsUtil.HybridCLRDataDir, "StrippedAOTDllsTempProj", target.ToString()), SettingsUtil.Il2CppBuildCacheDir,
            };
            var resolved = new string[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                if (!GenerateActionCommon.TryResolveProjectPath(values[index], "HybridCLR output", out resolved[index], out error)) return false;
            }
            outputs = new OutputSet
            {
                hotUpdateDirectory = resolved[0],
                strippedAotDirectory = resolved[1],
                generatedCppDirectory = resolved[2],
                methodBridgeFile = Path.Combine(resolved[2], "MethodBridge.cpp"),
                unityVersionFile = Path.Combine(resolved[2], "UnityVersion.h"),
                assemblyManifestFile = Path.Combine(resolved[2], "AssemblyManifest.cpp"),
                aotGenericReferenceFile = resolved[3],
                linkXmlFile = resolved[4],
                generatedLinkXmlFile = generatedLinkXml,
                strippedTempProject = resolved[5],
                il2CppBuildCache = resolved[6],
                sharedBuildCaches = new[] { "Library/Bee", "Library/PlayerDataCache" }
                    .Select(path => Path.GetFullPath(Path.Combine(GenerateActionCommon.ProjectRoot, path))).ToArray(),
                androidBuildInputs = target == BuildTarget.Android
                    ? new[] { "Assets/Plugins/Android/AndroidManifest.xml", "Assets/Plugins/Android/proguard-user.txt" }
                        .Select(path => Path.GetFullPath(Path.Combine(GenerateActionCommon.ProjectRoot, path))).ToArray()
                    : Array.Empty<string>(),
            };
            return true;
        }

        /// <summary>将目标、开发选项、HybridCLR 设置和完整输出角色绑定到确定性摘要。</summary>
        /// <param name="target">本次已冻结的实际构建目标。</param>
        /// <param name="outputs">已经解析的输出集合，包含两份保留表及可能失效的缓存角色。</param>
        /// <returns>聚合设置文本的摘要；仅读取和序列化设置，不保存配置文件。</returns>
        private static string ComputeSettingsHash(BuildTarget target, OutputSet outputs)
        {
            string value = target + "\n" + EditorUserBuildSettings.development + "\n" +
                           string.Join("\n", SettingsUtil.HotUpdateAssemblyFilesIncludePreserved ?? new List<string>()) + "\n" +
                           string.Join("\n", SettingsUtil.AOTAssemblyNames ?? new List<string>()) + "\n" +
                           Util.Json.Serialize(outputs) + "\n" + EditorJsonUtility.ToJson(SettingsUtil.HybridCLRSettings, false);
            return GenerateActionCommon.ComputeTextHash(value);
        }

        /// <summary>按执行前或正常生成后阶段比较冻结输入，并核对 Android 官方进程环境转换。</summary>
        /// <param name="expected">本次 Plan 保存的非空完整输入基线。</param>
        /// <param name="actual">当前重新捕获的输入状态；空值直接判为不一致。</param>
        /// <param name="afterGeneration">正常 GenerateAll 返回后传 true；默认 false 用于执行前比较。</param>
        /// <returns>所有冻结输入、临时设置和对应阶段的 StripAOT 合同均一致时返回 true，否则返回 false。</returns>
        private static bool FrozenStatesEqual(State expected, State actual, bool afterGeneration = false)
        {
            return actual != null && expected.Target == actual.Target && expected.Platform == actual.Platform &&
                   expected.Channel == actual.Channel && expected.Mode == actual.Mode &&
                   expected.MasterPath == actual.MasterPath && expected.MasterHash == actual.MasterHash &&
                   expected.SettingsHash == actual.SettingsHash &&
                   BuildActionCommon.PlayerSettingsEqual(expected.PlayerSettings, actual.PlayerSettings) &&
                   ((expected.Target != BuildTarget.Android && expected.Target != BuildTarget.WebGL) || (afterGeneration
                       ? HybridClrStripBuildContract.AfterEqual(expected.StripBuild, actual.StripBuild)
                       : HybridClrStripBuildContract.BeforeEqual(expected.StripBuild, actual.StripBuild))) &&
                   (expected.Target != BuildTarget.WebGL || afterGeneration || PlayerBuildAction.GeneratePlansEqual(expected.WebglGenerate, actual.WebglGenerate)) &&
                   (expected.Target != BuildTarget.Android || afterGeneration || PlayerBuildAction.AndroidGeneratePlansEqual(expected.AndroidGenerate, actual.AndroidGenerate)) &&
                   expected.BuildScriptsOnly == actual.BuildScriptsOnly && expected.ExportAndroidProject == actual.ExportAndroidProject &&
                   expected.BuildLocation == actual.BuildLocation && Util.Json.Serialize(expected.Callbacks) == Util.Json.Serialize(actual.Callbacks) &&
                   expected.Scenes.SequenceEqual(actual.Scenes, StringComparer.Ordinal) &&
                   expected.AotAssemblies.SequenceEqual(actual.AotAssemblies, StringComparer.Ordinal) &&
                   Util.Json.Serialize(expected.Outputs) == Util.Json.Serialize(actual.Outputs);
        }

        /// <summary>只解释本次已失败比较的已有DTO；不重跑比较器、弱引用门或任何Native捕获，不能作为许可。</summary>
        /// <param name="expected">原冻结State，诊断不修改或补造。</param><param name="actual">同次TryBuild已成功取得的current，可缺失用于纯边界诊断。</param>
        /// <returns>严格有界的字段摘要；DTO相同仍不能证明托管/弱见证通过。</returns>
        private static string DescribeFrozenStateMismatch(State expected, State actual)
        {
            var result = new Newtonsoft.Json.Linq.JObject
            {
                ["diagnosticOnly"] = true, ["notApprovalEvidence"] = true,
                ["captureSucceeded"] = true, ["originalComparisonCalled"] = true, ["originalComparisonResult"] = false,
                ["predicateResults"] = "notReevaluated; pure DTO differences are not original short-circuit gate results",
                ["referenceGates"] = "notReevaluated; weak/managed witness checks remain inconclusive even with identical DTOs",
                ["expectedMissing"] = expected == null, ["actualMissing"] = actual == null,
                ["sourcePredicateOrderOnly"] = new Newtonsoft.Json.Linq.JArray("actual!=null", "Target", "Platform", "Channel", "Mode", "MasterPath", "MasterHash", "SettingsHash", "PlayerSettingsEqual", "Strip.BeforeEqual", "GeneratePlansEqual(WebGL only)", "BuildScriptsOnly", "ExportAndroidProject", "BuildLocation", "Callbacks JSON", "Scenes sequence", "AotAssemblies sequence", "Outputs JSON"),
                ["generatePredicateOrderOnly"] = new Newtonsoft.Json.Linq.JArray("presence", "inputs.sha256", "SVS.BeforeEquals", "callbacks.sha256", "previousReportGuid", "webglTemplate", "Strip.BeforeEqual", "owned JSON", "auditorBaseline JSON", "normalResetStates", "TMP.BeforeEquals", "Facebook.BeforeEquals", "BuiltinUsage.BeforeEquals", "BuildEvents.BeforeEquals", "EmptyUsage.BeforeEquals", "FiniteAdditional.BeforeEquals", "PackageCallbacks.BeforeEquals"),
                ["detailsComplete"] = true,
            };
            try
            {
                var before = CreateFrozenStateDiagnosticProjection(expected);
                var after = CreateFrozenStateDiagnosticProjection(actual);
                result["beforeDtoSha256"] = GenerateActionCommon.ComputeTextHash(before.ToString(Newtonsoft.Json.Formatting.None));
                result["afterDtoSha256"] = GenerateActionCommon.ComputeTextHash(after.ToString(Newtonsoft.Json.Formatting.None));
                result["pureDtosEquivalent"] = Newtonsoft.Json.Linq.JToken.DeepEquals(before, after);
                var sections = new Newtonsoft.Json.Linq.JArray();
                foreach (string path in new[] { "", "WebglGenerate", "WebglGenerate.inputs", "WebglGenerate.callbacks", "WebglGenerate.sceneVisibilityBefore", "WebglGenerate.sceneVisibilityBefore.body", "WebglGenerate.sceneVisibilityBefore.observation" })
                {
                    var left = path.Length == 0 ? before : before.SelectToken(path);
                    var right = path.Length == 0 ? after : after.SelectToken(path);
                    var fields = FrozenStateDiagnosticFields(left, right);
                    sections.Add(new Newtonsoft.Json.Linq.JObject { ["section"] = path.Length == 0 ? "outer" : path,
                        ["differentFieldCount"] = fields.Count, ["fields"] = fields });
                }
                result["sections"] = sections;
                result["inputBeforeSha256"] = FrozenStateDiagnosticHashLiteral(before.SelectToken("WebglGenerate.inputs.sha256"));
                result["inputAfterSha256"] = FrozenStateDiagnosticHashLiteral(after.SelectToken("WebglGenerate.inputs.sha256"));
                result["callbackBeforeSha256"] = FrozenStateDiagnosticHashLiteral(before.SelectToken("WebglGenerate.callbacks.sha256"));
                result["callbackAfterSha256"] = FrozenStateDiagnosticHashLiteral(after.SelectToken("WebglGenerate.callbacks.sha256"));
                var leftRows = before.SelectToken("WebglGenerate.inputs.effectiveIdentities") as Newtonsoft.Json.Linq.JArray;
                var rightRows = after.SelectToken("WebglGenerate.inputs.effectiveIdentities") as Newtonsoft.Json.Linq.JArray;
                result["effectiveBeforeCount"] = leftRows?.Count ?? 0; result["effectiveAfterCount"] = rightRows?.Count ?? 0;
                result["effectiveBeforeSequenceSha256"] = GenerateActionCommon.ComputeTextHash(leftRows?.ToString(Newtonsoft.Json.Formatting.None) ?? "null");
                result["effectiveAfterSequenceSha256"] = GenerateActionCommon.ComputeTextHash(rightRows?.ToString(Newtonsoft.Json.Formatting.None) ?? "null");
                var changedRows = FrozenStateDiagnosticRows(leftRows, rightRows);
                result["effectiveChangedRowCount"] = changedRows.Count; result["effectiveChangedRows"] = changedRows;
                result["effectiveOrderOnly"] = changedRows.Count == 0 && !Newtonsoft.Json.Linq.JToken.DeepEquals(leftRows, rightRows);
            }
            catch (Exception exception)
            {
                result["detailsComplete"] = false; result["diagnosticErrorType"] = exception.GetType().FullName;
                result["pureDtosEquivalent"] = null;
            }
            return FormatFrozenStateDiagnostic(result);
        }

        /// <summary>显式复制原比较涉及的纯DTO字段；排除Request/lease/disposed及Generate未比较的恢复随机目录和link路径。</summary>
        /// <param name="value">已有State，不访问属性或现场引用。</param><returns>自有纯JSON树；JsonIgnore的Native/弱引用不参与，不改原DTO。</returns>
        private static Newtonsoft.Json.Linq.JObject CreateFrozenStateDiagnosticProjection(State value)
        {
            if (value == null) return new Newtonsoft.Json.Linq.JObject { ["missing"] = true };
            var serializer = new Newtonsoft.Json.JsonSerializer { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None,
                ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(), NullValueHandling = Newtonsoft.Json.NullValueHandling.Include };
            var result = Newtonsoft.Json.Linq.JObject.FromObject(new
            {
                value.Target, value.Platform, value.Channel, value.Mode, value.MasterPath, value.MasterHash, value.SettingsHash,
                value.PlayerSettings, value.StripBuild, value.WebglGenerate, value.BuildScriptsOnly, value.ExportAndroidProject,
                value.BuildLocation, value.Callbacks, value.Scenes, value.AotAssemblies, value.Outputs,
            }, serializer);
            if (result["WebglGenerate"] is Newtonsoft.Json.Linq.JObject generate)
            { generate.Remove("recoveryDirectory"); generate.Remove("masterLinkXml"); }
            return result;
        }

        /// <summary>只列固定DTO直接字段差异及完整值指纹/长度/数量，不输出配置字段正文，也不重跑其领域判据。</summary>
        /// <param name="before">自有原JSON节点，可缺失。</param><param name="after">自有current节点，可缺失。</param><returns>完整差异摘要数组；数组顺序由完整字段值SHA覆盖。</returns>
        private static Newtonsoft.Json.Linq.JArray FrozenStateDiagnosticFields(Newtonsoft.Json.Linq.JToken before, Newtonsoft.Json.Linq.JToken after)
        {
            var result = new Newtonsoft.Json.Linq.JArray();
            var left = before as Newtonsoft.Json.Linq.JObject; var right = after as Newtonsoft.Json.Linq.JObject;
            string[] fields = left == null && right == null ? new[] { "$value" } :
                (left?.Properties().Select(property => property.Name) ?? Enumerable.Empty<string>()).Concat(right?.Properties().Select(property => property.Name) ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            foreach (string name in fields)
            {
                var a = name == "$value" ? before : left?[name]; var b = name == "$value" ? after : right?[name];
                if (Newtonsoft.Json.Linq.JToken.DeepEquals(a, b)) continue;
                string aJson = a?.ToString(Newtonsoft.Json.Formatting.None), bJson = b?.ToString(Newtonsoft.Json.Formatting.None);
                result.Add(new Newtonsoft.Json.Linq.JObject { ["field"] = name, ["beforeExists"] = a != null, ["afterExists"] = b != null,
                    ["beforeSha256"] = aJson == null ? null : GenerateActionCommon.ComputeTextHash(aJson), ["afterSha256"] = bJson == null ? null : GenerateActionCommon.ComputeTextHash(bJson),
                    ["beforeBytes"] = aJson == null ? 0 : System.Text.Encoding.UTF8.GetByteCount(aJson), ["afterBytes"] = bJson == null ? 0 : System.Text.Encoding.UTF8.GetByteCount(bJson),
                    ["beforeCount"] = a is Newtonsoft.Json.Linq.JContainer ac ? ac.Count : 0, ["afterCount"] = b is Newtonsoft.Json.Linq.JContainer bc ? bc.Count : 0 });
            }
            return result;
        }

        /// <summary>按完整JSON token多重计数记录effective差异，另由序列SHA覆盖纯重排；不输出任意原始行。</summary>
        /// <param name="before">原完整effective数组。</param><param name="after">同次current完整数组。</param><returns>不同完整行的脱敏摘要及两侧重复次数。</returns>
        private static Newtonsoft.Json.Linq.JArray FrozenStateDiagnosticRows(Newtonsoft.Json.Linq.JArray before, Newtonsoft.Json.Linq.JArray after)
        {
            var left = (before ?? new Newtonsoft.Json.Linq.JArray()).GroupBy(value => value.ToString(Newtonsoft.Json.Formatting.None), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var right = (after ?? new Newtonsoft.Json.Linq.JArray()).GroupBy(value => value.ToString(Newtonsoft.Json.Formatting.None), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var result = new Newtonsoft.Json.Linq.JArray();
            foreach (string row in left.Keys.Concat(right.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
            {
                int a = left.TryGetValue(row, out int oldCount) ? oldCount : 0, b = right.TryGetValue(row, out int newCount) ? newCount : 0;
                if (a != b) result.Add(FrozenStateDiagnosticRow(Newtonsoft.Json.Linq.JToken.Parse(row), a, b));
            }
            return result;
        }

        /// <summary>仅识别原dirty/TMP对象行；类型受限、完整身份和资产路径仅摘要，未知语法只保完整行指纹。</summary>
        /// <param name="token">自有完整行token。</param><param name="beforeCount">原重复次数。</param><param name="afterCount">current重复次数。</param><returns>无配置正文或路径的纯摘要。</returns>
        private static Newtonsoft.Json.Linq.JObject FrozenStateDiagnosticRow(Newtonsoft.Json.Linq.JToken token, int beforeCount, int afterCount)
        {
            string raw = token.Type == Newtonsoft.Json.Linq.JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);
            var result = new Newtonsoft.Json.Linq.JObject { ["rowTokenSha256"] = GenerateActionCommon.ComputeTextHash(token.ToString(Newtonsoft.Json.Formatting.None)),
                ["rawRowSha256"] = GenerateActionCommon.ComputeTextHash(raw), ["rawBytes"] = System.Text.Encoding.UTF8.GetByteCount(raw),
                ["beforeCount"] = beforeCount, ["afterCount"] = afterCount, ["identityRecognized"] = false };
            string prefix = new[] { "PreservedDirtyObject=", "TMPPreservedObject=" }.FirstOrDefault(value => raw.StartsWith(value, StringComparison.Ordinal));
            if (prefix == null) return result;
            int tail = raw.LastIndexOf('|');
            if (tail < prefix.Length || FrozenStateDiagnosticHashLiteral(new Newtonsoft.Json.Linq.JValue(raw.Substring(tail + 1))) == null) return result;
            string identity = raw.Substring(prefix.Length, tail - prefix.Length); int first = identity.IndexOf('|'), second = first < 0 ? -1 : identity.IndexOf('|', first + 1), third = second < 0 ? -1 : identity.IndexOf('|', second + 1);
            if (first <= 0 || first > 256 || second < 0) return result;
            string type = identity.Substring(0, first), storage = identity.Substring(first + 1, second - first - 1);
            if (type.Any(character => !(character >= 'a' && character <= 'z' || character >= 'A' && character <= 'Z' || character >= '0' && character <= '9' || "._+`[]".IndexOf(character) >= 0))) return result;
            string id = third < 0 ? identity.Substring(second + 1) : identity.Substring(second + 1, third - second - 1);
            if (!long.TryParse(id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _) || (storage == "nonpersistent" ? third >= 0 : storage.Length != 32 || storage.Any(character => !(character >= '0' && character <= '9' || character >= 'a' && character <= 'f')) || third < 0)) return result;
            result["identityRecognized"] = true; result["tool"] = prefix.TrimEnd('='); result["type"] = type;
            result["identitySha256"] = GenerateActionCommon.ComputeTextHash(identity);
            result["assetPathSha256"] = third < 0 ? null : GenerateActionCommon.ComputeTextHash(identity.Substring(third + 1));
            result["objectJsonSha256"] = raw.Substring(tail + 1);
            return result;
        }

        /// <summary>只显式展示已是小写64hex的摘要，其它值不会作为正文输出。</summary>
        /// <param name="token">已有自有DTO字段，可缺失或损坏。</param><returns>准确64hex或null。</returns>
        private static string FrozenStateDiagnosticHashLiteral(Newtonsoft.Json.Linq.JToken token)
        {
            string value = token?.Type == Newtonsoft.Json.Linq.JTokenType.String ? (string)token : null;
            return value != null && value.Length == 64 && value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f') ? value : null;
        }

        /// <summary>整项省略超预算明细，不截断字符串；保留完整差异SHA/数量，给既有失败前缀预留一KiB。</summary>
        /// <param name="diagnostic">本方法拥有的脱敏诊断树，不是原State。</param><returns>最多14KiB UTF8，整个blocked消息不超过15KiB。</returns>
        private static string FormatFrozenStateDiagnostic(Newtonsoft.Json.Linq.JObject diagnostic)
        {
            diagnostic["fullDiagnosticSha256"] = GenerateActionCommon.ComputeTextHash(diagnostic.ToString(Newtonsoft.Json.Formatting.None));
            string prefix = "同次冻结差异：";
            if (System.Text.Encoding.UTF8.GetByteCount(prefix + diagnostic.ToString(Newtonsoft.Json.Formatting.None)) > 14 * 1024)
            {
                diagnostic["detailsComplete"] = false;
                diagnostic["effectiveRowsOmitted"] = diagnostic["effectiveChangedRows"] is Newtonsoft.Json.Linq.JArray rows ? rows.Count : 0;
                diagnostic.Remove("effectiveChangedRows");
            }
            if (System.Text.Encoding.UTF8.GetByteCount(prefix + diagnostic.ToString(Newtonsoft.Json.Formatting.None)) > 14 * 1024 && diagnostic["sections"] is Newtonsoft.Json.Linq.JArray sections)
            {
                diagnostic["detailsComplete"] = false;
                foreach (Newtonsoft.Json.Linq.JObject section in sections) { section["fieldDetailsOmitted"] = true; section.Remove("fields"); }
            }
            string output = prefix + diagnostic.ToString(Newtonsoft.Json.Formatting.None);
            if (System.Text.Encoding.UTF8.GetByteCount(output) > 14 * 1024)
                return prefix + new Newtonsoft.Json.Linq.JObject { ["diagnosticOnly"] = true, ["notApprovalEvidence"] = true, ["detailsComplete"] = false,
                    ["fullDiagnosticSha256"] = diagnostic["fullDiagnosticSha256"], ["effectiveChangedRowCount"] = diagnostic["effectiveChangedRowCount"], ["allDetailsOmitted"] = true }.ToString(Newtonsoft.Json.Formatting.None);
            return output;
        }

        /// <summary>按固定输出角色枚举精确产物路径，并按 Ordinal 去重且保留首次出现的顺序。</summary>
        /// <param name="outputs">已解析的三个目录和各文件角色；两份保留表可合法共用同一路径。</param>
        /// <returns>唯一产物路径数组；去重只合并路径，不免除各角色的独立语义校验。</returns>
        private static string[] GetExactOutputs(OutputSet outputs)
        {
            return new[]
            {
                outputs.hotUpdateDirectory, outputs.strippedAotDirectory,
                outputs.methodBridgeFile, outputs.unityVersionFile,
                outputs.assemblyManifestFile, outputs.aotGenericReferenceFile,
                outputs.linkXmlFile, outputs.generatedLinkXmlFile, outputs.strippedTempProject,
            }.Distinct(StringComparer.Ordinal).ToArray();
        }

        /// <summary>只读捕获所有精确唯一输出，目录记录完整枚举摘要，文件记录字节摘要。</summary>
        /// <param name="outputs">本次冻结的精确输出集合；三个目录角色使用目录捕获，其余角色使用文件捕获。</param>
        /// <param name="context">本次执行上下文，用于向每个产物捕获步骤传递取消令牌。</param>
        /// <returns>全部捕获成功后的产物数组；任一步读取失败或取消时不返回部分数组。</returns>
        private static GenerateActionCommon.Artifact[] CaptureArtifacts(OutputSet outputs, AgentActionExecutionContext context)
        {
            var directories = new HashSet<string>(new[]
            {
                outputs.hotUpdateDirectory, outputs.strippedAotDirectory, outputs.strippedTempProject,
            }, StringComparer.Ordinal);
            return GetExactOutputs(outputs).Select(path => directories.Contains(path)
                ? GenerateActionCommon.CaptureDirectory(path, "*", context.CancellationToken)
                : GenerateActionCommon.CaptureFile(path, context.CancellationToken)).ToArray();
        }

        /// <summary>只读检查 MethodBridge.cpp 的规范 DEVELOPMENT 标记与冻结开发构建选项一致。</summary>
        /// <param name="path">本次冻结的 MethodBridge.cpp 完整路径。</param>
        /// <param name="developmentBuild">请求与实际 Editor 已核对一致的开发构建值。</param>
        /// <returns>无返回值；文件存在且标记匹配时正常结束，否则抛出异常。</returns>
        private static void ValidateMethodBridge(string path, bool developmentBuild)
        {
            if (!File.Exists(path) || !EditorUtil.HybridCLR.TryReadMethodBridgeDevelopmentBuild(File.ReadAllText(path), out bool actual) || actual != developmentBuild)
            {
                throw new InvalidOperationException("MethodBridge.cpp 的规范 DEVELOPMENT 标记与冻结 DevelopmentBuild 不一致。");
            }
        }

        /// <summary>只读解析保留表，检查 linker 根节点及所需 assembly 全名条目。</summary>
        /// <param name="path">本次冻结的 Master AOT 保留表或 HybridCLR 引用保留表路径。</param>
        /// <param name="assemblies">要求存在的非空程序集全名集合；生成引用保留表传空集合，仅验证 XML 结构。</param>
        /// <returns>无返回值；结构和所需条目均满足时正常结束，否则抛出异常。</returns>
        private static void ValidateLinkXml(string path, IEnumerable<string> assemblies)
        {
            var document = new XmlDocument();
            document.Load(path);
            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "linker") throw new InvalidOperationException("link.xml 根节点不是 <linker>。");
            var existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (XmlNode node in root.ChildNodes)
            {
                if (node is XmlElement element && element.Name == "assembly") existing.Add(element.GetAttribute("fullname"));
            }
            string missing = (assemblies ?? Array.Empty<string>()).FirstOrDefault(assembly => !existing.Contains(assembly));
            if (missing != null) throw new InvalidOperationException("link.xml 缺少 AOT metadata assembly：" + missing);
        }

        /// <summary>规范化 AOT 配置文本，移除空白和末尾一次忽略大小写的 .dll 后缀。</summary>
        /// <param name="value">单个 AOT metadata 配置位置；允许空值或空白字符串。</param>
        /// <returns>规范化后的程序集名称文本；空值或空白输入返回空字符串。</returns>
        private static string StripDllSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string trimmed = value.Trim();
            return trimmed.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring(0, trimmed.Length - 4)
                : trimmed;
        }

        /// <summary>从冻结状态建立未完成恢复凭据，供成功执行补充真实后态和编译绑定。</summary>
        /// <param name="state">本次冻结的状态；其内部请求和输出集合必须有效。</param>
        /// <param name="artifacts">已完整捕获的产物数组，或计划与失败恢复阶段使用的空数组。</param>
        /// <returns>新建且默认未 finalized 的凭据；嵌套快照和数组保留原引用，不据此产生真实构建观察。</returns>
        private static Receipt CreateReceipt(State state, GenerateActionCommon.Artifact[] artifacts)
        {
            return new Receipt
            {
                masterGuid = state.Request.masterGuid,
                masterPath = state.MasterPath,
                masterHash = state.MasterHash,
                settingsHash = state.SettingsHash,
                platform = state.Platform.ToString(),
                channel = state.Channel.ToString(),
                developMode = state.Mode.ToString(),
                activeBuildTarget = state.Target.ToString(),
                developmentBuild = state.Request.developmentBuild,
                enabledScenes = state.Scenes,
                aotAssemblies = state.AotAssemblies,
                outputs = state.Outputs,
                artifacts = artifacts,
                callbacks = state.Callbacks,
                playerSettings = state.PlayerSettings,
                stripBuildBefore = state.StripBuild,
                webglGeneratePlan = state.WebglGenerate,
                webglEvidenceReference = state.WebglGenerate == null ? null : new HybridClrGenerateEvidenceStore(BuildActionCommon.ProjectRoot).Plan(state.WebglGenerate),
                androidEvidenceReference = state.AndroidGenerate?.unboundContracts?.Length == 0 ? new AndroidHybridClrGenerateEvidenceStore(BuildActionCommon.ProjectRoot).Plan(state.AndroidGenerate) : null,
                buildScriptsOnly = state.BuildScriptsOnly,
                exportAndroidProject = state.ExportAndroidProject,
                buildLocation = state.BuildLocation,
            };
        }

        /// <summary>将正式上下文、产物和前后态观察绑定到编译载荷，排除令牌及摘要本身以避免自引用。</summary>
        /// <param name="receipt">已经填入当前阶段上下文与产物的非空凭据。</param>
        /// <returns>待绑定或核对的编译载荷摘要；不调度编译，也不替代产物验证。</returns>
        private static string ComputeCompilationPayloadHash(Receipt receipt)
        {
            // WebGL与旧非Android回执继续使用原字段集合，不能因新增null字段使既有编译载荷失效。
            if (receipt.activeBuildTarget == BuildTarget.Android.ToString() && receipt.androidEvidenceReference != null)
                return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
                {
                    receipt.finalized, receipt.masterGuid, receipt.masterPath, receipt.masterHash, receipt.settingsHash,
                    receipt.platform, receipt.channel, receipt.developMode, receipt.activeBuildTarget, receipt.developmentBuild,
                    receipt.enabledScenes, receipt.aotAssemblies, receipt.outputs, receipt.artifacts, receipt.callbacks,
                    receipt.playerSettings, receipt.stripBuildBefore, receipt.stripBuildAfter, receipt.stripBuildObservation, receipt.androidEvidenceReference,
                    receipt.buildScriptsOnly, receipt.exportAndroidProject, receipt.buildLocation,
                }));
            return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
            {
                receipt.finalized, receipt.masterGuid, receipt.masterPath, receipt.masterHash, receipt.settingsHash,
                receipt.platform, receipt.channel, receipt.developMode, receipt.activeBuildTarget, receipt.developmentBuild,
                receipt.enabledScenes, receipt.aotAssemblies, receipt.outputs, receipt.artifacts,
                receipt.callbacks, receipt.playerSettings, receipt.stripBuildBefore, receipt.stripBuildAfter, receipt.stripBuildObservation, receipt.webglEvidenceReference,
                receipt.buildScriptsOnly, receipt.exportAndroidProject, receipt.buildLocation,
            }));
        }

        /// <summary>列出 GenerateAll 可能写入或失效的项目路径，供计划审阅及失败后的归属核查。</summary>
        /// <param name="outputs">本次冻结的生成目录、双保留表、缓存以及 Android 构建输入角色。</param>
        /// <returns>按项目相对路径规范化并以 Ordinal 去重的写域数组；不表示每条路径均已生成或完整验证。</returns>
        private static string[] GetWriteSet(OutputSet outputs)
        {
            return new[]
            {
                GenerateActionCommon.ToProjectRelative(outputs.hotUpdateDirectory),
                GenerateActionCommon.ToProjectRelative(outputs.strippedAotDirectory),
                GenerateActionCommon.ToProjectRelative(outputs.generatedCppDirectory),
                GenerateActionCommon.ToProjectRelative(outputs.aotGenericReferenceFile),
                GenerateActionCommon.ToProjectRelative(outputs.linkXmlFile),
                GenerateActionCommon.ToProjectRelative(outputs.generatedLinkXmlFile),
                GenerateActionCommon.ToProjectRelative(outputs.strippedTempProject),
                GenerateActionCommon.ToProjectRelative(outputs.il2CppBuildCache),
            }.Concat(outputs.sharedBuildCaches ?? Array.Empty<string>())
                .Concat(outputs.androidBuildInputs ?? Array.Empty<string>())
                .Select(GenerateActionCommon.ToProjectRelative).Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}
