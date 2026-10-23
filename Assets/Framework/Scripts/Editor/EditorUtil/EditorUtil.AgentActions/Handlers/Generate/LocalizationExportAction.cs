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
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Compilation;
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
        Description = "从当前有效 LocalizationSettings 导出本地化运行时数据，并核验生成结果。",
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
        private const string c_SupportedLanguagesProfile = "localization-supported-languages";
        private static readonly string[] s_SupportedLanguagesTypes =
            { "LocalizationSupportedLanguagesTables", "TbLocalizationSupportedLanguages", "LocalizationSupportedLanguage" };

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
            public int sourceContractVersion;
            public LocalizationSettings settings;
            public string componentHash;
            public string sourcePath;
            public string classPath;
            public string supportedLanguagesPath;
            public string inputHash;
            public DataModuleExportActionCommon.NamespaceSourceSnapshot namespaceSource;
            public DataModuleExportActionCommon.InputSnapshot inputSnapshot;
            public DataModuleExportActionCommon.ConfigurationSnapshot[] configurations;
            public GenerateActionCommon.Artifact[] configurationArtifacts;
            public GenerateActionCommon.Artifact supportedLanguagesCodeManifest;
            public string compilationToken;
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
            public DataModuleExportActionCommon.NamespaceSourceSnapshot NamespaceSource;
            public DataModuleExportActionCommon.InputSnapshot InputSnapshot;
            public DataModuleExportActionCommon.ConfigurationSnapshot[] Configurations;
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

            bool includesCode = target != "languages" && scope != "data";
            if (includesCode && GeneratedExportCompilationState.IsPending())
                return Blocked("已有导出编译待完成，请先 Verify 该收据。");
            DataModuleExportActionCommon.NamespaceSourceSnapshot namespaceSource = null;
            if (target != "languages" && !DataModuleExportActionCommon.TryCaptureNamespaceSource(
                    context.CancellationToken, out namespaceSource, out error)) return Blocked(error);
            DataModuleExportActionCommon.ConfigurationSnapshot[] configurations;
            DataModuleExportActionCommon.InputSnapshot inputSnapshot;
            string[] writeSet;
            try
            {
                configurations = BuildConfigurations(resolved.Settings, target, scope, sourcePath, classPath);
                inputSnapshot = CaptureInputSnapshot(resolved.Settings, target, scope, sourcePath,
                    resolved.ScenePath, configurations, context.CancellationToken);
                writeSet = BuildCompleteWriteSet(outputs, deletedPaths, sourcePath, classPath, target, scope, configurations);
                if (!DataModuleExportActionCommon.TryVerifyInputs(inputSnapshot, context.CancellationToken, out error) ||
                    (target != "languages" && !DataModuleExportActionCommon.TryVerifyNamespaceSource(namespaceSource,
                        context.CancellationToken, out error))) return Blocked(error);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { return Blocked(exception.Message); }
            var frozen = new State
            {
                Target = target, Scope = scope, ScenePath = resolved.ScenePath,
                ComponentId = resolved.ComponentId, ComponentHash = resolved.ComponentHash,
                InputHash = inputHash, Settings = resolved.Settings,
                SupportedLanguagesPath = resolved.SupportedLanguagesPath,
                SourcePath = sourcePath, ClassPath = classPath, Outputs = outputs, DeletedPaths = deletedPaths,
                NamespaceSource = namespaceSource, InputSnapshot = inputSnapshot, Configurations = configurations,
            };
            Receipt receipt = CreateReceipt(frozen, Array.Empty<GenerateActionCommon.Artifact>());
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"将导出 Localization {target}/{scope}，正式产物 {outputs.Length} 项。",
                DataJson = Util.Json.Serialize(receipt),
                RecoveryPayloadJson = Util.Json.Serialize(receipt),
                WriteSet = writeSet,
                Evidence = new[] { "Plan 已冻结唯一 LocalizationComponent、真实源与工具、配置写集、正式产物和删除项。" },
                State = frozen,
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
            if (!DataModuleExportActionCommon.TryVerifyInputs(frozen.InputSnapshot, context.CancellationToken, out error) ||
                (frozen.Target != "languages" && !DataModuleExportActionCommon.TryVerifyNamespaceSource(
                    frozen.NamespaceSource, context.CancellationToken, out error))) return Result("blocked", error);
            try
            {
                var currentInputs = CaptureInputSnapshot(frozen.Settings, frozen.Target, frozen.Scope, frozen.SourcePath,
                    frozen.ScenePath, frozen.Configurations, context.CancellationToken);
                if (!DataModuleExportActionCommon.HasExactInputScope(frozen.InputSnapshot, currentInputs))
                    return Result("blocked", "Localization 当前模板、工具或输入范围已漂移，请重新 Plan。");
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { return Result("blocked", exception.Message); }
            if (frozen.Target != "languages" && frozen.Scope != "data" && GeneratedExportCompilationState.IsPending())
                return Result("blocked", "已有导出编译待完成，请先 Verify 该收据。");

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

                Receipt receipt = CreateReceipt(frozen, artifacts);
                receipt.configurationArtifacts = DataModuleExportActionCommon.CaptureConfigurationArtifacts(
                    frozen.Configurations, context.CancellationToken);
                if (frozen.Target == "text" && frozen.Scope != "data")
                    receipt.supportedLanguagesCodeManifest = GenerateActionCommon.CaptureFile(
                        SupportedLanguagesCodeManifestPath(frozen.SourcePath), context.CancellationToken);
                if (!TryVerifySources(receipt, context.CancellationToken, out error))
                    return Result("partial", "Localization 已导出，但真实来源或配置验收失败：" + error);
                receipt.compilationToken = frozen.Target != "languages" && frozen.Scope != "data" ? Guid.NewGuid().ToString("N") : null;
                AgentActionResult result = AgentActionResult.Create(c_ActionId, "partial", "Localization 已导出，等待 Verify 核对产物。" );
                result.ReceiptJson = Util.Json.Serialize(receipt);
                result.DataJson = result.ReceiptJson;
                if (receipt.compilationToken != null)
                    GeneratedExportCompilationState.Schedule(receipt.compilationToken, GenerateActionCommon.ComputeTextHash(result.ReceiptJson));
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
            bool sourceContract;
            try
            {
                receipt = Util.Json.Deserialize<Receipt>(receiptJson);
                sourceContract = DataModuleExportActionCommon.HasSourceContract(receiptJson) || JObject.Parse(receiptJson).Properties().Any(property =>
                    new[] { "settings", "componentHash", "sourcePath", "classPath", "supportedLanguagesPath", "inputHash", "supportedLanguagesCodeManifest" }
                        .Contains(property.Name, StringComparer.Ordinal));
            }
            catch (Exception exception) { return Result("blocked", "Localization Receipt 无法解析：" + exception.Message); }
            if (receipt?.outputs == null || receipt.artifacts == null || receipt.outputs.Length == 0 ||
                receipt.outputs.Length != receipt.artifacts.Length)
                return Result("partial", "Localization Receipt 尚无完整产物证据。");
            if ((receipt.deletedPaths ?? Array.Empty<string>()).Any(File.Exists))
                return Result("partial", "Localization 计划删除项重新出现或未删除。");
            if (!GenerateActionCommon.TryVerifyArtifacts(receipt.artifacts, context.CancellationToken,
                    out GenerateActionCommon.Artifact[] actual, out string error))
                return Result("partial", "Localization 产物验证未通过：" + error);
            if (sourceContract)
            {
                if (!TryVerifySources(receipt, context.CancellationToken, out error))
                    return Result("partial", "Localization 来源与导出配置验证未通过：" + error);
                if (receipt.target != "languages" && receipt.scope != "data" &&
                    (!GeneratedExportCompilationState.TryVerify(receipt.compilationToken,
                         GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(receipt)), true, out error) ||
                     !DataModuleExportActionCommon.TryVerifyLoadedTypes(receipt.namespaceSource, receipt.configurations, out error) ||
                     (receipt.target == "text" && !TryVerifySupportedLanguagesTypes(receipt.namespaceSource.namespaceValue, receipt.classPath, out error))))
                    return Result("partial", "Localization 本次代码编译或类型验收未通过：" + error);
            }

            AgentActionResult success = AgentActionResult.Create(c_ActionId, "success", "Localization 输出与 SHA-256 已只读核对。" );
            success.EvidenceKinds = sourceContract && receipt.target != "languages" && receipt.scope != "data"
                ? AgentActionEvidence.Static | AgentActionEvidence.Compile | AgentActionEvidence.Artifact
                : AgentActionEvidence.Static | AgentActionEvidence.Artifact;
            success.Artifacts.AddRange(actual.Select(artifact => artifact.path));
            success.DataJson = Util.Json.Serialize(actual);
            success.Evidence.AddRange(actual.Select(artifact => $"{artifact.kind}:{artifact.path}，SHA-256={artifact.sha256}"));
            if (!sourceContract) success.Warnings.Add("历史 Receipt 只核对产物摘要；缺真实 Namespace、输入、配置和本次编译证据，不证明新契约。");
            else if (receipt.target == "languages") success.Evidence.Add("仅支持语言数据验收；不要求 Master，也不证明代码 Namespace 或运行时加载。");
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

        /// <summary>把冻结的输入、组件、配置范围和正式产物装入同一版本收据，不重新解析执行目标。</summary>
        private static Receipt CreateReceipt(State frozen, GenerateActionCommon.Artifact[] artifacts) => new Receipt
        {
            target = frozen.Target, scope = frozen.Scope, scenePath = frozen.ScenePath,
            componentId = frozen.ComponentId, componentHash = frozen.ComponentHash,
            outputs = frozen.Outputs, deletedPaths = frozen.DeletedPaths, artifacts = artifacts,
            sourceContractVersion = 1, settings = frozen.Settings, sourcePath = frozen.SourcePath,
            classPath = frozen.ClassPath, supportedLanguagesPath = frozen.SupportedLanguagesPath,
            inputHash = frozen.InputHash, namespaceSource = frozen.NamespaceSource,
            inputSnapshot = frozen.InputSnapshot, configurations = frozen.Configurations,
        };

        /// <summary>文本和字体固定 Profile 写持久配置；独立语言列表只使用临时配置，不创建假持久范围。</summary>
        internal static DataModuleExportActionCommon.ConfigurationSnapshot[] BuildConfigurations(
            LocalizationSettings settings, string target, string scope, string sourcePath, string classPath)
        {
            if (target == "languages") return Array.Empty<DataModuleExportActionCommon.ConfigurationSnapshot>();
            IEnumerable<DataTableUnitSettingBase> units = target == "text"
                ? settings.TextUnitsSettings.Cast<DataTableUnitSettingBase>()
                : settings.FontUnitsSettings.Cast<DataTableUnitSettingBase>();
            return new[] { DataModuleExportActionCommon.CreateConfigurationSnapshot(
                target == "text" ? EditorUtil.Luban.LubanExportProfiles.LocalizationText : EditorUtil.Luban.LubanExportProfiles.LocalizationFont,
                sourcePath, classPath, units.Select(unit => unit.SourcePath), scope != "data", scope != "data") };
        }

        /// <summary>冻结场景、精确 Excel、仅代码模式实际读取的正式数据、既有配置与真实模板和 Luban 工具成员。</summary>
        private static DataModuleExportActionCommon.InputSnapshot CaptureInputSnapshot(LocalizationSettings settings,
            string target, string scope, string sourcePath, string scenePath,
            DataModuleExportActionCommon.ConfigurationSnapshot[] configurations, CancellationToken cancellationToken)
        {
            var files = new List<string> { scenePath };
            files.AddRange(EnumerateSourceFiles(settings, target, sourcePath));
            files.AddRange(configurations.SelectMany(DataModuleExportActionCommon.ConfigurationFiles));
            if (scope == "code" && target == "font")
                files.AddRange(settings.FontUnitsSettings.Select(unit => unit.DatasExportPath).Where(path => !string.IsNullOrWhiteSpace(path)));
            if (scope == "code" && target == "text" && settings.DataFormat == LubanDataFormat.Json)
            {
                string language = LocalizationExcelPreFilter.SourceModel.Load(sourcePath, settings.TextUnitsSettings).Languages[0];
                files.AddRange(settings.TextUnitsSettings.Select(unit => unit.DatasExportPath.Replace("{0}", language)));
            }
            var directories = new List<string>();
            if (target != "languages" && scope != "data")
            {
                string templateKey = target == "text" ? EditorUtil.Luban.LubanExportProfiles.LocalizationText.TemplateKey
                    : EditorUtil.Luban.LubanExportProfiles.LocalizationFont.TemplateKey;
                directories.AddRange(EditorUtil.Luban.ExportHelper.GetLubanCustomTemplateDirs(templateKey) ?? Array.Empty<string>());
                if (target == "text")
                    directories.AddRange(EditorUtil.Luban.ExportHelper.GetLubanCustomTemplateDirs(c_SupportedLanguagesProfile) ?? Array.Empty<string>());
            }
            string luban = EditorUtil.Luban.CliRunner.GetLubanDllPath();
            if (string.IsNullOrWhiteSpace(luban)) throw new InvalidDataException("Luban 工具未安装，不能冻结实际导出工具。");
            directories.Add(IOPath.GetDirectoryName(luban));
            return DataModuleExportActionCommon.CaptureInputs(files, directories, cancellationToken);
        }

        /// <summary>包含反格式/meta 删除、代码目录、全部持久配置、支持语言输出清单及临时目录的真实写集。</summary>
        internal static string[] BuildCompleteWriteSet(Output[] outputs, string[] deletedPaths, string sourcePath,
            string classPath, string target, string scope, DataModuleExportActionCommon.ConfigurationSnapshot[] configurations)
        {
            IEnumerable<string> paths = DataModuleExportActionCommon.EnumerateFileWritePaths(outputs.Where(output => !output.directory).Select(output => output.path))
                .Concat(deletedPaths)
                .Concat(outputs.Where(output => output.directory).SelectMany(output => EnumerateDirectoryWritePaths(output.path)))
                .Concat(DataModuleExportActionCommon.EnumerateConfigurationWritePaths(configurations));
            if (target == "text" && scope != "data")
                paths = paths.Concat(DataModuleExportActionCommon.EnumerateFileWritePaths(new[] { SupportedLanguagesCodeManifestPath(sourcePath) }));
            string temp = IOPath.Combine(sourcePath, "_temp");
            paths = paths.Concat(EnumerateDirectoryWritePaths(temp));
            return DataModuleExportActionCommon.BuildWriteSet(paths);
        }

        /// <summary>声明目录内替换与清理及 Unity 可能补建的祖先元文件，不创建占位路径或文件。</summary>
        private static IEnumerable<string> EnumerateDirectoryWritePaths(string path)
        {
            if (!GenerateActionCommon.TryResolveProjectPath(path, "导出写入目录", out string full, out string error))
                throw new InvalidDataException(error);
            yield return Normalize(full).TrimEnd('/') + "/**";
            for (string current = full; GenerateActionCommon.ToProjectRelative(current).StartsWith("Assets/", StringComparison.Ordinal);
                 current = IOPath.GetDirectoryName(current))
                if (!Directory.Exists(current) || !File.Exists(current + ".meta")) yield return current + ".meta";
        }

        /// <summary>从收据冻结设置反推完整输出与配置，再严验真实来源；不接受损坏的新收据退回历史摘要。</summary>
        private static bool TryVerifySources(Receipt receipt, CancellationToken cancellationToken, out string error)
        {
            error = "Localization 新来源契约缺失或不完整。";
            if (receipt?.sourceContractVersion != 1 || receipt.settings == null || string.IsNullOrWhiteSpace(receipt.componentHash) ||
                string.IsNullOrWhiteSpace(receipt.componentId) || string.IsNullOrWhiteSpace(receipt.scenePath) ||
                receipt.inputSnapshot == null || receipt.configurations == null || receipt.configurationArtifacts == null ||
                !TryNormalize(receipt.target, receipt.scope, out string target, out string scope, out _) ||
                target != receipt.target || scope != receipt.scope) return false;
            try
            {
                if (!LocalizationSettingsResolver.TryResolve(out LocalizationSettingsResolver.Resolved current, out error) ||
                    !HasMatchingCurrentBinding(current, receipt.scenePath, receipt.componentId, receipt.componentHash, receipt.settings))
                { error ??= "Localization 当前活动场景、组件或设置已漂移，不能沿用原收据。"; return false; }
                if (!TryBuildPlan(receipt.settings, receipt.supportedLanguagesPath, target, scope, cancellationToken,
                        out string source, out string classes, out Output[] outputs, out _, out string hash, out error) ||
                    source != receipt.sourcePath || classes != receipt.classPath || hash != receipt.inputHash ||
                    !JToken.DeepEquals(JToken.FromObject(outputs), JToken.FromObject(receipt.outputs)))
                { error ??= "Localization 收据输出、设置或源摘要与当前真实范围不一致。"; return false; }
                string[] requiredDeletes = outputs.Where(output => !output.directory).SelectMany(output =>
                    new[] { IOPath.ChangeExtension(output.path, receipt.settings.DataFormat == LubanDataFormat.Json ? ".bytes" : ".json"),
                        IOPath.ChangeExtension(output.path, receipt.settings.DataFormat == LubanDataFormat.Json ? ".bytes" : ".json") + ".meta" }).ToArray();
                if (receipt.deletedPaths == null || requiredDeletes.Any(path => !receipt.deletedPaths.Contains(Normalize(path), StringComparer.Ordinal)))
                { error = "Localization 收据未完整覆盖反格式数据及元文件删除项。"; return false; }
                if (!HasExactArtifactCoverage(receipt.outputs, receipt.artifacts))
                { error = "Localization 收据摘要未完整对应每个计划输出。"; return false; }
                DataModuleExportActionCommon.InputSnapshot expectedInputs = CaptureInputSnapshot(receipt.settings,
                    target, scope, source, receipt.scenePath, BuildConfigurations(receipt.settings, target, scope, source, classes), cancellationToken);
                if (!DataModuleExportActionCommon.HasExactInputScope(receipt.inputSnapshot, expectedInputs))
                { error = "Localization 收据输入范围缺失，不能省略源、工具、模板、现有数据或配置。"; return false; }
                var expected = BuildConfigurations(receipt.settings, target, scope, source, classes);
                if (!JToken.DeepEquals(JToken.FromObject(expected), JToken.FromObject(receipt.configurations)))
                { error = "Localization 收据持久配置范围不完整或被改变。"; return false; }
                if (target == "languages")
                {
                    if (receipt.namespaceSource != null || receipt.configurations.Length != 0 || receipt.configurationArtifacts.Length != 0 ||
                        receipt.supportedLanguagesCodeManifest != null || receipt.compilationToken != null)
                    { error = "独立语言列表只有数据产物，不接受虚构代码或持久配置证据。"; return false; }
                    return DataModuleExportActionCommon.TryVerifyInputs(receipt.inputSnapshot, cancellationToken, out error);
                }
                if (!DataModuleExportActionCommon.TryVerifyNamespaceSource(receipt.namespaceSource, cancellationToken, out error) ||
                    !DataModuleExportActionCommon.TryVerifyUnchangedInputs(receipt.inputSnapshot, receipt.configurations, cancellationToken, out error) ||
                    !DataModuleExportActionCommon.TryVerifyConfigurationArtifacts(receipt.configurations, receipt.configurationArtifacts, cancellationToken, out error) ||
                    !DataModuleExportActionCommon.TryVerifyExportedConfigurations(receipt.namespaceSource, receipt.configurations, cancellationToken, out error)) return false;
                if (target == "text" && scope != "data")
                    return TryVerifySupportedLanguagesCode(source, classes, receipt.supportedLanguagesCodeManifest, cancellationToken, out error);
                if (receipt.supportedLanguagesCodeManifest != null) { error = "当前范围没有支持语言代码输出清单。"; return false; }
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { error = exception.Message; return false; }
        }

        /// <summary>把已解析的当前组件与冻结场景、身份、原 JSON Hash 和脱离设置精确绑定，防磁盘 SHA 掩盖实时脏设置。</summary>
        internal static bool HasMatchingCurrentBinding(LocalizationSettingsResolver.Resolved current, string scenePath,
            string componentId, string componentHash, LocalizationSettings settings) => current != null && settings != null &&
            current.ScenePath == scenePath && current.ComponentId == componentId && current.ComponentHash == componentHash &&
            JsonUtility.ToJson(current.Settings, false) == JsonUtility.ToJson(settings, false);

        /// <summary>支持语言代码全量入口仍写独立 output-manifest，但其 Luban conf/schema 只位于被清理的临时目录。</summary>
        private static string SupportedLanguagesCodeManifestPath(string sourcePath) => IOPath.Combine(
            EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(sourcePath), "output-manifests", c_SupportedLanguagesProfile + ".json");

        /// <summary>核对支持语言固定三类型的清单、真实归属和正文 Hash，不为临时 Profile 新增共享配置定义。</summary>
        private static bool TryVerifySupportedLanguagesCode(string sourcePath, string classPath,
            GenerateActionCommon.Artifact artifact, CancellationToken cancellationToken, out string error)
        {
            error = "支持语言代码产物清单缺失或不完整。";
            if (artifact == null || artifact.path != IOPath.GetFullPath(SupportedLanguagesCodeManifestPath(sourcePath))) return false;
            if (!GenerateActionCommon.TryVerifyArtifacts(new[] { artifact }, cancellationToken, out _, out error)) return false;
            JObject manifest = JObject.Parse(File.ReadAllText(SupportedLanguagesCodeManifestPath(sourcePath)));
            string source = GenerateActionCommon.ToProjectRelative(IOPath.GetFullPath(sourcePath));
            string code = GenerateActionCommon.ToProjectRelative(IOPath.GetFullPath(classPath));
            string[] names = manifest["Files"] is JArray files && files.All(item => item.Type == JTokenType.String)
                ? files.Values<string>().OrderBy(value => value, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
            if ((string)manifest["ProfileId"] != c_SupportedLanguagesProfile || (string)manifest["Source"] != source ||
                (string)manifest["CodeOutputDirectory"] != code || !names.SequenceEqual(s_SupportedLanguagesTypes.Select(name => name + ".cs").OrderBy(value => value, StringComparer.Ordinal)))
            { error = "支持语言代码清单 Profile、源、输出目录或三类型集合不匹配。"; return false; }
            foreach (string name in s_SupportedLanguagesTypes)
                if (!EditorUtil.Luban.GeneratedOutput.IsOwnedAndUnmodified(IOPath.Combine(classPath, name + ".cs"), c_SupportedLanguagesProfile, source))
                { error = "支持语言生成代码缺失、归属不符或正文被改写：" + name; return false; }
            error = null;
            return true;
        }

        /// <summary>本次编译令牌通过后只读精确类型身份，Tables 与表分别检查 ILubanTables 和 ITable，不实例化业务。</summary>
        private static bool TryVerifySupportedLanguagesTypes(string namespaceValue, string classPath, out string error)
        {
            error = null;
            foreach (string name in s_SupportedLanguagesTypes)
            {
                string fullName = string.IsNullOrEmpty(namespaceValue) ? name : namespaceValue + "." + name;
                string scriptPath = GenerateActionCommon.ToProjectRelative(IOPath.GetFullPath(IOPath.Combine(classPath, name + ".cs")));
                string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(scriptPath);
                string owner = string.IsNullOrWhiteSpace(assemblyName) ? null : IOPath.GetFileNameWithoutExtension(assemblyName);
                System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => assembly.GetName().Name == owner).ToArray();
                Type type = assemblies.Length == 1 ? assemblies[0].GetType(fullName, false, false) : null;
                if (type == null || type.FullName != fullName || type.Assembly.GetName().Name != owner ||
                    (name == s_SupportedLanguagesTypes[0] && !typeof(ILubanTables).IsAssignableFrom(type)) ||
                    (name == s_SupportedLanguagesTypes[1] && !typeof(ITable).IsAssignableFrom(type)))
                { error = "本次编译后未找到支持语言精确类型或接口契约不符：" + fullName; return false; }
            }
            return true;
        }

        /// <summary>核对每个计划输出与摘要的精确路径、种类一一对应，拒绝重复或替换范围。</summary>
        private static bool HasExactArtifactCoverage(Output[] outputs, GenerateActionCommon.Artifact[] artifacts)
        {
            if (outputs == null || artifacts == null || outputs.Length != artifacts.Length || outputs.Length == 0) return false;
            return outputs.Zip(artifacts, (output, artifact) => output != null && artifact != null &&
                artifact.path == IOPath.GetFullPath(output.path).TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar) &&
                artifact.kind == (output.directory ? "directory" : "file")).All(value => value);
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

        /// <summary>按实际 SourceModel 或独立兼容递归扫描规则枚举 Excel，忽略工作目录和 Office 锁文件。</summary>
        private static IEnumerable<string> EnumerateSourceFiles(LocalizationSettings settings, string target, string sourcePath)
        {
            string root = IOPath.GetFullPath(sourcePath);
            if (target == "languages") return Directory.GetFiles(root, "*.xlsx", SearchOption.AllDirectories)
                .Where(file => !Normalize(IOPath.GetRelativePath(root, file)).StartsWith("_configs/", StringComparison.OrdinalIgnoreCase))
                .Where(file => !Normalize(IOPath.GetRelativePath(root, file)).StartsWith("_temp/", StringComparison.OrdinalIgnoreCase))
                .Where(file => !IOPath.GetFileName(file).StartsWith("~$", StringComparison.Ordinal));
            IEnumerable<DataTableUnitSettingBase> units = target == "text" ? settings.TextUnitsSettings.Cast<DataTableUnitSettingBase>()
                : settings.FontUnitsSettings.Cast<DataTableUnitSettingBase>();
            return units.Select(unit => IOPath.GetFullPath(IOPath.Combine(root, unit.SourcePath)));
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
                IEnumerable<string> files = EnumerateSourceFiles(settings, target, sourcePath);
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
            deletes.Add(counterpart);
            deletes.Add(counterpart + ".meta");
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
        /// 只读已保存场景的唯一 LocalizationComponent，严格解包设置快照并保留原始组件 JSON 的冻结摘要。
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
            if (!EditorObjectJson.TryReadSnapshot(json, nameof(LocalizationComponent), "m_LocalizationSettings", out Snapshot snapshot, out error))
                return false;
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
