/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigExportRuntimeAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   显式 ConfigMaster GUID 与三维坐标的 ConfigRuntime 导出 Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using YooAsset;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.config.export-runtime",
        "导出运行时配置",
        "config",
        AgentActionOperationType.Generate,
        Description = "按 Platform、Channel、DevelopMode 坐标导出 ConfigRuntimeSO，并核验目标资产。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        ReloadSemantics = AgentActionReloadSemantics.ReloadNotExpected,
        Locks = new[] { "unity-editor", "asset-database", "config-master", "config-runtime-target" })]
    internal sealed class ConfigExportRuntimeAction : AgentActionHandler<ConfigExportRuntimeAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string masterGuid;
            [AgentActionRequired] public string platform;
            [AgentActionRequired] public string channel;
            [AgentActionRequired] public string developMode;
            [AgentActionRequired] public string savePath;
        }

        private sealed class State
        {
            public Request Request;
            public PlatformType Platform;
            public ChannelType Channel;
            public DevelopMode Mode;
            public string MasterPath;
            public string MasterHash;
            public string SavePath;
            public string SaveAbsolutePath;
            public string YooAssetSettingsPath;
            public DateTime PlaceholderTime;
        }

        [Serializable]
        private sealed class Receipt
        {
            public string masterGuid;
            public string masterPath;
            public string platform;
            public string channel;
            public string developMode;
            public string savePath;
            public string placeholderTime;
            public string resolvedNamespace;
            public GenerateActionCommon.Artifact[] artifacts;
        }

        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!GenerateActionCommon.TryValidateGuid(request?.masterGuid, out error)) return false;
            if (!GenerateActionCommon.TryParseCoordinate(
                    request.platform, request.channel, request.developMode,
                    out _, out _, out _, out error)) return false;
            return GenerateActionCommon.TryResolveAssetSavePath(request.savePath, out _, out _, out error);
        }

        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            GenerateActionCommon.TryParseCoordinate(
                request.platform, request.channel, request.developMode,
                out PlatformType platform, out ChannelType channel, out DevelopMode mode, out _);
            if (!GenerateActionCommon.TryResolveAssetSavePath(request.savePath, out string savePath, out string saveAbsolute, out string pathError))
            {
                return Blocked(pathError);
            }

            ConfigMasterSO master = GenerateActionCommon.ResolveMaster(request.masterGuid, out string masterPath);
            if (master == null)
            {
                return Blocked("masterGuid 未解析到 ConfigMasterSO 资产。");
            }
            if (!master.TryGetEntry(platform, channel, out PlatformChannelEntry entry))
            {
                return Blocked($"ConfigMaster 不存在 {platform}/{channel} 矩阵行；Plan 不会补齐或修改配置。");
            }
            if (!HasCompleteModeSnapshot(entry, mode))
            {
                return Blocked($"ConfigMaster 的 {platform}/{channel}/{mode} 四类配置条目不完整；现有 Getter/Validator 会自动补项，Plan 为保持只读而拒绝。请先在 ConfigWindow 修复。" );
            }

            IReadOnlyList<EditorUtil.Config.Validator.ValidationIssue> issues =
                EditorUtil.Config.Validator.Validate(master, platform, channel, mode);
            string[] errors = issues.Where(issue => issue.Level == EditorUtil.Config.Validator.Severity.Error)
                .Select(issue => issue.Path + ": " + issue.Message).ToArray();
            if (errors.Length > 0)
            {
                return Blocked("Config 校验未通过：" + string.Join("；", errors));
            }

            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(savePath);
            if (existing != null && !(existing is ConfigRuntimeSO))
            {
                return Blocked("savePath 已存在非 ConfigRuntimeSO 资产，拒绝覆盖：" + savePath);
            }

            EditorUtil.Config.DimensionalResolver.YooAssetResult yoo =
                EditorUtil.Config.DimensionalResolver.ResolveYooAsset(master, platform, channel, mode);
            string yooPath = string.IsNullOrWhiteSpace(yoo.YooAssetSettingsPath)
                ? null
                : yoo.YooAssetSettingsPath.Replace('\\', '/');
            if (yooPath != null)
            {
                if (!GenerateActionCommon.TryResolveProjectPath(yooPath, "YooAssetSettingsPath", out _, out string yooError) ||
                    !yooPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                    AssetDatabase.LoadAssetAtPath<YooAssetSettings>(yooPath) == null)
                {
                    return Blocked(yooError ?? "当前坐标的 YooAssetSettingsPath 未指向有效 YooAssetSettings.asset：" + yooPath);
                }
            }

            string masterAbsolute = Path.Combine(GenerateActionCommon.ProjectRoot, masterPath);
            string masterHash = GenerateActionCommon.ComputeFileHash(masterAbsolute, context.CancellationToken);
            DateTime placeholderTime = DateTime.Now;
            var state = new State
            {
                Request = request,
                Platform = platform,
                Channel = channel,
                Mode = mode,
                MasterPath = masterPath,
                MasterHash = masterHash,
                SavePath = savePath,
                SaveAbsolutePath = saveAbsolute,
                YooAssetSettingsPath = yooPath,
                PlaceholderTime = placeholderTime,
            };
            var receipt = new Receipt
            {
                masterGuid = request.masterGuid,
                masterPath = masterPath,
                platform = platform.ToString(),
                channel = channel.ToString(),
                developMode = mode.ToString(),
                savePath = savePath,
                placeholderTime = placeholderTime.ToString("O"),
                resolvedNamespace = EditorUtil.Config.DimensionalResolver.ResolveNamespace(master, platform, channel, mode),
                artifacts = Array.Empty<GenerateActionCommon.Artifact>(),
            };

            var writeSet = new List<string> { savePath, savePath + ".meta (仅新建资产时)" };
            if (yooPath != null) writeSet.Add(yooPath);
            string[] warnings = issues.Select(issue => issue.Path + ": " + issue.Message).ToArray();
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将 {masterPath} 的 {platform}/{channel}/{mode} 导出到 {savePath}。",
                DataJson = Util.Json.Serialize(receipt),
                State = state,
                WriteSet = writeSet.ToArray(),
                Evidence = warnings.Concat(new[] { "Plan 只读冻结 master GUID、三维坐标、savePath 与 YooAssetSettings 写入面。" }).ToArray(),
                RecoveryPayloadJson = Util.Json.Serialize(receipt),
            });
        }

        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen)) return Task.FromResult(AgentActionResult.Create(null, "blocked", "Config 导出冻结状态无效。"));
            ConfigMasterSO master = GenerateActionCommon.ResolveMaster(frozen.Request.masterGuid, out string masterPath);
            if (master == null || masterPath != frozen.MasterPath ||
                GenerateActionCommon.ComputeFileHash(Path.Combine(GenerateActionCommon.ProjectRoot, masterPath), context.CancellationToken) != frozen.MasterHash)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "ConfigMaster 身份或内容已漂移，请重新 Plan。"));
            }
            if (!master.TryGetEntry(frozen.Platform, frozen.Channel, out _))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "冻结的 Config 三维矩阵行已不存在。"));
            }

            ConfigRuntimeSO runtime = EditorUtil.Config.Exporter.Export(
                master, frozen.Platform, frozen.Channel, frozen.Mode, frozen.SavePath, frozen.PlaceholderTime, true);
            if (runtime == null || AssetDatabase.GetAssetPath(runtime) != frozen.SavePath)
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "领域 Exporter 未返回冻结 savePath 的 ConfigRuntimeSO；不会自动重放。"));
            }

            var artifacts = new List<GenerateActionCommon.Artifact>
            {
                GenerateActionCommon.CaptureFile(frozen.SaveAbsolutePath, context.CancellationToken),
            };
            if (File.Exists(frozen.SaveAbsolutePath + ".meta"))
            {
                artifacts.Add(GenerateActionCommon.CaptureFile(frozen.SaveAbsolutePath + ".meta", context.CancellationToken));
            }
            if (!string.IsNullOrEmpty(frozen.YooAssetSettingsPath))
            {
                artifacts.Add(GenerateActionCommon.CaptureFile(
                    Path.Combine(GenerateActionCommon.ProjectRoot, frozen.YooAssetSettingsPath), context.CancellationToken));
            }

            var receipt = new Receipt
            {
                masterGuid = frozen.Request.masterGuid,
                masterPath = frozen.MasterPath,
                platform = frozen.Platform.ToString(),
                channel = frozen.Channel.ToString(),
                developMode = frozen.Mode.ToString(),
                savePath = frozen.SavePath,
                placeholderTime = frozen.PlaceholderTime.ToString("O"),
                resolvedNamespace = runtime.Namespace,
                artifacts = artifacts.ToArray(),
            };
            AgentActionResult result = AgentActionResult.Create(null, "partial", "ConfigRuntime 已导出，等待只读 Verify 核对精确字段与 SHA-256。" );
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.DataJson = result.ReceiptJson;
            result.Artifacts.AddRange(artifacts.Select(artifact => artifact.path));
            result.Evidence.Add("Execute 仅调用 EditorUtil.Config.Exporter.Export 一次，仅保存并精确导入本次 ConfigRuntime 目标；失败不会自动重放。" );
            return Task.FromResult(result);
        }

        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            try { receipt = Util.Json.Deserialize<Receipt>(receiptJson); }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Config Export Receipt 无法解析：" + exception.Message));
            }
            string guidError = null;
            string pathError = null;
            string savePath = null;
            string saveAbsolute = null;
            if (receipt == null || !GenerateActionCommon.TryValidateGuid(receipt.masterGuid, out guidError) ||
                !GenerateActionCommon.TryResolveAssetSavePath(receipt.savePath, out savePath, out saveAbsolute, out pathError))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", guidError ?? pathError ?? "Config Export Receipt 不完整。"));
            }
            if (!GenerateActionCommon.TryVerifyArtifacts(receipt.artifacts, context.CancellationToken, out GenerateActionCommon.Artifact[] actual, out string artifactError))
            {
                AgentActionResult partial = AgentActionResult.Create(null, "partial", "Config 导出产物验证未通过：" + artifactError);
                partial.DataJson = Util.Json.Serialize(actual);
                return Task.FromResult(partial);
            }
            if (!actual.Any(artifact => artifact.kind == "file" &&
                                        string.Equals(artifact.path, saveAbsolute, StringComparison.Ordinal)))
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "Receipt 产物集合未覆盖冻结的 ConfigRuntime savePath。"));
            }

            ConfigRuntimeSO runtime = AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(savePath);
            if (runtime == null || runtime.Platform.ToString() != receipt.platform || runtime.Channel.ToString() != receipt.channel ||
                runtime.DevelopMode.ToString() != receipt.developMode || runtime.Namespace != receipt.resolvedNamespace)
            {
                return Task.FromResult(AgentActionResult.Create(null, "partial", "ConfigRuntimeSO 的 Platform/Channel/DevelopMode/Namespace 与 Receipt 不一致。"));
            }

            AgentActionResult success = AgentActionResult.Create(null, "success", "ConfigRuntime 精确字段与所有写入产物 SHA-256 已只读核对。" );
            success.EvidenceKinds = AgentActionEvidence.Static | AgentActionEvidence.Artifact;
            success.DataJson = Util.Json.Serialize(actual);
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path));
            success.Evidence.Add("Verify 未调用 Exporter、SaveAssets 或任何恢复写入。" );
            return Task.FromResult(success);
        }

        private static Task<AgentActionHandlerPlan> Blocked(string message)
        {
            return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = message });
        }

        private static bool HasCompleteModeSnapshot(PlatformChannelEntry entry, DevelopMode mode)
        {
            return entry?.AppConfigsByMode != null &&
                   entry.AppConfigsByMode.Any(item => item != null && item.Mode == mode && item.Config != null) &&
                   entry.PrivacyConfigsByMode != null &&
                   entry.PrivacyConfigsByMode.Any(item => item != null && item.Mode == mode && item.Config != null) &&
                   entry.SDKConfigsByMode != null &&
                   entry.SDKConfigsByMode.Any(item => item != null && item.Mode == mode && item.SDKConfigs != null) &&
                   entry.KitConfigsByMode != null &&
                   entry.KitConfigsByMode.Any(item => item != null && item.Mode == mode && item.KitConfigs != null);
        }
    }
}
