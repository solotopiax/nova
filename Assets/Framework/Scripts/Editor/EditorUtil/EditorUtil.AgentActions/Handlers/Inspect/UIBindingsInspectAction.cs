/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIBindingsInspectAction.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   普通 Prefab 的 UIView 控件及精确字段只读检查
 ***************************************************************/

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NovaFramework.Editor
{
    /// <summary>发现现有对象身份与直接序列化字段，不创建布局、添加组件或绑定事件。</summary>
    [AgentAction("nova.project.ui.inspect-bindings", "检查界面控件绑定", "ui", AgentActionOperationType.Inspect,
        Description = "只读检查已有普通 Prefab 的 UIView、直接对象引用字段与控件精确身份，供后续选择字段和对象。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.UnityRead,
        RequiredEvidence = AgentActionEvidence.Static, Idempotency = AgentActionIdempotency.ReadOnly,
        RequiresEditMode = true, Locks = new[] { "unity-editor", "asset-database", "ui-bindings" })]
    internal sealed class UIBindingsInspectAction : AgentActionHandler<UIBindingsInspectAction.Request>
    {
        internal const string Id = "nova.project.ui.inspect-bindings";

        /// <summary>只接受一个已有项目 Prefab 路径，实际资产归属与结构由领域 API 检查。</summary>
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string prefabPath;
        }

        /// <summary>跨重载保留原路径及 GUID；只允许继续读取同一资产，不恢复任何执行计划。</summary>
        [Serializable]
        internal sealed class Receipt
        {
            public string prefabPath;
            public string prefabGuid;
        }

        /// <summary>先拒绝非 Assets 相对路径和导航路径，避免任意文件进入领域加载入口。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = request != null && UIBindingsActionCommon.IsPrefabPath(request.prefabPath)
                ? null : "prefabPath 必须是 Assets 下已有普通 Prefab 的准确相对路径。";
            return error == null;
        }

        /// <summary>调用领域 Inspect 读取当前对象与字段，计划只冻结该 Prefab 的原资产身份。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            try
            {
                if (!TryValidateRequest(request, out string error))
                    return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = error });
                context.CancellationToken.ThrowIfCancellationRequested();
                EditorUtil.UIBindings.Inspection inspection = EditorUtil.UIBindings.Inspect(request.prefabPath);
                context.CancellationToken.ThrowIfCancellationRequested();
                var receipt = new Receipt { prefabPath = inspection.PrefabPath, prefabGuid = inspection.PrefabGuid };
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = "ready", Summary = "已读取当前界面控件、UIView 和可绑定字段；没有修改资产。",
                    DataJson = JsonConvert.SerializeObject(inspection), State = receipt,
                    RecoveryPayloadJson = JsonConvert.SerializeObject(receipt),
                    Evidence = new[] { "对象标识、准确类型和当前绑定来自实际 Prefab；不证明页面布局、事件或游戏运行已验收。" },
                });
            }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "无法检查该普通 Prefab，请核对项目归属、缺失脚本与未保存编辑。" }); }
        }

        /// <summary>再次读取同一 GUID 的当前状态，不把 Plan 时的检查结果当作最新结果。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context) =>
            Task.FromResult(Read(state as Receipt, context));

        /// <summary>跨重载只解析原资产身份并只读检查，不新增对象、不保存或重放绑定。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try { return Task.FromResult(Read(UIBindingsActionCommon.ParseReceipt<Receipt>(receiptJson), context)); }
            catch (OperationCanceledException) { throw; }
            catch { return Task.FromResult(AgentActionResult.Create(Id, "blocked", "界面检查回执无效，请重新检查。")); }
        }

        /// <summary>只读取得现有领域快照；原路径被替换时拒绝读取为原目标，异常不回显业务内容。</summary>
        private static AgentActionResult Read(Receipt receipt, AgentActionExecutionContext context)
        {
            try
            {
                if (receipt == null || !UIBindingsActionCommon.IsPrefabPath(receipt.prefabPath) ||
                    !GenerateActionCommon.TryValidateGuid(receipt.prefabGuid, out _)) throw new InvalidOperationException();
                context.CancellationToken.ThrowIfCancellationRequested();
                EditorUtil.UIBindings.Inspection inspection = EditorUtil.UIBindings.Inspect(receipt.prefabPath);
                context.CancellationToken.ThrowIfCancellationRequested();
                if (inspection.PrefabGuid != receipt.prefabGuid) throw new InvalidOperationException();
                AgentActionResult result = AgentActionResult.Create(Id, "success", "已只读检查当前界面控件与字段；没有修改资产。");
                result.DataJson = JsonConvert.SerializeObject(inspection);
                result.ReceiptJson = JsonConvert.SerializeObject(receipt);
                result.EvidenceKinds = AgentActionEvidence.Static;
                result.Evidence.Add("精确对象身份、类型、字段与当前引用来自同一实际 Prefab；不证明运行效果。");
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch { return AgentActionResult.Create(Id, "blocked", "原 Prefab 身份或当前只读检查条件已变化，请重新检查。"); }
        }
    }
}
