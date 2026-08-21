/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationExportAction.cs
 * author:    taoye
 * created:   2026/8/21
 * descrip:   Localization 文本、字体与语言列表精确导出 Agent Action
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
        "nova.project.localization.export",
        "导出 Localization",
        "localization",
        AgentActionOperationType.Generate,
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        Locks = new[] { "unity-editor", "asset-database", "localization-export" })]
    internal sealed class LocalizationExportAction : AgentActionHandler<LocalizationExportAction.Request>
    {
        private const string c_ActionId = "nova.project.localization.export";

        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string target;
            [AgentActionRequired] public string scope;
        }

        [Serializable]
        internal sealed class Output
        {
            public string kind;
            public string path;
            public bool directory;
        }

        [Serializable]
        private sealed class Receipt
        {
            public string target;
            public string scope;
            public string scenePath;
            public string componentId;
            public Output[] outputs;
            public string[] deletedPaths;
            public GenerateActionCommon.Artifact[] artifacts;
        }

        private sealed class State
        {
            public string Target;
            public string Scope;
            public string ScenePath;
            public string ComponentId;
            public string ComponentHash;
            public string InputHash;
            public LocalizationSettings Settings;
            public string SupportedLanguagesPath;
            public string SourcePath;
            public string ClassPath;
            public Output[] Outputs;
            public string[] DeletedPaths;
        }

        /// <summary>
        /// 只接受文本、字体、语言列表三类稳定入口及其有效导出范围。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (!TryNormalize(request?.target, request?.scope, out _, out _, out error)) return false;
            return true;
        }

        /// <summary>
        /// 只读解析活动场景配置，并冻结源文件、正式输出和旧格式删除项。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryNormalize(request.target, request.scope, out string target, out string scope, out string error))
                return Blocked(error);
            if (!LocalizationSettingsResolver.TryResolve(out LocalizationSettingsResolver.Resolved resolved, out error))
                return Blocked(error);
            if (!TryBuildPlan(resolved.Settings, resolved.SupportedLanguagesPath, target, scope,
                    context.CancellationToken, out string sourcePath, out string classPath,
                    out Output[] outputs, out string[] deletedPaths, out string inputHash, out error))
                return Blocked(error);

            string[] writeSet = outputs.Select(output => output.directory ? Normalize(output.path).TrimEnd('/') + "/**" : Normalize(output.path))
                .Concat(deletedPaths.Select(Normalize))
                .Append(Normalize(IOPath.Combine(sourcePath, "_temp")).TrimEnd('/') + "/**")
                .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var receipt = new Receipt
            {
                target = target, scope = scope, scenePath = resolved.ScenePath,
                componentId = resolved.ComponentId, outputs = outputs, deletedPaths = deletedPaths,
                artifacts = Array.Empty<GenerateActionCommon.Artifact>(),
            };
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将导出 Localization {target}/{scope}，正式产物 {outputs.Length} 项。",
                DataJson = Util.Json.Serialize(receipt),
                RecoveryPayloadJson = Util.Json.Serialize(receipt),
                WriteSet = writeSet,
                Evidence = new[] { "Plan 已冻结唯一 LocalizationComponent、源文件摘要、正式产物和删除项。" },
                State = new State
                {
                    Target = target, Scope = scope, ScenePath = resolved.ScenePath,
                    ComponentId = resolved.ComponentId, ComponentHash = resolved.ComponentHash,
                    InputHash = inputHash, Settings = resolved.Settings,
                    SupportedLanguagesPath = resolved.SupportedLanguagesPath,
                    SourcePath = sourcePath, ClassPath = classPath,
                    Outputs = outputs, DeletedPaths = deletedPaths,
                },
            });
        }

        /// <summary>
        /// 核对场景与源文件未漂移后，仅调用一个既有 Localization Exporter 入口。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen)) return Result("blocked", "Localization 导出冻结状态无效。");
            if (!LocalizationSettingsResolver.TryResolve(out LocalizationSettingsResolver.Resolved current, out string error) ||
                current.ScenePath != frozen.ScenePath || current.ComponentId != frozen.ComponentId ||
                current.ComponentHash != frozen.ComponentHash)
                return Result("blocked", error ?? "LocalizationComponent 已漂移，请重新 Plan。");
            if (!TryComputeInputHash(frozen.Settings, frozen.Target, frozen.SourcePath, context.CancellationToken,
                    out string currentInputHash, out error) || currentInputHash != frozen.InputHash)
                return Result("blocked", error ?? "Localization 源文件已漂移，请重新 Plan。");

            context.CancellationToken.ThrowIfCancellationRequested();
            bool success = ExecuteExporter(frozen);
            if (!success) return Result("partial", "Localization Exporter 返回失败；Action 不会自动重放。");

            try
            {
                GenerateActionCommon.Artifact[] artifacts = frozen.Outputs.Select(output => output.directory
                        ? GenerateActionCommon.CaptureDirectory(output.path, "*", context.CancellationToken)
                        : GenerateActionCommon.CaptureFile(output.path, context.CancellationToken))
                    .ToArray();
                string[] remainingDeletes = frozen.DeletedPaths.Where(File.Exists).ToArray();
                if (remainingDeletes.Length > 0)
                    return Result("partial", "Localization 已导出，但旧格式或旧语言文件未全部删除：" + string.Join(", ", remainingDeletes));

                var receipt = new Receipt
                {
                    target = frozen.Target, scope = frozen.Scope, scenePath = frozen.ScenePath,
                    componentId = frozen.ComponentId, outputs = frozen.Outputs,
                    deletedPaths = frozen.DeletedPaths, artifacts = artifacts,
                };
                AgentActionResult result = AgentActionResult.Create(c_ActionId, "partial", "Localization 已导出，等待 Verify 核对产物。" );
                result.ReceiptJson = Util.Json.Serialize(receipt);
                result.DataJson = result.ReceiptJson;
                result.Artifacts.AddRange(artifacts.Select(artifact => artifact.path));
                result.Evidence.Add("Execute 只调用既有 TextExporter 或 FontExporter 的一个公开入口。" );
                return Task.FromResult(result);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                return Result("partial", "Localization 已导出，但产物取证失败：" + exception.Message);
            }
        }

        /// <summary>
        /// 只读复算正式产物摘要，并确认计划中的删除项保持不存在。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            try { receipt = Util.Json.Deserialize<Receipt>(receiptJson); }
            catch (Exception exception) { return Result("blocked", "Localization Receipt 无法解析：" + exception.Message); }
            if (receipt?.outputs == null || receipt.artifacts == null || receipt.outputs.Length == 0 ||
                receipt.outputs.Length != receipt.artifacts.Length)
                return Result("partial", "Localization Receipt 尚无完整产物证据。");
            if ((receipt.deletedPaths ?? Array.Empty<string>()).Any(File.Exists))
                return Result("partial", "Localization 计划删除项重新出现或未删除。");
            if (!GenerateActionCommon.TryVerifyArtifacts(receipt.artifacts, context.CancellationToken,
                    out GenerateActionCommon.Artifact[] actual, out string error))
                return Result("partial", "Localization 产物验证未通过：" + error);

            AgentActionResult success = AgentActionResult.Create(c_ActionId, "success", "Localization 输出与 SHA-256 已只读核对。" );
            success.EvidenceKinds = AgentActionEvidence.Static | AgentActionEvidence.Artifact;
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path));
            success.DataJson = Util.Json.Serialize(actual);
            success.Evidence.AddRange(actual.Select(artifact => $"{artifact.kind}:{artifact.path}，SHA-256={artifact.sha256}"));
            return Task.FromResult(success);
        }

        /// <summary>
        /// 将请求规范为既有 Exporter 可以原子执行的目标与范围组合。
        /// </summary>
        internal static bool TryNormalize(string targetValue, string scopeValue, out string target, out string scope, out string error)
        {
            target = targetValue?.Trim().ToLowerInvariant();
            scope = scopeValue?.Trim().ToLowerInvariant();
            error = null;
            if (target != "text" && target != "font" && target != "languages") error = "target 只允许 text、font 或 languages。";
            else if (scope != "all" && scope != "code" && scope != "data") error = "scope 只允许 all、code 或 data。";
            else if (target == "languages" && scope != "data") error = "languages 只支持 data 范围。";
            return error == null;
        }

        /// <summary>
        /// 复用 Localization SourceModel 解析语言，并计算精确正式产物及删除项。
        /// </summary>
        internal static bool TryBuildPlan(LocalizationSettings settings, string supportedLanguagesPath,
            string target, string scope, CancellationToken cancellationToken,
            out string sourcePath, out string classPath, out Output[] outputs,
            out string[] deletedPaths, out string inputHash, out string error)
        {
            sourcePath = classPath = inputHash = null;
            outputs = Array.Empty<Output>();
            deletedPaths = Array.Empty<string>();
            error = null;
            try
            {
                bool text = target == "text" || target == "languages";
                sourcePath = text ? settings?.TextSourceDirPath : settings?.FontSourceDirPath;
                if (!GenerateActionCommon.TryResolveProjectPath(sourcePath, "Localization 源目录", out string sourceFull, out error) || !Directory.Exists(sourceFull)) return false;
                var result = new List<Output>();
                var deletes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (target == "languages")
                {
                    AddFileOutput(supportedLanguagesPath, settings.DataFormat, result, deletes);
                }
                else if (target == "text")
                {
                    if (settings.TextUnitsSettings == null || settings.TextUnitsSettings.Count == 0) throw new InvalidDataException("文本单元设置为空。");
                    LocalizationExcelPreFilter.SourceModel model = LocalizationExcelPreFilter.SourceModel.Load(sourcePath, settings.TextUnitsSettings);
                    classPath = FirstClassPath(settings.TextUnitsSettings);
                    if (scope != "data") AddDirectoryOutput(classPath, result);
                    if (scope != "code")
                    {
                        foreach (LocalizationTextUnitSetting unit in settings.TextUnitsSettings)
                        foreach (string language in model.Languages)
                            AddFileOutput(unit.DatasExportPath.Replace("{0}", language), settings.DataFormat, result, deletes);
                        foreach (LocalizationTextUnitSetting unit in settings.TextUnitsSettings)
                        foreach (string language in Enum.GetNames(typeof(Language)).Except(model.Languages))
                        {
                            string obsolete = unit.DatasExportPath.Replace("{0}", language);
                            if (File.Exists(obsolete)) deletes.Add(Normalize(obsolete));
                        }
                    }
                    if (scope == "all" && !string.IsNullOrWhiteSpace(supportedLanguagesPath))
                        AddFileOutput(supportedLanguagesPath, settings.DataFormat, result, deletes);
                }
                else
                {
                    if (settings?.FontUnitsSettings == null || settings.FontUnitsSettings.Count == 0) throw new InvalidDataException("字体单元设置为空。");
                    classPath = FirstClassPath(settings.FontUnitsSettings);
                    if (scope != "data") AddDirectoryOutput(classPath, result);
                    if (scope != "code") foreach (LocalizationFontUnitSetting unit in settings.FontUnitsSettings)
                        AddFileOutput(unit.DatasExportPath, settings.DataFormat, result, deletes);
                }
                if (result.Count == 0) throw new InvalidDataException("当前目标与范围没有正式输出。");
                foreach (Output output in result)
                    if (!GenerateActionCommon.TryResolveProjectPath(output.path, "Localization 输出", out _, out error)) return false;
                foreach (string path in deletes)
                    if (!GenerateActionCommon.TryResolveProjectPath(path, "Localization 删除项", out _, out error)) return false;
                if (!TryComputeInputHash(settings, target, sourcePath, cancellationToken, out inputHash, out error)) return false;
                outputs = result.GroupBy(output => (output.kind, output.path, output.directory)).Select(group => group.First()).ToArray();
                deletedPaths = deletes.OrderBy(path => path, StringComparer.Ordinal).ToArray();
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                error = "Localization 导出计划无效：" + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 调用与冻结 target/scope 一一对应的现有公开导出入口。
        /// </summary>
        private static bool ExecuteExporter(State state)
        {
            if (state.Target == "languages")
                return EditorUtil.Localization.TextExporter.ExportSupportedLanguages(state.SourcePath, state.SupportedLanguagesPath, state.Settings.DataFormat);
            if (state.Target == "text")
            {
                string[] templates = EditorUtil.Luban.ExportHelper.GetLubanCustomTemplateDirs(EditorUtil.Luban.LubanExportProfiles.LocalizationText.TemplateKey);
                if (state.Scope == "code") return EditorUtil.Localization.TextExporter.ExportTextCode(state.Settings, state.SourcePath, state.ClassPath, templates);
                if (state.Scope == "data") return EditorUtil.Localization.TextExporter.ExportTextData(state.Settings, state.SourcePath);
                return EditorUtil.Localization.TextExporter.ExportTextAll(state.Settings, state.SourcePath, state.ClassPath, templates, state.SupportedLanguagesPath);
            }
            if (state.Scope == "code") return EditorUtil.Localization.FontExporter.ExportFontCode(state.Settings, state.SourcePath, state.ClassPath);
            if (state.Scope == "data") return EditorUtil.Localization.FontExporter.ExportFontData(state.Settings, state.SourcePath);
            return EditorUtil.Localization.FontExporter.ExportFontAll(state.Settings, state.SourcePath, state.ClassPath);
        }

        /// <summary>
        /// 对实际会被 Exporter 读取的 xlsx 文件计算稳定聚合摘要。
        /// </summary>
        private static bool TryComputeInputHash(LocalizationSettings settings, string target, string sourcePath,
            CancellationToken cancellationToken, out string hash, out string error)
        {
            hash = null;
            error = null;
            try
            {
                if (!GenerateActionCommon.TryResolveProjectPath(sourcePath, "Localization 源目录", out string root, out error)) return false;
                IEnumerable<string> files;
                if (target == "languages")
                {
                    files = Directory.GetFiles(root, "*.xlsx", SearchOption.AllDirectories)
                        .Where(file => !IOPath.GetRelativePath(root, file).Replace('\\', '/').StartsWith("_configs/", StringComparison.OrdinalIgnoreCase))
                        .Where(file => !IOPath.GetRelativePath(root, file).Replace('\\', '/').StartsWith("_temp/", StringComparison.OrdinalIgnoreCase))
                        .Where(file => !IOPath.GetFileName(file).StartsWith("~$", StringComparison.Ordinal));
                }
                else
                {
                    IEnumerable<DataTableUnitSettingBase> units = target == "text"
                        ? settings.TextUnitsSettings.Cast<DataTableUnitSettingBase>()
                        : settings.FontUnitsSettings.Cast<DataTableUnitSettingBase>();
                    files = units.Select(unit => IOPath.GetFullPath(IOPath.Combine(root, unit.SourcePath)));
                }
                string[] ordered = files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(file => file, StringComparer.Ordinal).ToArray();
                if (ordered.Length == 0) throw new FileNotFoundException("未找到 Localization xlsx 源文件。");
                var entries = new List<string>();
                foreach (string file in ordered)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!File.Exists(file)) throw new FileNotFoundException("Localization xlsx 源文件不存在。", file);
                    if (!GenerateActionCommon.TryResolveProjectPath(file, "Localization xlsx 源文件", out _, out error)) return false;
                    entries.Add(IOPath.GetRelativePath(root, file).Replace('\\', '/') + ":" + GenerateActionCommon.ComputeFileHash(file, cancellationToken));
                }
                hash = GenerateActionCommon.ComputeTextHash(string.Join("\n", entries));
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                error = "Localization 源文件无法冻结：" + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 添加一个正式数据文件，并把可能被清理的另一格式文件纳入删除计划。
        /// </summary>
        private static void AddFileOutput(string path, LubanDataFormat format, ICollection<Output> outputs, ISet<string> deletes)
        {
            string suffix = format == LubanDataFormat.Binary ? ".bytes" : ".json";
            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("数据输出路径与当前 Luban 格式不匹配：" + path);
            string normalized = Normalize(path);
            outputs.Add(new Output { kind = "data", path = normalized, directory = false });
            string counterpart = normalized.Substring(0, normalized.Length - suffix.Length) + (format == LubanDataFormat.Binary ? ".json" : ".bytes");
            if (File.Exists(counterpart)) deletes.Add(counterpart);
        }

        /// <summary>
        /// 添加代码输出目录；目录内生成文件由既有 GeneratedOutput 负责替换与清理。
        /// </summary>
        private static void AddDirectoryOutput(string path, ICollection<Output> outputs)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("类型输出目录为空。");
            outputs.Add(new Output { kind = "code", path = Normalize(path), directory = true });
        }

        /// <summary>
        /// 按现有 Inspector/Pipify 规则取第一个非空类型输出目录。
        /// </summary>
        private static string FirstClassPath<T>(IEnumerable<T> units) where T : DataTableUnitSettingBase
        {
            return units.FirstOrDefault(unit => unit != null && !string.IsNullOrWhiteSpace(unit.ClassesExportPath))?.ClassesExportPath;
        }

        /// <summary>
        /// 统一项目相对路径的目录分隔符。
        /// </summary>
        private static string Normalize(string value) => value?.Replace('\\', '/');

        /// <summary>
        /// 创建写前阻断计划。
        /// </summary>
        private static Task<AgentActionHandlerPlan> Blocked(string message) =>
            Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = message });

        /// <summary>
        /// 创建不带伪证据的执行或验证结果。
        /// </summary>
        private static Task<AgentActionResult> Result(string status, string message) =>
            Task.FromResult(AgentActionResult.Create(c_ActionId, status, message));
    }

    /// <summary>
    /// 从活动场景唯一 Nova 层级稳定解析 LocalizationComponent 序列化快照。
    /// </summary>
    internal static class LocalizationSettingsResolver
    {
        [Serializable]
        private sealed class Snapshot
        {
            public LocalizationSettings m_LocalizationSettings = null;
            public string m_SupportedLanguagesDataExportPath = null;
        }

        internal sealed class Resolved
        {
            public string ScenePath;
            public string ComponentId;
            public string ComponentHash;
            public LocalizationSettings Settings;
            public string SupportedLanguagesPath;
        }

        /// <summary>
        /// 要求活动场景已保存，且唯一 Nova 层级下恰好一个 LocalizationComponent。
        /// </summary>
        internal static bool TryResolve(out Resolved resolved, out string error)
        {
            resolved = null;
            error = null;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                error = "活动场景必须已加载并保存，才能解析 Localization 设置。";
                return false;
            }
            Nova[] novas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Nova>(true)).ToArray();
            if (novas.Length != 1)
            {
                error = $"活动场景必须恰好包含一个 Nova 根节点，当前为 {novas.Length} 个。";
                return false;
            }
            LocalizationComponent[] components = novas[0].GetComponentsInChildren<LocalizationComponent>(true);
            if (components.Length != 1)
            {
                error = $"Nova 层级必须恰好包含一个 LocalizationComponent，当前为 {components.Length} 个。";
                return false;
            }
            string json = EditorJsonUtility.ToJson(components[0], false);
            Snapshot snapshot = JsonUtility.FromJson<Snapshot>(json);
            if (snapshot?.m_LocalizationSettings == null)
            {
                error = "LocalizationComponent.m_LocalizationSettings 未配置。";
                return false;
            }
            resolved = new Resolved
            {
                ScenePath = scene.path.Replace('\\', '/'),
                ComponentId = GlobalObjectId.GetGlobalObjectIdSlow(components[0]).ToString(),
                ComponentHash = GenerateActionCommon.ComputeTextHash(json),
                Settings = JsonUtility.FromJson<LocalizationSettings>(JsonUtility.ToJson(snapshot.m_LocalizationSettings, false)),
                SupportedLanguagesPath = snapshot.m_SupportedLanguagesDataExportPath,
            };
            return true;
        }
    }
}
