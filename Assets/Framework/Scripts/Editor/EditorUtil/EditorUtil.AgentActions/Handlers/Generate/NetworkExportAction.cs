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
            public string scope;
            public string scenePath;
            public string componentId;
            public string developMode;
            public string[] targets;
            public Output[] outputs;
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
        }

        /// <summary>
        /// 校验导出范围及 HostKey、NetCmd、Proto 目标组合。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (!TryNormalizeScope(request?.scope, out _))
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
            if (string.Equals(request.scope, "data", StringComparison.OrdinalIgnoreCase) && targets.Contains("proto"))
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

            string[] writeSet = outputs.Select(output => output.kind == "data" ? output.path : output.path.TrimEnd('/') + "/**")
                .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
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

            Receipt receipt = CreateReceipt(frozen, captured);
            AgentActionResult result = AgentActionResult.Create(c_ActionId, "partial", "Network 已导出，等待 Verify 核对产物摘要。");
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.DataJson = result.ReceiptJson;
            result.Artifacts.AddRange(captured.Select(output => output.artifact.path).Distinct(StringComparer.Ordinal));
            result.Evidence.Add("Execute 仅调用现有 Network HostKey/NetCmd/Proto Exporter，并记录输出 SHA-256。");
            if (captured.Any(output => output.kind == "code"))
            {
                result.Warnings.Add("代码产物摘要不等于 Unity 编译成功；domain reload 后应继续 Verify。");
            }
            return Task.FromResult(result);
        }

        /// <summary>
        /// 只读核对 Network 导出产物与 SHA-256 摘要。
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
            if (receipt?.targets == null || receipt.targets.Length == 0 || receipt.outputs == null || receipt.outputs.Length == 0 ||
                receipt.outputs.Any(output => output?.artifact == null))
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial", "Network Export Receipt 尚无完整产物证据。"));
            }
            if (!GenerateActionCommon.TryVerifyArtifacts(receipt.outputs.Select(output => output.artifact),
                    context.CancellationToken, out GenerateActionCommon.Artifact[] actual, out string error))
            {
                AgentActionResult partial = AgentActionResult.Create(c_ActionId, "partial", "Network 导出产物验证未通过：" + error);
                partial.DataJson = Util.Json.Serialize(actual);
                return Task.FromResult(partial);
            }

            AgentActionResult success = AgentActionResult.Create(c_ActionId, "success", "Network 输出范围与 SHA-256 已只读核对。");
            success.EvidenceKinds = AgentActionEvidence.Static | AgentActionEvidence.Artifact;
            success.DataJson = Util.Json.Serialize(receipt.outputs);
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path).Distinct(StringComparer.Ordinal));
            success.Evidence.AddRange(receipt.outputs.Select(output =>
                $"{output.target}/{output.kind}：{output.artifact.fileCount} 个文件，SHA-256={output.artifact.sha256}"));
            success.Evidence.Add("Verify 未调用 Exporter、AssetDatabase.Refresh 或任何恢复写入。");
            return Task.FromResult(success);
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
            if (target == "proto") return EditorUtil.Network.ProtoExporter.ExportAllProtos(proto);
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
        /// 从冻结状态和实际产物创建恢复 Receipt。
        /// </summary>
        private static Receipt CreateReceipt(State state, Output[] outputs)
        {
            return new Receipt
            {
                scope = state.Scope, scenePath = state.ScenePath, componentId = state.ComponentId,
                developMode = state.DevelopMode, targets = state.Targets, outputs = outputs,
            };
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
                string projectRoot = IOPath.GetFullPath(IOPath.GetDirectoryName(Application.dataPath) ?? Application.dataPath);
                string globalsPath = IOPath.Combine(projectRoot, "ProjectSettings/Nova/Globals.json");
                if (!File.Exists(globalsPath))
                {
                    error = "HostKey 导出需要已显式激活的 ConfigMaster；Globals.json 不存在。";
                    return false;
                }

                GenerateActionCommon.ActiveMasterFile active =
                    JsonUtility.FromJson<GenerateActionCommon.ActiveMasterFile>(File.ReadAllText(globalsPath));
                if (active == null || string.IsNullOrWhiteSpace(active.configMasterGuid))
                {
                    error = "Globals.json 未声明激活 ConfigMaster GUID。";
                    return false;
                }
                ConfigMasterSO master = GenerateActionCommon.ResolveMaster(active.configMasterGuid, out string masterPath);
                if (master == null)
                {
                    error = "HostKey 导出需要 Globals.json 绑定可加载的 ConfigMaster。";
                    return false;
                }
                if (!GenerateActionCommon.TryValidateActiveMasterBinding(active.configMasterGuid, masterPath, out error))
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
