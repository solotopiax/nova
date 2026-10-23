/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkExportAction.cs
 * author:    taoye
 * created:   2026/8/21
 * descrip:   Network HostKey/NetCmd/Proto 精确导出 Agent Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.network.export",
        "导出 Network",
        "network",
        AgentActionOperationType.Generate,
        Description = "从当前有效 NetworkSettings 导出网络配置与协议相关生成物，并核验结果。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        Locks = new[] { "unity-editor", "asset-database", "network-export" })]
    internal sealed class NetworkExportAction : AgentActionHandler<NetworkExportAction.Request>
    {
        private const string c_ActionId = "nova.project.network.export";

        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string scope;
            [AgentActionRequired] public string[] targets;
        }

        [Serializable]
        internal sealed class Output
        {
            public string target;
            public string kind;
            public string path;
            public GenerateActionCommon.Artifact artifact;
        }

        [Serializable]
        private sealed class Receipt
        {
            public int receiptVersion;
            public string scope;
            public string scenePath;
            public string componentId;
            public string settingsHash;
            public string developMode;
            public string[] targets;
            public Output[] expectedOutputs;
            public Output[] outputs;
            public int sourceContractVersion;
            public DataModuleExportActionCommon.NamespaceSourceSnapshot namespaceSource;
            public DataModuleExportActionCommon.InputSnapshot inputSnapshot;
            public DataModuleExportActionCommon.ConfigurationSnapshot[] configurations;
            public GenerateActionCommon.Artifact[] configurationArtifacts;
            public string compilationToken;
        }

        private sealed class State
        {
            public string Scope;
            public string ScenePath;
            public string ComponentId;
            public string SettingsHash;
            public string DevelopMode;
            public string[] Targets;
            public NetworkSettings NetworkSettings;
            public ProtoSettings ProtoSettings;
            public Output[] Outputs;
            public DataModuleExportActionCommon.NamespaceSourceSnapshot NamespaceSource;
            public DataModuleExportActionCommon.InputSnapshot InputSnapshot;
            public DataModuleExportActionCommon.ConfigurationSnapshot[] Configurations;
        }

        /// <summary>
        /// 校验导出范围及 HostKey、NetCmd、Proto 目标组合。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (!TryNormalizeScope(request?.scope, out string scope))
            {
                error = "scope 只允许 all、code 或 data。";
                return false;
            }
            if (request.targets == null || request.targets.Length == 0 || request.targets.Length > 3)
            {
                error = "targets 必须包含 hostkey、netcmd 或 proto，最多三项。";
                return false;
            }
            if (!TryNormalizeTargets(request.targets, out string[] targets, out error)) return false;
            if (scope == "data" && targets.Contains("proto"))
            {
                error = "Proto 只生成代码，不能使用 data scope。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 只读冻结 Network 设置、DevelopMode、源文件和输出范围。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            // 直接领域调用与 Dispatcher 调用共享相同校验，避免绕过规范化后的组合限制。
            if (!TryValidateRequest(request, out string validationError)) return Blocked(validationError);
            if (!NetworkSettingsResolver.TryResolve(out NetworkSettingsResolver.Resolved resolved, out string resolveError))
            {
                return Blocked(resolveError);
            }
            TryNormalizeScope(request.scope, out string scope);
            TryNormalizeTargets(request.targets, out string[] targets, out _);
            if (!TryBuildOutputs(resolved, scope, targets, out Output[] outputs, out string developMode, out string error))
            {
                return Blocked(error);
            }

            bool includesCode = DataModuleExportActionCommon.IncludesCode(scope);
            if (includesCode && GeneratedExportCompilationState.IsPending()) return Blocked("已有导出编译待完成，请先 Verify 该收据。");
            DataModuleExportActionCommon.NamespaceSourceSnapshot namespaceSource = null;
            if (targets.Any(target => target != "proto") &&
                !DataModuleExportActionCommon.TryCaptureNamespaceSource(context.CancellationToken, out namespaceSource, out error)) return Blocked(error);
            DataModuleExportActionCommon.ConfigurationSnapshot[] configurations;
            DataModuleExportActionCommon.InputSnapshot inputSnapshot;
            string[] writeSet;
            try
            {
                configurations = BuildConfigurations(resolved.NetworkSettings, outputs, targets, scope);
                inputSnapshot = CaptureInputSnapshot(resolved, configurations, targets, scope, context);
                writeSet = BuildCompleteWriteSet(outputs, configurations, resolved.NetworkSettings?.DataFormat ?? LubanDataFormat.Json);
                if ((namespaceSource != null && !DataModuleExportActionCommon.TryVerifyNamespaceSource(namespaceSource, context.CancellationToken, out error)) ||
                    !DataModuleExportActionCommon.TryVerifyInputs(inputSnapshot, context.CancellationToken, out error)) return Blocked(error);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { return Blocked(exception.Message); }
            var state = new State
            {
                Scope = scope,
                ScenePath = resolved.ScenePath,
                ComponentId = resolved.ComponentId,
                SettingsHash = resolved.SettingsHash,
                DevelopMode = developMode,
                Targets = targets,
                NetworkSettings = resolved.NetworkSettings,
                ProtoSettings = resolved.ProtoSettings,
                Outputs = outputs,
                NamespaceSource = namespaceSource, InputSnapshot = inputSnapshot, Configurations = configurations,
            };
            var receipt = CreateReceipt(state, Array.Empty<Output>());
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将按 {scope} 范围导出 Network：{string.Join(", ", targets)}。",
                DataJson = Util.Json.Serialize(receipt),
                State = state,
                WriteSet = writeSet,
                Evidence = new[] { "Plan 已只读冻结唯一 NetworkComponent、设置、ConfigRuntime DevelopMode 与精确输出范围。" },
                RecoveryPayloadJson = Util.Json.Serialize(receipt),
            });
        }

        /// <summary>
        /// 在冻结状态未漂移时调用对应 Network Exporter。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen))
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked", "Network 导出冻结状态无效。"));
            }
            if (!NetworkSettingsResolver.TryResolve(out NetworkSettingsResolver.Resolved current, out string resolveError) ||
                current.ScenePath != frozen.ScenePath || current.ComponentId != frozen.ComponentId ||
                current.SettingsHash != frozen.SettingsHash)
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked",
                    resolveError ?? "活动场景、NetworkComponent 或 Network 设置已漂移，请重新 Plan。"));
            }
            if (frozen.Targets.Contains("hostkey"))
            {
                if (!NetworkSettingsResolver.TryReadActiveDevelopMode(out DevelopMode currentMode, out _) ||
                    currentMode.ToString() != frozen.DevelopMode)
                {
                    return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked", "ConfigRuntime DevelopMode 已漂移，请重新 Plan。"));
                }
            }

            try
            {
                if (!DataModuleExportActionCommon.HasExactInputScope(frozen.InputSnapshot,
                        CaptureInputSnapshot(current, frozen.Configurations, frozen.Targets, frozen.Scope, context)))
                    return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked", "实际模板、协议或工具输入根已变化，请重新 Plan。"));
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked", exception.Message)); }
            string sourceError = null;
            if ((frozen.Targets.Any(target => target != "proto") &&
                 !DataModuleExportActionCommon.TryVerifyNamespaceSource(frozen.NamespaceSource, context.CancellationToken, out sourceError)) ||
                !DataModuleExportActionCommon.TryVerifyInputs(frozen.InputSnapshot, context.CancellationToken, out sourceError))
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked", sourceError));
            if (DataModuleExportActionCommon.IncludesCode(frozen.Scope) && GeneratedExportCompilationState.IsPending())
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked", "已有导出编译待完成，请先 Verify 该收据。"));

            foreach (string target in frozen.Targets)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (!ExecuteTarget(target, frozen.Scope, frozen.DevelopMode, frozen.NetworkSettings, frozen.ProtoSettings))
                {
                    return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial",
                        $"Network {target} 导出失败；此前目标可能已发布，Action 不会自动重放。"));
                }
            }

            Output[] captured;
            try
            {
                captured = frozen.Outputs.Select(output => new Output
                {
                    target = output.target,
                    kind = output.kind,
                    path = output.path,
                    artifact = output.kind == "data"
                        ? GenerateActionCommon.CaptureFile(output.path, context.CancellationToken)
                        : GenerateActionCommon.CaptureDirectory(output.path, "*", context.CancellationToken),
                }).ToArray();
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial",
                    "Network 已导出，但产物取证失败：" + exception.Message));
            }

            try
            {
                string sourceErrorAfter = null;
                if ((frozen.NamespaceSource != null && !DataModuleExportActionCommon.TryVerifyNamespaceSource(frozen.NamespaceSource, context.CancellationToken, out sourceErrorAfter)) ||
                    !DataModuleExportActionCommon.TryVerifyUnchangedInputs(frozen.InputSnapshot, frozen.Configurations, context.CancellationToken, out sourceErrorAfter) ||
                    (frozen.Configurations.Length > 0 && !DataModuleExportActionCommon.TryVerifyExportedConfigurations(frozen.NamespaceSource, frozen.Configurations, context.CancellationToken, out sourceErrorAfter)))
                    return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial", sourceErrorAfter));
                Receipt receipt = CreateReceipt(frozen, captured);
                receipt.configurationArtifacts = DataModuleExportActionCommon.CaptureConfigurationArtifacts(frozen.Configurations, context.CancellationToken);
                receipt.compilationToken = DataModuleExportActionCommon.IncludesCode(frozen.Scope) ? Guid.NewGuid().ToString("N") : null;
                AgentActionResult result = AgentActionResult.Create(c_ActionId, "partial", "Network 已导出，等待 Verify 核对产物摘要。");
                result.ReceiptJson = Util.Json.Serialize(receipt);
                result.DataJson = result.ReceiptJson;
                if (DataModuleExportActionCommon.IncludesCode(frozen.Scope))
                    GeneratedExportCompilationState.Schedule(receipt.compilationToken, GenerateActionCommon.ComputeTextHash(result.ReceiptJson));
                result.Artifacts.AddRange(captured.Select(output => output.artifact.path).Distinct(StringComparer.Ordinal));
                result.Evidence.Add("Execute 仅调用现有 Network HostKey/NetCmd/Proto Exporter，并记录输出 SHA-256。");
                if (captured.Any(output => output.kind == "code"))
                {
                    result.Warnings.Add("代码产物摘要不等于 Unity 编译成功；domain reload 后应继续 Verify。");
                }
                return Task.FromResult(result);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial", "Network 已导出但来源或配置取证失败：" + exception.Message));
            }
        }

        /// <summary>
        /// 只读核对计划冻结的完整输出集合、产物类型与 SHA-256；旧 Receipt 缺证据时保持 partial。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            try
            {
                receipt = Util.Json.Deserialize<Receipt>(receiptJson);
            }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked", "Network Export Receipt 无法解析：" + exception.Message));
            }
            if (receipt == null || receipt.receiptVersion != 2 ||
                !TryValidateOutputEvidence(receipt.scope, receipt.targets, receipt.expectedOutputs, receipt.outputs, out _))
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial",
                    "Network Export Receipt 尚无完整冻结范围及产物证据。"));
            }
            if (!GenerateActionCommon.TryVerifyArtifacts(receipt.outputs.Select(output => output.artifact),
                    context.CancellationToken, out GenerateActionCommon.Artifact[] actual, out string error))
            {
                AgentActionResult partial = AgentActionResult.Create(c_ActionId, "partial", "Network 导出产物验证未通过：" + error);
                partial.DataJson = Util.Json.Serialize(actual);
                return Task.FromResult(partial);
            }

            bool sourceContract;
            try { sourceContract = DataModuleExportActionCommon.HasSourceContract(receiptJson); }
            catch (Exception exception) { return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial", exception.Message)); }
            if (sourceContract)
            {
                if (!TryVerifySourceScope(receipt, context, out error)) return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial", error));
                bool needsNamespace = receipt.targets.Any(target => target != "proto");
                if (receipt.sourceContractVersion != 1 || receipt.configurations == null || (needsNamespace && receipt.configurations.Length == 0) ||
                    (needsNamespace && !DataModuleExportActionCommon.TryVerifyNamespaceSource(receipt.namespaceSource, context.CancellationToken, out error)) ||
                    !DataModuleExportActionCommon.TryVerifyUnchangedInputs(receipt.inputSnapshot, receipt.configurations, context.CancellationToken, out error) ||
                    !DataModuleExportActionCommon.TryVerifyConfigurationArtifacts(receipt.configurations, receipt.configurationArtifacts, context.CancellationToken, out error) ||
                    (needsNamespace && !DataModuleExportActionCommon.TryVerifyExportedConfigurations(receipt.namespaceSource, receipt.configurations, context.CancellationToken, out error)))
                    return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial", error ?? "新 Network 收据缺少完整来源或配置证据。"));
                if (DataModuleExportActionCommon.IncludesCode(receipt.scope) &&
                    (!GeneratedExportCompilationState.TryVerify(receipt.compilationToken, GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(receipt)), true, out error) ||
                     (needsNamespace && !DataModuleExportActionCommon.TryVerifyLoadedTypes(receipt.namespaceSource, receipt.configurations, out error))))
                    return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial", error));
            }
            AgentActionResult success = AgentActionResult.Create(c_ActionId, "success", "Network 输出范围与 SHA-256 已只读核对。");
            success.EvidenceKinds = (sourceContract && DataModuleExportActionCommon.IncludesCode(receipt.scope)
                ? AgentActionEvidence.Static | AgentActionEvidence.Compile : AgentActionEvidence.Static) | AgentActionEvidence.Artifact;
            if (!sourceContract) success.Warnings.Add("缺命名空间/输入快照，仅历史产物摘要；不证明新契约。");
            success.DataJson = Util.Json.Serialize(receipt.outputs);
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path).Distinct(StringComparer.Ordinal));
            success.Evidence.AddRange(receipt.outputs.Select(output =>
                $"{output.target}/{output.kind}：{output.artifact.fileCount} 个文件，SHA-256={output.artifact.sha256}"));
            success.Evidence.Add("Verify 未调用 Exporter、AssetDatabase.Refresh 或任何恢复写入。");
            return Task.FromResult(success);
        }

        /// <summary>
        /// 核对冻结范围与执行产物一一对应，拒绝缺项、重复、跨目标、错误类型及路径替换。
        /// 仅解析项目内路径，不读取当前设置重建旧计划，也不修改任何文件。
        /// </summary>
        internal static bool TryValidateOutputEvidence(string scope, string[] targets, Output[] expected,
            Output[] outputs, out string error)
        {
            error = "冻结输出范围与产物记录不完整或不一致。";
            if (!TryNormalizeScope(scope, out string normalizedScope) || scope != normalizedScope ||
                !TryNormalizeTargets(targets, out string[] normalizedTargets, out _) ||
                !targets.SequenceEqual(normalizedTargets) ||
                (scope == "data" && targets.Contains("proto")) ||
                expected == null || expected.Length == 0 || outputs == null || outputs.Length != expected.Length)
            {
                return false;
            }

            StringComparer pathComparer = IOPath.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var expectedKeys = new HashSet<string>(pathComparer);
            foreach (Output output in expected)
            {
                if (output == null || !targets.Contains(output.target) ||
                    (output.kind != "code" && output.kind != "data") ||
                    (output.target == "proto" && output.kind != "code") ||
                    (scope != "all" && output.kind != scope) ||
                    !GenerateActionCommon.TryResolveProjectPath(output.path, "冻结输出路径", out string fullPath, out _)) return false;
                fullPath = fullPath.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
                if (!expectedKeys.Add(output.target + "\n" + output.kind + "\n" + fullPath)) return false;
            }
            foreach (string target in targets)
            {
                string[] kinds = target == "proto" ? new[] { "code" } :
                    scope == "all" ? new[] { "code", "data" } : new[] { scope };
                if (kinds.Any(kind => !expected.Any(output => output.target == target && output.kind == kind))) return false;
            }

            var actualKeys = new HashSet<string>(pathComparer);
            foreach (Output output in outputs)
            {
                if (output?.artifact == null || !targets.Contains(output.target) ||
                    (output.kind != "code" && output.kind != "data") ||
                    !GenerateActionCommon.TryResolveProjectPath(output.path, "产物输出路径", out string fullPath, out _) ||
                    !GenerateActionCommon.TryResolveProjectPath(output.artifact.path, "产物摘要路径", out string artifactPath, out _)) return false;
                fullPath = fullPath.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
                artifactPath = artifactPath.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
                if (!pathComparer.Equals(fullPath, artifactPath) ||
                    output.artifact.kind != (output.kind == "data" ? "file" : "directory") ||
                    !actualKeys.Add(output.target + "\n" + output.kind + "\n" + fullPath)) return false;
            }
            if (!expectedKeys.SetEquals(actualKeys)) return false;
            error = null;
            return true;
        }

        /// <summary>
        /// 将导出范围规范为 all、code 或 data。
        /// </summary>
        internal static bool TryNormalizeScope(string value, out string scope)
        {
            scope = value?.Trim().ToLowerInvariant();
            return scope == "all" || scope == "code" || scope == "data";
        }

        /// <summary>
        /// 规范并校验 HostKey、NetCmd、Proto 目标集合。
        /// </summary>
        internal static bool TryNormalizeTargets(IEnumerable<string> values, out string[] targets, out string error)
        {
            error = null;
            targets = values?.Select(value => value?.Trim().ToLowerInvariant()).ToArray() ?? Array.Empty<string>();
            if (targets.Any(value => value != "hostkey" && value != "netcmd" && value != "proto"))
            {
                error = "targets 只允许 hostkey、netcmd 或 proto。";
                return false;
            }
            if (targets.Distinct(StringComparer.Ordinal).Count() != targets.Length)
            {
                error = "targets 不能包含重复项。";
                return false;
            }
            targets = targets.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return targets.Length > 0;
        }

        /// <summary>
        /// 按冻结目标规划 Network 正式输出，并记录 HostKey 使用的 DevelopMode。
        /// </summary>
        internal static bool TryBuildOutputs(NetworkSettingsResolver.Resolved resolved, string scope, string[] targets,
            out Output[] outputs, out string developMode, out string error)
        {
            var result = new List<Output>();
            developMode = null;
            error = null;
            if (!TryNormalizeScope(scope, out string normalizedScope) || scope != normalizedScope ||
                !TryNormalizeTargets(targets, out string[] normalizedTargets, out error) ||
                !targets.SequenceEqual(normalizedTargets) || (scope == "data" && targets.Contains("proto")))
            {
                error ??= "Network 导出范围或目标组合无效。";
                return Fail(out outputs);
            }
            foreach (string target in targets)
            {
                if (target == "proto")
                {
                    if (!TryPlanProto(resolved.ProtoSettings, result, out error)) return Fail(out outputs);
                    continue;
                }

                if (resolved.NetworkSettings == null)
                {
                    error = "NetworkComponent.m_Settings 未配置。";
                    return Fail(out outputs);
                }
                if (target == "hostkey")
                {
                    if (!NetworkSettingsResolver.TryReadActiveDevelopMode(out DevelopMode mode, out error))
                    {
                        return Fail(out outputs);
                    }
                    developMode = mode.ToString();
                    if (!TryPlanTable("hostkey", resolved.NetworkSettings.HostKeySettings,
                            resolved.NetworkSettings.HostKeySettings?.HostKeyUnits, scope, result, out error)) return Fail(out outputs);
                }
                else if (!TryPlanTable("netcmd", resolved.NetworkSettings.NetCmdSettings,
                             resolved.NetworkSettings.NetCmdSettings?.NetCmdUnits, scope, result, out error)) return Fail(out outputs);
            }
            outputs = result.GroupBy(output => output.target + "\n" + output.kind + "\n" + output.path, StringComparer.Ordinal)
                .Select(group => group.First()).OrderBy(output => output.path, StringComparer.Ordinal).ToArray();
            return outputs.Length > 0;
        }

        /// <summary>
        /// 为 HostKey 或 NetCmd 表冻结源文件与代码、数据输出。
        /// </summary>
        private static bool TryPlanTable<TSettings, TUnit>(string target, TSettings settings, IReadOnlyList<TUnit> units,
            string scope, ICollection<Output> outputs, out string error)
            where TSettings : class, IDataTableSettings where TUnit : DataTableUnitSettingBase
        {
            error = null;
            if (settings == null || units == null || units.Count == 0)
            {
                error = target + " 设置或单元为空。";
                return false;
            }
            if (!GenerateActionCommon.TryResolveProjectPath(settings.SourceDirPath, target + " SourceDirPath", out string sourceRoot, out error) ||
                !Directory.Exists(sourceRoot))
            {
                error ??= target + " 数据源目录不存在。";
                return false;
            }
            var codePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dataPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TUnit unit in units)
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.SourcePath))
                {
                    error = target + " 包含空单元或空 SourcePath。";
                    return false;
                }
                string sourcePath = IOPath.GetFullPath(IOPath.Combine(sourceRoot, unit.SourcePath));
                if (!IsBelow(sourceRoot, sourcePath) || !File.Exists(sourcePath))
                {
                    error = target + " 源文件不存在或越出 SourceDirPath：" + unit.SourcePath;
                    return false;
                }
                if (scope != "code")
                {
                    if (!ValidateOutput(unit.DatasExportPath, target + " DatasExportPath", out string dataPath, out error)) return false;
                    if (!dataPaths.Add(dataPath))
                    {
                        error = target + " 数据导出路径重复：" + dataPath;
                        return false;
                    }
                    outputs.Add(new Output { target = target, kind = "data", path = dataPath });
                }
                if (scope != "data")
                {
                    if (!ValidateOutput(unit.ClassesExportPath, target + " ClassesExportPath", out string codePath, out error)) return false;
                    codePaths.Add(codePath);
                }
            }
            if (scope != "data")
            {
                if (codePaths.Count != 1)
                {
                    error = target + " 类型导出路径必须统一且不能为空。";
                    return false;
                }
                outputs.Add(new Output { target = target, kind = "code", path = codePaths.Single() });
            }
            return true;
        }

        /// <summary>
        /// 冻结 Proto 输入及其代码输出范围。
        /// </summary>
        private static bool TryPlanProto(ProtoSettings settings, ICollection<Output> outputs, out string error)
        {
            error = null;
            if (settings?.ProtoUnits == null || settings.ProtoUnits.Count == 0)
            {
                error = "ProtoSettings 或 ProtoUnits 未配置。";
                return false;
            }
            if (!GenerateActionCommon.TryResolveProjectPath(settings.ProtoSourceDirPath, "ProtoSourceDirPath", out string root, out error) ||
                !Directory.Exists(root))
            {
                error ??= "Proto 源目录不存在。";
                return false;
            }
            foreach (ProtoUnitSetting unit in settings.ProtoUnits)
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.SourcePath) || string.IsNullOrWhiteSpace(unit.CSharpExportPath))
                {
                    error = "Proto 单元、SourcePath 或 CSharpExportPath 不能为空。";
                    return false;
                }
                string source = IOPath.GetFullPath(IOPath.Combine(root, unit.SourcePath));
                if (!IsBelow(root, source) || !GenerateActionCommon.IsWithinProject(source) || !File.Exists(source))
                {
                    error = "Proto 源文件不存在或位于项目外：" + unit.SourcePath;
                    return false;
                }
                if (!ValidateOutput(unit.CSharpExportPath, "Proto CSharpExportPath", out string output, out error)) return false;
                outputs.Add(new Output { target = "proto", kind = "code", path = output });
            }
            return true;
        }

        /// <summary>
        /// 校验并规范单个项目内输出路径。
        /// </summary>
        private static bool ValidateOutput(string path, string field, out string normalized, out string error)
        {
            normalized = path?.Replace('\\', '/');
            if (!GenerateActionCommon.TryResolveProjectPath(normalized, field, out _, out error)) return false;
            return true;
        }

        /// <summary>
        /// 判断目标路径是否严格位于指定目录下。
        /// </summary>
        private static bool IsBelow(string root, string path)
        {
            string prefix = root.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar) +
                            IOPath.DirectorySeparatorChar;
            StringComparison comparison = IOPath.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return path.StartsWith(prefix, comparison);
        }

        /// <summary>
        /// 调用与冻结目标和范围对应的既有 Network Exporter。
        /// </summary>
        private static bool ExecuteTarget(string target, string scope, string developMode, NetworkSettings network, ProtoSettings proto)
        {
            if (target == "proto") return scope != "data" && EditorUtil.Network.ProtoExporter.ExportAllProtos(proto);
            if (target == "hostkey")
            {
                if (!Enum.TryParse(developMode, out DevelopMode frozenMode)) return false;
                var operations = new NetworkExporter.ExportOperations { GetDevelopMode = () => frozenMode };
                NetworkExporter.ExportMode exportMode = scope == "code"
                    ? NetworkExporter.ExportMode.Code
                    : scope == "data" ? NetworkExporter.ExportMode.Data : NetworkExporter.ExportMode.All;
                return NetworkExporter.ExportHostKeys(
                    network.HostKeySettings,
                    exportMode,
                    network.DataFormat,
                    null,
                    operations);
            }
            return scope == "code" ? EditorUtil.Network.NetCmdExporter.ExportNetCmdCode(network.NetCmdSettings, network.DataFormat) :
                scope == "data" ? EditorUtil.Network.NetCmdExporter.ExportNetCmdData(network.NetCmdSettings, network.DataFormat) :
                EditorUtil.Network.NetCmdExporter.ExportNetCmdAll(network.NetCmdSettings, network.DataFormat);
        }

        /// <summary>
        /// 从冻结状态和实际产物创建版本 2 Receipt，保留独立的完整预期输出用于只读核验。
        /// </summary>
        private static Receipt CreateReceipt(State state, Output[] outputs)
        {
            return new Receipt
            {
                receiptVersion = 2,
                scope = state.Scope, scenePath = state.ScenePath, componentId = state.ComponentId, settingsHash = state.SettingsHash,
                developMode = state.DevelopMode, targets = state.Targets, outputs = outputs,
                sourceContractVersion = 1, namespaceSource = state.NamespaceSource, inputSnapshot = state.InputSnapshot,
                configurations = state.Configurations,
                expectedOutputs = state.Outputs.Select(output => new Output
                {
                    target = output.target, kind = output.kind, path = output.path,
                }).ToArray(),
            };
        }

        /// <summary>HostKey/NetCmd 固定 Profile 共用源快照；Proto 直接走 protoc，不建立 Master 或 Luban 配置依赖。</summary>
        private static DataModuleExportActionCommon.ConfigurationSnapshot[] BuildConfigurations(NetworkSettings settings,
            Output[] outputs, string[] targets, string scope)
        {
            var configurations = new List<DataModuleExportActionCommon.ConfigurationSnapshot>();
            foreach (string target in targets.Where(value => value != "proto"))
            {
                IDataTableSettings table = target == "hostkey" ? settings.HostKeySettings : settings.NetCmdSettings;
                EditorUtil.Luban.LubanExportProfile profile = target == "hostkey" ? EditorUtil.Luban.LubanExportProfiles.NetworkHostKey : EditorUtil.Luban.LubanExportProfiles.NetworkCmd;
                configurations.Add(DataModuleExportActionCommon.CreateConfigurationSnapshot(profile, table.SourceDirPath,
                    outputs.FirstOrDefault(output => output.target == target && output.kind == "code")?.path,
                    table.Units.Select(unit => unit.SourcePath), DataModuleExportActionCommon.IncludesCode(scope), true));
            }
            return configurations.ToArray();
        }

        /// <summary>冻结网络源表、配置、模板和实际工具；Proto 同时冻结根目录 imports 与 protoc include，仍不解析业务类型。</summary>
        private static DataModuleExportActionCommon.InputSnapshot CaptureInputSnapshot(NetworkSettingsResolver.Resolved resolved,
            DataModuleExportActionCommon.ConfigurationSnapshot[] configurations, string[] targets, string scope, AgentActionExecutionContext context)
        {
            var files = new List<string> { resolved.ScenePath };
            files.AddRange(configurations.SelectMany(DataModuleExportActionCommon.ConfigurationFiles));
            files.AddRange(configurations.SelectMany(configuration => configuration.sourcePaths.Select(source => IOPath.Combine(configuration.sourceDirectory, source))));
            if (scope == "code")
                foreach (string target in targets.Where(value => value != "proto"))
                {
                    IDataTableSettings settings = target == "hostkey" ? resolved.NetworkSettings.HostKeySettings : resolved.NetworkSettings.NetCmdSettings;
                    files.AddRange(settings.Units.Select(unit => unit.DatasExportPath).Where(path => !string.IsNullOrWhiteSpace(path)));
                }
            var directories = configurations.SelectMany(configuration => EditorUtil.Luban.ExportHelper.GetLubanCustomTemplateDirs(configuration.profileId) ?? Array.Empty<string>()).ToList();
            if (configurations.Length > 0)
            {
                string luban = EditorUtil.Luban.CliRunner.GetLubanDllPath();
                if (string.IsNullOrWhiteSpace(luban)) throw new InvalidDataException("Luban 工具未安装，不能冻结实际生成工具。");
                directories.Add(IOPath.GetDirectoryName(luban));
            }
            if (targets.Contains("proto"))
            {
                directories.Add(resolved.ProtoSettings.ProtoSourceDirPath);
                string protoc = EditorUtil.Proto.CliRunner.GetProtocPath();
                if (string.IsNullOrWhiteSpace(protoc)) throw new InvalidDataException("protoc 工具未安装，不能冻结实际生成工具。");
                files.Add(protoc);
                string include = EditorUtil.Proto.CliRunner.GetProtocIncludePath();
                if (!string.IsNullOrWhiteSpace(include)) directories.Add(include);
            }
            return DataModuleExportActionCommon.CaptureInputs(files, directories, context.CancellationToken);
        }

        /// <summary>展示配置/meta 以及数据反格式删除；代码保持现有目录边界，Proto 不增加 Luban 写入。</summary>
        private static string[] BuildCompleteWriteSet(Output[] outputs,
            DataModuleExportActionCommon.ConfigurationSnapshot[] configurations, LubanDataFormat format) =>
            DataModuleExportActionCommon.BuildWriteSet(DataModuleExportActionCommon.EnumerateFileWritePaths(outputs.Where(output => output.kind == "data")
                .SelectMany(output => new[] { output.path, IOPath.ChangeExtension(output.path, format == LubanDataFormat.Json ? ".bytes" : ".json") }))
                .Concat(DataModuleExportActionCommon.EnumerateDirectoryWritePaths(outputs.Where(output => output.kind == "code").Select(output => output.path)))
                .Concat(DataModuleExportActionCommon.EnumerateConfigurationWritePaths(configurations)));

        /// <summary>只读将新收据绑定当前 Network 设置、目标与完整输入；Proto 不因这一校验附加 Master 依赖。</summary>
        private static bool TryVerifySourceScope(Receipt receipt, AgentActionExecutionContext context, out string error)
        {
            error = "新 Network 收据的来源或目标范围不完整。";
            try
            {
                if (!NetworkSettingsResolver.TryResolve(out var resolved, out error) || resolved.ScenePath != receipt.scenePath ||
                    resolved.ComponentId != receipt.componentId || resolved.SettingsHash != receipt.settingsHash ||
                    !TryBuildOutputs(resolved, receipt.scope, receipt.targets, out Output[] outputs, out string mode, out error) ||
                    mode != receipt.developMode || !TryValidateOutputEvidence(receipt.scope, receipt.targets, outputs, receipt.outputs, out error)) return false;
                DataModuleExportActionCommon.ConfigurationSnapshot[] configurations = BuildConfigurations(resolved.NetworkSettings, outputs, receipt.targets, receipt.scope);
                if (!Newtonsoft.Json.Linq.JToken.DeepEquals(Newtonsoft.Json.Linq.JToken.FromObject(configurations),
                        receipt.configurations == null ? null : Newtonsoft.Json.Linq.JToken.FromObject(receipt.configurations)) ||
                    !DataModuleExportActionCommon.HasExactInputScope(receipt.inputSnapshot,
                        CaptureInputSnapshot(resolved, configurations, receipt.targets, receipt.scope, context)))
                { error = "新 Network 收据的 Profile、输出或完整输入范围与当前领域设置不一致。"; return false; }
                error = null;
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { error = exception.Message; return false; }
        }

        /// <summary>
        /// 统一返回空输出的计划失败结果。
        /// </summary>
        private static bool Fail(out Output[] outputs)
        {
            outputs = Array.Empty<Output>();
            return false;
        }

        /// <summary>
        /// 创建不包含可执行计划的阻断结果。
        /// </summary>
        private static Task<AgentActionHandlerPlan> Blocked(string message)
        {
            return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = message });
        }
    }

    internal static class NetworkSettingsResolver
    {
        internal sealed class Resolved
        {
            public string ScenePath;
            public string ComponentId;
            public string SettingsHash;
            public NetworkSettings NetworkSettings;
            public ProtoSettings ProtoSettings;
        }

        /// <summary>
        /// 从活动场景唯一 Nova 层级解析 Network 设置快照。
        /// </summary>
        internal static bool TryResolve(out Resolved resolved, out string error)
        {
            resolved = null;
            error = null;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                error = "活动场景必须已加载并保存，才能稳定解析 Network 设置。";
                return false;
            }
            Nova[] novas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Nova>(true)).ToArray();
            if (novas.Length != 1)
            {
                error = $"活动场景必须恰好包含一个 Nova 根节点，当前为 {novas.Length} 个。";
                return false;
            }
            NetworkComponent[] components = novas[0].GetComponentsInChildren<NetworkComponent>(true);
            if (components.Length != 1)
            {
                error = $"Nova 层级必须恰好包含一个 NetworkComponent，当前为 {components.Length} 个。";
                return false;
            }
            NetworkSettings network = Clone(components[0].NetworkSettings);
            ProtoSettings proto = Clone(components[0].ProtoSettings);
            if (network == null && proto == null)
            {
                error = "NetworkComponent 未配置 NetworkSettings 或 ProtoSettings。";
                return false;
            }
            string settingsJson = JsonUtility.ToJson(network, false) + "\n" + JsonUtility.ToJson(proto, false);
            resolved = new Resolved
            {
                ScenePath = scene.path.Replace('\\', '/'),
                ComponentId = GlobalObjectId.GetGlobalObjectIdSlow(components[0]).ToString(),
                SettingsHash = GenerateActionCommon.ComputeTextHash(settingsJson),
                NetworkSettings = network,
                ProtoSettings = proto,
            };
            return true;
        }

        /// <summary>
        /// 直接读取 Globals 与 ConfigMaster 的已导出 Runtime，避免只读 Plan 触发 WorkspaceActive 的自动推断或回写。
        /// </summary>
        internal static bool TryReadActiveDevelopMode(out DevelopMode mode, out string error)
        {
            mode = default;
            error = null;
            try
            {
                if (!EditorUtil.Config.WorkspaceActive.TryGetPersistedConfigMaster(
                        out ConfigMasterSO master, out _, out string masterPath, out error))
                {
                    return false;
                }

                ConfigRuntimeSO runtime = master.ExportTarget;
                if (runtime == null)
                {
                    string editorDirectory = IOPath.GetDirectoryName(masterPath)?.Replace('\\', '/');
                    string demoRoot = IOPath.GetDirectoryName(editorDirectory)?.Replace('\\', '/');
                    string runtimePath = string.IsNullOrWhiteSpace(demoRoot)
                        ? string.Empty
                        : demoRoot + "/Configs/ConfigRuntime.asset";
                    runtime = string.IsNullOrWhiteSpace(runtimePath)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(runtimePath);
                }
                if (runtime == null)
                {
                    error = "HostKey 导出需要 ConfigMaster 已导出的 ConfigRuntime。";
                    return false;
                }

                mode = runtime.DevelopMode;
                return true;
            }
            catch (Exception exception)
            {
                error = "HostKey 导出无法只读解析当前 ConfigRuntime：" + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 通过 Unity JSON 创建仅供执行使用的设置快照。
        /// </summary>
        private static T Clone<T>(T value) where T : class
        {
            return value == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(value, false));
        }
    }
}
