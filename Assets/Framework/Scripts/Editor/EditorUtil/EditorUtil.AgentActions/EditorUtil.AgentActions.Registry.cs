/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentActions.Registry.cs
 * author:    taoye
 * created:   2026/8/19
 * descrip:   Nova Project C# Action 的受控发现注册表
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Nova Project C# Action 的统一发现与执行入口。
        /// </summary>
        public static partial class AgentActions
        {
            internal sealed class RegisteredAction
            {
                public AgentActionDescriptor Descriptor;
                public IAgentActionHandler Handler;
            }

            /// <summary>
            /// 仅扫描 Framework Editor 程序集内强类型 Handler 的 Action Registry。
            /// </summary>
            [InitializeOnLoad]
            public static class Registry
            {
                private static readonly Regex s_IdPattern = new Regex(
                    @"^nova\.project\.[a-z0-9]+(?:-[a-z0-9]+)*\.[a-z0-9]+(?:[.-][a-z0-9]+)*$",
                    RegexOptions.CultureInvariant);

                private static long s_Generation;
                private static RegistrySnapshot s_Snapshot;

                static Registry()
                {
                    Rebuild();
                }

                /// <summary>
                /// 重新发现 Framework Editor 程序集中的 Action Handler。
                /// 重复 ID 会整体失效，不依赖不稳定的扫描顺序选择其中一个。
                /// </summary>
                public static void Rebuild()
                {
                    if (!AgentActionRuntime.IsMainThread)
                    {
                        throw new InvalidOperationException("Agent Action Registry 只能在 Unity 主线程重建。");
                    }

                    Assembly frameworkEditorAssembly = typeof(EditorUtil).Assembly;
                    var candidates = new List<(Type Type, AgentActionAttribute Attribute)>();
                    var issues = new List<AgentActionRegistryIssue>();
                    foreach (Type type in UnityEditor.TypeCache.GetTypesDerivedFrom<IAgentActionHandler>())
                    {
                        if (type.Assembly != frameworkEditorAssembly || type.IsAbstract || type.IsInterface)
                        {
                            continue;
                        }

                        AgentActionAttribute attribute = type.GetCustomAttribute<AgentActionAttribute>();
                        if (attribute == null)
                        {
                            issues.Add(new AgentActionRegistryIssue(
                                "missing-attribute",
                                $"{type.FullName} 实现了 IAgentActionHandler 但未声明 AgentActionAttribute。"));
                            continue;
                        }

                        candidates.Add((type, attribute));
                    }

                    var actions = new Dictionary<string, RegisteredAction>(StringComparer.Ordinal);
                    foreach (IGrouping<string, (Type Type, AgentActionAttribute Attribute)> group in
                             candidates.GroupBy(candidate => candidate.Attribute.Id, StringComparer.Ordinal))
                    {
                        if (string.IsNullOrEmpty(group.Key) || group.Count() != 1)
                        {
                            issues.Add(new AgentActionRegistryIssue(
                                "duplicate-id",
                                $"Action ID '{group.Key}' 必须唯一，当前命中：{string.Join(", ", group.Select(item => item.Type.FullName))}。"));
                            continue;
                        }

                        (Type type, AgentActionAttribute attribute) = group.Single();
                        if (!TryCreate(type, attribute, out RegisteredAction action, out AgentActionRegistryIssue issue))
                        {
                            issues.Add(issue);
                            continue;
                        }

                        actions.Add(attribute.Id, action);
                    }

                    long generation = Interlocked.Increment(ref s_Generation);
                    Interlocked.Exchange(ref s_Snapshot, new RegistrySnapshot(actions, issues, generation));
                }

                public static IReadOnlyList<AgentActionDescriptor> GetAll()
                {
                    return Current.Descriptors;
                }

                public static AgentActionDescriptor Describe(string actionId)
                {
                    return Find(actionId)?.Descriptor;
                }

                public static IReadOnlyList<AgentActionRegistryIssue> GetIssues()
                {
                    return Current.Issues;
                }

                internal static long Generation => Current.Generation;

                internal static RegisteredAction Find(string actionId)
                {
                    if (string.IsNullOrWhiteSpace(actionId))
                    {
                        return null;
                    }

                    Current.Actions.TryGetValue(actionId.Trim(), out RegisteredAction action);
                    return action;
                }

                private static bool TryCreate(
                    Type type,
                    AgentActionAttribute attribute,
                    out RegisteredAction action,
                    out AgentActionRegistryIssue issue)
                {
                    action = null;
                    issue = null;
                    if (!s_IdPattern.IsMatch(attribute.Id ?? string.Empty) ||
                        string.IsNullOrWhiteSpace(attribute.Domain) ||
                        !attribute.Id.StartsWith("nova.project." + attribute.Domain + ".", StringComparison.Ordinal))
                    {
                        issue = new AgentActionRegistryIssue(
                            "invalid-id",
                            $"{type.FullName} 的 Action ID 或 Domain 不符合 nova.project.<domain>.<verb> 约定。");
                        return false;
                    }

                    if (attribute.ContractMajor < 1)
                    {
                        issue = new AgentActionRegistryIssue("invalid-contract", $"{attribute.Id} 的 ContractMajor 必须大于 0。");
                        return false;
                    }

                    if (attribute.RequiredEvidence == AgentActionEvidence.None)
                    {
                        issue = new AgentActionRegistryIssue(
                            "missing-evidence",
                            $"{attribute.Id} 必须声明 success 所需的证据集合。");
                        return false;
                    }

                    AgentActionEffect writeEffects = AgentActionEffect.WorkspaceWrite |
                                                     AgentActionEffect.UnityWrite |
                                                     AgentActionEffect.ExternalWrite |
                                                     AgentActionEffect.BuildArtifact |
                                                     AgentActionEffect.Destructive;
                    if (attribute.Idempotency == AgentActionIdempotency.ReadOnly &&
                        (attribute.Effects & writeEffects) != 0)
                    {
                        issue = new AgentActionRegistryIssue(
                            "invalid-idempotency",
                            $"{attribute.Id} 声明为 ReadOnly，但同时声明了写入或构建副作用。");
                        return false;
                    }

                    if ((attribute.Effects & writeEffects) != 0 &&
                        (attribute.Locks == null || attribute.Locks.Length == 0))
                    {
                        issue = new AgentActionRegistryIssue(
                            "missing-lock",
                            $"{attribute.Id} 含写入或构建副作用，必须声明资源锁。");
                        return false;
                    }

                    AgentActionEffect confirmationEffects = AgentActionEffect.WorkspaceWrite |
                                                            AgentActionEffect.UnityWrite |
                                                            AgentActionEffect.Destructive |
                                                            AgentActionEffect.ExternalWrite |
                                                            AgentActionEffect.BuildArtifact |
                                                            AgentActionEffect.Credential;
                    if ((attribute.Effects & confirmationEffects) != 0 && !attribute.RequiresConfirmation)
                    {
                        issue = new AgentActionRegistryIssue(
                            "missing-confirmation",
                            $"{attribute.Id} 含写入、构建、凭据或破坏性副作用，必须要求确认。");
                        return false;
                    }

                    IAgentActionHandler handler;
                    try
                    {
                        handler = (IAgentActionHandler)Activator.CreateInstance(type, true);
                    }
                    catch (Exception exception)
                    {
                        issue = new AgentActionRegistryIssue(
                            "handler-construction-failed",
                            $"{attribute.Id} Handler 构造失败：{exception.Message}");
                        return false;
                    }

                    if (handler.RequestType == null ||
                        !handler.RequestType.IsClass ||
                        handler.RequestType.GetCustomAttribute<SerializableAttribute>() == null)
                    {
                        issue = new AgentActionRegistryIssue(
                            "invalid-request-type",
                            $"{attribute.Id} 的请求必须是 [Serializable] class。");
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(handler.RequestSchemaJson))
                    {
                        issue = new AgentActionRegistryIssue(
                            "missing-request-schema",
                            $"{attribute.Id} 必须提供非空请求 Schema。");
                        return false;
                    }

                    action = new RegisteredAction
                    {
                        Descriptor = new AgentActionDescriptor(attribute, handler.RequestType, handler.RequestSchemaJson),
                        Handler = handler,
                    };
                    return true;
                }

                private static RegistrySnapshot Current =>
                    Volatile.Read(ref s_Snapshot) ?? throw new InvalidOperationException("Agent Action Registry 尚未初始化。");

                /// <summary>
                /// Registry 每次重建生成不可变快照，读路径不会观察到半构建状态。
                /// </summary>
                private sealed class RegistrySnapshot
                {
                    /// <summary>
                    /// 冻结本代 Registry 的 Action、描述、问题与 generation。
                    /// </summary>
                    public RegistrySnapshot(
                        Dictionary<string, RegisteredAction> actions,
                        List<AgentActionRegistryIssue> issues,
                        long generation)
                    {
                        Actions = new Dictionary<string, RegisteredAction>(actions, StringComparer.Ordinal);
                        Descriptors = Array.AsReadOnly(actions.Values
                            .Select(item => item.Descriptor)
                            .OrderBy(item => item.Id, StringComparer.Ordinal)
                            .ToArray());
                        Issues = Array.AsReadOnly(issues.ToArray());
                        Generation = generation;
                    }

                    public IReadOnlyDictionary<string, RegisteredAction> Actions { get; }
                    public IReadOnlyList<AgentActionDescriptor> Descriptors { get; }
                    public IReadOnlyList<AgentActionRegistryIssue> Issues { get; }
                    public long Generation { get; }
                }
            }
        }
    }
}
