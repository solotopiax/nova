/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentCapabilities.cs
 * author:    taoye
 * created:   2026/8/25
 * descrip:   聚合 Skill、Action Registry 与 MCP 暴露状态
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NovaFramework.Mcp.Editor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Nova Agent 能力的只读聚合入口。不会执行 Action，也不会修改项目。
        /// </summary>
        public static partial class AgentCapabilities
        {
            /// <summary>
            /// 创建当前项目的 Skill、Action Registry 与 MCP 开放状态只读快照。
            /// </summary>
            /// <returns>用于 EditorWindow 展示的不可变能力快照。</returns>
            public static Snapshot CreateSnapshot()
            {
                var issues = new List<CapabilityIssue>();
                string agentsRoot = ResolveAgentsRoot(issues);
                ProjectionView projection = InspectProjection();
                bool catalogValid = projection.CatalogValidated || ValidateCatalog(agentsRoot, issues);
                if (catalogValid && !projection.CatalogValidated && !string.IsNullOrEmpty(projection.ErrorMessage))
                    issues.Add(new CapabilityIssue("Skill 投影", projection.ErrorMessage));
                if (string.IsNullOrEmpty(projection.FrameworkVersion))
                    projection.FrameworkVersion = ReadFrameworkVersion(agentsRoot, issues);
                List<SkillCapability> skills = catalogValid
                    ? ReadSkills(agentsRoot, projection, issues)
                    : new List<SkillCapability>();

                IReadOnlyList<AgentActionDescriptor> descriptors = AgentActions.Registry.GetAll();
                foreach (AgentActionRegistryIssue issue in AgentActions.Registry.GetIssues())
                    issues.Add(new CapabilityIssue("Action Registry", $"[{issue.Code}] {issue.Message}"));

                NovaProjectActionExposureSnapshot exposure = NovaProjectActionGateway.GetExposureSnapshot();
                if (!exposure.IsAvailable)
                    issues.Add(new CapabilityIssue("Nova MCP", exposure.ErrorMessage ?? "MCP Action 开放策略当前不可用。"));

                var policyIds = new HashSet<string>(exposure.PolicyActionIds ?? Array.Empty<string>(), StringComparer.Ordinal);
                var exposedIds = new HashSet<string>(exposure.ExposedActionIds ?? Array.Empty<string>(), StringComparer.Ordinal);
                var registeredIds = new HashSet<string>(descriptors.Select(item => item.Id), StringComparer.Ordinal);
                ValidateSkillActionLinks(skills, registeredIds, policyIds, issues);

                var actions = new List<ActionCapability>();
                foreach (AgentActionDescriptor descriptor in descriptors.OrderBy(item => item.Id, StringComparer.Ordinal))
                {
                    actions.Add(new ActionCapability
                    {
                        Descriptor = descriptor,
                        IsInMcpPolicy = policyIds.Contains(descriptor.Id),
                        IsMcpExposed = exposedIds.Contains(descriptor.Id),
                        SkillIds = skills
                            .Where(skill => skill.Adapters.Any(adapter =>
                                string.Equals(adapter.Kind, "agent-action", StringComparison.Ordinal) &&
                                string.Equals(adapter.Entry, descriptor.Id, StringComparison.Ordinal)))
                            .Select(skill => skill.Id)
                            .OrderBy(item => item, StringComparer.Ordinal)
                            .ToArray(),
                        BlockedSkillIds = skills
                            .Where(skill => skill.Adapters.Any(adapter =>
                                string.Equals(adapter.Kind, "agent-action-blocked", StringComparison.Ordinal) &&
                                string.Equals(adapter.Entry, descriptor.Id, StringComparison.Ordinal)))
                            .Select(skill => skill.Id)
                            .OrderBy(item => item, StringComparer.Ordinal)
                            .ToArray(),
                    });
                }

                return new Snapshot(
                    skills.AsReadOnly(),
                    actions.AsReadOnly(),
                    issues.AsReadOnly(),
                    projection.FrameworkVersion,
                    exposure.IsAvailable,
                    exposure.ErrorMessage);
            }

            private static string ResolveAgentsRoot(List<CapabilityIssue> issues)
            {
                try
                {
                    return AgentSkills.ResolveAgentsRoot();
                }
                catch (Exception exception)
                {
                    issues.Add(new CapabilityIssue("Skill Catalog", exception.Message));
                    return null;
                }
            }

            private static bool ValidateCatalog(string agentsRoot, List<CapabilityIssue> issues)
            {
                if (string.IsNullOrEmpty(agentsRoot))
                    return false;

                try
                {
                    AgentSkills.ValidateCatalogForDiscovery(agentsRoot);
                    return true;
                }
                catch (Exception exception)
                {
                    issues.Add(new CapabilityIssue("Skill Catalog", exception.Message));
                    return false;
                }
            }

            private static ProjectionView InspectProjection()
            {
                var result = new ProjectionView();
                try
                {
                    AgentSkills.ReconcileResult reconcile = AgentSkills.Reconcile(true);
                    result.CatalogValidated = true;
                    result.FrameworkVersion = reconcile.PackageVersion;
                    foreach (string id in reconcile.Unchanged)
                        result.Statuses[id] = ProjectionStatus.Current;
                    foreach (string id in reconcile.Added)
                        result.Statuses[id] = ProjectionStatus.Missing;
                    foreach (string id in reconcile.Updated)
                        result.Statuses[id] = ProjectionStatus.UpdateAvailable;
                    foreach (AgentSkills.ReconcileConflict conflict in reconcile.Conflicts)
                    {
                        result.Statuses[conflict.Id] = ProjectionStatus.Conflict;
                        result.Messages[conflict.Id] = ToProjectionMessage(conflict.Reason);
                    }
                }
                catch (Exception exception)
                {
                    result.ErrorMessage = exception.Message;
                }

                return result;
            }

            private static string ReadFrameworkVersion(string agentsRoot, List<CapabilityIssue> issues)
            {
                if (string.IsNullOrEmpty(agentsRoot))
                    return null;

                string packagePath = Path.Combine(Path.GetDirectoryName(agentsRoot) ?? string.Empty, "package.json");
                try
                {
                    return (string)JObject.Parse(File.ReadAllText(packagePath))["version"];
                }
                catch (Exception exception)
                {
                    issues.Add(new CapabilityIssue("Framework Package", $"无法读取版本：{exception.Message}"));
                    return null;
                }
            }

            private static List<SkillCapability> ReadSkills(
                string agentsRoot,
                ProjectionView projection,
                List<CapabilityIssue> issues)
            {
                var skills = new List<SkillCapability>();
                if (string.IsNullOrEmpty(agentsRoot))
                    return skills;

                string catalogPath = Path.Combine(agentsRoot, "catalog.json");
                try
                {
                    JObject catalog = JObject.Parse(File.ReadAllText(catalogPath));
                    var groupsBySkill = BuildGroupsBySkill(catalog["capabilityGroups"] as JObject);
                    foreach (JObject entry in (catalog["skills"] as JArray ?? new JArray()).OfType<JObject>())
                    {
                        string id = (string)entry["id"];
                        if (string.IsNullOrEmpty(id))
                        {
                            issues.Add(new CapabilityIssue("Skill Catalog", "发现缺少 id 的 Skill 条目。"));
                            continue;
                        }

                        try
                        {
                            skills.Add(ReadSkill(agentsRoot, entry, groupsBySkill, projection));
                        }
                        catch (Exception exception)
                        {
                            issues.Add(new CapabilityIssue(id, exception.Message));
                        }
                    }
                }
                catch (Exception exception)
                {
                    issues.Add(new CapabilityIssue("Skill Catalog", $"无法读取 {catalogPath}：{exception.Message}"));
                }

                return skills.OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
            }

            private static SkillCapability ReadSkill(
                string agentsRoot,
                JObject entry,
                IReadOnlyDictionary<string, List<string>> groupsBySkill,
                ProjectionView projection)
            {
                string id = (string)entry["id"];
                string root = Path.GetFullPath(agentsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                              + Path.DirectorySeparatorChar;
                string directory = Path.GetFullPath(Path.Combine(root, (string)entry["path"]));
                if (!directory.StartsWith(root, StringComparison.Ordinal) ||
                    (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException($"{id} 的 Skill 路径越界或包含不安全链接。");
                }
                string skillPath = Path.Combine(directory, "SKILL.md");
                string contractPath = Path.Combine(directory, "references", "contract.json");
                if ((File.GetAttributes(skillPath) & FileAttributes.ReparsePoint) != 0 ||
                    (File.GetAttributes(contractPath) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException($"{id} 的展示文件包含不安全链接。");
                }
                JObject contract = JObject.Parse(File.ReadAllText(contractPath));
                var adapters = new List<ActionAdapter>();
                foreach (JObject adapter in (contract["actionAdapters"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    adapters.Add(new ActionAdapter
                    {
                        Kind = (string)adapter["kind"],
                        Entry = (string)adapter["entry"],
                        When = (string)adapter["when"],
                    });
                }

                ProjectionStatus projectionStatus = projection.Statuses.TryGetValue(id, out ProjectionStatus value)
                    ? value
                    : ProjectionStatus.Unknown;
                return new SkillCapability
                {
                    Id = id,
                    Description = ReadFrontmatterDescription(skillPath),
                    Kind = (string)entry["kind"],
                    Status = (string)entry["status"],
                    MinimumEvidence = (string)contract["minimumEvidence"],
                    SkillFilePath = skillPath,
                    ContractFilePath = contractPath,
                    ConfirmationRule = (string)contract["confirmation"]?["rule"],
                    Projection = projectionStatus,
                    ProjectionMessage = projection.Messages.TryGetValue(id, out string message)
                        ? message
                        : ToProjectionMessage(projectionStatus),
                    Groups = groupsBySkill.TryGetValue(id, out List<string> groups)
                        ? groups.AsReadOnly()
                        : Array.Empty<string>(),
                    Journeys = ReadStrings(entry["journeys"]),
                    Effects = ReadStrings(contract["effects"]),
                    Inputs = ((contract["inputs"] as JArray) ?? new JArray())
                        .OfType<JObject>()
                        .Select(input => $"{(string)input["name"]}{((bool?)input["required"] == true ? "（必填）" : "（可选）")}")
                        .ToArray(),
                    Evidence = ReadStrings(contract["evidence"]),
                    Adapters = adapters.AsReadOnly(),
                };
            }

            private static Dictionary<string, List<string>> BuildGroupsBySkill(JObject groups)
            {
                var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                if (groups == null)
                    return result;

                foreach (JProperty group in groups.Properties())
                {
                    foreach (string id in ReadStrings(group.Value))
                    {
                        if (!result.TryGetValue(id, out List<string> skillGroups))
                        {
                            skillGroups = new List<string>();
                            result[id] = skillGroups;
                        }
                        skillGroups.Add(group.Name);
                    }
                }
                return result;
            }

            private static string ReadFrontmatterDescription(string skillPath)
            {
                foreach (string line in File.ReadLines(skillPath))
                {
                    if (line.StartsWith("description:", StringComparison.Ordinal))
                        return line.Substring("description:".Length).Trim();
                    if (line.StartsWith("#", StringComparison.Ordinal))
                        break;
                }
                return "未提供 Skill 功能说明。";
            }

            private static string[] ReadStrings(JToken token)
            {
                return token is JArray array
                    ? array.Values<string>().Where(value => !string.IsNullOrEmpty(value)).ToArray()
                    : Array.Empty<string>();
            }

            private static void ValidateSkillActionLinks(
                IEnumerable<SkillCapability> skills,
                HashSet<string> registeredIds,
                HashSet<string> policyIds,
                List<CapabilityIssue> issues)
            {
                foreach (SkillCapability skill in skills)
                {
                    foreach (ActionAdapter adapter in skill.Adapters)
                    {
                        if ((string.Equals(adapter.Kind, "agent-action", StringComparison.Ordinal) ||
                             string.Equals(adapter.Kind, "agent-action-blocked", StringComparison.Ordinal)) &&
                            !registeredIds.Contains(adapter.Entry))
                        {
                            issues.Add(new CapabilityIssue(skill.Id, $"引用的 Action 未注册：{adapter.Entry}"));
                        }
                        else if (string.Equals(adapter.Kind, "agent-action", StringComparison.Ordinal) &&
                                 !policyIds.Contains(adapter.Entry))
                        {
                            issues.Add(new CapabilityIssue(skill.Id,
                                $"声明为可调用的 Action 尚未进入 MCP 开放策略：{adapter.Entry}"));
                        }
                        else if (string.Equals(adapter.Kind, "agent-action-blocked", StringComparison.Ordinal) &&
                                 policyIds.Contains(adapter.Entry))
                        {
                            issues.Add(new CapabilityIssue(skill.Id,
                                $"声明为 blocked 的 Action 已进入 MCP 开放策略：{adapter.Entry}"));
                        }
                    }
                }
            }

            private static string ToProjectionMessage(ProjectionStatus status)
            {
                switch (status)
                {
                    case ProjectionStatus.Current: return "已投影，Agent 可发现";
                    case ProjectionStatus.Missing: return "尚未投影到当前项目";
                    case ProjectionStatus.UpdateAvailable: return "项目投影可更新";
                    case ProjectionStatus.Conflict: return "项目投影存在冲突";
                    default: return "未能确认项目投影状态";
                }
            }

            private static string ToProjectionMessage(string reason)
            {
                switch (reason)
                {
                    case "modified-managed": return "项目中的受管 Skill 已被修改，Nova 不会自动覆盖";
                    case "unowned-collision": return "项目中存在同名但不受 Nova 管理的 Skill";
                    case "missing-managed": return "受管 Skill 已被项目删除，Nova 不会自动重建";
                    case "unsafe-link": return "Skill 路径包含不安全链接";
                    default: return $"项目投影冲突：{reason}";
                }
            }

            private sealed class ProjectionView
            {
                public bool CatalogValidated;
                public string FrameworkVersion;
                public string ErrorMessage;
                public readonly Dictionary<string, ProjectionStatus> Statuses =
                    new Dictionary<string, ProjectionStatus>(StringComparer.Ordinal);
                public readonly Dictionary<string, string> Messages =
                    new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }
    }
}
