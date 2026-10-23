/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaProjectActionGateway.PlanJobs.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   同一受控网关的长计划登记、读取及清场
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace NovaFramework.Mcp.Editor
{
    public static partial class NovaProjectActionGateway
    {
        private const string PlanJobSessionKey = "Nova.ProjectActions.PlanJobs.v1";
        private static readonly string s_PlanJobDomainId = Guid.NewGuid().ToString("N");
        private static int s_PlanJobMainThread;
        private static NovaProjectActionPlanJobStore s_PlanJobs;

        /// <summary>由 Unity 域初始化回调记录主线程；普通请求不能自行宣称所在线程为主线程。</summary>
        [InitializeOnLoadMethod]
        private static void InitializePlanJobMainThread()
        {
            s_PlanJobMainThread = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>仅在主线程惰性恢复自有会话记录；不触发原计划或反序列化可执行状态。</summary>
        private static NovaProjectActionPlanJobStore GetPlanJobs()
        {
            if (s_PlanJobMainThread == 0 || Thread.CurrentThread.ManagedThreadId != s_PlanJobMainThread)
                throw new InvalidOperationException("长计划只能通过 Unity 主线程网关使用。");
            return s_PlanJobs ?? (s_PlanJobs = new NovaProjectActionPlanJobStore(
                SessionState.GetString(PlanJobSessionKey, string.Empty), s_PlanJobDomainId, () => DateTime.UtcNow,
                json => SessionState.SetString(PlanJobSessionKey, json), 16, TimeSpan.FromHours(1)));
        }

        /// <summary>登记一次只读计划并立即返回；同号重试只读已有状态，不会再次调用 Provider。</summary>
        private static NovaProjectActionGatewayResponse HandlePlanStart(JObject parameters,
            IReadOnlyDictionary<string, NovaProjectActionDescriptor> exposedActions)
        {
            RequestError error = ValidateOperationFields(parameters,
                new[] { "operation", "plan_mode", "action_id", "request_id", "request" }, Array.Empty<string>());
            if (error != null) return Error(error.Code, error.Message);
            string requestId = (string)parameters["request_id"];
            if (!Guid.TryParseExact(requestId, "N", out _)) return Error("invalid_request_id", "request_id 必须为调用方新建的 N 格式 GUID。");
            string actionId = (string)parameters["action_id"];
            string hash = HashPlanRequest(actionId, parameters["request"]);
            NovaProjectActionPlanJobStore store = GetPlanJobs();
            NovaProjectActionPlanJobStore.Record existing = store.Get(requestId, actionId);
            if (existing != null)
                return existing.RequestHash == hash ? ToPlanJobResponse("start", existing) :
                    Error("request_id_conflict", "同一 request_id 不允许改变 Action 或请求；不会重跑。");
            if (s_VerifyJobs != null && GetVerifyJobs().HasActive()) return Error("action_busy", "另一个长核验正在等待或运行；请先读取其状态。");
            if (!s_Gate.Wait(0)) return Error("action_busy", "另一个 Nova Project Action 请求正在处理。");
            try
            {
                if (IsEditorBusy()) return Error("editor_busy", "Unity 正在编译或更新包，请稳定后重新计划。");
                if (!TryGetExposedDescriptor(actionId, exposedActions, out NovaProjectActionDescriptor descriptor))
                    return ActionResponse("plan", actionId, "not_applicable", "该 Action 未注册或未向 MCP 开放。", null);
                error = ValidateActionRequest(descriptor, (JObject)parameters["request"]);
                if (error != null) return Error(error.Code, error.Message);
                if (!store.TryBegin(requestId, actionId, hash, out NovaProjectActionPlanJobStore.Record record,
                        out bool created, out string beginError))
                    return Error(beginError, "长计划登记被拒绝；记录容量或活动计划冲突，不会自动重跑。");
                if (created)
                {
                    var request = new JObject
                    {
                        ["operation"] = "plan", ["action_id"] = actionId,
                        ["request"] = parameters["request"].DeepClone(),
                    };
                    EditorApplication.CallbackFunction scheduled = null;
                    scheduled = () =>
                    {
                        EditorApplication.update -= scheduled;
                        RunQueuedPlanAsync(requestId, actionId, request);
                    };
                    EditorApplication.update += scheduled;
                }
                return ToPlanJobResponse("start", record);
            }
            finally { s_Gate.Release(); }
        }

        /// <summary>读取或明确忘记一条传输记录；恢复读取不依赖当前 Registry，也不获得执行权限。</summary>
        private static NovaProjectActionGatewayResponse HandlePlanJobControl(JObject parameters, bool forget)
        {
            RequestError error = ValidateOperationFields(parameters,
                new[] { "operation", "plan_mode", "action_id", "request_id" }, Array.Empty<string>());
            if (error != null) return Error(error.Code, error.Message);
            string requestId = (string)parameters["request_id"];
            if (!Guid.TryParseExact(requestId, "N", out _)) return Error("invalid_request_id", "request_id 格式无效。");
            string actionId = (string)parameters["action_id"];
            NovaProjectActionPlanJobStore store = GetPlanJobs();
            NovaProjectActionPlanJobStore.Record record = store.Get(requestId, actionId);
            if (record == null) return Error("plan_job_not_found", "未找到本 Action 的长计划记录；不会重跑。");
            if (!forget) return ToPlanJobResponse("status", record);
            if (!store.Forget(requestId, actionId)) return Error("action_busy", "尚未结束的计划不能忘记。");
            return ActionResponse("plan", actionId, "success", "已删除本条传输记录；原领域 Plan 与 Operation 的生命周期保持独立。", null);
        }

        /// <summary>下一次 Editor update 仍串行复用同一正式 Plan；所有异常都形成安全终态，绝不自动重试。</summary>
        private static async void RunQueuedPlanAsync(string requestId, string actionId, JObject request)
        {
            bool gateHeld = false;
            try
            {
                NovaProjectActionPlanJobStore store = GetPlanJobs();
                if (!store.TryMarkRunning(requestId, actionId)) return;
                NovaProjectActionGatewayResponse response;
                if (!(gateHeld = await s_Gate.WaitAsync(0))) response = Error("action_busy", "计划开始前网关已被占用，请读取结果后使用新请求编号重新计划。");
                else if (!TryGetExposedDescriptors(out INovaProjectActionProvider provider,
                             out IReadOnlyDictionary<string, NovaProjectActionDescriptor> exposed, out string registryError))
                    response = Error("action_registry_unavailable", registryError);
                else response = await HandlePlanAsync(request, provider, exposed);
                store.Complete(requestId, actionId, JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                try
                {
                    GetPlanJobs().Complete(requestId, actionId, JsonConvert.SerializeObject(
                        Error("internal_error", "长计划未正常完成；不会自动重试或执行。")));
                }
                catch (Exception) { /* 会话写入失败保留原状态，后续重载只能标记中断，不能重跑。 */ }
            }
            finally { if (gateHeld) s_Gate.Release(); }
        }

        /// <summary>在原响应外附加传输状态；同域未过期的 ready 仅是候选，Registry、输入漂移和一次性消费仍由 Core Execute 重新裁决。</summary>
        private static NovaProjectActionGatewayResponse ToPlanJobResponse(string operation, NovaProjectActionPlanJobStore.Record record)
        {
            JToken response = string.IsNullOrEmpty(record.ResponseJson) ? null : JsonConvert.DeserializeObject<JToken>(record.ResponseJson, new JsonSerializerSettings
            { DateParseHandling = DateParseHandling.None, TypeNameHandling = TypeNameHandling.None, MaxDepth = 128 });
            bool executable = record.State == "completed" && record.DomainId == s_PlanJobDomainId &&
                              (string)response?["data"]?["status"] == "ready" &&
                              DateTime.TryParse((string)response?["data"]?["payload"]?["expires_at_utc"], null,
                                  System.Globalization.DateTimeStyles.RoundtripKind, out DateTime expiry) && expiry.ToUniversalTime() > DateTime.UtcNow;
            return ActionResponse("plan", record.ActionId, record.State,
                "此状态只描述计划收件；请读取原响应，blocked 不可执行；重载或过期后需新编号重新计划；plan_candidate 不保证 Registry、输入或一次性计划仍有效。",
                new { plan_mode = operation, request_id = record.RequestId, expires_at_utc = record.ExpiresAtUtc.ToString("O"),
                    plan_candidate = executable, response });
        }

        /// <summary>对象属性排序后计算请求摘要；数组顺序保留，不把原始请求或凭据存入会话。</summary>
        private static string HashPlanRequest(string actionId, JToken request)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(actionId + "\n" +
                    CanonicalPlanRequest(request).ToString(Formatting.None)))).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>只规范化 JSON 对象属性顺序，保持数字、字符串、null 与数组的原始语义。</summary>
        private static JToken CanonicalPlanRequest(JToken token)
        {
            if (token is JObject obj) return new JObject(obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal)
                .Select(item => new JProperty(item.Name, CanonicalPlanRequest(item.Value))));
            if (token is JArray array) return new JArray(array.Select(CanonicalPlanRequest));
            return token.DeepClone();
        }
    }
}
