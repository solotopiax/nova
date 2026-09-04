/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaProjectActionProvider.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   Nova Project Action 的传输中立 Provider 契约
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NovaFramework.Mcp.Editor
{
    /// <summary>
    /// Nova MCP 中立网关响应；第三方 Provider Adapter 只负责将其序列化为自身协议。
    /// </summary>
    public sealed class NovaProjectActionGatewayResponse
    {
        public bool success;
        public string code;
        public string message;
        public object data;
    }

    /// <summary>
    /// Action 可能产生的副作用；传输适配器使用该中性标志执行独立安全审查。
    /// </summary>
    [Flags]
    public enum NovaProjectActionEffect
    {
        None = 0,
        WorkspaceRead = 1 << 0,
        WorkspaceWrite = 1 << 1,
        UnityRead = 1 << 2,
        UnityWrite = 1 << 3,
        ExternalRead = 1 << 4,
        ExternalWrite = 1 << 5,
        BuildArtifact = 1 << 6,
        Destructive = 1 << 7,
        Credential = 1 << 8,
    }

    /// <summary>
    /// Provider 注册或 Action 发现过程中形成的稳定问题。
    /// </summary>
    public sealed class NovaProjectActionProviderIssue
    {
        public string Code { get; set; }

        public string Message { get; set; }
    }

    /// <summary>
    /// Nova MCP 当前 Action 开放策略的只读快照。
    /// 只用于能力发现与诊断，不提供执行入口。
    /// </summary>
    public sealed class NovaProjectActionExposureSnapshot
    {
        public bool IsAvailable { get; set; }

        public string[] PolicyActionIds { get; set; } = Array.Empty<string>();

        public string[] ExposedActionIds { get; set; } = Array.Empty<string>();

        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 不包含 Framework CLR 类型信息的 Action 描述。
    /// </summary>
    public sealed class NovaProjectActionDescriptor
    {
        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public string Domain { get; set; }

        public string OperationType { get; set; }

        public NovaProjectActionEffect Effects { get; set; }

        public string[] RequiredEvidence { get; set; } = Array.Empty<string>();

        public string Idempotency { get; set; }

        public int ContractMajor { get; set; }

        public bool RequiresConfirmation { get; set; }

        public bool RequiresStableEditor { get; set; }

        /// <summary>
        /// Action 的 Plan 与 Execute 是否只允许在 Edit Mode 中进行。
        /// </summary>
        public bool RequiresEditMode { get; set; }

        public bool IsAvailable { get; set; }

        public string[] Locks { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Verify 阶段使用的只读资源锁；可与 Plan、Execute 的锁不同。
        /// </summary>
        public string[] VerifyLocks { get; set; } = Array.Empty<string>();

        public string RequestSchemaJson { get; set; }
    }

    /// <summary>
    /// Provider 冻结的一次性执行计划。
    /// </summary>
    public sealed class NovaProjectActionPlan
    {
        public string PlanId { get; set; }

        public string ActionId { get; set; }

        public string Status { get; set; }

        public string Summary { get; set; }

        public DateTime? ExpiresAtUtc { get; set; }

        public string DataJson { get; set; }

        public string OperationId { get; set; }

        public string RecoveryToken { get; set; }

        public string[] WriteSet { get; set; } = Array.Empty<string>();

        public string[] Evidence { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Provider 执行或只读核验的中性结果。
    /// </summary>
    public sealed class NovaProjectActionResult
    {
        public string ActionId { get; set; }

        public string Status { get; set; }

        public string Message { get; set; }

        public string DataJson { get; set; }

        public string RecoveryToken { get; set; }

        public string[] EvidenceKinds { get; set; } = Array.Empty<string>();

        public string[] Artifacts { get; set; } = Array.Empty<string>();

        public string[] Evidence { get; set; } = Array.Empty<string>();

        public string[] Warnings { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// 传输适配器可调用的最小 Action Provider；不接受类型名、方法名、反射参数或代码字符串。
    /// </summary>
    public interface INovaProjectActionProvider
    {
        IReadOnlyList<NovaProjectActionProviderIssue> GetIssues();

        /// <summary>
        /// 返回当前 Registry 中全部已注册 Action 的描述快照。
        /// Gateway 使用它校验显式白名单没有遗漏任何项目组 Action。
        /// </summary>
        IReadOnlyList<NovaProjectActionDescriptor> GetAll();

        NovaProjectActionDescriptor Describe(string actionId);

        Task<NovaProjectActionPlan> PlanAsync(
            string actionId,
            string requestJson,
            CancellationToken cancellationToken);

        Task<NovaProjectActionResult> ExecuteAsync(
            string actionId,
            string planId,
            string confirmationToken,
            CancellationToken cancellationToken);

        Task<NovaProjectActionResult> VerifyAsync(
            string actionId,
            string receipt,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// 当前 AppDomain 唯一的 Nova Project Action Provider 注册点。
    /// </summary>
    public static class NovaProjectActionProviderRegistry
    {
        private const string FrameworkAssemblyName = "NovaFramework.Editor";
        private const string FrameworkProviderTypeName = "NovaFramework.Editor.NovaProjectActionContractProvider";
        private static readonly object s_Gate = new object();
        private static INovaProjectActionProvider s_Provider;

        /// <summary>
        /// 注册唯一 Provider；同一实例重复注册保持幂等，不允许静默替换实现。
        /// </summary>
        /// <param name="provider">Framework 提供的受控 Action Provider。</param>
        public static void Register(INovaProjectActionProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            Type providerType = provider.GetType();
            if (!string.Equals(providerType.Assembly.GetName().Name, FrameworkAssemblyName, StringComparison.Ordinal) ||
                !string.Equals(providerType.FullName, FrameworkProviderTypeName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("只允许 NovaFramework.Editor 注册内置 Project Action Provider。");
            }

            lock (s_Gate)
            {
                if (s_Provider != null && !ReferenceEquals(s_Provider, provider))
                {
                    throw new InvalidOperationException("Nova Project Action Provider 已注册，不能被静默替换。");
                }

                s_Provider = provider;
            }
        }

        /// <summary>
        /// 尝试获取当前已注册 Provider。
        /// </summary>
        /// <param name="provider">成功时返回唯一 Provider。</param>
        /// <returns>Provider 已注册时返回 true。</returns>
        public static bool TryGet(out INovaProjectActionProvider provider)
        {
            lock (s_Gate)
            {
                provider = s_Provider;
                return provider != null;
            }
        }
    }
}
