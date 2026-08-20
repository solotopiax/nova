/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HotfixRefreshGameDllsAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   当前 Target 与激活坐标的业务热更 DLL 编译、整批复制 Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HybridCLR.Editor;
using HybridCLR.Editor.Installer;
using NovaFramework.Runtime;
using UnityEditor;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.hotfix.refresh-game-dlls",
        "刷新业务热更 DLL",
        "hotfix",
        AgentActionOperationType.Generate,
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite | AgentActionEffect.BuildArtifact,
        RequiredEvidence = AgentActionEvidence.Compile | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        ReloadSemantics = AgentActionReloadSemantics.ReloadNotExpected,
        Locks = new[]
        {
            "unity-editor", "asset-database", "build-settings", "active-config-master",
            "hybridclr-hot-update-output", "game-dll-targets",
        })]
    internal sealed class HotfixRefreshGameDllsAction : AgentActionHandler<HotfixRefreshGameDllsAction.Request>
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

        [Serializable]
        private sealed class Mapping
        {
            public string group;
            public string sourceLocation;
            public string targetLocation;
            public string assetLocation;
            public string sourcePath;
            public string targetPath;
            public string targetAssetPath;
        }

        [Serializable]
        private sealed class HashPair
        {
            public string sourcePath;
            public string sourceSha256;
            public string targetPath;
            public string targetSha256;
            public string targetAssetPath;
        }

        private sealed class State
        {
            public Request Request;
            public BuildTarget Target;
            public PlatformType Platform;
            public ChannelType Channel;
            public DevelopMode Mode;
            public string MasterPath;
            public string MasterHash;
            public string CompileOutput;
            public string SettingsHash;
            public Mapping[] Mappings;
        }

        [Serializable]
        private sealed class Receipt
        {
            public string masterGuid;
            public string masterPath;
            public string platform;
            public string channel;
            public string developMode;
            public string activeBuildTarget;
            public bool developmentBuild;
            public string compileOutput;
            public Mapping[] mappings;
            public HashPair[] hashPairs;
            public GenerateActionCommon.Artifact[] artifacts;
        }

        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!GenerateActionCommon.TryValidateGuid(request?.masterGuid, out error)) return false;
            if (!GenerateActionCommon.TryParseCoordinate(
                    request.platform, request.channel, request.developMode,
                    out _, out _, out _, out error)) return false;
            return GenerateActionCommon.TryParseActiveBuildTarget(request.activeBuildTarget, out _, out error);
        }

        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryBuildFrozenState(request, context, out State state, out string status, out string error))
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = status, Summary = error });
            }
            Receipt receipt = CreateReceipt(state, Array.Empty<HashPair>(), Array.Empty<GenerateActionCommon.Artifact>());
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将在 {state.Target} / DevelopmentBuild={request.developmentBuild} 编译业务热更 DLL，并整批复制 {state.Mappings.Length} 条冻结映射。",
                DataJson = Util.Json.Serialize(receipt),
                State = state,
                WriteSet = new[] { GenerateActionCommon.ToProjectRelative(state.CompileOutput) }
                    .Concat(state.Mappings.Select(mapping => GenerateActionCommon.ToProjectRelative(mapping.targetPath)))
                    .Distinct(StringComparer.Ordinal).ToArray(),
                Evidence = new[]
                {
                    $"已只读冻结 Master={state.MasterPath}，坐标={state.Platform}/{state.Channel}/{state.Mode}。",
                    $"已冻结 activeBuildTarget={state.Target}、DevelopmentBuild={request.developmentBuild} 与完整 Startup+Running 映射。",
                    "Action 只执行 CompileDllActiveBuildTarget -> CopyGameDlls，不执行 GenerateAll、AOT、Bundle 或 Player。",
                },
                RecoveryPayloadJson = Util.Json.Serialize(receipt),
            });
        }

        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen)) return Task.FromResult(AgentActionResult.Create(null, "blocked", "刷新业务 DLL 的冻结状态无效。"));
            if (!TryBuildFrozenState(frozen.Request, context, out State current, out _, out string error) || !FrozenStatesEqual(frozen, current))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "业务 DLL 上下文或完整映射已漂移，请重新 Plan：" + error));
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            EditorUtil.HybridCLR.CompileDllActiveBuildTarget();
            var sources = new Dictionary<string, GenerateActionCommon.Artifact>(StringComparer.Ordinal);
            foreach (Mapping mapping in frozen.Mappings)
            {
                if (!sources.ContainsKey(mapping.sourcePath))
                {
                    sources.Add(mapping.sourcePath, GenerateActionCommon.CaptureFile(mapping.sourcePath, context.CancellationToken));
                }
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            EditorUtil.HybridCLR.CopyGameDlls();
            if (EditorUserBuildSettings.activeBuildTarget != frozen.Target ||
                EditorUserBuildSettings.development != frozen.Request.developmentBuild)
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "业务 DLL 已编译/复制，但 Target 或 DevelopmentBuild 未保持冻结值；不会自动重放。"));
            }

            var pairs = new List<HashPair>();
            var artifacts = new List<GenerateActionCommon.Artifact>
            {
                GenerateActionCommon.CaptureDirectory(frozen.CompileOutput, "*", context.CancellationToken),
            };
            artifacts.AddRange(sources.Values);
            foreach (Mapping mapping in frozen.Mappings)
            {
                GenerateActionCommon.Artifact target = GenerateActionCommon.CaptureFile(mapping.targetPath, context.CancellationToken);
                GenerateActionCommon.Artifact source = sources[mapping.sourcePath];
                if (!string.Equals(source.sha256, target.sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(AgentActionResult.Create(null, "partial", "业务 DLL source/target SHA-256 不一致：" + mapping.targetLocation));
                }
                artifacts.Add(target);
                pairs.Add(new HashPair
                {
                    sourcePath = source.path,
                    sourceSha256 = source.sha256,
                    targetPath = target.path,
                    targetSha256 = target.sha256,
                    targetAssetPath = mapping.targetAssetPath,
                });
            }

            Receipt receipt = CreateReceipt(frozen, pairs.ToArray(),
                artifacts.GroupBy(artifact => artifact.path, StringComparer.Ordinal).Select(group => group.First()).ToArray());
            AgentActionResult result = AgentActionResult.Create(null, "partial", "业务热更 DLL 已完成 compile -> copy，等待只读 Verify 核对完整目录、映射与 SHA-256。" );
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.DataJson = result.ReceiptJson;
            result.Artifacts.AddRange(artifacts.Select(artifact => artifact.path).Distinct(StringComparer.Ordinal));
            result.Evidence.Add("Execute 使用现有领域 API 整批执行一次；失败不会自动重放或降级为手工复制。" );
            return Task.FromResult(result);
        }

        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            try { receipt = Util.Json.Deserialize<Receipt>(receiptJson); }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "业务 DLL Receipt 无法解析：" + exception.Message));
            }
            if (receipt == null || receipt.mappings == null || receipt.hashPairs == null || receipt.artifacts == null)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "业务 DLL Receipt 不完整。"));
            }
            if (!GenerateActionCommon.TryVerifyArtifacts(receipt.artifacts, context.CancellationToken, out GenerateActionCommon.Artifact[] actual, out string artifactError))
            {
                AgentActionResult partial = AgentActionResult.Create(null, "partial", "业务 DLL 产物验证未通过：" + artifactError);
                partial.DataJson = Util.Json.Serialize(actual);
                return Task.FromResult(partial);
            }
            if (receipt.hashPairs.Length != receipt.mappings.Length)
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "Receipt 未包含完整 source/target Hash 映射。"));
            }
            if (!GenerateActionCommon.TryResolveProjectPath(receipt.compileOutput, "Receipt compileOutput", out string compileOutput, out string compileError) ||
                !actual.Any(artifact => artifact.kind == "directory" && string.Equals(artifact.path, compileOutput, StringComparison.Ordinal)))
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", compileError ?? "Receipt 产物集合未覆盖完整编译输出目录。"));
            }
            Dictionary<string, GenerateActionCommon.Artifact> artifactsByPath = actual.ToDictionary(
                artifact => artifact.path, artifact => artifact, StringComparer.Ordinal);
            for (int index = 0; index < receipt.hashPairs.Length; index++)
            {
                HashPair pair = receipt.hashPairs[index];
                Mapping mapping = receipt.mappings[index];
                if (!string.Equals(pair.sourcePath, mapping.sourcePath, StringComparison.Ordinal) ||
                    !string.Equals(pair.targetPath, mapping.targetPath, StringComparison.Ordinal) ||
                    !string.Equals(pair.targetAssetPath, mapping.targetAssetPath, StringComparison.Ordinal) ||
                    !artifactsByPath.TryGetValue(pair.sourcePath, out GenerateActionCommon.Artifact sourceArtifact) ||
                    !artifactsByPath.TryGetValue(pair.targetPath, out GenerateActionCommon.Artifact targetArtifact) ||
                    !string.Equals(sourceArtifact.sha256, pair.sourceSha256, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(targetArtifact.sha256, pair.targetSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(AgentActionResult.Create(null, "partial", "Receipt 的完整映射、产物集合与 Hash 对不上：" + pair.targetPath));
                }
                if (!string.Equals(pair.sourceSha256, pair.targetSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(AgentActionResult.Create(null, "partial", "Receipt 中 source/target Hash 不一致：" + pair.targetPath));
                }
                if (!string.IsNullOrEmpty(pair.targetAssetPath) && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(pair.targetAssetPath)))
                {
                    return Task.FromResult(AgentActionResult.Create(null, "partial", "Assets 目标尚无只读可见的 AssetDatabase GUID：" + pair.targetAssetPath));
                }
            }

            AgentActionResult success = AgentActionResult.Create(null, "success", "业务 DLL 完整编译目录、source/target 映射、AssetDatabase 导入与 SHA-256 已只读核对。" );
            success.EvidenceKinds = AgentActionEvidence.Compile | AgentActionEvidence.Artifact;
            success.DataJson = Util.Json.Serialize(receipt.hashPairs);
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path));
            success.Evidence.Add("Verify 未调用 CompileDll、CopyGameDlls、ImportAsset 或任何恢复写入。" );
            success.Warnings.Add("该证据不代表 Bundle、Player、运行时加载或设备验证成功。" );
            return Task.FromResult(success);
        }

        private static bool TryBuildFrozenState(
            Request request,
            AgentActionExecutionContext context,
            out State state,
            out string status,
            out string error)
        {
            state = null;
            status = "blocked";
            error = null;
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

            EditorUtil.Config.DimensionalResolver.HybridCLRResult hybrid =
                EditorUtil.Config.DimensionalResolver.ResolveHybridCLR(master, platform, channel, mode);
            try { EditorUtil.HybridCLR.ValidateGameDllLists(hybrid.StartupGameDlls, hybrid.RunningGameDlls); }
            catch (Exception exception) { error = exception.Message; return false; }
            var mappings = new List<Mapping>();
            if (!TryAddMappings("startup", hybrid.StartupGameDlls, mappings, out error) ||
                !TryAddMappings("running", hybrid.RunningGameDlls, mappings, out error)) return false;
            if (mappings.Count == 0)
            {
                status = "not_applicable";
                error = "当前坐标没有 StartupGameDlls 或 RunningGameDlls 映射。";
                return false;
            }
            string duplicate = mappings.GroupBy(mapping => mapping.targetPath, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1).Select(group => group.Key).FirstOrDefault();
            if (duplicate != null)
            {
                error = "完整业务 DLL 映射存在重复目标：" + duplicate;
                return false;
            }

            if (!GenerateActionCommon.TryResolveProjectPath(SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target),
                    "HotUpdateDllsOutputDir", out string compileOutput, out error)) return false;
            string masterHash = GenerateActionCommon.ComputeFileHash(Path.Combine(GenerateActionCommon.ProjectRoot, masterPath), context.CancellationToken);
            string settingsHash = GenerateActionCommon.ComputeTextHash(target + "\n" + request.developmentBuild + "\n" +
                compileOutput + "\n" + string.Join("\n", SettingsUtil.HotUpdateAssemblyFilesIncludePreserved ?? new List<string>()));
            state = new State
            {
                Request = request,
                Target = target,
                Platform = platform,
                Channel = channel,
                Mode = mode,
                MasterPath = masterPath,
                MasterHash = masterHash,
                CompileOutput = compileOutput,
                SettingsHash = settingsHash,
                Mappings = mappings.ToArray(),
            };
            return true;
        }

        private static bool TryAddMappings(
            string group,
            IEnumerable<DllMasterAssetEntry> entries,
            ICollection<Mapping> result,
            out string error)
        {
            error = null;
            foreach (DllMasterAssetEntry entry in entries ?? Array.Empty<DllMasterAssetEntry>())
            {
                if (string.IsNullOrWhiteSpace(entry.SourceLocation) || string.IsNullOrWhiteSpace(entry.TargetLocation) ||
                    string.IsNullOrWhiteSpace(entry.AssetLocation))
                {
                    error = $"{group} DLL 映射包含空 SourceLocation/TargetLocation/AssetLocation。";
                    return false;
                }
                string sourceRaw = EditorUtil.HybridCLR.ResolvePathPlaceholders(entry.SourceLocation);
                string targetRaw = EditorUtil.HybridCLR.ResolvePathPlaceholders(entry.TargetLocation);
                if (!GenerateActionCommon.TryResolveProjectPath(sourceRaw, group + ".SourceLocation", out string sourcePath, out error) ||
                    !GenerateActionCommon.TryResolveProjectPath(targetRaw, group + ".TargetLocation", out string targetPath, out error)) return false;
                string targetRelative = GenerateActionCommon.ToProjectRelative(targetPath);
                if (targetRelative.StartsWith("Assets/Framework/", StringComparison.Ordinal) ||
                    targetRelative.StartsWith("Library/", StringComparison.Ordinal) ||
                    targetRelative.StartsWith("Packages/", StringComparison.Ordinal) ||
                    targetRelative.StartsWith("UPMPackages/", StringComparison.Ordinal) ||
                    targetRelative.StartsWith("ProjectSettings/", StringComparison.Ordinal))
                {
                    error = "业务 DLL TargetLocation 越过允许边界：" + targetRelative;
                    return false;
                }
                result.Add(new Mapping
                {
                    group = group,
                    sourceLocation = entry.SourceLocation,
                    targetLocation = entry.TargetLocation,
                    assetLocation = entry.AssetLocation,
                    sourcePath = sourcePath,
                    targetPath = targetPath,
                    targetAssetPath = targetRelative.StartsWith("Assets/", StringComparison.Ordinal) ? targetRelative : null,
                });
            }
            return true;
        }

        private static bool FrozenStatesEqual(State expected, State actual)
        {
            return actual != null && expected.Target == actual.Target && expected.Platform == actual.Platform &&
                   expected.Channel == actual.Channel && expected.Mode == actual.Mode &&
                   expected.MasterPath == actual.MasterPath && expected.MasterHash == actual.MasterHash &&
                   expected.CompileOutput == actual.CompileOutput && expected.SettingsHash == actual.SettingsHash &&
                   Util.Json.Serialize(expected.Mappings) == Util.Json.Serialize(actual.Mappings);
        }

        private static Receipt CreateReceipt(State state, HashPair[] pairs, GenerateActionCommon.Artifact[] artifacts)
        {
            return new Receipt
            {
                masterGuid = state.Request.masterGuid,
                masterPath = state.MasterPath,
                platform = state.Platform.ToString(),
                channel = state.Channel.ToString(),
                developMode = state.Mode.ToString(),
                activeBuildTarget = state.Target.ToString(),
                developmentBuild = state.Request.developmentBuild,
                compileOutput = state.CompileOutput,
                mappings = state.Mappings,
                hashPairs = pairs,
                artifacts = artifacts,
            };
        }
    }
}
