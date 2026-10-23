/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyInspectSettingsAction.cs
 * author:    Codex
 * created:   2026/10/13
 * descrip:   Pipify Batch 与参数字段脱敏只读检查
 ***************************************************************/

using System;
using System.Linq;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using Editing = NovaFramework.Editor.EditorUtil.Pipify.ConfigurationEditing;

namespace NovaFramework.Editor
{
    [AgentAction("nova.project.pipify.inspect-settings", "检查 Pipify 配置", "pipify", AgentActionOperationType.Inspect,
        Description = "只读检查现存 Batch、Steps 与真实基础字段 Schema；凭据仅显示 hasValue，不运行或迁移参数。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.UnityRead, RequiredEvidence = AgentActionEvidence.Static,
        Idempotency = AgentActionIdempotency.ReadOnly, RequiresEditMode = true,
        Locks = new[] { "unity-editor", "pipify-settings-assets" })]
    internal sealed class PipifyInspectSettingsAction : AgentActionHandler<PipifyInspectSettingsAction.Request>
    {
        /// <summary>显式 GUID 资产；batchName 可选筛选，stepId 可选查询已注册新 Step 的字段声明。</summary>
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string settingsGuid;
            public string batchName;
            public string stepId;
        }

        private sealed class State { internal Request Request; }

        /// <summary>拒绝空筛选；无默认资产选择或任意类型名。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (request == null || request.settingsGuid == null || request.settingsGuid.Length != 32 || !request.settingsGuid.All(Uri.IsHexDigit) ||
                request.batchName != null && string.IsNullOrWhiteSpace(request.batchName) || request.stepId != null && string.IsNullOrWhiteSpace(request.stepId))
            { error = "需要有效 settingsGuid；可选 batchName/stepId 不可为空。"; return false; }
            return true;
        }

        /// <summary>生成当前只读脱敏视图；内部保存请求，公开载荷只含资产身份和字段描述。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryValidateRequest(request, out string error)) return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = error });
            try
            {
                object view = BuildView(request);
                return Task.FromResult(new AgentActionHandlerPlan { Status = "ready", Summary = "Pipify Batch 与字段 Schema 已只读检查；凭据已脱敏。",
                    DataJson = Util.Json.Serialize(view), State = new State { Request = Util.Json.Deserialize<Request>(Util.Json.Serialize(request)) },
                    RecoveryPayloadJson = Util.Json.Serialize(request),
                    Evidence = new[] { "直接读取保存参数 token 与真实字段声明；不执行参数构造器、Step、Runner 参数迁移或保存。", "动态下拉仅使用源码固定包名提供者；未知 Package 提供者不反射执行。" } });
            }
            catch { return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "无法安全检查目标 Pipify 资产或所选 Batch/Step。" }); }
        }

        /// <summary>Execute 重新生成当前只读视图，结果不复用旧参数实例。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            return Task.FromResult(state is State typed ? ReadResult(typed.Request) : AgentActionResult.Create(null, "blocked", "Pipify 检查计划无效。"));
        }

        /// <summary>恢复验证仅检查同一资产和过滤条件，不重放任何写操作。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Request request;
            try { request = Util.Json.Deserialize<Request>(receiptJson); }
            catch { return Task.FromResult(AgentActionResult.Create(null, "blocked", "Pipify 检查 Receipt 无法解析。")); }
            return Task.FromResult(TryValidateRequest(request, out string error) ? ReadResult(request) : AgentActionResult.Create(null, "blocked", error));
        }

        /// <summary>不要求清空草稿即可检查，但准确标记是否存在未保存内容。</summary>
        private static object BuildView(Request request)
        {
            if (!PipifyConfigureBatchAction.TryLoad(request.settingsGuid, false, out PipifySettingsSO settings, out string path, out string error)) throw new InvalidOperationException(error);
            Batch[] batches = settings.Batches.Where(batch => batch != null && (request.batchName == null || batch.Name == request.batchName)).ToArray();
            if (request.batchName != null && batches.Length != 1) throw new InvalidOperationException("所选 Batch 缺失或名称重复。");
            PipifyStepInfo schemaInfo = request.stepId == null ? null : EditorUtil.Pipify.Registry.FindById(request.stepId);
            if (request.stepId != null && schemaInfo == null) throw new InvalidOperationException("所选 Step 未注册。");
            return new { request.settingsGuid, assetPath = path, stateSha256 = Editing.StateHash(settings), modelSha256 = Editing.ModelHash(),
                unsavedChanges = EditorUtility.IsDirty(settings) || Editing.HasUnsavedDraft(settings),
                parameterSchema = schemaInfo == null ? null : Editing.InspectStep(schemaInfo),
                batches = batches.Select(batch => new { batchName = batch.Name, stepCount = batch.Items.Count,
                    steps = batch.Items.Select((item, index) => Editing.InspectStep(EditorUtil.Pipify.Registry.FindById(item?.StepId), item, index)).ToArray() }).ToArray() };
        }

        /// <summary>只读结果可重复刷新，凭据原文和保存参数不会进入 Receipt。</summary>
        private static AgentActionResult ReadResult(Request request)
        {
            try
            {
                AgentActionResult result = AgentActionResult.Create(null, "success", "Pipify 配置与参数 Schema 检查完成；未修改资产。" );
                result.DataJson = Util.Json.Serialize(BuildView(request)); result.ReceiptJson = Util.Json.Serialize(request);
                result.EvidenceKinds = AgentActionEvidence.Static; result.Evidence.Add("已读真实注册元数据与保存 token；仅证明当前 Editor 配置可见，未运行 Step。");
                return result;
            }
            catch { return AgentActionResult.Create(null, "blocked", "无法安全检查目标 Pipify 资产或过滤条件。"); }
        }
    }
}
