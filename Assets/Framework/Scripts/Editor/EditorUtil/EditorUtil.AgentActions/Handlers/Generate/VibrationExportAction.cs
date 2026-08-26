/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrationExportAction.cs
 * author:    taoye
 * created:   2026/8/21
 * descrip:   Vibrate 单区域精确导出 Agent Action
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
    [AgentAction("nova.project.vibration.export", "导出 Vibration", "vibration", AgentActionOperationType.Generate,
        Description = "从当前有效 VibrateSettings 导出振动运行时数据，并核验生成结果。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput, RequiresConfirmation = true, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "asset-database", "vibration-export" })]
    internal sealed class VibrationExportAction : AgentActionHandler<VibrationExportAction.Request>
    {
        private const string c_ActionId = "nova.project.vibration.export";

        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string area;
            [AgentActionRequired] public string scope;
            public string[] sourcePaths;
        }

        [Serializable]
        internal sealed class Job
        {
            public string area;
            public string sourcePath;
            public string dataOutputPath;
            public string codeOutputPath;
        }

        [Serializable]
        private sealed class Receipt
        {
            public string area;
            public string scope;
            public string scenePath;
            public string componentId;
            public Job[] jobs;
            public DataModuleExportActionCommon.Output[] outputs;
        }

        private sealed class State
        {
            public string Area;
            public string Scope;
            public string ScenePath;
            public string ComponentId;
            public string SettingsHash;
            public VibrateSettings Settings;
            public Job[] Jobs;
        }

        /// <summary>
        /// 校验振动区域、导出范围和可选源文件列表。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!TryNormalizeArea(request?.area, out _)) { error = "area 只允许 emphasis 或 custom。"; return false; }
            if (!DataModuleExportActionCommon.TryNormalizeScope(request.scope, out _)) { error = "scope 只允许 all、code 或 data。"; return false; }
            return DataModuleExportActionCommon.TryValidateSourcePaths(request.sourcePaths, out error);
        }

        /// <summary>
        /// 只读冻结振动设置、选中 Unit 和正式输出范围。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!VibrationSettingsResolver.TryResolve(out VibrationSettingsResolver.Resolved resolved, out string error)) return Blocked(error);
            if (!TryBuildSelection(resolved.Settings, request, out VibrateSettings selected, out Job[] jobs, out error)) return Blocked(error);
            string area = request.area.Trim().ToLowerInvariant();
            string scope = request.scope.Trim().ToLowerInvariant();
            string[] outputs = EnumerateOutputs(jobs, scope).ToArray();
            if (!DataModuleExportActionCommon.TryValidateOutputPaths(outputs, out error)) return Blocked(error);
            var receipt = new Receipt { area = area, scope = scope, scenePath = resolved.ScenePath, componentId = resolved.ComponentId, jobs = jobs, outputs = Array.Empty<DataModuleExportActionCommon.Output>() };
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将导出 {jobs.Length} 个 Vibrate {area} Unit，范围为 {scope}。",
                DataJson = Util.Json.Serialize(receipt),
                State = new State { Area = area, Scope = scope, ScenePath = resolved.ScenePath, ComponentId = resolved.ComponentId, SettingsHash = resolved.SettingsHash, Settings = selected, Jobs = jobs },
                WriteSet = DataModuleExportActionCommon.BuildWriteSet(outputs),
                Evidence = new[] { "Plan 已冻结活动场景、唯一 Nova/VibrateComponent、单一区域、Unit 与精确输出路径。" },
                RecoveryPayloadJson = Util.Json.Serialize(receipt),
            });
        }

        /// <summary>
        /// 在冻结状态未漂移时调用既有 Vibrate Exporter。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen)) return Result("blocked", "Vibration 导出冻结状态无效。");
            if (!VibrationSettingsResolver.TryResolve(out VibrationSettingsResolver.Resolved current, out string error) ||
                current.ScenePath != frozen.ScenePath || current.ComponentId != frozen.ComponentId || current.SettingsHash != frozen.SettingsHash)
                return Result("blocked", error ?? "VibrateComponent 或 VibrateSettings 已漂移，请重新 Plan。");

            context.CancellationToken.ThrowIfCancellationRequested();
            bool emphasis = frozen.Area == "emphasis";
            List<VibrateUnitSetting> units = emphasis ? frozen.Settings.EmphasisUnitsSettings : frozen.Settings.CustomUnitsSettings;
            string sourceDir = emphasis ? frozen.Settings.EmphasisSourceDirPath : frozen.Settings.CustomSourceDirPath;
            bool ok = true;
            foreach (VibrateUnitSetting unit in units)
            {
                string sourceFile = IOPath.Combine(sourceDir, unit.SourcePath);
                if (DataModuleExportActionCommon.IncludesData(frozen.Scope))
                    ok &= emphasis
                        ? EditorUtil.Vibrate.ExportEmphasisData(sourceFile, unit.DatasExportPath, frozen.Settings, new EditorUtil.Vibrate.ExportOperations())
                        : EditorUtil.Vibrate.ExportCustomData(sourceFile, unit.DatasExportPath, frozen.Settings, new EditorUtil.Vibrate.ExportOperations());
                if (ok && DataModuleExportActionCommon.IncludesCode(frozen.Scope))
                    ok &= emphasis
                        ? EditorUtil.Vibrate.ExportEmphasisCode(sourceFile, unit.ClassesExportPath, frozen.Settings, new EditorUtil.Vibrate.ExportOperations())
                        : EditorUtil.Vibrate.ExportCustomCode(sourceFile, unit.ClassesExportPath, frozen.Settings, new EditorUtil.Vibrate.ExportOperations());
                if (!ok) break;
            }
            if (!ok) return Result("partial", "Vibrate Exporter 返回失败；Action 不会自动重放。");

            try
            {
                DataModuleExportActionCommon.Output[] outputs = CaptureOutputs(frozen.Jobs, frozen.Scope, context).ToArray();
                var receipt = new Receipt { area = frozen.Area, scope = frozen.Scope, scenePath = frozen.ScenePath, componentId = frozen.ComponentId, jobs = frozen.Jobs, outputs = outputs };
                AgentActionResult result = AgentActionResult.Create(c_ActionId, "partial", "Vibration 已导出，等待 Verify 复核产物。");
                result.ReceiptJson = result.DataJson = Util.Json.Serialize(receipt);
                result.Artifacts.AddRange(outputs.Select(output => output.artifact.path).Distinct(StringComparer.Ordinal));
                result.Evidence.Add("Execute 仅调用现有 EditorUtil.Vibrate 单区域 Exporter，并记录精确输出 SHA-256。");
                return Task.FromResult(result);
            }
            catch (Exception exception) { return Result("partial", "Vibration 已导出，但产物取证失败：" + exception.Message); }
        }

        /// <summary>
        /// 只读核对振动输出覆盖范围与 SHA-256 摘要。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            string error = null;
            GenerateActionCommon.Artifact[] actual = Array.Empty<GenerateActionCommon.Artifact>();
            try { receipt = Util.Json.Deserialize<Receipt>(receiptJson); }
            catch (Exception exception) { return Result("blocked", "Vibration Receipt 无法解析：" + exception.Message); }
            if (receipt?.jobs == null || receipt.jobs.Length == 0) return Result("partial", "Vibration Receipt 不完整。");
            DataModuleExportActionCommon.Output[] expected = EnumerateExpectedOutputs(receipt.jobs, receipt.scope).ToArray();
            if (!DataModuleExportActionCommon.HasExactCoverage(receipt.outputs, expected)) return Result("partial", "Vibration Receipt 未完整覆盖计划输出。");
            if (
                !DataModuleExportActionCommon.TryVerify(receipt.outputs, context.CancellationToken, out actual, out error))
                return Result("partial", "Vibration 产物验证未通过：" + (error ?? "Receipt 不完整。"));
            AgentActionResult success = AgentActionResult.Create(c_ActionId, "success", "Vibration 输出范围与 SHA-256 已只读核对。");
            success.EvidenceKinds = AgentActionEvidence.Static | AgentActionEvidence.Artifact;
            success.DataJson = Util.Json.Serialize(receipt.outputs);
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path));
            success.Evidence.Add("Verify 未调用 Exporter 或任何写入 API。");
            return Task.FromResult(success);
        }

        /// <summary>
        /// 将振动区域规范为 emphasis 或 custom。
        /// </summary>
        internal static bool TryNormalizeArea(string value, out string area)
        {
            area = value?.Trim().ToLowerInvariant();
            return area == "emphasis" || area == "custom";
        }

        /// <summary>
        /// 根据请求筛选振动 Unit，并生成与原设置隔离的执行快照。
        /// </summary>
        internal static bool TryBuildSelection(VibrateSettings settings, Request request, out VibrateSettings selected, out Job[] jobs, out string error)
        {
            selected = null; jobs = Array.Empty<Job>(); error = null;
            if (!TryNormalizeArea(request?.area, out string area) || !DataModuleExportActionCommon.TryNormalizeScope(request.scope, out string scope) || settings == null)
            { error = "VibrateSettings、area 或 scope 无效。"; return false; }
            List<VibrateUnitSetting> all = area == "emphasis" ? settings.EmphasisUnitsSettings : settings.CustomUnitsSettings;
            string sourceDir = area == "emphasis" ? settings.EmphasisSourceDirPath : settings.CustomSourceDirPath;
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir) || all == null || all.Count == 0)
            { error = $"Vibrate {area} 源目录或 Unit 无效。"; return false; }
            if (all.Any(unit => unit == null || string.IsNullOrWhiteSpace(unit.SourcePath)) || all.GroupBy(unit => unit.SourcePath, StringComparer.Ordinal).Any(group => group.Count() != 1))
            { error = $"Vibrate {area} Unit SourcePath 必须非空且唯一。"; return false; }
            HashSet<string> requested = request.sourcePaths == null || request.sourcePaths.Length == 0 ? null : new HashSet<string>(request.sourcePaths, StringComparer.Ordinal);
            List<VibrateUnitSetting> units = all.Where(unit => requested == null || requested.Contains(unit.SourcePath)).ToList();
            if (requested != null && units.Count != requested.Count)
            { error = $"请求包含不存在的 Vibrate {area} Unit：" + string.Join(", ", requested.Except(units.Select(unit => unit.SourcePath))); return false; }
            foreach (VibrateUnitSetting unit in units)
            {
                if (!DataModuleExportActionCommon.TryValidateSourceFile(sourceDir, unit.SourcePath, out error)) return false;
            }
            selected = JsonUtility.FromJson<VibrateSettings>(JsonUtility.ToJson(settings));
            if (area == "emphasis") selected.EmphasisUnitsSettings = selected.EmphasisUnitsSettings.Where(unit => units.Any(source => source.SourcePath == unit.SourcePath)).ToList();
            else selected.CustomUnitsSettings = selected.CustomUnitsSettings.Where(unit => units.Any(source => source.SourcePath == unit.SourcePath)).ToList();
            List<VibrateUnitSetting> chosen = area == "emphasis" ? selected.EmphasisUnitsSettings : selected.CustomUnitsSettings;
            jobs = chosen.Select(unit => new Job { area = area, sourcePath = unit.SourcePath, dataOutputPath = unit.DatasExportPath, codeOutputPath = unit.ClassesExportPath }).ToArray();
            foreach (Job job in jobs)
            {
                if (DataModuleExportActionCommon.IncludesData(scope) && string.IsNullOrWhiteSpace(job.dataOutputPath)) { error = $"Vibrate Unit '{job.sourcePath}' 缺少数据输出路径。"; return false; }
                if (DataModuleExportActionCommon.IncludesCode(scope) && string.IsNullOrWhiteSpace(job.codeOutputPath)) { error = $"Vibrate Unit '{job.sourcePath}' 缺少代码输出路径。"; return false; }
            }
            return true;
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
        /// 捕获执行完成后的振动产物摘要。
        /// </summary>
        private static IEnumerable<DataModuleExportActionCommon.Output> CaptureOutputs(IEnumerable<Job> jobs, string scope, AgentActionExecutionContext context)
        {
            if (DataModuleExportActionCommon.IncludesData(scope)) foreach (Job job in jobs) yield return DataModuleExportActionCommon.Capture("data", job.sourcePath, job.dataOutputPath, context.CancellationToken);
            if (DataModuleExportActionCommon.IncludesCode(scope)) foreach (string path in jobs.Select(job => job.codeOutputPath).Distinct(StringComparer.Ordinal)) yield return DataModuleExportActionCommon.Capture("code", "*", path, context.CancellationToken);
        }

        /// <summary>
        /// 枚举 Verify 应精确覆盖的振动产物身份。
        /// </summary>
        private static IEnumerable<DataModuleExportActionCommon.Output> EnumerateExpectedOutputs(IEnumerable<Job> jobs, string scope)
        {
            if (DataModuleExportActionCommon.IncludesData(scope)) foreach (Job job in jobs) yield return new DataModuleExportActionCommon.Output { kind = "data", sourcePath = job.sourcePath, path = job.dataOutputPath };
            if (DataModuleExportActionCommon.IncludesCode(scope)) foreach (string path in jobs.Select(job => job.codeOutputPath).Distinct(StringComparer.Ordinal)) yield return new DataModuleExportActionCommon.Output { kind = "code", sourcePath = "*", path = path };
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

    internal static class VibrationSettingsResolver
    {
        [Serializable] private sealed class Snapshot { public VibrateSettings m_Settings = null; }
        internal sealed class Resolved { public string ScenePath; public string ComponentId; public string SettingsHash; public VibrateSettings Settings; }

        /// <summary>
        /// 从活动场景唯一 Nova 层级解析 Vibrate 设置快照。
        /// </summary>
        internal static bool TryResolve(out Resolved resolved, out string error)
        {
            resolved = null; error = null;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path)) { error = "活动场景必须已加载并保存。"; return false; }
            Nova[] novas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Nova>(true)).ToArray();
            if (novas.Length != 1) { error = $"活动场景必须恰好包含一个 Nova 根节点，当前为 {novas.Length} 个。"; return false; }
            VibrateComponent[] components = novas[0].GetComponentsInChildren<VibrateComponent>(true);
            if (components.Length != 1) { error = $"Nova 层级必须恰好包含一个 VibrateComponent，当前为 {components.Length} 个。"; return false; }
            Snapshot snapshot = JsonUtility.FromJson<Snapshot>(EditorJsonUtility.ToJson(components[0], false));
            if (snapshot?.m_Settings == null) { error = "VibrateComponent.m_Settings 未配置。"; return false; }
            string json = JsonUtility.ToJson(snapshot.m_Settings, false);
            resolved = new Resolved { ScenePath = scene.path.Replace('\\', '/'), ComponentId = GlobalObjectId.GetGlobalObjectIdSlow(components[0]).ToString(), SettingsHash = GenerateActionCommon.ComputeTextHash(json), Settings = JsonUtility.FromJson<VibrateSettings>(json) };
            return true;
        }
    }
}
