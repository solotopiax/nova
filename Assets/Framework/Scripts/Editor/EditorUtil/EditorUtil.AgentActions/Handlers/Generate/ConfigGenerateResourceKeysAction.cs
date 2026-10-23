/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * filename:  ConfigGenerateResourceKeysAction.cs
 * descrip:   指定已保存 Master 的受控资源密钥源码生成
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Coord = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.Coord;
using Panels = NovaFramework.Editor.EditorUtil.Config.PanelEditing;
using Path = System.IO.Path;
using Editing = NovaFramework.Editor.EditorUtil.Config.DesignTimeEditing;

namespace NovaFramework.Editor
{
    /// <summary>单次调用现有生成器，不改运行时配置导出语义，不激活或切换 Master。</summary>
    [AgentAction("nova.project.config.generate-resource-keys", "生成客户端资源密钥", "config", AgentActionOperationType.Generate,
        Description = "按已保存的私有 Master 和明确坐标生成密钥源码与裁剪声明，编译后只读验收。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead |
            AgentActionEffect.UnityWrite | AgentActionEffect.Credential | AgentActionEffect.Destructive,
        RequiredEvidence = AgentActionEvidence.Compile | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.ReplaceGeneratedOutput, RequiresConfirmation = true, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "asset-database", "config-master", "resource-key-code-output" })]
    internal sealed class ConfigGenerateResourceKeysAction : AgentActionHandler<ConfigGenerateResourceKeysAction.Request>
    {
        private const string c_ActionId = "nova.project.config.generate-resource-keys";

        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string masterGuid;
            [AgentActionRequired] public string platform;
            [AgentActionRequired] public string channel;
            [AgentActionRequired] public string developMode;
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
            public Request request;
            public string masterPath;
            public string masterStateSha256;
            public string expectedStateSha256;
            public string buildTarget;
            public string assemblyGraphSha256;
            public string assemblyName;
            public string resolvedNamespace;
            public string sourcePath;
            public string previousSourcePath;
            public string linkerPath;
            public string[] replacements;
            public string[] deletions;
            public string[] createdDirectories;
            public FileState[] inputs;
            public FileState[] outputs;
            public bool executionStarted;
            public string compilationToken;
        }

        private sealed class State
        {
            internal Receipt Receipt;
            internal ResourceKeyCodeGenerator.GenerationPreview Preview;
            internal FileState[] BeforeOutputs;
        }

        /// <summary>只接受 Master GUID 与明确三维坐标；不允许 None 渠道、生成路径或密钥正文参数。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!GenerateActionCommon.TryValidateGuid(request?.masterGuid, out error)) return false;
            if (!GenerateActionCommon.TryParseCoordinate(request.platform, request.channel, request.developMode,
                out _, out ChannelType channel, out _, out error)) return false;
            if (channel != ChannelType.None) return true;
            error = "资源密钥生成需要明确的非 None 渠道。";
            return false;
        }

        /// <summary>只读冻结保存状态、真实程序集和精确文件迁移集；私有源码正文只驻留内部 State。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            try
            {
                if (!TryValidateRequest(request, out string error) ||
                    !ConfigPatchPanelsAction.TryLoad(request.masterGuid, true, out ConfigMasterSO master, out string path, out error))
                    return PlanResult("blocked", error);
                RequirePrivatePath(path);
                Coord coord = Coordinate(request);
                RequirePlatform(coord.Platform, EditorUserBuildSettings.activeBuildTarget.ToString());
                if (GeneratedExportCompilationState.IsPending()) return PlanResult("blocked", "已有导出编译尚未完成，请先完成其 Verify。");
                string stateHash = Panels.StateHash(master);
                ResourceKeyStore.Validate(master.ResourceEncryption);
                if (!master.ResourceEncryption.Packages.Any(package => package.Keys.Count > 0) &&
                    string.IsNullOrEmpty(master.ResourceEncryption.GeneratedCodeAssetPath))
                    return PlanResult("not_applicable", "该 Master 没有密钥且从未生成提供器，无需生成。");
                string plannedSource = ResourceKeyCodeGenerator.GetAssetPath(master);
                RequirePrivatePath(plannedSource);
                RequirePrivatePath(Path.GetDirectoryName(plannedSource).Replace('\\', '/') + "/link.xml");
                string previousPath = master.ResourceEncryption.GeneratedCodeAssetPath;
                if (!string.IsNullOrEmpty(previousPath))
                {
                    RequirePrivatePath(previousPath);
                    RequirePrivatePath(Path.GetDirectoryName(previousPath).Replace('\\', '/') + "/link.xml");
                }
                RequirePrivatePath("Assets/NovaGenerated/ResourceKeys/NovaProject.ResourceKeys.asmdef");
                ResourceKeyCodeGenerator.GenerationPreview preview = ResourceKeyCodeGenerator.PreviewGeneration(master, coord);
                if (!preview.Applicable) return PlanResult("not_applicable", "该 Master 没有密钥且从未生成提供器，无需生成。");
                RequirePrivatePath(preview.AssetPath);
                RequirePrivatePath(preview.LinkerPath);
                if (!string.IsNullOrEmpty(previousPath)) RequirePrivatePath(previousPath);
                foreach (string deletion in preview.Deletions) RequirePrivatePath(deletion);
                string[] directories = MissingDirectories(preview.OutputDirectory);
                string[] replacements = new[] { path, preview.AssetPath, preview.LinkerPath }
                    .Concat(new[] { preview.AssetPath + ".meta", preview.LinkerPath + ".meta" })
                    .Concat(directories.Select(directory => directory + ".meta")).Distinct(StringComparer.Ordinal).ToArray();
                string[] deletions = preview.Deletions.SelectMany(file => new[] { file, file + ".meta" }).ToArray();
                string[] inputPaths = new[] { path, path + ".meta", "Assets/NovaGenerated/ResourceKeys/NovaProject.ResourceKeys.asmdef",
                    "Assets/NovaGenerated/ResourceKeys/NovaProject.ResourceKeys.asmdef.meta" }
                    .Concat(ParentMetaPaths(preview.OutputDirectory))
                    .Concat(string.IsNullOrEmpty(previousPath) ? Array.Empty<string>() : new[]
                        { previousPath, previousPath + ".meta", Path.GetDirectoryName(previousPath).Replace('\\', '/') + "/link.xml",
                          Path.GetDirectoryName(previousPath).Replace('\\', '/') + "/link.xml.meta" })
                    .Distinct(StringComparer.Ordinal).ToArray();
                FileState[] inputs = CaptureStates(inputPaths, context.CancellationToken);
                FileState[] beforeOutputs = CaptureStates(replacements.Concat(deletions), context.CancellationToken);
                if (beforeOutputs.Any(file => file.exists && (File.GetAttributes(file.path) & FileAttributes.ReadOnly) != 0))
                    return PlanResult("blocked", "计划写入或迁移删除的目标存在只读文件，请先处理目标可写状态。");
                string graph = AssemblyGraphHash(preview.AssetPath, previousPath);
                var clone = UnityEngine.Object.Instantiate(master);
                string expected;
                try
                {
                    clone.ResourceEncryption.ClientCodeOutputDirectory = preview.OutputDirectory;
                    clone.ResourceEncryption.GeneratedCodeAssetPath = preview.AssetPath;
                    expected = Panels.StateHash(clone);
                }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
                if (Panels.StateHash(master) != stateHash || EditorUtility.IsDirty(master) || Editing.HasUnsavedDraft(master) ||
                    !VerifyStates(inputs, context.CancellationToken) || !VerifyStates(beforeOutputs, context.CancellationToken))
                    return PlanResult("blocked", "只读预演期间目标状态漂移，请重新计划。");
                var receipt = new Receipt
                {
                    request = new Request { masterGuid = request.masterGuid, platform = coord.Platform.ToString(), channel = coord.Channel.ToString(), developMode = coord.Mode.ToString() },
                    masterPath = path, masterStateSha256 = stateHash, expectedStateSha256 = expected,
                    buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(), assemblyGraphSha256 = graph,
                    assemblyName = preview.AssemblyName, resolvedNamespace = preview.Namespace,
                    sourcePath = preview.AssetPath, previousSourcePath = previousPath, linkerPath = preview.LinkerPath,
                    replacements = replacements, deletions = deletions, createdDirectories = directories,
                    inputs = inputs, outputs = Array.Empty<FileState>(),
                };
                string json = Util.Json.Serialize(receipt);
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "ready", Summary = "将已保存 Master 的客户端资源密钥生成到 " + preview.AssetPath + "，实际 Player 程序集为 " + preview.AssemblyName + "。",
                    DataJson = json, RecoveryPayloadJson = json,
                    State = new State { Receipt = receipt, Preview = preview, BeforeOutputs = beforeOutputs },
                    WriteSet = replacements.Concat(deletions).Concat(directories).Distinct(StringComparer.Ordinal).ToArray(),
                    Evidence = new[] { "Plan 未保存、生成、删除或刷新；仅冻结完整配置摘要、精确物理写集和当前 Player 程序集。密钥及生成正文不进入返回载荷。" },
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (ResourceKeyCodeGenerator.PreviewConflictException exception) { return PlanResult("blocked", exception.Message); }
            catch { return PlanResult("blocked", "资源密钥计划未通过保存状态、私有路径、配置、手写冲突或程序集校验；未开始生成。请核对 Master 与独立客户端输出目录。"); }
        }

        /// <summary>提交前重新核对冻结输入，单次 Generate 后仅保存该 Master 的生成路径状态。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen)) return Result(null, "blocked", "资源密钥冻结状态无效。");
            Receipt receipt = frozen.Receipt;
            if (receipt.executionStarted) return Result(receipt, "partial", "本次生成已开始，禁止重放 Execute；请使用 Verify。");
            try
            {
                Coord coord = Coordinate(receipt.request);
                RequirePlatform(coord.Platform, receipt.buildTarget);
                if (GeneratedExportCompilationState.IsPending() ||
                    !ConfigPatchPanelsAction.TryLoad(receipt.request.masterGuid, true, out ConfigMasterSO master, out string path, out _) ||
                    path != receipt.masterPath || Panels.StateHash(master) != receipt.masterStateSha256 ||
                    !VerifyStates(receipt.inputs, context.CancellationToken) || !VerifyStates(frozen.BeforeOutputs, context.CancellationToken) ||
                    AssemblyGraphHash(receipt.sourcePath, master.ResourceEncryption.GeneratedCodeAssetPath) != receipt.assemblyGraphSha256)
                    return Result(receipt, "blocked", "Master、草稿、目标文件或程序集已漂移，请重新计划。");
                foreach (string directory in receipt.createdDirectories)
                    if (Directory.Exists(directory)) return Result(receipt, "blocked", "计划创建的目录已出现，请重新计划。");
                ResourceKeyCodeGenerator.GenerationPreview current = ResourceKeyCodeGenerator.PreviewGeneration(master, coord);
                if (!SamePreview(current, frozen.Preview)) return Result(receipt, "blocked", "生成预览与冻结写集已漂移，请重新计划。");
                context.CancellationToken.ThrowIfCancellationRequested();
                receipt.executionStarted = true;
                ResourceKeyCodeGenerator.Generate(master, coord);
                EditorUtility.SetDirty(master);
                AssetDatabase.SaveAssetIfDirty(master);
                if (EditorUtility.IsDirty(master) || Panels.StateHash(master) != receipt.expectedStateSha256 ||
                    File.ReadAllText(receipt.sourcePath) != current.Code || File.ReadAllText(receipt.linkerPath) != current.Linker ||
                    receipt.deletions.Any(File.Exists) || receipt.createdDirectories.Any(directory => !Directory.Exists(directory)))
                    return Result(receipt, "partial", "生成已开始但保存状态或精确文件结果未通过核验；禁止重放 Execute。");
                receipt.outputs = CaptureStates(receipt.replacements.Concat(receipt.deletions), context.CancellationToken);
                if (receipt.replacements.Any(file => !receipt.outputs.Any(output => output.path == file && output.exists)) ||
                    !VerifyProtectedInputs(receipt, context.CancellationToken) ||
                    !VerifyStates(frozen.BeforeOutputs.Where(file => file.exists && file.path.EndsWith(".meta", StringComparison.Ordinal) &&
                        receipt.replacements.Contains(file.path)).ToArray(), context.CancellationToken))
                    return Result(receipt, "partial", "生成结果缺少计划文件或范围外输入发生变化；禁止重放 Execute。");
                receipt.compilationToken = Guid.NewGuid().ToString("N");
                string payload = Util.Json.Serialize(receipt);
                GeneratedExportCompilationState.Schedule(receipt.compilationToken, GenerateActionCommon.ComputeTextHash(payload));
                return Result(receipt, "partial", "资源密钥源码与 link.xml 已生成并保存；等待本次 Unity 编译及脚本重载后 Verify。");
            }
            catch (OperationCanceledException) when (!receipt.executionStarted) { throw; }
            catch { return Result(receipt, receipt.executionStarted ? "partial" : "blocked", receipt.executionStarted
                ? "资源密钥生成或保存已开始但未完成核验；不会重放，请使用 Verify。"
                : "资源密钥提交前复核失败，未开始生成。"); }
        }

        /// <summary>编译稳定后只读核对指定 Master、源码、link、删除结果、唯一提供器与实际程序集。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try
            {
                Receipt receipt = Util.Json.Deserialize<Receipt>(receiptJson);
                if (receipt == null || !receipt.executionStarted || receipt.outputs == null || receipt.outputs.Length == 0 ||
                    receipt.replacements == null || receipt.deletions == null || receipt.createdDirectories == null ||
                    !TryValidateRequest(receipt.request, out _)) return Result(null, "partial", "Receipt 尚无完整执行后证据，不会重放生成。");
                RequirePrivatePath(receipt.masterPath); RequirePrivatePath(receipt.sourcePath); RequirePrivatePath(receipt.linkerPath);
                Coord coord = Coordinate(receipt.request);
                RequirePlatform(coord.Platform, receipt.buildTarget);
                if (!GeneratedExportCompilationState.TryVerify(receipt.compilationToken,
                    GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(receipt)), true, out string error)) return Result(receipt, "partial", error);
                if (!ConfigPatchPanelsAction.TryLoad(receipt.request.masterGuid, true, out ConfigMasterSO master, out string path, out _) ||
                    path != receipt.masterPath || Panels.StateHash(master) != receipt.expectedStateSha256 ||
                    master.ResourceEncryption.GeneratedCodeAssetPath != receipt.sourcePath ||
                    ResourceKeyCodeGenerator.GetAssetPath(master) != receipt.sourcePath ||
                    Path.GetDirectoryName(receipt.sourcePath).Replace('\\', '/') + "/link.xml" != receipt.linkerPath ||
                    EditorUtil.Config.DimensionalResolver.ResolveNamespace(master, coord.Platform, coord.Channel, coord.Mode) != receipt.resolvedNamespace ||
                    AssemblyGraphHash(receipt.sourcePath, receipt.previousSourcePath) != receipt.assemblyGraphSha256)
                    return Result(receipt, "partial", "保存配置、坐标、生成路径或 Player 程序集与 Receipt 不一致。");
                string[] expectedPaths = receipt.replacements.Concat(receipt.deletions).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                if (!receipt.outputs.Select(output => output.path).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expectedPaths) ||
                    !VerifyStates(receipt.outputs, context.CancellationToken) || !VerifyProtectedInputs(receipt, context.CancellationToken) ||
                    receipt.deletions.Any(File.Exists) || receipt.createdDirectories.Any(directory => !Directory.Exists(directory)))
                    return Result(receipt, "partial", "执行后文件摘要、删除结果或范围外输入未通过只读核验。");
                string owner = Path.GetFileNameWithoutExtension(CompilationPipeline.GetAssemblyNameFromScriptPath(receipt.sourcePath));
                if (owner != receipt.assemblyName) return Result(receipt, "partial", "生成源码实际所属程序集已变化。");
                ResourceKeyCodeGenerator.ValidateGenerated(master, coord);
                AgentActionResult result = CreateResult(receipt, "success", "指定 Master 的资源密钥源码、link.xml、唯一提供器与实际 Player 程序集已在本次编译重载后只读核对。");
                result.EvidenceKinds = AgentActionEvidence.Compile | AgentActionEvidence.Artifact;
                result.Evidence.Add("Verify 不生成、不保存、不刷新；不改变激活 Master 或 ConfigRuntime。此结果不证明 Bundle、Player 或真机加载。");
                return Task.FromResult(result);
            }
            catch (OperationCanceledException) { throw; }
            catch { return Result(null, "partial", "资源密钥 Receipt 或只读一致性检查未通过；不会重放生成。"); }
        }

        /// <summary>将已通过请求校验的字符串坐标转为领域坐标，不读 Master 当前渠道和模式。</summary>
        private static Coord Coordinate(Request request)
        {
            if (!GenerateActionCommon.TryParseCoordinate(request.platform, request.channel, request.developMode,
                out PlatformType platform, out ChannelType channel, out DevelopMode mode, out _)) throw new InvalidOperationException();
            return new Coord(platform, channel, mode);
        }

        /// <summary>请求平台及精确 BuildTarget 必须保持一致，不自动切换 Unity 平台。</summary>
        private static void RequirePlatform(PlatformType platform, string buildTarget)
        {
            if (platform == PlatformType.None || EditorUtil.Config.ActivePlatform.Current != platform ||
                EditorUserBuildSettings.activeBuildTarget.ToString() != buildTarget) throw new InvalidOperationException();
        }

        /// <summary>受控入口仅允许无符号链接的业务 Assets 路径，拒绝 Framework、Packages 与公开包输出。</summary>
        internal static void RequirePrivatePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !GenerateActionCommon.TryResolveProjectPath(path, "私有资源密钥路径", out string absolute, out _))
                throw new InvalidOperationException();
            string relative = GenerateActionCommon.ToProjectRelative(absolute);
            if (relative != path || !relative.StartsWith("Assets/", StringComparison.Ordinal) ||
                relative.Equals("Assets/Framework", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("Assets/Framework/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("Assets/Packages/", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        }

        /// <summary>枚举实际尚未存在的父目录，使创建目录和 Unity 元文件进入计划写集。</summary>
        private static string[] MissingDirectories(string directory)
        {
            var result = new List<string>();
            for (string path = directory; path != "Assets" && !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path).Replace('\\', '/'))
                if (!Directory.Exists(path)) result.Add(path);
            return result.OrderBy(path => path.Count(character => character == '/')).ToArray();
        }

        /// <summary>只读冻结输出目录所有祖先元文件，保留已存在文件的 GUID 身份。</summary>
        private static IEnumerable<string> ParentMetaPaths(string directory)
        {
            for (string path = directory; path != "Assets" && !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path).Replace('\\', '/'))
                yield return path + ".meta";
        }

        /// <summary>冻结文件完整摘要及不存在状态，不读取正文到返回载荷。</summary>
        internal static FileState[] CaptureStates(IEnumerable<string> paths, CancellationToken token) => paths
            .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).Select(path =>
            {
                RequirePrivatePath(path);
                bool exists = File.Exists(path);
                return new FileState { path = path, exists = exists, sha256 = exists ? GenerateActionCommon.ComputeFileHash(path, token) : null };
            }).ToArray();

        /// <summary>逐文件核对不存在状态与完整 SHA-256，新增、删除、改写均使旧证据失效。</summary>
        internal static bool VerifyStates(FileState[] states, CancellationToken token)
        {
            if (states == null) return false;
            foreach (FileState state in states)
            {
                if (state == null) return false;
                RequirePrivatePath(state.path);
                bool exists = File.Exists(state.path);
                if (exists != state.exists || exists && GenerateActionCommon.ComputeFileHash(state.path, token) != state.sha256) return false;
            }
            return true;
        }

        /// <summary>只复核不属于计划写入或迁移删除的输入，避免把目标变化误判为漂移。</summary>
        private static bool VerifyProtectedInputs(Receipt receipt, CancellationToken token) => VerifyStates(receipt.inputs?
            .Where(input => !receipt.replacements.Contains(input.path) && !receipt.deletions.Contains(input.path)).ToArray(), token);

        /// <summary>比较当前与冻结生成结果及迁移集；私有正文仅在内存内部比较。</summary>
        private static bool SamePreview(ResourceKeyCodeGenerator.GenerationPreview first, ResourceKeyCodeGenerator.GenerationPreview second) =>
            first.Applicable == second.Applicable && first.AssetPath == second.AssetPath && first.LinkerPath == second.LinkerPath &&
            first.AssemblyName == second.AssemblyName && first.Namespace == second.Namespace && first.Code == second.Code &&
            first.Linker == second.Linker && first.Deletions.SequenceEqual(second.Deletions);

        /// <summary>冻结真实 Player 编译图、定义与热更列表；排除本次生成源码的预期成员变化。</summary>
        private static string AssemblyGraphHash(string sourcePath, string previousPath)
        {
            var parts = new List<string>();
            foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).OrderBy(item => item.name, StringComparer.Ordinal))
            {
                parts.Add(assembly.name + "|" + assembly.outputPath);
                parts.AddRange(assembly.defines.OrderBy(item => item, StringComparer.Ordinal));
                parts.AddRange(assembly.assemblyReferences.Select(item => item.name).OrderBy(item => item, StringComparer.Ordinal));
                parts.AddRange(assembly.compiledAssemblyReferences.OrderBy(item => item, StringComparer.Ordinal));
                parts.AddRange(assembly.sourceFiles.Select(path => Path.GetFullPath(path)).Where(path =>
                    path != Path.GetFullPath(sourcePath) && (string.IsNullOrEmpty(previousPath) || path != Path.GetFullPath(previousPath)))
                    .OrderBy(item => item, StringComparer.Ordinal));
                string definition = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
                if (!string.IsNullOrEmpty(definition) && File.Exists(definition)) parts.Add(GenerateActionCommon.ComputeFileHash(definition, CancellationToken.None));
                parts.Add("--assembly--");
            }
            parts.AddRange(HybridCLR.Editor.SettingsUtil.HotUpdateAssemblyFilesIncludePreserved.OrderBy(item => item, StringComparer.Ordinal));
            return GenerateActionCommon.ComputeTextHash(string.Join("\n", parts));
        }

        /// <summary>阻塞或无需执行计划不携带配置或异常正文。</summary>
        private static Task<AgentActionHandlerPlan> PlanResult(string status, string message) =>
            Task.FromResult(new AgentActionHandlerPlan { Status = status, Summary = message });

        /// <summary>只输出脱敏 Receipt 与正式物理产物地址，不输出源码、密钥或异常正文。</summary>
        private static Task<AgentActionResult> Result(Receipt receipt, string status, string message) =>
            Task.FromResult(CreateResult(receipt, status, message));

        /// <summary>组装脱敏结果对象，编译和真机证据由对应实际验证阶段声明。</summary>
        private static AgentActionResult CreateResult(Receipt receipt, string status, string message)
        {
            AgentActionResult result = AgentActionResult.Create(c_ActionId, status, message);
            if (receipt != null)
            {
                result.ReceiptJson = result.DataJson = Util.Json.Serialize(receipt);
                if (receipt.outputs != null) result.Artifacts.AddRange(receipt.outputs.Where(output => output.exists).Select(output => output.path));
            }
            return result;
        }
    }
}
