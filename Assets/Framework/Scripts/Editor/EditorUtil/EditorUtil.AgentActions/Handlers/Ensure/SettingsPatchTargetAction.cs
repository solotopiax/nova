/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SettingsPatchTargetAction.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   项目自有对象精确序列化字段保存 Action
 ***************************************************************/

using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NovaFramework.Editor
{
    [AgentAction("nova.project.settings.patch-target", "修改对象字段", "settings", AgentActionOperationType.Ensure,
        Description = "按检查返回的真实 propertyPath 与严格 JSON 修改项目自有普通 SO 或 Prefab 组件；只保存目标文件，不支持场景或专用配置旁路。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.WorkspaceWrite | AgentActionEffect.UnityRead |
            AgentActionEffect.UnityWrite | AgentActionEffect.Credential,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.EnsureState, RequiresConfirmation = true, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "serialized-target-assets" })]
    internal sealed class SettingsPatchTargetAction : AgentActionHandler<SettingsPatchTargetAction.Request>
    {
        /// <summary>单一现有资产和 1 至 64 个字段修改；不能传任意类型、方法、场景或对象创建参数。</summary>
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string targetGuid;
            public string globalObjectId;
            [AgentActionRequired] public FieldPatch[] patches;
        }

        /// <summary>valueJson 是一个 JSON 值的文本载体；真实 Unity 字段类型决定其合法类型。</summary>
        [Serializable]
        public sealed class FieldPatch
        {
            [AgentActionRequired] public string propertyPath;
            [AgentActionRequired] public string valueJson;
        }

        /// <summary>先校验目标与非重叠路径和 JSON 语法，真实可写类型在共享预演中强校验。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!SerializedTargetEditing.ValidateTarget(ToTarget(request), out error)) return false;
            return SerializedTargetEditing.ValidateEdits(ToEdits(request), out error);
        }

        /// <summary>预演只修改 SerializedObject 待应用缓存然后丢弃；计划公开精确路径与整体指纹，不公开字段值。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            try
            {
                SerializedTargetEditing.Plan plan = SerializedTargetEditing.BuildPlan(ToTarget(request), ToEdits(request), context.CancellationToken);
                SerializedTargetEditing.Receipt receipt = plan.Receipt;
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "ready", Summary = receipt.changes.Length == 0 ? "字段已满足，执行将不保存资产。" :
                        $"将修改 {receipt.changes.Length} 个精确字段，只保存 {receipt.assetPath}。",
                    DataJson = JsonConvert.SerializeObject(receipt), State = plan,
                    RecoveryPayloadJson = JsonConvert.SerializeObject(receipt),
                    WriteSet = receipt.changes.Length == 0 ? Array.Empty<string>() : new[] { receipt.assetPath },
                    Evidence = new[]
                    {
                        "冻结文件 GUID、GlobalObjectId、真实脚本、meta、整体序列化状态和字段写集；无组件 Instantiate、Apply 或保存。",
                        "Execute 再次复核漂移和草稿；只保存目标 Prefab 或单 SO；运行与场景行为另行验证。",
                    },
                });
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary =
                "目标、编辑期回调、草稿、字段类型或保存范围不满足通用编辑契约，未修改资产。" }); }
        }

        /// <summary>委托共享服务单次消费并保存已确认计划；写入开始后的失败返回 partial，禁止自动重放。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context) =>
            Task.FromResult(SerializedTargetEditing.Execute(state as SerializedTargetEditing.Plan, context.CancellationToken));

        /// <summary>只读核对 Receipt 整体状态和保存文件，不解析请求值、不再次赋值或保存。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context) =>
            Task.FromResult(SerializedTargetEditing.Verify(receiptJson, context.CancellationToken));

        /// <summary>冻结目标文件及对象标识，由服务核验真实项目自有 MonoScript。</summary>
        private static SerializedTargetEditing.Target ToTarget(Request request) => request == null ? null :
            new SerializedTargetEditing.Target { targetGuid = request.targetGuid, globalObjectId = request.globalObjectId };

        /// <summary>逐项保持原 JSON 文本及精确路径，省略修改不产生默认写入。</summary>
        private static SerializedTargetEditing.Edit[] ToEdits(Request request) => request?.patches?.Select(patch => patch == null ? null :
            new SerializedTargetEditing.Edit { propertyPath = patch.propertyPath, valueJson = patch.valueJson }).ToArray();
    }
}
