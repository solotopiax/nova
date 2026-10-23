/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * filename:  SettingsPatchBuildDefinesAction.cs
 * descrip:   精确 NamedBuildTarget 最终宏集合的单次受控保存
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>只维护项目显式 Scripting Define Symbols；不注入 asmdef versionDefines 或依赖宏。</summary>
    [AgentAction("nova.project.settings.patch-build-defines", "修改平台脚本宏", "settings", AgentActionOperationType.Ensure,
        Description = "按明确 NamedBuildTarget 设置完整最终脚本宏集合，支持空集合，并在本次编译重载后只读验证。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead | AgentActionEffect.UnityWrite,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.EnsureState, RequiresConfirmation = true, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "player-settings", "build-defines", "ProjectSettings" })]
    internal sealed class SettingsPatchBuildDefinesAction : AgentActionHandler<SettingsPatchBuildDefinesAction.Request>
    {
        private const string c_ActionId = "nova.project.settings.patch-build-defines";
        private const string c_SettingsPath = "ProjectSettings/ProjectSettings.asset";

        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string namedBuildTarget;
            [AgentActionRequired] public string[] symbols;
        }

        [Serializable]
        internal sealed class FileState
        {
            public string path;
            public string sha256;
        }

        [Serializable]
        internal sealed class Receipt
        {
            public string namedBuildTarget;
            public string activeBuildTarget;
            public string[] before;
            public string[] symbols;
            public string[] added;
            public string[] removed;
            public string plannedFileSha256;
            public string observedFileSha256;
            public string beforeStateSha256;
            public string expectedStateSha256;
            public string protectedStateSha256;
            public FileState[] protectedFiles;
            public bool changed;
            public bool executionStarted;
            public string compilationToken;
        }

        private sealed class State
        {
            internal Receipt Receipt;
            internal int Consumed;
        }

        /// <summary>目标只接受现有公共工具五个 NamedBuildTarget；数组空为明确清空，元素为唯一合法宏名。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (request == null || !TryTarget(request.namedBuildTarget, out _)) error = "namedBuildTarget 只允许 Standalone、iOS、Android、WindowsStoreApps 或 WebGL。";
            else if (!ValidateSymbols(request.symbols)) error = "symbols 必须是最多 512 个唯一 C# 宏标识的数组；明确清空请传 []。";
            return error == null;
        }

        /// <summary>读取真实 PlayerSettings 和完整宏集合，不 Apply 或保存；冻结唯一文件与其他设置保护摘要。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            try
            {
                if (!TryValidateRequest(request, out string error)) return PlanResult("blocked", error);
                TryTarget(request.namedBuildTarget, out NamedBuildTarget target);
                RequireTarget(target, EditorUserBuildSettings.activeBuildTarget.ToString());
                if (GeneratedExportCompilationState.IsPending()) return PlanResult("blocked", "已有导出编译尚未完成，请先完成其验收。");
                PlayerSettings settings = ResolveCleanSettings();
                string hash = FileHash(c_SettingsPath, context.CancellationToken);
                string[] before = EditorUtil.ScriptingDefineSymbols.GetScriptingDefineSymbols(target);
                string[] final = request.symbols.ToArray();
                string snapshot = Snapshot(settings);
                string protectedHash = ProtectedHash(snapshot, target.TargetName);
                string expected = ExpectedHash(snapshot, target.TargetName, final);
                FileState[] files = CaptureProtectedFiles(context.CancellationToken);
                if (Snapshot(settings) != snapshot || FileHash(c_SettingsPath, context.CancellationToken) != hash ||
                    !VerifyProtectedFiles(files, context.CancellationToken)) return PlanResult("blocked", "只读预演期间 PlayerSettings 或 ProjectSettings 已漂移。");
                var receipt = new Receipt
                {
                    namedBuildTarget = request.namedBuildTarget, activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    before = before, symbols = final, added = final.Except(before, StringComparer.Ordinal).ToArray(),
                    removed = before.Except(final, StringComparer.Ordinal).ToArray(), plannedFileSha256 = hash,
                    beforeStateSha256 = GenerateActionCommon.ComputeTextHash(snapshot), expectedStateSha256 = expected,
                    protectedStateSha256 = protectedHash, protectedFiles = files,
                    changed = !before.SequenceEqual(final),
                };
                string json = JsonConvert.SerializeObject(receipt);
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "ready", Summary = receipt.changed ? "将修改 " + request.namedBuildTarget + " 的完整脚本宏集合；新增 " + receipt.added.Length + "，移除 " + receipt.removed.Length + "。" : "完整宏集合已满足，执行将不写入、不触发编译。",
                    DataJson = json, RecoveryPayloadJson = json, State = new State { Receipt = receipt },
                    WriteSet = receipt.changed ? new[] { c_SettingsPath } : Array.Empty<string>(),
                    Evidence = new[] { "Plan 未修改 SerializedObject、保存或请求编译；冻结完整原宏、最终集合、真实 PlayerSettings 和其他 ProjectSettings 文件摘要。" },
                });
            }
            catch (OperationCanceledException) { throw; }
            catch { return PlanResult("blocked", "平台、PlayerSettings 草稿、序列化结构或目标文件不满足精确编辑契约；未写入宏。请关闭 Project Settings 及 PlayerSettings Inspector 后重试。"); }
        }

        /// <summary>单次复核后调用公共 Set 一次；它本身可能持久化并触发编译，不等待额外保存才算写入。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State plan) || Interlocked.Exchange(ref plan.Consumed, 1) != 0) return Result(null, "blocked", "宏计划已失效或已使用，请重新 Plan。");
            Receipt receipt = plan.Receipt;
            try
            {
                TryTarget(receipt.namedBuildTarget, out NamedBuildTarget target);
                RequireTarget(target, receipt.activeBuildTarget);
                PlayerSettings settings = ResolveCleanSettings();
                if (GeneratedExportCompilationState.IsPending() || FileHash(c_SettingsPath, context.CancellationToken) != receipt.plannedFileSha256 ||
                    GenerateActionCommon.ComputeTextHash(Snapshot(settings)) != receipt.beforeStateSha256 ||
                    !EditorUtil.ScriptingDefineSymbols.GetScriptingDefineSymbols(target).SequenceEqual(receipt.before) ||
                    !VerifyProtectedFiles(receipt.protectedFiles, context.CancellationToken))
                    return Result(receipt, "blocked", "完整原宏、PlayerSettings、其他文件或平台已漂移，未开始写入。");
                context.CancellationToken.ThrowIfCancellationRequested();
                receipt.executionStarted = true;
                if (!receipt.changed)
                {
                    receipt.observedFileSha256 = receipt.plannedFileSha256;
                    return Result(receipt, "partial", "宏集合已满足且未保存；请只读 Verify 完成验收。");
                }
                EditorUtil.ScriptingDefineSymbols.SetScriptingDefineSymbols(target, receipt.symbols);
                EditorUtility.SetDirty(settings);
                SaveNativeSettings(settings, receipt.expectedStateSha256);
                receipt.observedFileSha256 = FileHash(c_SettingsPath, context.CancellationToken);
                if (receipt.observedFileSha256 == receipt.plannedFileSha256 ||
                    GenerateActionCommon.ComputeTextHash(Snapshot(settings)) != receipt.expectedStateSha256 ||
                    !VerifyProtectedFiles(receipt.protectedFiles, context.CancellationToken))
                    return Result(receipt, "partial", "宏写入已开始，但原生设置保存或完整状态核验失败；禁止重放 Execute。");
                // 原生文件保存不保证清除内存脏标志；仅在文件真实变化且全部预期状态核过后清除。
                EditorUtility.ClearDirty(settings);
                if (!Matches(settings, target, receipt, context.CancellationToken)) return Result(receipt, "partial", "宏写入已开始，但精确保存或范围外设置核验失败；禁止重放 Execute。");
                receipt.compilationToken = Guid.NewGuid().ToString("N");
                string payload = JsonConvert.SerializeObject(receipt);
                GeneratedExportCompilationState.Schedule(receipt.compilationToken, GenerateActionCommon.ComputeTextHash(payload));
                return Result(receipt, "partial", "目标完整宏集合已写入；等待本次 Unity 编译和脚本重载后 Verify。");
            }
            catch (OperationCanceledException) when (!receipt.executionStarted) { throw; }
            catch { return Result(receipt, receipt.executionStarted ? "partial" : "blocked", receipt.executionStarted
                ? "宏修改已开始但未完成验收；不会重放，请使用 Verify。" : "提交前复核未通过，未写入宏。"); }
        }

        /// <summary>重载后只读核对最终宏、完整设置、精确文件与本次编译；已满足无写分支按静态/产物验收。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try
            {
                Receipt receipt = JsonConvert.DeserializeObject<Receipt>(receiptJson);
                if (receipt == null || !receipt.executionStarted || !TryTarget(receipt.namedBuildTarget, out NamedBuildTarget target) ||
                    !ValidateSymbols(receipt.symbols) || string.IsNullOrEmpty(receipt.observedFileSha256))
                    return Result(null, "partial", "Receipt 没有完整执行证据，不会重新修改宏。");
                RequireTarget(target, receipt.activeBuildTarget);
                if (receipt.changed && !GeneratedExportCompilationState.TryVerify(receipt.compilationToken,
                    GenerateActionCommon.ComputeTextHash(JsonConvert.SerializeObject(receipt)), true, out string error)) return Result(receipt, "partial", error);
                // Unity 重载可能仅重新标记原生对象；Verify 以完整保存快照判断，既不保存也不清除草稿。
                PlayerSettings settings = ResolveCleanSettings(true);
                if (FileHash(c_SettingsPath, context.CancellationToken) != receipt.observedFileSha256 ||
                    !Matches(settings, target, receipt, context.CancellationToken)) return Result(receipt, "partial", "最终宏、完整 PlayerSettings 或文件与 Receipt 不一致。");
                AgentActionResult result = CreateResult(receipt, "success", "目标完整宏集合和 ProjectSettings 已只读核对" + (receipt.changed ? "，本次编译重载通过。" : "；原配置已满足，未写入。"));
                result.EvidenceKinds = AgentActionEvidence.Static | AgentActionEvidence.Artifact | (receipt.changed ? AgentActionEvidence.Compile : AgentActionEvidence.None);
                result.Evidence.Add("Verify 不 Set、不保存、不编译或重放；不证明 Player、运行分支、asmdef versionDefines 或包依赖可用。");
                return Task.FromResult(result);
            }
            catch (OperationCanceledException) { throw; }
            catch { return Result(null, "partial", "宏 Receipt 或只读设置核验未通过；不会重放修改。"); }
        }

        /// <summary>五个准确用户名称映射为公共工具的 NamedBuildTarget，大小写、别名和 None 均不接受。</summary>
        internal static bool TryTarget(string name, out NamedBuildTarget target)
        {
            target = NamedBuildTarget.Unknown;
            switch (name)
            {
                case "Standalone": target = NamedBuildTarget.Standalone; return true;
                case "iOS": target = NamedBuildTarget.iOS; return true;
                case "Android": target = NamedBuildTarget.Android; return true;
                case "WindowsStoreApps": target = NamedBuildTarget.WindowsStoreApps; return true;
                case "WebGL": target = NamedBuildTarget.WebGL; return true;
                default: return false;
            }
        }

        /// <summary>宏数组保留明确顺序并拒绝空项、重复、分号、空白、控制字符和注入文本；空数组合法。</summary>
        internal static bool ValidateSymbols(string[] symbols) => symbols != null && symbols.Length <= 512 &&
            symbols.All(symbol => symbol != null && symbol != "true" && symbol != "false" && symbol.Length <= 128 &&
                Regex.IsMatch(symbol, @"\A[A-Za-z_][A-Za-z0-9_]*\z")) &&
            symbols.Distinct(StringComparer.Ordinal).Count() == symbols.Length;

        /// <summary>只编辑当前传统目标组；活动 Build Profile 会被 Unity Set 同步修改，故必须先拒绝。</summary>
        private static void RequireTarget(NamedBuildTarget target, string active)
        {
            if (UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile() != null ||
                EditorUserBuildSettings.activeBuildTarget.ToString() != active ||
                NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)).TargetName != target.TargetName)
                throw new InvalidOperationException();
        }

        /// <summary>定位唯一原生设置并拒绝编辑窗口；仅 Verify 可用完整预期快照核对重载产生的脏标志。</summary>
        private static PlayerSettings ResolveCleanSettings(bool verifyExpectedState = false)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorUtility.scriptCompilationFailed) throw new InvalidOperationException();
            PlayerSettings[] settings = Resources.FindObjectsOfTypeAll<PlayerSettings>();
            if (settings.Length != 1 || !verifyExpectedState && EditorUtility.IsDirty(settings[0])) throw new InvalidOperationException();
            if (!EditorUtility.IsPersistent(settings[0]) ||
                settings[0] != AssetDatabase.LoadAssetAtPath<PlayerSettings>(c_SettingsPath)) throw new InvalidOperationException();
            if (Resources.FindObjectsOfTypeAll<EditorWindow>().Any(window => window.GetType().FullName == "UnityEditor.SettingsWindow" ||
                window.GetType().FullName == "UnityEditor.ProjectSettingsWindow" || window.hasUnsavedChanges)) throw new InvalidOperationException();
            if (Resources.FindObjectsOfTypeAll<UnityEditor.Editor>().Any(editor => editor != null && editor.targets.Any(value => value is PlayerSettings)))
                throw new InvalidOperationException();
            if (!GenerateActionCommon.TryResolveProjectPath(c_SettingsPath, "PlayerSettings", out string path, out _) || !File.Exists(path) ||
                (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0) throw new InvalidOperationException();
            return settings[0];
        }

        /// <summary>从实际 Unity Editor 序列化提取完整状态；只在内存处理，不能回显其他项目设置。</summary>
        private static string Snapshot(PlayerSettings settings) => Canonical(JObject.Parse(EditorJsonUtility.ToJson(settings)));

        /// <summary>保存唯一原生设置的独立副本，并从真实文件独立重读完整状态；不反序列化覆盖全局对象。</summary>
        private static void SaveNativeSettings(PlayerSettings settings, string expectedStateSha256)
        {
            // Unity 6000.4.2f1 本机验收中原生保存跳过持久 PlayerSettings；非持久副本实际写入。
            // Instantiate 是 Unity Build Profile 自身复制全局设置的方式，避免 JSON 覆写原生对象。
            PlayerSettings copy = UnityEngine.Object.Instantiate(settings);
            UnityEngine.Object[] loaded = null;
            try
            {
                copy.name = settings.name;
                copy.hideFlags = settings.hideFlags;
                if (copy == settings || EditorUtility.IsPersistent(copy) ||
                    GenerateActionCommon.ComputeTextHash(Snapshot(copy)) != expectedStateSha256)
                    throw new InvalidOperationException("原生设置副本与冻结状态不一致。");
                string path = Path.GetFullPath(c_SettingsPath);
                UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { copy }, path, true);
                loaded = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(path);
                if (loaded == null || loaded.Length != 1 || !(loaded[0] is PlayerSettings persisted) ||
                    persisted == settings || persisted == copy || EditorUtility.IsPersistent(persisted) ||
                    GenerateActionCommon.ComputeTextHash(Snapshot(persisted)) != expectedStateSha256 ||
                    GenerateActionCommon.ComputeTextHash(Snapshot(settings)) != expectedStateSha256)
                    throw new InvalidOperationException("原生设置文件独立重读或全局状态不一致。");
            }
            finally
            {
                try
                {
                    if (loaded != null) foreach (UnityEngine.Object value in loaded)
                        if (value != null && value != settings && value != copy && !EditorUtility.IsPersistent(value)) UnityEngine.Object.DestroyImmediate(value);
                }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
            }
        }

        /// <summary>只剔除明确目标组的宏字段生成范围外摘要，其余全部 PlayerSettings 保留。</summary>
        internal static string ProtectedHash(string snapshot, string targetName)
        {
            JObject root = JObject.Parse(snapshot);
            JObject defines = DefineMap(root);
            defines.Remove(targetName);
            return GenerateActionCommon.ComputeTextHash(Canonical(root));
        }

        /// <summary>在独立 JSON 内存树替换唯一目标宏字段；不 Apply、不写 Unity YAML 或配置文件。</summary>
        internal static string ExpectedHash(string snapshot, string targetName, string[] symbols)
        {
            JObject root = JObject.Parse(snapshot);
            JObject defines = DefineMap(root);
            defines[targetName] = string.Join(";", symbols);
            return GenerateActionCommon.ComputeTextHash(Canonical(root));
        }

        /// <summary>必须存在真实 scriptingDefineSymbols 字典；未知 Unity 序列化结构明确阻断。</summary>
        private static JObject DefineMap(JObject root)
        {
            JObject body = root["PlayerSettings"] as JObject ?? root;
            if (!(body["scriptingDefineSymbols"] is JObject defines) || defines.Properties().Any(property => property.Value.Type != JTokenType.String))
                throw new InvalidOperationException();
            return defines;
        }

        /// <summary>规范对象属性顺序，保留真实数组顺序；避免 Unity 字典重排影响语义摘要。</summary>
        private static string Canonical(JToken value) => Sort(value).ToString(Formatting.None);

        /// <summary>复制 JSON 字段树并按属性名排序，不改写 Unity 对象或配置来源。</summary>
        private static JToken Sort(JToken value)
        {
            if (value is JObject obj) return new JObject(obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => new JProperty(property.Name, Sort(property.Value))));
            if (value is JArray array) return new JArray(array.Select(Sort));
            return value.DeepClone();
        }

        /// <summary>实际设置必须匹配最终完整集合、目标预期状态和全部范围外设置，不接受仅内存宏成功。</summary>
        private static bool Matches(PlayerSettings settings, NamedBuildTarget target, Receipt receipt, CancellationToken token)
        {
            string snapshot = Snapshot(settings);
            return EditorUtil.ScriptingDefineSymbols.GetScriptingDefineSymbols(target).SequenceEqual(receipt.symbols) &&
                GenerateActionCommon.ComputeTextHash(snapshot) == receipt.expectedStateSha256 &&
                ExpectedHash(snapshot, target.TargetName, receipt.symbols) == receipt.expectedStateSha256 &&
                ProtectedHash(snapshot, target.TargetName) == receipt.protectedStateSha256 && VerifyProtectedFiles(receipt.protectedFiles, token);
        }

        /// <summary>冻结其他第一层 Unity ProjectSettings 文件及成员集合，不保存无关配置或凭据原文。</summary>
        private static FileState[] CaptureProtectedFiles(CancellationToken token) => Directory.GetFiles("ProjectSettings", "*", SearchOption.TopDirectoryOnly)
            .Select(path => path.Replace('\\', '/')).Where(path => path != c_SettingsPath).OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new FileState { path = path, sha256 = FileHash(path, token) }).ToArray();

        /// <summary>其他第一层 ProjectSettings 新增、删除或改写均使旧计划/收据失效；不包含 Action 自身恢复存储目录。</summary>
        private static bool VerifyProtectedFiles(FileState[] expected, CancellationToken token)
        {
            if (expected == null) return false;
            FileState[] actual = CaptureProtectedFiles(token);
            return actual.Length == expected.Length && actual.Zip(expected, (first, second) => first.path == second.path && first.sha256 == second.sha256).All(match => match);
        }

        /// <summary>项目路径与重解析点先校验，再计算完整文件 SHA；不读取配置正文到结果。</summary>
        private static string FileHash(string path, CancellationToken token)
        {
            if (!GenerateActionCommon.TryResolveProjectPath(path, "ProjectSettings", out string absolute, out _)) throw new InvalidOperationException();
            return GenerateActionCommon.ComputeFileHash(absolute, token);
        }

        /// <summary>计划失败只返回固定原因，不泄露其他 PlayerSettings 配置。</summary>
        private static Task<AgentActionHandlerPlan> PlanResult(string status, string message) => Task.FromResult(new AgentActionHandlerPlan { Status = status, Summary = message });

        /// <summary>将脱敏收据结果包装为已完成任务，不触发写入或编译。</summary>
        private static Task<AgentActionResult> Result(Receipt receipt, string status, string message) => Task.FromResult(CreateResult(receipt, status, message));

        /// <summary>只返回明确宏差异、完整摘要与精确目标文件，不返回其他设置正文。</summary>
        private static AgentActionResult CreateResult(Receipt receipt, string status, string message)
        {
            AgentActionResult result = AgentActionResult.Create(c_ActionId, status, message);
            if (receipt != null)
            {
                result.DataJson = result.ReceiptJson = JsonConvert.SerializeObject(receipt);
                if (!string.IsNullOrEmpty(receipt.observedFileSha256)) result.Artifacts.Add(c_SettingsPath);
            }
            return result;
        }
    }
}
