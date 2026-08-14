/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentSkills.cs
 * author:    taoye
 * created:   2026/8/13
 * descrip:   Nova Project Skills 自动发现入口与结果模型
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Nova Project Skills 自动发现入口。
        /// </summary>
        public static partial class AgentSkills
        {
            /// <summary>
            /// Framework UPM 固定包名。
            /// </summary>
            private const string c_FrameworkPackageName = "com.solotopia.nova.framework";

            /// <summary>
            /// Framework 包内 Agents 目录名。
            /// </summary>
            private const string c_AgentsDirectoryName = "Agents";

            /// <summary>
            /// 单个未被自动覆盖的受管投影冲突。
            /// </summary>
            public sealed class ReconcileConflict
            {
                /// <summary>
                /// 创建一个结构化冲突结果，供交互式 Editor、CLI 和调用方统一处理。
                /// </summary>
                /// <param name="id">发生冲突的项目组 Skill id。</param>
                /// <param name="reason">不会自动覆盖的稳定原因码。</param>
                internal ReconcileConflict(string id, string reason)
                {
                    Id = id;
                    Reason = reason;
                }

                /// <summary>
                /// 获取发生冲突的项目组 Skill id。
                /// </summary>
                public string Id { get; }

                /// <summary>
                /// 获取不会自动覆盖的稳定原因码。
                /// </summary>
                public string Reason { get; }
            }

            /// <summary>
            /// 当前消费项目的全量自动 reconcile 结果。
            /// </summary>
            public sealed class ReconcileResult
            {
                /// <summary>
                /// 构造一次 reconcile 的不可变结果快照。
                /// </summary>
                /// <param name="agentsRoot">本次使用的 Agents 真源绝对路径。</param>
                /// <param name="packageVersion">Framework 包版本。</param>
                /// <param name="added">新增的受管 Skill。</param>
                /// <param name="updated">更新的未修改受管 Skill。</param>
                /// <param name="removed">删除的上游已移除且本地未修改的 Skill。</param>
                /// <param name="unchanged">内容未变化的受管 Skill。</param>
                /// <param name="conflicts">未覆盖的局部结构化冲突。</param>
                /// <param name="dryRun">是否只规划未写入。</param>
                internal ReconcileResult(
                    string agentsRoot,
                    string packageVersion,
                    IReadOnlyList<string> added,
                    IReadOnlyList<string> updated,
                    IReadOnlyList<string> removed,
                    IReadOnlyList<string> unchanged,
                    IReadOnlyList<ReconcileConflict> conflicts,
                    bool dryRun)
                {
                    AgentsRoot = agentsRoot;
                    PackageVersion = packageVersion;
                    Added = added;
                    Updated = updated;
                    Removed = removed;
                    Unchanged = unchanged;
                    Conflicts = conflicts;
                    DryRun = dryRun;
                }

                /// <summary>
                /// 获取本次使用的 Agents 真源绝对路径。
                /// </summary>
                public string AgentsRoot { get; }

                /// <summary>
                /// 获取本次使用的 Framework 包版本。
                /// </summary>
                public string PackageVersion { get; }

                /// <summary>
                /// 获取新增的受管 Skill id。
                /// </summary>
                public IReadOnlyList<string> Added { get; }

                /// <summary>
                /// 获取更新的受管 Skill id。
                /// </summary>
                public IReadOnlyList<string> Updated { get; }

                /// <summary>
                /// 获取删除的受管 Skill id。
                /// </summary>
                public IReadOnlyList<string> Removed { get; }

                /// <summary>
                /// 获取未变更的受管 Skill id。
                /// </summary>
                public IReadOnlyList<string> Unchanged { get; }

                /// <summary>
                /// 获取未自动覆盖的结构化冲突项。
                /// </summary>
                public IReadOnlyList<ReconcileConflict> Conflicts { get; }

                /// <summary>
                /// 获取本次是否只进行了规划。
                /// </summary>
                public bool DryRun { get; }

                /// <summary>
                /// 获取本次 reconcile 的最终结果状态；存在未自动覆盖的冲突时为 partial。
                /// </summary>
                public string Status => Conflicts.Count == 0 ? "success" : "partial";
            }

            /// <summary>
            /// 根据当前 Unity 项目解析到的 Framework 包执行全量 reconcile。
            /// </summary>
            /// <param name="dryRun">为 true 时仅返回规划结果，不写入项目。</param>
            /// <returns>本次 reconcile 结果。</returns>
            public static ReconcileResult Reconcile(bool dryRun = false)
            {
                string projectRoot = ResolveProjectRoot();
                if (!dryRun)
                {
                    // 恢复只依赖消费者项目内 journal；必须先于当前包真源解析执行，
                    // 否则升级瞬间包路径不可用会永久阻塞已登记事务的收敛。
                    ResumePendingTransactionForProject(projectRoot);
                }

                return ReconcileForProject(projectRoot, ResolveAgentsRoot(), dryRun);
            }

            /// <summary>
            /// 对指定消费项目和 Agents 真源执行全量 reconcile。
            /// 此入口供 batch/CI 和定向 Editor 测试使用，项目成员日常不需要手动调用。
            /// </summary>
            /// <param name="projectRoot">消费项目根目录。</param>
            /// <param name="agentsRoot">已解析 Framework 包中的 Agents 真源目录。</param>
            /// <param name="dryRun">为 true 时仅返回规划结果，不写入项目。</param>
            /// <returns>本次 reconcile 结果。</returns>
            public static ReconcileResult ReconcileForProject(string projectRoot, string agentsRoot, bool dryRun = false)
            {
                return ReconcileInternal(projectRoot, agentsRoot, dryRun);
            }

            /// <summary>
            /// 解析当前 Unity 消费项目的物理根目录。
            /// </summary>
            /// <returns>项目根目录绝对路径。</returns>
            private static string ResolveProjectRoot()
            {
                return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            }

            /// <summary>
            /// 优先从 PackageInfo 解析包根；开发态回退到 Assets/Framework/Agents。
            /// </summary>
            /// <returns>可用的 Agents 真源绝对路径。</returns>
            private static string ResolveAgentsRoot()
            {
                PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(EditorUtil).Assembly);
                if (packageInfo != null)
                {
                    if (!string.Equals(packageInfo.name, c_FrameworkPackageName, StringComparison.Ordinal)
                        || string.IsNullOrEmpty(packageInfo.version))
                    {
                        throw new InvalidOperationException("当前 Editor 程序集未解析到有效的 Nova Framework 包身份。");
                    }

                    if (string.IsNullOrEmpty(packageInfo.resolvedPath))
                    {
                        throw new InvalidOperationException("当前 Nova Framework 包缺少可用的 resolvedPath。");
                    }

                    string packageAgentsRoot = Path.Combine(packageInfo.resolvedPath, c_AgentsDirectoryName);
                    if (Directory.Exists(packageAgentsRoot))
                    {
                        return Path.GetFullPath(packageAgentsRoot);
                    }

                    throw new InvalidOperationException("当前 Nova Framework 包未包含 Agents 真源目录。");
                }

                string developmentAgentsRoot = Path.Combine(Application.dataPath, "Framework", c_AgentsDirectoryName);
                if (Directory.Exists(developmentAgentsRoot))
                {
                    return Path.GetFullPath(developmentAgentsRoot);
                }

                throw new InvalidOperationException("未找到当前 Nova Framework 包的 Agents 真源目录。");
            }
        }
    }
}
