/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigInspectBundleCollectorAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   ConfigMaster 顶层 BundleCollectorSetting 定位与存在性检查 Action
 ***************************************************************/

using System;
using System.Linq;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using YooAsset.Editor;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.config.inspect-bundle-collector",
        "定位 Bundle Collector",
        "config",
        AgentActionOperationType.Inspect,
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.UnityRead,
        RequiredEvidence = AgentActionEvidence.Static,
        Idempotency = AgentActionIdempotency.ReadOnly,
        ReloadSemantics = AgentActionReloadSemantics.ReloadNotExpected,
        Locks = new[] { "unity-editor", "configmaster-assets", "yooasset-settings" })]
    internal sealed class ConfigInspectBundleCollectorAction : AgentActionHandler<ConfigInspectBundleCollectorAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string masterGuid;
        }

        [Serializable]
        private sealed class ResultView
        {
            public string masterGuid;
            public string masterAssetPath;
            public string configuredPath;
            public bool pathConfigured;
            public bool assetExists;
            public bool collectorLoaded;
            public string assetName;
            public string assetType;
            public string summary;
        }

        private sealed class State
        {
            public string PayloadJson;
        }

        /// <summary>
        /// masterGuid 仅接受 Unity 32 位十六进制 GUID。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (request == null || request.masterGuid == null || request.masterGuid.Length != 32 || !request.masterGuid.All(Uri.IsHexDigit))
            {
                error = "masterGuid 必须是 32 位十六进制 Unity GUID。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 只读定位 ConfigMaster 顶层显式路径，并报告资产存在性与最小身份摘要。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryBuildView(request, out ResultView view, out string error))
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = error });
            }
            string payload = Util.Json.Serialize(request);
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = view.summary,
                DataJson = Util.Json.Serialize(view),
                State = new State { PayloadJson = payload },
                RecoveryPayloadJson = payload,
                Evidence = new[] { "只陈述 ConfigMaster 顶层显式路径、资产存在性和加载身份；未验证 Collector 内容正确性。" },
            });
        }

        /// <summary>
        /// 返回当前 BundleCollectorSetting 定位结果。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State typed) || string.IsNullOrWhiteSpace(typed.PayloadJson))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Bundle Collector 检查计划状态无效。"));
            }
            return VerifyPayloadAsync(typed.PayloadJson);
        }

        /// <summary>
        /// 重新按 GUID 加载 ConfigMaster 与 Collector 资产并复核存在性。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            return VerifyPayloadAsync(receiptJson);
        }

        /// <summary>
        /// 解析 Receipt 并构建最新只读定位结果。
        /// </summary>
        private static Task<AgentActionResult> VerifyPayloadAsync(string payloadJson)
        {
            Request request;
            try
            {
                request = Util.Json.Deserialize<Request>(payloadJson);
            }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Bundle Collector Receipt 无法解析：" + exception.Message));
            }
            if (!TryBuildView(request, out ResultView view, out string error))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", error));
            }

            AgentActionResult result = AgentActionResult.Create(null, "success", view.summary);
            result.DataJson = Util.Json.Serialize(view);
            result.ReceiptJson = payloadJson;
            result.EvidenceKinds = AgentActionEvidence.Static;
            result.Evidence.Add("已重新加载路径对应资产；结果不声明 Bundle、Group、Collector 或地址配置正确。" );
            return Task.FromResult(result);
        }

        /// <summary>
        /// 读取顶层显式路径并复用 YooAssetInjector.LoadBundleCollector 判断目标类型能否加载。
        /// </summary>
        private static bool TryBuildView(Request request, out ResultView view, out string error)
        {
            view = null;
            error = null;
            if (request == null || request.masterGuid == null || request.masterGuid.Length != 32 || !request.masterGuid.All(Uri.IsHexDigit))
            {
                error = "Bundle Collector Receipt 的 masterGuid 无效。";
                return false;
            }
            string masterPath = AssetDatabase.GUIDToAssetPath(request.masterGuid);
            ConfigMasterSO master = string.IsNullOrEmpty(masterPath) ? null : AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(masterPath);
            if (master == null)
            {
                error = "masterGuid 未指向可加载的 ConfigMasterSO。";
                return false;
            }

            string configuredPath = master.YooAssetEditorConfigs?.BundleCollectorSettingPath ?? string.Empty;
            UnityEngine.Object rawAsset = string.IsNullOrEmpty(configuredPath) ? null : AssetDatabase.LoadMainAssetAtPath(configuredPath);
            BundleCollectorSetting collector = EditorUtil.Config.YooAssetInjector.LoadBundleCollector(master);
            string summary;
            if (string.IsNullOrEmpty(configuredPath)) summary = "ConfigMaster 顶层未配置 BundleCollectorSettingPath。";
            else if (rawAsset == null) summary = "已配置 BundleCollectorSettingPath，但路径下不存在可加载资产。";
            else if (collector == null) summary = "路径下存在资产，但未能作为 BundleCollectorSetting 加载。";
            else summary = "已定位并加载 BundleCollectorSetting；尚未校验其内容正确性。";

            view = new ResultView
            {
                masterGuid = request.masterGuid,
                masterAssetPath = masterPath,
                configuredPath = configuredPath,
                pathConfigured = !string.IsNullOrEmpty(configuredPath),
                assetExists = rawAsset != null,
                collectorLoaded = collector != null,
                assetName = rawAsset == null ? null : rawAsset.name,
                assetType = rawAsset == null ? null : rawAsset.GetType().FullName,
                summary = summary,
            };
            return true;
        }
    }
}
