/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigInspectPluginTypesAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   SDK 与 Kit 配置类型的稳定只读扫描 Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.config.inspect-plugin-types",
        "扫描 Config 插件类型",
        "config",
        AgentActionOperationType.Inspect,
        Effects = AgentActionEffect.UnityRead,
        RequiredEvidence = AgentActionEvidence.Static,
        Idempotency = AgentActionIdempotency.ReadOnly,
        ReloadSemantics = AgentActionReloadSemantics.ReloadNotExpected,
        Locks = new[] { "unity-editor", "loaded-assemblies" })]
    internal sealed class ConfigInspectPluginTypesAction : AgentActionHandler<ConfigInspectPluginTypesAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string kind;
        }

        [Serializable]
        private sealed class PluginTypeView
        {
            public string kind;
            public string typeFullName;
            public string displayName;
            public string assemblyName;
            public string assemblyIdentity;
            public string moduleVersionId;
        }

        [Serializable]
        private sealed class ResultView
        {
            public string kind;
            public int count;
            public PluginTypeView[] types;
        }

        private sealed class State
        {
            public string PayloadJson;
        }

        /// <summary>
        /// kind 仅接受 sdk、kit 或 all，避免隐式扩大扫描语义。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (request == null || request.kind != "sdk" && request.kind != "kit" && request.kind != "all")
            {
                error = "kind 只能是 sdk、kit 或 all。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 仅通过 TypeCache 读取类型元数据，不构造任何消费项目插件实例。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            ResultView view = BuildView(request.kind);
            string payload = Util.Json.Serialize(request);
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = $"扫描到 {view.count} 个 {request.kind} Config 插件类型。",
                DataJson = Util.Json.Serialize(view),
                State = new State { PayloadJson = payload },
                RecoveryPayloadJson = payload,
                Evidence = new[] { "结果由 TypeCache 元数据生成，未调用 Scanner.ScanAll 或 Activator.CreateInstance；传输 DTO 不包含 System.Type。" },
            });
        }

        /// <summary>
        /// 重新扫描当前已加载程序集并返回纯数据结果。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State typed) || string.IsNullOrWhiteSpace(typed.PayloadJson))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "插件类型扫描计划状态无效。"));
            }
            return VerifyPayloadAsync(typed.PayloadJson);
        }

        /// <summary>
        /// 根据 Receipt 重新扫描当前已加载程序集，不复用旧 Type 对象。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            return VerifyPayloadAsync(receiptJson);
        }

        /// <summary>
        /// 解析扫描请求并返回最新快照。
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
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "插件类型扫描 Receipt 无法解析：" + exception.Message));
            }
            if (request == null || request.kind != "sdk" && request.kind != "kit" && request.kind != "all")
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "插件类型扫描 Receipt 的 kind 无效。"));
            }

            ResultView view = BuildView(request.kind);
            AgentActionResult result = AgentActionResult.Create(null, "success", $"插件类型扫描完成，共 {view.count} 项。");
            result.DataJson = Util.Json.Serialize(view);
            result.ReceiptJson = payloadJson;
            result.EvidenceKinds = AgentActionEvidence.Static;
            result.Evidence.Add("已通过 TypeCache 输出稳定排序的纯元数据快照，不会执行未知消费类型构造器。结果只证明当前 Editor 可发现这些配置类型。");
            return Task.FromResult(result);
        }

        /// <summary>
        /// 构建不携带 Type 的扫描结果，并按稳定键排序与去重。
        /// </summary>
        private static ResultView BuildView(string kind)
        {
            var items = new List<PluginTypeView>();
            if (kind == "sdk" || kind == "all")
            {
                items.AddRange(BuildMetadataViews<ISDKPluginConfig>("sdk"));
            }
            if (kind == "kit" || kind == "all")
            {
                items.AddRange(BuildMetadataViews<IKitConfig>("kit"));
            }

            PluginTypeView[] sorted = items
                .Where(item => !string.IsNullOrEmpty(item.typeFullName))
                .GroupBy(item => item.kind + "\n" + item.typeFullName + "\n" + item.assemblyIdentity + "\n" + item.moduleVersionId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(item => item.kind, StringComparer.Ordinal)
                .ThenBy(item => item.typeFullName, StringComparer.Ordinal)
                .ThenBy(item => item.assemblyIdentity, StringComparer.Ordinal)
                .ThenBy(item => item.moduleVersionId, StringComparer.Ordinal)
                .ToArray();
            return new ResultView { kind = kind, count = sorted.Length, types = sorted };
        }

        /// <summary>
        /// TypeCache 只枚举已加载类型元数据。DisplayName 是实例属性，受控 Action 不为展示字段执行消费类型构造器，因此回退为类型名。
        /// </summary>
        private static IEnumerable<PluginTypeView> BuildMetadataViews<TContract>(string kind)
        {
            return TypeCache.GetTypesDerivedFrom<TContract>()
                .Where(IsSafeConfigType)
                .Select(type => new PluginTypeView
                {
                    kind = kind,
                    typeFullName = type.FullName,
                    displayName = type.Name,
                    assemblyName = type.Assembly.GetName().Name,
                    assemblyIdentity = type.Assembly.FullName,
                    moduleVersionId = type.Module.ModuleVersionId.ToString("D"),
                });
        }

        private static bool IsSafeConfigType(Type type)
        {
            return type != null && !type.IsAbstract && !type.IsInterface && type.IsSerializable &&
                   !IsTestAssembly(type.Assembly) &&
                   (type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null);
        }

        private static bool IsTestAssembly(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies().Any(name => name.Name == "nunit.framework");
        }
    }
}
