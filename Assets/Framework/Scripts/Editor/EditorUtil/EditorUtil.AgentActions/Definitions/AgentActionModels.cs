/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AgentActionModels.cs
 * author:    taoye
 * created:   2026/8/19
 * descrip:   Nova Project C# Action 的公共描述、计划与结果模型
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Action 注册问题；有问题的 Action 不会进入可执行 Registry。
    /// </summary>
    public sealed class AgentActionRegistryIssue
    {
        internal AgentActionRegistryIssue(string code, string message)
        {
            Code = code;
            Message = message;
        }

        public string Code { get; }

        public string Message { get; }
    }

    /// <summary>
    /// 可被 Skill、MCP 或 Editor 工具发现的稳定 Action 描述。
    /// </summary>
    public sealed class AgentActionDescriptor
    {
        /// <summary>
        /// 从已验证注册特性与 Handler 请求契约建立安全描述。
        /// </summary>
        internal AgentActionDescriptor(AgentActionAttribute attribute, Type requestType, string requestSchemaJson)
        {
            Id = attribute.Id;
            DisplayName = attribute.DisplayName;
            Description = attribute.Description;
            Domain = attribute.Domain;
            OperationType = attribute.OperationType;
            Effects = attribute.Effects;
            RequiredEvidence = attribute.RequiredEvidence;
            Idempotency = attribute.Idempotency;
            ContractMajor = attribute.ContractMajor;
            RequiresConfirmation = attribute.RequiresConfirmation;
            RequiresStableEditor = attribute.RequiresStableEditor;
            RequiresEditMode = attribute.RequiresEditMode;
            Availability = AgentActionAvailability.Available;
            ReloadSemantics = attribute.ReloadSemantics;
            Locks = Array.AsReadOnly(attribute.Locks ?? Array.Empty<string>());
            RequestType = requestType;
            RequestSchemaJson = requestSchemaJson;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public string Domain { get; }

        public AgentActionOperationType OperationType { get; }

        public AgentActionEffect Effects { get; }

        public AgentActionEvidence RequiredEvidence { get; }

        public AgentActionIdempotency Idempotency { get; }

        public int ContractMajor { get; }

        public bool RequiresConfirmation { get; }

        public bool RequiresStableEditor { get; }

        /// <summary>
        /// Plan 与 Execute 是否必须在 Edit Mode 中进行。
        /// </summary>
        public bool RequiresEditMode { get; }

        public AgentActionAvailability Availability { get; }

        public AgentActionReloadSemantics ReloadSemantics { get; }

        public IReadOnlyList<string> Locks { get; }

        public string RequestSchemaJson { get; }

        internal Type RequestType { get; }
    }

    /// <summary>
    /// Plan 阶段返回给调用方的结构化计划。
    /// ready 计划才拥有一次性 PlanId。
    /// </summary>
    public sealed class AgentActionPlan
    {
        internal AgentActionPlan()
        {
        }

        public string PlanId { get; internal set; }

        public string ActionId { get; internal set; }

        public string Status { get; internal set; }

        public string Summary { get; internal set; }

        public DateTime? ExpiresAtUtc { get; internal set; }

        public string DataJson { get; internal set; }

        /// <summary>
        /// 本次计划对应的持久化 Operation 标识；不用于恢复或重放 Execute。
        /// </summary>
        public string OperationId { get; internal set; }

        /// <summary>
        /// domain reload 或调用断线后只读恢复 Verify 的不透明令牌。
        /// </summary>
        public string RecoveryToken { get; internal set; }

        public List<string> WriteSet { get; internal set; } = new List<string>();

        public List<string> Evidence { get; internal set; } = new List<string>();
    }

    /// <summary>
    /// Execute 或 Verify 的统一结果。
    /// </summary>
    public sealed class AgentActionResult
    {
        internal AgentActionResult()
        {
        }

        public string ActionId { get; internal set; }

        public string Status { get; internal set; }

        public string Message { get; internal set; }

        public string DataJson { get; internal set; }

        public string ReceiptJson { get; internal set; }

        public string RecoveryToken { get; internal set; }

        public AgentActionEvidence EvidenceKinds { get; internal set; }

        public List<string> Artifacts { get; internal set; } = new List<string>();

        public List<string> Evidence { get; internal set; } = new List<string>();

        public List<string> Warnings { get; internal set; } = new List<string>();

        internal static AgentActionResult Create(string actionId, string status, string message)
        {
            return new AgentActionResult
            {
                ActionId = actionId,
                Status = status,
                Message = message,
            };
        }
    }

    /// <summary>
    /// 可跨 domain reload 传递的 Receipt 信封，避免旧契约 Receipt 被新 Handler 静默解释。
    /// </summary>
    public sealed class AgentActionReceiptEnvelope
    {
        public string ActionId { get; set; }

        public int ContractMajor { get; set; }

        public string PayloadJson { get; set; }
    }
}
