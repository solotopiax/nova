/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentSkills.Cli.cs
 * author:    taoye
 * created:   2026/8/13
 * descrip:   Nova Project Skills batchmode 显式恢复入口
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AgentSkills
        {
            /// <summary>
            /// 仅供 headless/CI 使用的全量 reconcile 入口。
            /// C# 调用可使用本嵌套类型；Unity batchmode 请使用 <see cref="NovaProjectSkillsCli"/> 顶层入口。
            /// </summary>
            public static class Cli
            {
                /// <summary>
                /// 执行当前消费项目的 Framework Skills reconcile，并用进程退出码报告成功或失败。
                /// </summary>
                public static void Reconcile()
                {
                    try
                    {
                        ReconcileResult result = AgentSkills.Reconcile();
                        if (result.Conflicts.Count > 0)
                        {
                            Debug.LogWarning($"[Nova Project Skills][CLI] 保留本地冲突：{BuildConflictSummary(result.Conflicts)}");
                        }

                        // 部分完成已保留项目本地内容，不能伪装成可供 CI 继续执行的完整成功。
                        EditorApplication.Exit(result.Status == "partial" ? 1 : 0);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"[Nova Project Skills][CLI] reconcile 失败：{exception}");
                        EditorApplication.Exit(2);
                    }
                }

                /// <summary>
                /// 将结构化冲突压缩为 CLI 日志可直接检索的 id:reason 文本。
                /// </summary>
                /// <param name="conflicts">本次 reconcile 保留的冲突集合。</param>
                /// <returns>逗号分隔的稳定冲突摘要。</returns>
                private static string BuildConflictSummary(IReadOnlyList<ReconcileConflict> conflicts)
                {
                    var builder = new StringBuilder();
                    for (int index = 0; index < conflicts.Count; index++)
                    {
                        if (index > 0)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(conflicts[index].Id);
                        builder.Append(':');
                        builder.Append(conflicts[index].Reason);
                    }

                    return builder.ToString();
                }
            }
        }
    }

    /// <summary>
    /// 提供 Unity <c>-executeMethod</c> 可解析的 Nova Project Skills 顶层 batchmode 入口。
    /// </summary>
    public static class NovaProjectSkillsCli
    {
        /// <summary>
        /// 执行当前消费项目的 Framework Skills reconcile。
        /// 此入口只转发给既有 CLI，实现统一的冲突日志与进程退出码语义。
        /// 用法：<c>unity -batchmode -executeMethod NovaFramework.Editor.NovaProjectSkillsCli.Reconcile</c>。
        /// </summary>
        public static void Reconcile()
        {
            EditorUtil.AgentSkills.Cli.Reconcile();
        }
    }
}
