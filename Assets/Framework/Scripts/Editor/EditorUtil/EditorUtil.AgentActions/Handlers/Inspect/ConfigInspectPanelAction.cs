/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigInspectPanelAction.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   Config 实际面板字段及掩码的只读查阅
 ***************************************************************/

using System;
using System.Linq;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using Panels = NovaFramework.Editor.EditorUtil.Config.PanelEditing;

namespace NovaFramework.Editor
{
    [AgentAction("nova.project.config.inspect-panel", "查阅 Config 面板", "config", AgentActionOperationType.Inspect,
        Description = "只读描述真实面板字段、当前值、维度及同组坐标，凭据脱敏；不补结构或保存。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.UnityRead, RequiredEvidence = AgentActionEvidence.Static,
        Idempotency = AgentActionIdempotency.ReadOnly, RequiresEditMode = true, Locks = new[] { "unity-editor", "configmaster-assets" })]
    internal sealed class ConfigInspectPanelAction : AgentActionHandler<ConfigInspectPanelAction.Request>
    {
        /// <summary>真实 Master 与明确面板三维坐标；不存在默认或邻格推断。</summary>
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string masterGuid;
            [AgentActionRequired] public Panels.Target target;
        }

        /// <summary>只读计划重新读取实际状态，执行时不沿用旧值。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            AgentActionResult result = Read(request);
            return Task.FromResult(new AgentActionHandlerPlan { Status = result.Status == "success" ? "ready" : "blocked",
                Summary = result.Message, DataJson = result.DataJson, State = request,
                RecoveryPayloadJson = Util.Json.Serialize(request), Evidence = result.Evidence.ToArray() });
        }

        /// <summary>读取当前实际面板值与边界，保持只读。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context) =>
            Task.FromResult(state is Request request ? Read(request) : AgentActionResult.Create(null, "blocked", "面板查阅计划无效。"));

        /// <summary>只读恢复查阅；不应用或保存任何字段。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try { return Task.FromResult(Read(Util.Json.Deserialize<Request>(receiptJson))); }
            catch { return Task.FromResult(AgentActionResult.Create(null, "blocked", "面板查阅 Receipt 无效。")); }
        }

        /// <summary>只读加载面板并脱敏字段，未知或缺失结构明确拒绝。</summary>
        private static AgentActionResult Read(Request request)
        {
            try
            {
                if (request == null || !ConfigPatchPanelsAction.TryLoad(request.masterGuid, false, out ConfigMasterSO master, out _, out string error))
                    return AgentActionResult.Create(null, "blocked", "未找到可读 ConfigMaster。");
                AgentActionResult result = AgentActionResult.Create(null, "success", "已读取实际面板字段、掩码与同组坐标；凭据已脱敏。");
                result.DataJson = Util.Json.Serialize(Panels.Inspect(master, request.target));
                result.ReceiptJson = Util.Json.Serialize(request);
                result.EvidenceKinds = AgentActionEvidence.Static;
                result.Evidence.Add("只读 Unity 实际序列化字段，不补实例、不改维度、不保存或导出。");
                return result;
            }
            catch { return AgentActionResult.Create(null, "blocked", "面板坐标、类型、掩码或实际配置结构无效。"); }
        }
    }
}
