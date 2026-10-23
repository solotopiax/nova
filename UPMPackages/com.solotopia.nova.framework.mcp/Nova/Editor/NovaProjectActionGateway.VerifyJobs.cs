/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaProjectActionGateway.VerifyJobs.cs
 * author:    taoye
 * created:   2026/10/19
 * descrip:   复用有界收件箱保存一次正式只读Verify的完整脱敏响应
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace NovaFramework.Mcp.Editor
{
    public static partial class NovaProjectActionGateway
    {
        // Verify使用独立命名空间；原Plan会话、Receipt与Operation存储不被转换或覆盖。
        private const string VerifyJobSessionKey = "Nova.ProjectActions.VerifyJobs.v1";
        private static NovaProjectActionPlanJobStore s_VerifyJobs;

        /// <summary>只在已初始化的Unity主线程恢复自有Verify传输会话，复用原16项、一小时、2MiB有界算法。</summary>
        /// <returns>不包含可执行状态或原Receipt的独立收件箱。</returns>
        private static NovaProjectActionPlanJobStore GetVerifyJobs()
        {
            if (s_PlanJobMainThread == 0 || Thread.CurrentThread.ManagedThreadId != s_PlanJobMainThread)
                throw new InvalidOperationException("长核验只能通过 Unity 主线程网关使用。");
            return s_VerifyJobs ?? (s_VerifyJobs = new NovaProjectActionPlanJobStore(
                SessionState.GetString(VerifyJobSessionKey, string.Empty), s_PlanJobDomainId, () => DateTime.UtcNow,
                json => SessionState.SetString(VerifyJobSessionKey, json), 16, TimeSpan.FromHours(1)));
        }

        /// <summary>登记一次原正式Verify；同号同Action与Receipt只读取已有记录，不再次调用Provider。</summary>
        /// <param name="parameters">精确start字段；Receipt仅留在本次排队闭包，不持久化原文。</param>
        /// <param name="exposedActions">当前已验证的暴露策略。</param>
        /// <returns>立即返回queued或已有传输状态，不宣称核验成功。</returns>
        private static NovaProjectActionGatewayResponse HandleVerifyStart(JObject parameters,
            IReadOnlyDictionary<string, NovaProjectActionDescriptor> exposedActions)
        {
            RequestError error = ValidateOperationFields(parameters,
                new[] { "operation", "verify_mode", "action_id", "request_id", "receipt" }, Array.Empty<string>());
            if (error != null) return Error(error.Code, error.Message);
            string requestId = (string)parameters["request_id"];
            if (!Guid.TryParseExact(requestId, "N", out _)) return Error("invalid_request_id", "request_id 必须为调用方新建的 N 格式 GUID。");
            string actionId = (string)parameters["action_id"];
            string hash = HashPlanRequest("verify:" + actionId, parameters["receipt"]);
            NovaProjectActionPlanJobStore store = GetVerifyJobs();
            NovaProjectActionPlanJobStore.Record existing = store.Get(requestId, actionId);
            if (existing != null)
                return existing.RequestHash == hash ? ToVerifyJobResponse("start", existing) :
                    Error("request_id_conflict", "同一 request_id 不允许改变 Action 或 Receipt；不会重跑。");
            if (s_PlanJobs != null && s_PlanJobs.HasActive()) return Error("action_busy", "另一个长计划正在等待或运行；请先读取其状态。");
            if (!s_Gate.Wait(0)) return Error("action_busy", "另一个 Nova Project Action 请求正在处理。");
            try
            {
                if (IsEditorBusy()) return Error("editor_busy", "Unity 正在编译或更新包，请稳定后重新核验。");
                if (!TryGetExposedDescriptor(actionId, exposedActions, out _))
                    return ActionResponse("verify", actionId, "not_applicable", "该 Action 未注册或未向 MCP 开放。", null);
                if (!store.TryBegin(requestId, actionId, hash, out NovaProjectActionPlanJobStore.Record record,
                        out bool created, out string beginError))
                    return Error(beginError, "长核验登记被拒绝；记录容量或活动任务冲突，不会自动重跑。");
                if (created)
                {
                    var request = new JObject
                    {
                        ["operation"] = "verify", ["action_id"] = actionId,
                        ["receipt"] = (string)parameters["receipt"],
                    };
                    EditorApplication.CallbackFunction scheduled = null;
                    scheduled = () =>
                    {
                        EditorApplication.update -= scheduled;
                        RunQueuedVerifyAsync(requestId, actionId, request);
                    };
                    EditorApplication.update += scheduled;
                }
                return ToVerifyJobResponse("start", record);
            }
            finally { s_Gate.Release(); }
        }

        /// <summary>读取或明确忘记一条Verify传输记录；不读取新观察、不进Provider，也不改变原领域结果。</summary>
        /// <param name="parameters">只含模式、Action及原请求编号。</param><param name="forget">是否明确释放终态传输记录。</param>
        /// <returns>原完整脱敏响应或明确的安全错误。</returns>
        private static NovaProjectActionGatewayResponse HandleVerifyJobControl(JObject parameters, bool forget)
        {
            RequestError error = ValidateOperationFields(parameters,
                new[] { "operation", "verify_mode", "action_id", "request_id" }, Array.Empty<string>());
            if (error != null) return Error(error.Code, error.Message);
            string requestId = (string)parameters["request_id"];
            if (!Guid.TryParseExact(requestId, "N", out _)) return Error("invalid_request_id", "request_id 格式无效。");
            string actionId = (string)parameters["action_id"];
            NovaProjectActionPlanJobStore store = GetVerifyJobs();
            NovaProjectActionPlanJobStore.Record record = store.Get(requestId, actionId);
            if (record == null) return Error("verify_job_not_found", "未找到本 Action 的长核验记录；不会重跑。");
            if (!forget) return ToVerifyJobResponse("status", record);
            if (!store.Forget(requestId, actionId)) return Error("action_busy", "尚未结束的核验不能忘记。");
            return ActionResponse("verify", actionId, "success", "已删除本条传输记录；原领域 Receipt 与 Operation 保持原样。", null);
        }

        /// <summary>下一次Editor update仅一次复用原HandleVerifyAsync及None令牌，结束后保存原完整脱敏响应；不重试或重放Execute。</summary>
        /// <param name="requestId">原登记编号。</param><param name="actionId">原Action。</param><param name="request">仅在本次闭包持有的原Receipt。</param>
        private static async void RunQueuedVerifyAsync(string requestId, string actionId, JObject request)
        {
            bool gateHeld = false;
            try
            {
                NovaProjectActionPlanJobStore store = GetVerifyJobs();
                if (!store.TryMarkRunning(requestId, actionId)) return;
                NovaProjectActionGatewayResponse response;
                if (!(gateHeld = await s_Gate.WaitAsync(0))) response = Error("action_busy", "核验开始前网关已被占用，不会重跑原核验。");
                else if (!TryGetExposedDescriptors(out INovaProjectActionProvider provider,
                             out IReadOnlyDictionary<string, NovaProjectActionDescriptor> exposed, out string registryError))
                    response = Error("action_registry_unavailable", registryError);
                else response = await HandleVerifyAsync(request, provider, exposed);
                store.Complete(requestId, actionId, JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                try
                {
                    GetVerifyJobs().Complete(requestId, actionId, JsonConvert.SerializeObject(
                        Error("internal_error", "长核验未正常完成；不会自动重试或执行。")));
                }
                catch (Exception) { /* 会话写入失败保留原状态；后续域只能中断，不能自动重跑。 */ }
            }
            finally { if (gateHeld) s_Gate.Release(); }
        }

        /// <summary>只展示传输状态与原响应；completed不升级原partial或failure，也不产生Plan执行资格。</summary>
        /// <param name="mode">本次传输子模式。</param><param name="record">已有有界收件记录。</param>
        /// <returns>完整原响应和明确的Verify传输元数据，不含plan_candidate。</returns>
        private static NovaProjectActionGatewayResponse ToVerifyJobResponse(string mode, NovaProjectActionPlanJobStore.Record record)
        {
            JToken response = string.IsNullOrEmpty(record.ResponseJson) ? null : JsonConvert.DeserializeObject<JToken>(record.ResponseJson, new JsonSerializerSettings
            { DateParseHandling = DateParseHandling.None, TypeNameHandling = TypeNameHandling.None, MaxDepth = 128 });
            return ActionResponse("verify", record.ActionId, record.State,
                "此状态只描述核验收件；请归档原完整响应，completed不代表领域成功，重载、过期或断线不自动重跑。",
                new { verify_mode = mode, request_id = record.RequestId, expires_at_utc = record.ExpiresAtUtc.ToString("O"), response });
        }
    }
}
