/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BuildInspectReadinessAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   Player 构建前置条件的只读检查 Action
 ***************************************************************/

using System;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.build.inspect-readiness",
        "检查 Player 构建就绪状态",
        "build",
        AgentActionOperationType.Inspect,
        Description = "只读检查目标平台、Build Settings、Config、YooAsset 与 HybridCLR 的构建前置状态。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.UnityRead,
        RequiredEvidence = AgentActionEvidence.Static,
        Idempotency = AgentActionIdempotency.ReadOnly,
        ReloadSemantics = AgentActionReloadSemantics.ReloadNotExpected,
        Locks = new[] { "unity-editor", "build-settings", "active-config-master", "yooasset-settings", "hybridclr-settings" })]
    internal sealed class BuildInspectReadinessAction : AgentActionHandler<BuildInspectReadinessAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string target;
            public string packageName;
        }

        private sealed class State
        {
            public string PayloadJson;
        }

        /// <summary>
        /// 严格校验 BuildTarget，并将可选 Package 名限制为安全的 YooAsset 标识符。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!GenerateActionCommon.TryParseActiveBuildTarget(request?.target, out _, out error)) return false;
            if (!string.IsNullOrWhiteSpace(request.packageName) &&
                !BuildActionCommon.TryValidateName("packageName", request.packageName, out error)) return false;
            return true;
        }

        /// <summary>
        /// 只读冻结当前就绪报告；报告含 Error 仍是一次成功完成的 Inspect 计划。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            GenerateActionCommon.TryParseActiveBuildTarget(request.target, out BuildTarget target, out _);
            EditorUtil.ProjectGuard.BuildReadinessReport report =
                EditorUtil.ProjectGuard.InspectBuildReadiness(target, request.packageName);
            string payload = Util.Json.Serialize(request);
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = report.ready
                    ? $"{target} 构建前置检查通过，存在 {report.warningCount} 个警告。"
                    : $"{target} 构建前置检查完成，发现 {report.errorCount} 个错误、{report.warningCount} 个警告。",
                DataJson = Util.Json.Serialize(report),
                State = new State { PayloadJson = payload },
                RecoveryPayloadJson = payload,
                Evidence = new[]
                {
                    "只读冻结 Target、启用场景、Config 坐标、YooAsset Package、HybridCLR 与平台前置；未调用构建、生成、保存或自动修复。",
                    "Inspect 执行成功只表示规则已完成；是否可构建由 DataJson.ready 与 rules 判断。",
                },
            });
        }

        /// <summary>
        /// 重新执行同一只读探针并返回可恢复 Verify 的 Receipt。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State typed) || string.IsNullOrWhiteSpace(typed.PayloadJson))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "构建就绪检查计划状态无效。"));
            }
            return VerifyPayloadAsync(typed.PayloadJson);
        }

        /// <summary>
        /// 按 Receipt 请求重新读取当前工程状态；不会恢复或重放任何构建操作。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            return VerifyPayloadAsync(receiptJson);
        }

        /// <summary>
        /// 解析冻结请求并生成最新只读报告。
        /// </summary>
        private static Task<AgentActionResult> VerifyPayloadAsync(string payloadJson)
        {
            Request request;
            try
            {
                request = Util.Json.Deserialize<Request>(payloadJson);
            }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "构建就绪 Receipt 无法解析：" + exception.Message));
            }
            if (!GenerateActionCommon.TryParseActiveBuildTarget(request?.target, out BuildTarget target, out string error))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "构建就绪 Receipt 无效：" + error));
            }

            EditorUtil.ProjectGuard.BuildReadinessReport report =
                EditorUtil.ProjectGuard.InspectBuildReadiness(target, request.packageName);
            AgentActionResult result = AgentActionResult.Create(null, "success", report.ready
                ? "构建前置规则已完成且未发现错误。"
                : $"构建前置规则已完成：{report.errorCount} 个错误、{report.warningCount} 个警告。");
            result.DataJson = Util.Json.Serialize(report);
            result.ReceiptJson = payloadJson;
            result.EvidenceKinds = AgentActionEvidence.Static;
            result.Evidence.Add("已重新读取当前 Unity 工程状态；未写 SessionState、项目资产、ProjectSettings 或构建产物。" );
            result.Warnings.Add("该结果不证明 Player 构建、启动、运行时流程或真机行为成功。" );
            return Task.FromResult(result);
        }
    }
}
