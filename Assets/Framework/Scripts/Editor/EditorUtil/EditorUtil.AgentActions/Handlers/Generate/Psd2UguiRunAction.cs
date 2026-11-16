/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Psd2UguiRunAction.cs
 * author:    Codex
 * created:   2026/11/10
 * descrip:   PSD 转 UI 的固定操作、一次性计划与只读恢复验证
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Editor
{
    /// <summary>以固定强类型请求驱动可选包，既有授权通过 Core 确认令牌绑定原计划。</summary>
    [AgentAction("nova.project.psd2ugui.run", "执行 PSD 转 UI", "psd2ugui", AgentActionOperationType.Generate,
        Description = "准备、同步和生成 PSD 界面，编辑节点与配置，导出图片或处理九宫格；run-ai 会启动外部 AI 进程并传递 PSD 图片和提示词，须具有针对该操作的明确授权。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.UnityRead | AgentActionEffect.WorkspaceWrite |
            AgentActionEffect.UnityWrite | AgentActionEffect.ExternalWrite,
        RequiredEvidence = AgentActionEvidence.Static | AgentActionEvidence.Artifact,
        Idempotency = AgentActionIdempotency.SubmitOnce, RequiresConfirmation = true, RequiresStableEditor = true, RequiresEditMode = true,
        ReloadSemantics = AgentActionReloadSemantics.PlanInvalidatedVerifyOnly,
        Locks = new[] { "unity-editor", "asset-database", "psd2ugui" }, VerifyLocks = new string[] { })]
    internal sealed class Psd2UguiRunAction : AgentActionHandler<Psd2UguiRunAction.Request>
    {
        internal const string Id = "nova.project.psd2ugui.run";
        private static readonly string[] s_Operations = { "prepare", "sync", "rebind-source", "patch-nodes", "generate", "export-images", "apply-ai", "run-ai", "configure", "nine-slice" };
        private static readonly string[] s_ConfigurationFields =
        {
            "forceUseTMP", "hasForceUseTMP", "defaultTextType", "defaultImageType", "sharedAssetsDirectory", "sharedPrefabsDirectory",
            "nineSliceBorderTolerance", "hasNineSliceBorderTolerance", "aiMode", "copyPromptTemplates",
        };
        private static readonly string[] s_ExtendedConfigurationFields =
        {
            "uiFormTemplateAssetPath", "tmpGradientConversionMode", "readmeDoc", "imageReuseTolerance",
            "hasConvertZh2En", "convertZh2En", "hasShowCliWindow", "showCliWindow",
            "hasUseUIFormOutputDirectory", "useUIFormOutputDirectory", "hasAutoCropMinimalNineSlice", "autoCropMinimalNineSlice",
            "hasCompressImage", "compressImage", "hasRules", "rules",
        };
        private static readonly string[] s_AuthoringFields =
        {
            "uiFormName", "hasDrawLayerRectGizmos", "drawLayerRectGizmos", "hasDrawLayerRectGizmosColor", "drawLayerRectGizmosColor",
            "hasPreferSmallestLayerOnScenePick", "preferSmallestLayerOnScenePick",
        };

        /// <summary>唯一公开请求面；nullable 标量表示未提供，保留显式 false/零与 has 字段的配对语义。</summary>
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string operation;
            public string psdAssetPath;
            public string replacementPsdAssetPath;
            public string configAssetPath;
            public string imagesOutputDirectory;
            public string uiFormOutputDirectory;
            public string aiPromptsDirectory;
            public string spriteAssetPath;
            public Node[] nodes;
            public string jobId;
            public bool? forceUseTMP;
            public bool? hasForceUseTMP;
            public string defaultTextType;
            public string defaultImageType;
            public string sharedAssetsDirectory;
            public string sharedPrefabsDirectory;
            public int? nineSliceBorderTolerance;
            public bool? hasNineSliceBorderTolerance;
            public string aiMode;
            public bool? copyPromptTemplates;
            public string uiFormTemplateAssetPath;
            public string tmpGradientConversionMode;
            public string readmeDoc;
            public string imageReuseTolerance;
            public bool? hasConvertZh2En;
            public bool? convertZh2En;
            public bool? hasShowCliWindow;
            public bool? showCliWindow;
            public bool? hasUseUIFormOutputDirectory;
            public bool? useUIFormOutputDirectory;
            public bool? hasAutoCropMinimalNineSlice;
            public bool? autoCropMinimalNineSlice;
            public bool? hasCompressImage;
            public bool? compressImage;
            public bool? hasRules;
            public Rule[] rules;
            public string uiFormName;
            public bool? hasDrawLayerRectGizmos;
            public bool? drawLayerRectGizmos;
            public bool? hasDrawLayerRectGizmosColor;
            public ColorValue drawLayerRectGizmosColor;
            public bool? hasPreferSmallestLayerOnScenePick;
            public bool? preferSmallestLayerOnScenePick;
        }

        /// <summary>有序替换控件规则；省略成员保留同类型旧值，空字符串或空数组明确清除。</summary>
        [Serializable]
        public sealed class Rule
        {
            [AgentActionRequired] public string UIType;
            public string UITypeDesc;
            public string[] TypeMatches;
            public string UIPrefabAssetPath;
            public string UIHelper;
            public string Comment;
        }

        /// <summary>调试颜色必须显式提供四个有限通道，避免缺项被默认为零。</summary>
        [Serializable]
        public sealed class ColorValue
        {
            [AgentActionRequired] public float r;
            [AgentActionRequired] public float g;
            [AgentActionRequired] public float b;
            [AgentActionRequired] public float a;
        }

        /// <summary>仅允许当前控件已公开的槽位；恢复自动绑定与指定同源节点分别表达。</summary>
        [Serializable]
        public sealed class HelperBinding
        {
            [AgentActionRequired] public string slot;
            public string targetNodeId;
            [AgentActionRequired] public bool useAutomatic;
        }

        /// <summary>节点只按 Inspect 给出的持久标识编辑，不接纳对象类型名、任意属性路径或代码。</summary>
        [Serializable]
        public sealed class Node
        {
            [AgentActionRequired] public string id;
            public string name;
            public string uiType;
            public string referenceKind;
            public string referenceKey;
            public bool? markToExport;
            public bool? hasMarkToExport;
            public bool? hasActive;
            public bool? active;
            public bool? hasParent;
            public string parentId;
            public bool? hasSiblingIndex;
            public int? siblingIndex;
            public HelperBinding[] helperBindings;
        }

        /// <summary>可执行计划仅驻内存，保存原身份和内包原计划；重载不会重建或重放它。</summary>
        private sealed class State
        {
            internal string IdentityJson;
            internal string Operation;
            internal string PlanJson;
            internal string ReceiptJson;
            internal int Used;
        }

        /// <summary>限制固定操作、路径和操作相关字段，并要求显式 AI Job 身份与布尔配对。</summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (request == null || !s_Operations.Contains(request.operation, StringComparer.Ordinal))
                error = "operation 必须是 prepare、sync、rebind-source、patch-nodes、generate、export-images、apply-ai、run-ai、configure 或 nine-slice。";
            if (error != null) return false;
            string[] paths = { request.psdAssetPath, request.replacementPsdAssetPath, request.configAssetPath, request.imagesOutputDirectory, request.uiFormOutputDirectory,
                request.aiPromptsDirectory, request.spriteAssetPath, request.sharedAssetsDirectory, request.sharedPrefabsDirectory };
            if (paths.Any(path => !Psd2UguiActionBridge.IsAssetPath(path)))
                error = "所有资产和输出目录都必须是 Assets 下的项目相对路径，不能包含父目录或绝对路径。";
            if (error == null && request.operation != "configure" && request.operation != "nine-slice" && string.IsNullOrEmpty(request.psdAssetPath))
                error = "此操作需要明确 psdAssetPath；先用检查操作发现 PSD。";
            if (error == null && request.operation == "configure" && string.IsNullOrEmpty(request.configAssetPath))
                error = "修改配置需要明确 configAssetPath。";
            if (error == null && request.operation == "nine-slice" && string.IsNullOrEmpty(request.spriteAssetPath))
                error = "九宫格操作需要明确 spriteAssetPath。";
            if (error == null && request.operation == "apply-ai" && !ValidIdentifier(request.jobId))
                error = "应用 AI 结果必须提供检查结果中的准确 jobId，不能混用不同任务结果。";
            if (error == null && request.operation == "rebind-source" && (string.IsNullOrWhiteSpace(request.replacementPsdAssetPath)
                || request.replacementPsdAssetPath == request.psdAssetPath))
                error = "换源需要另一份明确的 Assets PSD/PSB 路径；将为新源建立独立编辑树并保留原树。";
            if (error == null && (!ValidPair(request.forceUseTMP, request.hasForceUseTMP) || !ValidPair(request.nineSliceBorderTolerance, request.hasNineSliceBorderTolerance)))
                error = "配置值必须与对应 has 字段同时提供，且 has 字段为 true；不修改时请同时省略。";
            if (error == null && request.nineSliceBorderTolerance < 0)
                error = "九宫格边缘容差不能小于零。";
            if (error == null && (!ValidPair(request.convertZh2En, request.hasConvertZh2En) || !ValidPair(request.showCliWindow, request.hasShowCliWindow)
                || !ValidPair(request.useUIFormOutputDirectory, request.hasUseUIFormOutputDirectory)
                || !ValidPair(request.autoCropMinimalNineSlice, request.hasAutoCropMinimalNineSlice) || !ValidPair(request.compressImage, request.hasCompressImage)
                || !ValidPair(request.drawLayerRectGizmos, request.hasDrawLayerRectGizmos)
                || !ValidPair(request.preferSmallestLayerOnScenePick, request.hasPreferSmallestLayerOnScenePick)))
                error = "开关值必须与对应 has 字段同时提供，且 has=true；不修改时同时省略。";
            if (error == null && (request.rules != null ? request.hasRules != true : request.hasRules.HasValue))
                error = "规则集合必须与 hasRules=true 同时提供；空数组表示明确清空规则。";
            if (error == null && request.rules != null && (request.rules.Length > 512 || request.rules.Any(rule => rule == null || !ValidIdentifier(rule.UIType)
                || !IsPrefabReferencePath(rule.UIPrefabAssetPath) || rule.TypeMatches != null && rule.TypeMatches.Any(match => match == null))
                || request.rules.Select(rule => rule.UIType).Distinct(StringComparer.Ordinal).Count() != request.rules.Length))
                error = "规则必须使用唯一 UIType，模板只接受 Assets/Packages 中的 Prefab 路径，匹配列表不能包含 null。";
            if (error == null && !IsPrefabReferencePath(request.uiFormTemplateAssetPath))
                error = "界面模板必须是 Assets/Packages 中的 Prefab 路径；空字符串表示清除引用。";
            if (error == null && (request.drawLayerRectGizmosColor != null ? request.hasDrawLayerRectGizmosColor != true : request.hasDrawLayerRectGizmosColor.HasValue))
                error = "调试颜色必须与 hasDrawLayerRectGizmosColor=true 同时提供。";
            if (error == null && request.drawLayerRectGizmosColor != null && new[] { request.drawLayerRectGizmosColor.r, request.drawLayerRectGizmosColor.g,
                request.drawLayerRectGizmosColor.b, request.drawLayerRectGizmosColor.a }.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                error = "调试颜色不能包含非有限数值。";
            var allowed = new HashSet<string>(StringComparer.Ordinal) { "operation", "configAssetPath" };
            if (request.operation != "configure" && request.operation != "nine-slice") allowed.Add("psdAssetPath");
            switch (request.operation)
            {
                case "prepare": allowed.UnionWith(new[] { "imagesOutputDirectory", "uiFormOutputDirectory", "aiPromptsDirectory" }); break;
                case "rebind-source": allowed.Add("replacementPsdAssetPath"); break;
                case "patch-nodes": allowed.Add("nodes"); allowed.UnionWith(s_AuthoringFields); break;
                case "generate": allowed.UnionWith(new[] { "imagesOutputDirectory", "uiFormOutputDirectory" }); break;
                case "export-images": allowed.Add("imagesOutputDirectory"); break;
                case "apply-ai": allowed.Add("jobId"); break;
                case "run-ai": allowed.UnionWith(new[] { "aiPromptsDirectory", "copyPromptTemplates" }); break;
                case "configure":
                    allowed.UnionWith(s_ConfigurationFields);
                    allowed.UnionWith(s_ExtendedConfigurationFields);
                    allowed.UnionWith(new[] { "imagesOutputDirectory", "uiFormOutputDirectory", "aiPromptsDirectory" });
                    break;
                case "nine-slice": allowed.Add("spriteAssetPath"); break;
            }
            JObject input = JObject.Parse(Psd2UguiActionBridge.SerializeRequest(request));
            string irrelevant = input.Properties().Select(item => item.Name).FirstOrDefault(field => !allowed.Contains(field));
            if (error == null && irrelevant != null) error = "当前操作不使用参数 " + irrelevant + "，请移除后重新计划。";
            if (error == null && request.operation == "patch-nodes")
            {
                bool rootChange = s_AuthoringFields.Any(field => input[field] != null);
                if ((request.nodes == null || request.nodes.Length == 0) && !rootChange || request.nodes?.Length > 512)
                    error = "patch-nodes 需要明确的编辑树设置或 1 至 512 个节点。";
                else if (request.nodes != null && (request.nodes.Any(node => node == null || !ValidIdentifier(node.id) || !ValidPair(node.markToExport, node.hasMarkToExport)
                    || !ValidPair(node.active, node.hasActive) || !ValidPair(node.siblingIndex, node.hasSiblingIndex)
                    || (node.parentId != null ? node.hasParent != true : node.hasParent.HasValue) || node.siblingIndex < 0) ||
                    request.nodes.Select(node => node.id).Distinct(StringComparer.Ordinal).Count() != request.nodes.Length))
                    error = "节点必须使用唯一持久 ID；开关、父级和顺序必须与对应 has 字段配对，顺序不能为负数。";
                else if (request.nodes != null && request.nodes.Any(node => node.name == null && node.uiType == null && node.referenceKind == null
                    && node.referenceKey == null && !node.markToExport.HasValue && !node.active.HasValue && node.parentId == null
                    && !node.siblingIndex.HasValue && (node.helperBindings == null || node.helperBindings.Length == 0)))
                    error = "每个节点都需要至少一项明确修改。";
                else if (request.nodes != null && request.nodes.Any(node => !ValidReference(node.referenceKind, node.referenceKey)))
                    error = "引用修改必须同时给出 referenceKind=ref/refp 和非空 referenceKey；清除时 referenceKind 为空字符串，referenceKey 应省略或为空。";
                else if (request.nodes != null && request.nodes.Any(node => node.helperBindings != null && (node.helperBindings.Length > 32
                    || node.helperBindings.Any(binding => binding == null || !ValidIdentifier(binding.slot)
                        || binding.useAutomatic && binding.targetNodeId != null || !binding.useAutomatic && binding.targetNodeId == null)
                    || node.helperBindings.Select(binding => binding.slot).Distinct(StringComparer.Ordinal).Count() != node.helperBindings.Length)))
                    error = "控件槽位必须唯一；手动绑定需明确同源节点 ID（空字符串清空），恢复自动绑定不能附带目标。";
            }
            return error == null;
        }

        /// <summary>冻结原内包计划及恢复指针；只有明确计划态和一致进程声明才能交给 Core 确认。</summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryValidateRequest(request, out string error))
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = error });
            if (!Psd2UguiActionBridge.TryCreate(out Psd2UguiActionBridge bridge, out bool missing, out error))
                return Task.FromResult(new AgentActionHandlerPlan { Status = missing ? "not_applicable" : "blocked", Summary = error });
            try
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (RequiresParameterContract(request))
                {
                    // 能力发现独立于目标是否存在，首次创建项目配置也必须可以使用新增字段。
                    Psd2UguiActionBridge.Response inspection = bridge.Inspect("{}");
                    if (!SupportsParameterContract(inspection))
                        return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "当前 PSD2UGUI 插件尚未提供完整面板参数接口，请升级插件后再修改新增参数。" });
                }
                Psd2UguiActionBridge.Response response = bridge.Plan(Psd2UguiActionBridge.SerializeRequest(request));
                bool ready = response.Success && (response.State == "planned" || response.State == "ready") && !string.IsNullOrWhiteSpace(response.PlanJson);
                if (ready && (string.IsNullOrWhiteSpace(response.ReceiptJson) || response.WriteSet.Length == 0 || response.Evidence.Length == 0))
                    return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "PSD 转 UI 计划缺少恢复回执、明确写入范围或核验证据，未执行。", DataJson = Psd2UguiActionBridge.PublicData(response) });
                if (ready && response.RequiresExternalProcess != (request.operation == "run-ai"))
                    return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "计划的外部进程声明与所选操作不一致，未执行。" });
                string receipt = Psd2UguiActionBridge.CreateReceipt(Id, bridge.IdentityJson, request.operation, response.ReceiptJson);
                string summary = response.Message;
                if (request.operation == "run-ai") summary += " 此操作会启动已配置的外部 AI 进程并传递 PSD 图片和提示词；须确认已有针对该行为的授权。";
                return Task.FromResult(new AgentActionHandlerPlan
                {
                    Status = ready ? "ready" : "blocked", Summary = summary, DataJson = Psd2UguiActionBridge.PublicData(response),
                    State = new State { IdentityJson = bridge.IdentityJson, Operation = request.operation, PlanJson = response.PlanJson, ReceiptJson = receipt },
                    RecoveryPayloadJson = receipt, WriteSet = response.WriteSet, Evidence = response.Evidence,
                });
            }
            catch
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "PSD 转 UI 计划未取得有效结果；没有执行写操作。" });
            }
        }

        /// <summary>在 Core 已消费计划后重核 DLL 身份并调用原内包 Execute 一次；异常保留恢复回执。</summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State frozen)) return Task.FromResult(Failure(null, "blocked", "PSD 转 UI 计划已失效，请重新计划。"));
            if (Interlocked.Exchange(ref frozen.Used, 1) != 0)
                return Task.FromResult(Failure(frozen.ReceiptJson, "blocked", "此 PSD 转 UI 计划已消费，不能再次执行。"));
            bool invoked = false;
            try
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (!Psd2UguiActionBridge.TryCreate(out Psd2UguiActionBridge bridge, out _, out _) || bridge.IdentityJson != frozen.IdentityJson)
                    return Task.FromResult(Failure(frozen.ReceiptJson, "blocked", "PSD2UGUI 包或 DLL 已变化；原计划未执行，请重新计划。"));
                invoked = true;
                Psd2UguiActionBridge.Response response = bridge.Execute(frozen.PlanJson);
                if (!string.IsNullOrEmpty(response.ReceiptJson))
                    frozen.ReceiptJson = Psd2UguiActionBridge.CreateReceipt(Id, frozen.IdentityJson, frozen.Operation, response.ReceiptJson);
                AgentActionResult result = Psd2UguiActionBridge.BuildResult(Id, response, frozen.ReceiptJson, false);
                if (string.IsNullOrEmpty(response.ReceiptJson) && result.Status == "success")
                {
                    result.Status = "partial";
                    result.Warnings.Add("执行结果缺少可恢复回执，不能确认完整完成。");
                }
                return Task.FromResult(result);
            }
            catch
            {
                return Task.FromResult(Failure(frozen.ReceiptJson, invoked ? "partial" : "blocked",
                    invoked ? "PSD 转 UI 执行未取得完整结果，可能已有产物；保留原恢复令牌核验，禁止自动重跑。" : "PSD 转 UI 执行前检查未完成，未调用写入入口。"));
            }
        }

        /// <summary>只将原内包回执交给 Verify；后台 AI 运行期间返回 partial，重载不会重启进程或重放修改。</summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            try
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                Psd2UguiActionBridge.Receipt receipt = Psd2UguiActionBridge.ParseReceipt(receiptJson, Id);
                if (!s_Operations.Contains(receipt.operation, StringComparer.Ordinal) || !string.IsNullOrEmpty(receipt.requestJson))
                    return Task.FromResult(Failure(receiptJson, "blocked", "PSD 转 UI 恢复回执与运行操作不匹配。"));
                if (string.IsNullOrWhiteSpace(receipt.receiptJson))
                    return Task.FromResult(Failure(receiptJson, "partial", "只有原计划恢复信息，尚无内包执行回执；不会重放执行。"));
                if (!Psd2UguiActionBridge.TryCreate(out Psd2UguiActionBridge bridge, out _, out _) || bridge.IdentityJson != receipt.identityJson)
                    return Task.FromResult(Failure(receiptJson, "partial", "PSD2UGUI 包身份已变化，无法用当前包核验旧回执；不会重放执行。"));
                Psd2UguiActionBridge.Response response = bridge.Verify(receipt.receiptJson);
                string updated = string.IsNullOrEmpty(response.ReceiptJson) ? receiptJson :
                    Psd2UguiActionBridge.CreateReceipt(Id, receipt.identityJson, receipt.operation, response.ReceiptJson);
                return Task.FromResult(Psd2UguiActionBridge.BuildResult(Id, response, updated, false));
            }
            catch
            {
                return Task.FromResult(Failure(receiptJson, "partial", "PSD 转 UI 回执核验未完成；原恢复信息保留，未重放执行。"));
            }
        }

        /// <summary>明确提供值时必须同步提供 has=true；省略成对字段表示保持原值。</summary>
        private static bool ValidPair<T>(T? value, bool? hasValue) where T : struct =>
            !value.HasValue && !hasValue.HasValue || value.HasValue && hasValue == true;

        /// <summary>新参数只发给明确声明版本 2 的插件，避免旧协议忽略字段后误报成功。</summary>
        internal static bool RequiresParameterContract(Request request)
        {
            JObject fields = JObject.Parse(Psd2UguiActionBridge.SerializeRequest(request));
            return request.operation == "rebind-source" || s_ExtendedConfigurationFields.Concat(s_AuthoringFields).Any(name => fields[name] != null)
                || request.nodes != null && request.nodes.Any(node => node.hasActive.HasValue || node.hasParent.HasValue || node.hasSiblingIndex.HasValue || node.helperBindings != null);
        }

        /// <summary>能力声明须来自成功的只读检查，缺声明和损坏数据均保持阻断。</summary>
        internal static bool SupportsParameterContract(Psd2UguiActionBridge.Response inspection)
        {
            if (inspection == null || !inspection.Success || inspection.State != "ready" || string.IsNullOrEmpty(inspection.DataJson)) return false;
            try
            {
                JToken version = JObject.Parse(inspection.DataJson)["parameterContractVersion"];
                return version?.Type == JTokenType.Integer && (int)version >= 2;
            }
            catch { return false; }
        }

        /// <summary>资源引用可读当前包模板，写入路径仍由原 Assets 校验器约束。</summary>
        private static bool IsPrefabReferencePath(string path) => string.IsNullOrEmpty(path)
            || (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal))
                && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                && !path.Any(char.IsControl) && !path.Contains("\\") && path.Split('/').All(segment => segment.Length > 0 && segment != "." && segment != "..");

        /// <summary>持久标识只作为准确数据键，拒绝空白、控制字符和异常长值。</summary>
        private static bool ValidIdentifier(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 512 && !value.Any(char.IsControl);

        /// <summary>引用类型与键必须组成完整修改，拒绝被内包忽略的单独键及清除时遗留键。</summary>
        private static bool ValidReference(string kind, string key) => kind == null ? key == null :
            kind == "" ? string.IsNullOrEmpty(key) : (kind == "ref" || kind == "refp") && !string.IsNullOrWhiteSpace(key);

        /// <summary>保留原回执并报告当前失败边界，不推定内包没有发生副作用。</summary>
        private static AgentActionResult Failure(string receipt, string status, string message)
        {
            AgentActionResult result = AgentActionResult.Create(Id, status, message);
            result.ReceiptJson = receipt;
            return result;
        }
    }
}
