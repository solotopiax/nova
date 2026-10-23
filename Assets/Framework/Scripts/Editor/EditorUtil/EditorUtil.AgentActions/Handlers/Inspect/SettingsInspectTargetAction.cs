/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SettingsInspectTargetAction.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   项目自有序列化对象的只读字段检查 Action
 ***************************************************************/

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NovaFramework.Editor
{
    [AgentAction("nova.project.settings.inspect-target", "检查对象字段", "settings", AgentActionOperationType.Inspect,
        Description = "读取项目自有普通 SO 或 Prefab 组件的真实字段路径、类型、tooltip 与当前值；敏感值脱敏，默认值未核实则 unknown。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.UnityRead,
        RequiredEvidence = AgentActionEvidence.Static, Idempotency = AgentActionIdempotency.ReadOnly,
        RequiresEditMode = true, Locks = new[] { "unity-editor", "serialized-target-assets" })]
    internal sealed class SettingsInspectTargetAction : AgentActionHandler<SettingsInspectTargetAction.Request>
    {
        /// <summary>目标文件 GUID 和可选对象标识；Prefab 组件必须提供精确 globalObjectId。</summary>
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string targetGuid;
            public string globalObjectId;
        }

        /// <summary>只读动作保留已规范化的实际目标身份，Execute/Verify 总是重新读取当前状态。</summary>
        private sealed class State
        {
            internal SerializedTargetEditing.Target Target;
        }

        /// <summary>静态校验标识格式，实际项目归属由共享服务在 Plan 加载时检查。</summary>
        protected override bool TryValidateRequest(Request request, out string error) =>
            SerializedTargetEditing.ValidateTarget(ToTarget(request), out error);

        /// <summary>只读描述真实目标并绑定精确对象身份；不创建对象、不调用用户方法、不保存文件。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            try
            {
                SerializedTargetEditing.Inspection view = SerializedTargetEditing.Inspect(ToTarget(request), context.CancellationToken);
                var target = new SerializedTargetEditing.Target { targetGuid = view.targetGuid, globalObjectId = view.globalObjectId };
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "ready", Summary = "已读取真实字段；默认值 unknown，敏感字段只显示是否配置。",
                    DataJson = JsonConvert.SerializeObject(view), State = new State { Target = target },
                    RecoveryPayloadJson = JsonConvert.SerializeObject(target),
                    Evidence = new[] { "字段路径、类型和 tooltip 来自实际 SerializedProperty；检查不证明业务配置完整或运行正常。" },
                });
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "目标不在当前可检查范围或无法读取，请核对实际资产和对象身份。" }); }
        }

        /// <summary>按 Plan 定位结果重新读取当前字段，拒绝把之前计划值当作最新检查结果。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context) =>
            Task.FromResult(state is State frozen ? Read(frozen.Target, context) :
                AgentActionResult.Create(null, "blocked", "字段检查计划状态无效，请重新 Plan。"));

        /// <summary>跨重载仅恢复只读目标身份并重新检查，不创建或恢复任何写入。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try { return Task.FromResult(Read(JsonConvert.DeserializeObject<SerializedTargetEditing.Target>(receiptJson), context)); }
            catch { return Task.FromResult(AgentActionResult.Create(null, "blocked", "字段检查 Receipt 无效。")); }
        }

        /// <summary>构造当前只读结果；值读取失败使用不含配置原值的稳定错误，不回显异常内容。</summary>
        private static AgentActionResult Read(SerializedTargetEditing.Target target, AgentActionExecutionContext context)
        {
            try
            {
                SerializedTargetEditing.Inspection view = SerializedTargetEditing.Inspect(target, context.CancellationToken);
                AgentActionResult result = AgentActionResult.Create(null, "success", "已读取当前目标字段；检查没有写入资产。");
                result.DataJson = JsonConvert.SerializeObject(view);
                result.ReceiptJson = JsonConvert.SerializeObject(target);
                result.EvidenceKinds = AgentActionEvidence.Static;
                result.Evidence.Add("来自当前真实对象的字段、可写边界和敏感值存在性；默认值未核实，未验证游戏运行。");
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch { return AgentActionResult.Create(null, "blocked", "无法只读检查当前目标，请核对资产归属和精确对象身份。"); }
        }

        /// <summary>将外部强类型请求转成共享服务标识，不按用户类型名或方法名寻找对象。</summary>
        private static SerializedTargetEditing.Target ToTarget(Request request) => request == null ? null :
            new SerializedTargetEditing.Target { targetGuid = request.targetGuid, globalObjectId = request.globalObjectId };
    }
}
