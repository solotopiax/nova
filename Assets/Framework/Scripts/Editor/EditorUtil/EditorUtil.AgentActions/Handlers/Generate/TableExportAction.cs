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
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.table.export",
        "导出 Table",
        "table",
        AgentActionOperationType.Generate,
        Description = "从当前有效 TableSettings 导出全部或指定的表代码与数据，并核验生成结果。",
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
        internal sealed class OutputEvidence
        {
            public string kind;
            public string projectId;
            public string descriptionId;
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
            public Job[] jobs;
            public OutputEvidence[] outputs;
            public TableSettings sourceSettings;
            public DataModuleExportActionCommon.InputSnapshot sourceInputs;
        }

        private sealed class State
        {
            public string Scope;
            public string ScenePath;
            public string ComponentId;
            public string SettingsHash;
            public TableSettings Settings;
            public Job[] Jobs;
            public DataModuleExportActionCommon.InputSnapshot SourceInputs;
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
            if (!TryCaptureSourceInputs(selectedSettings, jobs, context.CancellationToken,
                    out DataModuleExportActionCommon.InputSnapshot sourceInputs, out string sourceError))
            {
                return Blocked(sourceError);
            }

            string[] writeSet = BuildWriteSet(jobs);
            // 选择、冻结和执行使用同一规范范围，避免首尾空白使 code/data 落入全量分支。
            TryNormalizeScope(request.scope, out string scope);
            var state = new State
            {
                Scope = scope,
                ScenePath = resolved.ScenePath,
                ComponentId = resolved.ComponentId,
                SettingsHash = resolved.SettingsHash,
                Settings = selectedSettings,
                Jobs = jobs,
                SourceInputs = sourceInputs,
            };
            var receipt = new Receipt
            {
                receiptVersion = 2,
                scope = scope,
                scenePath = resolved.ScenePath,
                componentId = resolved.ComponentId,
                settingsHash = resolved.SettingsHash,
                jobs = jobs,
                outputs = Array.Empty<OutputEvidence>(),
                sourceSettings = selectedSettings,
                sourceInputs = sourceInputs,
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
                    "已冻结正式 luban.conf、已解析的配置/数据/Schema 读根、模板与实际 Luban/protoc 工具文件集合和 SHA；Table 命名空间仍由自身 Luban Target 决定。",
                    "读根采用保守全文件快照；Schema 的二次外部 include 不由当前 Project 模型解析，不据此声明其完整依赖闭包。",
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

            if (!TryMatchSourceInputs(frozen.Settings, frozen.Jobs, frozen.SourceInputs,
                    context.CancellationToken, out string sourceError))
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked",
                    "Table 配置、源文件或模板已漂移，请重新 Plan：" + sourceError));
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

            if (!TryMatchSourceInputs(frozen.Settings, frozen.Jobs, frozen.SourceInputs,
                    context.CancellationToken, out string postSourceError))
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial",
                    "Table 已导出，但读取来源在执行期间漂移；不会自动重放：" + postSourceError));
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
                receiptVersion = 2,
                scope = frozen.Scope,
                scenePath = frozen.ScenePath,
                componentId = frozen.ComponentId,
                settingsHash = frozen.SettingsHash,
                jobs = frozen.Jobs,
                outputs = outputs.ToArray(),
                sourceSettings = frozen.Settings,
                sourceInputs = frozen.SourceInputs,
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
            bool sourceContract = false;
            try
            {
                sourceContract = JObject.Parse(receiptJson).Properties().Any(property =>
                    new[] { "receiptVersion", "sourceSettings", "sourceInputs", "settingsHash" }
                        .Contains(property.Name, StringComparer.Ordinal));
                receipt = Util.Json.Deserialize<Receipt>(receiptJson);
            }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, sourceContract ? "partial" : "blocked",
                    "Table Export Receipt 无法解析：" + exception.Message));
            }
            if (receipt?.jobs == null || receipt.jobs.Length == 0 || receipt.outputs == null)
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "blocked", "Table Export Receipt 不完整。"));
            }
            if (sourceContract && (receipt.receiptVersion != 2 ||
                !TryVerifyCurrentSourceScope(receipt, context, out _)))
            {
                return Task.FromResult(AgentActionResult.Create(c_ActionId, "partial",
                    "Table 新 Receipt 的配置、源文件或模板快照缺失或已漂移；不会重放导出。"));
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
            if (!sourceContract)
            {
                success.Warnings.Add("旧 Table Receipt 仅证明产物摘要一致，未冻结配置、源文件或模板，不能继承新输入快照证据。");
            }
            return Task.FromResult(success);
        }

        /// <summary>
        /// 将新收据绑定到当前活动场景、组件与设置，再由当前设置重建选择和直接读取范围。
        /// </summary>
        /// <param name="receipt">必须包含设置身份和完整输入的新收据。</param>
        /// <param name="context">只读验证上下文。</param>
        /// <param name="error">设置、选择或来源漂移原因。</param>
        /// <returns>当前配置与来源仍完全匹配收据时返回 true。</returns>
        private static bool TryVerifyCurrentSourceScope(Receipt receipt, AgentActionExecutionContext context, out string error)
        {
            error = "新收据缺少完整场景、组件或设置身份。";
            try
            {
                if (string.IsNullOrWhiteSpace(receipt.scenePath) || string.IsNullOrWhiteSpace(receipt.componentId) ||
                    string.IsNullOrWhiteSpace(receipt.settingsHash) ||
                    !TableSettingsResolver.TryResolve(out TableSettingsResolver.Resolved current, out error) ||
                    current.ScenePath != receipt.scenePath || current.ComponentId != receipt.componentId ||
                    current.SettingsHash != receipt.settingsHash ||
                    !TryMatchSourceSelection(current.Settings, receipt.scope, receipt.sourceSettings, receipt.jobs,
                        out TableSettings selected, out Job[] jobs, out error)) return false;
                return TryMatchOutputPaths(jobs, receipt.outputs, out error) &&
                       TryMatchSourceInputs(selected, jobs, receipt.sourceInputs, context.CancellationToken, out error);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = exception.Message; return false; }
        }

        /// <summary>
        /// 将新收据每个 code/data 证据绑定当前重建 Job 的真实输出目录，避免另一目录的合法摘要替代正式产物。
        /// </summary>
        /// <param name="jobs">由当前 TableSettings 重建且已匹配的完整 Job。</param>
        /// <param name="outputs">新收据的执行后产物证据。</param>
        /// <param name="error">槽位、产物类型或物理输出路径不一致的原因。</param>
        /// <returns>每个正式输出槽位恰有同路径的 directory 证据时返回 true。</returns>
        internal static bool TryMatchOutputPaths(Job[] jobs, OutputEvidence[] outputs, out string error)
        {
            error = "新收据的正式输出目录证据缺失或范围不完整。";
            try
            {
                if (jobs == null || jobs.Any(job => job == null) || outputs == null ||
                    outputs.Length != jobs.Sum(job => (job.exportsCode ? 1 : 0) + (job.exportsData ? 1 : 0))) return false;
                foreach (Job job in jobs)
                    foreach (string kind in new[] { "code", "data" }.Where(kind => kind == "code" ? job.exportsCode : job.exportsData))
                    {
                        OutputEvidence[] matches = outputs.Where(output => SameOutput(CreateExpectedOutput(kind, job), output)).ToArray();
                        if (matches.Length != 1 || matches[0].artifact?.kind != "directory" ||
                            !GenerateActionCommon.TryResolveProjectPath(kind == "code" ? job.codeOutputPath : job.dataOutputPath,
                                "Table 当前输出", out string expected, out error) ||
                            !GenerateActionCommon.TryResolveProjectPath(matches[0].artifact.path,
                                "Table Receipt 输出", out string actual, out error) ||
                            !string.Equals(expected, actual, StringComparison.Ordinal))
                        { error = error ?? "新收据产物类型或物理路径与当前正式输出不一致。"; return false; }
                    }
                error = null;
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        /// <summary>
        /// 从当前 TableSettings 重建收据的限定描述，核对原生参数和完整 Job，不能由收据自造另一组合法来源。
        /// </summary>
        /// <param name="currentSettings">当前组件的真实设置。</param>
        /// <param name="scope">收据记录的规范导出范围。</param>
        /// <param name="expectedSettings">收据冻结的已选设置。</param>
        /// <param name="expectedJobs">收据冻结的完整输出身份。</param>
        /// <param name="selectedSettings">从当前设置重新解析的已选设置。</param>
        /// <param name="jobs">从当前设置重新解析的完整 Job。</param>
        /// <param name="error">限定描述或参数漂移原因。</param>
        /// <returns>完整设置和输出身份一致时返回 true。</returns>
        internal static bool TryMatchSourceSelection(TableSettings currentSettings, string scope,
            TableSettings expectedSettings, Job[] expectedJobs, out TableSettings selectedSettings,
            out Job[] jobs, out string error)
        {
            selectedSettings = null; jobs = Array.Empty<Job>(); error = "新收据缺少完整选中设置或 Job。";
            if (expectedSettings?.Projects == null || expectedSettings.Projects.Count == 0 ||
                expectedSettings.Projects.Any(project => project == null) || expectedJobs == null || expectedJobs.Length == 0 ||
                expectedJobs.Any(job => job == null || string.IsNullOrWhiteSpace(job.qualifiedDescriptionId)) ||
                expectedJobs.Select(job => job.qualifiedDescriptionId).Distinct(StringComparer.Ordinal).Count() != expectedJobs.Length)
                return false;
            if (!TryBuildSelection(currentSettings, new Request
                { scope = scope, projectId = expectedSettings.Projects.Count == 1 ? expectedSettings.Projects[0].Id : null,
                    descriptionIds = expectedJobs.Select(job => job.qualifiedDescriptionId).ToArray() },
                    out selectedSettings, out jobs, out error)) return false;
            if (!JToken.DeepEquals(JToken.FromObject(selectedSettings), JToken.FromObject(expectedSettings)) ||
                !JToken.DeepEquals(JToken.FromObject(jobs), JToken.FromObject(expectedJobs)))
            { error = "当前 TableSettings、原生参数或正式输出身份与新收据不一致。"; return false; }
            error = null;
            return true;
        }

        /// <summary>
        /// 复用 Project 只读模型解析直接读根，冻结全部非元文件及模板；不解释或改写 Luban 配置。
        /// </summary>
        /// <param name="settings">仅包含本次已选 Project 与导出描述的快照。</param>
        /// <param name="jobs">本次正式输出，用于拒绝读取根与输出重叠。</param>
        /// <param name="cancellationToken">取消只读扫描的令牌。</param>
        /// <param name="snapshot">配置、文件集合及 SHA 快照。</param>
        /// <param name="error">无法确定直接读根或存在路径风险时的原因。</param>
        /// <returns>已取得保守直接读根快照时返回 true。</returns>
        internal static bool TryCaptureSourceInputs(TableSettings settings, Job[] jobs, CancellationToken cancellationToken,
            out DataModuleExportActionCommon.InputSnapshot snapshot, out string error)
        {
            snapshot = null; error = null;
            try
            {
                if (settings?.Projects == null || settings.Projects.Count == 0 || jobs == null || jobs.Length == 0)
                    throw new InvalidDataException("Table 输入快照缺少 Project 或 Job。");
                var files = new HashSet<string>(StringComparer.Ordinal);
                var directories = new HashSet<string>(StringComparer.Ordinal);
                string luban = EditorUtil.Luban.CliRunner.GetLubanDllPath();
                if (string.IsNullOrWhiteSpace(luban)) throw new InvalidDataException("Luban 工具未安装，不能冻结实际生成工具。");
                directories.Add(Path.GetDirectoryName(Path.GetFullPath(luban)));
                bool requiresProtoc = false;
                foreach (TableLubanProjectSetting project in settings.Projects)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (project == null || !GenerateActionCommon.TryResolveProjectPath(project.ConfigPath,
                            "Table luban.conf", out string configPath, out error) || !File.Exists(configPath))
                        throw new InvalidDataException(error ?? "Table luban.conf 不存在。");
                    files.Add(configPath);
                    // 模型的 Excel/XML 诊断属于 Inspector 子集；不用这些诊断否定原生 Luban 支持的 Schema。
                    TableProjectModel model = TableProjectModelBuilder.Build(configPath, _ => new List<string>());
                    if (string.IsNullOrWhiteSpace(model.DataDirectory) || !Directory.Exists(model.DataDirectory))
                        throw new InvalidDataException("无法由现有 Project 模型解析数据读根；请核实原生 luban.conf。");
                    directories.Add(Path.GetDirectoryName(configPath));
                    directories.Add(Path.GetFullPath(model.DataDirectory));
                    foreach (string schemaPath in model.SchemaFiles)
                    {
                        if (Directory.Exists(schemaPath)) directories.Add(Path.GetFullPath(schemaPath));
                        else
                        {
                            files.Add(Path.GetFullPath(schemaPath));
                            directories.Add(Path.GetDirectoryName(Path.GetFullPath(schemaPath)));
                        }
                    }
                    foreach (TableExportDescriptionSetting description in project.ExportDescriptions)
                    {
                        foreach (string template in description.CustomTemplateDirs ?? new List<string>())
                            directories.Add(Path.GetFullPath(EditorUtil.Luban.ExportHelper.ResolveCustomTemplateDirectory(template)));
                        bool code = jobs.Any(job => job.projectId == project.Id && job.descriptionId == description.Id && job.exportsCode);
                        requiresProtoc |= code && description.CodeTargets?.Contains("protobuf3") == true;
                        if (code && description.CodeTargets?.Contains("protobuf3") == true &&
                            description.CodeTargets.Contains("cs-newtonsoft-json"))
                            directories.Add(Path.GetFullPath(EditorUtil.Luban.ExportHelper.ResolveCustomTemplateDirectory(
                                "Packages/com.solotopia.nova.framework/Templates/Luban/table-protobuf")));
                    }
                }
                if (requiresProtoc)
                {
                    string protoc = EditorUtil.Proto.CliRunner.GetProtocPath();
                    if (string.IsNullOrWhiteSpace(protoc)) throw new InvalidDataException("protoc 工具未安装，不能冻结实际生成工具。");
                    files.Add(Path.GetFullPath(protoc));
                    string include = EditorUtil.Proto.CliRunner.GetProtocIncludePath();
                    if (!string.IsNullOrWhiteSpace(include)) directories.Add(Path.GetFullPath(include));
                }
                foreach (string output in jobs.SelectMany(EnumerateJobOutputPaths).Select(Path.GetFullPath))
                    foreach (string directory in directories)
                        if (IsPathWithin(output, directory) || IsPathWithin(directory, output))
                            throw new InvalidDataException("Table 正式输出与冻结读根重叠，无法保证来源只读：" + output);
                snapshot = DataModuleExportActionCommon.CaptureInputs(files, directories, cancellationToken);
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 重建相同直接读根与模板解析结果，核对文件内容和新增、删除成员，避免旧路径 SHA 漏掉集合漂移。
        /// </summary>
        /// <param name="settings">计划或新收据中的选中配置。</param>
        /// <param name="jobs">计划或新收据中的正式输出。</param>
        /// <param name="expected">必须存在的原始输入快照。</param>
        /// <param name="cancellationToken">取消扫描令牌。</param>
        /// <param name="error">身份、字节或成员集合漂移原因。</param>
        /// <returns>当前来源仍完全匹配原快照时返回 true。</returns>
        internal static bool TryMatchSourceInputs(TableSettings settings, Job[] jobs,
            DataModuleExportActionCommon.InputSnapshot expected, CancellationToken cancellationToken, out string error)
        {
            error = null;
            if (expected == null) { error = "输入快照缺失。"; return false; }
            if (!TryCaptureSourceInputs(settings, jobs, cancellationToken,
                    out DataModuleExportActionCommon.InputSnapshot actual, out error)) return false;
            if (!DataModuleExportActionCommon.TryVerifyInputs(expected, cancellationToken, out error)) return false;
            if (!string.Equals(Util.Json.Serialize(expected), Util.Json.Serialize(actual), StringComparison.Ordinal))
            { error = "Luban 配置解析出的读根或模板文件集合已漂移。"; return false; }
            return true;
        }

        /// <summary>
        /// 按完整目录边界判断路径包含关系，不把相同前缀的相邻目录视作包含。
        /// </summary>
        /// <param name="candidate">待核路径。</param>
        /// <param name="directory">候选父目录。</param>
        /// <returns>路径等于或位于目录内时返回 true。</returns>
        private static bool IsPathWithin(string candidate, string directory)
        {
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string path = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 展示正式输出目录及 Unity 为新目录和祖先目录创建的元文件，不把只读来源纳入写集。
        /// </summary>
        /// <param name="jobs">本次已选的正式 code/data 目录。</param>
        /// <returns>排序去重后的保守正式写入范围。</returns>
        internal static string[] BuildWriteSet(IEnumerable<Job> jobs)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string output in jobs.SelectMany(EnumerateJobOutputPaths))
            {
                string normalized = Normalize(output).TrimEnd('/');
                paths.Add(normalized + "/**");
                string directory = Path.GetFullPath(output);
                while (!string.IsNullOrEmpty(directory) && GenerateActionCommon.ToProjectRelative(directory).StartsWith("Assets/", StringComparison.Ordinal))
                {
                    if (!Directory.Exists(directory) || !File.Exists(directory + ".meta"))
                        paths.Add(GenerateActionCommon.ToProjectRelative(directory + ".meta"));
                    directory = Path.GetDirectoryName(directory);
                }
            }
            return paths.OrderBy(value => value, StringComparer.Ordinal).ToArray();
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
        /// 只读取活动且已保存场景的唯一 TableComponent，严格解包组件 JSON 后冻结真实存在的 m_Setting。
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
            if (!EditorObjectJson.TryReadSnapshot(componentJson, nameof(TableComponent), "m_Setting", out TableComponentSnapshot snapshot, out error))
                return false;
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
