/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundExportAction.cs
 * author:    taoye
 * created:   2026/8/21
 * descrip:   Sound 精确导出 Agent Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaFramework.Editor
{
    [AgentAction("nova.project.sound.export", "导出 Sound", "sound", AgentActionOperationType.Generate,
        Description = "从当前有效 SoundSettings 导出声音运行时数据，并核验生成结果。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput, RequiresConfirmation = true, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "asset-database", "sound-export" })]
    internal sealed class SoundExportAction : AgentActionHandler<SoundExportAction.Request>
    {
        private const string c_ActionId = "nova.project.sound.export";

        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string scope;
            public string[] sourcePaths;
        }

        [Serializable]
        internal sealed class Job
        {
            public string sourcePath;
            public string dataOutputPath;
            public string codeOutputPath;
        }

        [Serializable]
        internal sealed class Receipt
        {
            public string scope;
            public string scenePath;
            public string componentId;
            public string settingsHash;
            public Job[] jobs;
            public DataModuleExportActionCommon.Output[] outputs;
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
            public SoundSettings Settings;
            public Job[] Jobs;
            public DataModuleExportActionCommon.NamespaceSourceSnapshot NamespaceSource;
            public DataModuleExportActionCommon.InputSnapshot InputSnapshot;
            public DataModuleExportActionCommon.ConfigurationSnapshot[] Configurations;
        }

        /// <summary>
        /// 校验 Sound 导出范围和可选源文件列表。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!DataModuleExportActionCommon.TryNormalizeScope(request?.scope, out _))
            {
                error = "scope 只允许 all、code 或 data。";
                return false;
            }
            return DataModuleExportActionCommon.TryValidateSourcePaths(request.sourcePaths, out error);
        }

        /// <summary>
        /// 只读冻结 Sound 设置、选中 Unit 和正式输出范围。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!SoundSettingsResolver.TryResolve(out SoundSettingsResolver.Resolved resolved, out string error)) return Blocked(error);
            if (!TryBuildSelection(resolved.Settings, request, out SoundSettings selected, out Job[] jobs, out error)) return Blocked(error);
            string scope = request.scope.Trim().ToLowerInvariant();
            string[] outputs = EnumerateOutputs(jobs, scope).ToArray();
            if (!DataModuleExportActionCommon.TryValidateOutputPaths(outputs, out error)) return Blocked(error);
            if (DataModuleExportActionCommon.IncludesCode(scope) && GeneratedExportCompilationState.IsPending()) return Blocked("已有导出编译待完成，请先 Verify 该收据。");
            if (!DataModuleExportActionCommon.TryCaptureNamespaceSource(context.CancellationToken, out var namespaceSource, out error)) return Blocked(error);
            DataModuleExportActionCommon.ConfigurationSnapshot[] configurations;
            DataModuleExportActionCommon.InputSnapshot inputSnapshot;
            string[] writeSet;
            try
            {
                configurations = BuildConfigurations(selected, jobs, scope);
                inputSnapshot = CaptureInputSnapshot(configurations, jobs, resolved.ScenePath, scope, context);
                writeSet = BuildCompleteWriteSet(configurations, jobs, scope, selected.DataFormat);
                if (!DataModuleExportActionCommon.TryVerifyNamespaceSource(namespaceSource, context.CancellationToken, out error) ||
                    !DataModuleExportActionCommon.TryVerifyInputs(inputSnapshot, context.CancellationToken, out error)) return Blocked(error);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { return Blocked(exception.Message); }

            var receipt = new Receipt { scope = scope, scenePath = resolved.ScenePath, componentId = resolved.ComponentId, settingsHash = resolved.SettingsHash, jobs = jobs, outputs = Array.Empty<DataModuleExportActionCommon.Output>(), sourceContractVersion = 1, namespaceSource = namespaceSource, inputSnapshot = inputSnapshot, configurations = configurations };
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将导出 {jobs.Length} 个 Sound Unit，范围为 {scope}。",
                DataJson = Util.Json.Serialize(receipt),
                State = new State { Scope = scope, ScenePath = resolved.ScenePath, ComponentId = resolved.ComponentId, SettingsHash = resolved.SettingsHash, Settings = selected, Jobs = jobs, NamespaceSource = namespaceSource, InputSnapshot = inputSnapshot, Configurations = configurations },
                WriteSet = writeSet,
                Evidence = new[] { "Plan 已冻结活动场景、唯一 Nova/SoundComponent、SoundSettings、Unit 与精确输出路径。" },
                RecoveryPayloadJson = Util.Json.Serialize(receipt),
            });
        }

        /// <summary>
        /// 写前核对冻结状态并调用既有 Sound Exporter；写后失败保留收据并返回 partial，禁止重放。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen)) return Result("blocked", "Sound 导出冻结状态无效。");
            if (!SoundSettingsResolver.TryResolve(out SoundSettingsResolver.Resolved current, out string error) ||
                current.ScenePath != frozen.ScenePath || current.ComponentId != frozen.ComponentId || current.SettingsHash != frozen.SettingsHash)
                return Result("blocked", error ?? "SoundComponent 或 SoundSettings 已漂移，请重新 Plan。");

            try
            {
                if (!DataModuleExportActionCommon.HasExactInputScope(frozen.InputSnapshot,
                        CaptureInputSnapshot(frozen.Configurations, frozen.Jobs, current.ScenePath, frozen.Scope, context)))
                    return Result("blocked", "实际模板或工具输入根已变化，请重新 Plan。");
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { return Result("blocked", exception.Message); }
            if (!DataModuleExportActionCommon.TryVerifyNamespaceSource(frozen.NamespaceSource, context.CancellationToken, out error) ||
                !DataModuleExportActionCommon.TryVerifyInputs(frozen.InputSnapshot, context.CancellationToken, out error))
                return Result("blocked", error);
            if (DataModuleExportActionCommon.IncludesCode(frozen.Scope) && GeneratedExportCompilationState.IsPending())
                return Result("blocked", "已有导出编译待完成，请先 Verify 该收据。");

            context.CancellationToken.ThrowIfCancellationRequested();
            bool ok = true;
            foreach (SoundUnitSetting unit in frozen.Settings.SoundUnitsSettings)
            {
                if (DataModuleExportActionCommon.IncludesData(frozen.Scope))
                    ok &= EditorUtil.Sound.Exporter.ExportData(frozen.Settings.SourceDirPath, frozen.Settings, unit, new EditorUtil.Sound.Exporter.ExportOperations());
                if (ok && DataModuleExportActionCommon.IncludesCode(frozen.Scope))
                    ok &= EditorUtil.Sound.Exporter.ExportCode(frozen.Settings.SourceDirPath, frozen.Settings, unit,
                        unit.ClassesExportPath, null, new EditorUtil.Sound.Exporter.ExportOperations());
                if (!ok) break;
            }
            if (!ok) return Result("partial", "Sound Exporter 返回失败；Action 不会自动重放。");

            // Exporter 已完成写入，先保留基础收据，再读取产物；任何后验失败都不能回到写前 blocked。
            var receipt = new Receipt
            {
                scope = frozen.Scope, scenePath = frozen.ScenePath, componentId = frozen.ComponentId,
                settingsHash = frozen.SettingsHash, jobs = frozen.Jobs,
                outputs = Array.Empty<DataModuleExportActionCommon.Output>(), sourceContractVersion = 1,
                namespaceSource = frozen.NamespaceSource, inputSnapshot = frozen.InputSnapshot,
                configurations = frozen.Configurations, configurationArtifacts = Array.Empty<GenerateActionCommon.Artifact>(),
            };
            return Task.FromResult(CompletePostExport(receipt, context));
        }

        /// <summary>
        /// 核验一次导出后的真实产物、输入范围和配置；失败或取消仍附带现有收据，不调用 Exporter 或恢复写入。
        /// </summary>
        /// <param name="receipt">Execute 在已完成导出后建立的基础收据；仅记录本次冻结范围。</param>
        /// <param name="context">产物和来源读取的取消上下文；取消不能抹去已经发生的写入。</param>
        /// <returns>包含执行收据的 partial 结果；后验通过时才安排代码编译，最终成功由 Verify 判断。</returns>
        internal static AgentActionResult CompletePostExport(Receipt receipt, AgentActionExecutionContext context)
        {
            AgentActionResult result = AgentActionResult.Create(c_ActionId, "partial", "Sound 已导出，等待 Verify 复核产物。");
            try
            {
                var outputs = new List<DataModuleExportActionCommon.Output>();
                foreach (DataModuleExportActionCommon.Output output in CaptureOutputs(receipt.jobs, receipt.scope, context))
                {
                    outputs.Add(output);
                    receipt.outputs = outputs.ToArray();
                }
                receipt.configurationArtifacts = DataModuleExportActionCommon.CaptureConfigurationArtifacts(receipt.configurations, context.CancellationToken);
                if (!DataModuleExportActionCommon.HasExactInputScope(receipt.inputSnapshot,
                        CaptureInputSnapshot(receipt.configurations, receipt.jobs, receipt.scenePath, receipt.scope, context)))
                    throw new InvalidDataException("实际模板或工具输入根已变化。");
                if (!DataModuleExportActionCommon.TryVerifyNamespaceSource(receipt.namespaceSource, context.CancellationToken, out string error) ||
                    !DataModuleExportActionCommon.TryVerifyUnchangedInputs(receipt.inputSnapshot, receipt.configurations, context.CancellationToken, out error) ||
                    !DataModuleExportActionCommon.TryVerifyExportedConfigurations(receipt.namespaceSource, receipt.configurations, context.CancellationToken, out error))
                    throw new InvalidDataException(error ?? "导出后的来源或配置证据不完整。");
                if (DataModuleExportActionCommon.IncludesCode(receipt.scope))
                {
                    receipt.compilationToken = Guid.NewGuid().ToString("N");
                    GeneratedExportCompilationState.Schedule(receipt.compilationToken, GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(receipt)));
                }
                result.Evidence.Add("Execute 仅调用现有 EditorUtil.Sound.Exporter，并记录精确输出 SHA-256。");
            }
            catch (Exception exception)
            {
                // 此处仅在领域写入完成后调用；取消也保留 partial 与执行收据，不能提示重新执行。
                result.Message = "Sound 已导出，但产物或来源取证失败；保留收据，不会重放：" + exception.Message;
            }
            result.ReceiptJson = result.DataJson = Util.Json.Serialize(receipt);
            result.Artifacts.AddRange((receipt.outputs ?? Array.Empty<DataModuleExportActionCommon.Output>())
                .Where(output => output?.artifact != null).Select(output => output.artifact.path).Distinct(StringComparer.Ordinal));
            return result;
        }

        /// <summary>
        /// 只读核对 Sound 输出覆盖范围与 SHA-256 摘要。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            string error = null;
            GenerateActionCommon.Artifact[] actual = Array.Empty<GenerateActionCommon.Artifact>();
            try { receipt = Util.Json.Deserialize<Receipt>(receiptJson); }
            catch (Exception exception) { return Result("blocked", "Sound Receipt 无法解析：" + exception.Message); }
            if (receipt?.jobs == null || receipt.jobs.Length == 0) return Result("partial", "Sound Receipt 不完整。");
            DataModuleExportActionCommon.Output[] expected = EnumerateExpectedOutputs(receipt.jobs, receipt.scope).ToArray();
            if (!DataModuleExportActionCommon.HasExactCoverage(receipt.outputs, expected)) return Result("partial", "Sound Receipt 未完整覆盖计划输出。");
            if (
                !DataModuleExportActionCommon.TryVerify(receipt.outputs, context.CancellationToken, out actual, out error))
                return Result("partial", "Sound 产物验证未通过：" + (error ?? "Receipt 不完整。"));
            bool sourceContract;
            try { sourceContract = DataModuleExportActionCommon.HasSourceContract(receiptJson); }
            catch (Exception exception) { return Result("partial", exception.Message); }
            if (sourceContract)
            {
                if (!TryVerifySourceScope(receipt, context, out error)) return Result("partial", error);
                if (receipt.sourceContractVersion != 1 || receipt.configurations == null || receipt.configurations.Length == 0 ||
                    !DataModuleExportActionCommon.TryVerifyNamespaceSource(receipt.namespaceSource, context.CancellationToken, out error) ||
                    !DataModuleExportActionCommon.TryVerifyUnchangedInputs(receipt.inputSnapshot, receipt.configurations, context.CancellationToken, out error) ||
                    !DataModuleExportActionCommon.TryVerifyConfigurationArtifacts(receipt.configurations, receipt.configurationArtifacts, context.CancellationToken, out error) ||
                    !DataModuleExportActionCommon.TryVerifyExportedConfigurations(receipt.namespaceSource, receipt.configurations, context.CancellationToken, out error))
                    return Result("partial", error ?? "新导出收据缺少完整来源或配置证据。");
                if (DataModuleExportActionCommon.IncludesCode(receipt.scope) &&
                    (!GeneratedExportCompilationState.TryVerify(receipt.compilationToken, GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(receipt)), true, out error) ||
                     !DataModuleExportActionCommon.TryVerifyLoadedTypes(receipt.namespaceSource, receipt.configurations, out error)))
                    return Result("partial", error);
            }
            AgentActionResult success = AgentActionResult.Create(c_ActionId, "success", "Sound 输出范围与 SHA-256 已只读核对。");
            success.EvidenceKinds = (sourceContract && DataModuleExportActionCommon.IncludesCode(receipt.scope)
                ? AgentActionEvidence.Static | AgentActionEvidence.Compile : AgentActionEvidence.Static) | AgentActionEvidence.Artifact;
            if (!sourceContract) success.Warnings.Add("缺命名空间/输入快照，仅历史产物摘要；不证明新契约。");
            success.DataJson = Util.Json.Serialize(receipt.outputs);
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path));
            success.Evidence.Add("Verify 未调用 Exporter 或任何写入 API。");
            return Task.FromResult(success);
        }

        /// <summary>
        /// 根据请求筛选 Sound Unit，并生成与原设置隔离的执行快照。
        /// </summary>
        internal static bool TryBuildSelection(SoundSettings settings, Request request, out SoundSettings selected, out Job[] jobs, out string error)
        {
            selected = null; jobs = Array.Empty<Job>(); error = null;
            if (!DataModuleExportActionCommon.TryNormalizeScope(request?.scope, out string scope) || settings?.SoundUnitsSettings == null || settings.SoundUnitsSettings.Count == 0)
            { error = "SoundSettings、scope 或 Unit 无效。"; return false; }
            if (string.IsNullOrWhiteSpace(settings.SourceDirPath) || !Directory.Exists(settings.SourceDirPath))
            { error = "Sound 源目录不存在。"; return false; }
            if (settings.SoundUnitsSettings.Any(unit => unit == null || string.IsNullOrWhiteSpace(unit.SourcePath)) ||
                settings.SoundUnitsSettings.GroupBy(unit => unit.SourcePath, StringComparer.Ordinal).Any(group => group.Count() != 1))
            { error = "Sound Unit SourcePath 必须非空且唯一。"; return false; }
            HashSet<string> requested = request.sourcePaths == null || request.sourcePaths.Length == 0 ? null : new HashSet<string>(request.sourcePaths, StringComparer.Ordinal);
            List<SoundUnitSetting> units = settings.SoundUnitsSettings.Where(unit => requested == null || requested.Contains(unit.SourcePath)).ToList();
            if (requested != null && units.Count != requested.Count)
            { error = "请求包含不存在的 Sound Unit：" + string.Join(", ", requested.Except(units.Select(unit => unit.SourcePath))); return false; }
            foreach (SoundUnitSetting unit in units)
            {
                if (!DataModuleExportActionCommon.TryValidateSourceFile(settings.SourceDirPath, unit.SourcePath, out error)) return false;
            }
            selected = JsonUtility.FromJson<SoundSettings>(JsonUtility.ToJson(settings));
            selected.SoundUnitsSettings = selected.SoundUnitsSettings.Where(unit => units.Any(source => source.SourcePath == unit.SourcePath)).ToList();
            jobs = selected.SoundUnitsSettings.Select(unit => new Job { sourcePath = unit.SourcePath, dataOutputPath = unit.DatasExportPath, codeOutputPath = unit.ClassesExportPath }).ToArray();
            return ValidateJobs(jobs, scope, out error);
        }

        /// <summary>
        /// 校验每个导出任务具备当前范围要求的输出路径。
        /// </summary>
        private static bool ValidateJobs(Job[] jobs, string scope, out string error)
        {
            error = null;
            foreach (Job job in jobs)
            {
                if (DataModuleExportActionCommon.IncludesData(scope) && string.IsNullOrWhiteSpace(job.dataOutputPath)) { error = $"Sound Unit '{job.sourcePath}' 缺少数据输出路径。"; return false; }
                if (DataModuleExportActionCommon.IncludesCode(scope) && string.IsNullOrWhiteSpace(job.codeOutputPath)) { error = $"Sound Unit '{job.sourcePath}' 缺少代码输出路径。"; return false; }
            }
            return true;
        }

        /// <summary>按精确 Unit/代码目录记录 Sound 固定 Profile；当前逐 Unit 入口不发布全量 output-manifest。</summary>
        private static DataModuleExportActionCommon.ConfigurationSnapshot[] BuildConfigurations(SoundSettings settings, Job[] jobs, string scope) =>
            jobs.GroupBy(job => DataModuleExportActionCommon.IncludesCode(scope) ? job.codeOutputPath : string.Empty, StringComparer.Ordinal)
                .Select(group => DataModuleExportActionCommon.CreateConfigurationSnapshot(EditorUtil.Luban.LubanExportProfiles.Sound,
                    settings.SourceDirPath, group.Key, group.Select(job => job.sourcePath), DataModuleExportActionCommon.IncludesCode(scope), false)).ToArray();

        /// <summary>冻结源 Excel、代码模式读取的数据、既有配置和真实模板/Luban 目录；不运行环境检测。</summary>
        internal static DataModuleExportActionCommon.InputSnapshot CaptureInputSnapshot(
            DataModuleExportActionCommon.ConfigurationSnapshot[] configurations, Job[] jobs, string scenePath, string scope, AgentActionExecutionContext context)
        {
            IEnumerable<string> files = new[] { scenePath }.Concat(configurations.SelectMany(DataModuleExportActionCommon.ConfigurationFiles))
                .Concat(configurations.SelectMany(configuration => configuration.sourcePaths.Select(source => Path.Combine(configuration.sourceDirectory, source))))
                .Concat(scope == "code" ? jobs.Select(job => job.dataOutputPath).Where(path => !string.IsNullOrWhiteSpace(path)) : Array.Empty<string>());
            var directories = (EditorUtil.Luban.ExportHelper.GetLubanCustomTemplateDirs(EditorUtil.Luban.LubanExportProfiles.Sound.TemplateKey) ?? Array.Empty<string>()).ToList();
            string luban = EditorUtil.Luban.CliRunner.GetLubanDllPath();
            if (string.IsNullOrWhiteSpace(luban)) throw new InvalidDataException("Luban 工具未安装，不能冻结实际生成工具。");
            directories.Add(Path.GetDirectoryName(luban));
            return DataModuleExportActionCommon.CaptureInputs(files, directories, context.CancellationToken);
        }

        /// <summary>包含数据反格式删除、目录代码写入及配置/meta 副作用，避免实际 Exporter 超出展示写集。</summary>
        private static string[] BuildCompleteWriteSet(DataModuleExportActionCommon.ConfigurationSnapshot[] configurations, Job[] jobs, string scope, LubanDataFormat format)
        {
            IEnumerable<string> filePaths = DataModuleExportActionCommon.IncludesData(scope)
                ? jobs.SelectMany(job => new[] { job.dataOutputPath, Path.ChangeExtension(job.dataOutputPath, format == LubanDataFormat.Json ? ".bytes" : ".json") })
                : Array.Empty<string>();
            return DataModuleExportActionCommon.BuildWriteSet(DataModuleExportActionCommon.EnumerateFileWritePaths(filePaths)
                .Concat(DataModuleExportActionCommon.IncludesCode(scope) ? DataModuleExportActionCommon.EnumerateDirectoryWritePaths(jobs.Select(job => job.codeOutputPath)) : Array.Empty<string>())
                .Concat(DataModuleExportActionCommon.EnumerateConfigurationWritePaths(configurations)));
        }

        /// <summary>
        /// 枚举当前范围将写入的正式输出路径。
        /// </summary>
        private static IEnumerable<string> EnumerateOutputs(IEnumerable<Job> jobs, string scope)
        {
            if (DataModuleExportActionCommon.IncludesData(scope)) foreach (Job job in jobs) yield return job.dataOutputPath;
            if (DataModuleExportActionCommon.IncludesCode(scope)) foreach (string path in jobs.Select(job => job.codeOutputPath).Distinct(StringComparer.Ordinal)) yield return path;
        }

        /// <summary>
        /// 捕获执行完成后的 Sound 产物摘要。
        /// </summary>
        private static IEnumerable<DataModuleExportActionCommon.Output> CaptureOutputs(IEnumerable<Job> jobs, string scope, AgentActionExecutionContext context)
        {
            if (DataModuleExportActionCommon.IncludesData(scope)) foreach (Job job in jobs) yield return DataModuleExportActionCommon.Capture("data", job.sourcePath, job.dataOutputPath, context.CancellationToken);
            if (DataModuleExportActionCommon.IncludesCode(scope)) foreach (string path in jobs.Select(job => job.codeOutputPath).Distinct(StringComparer.Ordinal)) yield return DataModuleExportActionCommon.Capture("code", "*", path, context.CancellationToken);
        }

        /// <summary>
        /// 枚举 Verify 应精确覆盖的 Sound 产物身份。
        /// </summary>
        private static IEnumerable<DataModuleExportActionCommon.Output> EnumerateExpectedOutputs(IEnumerable<Job> jobs, string scope)
        {
            if (DataModuleExportActionCommon.IncludesData(scope)) foreach (Job job in jobs) yield return new DataModuleExportActionCommon.Output { kind = "data", sourcePath = job.sourcePath, path = job.dataOutputPath };
            if (DataModuleExportActionCommon.IncludesCode(scope)) foreach (string path in jobs.Select(job => job.codeOutputPath).Distinct(StringComparer.Ordinal)) yield return new DataModuleExportActionCommon.Output { kind = "code", sourcePath = "*", path = path };
        }

        /// <summary>将新收据重新绑定当前组件、设置及完整来源路径；不执行导出或写回工作区。</summary>
        private static bool TryVerifySourceScope(Receipt receipt, AgentActionExecutionContext context, out string error)
        {
            error = "新收据缺少完整组件或输入范围。";
            try
            {
                if (receipt.jobs == null || receipt.jobs.Any(job => job == null) ||
                    !SoundSettingsResolver.TryResolve(out var resolved, out error) || resolved.ScenePath != receipt.scenePath ||
                    resolved.ComponentId != receipt.componentId || resolved.SettingsHash != receipt.settingsHash ||
                    !TryBuildSelection(resolved.Settings, new Request { scope = receipt.scope, sourcePaths = receipt.jobs.Select(job => job.sourcePath).ToArray() },
                        out var selected, out Job[] jobs, out error)) return false;
                DataModuleExportActionCommon.ConfigurationSnapshot[] configurations = BuildConfigurations(selected, jobs, receipt.scope);
                if (!Newtonsoft.Json.Linq.JToken.DeepEquals(Newtonsoft.Json.Linq.JToken.FromObject(jobs), Newtonsoft.Json.Linq.JToken.FromObject(receipt.jobs)) ||
                    !Newtonsoft.Json.Linq.JToken.DeepEquals(Newtonsoft.Json.Linq.JToken.FromObject(configurations),
                        receipt.configurations == null ? null : Newtonsoft.Json.Linq.JToken.FromObject(receipt.configurations)) ||
                    !DataModuleExportActionCommon.HasExactInputScope(receipt.inputSnapshot,
                        CaptureInputSnapshot(configurations, jobs, resolved.ScenePath, receipt.scope, context)))
                { error = "新收据的 Profile、Unit、代码目录或完整输入范围与当前领域设置不一致。"; return false; }
                error = null;
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { error = exception.Message; return false; }
        }

        /// <summary>
        /// 创建不包含可执行计划的阻断结果。
        /// </summary>
        private static Task<AgentActionHandlerPlan> Blocked(string error) => Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = error });

        /// <summary>
        /// 创建不附加伪证据的执行或验证结果。
        /// </summary>
        private static Task<AgentActionResult> Result(string status, string message) => Task.FromResult(AgentActionResult.Create(c_ActionId, status, message));
    }

    internal static class SoundSettingsResolver
    {
        [Serializable] private sealed class Snapshot { public SoundSettings m_Settings = null; }
        internal sealed class Resolved { public string ScenePath; public string ComponentId; public string SettingsHash; public SoundSettings Settings; }

        /// <summary>
        /// 从已保存场景的唯一 SoundComponent 严格解包 m_Settings，只读冻结声音设置快照。
        /// </summary>
        internal static bool TryResolve(out Resolved resolved, out string error)
        {
            resolved = null; error = null;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path)) { error = "活动场景必须已加载并保存。"; return false; }
            Nova[] novas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Nova>(true)).ToArray();
            if (novas.Length != 1) { error = $"活动场景必须恰好包含一个 Nova 根节点，当前为 {novas.Length} 个。"; return false; }
            SoundComponent[] components = novas[0].GetComponentsInChildren<SoundComponent>(true);
            if (components.Length != 1) { error = $"Nova 层级必须恰好包含一个 SoundComponent，当前为 {components.Length} 个。"; return false; }
            if (!EditorObjectJson.TryReadSnapshot(EditorJsonUtility.ToJson(components[0], false), nameof(SoundComponent), "m_Settings", out Snapshot snapshot, out error)) return false;
            if (snapshot?.m_Settings == null) { error = "SoundComponent.m_Settings 未配置。"; return false; }
            string json = JsonUtility.ToJson(snapshot.m_Settings, false);
            resolved = new Resolved { ScenePath = scene.path.Replace('\\', '/'), ComponentId = GlobalObjectId.GetGlobalObjectIdSlow(components[0]).ToString(), SettingsHash = GenerateActionCommon.ComputeTextHash(json), Settings = JsonUtility.FromJson<SoundSettings>(json) };
            return true;
        }
    }
}
