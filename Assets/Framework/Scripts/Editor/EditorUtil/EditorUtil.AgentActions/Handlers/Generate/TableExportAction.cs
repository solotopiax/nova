/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableExportAction.cs
 * author:    taoye
 * created:   2026/8/21
 * descrip:   Table Luban 精确导出 Agent Action
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

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.table.export",
        "导出 Table",
        "table",
        AgentActionOperationType.Generate,
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        Locks = new[] { "unity-editor", "asset-database", "table-export" })]
    internal sealed class TableExportAction : AgentActionHandler<TableExportAction.Request>
    {
        private const string c_ActionId = "nova.project.table.export";

        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string scope;
            public string projectId;
            public string[] descriptionIds;
        }

        [Serializable]
        internal sealed class Job
        {
            public string projectId;
            public string projectName;
            public string descriptionId;
            public string descriptionName;
            public string qualifiedDescriptionId;
            public string codeOutputPath;
            public string dataOutputPath;
            public bool exportsCode;
            public bool exportsData;
        }

        [Serializable]
        private sealed class OutputEvidence
        {
            public string kind;
            public string projectId;
            public string descriptionId;
            public GenerateActionCommon.Artifact artifact;
        }

        [Serializable]
        private sealed class Receipt
        {
            public string scope;
            public string scenePath;
            public string componentId;
            public Job[] jobs;
            public OutputEvidence[] outputs;
        }

        private sealed class State
        {
            public string Scope;
            public string ScenePath;
            public string ComponentId;
            public string SettingsHash;
            public TableSettings Settings;
            public Job[] Jobs;
        }

        /// <summary>
        /// 校验 scope 与可选 Project/Description 标识，避免把含糊范围带入 Plan。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (!TryNormalizeScope(request?.scope, out _))
            {
                error = "scope 只允许 all、code 或 data。";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(request.projectId) &&
                (request.projectId.Length > 128 || request.projectId.IndexOf('\0') >= 0))
            {
                error = "projectId 必须为空或长度不超过 128 的非空标识。";
                return false;
            }
            if (request.descriptionIds != null &&
                (request.descriptionIds.Length > 128 || request.descriptionIds.Any(id => !IsIdentifier(id))))
            {
                error = "descriptionIds 最多 128 项，且每项必须是长度不超过 256 的非空标识。";
                return false;
            }
            if (request.descriptionIds != null &&
                request.descriptionIds.Distinct(StringComparer.Ordinal).Count() != request.descriptionIds.Length)
            {
                error = "descriptionIds 不能包含重复项。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 解析活动场景的唯一 Nova/TableComponent，并冻结实际 Project、导出描述与输出目录。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TableSettingsResolver.TryResolve(out TableSettingsResolver.Resolved resolved, out string resolveError))
            {
                return Blocked(resolveError);
            }
            if (!TryBuildSelection(resolved.Settings, request, out TableSettings selectedSettings,
                    out Job[] jobs, out string selectionError))
            {
                return Blocked(selectionError);
            }

            foreach (Job job in jobs)
            {
                foreach (string outputPath in EnumerateJobOutputPaths(job))
                {
                    if (!GenerateActionCommon.TryResolveProjectPath(outputPath, "Table 输出目录", out _, out string pathError))
                    {
                        return Blocked(pathError);
                    }
                }
            }

            string[] writeSet = jobs.SelectMany(EnumerateJobOutputPaths)
                .Select(path => Normalize(path).TrimEnd('/') + "/**")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            string scope = request.scope.ToLowerInvariant();
            var state = new State
            {
                Scope = scope,
                ScenePath = resolved.ScenePath,
                ComponentId = resolved.ComponentId,
                SettingsHash = resolved.SettingsHash,
                Settings = selectedSettings,
                Jobs = jobs,
            };
            var receipt = new Receipt
            {
                scope = scope,
                scenePath = resolved.ScenePath,
                componentId = resolved.ComponentId,
                jobs = jobs,
                outputs = Array.Empty<OutputEvidence>(),
            };
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将导出 {jobs.Length} 个 Table 描述，范围为 {scope}。",
                DataJson = Util.Json.Serialize(receipt),
                State = state,
                WriteSet = writeSet,
                Evidence = new[]
                {
                    "Plan 已只读冻结活动场景、唯一 Nova/TableComponent、TableSettings、Project、Description 与输出目录。",
                },
                RecoveryPayloadJson = Util.Json.Serialize(receipt),
            });
        }

        /// <summary>
        /// 核对冻结设置未漂移后，只调用一次现有 EditorUtil.Table.Exporter 对应入口。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen))
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked", "Table 导出冻结状态无效。"));
            }
            if (!TableSettingsResolver.TryResolve(out TableSettingsResolver.Resolved current, out string resolveError) ||
                !string.Equals(current.ScenePath, frozen.ScenePath, StringComparison.Ordinal) ||
                !string.Equals(current.ComponentId, frozen.ComponentId, StringComparison.Ordinal) ||
                !string.Equals(current.SettingsHash, frozen.SettingsHash, StringComparison.Ordinal))
            {
                return Task.FromResult(AgentActionResult.Create(
                    c_ActionId, "blocked", resolveError ?? "活动场景、TableComponent 或 TableSettings 已漂移，请重新 Plan。"));
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            string[] ids = frozen.Jobs.Select(job => job.qualifiedDescriptionId).ToArray();
            bool exported;
            switch (frozen.Scope)
            {
                case "code":
                    exported = EditorUtil.Table.Exporter.ExportCode(frozen.Settings, ids);
                    break;
                case "data":
                    exported = EditorUtil.Table.Exporter.ExportData(frozen.Settings, ids);
                    break;
                default:
                    exported = EditorUtil.Table.Exporter.ExportAll(frozen.Settings, ids);
                    break;
            }
            if (!exported)
            {
                return Task.FromResult(AgentActionResult.Create(
                    c_ActionId, "partial", "Table Exporter 返回失败；此前描述可能已经发布，Action 不会自动重放。"));
            }

            var outputs = new List<OutputEvidence>();
            try
            {
                foreach (Job job in frozen.Jobs)
                {
                    if (job.exportsCode)
                    {
                        outputs.Add(CaptureOutput("code", job, job.codeOutputPath, context));
                    }
                    if (job.exportsData)
                    {
                        outputs.Add(CaptureOutput("data", job, job.dataOutputPath, context));
                    }
                }
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                return Task.FromResult(AgentActionResult.Create(
                    c_ActionId, "partial", "Table 已导出，但产物取证失败：" + exception.Message));
            }

            var receipt = new Receipt
            {
                scope = frozen.Scope,
                scenePath = frozen.ScenePath,
                componentId = frozen.ComponentId,
                jobs = frozen.Jobs,
                outputs = outputs.ToArray(),
            };
            AgentActionResult result = AgentActionResult.Create(
                c_ActionId, "partial", "Table 已导出，等待 Verify 核对代码与数据产物摘要。" );
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.DataJson = result.ReceiptJson;
            result.Artifacts.AddRange(outputs.Select(output => output.artifact.path).Distinct(StringComparer.Ordinal));
            result.Evidence.Add("Execute 仅调用现有 EditorUtil.Table.Exporter 一次，并分别记录 code/data 目录 SHA-256。" );
            if (outputs.Any(output => output.kind == "code"))
            {
                result.Warnings.Add("代码产物摘要不等于 Unity 编译成功；domain reload 后应在 Editor 稳定状态继续 Verify。" );
            }
            return Task.FromResult(result);
        }

        /// <summary>
        /// 只读复算每个描述的 code/data 目录摘要，并核对 Receipt 覆盖完整性。
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
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked", "Table Export Receipt 无法解析：" + exception.Message));
            }
            if (receipt?.jobs == null || receipt.jobs.Length == 0 || receipt.outputs == null)
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked", "Table Export Receipt 不完整。"));
            }

            OutputEvidence[] expectedOutputs = receipt.jobs.SelectMany(job =>
                    (job.exportsCode ? new[] { CreateExpectedOutput("code", job) } : Array.Empty<OutputEvidence>())
                    .Concat(job.exportsData ? new[] { CreateExpectedOutput("data", job) } : Array.Empty<OutputEvidence>()))
                .ToArray();
            if (receipt.outputs.Length != expectedOutputs.Length || expectedOutputs.Any(expected =>
                    !receipt.outputs.Any(actual => SameOutput(expected, actual) && actual.artifact != null)))
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial", "Receipt 未完整覆盖计划中的 code/data 输出。"));
            }

            GenerateActionCommon.Artifact[] expectedArtifacts = receipt.outputs.Select(output => output.artifact).ToArray();
            if (!GenerateActionCommon.TryVerifyArtifacts(expectedArtifacts, context.CancellationToken,
                    out GenerateActionCommon.Artifact[] actual, out string artifactError))
            {
                AgentActionResult partial = AgentActionResult.Create(c_ActionId, "partial", "Table 导出产物验证未通过：" + artifactError);
                partial.DataJson = Util.Json.Serialize(actual);
                return Task.FromResult(partial);
            }

            AgentActionResult success = AgentActionResult.Create(c_ActionId, "success", "Table code/data 输出范围与 SHA-256 已只读核对。" );
            success.EvidenceKinds = AgentActionEvidence.Static | AgentActionEvidence.Artifact;
            success.DataJson = Util.Json.Serialize(receipt.outputs);
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path).Distinct(StringComparer.Ordinal));
            success.Evidence.AddRange(receipt.outputs.Select(output =>
                $"{output.kind}:{output.projectId}/{output.descriptionId}，{output.artifact.fileCount} 个文件，SHA-256={output.artifact.sha256}"));
            success.Evidence.Add("Verify 未调用 Exporter、AssetDatabase.Refresh 或任何恢复写入。" );
            return Task.FromResult(success);
        }

        /// <summary>
        /// 从已解析 Settings 中选择 Project/Description，并生成只含目标任务的冻结副本。
        /// </summary>
        internal static bool TryBuildSelection(
            TableSettings settings,
            Request request,
            out TableSettings selectedSettings,
            out Job[] jobs,
            out string error)
        {
            selectedSettings = null;
            jobs = Array.Empty<Job>();
            error = null;
            if (!TryNormalizeScope(request?.scope, out string scope) || settings?.Projects == null)
            {
                error = "TableSettings 或 scope 无效。";
                return false;
            }

            List<TableLubanProjectSetting> projects = settings.Projects.Where(project => project != null).ToList();
            if (projects.GroupBy(project => project.Id, StringComparer.Ordinal).Any(group =>
                    string.IsNullOrWhiteSpace(group.Key) || group.Count() != 1))
            {
                error = "Luban Project ID 不能为空且必须唯一。";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(request.projectId))
            {
                projects = projects.Where(project => project.Id == request.projectId).ToList();
                if (projects.Count == 0)
                {
                    error = "Luban Project 不存在：" + request.projectId;
                    return false;
                }
            }

            var requested = request.descriptionIds == null || request.descriptionIds.Length == 0
                ? null
                : new HashSet<string>(request.descriptionIds, StringComparer.Ordinal);
            var matched = new HashSet<string>(StringComparer.Ordinal);
            var selectedJobs = new List<Job>();
            TableSettings clone = JsonUtility.FromJson<TableSettings>(JsonUtility.ToJson(settings, false));
            clone.Projects = clone.Projects.Where(project => projects.Any(source => source.Id == project.Id)).ToList();
            foreach (TableLubanProjectSetting project in clone.Projects)
            {
                if (!GenerateActionCommon.TryResolveProjectPath(
                        project.ConfigPath, "Luban Project configPath", out string configPath, out string configError) ||
                    !File.Exists(configPath))
                {
                    error = configError ?? $"Luban Project '{project.Name}' 的 luban.conf 不存在。";
                    return false;
                }
                EditorUtil.Table.DescriptionValidationResult validation =
                    EditorUtil.Table.DescriptionValidator.Validate(project.ExportDescriptions);
                if (!validation.IsValid)
                {
                    error = $"Luban Project '{project.Name}'：{string.Join(" ", validation.Errors)}";
                    return false;
                }

                var selectedDescriptions = new List<TableExportDescriptionSetting>();
                foreach (TableExportDescriptionSetting description in project.ExportDescriptions ?? new List<TableExportDescriptionSetting>())
                {
                    string qualified = project.Id + "/" + description.Id;
                    bool selected = requested == null
                        ? description.Enabled
                        : requested.Contains(description.Id) || requested.Contains(qualified);
                    if (!selected) continue;
                    if (!TryValidateDescriptionForScope(description, scope, out error)) return false;
                    if (requested != null)
                    {
                        if (requested.Contains(description.Id)) matched.Add(description.Id);
                        if (requested.Contains(qualified)) matched.Add(qualified);
                    }
                    selectedDescriptions.Add(description);
                    selectedJobs.Add(new Job
                    {
                        projectId = project.Id,
                        projectName = project.Name,
                        descriptionId = description.Id,
                        descriptionName = description.Name,
                        qualifiedDescriptionId = qualified,
                        codeOutputPath = Normalize(description.CodeOutputPath),
                        dataOutputPath = Normalize(description.DataOutputPath),
                        exportsCode = scope != "data" && description.CodeTargets != null && description.CodeTargets.Count > 0,
                        exportsData = scope != "code" && description.DataTargets != null && description.DataTargets.Count > 0,
                    });
                }
                project.ExportDescriptions = selectedDescriptions;
            }

            if (requested != null && requested.Except(matched, StringComparer.Ordinal).Any())
            {
                error = "导出描述不存在：" + string.Join(", ", requested.Except(matched, StringComparer.Ordinal)) + "。";
                return false;
            }
            if (selectedJobs.Count == 0)
            {
                error = "没有选择任何导出描述。";
                return false;
            }
            selectedSettings = clone;
            jobs = selectedJobs.OrderBy(job => job.qualifiedDescriptionId, StringComparer.Ordinal).ToArray();
            return true;
        }

        /// <summary>
        /// 规范化 Action 的三种导出范围。
        /// </summary>
        internal static bool TryNormalizeScope(string value, out string scope)
        {
            scope = value?.Trim().ToLowerInvariant();
            return scope == "all" || scope == "code" || scope == "data";
        }

        /// <summary>
        /// 校验一个描述在指定范围内具备目标和输出目录。
        /// </summary>
        private static bool TryValidateDescriptionForScope(TableExportDescriptionSetting description, string scope, out string error)
        {
            error = null;
            bool hasCode = description.CodeTargets != null && description.CodeTargets.Count > 0;
            bool hasData = description.DataTargets != null && description.DataTargets.Count > 0;
            if (string.IsNullOrWhiteSpace(description.Target)) error = $"导出描述 {description.Id} 未配置 Target。";
            else if (scope == "code" && !hasCode) error = $"导出描述 {description.Id} 未配置代码目标。";
            else if (scope == "data" && !hasData) error = $"导出描述 {description.Id} 未配置数据目标。";
            else if (scope == "all" && !hasCode && !hasData) error = $"导出描述 {description.Id} 未配置代码目标或数据目标。";
            else if (scope != "data" && hasCode && string.IsNullOrWhiteSpace(description.CodeOutputPath)) error = $"导出描述 {description.Id} 未配置代码输出目录。";
            else if (scope != "code" && hasData && string.IsNullOrWhiteSpace(description.DataOutputPath)) error = $"导出描述 {description.Id} 未配置数据输出目录。";
            return error == null;
        }

        /// <summary>
        /// 枚举单个冻结任务在当前范围内允许写入的输出目录。
        /// </summary>
        private static IEnumerable<string> EnumerateJobOutputPaths(Job job)
        {
            if (job.exportsCode) yield return job.codeOutputPath;
            if (job.exportsData) yield return job.dataOutputPath;
        }

        /// <summary>
        /// 捕获一个 code/data 输出目录的完整文件摘要。
        /// </summary>
        private static OutputEvidence CaptureOutput(
            string kind,
            Job job,
            string path,
            AgentActionExecutionContext context)
        {
            return new OutputEvidence
            {
                kind = kind,
                projectId = job.projectId,
                descriptionId = job.descriptionId,
                artifact = GenerateActionCommon.CaptureDirectory(path, "*", context.CancellationToken),
            };
        }

        /// <summary>
        /// 建立 Verify 期望的输出身份，不包含执行后摘要。
        /// </summary>
        private static OutputEvidence CreateExpectedOutput(string kind, Job job)
        {
            return new OutputEvidence
            {
                kind = kind,
                projectId = job.projectId,
                descriptionId = job.descriptionId,
            };
        }

        /// <summary>
        /// 比较两个输出证据是否属于同一 Project、Description 与 code/data 槽位。
        /// </summary>
        private static bool SameOutput(OutputEvidence left, OutputEvidence right)
        {
            return right != null && left.kind == right.kind && left.projectId == right.projectId &&
                   left.descriptionId == right.descriptionId;
        }

        /// <summary>
        /// 判断 Description 标识是否可安全进入严格请求与集合匹配。
        /// </summary>
        private static bool IsIdentifier(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 256 && value.IndexOf('\0') < 0;
        }

        /// <summary>
        /// 统一项目相对路径的目录分隔符。
        /// </summary>
        private static string Normalize(string path)
        {
            return path?.Replace('\\', '/');
        }

        /// <summary>
        /// 创建不含可执行计划标识的写前阻断结果。
        /// </summary>
        private static Task<AgentActionHandlerPlan> Blocked(string message)
        {
            return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = message });
        }
    }

    /// <summary>
    /// 从活动场景的唯一 Nova 层级稳定解析 TableSettings 序列化快照。
    /// </summary>
    internal static class TableSettingsResolver
    {
        [Serializable]
        private sealed class TableComponentSnapshot
        {
            public TableSettings m_Setting = null;
        }

        internal sealed class Resolved
        {
            public string ScenePath;
            public string ComponentId;
            public string SettingsHash;
            public TableSettings Settings;
        }

        /// <summary>
        /// 只读取活动且已保存场景，要求其中恰好一个 Nova 根及其下恰好一个 TableComponent。
        /// </summary>
        internal static bool TryResolve(out Resolved resolved, out string error)
        {
            resolved = null;
            error = null;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                error = "活动场景必须已加载并保存，才能稳定解析 TableSettings。";
                return false;
            }

            Nova[] novas = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Nova>(true))
                .ToArray();
            if (novas.Length != 1)
            {
                error = $"活动场景必须恰好包含一个 Nova 根节点，当前为 {novas.Length} 个。";
                return false;
            }
            TableComponent[] components = novas[0].GetComponentsInChildren<TableComponent>(true);
            if (components.Length != 1)
            {
                error = $"Nova 层级必须恰好包含一个 TableComponent，当前为 {components.Length} 个。";
                return false;
            }

            string componentJson = EditorJsonUtility.ToJson(components[0], false);
            TableComponentSnapshot snapshot = JsonUtility.FromJson<TableComponentSnapshot>(componentJson);
            if (snapshot?.m_Setting == null)
            {
                error = "TableComponent.m_Setting 未配置。";
                return false;
            }
            string settingsJson = JsonUtility.ToJson(snapshot.m_Setting, false);
            resolved = new Resolved
            {
                ScenePath = scene.path.Replace('\\', '/'),
                ComponentId = GlobalObjectId.GetGlobalObjectIdSlow(components[0]).ToString(),
                SettingsHash = GenerateActionCommon.ComputeTextHash(settingsJson),
                Settings = JsonUtility.FromJson<TableSettings>(settingsJson),
            };
            return true;
        }
    }
}
