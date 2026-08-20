/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AgentActionContracts.cs
 * author:    taoye
 * created:   2026/8/19
 * descrip:   Nova Project C# Action 的分类与注册契约
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Action 的操作性质。领域由 Descriptor.Domain 独立表达，避免分类与业务模块耦合。
    /// </summary>
    public enum AgentActionOperationType
    {
        Inspect,
        Ensure,
        Generate,
        Build,
        Package,
        RuntimeProbe,
        Delivery,
    }

    /// <summary>
    /// Action 可能产生的副作用。多个副作用可以组合。
    /// </summary>
    [Flags]
    public enum AgentActionEffect
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
    /// Action 达到 success 所需的证据集合；多个证据可以组合。
    /// </summary>
    [Flags]
    public enum AgentActionEvidence
    {
        None = 0,
        Static = 1 << 0,
        Compile = 1 << 1,
        Play = 1 << 2,
        Artifact = 1 << 3,
        Device = 1 << 4,
        External = 1 << 5,
        PackageResolution = 1 << 6,
    }

    /// <summary>
    /// Action 的幂等策略。
    /// </summary>
    public enum AgentActionIdempotency
    {
        ReadOnly,
        EnsureState,
        ReplaceGeneratedOutput,
        CreateIfAbsent,
        SubmitOnce,
    }

    /// <summary>
    /// Action 在当前 Registry 快照中的可用状态。
    /// </summary>
    public enum AgentActionAvailability
    {
        Available,
    }

    /// <summary>
    /// Action 与 Unity domain reload 的契约关系。
    /// 可执行计划始终只驻留内存；重载后只允许依据持久化 Operation 进入 Verify。
    /// </summary>
    public enum AgentActionReloadSemantics
    {
        PlanInvalidatedVerifyOnly,
        ReloadNotExpected,
    }

    /// <summary>
    /// 标记请求 DTO 中必须显式提供的字段。
    /// 引用类型默认允许省略；值类型默认必须提供。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    internal sealed class AgentActionRequiredAttribute : Attribute
    {
    }

    /// <summary>
    /// 将框架内的强类型 Handler 注册为受控 Nova Project Action。
    /// 仅扫描 NovaFramework.Editor 程序集，不接受消费项目任意脚本注册。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    internal sealed class AgentActionAttribute : Attribute
    {
        public AgentActionAttribute(
            string id,
            string displayName,
            string domain,
            AgentActionOperationType operationType)
        {
            Id = id;
            DisplayName = displayName;
            Domain = domain;
            OperationType = operationType;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Domain { get; }

        public AgentActionOperationType OperationType { get; }

        public AgentActionEffect Effects { get; set; }

        public AgentActionEvidence RequiredEvidence { get; set; }

        public AgentActionIdempotency Idempotency { get; set; }

        public int ContractMajor { get; set; } = 1;

        public bool RequiresConfirmation { get; set; }

        public bool RequiresStableEditor { get; set; } = true;

        /// <summary>
        /// Action 的 Plan 与 Execute 是否必须在 Edit Mode 中进行。
        /// 该契约与编译/包更新稳定性独立，不限制未来 RuntimeProbe 在 Play Mode 中执行。
        /// </summary>
        public bool RequiresEditMode { get; set; }

        public AgentActionReloadSemantics ReloadSemantics { get; set; } =
            AgentActionReloadSemantics.PlanInvalidatedVerifyOnly;

        public string[] Locks { get; set; } = Array.Empty<string>();
    }
}
