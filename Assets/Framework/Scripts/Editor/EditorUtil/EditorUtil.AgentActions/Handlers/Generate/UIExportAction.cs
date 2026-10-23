/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIExportAction.cs
 * author:    taoye
 * created:   2026/10/12
 * descrip:   UI 单次受控导出，冻结来源、正式输出与编译后收据
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using IOPath = System.IO.Path;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 复用 UI 公共导出入口，不编辑配置、Excel、Prefab 或生成文件正文。
    /// </summary>
    [AgentAction("nova.project.ui.export", "导出 UI 注册表", "ui", AgentActionOperationType.Generate,
        Description = "从当前场景冻结 UI 配置与 Excel，单次导出并在编译后只读验证收据。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite | AgentActionEffect.Destructive,
        RequiredEvidence = AgentActionEvidence.Compile | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput, RequiresConfirmation = true, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "asset-database", "active-scene", "ui-registration", "ui-export" })]
    internal sealed class UIExportAction : AgentActionHandler<UIExportAction.Request>
    {
        private const string c_ActionId = "nova.project.ui.export";

        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string scope;
            public string sourcePath;
        }

        [Serializable]
        internal sealed class FileState
        {
            public string path;
            public bool exists;
            public string sha256;
        }

        [Serializable]
        internal sealed class Receipt
        {
            public string scope;
            public string sourcePath;
            public string scenePath;
            public string componentId;
            public string settingsHash;
            public string masterGuid;
            public string masterPath;
            public string topModule;
            public string sourceDirectory;
            public string codeDirectory;
            public string[] replacements;
            public string[] deletions;
            public FileState[] inputs;
            public FileState[] protectedFiles;
            public string[] templateDirectories;
            public FileState[] outputs;
            public string compilationToken;
        }

        internal sealed class Preview
        {
            public UISettings Settings;
            public string[] Replacements;
            public string[] Deletions;
            public string[] CodeFiles;
            public string CodeDirectory;
        }

        private sealed class State
        {
            public Preview Preview;
            public Receipt Receipt;
            public FileState[] BeforeOutputs;
        }

        /// <summary>
        /// 只接受 all/code/data 与单个规范相对 Excel 路径；单文件 all 没有原子领域入口。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (!DataModuleExportActionCommon.TryNormalizeScope(request?.scope, out string scope))
                error = "scope 只允许 all、code 或 data。";
            else if (request.sourcePath != null && !IsSourcePath(request.sourcePath))
                error = "sourcePath 必须是规范相对 Excel 路径；全量导出请省略此字段。";
            else if (request.sourcePath != null && scope == "all")
                error = "单文件只允许 code 或 data；现有 UI 入口不提供原子单文件 all。";
            return error == null;
        }

        /// <summary>
        /// 只读冻结场景身份、完整设置、全部输入和准确替换/删除范围，不同步 _configs。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryValidateRequest(request, out string error)) return Blocked(error);
            if (GeneratedExportCompilationState.IsPending()) return Blocked("已有导出编译待完成，请先验证该收据。");
            if (!UISettingsResolver.TryResolve(out UISettingsResolver.Resolved resolved, out error)) return Blocked(error);
            try
            {
                // 扫描前后各核一次来源，避免旧 Schema 预览与扫描后的新文件 Hash 组合成有效计划。
                FileState[] initialInputs = CaptureStates(EnumerateInputFiles(resolved.Settings, resolved), context.CancellationToken);
                if (!TryBuildPreview(resolved.Settings, request, out Preview preview, out error) ||
                    !TryVerifyStates(initialInputs, context.CancellationToken, out error)) return Blocked(error);
                string scope = request.scope.Trim().ToLowerInvariant();
                var receipt = new Receipt
                {
                    scope = scope, sourcePath = request.sourcePath,
                    scenePath = resolved.ScenePath, componentId = resolved.ComponentId, settingsHash = resolved.SettingsHash,
                    masterGuid = resolved.MasterGuid, masterPath = resolved.MasterPath, topModule = resolved.TopModule,
                    sourceDirectory = preview.Settings.SourceDirPath, codeDirectory = preview.CodeDirectory,
                    replacements = preview.Replacements, deletions = preview.Deletions,
                    inputs = initialInputs,
                    protectedFiles = CaptureStates(EnumerateProtectedFiles(preview), context.CancellationToken),
                    templateDirectories = EditorUtil.Luban.ExportHelper.GetLubanCustomTemplateDirs(EditorUtil.Luban.LubanExportProfiles.UI.TemplateKey) ?? Array.Empty<string>(),
                    outputs = Array.Empty<FileState>(),
                };
                FileState[] before = CaptureStates(EnumerateWritePaths(preview), context.CancellationToken);
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "ready", Summary = $"UI {scope} 导出：{request.sourcePath ?? "全部 Unit"}；替换 {preview.Replacements.Length} 个文件，删除 {preview.Deletions.Length} 个路径。",
                    DataJson = Util.Json.Serialize(receipt), RecoveryPayloadJson = Util.Json.Serialize(receipt),
                    State = new State { Preview = preview, Receipt = receipt, BeforeOutputs = before },
                    WriteSet = DataModuleExportActionCommon.BuildWriteSet(EnumerateWritePaths(preview)),
                    Evidence = new[] { "只读扫描完整 UI Excel；已冻结持久场景、配置命名空间、共享 UITables、_configs、生成文件所有权和反格式删除范围。", "导出器可能更新 _configs 后失败；正式数据/代码由既有 OutputApplier 事务发布。" },
                });
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { return Blocked(exception.Message); }
        }

        /// <summary>
        /// 复核冻结输入后单次调用公共 UI Exporter，保留执行后收据并安排本次编译。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen)) return Result("blocked", "UI 导出冻结状态无效。");
            if (GeneratedExportCompilationState.IsPending()) return Result("blocked", "已有导出编译待完成。");
            Receipt receipt = frozen.Receipt;
            if (!TryMatchResolved(receipt, out string error) ||
                !TryVerifyStates(receipt.inputs, context.CancellationToken, out error) ||
                !TryVerifyStates(frozen.BeforeOutputs, context.CancellationToken, out error) ||
                !TryVerifyStates(receipt.protectedFiles, context.CancellationToken, out error) ||
                !TryVerifyDirectoryMembership(receipt, out error))
                return Result("blocked", error ?? "UI 输入或输出已漂移，请重新 Plan。");
            try
            {
                if (!TryBuildPreview(frozen.Preview.Settings, new Request { scope = receipt.scope, sourcePath = receipt.sourcePath },
                        out Preview current, out error) || !SamePaths(current.Replacements, receipt.replacements) || !SamePaths(current.Deletions, receipt.deletions))
                    return Result("blocked", error ?? "UI 输出范围已漂移，请重新 Plan。");
                if (!TryVerifyStates(receipt.inputs, context.CancellationToken, out error) ||
                    !TryVerifyStates(frozen.BeforeOutputs, context.CancellationToken, out error)) return Result("blocked", error);
                context.CancellationToken.ThrowIfCancellationRequested();
                bool success = Export(frozen.Preview.Settings, receipt.scope, receipt.sourcePath, frozen.Preview.CodeDirectory);
                if (!success) return Result("partial", "UI Exporter 返回失败；_configs 可能已更新，Action 不会自动重放。");
                RequireCleanWorkspace(receipt.sourceDirectory);
                if (!TryVerifyStates(receipt.inputs.Where(item => !IsPlannedReplacement(item.path, receipt)).ToArray(), context.CancellationToken, out error) ||
                    !TryVerifyStates(receipt.protectedFiles, context.CancellationToken, out error) ||
                    !TryVerifyDirectoryMembership(receipt, out error))
                    return Result("partial", "UI 已导出但来源或范围外文件发生变化：" + error);
                receipt.outputs = CaptureStates(receipt.replacements.Concat(receipt.deletions), context.CancellationToken);
                if (receipt.replacements.Any(path => !receipt.outputs.Any(item => item.path == path && item.exists)) ||
                    receipt.deletions.Any(path => receipt.outputs.Any(item => item.path == path && item.exists)))
                    return Result("partial", "UI 导出后缺少计划替换文件或计划删除未完成。");
                if (!TryVerifyExportedConfiguration(receipt, out error)) return Result("partial", error);
                receipt.compilationToken = Guid.NewGuid().ToString("N");
                string json = Util.Json.Serialize(receipt);
                GeneratedExportCompilationState.Schedule(receipt.compilationToken, GenerateActionCommon.ComputeTextHash(json));
                AgentActionResult result = AgentActionResult.Create(c_ActionId, "partial", "UI 已导出，等待本次 Unity 编译及代码重载后 Verify。");
                result.ReceiptJson = result.DataJson = json;
                result.Artifacts.AddRange(receipt.outputs.Where(item => item.exists).Select(item => item.path));
                result.Evidence.Add("Execute 仅调用一次现有 UI 公共导出器，并绑定执行后完整文件摘要与本次编译令牌。");
                return Task.FromResult(result);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { return Result("partial", "UI 执行或产物取证失败，不会重放：" + exception.Message); }
        }

        /// <summary>
        /// 编译/重载后只读核对收据身份、精确输出与删除结果，禁止重新导出。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try
            {
                Receipt receipt = Util.Json.Deserialize<Receipt>(receiptJson);
                if (receipt?.outputs == null || receipt.outputs.Length == 0 || receipt.replacements == null || receipt.deletions == null)
                    return Result("partial", "UI Receipt 尚无完整执行后产物证据；不会重放 Execute。");
                if (!GeneratedExportCompilationState.TryVerify(receipt.compilationToken, GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(receipt)),
                        DataModuleExportActionCommon.IncludesCode(receipt.scope), out string error)) return Result("partial", error);
                if (!TryMatchResolved(receipt, out error)) return Result("partial", error);
                RequireCleanWorkspace(receipt.sourceDirectory);
                if (!TryVerifyExportedConfiguration(receipt, out error)) return Result("partial", error);
                string[] expected = DataModuleExportActionCommon.BuildWriteSet(receipt.replacements.Concat(receipt.deletions));
                if (!SamePaths(receipt.outputs.Select(item => item.path), expected) ||
                    !TryVerifyStates(receipt.outputs, context.CancellationToken, out error) ||
                    !TryVerifyStates(receipt.inputs.Where(item => !IsPlannedReplacement(item.path, receipt)).ToArray(), context.CancellationToken, out error) ||
                    !TryVerifyStates(receipt.protectedFiles, context.CancellationToken, out error) ||
                    !TryVerifyDirectoryMembership(receipt, out error))
                    return Result("partial", error ?? "UI Receipt 未准确覆盖全部计划输出。");
                if (DataModuleExportActionCommon.IncludesCode(receipt.scope) && receipt.replacements.Where(path => path.EndsWith(".cs", StringComparison.Ordinal)).Any(path =>
                        !EditorUtil.Luban.GeneratedOutput.IsOwnedAndUnmodified(path, EditorUtil.Luban.LubanExportProfiles.UI.Id, GenerateActionCommon.ToProjectRelative(receipt.sourceDirectory))))
                    return Result("partial", "UI 生成代码所有权或正文完整性未通过。");
                AgentActionResult result = AgentActionResult.Create(c_ActionId, "success", "UI 输出与删除范围已验证，本次 Unity 编译成功。");
                result.EvidenceKinds = AgentActionEvidence.Compile | AgentActionEvidence.Artifact;
                result.DataJson = Util.Json.Serialize(receipt.outputs);
                result.Artifacts.AddRange(receipt.outputs.Where(item => item.exists).Select(item => item.path));
                result.Evidence.Add("Verify 只读取当前场景、来源和文件摘要，不调用 Exporter、Refresh 或编译请求。Play/资源加载未由本 Action 验证。");
                return Task.FromResult(result);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { return Result("partial", "UI Receipt 验证失败：" + exception.Message); }
        }

        /// <summary>
        /// 只读建立与领域导出同源的精确文件预览；测试可注入 Excel 结构扫描器。
        /// </summary>
        internal static bool TryBuildPreview(UISettings settings, Request request, out Preview preview, out string error,
            Func<string, int, IReadOnlyList<string>> scanner = null)
        {
            preview = null; error = null;
            try
            {
                var handler = new UIExportAction();
                if (!handler.TryValidateRequest(request, out error)) return false;
                if (settings?.UIUnitsSettings == null || settings.UIUnitsSettings.Count == 0 || settings.UIUnitsSettings.Count > 128)
                    throw new InvalidDataException("UISettings 必须包含 1 至 128 个 Unit。");
                if (settings.DataFormat != LubanDataFormat.Json && settings.DataFormat != LubanDataFormat.Binary)
                    throw new InvalidDataException("UI DataFormat 只支持 Json 或 Binary。");
                if (!TryResolveWritablePath(settings.SourceDirPath, out string source, out error) || !Directory.Exists(source)) return false;
                RequireCleanWorkspace(source);
                UISettings snapshot = JsonUtility.FromJson<UISettings>(JsonUtility.ToJson(settings));
                snapshot.SourceDirPath = source;
                var assets = new HashSet<string>(StringComparer.Ordinal);
                var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var data = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var code = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (UIUnitSetting unit in snapshot.UIUnitsSettings)
                {
                    if (unit == null || !IsSourcePath(unit.SourcePath) || !sources.Add(unit.SourcePath)) throw new InvalidDataException("UI Unit SourcePath 必须是唯一的规范相对 Excel 路径。");
                    if (!DataModuleExportActionCommon.TryValidateSourceFile(source, unit.SourcePath, out error)) return false;
                    if (string.IsNullOrWhiteSpace(unit.AssetLocation) || !assets.Add(unit.AssetLocation.Trim())) throw new InvalidDataException("UI Unit AssetLocation 必须非空且唯一。");
                    if (!TryResolveWritablePath(unit.DatasExportPath, out string output, out error)) return false;
                    string extension = snapshot.DataFormat == LubanDataFormat.Binary ? ".bytes" : ".json";
                    if (!output.EndsWith(extension, StringComparison.OrdinalIgnoreCase) || !data.Add(output)) throw new InvalidDataException("UI 数据输出路径必须唯一，并与 DataFormat 扩展名一致。");
                    unit.DatasExportPath = output;
                    if (!TryResolveWritablePath(unit.ClassesExportPath, out string codePath, out error)) return false;
                    unit.ClassesExportPath = codePath;
                    code.Add(codePath);
                }
                if (code.Count != 1) throw new InvalidDataException("所有 UI Unit 必须使用同一个 ClassesExportPath。");
                string codeDirectory = code.Single();
                UIUnitSetting target = request.sourcePath == null ? null : snapshot.UIUnitsSettings.SingleOrDefault(unit => unit.SourcePath == request.sourcePath);
                if (request.sourcePath != null && target == null) throw new InvalidDataException("sourcePath 未精确匹配 UI Unit：" + request.sourcePath);
                if (request.scope.Trim().ToLowerInvariant() == "code" && snapshot.DataFormat == LubanDataFormat.Json &&
                    snapshot.UIUnitsSettings.Where(unit => target == null || unit == target).Any(unit => !File.Exists(unit.DatasExportPath)))
                    throw new InvalidDataException("code-only 的 Map 属性依赖已导出的正式 JSON；请先完成 data 导出。");
                ValidateConfigurationFile(source);
                if (scanner == null) UIExporter.UIExcelValidator.Validate(source, snapshot.UIUnitsSettings);
                EditorUtil.Luban.LubanExportProfile profile = EditorUtil.Luban.LubanExportProfiles.UI;
                EditorUtil.Luban.LubanSchemaManifest manifest = EditorUtil.Luban.LubanSchemaManifestBuilder.Build(source, profile.Id,
                    ((IDataTableSettings)snapshot).Units, profile.MinHeaderRowCount, scanner);
                var replacements = new List<string>();
                var deletions = new List<string>();
                var codeFiles = new List<string>();
                string scope = request.scope.Trim().ToLowerInvariant();
                if (DataModuleExportActionCommon.IncludesData(scope))
                    foreach (UIUnitSetting unit in snapshot.UIUnitsSettings.Where(unit => target == null || unit == target))
                    {
                        replacements.Add(unit.DatasExportPath);
                        string counterpart = IOPath.ChangeExtension(unit.DatasExportPath, snapshot.DataFormat == LubanDataFormat.Json ? ".bytes" : ".json");
                        deletions.Add(counterpart); deletions.Add(counterpart + ".meta");
                    }
                if (DataModuleExportActionCommon.IncludesCode(scope))
                {
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { profile.ManagerName + ".cs" };
                    foreach (EditorUtil.Luban.LubanSchemaUnit unit in manifest.Units.Where(unit => target == null || unit.SourcePath == target.SourcePath))
                        foreach (EditorUtil.Luban.LubanSchemaTable table in unit.Tables)
                        {
                            if (!IsIdentifier(table.ValueType) || !IsIdentifier(table.Name)) throw new InvalidDataException("UI 表类型必须是规范 C# 标识符。");
                            names.Add(table.Name + ".cs"); names.Add(table.ValueType + ".cs");
                        }
                    foreach (string name in names)
                    {
                        string path = IOPath.Combine(codeDirectory, name);
                        if (File.Exists(path) && !EditorUtil.Luban.GeneratedOutput.IsOwnedAndUnmodified(path, profile.Id, GenerateActionCommon.ToProjectRelative(source)))
                            throw new InvalidDataException("UI 代码目标缺少有效所有权或正文已被修改，请先处理冲突：" + path);
                        replacements.Add(path); codeFiles.Add(path);
                    }
                    if (target == null && Directory.Exists(codeDirectory))
                        foreach (string path in Directory.GetFiles(codeDirectory, "*.cs", SearchOption.TopDirectoryOnly))
                        {
                            if (names.Contains(IOPath.GetFileName(path)) || !EditorUtil.Luban.GeneratedOutput.IsOwnedAndUnmodified(path, profile.Id, GenerateActionCommon.ToProjectRelative(source))) continue;
                            deletions.Add(path);
                            if (File.Exists(path + ".meta")) deletions.Add(path + ".meta");
                        }
                    if (target == null) replacements.Add(IOPath.Combine(EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(source), "output-manifests", profile.Id + ".json"));
                }
                string configRoot = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(source);
                replacements.Add(IOPath.Combine(configRoot, EditorUtil.Luban.ConfigSyncer.c_LubanConfFileName));
                replacements.Add(IOPath.Combine(configRoot, EditorUtil.Luban.ConfigSyncer.c_TablesXmlFileName));
                replacements.Add(EditorUtil.Luban.LubanSchemaManifestStore.GetPath(source));
                string[] all = replacements.Concat(deletions).ToArray();
                foreach (string path in all)
                {
                    if (!TryResolveWritablePath(path, out _, out error)) return false;
                    if (Directory.Exists(path)) throw new InvalidDataException("UI 文件输出被目录占用：" + path);
                    if (snapshot.UIUnitsSettings.Any(unit => SamePath(path, IOPath.Combine(source, unit.SourcePath)))) throw new InvalidDataException("UI 输出不能覆盖 Excel 源文件。");
                    if (path.StartsWith(IOPath.Combine(source, "_temp") + IOPath.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException("UI 正式输出不能位于导出临时目录。");
                }
                if (replacements.Distinct(StringComparer.OrdinalIgnoreCase).Count() != replacements.Count ||
                    replacements.Intersect(deletions, StringComparer.OrdinalIgnoreCase).Any()) throw new InvalidDataException("UI 输出身份存在重叠。");
                preview = new Preview { Settings = snapshot, Replacements = DataModuleExportActionCommon.BuildWriteSet(replacements), Deletions = DataModuleExportActionCommon.BuildWriteSet(deletions), CodeFiles = codeFiles.ToArray(), CodeDirectory = codeDirectory };
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        /// <summary>
        /// 根据范围选择一个既有公共入口；不把单文件 code/data 两次调用拼成 all。
        /// </summary>
        private static bool Export(UISettings settings, string scope, string sourcePath, string codeDirectory)
        {
            if (sourcePath != null)
                return scope == "code"
                    ? EditorUtil.UI.Exporter.ExportCodeForFile(settings, settings.SourceDirPath, IOPath.Combine(settings.SourceDirPath, sourcePath), codeDirectory)
                    : EditorUtil.UI.Exporter.ExportDataForFile(settings, settings.SourceDirPath, IOPath.Combine(settings.SourceDirPath, sourcePath));
            return scope == "all" ? EditorUtil.UI.Exporter.ExportAll(settings, settings.SourceDirPath)
                : scope == "code" ? EditorUtil.UI.Exporter.ExportCode(settings, settings.SourceDirPath)
                : EditorUtil.UI.Exporter.ExportData(settings, settings.SourceDirPath);
        }

        /// <summary>
        /// 收集所有 Excel、场景、ConfigMaster 与既有 _configs；code 模式也冻结正式数据输入。
        /// </summary>
        private static IEnumerable<string> EnumerateInputFiles(UISettings settings, UISettingsResolver.Resolved resolved)
        {
            yield return resolved.ScenePath; yield return resolved.MasterPath;
            yield return "ProjectSettings/Nova/Globals.json";
            foreach (UIUnitSetting unit in settings.UIUnitsSettings)
            {
                if (unit == null) throw new InvalidDataException("UI Unit 不能为空。");
                yield return IOPath.Combine(settings.SourceDirPath, unit.SourcePath);
                yield return unit.DatasExportPath;
            }
            string config = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(settings.SourceDirPath);
            foreach (string name in new[] { EditorUtil.Luban.ConfigSyncer.c_LubanConfFileName, EditorUtil.Luban.ConfigSyncer.c_TablesXmlFileName, EditorUtil.Luban.LubanSchemaManifestStore.FileName }) yield return IOPath.Combine(config, name);
            foreach (string directory in EditorUtil.Luban.ExportHelper.GetLubanCustomTemplateDirs(EditorUtil.Luban.LubanExportProfiles.UI.TemplateKey) ?? Array.Empty<string>())
                foreach (string path in EnumerateSafeFiles(directory)) yield return path;
        }

        /// <summary>
        /// 展示正式文件及 Unity 为新输出目录/文件创建的元文件，避免隐式遗漏 _configs 等目录副作用。
        /// </summary>
        private static IEnumerable<string> EnumerateWritePaths(Preview preview)
        {
            foreach (string path in preview.Deletions) yield return path;
            foreach (string path in preview.Replacements)
            {
                yield return path; yield return path + ".meta";
                string directory = IOPath.GetDirectoryName(path);
                while (!string.IsNullOrEmpty(directory) && GenerateActionCommon.ToProjectRelative(directory).StartsWith("Assets/", StringComparison.Ordinal))
                {
                    if (!Directory.Exists(directory) || !File.Exists(directory + ".meta")) yield return directory + ".meta";
                    directory = IOPath.GetDirectoryName(directory);
                }
            }
        }

        /// <summary>
        /// 记录代码目录中计划范围外的文件，以及 data-only 不应修改的所有代码文件。
        /// </summary>
        private static IEnumerable<string> EnumerateProtectedFiles(Preview preview)
        {
            if (!Directory.Exists(preview.CodeDirectory)) yield break;
            var allowed = new HashSet<string>(preview.Replacements.Concat(preview.Deletions).Concat(preview.Replacements.Select(path => path + ".meta")), StringComparer.Ordinal);
            foreach (string path in EnumerateSafeFiles(preview.CodeDirectory)) if (!allowed.Contains(path)) yield return path;
        }

        /// <summary>
        /// 复查模板目录和范围外代码的完整文件集合，新增文件也使旧计划/收据失效。
        /// </summary>
        private static bool TryVerifyDirectoryMembership(Receipt receipt, out string error)
        {
            error = null;
            try
            {
                string[] currentTemplates = EditorUtil.Luban.ExportHelper.GetLubanCustomTemplateDirs(EditorUtil.Luban.LubanExportProfiles.UI.TemplateKey) ?? Array.Empty<string>();
                if (!SamePaths(receipt.templateDirectories ?? Array.Empty<string>(), currentTemplates))
                    throw new InvalidDataException("UI 实际模板目录已漂移。");
                string[] templateFiles = currentTemplates.SelectMany(EnumerateSafeFiles).ToArray();
                string[] expectedTemplates = receipt.inputs.Where(item => currentTemplates.Any(directory => item.path.StartsWith(IOPath.GetFullPath(directory) + IOPath.DirectorySeparatorChar, StringComparison.Ordinal)))
                    .Select(item => item.path).ToArray();
                if (!SamePaths(templateFiles, expectedTemplates)) throw new InvalidDataException("UI 模板文件集合已漂移。");
                var preview = new Preview { CodeDirectory = receipt.codeDirectory, Replacements = receipt.replacements, Deletions = receipt.deletions };
                if (!SamePaths(EnumerateProtectedFiles(preview), receipt.protectedFiles.Select(item => item.path)))
                    throw new InvalidDataException("UI 输出范围外的代码文件集合已漂移。");
            }
            catch (Exception exception) { error = exception.Message; }
            return error == null;
        }

        /// <summary>
        /// 递归读取前拒绝重解析点，避免模板或输出目录穿透项目边界。
        /// </summary>
        private static IEnumerable<string> EnumerateSafeFiles(string directory)
        {
            if (!GenerateActionCommon.TryResolveProjectPath(directory, "只读目录", out string root, out string error)) throw new InvalidDataException(error);
            foreach (string path in Directory.GetFileSystemEntries(root))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("目录包含符号链接或重解析点：" + path);
                if (Directory.Exists(path)) { foreach (string child in EnumerateSafeFiles(path)) yield return child; }
                else yield return path;
            }
        }

        /// <summary>
        /// 捕获规范项目内文件的存在性和 SHA-256，缺失路径也构成冻结状态。
        /// </summary>
        internal static FileState[] CaptureStates(IEnumerable<string> paths, CancellationToken cancellationToken)
        {
            var result = new List<FileState>();
            foreach (string path in DataModuleExportActionCommon.BuildWriteSet(paths))
            {
                if (!GenerateActionCommon.TryResolveProjectPath(path, "冻结文件", out string full, out string error)) throw new InvalidDataException(error);
                if (Directory.Exists(full)) throw new InvalidDataException("文件路径被目录占用：" + full);
                bool exists = File.Exists(full);
                result.Add(new FileState { path = full, exists = exists, sha256 = exists ? GenerateActionCommon.ComputeFileHash(full, cancellationToken) : null });
            }
            return result.GroupBy(item => item.path, StringComparer.Ordinal).Select(group => group.First()).OrderBy(item => item.path, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// 只读比较全部冻结文件；出现增删或正文变化即拒绝使用旧计划/收据。
        /// </summary>
        internal static bool TryVerifyStates(FileState[] states, CancellationToken cancellationToken, out string error)
        {
            error = null;
            if (states == null) { error = "文件冻结状态缺失。"; return false; }
            try
            {
                FileState[] actual = CaptureStates(states.Select(item => item.path), cancellationToken);
                if (actual.Length != states.Length || actual.Any(item => !states.Any(expected => expected.path == item.path && expected.exists == item.exists && expected.sha256 == item.sha256)))
                    error = "文件存在性或正文 Hash 已漂移。";
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { error = exception.Message; }
            return error == null;
        }

        /// <summary>
        /// 复核活动场景、组件和配置命名空间身份，不修复任何工作区绑定。
        /// </summary>
        private static bool TryMatchResolved(Receipt receipt, out string error)
        {
            if (!UISettingsResolver.TryResolve(out UISettingsResolver.Resolved current, out error)) return false;
            if (current.ScenePath != receipt.scenePath || current.ComponentId != receipt.componentId || current.SettingsHash != receipt.settingsHash ||
                current.MasterGuid != receipt.masterGuid || current.MasterPath != receipt.masterPath || current.TopModule != receipt.topModule)
                error = "UI 场景、组件、设置或 ConfigMaster 命名空间已漂移。";
            return error == null;
        }

        /// <summary>
        /// 限制自动导出的来源和输出到业务 Assets，避免写入包、缓存和框架本体。
        /// </summary>
        private static bool TryResolveWritablePath(string path, out string full, out string error)
        {
            full = null;
            if (string.IsNullOrWhiteSpace(path) || path.Any(character => char.IsControl(character) || character == '"'))
            { error = "UI 路径不能包含控制字符或引号。"; return false; }
            if (!GenerateActionCommon.TryResolveProjectPath(path, "UI 路径", out full, out error)) return false;
            string relative = GenerateActionCommon.ToProjectRelative(full);
            if (!relative.StartsWith("Assets/", StringComparison.Ordinal) || relative.StartsWith("Assets/Framework/", StringComparison.Ordinal) || relative == "Assets/Framework")
            { error = "UI 自动导出路径必须位于业务 Assets，不能修改 Framework、Packages 或 Library。"; return false; }
            return true;
        }

        /// <summary>
        /// 确认 _temp 和流水线临时目录为空，避免删除用户既有临时内容。
        /// </summary>
        private static void RequireCleanWorkspace(string source)
        {
            string temp = IOPath.Combine(source, "_temp");
            if (Directory.Exists(temp) || File.Exists(temp) || File.Exists(temp + ".meta") ||
                Directory.GetFileSystemEntries(source, "_luban_temp_*").Length > 0)
                throw new InvalidDataException("UI 源目录存在既有临时内容，自动导出不会删除，请先处理：" + source);
        }

        /// <summary>
        /// 既有 luban.conf 必须遵守 UI 专用输入契约，不能借配置额外导入未知来源。
        /// </summary>
        private static void ValidateConfigurationFile(string source)
        {
            string path = IOPath.Combine(EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(source), EditorUtil.Luban.ConfigSyncer.c_LubanConfFileName);
            if (!GenerateActionCommon.TryResolveProjectPath(path, "UI luban.conf", out _, out string error)) throw new InvalidDataException(error);
            if (!File.Exists(path)) return;
            JObject value = JObject.Parse(File.ReadAllText(path));
            string[] fields = { "dataDir", "groups", "schemaFiles", "targets" };
            if (value.Properties().Any(item => !fields.Contains(item.Name, StringComparer.Ordinal)) || (string)value["dataDir"] != ".." ||
                !(value["schemaFiles"] is JArray schemas) || schemas.Count != 1 || !(schemas[0] is JObject schema) ||
                (string)schema["fileName"] != EditorUtil.Luban.ConfigSyncer.c_TablesXmlFileName || (string)schema["type"] != "" ||
                schema.Properties().Any(item => !new[] { "fileName", "type" }.Contains(item.Name, StringComparer.Ordinal)) ||
                !(value["targets"] is JArray targets) || targets.Count != 1 || !(targets[0] is JObject target) ||
                target.Properties().Any(item => !new[] { "name", "manager", "groups", "topModule" }.Contains(item.Name, StringComparer.Ordinal)))
                throw new InvalidDataException("UI luban.conf 含非标准配置，无法冻结全部输入；请先核实项目定制配置。");
        }

        /// <summary>
        /// 同步器可能吞写入错误，因此执行后必须核对实际 target、manager 和命名空间。
        /// </summary>
        private static bool TryVerifyExportedConfiguration(Receipt receipt, out string error)
        {
            error = null;
            try
            {
                ValidateConfigurationFile(receipt.sourceDirectory);
                string path = IOPath.Combine(EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(receipt.sourceDirectory), EditorUtil.Luban.ConfigSyncer.c_LubanConfFileName);
                JObject value = JObject.Parse(File.ReadAllText(path));
                JObject target = (JObject)((JArray)value["targets"])[0];
                EditorUtil.Luban.LubanExportProfile profile = EditorUtil.Luban.LubanExportProfiles.UI;
                if ((string)target["name"] != profile.TargetName || (string)target["manager"] != profile.ManagerName || (string)target["topModule"] != receipt.topModule)
                    throw new InvalidDataException("UI 导出后的 luban.conf target、manager 或命名空间不匹配。");
            }
            catch (Exception exception) { error = exception.Message; }
            return error == null;
        }

        /// <summary>
        /// 判定执行期间允许更新的配置与数据输出，其他输入必须保持原 Hash。
        /// </summary>
        private static bool IsPlannedReplacement(string path, Receipt receipt) => receipt.replacements.Contains(path, StringComparer.Ordinal);

        /// <summary>
        /// 校验简单 Excel 相对路径，拒绝导航、绝对路径和空片段。
        /// </summary>
        private static bool IsSourcePath(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 1024 && !value.Any(character => char.IsControl(character) || character == '"') &&
            !IOPath.IsPathRooted(value) && !value.Contains("\\") && !value.Contains(":") && value.Split('/').All(part => part.Length > 0 && part != "." && part != "..") &&
            (value.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".xls", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 只接受无需路径或额外命名空间解释的表类型标识符。
        /// </summary>
        private static bool IsIdentifier(string value) => !string.IsNullOrEmpty(value) && (char.IsLetter(value[0]) || value[0] == '_') && value.All(character => char.IsLetterOrDigit(character) || character == '_');

        /// <summary>
        /// 比较完整规范路径身份。
        /// </summary>
        private static bool SamePath(string left, string right) => string.Equals(IOPath.GetFullPath(left), IOPath.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 比较无重复的精确文件集合。
        /// </summary>
        private static bool SamePaths(IEnumerable<string> left, IEnumerable<string> right) => left.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(right.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal);

        /// <summary>
        /// 产生没有可执行状态的阻断计划。
        /// </summary>
        private static Task<AgentActionHandlerPlan> Blocked(string error) => Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = error });

        /// <summary>
        /// 产生不附加未经验证证据的结果。
        /// </summary>
        private static Task<AgentActionResult> Result(string status, string message) => Task.FromResult(AgentActionResult.Create(c_ActionId, status, message));
    }

    /// <summary>
    /// 从已保存活动场景和持久工作区锚点解析 UI 设置，不调用场景路由或修复入口。
    /// </summary>
    internal static class UISettingsResolver
    {
        [Serializable] private sealed class Snapshot { public UISettings m_UISettings = null; }
        internal sealed class Resolved
        {
            public string ScenePath;
            public string ComponentId;
            public string SettingsHash;
            public string MasterGuid;
            public string MasterPath;
            public string TopModule;
            public UISettings Settings;
        }

        /// <summary>
        /// 拒绝未保存/脏场景、非唯一 Nova 或 UIComponent，以及无持久 Namespace 的 Master。
        /// </summary>
        internal static bool TryResolve(out Resolved resolved, out string error)
        {
            resolved = null; error = null;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path) || scene.isDirty)
            { error = "活动场景必须已加载、已保存且无未保存改动。"; return false; }
            Nova[] novas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Nova>(true)).ToArray();
            if (novas.Length != 1) { error = "活动场景必须恰好包含一个 Nova。"; return false; }
            UIComponent[] components = novas[0].GetComponentsInChildren<UIComponent>(true);
            if (components.Length != 1) { error = "Nova 层级必须恰好包含一个 UIComponent。"; return false; }
            if (!TryReadSettingsJson(EditorJsonUtility.ToJson(components[0], false), out UISettings settings, out error)) return false;
            if (!EditorUtil.Config.WorkspaceActive.TryGetPersistedConfigMaster(out ConfigMasterSO master, out string guid, out string path, out error)) return false;
            if (EditorUtility.IsDirty(master) || string.IsNullOrWhiteSpace(master.Namespace) ||
                master.Namespace.Split('.').Any(part => string.IsNullOrEmpty(part) || !(char.IsLetter(part[0]) || part[0] == '_') || part.Any(character => !(char.IsLetterOrDigit(character) || character == '_'))))
            { error = "ConfigMaster 必须已保存并配置规范 Namespace。"; return false; }
            string json = JsonUtility.ToJson(settings, false);
            resolved = new Resolved
            {
                ScenePath = scene.path, ComponentId = GlobalObjectId.GetGlobalObjectIdSlow(components[0]).ToString(),
                SettingsHash = GenerateActionCommon.ComputeTextHash(json), Settings = JsonUtility.FromJson<UISettings>(json),
                MasterGuid = guid, MasterPath = path, TopModule = master.Namespace,
            };
            return true;
        }

        /// <summary>
        /// 复用严格组件快照入口读取真实存在的 m_UISettings，兼容唯一 MonoBehaviour 包根及旧扁平根。
        /// </summary>
        internal static bool TryReadSettingsJson(string json, out UISettings settings, out string error)
        {
            settings = null; error = null;
            if (!EditorObjectJson.TryReadSnapshot(json, nameof(UIComponent), "m_UISettings", out Snapshot snapshot, out error)) return false;
            settings = snapshot?.m_UISettings;
            if (settings == null) error = "UIComponent.m_UISettings 未配置。";
            return error == null;
        }
    }
}
