/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IapProductCatalogUploadAction.cs
 * author:    Codex
 * created:   2026/10/22
 * descrip:   冻结既有 IAP 商品目录并至多提交一次的受控 Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>只提交明确冻结的现有目录；HTTP 接受不替代服务端应用确认。</summary>
    [AgentAction("nova.project.iap.upload-product-catalog", "提交 IAP 商品目录", "iap", AgentActionOperationType.Delivery,
        Description = "冻结已导出的完整商品目录、版本与后台目标，提交至多一次并核验脱敏回执。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.UnityRead | AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.ExternalWrite | AgentActionEffect.Credential,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact | AgentActionEvidence.External,
        Idempotency = AgentActionIdempotency.SubmitOnce, RequiresConfirmation = true,
        RequiresStableEditor = true, RequiresEditMode = true,
        ReloadSemantics = AgentActionReloadSemantics.PlanInvalidatedVerifyOnly,
        Locks = new[] { "unity-editor", "config-runtime-target", "iap-product-catalog-upload" })]
    internal sealed class IapProductCatalogUploadAction : AgentActionHandler<IapProductCatalogUploadAction.Request>
    {
        internal const string Id = "nova.project.iap.upload-product-catalog";
        private static readonly string[] s_MetadataFields = { "schemaVersion", "appId", "platform", "channel", "developMode", "version", "uploadUri", "catalogHash", "productCount", "requestSha256", "requestByteCount" };
        private static readonly string[] s_OutcomeFields = { "schemaVersion", "requestSha256", "outcome", "dispatched", "statusCode", "responseByteCount", "responseSha256", "versionConflict", "exceptionKind", "startedAtUtc", "completedAtUtc" };
        private static readonly string[] s_SnapshotFields = { "schemaVersion", "masterGuid", "masterPath", "runtimeGuid", "runtimePath", "activeBuildTarget", "masterMemorySha256", "runtimeMemorySha256", "codeIdentitySha256", "files", "catalog" };
        private readonly IapProductCatalogReceiptStore m_Store;

        /// <summary>供唯一核心 Registry 创建生产 Handler，回执固定在领域目录内。</summary>
        public IapProductCatalogUploadAction() : this(new IapProductCatalogReceiptStore()) { }

        /// <summary>供内部无网络回归使用隔离记录目录，不替换上传桥或接收外部执行代码。</summary>
        internal IapProductCatalogUploadAction(IapProductCatalogReceiptStore store)
        {
            m_Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>只接受运行配置身份与明确环境意图，其他输入由当前工程读取。</summary>
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string runtimeGuid;
            [AgentActionRequired] public string developMode;
        }

        /// <summary>密文请求仅驻内存，和一次性 Core Plan 共同失效，不写回执或日志。</summary>
        private sealed class State
        {
            internal Request Request;
            internal IapProductCatalogAdapter Adapter;
            internal object Prepared;
            internal JObject Snapshot;
            internal string SnapshotHash;
            internal string AttemptId;
            internal int Used;
        }

        /// <summary>拒绝不规范 GUID、数字枚举和缺省环境，不替用户选择正式后台。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (request == null || !Guid.TryParseExact(request.runtimeGuid, "N", out _) ||
                request.developMode != "Debug" && request.developMode != "Release")
                error = "必须提供现有 runtimeGuid 和精确的 developMode（Debug 或 Release）。";
            return error == null;
        }

        /// <summary>只读捕获已保存配置、全目录与代码身份，生成可恢复但不可重发的领域标识。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryCapture(request, context.CancellationToken, out State state, out bool missing, out string error))
                return Task.FromResult(new AgentActionHandlerPlan { Status = missing ? "not_applicable" : "blocked", Summary = error });
            state.AttemptId = Guid.NewGuid().ToString("N");
            string receipt = Receipt(state.AttemptId, state.Snapshot, state.SnapshotHash, string.Empty);
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready", Summary = "将向冻结环境提交完整 IAP 商品目录至多一次；当前无后台应用确认契约，执行后最高为 partial。",
                State = state, DataJson = state.Snapshot.ToString(Formatting.None), RecoveryPayloadJson = receipt,
                WriteSet = new[] { m_Store.RelativePath(state.AttemptId), "HTTP POST " + (string)state.Snapshot["catalog"]["uploadUri"] },
                Evidence = new[] { "读取已保存 ExportTarget；不导出、不修复 Globals、不发送 HTTP。", "冻结完整规范目录与最终请求摘要；密钥、Body 和原始响应不进入公开回执。", "2xx 只证明 HTTP 接受；精确版本冲突表示本目录未应用；断线或失败不自动重发。" },
            });
        }

        /// <summary>重新核对输入后先保存意图，再仅发送原 Prepared；任何返回路径都不重试。</summary>
        public override async Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen) || Interlocked.Exchange(ref frozen.Used, 1) != 0)
                return AgentActionResult.Create(Id, "blocked", "IAP 冻结状态无效或已经消费。");
            string recordHash = string.Empty;
            bool intentSaved = false;
            try
            {
                if (context.CancellationToken.IsCancellationRequested ||
                    !TryCapture(frozen.Request, context.CancellationToken, out State current, out _, out _) ||
                    frozen.SnapshotHash != current.SnapshotHash)
                    return AgentActionResult.Create(Id, "blocked", "配置、目录、版本、环境或代码身份已变化；没有发送请求。");
                try
                {
                    recordHash = m_Store.CreateIntent(frozen.AttemptId, frozen.SnapshotHash);
                    intentSaved = true;
                }
                catch (Exception)
                {
                    return AgentActionResult.Create(Id, "blocked", "发送前意图保存失败；没有发送请求，原记录保留。");
                }

                JObject outcome = ParseOutcome(await frozen.Adapter.SubmitAsync(frozen.Prepared, context.CancellationToken),
                    (string)frozen.Snapshot["catalog"]["requestSha256"]);
                recordHash = m_Store.Complete(frozen.AttemptId, frozen.SnapshotHash, recordHash, outcome);
                return BuildResult(frozen.AttemptId, frozen.Snapshot, frozen.SnapshotHash, recordHash, outcome, false);
            }
            catch (Exception)
            {
                AgentActionResult result = AgentActionResult.Create(Id, intentSaved ? "partial" : "blocked",
                    intentSaved ? "提交意图已落盘，但完整结果未保存；远端效果未知，禁止自动重发。" : "IAP 输入核对未完成；没有发送请求。");
                result.ReceiptJson = Receipt(frozen.AttemptId, frozen.Snapshot, frozen.SnapshotHash, recordHash);
                result.DataJson = new JObject { ["attemptId"] = frozen.AttemptId, ["remoteApplication"] = "unknown" }.ToString(Formatting.None);
                return result;
            }
            finally
            {
                frozen.Prepared = null;
            }
        }

        /// <summary>只读原 attempt 记录，不调用桥或上传接口；保留断线时已经发生写入的可能性。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                JObject receipt = ParseReceipt(receiptJson);
                string attempt = (string)receipt["attemptId"], hash = (string)receipt["snapshotSha256"];
                JObject record = m_Store.Read(attempt, hash, out string recordHash);
                string expectedRecordHash = (string)receipt["recordSha256"];
                if (expectedRecordHash.Length != 0 && expectedRecordHash != recordHash)
                    throw new InvalidOperationException("原回执文件已变化。");
                JObject snapshot = (JObject)receipt["snapshot"];
                if ((string)record["phase"] == "dispatching")
                {
                    AgentActionResult pending = AgentActionResult.Create(Id, "partial", "仅有发送意图，实际响应未知；不会恢复或重发上传。");
                    pending.ReceiptJson = receiptJson;
                    pending.DataJson = new JObject { ["attemptId"] = attempt, ["remoteApplication"] = "unknown" }.ToString(Formatting.None);
                    return Task.FromResult(pending);
                }
                JObject outcome = ParseOutcome(record["outcome"].ToString(Formatting.None), (string)snapshot["catalog"]["requestSha256"]);
                return Task.FromResult(BuildResult(attempt, snapshot, hash, recordHash, outcome, SourceDrifted(snapshot, context.CancellationToken)));
            }
            catch (Exception)
            {
                return Task.FromResult(AgentActionResult.Create(Id, "partial", "原 IAP 回执缺失、不完整或身份变化；远端状态未知，不重发。"));
            }
        }

        /// <summary>只读定位唯一激活 Master 与其显式 ExportTarget，拒绝草稿和任何路径修复。</summary>
        private static bool TryCapture(Request request, CancellationToken token, out State state, out bool missing, out string error)
        {
            state = null; missing = false; error = null;
            try
            {
                token.ThrowIfCancellationRequested();
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("需要稳定 Edit Mode。");
                if (!IapProductCatalogAdapter.TryCreate(out IapProductCatalogAdapter adapter, out missing, out error)) return false;
                if (!EditorUtil.Config.WorkspaceActive.TryGetPersistedConfigMaster(out ConfigMasterSO master, out string masterGuid, out string masterPath, out _))
                    throw new InvalidOperationException("持久化激活 Master 不完整；请先通过配置流程修复。");
                ConfigRuntimeSO runtime = master.ExportTarget;
                string runtimePath = runtime == null ? null : AssetDatabase.GetAssetPath(runtime);
                if (runtime == null || EditorUtility.IsDirty(master) || EditorUtility.IsDirty(runtime) ||
                    AssetDatabase.AssetPathToGUID(runtimePath) != request.runtimeGuid ||
                    runtime.DevelopMode.ToString() != request.developMode || master.CurrentDevelopMode.ToString() != request.developMode ||
                    master.CurrentPlatform != runtime.Platform || master.CurrentChannel != runtime.Channel)
                    throw new InvalidOperationException("只接纳当前坐标下已保存的显式 ExportTarget；请先完成正式配置导出。");
                if (!GenerateActionCommon.TryResolveAssetSavePath(masterPath, out _, out _, out _) ||
                    !GenerateActionCommon.TryResolveAssetSavePath(runtimePath, out _, out _, out _))
                    throw new InvalidOperationException("Master 与 Runtime 必须为项目自有规范资产。");
                var files = new JObject();
                foreach (string path in new[] { masterPath, masterPath + ".meta", runtimePath, runtimePath + ".meta", "ProjectSettings/Nova/Globals.json", "ProjectSettings/ProjectSettings.asset" }.Distinct().OrderBy(p => p, StringComparer.Ordinal))
                {
                    GenerateActionCommon.Artifact artifact = GenerateActionCommon.CaptureFile(path, token);
                    files[path] = artifact.sha256;
                }
                object prepared = adapter.Prepare(runtime);
                JObject catalog = ParseMetadata(adapter.Describe(prepared));
                if ((string)catalog["developMode"] != request.developMode || (string)catalog["version"] != PlayerSettings.bundleVersion ||
                    (string)catalog["platform"] != runtime.Platform.ToString() || (string)catalog["channel"] != runtime.Channel.ToString())
                    throw new InvalidOperationException("IAP 桥的环境或版本与当前配置不一致。");
                var snapshot = new JObject
                {
                    ["schemaVersion"] = 1, ["masterGuid"] = masterGuid, ["masterPath"] = masterPath,
                    ["runtimeGuid"] = request.runtimeGuid, ["runtimePath"] = runtimePath,
                    ["activeBuildTarget"] = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    ["masterMemorySha256"] = GenerateActionCommon.ComputeTextHash(EditorJsonUtility.ToJson(master)),
                    ["runtimeMemorySha256"] = GenerateActionCommon.ComputeTextHash(EditorJsonUtility.ToJson(runtime)),
                    ["codeIdentitySha256"] = GenerateActionCommon.ComputeTextHash(adapter.IdentityJson), ["files"] = files, ["catalog"] = catalog,
                };
                state = new State { Request = new Request { runtimeGuid = request.runtimeGuid, developMode = request.developMode }, Adapter = adapter,
                    Prepared = prepared, Snapshot = snapshot, SnapshotHash = GenerateActionCommon.ComputeTextHash(snapshot.ToString(Formatting.None)) };
                return true;
            }
            catch (Exception) { error = "IAP 目录冻结失败；未发送请求，未修改配置。"; return false; }
        }

        /// <summary>校验桥提供的脱敏目录身份，不接纳任意目标、密钥或目录正文。</summary>
        internal static JObject ParseMetadata(string json)
        {
            JObject value = IapProductCatalogReceiptStore.Parse(json, s_MetadataFields);
            RequireVersion(value);
            foreach (string name in new[] { "platform", "channel", "developMode", "version", "uploadUri", "catalogHash", "requestSha256" }) RequireString(value, name, 2048);
            foreach (string name in new[] { "appId", "productCount", "requestByteCount" })
                if (value[name].Type != JTokenType.Integer || (long)value[name] <= 0 || (long)value[name] > int.MaxValue) throw new InvalidOperationException("IAP 正整数元数据无效。");
            string mode = (string)value["developMode"];
            if (!new[] { "Android", "iOS", "WebGL" }.Contains((string)value["platform"]) ||
                !new[] { "Official", "Google", "Apple", "WeChat", "TikTok", "Alipay" }.Contains((string)value["channel"]))
                throw new InvalidOperationException("IAP 平台或渠道不属于已核协议范围。");
            string uri = mode == "Debug" ? "https://t-tools-admin.ahameet.com/api/admin/v1/iap/product-catalogs/upload" :
                mode == "Release" ? "https://tools-admin.ahameet.com/api/admin/v1/iap/product-catalogs/upload" : null;
            if (uri == null || (string)value["uploadUri"] != uri || !IapProductCatalogReceiptStore.IsHash((string)value["catalogHash"]) ||
                !IapProductCatalogReceiptStore.IsHash((string)value["requestSha256"]) || string.IsNullOrWhiteSpace((string)value["version"]))
                throw new InvalidOperationException("IAP 目标、目录或请求摘要无效。");
            return value;
        }

        /// <summary>核对完整固定响应事实；任何未知字段或状态组合保留为结果未知。</summary>
        internal static JObject ParseOutcome(string json, string requestHash)
        {
            JObject value = IapProductCatalogReceiptStore.Parse(json, s_OutcomeFields);
            RequireVersion(value);
            foreach (string name in new[] { "requestSha256", "outcome", "responseSha256", "exceptionKind", "startedAtUtc", "completedAtUtc" }) RequireString(value, name, 128);
            if ((string)value["requestSha256"] != requestHash || !IapProductCatalogReceiptStore.IsHash(requestHash) ||
                value["dispatched"].Type != JTokenType.Boolean || value["versionConflict"].Type != JTokenType.Boolean ||
                value["statusCode"].Type != JTokenType.Integer || value["responseByteCount"].Type != JTokenType.Integer ||
                (long)value["responseByteCount"] < 0 || (long)value["responseByteCount"] > int.MaxValue)
                throw new InvalidOperationException("IAP 结果身份或字段类型无效。");
            string outcome = (string)value["outcome"], kind = (string)value["exceptionKind"], responseHash = (string)value["responseSha256"];
            long status = (long)value["statusCode"];
            bool dispatched = (bool)value["dispatched"], conflict = (bool)value["versionConflict"];
            bool http = outcome == "http-accepted" || outcome == "version-conflict" || outcome == "http-error";
            if (http)
            {
                if (!dispatched || status < 100 || status > 599 || !IapProductCatalogReceiptStore.IsHash(responseHash) || kind != string.Empty ||
                    (outcome == "http-accepted") != (status >= 200 && status < 300) ||
                    (outcome == "version-conflict") != conflict || conflict && status != 409)
                    throw new InvalidOperationException("IAP HTTP 结果组合无效。");
            }
            else if (status != 0 || conflict || (long)value["responseByteCount"] != 0 || responseHash != string.Empty ||
                !(outcome == "cancelled" && kind == "cancelled" || outcome == "transport-error" && (kind == "timeout" || kind == "transport") ||
                  outcome == "busy" && kind == "busy" && !dispatched || outcome == "cancelled-before-send" && kind == "cancelled" && !dispatched))
                throw new InvalidOperationException("IAP 异常结果组合无效。");
            if (!DateTimeOffset.TryParse((string)value["startedAtUtc"], out _) || !DateTimeOffset.TryParse((string)value["completedAtUtc"], out _))
                throw new InvalidOperationException("IAP 结果时间无效。");
            return value;
        }

        /// <summary>恢复载荷仅绑定当前操作的目录快照与回执，不反序列化任何可执行对象。</summary>
        internal static JObject ParseReceipt(string json)
        {
            JObject value = IapProductCatalogReceiptStore.Parse(json, new[] { "schemaVersion", "attemptId", "snapshot", "snapshotSha256", "recordSha256" });
            RequireVersion(value);
            foreach (string name in new[] { "attemptId", "snapshotSha256", "recordSha256" }) RequireString(value, name, 128);
            if (!Guid.TryParseExact((string)value["attemptId"], "N", out Guid id) || id.ToString("N") != (string)value["attemptId"] ||
                !IapProductCatalogReceiptStore.IsHash((string)value["snapshotSha256"]) ||
                (string)value["recordSha256"] != string.Empty && !IapProductCatalogReceiptStore.IsHash((string)value["recordSha256"]) || !(value["snapshot"] is JObject snapshot))
                throw new InvalidOperationException("IAP 恢复身份无效。");
            snapshot = IapProductCatalogReceiptStore.Parse(snapshot.ToString(Formatting.None), s_SnapshotFields);
            RequireVersion(snapshot);
            foreach (string name in new[] { "masterGuid", "runtimeGuid", "masterPath", "runtimePath", "activeBuildTarget", "masterMemorySha256", "runtimeMemorySha256", "codeIdentitySha256" }) RequireString(snapshot, name, 1024);
            foreach (string name in new[] { "masterGuid", "runtimeGuid" })
                if (!Guid.TryParseExact((string)snapshot[name], "N", out _)) throw new InvalidOperationException("IAP 配置 GUID 无效。");
            foreach (string name in new[] { "masterMemorySha256", "runtimeMemorySha256", "codeIdentitySha256" })
                if (!IapProductCatalogReceiptStore.IsHash((string)snapshot[name])) throw new InvalidOperationException("IAP 配置摘要无效。");
            if (!GenerateActionCommon.TryParseActiveBuildTarget((string)snapshot["activeBuildTarget"], out _, out _) ||
                !GenerateActionCommon.TryResolveAssetSavePath((string)snapshot["masterPath"], out string normalizedMaster, out _, out _) || normalizedMaster != (string)snapshot["masterPath"] ||
                !GenerateActionCommon.TryResolveAssetSavePath((string)snapshot["runtimePath"], out string normalizedRuntime, out _, out _) || normalizedRuntime != (string)snapshot["runtimePath"])
                throw new InvalidOperationException("IAP 恢复路径或构建目标无效。");
            if (GenerateActionCommon.ComputeTextHash(snapshot.ToString(Formatting.None)) != (string)value["snapshotSha256"] ||
                !(snapshot["files"] is JObject files) || !(snapshot["catalog"] is JObject))
                throw new InvalidOperationException("IAP 恢复快照摘要无效。");
            string masterPath = (string)snapshot["masterPath"], runtimePath = (string)snapshot["runtimePath"];
            string[] expected = { masterPath, masterPath + ".meta", runtimePath, runtimePath + ".meta", "ProjectSettings/Nova/Globals.json", "ProjectSettings/ProjectSettings.asset" };
            if (masterPath == runtimePath || files.Count != expected.Length ||
                expected.Any(path => files[path]?.Type != JTokenType.String || !IapProductCatalogReceiptStore.IsHash((string)files[path])))
                throw new InvalidOperationException("IAP 恢复文件集合无效。");
            ParseMetadata(snapshot["catalog"].ToString(Formatting.None));
            return value;
        }

        /// <summary>形成明确阶段的 partial；当前协议不提供服务端实际应用的权威证据。</summary>
        private AgentActionResult BuildResult(string attempt, JObject snapshot, string snapshotHash, string recordHash, JObject outcome, bool drift)
        {
            bool accepted = (string)outcome["outcome"] == "http-accepted", conflict = (bool)outcome["versionConflict"];
            AgentActionResult result = AgentActionResult.Create(Id, "partial", accepted ? "冻结目录已收到 HTTP 2xx；后台实际应用尚无权威确认。" :
                conflict ? "服务端明确拒绝此版本的新目录；未自动修改版本或重新提交。" : "提交未获得目录应用确认；保留真实结果，不自动重发。");
            result.ReceiptJson = Receipt(attempt, snapshot, snapshotHash, recordHash);
            result.DataJson = new JObject { ["attemptId"] = attempt, ["catalog"] = snapshot["catalog"].DeepClone(), ["outcome"] = outcome.DeepClone(),
                ["httpAccepted"] = accepted, ["remoteApplication"] = conflict ? "not-applied" : (bool)outcome["dispatched"] ? "unknown" : "not-dispatched",
                ["currentSourceDrift"] = drift }.ToString(Formatting.None);
            result.EvidenceKinds = AgentActionEvidence.Static | AgentActionEvidence.Artifact;
            result.Artifacts.Add(m_Store.RelativePath(attempt));
            result.Evidence.Add("只保存本 attempt 的真实有限响应事实；没有重放上传，未证明后台落库。");
            if (drift) result.Warnings.Add("当前源文件已变化；原提交结果仍按原冻结目录解释。");
            return result;
        }

        /// <summary>生成小型领域回执；不包含密文、密钥、请求正文或原始响应。</summary>
        private static string Receipt(string attempt, JObject snapshot, string snapshotHash, string recordHash) =>
            new JObject { ["schemaVersion"] = 1, ["attemptId"] = attempt, ["snapshot"] = snapshot.DeepClone(),
                ["snapshotSha256"] = snapshotHash, ["recordSha256"] = recordHash }.ToString(Formatting.None);

        /// <summary>只读比较原文件身份；当前状态不覆盖或撤销已经保存的原提交事实。</summary>
        private static bool SourceDrifted(JObject snapshot, CancellationToken token)
        {
            try
            {
                foreach (JProperty file in ((JObject)snapshot["files"]).Properties())
                    if (file.Value.Type != JTokenType.String || GenerateActionCommon.CaptureFile(file.Name, token).sha256 != (string)file.Value) return true;
                return (string)snapshot["activeBuildTarget"] != EditorUserBuildSettings.activeBuildTarget.ToString() ||
                    (string)snapshot["catalog"]["version"] != PlayerSettings.bundleVersion;
            }
            catch (Exception) { return true; }
        }

        /// <summary>核对固定协议版本，拒绝字符串数字及未来未知版本。</summary>
        private static void RequireVersion(JObject value)
        {
            if (value["schemaVersion"].Type != JTokenType.Integer || (long)value["schemaVersion"] != 1)
                throw new InvalidOperationException("IAP 载荷协议版本无效。");
        }

        /// <summary>拒绝自动转换类型和超长字符串，避免回执吞入响应正文或类型元数据。</summary>
        private static void RequireString(JObject value, string name, int maxLength)
        {
            if (value[name].Type != JTokenType.String || ((string)value[name]).Length > maxLength)
                throw new InvalidOperationException("IAP 字符串元数据无效。");
        }
    }
}
