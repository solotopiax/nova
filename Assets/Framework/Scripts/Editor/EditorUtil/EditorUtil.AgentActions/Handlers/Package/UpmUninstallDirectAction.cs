/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UpmUninstallDirectAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   UPM direct dependency 卸载的独立 Project Action 包装
 ***************************************************************/

using System;
using System.Threading.Tasks;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.upm.uninstall-direct",
        "卸载消费项目 direct UPM 依赖",
        "upm",
        AgentActionOperationType.Package,
        Effects = AgentActionEffect.WorkspaceRead |
                  AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead |
                  AgentActionEffect.UnityWrite |
                  AgentActionEffect.Destructive,
        RequiredEvidence = AgentActionEvidence.PackageResolution,
        Idempotency = AgentActionIdempotency.SubmitOnce,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        Locks = new string[]
        {
            "unity-editor",
            "unity-package-manager",
            "Packages/manifest.json",
            "Packages/packages-lock.json",
            "domain-reload",
        })]
    internal sealed class UpmUninstallDirectAction : AgentActionHandler<UpmUninstallDirectAction.Request>
    {
        private readonly UpmManageLatestAction _domainAdapter = new UpmManageLatestAction();

        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string packageName;
        }

        /// <summary>
        /// 卸载 Action 只接受一个精确包名，不接受 registry 或操作类型输入。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            return UpmManageLatestAction.TryValidatePackageName(request.packageName, out error);
        }

        /// <summary>
        /// 复用 PlugPals 的只读卸载计划，不复制 manifest、依赖消费者或 registry 清理算法。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            return _domainAdapter.PlanAsync(
                new UpmManageLatestAction.Request
                {
                    action = "uninstall",
                    packageName = request.packageName,
                },
                context);
        }

        /// <summary>
        /// 执行 PlugPals 已冻结的卸载计划，确认仍由 Dispatcher 绑定当前一次性 PlanId。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            return _domainAdapter.ExecuteAsync(state, context);
        }

        /// <summary>
        /// 复用 PlugPals 的只读后验，同时检查 direct manifest 与完整解析图。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            return _domainAdapter.VerifyAsync(receiptJson, context);
        }
    }
}
