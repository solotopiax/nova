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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
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
    internal sealed class PlayerBuildAction : AgentActionHandler<PlayerBuildAction.Request>
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
        }

        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!BuildActionCommon.TryResolveActiveTarget(request.target, out _, out error) ||
                !BuildActionCommon.TryResolveOutputPath(request.outputPath, out _, out error) ||
                !TryResolveBuildMode(request.buildMode, out _, out error))
            {
                return false;
            }
            return true;
        }

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
                    Summary = $"Player Action 首版只安全支持 Standalone 桌面目标；{target} 的平台专属构建、导出与签名设置尚未进入冻结契约。",
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
            var state = new State
            {
                Target = target,
                OutputPath = outputPath,
                DevelopmentBuild = request.developmentBuild,
                BuildMode = buildMode,
                BuildModeLogical = request.buildMode,
                Scenes = scenes,
                Settings = settings,
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
                }),
                WriteSet = GetWriteSet(outputPath, request.buildMode),
                Evidence = new[]
                {
                    "BuildReport + 精确产物路径、文件/目录类型与 SHA-256。",
                    "BuildReport 不证明 Player 已启动或业务运行正确。",
                },
                RecoveryPayloadJson = Util.Json.Serialize(recovery),
            });
        }

        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State plan))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Player Action 的冻结计划状态无效。"));
            }
            if (EditorUserBuildSettings.activeBuildTarget != plan.Target)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "activeBuildTarget 已变化；不会切换平台或执行旧计划。"));
            }
            BuildActionCommon.PlayerSettingsSnapshot currentSettings = BuildActionCommon.CapturePlayerSettings(
                plan.Target, plan.DevelopmentBuild, context.CancellationToken);
            if (!BuildActionCommon.PlayerSettingsEqual(plan.Settings, currentSettings))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Player 平台专属设置或 ProjectSettings 已变化；不会执行旧计划。"));
            }
            string[] currentScenes = BuildActionCommon.GetEnabledBuildScenes();
            if (!BuildActionCommon.SceneSnapshotsEqual(plan.Scenes, currentScenes))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Build Settings 场景在确认后变化；不会自动重计划或构建。"));
            }

            string parent = Path.GetDirectoryName(plan.OutputPath);
            if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent)) Directory.CreateDirectory(parent);
            BuildReport report = EditorUtil.Build.BuildPlayer(
                plan.Target,
                plan.OutputPath,
                plan.DevelopmentBuild,
                plan.BuildMode);
            if (report == null || report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new InvalidOperationException("BuildReport 未返回 Succeeded。" );
            }
            string reportOutput = Path.GetFullPath(report.summary.outputPath);
            if (!string.Equals(reportOutput, plan.OutputPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"BuildReport 输出与冻结 outputPath 不一致：expected={plan.OutputPath}, actual={reportOutput}。" );
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
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.DataJson = result.ReceiptJson;
            result.EvidenceKinds = AgentActionEvidence.Artifact;
            result.Artifacts.Add(plan.OutputPath);
            result.Evidence.Add("已核对 BuildReport、精确 outputPath、文件/目录类型与 SHA-256。" );
            result.Warnings.Add("BuildReport 与产物只证明构建完成，不证明 Player 已启动、设备安装或业务运行正确。" );
            return Task.FromResult(result);
        }

        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            return Task.FromResult(BuildActionCommon.VerifyArtifact(receiptJson, "Player", context.CancellationToken));
        }

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

        private static string[] GetWriteSet(string outputPath, string buildMode)
        {
            var result = new System.Collections.Generic.List<string>
            {
                outputPath,
                "Library/Bee (Unity Player build cache)",
                "Library/Nova/YooAssetRuntimeSettingsStaging.json (temporary ownership marker)",
                "Active Sample Resources/YooAssetSettings.asset + .meta (temporary ADR-060 staging, removed after build)",
                "All currently dirty Unity assets (ADR-060 staging currently invokes AssetDatabase.SaveAssets)",
                "EditorUserBuildSettings.development (temporary, restored in finally)",
            };
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
