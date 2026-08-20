/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UpmManageLatestAction.cs
 * author:    taoye
 * created:   2026/8/19
 * descrip:   UPM 最新版本安装与升级的 Project Action 包装
 ***************************************************************/

using System;
using System.Linq;
using System.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.upm.manage-latest",
        "管理消费项目 UPM 包",
        "upm",
        AgentActionOperationType.Package,
        Effects = AgentActionEffect.WorkspaceRead |
                  AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead |
                  AgentActionEffect.UnityWrite |
                  AgentActionEffect.ExternalRead,
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
    internal sealed class UpmManageLatestAction : AgentActionHandler<UpmManageLatestAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string action;
            [AgentActionRequired] public string packageName;
            public string registryUrl;
        }

        private sealed class State
        {
            public string PlugPalsPlanId;
            public string Action;
        }

        [Serializable]
        private sealed class PlanView
        {
            public string status;
            public string action;
            public string packageName;
            public string currentVersion;
            public string targetVersion;
            public string selectedRegistryUrl;
            public string[] messages;
            public string[] consumers;
            public string[] missingDependencies;
        }

        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (request.action != "install-latest" && request.action != "upgrade-latest")
            {
                error = "action 只允许 install-latest 或 upgrade-latest；卸载必须使用 nova.project.upm.uninstall-direct。";
                return false;
            }
            if (!TryValidatePackageName(request.packageName, out error))
            {
                return false;
            }
            if (!TryValidateRegistryUrl(request.registryUrl, out error))
            {
                return false;
            }
            error = null;
            return true;
        }

        public override async Task<AgentActionHandlerPlan> PlanAsync(
            Request request,
            AgentActionExecutionContext context)
        {
            if (request.action != "uninstall" && !TryValidateConfiguredRegistryUrls(out string registryError))
            {
                return new AgentActionHandlerPlan
                {
                    Status = "blocked",
                    Summary = registryError,
                    Evidence = new[] { "Plan 在读取 registry 目录前拒绝了不安全的已配置 URL。" },
                };
            }

            EditorUtil.PlugPals.ProjectPackageOperationPlan plan =
                await EditorUtil.PlugPals.PlanProjectPackageOperationAsync(
                    new EditorUtil.PlugPals.ProjectPackageOperationRequest
                    {
                        action = request.action,
                        packageName = request.packageName,
                        registryUrl = request.registryUrl,
                    },
                    context.CancellationToken);

            if (plan == null)
            {
                return new AgentActionHandlerPlan
                {
                    Status = "blocked",
                    Summary = "UPM 领域计划未返回结果；不会创建可执行计划或恢复记录。",
                };
            }

            if (!TryValidateRegistryUrl(plan.selectedRegistryUrl, out registryError))
            {
                return new AgentActionHandlerPlan
                {
                    Status = "blocked",
                    Summary = "UPM 计划选中了不安全的 registry URL；不会创建可执行计划或恢复记录。",
                    Evidence = new[] { "Plan 拒绝将认证、query 或 fragment 写入 Action Recovery Receipt。" },
                };
            }

            string summary = plan.messages == null || plan.messages.Count == 0
                ? $"UPM 操作计划状态：{plan.status}。"
                : string.Join("；", plan.messages);
            var result = new AgentActionHandlerPlan
            {
                Status = plan.status,
                Summary = summary,
                DataJson = Util.Json.Serialize(new PlanView
                {
                    status = plan.status,
                    action = plan.action,
                    packageName = plan.packageName,
                    currentVersion = plan.currentVersion,
                    targetVersion = plan.targetVersion,
                    selectedRegistryUrl = plan.selectedRegistryUrl,
                    messages = plan.messages?.ToArray() ?? Array.Empty<string>(),
                    consumers = plan.consumers?.ToArray() ?? Array.Empty<string>(),
                    missingDependencies = plan.missingDependencies?.ToArray() ?? Array.Empty<string>(),
                }),
                Evidence = plan.messages?.ToArray() ?? Array.Empty<string>(),
            };

            if (plan.status == "ready")
            {
                result.State = new State { PlugPalsPlanId = plan.planId, Action = plan.action };
                result.WriteSet = new[]
                {
                    "Packages/manifest.json",
                    "Packages/packages-lock.json (Unity Package Manager Resolve)",
                };
                result.RecoveryPayloadJson = Util.Json.Serialize(new EditorUtil.PlugPals.ProjectPackageOperationReceipt
                {
                    action = plan.action,
                    packageName = plan.packageName,
                    expectedVersion = plan.targetVersion,
                    expectedRegistryUrl = plan.action == "uninstall" ? null : plan.selectedRegistryUrl,
                });
            }

            return result;
        }

        public override async Task<AgentActionResult> ExecuteAsync(
            object state,
            AgentActionExecutionContext context)
        {
            if (!(state is State upmState) || string.IsNullOrEmpty(upmState.PlugPalsPlanId))
            {
                return AgentActionResult.Create(null, "blocked", "UPM Action 的内部计划状态无效。");
            }

            if (upmState.Action != "uninstall" && !TryValidateConfiguredRegistryUrls(out string registryError))
            {
                return AgentActionResult.Create(null, "blocked", registryError);
            }

            EditorUtil.PlugPals.ProjectPackageOperationResult result =
                await EditorUtil.PlugPals.ExecutePlannedProjectPackageOperationAsync(
                    upmState.PlugPalsPlanId,
                    true,
                    context.CancellationToken);

            if (result?.receipt != null && !TryValidateRegistryUrl(result.receipt.expectedRegistryUrl, out _))
            {
                AgentActionResult unsafeResult = AgentActionResult.Create(
                    null,
                    "partial",
                    "UPM 操作已返回，但 registry URL 不满足安全收据契约；不会持久化该 Receipt 或自动重放。");
                unsafeResult.Evidence.Add("UPM 结果中的不安全 registry URL 已被隔离，未写入 Action Operation。");
                return unsafeResult;
            }

            if (result == null)
            {
                return AgentActionResult.Create(null, "partial", "UPM 领域执行未返回结果；不会自动重放。");
            }

            AgentActionResult actionResult = AgentActionResult.Create(null, result.status, result.message);
            actionResult.DataJson = Util.Json.Serialize(result);
            if (result.receipt != null)
            {
                actionResult.ReceiptJson = Util.Json.Serialize(result.receipt);
                actionResult.Evidence.Add("UPM manifest 已提交；Receipt 可跨 domain reload 用于只读 Verify。");
            }
            return actionResult;
        }

        public override Task<AgentActionResult> VerifyAsync(
            string receiptJson,
            AgentActionExecutionContext context)
        {
            EditorUtil.PlugPals.ProjectPackageOperationReceipt receipt;
            try
            {
                receipt = Util.Json.Deserialize<EditorUtil.PlugPals.ProjectPackageOperationReceipt>(receiptJson);
            }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "UPM Receipt 无法解析：" + exception.Message));
            }

            if (receipt == null)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "UPM Receipt 不能为空。"));
            }

            if (!TryValidateRegistryUrl(receipt.expectedRegistryUrl, out string registryError))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", registryError));
            }

            EditorUtil.PlugPals.ProjectPackageOperationResult result =
                EditorUtil.PlugPals.VerifyProjectPackageOperation(receipt);
            AgentActionResult actionResult = AgentActionResult.Create(null, result.status, result.message);
            actionResult.DataJson = Util.Json.Serialize(result);
            actionResult.ReceiptJson = receiptJson;
            if (result.status == "success")
            {
                actionResult.EvidenceKinds = AgentActionEvidence.PackageResolution;
            }
            actionResult.Evidence.Add("Verify 只读核对 manifest、packages-lock、精确版本与 registry 来源。");
            return Task.FromResult(actionResult);
        }

        /// <summary>
        /// MCP/OperationStore 不接受带认证信息、query 或 fragment 的 registry URL。
        /// 这些字符串可能进入 Plan/Receipt，必须在任何持久化之前拒绝。
        /// </summary>
        private static bool TryValidateRegistryUrl(string value, out string error)
        {
            if (string.IsNullOrEmpty(value))
            {
                error = null;
                return true;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri registryUri) ||
                (registryUri.Scheme != Uri.UriSchemeHttp && registryUri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrEmpty(registryUri.Host) ||
                !string.IsNullOrEmpty(registryUri.UserInfo) ||
                !string.IsNullOrEmpty(registryUri.Query) ||
                !string.IsNullOrEmpty(registryUri.Fragment))
            {
                error = "registryUrl 必须是不含认证、query 或 fragment 的绝对 HTTP(S) URL，且只用于已配置 registry 消歧。";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 验证单一 UPM 包名，供安装、升级与独立卸载 Action 共用同一输入边界。
        /// </summary>
        internal static bool TryValidatePackageName(string value, out string error)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 214 || value != value.Trim())
            {
                error = "packageName 必须是非空、无首尾空白且不超过 214 字符的包名。";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// PlugPals 会读取全部已配置 registry；任一不安全地址都可能被计划错误或 Receipt 携带，
        /// 因此在调用领域 Plan/Execute 前统一拒绝。
        /// </summary>
        private static bool TryValidateConfiguredRegistryUrls(out string error)
        {
            EditorUtil.PlugPals.RegistriesConfig registries = EditorUtil.PlugPals.LoadRegistries();
            if (registries == null ||
                !TryValidateRegistryUrl(registries.externalUrl, out _) ||
                !TryValidateRegistryUrl(registries.internalUrl, out _))
            {
                error = "项目已配置的 registry URL 含认证、query 或 fragment；请先在 PlugPals 配置中移除敏感 URL。";
                return false;
            }

            error = null;
            return true;
        }
    }
}
