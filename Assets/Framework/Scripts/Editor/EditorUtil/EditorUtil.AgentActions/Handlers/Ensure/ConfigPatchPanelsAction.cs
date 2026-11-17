/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigPatchPanelsAction.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   Config 多面板字段与维度的受控批量保存
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using Editing = NovaFramework.Editor.EditorUtil.Config.DesignTimeEditing;
using Panels = NovaFramework.Editor.EditorUtil.Config.PanelEditing;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction("nova.project.config.patch-panels", "编辑 Config 面板", "config", AgentActionOperationType.Ensure,
        Description = "按真实面板字段与明确坐标批量编辑配置和维度，或为未配置的 Privacy 逻辑组首次生成本地 AES；只保存 Master，不导出 Runtime、不构建。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead |
            AgentActionEffect.UnityWrite | AgentActionEffect.Credential,
        RequiredEvidence = AgentActionEvidence.Static, Idempotency = AgentActionIdempotency.EnsureState,
        RequiresConfirmation = true, RequiresEditMode = true, Locks = new[] { "unity-editor", "configmaster-assets" })]
    internal sealed class ConfigPatchPanelsAction : AgentActionHandler<ConfigPatchPanelsAction.Request>
    {
        /// <summary>只定位一个真实 Master，全部面板、坐标和相对字段来自固定领域请求。</summary>
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string masterGuid;
            public Panels.Change[] changes;
            /// <summary>省略时保持既有字段/维度编辑；首次本地密钥准备使用互斥的 initialize-privacy-aes。</summary>
            public string operation;
            /// <summary>首次准备的唯一 PrivacyConfigs 目标；不得附加 fields、dimensions 或 configType。</summary>
            public Panels.Target privacyTarget;
        }

        /// <summary>只包含资产身份、完整指纹及脱敏物理写集；不能依据 Receipt 重放字段值。</summary>
        [Serializable]
        private sealed class Receipt
        {
            public int schemaVersion = 1;
            public string masterGuid;
            public string assetPath;
            public string plannedAssetSha256;
            public string metaSha256;
            public string beforeStateSha256;
            public string expectedStateSha256;
            public string observedAssetSha256;
            public bool executionStarted;
            public Panels.Difference[] physicalChanges;
            public string operation;
            public Panels.Target privacyTarget;
            public string editorAssemblyFullName;
            public string editorModuleVersionId;
        }

        /// <summary>字段值仅保存在一次性内存执行态，凭据不进入操作记录。</summary>
        private sealed class State
        {
            internal Receipt Receipt;
            internal Panels.Change[] Changes;
            internal int Consumed;
        }

        private const string c_InitializePrivacyAes = "initialize-privacy-aes";

        /// <summary>请求只接受真实 GUID、固定面板/枚举、字段路径和严格 JSON token。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            try
            {
                if (request == null || !GuidValid(request.masterGuid)) throw new InvalidOperationException("需要有效的 ConfigMaster GUID。");
                if (request.operation == c_InitializePrivacyAes)
                {
                    if (request.changes != null) throw new InvalidOperationException("首次准备不能混用 changes。");
                    Panels.ValidatePrivacyInitializationTarget(request.privacyTarget);
                }
                else
                {
                    if (request.operation != null || request.privacyTarget != null)
                        throw new InvalidOperationException("面板操作分支无效。");
                    Panels.Validate(request.changes);
                }
                return true;
            }
            catch { error = "Config 面板请求的 GUID、坐标、字段或 JSON 值无效。"; return false; }
        }

        /// <summary>冻结完整文件与内存状态；领域修改仅作用于 clone，计划只报告真实写集。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryValidateRequest(request, out string error) || !TryLoad(request.masterGuid, true, out ConfigMasterSO master, out string path, out error))
                return Task.FromResult(Blocked(error));
            try
            {
                // partial helper、原面板预演和统一保存都来自同一已加载 Editor 模块，不能只按 Handler 名称冻结。
                string editorAssemblyFullName = typeof(ConfigPatchPanelsAction).Assembly.FullName;
                string editorModuleVersionId = typeof(ConfigPatchPanelsAction).Module.ModuleVersionId.ToString("D");
                string hash = FileHash(path, context.CancellationToken);
                string metaHash = FileHash(path + ".meta", context.CancellationToken);
                // 二次复制请求，调用方后续修改原 DTO 不能改变冻结计划。
                Panels.Target privacyTarget = request.privacyTarget == null ? null :
                    Util.Json.Deserialize<Panels.Target>(Util.Json.Serialize(request.privacyTarget));
                // 初次生成只发生在本次 Plan；后续 Execute 使用这份内存请求，重载后不能随机重算并沿用旧计划。
                Panels.Change[] changes = request.operation == c_InitializePrivacyAes ?
                    Panels.CreatePrivacyInitializationChanges(master, privacyTarget) :
                    Util.Json.Deserialize<Panels.Change[]>(Util.Json.Serialize(request.changes));
                using (Panels.Preview preview = Panels.BuildPreview(master, changes))
                {
                    if (hash != FileHash(path, context.CancellationToken) || metaHash != FileHash(path + ".meta", context.CancellationToken) || Panels.StateHash(master) != preview.BeforeHash ||
                        EditorUtility.IsDirty(master) || Editing.HasUnsavedDraft(master))
                        return Task.FromResult(Blocked("预演时配置或草稿发生变化，请重新 Plan。"));
                    var receipt = new Receipt { masterGuid = request.masterGuid, assetPath = path,
                        plannedAssetSha256 = hash, metaSha256 = metaHash, beforeStateSha256 = preview.BeforeHash, expectedStateSha256 = preview.ExpectedHash,
                        physicalChanges = preview.Differences, operation = request.operation, privacyTarget = privacyTarget,
                        editorAssemblyFullName = editorAssemblyFullName, editorModuleVersionId = editorModuleVersionId };
                    if (!ImplementationIdentityMatches(receipt, false))
                        return Task.FromResult(Blocked("配置预演、首次准备或共享保存的已加载实现身份变化，请重新 Plan。"));
                    return Task.FromResult(new AgentActionHandlerPlan
                    {
                        Status = "ready", Summary = preview.Differences.Length == 0 ? "面板目标已满足，保持文件不变。" :
                            request.operation == c_InitializePrivacyAes ? "将为已展示的未配置 Privacy 逻辑组保存项目本地密钥；之后需另行正式导出运行配置。" :
                            "将保存已展示的配置字段或维度变化；未导出运行配置。",
                        DataJson = Util.Json.Serialize(new { receipt.masterGuid, receipt.assetPath, receipt.beforeStateSha256,
                            receipt.expectedStateSha256, receipt.physicalChanges, receipt.operation, receipt.privacyTarget,
                            receipt.editorAssemblyFullName, receipt.editorModuleVersionId, saveOnly = true }),
                        State = new State { Receipt = receipt, Changes = changes }, RecoveryPayloadJson = Util.Json.Serialize(receipt),
                        WriteSet = preview.Differences.Length == 0 ? Array.Empty<string>() : new[] { path },
                        Evidence = new[] { "完整字段预演在独立工作副本；复用现有维度分裂/合并/广播及统一保存。",
                            "计划列出全部物理序列化差异；请求值、凭据、逐值哈希不写入公开计划或 Receipt。",
                            "首次 Privacy 准备只接受整组空值或公开占位；有效同组凭据保持，未知旧值或混合组阻断。生成值只在一次性内存计划内。",
                            "已冻结实际加载的 Editor 程序集完整身份与模块 MVID，覆盖 Handler、全部 PanelEditing partial、维度预演与共享保存。",
                            "保存成功只证明 Master 状态，不替代 Runtime、生成物、编译、设备或外部系统验收。" },
                    });
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (InvalidOperationException exception) when (exception.Message.StartsWith("panel-stage:", StringComparison.Ordinal))
            { return Task.FromResult(Blocked("配置面板预演失败（" + exception.Message + "）；未修改正式资产。")); }
            catch (InvalidOperationException) when (request.operation == c_InitializePrivacyAes)
            { return Task.FromResult(Blocked("Privacy 首次准备要求完整同组旧值一致，且为空、公开占位或已有效凭据；未知旧值不能覆盖，未修改正式资产。")); }
            catch { return Task.FromResult(Blocked("配置面板预演未通过字段类型、逻辑组冲突、规范结构或请求外写集检查。")); }
        }

        /// <summary>单次消费且提交前复核状态/草稿；保存失败保留真实写入边界，不自动回滚或重放。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen) || frozen.Receipt == null || Interlocked.Exchange(ref frozen.Consumed, 1) != 0)
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "配置面板计划无效或已使用，请重新 Plan。"));
            Receipt receipt = frozen.Receipt;
            bool writeStarted = false;
            try
            {
                if (!TryLoad(receipt.masterGuid, true, out ConfigMasterSO master, out string path, out string error) || path != receipt.assetPath)
                    return Task.FromResult(AgentActionResult.Create(null, "blocked", error ?? "目标 ConfigMaster 路径漂移。"));
                AssertFrozen(master, receipt, context.CancellationToken);
                using (Panels.Preview preview = Panels.BuildPreview(master, frozen.Changes))
                {
                    if (preview.BeforeHash != receipt.beforeStateSha256 || preview.ExpectedHash != receipt.expectedStateSha256 ||
                        Util.Json.Serialize(preview.Differences) != Util.Json.Serialize(receipt.physicalChanges))
                        return Task.FromResult(AgentActionResult.Create(null, "blocked", "配置目标状态或精确写集漂移，请重新 Plan。"));
                    if (preview.Differences.Length > 0)
                    {
                        using (var so = new SerializedObject(preview.WorkingCopy))
                            Editing.SaveWorkingCopy(master, preview.WorkingCopy, so, preview.SaveCoordinate, new Editing.SaveOptions(), clone =>
                            {
                                AssertFrozen(master, receipt, context.CancellationToken);
                                if (Panels.StateHash(clone) != receipt.expectedStateSha256) throw new InvalidOperationException("提交前工作副本漂移。");
                            });
                        writeStarted = true;
                    }
                    receipt.executionStarted = true;
                    receipt.observedAssetSha256 = FileHash(path, CancellationToken.None);
                    bool matches = !EditorUtility.IsDirty(master) && Panels.StateHash(master) == receipt.expectedStateSha256;
                    AgentActionResult result = Result(receipt, matches ? "success" : "partial", matches ?
                        "Config 面板目标已保存并复核；运行配置仍需单独导出。" : "保存后状态尚未通过复核，请使用 Verify。");
                    if (matches && writeStarted) result.Warnings.AddRange(Editing.NotifyMasterSaved(master));
                    return Task.FromResult(result);
                }
            }
            catch (Editing.DesignTimeSaveException exception) { writeStarted |= exception.WriteStarted; return Task.FromResult(Failure(receipt, writeStarted)); }
            catch (OperationCanceledException) when (!writeStarted) { throw; }
            catch { return Task.FromResult(Failure(receipt, writeStarted)); }
            finally
            {
                // CLR 字符串不能保证原地清零；释放本次首次准备计划持有的秘密引用，不把它写入恢复载荷。
                if (receipt.operation == c_InitializePrivacyAes) frozen.Changes = null;
            }
        }

        /// <summary>只读核对完整目标状态与保存文件指纹；不调用补结构 Getter、保存或执行恢复。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try
            {
                Receipt receipt = Util.Json.Deserialize<Receipt>(receiptJson);
                if (receipt == null || receipt.schemaVersion != 1 || !GuidValid(receipt.masterGuid) || !receipt.executionStarted ||
                    !HashValid(receipt.expectedStateSha256) || !HashValid(receipt.observedAssetSha256) || !HashValid(receipt.metaSha256) || receipt.physicalChanges == null)
                    return Task.FromResult(AgentActionResult.Create(null, "blocked", "面板 Receipt 缺有效执行证据，不能重放。"));
                if (receipt.operation == c_InitializePrivacyAes) Panels.ValidatePrivacyInitializationTarget(receipt.privacyTarget);
                else if (receipt.operation != null || receipt.privacyTarget != null)
                    return Task.FromResult(AgentActionResult.Create(null, "blocked", "面板 Receipt 操作分支无效。"));
                if (!ImplementationIdentityMatches(receipt, receipt.operation == null))
                    return Task.FromResult(AgentActionResult.Create(null, "blocked", "面板 Receipt 的已加载实现身份变化或缺失，只能重新核对；不会重放或保存。"));
                if (!TryLoad(receipt.masterGuid, true, out ConfigMasterSO master, out string path, out string error) || path != receipt.assetPath)
                    return Task.FromResult(AgentActionResult.Create(null, "blocked", error ?? "Receipt 目标路径已变化。"));
                bool matches = FileHash(path, context.CancellationToken) == receipt.observedAssetSha256 &&
                    FileHash(path + ".meta", context.CancellationToken) == receipt.metaSha256 && Panels.StateHash(master) == receipt.expectedStateSha256;
                return Task.FromResult(Result(receipt, matches ? "success" : "partial", matches ?
                    "ConfigMaster 文件及全部目标字段状态一致；未推断运行或设备成功。" : "ConfigMaster 与 Receipt 不一致，请检查后重新 Plan。"));
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(AgentActionResult.Create(null, "blocked", "无法只读验证面板 Receipt。")); }
        }

        /// <summary>领域资产必须是 Assets 内无符号链接真实可写的 Master；写入前拒绝脏态及注册窗口草稿。</summary>
        internal static bool TryLoad(string guid, bool clean, out ConfigMasterSO master, out string path, out string error)
        {
            master = null; path = null; error = null;
            try
            {
                if (!GuidValid(guid)) throw new InvalidOperationException();
                path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == null || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !GenerateActionCommon.TryResolveProjectPath(path, "ConfigMaster", out string absolute, out _) ||
                    !File.Exists(absolute) || (File.GetAttributes(absolute) & FileAttributes.ReadOnly) != 0)
                    throw new InvalidOperationException();
                master = AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(path);
                if (master == null || !EditorUtility.IsPersistent(master) || AssetDatabase.AssetPathToGUID(path) != guid)
                    throw new InvalidOperationException();
                if (clean && (EditorUtility.IsDirty(master) || Editing.HasUnsavedDraft(master)))
                { error = "目标 Master 或 ConfigWindow 存在未保存草稿，请先保存或放弃草稿。"; return false; }
                return true;
            }
            catch { error = "需要 Assets 内存在、可写且 GUID 一致的 ConfigMaster。"; return false; }
        }

        /// <summary>每次预演或提交前复核已加载实现身份、文件、完整内存状态及全部已注册草稿，不仅核对请求字段。</summary>
        private static void AssertFrozen(ConfigMasterSO master, Receipt receipt, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!ImplementationIdentityMatches(receipt, false) || EditorUtility.IsDirty(master) || Editing.HasUnsavedDraft(master) || FileHash(receipt.assetPath, token) != receipt.plannedAssetSha256 ||
                FileHash(receipt.assetPath + ".meta", token) != receipt.metaSha256 ||
                Panels.StateHash(master) != receipt.beforeStateSha256) throw new InvalidOperationException("配置、已加载实现或草稿漂移。");
        }

        /// <summary>固定检查实际执行类型的同一已加载模块，完整覆盖 partial helper、原预演及共享保存；不按请求解析任意类型。</summary>
        /// <param name="receipt">本次计划冻结的完整程序集名称与模块 MVID。</param>
        /// <param name="allowLegacy">仅旧普通字段 Receipt 可缺少新增身份字段并继续只读验证；首建及所有 Execute 均不豁免。</param>
        private static bool ImplementationIdentityMatches(Receipt receipt, bool allowLegacy)
        {
            if (receipt.editorAssemblyFullName == null && receipt.editorModuleVersionId == null)
                return allowLegacy && receipt.operation == null;
            Type[] types = { typeof(ConfigPatchPanelsAction), typeof(Panels), typeof(Editing),
                typeof(EditorUtil.Config.DimensionProjector), typeof(SerializedPropertyValues), typeof(ConfigMasterSO) };
            return types.All(type => type.Assembly.FullName == receipt.editorAssemblyFullName &&
                type.Module.ModuleVersionId.ToString("D") == receipt.editorModuleVersionId);
        }

        /// <summary>计算已经通过项目路径门的真实文件指纹。</summary>
        private static string FileHash(string path, CancellationToken token) => GenerateActionCommon.ComputeFileHash(
            Path.Combine(GenerateActionCommon.ProjectRoot, path), token);

        /// <summary>GUID 必须恰好 32 位十六进制。</summary>
        private static bool GuidValid(string value) => value != null && value.Length == 32 && value.All(Uri.IsHexDigit);

        /// <summary>完整 SHA-256 必须恰好 64 位十六进制。</summary>
        private static bool HashValid(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);

        /// <summary>阻塞计划不携带值或异常正文。</summary>
        private static AgentActionHandlerPlan Blocked(string message) => new AgentActionHandlerPlan { Status = "blocked", Summary = message };

        /// <summary>写入开始后的失败保留实际文件摘要，供只读 Verify 判断，不声称原子回滚。</summary>
        private static AgentActionResult Failure(Receipt receipt, bool started)
        {
            if (!started) return AgentActionResult.Create(null, "blocked", "配置提交前复核失败，未开始写入。");
            receipt.executionStarted = true;
            try { receipt.observedAssetSha256 = FileHash(receipt.assetPath, CancellationToken.None); } catch { }
            return Result(receipt, "partial", "配置写入已开始但未完成核验，请使用 Verify；不得重放 Execute。");
        }

        /// <summary>结果只报告完整指纹与物理地址，成功保存不等同 Runtime 导出。</summary>
        private static AgentActionResult Result(Receipt receipt, string status, string message)
        {
            AgentActionResult result = AgentActionResult.Create(null, status, message);
            result.DataJson = Util.Json.Serialize(new { receipt.masterGuid, receipt.assetPath, receipt.expectedStateSha256,
                receipt.observedAssetSha256, receipt.physicalChanges, receipt.operation, receipt.privacyTarget,
                receipt.editorAssemblyFullName, receipt.editorModuleVersionId, saveOnly = true });
            result.ReceiptJson = Util.Json.Serialize(receipt);
            result.EvidenceKinds = AgentActionEvidence.Static;
            result.Evidence.Add("已核对 ConfigMaster 的完整序列化目标状态与文件摘要。");
            return result;
        }
    }
}
