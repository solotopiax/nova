/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIBindingsConfigureAction.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   UIView 精确控件绑定与可选 partial 字段生成的受控接线
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Bindings = NovaFramework.Editor.EditorUtil.UIBindings;
using FileState = NovaFramework.Editor.UIBindingsActionCommon.FileState;

namespace NovaFramework.Editor
{
    /// <summary>只在显式请求时调用领域工具；生成与绑定分成独立计划，绝不跨重载继续写 Prefab。</summary>
    [AgentAction("nova.project.ui.configure-bindings", "配置界面控件绑定", "ui", AgentActionOperationType.Ensure,
        Description = "按明确对象身份绑定已有 UIView 的空引用字段，或单独生成工具归属的 partial 字段；生成编译后必须重新计划绑定。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.EnsureState, RequiresConfirmation = true, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "asset-database", "ui-bindings", "serialized-target-assets", "ui-binding-code-output" })]
    internal sealed class UIBindingsConfigureAction : AgentActionHandler<UIBindingsConfigureAction.Request>
    {
        internal const string Id = "nova.project.ui.configure-bindings";

        /// <summary>一次只选 bind 或 generate；参数来自同一 Prefab 的当前 Inspect，不接受源码或输出路径。</summary>
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string mode;
            [AgentActionRequired] public string prefabPath;
            [AgentActionRequired] public string viewObjectId;
            [AgentActionRequired] public Binding[] bindings;
        }

        /// <summary>精确字段、目标持久身份与类型；类型字符串只作领域对象引用类型核对，不是方法入口。</summary>
        [Serializable]
        public sealed class Binding
        {
            [AgentActionRequired] public string fieldName;
            [AgentActionRequired] public string targetObjectId;
            [AgentActionRequired] public string typeName;
        }

        /// <summary>保存原计划和实际提交证据；不含可恢复执行的领域计划或生成源码正文。</summary>
        [Serializable]
        internal sealed class Receipt
        {
            public Request request;
            public string prefabGuid;
            public string prefabHash;
            public string prefabStateHash;
            public string scriptPath;
            public string scriptHash;
            public string scriptMetaHash;
            public FileState[] protectedFiles;
            public string outputPath;
            public string outputHash;
            public string sourceSnapshotHash;
            public bool changed;
            public int changedFields;
            public bool executionStarted;
            public Bindings.BindingReceipt binding;
            public Bindings.GenerationReceipt generation;
            public FileState[] outputFiles;
            public string compilationToken;
            public string failureKind;
            public string recoveryPath;
        }

        /// <summary>可执行状态只驻留当前域；一次性消费后，后续只能拿原回执只读核验。</summary>
        private sealed class State
        {
            internal Receipt Receipt;
            internal int Consumed;
        }

        /// <summary>拒绝未知模式、空批次、重复字段与非规范对象身份；实际字段和类型仍由领域 API 整批核验。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (request == null || request.mode != "bind" && request.mode != "generate")
                error = "mode 只允许 bind 或 generate；源码生成与 Prefab 绑定必须分别计划。";
            else if (!UIBindingsActionCommon.IsPrefabPath(request.prefabPath) || !UIBindingsActionCommon.IsObjectId(request.viewObjectId))
                error = "请使用 Inspect 返回的准确 prefabPath 和 viewObjectId。";
            else if (request.bindings == null || request.bindings.Length < 1 || request.bindings.Length > 128 ||
                request.bindings.Any(binding => binding == null || binding.fieldName == null || binding.fieldName.Length > 128 ||
                    !Regex.IsMatch(binding.fieldName, @"\A[A-Za-z_][A-Za-z0-9_]*\z") ||
                    !UIBindingsActionCommon.IsObjectId(binding.targetObjectId) || string.IsNullOrEmpty(binding.typeName) ||
                    binding.typeName.Length > 2048 || binding.typeName.Any(char.IsControl) || !binding.typeName.Contains(',')) ||
                request.bindings.Select(binding => binding.fieldName).Distinct(StringComparer.Ordinal).Count() != request.bindings.Length)
                error = "bindings 必须包含一至一百二十八个唯一字段、准确目标 GlobalObjectId 和 AssemblyQualifiedName。";
            return error == null;
        }

        /// <summary>只读调用领域 Preview，冻结对象、脚本、完整 Prefab、生成归属与准确文件写集。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            try
            {
                if (!TryValidateRequest(request, out string error)) return PlanResult("blocked", error);
                if (!UIBindingsActionCommon.IsStableEditor() || GeneratedExportCompilationState.IsPending())
                    return PlanResult("blocked", "请先完成当前 Unity 编译、重载或已有导出的核验，再计划界面绑定。");
                context.CancellationToken.ThrowIfCancellationRequested();
                Bindings.Inspection inspection = Bindings.Inspect(request.prefabPath);
                var receipt = new Receipt { request = CopyRequest(request), prefabGuid = inspection.PrefabGuid };
                string[] writeSet;
                if (request.mode == "bind")
                {
                    Bindings.BindingPlan preview = Bindings.Preview(ToDomain(request));
                    receipt.prefabHash = preview.PrefabHash; receipt.prefabStateHash = preview.StateHash;
                    receipt.scriptPath = preview.ScriptPath; receipt.scriptHash = preview.ScriptHash; receipt.scriptMetaHash = preview.ScriptMetaHash;
                    receipt.sourceSnapshotHash = SourceHash(preview.Sources);
                    receipt.changedFields = preview.ChangedFields; receipt.changed = preview.ChangedFields > 0;
                    receipt.protectedFiles = CaptureInputs(receipt, context.CancellationToken);
                    writeSet = receipt.changed ? new[] { request.prefabPath } : Array.Empty<string>();
                }
                else
                {
                    Bindings.GenerationPlan preview = Bindings.PreviewGeneration(ToDomain(request));
                    receipt.prefabHash = preview.PrefabHash; receipt.prefabStateHash = preview.StateHash;
                    receipt.scriptPath = preview.ScriptPath; receipt.scriptHash = preview.ScriptHash; receipt.scriptMetaHash = preview.ScriptMetaHash;
                    receipt.outputPath = preview.OutputPath; receipt.outputHash = GenerateActionCommon.ComputeTextHash(preview.Content);
                    receipt.sourceSnapshotHash = SourceHash(preview.Sources); receipt.changed = preview.HasChanges;
                    receipt.protectedFiles = CaptureInputs(receipt, context.CancellationToken);
                    bool missingMeta = receipt.protectedFiles.Any(file => file.path == receipt.outputPath + ".meta" && !file.exists);
                    writeSet = (receipt.changed ? new[] { receipt.outputPath } : Array.Empty<string>())
                        .Concat(missingMeta ? new[] { receipt.outputPath + ".meta" } : Array.Empty<string>()).ToArray();
                }
                context.CancellationToken.ThrowIfCancellationRequested();
                if (!UIBindingsActionCommon.MatchesFiles(receipt.protectedFiles, context.CancellationToken) ||
                    !MatchesPreview(receipt, context.CancellationToken)) return PlanResult("blocked", "只读预演期间对象、脚本或文件已经漂移，请重新计划。");
                string json = JsonConvert.SerializeObject(receipt);
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "ready", Summary = request.mode == "bind"
                        ? receipt.changed ? $"将为一个 UIView 绑定 {receipt.changedFields} 个空字段，只保存原 Prefab。" : "请求字段已绑定同一对象，执行不保存 Prefab。"
                        : $"将单独核对并生成 {receipt.outputPath}；编译重载并 Verify 后，须新建 bind 计划。",
                    DataJson = json, RecoveryPayloadJson = json, State = new State { Receipt = receipt }, WriteSet = writeSet,
                    Evidence = new[] { "Plan 只调用领域检查和预演；冻结 GlobalObjectId、准确类型、Prefab/脚本及元数据摘要。生成仅允许工具归属 partial，不含任意源码、输出路径或方法请求。" },
                });
            }
            catch (OperationCanceledException) { throw; }
            catch { return PlanResult("blocked", "界面绑定计划未通过对象、字段、脚本归属、草稿或生成冲突检查；没有修改文件。"); }
        }

        /// <summary>一次性复核后只调用一个领域提交；generate 不绑定 Prefab，写后错误保留回执且禁止重放。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State plan) || Interlocked.Exchange(ref plan.Consumed, 1) != 0)
                return Result(null, "blocked", "界面绑定计划无效或已消费，请重新计划。");
            Receipt receipt = plan.Receipt;
            try
            {
                if (!UIBindingsActionCommon.IsStableEditor() || GeneratedExportCompilationState.IsPending() ||
                    !UIBindingsActionCommon.MatchesFiles(receipt.protectedFiles, context.CancellationToken) ||
                    !MatchesPreview(receipt, context.CancellationToken))
                    return Result(receipt, "blocked", "提交前对象、脚本、生成归属、目标文件或编译状态已漂移；没有开始写入。");
                context.CancellationToken.ThrowIfCancellationRequested();
                if (receipt.request.mode == "bind")
                {
                    Bindings.BindingPlan current = Bindings.Preview(ToDomain(receipt.request));
                    if (!SameBindingPreview(current, receipt)) return Result(receipt, "blocked", "新鲜绑定预览与已确认计划不一致；没有写入。");
                    receipt.executionStarted = true;
                    receipt.binding = Bindings.Apply(current);
                    receipt.outputFiles = UIBindingsActionCommon.CaptureFiles(new[] { receipt.request.prefabPath, receipt.request.prefabPath + ".meta" }, CancellationToken.None);
                    return Result(receipt, "partial", "本次绑定提交已结束，请只读 Verify 核对实际保存和范围外文件；不会重复提交。");
                }

                Bindings.GenerationPlan generation = Bindings.PreviewGeneration(ToDomain(receipt.request));
                if (!SameGenerationPreview(generation, receipt)) return Result(receipt, "blocked", "新鲜生成预览与已确认输出不一致；没有写入。");
                receipt.executionStarted = true;
                receipt.generation = Bindings.Generate(generation);
                if (!MatchesGenerationReceipt(receipt) || !Bindings.VerifyGeneration(receipt.generation) ||
                    !ProtectedInputsMatch(receipt, CancellationToken.None))
                    return Result(receipt, "partial", "字段生成已开始，但输出或保护输入未通过检查；保留原回执，禁止重放。");
                receipt.compilationToken = Guid.NewGuid().ToString("N");
                GeneratedExportCompilationState.Schedule(receipt.compilationToken, CompilationHash(receipt));
                // 只导入计划中的唯一源码；首次生成的 .meta 已列入写集。编译登记先于可能触发编译的导入。
                AssetDatabase.ImportAsset(receipt.outputPath, ImportAssetOptions.ForceUpdate);
                receipt.outputFiles = UIBindingsActionCommon.CaptureFiles(new[] { receipt.outputPath, receipt.outputPath + ".meta" }, CancellationToken.None);
                return Result(receipt, "partial", "字段源码已生成；等待本次 Unity 编译与脚本重载后 Verify，再新建 bind 计划。");
            }
            catch (OperationCanceledException) when (!receipt.executionStarted) { throw; }
            catch (Exception exception)
            {
                receipt.failureKind = exception is AggregateException ? "recovery-incomplete" : "submission-failed";
                if (exception.Data[Bindings.RecoveryPathDataKey] is string path &&
                    GenerateActionCommon.TryResolveProjectPath(path, "UI 绑定恢复目录", out string absolute, out _) &&
                    GenerateActionCommon.ToProjectRelative(absolute).StartsWith("Library/Nova/UIBindings/", StringComparison.Ordinal))
                    receipt.recoveryPath = GenerateActionCommon.ToProjectRelative(absolute);
                return Result(receipt, receipt.executionStarted ? "partial" : "blocked", receipt.executionStarted
                    ? "提交未完成验收；请保留回执及其中恢复目录，不重放 Execute。只读 Verify 不会自动恢复或再次生成。"
                    : "提交前校验失败，没有开始界面绑定或生成。");
            }
        }

        /// <summary>仅核对原提交、完整保存和本次编译；不生成、不绑定、不导入或恢复任何文件。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt = null;
            try
            {
                receipt = UIBindingsActionCommon.ParseReceipt<Receipt>(receiptJson);
                if (!TryValidateRequest(receipt.request, out _) || !HasCompleteReceipt(receipt) || !receipt.executionStarted || !string.IsNullOrEmpty(receipt.failureKind) ||
                    !UIBindingsActionCommon.IsStableEditor() || receipt.outputFiles == null ||
                    !UIBindingsActionCommon.MatchesFiles(receipt.outputFiles, context.CancellationToken) ||
                    !ProtectedInputsMatch(receipt, context.CancellationToken))
                    return Result(receipt, "partial", "回执尚无完整提交证据，或文件、草稿、编译与恢复状态不满足只读验收；不会重放。");
                context.CancellationToken.ThrowIfCancellationRequested();
                if (receipt.request.mode == "bind")
                {
                    if (!MatchesBindingReceipt(receipt) || !Bindings.Verify(receipt.binding))
                        return Result(receipt, "partial", "Prefab 字节、脚本或全部目标字段与原绑定回执不一致。");
                    AgentActionResult result = CreateResult(receipt, "success", "本次精确字段已保存，Prefab、脚本与范围外文件已只读核对。");
                    result.EvidenceKinds = AgentActionEvidence.Static | AgentActionEvidence.Artifact;
                    result.Evidence.Add("只核对原 Prefab 的准确字段；未证明页面运行、交互或事件行为。");
                    return Task.FromResult(result);
                }
                if (!MatchesGenerationReceipt(receipt) || !Bindings.VerifyGeneration(receipt.generation) ||
                    !GeneratedExportCompilationState.TryVerify(receipt.compilationToken, CompilationHash(receipt), true, out _))
                    return Result(receipt, "partial", "生成源码、原始来源或本次编译重载证据不足；请保留原回执，只读核验，不能直接绑定。");
                AgentActionResult generated = CreateResult(receipt, "success", "字段源码及原始来源已只读核对，本次编译重载通过；绑定须重新 Inspect 和创建新计划。");
                generated.EvidenceKinds = AgentActionEvidence.Static | AgentActionEvidence.Artifact | AgentActionEvidence.Compile;
                generated.Evidence.Add("本次仅完成字段生成；Prefab 仍保持原计划字节，未自动绑定或修改任何事件。");
                return Task.FromResult(generated);
            }
            catch (OperationCanceledException) { throw; }
            catch { return Result(receipt, "partial", "界面绑定回执或只读核验未通过；不会生成、绑定或自动恢复文件。"); }
        }

        /// <summary>重新取得领域计划并比对冻结材料；不消费计划，也不允许根据新状态扩大写集。</summary>
        private static bool MatchesPreview(Receipt receipt, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Bindings.Inspect(receipt.request.prefabPath).PrefabGuid != receipt.prefabGuid) return false;
            return receipt.request.mode == "bind" ? SameBindingPreview(Bindings.Preview(ToDomain(receipt.request)), receipt)
                : SameGenerationPreview(Bindings.PreviewGeneration(ToDomain(receipt.request)), receipt);
        }

        /// <summary>绑定预览必须保持全部对象状态、真实业务脚本及准确变化数量，不以字段名相同代替。</summary>
        private static bool SameBindingPreview(Bindings.BindingPlan preview, Receipt receipt) =>
            SameRequest(preview.Request, receipt.request) && preview.PrefabHash == receipt.prefabHash &&
            preview.StateHash == receipt.prefabStateHash && preview.ScriptPath == receipt.scriptPath &&
            preview.ScriptHash == receipt.scriptHash && preview.ScriptMetaHash == receipt.scriptMetaHash &&
            preview.ChangedFields == receipt.changedFields && (preview.ChangedFields > 0) == receipt.changed &&
            SourceHash(preview.Sources) == receipt.sourceSnapshotHash;

        /// <summary>生成预览必须保持正文摘要、输出路径、来源全集及原 Prefab，不能借新预览覆盖漂移。</summary>
        private static bool SameGenerationPreview(Bindings.GenerationPlan preview, Receipt receipt) =>
            SameRequest(preview.Request, receipt.request) && preview.PrefabHash == receipt.prefabHash &&
            preview.StateHash == receipt.prefabStateHash && preview.ScriptPath == receipt.scriptPath &&
            preview.ScriptHash == receipt.scriptHash && preview.ScriptMetaHash == receipt.scriptMetaHash &&
            preview.OutputPath == receipt.outputPath && GenerateActionCommon.ComputeTextHash(preview.Content) == receipt.outputHash &&
            preview.HasChanges == receipt.changed && SourceHash(preview.Sources) == receipt.sourceSnapshotHash;

        /// <summary>对领域保存回执逐项绑定原请求与脚本，不接受别的 Prefab 或变更数量替换本次提交。</summary>
        private static bool MatchesBindingReceipt(Receipt receipt) => receipt.binding != null &&
            SameRequest(receipt.binding.Request, receipt.request) && receipt.binding.ChangedFields == receipt.changedFields &&
            receipt.binding.ScriptPath == receipt.scriptPath && receipt.binding.ScriptHash == receipt.scriptHash &&
            receipt.binding.ScriptMetaHash == receipt.scriptMetaHash && receipt.generation == null &&
            SourceHash(receipt.binding.Sources) == receipt.sourceSnapshotHash &&
            receipt.outputFiles.Single(file => file.path == receipt.request.prefabPath).sha256 == receipt.binding.PrefabHash &&
            receipt.outputFiles.Single(file => file.path == receipt.request.prefabPath + ".meta").sha256 == receipt.binding.PrefabMetaHash;

        /// <summary>生成回执必须指向原工具输出、原脚本和原源码集合，并明确仍需本次编译。</summary>
        private static bool MatchesGenerationReceipt(Receipt receipt) => receipt.generation != null && receipt.binding == null &&
            receipt.generation.OutputPath == receipt.outputPath && receipt.generation.OutputHash == receipt.outputHash &&
            receipt.generation.ScriptPath == receipt.scriptPath && receipt.generation.ScriptHash == receipt.scriptHash &&
            receipt.generation.Changed == receipt.changed && receipt.generation.RequiresCompilation &&
            SourceHash(receipt.generation.Sources) == receipt.sourceSnapshotHash;

        /// <summary>只接受原计划必需文件全集和两个真实产物，不能删去保护条目或用空数组补出成功。</summary>
        internal static bool HasCompleteReceipt(Receipt receipt)
        {
            if (receipt?.request == null || !UIBindingsActionCommon.IsPrefabPath(receipt.request.prefabPath) ||
                !GenerateActionCommon.TryValidateGuid(receipt.prefabGuid, out _) ||
                !UIBindingsActionCommon.IsAssetPath(receipt.scriptPath) || !receipt.scriptPath.EndsWith(".cs", StringComparison.Ordinal) ||
                !IsHash(receipt.prefabHash) || !IsHash(receipt.prefabStateHash) || !IsHash(receipt.scriptHash) || !IsHash(receipt.scriptMetaHash) || !IsHash(receipt.sourceSnapshotHash) ||
                receipt.protectedFiles == null || receipt.protectedFiles.Any(file => !ValidFileState(file)) ||
                receipt.outputFiles == null || receipt.outputFiles.Length != 2 || receipt.outputFiles.Any(file => !ValidFileState(file) || !file.exists)) return false;
            string[] protectedPaths = new[] { receipt.request.prefabPath, receipt.request.prefabPath + ".meta", receipt.scriptPath, receipt.scriptPath + ".meta" };
            string output = receipt.request.prefabPath;
            if (receipt.request.mode == "generate")
            {
                if (!UIBindingsActionCommon.IsAssetPath(receipt.outputPath) || !receipt.outputPath.EndsWith(".cs", StringComparison.Ordinal) ||
                    receipt.outputPath == receipt.scriptPath || Path.GetDirectoryName(receipt.outputPath) != Path.GetDirectoryName(receipt.scriptPath) ||
                    !IsHash(receipt.outputHash) || !IsHash(receipt.sourceSnapshotHash)) return false;
                protectedPaths = protectedPaths.Concat(new[] { receipt.outputPath, receipt.outputPath + ".meta" }).ToArray();
                output = receipt.outputPath;
            }
            else if (receipt.request.mode != "bind" || receipt.changedFields < 0 || receipt.changedFields > receipt.request.bindings.Length ||
                receipt.changed != (receipt.changedFields > 0) || receipt.outputPath != null || receipt.compilationToken != null) return false;
            if (!receipt.protectedFiles.Select(file => file.path).OrderBy(path => path, StringComparer.Ordinal)
                    .SequenceEqual(protectedPaths.OrderBy(path => path, StringComparer.Ordinal)) ||
                !receipt.outputFiles.Select(file => file.path).OrderBy(path => path, StringComparer.Ordinal)
                    .SequenceEqual(new[] { output, output + ".meta" }.OrderBy(path => path, StringComparer.Ordinal))) return false;
            return receipt.protectedFiles.Where(file => protectedPaths.Take(4).Contains(file.path)).All(file => file.exists) &&
                receipt.protectedFiles.Single(file => file.path == receipt.request.prefabPath).sha256 == receipt.prefabHash &&
                receipt.protectedFiles.Single(file => file.path == receipt.scriptPath).sha256 == receipt.scriptHash &&
                receipt.protectedFiles.Single(file => file.path == receipt.scriptPath + ".meta").sha256 == receipt.scriptMetaHash &&
                (receipt.request.mode != "generate" || receipt.outputFiles.Single(file => file.path == output).sha256 == receipt.outputHash);
        }

        /// <summary>存在文件必须携带完整摘要，缺失文件不得带伪造字节摘要。</summary>
        private static bool ValidFileState(FileState file) => file != null && UIBindingsActionCommon.IsAssetPath(file.path) &&
            (file.exists ? IsHash(file.sha256) : file.sha256 == null);

        /// <summary>只接受当前共享 SHA256 工具输出的六十四位小写十六进制摘要。</summary>
        private static bool IsHash(string value) => value != null && Regex.IsMatch(value, @"\A[0-9a-f]{64}\z");

        /// <summary>冻结实际 Prefab、脚本和元数据；生成分支同时保护原输出与原输出元数据。</summary>
        private static FileState[] CaptureInputs(Receipt receipt, CancellationToken cancellationToken) =>
            UIBindingsActionCommon.CaptureFiles(new[] { receipt.request.prefabPath, receipt.request.prefabPath + ".meta", receipt.scriptPath, receipt.scriptPath + ".meta" }
                .Concat(receipt.request.mode == "generate" ? new[] { receipt.outputPath, receipt.outputPath + ".meta" } : Array.Empty<string>()).ToArray(), cancellationToken);

        /// <summary>绑定仅允许 Prefab 正文变化；生成仅允许源码和首次创建元数据，已有元数据始终保持。</summary>
        private static bool ProtectedInputsMatch(Receipt receipt, CancellationToken cancellationToken)
        {
            if (receipt.protectedFiles == null || receipt.request == null ||
                !GenerateActionCommon.TryValidateGuid(receipt.prefabGuid, out _) ||
                AssetDatabase.AssetPathToGUID(receipt.request.prefabPath) != receipt.prefabGuid) return false;
            return UIBindingsActionCommon.MatchesFiles(receipt.protectedFiles.Where(file => file != null &&
                !(receipt.request.mode == "bind" && file.path == receipt.request.prefabPath) &&
                !(receipt.request.mode == "generate" && (file.path == receipt.outputPath || file.path == receipt.outputPath + ".meta" && !file.exists))).ToArray(), cancellationToken);
        }

        /// <summary>编译绑定完整冻结与领域提交材料；仅导入后才取得的新元数据摘要由独立产物门核验。</summary>
        internal static string CompilationHash(Receipt receipt)
        {
            JObject json = JObject.FromObject(receipt);
            json.Remove(nameof(Receipt.outputFiles));
            return GenerateActionCommon.ComputeTextHash(json.ToString(Formatting.None));
        }

        /// <summary>对源码与 asmdef 全集作稳定摘要，领域 API 继续负责枚举、归属及实际字节核验。</summary>
        private static string SourceHash(Bindings.SourceSnapshot[] sources)
        {
            if (sources == null || sources.Any(source => source == null) ||
                sources.Select(source => source.Path).Distinct(StringComparer.Ordinal).Count() != sources.Length) throw new InvalidOperationException();
            return GenerateActionCommon.ComputeTextHash(JsonConvert.SerializeObject(sources.OrderBy(source => source.Path, StringComparer.Ordinal).ToArray()));
        }

        /// <summary>只转换固定字段，不按类型名或方法字符串寻找业务执行入口。</summary>
        private static Bindings.BindingRequest ToDomain(Request request) => new Bindings.BindingRequest
        {
            PrefabPath = request.prefabPath, ViewObjectId = request.viewObjectId,
            Bindings = request.bindings.Select(binding => new Bindings.Binding
            {
                FieldName = binding.fieldName, TargetObjectId = binding.targetObjectId, TypeName = binding.typeName,
            }).ToArray(),
        };

        /// <summary>请求使用独立数组，调用方改动原请求不会改变计划。</summary>
        private static Request CopyRequest(Request request) => new Request
        {
            mode = request.mode, prefabPath = request.prefabPath, viewObjectId = request.viewObjectId,
            bindings = request.bindings.Select(binding => new Binding
            { fieldName = binding.fieldName, targetObjectId = binding.targetObjectId, typeName = binding.typeName }).ToArray(),
        };

        /// <summary>保持数组顺序和每项精确值，不能按相同层级名称或可赋值父类型放宽请求。</summary>
        private static bool SameRequest(Bindings.BindingRequest domain, Request request) =>
            domain != null && JsonConvert.SerializeObject(domain) == JsonConvert.SerializeObject(ToDomain(request));

        /// <summary>阻断计划不保存可执行状态或回执，也不修改目标文件。</summary>
        private static Task<AgentActionHandlerPlan> PlanResult(string status, string message) =>
            Task.FromResult(new AgentActionHandlerPlan { Status = status, Summary = message });

        /// <summary>统一返回可跨重载只读核验的回执；任何阶段都不从回执恢复执行。</summary>
        private static Task<AgentActionResult> Result(Receipt receipt, string status, string message) =>
            Task.FromResult(CreateResult(receipt, status, message));

        /// <summary>领域参数和摘要是有限公开材料；异常原文、源码正文与未保存业务值不进入返回。</summary>
        private static AgentActionResult CreateResult(Receipt receipt, string status, string message)
        {
            AgentActionResult result = AgentActionResult.Create(Id, status, message);
            result.ReceiptJson = receipt == null ? null : JsonConvert.SerializeObject(receipt);
            result.DataJson = result.ReceiptJson;
            if (receipt?.outputFiles != null) result.Artifacts.AddRange(receipt.outputFiles.Where(file => file.exists).Select(file => file.path));
            return result;
        }
    }
}
