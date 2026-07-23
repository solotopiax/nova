/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ProjectGuard.Models.cs
 * author:    taoye
 * created:   2026/7/15
 * descrip:   Nova 项目规范守卫报告模型
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Nova 项目结构的集中式只读校验入口。
        /// </summary>
        public static partial class ProjectGuard
        {
            public enum NovaGuardProfile
            {
                Quick,
                Play,
                Build,
                Release,
            }

            public enum NovaGuardSeverity
            {
                Info,
                Warning,
                Error,
            }

            public sealed class NovaGuardIssue
            {
                public NovaGuardIssue(string ruleId, NovaGuardSeverity severity, string message,
                    string assetPath = null)
                {
                    RuleId = ruleId ?? throw new ArgumentNullException(nameof(ruleId));
                    Severity = severity;
                    Message = message ?? throw new ArgumentNullException(nameof(message));
                    AssetPath = assetPath ?? string.Empty;
                }

                public string RuleId { get; }
                public NovaGuardSeverity Severity { get; }
                public string Message { get; }
                public string AssetPath { get; }
            }

            public sealed class NovaGuardReport
            {
                private readonly List<NovaGuardIssue> m_Issues = new();

                public ReadOnlyCollection<NovaGuardIssue> Issues => m_Issues
                    .OrderByDescending(issue => issue.Severity)
                    .ThenBy(issue => issue.RuleId, StringComparer.Ordinal)
                    .ThenBy(issue => issue.AssetPath, StringComparer.Ordinal)
                    .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                    .ToList()
                    .AsReadOnly();

                public bool HasErrors => m_Issues.Any(issue => issue.Severity == NovaGuardSeverity.Error);

                public void Add(NovaGuardIssue issue)
                {
                    if (issue == null)
                        throw new ArgumentNullException(nameof(issue));

                    m_Issues.Add(issue);
                }
            }
        }
    }
}
