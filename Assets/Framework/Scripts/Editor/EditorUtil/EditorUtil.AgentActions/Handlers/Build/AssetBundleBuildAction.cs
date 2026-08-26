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
                  AgentActionEffect.BuildArtifact |
                  AgentActionEffect.Destructive,
        RequiredEvidence = AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        ReloadSemantics = AgentActionReloadSemantics.ReloadNotExpected,
        Locks = new[] { "unity-editor", "yooasset-build", "sbp-build-cache", "Bundles", "Assets/StreamingAssets" })]
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
            public ECompressOption Compression;
            public EFileNameStyle FileNameStyle;
            public EBundledCopyOption BundledCopyOption;
            public string BundledCopyParams;
            public BuildActionCommon.BundleInputSnapshot Inputs;
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
            public string compression;
            public string fileNameStyle;
            public string bundledCopyOption;
            public string bundledCopyParams;
            public BuildActionCommon.BundleInputSnapshot inputs;
        }

        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!BuildActionCommon.TryResolveActiveTarget(request.target, out _, out error) ||
                !BuildActionCommon.TryValidateName("packageName", request.packageName, out error) ||
                !BuildActionCommon.TryResolveEncryptionPolicy(request.encryptionPolicy, out error) ||
                !BuildActionCommon.TryResolveCompression(request.compression, out _, out error) ||
                !BuildActionCommon.TryResolveFileNameStyle(request.fileNameStyle, out _, out error) ||
                !BuildActionCommon.TryResolveBundledCopyOption(request.bundledCopyOption, out EBundledCopyOption copyOption, out error))
            {
                return false;
            }
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

        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            BuildActionCommon.TryResolveActiveTarget(request.target, out BuildTarget target, out _);
            BuildActionCommon.TryResolveCompression(request.compression, out ECompressOption compression, out _);
            BuildActionCommon.TryResolveFileNameStyle(request.fileNameStyle, out EFileNameStyle fileNameStyle, out _);
            BuildActionCommon.TryResolveBundledCopyOption(request.bundledCopyOption, out EBundledCopyOption copyOption, out _);
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
                Compression = compression,
                FileNameStyle = fileNameStyle,
                BundledCopyOption = copyOption,
                BundledCopyParams = request.bundledCopyParams ?? string.Empty,
                Inputs = inputs,
            };
            var recovery = new BuildActionCommon.ArtifactReceipt
            {
                target = target.ToString(),
                outputPath = outputPath,
                outputKind = "directory",
                manifestPath = manifestPath,
                packageName = request.packageName,
                packageVersion = version,
                bundledOutputPath = copyOption == EBundledCopyOption.None
                    ? null
                    : BuildActionCommon.ResolveBundledPackageRoot(request.packageName),
            };
            var writeSet = new System.Collections.Generic.List<string> { outputPath };
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
                    writeSet.Add(BuildActionCommon.ResolveStreamingAssetsRoot() + " (clear before bundled copy)");
                }
            }

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
                    encryptionPolicy = BuildActionCommon.NoEncryptionPolicy,
                    compression = request.compression,
                    fileNameStyle = request.fileNameStyle,
                    bundledCopyOption = request.bundledCopyOption,
                    bundledCopyParams = request.bundledCopyParams ?? string.Empty,
                    inputs = inputs,
                }),
                WriteSet = writeSet.ToArray(),
                Evidence = new[]
                {
                    "已冻结激活 ConfigMaster 当前坐标、YooAssetSettings、BundleCollectorSetting、目标 Package 与配置 Hash。",
                    copyOption == EBundledCopyOption.None
                        ? "构建后只读核对输出目录、YooAsset manifest 与 SHA-256。"
                        : "构建后额外核对 BundledCopy 目标 Package 目录与 SHA-256。",
                },
                RecoveryPayloadJson = Util.Json.Serialize(recovery),
            });
        }

        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State plan))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "AssetBundle Action 的冻结计划状态无效。"));
            }
            if (EditorUserBuildSettings.activeBuildTarget != plan.Target)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "activeBuildTarget 已变化；不会切换平台或执行旧计划。"));
            }
            if (!BuildActionCommon.TryCaptureBundleInputs(
                    plan.PackageName, context.CancellationToken,
                    out BuildActionCommon.BundleInputSnapshot currentInputs, out string inputError) ||
                !BuildActionCommon.BundleInputsEqual(plan.Inputs, currentInputs))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "激活 ConfigMaster、YooAsset、Collector 或 Package 输入已漂移，请重新 Plan：" + inputError));
            }
            if (!BuildActionCommon.TryValidateActiveBundleRuntime(plan.Inputs, plan.Version, out string runtimeError))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", runtimeError));
            }
            if (!plan.ClearBuildCache && Directory.Exists(plan.OutputPath))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "目标版本目录在确认后出现；不会覆盖或自动重计划。"));
            }

            YooAsset.Editor.BuildResult result = EditorUtil.BundleBuilder.BuildAssetBundle(new AssetBundleBuildArgs
            {
                Target = plan.Target,
                PackageName = plan.PackageName,
                BuildVersion = plan.Version,
                ClearBuildCache = plan.ClearBuildCache,
                UseAssetDependencyDB = plan.UseAssetDependencyDB,
                BundleEncryptorClassName = typeof(EncryptionNone).FullName,
                ManifestEncryptorClassName = typeof(ManifestEncryptorNone).FullName,
                ManifestDecryptorClassName = typeof(ManifestDecryptorNone).FullName,
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
            BuildActionCommon.ArtifactReceipt receipt = BuildActionCommon.CaptureArtifact(
                new BuildActionCommon.ArtifactReceipt
                {
                    target = plan.Target.ToString(),
                    outputPath = plan.OutputPath,
                    manifestPath = plan.ManifestPath,
                    packageName = plan.PackageName,
                    packageVersion = plan.Version,
                    bundledOutputPath = plan.BundledCopyOption == EBundledCopyOption.None
                        ? null
                        : BuildActionCommon.ResolveBundledPackageRoot(plan.PackageName),
                },
                "Succeeded",
                context.CancellationToken);
            AgentActionResult actionResult = AgentActionResult.Create(null, "success", "AssetBundle 构建完成，产物与 manifest 已生成。");
            actionResult.ReceiptJson = Util.Json.Serialize(receipt);
            actionResult.DataJson = actionResult.ReceiptJson;
            actionResult.EvidenceKinds = AgentActionEvidence.Artifact;
            actionResult.Artifacts.Add(plan.OutputPath);
            actionResult.Artifacts.Add(plan.ManifestPath);
            if (!string.IsNullOrWhiteSpace(receipt.bundledOutputPath))
            {
                actionResult.Artifacts.Add(receipt.bundledOutputPath);
            }
            actionResult.Evidence.Add("已核对输出目录、manifest 与 SHA-256；不代表运行时加载验证。" );
            return Task.FromResult(actionResult);
        }

        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            return Task.FromResult(BuildActionCommon.VerifyArtifact(receiptJson, "AssetBundle", context.CancellationToken));
        }
    }
}
