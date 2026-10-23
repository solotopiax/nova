/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetBundleBuildAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   标准 YooAsset AssetBundle 构建 Project Action
 ***************************************************************/

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using YooAsset;
using YooAsset.Editor;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.bundle.build-asset",
        "构建 YooAsset AssetBundle",
        "bundle",
        AgentActionOperationType.Build,
        Description = "按已冻结的 YooAsset Package、平台、版本与缓存参数构建 AssetBundle，并核验构建产物。",
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
        Locks = new[] { "unity-editor", "asset-database", "yooasset-build", "sbp-build-cache", "Library/PlayerScriptAssemblies", "Library/AssetDependencyDB", "Bundles", "Assets/StreamingAssets" })]
    internal sealed class AssetBundleBuildAction : AgentActionHandler<AssetBundleBuildAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string target;
            [AgentActionRequired] public string packageName;
            public string buildVersion;
            public bool clearBuildCache;
            public bool useAssetDependencyDB;
            [AgentActionRequired] public string encryptionPolicy;
            public string manifestEncryptionPolicy;
            [AgentActionRequired] public string compression;
            [AgentActionRequired] public string fileNameStyle;
            [AgentActionRequired] public string bundledCopyOption;
            public string bundledCopyParams;
        }

        private sealed class State
        {
            public BuildTarget Target;
            public string PackageName;
            public string Version;
            public string OutputPath;
            public string ManifestPath;
            public bool ClearBuildCache;
            public bool UseAssetDependencyDB;
            public string EncryptionPolicy;
            public string ManifestEncryptionPolicy;
            public string BundleEncryptorClassName;
            public string ManifestEncryptorClassName;
            public string ManifestDecryptorClassName;
            public ECompressOption Compression;
            public EFileNameStyle FileNameStyle;
            public EBundledCopyOption BundledCopyOption;
            public string BundledCopyParams;
            public BuildActionCommon.BundleInputSnapshot Inputs;
            public BuildActionCommon.BundleWriteSnapshot Writes;
        }

        [Serializable]
        private sealed class PlanView
        {
            public string target;
            public string packageName;
            public string buildVersion;
            public string outputPath;
            public string manifestPath;
            public bool clearBuildCache;
            public bool useAssetDependencyDB;
            public string encryptionPolicy;
            public string manifestEncryptionPolicy;
            public string bundleEncryptorClassName;
            public string manifestEncryptorClassName;
            public string manifestDecryptorClassName;
            public string compression;
            public string fileNameStyle;
            public string bundledCopyOption;
            public string bundledCopyParams;
            public BuildActionCommon.BundleInputSnapshot inputs;
            public BuildActionCommon.BundleWriteSnapshot sharedWrites;
        }

        /// <summary>
        /// 校验活动平台、包名与固定构建选项；加密参数只接受声明的逻辑值，不接受类型名。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!BuildActionCommon.TryResolveActiveTarget(request.target, out BuildTarget target, out error) ||
                !BuildActionCommon.TryValidateName("packageName", request.packageName, out error) ||
                !BuildActionCommon.TryResolveEncryptionPolicy(request.encryptionPolicy, out _, out error) ||
                !BuildActionCommon.TryResolveManifestEncryptionPolicy(request.manifestEncryptionPolicy, out _, out _, out _, out error) ||
                !BuildActionCommon.TryResolveCompression(request.compression, out _, out error) ||
                !BuildActionCommon.TryResolveFileNameStyle(request.fileNameStyle, out _, out error) ||
                !BuildActionCommon.TryResolveBundledCopyOption(request.bundledCopyOption, out EBundledCopyOption copyOption, out error))
            {
                return false;
            }
            if (target == BuildTarget.WebGL &&
                !EditorUtil.BundleBuilder.IsWebGLBundledCopyOptionSupported(copyOption))
            {
                error = "WebGL 的 bundledCopyOption 仅允许 none、clear-and-copy-by-tags 或 clear-and-copy-all。";
                return false;
            }
            EditorUtil.BundleBuilder.ResolveBundledCopy(
                target,
                copyOption,
                request.bundledCopyParams,
                out copyOption,
                out _);
            if (!string.IsNullOrWhiteSpace(request.buildVersion) &&
                !BuildActionCommon.TryValidateName("buildVersion", request.buildVersion, out error))
            {
                return false;
            }
            if (BuildActionCommon.RequiresTags(copyOption) && string.IsNullOrWhiteSpace(request.bundledCopyParams))
            {
                error = "按标签拷贝时 bundledCopyParams 不能为空。";
                return false;
            }
            if ((request.bundledCopyParams?.Length ?? 0) > 2048)
            {
                error = "bundledCopyParams 不能超过 2048 个字符。";
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>
        /// 只读冻结构建输入、加密逻辑值及正式类型名，供一次性确认后的执行使用。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            BuildActionCommon.TryResolveActiveTarget(request.target, out BuildTarget target, out _);
            if (!BuildActionCommon.TryResolveEncryptionPolicy(request.encryptionPolicy, out string bundleEncryptorClassName, out string encryptionError) ||
                !BuildActionCommon.TryResolveManifestEncryptionPolicy(request.manifestEncryptionPolicy,
                    out string manifestEncryptionPolicy, out string manifestEncryptorClassName, out string manifestDecryptorClassName, out encryptionError))
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = encryptionError });
            }
            BuildActionCommon.TryResolveCompression(request.compression, out ECompressOption compression, out _);
            BuildActionCommon.TryResolveFileNameStyle(request.fileNameStyle, out EFileNameStyle fileNameStyle, out _);
            BuildActionCommon.TryResolveBundledCopyOption(request.bundledCopyOption, out EBundledCopyOption copyOption, out _);
            EditorUtil.BundleBuilder.ResolveBundledCopy(
                target,
                copyOption,
                request.bundledCopyParams,
                out copyOption,
                out string bundledCopyParams);
            string version = string.IsNullOrWhiteSpace(request.buildVersion)
                ? EditorUtil.BundleBuilder.GetDefaultPackageVersion()
                : request.buildVersion;
            string outputPath = BuildActionCommon.ResolveBundleOutput(target, request.packageName, version);
            string packageRoot = Directory.GetParent(outputPath)?.FullName ?? outputPath;

            if (!BuildActionCommon.TryCaptureBundleInputs(
                    request.packageName, context.CancellationToken,
                    out BuildActionCommon.BundleInputSnapshot inputs, out string inputError))
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = inputError });
            }
            string manifestPath = BuildActionCommon.ResolveBundleManifest(
                outputPath, request.packageName, version, inputs.packageFilePrefix);
            if (!BuildActionCommon.TryCaptureBundleWrites(target, request.packageName, version, false, request.useAssetDependencyDB, inputs,
                    context.CancellationToken, out BuildActionCommon.BundleWriteSnapshot writes, out string writeError))
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "blocked", Summary = writeError,
                    DataJson = Util.Json.Serialize(new { inputs, sharedWrites = writes }),
                });
            }

            if (!request.clearBuildCache && Directory.Exists(outputPath))
            {
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "blocked",
                    Summary = "目标版本目录已存在，且 clearBuildCache=false；YooAsset 会拒绝覆盖。",
                    DataJson = Util.Json.Serialize(new { target = target.ToString(), buildVersion = version, outputPath }),
                    Evidence = new[] { "Plan 只读发现既有目标版本目录。" },
                });
            }

            var state = new State
            {
                Target = target,
                PackageName = request.packageName,
                Version = version,
                OutputPath = outputPath,
                ManifestPath = manifestPath,
                ClearBuildCache = request.clearBuildCache,
                UseAssetDependencyDB = request.useAssetDependencyDB,
                EncryptionPolicy = request.encryptionPolicy,
                ManifestEncryptionPolicy = manifestEncryptionPolicy,
                BundleEncryptorClassName = bundleEncryptorClassName,
                ManifestEncryptorClassName = manifestEncryptorClassName,
                ManifestDecryptorClassName = manifestDecryptorClassName,
                Compression = compression,
                FileNameStyle = fileNameStyle,
                BundledCopyOption = copyOption,
                BundledCopyParams = bundledCopyParams,
                Inputs = inputs,
                Writes = writes,
            };
            var recovery = new BuildActionCommon.ArtifactReceipt
            {
                target = target.ToString(),
                outputPath = outputPath,
                outputKind = "directory",
                manifestPath = manifestPath,
                packageName = request.packageName,
                packageVersion = version,
                bundleWrites = writes,
                bundleInputs = inputs,
                bundledOutputPath = copyOption == EBundledCopyOption.None
                    ? null
                    : BuildActionCommon.ResolveBundledPackageRoot(request.packageName),
            };
            var writeSet = new System.Collections.Generic.List<string> { outputPath };
            writeSet.AddRange(BuildActionCommon.GetBundleSharedWriteSet(writes));
            if (request.clearBuildCache)
            {
                writeSet.Add(packageRoot + " (delete/recreate package root)");
                writeSet.Add("Unity SBP BuildCache (global purge)");
            }
            if (copyOption != EBundledCopyOption.None)
            {
                writeSet.Add(BuildActionCommon.ResolveBundledPackageRoot(request.packageName));
                if (copyOption == EBundledCopyOption.ClearAndCopyAll ||
                    copyOption == EBundledCopyOption.ClearAndCopyByTags)
                {
                    writeSet.Add(BuildActionCommon.ResolveBundledPackageRoot(request.packageName) + " (clear before bundled copy)");
                }
            }
            else if (target == BuildTarget.WebGL)
            {
                writeSet.Add(BuildActionCommon.ResolveBundledPackageRoot(request.packageName) + " (delete stale WebGL bundled package root)");
            }

            if (!BuildArtifactReceiptCodec.TryEncode(recovery, out string recoveryPayload, out string recoveryError))
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "完整构建恢复回执无法安全保存：" + recoveryError });

            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将为活动平台 {target} 构建 AssetBundle：{request.packageName}@{version}。",
                State = state,
                DataJson = Util.Json.Serialize(new PlanView
                {
                    target = target.ToString(),
                    packageName = request.packageName,
                    buildVersion = version,
                    outputPath = outputPath,
                    manifestPath = manifestPath,
                    clearBuildCache = request.clearBuildCache,
                    useAssetDependencyDB = request.useAssetDependencyDB,
                    encryptionPolicy = state.EncryptionPolicy,
                    manifestEncryptionPolicy = state.ManifestEncryptionPolicy,
                    bundleEncryptorClassName = state.BundleEncryptorClassName,
                    manifestEncryptorClassName = state.ManifestEncryptorClassName,
                    manifestDecryptorClassName = state.ManifestDecryptorClassName,
                    compression = request.compression,
                    fileNameStyle = request.fileNameStyle,
                    bundledCopyOption = request.bundledCopyOption,
                    bundledCopyParams = bundledCopyParams,
                    inputs = inputs,
                    sharedWrites = writes,
                }),
                WriteSet = writeSet.ToArray(),
                Evidence = new[]
                {
                    "已冻结激活 ConfigMaster 当前坐标、YooAssetSettings、BundleCollectorSetting、目标 Package 与配置 Hash。",
                    "已冻结共享 OutputCache、脚本/临时输出、可选依赖数据库及实际回调编译身份；共享缓存可增写或后台清理。",
                    "已冻结 Bundle 与清单加密逻辑值、正式类型名及清单解密配对；不会接受任意 C# 类型名。",
                    copyOption == EBundledCopyOption.None
                        ? "构建后只读核对输出目录、YooAsset manifest 与 SHA-256。"
                        : "构建后额外核对首包目标 Package 目录与 SHA-256。",
                },
                RecoveryPayloadJson = recoveryPayload,
            });
        }

        /// <summary>
        /// 复核冻结输入后只构建一次，再只读等待正规 Editor update 完成已审 EDM 工作；域变化或未知工作保留 partial。
        /// </summary>
        /// <param name="state">已确认且尚未漂移的正式构建计划。</param>
        /// <param name="context">执行令牌、项目上下文及本次回执归属。</param>
        /// <returns>本次构建的成功或 partial 结果，不自动重复执行。</returns>
        public override async Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State plan))
            {
                return AgentActionResult.Create(null, "blocked", "AssetBundle Action 的冻结计划状态无效。");
            }
            if (EditorUserBuildSettings.activeBuildTarget != plan.Target)
            {
                return AgentActionResult.Create(null, "blocked", "activeBuildTarget 已变化；不会切换平台或执行旧计划。");
            }
            if (!BuildActionCommon.TryCaptureBundleInputs(
                    plan.PackageName, context.CancellationToken,
                    out BuildActionCommon.BundleInputSnapshot currentInputs, out string inputError) ||
                !BuildActionCommon.BundleInputsEqual(plan.Inputs, currentInputs))
            {
                return AgentActionResult.Create(null, "blocked", "激活 ConfigMaster、YooAsset、Collector 或 Package 输入已漂移，请重新 Plan：" + inputError);
            }
            if (!BuildActionCommon.TryValidateActiveBundleRuntime(plan.Inputs, plan.Version, out string runtimeError))
            {
                return AgentActionResult.Create(null, "blocked", runtimeError);
            }
            if (!BuildActionCommon.TryCaptureBundleWrites(plan.Target, plan.PackageName, plan.Version, false, plan.UseAssetDependencyDB, currentInputs,
                    context.CancellationToken, out BuildActionCommon.BundleWriteSnapshot currentWrites, out string writeError) ||
                !BuildActionCommon.BundleWritesEqual(plan.Writes, currentWrites))
                return AgentActionResult.Create(null, "blocked", "共享路径、回调或未保存资产状态已漂移，请重新 Plan：" + writeError);
            if (!plan.ClearBuildCache && Directory.Exists(plan.OutputPath))
            {
                return AgentActionResult.Create(null, "blocked", "目标版本目录在确认后出现；不会覆盖或自动重计划。");
            }

            using (BuildActionCommon.CallbackExecutionScope callbackScope = BuildActionCommon.BeginCallbackExecution(plan.Writes.callbacks))
            try
            {
                YooAsset.Editor.BuildResult result = EditorUtil.BundleBuilder.BuildAssetBundle(new AssetBundleBuildArgs
                {
                    Target = plan.Target,
                    PackageName = plan.PackageName,
                    BuildVersion = plan.Version,
                    ClearBuildCache = plan.ClearBuildCache,
                    UseAssetDependencyDB = plan.UseAssetDependencyDB,
                    BundleEncryptorClassName = plan.BundleEncryptorClassName,
                    ManifestEncryptorClassName = plan.ManifestEncryptorClassName,
                    ManifestDecryptorClassName = plan.ManifestDecryptorClassName,
                    Compression = plan.Compression,
                    FileNameStyle = plan.FileNameStyle,
                    BundledCopyOption = plan.BundledCopyOption,
                    BundledCopyParams = plan.BundledCopyParams,
                });
                string actualOutput = Path.GetFullPath(result.OutputPackageDirectory);
                if (!string.Equals(actualOutput, plan.OutputPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"领域构建输出与冻结计划不一致：expected={plan.OutputPath}, actual={actualOutput}。");
                }
                BuildActionCommon.CallbackExecutionReceipt callbackExecution = await callbackScope.CompleteAsync(plan.OutputPath, plan.PackageName, plan.Version, plan.Writes, context.CancellationToken);
                if (callbackExecution != null && !callbackExecution.complete) throw new InvalidOperationException("正常构建回调未满足生成/标签合同：" + callbackExecution.error);
                BuildActionCommon.ArtifactReceipt receipt = BuildActionCommon.CaptureArtifact(
                    new BuildActionCommon.ArtifactReceipt
                    {
                        target = plan.Target.ToString(),
                        outputPath = plan.OutputPath,
                        manifestPath = plan.ManifestPath,
                        packageName = plan.PackageName,
                        packageVersion = plan.Version,
                        bundleWrites = plan.Writes,
                        bundleInputs = plan.Inputs,
                        callbackExecution = callbackExecution,
                        bundledOutputPath = plan.BundledCopyOption == EBundledCopyOption.None
                            ? null
                            : BuildActionCommon.ResolveBundledPackageRoot(plan.PackageName),
                    },
                    "Succeeded",
                    context.CancellationToken);
                AgentActionResult actionResult = AgentActionResult.Create(null, "success", "AssetBundle 构建完成，产物与 manifest 已生成。");
                actionResult.ReceiptJson = BuildArtifactReceiptCodec.Encode(receipt);
                actionResult.DataJson = Util.Json.Serialize(receipt);
                actionResult.EvidenceKinds = AgentActionEvidence.Artifact;
                actionResult.Artifacts.Add(plan.OutputPath);
                actionResult.Artifacts.Add(plan.ManifestPath);
                if (!string.IsNullOrWhiteSpace(receipt.bundledOutputPath))
                {
                    actionResult.Artifacts.Add(receipt.bundledOutputPath);
                }
                actionResult.Evidence.Add("已核对输出目录、manifest 与 SHA-256；不代表运行时加载验证。" );
                return actionResult;
            }
            catch (Exception exception)
            {
                AgentActionResult failed = AgentActionResult.Create(null, "partial", "AssetBundle 已进入构建，后续未完成：" + exception.Message);
                var recovery = new BuildActionCommon.ArtifactReceipt
                {
                    finalized = false, target = plan.Target.ToString(), outputPath = plan.OutputPath,
                    manifestPath = plan.ManifestPath, packageName = plan.PackageName, packageVersion = plan.Version,
                    bundleWrites = plan.Writes,
                    bundleInputs = plan.Inputs,
                    callbackExecution = callbackScope.Complete(CancellationToken.None),
                    bundledOutputPath = plan.BundledCopyOption == EBundledCopyOption.None ? null : BuildActionCommon.ResolveBundledPackageRoot(plan.PackageName),
                };
                BuildActionCommon.RecordPreservedSceneState(recovery, CancellationToken.None);
                // 编码失败不重复抛出；Core 保留写前完整恢复凭据，失败产物仍明确列出。
                if (BuildArtifactReceiptCodec.TryEncode(recovery, out string failedPayload, out string receiptError))
                    failed.ReceiptJson = failedPayload;
                else failed.Warnings.Add("执行后的完整恢复回执未能保存：" + receiptError);
                failed.DataJson = Util.Json.Serialize(recovery);
                failed.Artifacts.Add(plan.OutputPath);
                failed.Artifacts.AddRange(BuildActionCommon.GetBundleSharedWriteSet(plan.Writes));
                failed.Warnings.Add("保留可能已写入的输出、共享缓存与临时残留供归属核查；不会自动重跑、删除已有缓存或生成成功凭据。");
                if (recovery.preservedSceneInputsUnchanged == false) failed.Warnings.Add("执行前源资产或场景 setup 未保持：" + recovery.preservedSceneInputError);
                return failed;
            }
        }

        /// <summary>兼容旧明文与新有界压缩回执，只读核对完整产物和回调证据，不重放构建。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            return Task.FromResult(BuildActionCommon.VerifyArtifact(receiptJson, "AssetBundle", context.CancellationToken));
        }
    }
}
