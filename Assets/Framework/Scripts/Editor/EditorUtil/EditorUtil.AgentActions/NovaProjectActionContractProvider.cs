/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaProjectActionContractProvider.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   Framework Agent Action 到传输中立契约的 Provider
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Mcp.Editor;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 将 Framework 内部 Agent Action 映射为传输中立的 Provider 契约。
    /// </summary>
    internal sealed class NovaProjectActionContractProvider : INovaProjectActionProvider
    {
        private static readonly NovaProjectActionContractProvider s_Instance = new NovaProjectActionContractProvider();

        /// <summary>
        /// 每次 domain load 注册唯一 Framework Provider，不通过反射发现消费项目实现。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Register()
        {
            NovaProjectActionProviderRegistry.Register(s_Instance);
        }

        public IReadOnlyList<NovaProjectActionProviderIssue> GetIssues()
        {
            return EditorUtil.AgentActions.Registry.GetIssues()
                .Select(issue => new NovaProjectActionProviderIssue
                {
                    Code = issue.Code,
                    Message = issue.Message,
                })
                .ToArray();
        }

        public NovaProjectActionDescriptor Describe(string actionId)
        {
            return Map(EditorUtil.AgentActions.Registry.Describe(actionId));
        }

        public async Task<NovaProjectActionPlan> PlanAsync(
            string actionId,
            string requestJson,
            CancellationToken cancellationToken)
        {
            AgentActionPlan plan = await EditorUtil.AgentActions.PlanAsync(actionId, requestJson, cancellationToken);
            return new NovaProjectActionPlan
            {
                PlanId = plan.PlanId,
                ActionId = plan.ActionId,
                Status = plan.Status,
                Summary = plan.Summary,
                ExpiresAtUtc = plan.ExpiresAtUtc,
                DataJson = plan.DataJson,
                OperationId = plan.OperationId,
                RecoveryToken = plan.RecoveryToken,
                WriteSet = plan.WriteSet?.ToArray() ?? Array.Empty<string>(),
                Evidence = plan.Evidence?.ToArray() ?? Array.Empty<string>(),
            };
        }

        public async Task<NovaProjectActionResult> ExecuteAsync(
            string actionId,
            string planId,
            string confirmationToken,
            CancellationToken cancellationToken)
        {
            AgentActionResult result = await EditorUtil.AgentActions.ExecuteAsync(
                actionId,
                planId,
                confirmationToken,
                cancellationToken);
            return Map(result);
        }

        public async Task<NovaProjectActionResult> VerifyAsync(
            string actionId,
            string receipt,
            CancellationToken cancellationToken)
        {
            AgentActionResult result = await EditorUtil.AgentActions.VerifyAsync(actionId, receipt, cancellationToken);
            return Map(result);
        }

        private static NovaProjectActionDescriptor Map(AgentActionDescriptor descriptor)
        {
            if (descriptor == null) return null;

            return new NovaProjectActionDescriptor
            {
                Id = descriptor.Id,
                DisplayName = descriptor.DisplayName,
                Domain = descriptor.Domain,
                OperationType = ToKebabCase(descriptor.OperationType.ToString()),
                Effects = (NovaProjectActionEffect)(int)descriptor.Effects,
                RequiredEvidence = ExpandFlags(descriptor.RequiredEvidence),
                Idempotency = ToKebabCase(descriptor.Idempotency.ToString()),
                ContractMajor = descriptor.ContractMajor,
                RequiresConfirmation = descriptor.RequiresConfirmation,
                RequiresStableEditor = descriptor.RequiresStableEditor,
                RequiresEditMode = descriptor.RequiresEditMode,
                IsAvailable = descriptor.Availability == AgentActionAvailability.Available,
                Locks = descriptor.Locks?.ToArray() ?? Array.Empty<string>(),
                RequestSchemaJson = descriptor.RequestSchemaJson,
            };
        }

        private static NovaProjectActionResult Map(AgentActionResult result)
        {
            if (result == null) return null;

            return new NovaProjectActionResult
            {
                ActionId = result.ActionId,
                Status = result.Status,
                Message = result.Message,
                DataJson = result.DataJson,
                RecoveryToken = result.RecoveryToken,
                EvidenceKinds = ExpandFlags(result.EvidenceKinds),
                Artifacts = result.Artifacts?.ToArray() ?? Array.Empty<string>(),
                Evidence = result.Evidence?.ToArray() ?? Array.Empty<string>(),
                Warnings = result.Warnings?.ToArray() ?? Array.Empty<string>(),
            };
        }

        private static string[] ExpandFlags<T>(T value) where T : Enum
        {
            ulong raw = Convert.ToUInt64(value);
            if (raw == 0) return new[] { "none" };

            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .Where(item => Convert.ToUInt64(item) != 0 &&
                               (raw & Convert.ToUInt64(item)) == Convert.ToUInt64(item))
                .Select(item => ToKebabCase(item.ToString()))
                .ToArray();
        }

        private static string ToKebabCase(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var characters = new List<char>(value.Length + 8);
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (index > 0 && char.IsUpper(character)) characters.Add('-');
                characters.Add(char.ToLowerInvariant(character));
            }
            return new string(characters.ToArray());
        }
    }
}
