/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentCapabilities.Models.cs
 * author:    taoye
 * created:   2026/8/25
 * descrip:   Nova Agent 能力总览只读模型
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AgentCapabilities
        {
            public enum ProjectionStatus
            {
                Current,
                Missing,
                UpdateAvailable,
                Conflict,
                Unknown,
            }

            public sealed class Snapshot
            {
                internal Snapshot(
                    IReadOnlyList<SkillCapability> skills,
                    IReadOnlyList<ActionCapability> actions,
                    IReadOnlyList<CapabilityIssue> issues,
                    string frameworkVersion,
                    bool mcpAvailable,
                    string mcpError)
                {
                    Skills = skills;
                    Actions = actions;
                    Issues = issues;
                    FrameworkVersion = frameworkVersion;
                    McpAvailable = mcpAvailable;
                    McpError = mcpError;
                    McpExposedActionCount = 0;
                    foreach (ActionCapability action in actions)
                    {
                        if (action.IsMcpExposed)
                            McpExposedActionCount++;
                    }
                }

                public IReadOnlyList<SkillCapability> Skills { get; }
                public IReadOnlyList<ActionCapability> Actions { get; }
                public IReadOnlyList<CapabilityIssue> Issues { get; }
                public string FrameworkVersion { get; }
                public bool McpAvailable { get; }
                public string McpError { get; }
                public int McpExposedActionCount { get; }
            }

            public sealed class SkillCapability
            {
                public string Id { get; internal set; }
                public string Description { get; internal set; }
                public string Kind { get; internal set; }
                public string Status { get; internal set; }
                public string MinimumEvidence { get; internal set; }
                public string SkillFilePath { get; internal set; }
                public string ContractFilePath { get; internal set; }
                public string ConfirmationRule { get; internal set; }
                public ProjectionStatus Projection { get; internal set; }
                public string ProjectionMessage { get; internal set; }
                public IReadOnlyList<string> Groups { get; internal set; } = Array.Empty<string>();
                public IReadOnlyList<string> Journeys { get; internal set; } = Array.Empty<string>();
                public IReadOnlyList<string> Effects { get; internal set; } = Array.Empty<string>();
                public IReadOnlyList<string> Inputs { get; internal set; } = Array.Empty<string>();
                public IReadOnlyList<string> Evidence { get; internal set; } = Array.Empty<string>();
                public IReadOnlyList<ActionAdapter> Adapters { get; internal set; } = Array.Empty<ActionAdapter>();
            }

            public sealed class ActionAdapter
            {
                public string Kind { get; internal set; }
                public string Entry { get; internal set; }
                public string When { get; internal set; }
            }

            public sealed class ActionCapability
            {
                public AgentActionDescriptor Descriptor { get; internal set; }
                public bool IsInMcpPolicy { get; internal set; }
                public bool IsMcpExposed { get; internal set; }
                public IReadOnlyList<string> SkillIds { get; internal set; } = Array.Empty<string>();
                public IReadOnlyList<string> BlockedSkillIds { get; internal set; } = Array.Empty<string>();
            }

            public sealed class CapabilityIssue
            {
                internal CapabilityIssue(string source, string message)
                {
                    Source = source;
                    Message = message;
                }

                public string Source { get; }
                public string Message { get; }
            }
        }
    }
}
