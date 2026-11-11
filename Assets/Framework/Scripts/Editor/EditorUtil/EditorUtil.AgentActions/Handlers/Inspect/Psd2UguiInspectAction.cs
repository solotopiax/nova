/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Psd2UguiInspectAction.cs
 * author:    Codex
 * created:   2026/11/10
 * descrip:   PSD 转 UI 的只读发现与节点检查
 ***************************************************************/

using System;
using System.Threading.Tasks;

namespace NovaFramework.Editor
{
    /// <summary>通过可选包固定协议发现 PSD、配置和可编辑节点，不创建或修复资产。</summary>
    [AgentAction("nova.project.psd2ugui.inspect", "检查 PSD 转 UI", "psd2ugui", AgentActionOperationType.Inspect,
        Description = "只读发现 PSD、配置、转换节点和现有 AI 任务；空路径用于发现项目现状。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.UnityRead,
        RequiredEvidence = AgentActionEvidence.Static, Idempotency = AgentActionIdempotency.ReadOnly,
        RequiresEditMode = true, ReloadSemantics = AgentActionReloadSemantics.PlanInvalidatedVerifyOnly,
        Locks = new[] { "unity-editor", "psd2ugui" })]
    internal sealed class Psd2UguiInspectAction : AgentActionHandler<Psd2UguiInspectAction.Request>
    {
        internal const string Id = "nova.project.psd2ugui.inspect";

        /// <summary>仅接受可选的项目资产路径；省略时由内包发现既有对象。</summary>
        [Serializable]
        public sealed class Request
        {
            public string psdAssetPath;
            public string configAssetPath;
        }

        /// <summary>拒绝绝对路径和逃逸路径，存在性与实际类型由固定内包协议检查。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = request != null && Psd2UguiActionBridge.IsAssetPath(request.psdAssetPath) &&
                Psd2UguiActionBridge.IsAssetPath(request.configAssetPath) ? null : "PSD 和配置必须是 Assets 下的项目相对路径；省略路径可发现现有资产。";
            return error == null;
        }

        /// <summary>只读 Inspect 并冻结桥身份；缺包为不适用，旧包缺固定协议时明确阻断。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryValidateRequest(request, out string error))
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = error });
            if (!Psd2UguiActionBridge.TryCreate(out Psd2UguiActionBridge bridge, out bool missing, out error))
                return Task.FromResult(new AgentActionHandlerPlan { Status = missing ? "not_applicable" : "blocked", Summary = error });
            try
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                string requestJson = Psd2UguiActionBridge.SerializeRequest(request);
                Psd2UguiActionBridge.Response response = bridge.Inspect(requestJson);
                string receipt = Psd2UguiActionBridge.CreateReceipt(Id, bridge.IdentityJson, "inspect", response.ReceiptJson, requestJson);
                bool ready = response.Success && (response.State == "ready" || response.State == "succeeded");
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = ready ? "ready" : "blocked", Summary = response.Message,
                    State = receipt, RecoveryPayloadJson = receipt, DataJson = Psd2UguiActionBridge.PublicData(response), Evidence = response.Evidence,
                });
            }
            catch
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "PSD 转 UI 检查未取得有效结果；没有创建或修改资产。" });
            }
        }

        /// <summary>在当前域重新核对身份并只读采集检查结果，不把计划时快照当作当前状态。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            return VerifyAsync(state as string, context);
        }

        /// <summary>重载后仅按原检查参数重新读取；桥身份变动时拒绝解释旧回执。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                Psd2UguiActionBridge.Receipt receipt = Psd2UguiActionBridge.ParseReceipt(receiptJson, Id);
                if (receipt.operation != "inspect" || !TryParseRequest(receipt.requestJson, out _, out _) ||
                    !Psd2UguiActionBridge.TryCreate(out Psd2UguiActionBridge bridge, out _, out _) || bridge.IdentityJson != receipt.identityJson)
                    return Task.FromResult(Failure(receiptJson, "PSD 转 UI 检查参数或包身份已变化；请重新检查。"));
                return Task.FromResult(Psd2UguiActionBridge.BuildResult(Id, bridge.Inspect(receipt.requestJson), receiptJson, true));
            }
            catch
            {
                return Task.FromResult(Failure(receiptJson, "PSD 转 UI 检查回执不可用，未修改任何资产。"));
            }
        }

        /// <summary>检查失败仍保留原回执，便于定位身份或协议问题。</summary>
        private static AgentActionResult Failure(string receipt, string message)
        {
            AgentActionResult result = AgentActionResult.Create(Id, "blocked", message);
            result.ReceiptJson = receipt;
            return result;
        }
    }
}
