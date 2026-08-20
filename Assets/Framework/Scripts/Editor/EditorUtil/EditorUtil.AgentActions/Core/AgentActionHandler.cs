/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AgentActionHandler.cs
 * author:    taoye
 * created:   2026/8/19
 * descrip:   强类型 Nova Project C# Action Handler 基类
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Editor
{
    internal sealed class AgentActionExecutionContext
    {
        public AgentActionExecutionContext(string confirmationToken, CancellationToken cancellationToken)
        {
            ConfirmationToken = confirmationToken;
            CancellationToken = cancellationToken;
        }

        public string ConfirmationToken { get; }

        public CancellationToken CancellationToken { get; }
    }

    internal sealed class AgentActionHandlerPlan
    {
        public string Status;
        public string Summary;
        public string DataJson;
        public object State;
        public string[] WriteSet = Array.Empty<string>();
        public string[] Evidence = Array.Empty<string>();

        /// <summary>
        /// 可选的领域 Verify 载荷。需要跨 domain reload 恢复验证的 Action 应在 Plan 阶段生成它。
        /// </summary>
        public string RecoveryPayloadJson;
    }

    internal interface IAgentActionHandler
    {
        Type RequestType { get; }

        string RequestSchemaJson { get; }

        bool TryParseRequest(string requestJson, out object request, out string error);

        Task<AgentActionHandlerPlan> PlanAsync(object request, AgentActionExecutionContext context);

        Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context);

        Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context);
    }

    /// <summary>
    /// 每个 Action 使用独立请求 DTO；传输边界是 JSON，Handler 内部保持强类型。
    /// </summary>
    internal abstract class AgentActionHandler<TRequest> : IAgentActionHandler
        where TRequest : class
    {
        public Type RequestType => typeof(TRequest);

        public string RequestSchemaJson => RequestContract.SchemaJson;

        /// <summary>
        /// 请求契约的唯一真源。子类可覆写以补充更严格的 Schema 与语义校验。
        /// </summary>
        protected virtual AgentActionRequestContract<TRequest> RequestContract { get; } =
            AgentActionRequestContract<TRequest>.CreateDefault();

        /// <summary>
        /// 在进入领域 Plan 前执行 Action 专属语义校验。
        /// </summary>
        protected virtual bool TryValidateRequest(TRequest request, out string error)
        {
            error = null;
            return true;
        }

        /// <summary>
        /// 使用 Handler 声明的唯一请求契约完成严格解析与语义校验。
        /// </summary>
        public bool TryParseRequest(string requestJson, out object request, out string error)
        {
            request = null;
            if (!RequestContract.TryParse(requestJson, out TRequest typedRequest, out error))
            {
                return false;
            }

            if (!TryValidateRequest(typedRequest, out error))
            {
                return false;
            }

            request = typedRequest;
            return true;
        }

        public Task<AgentActionHandlerPlan> PlanAsync(object request, AgentActionExecutionContext context)
        {
            return PlanAsync((TRequest)request, context);
        }

        public abstract Task<AgentActionHandlerPlan> PlanAsync(
            TRequest request,
            AgentActionExecutionContext context);

        public abstract Task<AgentActionResult> ExecuteAsync(
            object state,
            AgentActionExecutionContext context);

        public abstract Task<AgentActionResult> VerifyAsync(
            string receiptJson,
            AgentActionExecutionContext context);
    }

    /// <summary>
    /// Action 请求的严格 JSON 契约。Schema、结构解析和基础类型检查由同一对象提供，避免传输层复制规则。
    /// </summary>
    internal sealed class AgentActionRequestContract<TRequest> where TRequest : class
    {
        private const int c_MaxDepth = 64;
        private const int c_MaxPayloadBytes = 64 * 1024;
        private readonly Dictionary<string, MemberContract> m_Members;

        private AgentActionRequestContract(Dictionary<string, MemberContract> members, string schemaJson)
        {
            m_Members = members;
            SchemaJson = schemaJson;
        }

        public string SchemaJson { get; }

        /// <summary>
        /// 从 DTO 公共字段和可写属性生成默认严格契约。
        /// </summary>
        public static AgentActionRequestContract<TRequest> CreateDefault()
        {
            Dictionary<string, MemberContract> members = CollectMembers(typeof(TRequest));
            JObject schema = CreateTypeSchema(typeof(TRequest), new HashSet<Type>());
            return new AgentActionRequestContract<TRequest>(members, schema.ToString(Formatting.None));
        }

        /// <summary>
        /// 严格解析请求：拒绝重复字段、未知字段、缺失必填字段和不匹配的 JSON token 类型。
        /// </summary>
        public bool TryParse(string requestJson, out TRequest request, out string error)
        {
            request = null;
            error = null;
            string json = string.IsNullOrWhiteSpace(requestJson) ? "{}" : requestJson;
            if (System.Text.Encoding.UTF8.GetByteCount(json) > c_MaxPayloadBytes)
            {
                error = "Action 请求不能超过 64 KiB。";
                return false;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                });
            }
            catch (JsonException exception)
            {
                error = "Action 请求必须是合法且无重复字段的 JSON object：" + exception.Message;
                return false;
            }

            if (GetDepth(root) > c_MaxDepth)
            {
                error = "Action 请求嵌套层级超过限制。";
                return false;
            }

            foreach (JProperty property in root.Properties())
            {
                if (!m_Members.TryGetValue(property.Name, out MemberContract member))
                {
                    error = $"Action 请求包含未知字段：{property.Name}。";
                    return false;
                }

                if (!TryValidateToken(property.Value, member.Type, property.Name, out error))
                {
                    return false;
                }
            }

            string missing = m_Members
                .Where(item => item.Value.Required && root[item.Key] == null)
                .Select(item => item.Key)
                .FirstOrDefault();
            if (missing != null)
            {
                error = $"Action 请求缺少必填字段：{missing}。";
                return false;
            }

            try
            {
                request = root.ToObject<TRequest>(JsonSerializer.Create(new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error,
                    TypeNameHandling = TypeNameHandling.None,
                    MaxDepth = c_MaxDepth,
                }));
            }
            catch (JsonException exception)
            {
                error = "Action 请求无法按契约解析：" + exception.Message;
                return false;
            }

            if (request == null)
            {
                error = "Action 请求不能为空。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 收集请求 DTO 可参与 JSON 契约的公共字段和可写属性。
        /// </summary>
        private static Dictionary<string, MemberContract> CollectMembers(Type contractType)
        {
            var result = new Dictionary<string, MemberContract>(StringComparer.Ordinal);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            foreach (FieldInfo field in contractType.GetFields(flags))
            {
                AddMember(result, field.Name, field.FieldType, field.GetCustomAttribute<AgentActionRequiredAttribute>() != null);
            }

            foreach (PropertyInfo property in contractType.GetProperties(flags))
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    AddMember(result, property.Name, property.PropertyType,
                        property.GetCustomAttribute<AgentActionRequiredAttribute>() != null);
                }
            }
            return result;
        }

        /// <summary>
        /// 添加单个 DTO 成员并计算它是否必须显式出现。
        /// </summary>
        private static void AddMember(Dictionary<string, MemberContract> result, string name, Type type, bool explicitlyRequired)
        {
            bool required = explicitlyRequired || type.IsValueType && Nullable.GetUnderlyingType(type) == null;
            result[name] = new MemberContract(type, required);
        }

        /// <summary>
        /// 递归生成受限 JSON Schema，并在递归类型处安全收口。
        /// </summary>
        private static JObject CreateTypeSchema(Type type, HashSet<Type> stack)
        {
            Type effective = Nullable.GetUnderlyingType(type) ?? type;
            if (effective == typeof(string) || effective == typeof(char) || effective.IsEnum)
            {
                var schema = new JObject { ["type"] = "string" };
                if (effective.IsEnum)
                {
                    schema["enum"] = new JArray(Enum.GetNames(effective));
                }
                return schema;
            }
            if (effective == typeof(bool)) return new JObject { ["type"] = "boolean" };
            if (IsInteger(effective)) return new JObject { ["type"] = "integer" };
            if (IsNumber(effective)) return new JObject { ["type"] = "number" };
            if (typeof(IDictionary).IsAssignableFrom(effective))
            {
                Type valueType = effective.IsGenericType ? effective.GetGenericArguments().Last() : typeof(object);
                return new JObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = CreateTypeSchema(valueType, stack),
                };
            }
            if (effective.IsArray || typeof(IEnumerable).IsAssignableFrom(effective))
            {
                Type itemType = effective.IsArray
                    ? effective.GetElementType()
                    : effective.IsGenericType ? effective.GetGenericArguments().First() : typeof(object);
                return new JObject { ["type"] = "array", ["items"] = CreateTypeSchema(itemType ?? typeof(object), stack) };
            }

            if (effective == typeof(object) || !stack.Add(effective))
            {
                return new JObject { ["type"] = "object" };
            }

            Dictionary<string, MemberContract> members = CollectMembers(effective);
            var properties = new JObject();
            foreach (KeyValuePair<string, MemberContract> pair in members.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                properties[pair.Key] = CreateTypeSchema(pair.Value.Type, stack);
            }
            stack.Remove(effective);
            return new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = properties,
                ["required"] = new JArray(members.Where(item => item.Value.Required).Select(item => item.Key)),
            };
        }

        /// <summary>
        /// 递归核对 JSON token 与 DTO 类型，拒绝嵌套未知字段和错误类型。
        /// </summary>
        private static bool TryValidateToken(JToken token, Type type, string path, out string error)
        {
            error = null;
            if (token.Type == JTokenType.Null)
            {
                if (!type.IsValueType || Nullable.GetUnderlyingType(type) != null) return true;
                error = $"Action 请求字段 {path} 不允许为 null。";
                return false;
            }

            Type effective = Nullable.GetUnderlyingType(type) ?? type;
            if (effective == typeof(string) || effective == typeof(char) || effective.IsEnum)
                return RequireToken(token, JTokenType.String, path, out error);
            if (effective == typeof(bool)) return RequireToken(token, JTokenType.Boolean, path, out error);
            if (IsInteger(effective)) return RequireToken(token, JTokenType.Integer, path, out error);
            if (IsNumber(effective))
            {
                if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) return true;
                error = $"Action 请求字段 {path} 必须是 number。";
                return false;
            }

            if (typeof(IDictionary).IsAssignableFrom(effective))
            {
                if (!(token is JObject dictionary))
                {
                    error = $"Action 请求字段 {path} 必须是 object。";
                    return false;
                }
                Type valueType = effective.IsGenericType ? effective.GetGenericArguments().Last() : typeof(object);
                foreach (JProperty property in dictionary.Properties())
                {
                    if (!TryValidateToken(property.Value, valueType, path + "." + property.Name, out error)) return false;
                }
                return true;
            }

            if (effective.IsArray || typeof(IEnumerable).IsAssignableFrom(effective))
            {
                if (!(token is JArray array))
                {
                    error = $"Action 请求字段 {path} 必须是 array。";
                    return false;
                }
                Type itemType = effective.IsArray
                    ? effective.GetElementType()
                    : effective.IsGenericType ? effective.GetGenericArguments().First() : typeof(object);
                for (int index = 0; index < array.Count; index++)
                {
                    if (!TryValidateToken(array[index], itemType ?? typeof(object), $"{path}[{index}]", out error)) return false;
                }
                return true;
            }

            if (!(token is JObject obj))
            {
                error = $"Action 请求字段 {path} 必须是 object。";
                return false;
            }
            if (effective == typeof(object)) return true;

            Dictionary<string, MemberContract> members = CollectMembers(effective);
            foreach (JProperty property in obj.Properties())
            {
                if (!members.TryGetValue(property.Name, out MemberContract member))
                {
                    error = $"Action 请求包含未知字段：{path}.{property.Name}。";
                    return false;
                }
                if (!TryValidateToken(property.Value, member.Type, path + "." + property.Name, out error)) return false;
            }
            string missing = members.Where(item => item.Value.Required && obj[item.Key] == null).Select(item => item.Key).FirstOrDefault();
            if (missing == null) return true;
            error = $"Action 请求缺少必填字段：{path}.{missing}。";
            return false;
        }

        /// <summary>
        /// 核对单个基础 JSON token 类型并生成稳定错误信息。
        /// </summary>
        private static bool RequireToken(JToken token, JTokenType required, string path, out string error)
        {
            if (token.Type == required)
            {
                error = null;
                return true;
            }
            error = $"Action 请求字段 {path} 必须是 {required.ToString().ToLowerInvariant()}。";
            return false;
        }

        /// <summary>
        /// 判断 CLR 类型是否为整数类型。
        /// </summary>
        private static bool IsInteger(Type type)
        {
            return type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) ||
                   type == typeof(ushort) || type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong);
        }

        /// <summary>
        /// 判断 CLR 类型是否为 JSON number 可表达的数值类型。
        /// </summary>
        private static bool IsNumber(Type type)
        {
            return IsInteger(type) || type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        }

        /// <summary>
        /// 计算 JSON token 的最大嵌套深度。
        /// </summary>
        private static int GetDepth(JToken token)
        {
            return token.HasValues ? 1 + token.Children().Max(GetDepth) : 1;
        }

        private sealed class MemberContract
        {
            /// <summary>
            /// 建立单个成员的类型与必填约束。
            /// </summary>
            public MemberContract(Type type, bool required)
            {
                Type = type;
                Required = required;
            }

            public Type Type { get; }
            public bool Required { get; }
        }
    }
}
