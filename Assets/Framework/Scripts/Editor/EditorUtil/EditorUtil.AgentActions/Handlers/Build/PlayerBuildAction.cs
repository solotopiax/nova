/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerBuildAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   精确输出路径的 Unity Player 构建 Project Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using global::HybridCLR.Editor;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;
using BuildReport = UnityEditor.Build.Reporting.BuildReport;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.player.build",
        "构建 Unity Player",
        "player",
        AgentActionOperationType.Build,
        Description = "按目标平台、Build Settings 场景、开发模式与输出路径构建 Unity Player，并返回 BuildReport 证据。",
        Effects = AgentActionEffect.WorkspaceRead |
                  AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead |
                  AgentActionEffect.UnityWrite |
                  AgentActionEffect.ExternalWrite |
                  AgentActionEffect.BuildArtifact |
                  AgentActionEffect.Destructive,
        RequiredEvidence = AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        ReloadSemantics = AgentActionReloadSemantics.ReloadNotExpected,
        Locks = new[] { "unity-editor", "player-build", "Assets", "ProjectSettings" })]
    internal sealed partial class PlayerBuildAction : AgentActionHandler<PlayerBuildAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string target;
            [AgentActionRequired] public string outputPath;
            public bool developmentBuild;
            [AgentActionRequired] public string buildMode;
        }

        private sealed class State
        {
            public BuildTarget Target;
            public string OutputPath;
            public bool DevelopmentBuild;
            public EditorUtil.Build.BuildMode BuildMode;
            public string BuildModeLogical;
            public string[] Scenes;
            public BuildActionCommon.PlayerSettingsSnapshot Settings;
            public PlayerInputSnapshot Inputs;
            public FileState[] OutputBaseline;
            public CallbackInputSnapshot Callbacks;
            public FileState[] AuditorBaseline;
            public string AuditorRecoveryDirectory;
        }

        /// <summary>仅保存指纹与身份；配置正文和凭据不进入计划或回执。</summary>
        [Serializable]
        internal sealed class FileState
        {
            public string path;
            public string kind;
            public string sha256;
        }

        /// <summary>场景正文、meta 与 Unity 导入依赖共同绑定到本次有序场景列表。</summary>
        [Serializable]
        internal sealed class SceneInput
        {
            public string path;
            public string guid;
            public string bodySha256;
            public string metaSha256;
            public string dependencyHash;
        }

        /// <summary>Player 专属输入，不复用 Bundle 的 packageName 或 SBP 回调事务。</summary>
        [Serializable]
        internal sealed class PlayerInputSnapshot
        {
            public string sha256;
            public SceneInput[] scenes;
            public string[] sceneSetup;
            public string[] dependencyIdentities;
            public string[] dependencyPaths;
            public FileState[] files;
            public string[] effectiveIdentities;
            public string masterPath;
            public string masterGuid;
            public string runtimePath;
            public string runtimeGuid;
            public string platform;
            public string channel;
            public string developMode;
            public string yooSettingsPath;
            public string collectorPath;
            public string stagingTarget;
            public string resourcesDirectory;
            public bool resourcesDirectoryExisted;
            public bool resourcesMetaExisted;
            public string[] outputRoots;
            public string aotOutput;
            public string strippedAotSource;
        }

        /// <summary>可跨域只读校验的执行回执；应用标识与 Bundle 包名分开。</summary>
        [Serializable]
        private sealed class PlayerReceipt
        {
            public int schemaVersion;
            public BuildActionCommon.ArtifactReceipt artifact;
            public string inputSha256;
            public SceneInput[] scenes;
            public string applicationIdentifier;
            public string reportPlatform;
            public int reportOptions;
            public int reportErrors;
            public string reportGuid;
            public bool inputsPreserved;
            public bool stagingClean;
            public FileState[] outputArtifacts;
            public string callbackSha256;
            public FileState aotArtifact;
            public FileState[] aotDlls;
            public ReportCopyReceipt reportCopy;
            public NormalResetState[] normalResetBefore;
            public NormalResetState[] normalResetAfter;
            public bool callbackChainReturned;
            public string normalResetError;
            public PlayerTmpFontBuildContract.Snapshot tmpFontBefore;
            public PlayerTmpFontBuildContract.Snapshot tmpFontAfter;
            public string tmpFontError;
            public PlayerFacebookBuildContract.Snapshot facebookBefore;
            public PlayerFacebookBuildContract.Snapshot facebookAfter;
            public string facebookError;
            public PlayerBuiltinUsageBuildContract.Snapshot builtinUsageBefore;
            public PlayerBuiltinUsageBuildContract.Snapshot builtinUsageAfter;
            public PlayerBuiltinUsageBuildContract.ReportMaterial builtinUsageReport;
            public string builtinUsageError;
            public PlayerBuildEventsContract.Snapshot buildEventsBefore;
            public PlayerBuildEventsContract.Snapshot buildEventsAfter;
            public PlayerBuildEventsContract.ReportMaterial buildEventsReport;
            public string buildEventsError;
            public PlayerEmptyUsageBuildContract.Snapshot emptyUsageBefore;
            public PlayerEmptyUsageBuildContract.Snapshot emptyUsageAfter;
            public PlayerEmptyUsageBuildContract.ReportMaterial emptyUsageReport;
            public string emptyUsageError;
            public PlayerPackageCallbacksContract.Snapshot packageCallbacksBefore;
            public PlayerPackageCallbacksContract.Snapshot packageCallbacksAfter;
            public PlayerPackageCallbacksContract.ReportMaterial packageCallbacksReport;
            public string packageCallbacksError;
            public PlayerFiniteAdditionalBuildContract.Snapshot finiteAdditionalBefore, finiteAdditionalAfter;
            public PlayerFiniteAdditionalBuildContract.ReportMaterial finiteAdditionalReport;
            public string finiteAdditionalError;
        }

        /// <summary>官方报告复制的完成或失败材料；原始报告留在本地，不把正文放入回执。</summary>
        [Serializable]
        internal sealed class ReportCopyReceipt
        {
            public bool complete;
            public bool copied;
            public bool restored;
            public string reportGuid;
            public FileState source;
            public FileState destination;
            public string error;
            public string recoveryDirectory;
        }

        /// <summary>按真实 Player 接口收窄的回调、间接发现闭包及准确写集。</summary>
        [Serializable]
        internal sealed class CallbackInputSnapshot
        {
            public string sha256;
            public string[] identities;
            public string[] inputs;
            public string[] writes;
            public string[] temporaryFiles;
            public FileState[] temporaryBaseline;
            public string[] temporaryDirectoryIdentities;
            public string[] unsupported;
            public NormalResetState[] normalResetStates;
            public PlayerTmpFontBuildContract.Snapshot tmpFonts;
            public PlayerFacebookBuildContract.Snapshot facebook;
            public PlayerBuiltinUsageBuildContract.Snapshot builtinUsage;
            public PlayerBuildEventsContract.Snapshot buildEvents;
            public PlayerEmptyUsageBuildContract.Snapshot emptyUsage;
            public PlayerPackageCallbacksContract.Snapshot packageCallbacks;
            public string[] packageDependencyPaths;
            public PlayerFiniteAdditionalBuildContract.Snapshot finiteAdditional;
            [NonSerialized, Newtonsoft.Json.JsonIgnore] public QualityBuiltinBaseline qualityBuiltin;
        }

        /// <summary>正常构建后标量 reset 的实际字段身份和值；不保存 FieldInfo、不修改厂商字段。</summary>
        [Serializable]
        internal sealed class NormalResetState
        {
            public string callbackLabel;
            public string typeName;
            public string fieldName;
            public string fieldType;
            public string assemblySha256;
            public string moduleVersionId;
            public int value;
        }

        /// <summary>仅当前域持有两准确 scratch 引用，禁止把同形替换容器认成本轮原状态。</summary>
        internal sealed class QualityBuiltinBaseline
        {
            public FieldInfo assetsField;
            public FieldInfo typesField;
            public List<UnityEngine.Rendering.RenderPipelineAsset> assets;
            public HashSet<Type> types;
            public int rosterCount;
        }

        [Serializable]
        private sealed class PlanView
        {
            public string target;
            public string outputPath;
            public bool developmentBuild;
            public string buildMode;
            public string[] scenes;
            public bool outputAlreadyExists;
            public BuildActionCommon.PlayerSettingsSnapshot settings;
            public string inputSha256;
            public SceneInput[] sceneInputs;
            public string masterPath;
            public string runtimePath;
            public string platform;
            public string channel;
            public string developMode;
            public string yooSettingsPath;
            public string collectorPath;
            public string stagingTarget;
            public string[] effectiveIdentities;
            public FileState[] outputBaseline;
            public CallbackInputSnapshot callbacks;
            public FileState[] auditorBaseline;
        }

        /// <summary>保持四字段请求，拒绝不安全路径与目标产物类型。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!BuildActionCommon.TryResolveActiveTarget(request.target, out BuildTarget target, out error) ||
                !BuildActionCommon.TryResolveOutputPath(request.outputPath, out string output, out error) ||
                !TryResolveBuildMode(request.buildMode, out _, out error))
            {
                return false;
            }
            return TryValidateOutput(target, output, out error);
        }

        /// <summary>冻结真实消费输入与准确暂存目标；仅合法加载已审既有资产及明确正常内存缓存，不保存用户资产。</summary>
        /// <param name="request">已由严格请求契约验证的目标、精确输出路径、开发构建开关与构建模式。</param>
        /// <param name="context">本次 Action 上下文，提供输入捕获与文件摘要的取消令牌。</param>
        /// <returns>完整输入和回调合同可冻结时返回 ready 计划，否则返回含具体缺口的 blocked 计划。</returns>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            BuildActionCommon.TryResolveActiveTarget(request.target, out BuildTarget target, out _);
            BuildActionCommon.TryResolveOutputPath(request.outputPath, out string outputPath, out _);
            TryResolveBuildMode(request.buildMode, out EditorUtil.Build.BuildMode buildMode, out _);
            if (!BuildActionCommon.IsSupportedPlayerTarget(target))
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "blocked",
                    Summary = $"Player Action 当前只支持 Standalone 桌面目标与 WebGL；{target} 的平台专属构建、导出或签名设置尚未进入冻结契约。",
                });
            }
            string[] scenes = BuildActionCommon.GetEnabledBuildScenes();
            if (scenes.Length == 0)
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "blocked",
                    Summary = "Build Settings 没有启用场景，无法冻结 Player 构建输入。",
                    Evidence = new[] { "Plan 只读检查了 EditorBuildSettings.scenes。" },
                });
            }
            string missingScene = scenes.FirstOrDefault(scene => string.IsNullOrWhiteSpace(scene) ||
                !File.Exists(Path.Combine(BuildActionCommon.ProjectRoot, scene)));
            if (missingScene != null)
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "blocked",
                    Summary = "Build Settings 中的启用场景不存在：" + missingScene,
                    Evidence = new[] { "Plan 只读检查了冻结场景路径。" },
                });
            }
            if (scenes.Distinct(StringComparer.Ordinal).Count() != scenes.Length)
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "blocked",
                    Summary = "Build Settings 的启用场景包含重复路径，拒绝生成歧义计划。",
                });
            }

            BuildActionCommon.PlayerSettingsSnapshot settings = BuildActionCommon.CapturePlayerSettings(
                target, request.developmentBuild, context.CancellationToken);
            if (!TryCaptureInputs(target, outputPath, request.developmentBuild, context.CancellationToken,
                    out PlayerInputSnapshot inputs, out string inputError))
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = inputError });
            }
            CallbackInputSnapshot callbacks = CapturePlayerCallbacks(target, inputs.dependencyPaths, context.CancellationToken);
            if (callbacks.unsupported.Length != 0)
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "blocked", Summary = "当前可执行 Player 回调尚有未冻结分支：" + string.Join("；", callbacks.unsupported),
                    DataJson = Util.Json.Serialize(callbacks),
                });
            if (!TryCaptureInputs(target, outputPath, request.developmentBuild, context.CancellationToken,
                    out PlayerInputSnapshot preparedInputs, out inputError, callbacks.tmpFonts) ||
                !PreparedInputsPreserveOriginal(inputs, preparedInputs))
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = inputError ?? "合法TMP资产加载期间其他原输入或已有dirty正文变化；不重新吸收用户状态。" });
            inputs = preparedInputs;
            FileState[] outputBaseline = CapturePaths(inputs.outputRoots, context.CancellationToken);
            FileState[] auditorBaseline = CaptureAuditorBaseline(context.CancellationToken);
            string auditorRecoveryDirectory = Path.Combine(BuildActionCommon.ProjectRoot, "Library/Nova/PlayerReportRecovery", Guid.NewGuid().ToString("N"));
            var state = new State
            {
                Target = target,
                OutputPath = outputPath,
                DevelopmentBuild = request.developmentBuild,
                BuildMode = buildMode,
                BuildModeLogical = request.buildMode,
                Scenes = scenes,
                Settings = settings,
                Inputs = inputs,
                OutputBaseline = outputBaseline,
                Callbacks = callbacks,
                AuditorBaseline = auditorBaseline,
                AuditorRecoveryDirectory = auditorRecoveryDirectory,
            };
            var recovery = new BuildActionCommon.ArtifactReceipt
            {
                target = target.ToString(),
                outputPath = outputPath,
                scenes = scenes,
                developmentBuild = request.developmentBuild,
                buildMode = request.buildMode,
            };
            bool outputExists = File.Exists(outputPath) || Directory.Exists(outputPath);
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将以精确路径为活动平台 {target} 构建 Player：{outputPath}。",
                State = state,
                DataJson = Util.Json.Serialize(new PlanView
                {
                    target = target.ToString(),
                    outputPath = outputPath,
                    developmentBuild = request.developmentBuild,
                    buildMode = request.buildMode,
                    scenes = scenes,
                    outputAlreadyExists = outputExists,
                    settings = settings,
                    inputSha256 = inputs.sha256,
                    sceneInputs = inputs.scenes,
                    masterPath = inputs.masterPath,
                    runtimePath = inputs.runtimePath,
                    platform = inputs.platform,
                    channel = inputs.channel,
                    developMode = inputs.developMode,
                    yooSettingsPath = inputs.yooSettingsPath,
                    collectorPath = inputs.collectorPath,
                    stagingTarget = inputs.stagingTarget,
                    effectiveIdentities = inputs.effectiveIdentities,
                    outputBaseline = outputBaseline,
                    callbacks = callbacks,
                    auditorBaseline = auditorBaseline,
                }),
                WriteSet = GetWriteSet(inputs, outputPath, request.buildMode).Concat(callbacks.writes)
                    .Concat(auditorBaseline.Length == 0 ? Array.Empty<string>() : new[] { auditorRecoveryDirectory + " (owned report recovery, removed after completion)" })
                    .Distinct(StringComparer.Ordinal).ToArray(),
                Evidence = new[]
                {
                    "BuildReport + 精确产物路径、文件/目录类型与 SHA-256。",
                    "BuildReport 不证明 Player 已启动或业务运行正确。",
                },
                RecoveryPayloadJson = Util.Json.Serialize(new PlayerReceipt { schemaVersion = 2, artifact = recovery }),
            });
        }

        /// <summary>复核冻结输入与输出基线后只执行一次，报告和清场均通过才成功。</summary>
        /// <param name="state">原 Plan 保留的准确 State，不能用当前配置重建或替代。</param>
        /// <param name="context">本次执行上下文及取消令牌；写前漂移阻断，写入失败保留实际材料。</param>
        /// <returns>正式报告、产物和完整后验均成立时返回 success；写前不符返回 blocked，构建或恢复未完成返回 partial。</returns>
        public override async Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State plan))
            {
                return AgentActionResult.Create(null, "blocked", "Player Action 的冻结计划状态无效。");
            }
            if (EditorUserBuildSettings.activeBuildTarget != plan.Target)
            {
                return AgentActionResult.Create(null, "blocked", "activeBuildTarget 已变化；不会切换平台或执行旧计划。");
            }
            BuildActionCommon.PlayerSettingsSnapshot currentSettings = BuildActionCommon.CapturePlayerSettings(
                plan.Target, plan.DevelopmentBuild, context.CancellationToken);
            if (!BuildActionCommon.PlayerSettingsEqual(plan.Settings, currentSettings))
            {
                return AgentActionResult.Create(null, "blocked", "Player 平台专属设置或 ProjectSettings 已变化；不会执行旧计划。");
            }
            string[] currentScenes = BuildActionCommon.GetEnabledBuildScenes();
            if (!BuildActionCommon.SceneSnapshotsEqual(plan.Scenes, currentScenes))
            {
                return AgentActionResult.Create(null, "blocked", "Build Settings 场景在确认后变化；不会自动重计划或构建。");
            }
            if (!TryCaptureInputs(plan.Target, plan.OutputPath, plan.DevelopmentBuild, context.CancellationToken,
                    out PlayerInputSnapshot currentInputs, out string inputError, plan.Callbacks.tmpFonts) ||
                !string.Equals(plan.Inputs.sha256, currentInputs.sha256, StringComparison.Ordinal))
            {
                return AgentActionResult.Create(null, "blocked",
                    inputError ?? "场景正文、依赖、Master、Runtime、YooAsset、工具或有效设置已变化，旧 Player 计划失效。");
            }
            if (Util.Json.Serialize(plan.OutputBaseline) != Util.Json.Serialize(CapturePaths(plan.Inputs.outputRoots, context.CancellationToken)))
            {
                return AgentActionResult.Create(null, "blocked", "Player 输出目录内容或存在性已变化，旧覆盖确认失效。");
            }
            CallbackInputSnapshot currentCallbacks = CapturePlayerCallbacks(plan.Target, currentInputs.dependencyPaths, context.CancellationToken);
            if (currentCallbacks.unsupported.Length != 0 || currentCallbacks.sha256 != plan.Callbacks.sha256 ||
                !NormalResetBaselinesEqual(plan.Callbacks.normalResetStates, currentCallbacks.normalResetStates) ||
                !PlayerTmpFontBuildContract.BeforeEquals(plan.Callbacks.tmpFonts, currentCallbacks.tmpFonts) ||
                !PlayerFacebookBuildContract.BeforeEquals(plan.Callbacks.facebook, currentCallbacks.facebook) ||
                !PlayerBuiltinUsageBuildContract.BeforeEquals(plan.Callbacks.builtinUsage, currentCallbacks.builtinUsage) ||
                !PlayerBuildEventsContract.BeforeEquals(plan.Callbacks.buildEvents, currentCallbacks.buildEvents) ||
                !PlayerEmptyUsageBuildContract.BeforeEquals(plan.Callbacks.emptyUsage, currentCallbacks.emptyUsage) ||
                !PlayerFiniteAdditionalBuildContract.BeforeEquals(plan.Callbacks.finiteAdditional, currentCallbacks.finiteAdditional) ||
                !PlayerPackageCallbacksContract.BeforeEquals(plan.Callbacks.packageCallbacks, currentCallbacks.packageCallbacks))
                return AgentActionResult.Create(null, "blocked", "Player 回调、间接实现、资源配置或准确写集已变化，旧计划失效。");
            if (plan.Callbacks.qualityBuiltin != null && (currentCallbacks.qualityBuiltin == null ||
                !ReferenceEquals(plan.Callbacks.qualityBuiltin.assets, currentCallbacks.qualityBuiltin.assets) ||
                !ReferenceEquals(plan.Callbacks.qualityBuiltin.types, currentCallbacks.qualityBuiltin.types)))
                return AgentActionResult.Create(null, "blocked", "QualitySettings 原 scratch 容器身份已变化，旧计划失效；不清理替换状态。");
            if (Util.Json.Serialize(plan.AuditorBaseline) != Util.Json.Serialize(CaptureAuditorBaseline(context.CancellationToken)))
                return AgentActionResult.Create(null, "blocked", "ProjectAuditor 报告基线已变化，旧报告覆盖确认失效。");

            string parent = Path.GetDirectoryName(plan.OutputPath);
            if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent)) Directory.CreateDirectory(parent);
            BuildReport report = null;
            ProjectAuditorReportScope auditor = null;
            PlayerCallbackExecutionScope callbackScope = null;
            ReportCopyReceipt reportCopy;
            try
            {
                auditor = new ProjectAuditorReportScope(plan.AuditorBaseline, plan.AuditorRecoveryDirectory);
                using (callbackScope = new PlayerCallbackExecutionScope(plan.Callbacks, context.CancellationToken))
                {
                    report = EditorUtil.Build.BuildPlayer(plan.Target, plan.OutputPath, plan.DevelopmentBuild, plan.BuildMode);
                    callbackScope.Complete(report);
                }
                reportCopy = await auditor.CompleteAsync(report);
                if (reportCopy != null && !reportCopy.complete) throw new InvalidOperationException(reportCopy.error);
                context.CancellationToken.ThrowIfCancellationRequested();
                if (report == null || report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    throw new InvalidOperationException("BuildReport 未返回 Succeeded。" );
                }
                string reportOutput = Path.GetFullPath(report.summary.outputPath);
                if (!string.Equals(reportOutput, plan.OutputPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"BuildReport 输出与冻结 outputPath 不一致：expected={plan.OutputPath}, actual={reportOutput}。" );
                }
                if (report.summary.platform != plan.Target || report.summary.options != ResolveOptions(plan.DevelopmentBuild, plan.BuildMode) ||
                    report.summary.totalErrors != 0)
                {
                    throw new InvalidOperationException("BuildReport 平台、选项或错误数与冻结 Player 契约不一致。");
                }
                ValidateStagingClean(plan.Inputs);
                if (!TryCaptureInputs(plan.Target, plan.OutputPath, plan.DevelopmentBuild, context.CancellationToken,
                        out PlayerInputSnapshot postInputs, out string postError, plan.Callbacks.tmpFonts) ||
                    !string.Equals(plan.Inputs.sha256, postInputs.sha256, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(postError ?? "Player 构建改变了未授权输入；不能以 BuildReport 成功代替恢复。");
                }

                BuildActionCommon.ArtifactReceipt receipt = BuildActionCommon.CaptureArtifact(
                    new BuildActionCommon.ArtifactReceipt
                    {
                        target = plan.Target.ToString(),
                        outputPath = plan.OutputPath,
                        scenes = plan.Scenes,
                        developmentBuild = plan.DevelopmentBuild,
                        buildMode = plan.BuildModeLogical,
                    },
                    report.summary.result.ToString(),
                    context.CancellationToken);
                AgentActionResult result = AgentActionResult.Create(null, "success", "Player BuildReport 为 Succeeded，精确输出产物已生成。" );
                var playerReceipt = new PlayerReceipt
                {
                    schemaVersion = 4,
                    artifact = receipt,
                    inputSha256 = plan.Inputs.sha256,
                    scenes = plan.Inputs.scenes,
                    applicationIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(plan.Target))),
                    reportPlatform = report.summary.platform.ToString(),
                    reportOptions = (int)report.summary.options,
                    reportErrors = report.summary.totalErrors,
                    reportGuid = report.summary.guid.ToString(),
                    inputsPreserved = true,
                    stagingClean = true,
                    outputArtifacts = CapturePaths(plan.Inputs.outputRoots, context.CancellationToken),
                    callbackSha256 = plan.Callbacks.sha256,
                    aotArtifact = string.IsNullOrEmpty(plan.Inputs.aotOutput) ? null : CapturePaths(new[] { plan.Inputs.aotOutput }, context.CancellationToken).Single(),
                    aotDlls = CaptureAotDlls(plan.Inputs, context.CancellationToken),
                    reportCopy = reportCopy,
                    normalResetBefore = plan.Callbacks.normalResetStates,
                    normalResetAfter = callbackScope.NormalResetAfter,
                    callbackChainReturned = callbackScope.ChainReturned,
                    normalResetError = callbackScope.NormalResetError,
                    tmpFontBefore = plan.Callbacks.tmpFonts,
                    tmpFontAfter = callbackScope.TmpFontAfter,
                    tmpFontError = callbackScope.TmpFontError,
                    facebookBefore = plan.Callbacks.facebook,
                    facebookAfter = callbackScope.FacebookAfter,
                    facebookError = callbackScope.FacebookError,
                    builtinUsageBefore = plan.Callbacks.builtinUsage,
                    builtinUsageAfter = callbackScope.BuiltinUsageAfter,
                    builtinUsageReport = callbackScope.BuiltinUsageReport,
                    builtinUsageError = callbackScope.BuiltinUsageError,
                    buildEventsBefore = plan.Callbacks.buildEvents,
                    buildEventsAfter = callbackScope.BuildEventsAfter,
                    buildEventsReport = callbackScope.BuildEventsReport,
                    buildEventsError = callbackScope.BuildEventsError,
                    emptyUsageBefore = plan.Callbacks.emptyUsage,
                    emptyUsageAfter = callbackScope.EmptyUsageAfter,
                    emptyUsageReport = callbackScope.EmptyUsageReport,
                    emptyUsageError = callbackScope.EmptyUsageError,
                    packageCallbacksBefore = plan.Callbacks.packageCallbacks,
                    packageCallbacksAfter = callbackScope.PackageCallbacksAfter,
                    packageCallbacksReport = callbackScope.PackageCallbacksReport,
                    packageCallbacksError = callbackScope.PackageCallbacksError,
                    finiteAdditionalBefore = plan.Callbacks.finiteAdditional,
                    finiteAdditionalAfter = callbackScope.FiniteAdditionalAfter,
                    finiteAdditionalReport = callbackScope.FiniteAdditionalReport,
                    finiteAdditionalError = callbackScope.FiniteAdditionalError,
                };
                result.ReceiptJson = Util.Json.Serialize(playerReceipt);
                result.DataJson = result.ReceiptJson;
                result.EvidenceKinds = AgentActionEvidence.Artifact;
                result.Artifacts.Add(plan.OutputPath);
                if (!string.IsNullOrEmpty(plan.Inputs.aotOutput)) result.Artifacts.Add(plan.Inputs.aotOutput);
                result.Evidence.Add("已核对 BuildReport、精确 outputPath、文件/目录类型与 SHA-256。" );
                if (reportCopy != null) result.Evidence.Add("官方报告延迟复制已自行结束，并核对本轮 BuildReport 身份与两个报告文件摘要。");
                if (plan.Callbacks.tmpFonts.fonts.Length != 0)
                    result.Warnings.Add("TMP 字体按官方回调重置；字体和 atlas 可以变 dirty，原文件未保存。编辑器字体缓存和资源监听保留正常状态，未声称恢复原 FontEngine 字体面。");
                result.Warnings.Add("BuildReport 与产物只证明构建完成，不证明 Player 已启动、设备安装或业务运行正确。" );
                auditor.Commit();
                return result;
            }
            catch (Exception exception)
            {
                ReportCopyReceipt recovery = auditor == null ? new ReportCopyReceipt { complete = false, error = exception.Message } :
                    await auditor.RecoverAsync(report, exception.Message);
                AgentActionResult failed = AgentActionResult.Create(null, "partial", "Player 构建或官方延迟报告未完成：" + exception.Message);
                failed.ReceiptJson = Util.Json.Serialize(new PlayerReceipt
                {
                    schemaVersion = 4, reportCopy = recovery,
                    normalResetBefore = plan.Callbacks.normalResetStates,
                    normalResetAfter = callbackScope?.NormalResetAfter,
                    callbackChainReturned = callbackScope?.ChainReturned ?? false,
                    normalResetError = callbackScope?.NormalResetError,
                    tmpFontBefore = plan.Callbacks.tmpFonts,
                    tmpFontAfter = callbackScope?.TmpFontAfter,
                    tmpFontError = callbackScope?.TmpFontError,
                    facebookBefore = plan.Callbacks.facebook,
                    facebookAfter = callbackScope?.FacebookAfter,
                    facebookError = callbackScope?.FacebookError,
                    builtinUsageBefore = plan.Callbacks.builtinUsage,
                    builtinUsageAfter = callbackScope?.BuiltinUsageAfter,
                    builtinUsageReport = callbackScope?.BuiltinUsageReport,
                    builtinUsageError = callbackScope?.BuiltinUsageError,
                    buildEventsBefore = plan.Callbacks.buildEvents,
                    buildEventsAfter = callbackScope?.BuildEventsAfter,
                    buildEventsReport = callbackScope?.BuildEventsReport,
                    buildEventsError = callbackScope?.BuildEventsError,
                    emptyUsageBefore = plan.Callbacks.emptyUsage,
                    emptyUsageAfter = callbackScope?.EmptyUsageAfter,
                    emptyUsageReport = callbackScope?.EmptyUsageReport,
                    emptyUsageError = callbackScope?.EmptyUsageError,
                    packageCallbacksBefore = plan.Callbacks.packageCallbacks,
                    packageCallbacksAfter = callbackScope?.PackageCallbacksAfter,
                    packageCallbacksReport = callbackScope?.PackageCallbacksReport,
                    packageCallbacksError = callbackScope?.PackageCallbacksError,
                    finiteAdditionalBefore = plan.Callbacks.finiteAdditional,
                    finiteAdditionalAfter = callbackScope?.FiniteAdditionalAfter,
                    finiteAdditionalReport = callbackScope?.FiniteAdditionalReport,
                    finiteAdditionalError = callbackScope?.FiniteAdditionalError,
                });
                failed.DataJson = failed.ReceiptJson;
                if (!string.IsNullOrEmpty(recovery.recoveryDirectory)) failed.Artifacts.Add(recovery.recoveryDirectory);
                return failed;
            }
        }

        /// <summary>重载后仅验证原回执与当前输入、产物，不重放构建。</summary>
        /// <param name="receiptJson">原执行保存的 schema 4 回执，旧版本或缺失材料不能按当前设置补造。</param>
        /// <param name="context">本次只读验证上下文，取消令牌传给文件和输入摘要读取。</param>
        /// <returns>原回执和当前完整合同一致时返回产物验证结果；缺失、漂移或未完成后验返回 partial。</returns>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try
            {
                PlayerReceipt receipt = Util.Json.Deserialize<PlayerReceipt>(receiptJson);
                if (receipt?.reportCopy != null && !receipt.reportCopy.complete)
                    return Task.FromResult(AgentActionResult.Create(null, "partial", receipt.reportCopy.error ?? "官方报告失败材料仅供恢复核对，不自动重放或覆盖报告。"));
                if (receipt?.schemaVersion != 4 || receipt.artifact == null || !receipt.artifact.finalized ||
                    !receipt.inputsPreserved || !receipt.stagingClean || string.IsNullOrEmpty(receipt.inputSha256))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", "Player 回执缺少完整输入保持与清场证据。"));
                if (!BuildActionCommon.TryResolveActiveTarget(receipt.artifact.target, out BuildTarget target, out string error) ||
                    !TryResolveBuildMode(receipt.artifact.buildMode, out EditorUtil.Build.BuildMode mode, out error) ||
                    receipt.artifact.developmentBuild == null || receipt.reportPlatform != receipt.artifact.target ||
                    receipt.reportErrors != 0 || receipt.reportOptions != (int)ResolveOptions(receipt.artifact.developmentBuild.Value, mode))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", error ?? "Player 回执平台或 BuildReport 摘要无效。"));
                if (!TryCaptureInputs(target, receipt.artifact.outputPath, receipt.artifact.developmentBuild.Value,
                        context.CancellationToken, out PlayerInputSnapshot inputs, out error, receipt.tmpFontBefore) || inputs.sha256 != receipt.inputSha256 ||
                    Util.Json.Serialize(inputs.scenes) != Util.Json.Serialize(receipt.scenes))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", error ?? "Player 冻结输入与回执已漂移。"));
                ValidateStagingClean(inputs);
                CallbackInputSnapshot callbacks = CapturePlayerCallbacksObserved(target, inputs.dependencyPaths, context.CancellationToken, false, receipt.tmpFontBefore, receipt.tmpFontAfter);
                if (callbacks.unsupported.Length != 0 || callbacks.sha256 != receipt.callbackSha256)
                    return Task.FromResult(AgentActionResult.Create(null, "partial", "Player 回调输入与回执已漂移。"));
                if (!receipt.callbackChainReturned || !string.IsNullOrEmpty(receipt.normalResetError) ||
                    !TryValidateNormalResetReceipt(receipt.normalResetBefore, receipt.normalResetAfter, callbacks.normalResetStates, out error))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", error ?? receipt.normalResetError ?? "正常回调链或 reset 后验未完成。"));
                if (!string.IsNullOrEmpty(receipt.tmpFontError) || !PlayerTmpFontBuildContract.TryValidateAfter(receipt.tmpFontBefore, receipt.tmpFontAfter, callbacks.tmpFonts, out error))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", error ?? receipt.tmpFontError ?? "TMP 正常清理后验未完成；不重放或预清字体。"));
                if (!string.IsNullOrEmpty(receipt.facebookError) || !PlayerFacebookBuildContract.TryValidateAfter(receipt.facebookBefore, receipt.facebookAfter, callbacks.facebook, out error))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", error ?? receipt.facebookError ?? "Facebook 正常绑定后验未完成；不调用 getter 补证据。"));
                if (!string.IsNullOrEmpty(receipt.builtinUsageError) || !PlayerBuiltinUsageBuildContract.TryValidateAfter(receipt.builtinUsageBefore, receipt.builtinUsageAfter, callbacks.builtinUsage, out error) ||
                    !PlayerBuiltinUsageBuildContract.TryValidateReport(receipt.builtinUsageBefore, receipt.builtinUsageReport, receipt.reportGuid, out error))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", error ?? receipt.builtinUsageError ?? "Unity usage 本地后验/报告材料不足；不重发或声称远端送达。"));
                if (!string.IsNullOrEmpty(receipt.buildEventsError) || !PlayerBuildEventsContract.TryValidateAfter(receipt.buildEventsBefore, receipt.buildEventsAfter, callbacks.buildEvents, out error) ||
                    !PlayerBuildEventsContract.TryValidateReport(receipt.buildEventsBefore, receipt.buildEventsReport, receipt.reportGuid, out error))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", error ?? receipt.buildEventsError ?? "BuildEvents 本地后验/真实报告不足；不补计数或重发。"));
                if (!string.IsNullOrEmpty(receipt.emptyUsageError) || !PlayerEmptyUsageBuildContract.TryValidateAfter(receipt.emptyUsageBefore, receipt.emptyUsageAfter, callbacks.emptyUsage, out error) ||
                    !PlayerEmptyUsageBuildContract.TryValidateReport(receipt.emptyUsageBefore, receipt.emptyUsageReport, receipt.reportGuid, out error))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", error ?? receipt.emptyUsageError ?? "SRP/Adaptive空usage后验不足；不清表或重发。"));
                if (!string.IsNullOrEmpty(receipt.finiteAdditionalError) || !PlayerFiniteAdditionalBuildContract.TryValidateAfter(receipt.finiteAdditionalBefore, receipt.finiteAdditionalAfter, callbacks.finiteAdditional, out error) ||
                    !PlayerFiniteAdditionalBuildContract.TryValidateReport(receipt.finiteAdditionalBefore, receipt.finiteAdditionalReport, receipt.reportGuid, out error))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", error ?? receipt.finiteAdditionalError ?? "五项有限回调后验不足，不重放或补造 usage。"));
                if (!string.IsNullOrEmpty(receipt.packageCallbacksError) || !PlayerPackageCallbacksContract.TryValidateAfter(receipt.packageCallbacksBefore, receipt.packageCallbacksAfter, callbacks.packageCallbacks, out error) ||
                    !PlayerPackageCallbacksContract.TryValidateReport(receipt.packageCallbacksBefore, receipt.packageCallbacksAfter, receipt.packageCallbacksReport, receipt.reportGuid, out error))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", error ?? receipt.packageCallbacksError ?? "Player 包回调后验不足；不重放或补统计。"));
                if (Util.Json.Serialize(CapturePaths(inputs.outputRoots, context.CancellationToken)) != Util.Json.Serialize(receipt.outputArtifacts))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", "Player 主产物或平台伴随产物已漂移。"));
                FileState aotArtifact = string.IsNullOrEmpty(inputs.aotOutput) ? null :
                    CapturePaths(new[] { inputs.aotOutput }, context.CancellationToken).Single();
                if (Util.Json.Serialize(aotArtifact) != Util.Json.Serialize(receipt.aotArtifact))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", "本次最终裁剪 AOT DLL 已漂移。"));
                if (Util.Json.Serialize(CaptureAotDlls(inputs, context.CancellationToken)) != Util.Json.Serialize(receipt.aotDlls))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", "本次 AOT DLL 与正式裁剪输入的逐文件证据已漂移。"));
                if (receipt.reportCopy != null && receipt.reportCopy.reportGuid != receipt.reportGuid)
                    return Task.FromResult(AgentActionResult.Create(null, "partial", "Player 与官方复制回执的 BuildReport GUID 不一致。"));
                if (!VerifyAuditorReceipt(receipt.reportCopy, out error))
                    return Task.FromResult(AgentActionResult.Create(null, "partial", error));
                AgentActionResult result = BuildActionCommon.VerifyArtifact(Util.Json.Serialize(receipt.artifact), "Player", context.CancellationToken);
                result.ReceiptJson = receiptJson;
                return Task.FromResult(result);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "Player 回执验证失败：" + exception.Message));
            }
        }

        /// <summary>精确解析三档构建模式，不接受默认回退。</summary>
        private static bool TryResolveBuildMode(
            string value,
            out EditorUtil.Build.BuildMode buildMode,
            out string error)
        {
            switch (value)
            {
                case "build": buildMode = EditorUtil.Build.BuildMode.Build; error = null; return true;
                case "clean-build": buildMode = EditorUtil.Build.BuildMode.CleanBuild; error = null; return true;
                case "force-skip-data-build": buildMode = EditorUtil.Build.BuildMode.ForceSkipDataBuild; error = null; return true;
                default:
                    buildMode = default;
                    error = "buildMode 仅允许 build、clean-build 或 force-skip-data-build。";
                    return false;
            }
        }

        /// <summary>拒绝源码覆盖、符号链接和平台类型歧义，保留已有四字段调用。</summary>
        internal static bool TryValidateOutput(BuildTarget target, string outputPath, out string error)
        {
            error = null;
            string root = BuildActionCommon.ProjectRoot;
            string output = Path.GetFullPath(outputPath);
            if (IsWithin(root, output) || string.Equals(root, output, StringComparison.OrdinalIgnoreCase))
            {
                error = "Player 输出不能覆盖工程根目录或其祖先。";
                return false;
            }
            if (IsWithin(output, root) && !IsWithin(output, Path.Combine(root, "Build")) &&
                !IsWithin(output, Path.Combine(root, "Builds")))
            {
                error = "工程内 Player 输出必须位于 Build 或 Builds 正式产物子目录，不能覆盖源码、资源或配置。";
                return false;
            }
            try { RejectLinks(output); }
            catch (IOException exception) { error = exception.Message; return false; }
            bool directory = target == BuildTarget.WebGL || target == BuildTarget.StandaloneOSX;
            if (directory && File.Exists(output) || !directory && Directory.Exists(output))
            {
                error = "Player 输出现存类型与目标平台不一致。";
                return false;
            }
            string extension = Path.GetExtension(output);
            if (target == BuildTarget.StandaloneOSX && !string.Equals(extension, ".app", StringComparison.OrdinalIgnoreCase) ||
                (target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64) && !string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase) ||
                target == BuildTarget.WebGL && !string.IsNullOrEmpty(extension))
            {
                error = "Player 输出格式不匹配：macOS 使用 .app，Windows 使用 .exe，WebGL 使用无文件后缀的目录。";
                return false;
            }
            return true;
        }

        /// <summary>按公共 BuildPlayer 的原始规则计算选项，便于核对报告。</summary>
        internal static BuildOptions ResolveOptions(bool developmentBuild, EditorUtil.Build.BuildMode mode)
        {
            BuildOptions result = developmentBuild ? BuildOptions.Development : BuildOptions.None;
            if (mode == EditorUtil.Build.BuildMode.CleanBuild) result |= BuildOptions.CleanBuildCache;
            if (mode == EditorUtil.Build.BuildMode.ForceSkipDataBuild) result |= BuildOptions.BuildScriptsOnly;
            return result;
        }

        /// <summary>冻结实际源/内存/工具；准确导入字体零纹理缓存仅作非保存分类，本轮TMP字体和atlas只投影dirty存在性，其余完整JSON与dirty仍保护。</summary>
        /// <param name="target">本次准确活动目标。</param>
        /// <param name="outputPath">已验证精确输出。</param>
        /// <param name="developmentBuild">冻结开发选项。</param>
        /// <param name="token">真实文件读取取消信号。</param>
        /// <param name="snapshot">实际文件/有效对象/场景输入。</param>
        /// <param name="error">无法安全捕获的具体原因。</param>
        /// <param name="approvedTmp">Plan已验证或原回执保存的准确TMP字体/atlas集合；初次基础捕获为null。</param>
        /// <param name="generating">仅内部 Generate adapter 提供的准确输出拥有域；默认 null 保持普通 Player 完整输入合同。</param>
        /// <param name="approvedSvs">仅Generate严格SVS前后态校验签发的短命许可；默认null保持Player/Bundle原语义。</param>
        /// <returns>输入可完整冻结时为true；不会设置或清理任何dirty标志。</returns>
        private static bool TryCaptureInputs(BuildTarget target, string outputPath, bool developmentBuild, CancellationToken token,
            out PlayerInputSnapshot snapshot, out string error, PlayerTmpFontBuildContract.Snapshot approvedTmp = null,
            HybridClrGenerateInputProjection generating = null, GenerateSceneVisibilityContract.ValidatedCapture approvedSvs = null) => TryCaptureInputsWithDiagnostics(target, outputPath, developmentBuild, token, out snapshot, out error, out _, approvedTmp, generating, approvedSvs, false);

        /// <summary>同一次读取可额外返回非序列化纯诊断；冻结实际源/内存/工具；准确导入字体零纹理缓存仅作非保存分类，本轮TMP字体和atlas只投影dirty存在性，其余完整JSON与dirty仍保护。</summary>
        /// <param name="target">本次准确活动目标。</param>
        /// <param name="outputPath">已验证精确输出。</param>
        /// <param name="developmentBuild">冻结开发选项。</param>
        /// <param name="token">真实文件读取取消信号。</param>
        /// <param name="snapshot">实际文件/有效对象/场景输入。</param>
        /// <param name="fontCacheDecisions">本次dirty过滤实际执行的Texture2D guard纯记录，不进入Snapshot/输入SHA/Store或权限。</param>
        /// <param name="collectFontCacheDiagnostics">仅Generate原始/准备两次捕获显式收集；原九参wrapper传false保持其它调用不记录。</param>
        /// <param name="error">无法安全捕获的具体原因。</param>
        /// <param name="approvedTmp">Plan已验证或原回执保存的准确TMP字体/atlas集合；初次基础捕获为null。</param>
        /// <param name="generating">仅内部 Generate adapter 提供的准确输出拥有域；默认 null 保持普通 Player 完整输入合同。</param>
        /// <param name="approvedSvs">仅Generate严格SVS前后态校验签发的短命许可；默认null保持Player/Bundle原语义。</param>
        /// <returns>输入可完整冻结时为true；不会设置或清理任何dirty标志。</returns>
        private static bool TryCaptureInputsWithDiagnostics(BuildTarget target, string outputPath, bool developmentBuild, CancellationToken token,
            out PlayerInputSnapshot snapshot, out string error, out BuildImportedFontTextureCacheGuard.DecisionTrace[] fontCacheDecisions, PlayerTmpFontBuildContract.Snapshot approvedTmp = null,
            HybridClrGenerateInputProjection generating = null, GenerateSceneVisibilityContract.ValidatedCapture approvedSvs = null, bool collectFontCacheDiagnostics = true)
        {
            snapshot = null;
            error = null;
            fontCacheDecisions = Array.Empty<BuildImportedFontTextureCacheGuard.DecisionTrace>();
            try
            {
                if (generating == null ? !TryValidateOutput(target, outputPath, out error) : !TryValidateGenerateOutput(target, outputPath, generating, out error)) return false;
                if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                { error = "当前 Unity 未安装或未启用请求平台的构建支持。"; return false; }
                SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
                if (setup.Any(scene => string.IsNullOrEmpty(scene.path) || scene.isLoaded &&
                    UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty))
                { error = "Player 计划要求已打开场景已保存且无脏数据；不会保存或关闭用户场景。"; return false; }
                if (!EditorUtil.Config.WorkspaceActive.TryGetPersistedConfigMaster(out ConfigMasterSO master,
                        out string masterGuid, out string masterPath, out error)) return false;
                string demoRoot = YooAssetRuntimeSettingsStaging.ResolveDemoRoot(masterPath);
                ConfigRuntimeSO runtime = master.ExportTarget != null ? master.ExportTarget :
                    AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(demoRoot + "/Configs/ConfigRuntime.asset");
                if (runtime == null || runtime.Platform != master.CurrentPlatform || runtime.Channel != master.CurrentChannel ||
                    runtime.DevelopMode != master.CurrentDevelopMode || master.CurrentPlatform == PlatformType.None)
                { error = "当前 Master 与已保存 ConfigRuntimeSO 的平台、渠道或 DevelopMode 不一致。"; return false; }
                string runtimePath = AssetDatabase.GetAssetPath(runtime);
                EditorUtil.Config.DimensionalResolver.YooAssetResult resolved = EditorUtil.Config.DimensionalResolver.ResolveYooAsset(
                    master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
                YooAssetSettings yoo = AssetDatabase.LoadAssetAtPath<YooAssetSettings>(resolved.YooAssetSettingsPath);
                BundleCollectorSetting collector = AssetDatabase.LoadAssetAtPath<BundleCollectorSetting>(resolved.BundleCollectorSettingPath);
                if (yoo == null || collector == null)
                { error = "当前坐标的权威 YooAssetSettings 或 BundleCollectorSetting 未保存或不可加载。"; return false; }
                if (new UnityEngine.Object[] { master, runtime, yoo, collector }.Any(EditorUtility.IsDirty))
                { error = "Player 直接配置源有未保存修改；请保存或恢复当前目标输入后重新计划。"; return false; }
                string[] resources = Directory.GetDirectories(Path.Combine(BuildActionCommon.ProjectRoot, demoRoot), "Resources", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(BuildActionCommon.ProjectRoot, path).Replace('\\', '/')).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                const string prefix = "Assets/Samples/";
                int editorIndex = masterPath.LastIndexOf("/Editor/", StringComparison.Ordinal);
                bool consumer = masterPath.StartsWith(prefix, StringComparison.Ordinal) && editorIndex >= prefix.Length &&
                    masterPath.Substring(prefix.Length, editorIndex - prefix.Length).Contains("/");
                string resourcesDirectory = YooAssetRuntimeSettingsStaging.ResolveTargetResourcesDirectory(demoRoot, consumer, resources);
                string stagingTarget = resourcesDirectory + "/YooAssetSettings.asset";
                string[] existingRuntime = AssetDatabase.FindAssets("t:YooAssetSettings").Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.EndsWith("/Resources/YooAssetSettings.asset", StringComparison.Ordinal)).ToArray();
                YooAssetRuntimeSettingsStaging.ValidateNoRuntimeSettingsConflicts(stagingTarget, existingRuntime);
                if (File.Exists(YooAssetRuntimeSettingsStaging.GetMarkerFilePath()) || File.Exists(PhysicalPath(stagingTarget)) ||
                    File.Exists(PhysicalPath(stagingTarget) + ".meta"))
                { error = "YooAsset staging 存在旧 marker、副本或 meta；只读 Plan 不恢复或覆盖。"; return false; }

                string[] scenes = BuildActionCommon.GetEnabledBuildScenes();
                if (scenes.Length == 0 || scenes.Distinct(StringComparer.Ordinal).Count() != scenes.Length)
                { error = "Player 启用场景为空或存在重复路径。"; return false; }
                var roots = new HashSet<string>(scenes.Concat(setup.Select(scene => scene.path)), StringComparer.Ordinal)
                { masterPath, runtimePath, resolved.YooAssetSettingsPath, resolved.BundleCollectorSettingPath };
                string[] assetPaths = AssetDatabase.GetAllAssetPaths();
                foreach (string path in assetPaths.Where(path => !AssetDatabase.IsValidFolder(path) &&
                    (path.Contains("/Resources/") || path.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal) ||
                     path.StartsWith("Assets/Plugins/", StringComparison.Ordinal) || path.EndsWith(".asmdef", StringComparison.Ordinal) ||
                     path.EndsWith(".asmref", StringComparison.Ordinal) || path.EndsWith("/link.xml", StringComparison.Ordinal)))) roots.Add(path);
                var files = new HashSet<string>(StringComparer.Ordinal);
                var effective = new List<string>
                {
                    "Target=" + target,
                    "EditorDevelopment=" + EditorUserBuildSettings.development,
                    "StandaloneSubtarget=" + EditorUserBuildSettings.standaloneBuildSubtarget,
                    "RequestedDevelopment=" + developmentBuild,
                    "Runtime=" + runtimePath + "|" + AssetDatabase.AssetPathToGUID(runtimePath),
                    "Master=" + masterPath + "|" + masterGuid,
                    "Coordinates=" + master.CurrentPlatform + "|" + master.CurrentChannel + "|" + master.CurrentDevelopMode,
                    "MasterObject=" + HashObject(master),
                    "RuntimeObject=" + HashObject(runtime),
                    "YooSettingsObject=" + HashObject(yoo),
                    "CollectorObject=" + HashObject(collector),
                };
                UnityEngine.Object[] nativeSettings = Resources.FindObjectsOfTypeAll(typeof(PlayerSettings));
                if (nativeSettings.Length != 1)
                { error = "无法唯一读取当前 native PlayerSettings；不会用落盘文件替代有效内存设置。"; return false; }
                effective.Add("NativePlayerSettings=" + HashObject(nativeSettings[0]));
                UnityEngine.Object[] loadedObjects = Resources.FindObjectsOfTypeAll<UnityEngine.Object>();
                var fontCacheTraceRows = collectFontCacheDiagnostics ? new List<BuildImportedFontTextureCacheGuard.DecisionTrace>() : null;
                BuildImportedFontTextureCacheGuard.DecisionTrace lastFontCacheTrace = null;
                string[] dirtyRows = loadedObjects.Where(value => value != null && EditorUtility.IsPersistent(value) && EditorUtility.IsDirty(value) &&
                    !ExcludeImportedFontCacheWithOptionalTrace(value, fontCacheTraceRows, ref lastFontCacheTrace))
                    .Select(value =>
                    {
                        string row = "PreservedDirtyObject=" + ObjectIdentity(value) + "|" + HashObject(value);
                        if (lastFontCacheTrace != null) lastFontCacheTrace.exactDirtyRow = row;
                        return row;
                    }).ToArray();
                fontCacheDecisions = fontCacheTraceRows?.ToArray() ?? Array.Empty<BuildImportedFontTextureCacheGuard.DecisionTrace>();
                string[] projectedDirtyRows = ProjectDirtyObjectRows(dirtyRows, CaptureApprovedTmpObjectRows(approvedTmp, loadedObjects));
                if (approvedSvs != null)
                {
                    if (generating == null) throw new InvalidOperationException("SVS基线投影只适用于Generate内部拥有域。");
                    projectedDirtyRows = GenerateSceneVisibilityContract.ProjectDirtyRows(projectedDirtyRows, approvedSvs);
                }
                effective.AddRange(projectedDirtyRows);
                var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
                effective.Add("ScriptingBackend=" + PlayerSettings.GetScriptingBackend(namedTarget));
                effective.Add("Defines=" + PlayerSettings.GetScriptingDefineSymbols(namedTarget));
                effective.Add("GraphicsAPIs=" + string.Join(",", PlayerSettings.GetGraphicsAPIs(target).Select(value => value.ToString())));
                foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player).OrderBy(value => value.name, StringComparer.Ordinal))
                {
                    token.ThrowIfCancellationRequested();
                    effective.Add("PlayerAssembly=" + assembly.name + "|" + GenerateActionCommon.ComputeTextHash(string.Join("\n", assembly.defines)));
                    foreach (string source in assembly.sourceFiles) AddFileAndMeta(files, source, true);
                    foreach (string reference in assembly.compiledAssemblyReferences) AddFileAndMeta(files, reference, true);
                }
                string[] dependencies = AssetDatabase.GetDependencies(roots.OrderBy(value => value, StringComparer.Ordinal).ToArray(), true)
                    .Concat(roots).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                foreach (string path in dependencies)
                {
                    token.ThrowIfCancellationRequested();
                    if (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal))
                        AddFileAndMeta(files, path, true);
                }
                foreach (string path in Directory.GetFiles(Path.Combine(BuildActionCommon.ProjectRoot, "ProjectSettings"), "*", SearchOption.AllDirectories))
                    files.Add(Path.GetFullPath(path));
                AddFileAndMeta(files, "Packages/manifest.json", true);
                AddFileAndMeta(files, "Packages/packages-lock.json", true);
                if (!TryCaptureTools(target, namedTarget, effective, files, out error, generating != null)) return false;
                string aotOutput = null;
                string strippedAotSource = null;
                if (File.Exists(PhysicalPath("ProjectSettings/HybridCLRSettings.asset")))
                {
                    var hybrid = SettingsUtil.HybridCLRSettings;
                    if (EditorUtility.IsDirty(hybrid))
                    { error = "HybridCLR Settings 有未保存修改；不以落盘默认代替有效配置。"; return false; }
                    effective.Add("HybridCLRSettings=" + HashObject(hybrid));
                    aotOutput = Path.GetFullPath(SettingsUtil.GetAssembliesPostIl2CppStripDir(target));
                    if (IsWithin(aotOutput, Path.Combine(BuildActionCommon.ProjectRoot, "Assets")) ||
                        IsWithin(aotOutput, Path.Combine(BuildActionCommon.ProjectRoot, "Packages")) ||
                        IsWithin(aotOutput, Path.Combine(BuildActionCommon.ProjectRoot, "ProjectSettings")) ||
                        string.Equals(aotOutput, BuildActionCommon.ProjectRoot, StringComparison.OrdinalIgnoreCase) || IsWithin(BuildActionCommon.ProjectRoot, aotOutput))
                    { error = "HybridCLR AOT 输出不能重建源码、配置、工程根或祖先目录。"; return false; }
                    RejectLinks(aotOutput);
                    if (hybrid.enable)
                    {
                        if (PlayerSettings.GetScriptingBackend(namedTarget) != ScriptingImplementation.IL2CPP)
                        { error = "HybridCLR 当前启用但目标 backend 不是 IL2CPP；不会让回调自动改 PlayerSettings。"; return false; }
                        if (generating == null) EditorUtil.HybridCLR.ValidateMethodBridgeDevelopmentBuild(developmentBuild);
                        files.Add(SettingsUtil.GeneratedCppDir);
                        files.Add(SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target));
                        foreach (string external in hybrid.externalHotUpdateAssembliyDirs ?? Array.Empty<string>())
                        { files.Add(Path.GetFullPath(external)); files.Add(Path.GetFullPath(Path.Combine(external, target.ToString()))); }
                        UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(SettingsUtil).Assembly);
                        if (package?.name != "com.solotopia.hybridclr")
                        { error = "HybridCLR 当前实际包身份无法唯一确认。"; return false; }
                        string packageJson = Path.Combine(package.resolvedPath, "package.json");
                        string localVersion = Path.Combine(SettingsUtil.LocalIl2CppDir, "libil2cpp/hybridclr/generated/libil2cpp-version.txt");
                        AddFileAndMeta(files, packageJson, true);
                        AddFileAndMeta(files, Path.Combine(package.resolvedPath, "Core/Data~/hybridclr_version.json"), true);
                        AddFileAndMeta(files, localVersion, true);
                        string declaredVersion = (string)Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(packageJson))["version"];
                        if (string.IsNullOrEmpty(declaredVersion) || File.ReadAllText(localVersion) != declaredVersion ||
                            !Directory.Exists(Path.Combine(SettingsUtil.LocalIl2CppDir, "libil2cpp/hybridclr")))
                        { error = "HybridCLR 当前安装版本与实际包版本不匹配；只读计划不运行 Installer。"; return false; }
                        if (!hybrid.useGlobalIl2cpp) files.Add(Path.GetFullPath(SettingsUtil.LocalIl2CppDir));
                        strippedAotSource = GetStrippedAotSource(target);
                    }
                }
                IEnumerable<string> protectedInputs = string.IsNullOrEmpty(aotOutput) ? files : files.Concat(new[] { GetStrippedAotSource(target) });
                if (!string.IsNullOrEmpty(aotOutput) && !TryValidateAotOutput(aotOutput, protectedInputs,
                        GetOutputRoots(target, outputPath), out error)) return false;
                var sceneInputs = scenes.Select(path => new SceneInput
                {
                    path = path,
                    guid = AssetDatabase.AssetPathToGUID(path),
                    bodySha256 = GenerateActionCommon.ComputeFileHash(PhysicalPath(path), token),
                    metaSha256 = GenerateActionCommon.ComputeFileHash(PhysicalPath(path) + ".meta", token),
                    dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
                }).ToArray();
                foreach (string folder in assetPaths.Where(path => AssetDatabase.IsValidFolder(path) &&
                    (Path.GetFileName(path) == "Resources" || path == "Assets/StreamingAssets")))
                    effective.Add("InputDirectory=" + folder + "|" + AssetDatabase.AssetPathToGUID(folder));
                snapshot = new PlayerInputSnapshot
                {
                    scenes = sceneInputs,
                    sceneSetup = setup.Select(scene => scene.path + "|" + scene.isLoaded + "|" + scene.isActive).ToArray(),
                    dependencyPaths = dependencies,
                    dependencyIdentities = dependencies.Select(path => path + "|" + AssetDatabase.AssetPathToGUID(path) + "|" + AssetDatabase.GetAssetDependencyHash(path)).ToArray(),
                    files = generating == null ? CapturePaths(files, token) : Array.Empty<FileState>(),
                    effectiveIdentities = effective.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    masterPath = masterPath,
                    masterGuid = masterGuid,
                    runtimePath = runtimePath,
                    runtimeGuid = AssetDatabase.AssetPathToGUID(runtimePath),
                    platform = master.CurrentPlatform.ToString(),
                    channel = master.CurrentChannel.ToString(),
                    developMode = master.CurrentDevelopMode.ToString(),
                    yooSettingsPath = resolved.YooAssetSettingsPath,
                    collectorPath = resolved.BundleCollectorSettingPath,
                    stagingTarget = stagingTarget,
                    resourcesDirectory = resourcesDirectory,
                    resourcesDirectoryExisted = Directory.Exists(PhysicalPath(resourcesDirectory)),
                    resourcesMetaExisted = File.Exists(PhysicalPath(resourcesDirectory) + ".meta"),
                    outputRoots = GetOutputRoots(target, outputPath).Concat(string.IsNullOrEmpty(aotOutput) ? Array.Empty<string>() : new[] { aotOutput }).ToArray(),
                    aotOutput = aotOutput,
                    strippedAotSource = strippedAotSource,
                };
                if (generating != null)
                {
                    // 原始源字节与 meta 已逐项捕获；只规整包含本次生成源码的聚合导入摘要，避免把输出变化冒充源漂移。
                    foreach (string parentMeta in GetGenerateAssetParentMetas(generating.ExportSpec())) files.Add(parentMeta);
                    snapshot.files = generating.Capture(files, token).Select(row => new FileState { path = row.path, kind = row.kind, sha256 = row.sha256 }).ToArray();
                    string[] exactOwnedFiles = generating.ExportSpec().files;
                    foreach (string dependency in dependencies)
                        if (generating.Owns(PhysicalPath(dependency)) && !exactOwnedFiles.Contains(PhysicalPath(dependency), StringComparer.Ordinal))
                            throw new InvalidOperationException("Generate 输出与非生成的实际资源依赖相交：" + dependency);
                    snapshot.dependencyPaths = dependencies.Where(path => !generating.Owns(PhysicalPath(path))).ToArray();
                    snapshot.dependencyIdentities = CaptureGenerateDependencyIdentities(snapshot.dependencyPaths, generating, token);
                    foreach (SceneInput scene in snapshot.scenes)
                        scene.dependencyHash = snapshot.dependencyIdentities.Single(row => row.StartsWith(scene.path + "|", StringComparison.Ordinal));
                    snapshot.effectiveIdentities = snapshot.effectiveIdentities.Concat(new[] { "GenerateOwnedInputs=" + generating.Identity() }).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                }
                if (approvedSvs != null && !GenerateSceneVisibilityContract.BeforeEquals(approvedSvs.observed, GenerateSceneVisibilityContract.Capture()))
                    throw new InvalidOperationException("SVS许可签发后在输入捕获中继续变化，不能投影过期观测。");
                snapshot.sha256 = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(snapshot));
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                error = "Player 输入冻结失败：" + exception.Message;
                return false;
            }
        }

        /// <summary>只枚举已批准snapshot对应的现存持久字体/同文件atlas，以完整64位EntityId比对，不通过ID反查加载或创建对象。</summary>
        /// <param name="approved">当前Plan或原回执准确字体集合。</param>
        /// <param name="loaded">本轮已加载对象库存，不承担池/对象销毁所有权。</param>
        /// <returns>无论clean/dirty均记录完整Editor JSON的准确对象行。</returns>
        private static string[] CaptureApprovedTmpObjectRows(PlayerTmpFontBuildContract.Snapshot approved, UnityEngine.Object[] loaded)
        {
            if (approved == null) return Array.Empty<string>();
            if (approved.fonts == null || approved.fonts.Any(font => font == null)) throw new InvalidOperationException("TMP批准字体材料缺失。");
            var rows = new List<string>();
            foreach (PlayerTmpFontBuildContract.FontState font in approved.fonts)
            {
                UnityEngine.Object currentFont = loaded.SingleOrDefault(value => value != null && UnityEngine.EntityId.ToULong(value.GetEntityId()) == font.fontInstanceId);
                UnityEngine.Object currentAtlas = loaded.SingleOrDefault(value => value != null && UnityEngine.EntityId.ToULong(value.GetEntityId()) == font.atlasInstanceId);
                if (currentFont == null || currentAtlas == null || !EditorUtility.IsPersistent(currentFont) || !EditorUtility.IsPersistent(currentAtlas) ||
                    currentFont.GetType() != FindType("TMPro.TMP_FontAsset") || currentAtlas.GetType() != typeof(Texture2D) ||
                    ObjectIdentity(currentFont) != font.fontIdentity || ObjectIdentity(currentAtlas) != font.atlasIdentity ||
                    AssetDatabase.GetAssetPath(currentFont) != font.path || AssetDatabase.GetAssetPath(currentAtlas) != font.path || HashObject(currentFont) != font.fontJsonSha256)
                    throw new InvalidOperationException("TMP批准字体/atlas当前对象身份或字体正文漂移；不补加载或扩大dirty例外。");
                rows.Add("TMPPreservedObject=" + ObjectIdentity(currentFont) + "|" + HashObject(currentFont));
                rows.Add("TMPPreservedObject=" + ObjectIdentity(currentAtlas) + "|" + HashObject(currentAtlas));
            }
            return rows.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        /// <summary>保留Where/Select逐对象交错顺序；只有已记录为Texture2D的该次判定留纯值，原Select稍后绑定已算dirty行。</summary>
        /// <param name="value">原persistent/dirty筛选已通过的当前对象。</param><param name="traces">Generate本次局部收集器；null走原bool入口。</param>
        /// <param name="current">当前对象的纯记录；不能跨另一个对象绑定或含Native引用。</param>
        /// <returns>原guard分类结果，true仍从dirty输入排除；不新增身份/JSON读取。</returns>
        private static bool ExcludeImportedFontCacheWithOptionalTrace(UnityEngine.Object value, List<BuildImportedFontTextureCacheGuard.DecisionTrace> traces, ref BuildImportedFontTextureCacheGuard.DecisionTrace current)
        {
            current = null;
            if (traces == null) return BuildImportedFontTextureCacheGuard.IsImportedEmptyFontTextureCache(value);
            bool excluded = BuildImportedFontTextureCacheGuard.IsImportedEmptyFontTextureCacheWithDiagnostics(value, out var trace);
            if ((string)trace.observed["candidateType"] == typeof(Texture2D).FullName) { current = trace; traces.Add(trace); }
            return excluded;
        }

        /// <summary>纯投影只忽略准确已批准TMP对象的dirty行存在性，完整JSON hash保持；其他dirty原样保留。</summary>
        /// <param name="dirtyRows">实际或初次快照的PreservedDirtyObject行。</param>
        /// <param name="approvedRows">内部准确对象库存生成的TMPPreservedObject行，不来自用户请求。</param>
        /// <returns>稳定有序输入行；身份/摘要冲突和非法材料直接抛出。</returns>
        internal static string[] ProjectDirtyObjectRows(IEnumerable<string> dirtyRows, IEnumerable<string> approvedRows)
        {
            var approved = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string row in approvedRows)
            {
                ParseObjectInputRow(row, "TMPPreservedObject=", out string identity, out string hash);
                if (approved.ContainsKey(identity)) throw new InvalidOperationException("TMP批准对象身份重复。");
                approved.Add(identity, hash);
            }
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string row in dirtyRows)
            {
                ParseObjectInputRow(row, "PreservedDirtyObject=", out string identity, out string hash);
                if (!seen.Add(identity)) throw new InvalidOperationException("dirty对象身份重复。");
                if (!approved.TryGetValue(identity, out string expected)) result.Add(row);
                else if (expected != hash) throw new InvalidOperationException("TMP原dirty对象的完整JSON正文变化；不投影正文差异。");
            }
            result.AddRange(approved.Select(pair => "TMPPreservedObject=" + pair.Key + "|" + pair.Value));
            return result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        /// <summary>按最后一个分隔符解析完整身份和规范SHA，路径内字符不会造成前缀误匹配。</summary>
        /// <param name="row">内部构造的准确行。</param>
        /// <param name="prefix">调用点固定允许的行类型。</param>
        /// <param name="identity">完整类型/GUID/fileID/path身份。</param>
        /// <param name="sha256">完整JSON SHA。</param>
        private static void ParseObjectInputRow(string row, string prefix, out string identity, out string sha256)
        {
            int split = row?.LastIndexOf('|') ?? -1;
            if (row == null || !row.StartsWith(prefix, StringComparison.Ordinal) || split <= prefix.Length || row.Length - split - 1 != 64)
                throw new InvalidOperationException("对象输入行身份/摘要形状无效。");
            identity = row.Substring(prefix.Length, split - prefix.Length); sha256 = row.Substring(split + 1);
            if (sha256.Any(value => !(value >= '0' && value <= '9' || value >= 'a' && value <= 'f'))) throw new InvalidOperationException("对象输入摘要非规范SHA256。");
        }

        /// <summary>合法Load后只允许上述精确TMP行投影，所有原文件/场景/设置/其他dirty必须仍逐字段相等。</summary>
        /// <param name="original">任何回调资产加载前原输入。</param>
        /// <param name="prepared">已验证TMP加载后的准确投影输入。</param>
        /// <returns>未吸收任何其他新输入时为true，不修改两份原材料。</returns>
        private static bool PreparedInputsPreserveOriginal(PlayerInputSnapshot original, PlayerInputSnapshot prepared)
        {
            if (original == null || prepared == null) return false;
            PlayerInputSnapshot before = Util.Json.Deserialize<PlayerInputSnapshot>(Util.Json.Serialize(original));
            PlayerInputSnapshot after = Util.Json.Deserialize<PlayerInputSnapshot>(Util.Json.Serialize(prepared));
            before.sha256 = null; after.sha256 = null;
            try
            {
                before.effectiveIdentities = before.effectiveIdentities.Where(row => !row.StartsWith("PreservedDirtyObject=", StringComparison.Ordinal))
                    .Concat(ProjectDirtyObjectRows(before.effectiveIdentities.Where(row => row.StartsWith("PreservedDirtyObject=", StringComparison.Ordinal)),
                        after.effectiveIdentities.Where(row => row.StartsWith("TMPPreservedObject=", StringComparison.Ordinal))))
                    .OrderBy(row => row, StringComparer.Ordinal).ToArray();
                return Util.Json.Serialize(before) == Util.Json.Serialize(after);
            }
            catch (InvalidOperationException) { return false; }
        }

        /// <summary>读取当前 Unity 正规模块路径与实际编译工具，不执行版本命令或修改 PATH。</summary>
        /// <param name="target">本次实际目标，决定真实 PlaybackEngine。</param>
        /// <param name="namedTarget">对应的脚本后端查询域。</param>
        /// <param name="identities">追加引擎、工具和模板有效身份的本次局部集合。</param>
        /// <param name="files">追加必须保持的工具文件和目录，不赋予写入权。</param>
        /// <param name="error">当前模块、模板或覆盖设置不受支持时的具体原因。</param>
        /// <param name="generating">仅 Generate 可使用原包装器的准确缺失 PROJECT 模板回退；默认 false 保持 Player 原行为。</param>
        /// <returns>全部当前工具和模板来源均可冻结时为 true。</returns>
        private static bool TryCaptureTools(BuildTarget target, NamedBuildTarget namedTarget, ICollection<string> identities,
            ISet<string> files, out string error, bool generating = false)
        {
            error = null;
            string module = BuildPipeline.GetPlaybackEngineDirectory(target, BuildOptions.None);
            if (string.IsNullOrEmpty(module) || !Directory.Exists(module))
            { error = "当前目标的实际 PlaybackEngine 路径不可用。"; return false; }
            identities.Add("UnityVersion=" + Application.unityVersion);
            identities.Add("UnityApplication=" + EditorApplication.applicationPath);
            identities.Add("PlaybackEngine=" + Path.GetFullPath(module));
            string contents = EditorApplication.applicationContentsPath;
            AddFileAndMeta(files, Application.platform == RuntimePlatform.OSXEditor ? Path.Combine(contents, "MacOS/Unity") : EditorApplication.applicationPath, true);
            AddFileAndMeta(files, typeof(BuildPipeline).Assembly.Location, true);
            string resources = Path.Combine(contents, "Resources");
            AddFileAndMeta(files, Path.Combine(resources, "BuildPipeline/bee_backend"), true);
            if (PlayerSettings.GetScriptingBackend(namedTarget) == ScriptingImplementation.IL2CPP)
            {
                AddFileAndMeta(files, Path.Combine(resources, "Scripting/il2cpp/build/deploy/il2cpp"), true);
                AddFileAndMeta(files, Path.Combine(resources, "Scripting/il2cpp/build/deploy/UnityLinker"), true);
            }
            if (target == BuildTarget.WebGL)
            {
                if (!string.IsNullOrEmpty(PlayerSettings.WebGL.modulesDirectory))
                { error = "当前 WebGL modulesDirectory 自定义覆盖尚无冻结工具契约。"; return false; }
                string[] relative =
                {
                    "UnityEditor.WebGL.Extensions.dll", "BuildTools/Emscripten/emscripten/emscripten-version.txt",
                    "BuildTools/Emscripten/emscripten/emcc", "BuildTools/Emscripten/emscripten/emcc.py",
                    "BuildTools/Emscripten/llvm/clang", "BuildTools/Emscripten/llvm/wasm-ld",
                    "BuildTools/Emscripten/node/node", "BuildTools/Emscripten/python/bin/python3",
                };
                foreach (string path in relative) AddFileAndMeta(files, Path.Combine(module, path), true);
                string template = PlayerSettings.WebGL.template;
                string templatePath;
                if (template.StartsWith("PROJECT:", StringComparison.Ordinal))
                    templatePath = Path.Combine(BuildActionCommon.ProjectRoot, "Assets/WebGLTemplates", template.Substring(8));
                else if (template.StartsWith("APPLICATION:", StringComparison.Ordinal))
                    templatePath = Path.Combine(module, "BuildTools/WebGLTemplates/Base", template.Substring(12));
                else { error = "当前 WebGL template 没有可解析的 PROJECT/APPLICATION 来源。"; return false; }
                if (generating && template.StartsWith("PROJECT:", StringComparison.Ordinal) && !Directory.Exists(templatePath))
                {
                    files.Add(Path.GetFullPath(templatePath));
                    templatePath = Path.Combine(module, "BuildTools/WebGLTemplates/Base/Default");
                    identities.Add("GenerateTemporaryWebGLTemplate=APPLICATION:Default");
                }
                if (!Directory.Exists(templatePath)) { error = "当前 WebGL 模板源目录不存在：template=" + template + "; resolved=" + templatePath; return false; }
                files.Add(Path.GetFullPath(templatePath));
                files.Add(Path.Combine(module, "BuildTools/WebGLTemplates/WebGLIncludes"));
                identities.Add("WebGLTemplate=" + template + "|" + Path.GetFullPath(templatePath));
            }
            else if (target == BuildTarget.Android && generating)
            {
                AndroidGenerateToolchainContract.Snapshot tools = AndroidGenerateToolchainContract.Capture(CancellationToken.None);
                identities.Add("AndroidGenerateToolchain=" + AndroidGenerateToolchainContract.StableIdentity(tools));
                foreach (string path in AndroidGenerateToolchainContract.InputFiles(tools))
                {
                    files.Add(path);
                    if (File.Exists(path + ".meta")) files.Add(path + ".meta");
                }
            }
            return true;
        }

        /// <summary>只读记录缺失、文件或目录，并拒绝链接穿透；空目录也有真实指纹。</summary>
        internal static FileState[] CapturePaths(IEnumerable<string> paths, CancellationToken token)
        {
            return paths.Select(PhysicalPath).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).Select(path =>
            {
                token.ThrowIfCancellationRequested();
                RejectLinks(path);
                if (File.Exists(path)) return new FileState { path = path, kind = "file", sha256 = GenerateActionCommon.ComputeFileHash(path, token) };
                if (!Directory.Exists(path)) return new FileState { path = path, kind = "absent" };
                var inventory = new List<string>();
                foreach (string directory in Directory.GetDirectories(path, "*", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
                { RejectLinks(directory); inventory.Add("D|" + Path.GetRelativePath(path, directory).Replace('\\', '/')); }
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
                { token.ThrowIfCancellationRequested(); RejectLinks(file); inventory.Add("F|" + Path.GetRelativePath(path, file).Replace('\\', '/') + "|" + GenerateActionCommon.ComputeFileHash(file, token)); }
                return new FileState { path = path, kind = "directory", sha256 = GenerateActionCommon.ComputeTextHash(string.Join("\n", inventory)) };
            }).ToArray();
        }

        /// <summary>记录正文与存在的 meta，源缺失时明确失败。</summary>
        private static void AddFileAndMeta(ISet<string> files, string path, bool required)
        {
            string physical = PhysicalPath(path);
            if (required && !File.Exists(physical) && !Directory.Exists(physical)) throw new FileNotFoundException("构建源或工具不存在：" + physical, physical);
            if (File.Exists(physical) || Directory.Exists(physical)) files.Add(physical);
            if (File.Exists(physical + ".meta")) files.Add(physical + ".meta");
        }

        /// <summary>哈希有效序列化内存状态，不输出内容，不触发保存。</summary>
        private static string HashObject(UnityEngine.Object value)
        {
            if (value == null) throw new InvalidOperationException("输入对象为空。");
            return GenerateActionCommon.ComputeTextHash(EditorJsonUtility.ToJson(value));
        }

        /// <summary>Assets/Packages 使用 Unity 实际映射，外部工具保留绝对路径身份。</summary>
        private static string PhysicalPath(string path)
        {
            string physical = Path.IsPathRooted(path) ? path : FileUtil.GetPhysicalPath(path);
            return Path.GetFullPath(Path.IsPathRooted(physical) ? physical : Path.Combine(BuildActionCommon.ProjectRoot, string.IsNullOrEmpty(physical) ? path : physical));
        }

        /// <summary>路径边界按目录分隔符判断，避免同前缀目录误判。</summary>
        private static bool IsWithin(string path, string root)
        {
            return Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>只定位已审官方类型；按实际 Type 去重 UnityEditor 转发入口，不合并同名的不同类型，不实例化或调用回调。</summary>
        /// <returns>唯一实际官方类型；当前域未加载时为空，存在不同实际类型时拒绝。</returns>
        private static Type GetAuditorType()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(assembly =>
                assembly.GetType("Unity.ProjectAuditor.Editor.Build.LastBuildReportProvider", false))
                .Where(type => type != null).Distinct().SingleOrDefault();
        }

        /// <summary>核对已审静态报告字段与官方延迟函数形状，未知版本拒绝继承合同。</summary>
        private static void ValidateAuditorIdentity(Type type)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo report = type.GetField("s_LastBuildReport", flags);
            MethodInfo copy = type.GetMethod("DelayedBuildReportCopy", flags, null, Type.EmptyTypes, null);
            if (report?.FieldType != typeof(BuildReport) || copy?.ReturnType != typeof(void))
                throw new InvalidOperationException("ProjectAuditor 报告字段或延迟方法与已审实现不一致。");
        }

        /// <summary>只读官方 update 的准确订阅状态，不执行或移除任何官方回调。</summary>
        private static bool HasAuditorPendingCopy(Type type)
        {
            MethodInfo method = type.GetMethod("DelayedBuildReportCopy", BindingFlags.Static | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            return EditorApplication.update?.GetInvocationList().Any(callback => callback.Method == method) == true;
        }

        /// <summary>报告输出单独冻结，避免正规 Build 覆盖 LastBuild 被误算为不可变构建输入。</summary>
        private static FileState[] CaptureAuditorBaseline(CancellationToken token)
        {
            Type type = GetAuditorType();
            if (type == null) return Array.Empty<FileState>();
            ValidateAuditorIdentity(type);
            if (HasAuditorPendingCopy(type)) throw new InvalidOperationException("ProjectAuditor 旧延迟复制尚未结束。");
            FileState[] files = CapturePaths(new[] { "Library/LastBuild.buildreport", "Library/LastCleanBuild.buildreport" }, token);
            if (files.Any(file => file.kind == "directory")) throw new InvalidOperationException("官方报告路径被目录占用。");
            return files;
        }

        /// <summary>完成证据绑定准确源和目标；报告未复制分支也须保持目标基线。</summary>
        internal static bool ValidateReportCopyEvidence(bool copied, FileState source, FileState destination,
            FileState destinationBaseline, out string error)
        {
            if (source?.kind != "file" || string.IsNullOrEmpty(source.sha256))
            { error = "本次官方 LastBuild 报告不存在或没有文件摘要。"; return false; }
            if (copied ? destination?.kind != "file" || destination.sha256 != source.sha256 :
                Util.Json.Serialize(destination) != Util.Json.Serialize(destinationBaseline))
            { error = copied ? "官方延迟复制目标未与本次 LastBuild 字节一致。" : "无 packedAssets 分支改变了未授权的 LastClean 报告。"; return false; }
            error = null;
            return true;
        }

        /// <summary>在主线程异步等待已冻结的报告状态谓词，有限超时且不阻塞 Editor update。</summary>
        internal static async UniTask<bool> WaitForNoPendingReportAsync(Func<bool> hasPending, double timeoutSeconds)
        {
            double expires = EditorApplication.timeSinceStartup + timeoutSeconds;
            while (hasPending())
            {
                if (EditorApplication.timeSinceStartup >= expires) return false;
                await UniTask.Delay(50, ignoreTimeScale: true, cancellationToken: CancellationToken.None);
            }
            return true;
        }

        /// <summary>回执 Verify 只读核两报告摘要与无延迟订阅，不调用报告处理器补造旧证据。</summary>
        private static bool VerifyAuditorReceipt(ReportCopyReceipt receipt, out string error)
        {
            Type type = GetAuditorType();
            if (type == null)
            { error = receipt == null ? null : "官方报告类型已变化。"; return receipt == null; }
            if (receipt?.complete != true || string.IsNullOrEmpty(receipt.reportGuid) || HasAuditorPendingCopy(type))
            { error = "官方报告回执缺完成身份或仍有待执行复制。"; return false; }
            FileState[] files = CaptureAuditorBaseline(CancellationToken.None);
            if (Util.Json.Serialize(files.Single(file => Path.GetFileName(file.path) == "LastBuild.buildreport")) != Util.Json.Serialize(receipt.source) ||
                Util.Json.Serialize(files.Single(file => Path.GetFileName(file.path) == "LastCleanBuild.buildreport")) != Util.Json.Serialize(receipt.destination))
            { error = "官方报告源或复制目标已漂移。"; return false; }
            BuildReport latest = BuildReport.GetLatestReport();
            if (latest == null || latest.summary.guid.ToString() != receipt.reportGuid)
            { error = "当前官方 LastBuild 的 BuildReport GUID 与原回执不一致。"; return false; }
            error = null;
            return true;
        }

        /// <summary>保留本轮准确报告基线，等待官方 update 自行完成复制，并在失败时保留可恢复材料。</summary>
        private sealed class ProjectAuditorReportScope
        {
            private readonly Type m_Type;
            private readonly BuildReport m_PreviousReport;
            private readonly FileState[] m_Baseline;
            private readonly string m_RecoveryDirectory;
            private readonly string[] m_NewParentDirectories;
            private bool m_TimedOut;

            /// <summary>在 Build 前写入已声明的本地备份；原路径及正文不进入外部输出。</summary>
            public ProjectAuditorReportScope(FileState[] baseline, string recoveryDirectory)
            {
                m_Type = GetAuditorType();
                m_Baseline = baseline;
                m_RecoveryDirectory = m_Type == null ? null : recoveryDirectory;
                if (m_Type == null) return;
                m_PreviousReport = (BuildReport)m_Type.GetField("s_LastBuildReport", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                RejectLinks(recoveryDirectory);
                if (Directory.Exists(recoveryDirectory) || File.Exists(recoveryDirectory))
                    throw new InvalidOperationException("本轮官方报告恢复目录已被占用。");
                var parents = new List<string>();
                for (string parent = Path.GetDirectoryName(recoveryDirectory); !Directory.Exists(parent); parent = Path.GetDirectoryName(parent)) parents.Add(parent);
                m_NewParentDirectories = parents.ToArray();
                try
                {
                    Directory.CreateDirectory(recoveryDirectory);
                    foreach (FileState file in baseline.Where(file => file.kind == "file"))
                        File.Copy(file.path, Path.Combine(recoveryDirectory, Path.GetFileName(file.path)));
                    File.WriteAllText(Path.Combine(recoveryDirectory, "baseline.json"), Util.Json.Serialize(baseline));
                }
                catch { Commit(); throw; }
            }

            /// <summary>最多等待十秒且不阻塞 Editor update；取消也先让准确官方回调自行结束。</summary>
            private async UniTask<bool> WaitForOfficialCopyAsync()
            {
                if (m_Type == null) return true;
                bool complete = await WaitForNoPendingReportAsync(() => HasAuditorPendingCopy(m_Type), 10);
                m_TimedOut |= !complete;
                return complete;
            }

            /// <summary>完成时核对本轮 BuildReport 对象身份、复制自注销及源目标完整字节证据。</summary>
            public async UniTask<ReportCopyReceipt> CompleteAsync(BuildReport report)
            {
                if (m_Type == null) return null;
                if (!await WaitForOfficialCopyAsync())
                    return new ReportCopyReceipt { complete = false, error = "官方报告复制十秒内未结束；保留原报告备份，不移除官方回调。", recoveryDirectory = m_RecoveryDirectory };
                bool copied = report != null && report.packedAssets.Length != 0;
                BuildReport official = (BuildReport)m_Type.GetField("s_LastBuildReport", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                if (report == null || copied && official != report)
                    throw new InvalidOperationException("官方延迟报告不是本轮 BuildReport，拒绝继承其他构建证据。");
                BuildReport latest = BuildReport.GetLatestReport();
                if (latest == null || latest.summary.guid != report.summary.guid)
                    throw new InvalidOperationException("官方 LastBuild 报告 GUID 未绑定本轮构建。");
                FileState[] files = CaptureAuditorBaseline(CancellationToken.None);
                FileState source = files.Single(file => Path.GetFileName(file.path) == "LastBuild.buildreport");
                FileState destination = files.Single(file => Path.GetFileName(file.path) == "LastCleanBuild.buildreport");
                if (!ValidateReportCopyEvidence(copied, source, destination,
                        m_Baseline.Single(file => Path.GetFileName(file.path) == "LastCleanBuild.buildreport"), out string error))
                    throw new InvalidOperationException(error);
                return new ReportCopyReceipt { complete = true, copied = copied, reportGuid = report.summary.guid.ToString(), source = source, destination = destination };
            }

            /// <summary>失败仅在官方复制已结束且能证明本轮归属时恢复 LastClean；不能安全恢复则保留备份。</summary>
            public async UniTask<ReportCopyReceipt> RecoverAsync(BuildReport report, string cause)
            {
                var receipt = new ReportCopyReceipt { complete = false, error = cause, recoveryDirectory = m_RecoveryDirectory };
                if (m_Type == null) return receipt;
                try
                {
                    if (m_TimedOut || !await WaitForOfficialCopyAsync())
                    { receipt.error += "；官方延迟复制仍未结束，未执行会被再次覆盖的伪恢复。"; return receipt; }
                    BuildReport official = (BuildReport)m_Type.GetField("s_LastBuildReport", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    FileState baseline = m_Baseline.Single(file => Path.GetFileName(file.path) == "LastCleanBuild.buildreport");
                    FileState current = CapturePaths(new[] { baseline.path }, CancellationToken.None).Single();
                    if (Util.Json.Serialize(current) != Util.Json.Serialize(baseline))
                    {
                        if (official == null || official == m_PreviousReport || report != null && official != report)
                        { receipt.error += "；复制目标归属不明，保留当前文件和原备份。"; return receipt; }
                        FileState source = CapturePaths(new[] { "Library/LastBuild.buildreport" }, CancellationToken.None).Single();
                        if (!ValidateReportCopyEvidence(true, source, current, baseline, out string error))
                        { receipt.error += "；" + error; return receipt; }
                        if (baseline.kind == "file")
                        {
                            string backup = Path.Combine(m_RecoveryDirectory, Path.GetFileName(baseline.path));
                            RejectLinks(backup);
                            if (!File.Exists(backup) || GenerateActionCommon.ComputeFileHash(backup, CancellationToken.None) != baseline.sha256)
                            { receipt.error += "；原报告备份已漂移，不覆盖当前目标。"; return receipt; }
                            File.Copy(backup, baseline.path, true);
                        }
                        else File.Delete(baseline.path);
                        current = CapturePaths(new[] { baseline.path }, CancellationToken.None).Single();
                        if (Util.Json.Serialize(current) != Util.Json.Serialize(baseline))
                        { receipt.error += "；官方 LastClean 报告未恢复基线。"; return receipt; }
                    }
                    receipt.restored = true;
                    receipt.destination = current;
                    Commit();
                    receipt.recoveryDirectory = null;
                }
                catch (Exception exception) { receipt.error += "；报告恢复失败：" + exception.Message; }
                return receipt;
            }

            /// <summary>已完成或已准确恢复后仅删除本轮备份目录，不改变正式报告和官方缓存字段。</summary>
            public void Commit()
            {
                if (!string.IsNullOrEmpty(m_RecoveryDirectory) && Directory.Exists(m_RecoveryDirectory)) Directory.Delete(m_RecoveryDirectory, true);
                foreach (string directory in m_NewParentDirectories ?? Array.Empty<string>())
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            }
        }

        /// <summary>拒绝路径自身与现存祖先的符号链接，避免确认后写入其他目录。</summary>
        private static void RejectLinks(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Player 冻结路径包含符号链接：" + current);
        }

        /// <summary>桌面文件同时冻结伴随数据与引擎库，目录型产物冻结完整目录。</summary>
        private static string[] GetOutputRoots(BuildTarget target, string outputPath)
        {
            if (target == BuildTarget.WebGL || target == BuildTarget.StandaloneOSX) return new[] { outputPath };
            string parent = Path.GetDirectoryName(outputPath);
            string name = Path.GetFileNameWithoutExtension(outputPath);
            return new[] { outputPath, Path.Combine(parent, name + "_Data"), Path.Combine(parent,
                target == BuildTarget.StandaloneLinux64 ? "UnityPlayer.so" : "UnityPlayer.dll"),
                Path.Combine(parent, "MonoBleedingEdge"), Path.Combine(parent, "UnityCrashHandler64.exe"),
                Path.Combine(parent, name + "_BackUpThisFolder_ButDontShipItWithYourGame") };
        }

        /// <summary>复用 HybridCLR 当前正规平台布局，源目录只在本次 Build 后作为 AOT 验证输入。</summary>
        private static string GetStrippedAotSource(BuildTarget target)
        {
            string folder;
            switch (target)
            {
                case BuildTarget.WebGL: folder = "WebGL"; break;
                case BuildTarget.StandaloneOSX: folder = "MacStandalonePlayerBuildProgram"; break;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64: folder = "WinPlayerBuildProgram"; break;
                case BuildTarget.StandaloneLinux64: folder = "LinuxPlayerBuildProgram"; break;
                default: throw new InvalidOperationException("当前目标没有已审的 ManagedStripped AOT 源目录。");
            }
            return Path.Combine(BuildActionCommon.ProjectRoot, "Library/Bee/artifacts", folder, "ManagedStripped");
        }

        /// <summary>以本次已冻结的 Bee 源和 AOT 目标进行完整后验；未启用 HybridCLR 时不补造 DLL 证据。</summary>
        private static FileState[] CaptureAotDlls(PlayerInputSnapshot inputs, CancellationToken token)
        {
            if (string.IsNullOrEmpty(inputs.strippedAotSource)) return Array.Empty<FileState>();
            return CaptureFinalAotDlls(inputs.strippedAotSource, inputs.aotOutput, token);
        }

        /// <summary>拒绝 AOT 重建目录与真实输入或 Player 产物重叠，避免预回调先删除源再由后验报错。</summary>
        internal static bool TryValidateAotOutput(string aotOutput, IEnumerable<string> inputPaths,
            IEnumerable<string> playerOutputs, out string error)
        {
            string output = PhysicalPath(aotOutput);
            foreach (string path in inputPaths.Concat(playerOutputs).Select(PhysicalPath).Distinct(StringComparer.Ordinal))
            {
                if (string.Equals(output, path, StringComparison.OrdinalIgnoreCase) || IsWithin(output, path) || IsWithin(path, output))
                {
                    error = "HybridCLR AOT 重建目录与冻结输入或 Player 产物重叠：" + output + "；冲突路径：" + path;
                    return false;
                }
            }
            error = null;
            return true;
        }

        /// <summary>最终目录只接受回调实际复制的非空平铺 DLL 集，并逐名核对正规 Bee 源的 SHA。</summary>
        internal static FileState[] CaptureFinalAotDlls(string strippedAotSource, string aotOutput, CancellationToken token)
        {
            string sourcePath = PhysicalPath(strippedAotSource);
            string destinationPath = PhysicalPath(aotOutput);
            RejectLinks(sourcePath);
            RejectLinks(destinationPath);
            if (!Directory.Exists(sourcePath) || !Directory.Exists(destinationPath))
                throw new InvalidOperationException("本次正规 ManagedStripped 或最终 AOT 输出不存在。");
            FileState[] source = CapturePaths(Directory.GetFiles(sourcePath, "*.dll"), token);
            FileState[] destination = CapturePaths(Directory.GetFileSystemEntries(destinationPath), token);
            if (source.Length == 0 || source.Length != destination.Length || source.Any(file =>
                !destination.Any(candidate => candidate.kind == "file" && Path.GetFileName(candidate.path) == Path.GetFileName(file.path) && candidate.sha256 == file.sha256)))
                throw new InvalidOperationException("最终 AOT DLL 未与本次正规裁剪目录逐文件对应，已有目录不代表复制成功。");
            return destination;
        }

        /// <summary>最终后验同时核对 marker、资产/meta 及 Resources 原存在性。</summary>
        private static void ValidateStagingClean(PlayerInputSnapshot inputs)
        {
            if (File.Exists(YooAssetRuntimeSettingsStaging.GetMarkerFilePath()) || File.Exists(PhysicalPath(inputs.stagingTarget)) ||
                File.Exists(PhysicalPath(inputs.stagingTarget) + ".meta") ||
                Directory.Exists(PhysicalPath(inputs.resourcesDirectory)) != inputs.resourcesDirectoryExisted ||
                File.Exists(PhysicalPath(inputs.resourcesDirectory) + ".meta") != inputs.resourcesMetaExisted)
                throw new InvalidOperationException("YooAsset staging 未恢复原存在性；保留真实残留，不据 BuildReport 成功宣称清场。");
        }

        /// <summary>按当前引擎会实例化的 Player 接口与精确属性签名发现，不包含平台切换和配置 provider 噪声。</summary>
        /// <param name="target">本次拟构建目标；各分支仍独立核对真实活动域。</param>
        /// <param name="dependencyPaths">已冻结完整 Player 递归依赖逻辑路径；用于空内容合同。</param>
        /// <param name="token">回调源码、程序集与临时文件基线捕获的取消令牌。</param>
        /// <param name="generating">仅内部 WebGL Generate 用途为 true，进程环境另由 StripAOT 类型化前后态绑定；默认 false。</param>
        /// <returns>实际回调身份、输入、写集、临时基线、真实状态与具体 unsupported 的完整快照。</returns>
        private static CallbackInputSnapshot CapturePlayerCallbacks(BuildTarget target, string[] dependencyPaths, CancellationToken token, bool generating = false)
            => CapturePlayerCallbacksCore(target, dependencyPaths, token, generating, null);

        /// <summary>只在本次同步采集内传递原 TMP 与真正 Scope after，不保留未清理字体对象或使用全局隐式状态。</summary>
        private sealed class TmpObservationContext
        {
            internal readonly PlayerTmpFontBuildContract.Snapshot before;
            internal readonly PlayerTmpFontBuildContract.Snapshot scopeAfter;

            /// <summary>保存原材料引用，两个阶段语义固定，不把较晚 current 当作 Scope after。</summary>
            /// <param name="before">原 Prepare 实际完整材料。</param>
            /// <param name="scopeAfter">本轮真正 Scope after。</param>
            internal TmpObservationContext(PlayerTmpFontBuildContract.Snapshot before, PlayerTmpFontBuildContract.Snapshot scopeAfter)
            { this.before = before; this.scopeAfter = scopeAfter; }
        }

        /// <summary>重新捕获所有回调/源码/程序集/输入，仅 TMP 采用原许可集的同域现存观察，不再次 Load。</summary>
        /// <param name="target">本次实际目标。</param>
        /// <param name="dependencyPaths">当前重新核对的依赖路径。</param>
        /// <param name="token">本阶段取消令牌。</param>
        /// <param name="generating">是否 Generate 内部准确分支。</param>
        /// <param name="before">原 Prepare 实际材料。</param>
        /// <param name="scopeAfter">真正 Scope after，不能用当前重捕代替。</param>
        /// <returns>完整当前回调材料；TMP 历史绑定与实际库存有独立字段和必要校验。</returns>
        private static CallbackInputSnapshot CapturePlayerCallbacksObserved(BuildTarget target, string[] dependencyPaths, CancellationToken token,
            bool generating, PlayerTmpFontBuildContract.Snapshot before, PlayerTmpFontBuildContract.Snapshot scopeAfter)
            => CapturePlayerCallbacksCore(target, dependencyPaths, token, generating, new TmpObservationContext(before, scopeAfter));

        /// <summary>统一完整回调枚举和源身份捕获，显式上下文只控制 TMP 的准备或同域观察模式。</summary>
        /// <param name="target">当前正式目标。</param>
        /// <param name="dependencyPaths">当前完整依赖。</param>
        /// <param name="token">读取与枚举取消令牌。</param>
        /// <param name="generating">是否使用准确 Generate 输入投影。</param>
        /// <param name="observation">null 保留原 Prepare，非 null 仅使用原 TMP marker 库存。</param>
        /// <returns>所有当前回调、源码、写集及真实类型化状态。</returns>
        private static CallbackInputSnapshot CapturePlayerCallbacksCore(BuildTarget target, string[] dependencyPaths, CancellationToken token,
            bool generating, TmpObservationContext observation)
        {
            var identities = new List<string>();
            var inputs = new List<string>();
            var writes = new HashSet<string>(StringComparer.Ordinal);
            var temporary = new HashSet<string>(StringComparer.Ordinal);
            var unsupported = new List<string>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IOrderedCallback>().Where(IsPlayerCallbackType))
            {
                if (type.FullName == "UnityEditor.Build.BuildPipelineInterfaces+AttributeCallbackWrapper") continue;
                AddPlayerCallbackCore(type, type.FullName, target, identities, inputs, writes, temporary, unsupported, token, generating, observation);
            }
            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<UnityEditor.Callbacks.PostProcessSceneAttribute>())
                if (BuildActionCommon.HasBuildAttributeSignature(method, Type.EmptyTypes))
                    AddPlayerCallbackCore(method.DeclaringType, method.DeclaringType.FullName + "." + method.Name + " [PostProcessScene]",
                        target, identities, inputs, writes, temporary, unsupported, token, generating, observation);
            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<UnityEditor.Callbacks.PostProcessBuildAttribute>())
                if (BuildActionCommon.HasBuildAttributeSignature(method, new[] { typeof(BuildTarget), typeof(string) }))
                    AddPlayerCallbackCore(method.DeclaringType, method.DeclaringType.FullName + "." + method.Name + " [PostProcessBuild]",
                        target, identities, inputs, writes, temporary, unsupported, token, generating, observation);
            foreach (Type type in TypeCache.GetTypesDerivedFrom<NovaSDKBuildProcessor>().Where(type => !type.IsAbstract && !type.ContainsGenericParameters))
                AddPlayerCallbackCore(type, type.FullName + " [NovaSDKBuildProcessor]", target, identities, inputs, writes, temporary, unsupported, token, generating, observation);
            foreach (string path in temporary) writes.Add(path);
            var snapshot = new CallbackInputSnapshot
            {
                identities = identities.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                inputs = inputs.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                writes = writes.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                temporaryFiles = temporary.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                temporaryBaseline = CapturePaths(temporary, token),
                temporaryDirectoryIdentities = new[] { "Assets/Resources", "Library/InputSystem", "Library/com.unity.services.core", "Temp/BurstOutput", "Temp/ManagedSymbols" }
                    .Select(path => path + "|" + Directory.Exists(PhysicalPath(path)) + "|" + File.Exists(PhysicalPath(path) + ".meta")).ToArray(),
                unsupported = unsupported.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            };
            if (inputs.Contains("QualityBuiltinState=assets:0|types:0", StringComparer.Ordinal))
                snapshot.qualityBuiltin = CaptureQualityBuiltinState(inputs, token);
            snapshot.inputs = inputs.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            snapshot.normalResetStates = CaptureNormalResetStates(snapshot.inputs, token);
            snapshot.tmpFonts = observation == null ? PlayerTmpFontBuildContract.Capture(snapshot.inputs, token) :
                PlayerTmpFontBuildContract.CaptureObserved(snapshot.inputs, observation.before, observation.scopeAfter, true, token);
            snapshot.facebook = PlayerFacebookBuildContract.Capture(snapshot.inputs, token);
            snapshot.builtinUsage = PlayerBuiltinUsageBuildContract.Capture(snapshot.inputs);
            snapshot.buildEvents = PlayerBuildEventsContract.Capture(snapshot.inputs);
            snapshot.emptyUsage = PlayerEmptyUsageBuildContract.Capture(snapshot.inputs);
            snapshot.packageCallbacks = PlayerPackageCallbacksContract.Capture(snapshot.inputs, dependencyPaths);
            snapshot.packageDependencyPaths = dependencyPaths;
            snapshot.finiteAdditional = PlayerFiniteAdditionalBuildContract.Capture(snapshot.inputs);
            if (generating)
            {
                if (snapshot.packageCallbacks.branches.Contains("PlayerPackage=ProBuilder", StringComparer.Ordinal))
                {
                    string repositoryState = CaptureGenerateRepositoryState(inputs, token);
                    snapshot.inputs = snapshot.inputs.Concat(inputs).Concat(new[] { "GenerateProBuilderRepositoryState=" + repositoryState }).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                }
                snapshot.inputs = ProjectGenerateAssemblyRoster(snapshot.inputs, snapshot.identities);
            }
            snapshot.sha256 = ComputeCallbackContractHash(snapshot);
            return snapshot;
        }

        /// <summary>当前 Player 入口只发现会执行的接口；平台切换和配置 provider 不成为直接构建回调。</summary>
        internal static bool IsPlayerCallbackType(Type type)
        {
            if (type == null || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters) return false;
            if (typeof(BuildPlayerProcessor).IsAssignableFrom(type)) return true;
            var kinds = new HashSet<string>(StringComparer.Ordinal)
            {
                "UnityEditor.Build.IPreprocessBuild", "UnityEditor.Build.IPostprocessBuild",
                "UnityEditor.Build.IPreprocessBuildWithReport", "UnityEditor.Build.IPostprocessBuildWithReport",
                "UnityEditor.Build.IPreprocessBuildWithContext", "UnityEditor.Build.IPostprocessBuildWithContext",
                "UnityEditor.Build.IProcessScene", "UnityEditor.Build.IProcessSceneWithReport",
                "UnityEditor.Build.IFilterBuildAssemblies", "UnityEditor.Build.IUnityLinkerProcessor",
                "UnityEditor.Build.IGenerateNativePluginsForAssemblies", "UnityEditor.Build.IPostBuildPlayerScriptDLLs",
                "UnityEditor.Build.IPreprocessShaders", "UnityEditor.Build.IPreprocessComputeShaders",
            };
            return type.GetInterfaces().Any(contract => kinds.Contains(contract.FullName));
        }

        /// <summary>当前 WebGL 分支逐类型审核；未知身份明确阻断，不使用包前缀或目标泛白名单。</summary>
        /// <param name="type">当前真实发现的已加载回调声明类型。</param>
        /// <param name="label">准确类型或属性方法标签，用于区分同一类型的不同执行分支。</param>
        /// <param name="target">本次拟构建目标，不能替代真实 activeBuildTarget。</param>
        /// <param name="identities">追加实际标签、程序集全名与模块 MVID 的局部集合。</param>
        /// <param name="inputs">追加经核对的源码、DLL、配置和自然状态合同材料。</param>
        /// <param name="writes">追加该精确分支明确声明的文件或正常内存效果。</param>
        /// <param name="temporary">追加本轮需保存并准确恢复的临时文件路径。</param>
        /// <param name="unsupported">保留未知身份、实现或自然状态不符的具体阻碍。</param>
        /// <param name="token">捕获取消令牌；取消继续传播，不转写为普通 unsupported。</param>
        /// <param name="generating">内部 Generate 用途；只改变环境合同材料，默认保留普通 Player 行为。</param>
        /// <returns>无返回值；具体未知分支加入 unsupported，取消继续传播。</returns>
        private static void AddPlayerCallback(Type type, string label, BuildTarget target, ICollection<string> identities,
            ICollection<string> inputs, ISet<string> writes, ISet<string> temporary, ICollection<string> unsupported, CancellationToken token, bool generating = false)
            => AddPlayerCallbackCore(type, label, target, identities, inputs, writes, temporary, unsupported, token, generating, null);

        /// <summary>保留所有精确回调身份和自然分支门，仅 TMP 可通过显式上下文选择无 Load 后态观察。</summary>
        /// <param name="type">实际当前声明类型。</param>
        /// <param name="label">准确类型或属性标签。</param>
        /// <param name="target">当前实际目标。</param>
        /// <param name="identities">实际程序集/MVID 行。</param>
        /// <param name="inputs">当前源码与配置输入。</param>
        /// <param name="writes">当前精确声明写域。</param>
        /// <param name="temporary">本轮临时路径。</param>
        /// <param name="unsupported">未知身份或状态具体原因。</param>
        /// <param name="token">取消令牌。</param>
        /// <param name="generating">准确 Generate 分支。</param>
        /// <param name="observation">null 保留原准备，非 null 只改变 TMP 观察方式。</param>
        private static void AddPlayerCallbackCore(Type type, string label, BuildTarget target, ICollection<string> identities,
            ICollection<string> inputs, ISet<string> writes, ISet<string> temporary, ICollection<string> unsupported, CancellationToken token,
            bool generating, TmpObservationContext observation)
        {
            token.ThrowIfCancellationRequested();
            identities.Add(label + "|" + type.Assembly.FullName + "|" + type.Module.ModuleVersionId);
            try
            {
                if (target != BuildTarget.WebGL)
                { unsupported.Add(label + "：当前真实回调合同优先 WebGL，桌面回调尚未逐分支冻结。" ); return; }
                // 同一类型可同时声明空分支和有副作用的属性回调，必须按精确方法标签区分。
                if (type.FullName == "Facebook.Unity.Editor.XCodePostProcess" &&
                    label == "Facebook.Unity.Editor.XCodePostProcess.PostProcessBuild_iOS [PostProcessBuild]")
                {
                    RequireAssembly(type, "5645b83e43ed8179e72d9f0cacc267280df27e6cd2da00ec71ad17f6a18a80e1", inputs, token);
                    inputs.Add(label + "=target argument is WebGL; verified immediate non-iOS return; no type/module initializer");
                    return;
                }
                if (type.FullName == "Firebase.Crashlytics.Editor.iOSPostBuild" &&
                    label == "Firebase.Crashlytics.Editor.iOSPostBuild.OnPostprocessBuild [PostProcessBuild]")
                {
                    RequireAssembly(type, "5a23d6ae59c11fd1a2ddfee94087dec26eec054d3618fad4ea9e7c1dfeae1e18", inputs, token);
                    inputs.Add(label + "=target argument is WebGL; verified immediate non-iOS/non-tvOS return; initializer only creates constant path list");
                    return;
                }
                if (type.FullName == "Firebase.Editor.XcodeProjectModifier" &&
                    label == "Firebase.Editor.XcodeProjectModifier.PostProcessBuild [PostProcessBuild]")
                {
                    CaptureFirebaseXcodeLogger(type, inputs, writes, token);
                    return;
                }
                if (type.FullName == "Firebase.Crashlytics.Editor.AndroidPreBuild" &&
                    label == "Firebase.Crashlytics.Editor.AndroidPreBuild.BuildComplete [PostProcessBuild]" ||
                    type.FullName == "GooglePlayServices.PlayServicesPreBuild" &&
                    label == "GooglePlayServices.PlayServicesPreBuild.BuildComplete [PostProcessBuild]" ||
                    type.FullName == "GooglePlayServices.PlayServicesResolver" &&
                    label == "GooglePlayServices.PlayServicesResolver.OnPostProcessBuild [PostProcessBuild]")
                {
                    RequireAssembly(type, type.FullName == "Firebase.Crashlytics.Editor.AndroidPreBuild" ?
                        "5a23d6ae59c11fd1a2ddfee94087dec26eec054d3618fad4ea9e7c1dfeae1e18" :
                        "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0", inputs, token);
                    inputs.Add("NormalPostbuildReset=" + label);
                    writes.Add(type.FullName + " normal postbuild scalar reset to false/0 (official callback only; not restored)");
                    return;
                }
                if (type.FullName == "Facebook.Unity.Editor.XCodePostProcess" &&
                    label == "Facebook.Unity.Editor.XCodePostProcess.OnPostProcessBuild [PostProcessBuild]")
                {
                    RequireAssembly(type, "5645b83e43ed8179e72d9f0cacc267280df27e6cd2da00ec71ad17f6a18a80e1", inputs, token);
                    Type settings = FindType("Facebook.Unity.Settings.FacebookSettings") ?? throw new InvalidOperationException("Facebook Settings 类型不存在。");
                    RequireAssembly(settings, "dea1b3857a8560e05ef90bab8ef8342a82dbf24a754dcc3b371913a917a75a96", inputs, token);
                    PlayerFacebookBuildContract.Prepare(inputs, writes, token);
                    return;
                }
                // 两个场景属性回调在真实非 Android 活动域自然返回；同类型的构建后 reset 不在此合同内。
                if (type.FullName == "Firebase.Crashlytics.Editor.AndroidPreBuild" &&
                    label == "Firebase.Crashlytics.Editor.AndroidPreBuild.UpdateResourceFiles [PostProcessScene]" ||
                    type.FullName == "GooglePlayServices.PlayServicesPreBuild" &&
                    label == "GooglePlayServices.PlayServicesPreBuild.WarnIfAutoResolveDisabled [PostProcessScene]")
                {
                    if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                        throw new InvalidOperationException("场景回调的真实活动目标必须是 WebGL；拟构建目标不能代替活动域。");
                    RequireAssembly(type, type.FullName == "Firebase.Crashlytics.Editor.AndroidPreBuild" ?
                        "5a23d6ae59c11fd1a2ddfee94087dec26eec054d3618fad4ea9e7c1dfeae1e18" :
                        "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0", inputs, token);
                    if (type.TypeInitializer != null)
                        throw new InvalidOperationException("已审场景早退类型出现新的静态初始化器。");
                    inputs.Add(label + "=verified active WebGL return before state writes/settings/resources/resolve; existing flag only read");
                    return;
                }
                switch (type.FullName)
                {
                    case "NovaFramework.Editor.PrivacyAesMaintenanceGuard":
                    case "NovaFramework.Editor.ResourceKeyBuildPreprocessor":
                    case "NovaFramework.Editor.NovaBuildPreprocessor":
                    case "NovaFramework.Editor.NovaBuildPostprocessor":
                    case "NovaFramework.Editor.YooAssetRuntimeSettingsBuildPreprocessor":
                    case "NovaFramework.Editor.YooAssetRuntimeSettingsBuildPostprocessor":
                        if (type.Assembly != typeof(PlayerBuildAction).Assembly) break;
                        inputs.Add(label + "=Nova 正式源码与当前 Master/Runtime 已冻结");
                        return;
                    case "NovaFramework.Editor.NativeBuildProcessor":
                        if (type != typeof(NativeBuildProcessor)) break;
                        CaptureSdkNoWebglHooks(type, inputs, token);
                        return;
                    case "NovaFramework.SDK.AppleSignIn.Editor.AppleSignInPluginBuildProcessor":
                    case "NovaFramework.SDK.AppsFlyerPlugin.Editor.AppsFlyerPluginBuildProcessor":
                    case "NovaFramework.SDK.Facebook.Editor.FacebookPluginBuildProcessor":
                    case "NovaFramework.SDK.FirebasePlugin.Editor.FirebasePluginBuildProcessor":
                    case "NovaFramework.SDK.GoogleSignIn.Editor.GoogleSignInPluginBuildProcessor":
                    case "NovaFramework.SDK.MaxAdPlugin.Editor.MaxAdPluginBuildProcessor":
                        CaptureSdkNoWebglHooks(type, inputs, token);
                        return;
                    case "TMPro.TMP_PreBuildProcessor":
                        RequirePackageSource(type, "com.unity.ugui", "Editor/TMP/TMP_PreBuildProcessor.cs", "462778e355246ff3c361fb3e6f057d4306dbbec8cba19becfbf2009b827232ac", inputs, token);
                        CaptureTmpFontNormalBranchCore(type, inputs, writes, token, observation);
                        return;
                    case "UnityEditor.TextCore.Text.TextCorePreBuildProcessor":
                        RequireAssembly(type, "5ccf7a5a43c7b0dc5e1280f9b38142115c8d2ced8ee5518806fffbcb0eb3ef70", inputs, token);
                        CaptureFontEarlyReturn("t:FontAsset", "UnityEngine.TextCore.Text.FontAsset", true, inputs);
                        return;
                    case "UnityEngine.InputSystem.Editor.InputSettingsBuildProvider":
                        RequirePackageSource(type, "com.unity.inputsystem", "InputSystem/Editor/Settings/InputSettingsBuildProvider.cs", "1dc2c8f34d80a01e44fbc5e2afdf7991ddf139ebd25cc732d08cd0c16dbb6268", inputs, token);
                        CaptureInputSettings(type, inputs, token);
                        inputs.Add("PreloadedAssets=" + string.Join("|", PlayerSettings.GetPreloadedAssets().Select(ObjectIdentity)));
                        writes.Add("PlayerSettings.preloadedAssets (temporary, exact ordered restore)");
                        temporary.Add("ProjectSettings/ProjectSettings.asset");
                        return;
                    case "UnityEngine.InputSystem.Editor.ProjectWideActionsBuildProvider":
                        RequirePackageSource(type, "com.unity.inputsystem", "InputSystem/Editor/ProjectWideActions/ProjectWideActionsBuildProvider.cs", "3cebfc6059805c45876329c7b043efcbe254f338d8402d198bc06aa515caeaee", inputs, token);
                        RequirePackageSource(type, "com.unity.inputsystem", "InputSystem/Editor/Internal/BuildProviderHelpers.cs", "62ac3ed7f034ca8edde3c17a88f9aed50a36239a1d4bf1c8235657575d141c7b", inputs, token);
                        EditorBuildSettings.TryGetConfigObject("com.unity.input.settings.actions", out UnityEngine.Object actions);
                        inputs.Add("ProjectWideActions=" + ObjectIdentity(actions));
                        if (actions != null)
                        {
                            FieldInfo flag = actions.GetType().GetField("m_IsProjectWide", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (actions.GetType().FullName != "UnityEngine.InputSystem.InputActionAsset" || flag?.FieldType != typeof(bool))
                                throw new InvalidOperationException("ProjectWideActions 实际类型或标志字段与已审合同不符。");
                            inputs.Add("ProjectWideActionsObject=" + HashObject(actions) + "|dirty=" + EditorUtility.IsDirty(actions) + "|flag=" + flag.GetValue(actions));
                            CaptureObjectSource(actions, inputs, token);
                        }
                        writes.Add("PlayerSettings.preloadedAssets (temporary, exact ordered restore)");
                        temporary.Add("ProjectSettings/ProjectSettings.asset");
                        if (actions != null) writes.Add(AssetDatabase.GetAssetPath(actions) + " (temporary m_IsProjectWide restore)");
                        return;
                    case "UnityEngine.InputSystem.Editor.LinkFileGenerator":
                        RequirePackageSource(type, "com.unity.inputsystem", "InputSystem/Editor/BuildPipeline/LinkFileGenerator.cs", "08749ccd315900199503e90e0c922c94a8fa922d1b6c08d05d4bd63a8646a562", inputs, token);
                        temporary.Add("Library/InputSystem/" + target + "Link.xml");
                        CaptureAssemblyRoster(inputs);
                        return;
                    case "Unity.Services.Core.Configuration.Editor.ProjectConfigurationBuildInjectorWithPlayerProcessor":
                        RequirePackageSource(type, "com.unity.services.core", "Editor/Core/Configuration/ProjectConfigurationBuildInjectorWithPlayerProcessor.cs", "5c68f7de0768fe1ce239f984be1c055103eaa24456e38c57e077d46b8b10a66c", inputs, token);
                        foreach (var source in new[]
                        {
                            new[] { "Editor/Core/Configuration/ProjectConfigurationBuilder.cs", "b4a8db577712cae375803d4f9f9ce04944e788bbb8619f3279fb3e8216d0680a" },
                            new[] { "Editor/Core/Configuration/IConfigurationProvider.cs", "eb26dc67301dac3983bb51db2d8d8f9fd27424624e0d644d748c261cbda55260" },
                            new[] { "Editor/Core/Configuration/ConfigurationBuilder.cs", "27664728058853e9fad1b1165b2e91f0e0d9b0644312647db384c059e77c79b5" },
                            new[] { "Editor/Core/Configuration/Utils/AssetUtils.cs", "22fdd895b5126ee1272aa2e12dd870444d66d4983d73f4a88ecd77f643f4feb3" },
                            new[] { "Editor/Core/Environments/Save/EnvironmentSaveSystem.cs", "7e3c15fa76ff5542c9aba7c45ec78e5c70698b980038c42c2304a570d37c2922" },
                            new[] { "Editor/Core/Environments/Settings/EnvironmentSettings.cs", "d537141db7126c7e7f80957c2ca5a9c8fd4428e63a2cf29e1cf786847caa4297" },
                            new[] { "Editor/Core/Utils/FileSystem/FileSystem.cs", "08463c0f10c02dcf641a3830ebf1de90fe4a77aee4ba78bf177570a8b5600aa3" },
                        }) RequirePackageSource(type, "com.unity.services.core", source[0], source[1], inputs, token);
                        CaptureServicesProviders(type, inputs, token);
                        temporary.Add("Library/com.unity.services.core/UnityServicesProjectConfiguration.json");
                        return;
                    case "Unity.Services.Core.Editor.LinkFileGenerator":
                        RequirePackageSource(type, "com.unity.services.core", "Editor/Core/Linker/LinkFileGenerator.cs", "b2cd1d7af82ffc51a7068bd2f292972b27a14cd72caf8eebdd17e01e0032ee63", inputs, token);
                        temporary.Add("Library/com.unity.services.core/link.xml");
                        CaptureAssemblyRoster(inputs);
                        return;
                    case "Unity.Services.Core.Editor.ProjectUnlinkBuildWarning":
                        RequirePackageSource(type, "com.unity.services.core", "Editor/Core/Build/ProjectUnlinkBuildWarning.cs", "254ea67aac9d035520abc6a041501100f17c09caed4ea16348a2af8ef34f4238", inputs, token);
                        inputs.Add("CloudProject=" + GenerateActionCommon.ComputeTextHash(CloudProjectSettings.projectId ?? string.Empty));
                        return;
                    case "Unity.PerformanceTesting.Editor.TestRunBuilder":
                        RequirePackageSource(type, "com.unity.test-framework.performance", "Editor/TestRunBuilder.cs", "84900e8a90752ffa40f4ddccca513e0bf41bc1a66fadc769c8054f137fe6b81f", inputs, token);
                        if (EditorPrefs.GetBool("PT_ResourcesCleanup", false) && Directory.Exists(PhysicalPath("Assets/Resources")))
                            throw new InvalidOperationException("Performance Test 旧清理标志将删除已有 Assets/Resources；需先处理真实旧状态再重新计划。");
                        inputs.Add("PT_ResourcesCleanup=" + EditorPrefs.GetBool("PT_ResourcesCleanup", false));
                        temporary.Add("Assets/Resources/PerformanceTestRunInfo.json");
                        temporary.Add("Assets/Resources/PerformanceTestRunInfo.json.meta");
                        temporary.Add("Assets/Resources/PerformanceTestRunSettings.json");
                        temporary.Add("Assets/Resources/PerformanceTestRunSettings.json.meta");
                        writes.Add("Assets/Resources + .meta (only current callback temporary directory)");
                        writes.Add("EditorPrefs.PT_ResourcesCleanup (temporary, restored)");
                        return;
                    case "Unity.PerformanceTesting.Editor.PerformanceTestBuildAssemblyFilter":
                        RequirePackageSource(type, "com.unity.test-framework.performance", "Editor/PerformanceTestBuildAssemblyFilter.cs", "ff525262898e19b549ead3da86c408a93ba282745bbf1d3af549bb3c8a2d1347", inputs, token);
                        return;
                    case "UnityEditor.TestRunner.TestBuildAssemblyFilter":
                        RequirePackageSource(type, "com.unity.test-framework", "UnityEditor.TestRunner/TestBuildAssemblyFilter.cs", "861f477fd4813bc932997b6b0c027bd9c0b4f0d61149150a112df24a947d9ba7", inputs, token);
                        inputs.Add("TestBuildAssemblyFilter=IncludeTestAssemblies/options + frozen native PlayerSettings");
                        return;
                    case "HybridCLR.Editor.BuildProcessors.CheckSettings":
                    case "HybridCLR.Editor.BuildProcessors.CopyStrippedAOTAssemblies":
                    case "HybridCLR.Editor.BuildProcessors.FilterHotFixAssemblies":
                    case "HybridCLR.Editor.BuildProcessors.MsvcStdextWorkaround":
                    case "HybridCLR.Editor.BuildProcessors.PatchScriptingAssemblyList":
                        CaptureHybridCallbackSource(type, inputs, token);
                        if (!File.Exists(PhysicalPath("ProjectSettings/HybridCLRSettings.asset")))
                            throw new InvalidOperationException("HybridCLR Settings 尚未保存，不能首次初始化或猜默认配置。");
                        inputs.Add("HybridCLRSettings=" + HashObject(SettingsUtil.HybridCLRSettings));
                        if (generating)
                        {
                            inputs.Add("HybridCLRGenerateEnvironment=official typed before/after; no blind restore");
                            writes.Add("process UNITY_IL2CPP_PATH + _CL_ (official persistent transition, exact StripAOT receipt)");
                        }
                        else
                        {
                            inputs.Add("UNITY_IL2CPP_PATH=" + GenerateActionCommon.ComputeTextHash(Environment.GetEnvironmentVariable("UNITY_IL2CPP_PATH") ?? string.Empty));
                            inputs.Add("_CL_=" + GenerateActionCommon.ComputeTextHash(Environment.GetEnvironmentVariable("_CL_") ?? string.Empty));
                            writes.Add("process UNITY_IL2CPP_PATH + _CL_ (temporary, restored)");
                        }
                        if (type.Name == "CopyStrippedAOTAssemblies") writes.Add(Path.GetFullPath(SettingsUtil.GetAssembliesPostIl2CppStripDir(target)) + " (final stripped AOT output)");
                        return;
                    case "Unity.Burst.Editor.BurstAOTCompilerPostprocessor":
                        RequirePackageSource(type, "com.unity.burst", "Editor/BurstAotCompiler.cs", "32c8a15bde1806c6cdc5f7536bc798398cd1ec25a2a0df5594e3ea7f1d3ba0f2", inputs, token);
                        RequirePackageSource(type, "com.unity.burst", "Editor/BurstPlatformAotSettings.cs", "a02e3a9fe059f8f9e4543469d0ba0c5a7e308aee0f0a0816fcbe66d505e9c037", inputs, token);
                        foreach (string source in new[] { "ProjectSettings/BurstAotSettings_WebGL.json", "ProjectSettings/CommonBurstAotSettings.json" })
                        {
                            string full = PhysicalPath(source);
                            if (File.Exists(full))
                            {
                                var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(full));
                                if (json["Version"]?.Type != Newtonsoft.Json.Linq.JTokenType.Integer || (int)json["Version"] < 4 || (int)json["Version"] > 5)
                                    throw new InvalidOperationException("Burst 配置版本缺失、形状无效、旧版会保存或版本未审；需先处理该源状态再计划。");
                                inputs.Add(source + "|" + GenerateActionCommon.ComputeFileHash(full, token));
                            }
                        }
                        if (Directory.Exists(PhysicalPath("Temp/BurstOutput")))
                            throw new InvalidOperationException("Burst 将删除已有 Temp/BurstOutput；本轮仅接受新临时输出目录。");
                        temporary.Add("Library/BurstCache/AotSettings_WebGL.hash");
                        writes.Add("Temp/BurstOutput (current generated native plugins, removed after packaging)");
                        inputs.Add("BurstPdbSearch=" + Util.Json.Serialize(CapturePaths(new[] { "Temp/ManagedSymbols" }, token)));
                        return;
                    case "Unity.VisualScripting.AotPreBuilder":
                    case "LinkerCreator":
                    case "Unity.VisualScripting.Analytics.OnPreprocessBuildAnalyticsEventHandler":
                        CaptureUnusedVisualScripting(type, inputs, token);
                        return;
                    case "ReloadAssets":
                        RequirePackageSource(type, "com.unity.visualscripting", "Editor/VisualScripting.Core/Utilities/ReloadAssets.cs", "9ca2f925af0c58e6d0de8a96597d0c3550235dd9898b14257ac963844b12c406", inputs, token);
                        RequireEmptyDelegate(type, "OnBuildCompleted", inputs);
                        return;
                    case "Firebase.Editor.AnalyticsFixPropertyRemover":
                    case "Firebase.Editor.DllLocationPatcher":
                        RequireAssembly(type, "b50eefa93d23c673bc8a97266e3d36dcfba0e9d3e49f5e7cc7850d595c12f1bf", inputs, token);
                        inputs.Add(label + "=verified WebGL early return");
                        return;
                    case "Google.IOSResolver":
                        RequireAssembly(type, "ca00858a5f4a657e949480015124f9c783ea80c42bb6000a9c5ec0112ffa5ca5", inputs, token);
                        inputs.Add(label + "=CocoapodsIntegrationEnabled/InjectDependencies return false before iOS/tvOS preferences or dependency discovery");
                        return;
                    case "Firebase.Editor.XcodeProjectPatcher":
                        RequireAssembly(type, "b50eefa93d23c673bc8a97266e3d36dcfba0e9d3e49f5e7cc7850d595c12f1bf", inputs, token);
                        inputs.Add(label + "=Enabled returns false before Google.IOSResolver on current WebGL target");
                        return;
                    case "Firebase.Editor.FirebaseLinkXmlProcessor":
                        RequireAssembly(type, "b50eefa93d23c673bc8a97266e3d36dcfba0e9d3e49f5e7cc7850d595c12f1bf", inputs, token);
                        temporary.Add("Temp/FirebaseAdditionalLink.xml");
                        CaptureAssemblyRoster(inputs);
                        return;
                    case "Firebase.Messaging.Editor.FirebaseMessagingActivityGenerator":
                        CapturePackageIdentity(type, inputs, token);
                        inputs.Add(label + "=verified non-Android branch");
                        return;
                    case "Firebase.Messaging.Editor.FirebaseMessagingAndroidManifestModifier":
                        CapturePackageIdentity(type, inputs, token);
                        temporary.Add("Assets/Plugins/Android/AndroidManifest.xml");
                        temporary.Add("Assets/Plugins/Android/AndroidManifest.xml.meta");
                        return;
                    case "TMPro.TMP_PostBuildProcessHandler":
                    case "UnityEngine.InputSystem.iOSPostProcessBuild":
                    case "NovaFramework.Editor.IOSPodsDeploymentTargetPostprocessor":
                    case "NovaFramework.SDK.AIHelp.Editor.AIHelpIOSBuildProcessor":
                    case "Facebook.Unity.PostProcess.DisableBitcode":
                    case "UniWebViewPostBuildProcessorIOS":
                    case "UnityEditor.Purchasing.BuildPreprocessor":
                        CapturePackageIdentity(type, inputs, token);
                        inputs.Add(label + "=verified non-iOS/non-Android body");
                        return;
                    case "UnityEditor.UnityBuildPostprocessor":
                        RequireAssembly(type, "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
                        inputs.Add("StaticBatching=" + PlayerSettings.GetStaticBatchingForPlatform(target));
                        return;
                    case "UnityEditor.Build.Rendering.EnsureSinglePipelineOnBuild":
                        RequireAssembly(type, "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
                        CaptureQualityBuiltinState(inputs, token);
                        writes.Add("QualitySettings.s_RenderPipelineAssets + s_RenderPipelineAssetsTypes (official temporary scratch; restore only exact original empty containers and frozen null roster)");
                        return;
                    case "NovaFramework.Editor.HybridClrStripGradleScope":
                        const string stripGuardPath = "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Generate/HybridClrStripGradleScope.cs";
                        const string stripGuardHash = "ad53122261b2dc7bcfb14d41640e1754932ed82f2a7d6d2d42aadf3d1c3f048a";
                        if (label != type.FullName || GenerateActionCommon.ComputeFileHash(PhysicalPath(stripGuardPath), token) != stripGuardHash || !IsInactiveStripGradleScope(type))
                            throw new InvalidOperationException("自有StripAOT guard源码/类型/当前租约不符合准确自然inactive合同；不更改租约。");
                        inputs.Add("OwnCallbackSource=" + stripGuardPath + "|" + stripGuardHash);
                        inputs.Add("HybridClrStripGradleScopeLease=inactive; all three official callbacks return before settings/report/path reads");
                        return;
                    case "UnityEditor.BuildEventsHandlerPostProcess":
                        RequireAssembly(type, "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
                        Type webglModule = FindType("UnityEditor.WebGL.TargetExtension") ?? throw new InvalidOperationException("WebGL 当前模块程序集未自然加载。");
                        RequireAssembly(webglModule, "031d9fe2ab801dfe01750f271eea8f16ac7e7b86803977f15b43323e6ad6d96e", inputs, token);
                        inputs.Add(PlayerBuildEventsContract.Marker);
                        writes.Add("BuildEventsHandlerPostProcess: official SceneView s_EventSent=true/counts=current frozen roster normal memory update; no manual reset or restore; WebGL base Properties creates only empty managed object, as IBuildPlatformProperties naturally null; derived provider roster must remain empty");
                        writes.Add("Unity native usage: exact sceneViewInfo and buildPackageList from frozen existing views/default2d and actual BuildReport ManagedLibrary/dll package mapping, configured native queue/limit/diagnostic effects; no ack, delivery or queue-tail claim; no arbitrary Library write permission");
                        return;
                    case "UnityEditor.Audio.Analytics.AudioRandomContainerBuildAnalyticsEvent":
                    case "UnityEditor.Audio.Analytics.UIBuildAnalyticsEvent":
                    case "UnityEditor.InsightsEditor.EditorAnalytics.InsightsEditorAnalytic":
                        RequireAssembly(type, "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
                        RequireAssembly(typeof(AudioSettings), "1b94c002a33d7fcd96a58f693794abf38e61895cd25c444f2b1018e72f4a322d", inputs, token);
                        RequireAssembly(typeof(UnityEngine.UIElements.VisualTreeAsset), "a6e2aa385f64e530211e441ea7e877a953d08c6aad41c32bafa0ab37e11b0160", inputs, token);
                        RequireAssembly(typeof(UnityEngine.Analytics.IAnalytic), "22282580412e620fe151d5d7d7d690e929a96be27ebfc65e22ccb0094f148b0d", inputs, token);
                        RequireAssembly(typeof(UnityEngine.Object), "25281ab07e11130a45ede1e02cf3003666f9616cca68ae8ab9637f0ccffa33ad", inputs, token);
                        inputs.Add(type.FullName == "UnityEditor.Audio.Analytics.AudioRandomContainerBuildAnalyticsEvent" ? PlayerBuiltinUsageBuildContract.AudioMarker :
                            type.FullName == "UnityEditor.Audio.Analytics.UIBuildAnalyticsEvent" ? PlayerBuiltinUsageBuildContract.UiMarker : PlayerBuiltinUsageBuildContract.InsightsMarker);
                        writes.Add("Unity native EditorAnalytics: exact built-in AudioRandom/UI/Insights event registration, configured queue/limit handling and fixed registration warning; payload derives only from actual BuildReport and frozen existing configuration; delivery/native queue exit not asserted; analyticSent downstream must remain empty");
                        writes.Add("Insights official pure command-line predicate/create-project cache and read-initialized compiler owner; DebuggerEventListHandler pure-memory default handler/list/lock initialization and native event diagnostic append; never clear/reset/substitute either cache");
                        return;
                    case "UnityEditor.AdaptivePerformance.Basic.Editor.BasicProviderBuildProcess":
                    case "UnityEditor.AdaptivePerformance.Editor.AdaptivePerformanceGeneralBuildProcessor":
                        RequireAssembly(type, "7988058106626eccc3acb284e4e89a27ce3936a80439c8cf9d0a40fd451aa6db", inputs, token);
                        RequireAssembly(FindType("UnityEngine.AdaptivePerformance.AdaptivePerformanceGeneralSettings"), "c87e2495f8670cafcfa16488fd68f77cceeb1427c2842acc8c911fe2c6639f36", inputs, token);
                        RequireAssembly(typeof(EditorUserBuildSettings), "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
                        string adaptiveMarker = type.FullName == "UnityEditor.AdaptivePerformance.Basic.Editor.BasicProviderBuildProcess" ? PlayerFiniteAdditionalBuildContract.Basic : PlayerFiniteAdditionalBuildContract.General;
                        PlayerFiniteAdditionalBuildContract.Capture(new[] { adaptiveMarker });
                        inputs.Add(adaptiveMarker);
                        writes.Add("Adaptive 两项：无活动 profile、无两类配置、原 preloaded 无空槽/设置型；官方 CleanOldSettings 不触发 SetPreloadedAssets；正常 General settings key 及 EditorUtilities 固定路径数组可纯内存初始化，不创建或加载 provider。");
                        return;
                    case "UnityEditor.Build.Rendering.RenderPipelineGlobalSettingsStripper":
                        RequireAssembly(type, "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
                        RequireAssembly(typeof(UnityEngine.Rendering.GraphicsSettings), "25281ab07e11130a45ede1e02cf3003666f9616cca68ae8ab9637f0ccffa33ad", inputs, token);
                        PlayerFiniteAdditionalBuildContract.Capture(new[] { PlayerFiniteAdditionalBuildContract.Stripper });
                        inputs.Add(PlayerFiniteAdditionalBuildContract.Stripper);
                        writes.Add("GlobalSettingsStripper：完整管线 roster 全 null/global null，仅官方两私有 bool reset=false 与准确 UnityEngine CollectionPool<List<RenderPipelineAsset>,RenderPipelineAsset> 一次空列表借还/容量增长；已审纯内存池初始化，无资产 dirty/clear/save；后验保护原池/list/原条目与活跃借用，不手动恢复。");
                        return;
                    case "UnityEditor.Rendering.Analytics.BuildTargetAnalytic":
                        RequireAuditedSrpAssembly(type, inputs, token);
                        RequirePackageSource(type, "com.unity.render-pipelines.core", "Editor/Analytics/BuildTargetAnalytic.cs", "0f95b520e715f1c11c8ff12a9a58dd8a61e9a66e812a723ad1e0121488e65502", inputs, token);
                        RequireAssembly(typeof(EditorAnalytics), "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
                        RequireAssembly(typeof(UnityEngine.Rendering.GraphicsSettings), "25281ab07e11130a45ede1e02cf3003666f9616cca68ae8ab9637f0ccffa33ad", inputs, token);
                        RequireAssembly(typeof(UnityEngine.Analytics.IAnalytic), "22282580412e620fe151d5d7d7d690e929a96be27ebfc65e22ccb0094f148b0d", inputs, token);
                        PlayerFiniteAdditionalBuildContract.Capture(new[] { PlayerFiniteAdditionalBuildContract.BuildTarget });
                        inputs.Add(PlayerFiniteAdditionalBuildContract.BuildTarget);
                        writes.Add("正常 Unity usage uBuildTargetAnalytic/unity.srp：原 Analytics 配置、准确 WebGL/质量级数量/Built-In Render Pipeline payload；原生注册、10次每小时/1000元素限制、队列及诊断效果，不声称送达/队列退出，不重发。");
                        return;
                    case "UnityEngine.InputSystem.Editor.InputBuildAnalytic+ReportProcessor":
                        RequireAssembly(type, "5c6a15b86b5395349d194fa01adaa789d7395c52d446f8e884f870249c497efb", inputs, token);
                        RequirePackageSource(type, "com.unity.inputsystem", "InputSystem/Editor/Analytics/InputBuildAnalytic.cs", "a705581c238ef8410b7a18fb993629856e6ca6963c629b54a2e85c9edb0c6291", inputs, token);
                        RequirePackageSource(type, "com.unity.inputsystem", "InputSystem/NativeInputRuntime.cs", "c7f0e56c880b4c6cb050ea1a7c982f3bbd9a38aacecf35a767e1c643338f0067", inputs, token);
                        RequirePackageSource(type, "com.unity.inputsystem", "InputSystem/InputAnalytics.cs", "e9f596f0f71f9c2b441e7b948eec9619593b96dadd52c2c16e637d5d4738b19d", inputs, token);
                        RequirePackageSource(type, "com.unity.inputsystem", "InputSystem/InputSettings.cs", "7f40a1cc06d0f842e36eaa775643b3a07b021c18ff63d7304c0b052f1646eb2d", inputs, token);
                        RequirePackageSource(type, "com.unity.inputsystem", "InputSystem/Plugins/iOS/InputSettingsiOS.cs", "3d56a78c8073c6fa8136aa75f22c993c8cff9f98024c28f1bf32d0be93ee0bd0", inputs, token);
                        RequireAssembly(typeof(EditorAnalytics), "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
                        RequireAssembly(typeof(UnityEngine.Object), "25281ab07e11130a45ede1e02cf3003666f9616cca68ae8ab9637f0ccffa33ad", inputs, token);
                        RequireAssembly(typeof(UnityEngine.Analytics.IAnalytic), "22282580412e620fe151d5d7d7d690e929a96be27ebfc65e22ccb0094f148b0d", inputs, token);
                        CaptureInputSettings(type, inputs, token);
                        PlayerFiniteAdditionalBuildContract.Capture(new[] { PlayerFiniteAdditionalBuildContract.Input });
                        inputs.Add(PlayerFiniteAdditionalBuildContract.Input);
                        writes.Add("正常 Input usage input_build_completed/unity.input：仅既有准确 NativeInputRuntime，payload 为真实报告 GUID、现存设置/feature flags/是否设置及 actions 资产和默认比较；原 Analytics 配置下注册、100次每小时/100元素限制、排队/诊断。官方 Gather 可创建并 finally DestroyImmediate 一个准确临时 InputSettings（纯字段默认值及 iOS DTO）；原设置/对象集保持，不声明送达/队列退出，不预创建或发送。");
                        return;
                    case "UnityEditor.Rendering.RenderPipelineGraphicsSettingsAnalytics":
                        RequireAuditedSrpAssembly(type, inputs, token);
                        RequireAuditedSrpAssembly(FindType("UnityEngine.Rendering.ListPool`1"), inputs, token);
                        RequirePackageSource(type, "com.unity.render-pipelines.core", "Editor/Analytics/RenderPipelineGraphicsSettingsAnalytics.cs", "39f912bc8faa9005d43f813448fa1293301d23f433f252ba49b0dab5e14ff08f", inputs, token);
                        RequirePackageSource(type, "com.unity.render-pipelines.core", "Runtime/Common/ObjectPools.cs", "6b032c829eab722b33536a4fa5ed759ff4edf71031efc365045c9908a5275f38", inputs, token);
                        RequireAssembly(typeof(EditorAnalytics), "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
                        RequireAssembly(typeof(UnityEngine.Rendering.GraphicsSettings), "25281ab07e11130a45ede1e02cf3003666f9616cca68ae8ab9637f0ccffa33ad", inputs, token);
                        RequireAssembly(typeof(UnityEngine.Analytics.IAnalytic), "22282580412e620fe151d5d7d7d690e929a96be27ebfc65e22ccb0094f148b0d", inputs, token);
                        inputs.Add(PlayerEmptyUsageBuildContract.SrpMarker);
                        writes.Add("SRP usage: current global must remain null; no settings callback, Activator or ToNestedColumn; configured native analytic registration/diagnostics only, empty DataList sends no payload; exact private ListPool<AnalyticsData> may initialize pure owners and normally borrow/release one empty list, never clear preexisting payload or replay Gather; no delivery/queue-exit claim");
                        return;
                    case "UnityEditor.AdaptivePerformance.Editor.Analytics.Hooks.BuildHook":
                        RequireAssembly(type, "7988058106626eccc3acb284e4e89a27ce3936a80439c8cf9d0a40fd451aa6db", inputs, token);
                        RequireAssembly(typeof(EditorAnalytics), "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
                        RequireAssembly(typeof(UnityEngine.Analytics.IAnalytic), "22282580412e620fe151d5d7d7d690e929a96be27ebfc65e22ccb0094f148b0d", inputs, token);
                        RequireAssembly(typeof(UnityEngine.Object), "25281ab07e11130a45ede1e02cf3003666f9616cca68ae8ab9637f0ccffa33ad", inputs, token);
                        inputs.Add(PlayerEmptyUsageBuildContract.AdaptiveMarker);
                        writes.Add("Adaptive usage: exact empty existing metadata Dictionary prevents every provider getter; readonly backing access may naturally run audited metadata cctor (empty containers + one known PlayModeStateChanged subscription + readonly play-state query, no package/search request); never clear table/queues or invoke GetLoaders preflight; official scene callback may initialize pure comparison cache and overwrite its private usage DTO from actual WebGL report (version empty/providers null), configured native registration/queue/diagnostics/fixed failure warning; no scene attempt count, ack or queue-exit claim");
                        return;
                    case "UnityEditor.Rendering.Analytics.VolumeProfileOverridesAnalytic":
                        RequireAuditedSrpAssembly(type, inputs, token);
                        RequirePackageSource(type, "com.unity.render-pipelines.core", "Editor/Analytics/VolumeProfileOverridesAnalytic.cs", "2b5949226edab137cc62c7d7530e717fbdbf93f3282b77c3c9b993df3fd16e89", inputs, token);
                        FieldInfo rootsField = type.GetField("k_SearchFolders", BindingFlags.NonPublic | BindingFlags.Static);
                        if (rootsField?.FieldType != typeof(string[]) || !rootsField.IsInitOnly)
                            throw new InvalidOperationException("VolumeProfile 搜索根字段与已审身份不符。");
                        var roots = (string[])rootsField.GetValue(null);
                        if (roots == null || !roots.SequenceEqual(new[] { "Assets" }, StringComparer.Ordinal))
                            throw new InvalidOperationException("VolumeProfile 实际搜索根已变化；不调用收集或加载资产。");
                        const string volumeFilter = "t:VolumeProfile glob:\"**/*.asset\"";
                        string[] volumeGuids = AssetDatabase.FindAssets(volumeFilter, roots);
                        inputs.Add("VolumeProfileSearch=" + volumeFilter + "|Assets|" + string.Join("|", volumeGuids.OrderBy(value => value, StringComparer.Ordinal)));
                        if (volumeGuids.Length != 0) throw new InvalidOperationException("VolumeProfile 搜索非空；加载、默认组件实例化与外发尚未进入合同。");
                        writes.Add("VolumeProfileOverridesAnalytic.<>c.<>9 + <>9__6_0 (official pure-memory delegate cache initialization; empty GUID roster prevents invocation)");
                        return;
                    case "UnityEditor.Rendering.CorePreprocessBuild":
                    case "UnityEditor.Rendering.ProbeVolumeBuildProcessor":
                        CaptureSrpBuiltinCallback(type, target, inputs, token);
                        if (type.FullName == "UnityEditor.Rendering.CorePreprocessBuild")
                            writes.Add("CorePreprocessBuild.m_BuildData + CoreBuildData.m_Instance (official temporary memory cache; completion requires both null)");
                        return;
                    case "Unity.ProjectAuditor.Editor.Build.LastBuildReportProvider":
                        RequireAssembly(type, "2c20ea0b8555c4851b9885a2115f2499cecdea88ed0ef28ccdd530829113eac9", inputs, token);
                        ValidateAuditorIdentity(type);
                        if (HasAuditorPendingCopy(type)) throw new InvalidOperationException("ProjectAuditor 有旧延迟报告复制；不会覆盖旧任务或以新构建接管。");
                        inputs.Add("ProjectAuditorDelayedCopy=no pending; exact official update callback");
                        writes.Add(PhysicalPath("Library/LastBuild.buildreport"));
                        writes.Add(PhysicalPath("Library/LastCleanBuild.buildreport"));
                        return;
                    default:
                        break;
                }
                if (PlayerPackageCallbacksContract.Prepare(type, label, inputs, writes, token)) return;
                unsupported.Add(label + "：尚无当前源码/编译身份和实际分支写入合同。");
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                unsupported.Add(label + "：" + exception.Message);
            }
        }

        /// <summary>只读无cctor自有guard的真实租约；独立Player仅接纳自然inactive，不调用Begin/Dispose/回调。</summary>
        /// <param name="type">准确当前域候选类型。</param>
        /// <returns>当前准确类型、字段形状和自然null租约全部满足时返回true。</returns>
        internal static bool IsInactiveStripGradleScope(Type type)
        {
            if (type == null || type.Assembly != typeof(PlayerBuildAction).Assembly || type.FullName != "NovaFramework.Editor.HybridClrStripGradleScope" || type.TypeInitializer != null) return false;
            FieldInfo active = type.GetField("s_Active", BindingFlags.NonPublic | BindingFlags.Static);
            return active?.DeclaringType == type && active.FieldType.DeclaringType == type && active.FieldType.FullName == type.FullName + "+Lease" && active.GetValue(null) == null;
        }

        /// <summary>仅读实际活动目标的完整 Quality roster；局部 List 不接触官方共享 scratch 或 SamePipelineAssetsForPlatform。</summary>
        internal static QualityBuiltinBaseline CaptureQualityBuiltinState(ICollection<string> inputs, CancellationToken token)
        {
            RequireAssembly(typeof(QualitySettings), "25281ab07e11130a45ede1e02cf3003666f9616cca68ae8ab9637f0ccffa33ad", inputs, token);
            RequireAssembly(typeof(BuildPipeline), "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            FieldInfo assetsField = typeof(QualitySettings).GetField("s_RenderPipelineAssets", flags);
            FieldInfo typesField = typeof(QualitySettings).GetField("s_RenderPipelineAssetsTypes", flags);
            if (assetsField?.FieldType != typeof(List<UnityEngine.Rendering.RenderPipelineAsset>) || typesField?.FieldType != typeof(HashSet<Type>))
                throw new InvalidOperationException("QualitySettings scratch 字段与已审身份不符。");
            var assets = (List<UnityEngine.Rendering.RenderPipelineAsset>)assetsField.GetValue(null);
            var types = (HashSet<Type>)typesField.GetValue(null);
            if (!TryValidateQualityBuiltinState(0, 0, assets?.Count ?? -1, types?.Count ?? -1, out string error))
                throw new InvalidOperationException(error);
            MethodInfo groupName = typeof(BuildPipeline).GetMethod("GetBuildTargetGroupName", flags, null, new[] { typeof(BuildTarget) }, null);
            MethodInfo getAll = typeof(QualitySettings).GetMethod("GetAllRenderPipelineAssetsForPlatform", flags, null,
                new[] { typeof(string), typeof(List<UnityEngine.Rendering.RenderPipelineAsset>).MakeByRefType() }, null);
            if (groupName?.ReturnType != typeof(string) || getAll?.ReturnType != typeof(void) || !getAll.IsPublic)
                throw new InvalidOperationException("QualitySettings 完整 roster helper 签名已变化。");
            BuildTarget active = EditorUserBuildSettings.activeBuildTarget;
            string group = (string)groupName.Invoke(null, new object[] { active });
            if (string.IsNullOrEmpty(group)) throw new InvalidOperationException("活动目标没有有效 QualitySettings 平台组名。");
            var roster = new List<UnityEngine.Rendering.RenderPipelineAsset>();
            object[] arguments = { group, roster };
            getAll.Invoke(null, arguments);
            if (!ReferenceEquals(roster, arguments[1])) throw new InvalidOperationException("QualitySettings 局部 roster 容器被替换，与已审分支不符。");
            if (!TryValidateQualityBuiltinState(roster.Count, roster.Count(value => !ReferenceEquals(value, null)), assets.Count, types.Count, out error))
                throw new InvalidOperationException(error);
            if (!ReferenceEquals(assets, assetsField.GetValue(null)) || !ReferenceEquals(types, typesField.GetValue(null)))
                throw new InvalidOperationException("QualitySettings 只读捕获期间原 scratch 身份已变化。");
            inputs.Add("QualityBuiltinRoster=" + active + "|" + group + "|all-managed-null:" + roster.Count);
            inputs.Add("QualityBuiltinState=assets:0|types:0");
            return new QualityBuiltinBaseline { assetsField = assetsField, typesField = typesField, assets = assets, types = types, rosterCount = roster.Count };
        }

        /// <summary>全 null roster 可包含多个质量级；非空管线、旧 scratch 或未知数量均不接管。</summary>
        internal static bool TryValidateQualityBuiltinState(int rosterCount, int nonNullCount, int scratchAssetsCount, int scratchTypesCount, out string error)
        {
            error = null;
            if (rosterCount < 0 || nonNullCount != 0) error = "完整 Quality roster 非全 managed null 或数量未知；不按 Builtin 分支放行。";
            else if (scratchAssetsCount != 0 || scratchTypesCount != 0) error = "QualitySettings 原 scratch 非空或未知；保留旧状态，不清缓存。";
            return error == null;
        }

        /// <summary>仅还原准确原空容器及本次冻结的官方完成内容；未知引用、数量或元素不清理。</summary>
        internal static bool TryRestoreQualityBuiltinState(QualityBuiltinBaseline baseline,
            List<UnityEngine.Rendering.RenderPipelineAsset> assets, HashSet<Type> types, out string error)
        {
            error = null;
            if (baseline == null || baseline.rosterCount < 0 || assets == null || types == null ||
                !ReferenceEquals(baseline.assets, assets) || !ReferenceEquals(baseline.types, types))
                error = "QualitySettings scratch 原引用缺失或已替换；保留未知状态。";
            else if (!(assets.Count == 0 && types.Count == 0) &&
                !(assets.Count == baseline.rosterCount && assets.All(value => ReferenceEquals(value, null)) &&
                  (baseline.rosterCount == 0 ? types.Count == 0 : types.Count == 1 && types.Contains(null))))
                error = "QualitySettings scratch 内容不是原空或本次精确全 null 完成态；保留实际残留。";
            if (error != null) return false;
            assets.Clear();
            types.Clear();
            return true;
        }

        /// <summary>冻结两个准确 SRP 回调的 Builtin 空资产分支；只读目标完整 roster 与缓存字段，不调用懒初始化 getter。</summary>
        /// <param name="callback">准确 CorePreprocessBuild 或 ProbeVolumeBuildProcessor 声明类型。</param>
        /// <param name="target">Probe 使用的拟构建目标；Core 另读取并冻结真实活动目标。</param>
        /// <param name="inputs">追加成对程序集身份、正式源码、完整 roster 和原空状态摘要。</param>
        /// <param name="token">源码、DLL 与 APV 原树摘要捕获的取消令牌。</param>
        internal static void CaptureSrpBuiltinCallback(Type callback, BuildTarget target, ICollection<string> inputs, CancellationToken token)
        {
            if (callback == null || callback.Assembly.GetName().Name != "Unity.RenderPipelines.Core.Editor" ||
                callback.FullName != "UnityEditor.Rendering.CorePreprocessBuild" && callback.FullName != "UnityEditor.Rendering.ProbeVolumeBuildProcessor")
                throw new InvalidOperationException("SRP 回调不属于已审两个准确类型。");
            RequireAuditedSrpAssembly(callback, inputs, token);
            foreach (string[] source in new[]
            {
                new[] { "Editor/BuildTargetExtensions.cs", "d7309c7dfe0edb0dbfff9f5f6db8f17f655b0740acd736a423b0f1334b5fb29f" },
                new[] { "Editor/BuildProcessors/CoreBuildData.cs", "5ca66359d8329ea1e88afedfaba140ee7278db615b72fef1fa5f69898b881e76" },
                new[] { "Editor/BuildProcessors/CorePreprocessBuild.cs", "a6fe7b42ea439f9c1d08e9b4ca07cad9159f9b40dd25800f47bcff53fc8375cf" },
                new[] { "Editor/Lighting/ProbeVolume/ProbeVolumeBuildProcessor.cs", "acf28096f7be75cdbf921ec7b018499c39f9b6d074642f9b61a6e6192e0b1cac" },
                new[] { "Editor/AssetDatabaseHelper.cs", "7df9edf7fb2e2414faeebeb52531b00c2dda4364fcfbbe38648826a7cd480486" },
            }) RequirePackageSource(callback, "com.unity.render-pipelines.core", source[0], source[1], inputs, token);
            Type bakingSet = FindType("UnityEngine.Rendering.ProbeVolumeBakingSet");
            if (bakingSet == null || bakingSet.Assembly.GetName().Name != "Unity.RenderPipelines.Core.Runtime")
                throw new InvalidOperationException("SRP Runtime baking-set 类型身份不匹配。");
            RequireAuditedSrpAssembly(bakingSet, inputs, token);
            foreach (string[] source in new[]
            {
                new[] { "Runtime/Settings/IncludeRenderPipelineAsset.cs", "d0554a7680459fc842ccff749b7e62b11763a92e8ebec7638d56cd160a5a95c0" },
                new[] { "Runtime/Lighting/ProbeVolume/IProbeVolumeEnabledRenderPipeline.cs", "ea3791c2d13310c346cd7e181253d0e768e9aaef75c831ff92d6a8ce9aebc585" },
            }) RequirePackageSource(bakingSet, "com.unity.render-pipelines.core", source[0], source[1], inputs, token);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            Type coreData = callback.Assembly.GetType("UnityEditor.Rendering.CoreBuildData", true);
            Type coreProcessor = callback.Assembly.GetType("UnityEditor.Rendering.CorePreprocessBuild", true);
            Type probeProcessor = callback.Assembly.GetType("UnityEditor.Rendering.ProbeVolumeBuildProcessor", true);
            FieldInfo coreField = coreProcessor.GetField("m_BuildData", flags);
            FieldInfo instanceField = coreData.GetField("m_Instance", flags);
            FieldInfo processedField = probeProcessor.GetField("s_BakingSetsProcessedLastBuild", flags);
            if (coreField?.FieldType != coreData || instanceField?.FieldType != coreData ||
                processedField?.FieldType != typeof(HashSet<>).MakeGenericType(bakingSet))
                throw new InvalidOperationException("SRP 缓存 backing field 与已审身份不匹配。");
            // 仅读默认空槽；Probe 的已审静态初始化只创建内存 HashSet，不清理旧集合或资产。
            object core = coreField.GetValue(null);
            object instance = instanceField.GetValue(null);
            object processed = processedField.GetValue(null);
            PropertyInfo processedCount = processedField.FieldType.GetProperty("Count");
            int count = processed == null ? -1 : (int)processedCount.GetValue(processed);
            Type helper = callback.Assembly.GetType("UnityEditor.Rendering.BuildTargetExtensions", true);
            MethodInfo method = helper.GetMethods(flags).Single(value => value.Name == "TryGetRenderPipelineAssets" &&
                value.IsGenericMethodDefinition && value.GetGenericArguments().Length == 1 && value.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(UnityEngine.Rendering.RenderPipelineAsset));
            ParameterInfo[] parameters = method.GetParameters();
            if (method.ReturnType != typeof(bool) || parameters[0].ParameterType != typeof(BuildTarget) ||
                parameters[1].ParameterType != typeof(List<UnityEngine.Rendering.RenderPipelineAsset>))
                throw new InvalidOperationException("SRP 目标完整 roster helper 签名已变化。");
            // Core CreateInstance 实际消费 activeBuildTarget；Probe 消费 BuildPlayerOptions.target，二者分别冻结。
            BuildTarget effectiveTarget = callback.FullName == "UnityEditor.Rendering.CorePreprocessBuild" ?
                EditorUserBuildSettings.activeBuildTarget : target;
            var assets = new List<UnityEngine.Rendering.RenderPipelineAsset>();
            bool obtained = (bool)method.Invoke(null, new object[] { effectiveTarget, assets });
            if (obtained != (assets.Count != 0)) throw new InvalidOperationException("SRP 完整 roster 结果不符已审合同。");
            if (!TryValidateSrpBuiltinState(assets.Count, core == null, instance == null, count, out string error))
                throw new InvalidOperationException(error);
            if (!ReferenceEquals(core, coreField.GetValue(null)) || !ReferenceEquals(instance, instanceField.GetValue(null)) ||
                !ReferenceEquals(processed, processedField.GetValue(null)) || count != (int)processedCount.GetValue(processed))
                throw new InvalidOperationException("SRP 只读捕获期间缓存状态已变化。");
            inputs.Add("SRPBuiltinRoster=" + effectiveTarget + "|empty");
            inputs.Add("SRPBuiltinState=core:null|instance:null|probe:0");
            inputs.Add("SRPBuiltinAPV=" + Util.Json.Serialize(CapturePaths(new[] { "Library/TempAPVStreamingAssets" }, token)));
        }

        /// <summary>只接受完整 roster 为空、双 Core 空槽与 Probe 空集合；未知数量或旧缓存不清理、不猜归属。</summary>
        internal static bool TryValidateSrpBuiltinState(int rosterCount, bool coreSlotNull, bool coreInstanceNull,
            int processedCount, out string error)
        {
            error = null;
            if (rosterCount != 0)
                error = "当前只接纳 Builtin 空 SRP roster；非空资产的 supportProbeVolume/GPU getter 尚未进入合同。";
            else if (!coreSlotNull || !coreInstanceNull)
                error = "CoreBuildData 存在旧缓存或构建残留，无法唯一归属本轮；保留状态并阻断，不调用 instance 或 Dispose。";
            else if (processedCount != 0)
                error = "Probe 上次处理集合非空或未知；不能按无 APV 写入分支接管或清空。";
            return error == null;
        }

        /// <summary>Nova SDK 当前没有 WebGL 覆盖，构造与实际程序集指纹仍独立冻结。</summary>
        private static void CaptureSdkNoWebglHooks(Type type, ICollection<string> inputs, CancellationToken token)
        {
            string module;
            string file;
            string expected;
            switch (type.FullName)
            {
                case "NovaFramework.Editor.NativeBuildProcessor":
                    module = null; file = "Scripts/Editor/BuildProcessor/Native/NativeBuildProcessor.cs"; expected = "d39e80105ff80fda84e02263524b940d1185204fbb1a674e0831add27611ae01"; break;
                case "NovaFramework.SDK.AppleSignIn.Editor.AppleSignInPluginBuildProcessor":
                    module = "applesignin"; file = "AppleSignInPluginBuildProcessor.cs"; expected = "6787a735ea8d39d7d11376717e929cfe6f314c1b7312e6ec44878228949b8bb2"; break;
                case "NovaFramework.SDK.AppsFlyerPlugin.Editor.AppsFlyerPluginBuildProcessor":
                    module = "appsflyer"; file = "AppsFlyerPluginBuildProcessor.cs"; expected = "7be334cf69d3787ad1f56e0527e7da4a9319d240b2cf05f966e061cf228d6ad2"; break;
                case "NovaFramework.SDK.Facebook.Editor.FacebookPluginBuildProcessor":
                    module = "facebook"; file = "FacebookPluginBuildProcessor.cs"; expected = "794683c034f6e057180c9f8560d68cb6a4a9238d9690a43540ddf58335535f73"; break;
                case "NovaFramework.SDK.FirebasePlugin.Editor.FirebasePluginBuildProcessor":
                    module = "firebase"; file = "FirebasePluginBuildProcessor.cs"; expected = "17365decefedbeadc9174f99a836a8006f672a84194cee228459d3d7ab8393a1"; break;
                case "NovaFramework.SDK.GoogleSignIn.Editor.GoogleSignInPluginBuildProcessor":
                    module = "googlesignin"; file = "GoogleSignInPluginBuildProcessor.cs"; expected = "0ccdcd9877faed9a5d04cadcd4fd6889a997855a692ae136809100646b1e22db"; break;
                case "NovaFramework.SDK.MaxAdPlugin.Editor.MaxAdPluginBuildProcessor":
                    module = "max"; file = "MaxAdPluginBuildProcessor.cs"; expected = "3cef928bd4342b5590fbfed242390af089a900ea12750138b0744f332bf4bee7"; break;
                default: throw new InvalidOperationException("Nova SDK 不属于已审七个具体实现。");
            }
            RequirePackageSource(typeof(NovaSDKBuildProcessor), "com.solotopia.nova.framework", "Scripts/Editor/BuildProcessor/Core/NovaSDKBuildProcessor.cs", "a1a8c442997b86e38b541245a815aa491b9282de92913d8fdc478e798ac3441a", inputs, token);
            RequirePackageSource(type, module == null ? "com.solotopia.nova.framework" : "com.solotopia.nova.framework.sdk." + module,
                module == null ? file : "Nova/Scripts/Editor/BuildProcessor/" + file, expected, inputs, token);
            string[] methods = { "OnPreprocessBuildOnWebGL", "OnAfterNovaPreprocessBuildOnWebGL", "OnPostprocessBuildOnWebGL", "OnAfterNovaPostprocessBuildOnWebGL" };
            foreach (string name in methods)
                if (type.GetMethod(name)?.DeclaringType != typeof(NovaSDKBuildProcessor))
                    throw new InvalidOperationException("Nova SDK 存在当前尚未审核的 WebGL override：" + name);
            ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null || type.TypeInitializer != null) throw new InvalidOperationException("Nova SDK 构造或静态初始化与已审 WebGL 空分支不一致。");
            inputs.Add(type.FullName + "|ctor=" + GenerateActionCommon.ComputeTextHash(Convert.ToBase64String(constructor.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>())));
            inputs.Add(type.FullName + "|no-WebGL-overrides");
        }

        /// <summary>逐文件冻结 HybridCLR 的五个真实回调及共享路径实现，不把包名当行为合同。</summary>
        private static void CaptureHybridCallbackSource(Type type, ICollection<string> inputs, CancellationToken token)
        {
            string file;
            string expected;
            switch (type.FullName)
            {
                case "HybridCLR.Editor.BuildProcessors.CheckSettings": file = "CheckSettings.cs"; expected = "8ee35ca507f002bedf8825ba726ea8cacd33d78aaf37a22ece6fbfccb054bf00"; break;
                case "HybridCLR.Editor.BuildProcessors.CopyStrippedAOTAssemblies": file = "CopyStrippedAOTAssemblies.cs"; expected = "d1faa5b527bf5483f07d42b9386b0d5a62ee889d3239546d89a95cd89f887ee4"; break;
                case "HybridCLR.Editor.BuildProcessors.FilterHotFixAssemblies": file = "FilterHotFixAssemblies.cs"; expected = "b14ac6a3f63bb4988a82709a9d60e05ab9afc2afc060061fa760bc89b790f36f"; break;
                case "HybridCLR.Editor.BuildProcessors.MsvcStdextWorkaround": file = "MsvcStdextWorkaround.cs"; expected = "9ea10f6b6001a5c06f1c858a5a0aa48bdc6b3c428c47339fe59ae5570f699f9e"; break;
                case "HybridCLR.Editor.BuildProcessors.PatchScriptingAssemblyList": file = "PatchScriptingAssemblyList.cs"; expected = "9a79a34c94b34a84c3457fd7aca2ba5f013aa6fa2592a15747ef7fc6fff05e3d"; break;
                default: throw new InvalidOperationException("未知 HybridCLR Player 回调。");
            }
            RequirePackageSource(type, "com.solotopia.hybridclr", "Core/Editor/BuildProcessors/" + file, expected, inputs, token);
            RequirePackageSource(typeof(SettingsUtil), "com.solotopia.hybridclr", "Core/Editor/SettingsUtil.cs", "9c0cb3d954bb750f252d4d61fecdb86069d995776dea0ab876f7c03686dcd320", inputs, token);
        }

        /// <summary>只读已加载 Input 设置 backing field，避免调用 setter 或首次建立默认配置。</summary>
        private static void CaptureInputSettings(Type callback, ICollection<string> inputs, CancellationToken token)
        {
            RequirePackageSource(callback, "com.unity.inputsystem", "InputSystem/Editor/Internal/BuildProviderHelpers.cs", "62ac3ed7f034ca8edde3c17a88f9aed50a36239a1d4bf1c8235657575d141c7b", inputs, token);
            Type system = callback.Assembly.GetType("UnityEngine.InputSystem.InputSystem", false);
            if (system == null || !system.IsDefined(typeof(InitializeOnLoadAttribute), false))
                throw new InvalidOperationException("InputSystem 当前初始化身份与已审源码不匹配。");
            RequirePackageSource(system, "com.unity.inputsystem", "InputSystem/InputSystem.cs", "c6a32a4cd7af54de398e3d71ee89317d7b69af78176087c409890c7973b731eb", inputs, token);
            RequirePackageSource(system, "com.unity.inputsystem", "InputSystem/InputManager.cs", "71c69ea70d421d7c44c890fd8a7c6ee9d2c20990a1a00265071db9c9f691dbb4", inputs, token);
            object manager = system.GetField("s_Manager", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            UnityEngine.Object settings = manager?.GetType().GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(manager) as UnityEngine.Object;
            if (manager == null || settings == null) throw new InvalidOperationException("InputSystem 已加载 manager/settings 无法精确读取；不初始化替代输入。");
            inputs.Add("InputSettings=" + ObjectIdentity(settings) + "|" + HashObject(settings) + "|persistent=" + EditorUtility.IsPersistent(settings));
            CaptureObjectSource(settings, inputs, token);
        }

        /// <summary>持久化回调对象冻结正文和 meta；内存对象保留准确身份，不假造文件。</summary>
        private static void CaptureObjectSource(UnityEngine.Object value, ICollection<string> inputs, CancellationToken token)
        {
            string path = AssetDatabase.GetAssetPath(value);
            if (string.IsNullOrEmpty(path)) return;
            inputs.Add("CallbackObjectSource=" + Util.Json.Serialize(CapturePaths(new[] { path, path + ".meta" }, token)));
        }

        /// <summary>精确验证本轮精读的缓存包源码指纹，版本改变后旧合同失效。</summary>
        private static void RequirePackageSource(Type type, string packageName, string relative, string expected,
            ICollection<string> inputs, CancellationToken token)
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly);
            string path = package == null ? null : Path.Combine(package.resolvedPath, relative);
            bool identityMatches = package?.name == packageName;
            if (package == null && packageName == "com.solotopia.nova.framework" && type.Assembly == typeof(EditorUtil).Assembly)
            {
                // 开发态 Framework 编译自 Assets；沿用 Skills 的唯一框架解析，不放宽其他包或相同名称的程序集。
                string root = Path.GetFullPath(Path.Combine(EditorUtil.AgentSkills.ResolveAgentsRoot(), ".."));
                string manifest = Path.Combine(root, "package.json");
                var identity = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(manifest));
                identityMatches = (string)identity["name"] == packageName;
                path = Path.Combine(root, relative);
                inputs.Add("CallbackFrameworkManifest=" + GenerateActionCommon.ComputeFileHash(manifest, token));
            }
            if (!identityMatches || !File.Exists(path) || GenerateActionCommon.ComputeFileHash(path, token) != expected)
                throw new InvalidOperationException("回调精确包源码与已审指纹不匹配：" + packageName + "/" + relative);
            CapturePackageIdentity(type, inputs, token);
            inputs.Add("CallbackSource=" + packageName + "/" + relative + "|" + expected);
        }

        /// <summary>记录当前包和实际 DLL，只读获取身份不实例化回调。</summary>
        private static void CapturePackageIdentity(Type type, ICollection<string> inputs, CancellationToken token)
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly);
            inputs.Add("CallbackPackage=" + type.FullName + "|" + package?.name + "|" + package?.version);
            string location = type.Assembly.Location;
            if (string.IsNullOrEmpty(location) || !File.Exists(location)) throw new InvalidOperationException("回调没有实际 DLL 指纹。");
            inputs.Add("CallbackAssembly=" + type.Assembly.FullName + "|" + GenerateActionCommon.ComputeFileHash(location, token));
        }

        /// <summary>精确核对当前安装引擎或 vendor DLL 的已审正文 SHA。</summary>
        private static void RequireAssembly(Type type, string expected, ICollection<string> inputs, CancellationToken token)
        {
            if (GenerateActionCommon.ComputeFileHash(type.Assembly.Location, token) != expected)
                throw new InvalidOperationException("回调实际 DLL 与已审版本不一致。");
            CapturePackageIdentity(type, inputs, token);
        }

        /// <summary>只读三个准确构建后 reset 字段；同一字段的 before 与 after 均使用实际模块和整 DLL 身份。</summary>
        /// <param name="inputs">当前准确回调 marker 集合，未知或不存在的 marker 不产生字段条目。</param>
        /// <param name="token">程序集文件哈希取消令牌。</param>
        /// <returns>按固定回调顺序排列的实际状态，不调用 getter、callback 或 SetValue。</returns>
        internal static NormalResetState[] CaptureNormalResetStates(IEnumerable<string> inputs, CancellationToken token)
        {
            var result = new List<NormalResetState>();
            foreach (string[] definition in new[]
            {
                new[] { "Firebase.Crashlytics.Editor.AndroidPreBuild.BuildComplete [PostProcessBuild]", "Firebase.Crashlytics.Editor.AndroidPreBuild", "Generated", "System.Boolean", "5a23d6ae59c11fd1a2ddfee94087dec26eec054d3618fad4ea9e7c1dfeae1e18" },
                new[] { "GooglePlayServices.PlayServicesPreBuild.BuildComplete [PostProcessBuild]", "GooglePlayServices.PlayServicesPreBuild", "HasWarned", "System.Boolean", "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0" },
                new[] { "GooglePlayServices.PlayServicesResolver.OnPostProcessBuild [PostProcessBuild]", "GooglePlayServices.PlayServicesResolver", "scenesProcessed", "System.Int32", "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0" },
            })
            {
                if (!inputs.Contains("NormalPostbuildReset=" + definition[0], StringComparer.Ordinal)) continue;
                Type type = FindType(definition[1]);
                if (type == null || GenerateActionCommon.ComputeFileHash(type.Assembly.Location, token) != definition[4])
                    throw new InvalidOperationException("正常 reset 类型或实际 DLL 身份变化。");
                // Resolver 在正式 Editor 域由 Unity InitializeOnLoad 初始化；不执行其 getter 或私有初始化方法。
                if (definition[1] == "GooglePlayServices.PlayServicesResolver" ? !type.IsDefined(typeof(InitializeOnLoadAttribute), false) : type.TypeInitializer != null)
                    throw new InvalidOperationException("正常 reset 类型的已审初始化身份变化。");
                FieldInfo field = type.GetField(definition[2], BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (field == null || field.DeclaringType != type || field.FieldType.FullName != definition[3])
                    throw new InvalidOperationException("正常 reset 的准确静态字段身份变化。");
                result.Add(new NormalResetState
                {
                    callbackLabel = definition[0], typeName = definition[1], fieldName = definition[2], fieldType = definition[3],
                    assemblySha256 = definition[4], moduleVersionId = type.Module.ModuleVersionId.ToString("D"),
                    value = Convert.ToInt32(field.GetValue(null)),
                });
            }
            return result.ToArray();
        }

        /// <summary>仅将已明确允许正常 reset 的标量投影为零，保留每个字段与程序集身份；不修改输入对象。</summary>
        /// <param name="states">实际状态数组。</param>
        /// <returns>用于比较不变量的独立副本。</returns>
        internal static NormalResetState[] NormalizeNormalResetStates(NormalResetState[] states)
        {
            if (states == null || states.Any(state => state == null)) throw new InvalidOperationException("正常 reset 状态缺失。");
            return states.Select(state => new NormalResetState
            {
                callbackLabel = state.callbackLabel, typeName = state.typeName, fieldName = state.fieldName,
                fieldType = state.fieldType, assemblySha256 = state.assemblySha256, moduleVersionId = state.moduleVersionId, value = 0,
            }).ToArray();
        }

        /// <summary>确认后到真正构建前必须逐字段保留原值；正常 reset 的 after 豁免不能用于写前漂移。</summary>
        /// <param name="expected">Plan 冻结的准确状态。</param>
        /// <param name="actual">当前实际读取状态。</param>
        /// <returns>两组非空数组全部字段、顺序与原值相等时返回 true。</returns>
        internal static bool NormalResetBaselinesEqual(NormalResetState[] expected, NormalResetState[] actual)
        {
            return expected != null && actual != null && !expected.Any(state => state == null) && !actual.Any(state => state == null) &&
                Util.Json.Serialize(expected) == Util.Json.Serialize(actual);
        }

        /// <summary>回执要求正常链返回后的实际值为零、当前值仍为零，且 before/after/current 保留同一完整字段身份。</summary>
        /// <param name="before">原 Plan 或执行回执保存的实际 before。</param>
        /// <param name="after">正常回调后实际读回的 after。</param>
        /// <param name="current">Verify 或 Dispose 当前实际状态。</param>
        /// <param name="error">缺失、身份或后验不符的原因。</param>
        /// <returns>全组准确身份与预期零值均吻合时返回 true。</returns>
        internal static bool TryValidateNormalResetReceipt(NormalResetState[] before, NormalResetState[] after, NormalResetState[] current, out string error)
        {
            error = null;
            if (before == null || after == null || current == null || before.Any(state => state == null) || after.Any(state => state == null) || current.Any(state => state == null))
                error = "正常 reset 的 before/after/current 材料缺失。";
            else if (before.Any(state => state.fieldType == "System.Boolean" && state.value != 0 && state.value != 1) ||
                after.Any(state => state.value != 0) || current.Any(state => state.value != 0))
                error = "正常 reset 的原布尔或实际后验值不符；不主动修改字段。";
            else if (Util.Json.Serialize(NormalizeNormalResetStates(before)) != Util.Json.Serialize(NormalizeNormalResetStates(after)) ||
                Util.Json.Serialize(NormalizeNormalResetStates(after)) != Util.Json.Serialize(NormalizeNormalResetStates(current)))
                error = "正常 reset 的准确 callback/字段/模块身份或完整集合变化。";
            return error == null;
        }

        /// <summary>回调 hash 保留全部不变量和准确 reset 合同，标量 before 由独立写前复核绑定，不丢弃输入保护。</summary>
        /// <param name="snapshot">当前完整回调快照。</param>
        /// <returns>只规整明确 reset 标量后的完整合同哈希。</returns>
        private static string ComputeCallbackContractHash(CallbackInputSnapshot snapshot)
        {
            var invariant = new CallbackInputSnapshot
            {
                identities = snapshot.identities, inputs = snapshot.inputs, writes = snapshot.writes,
                temporaryFiles = snapshot.temporaryFiles, temporaryBaseline = snapshot.temporaryBaseline,
                temporaryDirectoryIdentities = snapshot.temporaryDirectoryIdentities, unsupported = snapshot.unsupported,
                normalResetStates = NormalizeNormalResetStates(snapshot.normalResetStates),
            };
            return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(invariant) + "|TMP=" + PlayerTmpFontBuildContract.InvariantHash(snapshot.tmpFonts) +
                "|Facebook=" + PlayerFacebookBuildContract.InvariantHash(snapshot.facebook) +
                "|BuiltinUsage=" + PlayerBuiltinUsageBuildContract.InvariantHash(snapshot.builtinUsage) +
                "|BuildEvents=" + PlayerBuildEventsContract.InvariantHash(snapshot.buildEvents) +
                "|EmptyUsage=" + PlayerEmptyUsageBuildContract.InvariantHash(snapshot.emptyUsage) +
                "|FiniteAdditional=" + PlayerFiniteAdditionalBuildContract.InvariantHash(snapshot.finiteAdditional) +
                "|PackageCallbacks=" + PlayerPackageCallbacksContract.InvariantHash(snapshot.packageCallbacks));
        }

        /// <summary>冻结 Firebase 非 iOS 分支的准确日志下游，仅接纳无文件名、无未知委托的既有 Logger 状态。</summary>
        /// <param name="callback">当前已加载的准确 XcodeProjectModifier 类型。</param>
        /// <param name="inputs">记录日志字段和命令行摘要，禁止输出完整命令行。</param>
        /// <param name="writes">声明原固定 WebGL debug 文本可能写入的 Console 或 stdout。</param>
        /// <param name="token">程序集哈希取消令牌。</param>
        private static void CaptureFirebaseXcodeLogger(Type callback, ICollection<string> inputs, ISet<string> writes, CancellationToken token)
        {
            RequireAssembly(callback, "b50eefa93d23c673bc8a97266e3d36dcfba0e9d3e49f5e7cc7850d595c12f1bf", inputs, token);
            if (callback.FullName != "Firebase.Editor.XcodeProjectModifier")
                throw new InvalidOperationException("Firebase Xcode 回调类型不在已审合同内。");
            const BindingFlags sf = BindingFlags.NonPublic | BindingFlags.Static;
            const BindingFlags inf = BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo categoryField = callback.GetField("logger", sf);
            Type categoryType = categoryField?.FieldType;
            if (categoryType == null || categoryType.FullName != "Firebase.Editor.CategoryLogger" || categoryType.Assembly != callback.Assembly)
                throw new InvalidOperationException("Firebase CategoryLogger 字段身份变化。");
            FieldInfo loggerField = categoryType.GetField("logger", inf);
            FieldInfo prefixField = categoryType.GetField("category", inf);
            Type loggerType = loggerField?.FieldType;
            if (loggerType == null || loggerType.FullName != "Google.Logger" || loggerType.Assembly.GetName().Name != "Google.VersionHandlerImpl" ||
                prefixField?.FieldType != typeof(string) || !prefixField.IsInitOnly)
                throw new InvalidOperationException("Firebase Logger 下游类型或固定类别字段变化。");
            RequireAssembly(loggerType, "c3a6d0419b41b0cbc7ad518b0cc2b311af043b5d71ed709ea3baa576b79862ce", inputs, token);
            FieldInfo levelField = loggerType.GetField("<Level>k__BackingField", inf);
            FieldInfo targetField = loggerType.GetField("<Target>k__BackingField", inf);
            FieldInfo filenameField = loggerType.GetField("<LogFilename>k__BackingField", inf);
            FieldInfo messageField = loggerType.GetField("LogMessage", inf);
            if (levelField?.FieldType.FullName != "Google.LogLevel" || targetField?.FieldType.FullName != "Google.LogTarget" ||
                filenameField?.FieldType != typeof(string) || messageField == null || !typeof(Delegate).IsAssignableFrom(messageField.FieldType))
                throw new InvalidOperationException("Google Logger backing fields 与已审合同不符。");
            // 固定 Xcode cctor 仅创建常量、CategoryLogger/Google.Logger 与字典；不注册事件、加载配置或发日志。
            object category = categoryField.GetValue(null);
            object logger = category == null ? null : loggerField.GetValue(category);
            if (category == null || category.GetType() != categoryType || logger == null || logger.GetType() != loggerType ||
                (string)prefixField.GetValue(category) != "[XcodeProjectModifier] ")
                throw new InvalidOperationException("Firebase Xcode 实际 Logger 或固定类别未知。");
            int level = Convert.ToInt32(levelField.GetValue(logger));
            int target = Convert.ToInt32(targetField.GetValue(logger));
            if (level < 0 || level > 4 || target < 0 || (target & ~7) != 0 || filenameField.GetValue(logger) != null || messageField.GetValue(logger) != null)
                throw new InvalidOperationException("Firebase Xcode Logger 含未知级别、文件目标或委托；不执行日志下游或修改设置。");
            string commandLine = Environment.CommandLine;
            bool debugSwitch = commandLine.ToLower().Contains("-gvh_log_debug");
            if (!ReferenceEquals(category, categoryField.GetValue(null)) || !ReferenceEquals(logger, loggerField.GetValue(category)) ||
                level != Convert.ToInt32(levelField.GetValue(logger)) || target != Convert.ToInt32(targetField.GetValue(logger)) ||
                filenameField.GetValue(logger) != null || messageField.GetValue(logger) != null)
                throw new InvalidOperationException("Firebase Xcode Logger 只读捕获期间状态变化。");
            inputs.Add("FirebaseXcodeLogger=level:" + level + "|target:" + target + "|filename:null|delegate:null|debugSwitch:" + debugSwitch);
            inputs.Add("FirebaseXcodeCommandLine=" + GenerateActionCommon.ComputeTextHash(commandLine) + "|culture:" + System.Globalization.CultureInfo.CurrentCulture.Name);
            inputs.Add("FirebaseXcodeWebGL=verified non-iOS/tvOS return after fixed debug logger; no project/delegate/config/save path");
            if (level == 0 || debugSwitch)
            {
                if ((target & 2) != 0) writes.Add("Unity Console: fixed Firebase Xcode skip-debug message for WebGL");
                if ((target & 1) != 0) writes.Add("Process stdout: fixed Firebase Xcode skip-debug message for WebGL");
            }
        }

        /// <summary>仅为精确已审 SRP 回调及其 Runtime 支持类型匹配成对的编译身份，不放宽源码或状态门。</summary>
        /// <param name="type">当前域实际回调或 ProbeVolumeBakingSet 类型。</param>
        /// <param name="inputs">记录当前完整程序集身份与实际 DLL 指纹的输入集合。</param>
        /// <param name="token">取消令牌。</param>
        private static void RequireAuditedSrpAssembly(Type type, ICollection<string> inputs, CancellationToken token)
        {
            if (type == null || type.FullName != "UnityEditor.Rendering.Analytics.VolumeProfileOverridesAnalytic" &&
                type.FullName != "UnityEditor.Rendering.CorePreprocessBuild" &&
                type.FullName != "UnityEditor.Rendering.ProbeVolumeBuildProcessor" &&
                type.FullName != "UnityEditor.Rendering.RenderPipelineGraphicsSettingsAnalytics" &&
                type.FullName != "UnityEditor.Rendering.Analytics.BuildTargetAnalytic" &&
                type.FullName != "UnityEngine.Rendering.ListPool`1" &&
                type.FullName != "UnityEngine.Rendering.ProbeVolumeBakingSet")
                throw new InvalidOperationException("类型不属于已审 SRP 空分支的精确集合。");
            string hash = GenerateActionCommon.ComputeFileHash(type.Assembly.Location, token);
            if (!IsAuditedSrpAssemblyIdentity(type.Assembly.GetName().Name, type.Module.ModuleVersionId.ToString("D"), hash))
                throw new InvalidOperationException("SRP 实际程序集名称、MVID 与 DLL 指纹未成对匹配已审编译身份。");
            CapturePackageIdentity(type, inputs, token);
        }

        /// <summary>校验已离线逐方法核对的 Android/WebGL 编译变体，拒绝未知身份和跨变体拼接。</summary>
        /// <param name="assemblyName">程序集短名称。</param>
        /// <param name="moduleVersionId">当前模块 MVID 的规范 D 格式。</param>
        /// <param name="sha256">实际 DLL 的完整 SHA-256。</param>
        /// <returns>名称、MVID 与哈希精确匹配同一条已审身份时返回 true。</returns>
        internal static bool IsAuditedSrpAssemblyIdentity(string assemblyName, string moduleVersionId, string sha256)
        {
            if (assemblyName == "Unity.RenderPipelines.Core.Editor")
                return moduleVersionId == "4d035734-6e59-48ec-a809-a0e8dc99f42f" &&
                    sha256 == "9682dcacd147095c8da2c916f7e74e54ad3dc33156dbd831aa8619391b1da72f" ||
                    moduleVersionId == "01872122-99fc-4c99-a81c-34e756273104" &&
                    sha256 == "28f303678ed87a2127048483d2279d3bc8fdfe6d693929bf3c4106019de40a07";
            if (assemblyName == "Unity.RenderPipelines.Core.Runtime")
                return moduleVersionId == "582ae75f-7fa3-4085-85b6-a284913ccaf3" &&
                    sha256 == "f2671dc7c1cdbb88bb6eab2b17aacaf1a0c0532bbfae6dbf28a3c33f6d1bfa05" ||
                    moduleVersionId == "c1f805c0-be87-4c1c-98ba-25d62aa3faa2" &&
                    sha256 == "15ac5102e62a1934ce2f4080a6848e56c69affc8f469eadd39f4c47aa6d4ae00";
            return false;
        }

        /// <summary>精确 pin 当前 TMP 双目标编译变体及清理/资源下游，只支持已审空表单 atlas 的正常写入。</summary>
        /// <param name="callback">准确 TMP_PreBuildProcessor。</param>
        /// <param name="inputs">源码、DLL 与真实字体输入。</param>
        /// <param name="writes">正常加载/清理/失败 UI 的明确效果。</param>
        /// <param name="token">文件指纹取消令牌。</param>
        private static void CaptureTmpFontNormalBranch(Type callback, ICollection<string> inputs, ISet<string> writes, CancellationToken token)
            => CaptureTmpFontNormalBranchCore(callback, inputs, writes, token, null);

        /// <summary>每次重新执行完整 TMP DLL/源/下游身份门，再明确选择原 Prepare 或不加载对象的后态观察。</summary>
        /// <param name="callback">当前准确 TMP 回调类型。</param>
        /// <param name="inputs">本次源码、DLL 和分类输入。</param>
        /// <param name="writes">本次精确写域。</param>
        /// <param name="token">文件读取取消令牌。</param>
        /// <param name="observation">null 允许原 Prepare 合法 Load，非 null 只读取原许可集。</param>
        private static void CaptureTmpFontNormalBranchCore(Type callback, ICollection<string> inputs, ISet<string> writes, CancellationToken token, TmpObservationContext observation)
        {
            string editorSha = GenerateActionCommon.ComputeFileHash(callback.Assembly.Location, token);
            Type font = FindType("TMPro.TMP_FontAsset");
            string runtimeSha = font == null ? null : GenerateActionCommon.ComputeFileHash(font.Assembly.Location, token);
            bool pair = editorSha == "261981dff5bcab3e8616f51dc8a03d6dad2c515b9875ebe9614165aadab82779" && runtimeSha == "2425e9486ec0642ee96b44c45d6a66435fa31825be1847df9bdbfba05114ae6b" ||
                editorSha == "a307d38e1b743130cf0a6ca50400aec3e5935dd2c38d37225f0af407965f09a2" && runtimeSha == "4cb817a22266adff6af39998e9bfb2b5b322734e533a224a1c114ab057712a57";
            if (callback.FullName != "TMPro.TMP_PreBuildProcessor" || !pair)
                throw new InvalidOperationException("TMP 实际完整 DLL 配对不属于已审 Android/WebGL 变体。");
            foreach (string[] source in new[]
            {
                new[] { "Runtime/TMP/TMP_FontAssetCommon.cs", "TMPro.KerningTable", "03264da872d202b1723e74d8039334953e79602dcdef2af2b697e35bad657473" },
                new[] { "Runtime/TMP/TMP_TextUtilities.cs", "TMPro.TMP_TextUtilities", "2da72b855acf767edb94494108b3625c4f837a6d16642a34b1325a67246d25c7" },
                new[] { "Runtime/TMP/TMP_FontAsset.cs", "TMPro.TMP_FontAsset", "0a5e57c7cd708b522518d183f39c6f958b6135a282ac724d6c8cd520e8e7798f" },
                new[] { "Runtime/TMP/TMP_Asset.cs", "TMPro.TMP_Asset", "249e0eecb80d0e9ee8ed700019e05f295e14c5648f0bd28e166137fc24b00c34" },
                new[] { "Runtime/TMP/TMP_FontFeatureTable.cs", "TMPro.TMP_FontFeatureTable", "ea13f58bf8d7ef230edcf6369f9fe374af1836cfccc6330b68e4caa5f41d62ad" },
                new[] { "Runtime/TMP/TMP_ResourcesManager.cs", "TMPro.TMP_ResourceManager", "4809056372bcc3f6569c7d292fe724a8ff4ac59404c3ab4f7dfa790ff0832130" },
                new[] { "Runtime/TMP/TMP_Settings.cs", "TMPro.TMP_Settings", "67b815d1e112d4bdad5a7935d8111630320fb180f3804c1298602f8676f2a237" },
                new[] { "Runtime/TMP/TMP_TextElement.cs", "TMPro.TMP_TextElement", "493e18e7d05b988751a2e0a48d46d3eb18cdc95a240084bc818c52911bf4f04a" },
                new[] { "Runtime/TMP/TMP_Character.cs", "TMPro.TMP_Character", "238a4ee040c542469ce3f9ba24b156d2caae463923d230af65e439677082de03" },
                new[] { "Editor/TMP/TMP_EditorResourceManager.cs", "TMPro.TMP_EditorResourceManager", "edd7491705a98659c403718d9cafabf450956c5fef57222ded21af565231a209" },
                new[] { "Editor/TMP/PropertyDrawers/TMP_PropertyDrawerUtilities.cs", "TMPro.EditorUtilities.TMP_PropertyDrawerUtilities", "d11f0c751b00812964e1fa2049c3625f41295dd799d3e536f7fef76a9880ee3e" },
            }) RequirePackageSource(FindType(source[1]) ?? throw new InvalidOperationException("TMP 下游类型不存在。"), "com.unity.ugui", source[0], source[2], inputs, token);
            RequireAssembly(typeof(Camera), "25281ab07e11130a45ede1e02cf3003666f9616cca68ae8ab9637f0ccffa33ad", inputs, token);
            RequireAssembly(typeof(Canvas), "d381d3539c5321ecc859f5ca0240c58a521e6510c1e18e9e81d24778bbc49a16", inputs, token);
            RequireAssembly(FindType("UnityEditor.TextCore.Text.TextCorePropertyDrawerUtilities"), "5ccf7a5a43c7b0dc5e1280f9b38142115c8d2ced8ee5518806fffbcb0eb3ef70", inputs, token);
            RequireAssembly(typeof(UnityEngine.TextCore.LowLevel.FontEngine), "47b47908e7dd8bf5033ae185b07db9728a68597b597d437f405dac618c885485", inputs, token);
            RequireAssembly(FindType("UnityEditor.TextCore.LowLevel.FontEngineEditorUtilities"), "0ecd4dd52fd978d0571c2ba66336d65fddc9591c5ca42f559cd9e592e514f143", inputs, token);
            if (observation == null) PlayerTmpFontBuildContract.Prepare(inputs, writes, token);
            else PlayerTmpFontBuildContract.ObservePrepared(inputs, writes, observation.before, observation.scopeAfter, token);
        }

        /// <summary>只接受字体实际过滤条件不命中的自然分支，不改字体标志或预清动态图集。</summary>
        private static void CaptureFontEarlyReturn(string filter, string typeName, bool assetsOnly, ICollection<string> inputs)
        {
            Type fontType = FindType(typeName);
            if (fontType == null) throw new InvalidOperationException("字体类型不存在。");
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            FieldInfo modeField = fontType.GetField("m_AtlasPopulationMode", flags);
            FieldInfo clearField = fontType.GetField("m_ClearDynamicDataOnBuild", flags);
            FieldInfo atlasField = fontType.GetField("m_AtlasTexture", flags);
            FieldInfo atlasesField = fontType.GetField("m_AtlasTextures", flags);
            if (modeField == null || clearField?.FieldType != typeof(bool) || atlasField?.FieldType != typeof(Texture2D) || atlasesField?.FieldType != typeof(Texture2D[]))
                throw new InvalidOperationException("字体实际 backing fields 与已审源码不匹配。");
            foreach (string path in AssetDatabase.FindAssets(filter).Select(AssetDatabase.GUIDToAssetPath).OrderBy(value => value, StringComparer.Ordinal))
            {
                if (assetsOnly && !path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;
                UnityEngine.Object font = AssetDatabase.LoadAssetAtPath(path, fontType);
                if (font == null) continue;
                int mode = Convert.ToInt32(modeField.GetValue(font));
                bool clear = (bool)clearField.GetValue(font);
                inputs.Add("Font=" + ObjectIdentity(font) + "|" + HashObject(font) + "|" + mode + "|" + clear);
                if ((mode != 1 && mode != 2) || !clear) continue;
                var atlas = atlasField.GetValue(font) as Texture2D;
                if (atlas == null)
                    throw new InvalidOperationException("动态图集过滤将触发 atlasTexture 懒初始化或空引用；只读计划不会调用该 getter：" + path);
                inputs.Add("FontAtlas=" + ObjectIdentity(atlas) + "|" + atlas.width + "|" + atlas.height);
                if (atlas.width != 0)
                    throw new InvalidOperationException("动态图集构建清理会修改字体和 atlas，目前缺完整对象恢复合同：" + path);
            }
        }

        /// <summary>只读取已初始化 Visual Scripting 使用缓存，真实启用时不跳过生成。</summary>
        private static void CaptureUnusedVisualScripting(Type callbackType, ICollection<string> inputs, CancellationToken token)
        {
            string relative;
            string expected;
            switch (callbackType.FullName)
            {
                case "Unity.VisualScripting.AotPreBuilder": relative = "Editor/VisualScripting.Core/Platforms/AotPreBuilder.cs"; expected = "4d777ca77d94d176fd9671d6ebb611de3f7e82b780127d53c63c4ade26830a0f"; break;
                case "LinkerCreator": relative = "Editor/VisualScripting.Flow/Linker/LinkerCreator.cs"; expected = "3f01e8c0cfeef02e5f6186062c94fb17f8b8ae81854a9877e23081a9b96c1c8a"; break;
                case "Unity.VisualScripting.Analytics.OnPreprocessBuildAnalyticsEventHandler": relative = "Editor/VisualScripting.Core/Analytics/OnPreprocessBuildAnalyticsEventHandler.cs"; expected = "650e735c414c69af266d2e8c435613ae14c076442eeae8f83023b57503af75f5"; break;
                default: throw new InvalidOperationException("未知 Visual Scripting 构建回调。");
            }
            RequirePackageSource(callbackType, "com.unity.visualscripting", relative, expected, inputs, token);
            Type usage = FindType("Unity.VisualScripting.VSUsageUtility");
            if (usage == null || !SessionState.GetBool("Unity.VisualScripting.IsVisualScriptingUsedSet", false))
                throw new InvalidOperationException("Visual Scripting 使用缓存尚未在当前会话初始化；只读计划不触发 cctor 或旧版迁移。");
            RequirePackageSource(usage, "com.unity.visualscripting", "Editor/VisualScripting.Core/Utilities/VSUsageUtility.cs", "4a3c96085d6e7f0947e1e487706b9ad4e62bbf3c3db04cbc4209ed5a5800091d", inputs, token);
            FieldInfo field = usage?.GetField("_isVisualScriptingUsed", BindingFlags.NonPublic | BindingFlags.Static);
            if (field?.GetValue(null) is not bool used || used || SessionState.GetBool("Unity.VisualScripting.IsVisualScriptingUsed", false))
                throw new InvalidOperationException("当前 Visual Scripting 已使用或使用缓存未知，生成与场景恢复合同待补。");
            inputs.Add("VisualScriptingUsed=false|" + SessionState.GetBool("Unity.VisualScripting.IsVisualScriptingUsedSet", false));
        }

        /// <summary>间接 UGS provider 只接受当前三具体实现，冻结迁移源和精确命令行参数摘要。</summary>
        private static void CaptureServicesProviders(Type callbackType, ICollection<string> inputs, CancellationToken token)
        {
            Type contract = callbackType.Assembly.GetType("Unity.Services.Core.Configuration.Editor.IConfigurationProvider", false);
            if (contract == null) throw new InvalidOperationException("UGS 配置 provider 接口不存在。");
            foreach (Type provider in TypeCache.GetTypesDerivedFrom(contract).Where(value => !value.IsAbstract && !value.IsInterface && !value.ContainsGenericParameters))
            {
                string relative;
                string expected;
                switch (provider.FullName)
                {
                    case "Unity.Services.Core.Editor.CloudEnvironmentConfigProvider":
                        relative = "Editor/Core/CloudEnvironment/CloudEnvironmentConfigProvider.cs"; expected = "0ad77301e72b06dc2cb9b72581489dfe4c2057d261c3ec89ff604b4c5a5e2e63"; break;
                    case "Unity.Services.Core.Editor.OperatePackageVersionConfigProvider":
                        relative = "Editor/Core/Telemetry/OperatePackageVersionConfigProvider.cs"; expected = "1e01b9cf6be2cefec5f682a7e475dcdd3bcf3b6ad68b1db48876d9c1e4f93edc"; break;
                    case "Unity.Services.Core.Editor.Environments.Config.EnvironmentConfigProvider":
                        relative = "Editor/Core/Environments/Config/EnvironmentConfigProvider.cs"; expected = "11aadd9683e9e4fa42a453f6f8e5f2fb13e15e8550d1369d7135bcb02ae6ec4b"; break;
                    default: throw new InvalidOperationException("未知 UGS provider：" + provider.FullName);
                }
                RequirePackageSource(provider, "com.unity.services.core", relative, expected, inputs, token);
                inputs.Add("UGSProvider=" + provider.FullName + "|" + provider.Module.ModuleVersionId);
            }
            string settings = PhysicalPath("ProjectSettings/Packages/com.unity.services.core/Settings.json");
            if (File.Exists(settings))
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(settings));
                string name = (string)json["EnvironmentName"];
                string id = (string)json["EnvironmentId"];
                if (!string.IsNullOrEmpty(name) && (!Guid.TryParse(id, out Guid guid) || guid == Guid.Empty))
                    throw new InvalidOperationException("UGS 环境旧设置会触发保存迁移；请先明确处理该源状态再重新计划。");
                inputs.Add("UGSEnvironmentSource=" + GenerateActionCommon.ComputeFileHash(settings, token));
            }
            string[] args = Environment.GetCommandLineArgs();
            string arg = args.FirstOrDefault(value => value.StartsWith("-cloudEnvironment", StringComparison.Ordinal));
            int index = Array.IndexOf(args, arg);
            string cloud = arg == "-cloudEnvironment" && index >= 0 && index + 1 < args.Length ? args[index + 1] : arg;
            inputs.Add("CloudEnvironmentArgument=" + GenerateActionCommon.ComputeTextHash(cloud ?? string.Empty));
            CaptureAssemblyRoster(inputs);
        }

        /// <summary>冻结动态链接和包初始化发现的实际程序集闭包，避免只核外层回调。</summary>
        private static void CaptureAssemblyRoster(ICollection<string> inputs)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().Where(value => !value.IsDynamic).OrderBy(value => value.FullName, StringComparer.Ordinal))
                inputs.Add("LoadedAssembly=" + assembly.FullName + "|" + assembly.ManifestModule.ModuleVersionId);
        }

        /// <summary>固定委托字段必须真实为空；不清除或调用未知事件。</summary>
        private static void RequireEmptyDelegate(Type type, string name, ICollection<string> inputs)
        {
            FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (field == null || !typeof(Delegate).IsAssignableFrom(field.FieldType) || field.GetValue(null) != null)
                throw new InvalidOperationException("回调委托不属于当前已审空分支：" + name);
            inputs.Add(type.FullName + "." + name + "=null");
        }

        /// <summary>只按完整类型名查找当前程序集，不实例化或执行目标类型。</summary>
        private static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false))
                .Where(type => type != null).Distinct().SingleOrDefault();
        }

        /// <summary>持久对象使用 GUID/fileID，空对象以 null 记录。</summary>
        private static string ObjectIdentity(UnityEngine.Object value)
        {
            if (value == null) return "null";
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long fileId))
                return value.GetType().FullName + "|nonpersistent|" + UnityEngine.EntityId.ToULong(value.GetEntityId());
            return value.GetType().FullName + "|" + guid + "|" + fileId + "|" + AssetDatabase.GetAssetPath(value);
        }

        /// <summary>精确保留已审临时文件和 Input 设置，成功与失败都恢复原存在性。</summary>
        private sealed class PlayerCallbackExecutionScope : IDisposable
        {
            private readonly Dictionary<string, byte[]> m_Files;
            private readonly string[] m_OriginalDirectories;
            private readonly UnityEngine.Object[] m_Preloaded;
            private readonly UnityEngine.Object m_Actions;
            private readonly FieldInfo m_ProjectWide;
            private readonly object m_ProjectWideBefore;
            private readonly bool m_PerformanceCleanup;
            private readonly bool m_PerformanceCleanupExisted;
            private readonly CancellationToken m_Token;
            private bool m_Completed;
            private readonly string m_Il2CppEnvironment;
            private readonly string m_ClEnvironment;
            private readonly bool m_RestoreEnvironment;
            private readonly UnityEngine.Object m_NativeSettings;
            private readonly bool m_NativeSettingsDirty;
            private readonly bool m_ActionsDirty;
            private readonly string m_ActionsHash;
            private readonly FieldInfo m_SrpCoreSlot;
            private readonly FieldInfo m_SrpCoreInstance;
            private readonly FieldInfo m_SrpProcessed;
            private readonly PropertyInfo m_SrpProcessedCount;
            private readonly string m_SrpApvBaseline;
            private readonly QualityBuiltinBaseline m_QualityBuiltin;
            private readonly string[] m_CallbackInputs;
            private readonly NormalResetState[] m_NormalResetBefore;
            public NormalResetState[] NormalResetAfter;
            public string NormalResetError;
            public bool ChainReturned;
            private readonly PlayerTmpFontBuildContract.Snapshot m_TmpFontBefore;
            public PlayerTmpFontBuildContract.Snapshot TmpFontAfter;
            public string TmpFontError;
            private readonly PlayerFacebookBuildContract.Snapshot m_FacebookBefore;
            public PlayerFacebookBuildContract.Snapshot FacebookAfter;
            public string FacebookError;
            private readonly PlayerBuiltinUsageBuildContract.Snapshot m_BuiltinUsageBefore;
            public PlayerBuiltinUsageBuildContract.Snapshot BuiltinUsageAfter;
            public PlayerBuiltinUsageBuildContract.ReportMaterial BuiltinUsageReport;
            public string BuiltinUsageError;
            private readonly PlayerBuildEventsContract.Snapshot m_BuildEventsBefore;
            public PlayerBuildEventsContract.Snapshot BuildEventsAfter;
            public PlayerBuildEventsContract.ReportMaterial BuildEventsReport;
            public string BuildEventsError;
            private readonly PlayerEmptyUsageBuildContract.Snapshot m_EmptyUsageBefore;
            public PlayerEmptyUsageBuildContract.Snapshot EmptyUsageAfter;
            public PlayerEmptyUsageBuildContract.ReportMaterial EmptyUsageReport;
            public string EmptyUsageError;
            private readonly PlayerPackageCallbacksContract.Snapshot m_PackageCallbacksBefore;
            private readonly string[] m_PackageDependencyPaths;
            public PlayerPackageCallbacksContract.Snapshot PackageCallbacksAfter;
            public PlayerPackageCallbacksContract.ReportMaterial PackageCallbacksReport;
            public string PackageCallbacksError;
            private readonly PlayerFiniteAdditionalBuildContract.Snapshot m_FiniteAdditionalBefore;
            public PlayerFiniteAdditionalBuildContract.Snapshot FiniteAdditionalAfter;
            public PlayerFiniteAdditionalBuildContract.ReportMaterial FiniteAdditionalReport;
            public string FiniteAdditionalError;

            /// <summary>在真正构建前保存精确写集，正文只驻留内存，不输出凭据。</summary>
            /// <param name="callbacks">原 Plan 完整回调快照；先核真实 before，再保存精确临时恢复基线。</param>
            /// <param name="token">本轮执行取消令牌，成功完成恢复后继续检查取消。</param>
            /// <param name="restoreEnvironment">普通 Player 默认恢复环境；Generate 专用 false 仅保留官方正常转换，由独立类型化合同核对。</param>
            public PlayerCallbackExecutionScope(CallbackInputSnapshot callbacks, CancellationToken token, bool restoreEnvironment = true)
            {
                m_RestoreEnvironment = restoreEnvironment;
                m_CallbackInputs = callbacks.inputs;
                m_NormalResetBefore = callbacks.normalResetStates;
                m_TmpFontBefore = callbacks.tmpFonts;
                m_FacebookBefore = callbacks.facebook;
                m_BuiltinUsageBefore = callbacks.builtinUsage;
                m_BuildEventsBefore = callbacks.buildEvents;
                m_EmptyUsageBefore = callbacks.emptyUsage;
                m_PackageCallbacksBefore = callbacks.packageCallbacks;
                m_PackageDependencyPaths = callbacks.packageDependencyPaths;
                if (!PlayerPackageCallbacksContract.BeforeEquals(m_PackageCallbacksBefore, PlayerPackageCallbacksContract.Capture(m_CallbackInputs, m_PackageDependencyPaths)))
                    throw new InvalidOperationException("Player 包回调原配置/内容/统计在构建前变化。");
                m_FiniteAdditionalBefore = callbacks.finiteAdditional;
                if (!PlayerFiniteAdditionalBuildContract.BeforeEquals(m_FiniteAdditionalBefore, PlayerFiniteAdditionalBuildContract.Capture(m_CallbackInputs)))
                    throw new InvalidOperationException("五项有限回调原状态写前变化。");
                if (!PlayerEmptyUsageBuildContract.BeforeEquals(m_EmptyUsageBefore, PlayerEmptyUsageBuildContract.Capture(m_CallbackInputs)))
                    throw new InvalidOperationException("SRP/Adaptive空usage输入在构建前变化。");
                if (!PlayerBuildEventsContract.BeforeEquals(m_BuildEventsBefore, PlayerBuildEventsContract.Capture(m_CallbackInputs)))
                    throw new InvalidOperationException("BuildEvents 原模块/窗口/统计在构建前变化；不开始构建。");
                if (!PlayerBuiltinUsageBuildContract.BeforeEquals(m_BuiltinUsageBefore, PlayerBuiltinUsageBuildContract.Capture(m_CallbackInputs)))
                    throw new InvalidOperationException("Unity usage 配置/缓存/委托在构建前变化；不开始构建。");
                if (!PlayerFacebookBuildContract.BeforeEquals(m_FacebookBefore, PlayerFacebookBuildContract.Capture(m_CallbackInputs, token)))
                    throw new InvalidOperationException("Facebook 原配置/singleton 在构建前变化；不开始构建。");
                if (!PlayerTmpFontBuildContract.BeforeEquals(m_TmpFontBefore, PlayerTmpFontBuildContract.Capture(m_CallbackInputs, token)))
                    throw new InvalidOperationException("TMP 字体/缓存/事件原状态在构建前变化；不开始构建。");
                if (!NormalResetBaselinesEqual(m_NormalResetBefore, CaptureNormalResetStates(m_CallbackInputs, token)))
                    throw new InvalidOperationException("正常 reset 原值在构建前变化；不开始构建。");
                m_QualityBuiltin = callbacks.qualityBuiltin;
                if (m_QualityBuiltin != null && (!ReferenceEquals(m_QualityBuiltin.assets, m_QualityBuiltin.assetsField.GetValue(null)) ||
                    !ReferenceEquals(m_QualityBuiltin.types, m_QualityBuiltin.typesField.GetValue(null)) ||
                    !TryValidateQualityBuiltinState(m_QualityBuiltin.rosterCount, 0, m_QualityBuiltin.assets.Count, m_QualityBuiltin.types.Count, out _)))
                    throw new InvalidOperationException("QualitySettings scratch 已离开本轮冻结原空状态；不开始构建。");
                m_Token = token;
                m_Files = callbacks.temporaryFiles.Select(PhysicalPath).Distinct(StringComparer.Ordinal).ToDictionary(path => path,
                    path => File.Exists(path) ? File.ReadAllBytes(path) : null, StringComparer.Ordinal);
                m_OriginalDirectories = new[] { "Assets/Resources", "Library/InputSystem", "Library/com.unity.services.core" }
                    .Select(PhysicalPath).Where(Directory.Exists).ToArray();
                m_Preloaded = PlayerSettings.GetPreloadedAssets();
                EditorBuildSettings.TryGetConfigObject("com.unity.input.settings.actions", out m_Actions);
                m_ProjectWide = m_Actions?.GetType().GetField("m_IsProjectWide", BindingFlags.NonPublic | BindingFlags.Instance);
                m_ProjectWideBefore = m_ProjectWide?.GetValue(m_Actions);
                m_ActionsDirty = m_Actions != null && EditorUtility.IsDirty(m_Actions);
                m_ActionsHash = m_Actions == null ? null : HashObject(m_Actions);
                m_PerformanceCleanupExisted = EditorPrefs.HasKey("PT_ResourcesCleanup");
                m_PerformanceCleanup = EditorPrefs.GetBool("PT_ResourcesCleanup", false);
                m_Il2CppEnvironment = Environment.GetEnvironmentVariable("UNITY_IL2CPP_PATH");
                m_ClEnvironment = Environment.GetEnvironmentVariable("_CL_");
                m_NativeSettings = Resources.FindObjectsOfTypeAll(typeof(PlayerSettings)).Single();
                m_NativeSettingsDirty = EditorUtility.IsDirty(m_NativeSettings);
                if (callbacks.inputs.Contains("SRPBuiltinState=core:null|instance:null|probe:0", StringComparer.Ordinal))
                {
                    const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
                    Type coreData = FindType("UnityEditor.Rendering.CoreBuildData");
                    m_SrpCoreSlot = FindType("UnityEditor.Rendering.CorePreprocessBuild").GetField("m_BuildData", flags);
                    m_SrpCoreInstance = coreData.GetField("m_Instance", flags);
                    m_SrpProcessed = FindType("UnityEditor.Rendering.ProbeVolumeBuildProcessor").GetField("s_BakingSetsProcessedLastBuild", flags);
                    m_SrpProcessedCount = m_SrpProcessed.FieldType.GetProperty("Count");
                    object processed = m_SrpProcessed.GetValue(null);
                    if (!TryValidateSrpBuiltinState(0, m_SrpCoreSlot.GetValue(null) == null, m_SrpCoreInstance.GetValue(null) == null,
                            processed == null ? -1 : (int)m_SrpProcessedCount.GetValue(processed), out string error))
                        throw new InvalidOperationException(error);
                    m_SrpApvBaseline = Util.Json.Serialize(CapturePaths(new[] { "Library/TempAPVStreamingAssets" }, token));
                }
            }

            /// <summary>记录正式回调链已返回；仍由 Dispose 核对实际恢复。</summary>
            /// <param name="report">本轮官方 BuildReport，仅复制实际 usage payload 输入，不重发事件。</param>
            public void Complete(BuildReport report)
            {
                ChainReturned = true;
                try { BuiltinUsageReport = PlayerBuiltinUsageBuildContract.CaptureReport(m_BuiltinUsageBefore, report); }
                catch (Exception error) { BuiltinUsageError = error.Message; throw; }
                try { BuildEventsReport = PlayerBuildEventsContract.CaptureReport(m_BuildEventsBefore, report); }
                catch (Exception error) { BuildEventsError = error.Message; throw; }
                try { EmptyUsageReport = PlayerEmptyUsageBuildContract.CaptureReport(m_EmptyUsageBefore, report); }
                catch (Exception error) { EmptyUsageError = error.Message; throw; }
                try { FiniteAdditionalReport = PlayerFiniteAdditionalBuildContract.CaptureReport(m_FiniteAdditionalBefore, report); }
                catch (Exception error) { FiniteAdditionalError = error.Message; throw; }
                try { PackageCallbacksReport = PlayerPackageCallbacksContract.CaptureReport(m_PackageCallbacksBefore, report); }
                catch (Exception error) { PackageCallbacksError = error.Message; throw; }
                m_Completed = true;
            }

            /// <summary>仅恢复本轮准确临时写集，保留正式输出与用户其他资产；Generate 保留独立合同约束的官方环境转换。</summary>
            /// <returns>无返回值；无法证明准确恢复时保留实际状态并抛出异常。</returns>
            public void Dispose()
            {
                try { NormalResetAfter = CaptureNormalResetStates(m_CallbackInputs, CancellationToken.None); }
                catch (Exception error) { NormalResetError = error.Message; }
                try { TmpFontAfter = PlayerTmpFontBuildContract.CaptureObserved(m_CallbackInputs, m_TmpFontBefore, null, false, CancellationToken.None); }
                catch (Exception error) { TmpFontError = error.Message; }
                try { FacebookAfter = PlayerFacebookBuildContract.Capture(m_CallbackInputs, CancellationToken.None); }
                catch (Exception error) { FacebookError = error.Message; }
                try { BuiltinUsageAfter = PlayerBuiltinUsageBuildContract.Capture(m_CallbackInputs); }
                catch (Exception error) { BuiltinUsageError = error.Message; }
                try { BuildEventsAfter = PlayerBuildEventsContract.Capture(m_CallbackInputs); }
                catch (Exception error) { BuildEventsError = error.Message; }
                try { EmptyUsageAfter = PlayerEmptyUsageBuildContract.Capture(m_CallbackInputs); }
                catch (Exception error) { EmptyUsageError = error.Message; }
                try { FiniteAdditionalAfter = PlayerFiniteAdditionalBuildContract.Capture(m_CallbackInputs); }
                catch (Exception error) { FiniteAdditionalError = error.Message; }
                try { PackageCallbacksAfter = PlayerPackageCallbacksContract.Capture(m_CallbackInputs, m_PackageDependencyPaths); }
                catch (Exception error) { PackageCallbacksError = error.Message; }
                PlayerSettings.SetPreloadedAssets(m_Preloaded);
                if (m_RestoreEnvironment)
                {
                    Environment.SetEnvironmentVariable("UNITY_IL2CPP_PATH", m_Il2CppEnvironment);
                    Environment.SetEnvironmentVariable("_CL_", m_ClEnvironment);
                }
                if (m_ProjectWide != null) m_ProjectWide.SetValue(m_Actions, m_ProjectWideBefore);
                bool refresh = false;
                foreach (KeyValuePair<string, byte[]> item in m_Files)
                {
                    byte[] current = File.Exists(item.Key) ? File.ReadAllBytes(item.Key) : null;
                    if (item.Value != null)
                    {
                        if (current == null || !item.Value.SequenceEqual(current))
                        { Directory.CreateDirectory(Path.GetDirectoryName(item.Key)); File.WriteAllBytes(item.Key, item.Value); refresh = true; }
                    }
                    else if (current != null) { File.Delete(item.Key); refresh = true; }
                }
                foreach (string directory in new[] { "Assets/Resources", "Library/InputSystem", "Library/com.unity.services.core" }.Select(PhysicalPath))
                    if (!m_OriginalDirectories.Contains(directory, StringComparer.Ordinal) && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                        if (File.Exists(directory + ".meta")) File.Delete(directory + ".meta");
                        refresh = true;
                    }
                if (m_PerformanceCleanupExisted) EditorPrefs.SetBool("PT_ResourcesCleanup", m_PerformanceCleanup);
                else EditorPrefs.DeleteKey("PT_ResourcesCleanup");
                if (m_NativeSettingsDirty) EditorUtility.SetDirty(m_NativeSettings);
                else EditorUtility.ClearDirty(m_NativeSettings);
                if (refresh) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (m_Actions != null)
                {
                    if (HashObject(m_Actions) != m_ActionsHash)
                        throw new InvalidOperationException("InputActionAsset 正文状态没有恢复；不覆盖用户其它内存修改。");
                    if (m_ActionsDirty) EditorUtility.SetDirty(m_Actions); else EditorUtility.ClearDirty(m_Actions);
                }
                string burstOutput = PhysicalPath("Temp/BurstOutput");
                if (Directory.Exists(burstOutput)) Directory.Delete(burstOutput, true);
                foreach (KeyValuePair<string, byte[]> item in m_Files)
                    if (item.Value == null ? File.Exists(item.Key) : !File.Exists(item.Key) || !item.Value.SequenceEqual(File.ReadAllBytes(item.Key)))
                        throw new InvalidOperationException("Player 回调临时文件未恢复：" + item.Key);
                if (!PlayerSettings.GetPreloadedAssets().SequenceEqual(m_Preloaded) || m_ProjectWide != null && !Equals(m_ProjectWide.GetValue(m_Actions), m_ProjectWideBefore))
                    throw new InvalidOperationException("Input 回调预加载列表或 ProjectWide 标志未恢复。");
                if (m_QualityBuiltin != null && !TryRestoreQualityBuiltinState(m_QualityBuiltin,
                    m_QualityBuiltin.assetsField.GetValue(null) as List<UnityEngine.Rendering.RenderPipelineAsset>,
                    m_QualityBuiltin.typesField.GetValue(null) as HashSet<Type>, out string qualityError))
                    throw new InvalidOperationException(qualityError);
                // SRP 检查放在其他已授权临时恢复之后；同步构建中无法唯一记录缓存创建者，不猜归属清理残留对象。
                if (m_SrpCoreSlot != null)
                {
                    var errors = new List<string>();
                    object processed = m_SrpProcessed.GetValue(null);
                    if (!TryValidateSrpBuiltinState(0, m_SrpCoreSlot.GetValue(null) == null, m_SrpCoreInstance.GetValue(null) == null,
                            processed == null ? -1 : (int)m_SrpProcessedCount.GetValue(processed), out string error))
                        errors.Add(error);
                    string actual = Util.Json.Serialize(CapturePaths(new[] { "Library/TempAPVStreamingAssets" }, CancellationToken.None));
                    if (actual != m_SrpApvBaseline) errors.Add("Builtin Probe 改变了原 APV 临时目录；保留真实残留，不清旧数据或宣称恢复。");
                    if (errors.Count != 0) throw new InvalidOperationException(string.Join("；", errors));
                }
                if (FiniteAdditionalError != null) throw new InvalidOperationException(FiniteAdditionalError);
                if (m_Completed && !PlayerFiniteAdditionalBuildContract.TryValidateAfter(m_FiniteAdditionalBefore, FiniteAdditionalAfter, FiniteAdditionalAfter, out string finiteError))
                {
                    FiniteAdditionalError = finiteError;
                    throw new InvalidOperationException(finiteError);
                }
                if (PackageCallbacksError != null) throw new InvalidOperationException(PackageCallbacksError);
                if (m_Completed && (!PlayerPackageCallbacksContract.TryValidateAfter(m_PackageCallbacksBefore, PackageCallbacksAfter, PackageCallbacksAfter, out string packageError) ||
                    !PlayerPackageCallbacksContract.TryValidateReport(m_PackageCallbacksBefore, PackageCallbacksAfter, PackageCallbacksReport, PackageCallbacksReport?.guid, out packageError)))
                {
                    PackageCallbacksError = packageError;
                    throw new InvalidOperationException(packageError);
                }
                if (EmptyUsageError != null) throw new InvalidOperationException(EmptyUsageError);
                if (m_Completed && !PlayerEmptyUsageBuildContract.TryValidateAfter(m_EmptyUsageBefore, EmptyUsageAfter, EmptyUsageAfter, out string emptyUsageError))
                {
                    EmptyUsageError = emptyUsageError;
                    throw new InvalidOperationException(emptyUsageError);
                }
                if (BuildEventsError != null) throw new InvalidOperationException(BuildEventsError);
                if (m_Completed && !PlayerBuildEventsContract.TryValidateAfter(m_BuildEventsBefore, BuildEventsAfter, BuildEventsAfter, out string buildEventsError))
                {
                    BuildEventsError = buildEventsError;
                    throw new InvalidOperationException(buildEventsError);
                }
                if (BuiltinUsageError != null) throw new InvalidOperationException(BuiltinUsageError);
                if (m_Completed && !PlayerBuiltinUsageBuildContract.TryValidateAfter(m_BuiltinUsageBefore, BuiltinUsageAfter, BuiltinUsageAfter, out string usageError))
                {
                    BuiltinUsageError = usageError;
                    throw new InvalidOperationException(usageError);
                }
                if (FacebookError != null) throw new InvalidOperationException(FacebookError);
                if (m_Completed && !PlayerFacebookBuildContract.TryValidateAfter(m_FacebookBefore, FacebookAfter, FacebookAfter, out string facebookError))
                {
                    FacebookError = facebookError;
                    throw new InvalidOperationException(facebookError);
                }
                if (TmpFontError != null) throw new InvalidOperationException(TmpFontError);
                if (m_Completed && !PlayerTmpFontBuildContract.TryValidateAfter(m_TmpFontBefore, TmpFontAfter, TmpFontAfter, out string tmpError))
                {
                    TmpFontError = tmpError;
                    throw new InvalidOperationException(tmpError);
                }
                if (NormalResetError != null) throw new InvalidOperationException(NormalResetError);
                if (m_Completed && !TryValidateNormalResetReceipt(m_NormalResetBefore, NormalResetAfter, NormalResetAfter, out string resetError))
                {
                    NormalResetError = resetError;
                    throw new InvalidOperationException(resetError);
                }
                if (m_Completed) m_Token.ThrowIfCancellationRequested();
            }
        }

        /// <summary>输出、Unity 缓存和本轮实际 Resources 目标逐路径展示。</summary>
        private static string[] GetWriteSet(PlayerInputSnapshot inputs, string outputPath, string buildMode)
        {
            var result = new System.Collections.Generic.List<string>
            {
                inputs.stagingTarget,
                inputs.stagingTarget + ".meta",
                YooAssetRuntimeSettingsStaging.GetMarkerFilePath(),
                "Library/Bee (Unity Player build cache)",
                "Library/BuildPlayerData (Unity Player build cache)",
                "Library/PlayerDataCache (Unity Player build cache)",
                "Temp/StagingArea (Unity Player staging)",
                "EditorUserBuildSettings.development (temporary, restored in finally)",
            };
            result.AddRange(inputs.outputRoots);
            if (!inputs.resourcesDirectoryExisted) result.Add(inputs.resourcesDirectory + " + .meta (temporary directory)");
            string parent = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
            {
                result.Add(parent + " (create output parent)");
            }
            if (buildMode == "clean-build")
            {
                result.Add("Unity Player build cache (clean/rebuild)");
            }
            return result.ToArray();
        }
    }
}
