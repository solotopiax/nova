/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaProjectActionGateway.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   Nova Project Action 的 Provider 中立受控网关
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace NovaFramework.Mcp.Editor
{
    /// <summary>
    /// 向独立第三方 Provider Adapter 提供的中立受控网关。
    /// 本类不注册任何特定 Provider Tool，也不包含任意 C# 执行入口。
    /// </summary>
    public static class NovaProjectActionGateway
    {
        private const int ProtocolVersion = 1;
        private const int MaxPayloadBytes = 64 * 1024;
        private const int MaxStringBytes = 16 * 1024;
        private const int MaxIdentifierBytes = 512;

        private static readonly HashSet<string> s_AllowedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "operation",
            "action_id",
            "request",
            "plan_id",
            "confirmation_token",
            "receipt",
        };

        /// <summary>
        /// 网关的唯一显式暴露策略。所有已注册 Project Action 必须逐项列入；
        /// Gateway 仍会校验 Registry、Schema 与全量覆盖，避免注册和开放状态静默漂移。
        /// </summary>
        private static readonly ExposurePolicy[] s_ExposurePolicies =
        {
            new ExposurePolicy("nova.project.upm.manage-latest"),
            new ExposurePolicy("nova.project.upm.uninstall-direct"),
            new ExposurePolicy("nova.project.config.validate-coordinate"),
            new ExposurePolicy("nova.project.config.inspect-plugin-types"),
            new ExposurePolicy("nova.project.config.ensure-plugin-instances"),
            new ExposurePolicy("nova.project.config.inspect-bundle-collector"),
            new ExposurePolicy("nova.project.config.export-runtime"),
            new ExposurePolicy("nova.project.hotfix.refresh-game-dlls"),
            new ExposurePolicy("nova.project.hotfix.generate-artifacts"),
            new ExposurePolicy("nova.project.build.inspect-readiness"),
            new ExposurePolicy("nova.project.bundle.build-asset"),
            new ExposurePolicy("nova.project.bundle.build-raw-file"),
            new ExposurePolicy("nova.project.player.build"),
            new ExposurePolicy("nova.project.android.resolve-dependencies"),
            new ExposurePolicy("nova.project.table.export"),
            new ExposurePolicy("nova.project.network.export"),
            new ExposurePolicy("nova.project.sound.export"),
            new ExposurePolicy("nova.project.vibration.export"),
            new ExposurePolicy("nova.project.localization.export"),
            new ExposurePolicy("nova.project.pipify.run-batch"),
        };

        private static readonly SemaphoreSlim s_Gate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 返回当前 Nova MCP Action 开放策略与实际可用结果的只读快照。
        /// 本入口不会创建 Plan、执行 Action 或修改项目。
        /// </summary>
        public static NovaProjectActionExposureSnapshot GetExposureSnapshot()
        {
            string[] policyActionIds = s_ExposurePolicies
                .Select(item => item.ActionId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            bool available = TryGetExposedDescriptors(
                out _,
                out IReadOnlyDictionary<string, NovaProjectActionDescriptor> exposedActions,
                out string errorMessage);
            return new NovaProjectActionExposureSnapshot
            {
                IsAvailable = available,
                PolicyActionIds = policyActionIds,
                ExposedActionIds = available
                    ? exposedActions.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray()
                    : Array.Empty<string>(),
                ErrorMessage = available ? null : SanitizeText(errorMessage),
            };
        }

        /// <summary>
        /// Domain reload 后由请求入口重新执行同一校验；这里不缓存失败结果，避免 Registry
        /// 重建后仍被过期失败状态永久阻断。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ValidateExposurePolicyAtStartup()
        {
            TryGetExposedDescriptors(out _, out _, out _);
        }

        /// <summary>
        /// 接收 Provider Adapter 转交的原始请求，完成协议校验、暴露策略校验与单请求串行分发。
        /// </summary>
        /// <param name="parameters">中立 JSON 参数对象。</param>
        /// <returns>与具体 Provider 序列化类型无关的结构化响应。</returns>
        public static async Task<NovaProjectActionGatewayResponse> HandleCommand(JObject parameters)
        {
            RequestError requestError = ValidateEnvelope(parameters);
            if (requestError != null)
            {
                return Error(requestError.Code, requestError.Message);
            }

            if (!TryGetExposedDescriptors(
                    out INovaProjectActionProvider provider,
                    out IReadOnlyDictionary<string, NovaProjectActionDescriptor> exposedActions,
                    out string policyError))
            {
                return Error("action_registry_unavailable", policyError);
            }

            if (!await s_Gate.WaitAsync(0))
            {
                return Error("action_busy", "另一个 Nova Project Action 请求正在处理，请稍后重试。");
            }

            try
            {
                string operation = (string)parameters["operation"];
                switch (operation)
                {
                    case "describe":
                        return HandleDescribe(parameters, exposedActions);
                    case "plan":
                        return await HandlePlanAsync(parameters, provider, exposedActions);
                    case "execute":
                        return await HandleExecuteAsync(parameters, provider, exposedActions);
                    case "verify":
                        return await HandleVerifyAsync(parameters, provider, exposedActions);
                    default:
                        return Error("unknown_operation", "operation 只允许 describe、plan、execute 或 verify。");
                }
            }
            catch (Exception)
            {
                return Error("internal_error", "Nova Project Action 桥发生内部错误；未自动重放任何写操作。");
            }
            finally
            {
                s_Gate.Release();
            }
        }

        /// <summary>
        /// 返回全部或指定的已开放 Action 描述及请求 Schema，不触发领域写入。
        /// </summary>
        /// <param name="parameters">已通过外层校验的 MCP 请求参数。</param>
        /// <param name="exposedActions">当前通过暴露策略校验的 Action 映射。</param>
        /// <returns>describe 操作的结构化响应。</returns>
        private static NovaProjectActionGatewayResponse HandleDescribe(
            JObject parameters,
            IReadOnlyDictionary<string, NovaProjectActionDescriptor> exposedActions)
        {
            RequestError error = ValidateOperationFields(
                parameters,
                new[] { "operation" },
                new[] { "action_id" });
            if (error != null)
            {
                return Error(error.Code, error.Message);
            }

            string actionId = ReadOptionalString(parameters, "action_id");
            if (!string.IsNullOrEmpty(actionId))
            {
                if (!TryGetExposedDescriptor(actionId, exposedActions, out NovaProjectActionDescriptor descriptor))
                {
                    return ActionResponse("describe", actionId, "not_applicable", "该 Action 未注册或未向 MCP 开放。", null);
                }

                return ActionResponse("describe", actionId, "success", "已返回 Nova Project Action 描述。", ToDescriptorView(descriptor));
            }

            DescriptorView[] actions = exposedActions.Values
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(ToDescriptorView)
                .ToArray();
            return ActionResponse("describe", null, "success", "已返回 MCP 可调用的 Nova Project Action。", actions);
        }

        /// <summary>
        /// 校验请求并委托 Core 冻结一次性计划，保持领域 Schema 的唯一解析权在 Core。
        /// </summary>
        /// <param name="parameters">已通过外层校验的 MCP 请求参数。</param>
        /// <param name="exposedActions">当前通过暴露策略校验的 Action 映射。</param>
        /// <returns>包含计划信息或拒绝原因的结构化响应。</returns>
        private static async Task<NovaProjectActionGatewayResponse> HandlePlanAsync(
            JObject parameters,
            INovaProjectActionProvider provider,
            IReadOnlyDictionary<string, NovaProjectActionDescriptor> exposedActions)
        {
            RequestError error = ValidateOperationFields(
                parameters,
                new[] { "operation", "action_id", "request" },
                Array.Empty<string>());
            if (error != null)
            {
                return Error(error.Code, error.Message);
            }

            if (IsEditorBusy())
            {
                return Error("editor_busy", "Unity 正在编译或更新包，请稳定后重新计划。");
            }

            string actionId = (string)parameters["action_id"];
            if (!TryGetExposedDescriptor(actionId, exposedActions, out NovaProjectActionDescriptor descriptor))
            {
                return ActionResponse("plan", actionId, "not_applicable", "该 Action 未注册或未向 MCP 开放。", null);
            }

            JObject request = (JObject)parameters["request"];
            error = ValidateActionRequest(descriptor, request);
            if (error != null)
            {
                return Error(error.Code, error.Message);
            }

            NovaProjectActionPlan plan = await provider.PlanAsync(
                actionId,
                request.ToString(Formatting.None),
                CancellationToken.None);
            return ActionResponse("plan", actionId, plan.Status, SanitizeText(plan.Summary), ToPlanView(plan));
        }

        /// <summary>
        /// 消费已冻结计划并委托 Core 执行，返回经传输层裁剪后的执行结果。
        /// </summary>
        /// <param name="parameters">已通过外层校验的 MCP 请求参数。</param>
        /// <param name="exposedActions">当前通过暴露策略校验的 Action 映射。</param>
        /// <returns>包含执行状态、证据与恢复令牌的结构化响应。</returns>
        private static async Task<NovaProjectActionGatewayResponse> HandleExecuteAsync(
            JObject parameters,
            INovaProjectActionProvider provider,
            IReadOnlyDictionary<string, NovaProjectActionDescriptor> exposedActions)
        {
            RequestError error = ValidateOperationFields(
                parameters,
                new[] { "operation", "action_id", "plan_id" },
                new[] { "confirmation_token" });
            if (error != null)
            {
                return Error(error.Code, error.Message);
            }

            if (IsEditorBusy())
            {
                return Error("editor_busy", "Unity 正在编译或更新包；旧计划不可执行，请稳定后重新计划。");
            }

            string actionId = (string)parameters["action_id"];
            if (!TryGetExposedDescriptor(actionId, exposedActions, out _))
            {
                return ActionResponse("execute", actionId, "not_applicable", "该 Action 未注册或未向 MCP 开放。", null);
            }

            string planId = (string)parameters["plan_id"];
            string confirmationToken = ReadOptionalString(parameters, "confirmation_token");
            NovaProjectActionResult result = await provider.ExecuteAsync(
                actionId,
                planId,
                confirmationToken,
                CancellationToken.None);

            ResultView view = ToResultView(result);
            view.authorization_source = "caller_asserted";
            return ActionResponse("execute", result.ActionId ?? actionId, result.Status, SanitizeText(result.Message), view);
        }

        /// <summary>
        /// 使用恢复令牌委托 Core 只读核验领域状态，绝不恢复或重放 Execute。
        /// </summary>
        /// <param name="parameters">已通过外层校验的 MCP 请求参数。</param>
        /// <param name="exposedActions">当前通过暴露策略校验的 Action 映射。</param>
        /// <returns>包含核验状态与证据的结构化响应。</returns>
        private static async Task<NovaProjectActionGatewayResponse> HandleVerifyAsync(
            JObject parameters,
            INovaProjectActionProvider provider,
            IReadOnlyDictionary<string, NovaProjectActionDescriptor> exposedActions)
        {
            RequestError error = ValidateOperationFields(
                parameters,
                new[] { "operation", "action_id", "receipt" },
                Array.Empty<string>());
            if (error != null)
            {
                return Error(error.Code, error.Message);
            }

            if (IsEditorBusy())
            {
                return ActionResponse(
                    "verify",
                    (string)parameters["action_id"],
                    "partial",
                    "Unity 正在编译或更新包，暂不能完成验证。",
                    null);
            }

            string actionId = (string)parameters["action_id"];
            if (!TryGetExposedDescriptor(actionId, exposedActions, out _))
            {
                return ActionResponse("verify", actionId, "not_applicable", "该 Action 未注册或未向 MCP 开放。", null);
            }

            string recoveryToken = (string)parameters["receipt"];
            NovaProjectActionResult result = await provider.VerifyAsync(
                actionId,
                recoveryToken,
                CancellationToken.None);
            return ActionResponse(
                "verify",
                actionId,
                result.Status,
                SanitizeText(result.Message),
                ToResultView(result));
        }

        /// <summary>
        /// 校验 MCP 顶层请求对象、允许字段、总大小和 operation 基础类型。
        /// </summary>
        /// <param name="parameters">待校验的原始请求参数。</param>
        /// <returns>校验失败时的错误对象；成功时为 null。</returns>
        private static RequestError ValidateEnvelope(JObject parameters)
        {
            if (parameters == null)
            {
                return new RequestError("invalid_request", "参数对象不能为空。");
            }

            if (Encoding.UTF8.GetByteCount(parameters.ToString(Formatting.None)) > MaxPayloadBytes)
            {
                return new RequestError("payload_too_large", "MCP 请求不能超过 64 KiB。");
            }

            JProperty unknown = parameters.Properties().FirstOrDefault(property => !s_AllowedFields.Contains(property.Name));
            if (unknown != null)
            {
                return new RequestError("unknown_field", $"不支持参数字段：{unknown.Name}。");
            }

            JToken operation = parameters["operation"];
            if (operation == null || operation.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)operation))
            {
                return new RequestError("invalid_request", "operation 必须是非空字符串。");
            }

            return ValidateStringSize("operation", (string)operation, MaxIdentifierBytes);
        }

        /// <summary>
        /// 按指定 operation 的字段集合校验必填项、可选项、类型和字符串尺寸。
        /// </summary>
        /// <param name="parameters">已通过顶层信封校验的请求参数。</param>
        /// <param name="required">当前操作必须提供的字段名。</param>
        /// <param name="optional">当前操作允许但可省略的字段名。</param>
        /// <returns>校验失败时的错误对象；成功时为 null。</returns>
        private static RequestError ValidateOperationFields(
            JObject parameters,
            IEnumerable<string> required,
            IEnumerable<string> optional)
        {
            var requiredSet = new HashSet<string>(required, StringComparer.Ordinal);
            var allowedSet = new HashSet<string>(requiredSet, StringComparer.Ordinal);
            allowedSet.UnionWith(optional);

            foreach (string field in requiredSet)
            {
                if (parameters[field] == null || parameters[field].Type == JTokenType.Null)
                {
                    return new RequestError("missing_field", $"当前 operation 缺少字段：{field}。");
                }
            }

            JProperty extra = parameters.Properties().FirstOrDefault(property => !allowedSet.Contains(property.Name));
            if (extra != null)
            {
                return new RequestError("unexpected_field", $"当前 operation 不接受字段：{extra.Name}。");
            }

            foreach (JProperty property in parameters.Properties())
            {
                if (property.Name == "request")
                {
                    if (property.Value.Type != JTokenType.Object)
                    {
                        return new RequestError("invalid_type", "request 必须是 JSON object。");
                    }
                    continue;
                }

                if (property.Value.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)property.Value))
                {
                    return new RequestError("invalid_type", $"{property.Name} 必须是非空字符串。");
                }

                int limit = property.Name == "receipt" ? MaxPayloadBytes : MaxIdentifierBytes;
                RequestError sizeError = ValidateStringSize(property.Name, (string)property.Value, limit);
                if (sizeError != null)
                {
                    return sizeError;
                }
            }

            return null;
        }

        /// <summary>
        /// 根据 Descriptor Schema 做传输层顶层保护，字段语义仍由 Core Handler 统一裁决。
        /// </summary>
        /// <param name="descriptor">目标 Action 的已注册描述。</param>
        /// <param name="request">待传递给 Core 的 Action 请求对象。</param>
        /// <returns>校验失败时的错误对象；成功时为 null。</returns>
        private static RequestError ValidateActionRequest(NovaProjectActionDescriptor descriptor, JObject request)
        {
            if (descriptor == null)
            {
                return new RequestError("unknown_action", "该 Action 没有已注册的 MCP 请求契约。");
            }

            if (!TryGetDescriptorSchema(descriptor, out JObject schema, out string schemaError))
            {
                return new RequestError("invalid_action_schema", schemaError);
            }

            JObject properties = schema["properties"] as JObject;
            if (properties == null)
            {
                return new RequestError("invalid_action_schema", "MCP 暴露的 Action 请求 Schema 缺少 properties object。");
            }

            JToken additionalProperties = schema["additionalProperties"];
            if (additionalProperties != null && additionalProperties.Type == JTokenType.Boolean &&
                !additionalProperties.Value<bool>())
            {
                JProperty unknown = request.Properties().FirstOrDefault(property => properties[property.Name] == null);
                if (unknown != null)
                {
                    return new RequestError("unknown_request_field", $"Action 请求不接受字段：{unknown.Name}。");
                }
            }

            // 字段名、必填项、嵌套结构与语义都由 Core Handler 的严格解析器在 PlanAsync 中裁决。
            // MCP 仅根据 Descriptor Schema 做顶层路由保护，并保留与 Action 无关的传输尺寸上限。
            return ValidateRequestTransportLimits(request, "request", 0);
        }

        /// <summary>
        /// 递归限制 Action 请求的嵌套深度和字符串字节数，避免传输层承载异常大输入。
        /// </summary>
        /// <param name="token">当前待校验的 JSON 节点。</param>
        /// <param name="path">用于错误提示的逻辑字段路径。</param>
        /// <param name="depth">当前节点的嵌套深度。</param>
        /// <returns>超出限制时的错误对象；成功时为 null。</returns>
        private static RequestError ValidateRequestTransportLimits(JToken token, string path, int depth)
        {
            if (depth > 64)
            {
                return new RequestError("request_too_deep", "Action 请求嵌套层级超过允许范围。");
            }

            if (token is JValue value && value.Type == JTokenType.String)
            {
                return ValidateStringSize(path, (string)value.Value, MaxStringBytes);
            }

            if (!token.HasValues)
            {
                return null;
            }

            foreach (JToken child in token.Children())
            {
                RequestError error = ValidateRequestTransportLimits(child, path, depth + 1);
                if (error != null)
                {
                    return error;
                }
            }
            return null;
        }

        /// <summary>
        /// 按 UTF-8 字节数校验单个字符串字段，避免字符数与实际传输大小不一致。
        /// </summary>
        /// <param name="field">用于错误提示的字段名。</param>
        /// <param name="value">待校验的字符串值。</param>
        /// <param name="limit">允许的最大 UTF-8 字节数。</param>
        /// <returns>超出限制时的错误对象；成功时为 null。</returns>
        private static RequestError ValidateStringSize(string field, string value, int limit)
        {
            return Encoding.UTF8.GetByteCount(value ?? string.Empty) <= limit
                ? null
                : new RequestError("field_too_large", $"字段 {field} 超过允许长度。");
        }

        /// <summary>
        /// 从已通过策略校验的映射中查找一个可调用 Action 描述。
        /// </summary>
        /// <param name="actionId">请求的 Action ID。</param>
        /// <param name="exposedActions">当前已开放 Action 的映射。</param>
        /// <param name="descriptor">查找成功时返回的 Action 描述。</param>
        /// <returns>仅当 Action ID 非空且存在于开放映射时返回 true。</returns>
        private static bool TryGetExposedDescriptor(
            string actionId,
            IReadOnlyDictionary<string, NovaProjectActionDescriptor> exposedActions,
            out NovaProjectActionDescriptor descriptor)
        {
            descriptor = null;
            if (string.IsNullOrWhiteSpace(actionId) || exposedActions == null)
            {
                return false;
            }

            return exposedActions.TryGetValue(actionId, out descriptor);
        }

        /// <summary>
        /// 验证 Registry、全量显式策略与 Schema，构建当前可向 MCP 开放的 Action 映射。
        /// </summary>
        /// <param name="provider">成功时返回当前唯一 Provider。</param>
        /// <param name="exposedActions">成功时返回按 Action ID 索引的开放映射。</param>
        /// <param name="error">失败时返回 fail-closed 的原因。</param>
        /// <returns>所有策略项均安全且可解析时返回 true。</returns>
        private static bool TryGetExposedDescriptors(
            out INovaProjectActionProvider provider,
            out IReadOnlyDictionary<string, NovaProjectActionDescriptor> exposedActions,
            out string error)
        {
            provider = null;
            exposedActions = null;
            error = null;
            if (!NovaProjectActionProviderRegistry.TryGet(out provider))
            {
                error = "Nova Project Action Provider 尚未注册，MCP 已安全关闭。";
                return false;
            }

            IReadOnlyList<NovaProjectActionProviderIssue> issues = provider.GetIssues();
            if (issues == null || issues.Count != 0)
            {
                error = "Nova Project Action Registry 存在未解决问题，MCP 已安全关闭。";
                return false;
            }

            if (s_ExposurePolicies == null || s_ExposurePolicies.Length == 0)
            {
                error = "Nova Project Action MCP 未配置任何显式暴露策略。";
                return false;
            }

            var result = new Dictionary<string, NovaProjectActionDescriptor>(StringComparer.Ordinal);
            foreach (ExposurePolicy policy in s_ExposurePolicies)
            {
                if (policy == null || string.IsNullOrWhiteSpace(policy.ActionId) || result.ContainsKey(policy.ActionId))
                {
                    error = "Nova Project Action MCP 暴露策略无效或包含重复 Action。";
                    return false;
                }

                NovaProjectActionDescriptor descriptor = provider.Describe(policy.ActionId);
                if (descriptor == null)
                {
                    error = "MCP 暴露策略引用了未注册的 Nova Project Action。";
                    return false;
                }

                if (!descriptor.IsAvailable)
                {
                    error = "MCP 暴露策略包含当前不可用的 Nova Project Action。";
                    return false;
                }

                if (!TryGetDescriptorSchema(descriptor, out _, out error))
                {
                    return false;
                }

                result.Add(policy.ActionId, descriptor);
            }

            IReadOnlyList<NovaProjectActionDescriptor> registered = provider.GetAll();
            if (registered == null || registered.Any(item => item == null || string.IsNullOrWhiteSpace(item.Id)))
            {
                error = "Nova Project Action Registry 返回了无效描述，MCP 已安全关闭。";
                return false;
            }

            string[] registeredIds = registered.Select(item => item.Id)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            string[] policyIds = result.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray();
            if (!registeredIds.SequenceEqual(policyIds, StringComparer.Ordinal))
            {
                error = "Nova Project Action MCP 显式白名单未完整覆盖当前 Registry，MCP 已安全关闭。";
                return false;
            }

            exposedActions = result;
            return true;
        }

        /// <summary>
        /// 解析已注册 Action 的请求 Schema，并限制其必须为 JSON object。
        /// </summary>
        /// <param name="descriptor">包含 RequestSchemaJson 的 Action 描述。</param>
        /// <param name="schema">成功时返回解析后的 Schema 对象。</param>
        /// <param name="error">失败时返回缺失或解析失败原因。</param>
        /// <returns>Schema 可安全作为对象读取时返回 true。</returns>
        private static bool TryGetDescriptorSchema(
            NovaProjectActionDescriptor descriptor,
            out JObject schema,
            out string error)
        {
            schema = null;
            error = null;
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.RequestSchemaJson))
            {
                error = "MCP 暴露的 Action 缺少请求 Schema。";
                return false;
            }

            try
            {
                schema = JToken.Parse(descriptor.RequestSchemaJson) as JObject;
                if (schema == null)
                {
                    error = "MCP 暴露的 Action 请求 Schema 必须是 JSON object。";
                    return false;
                }
                return true;
            }
            catch (JsonException)
            {
                error = "MCP 暴露的 Action 请求 Schema 无法解析。";
                return false;
            }
        }

        /// <summary>
        /// 判断 Unity 是否正在编译或更新资产，此状态下不得创建或执行计划。
        /// </summary>
        /// <returns>Editor 不稳定时返回 true。</returns>
        private static bool IsEditorBusy()
        {
            return EditorApplication.isCompiling || EditorApplication.isUpdating;
        }

        /// <summary>
        /// 构造 Provider 中立的错误响应，并标记可重试的瞬时状态。
        /// </summary>
        /// <param name="code">稳定的传输层错误码。</param>
        /// <param name="message">面向调用方的安全错误说明。</param>
        /// <returns>可由 Provider Adapter 序列化的错误响应对象。</returns>
        private static NovaProjectActionGatewayResponse Error(string code, string message)
        {
            return new NovaProjectActionGatewayResponse
            {
                success = false,
                code = code,
                message = message,
                data = new ErrorView
                {
                    protocol_version = ProtocolVersion,
                    message = message,
                    retryable = code == "editor_busy" || code == "action_busy",
                },
            };
        }

        /// <summary>
        /// 统一包装 describe、plan、execute 与 verify 的成功或领域状态响应。
        /// </summary>
        /// <param name="operation">已处理的 MCP 操作名。</param>
        /// <param name="actionId">关联的 Action ID；列举 describe 时可为空。</param>
        /// <param name="status">Core 返回或 Adapter 生成的状态。</param>
        /// <param name="message">经过调用边界筛选的状态说明。</param>
        /// <param name="payload">可安全返回给 MCP 的 DTO 负载。</param>
        /// <returns>可由 Provider Adapter 序列化的成功响应对象。</returns>
        private static NovaProjectActionGatewayResponse ActionResponse(
            string operation,
            string actionId,
            string status,
            string message,
            object payload)
        {
            return new NovaProjectActionGatewayResponse
            {
                success = true,
                message = "Nova Project Action 请求已处理。",
                data = new ActionResponseView
                {
                    protocol_version = ProtocolVersion,
                    operation = operation,
                    action_id = actionId,
                    status = status,
                    message = message,
                    payload = payload,
                },
            };
        }

        /// <summary>
        /// 将 Core Action 描述投影为不暴露 CLR 实现细节的 MCP DTO。
        /// </summary>
        /// <param name="descriptor">已通过暴露策略校验的 Core 描述。</param>
        /// <returns>供 describe 响应使用的安全描述 DTO。</returns>
        private static DescriptorView ToDescriptorView(NovaProjectActionDescriptor descriptor)
        {
            return new DescriptorView
            {
                action_id = descriptor.Id,
                display_name = descriptor.DisplayName,
                description = descriptor.Description,
                domain = descriptor.Domain,
                operation_type = descriptor.OperationType,
                effects = ExpandFlags(descriptor.Effects),
                required_evidence = descriptor.RequiredEvidence ?? Array.Empty<string>(),
                idempotency = descriptor.Idempotency,
                contract_major = descriptor.ContractMajor,
                requires_confirmation = descriptor.RequiresConfirmation,
                requires_stable_editor = descriptor.RequiresStableEditor,
                requires_edit_mode = descriptor.RequiresEditMode,
                locks = descriptor.Locks ?? Array.Empty<string>(),
                verify_locks = descriptor.VerifyLocks ?? Array.Empty<string>(),
                request_schema = ParseAndSanitizeJson(descriptor.RequestSchemaJson),
            };
        }

        /// <summary>
        /// 将 Core 计划投影为 MCP DTO，并清理其中可能出现的路径或 URL 文本。
        /// </summary>
        /// <param name="plan">Core 创建的计划结果。</param>
        /// <returns>供 plan 响应使用的安全计划 DTO。</returns>
        private static PlanView ToPlanView(NovaProjectActionPlan plan)
        {
            return new PlanView
            {
                plan_id = plan.PlanId,
                expires_at_utc = plan.ExpiresAtUtc?.ToString("O"),
                operation_id = plan.OperationId,
                recovery_token = plan.RecoveryToken,
                data = ParseAndSanitizeJson(plan.DataJson),
                write_set = (plan.WriteSet ?? Array.Empty<string>()).Select(SanitizeText).ToArray(),
                evidence = (plan.Evidence ?? Array.Empty<string>()).Select(SanitizeText).ToArray(),
            };
        }

        /// <summary>
        /// 将 Core 执行或核验结果投影为 MCP DTO，并裁剪敏感文本。
        /// </summary>
        /// <param name="result">Core 返回的执行或核验结果。</param>
        /// <returns>供 execute 或 verify 响应使用的安全结果 DTO。</returns>
        private static ResultView ToResultView(NovaProjectActionResult result)
        {
            return new ResultView
            {
                data = ParseAndSanitizeJson(result.DataJson),
                recovery_token = result.RecoveryToken,
                evidence_kinds = result.EvidenceKinds ?? Array.Empty<string>(),
                artifacts = (result.Artifacts ?? Array.Empty<string>()).Select(SanitizeText).ToArray(),
                evidence = (result.Evidence ?? Array.Empty<string>()).Select(SanitizeText).ToArray(),
                warnings = (result.Warnings ?? Array.Empty<string>()).Select(SanitizeText).ToArray(),
            };
        }

        /// <summary>
        /// 解析 Core 返回的 JSON 并递归脱敏；无法解析时返回稳定的不可用占位值。
        /// </summary>
        /// <param name="json">Core 返回的 JSON 文本。</param>
        /// <returns>可安全写入 MCP 响应的 JSON 节点或 null。</returns>
        private static JToken ParseAndSanitizeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                JToken token = JToken.Parse(json);
                SanitizeToken(token);
                return token;
            }
            catch (JsonException)
            {
                return JValue.CreateString("[unavailable]");
            }
        }

        /// <summary>
        /// 原地递归打码敏感字段，并清理字符串中的项目路径与 URL 认证信息。
        /// </summary>
        /// <param name="token">待处理的 JSON 节点。</param>
        private static void SanitizeToken(JToken token)
        {
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties().ToArray())
                {
                    if (IsSensitiveField(property.Name))
                    {
                        property.Value = "[redacted]";
                    }
                    else
                    {
                        SanitizeToken(property.Value);
                    }
                }
                return;
            }

            if (token is JArray array)
            {
                foreach (JToken child in array)
                {
                    SanitizeToken(child);
                }
                return;
            }

            if (token is JValue value && value.Type == JTokenType.String)
            {
                value.Value = SanitizeText((string)value.Value);
            }
        }

        /// <summary>
        /// 判断 JSON 字段名是否可能承载凭据或 AES 密钥材料。
        /// </summary>
        /// <param name="name">待规范化检查的字段名。</param>
        /// <returns>字段应被完全打码时返回 true。</returns>
        private static bool IsSensitiveField(string name)
        {
            string normalized = name?.Replace("_", string.Empty).ToLowerInvariant() ?? string.Empty;
            return normalized.Contains("password") || normalized.Contains("secret") ||
                   normalized.Contains("credential") || normalized.Contains("accesstoken") ||
                   normalized.Contains("authtoken") || normalized.Contains("apikey") ||
                   normalized.Contains("aeskey") || normalized.Contains("aesiv");
        }

        /// <summary>
        /// 清理文本中的项目绝对路径及 URL 用户名、密码、查询参数和片段。
        /// </summary>
        /// <param name="value">待返回给 MCP 调用方的原始文本。</param>
        /// <returns>已移除敏感定位与认证信息的文本。</returns>
        private static string SanitizeText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            string sanitized = string.IsNullOrEmpty(projectRoot)
                ? value
                : value.Replace(projectRoot, "<project-root>");

            string[] parts = sanitized.Split(new[] { ' ', '\t', '\r', '\n', '，', '；', '。' });
            foreach (string part in parts.Where(item => item.Contains("://")))
            {
                string candidate = part.Trim('"', '\'', '(', ')', '[', ']', '{', '}', ',', ';');
                if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri uri))
                {
                    continue;
                }

                var builder = new UriBuilder(uri)
                {
                    UserName = string.Empty,
                    Password = string.Empty,
                    Query = string.Empty,
                    Fragment = string.Empty,
                };
                sanitized = sanitized.Replace(candidate, builder.Uri.AbsoluteUri.TrimEnd('/'));
            }
            return sanitized;
        }

        /// <summary>
        /// 将标志枚举展开为稳定的 kebab-case 字符串数组，零值使用 none 表示。
        /// </summary>
        /// <typeparam name="T">待展开的枚举类型。</typeparam>
        /// <param name="value">待展开的标志枚举值。</param>
        /// <returns>供 MCP DTO 使用的字符串标志集合。</returns>
        private static string[] ExpandFlags<T>(T value) where T : Enum
        {
            ulong raw = Convert.ToUInt64(value);
            if (raw == 0)
            {
                return new[] { "none" };
            }

            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .Where(item => Convert.ToUInt64(item) != 0 && (raw & Convert.ToUInt64(item)) == Convert.ToUInt64(item))
                .Select(item => ToKebabCase(item.ToString()))
                .ToArray();
        }

        /// <summary>
        /// 将 PascalCase 枚举或标识文本转换为 MCP 响应使用的 kebab-case。
        /// </summary>
        /// <param name="value">待转换的原始文本。</param>
        /// <returns>转换后的文本；空值保持不变。</returns>
        private static string ToKebabCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var builder = new StringBuilder(value.Length + 8);
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (index > 0 && char.IsUpper(character))
                {
                    builder.Append('-');
                }
                builder.Append(char.ToLowerInvariant(character));
            }
            return builder.ToString();
        }

        /// <summary>
        /// 读取可选字符串字段，并将 JSON null 统一映射为 C# null。
        /// </summary>
        /// <param name="parameters">已通过结构校验的参数对象。</param>
        /// <param name="name">待读取的字段名。</param>
        /// <returns>字段字符串值，缺失或 JSON null 时返回 null。</returns>
        private static string ReadOptionalString(JObject parameters, string name)
        {
            JToken token = parameters[name];
            return token == null || token.Type == JTokenType.Null ? null : (string)token;
        }

        private sealed class RequestError
        {
            /// <summary>
            /// 创建供 Adapter 内部短路返回的传输校验错误。
            /// </summary>
            /// <param name="code">稳定的错误码。</param>
            /// <param name="message">面向调用方的安全错误说明。</param>
            public RequestError(string code, string message)
            {
                Code = code;
                Message = message;
            }

            public string Code { get; }
            public string Message { get; }
        }

        /// <summary>
        /// 仅表达“此 Action 经过 MCP 暴露审查”，不携带实现入口或绕过 Core 的权限。
        /// </summary>
        private sealed class ExposurePolicy
        {
            /// <summary>
            /// 创建一条只按 Action ID 表达的显式 MCP 暴露策略。
            /// </summary>
            /// <param name="actionId">经人工审查后允许暴露的 Action ID。</param>
            public ExposurePolicy(string actionId)
            {
                ActionId = actionId;
            }

            public string ActionId { get; }
        }

        private sealed class ErrorView
        {
            public int protocol_version;
            public string message;
            public bool retryable;
        }

        private sealed class ActionResponseView
        {
            public int protocol_version;
            public string operation;
            public string action_id;
            public string status;
            public string message;
            public object payload;
        }

        private sealed class DescriptorView
        {
            public string action_id;
            public string display_name;
            public string description;
            public string domain;
            public string operation_type;
            public string[] effects;
            public string[] required_evidence;
            public string idempotency;
            public int contract_major;
            public bool requires_confirmation;
            public bool requires_stable_editor;
            public bool requires_edit_mode;
            public string[] locks;
            public string[] verify_locks;
            public object request_schema;
        }

        private sealed class PlanView
        {
            public string plan_id;
            public string expires_at_utc;
            public string operation_id;
            public string recovery_token;
            public JToken data;
            public string[] write_set;
            public string[] evidence;
        }

        private sealed class ResultView
        {
            public JToken data;
            public string recovery_token;
            public string[] evidence_kinds;
            public string[] artifacts;
            public string[] evidence;
            public string[] warnings;
            public string authorization_source;
        }
    }
}
