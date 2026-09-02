/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigValidateCoordinateAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   指定 ConfigMaster 三维坐标的只读校验 Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.config.validate-coordinate",
        "校验 Config 坐标",
        "config",
        AgentActionOperationType.Inspect,
        Description = "只读校验 ConfigMaster 的 Platform、Channel、DevelopMode 坐标及运行时导出目标。",
        Effects = AgentActionEffect.WorkspaceRead | AgentActionEffect.UnityRead,
        RequiredEvidence = AgentActionEvidence.Static,
        Idempotency = AgentActionIdempotency.ReadOnly,
        ReloadSemantics = AgentActionReloadSemantics.ReloadNotExpected,
        Locks = new[] { "unity-editor", "configmaster-assets" })]
    internal sealed class ConfigValidateCoordinateAction : AgentActionHandler<ConfigValidateCoordinateAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string masterGuid;
            [AgentActionRequired] public string platform;
            [AgentActionRequired] public string channel;
            [AgentActionRequired] public string developMode;
        }

        [Serializable]
        private sealed class IssueView
        {
            public string path;
            public string message;
            public string severity;
        }

        [Serializable]
        private sealed class ResultView
        {
            public string masterGuid;
            public string assetPath;
            public string platform;
            public string channel;
            public string developMode;
            public bool valid;
            public int errorCount;
            public int warningCount;
            public IssueView[] issues;
        }

        private sealed class State
        {
            public string PayloadJson;
        }

        /// <summary>
        /// 校验 GUID 与三维枚举均为明确有效值，拒绝 None 和大小写漂移。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            if (!TryParseRequestValues(request, out _, out _, out _, out error)) return false;
            return true;
        }

        /// <summary>
        /// 只读生成当前坐标校验快照；结构不完整时不调用带补齐式 getter 的领域 Validator。
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
                Summary = view.valid
                    ? $"Config 坐标 {request.platform}/{request.channel}/{request.developMode} 校验通过。"
                    : $"Config 坐标发现 {view.errorCount} 个错误、{view.warningCount} 个警告。",
                DataJson = Util.Json.Serialize(view),
                State = new State { PayloadJson = payload },
                RecoveryPayloadJson = payload,
                Evidence = new[] { "只读解析 ConfigMaster，并按指定三维坐标执行结构预检与 Config.Validator 校验。" },
            });
        }

        /// <summary>
        /// 返回 Plan 冻结请求对应的当前只读结果，并生成可恢复 Verify 的 Receipt。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State typed) || string.IsNullOrWhiteSpace(typed.PayloadJson))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Config 校验计划状态无效。"));
            }
            return VerifyPayloadAsync(typed.PayloadJson);
        }

        /// <summary>
        /// 重新按 Receipt 中的 GUID 加载资产并只读复核当前坐标。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            return VerifyPayloadAsync(receiptJson);
        }

        /// <summary>
        /// 解析 Receipt 请求并构建最新校验结果。
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
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Config 校验 Receipt 无法解析：" + exception.Message));
            }

            if (!TryBuildView(request, out ResultView view, out string error))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", error));
            }

            AgentActionResult result = AgentActionResult.Create(null, "success", view.valid
                ? "指定 Config 坐标校验通过。"
                : $"校验已完成：{view.errorCount} 个错误、{view.warningCount} 个警告。");
            result.DataJson = Util.Json.Serialize(view);
            result.ReceiptJson = payloadJson;
            result.EvidenceKinds = AgentActionEvidence.Static;
            result.Evidence.Add("已重新按 GUID 加载 ConfigMaster，并只读复核指定坐标。校验完成不代表业务值适用于服务器或真机环境。");
            return Task.FromResult(result);
        }

        /// <summary>
        /// 解析请求、定位资产并构建不会修改 ConfigMaster 的校验视图。
        /// </summary>
        private static bool TryBuildView(Request request, out ResultView view, out string error)
        {
            view = null;
            if (!TryParseRequestValues(request, out PlatformType platform, out ChannelType channel, out DevelopMode mode, out error))
            {
                return false;
            }
            if (!TryLoadMaster(request.masterGuid, out ConfigMasterSO master, out string assetPath, out error))
            {
                return false;
            }

            var issues = new List<IssueView>();
            if (!TryGetCompleteCoordinate(master, platform, channel, mode, out string structureError))
            {
                issues.Add(new IssueView
                {
                    path = $"Entries[{platform}/{channel}/{mode}]",
                    message = structureError,
                    severity = EditorUtil.Config.Validator.Severity.Error.ToString(),
                });
            }
            else
            {
                issues.AddRange(EditorUtil.Config.Validator.Validate(master, platform, channel, mode)
                    .Select(issue => new IssueView
                    {
                        path = issue.Path,
                        message = issue.Message,
                        severity = issue.Level.ToString(),
                    }));
            }

            int errors = issues.Count(issue => issue.severity == EditorUtil.Config.Validator.Severity.Error.ToString());
            int warnings = issues.Count - errors;
            view = new ResultView
            {
                masterGuid = request.masterGuid,
                assetPath = assetPath,
                platform = platform.ToString(),
                channel = channel.ToString(),
                developMode = mode.ToString(),
                valid = errors == 0,
                errorCount = errors,
                warningCount = warnings,
                issues = issues.ToArray(),
            };
            return true;
        }

        /// <summary>
        /// 确认目标格的四类 mode 包装项均已存在，避免只读校验通过 getter 隐式补结构。
        /// </summary>
        private static bool TryGetCompleteCoordinate(
            ConfigMasterSO master,
            PlatformType platform,
            ChannelType channel,
            DevelopMode mode,
            out string error)
        {
            error = null;
            if (!master.TryGetEntry(platform, channel, out PlatformChannelEntry entry))
            {
                error = "未找到对应 Platform×Channel 行；只读 Action 不会自动补齐。";
                return false;
            }
            DevelopModeSDKEntry sdkSlot = entry.SDKConfigsByMode?.FirstOrDefault(item => item != null && item.Mode == mode);
            DevelopModeKitEntry kitSlot = entry.KitConfigsByMode?.FirstOrDefault(item => item != null && item.Mode == mode);
            if (entry.AppConfigsByMode == null || !entry.AppConfigsByMode.Any(item => item != null && item.Mode == mode) ||
                entry.PrivacyConfigsByMode == null || !entry.PrivacyConfigsByMode.Any(item => item != null && item.Mode == mode) ||
                sdkSlot?.SDKConfigs == null || kitSlot?.KitConfigs == null)
            {
                error = "目标坐标的 App/Privacy/SDK/Kit mode 包装项不完整；只读 Action 不会自动补齐。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 严格解析请求中的三维枚举值。
        /// </summary>
        private static bool TryParseRequestValues(
            Request request,
            out PlatformType platform,
            out ChannelType channel,
            out DevelopMode mode,
            out string error)
        {
            platform = default;
            channel = default;
            mode = default;
            error = null;
            if (request == null || !IsGuid(request.masterGuid))
            {
                error = "masterGuid 必须是 32 位十六进制 Unity GUID。";
                return false;
            }
            if (!Enum.TryParse(request.platform, false, out platform) || !Enum.IsDefined(typeof(PlatformType), platform) ||
                platform == PlatformType.None || request.platform != platform.ToString())
            {
                error = "platform 必须是有效且非 None 的 PlatformType 名称。";
                return false;
            }
            if (!Enum.TryParse(request.channel, false, out channel) || !Enum.IsDefined(typeof(ChannelType), channel) ||
                request.channel != channel.ToString())
            {
                error = "channel 必须是有效的 ChannelType 名称。";
                return false;
            }
            if (!Enum.TryParse(request.developMode, false, out mode) || !Enum.IsDefined(typeof(DevelopMode), mode) ||
                request.developMode != mode.ToString())
            {
                error = "developMode 必须是有效的 DevelopMode 名称。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 按 Unity GUID 精确加载 ConfigMasterSO，禁止回退为全工程首个资产。
        /// </summary>
        private static bool TryLoadMaster(string guid, out ConfigMasterSO master, out string assetPath, out string error)
        {
            assetPath = AssetDatabase.GUIDToAssetPath(guid);
            master = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(assetPath);
            error = master == null ? "masterGuid 未指向可加载的 ConfigMasterSO。" : null;
            return master != null;
        }

        /// <summary>
        /// 判断字符串是否为 Unity 资产使用的 32 位十六进制 GUID。
        /// </summary>
        private static bool IsGuid(string value)
        {
            return value != null && value.Length == 32 && value.All(Uri.IsHexDigit);
        }
    }
}
