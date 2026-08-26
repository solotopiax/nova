/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentSkills.Reconciler.cs
 * author:    taoye
 * created:   2026/8/13
 * descrip:   Nova Project Skills 全量安全桥接实现
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AgentSkills
        {
            /// <summary>
            /// 消费项目受管状态文件名。
            /// </summary>
            private const string c_StateFileName = "nova-skills.lock.json";

            /// <summary>
            /// 可恢复事务日志文件名。
            /// </summary>
            private const string c_TransactionFileName = "nova-skills.transaction.json";

            /// <summary>
            /// 事务 staging 根目录名。
            /// </summary>
            private const string c_StagingDirectoryName = ".nova-skills-staging";

            /// <summary>
            /// Python 与 Editor bridge 共用的 Library 临时目录锁文件名。
            /// </summary>
            private const string c_SyncLockFileName = ".nova-skills-sync.lock";

            /// <summary>
            /// Unity 消费项目的本地临时目录名。
            /// </summary>
            private const string c_LibraryDirectoryName = "Library";

            /// <summary>
            /// Library 中 Nova 专属临时目录名。
            /// </summary>
            private const string c_LibraryNovaDirectoryName = "Nova";

            /// <summary>
            /// Library/Nova 下 Skills bridge 专属临时目录名。
            /// </summary>
            private const string c_LibraryAgentSkillsDirectoryName = "AgentSkills";

            /// <summary>
            /// 首次正式全量分发 Catalog 的 schema 版本。
            /// </summary>
            private const int c_CatalogSchemaVersion = 1;

            /// <summary>
            /// 首次正式受管状态 schema 版本。
            /// </summary>
            private const int c_StateSchemaVersion = 1;

            /// <summary>
            /// 首次正式事务 schema 版本。
            /// </summary>
            private const int c_TransactionSchemaVersion = 1;

            /// <summary>
            /// Catalog 文件名。
            /// </summary>
            private const string c_CatalogFileName = "catalog.json";

            /// <summary>
            /// 受管项目根目录中的 Agent 配置目录名。
            /// </summary>
            private const string c_AgentsProjectionDirectoryName = ".agents";

            /// <summary>
            /// 受管可发现 Skill 子目录名。
            /// </summary>
            private const string c_SkillsDirectoryName = "skills";

            /// <summary>
            /// Nova 自动 bridge 唯一可以取得所有权的项目组 Skill id 前缀。
            /// </summary>
            private const string c_ProjectSkillIdPrefix = "nova-project-";

            /// <summary>
            /// 所有项目组 Skill 首个正文段落必须声明的共同底线。
            /// </summary>
            private const string c_ProjectSkillCommonBaseline =
                "触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。";

            /// <summary>
            /// 项目组 Skill 声明按需加载资料时使用的固定章节标题。
            /// </summary>
            private const string c_ProgressiveDisclosureHeading = "## 渐进式披露";

            /// <summary>
            /// 项目组 Skill 允许的 id 正则。
            /// </summary>
            private static readonly Regex s_SkillIdPattern = new Regex(
                "^[a-z0-9]+(?:-[a-z0-9]+)*$",
                RegexOptions.Compiled);

            /// <summary>
            /// SHA-256 十六进制字符串正则。
            /// </summary>
            private static readonly Regex s_Sha256Pattern = new Regex(
                "^[0-9a-f]{64}$",
                RegexOptions.Compiled);

            /// <summary>
            /// SKILL.md frontmatter 中 name 字段的严格解析规则。
            /// </summary>
            private static readonly Regex s_FrontmatterNamePattern = new Regex(
                "^name:\\s*([^\\s#]+)\\s*$",
                RegexOptions.Compiled);

            /// <summary>
            /// 同一 Editor 进程中的临界区闸门；补足 POSIX 文件记录锁的同进程可重入语义。
            /// </summary>
            private static readonly object s_ProjectionLockGate = new object();

            /// <summary>
            /// Catalog 和 contract 允许的 Skill 类型集合。
            /// </summary>
            private static readonly HashSet<string> s_SkillKinds = new HashSet<string>(StringComparer.Ordinal)
            {
                "router",
                "operation",
                "workflow",
            };

            /// <summary>
            /// Catalog 和 contract 允许声明的副作用集合。
            /// </summary>
            private static readonly HashSet<string> s_SkillEffects = new HashSet<string>(StringComparer.Ordinal)
            {
                "read",
                "workspace-write",
                "unity-write",
                "generated-output",
                "build",
            };

            /// <summary>
            /// Catalog 和 contract 允许的最低证据等级集合。
            /// </summary>
            private static readonly HashSet<string> s_MinimumEvidenceLevels = new HashSet<string>(StringComparer.Ordinal)
            {
                "static",
                "compile",
                "play",
                "bundle-build",
                "player-build",
            };

            /// <summary>
            /// contract 可声明的固定 Action Adapter 类型集合。
            /// </summary>
            private static readonly HashSet<string> s_ActionAdapterKinds = new HashSet<string>(StringComparer.Ordinal)
            {
                "agent-action",
                "agent-action-blocked",
                "csharp-api",
                "cli",
                "pipify",
                "unity-editor-api",
                "unity-editor-automation",
                "unity-menu",
                "workspace-edit",
                "workspace-inspection",
            };

            /// <summary>
            /// 单个 Action Adapter 必须且只能包含的字段。
            /// </summary>
            private static readonly HashSet<string> s_ActionAdapterFields = new HashSet<string>(StringComparer.Ordinal)
            {
                "kind",
                "entry",
                "when",
            };

            /// <summary>
            /// contract 可声明的幂等性策略集合。
            /// </summary>
            private static readonly HashSet<string> s_ContractIdempotency = new HashSet<string>(StringComparer.Ordinal)
            {
                "read-only",
                "ensure-state",
                "orchestrate",
            };

            /// <summary>
            /// contract 可声明的结果状态集合。
            /// </summary>
            private static readonly HashSet<string> s_ContractResultStates = new HashSet<string>(StringComparer.Ordinal)
            {
                "success",
                "partial",
                "blocked",
                "not_applicable",
            };

            /// <summary>
            /// 当前 Catalog 顶层允许字段，必须与随包 schema 保持一致。
            /// </summary>
            private static readonly HashSet<string> s_CatalogFields = new HashSet<string>(StringComparer.Ordinal)
            {
                "schemaVersion",
                "package",
                "capabilityGroups",
                "skills",
            };

            /// <summary>
            /// 当前 Catalog 顶层必填字段。
            /// </summary>
            private static readonly HashSet<string> s_RequiredCatalogFields = new HashSet<string>(StringComparer.Ordinal)
            {
                "schemaVersion",
                "package",
                "skills",
            };

            /// <summary>
            /// 当前 Catalog 单个 Skill 条目允许字段。
            /// </summary>
            private static readonly HashSet<string> s_CatalogSkillFields = new HashSet<string>(StringComparer.Ordinal)
            {
                "id",
                "path",
                "kind",
                "status",
                "journeys",
                "effects",
                "minimumEvidence",
                "replacedBy",
            };

            /// <summary>
            /// 当前 Catalog 单个 Skill 条目必填字段。
            /// </summary>
            private static readonly HashSet<string> s_RequiredCatalogSkillFields = new HashSet<string>(StringComparer.Ordinal)
            {
                "id",
                "path",
                "kind",
                "status",
                "journeys",
                "effects",
                "minimumEvidence",
            };

            /// <summary>
            /// 当前 Catalog 支持的 Skill 生命周期状态。
            /// </summary>
            private static readonly HashSet<string> s_CatalogSkillStatuses = new HashSet<string>(StringComparer.Ordinal)
            {
                "experimental",
                "stable",
                "deprecated",
            };

            /// <summary>
            /// 受管事务的固定顶层字段集合。
            /// </summary>
            private static readonly HashSet<string> s_TransactionFields = new HashSet<string>(StringComparer.Ordinal)
            {
                "schemaVersion",
                "transactionId",
                "previousState",
                "finalState",
                "pending",
            };

            /// <summary>
            /// 受管状态的固定字段集合。
            /// </summary>
            private static readonly HashSet<string> s_StateFields = new HashSet<string>(StringComparer.Ordinal)
            {
                "schemaVersion",
                "package",
                "packageVersion",
                "catalogHash",
                "managed",
            };

            /// <summary>
            /// 判断 id 是否位于 Nova 可以管理的项目组 Skill 命名空间。
            /// </summary>
            /// <param name="skillId">待验证的 Skill id。</param>
            /// <returns>id 合法且以 `nova-project-` 开头时返回 true。</returns>
            private static bool IsManagedProjectSkillId(string skillId)
            {
                return !string.IsNullOrEmpty(skillId)
                    && s_SkillIdPattern.IsMatch(skillId)
                    && skillId.StartsWith(c_ProjectSkillIdPrefix, StringComparison.Ordinal);
            }

            /// <summary>
            /// Catalog 中单个可发现 Skill 的最小信息。
            /// </summary>
            private sealed class CatalogSkill
            {
                /// <summary>
                /// 初始化 Catalog Skill。
                /// </summary>
                /// <param name="id">Skill id。</param>
                /// <param name="relativePath">相对 Agents 根的目录路径。</param>
                public CatalogSkill(string id, string relativePath)
                {
                    Id = id;
                    RelativePath = relativePath;
                }

                /// <summary>
                /// 获取 Skill id。
                /// </summary>
                public string Id { get; }

                /// <summary>
                /// 获取相对 Agents 根的目录路径。
                /// </summary>
                public string RelativePath { get; }
            }

            /// <summary>
            /// 事务中一个新增、更新或删除动作的运行期描述。
            /// </summary>
            private sealed class PlannedAction
            {
                /// <summary>
                /// 初始化计划动作。
                /// </summary>
                /// <param name="action">动作类型，值为 add、update 或 remove。</param>
                /// <param name="skillId">Skill id。</param>
                /// <param name="sourceDirectory">新增/更新时的真源目录；删除时为 null。</param>
                /// <param name="sourceHash">新增/更新时的真源 hash；删除时为 null。</param>
                /// <param name="previousTargetHash">更新/删除前的受管目标 hash；新增时为 null。</param>
                public PlannedAction(
                    string action,
                    string skillId,
                    string sourceDirectory,
                    string sourceHash,
                    string previousTargetHash)
                {
                    Action = action;
                    SkillId = skillId;
                    SourceDirectory = sourceDirectory;
                    SourceHash = sourceHash;
                    PreviousTargetHash = previousTargetHash;
                }

                /// <summary>
                /// 获取动作类型。
                /// </summary>
                public string Action { get; }

                /// <summary>
                /// 获取 Skill id。
                /// </summary>
                public string SkillId { get; }

                /// <summary>
                /// 获取新增/更新真源目录；删除时为 null。
                /// </summary>
                public string SourceDirectory { get; }

                /// <summary>
                /// 获取新增/更新真源 hash；删除时为 null。
                /// </summary>
                public string SourceHash { get; }

                /// <summary>
                /// 获取更新/删除前的目标 hash；新增时为 null。
                /// </summary>
                public string PreviousTargetHash { get; }
            }

            /// <summary>
            /// 单次 reconcile 的冻结计划，保证写入前的输入集合和最终 state 相互对应。
            /// </summary>
            private sealed class ReconcilePlan
            {
                /// <summary>
                /// 初始化冻结计划。
                /// </summary>
                /// <param name="targetRoot">项目可发现 Skill 根目录。</param>
                /// <param name="statePath">受管 state 路径。</param>
                /// <param name="previousState">规划前 state；首次投影时为 null。</param>
                /// <param name="finalState">计划成功后的受管 state。</param>
                /// <param name="actions">需要事务执行的动作。</param>
                /// <param name="result">面向调用方的结果快照。</param>
                public ReconcilePlan(
                    string targetRoot,
                    string statePath,
                    JObject previousState,
                    JObject finalState,
                    List<PlannedAction> actions,
                    ReconcileResult result)
                {
                    TargetRoot = targetRoot;
                    StatePath = statePath;
                    PreviousState = previousState;
                    FinalState = finalState;
                    Actions = actions;
                    Result = result;
                }

                /// <summary>
                /// 获取项目可发现 Skill 根目录。
                /// </summary>
                public string TargetRoot { get; }

                /// <summary>
                /// 获取 state 文件路径。
                /// </summary>
                public string StatePath { get; }

                /// <summary>
                /// 获取规划前 state；首次投影时为 null。
                /// </summary>
                public JObject PreviousState { get; }

                /// <summary>
                /// 获取计划成功后的 state。
                /// </summary>
                public JObject FinalState { get; }

                /// <summary>
                /// 获取事务执行动作。
                /// </summary>
                public List<PlannedAction> Actions { get; }

                /// <summary>
                /// 获取调用方结果快照。
                /// </summary>
                public ReconcileResult Result { get; }
            }

            /// <summary>
            /// 固定受管路径集合，先整体检查再写入以拒绝重解析点越权。
            /// </summary>
            private sealed class ManagedPaths
            {
                /// <summary>
                /// 初始化受管路径集合。
                /// </summary>
                /// <param name="agentsDirectory">项目根 .agents 目录。</param>
                /// <param name="targetRoot">项目根 .agents/skills 目录。</param>
                /// <param name="statePath">受管 state 路径。</param>
                /// <param name="transactionPath">事务日志路径。</param>
                /// <param name="stagingRoot">事务 staging 根目录。</param>
                /// <param name="lockPath">跨进程锁路径。</param>
                public ManagedPaths(
                    string agentsDirectory,
                    string targetRoot,
                    string statePath,
                    string transactionPath,
                    string stagingRoot,
                    string lockPath)
                {
                    AgentsDirectory = agentsDirectory;
                    TargetRoot = targetRoot;
                    StatePath = statePath;
                    TransactionPath = transactionPath;
                    StagingRoot = stagingRoot;
                    LockPath = lockPath;
                }

                /// <summary>
                /// 获取 .agents 目录。
                /// </summary>
                public string AgentsDirectory { get; }

                /// <summary>
                /// 获取 .agents/skills 目录。
                /// </summary>
                public string TargetRoot { get; }

                /// <summary>
                /// 获取 state 文件。
                /// </summary>
                public string StatePath { get; }

                /// <summary>
                /// 获取事务日志。
                /// </summary>
                public string TransactionPath { get; }

                /// <summary>
                /// 获取 staging 根目录。
                /// </summary>
                public string StagingRoot { get; }

                /// <summary>
                /// 获取跨进程锁文件。
                /// </summary>
                public string LockPath { get; }
            }

            /// <summary>
            /// 在固定文件首字节上持有跨语言排他锁。
            /// </summary>
            private sealed class ProjectionLock : IDisposable
            {
                private readonly FileStream m_Stream;
                private readonly bool m_OwnsProcessGate;

                /// <summary>
                /// 初始化已加锁文件流。
                /// </summary>
                /// <param name="stream">已持有首字节锁的文件流。</param>
                /// <param name="ownsProcessGate">是否持有本进程内同步闸门。</param>
                public ProjectionLock(FileStream stream, bool ownsProcessGate)
                {
                    m_Stream = stream;
                    m_OwnsProcessGate = ownsProcessGate;
                }

                /// <summary>
                /// 释放首字节锁并关闭文件流，不删除 inode。
                /// </summary>
                public void Dispose()
                {
                    try
                    {
                        m_Stream.Unlock(0, 1);
                    }
                    catch (IOException)
                    {
                        // 进程异常或平台已经释放锁时不掩盖后续资源释放。
                    }
                    finally
                    {
                        try
                        {
                            m_Stream.Dispose();
                        }
                        finally
                        {
                            if (m_OwnsProcessGate)
                            {
                                Monitor.Exit(s_ProjectionLockGate);
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// 执行全量 reconcile；正常调用路径会先续传旧事务，再冻结新计划。
            /// </summary>
            /// <param name="projectRoot">消费项目根目录。</param>
            /// <param name="agentsRoot">已解析 Framework 包中的 Agents 根目录。</param>
            /// <param name="dryRun">为 true 时只规划不写入。</param>
            /// <returns>本次 reconcile 结果。</returns>
            private static ReconcileResult ReconcileInternal(string projectRoot, string agentsRoot, bool dryRun)
            {
                string normalizedProjectRoot = NormalizeDirectory(projectRoot, "消费项目根目录");
                ManagedPaths paths = GetManagedPaths(normalizedProjectRoot);

                if (dryRun && File.Exists(paths.TransactionPath))
                {
                    throw new InvalidOperationException("存在未完成的 Nova Skill 投影事务；请先执行非 dry-run reconcile 恢复。");
                }

                if (!dryRun && File.Exists(paths.TransactionPath))
                {
                    using (AcquireProjectionLock(paths))
                    {
                        ResumeTransaction(normalizedProjectRoot, paths);
                    }
                }

                // 已登记事务的续传不依赖本轮 Framework 真源，必须先完成它；
                // 随后才允许包解析或 Catalog 校验失败阻断新的规划。
                string normalizedAgentsRoot = NormalizeDirectory(agentsRoot, "Agents 真源目录");

                if (dryRun)
                {
                    return BuildPlan(normalizedProjectRoot, normalizedAgentsRoot, true).Result;
                }

                using (AcquireProjectionLock(paths))
                {
                    paths = GetManagedPaths(normalizedProjectRoot);
                    ResumeTransaction(normalizedProjectRoot, paths);
                    ReconcilePlan plan = BuildPlan(normalizedProjectRoot, normalizedAgentsRoot, false);
                    if (plan.Actions.Count == 0)
                    {
                        JObject currentState = ReadJsonObjectIfExists(plan.StatePath);
                        EnsureStateUnchanged(currentState, plan.PreviousState, plan.FinalState, "规划后");
                        if (!JToken.DeepEquals(currentState, plan.FinalState))
                        {
                            WriteJsonAtomically(plan.StatePath, plan.FinalState);
                        }

                        return plan.Result;
                    }

                    BeginTransaction(normalizedProjectRoot, plan);
                    ResumeTransaction(normalizedProjectRoot, GetManagedPaths(normalizedProjectRoot));
                    return plan.Result;
                }
            }

            /// <summary>
            /// 仅凭消费项目内受管路径续传中断事务，供包真源暂不可解析的升级窗口优先收敛现场。
            /// </summary>
            /// <param name="projectRoot">消费项目根目录。</param>
            private static void ResumePendingTransactionForProject(string projectRoot)
            {
                string normalizedProjectRoot = NormalizeDirectory(projectRoot, "消费项目根目录");
                ManagedPaths paths = GetManagedPaths(normalizedProjectRoot);
                if (!File.Exists(paths.TransactionPath))
                {
                    return;
                }

                using (AcquireProjectionLock(paths))
                {
                    ResumeTransaction(normalizedProjectRoot, GetManagedPaths(normalizedProjectRoot));
                }
            }

            /// <summary>
            /// 构建当前项目的受管路径集合，并拒绝已有链接或错误文件类型。
            /// </summary>
            /// <param name="projectRoot">消费项目根目录。</param>
            /// <returns>已验证的受管路径集合。</returns>
            private static ManagedPaths GetManagedPaths(string projectRoot)
            {
                string agentsDirectory = Path.Combine(projectRoot, c_AgentsProjectionDirectoryName);
                string targetRoot = Path.Combine(agentsDirectory, c_SkillsDirectoryName);
                string statePath = Path.Combine(agentsDirectory, c_StateFileName);
                string transactionPath = Path.Combine(agentsDirectory, c_TransactionFileName);
                string stagingRoot = Path.Combine(agentsDirectory, c_StagingDirectoryName);
                string libraryDirectory = Path.Combine(projectRoot, c_LibraryDirectoryName);
                string novaLibraryDirectory = Path.Combine(libraryDirectory, c_LibraryNovaDirectoryName);
                string lockDirectory = Path.Combine(novaLibraryDirectory, c_LibraryAgentSkillsDirectoryName);
                string lockPath = Path.Combine(lockDirectory, c_SyncLockFileName);

                EnsureNotReparsePoint(agentsDirectory, ".agents");
                EnsureNotReparsePoint(targetRoot, ".agents/skills");
                EnsureNotReparsePoint(statePath, c_StateFileName);
                EnsureNotReparsePoint(transactionPath, c_TransactionFileName);
                EnsureNotReparsePoint(stagingRoot, c_StagingDirectoryName);
                EnsureNotReparsePoint(libraryDirectory, "Library");
                EnsureNotReparsePoint(novaLibraryDirectory, "Library/Nova");
                EnsureNotReparsePoint(lockDirectory, "Library/Nova/AgentSkills");
                EnsureNotReparsePoint(lockPath, "Library/Nova/AgentSkills/.nova-skills-sync.lock");
                EnsureExpectedDirectory(agentsDirectory, ".agents");
                EnsureExpectedDirectory(targetRoot, ".agents/skills");
                EnsureExpectedFile(statePath, c_StateFileName);
                EnsureExpectedFile(transactionPath, c_TransactionFileName);
                EnsureExpectedDirectory(stagingRoot, c_StagingDirectoryName);
                EnsureExpectedDirectory(libraryDirectory, "Library");
                EnsureExpectedDirectory(novaLibraryDirectory, "Library/Nova");
                EnsureExpectedDirectory(lockDirectory, "Library/Nova/AgentSkills");
                EnsureExpectedFile(lockPath, "Library/Nova/AgentSkills/.nova-skills-sync.lock");
                return new ManagedPaths(agentsDirectory, targetRoot, statePath, transactionPath, stagingRoot, lockPath);
            }

            /// <summary>
            /// 获取固定锁文件的跨语言字节范围锁。
            /// </summary>
            /// <param name="paths">已验证的受管路径集合。</param>
            /// <returns>释放时会解锁但保留 inode 的锁对象。</returns>
            private static ProjectionLock AcquireProjectionLock(ManagedPaths paths)
            {
                if (!Monitor.TryEnter(s_ProjectionLockGate))
                {
                    throw new InvalidOperationException("当前 Editor 进程已有 Nova Skill reconcile 正在进行，请等待其完成后再重试。");
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(paths.LockPath));
                    GetManagedPaths(Path.GetDirectoryName(paths.AgentsDirectory));
                    FileStream stream = new FileStream(
                        paths.LockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite);
                    try
                    {
                        stream.Lock(0, 1);
                    }
                    catch (IOException exception)
                    {
                        stream.Dispose();
                        throw new InvalidOperationException("另一个 Nova Skill reconcile 正在进行，请等待其完成后再重试。", exception);
                    }

                    try
                    {
                        WriteLockMetadata(stream);
                        return new ProjectionLock(stream, true);
                    }
                    catch
                    {
                        stream.Unlock(0, 1);
                        stream.Dispose();
                        throw;
                    }
                }
                catch
                {
                    Monitor.Exit(s_ProjectionLockGate);
                    throw;
                }
            }

            /// <summary>
            /// 写入锁持有者诊断信息；正确性只依赖字节范围锁而不依赖该文本。
            /// </summary>
            /// <param name="stream">已持有首字节锁的文件流。</param>
            private static void WriteLockMetadata(FileStream stream)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(
                    $"{{\"schemaVersion\":1,\"processId\":{System.Diagnostics.Process.GetCurrentProcess().Id},\"token\":\"{Guid.NewGuid():N}\"}}");
                stream.SetLength(0);
                stream.Position = 0;
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            /// <summary>
            /// 校验 Catalog 和 package 身份后构建全量 reconcile 计划。
            /// </summary>
            /// <param name="projectRoot">消费项目根目录。</param>
            /// <param name="agentsRoot">已解析 Agents 真源目录。</param>
            /// <param name="dryRun">是否只规划未写入。</param>
            /// <returns>冻结的 reconcile 计划。</returns>
            private static ReconcilePlan BuildPlan(string projectRoot, string agentsRoot, bool dryRun)
            {
                JObject catalog;
                JObject package;
                List<CatalogSkill> catalogSkills = LoadAndValidateCatalog(agentsRoot, out catalog, out package);
                ManagedPaths paths = GetManagedPaths(projectRoot);
                JObject previousState;
                JObject managed = ReadManagedStateForReconcile(paths.StatePath, out previousState);
                JObject finalManaged = (JObject)managed.DeepClone();
                var actions = new List<PlannedAction>();
                var added = new List<string>();
                var updated = new List<string>();
                var removed = new List<string>();
                var unchanged = new List<string>();
                var conflicts = new List<ReconcileConflict>();
                var catalogIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (CatalogSkill skill in catalogSkills)
                {
                    catalogIds.Add(skill.Id);
                    string sourceDirectory = GetSafeChild(agentsRoot, skill.RelativePath, skill.Id);
                    string targetDirectory = Path.Combine(paths.TargetRoot, skill.Id);
                    string sourceHash = ComputeTreeHash(sourceDirectory);
                    JToken managedEntry = managed[skill.Id];

                    if (IsReparsePoint(targetDirectory))
                    {
                        conflicts.Add(new ReconcileConflict(skill.Id, "unsafe-link"));
                        continue;
                    }

                    if (Directory.Exists(targetDirectory) || File.Exists(targetDirectory))
                    {
                        if (!Directory.Exists(targetDirectory))
                        {
                            conflicts.Add(new ReconcileConflict(skill.Id, managedEntry == null ? "unowned-collision" : "modified-managed"));
                            continue;
                        }

                        if (managedEntry == null)
                        {
                            conflicts.Add(new ReconcileConflict(skill.Id, "unowned-collision"));
                            continue;
                        }

                        string previousTargetHash = ReadManagedHash(managedEntry, "targetHash", skill.Id);
                        string previousSourceHash = ReadManagedHash(managedEntry, "sourceHash", skill.Id);
                        string targetHash;
                        try
                        {
                            targetHash = ComputeTreeHash(targetDirectory);
                        }
                        catch (InvalidOperationException)
                        {
                            conflicts.Add(new ReconcileConflict(skill.Id, "unsafe-link"));
                            continue;
                        }

                        if (!string.Equals(targetHash, previousTargetHash, StringComparison.Ordinal))
                        {
                            conflicts.Add(new ReconcileConflict(skill.Id, "modified-managed"));
                            continue;
                        }

                        if (string.Equals(sourceHash, previousSourceHash, StringComparison.Ordinal))
                        {
                            unchanged.Add(skill.Id);
                            continue;
                        }

                        actions.Add(new PlannedAction("update", skill.Id, sourceDirectory, sourceHash, previousTargetHash));
                        finalManaged[skill.Id] = CreateManagedEntry(sourceHash, sourceHash);
                        updated.Add(skill.Id);
                        continue;
                    }

                    if (managedEntry != null)
                    {
                        // 已登记但被项目删除的目录不能视为首次新增；否则会绕过
                        // 本地删除的人工意图并重新取得该目录的内容所有权。
                        conflicts.Add(new ReconcileConflict(skill.Id, "missing-managed"));
                        continue;
                    }

                    actions.Add(new PlannedAction("add", skill.Id, sourceDirectory, sourceHash, null));
                    finalManaged[skill.Id] = CreateManagedEntry(sourceHash, sourceHash);
                    added.Add(skill.Id);
                }

                var staleManagedIds = new List<string>();
                foreach (JProperty property in managed.Properties())
                {
                    if (!catalogIds.Contains(property.Name))
                    {
                        staleManagedIds.Add(property.Name);
                    }
                }

                staleManagedIds.Sort(StringComparer.Ordinal);
                foreach (string skillId in staleManagedIds)
                {
                    string targetDirectory = Path.Combine(paths.TargetRoot, skillId);
                    string previousTargetHash = ReadManagedHash(managed[skillId], "targetHash", skillId);
                    if (IsReparsePoint(targetDirectory))
                    {
                        conflicts.Add(new ReconcileConflict(skillId, "unsafe-link"));
                        continue;
                    }

                    if (!Directory.Exists(targetDirectory) && !File.Exists(targetDirectory))
                    {
                        actions.Add(new PlannedAction("remove", skillId, null, null, previousTargetHash));
                        finalManaged.Remove(skillId);
                        removed.Add(skillId);
                        continue;
                    }

                    if (!Directory.Exists(targetDirectory))
                    {
                        conflicts.Add(new ReconcileConflict(skillId, "modified-managed"));
                        continue;
                    }

                    string targetHash;
                    try
                    {
                        targetHash = ComputeTreeHash(targetDirectory);
                    }
                    catch (InvalidOperationException)
                    {
                        conflicts.Add(new ReconcileConflict(skillId, "unsafe-link"));
                        continue;
                    }

                    if (!string.Equals(targetHash, previousTargetHash, StringComparison.Ordinal))
                    {
                        conflicts.Add(new ReconcileConflict(skillId, "modified-managed"));
                        continue;
                    }

                    actions.Add(new PlannedAction("remove", skillId, null, null, previousTargetHash));
                    finalManaged.Remove(skillId);
                    removed.Add(skillId);
                }

                string packageVersion = ReadRequiredString(package, "version", "Framework package.json");
                JObject finalState = CreateState(packageVersion, ComputeFileHash(Path.Combine(agentsRoot, c_CatalogFileName)), finalManaged);
                var result = new ReconcileResult(
                    agentsRoot,
                    packageVersion,
                    added.AsReadOnly(),
                    updated.AsReadOnly(),
                    removed.AsReadOnly(),
                    unchanged.AsReadOnly(),
                    conflicts.AsReadOnly(),
                    dryRun);
                return new ReconcilePlan(paths.TargetRoot, paths.StatePath, previousState, finalState, actions, result);
            }

            /// <summary>
            /// 读取并校验 Catalog、包身份、扁平目录和可选能力分组。
            /// </summary>
            /// <param name="agentsRoot">Agents 真源目录。</param>
            /// <param name="catalog">输出已解析 Catalog。</param>
            /// <param name="package">输出已解析 package.json。</param>
            /// <returns>保持 Catalog 声明顺序的全量 Skill 列表。</returns>
            private static List<CatalogSkill> LoadAndValidateCatalog(string agentsRoot, out JObject catalog, out JObject package)
            {
                EnsureNormalDirectory(agentsRoot, "Agents 真源目录");
                EnsureNoReparsePoints(agentsRoot);
                string startHerePath = Path.Combine(agentsRoot, "..", "Docs", "START_HERE.md");
                if (!File.Exists(startHerePath))
                {
                    throw new InvalidOperationException("Framework 真源缺少 Docs/START_HERE.md。");
                }

                catalog = ReadJsonObject(Path.Combine(agentsRoot, c_CatalogFileName));
                package = ReadJsonObject(Path.Combine(agentsRoot, "..", "package.json"));
                ValidateObjectFields(catalog, s_CatalogFields, s_RequiredCatalogFields, "catalog.json");
                if ((int?)catalog["schemaVersion"] != c_CatalogSchemaVersion)
                {
                    throw new InvalidOperationException($"catalog.json 的 schemaVersion 必须为 {c_CatalogSchemaVersion}。");
                }

                if (!string.Equals((string)catalog["package"], c_FrameworkPackageName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("catalog.json 未声明正确的 Framework 包名。");
                }

                if (!string.Equals((string)package["name"], c_FrameworkPackageName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("package.json 未声明正确的 Framework 包名。");
                }

                JArray skills = catalog["skills"] as JArray;
                if (skills == null)
                {
                    throw new InvalidOperationException("catalog.json 的 skills 必须是数组。");
                }

                var result = new List<CatalogSkill>();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                var requirements = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var entriesById = new Dictionary<string, JObject>(StringComparer.Ordinal);
                var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (JToken token in skills)
                {
                    JObject entry = token as JObject;
                    if (entry == null)
                    {
                        throw new InvalidOperationException("catalog.json 的 skills 只能包含对象。");
                    }

                    ValidateObjectFields(entry, s_CatalogSkillFields, s_RequiredCatalogSkillFields, "catalog.json Skill");
                    string skillId = entry == null ? null : (string)entry["id"];
                    string relativePath = entry == null ? null : (string)entry["path"];
                    if (!IsManagedProjectSkillId(skillId))
                    {
                        throw new InvalidOperationException("catalog.json 包含非法项目组 Skill id。");
                    }

                    if (!ids.Add(skillId))
                    {
                        throw new InvalidOperationException($"catalog.json 重复声明 Skill：{skillId}。");
                    }

                    entriesById[skillId] = entry;

                    if (!string.Equals(relativePath, $"Skills/{skillId}", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"{skillId} 必须位于平铺目录 Skills/{skillId}。");
                    }

                    string sourceDirectory = GetSafeChild(agentsRoot, relativePath, skillId);
                    EnsureNormalDirectory(sourceDirectory, $"{skillId} 真源目录");
                    if (!File.Exists(Path.Combine(sourceDirectory, "SKILL.md")))
                    {
                        throw new InvalidOperationException($"{skillId} 缺少 SKILL.md。");
                    }

                    requirements[skillId] = ValidateSkillSourceContract(skillId, entry, sourceDirectory);
                    if (string.Equals((string)entry["status"], "deprecated", StringComparison.Ordinal))
                    {
                        replacements[skillId] = (string)entry["replacedBy"];
                    }

                    result.Add(new CatalogSkill(skillId, relativePath));
                }

                foreach (KeyValuePair<string, List<string>> requirement in requirements)
                {
                    foreach (string requiredSkillId in requirement.Value)
                    {
                        if (string.Equals(requiredSkillId, requirement.Key, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException($"{requirement.Key} 的 contract.json 不可依赖自身。");
                        }

                        if (!ids.Contains(requiredSkillId))
                        {
                            throw new InvalidOperationException($"{requirement.Key} 的 contract.json 依赖不存在的 Skill：{requiredSkillId}。");
                        }
                    }
                }

                ValidateDependencyCycles(requirements, "requires");

                foreach (KeyValuePair<string, List<string>> requirement in requirements)
                {
                    string sourceKind = (string)entriesById[requirement.Key]["kind"];
                    if (requirement.Value.Count > 0
                        && !string.Equals(sourceKind, "workflow", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"{requirement.Key} 的 contract.json requires 仅 workflow 可以声明。");
                    }

                    foreach (string requiredSkillId in requirement.Value)
                    {
                        if (!string.Equals(
                            (string)entriesById[requiredSkillId]["kind"],
                            "operation",
                            StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                $"{requirement.Key} 的 contract.json requires 只能指向 operation：{requiredSkillId}。");
                        }
                    }
                }

                foreach (KeyValuePair<string, string> replacement in replacements)
                {
                    if (string.Equals(replacement.Key, replacement.Value, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"{replacement.Key} 的 replacedBy 不可指向自身。");
                    }

                    if (!entriesById.ContainsKey(replacement.Value))
                    {
                        throw new InvalidOperationException(
                            $"{replacement.Key} 的 replacedBy 指向不存在的 Skill：{replacement.Value}。");
                    }
                }

                var replacementGraph = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> replacement in replacements)
                {
                    replacementGraph[replacement.Key] = new List<string> { replacement.Value };
                }

                ValidateDependencyCycles(replacementGraph, "replacedBy");
                foreach (KeyValuePair<string, string> replacement in replacements)
                {
                    if (string.Equals(
                        (string)entriesById[replacement.Value]["status"],
                        "deprecated",
                        StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"{replacement.Key} 的 replacedBy 不可继续指向已弃用 Skill：{replacement.Value}。");
                    }
                }

                string skillsRoot = Path.Combine(agentsRoot, c_SkillsDirectoryName);
                EnsureNormalDirectory(skillsRoot, "Skills 真源目录");
                foreach (string directory in Directory.GetDirectories(skillsRoot))
                {
                    EnsureNotReparsePoint(directory, "Skills 子目录");
                    string id = Path.GetFileName(directory);
                    if (!ids.Contains(id))
                    {
                        throw new InvalidOperationException($"Skills/{id} 未登记在 catalog.skills。");
                    }
                }

                ValidateCapabilityGroups(catalog, ids);
                return result;
            }

            /// <summary>
            /// 复用投影链的完整规则，只读校验当前 Agents Catalog 与全部 Skill 契约。
            /// 能力浏览器仅在本入口通过后读取展示字段。
            /// </summary>
            internal static void ValidateCatalogForDiscovery(string agentsRoot)
            {
                string normalizedAgentsRoot = NormalizeDirectory(agentsRoot, "Agents 真源目录");
                LoadAndValidateCatalog(normalizedAgentsRoot, out _, out _);
            }

            /// <summary>
            /// 校验能力分组只用于导航，不允许引用 Catalog 外的 Skill。
            /// </summary>
            /// <param name="catalog">当前 Catalog。</param>
            /// <param name="knownIds">已登记 Skill id 集合。</param>
            private static void ValidateCapabilityGroups(JObject catalog, HashSet<string> knownIds)
            {
                JToken groupsToken = catalog["capabilityGroups"];
                if (groupsToken == null)
                {
                    return;
                }

                JObject groups = groupsToken as JObject;
                if (groups == null)
                {
                    throw new InvalidOperationException("catalog.json 的 capabilityGroups 必须是对象。");
                }

                foreach (JProperty group in groups.Properties())
                {
                    JArray ids = group.Value as JArray;
                    if (ids == null)
                    {
                        throw new InvalidOperationException($"能力分组 {group.Name} 必须是数组。");
                    }

                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (JToken idToken in ids)
                    {
                        string id = (string)idToken;
                        if (string.IsNullOrEmpty(id) || !seen.Add(id) || !knownIds.Contains(id))
                        {
                            throw new InvalidOperationException($"能力分组 {group.Name} 包含非法或重复 Skill：{id}。");
                        }
                    }
                }
            }

            /// <summary>
            /// 校验一个 Catalog 条目与其 SKILL.md、contract.json 的完整 P0 真源契约。
            /// </summary>
            /// <param name="skillId">Catalog 已验证的项目组 Skill id。</param>
            /// <param name="catalogEntry">当前 Catalog 条目。</param>
            /// <param name="sourceDirectory">Skill 真源目录。</param>
            /// <returns>已验证的 requires 列表，供调用方进行全局依赖检查。</returns>
            private static List<string> ValidateSkillSourceContract(
                string skillId,
                JObject catalogEntry,
                string sourceDirectory)
            {
                string kind = ReadRequiredString(catalogEntry, "kind", $"{skillId} 的 Catalog");
                if (!s_SkillKinds.Contains(kind))
                {
                    throw new InvalidOperationException($"{skillId} 的 kind 不受支持。");
                }

                string status = ReadRequiredString(catalogEntry, "status", $"{skillId} 的 Catalog");
                if (!s_CatalogSkillStatuses.Contains(status))
                {
                    throw new InvalidOperationException($"{skillId} 的 status 不受支持。");
                }

                JProperty replacementProperty = catalogEntry.Property("replacedBy");
                string replacement = replacementProperty == null ? null : (string)replacementProperty.Value;
                if (string.Equals(status, "deprecated", StringComparison.Ordinal))
                {
                    if (!IsManagedProjectSkillId(replacement))
                    {
                        throw new InvalidOperationException($"{skillId} 已弃用时必须声明合法 replacedBy。");
                    }
                }
                else if (replacementProperty != null)
                {
                    throw new InvalidOperationException($"{skillId} 仅 deprecated 状态可以声明 replacedBy。");
                }

                ReadStringArray(
                    catalogEntry["journeys"],
                    $"{skillId} 的 journeys",
                    true,
                    false,
                    false);

                List<string> effects = ReadStringArray(
                    catalogEntry["effects"],
                    $"{skillId} 的 effects",
                    true,
                    true,
                    true);
                foreach (string effect in effects)
                {
                    if (!s_SkillEffects.Contains(effect))
                    {
                        throw new InvalidOperationException($"{skillId} 的 effects 包含不支持值：{effect}。");
                    }
                }

                string minimumEvidence = ReadRequiredString(
                    catalogEntry,
                    "minimumEvidence",
                    $"{skillId} 的 Catalog");
                if (!s_MinimumEvidenceLevels.Contains(minimumEvidence))
                {
                    throw new InvalidOperationException($"{skillId} 的 minimumEvidence 不受支持。");
                }

                bool hasBuildEffect = effects.Contains("build");
                bool hasBuildEvidence = string.Equals(minimumEvidence, "bundle-build", StringComparison.Ordinal)
                    || string.Equals(minimumEvidence, "player-build", StringComparison.Ordinal);
                if (hasBuildEffect != hasBuildEvidence)
                {
                    throw new InvalidOperationException(
                        $"{skillId} 的 build effect 与 minimumEvidence 构建证据必须成对声明。");
                }

                string skillFile = Path.Combine(sourceDirectory, "SKILL.md");
                if (!string.Equals(ReadSkillFrontmatterName(skillFile), skillId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"{skillId} 与 SKILL.md frontmatter name 不一致。");
                }

                string firstBodyParagraph = ReadFirstSkillBodyParagraph(skillFile);
                if (firstBodyParagraph == null
                    || firstBodyParagraph.IndexOf(c_ProjectSkillCommonBaseline, StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException(
                        $"{skillId} 的 SKILL.md 首个正文段落必须包含“{c_ProjectSkillCommonBaseline}”。");
                }

                ValidateSkillProgressiveDisclosure(skillId, skillFile);

                JObject contract = ReadJsonObject(Path.Combine(sourceDirectory, "references", "contract.json"));
                if ((int?)contract["schemaVersion"] != 1)
                {
                    throw new InvalidOperationException($"{skillId} 的 contract.json schemaVersion 必须为 1。");
                }

                if (!string.Equals((string)contract["id"], skillId, StringComparison.Ordinal)
                    || !string.Equals((string)contract["kind"], kind, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"{skillId} 与 contract.json id 或 kind 不一致。");
                }

                List<string> contractEffects = ReadStringArray(
                    contract["effects"],
                    $"{skillId} 的 contract.json effects",
                    true,
                    true,
                    false);
                foreach (string effect in contractEffects)
                {
                    if (!s_SkillEffects.Contains(effect))
                    {
                        throw new InvalidOperationException($"{skillId} 的 contract.json effects 包含不支持值：{effect}。");
                    }
                }

                if (!StringListsEqual(effects, contractEffects))
                {
                    throw new InvalidOperationException($"{skillId} 与 contract.json effects 不一致。");
                }

                if (!string.Equals((string)contract["minimumEvidence"], minimumEvidence, StringComparison.Ordinal)
                    || !s_MinimumEvidenceLevels.Contains((string)contract["minimumEvidence"]))
                {
                    throw new InvalidOperationException($"{skillId} 与 contract.json minimumEvidence 不一致或不受支持。");
                }

                JObject compatibility = contract["compatibility"] as JObject;
                if (compatibility == null || compatibility["framework"] == null
                    || compatibility["framework"].Type != JTokenType.String)
                {
                    throw new InvalidOperationException($"{skillId} 的 contract.json compatibility 必须声明 framework。");
                }

                List<string> requires = ReadStringArray(
                    contract["requires"],
                    $"{skillId} 的 contract.json requires",
                    false,
                    false,
                    true);
                foreach (string requiredSkillId in requires)
                {
                    if (!s_SkillIdPattern.IsMatch(requiredSkillId))
                    {
                        throw new InvalidOperationException($"{skillId} 的 contract.json requires 包含非法 Skill id。");
                    }
                }

                ValidateActionAdapters(skillId, contract["actionAdapters"]);
                ValidateContractInputs(skillId, contract["inputs"]);
                ValidateContractWriteScope(skillId, contract["writeScope"]);
                ReadStringArray(contract["locks"], $"{skillId} 的 contract.json locks", false, true, true);

                if (!s_ContractIdempotency.Contains((string)contract["idempotency"]))
                {
                    throw new InvalidOperationException($"{skillId} 的 contract.json idempotency 不受支持。");
                }

                JObject confirmation = contract["confirmation"] as JObject;
                if (confirmation == null || confirmation["rule"] == null
                    || confirmation["rule"].Type != JTokenType.String)
                {
                    throw new InvalidOperationException($"{skillId} 的 contract.json confirmation 必须含 rule。");
                }

                ReadStringArray(
                    confirmation["requiredFor"],
                    $"{skillId} 的 contract.json confirmation.requiredFor",
                    false,
                    false,
                    false);

                List<string> resultStates = ReadStringArray(
                    contract["resultStates"],
                    $"{skillId} 的 contract.json resultStates",
                    true,
                    true,
                    true);
                foreach (string resultState in resultStates)
                {
                    if (!s_ContractResultStates.Contains(resultState))
                    {
                        throw new InvalidOperationException($"{skillId} 的 contract.json resultStates 包含不支持值：{resultState}。");
                    }
                }

                ReadStringArray(contract["evidence"], $"{skillId} 的 contract.json evidence", true, true, false);
                return requires;
            }

            /// <summary>
            /// 校验 contract 至少声明一个严格且不重复的 Action Adapter。
            /// </summary>
            private static void ValidateActionAdapters(string skillId, JToken token)
            {
                JArray adapters = token as JArray;
                if (adapters == null || adapters.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"{skillId} 的 contract.json actionAdapters 必须是非空数组。");
                }

                var identities = new HashSet<string>(StringComparer.Ordinal);
                foreach (JToken adapterToken in adapters)
                {
                    JObject adapter = adapterToken as JObject;
                    if (adapter == null || adapter.Count != s_ActionAdapterFields.Count)
                    {
                        throw new InvalidOperationException(
                            $"{skillId} 的 contract.json actionAdapters 项必须只含 kind、entry、when。");
                    }

                    foreach (JProperty property in adapter.Properties())
                    {
                        if (!s_ActionAdapterFields.Contains(property.Name))
                        {
                            throw new InvalidOperationException(
                                $"{skillId} 的 contract.json actionAdapters 项必须只含 kind、entry、when。");
                        }
                    }

                    string kind = adapter["kind"] == null || adapter["kind"].Type != JTokenType.String
                        ? null
                        : (string)adapter["kind"];
                    string entry = adapter["entry"] == null || adapter["entry"].Type != JTokenType.String
                        ? null
                        : (string)adapter["entry"];
                    string when = adapter["when"] == null || adapter["when"].Type != JTokenType.String
                        ? null
                        : (string)adapter["when"];
                    if (!s_ActionAdapterKinds.Contains(kind)
                        || string.IsNullOrEmpty(entry)
                        || string.IsNullOrEmpty(when))
                    {
                        throw new InvalidOperationException(
                            $"{skillId} 的 contract.json actionAdapters 项不合法。");
                    }

                    string identity = kind + "\0" + entry + "\0" + when;
                    if (!identities.Add(identity))
                    {
                        throw new InvalidOperationException(
                            $"{skillId} 的 contract.json actionAdapters 不能重复。");
                    }
                }
            }

            /// <summary>
            /// 校验 contract 输入描述为至少一项 name/required 完整的对象。
            /// </summary>
            /// <param name="skillId">当前项目组 Skill id。</param>
            /// <param name="token">contract.inputs JSON token。</param>
            private static void ValidateContractInputs(string skillId, JToken token)
            {
                JArray inputs = token as JArray;
                if (inputs == null || inputs.Count == 0)
                {
                    throw new InvalidOperationException($"{skillId} 的 contract.json inputs 必须是非空数组。");
                }

                foreach (JToken itemToken in inputs)
                {
                    JObject item = itemToken as JObject;
                    if (item == null || string.IsNullOrEmpty((string)item["name"])
                        || item["required"] == null || item["required"].Type != JTokenType.Boolean)
                    {
                        throw new InvalidOperationException($"{skillId} 的 contract.json inputs 必须声明 name 与 required。");
                    }
                }
            }

            /// <summary>
            /// 校验 contract 写入范围同时显式声明 allow 和 deny 字符串数组。
            /// </summary>
            /// <param name="skillId">当前项目组 Skill id。</param>
            /// <param name="token">contract.writeScope JSON token。</param>
            private static void ValidateContractWriteScope(string skillId, JToken token)
            {
                JObject writeScope = token as JObject;
                if (writeScope == null)
                {
                    throw new InvalidOperationException($"{skillId} 的 contract.json writeScope 必须是对象。");
                }

                ReadStringArray(writeScope["allow"], $"{skillId} 的 contract.json writeScope.allow", false, false, false);
                ReadStringArray(writeScope["deny"], $"{skillId} 的 contract.json writeScope.deny", false, false, false);
            }

            /// <summary>
            /// 按 catalog schema 的 additionalProperties 与 required 约束校验 JSON 对象字段。
            /// </summary>
            /// <param name="value">待校验的 JSON 对象。</param>
            /// <param name="allowedFields">允许出现的字段集合。</param>
            /// <param name="requiredFields">必须出现的字段集合。</param>
            /// <param name="label">错误信息中的对象名称。</param>
            private static void ValidateObjectFields(
                JObject value,
                HashSet<string> allowedFields,
                HashSet<string> requiredFields,
                string label)
            {
                foreach (JProperty property in value.Properties())
                {
                    if (!allowedFields.Contains(property.Name))
                    {
                        throw new InvalidOperationException($"{label} 包含不支持字段：{property.Name}。");
                    }
                }

                foreach (string field in requiredFields)
                {
                    if (value.Property(field) == null)
                    {
                        throw new InvalidOperationException($"{label} 缺少 {field}。");
                    }
                }
            }

            /// <summary>
            /// 将 JSON 字符串数组转换为列表，并按调用方约束检查空值和重复项。
            /// </summary>
            /// <param name="token">待解析数组 token。</param>
            /// <param name="label">用于错误信息的字段名称。</param>
            /// <param name="requireNonEmptyArray">是否要求数组至少有一项。</param>
            /// <param name="requireNonEmptyValues">是否要求数组元素为非空字符串。</param>
            /// <param name="requireUniqueValues">是否拒绝重复字符串。</param>
            /// <returns>按原声明顺序返回的字符串列表。</returns>
            private static List<string> ReadStringArray(
                JToken token,
                string label,
                bool requireNonEmptyArray,
                bool requireNonEmptyValues,
                bool requireUniqueValues)
            {
                JArray array = token as JArray;
                if (array == null || (requireNonEmptyArray && array.Count == 0))
                {
                    throw new InvalidOperationException($"{label} 必须是{(requireNonEmptyArray ? "非空" : "")}字符串数组。");
                }

                var result = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (JToken valueToken in array)
                {
                    if (valueToken.Type != JTokenType.String)
                    {
                        throw new InvalidOperationException($"{label} 只能包含字符串。");
                    }

                    string value = (string)valueToken;
                    if (requireNonEmptyValues && string.IsNullOrEmpty(value))
                    {
                        throw new InvalidOperationException($"{label} 不能包含空字符串。");
                    }

                    if (requireUniqueValues && !seen.Add(value))
                    {
                        throw new InvalidOperationException($"{label} 不能包含重复字符串。");
                    }

                    result.Add(value);
                }

                return result;
            }

            /// <summary>
            /// 判断两个字符串列表的元素和顺序是否完全一致。
            /// </summary>
            /// <param name="left">左侧列表。</param>
            /// <param name="right">右侧列表。</param>
            /// <returns>长度和每个同位置元素均相等时返回 true。</returns>
            private static bool StringListsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
            {
                if (left.Count != right.Count)
                {
                    return false;
                }

                for (int index = 0; index < left.Count; index++)
                {
                    if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// 读取 SKILL.md 的标准 YAML frontmatter name，缺失或格式不符时返回 null。
            /// </summary>
            /// <param name="skillFile">SKILL.md 文件路径。</param>
            /// <returns>frontmatter name；无法读取时返回 null。</returns>
            private static string ReadSkillFrontmatterName(string skillFile)
            {
                string content = File.ReadAllText(skillFile, Encoding.UTF8).Replace("\r\n", "\n");
                if (!content.StartsWith("---\n", StringComparison.Ordinal))
                {
                    return null;
                }

                int closingIndex = content.IndexOf("\n---", 4, StringComparison.Ordinal);
                if (closingIndex < 0)
                {
                    return null;
                }

                string frontmatter = content.Substring(4, closingIndex - 4);
                foreach (string line in frontmatter.Split('\n'))
                {
                    Match match = s_FrontmatterNamePattern.Match(line);
                    if (match.Success)
                    {
                        return match.Groups[1].Value.Trim().Trim('\"', '\'');
                    }
                }

                return null;
            }

            /// <summary>
            /// 读取 frontmatter 后的首个正文段落；跳过空行和 Markdown 标题，命中段落后立即停止。
            /// </summary>
            /// <param name="skillFile">SKILL.md 文件路径。</param>
            /// <returns>首个正文段落；不存在时返回 null。</returns>
            private static string ReadFirstSkillBodyParagraph(string skillFile)
            {
                using (var reader = new StreamReader(skillFile, Encoding.UTF8, true))
                {
                    if (!string.Equals(reader.ReadLine(), "---", StringComparison.Ordinal))
                    {
                        return null;
                    }

                    string line;
                    while ((line = reader.ReadLine()) != null
                        && !string.Equals(line.TrimEnd('\r'), "---", StringComparison.Ordinal))
                    {
                    }

                    if (line == null)
                    {
                        return null;
                    }

                    var paragraph = new StringBuilder();
                    while ((line = reader.ReadLine()) != null)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length == 0)
                        {
                            if (paragraph.Length > 0)
                            {
                                break;
                            }

                            continue;
                        }

                        if (paragraph.Length == 0 && IsMarkdownHeading(trimmed))
                        {
                            continue;
                        }

                        if (paragraph.Length > 0)
                        {
                            paragraph.Append('\n');
                        }

                        paragraph.Append(trimmed);
                    }

                    return paragraph.Length == 0 ? null : paragraph.ToString();
                }
            }

            /// <summary>
            /// 判断一行是否为 ATX Markdown 标题。
            /// </summary>
            private static bool IsMarkdownHeading(string line)
            {
                int markerCount = 0;
                while (markerCount < line.Length && markerCount < 6 && line[markerCount] == '#')
                {
                    markerCount++;
                }

                return markerCount > 0
                    && markerCount < line.Length
                    && char.IsWhiteSpace(line[markerCount]);
            }

            /// <summary>
            /// 校验 Skill 声明固定渐进式披露章节及按需读取语义。
            /// </summary>
            private static void ValidateSkillProgressiveDisclosure(string skillId, string skillFile)
            {
                bool hasHeading = false;
                bool hasOnDemandRoute = false;
                using (var reader = new StreamReader(skillFile, Encoding.UTF8, true))
                {
                    string line;
                    bool inBody = false;
                    int frontmatterDelimiterCount = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string trimmed = line.Trim();
                        if (!inBody)
                        {
                            if (string.Equals(trimmed, "---", StringComparison.Ordinal))
                            {
                                frontmatterDelimiterCount++;
                                inBody = frontmatterDelimiterCount == 2;
                            }

                            continue;
                        }

                        if (!hasHeading
                            && string.Equals(trimmed, c_ProgressiveDisclosureHeading, StringComparison.Ordinal))
                        {
                            hasHeading = true;
                        }

                        if (!hasOnDemandRoute && trimmed.IndexOf("仅在", StringComparison.Ordinal) >= 0)
                        {
                            hasOnDemandRoute = true;
                        }

                        if (hasHeading && hasOnDemandRoute)
                        {
                            return;
                        }
                    }
                }

                throw new InvalidOperationException(
                    $"{skillId} 的 SKILL.md 必须声明 {c_ProgressiveDisclosureHeading}，并以“仅在”描述按需读取路由。");
            }

            /// <summary>
            /// 校验 Skill 有向关系图不包含循环。
            /// </summary>
            private static void ValidateDependencyCycles(
                Dictionary<string, List<string>> graph,
                string label)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                var visiting = new HashSet<string>(StringComparer.Ordinal);
                var ids = new List<string>(graph.Keys);
                ids.Sort(StringComparer.Ordinal);
                foreach (string skillId in ids)
                {
                    VisitDependencyNode(skillId, graph, label, visiting, visited);
                }
            }

            /// <summary>
            /// 深度优先访问一个 Skill 关系节点，并在回边处快速失败。
            /// </summary>
            private static void VisitDependencyNode(
                string skillId,
                Dictionary<string, List<string>> graph,
                string label,
                HashSet<string> visiting,
                HashSet<string> visited)
            {
                if (visiting.Contains(skillId))
                {
                    throw new InvalidOperationException($"{label} 出现循环：{skillId}。");
                }

                if (visited.Contains(skillId))
                {
                    return;
                }

                visiting.Add(skillId);
                List<string> nextIds;
                if (graph.TryGetValue(skillId, out nextIds))
                {
                    foreach (string nextId in nextIds)
                    {
                        if (graph.ContainsKey(nextId))
                        {
                            VisitDependencyNode(nextId, graph, label, visiting, visited);
                        }
                    }
                }

                visiting.Remove(skillId);
                visited.Add(skillId);
            }

            /// <summary>
            /// 读取唯一受管 state；目标冲突交给后续规划逐项保留为 partial。
            /// </summary>
            /// <param name="statePath">状态文件路径。</param>
            /// <param name="previousState">输出规划前原始 state。</param>
            /// <returns>规范化的受管记录对象。</returns>
            private static JObject ReadManagedStateForReconcile(string statePath, out JObject previousState)
            {
                previousState = ReadJsonObjectIfExists(statePath);
                if (previousState == null)
                {
                    return new JObject();
                }

                ValidateObjectFields(previousState, s_StateFields, s_StateFields, "受管状态");
                ValidateStateHeader(previousState, c_StateSchemaVersion, statePath);
                ValidateHash((string)previousState["catalogHash"], "catalogHash");
                return ValidateManagedEntries(previousState["managed"], statePath);
            }

            /// <summary>
            /// 启动新事务：先把新增/更新源写入 staging，再原子写入动作日志。
            /// </summary>
            /// <param name="projectRoot">消费项目根目录。</param>
            /// <param name="plan">已冻结的 reconcile 计划。</param>
            private static void BeginTransaction(string projectRoot, ReconcilePlan plan)
            {
                ManagedPaths paths = GetManagedPaths(projectRoot);
                Directory.CreateDirectory(plan.TargetRoot);
                Directory.CreateDirectory(paths.StagingRoot);
                string transactionId = Guid.NewGuid().ToString("N");
                string stagingDirectory = Path.Combine(paths.StagingRoot, transactionId);
                string newRoot = Path.Combine(stagingDirectory, "new");
                Directory.CreateDirectory(newRoot);
                bool journalWritten = false;
                try
                {
                    var pending = new JArray();
                    foreach (PlannedAction action in plan.Actions)
                    {
                        var pendingItem = new JObject
                        {
                            ["action"] = action.Action,
                            ["id"] = action.SkillId,
                        };
                        if (action.Action == "add" || action.Action == "update")
                        {
                            string stagedSkill = Path.Combine(newRoot, action.SkillId);
                            CopyDirectory(action.SourceDirectory, stagedSkill);
                            string targetHash = ComputeTreeHash(stagedSkill);
                            string sourceHashAfterCopy = ComputeTreeHash(action.SourceDirectory);
                            if (!string.Equals(action.SourceHash, targetHash, StringComparison.Ordinal)
                                || !string.Equals(action.SourceHash, sourceHashAfterCopy, StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException($"复制 {action.SkillId} 时 Framework 真源发生变化，拒绝登记混合版本投影。");
                            }

                            pendingItem["sourceHash"] = action.SourceHash;
                            pendingItem["targetHash"] = targetHash;
                        }

                        if (action.Action == "update" || action.Action == "remove")
                        {
                            pendingItem["previousTargetHash"] = action.PreviousTargetHash;
                        }

                        pending.Add(pendingItem);
                    }

                    var transaction = new JObject
                    {
                        ["schemaVersion"] = c_TransactionSchemaVersion,
                        ["transactionId"] = transactionId,
                        ["previousState"] = plan.PreviousState == null ? JValue.CreateNull() : plan.PreviousState.DeepClone(),
                        ["finalState"] = plan.FinalState.DeepClone(),
                        ["pending"] = pending,
                    };
                    GetManagedPaths(projectRoot);
                    WriteJsonAtomically(paths.TransactionPath, transaction);
                    journalWritten = true;
                }
                finally
                {
                    if (!journalWritten && Directory.Exists(stagingDirectory))
                    {
                        DeleteDirectorySafely(stagingDirectory);
                        TryDeleteEmptyDirectory(paths.StagingRoot);
                    }
                }
            }

            /// <summary>
            /// 续传已有事务；所有目标完成并重新校验后才写入最终 state。
            /// </summary>
            /// <param name="projectRoot">消费项目根目录。</param>
            /// <param name="paths">当前已验证的受管路径。</param>
            /// <returns>存在并完成事务时返回 true；没有事务时返回 false。</returns>
            private static bool ResumeTransaction(string projectRoot, ManagedPaths paths)
            {
                if (!File.Exists(paths.TransactionPath))
                {
                    return false;
                }

                JObject transaction = ReadJsonObject(paths.TransactionPath);
                if ((int?)transaction["schemaVersion"] != c_TransactionSchemaVersion)
                {
                    throw new InvalidOperationException($"受管事务 schemaVersion 不受支持：{paths.TransactionPath}。");
                }

                JObject previousState;
                JObject finalState;
                JObject finalManaged;
                JArray pending;
                string transactionId;
                ValidateTransaction(
                    transaction,
                    paths.TransactionPath,
                    out transactionId,
                    out previousState,
                    out finalState,
                    out finalManaged,
                    out pending);

                string stagingDirectory = Path.Combine(paths.StagingRoot, transactionId);
                EnsureNormalDirectory(stagingDirectory, "受管事务 staging");
                EnsureNoReparsePoints(stagingDirectory);
                JObject currentState = ReadJsonObjectIfExists(paths.StatePath);
                EnsureStateUnchanged(currentState, previousState, finalState, "中断事务期间");
                Directory.CreateDirectory(paths.TargetRoot);
                string newRoot = Path.Combine(stagingDirectory, "new");
                string backupRoot = Path.Combine(stagingDirectory, "backup");
                var seenIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (JToken token in pending)
                {
                    JObject item = token as JObject;
                    string action = item == null ? null : (string)item["action"];
                    string skillId = item == null ? null : (string)item["id"];
                    if ((action != "add" && action != "update" && action != "remove")
                        || !IsManagedProjectSkillId(skillId)
                        || !seenIds.Add(skillId))
                    {
                        throw new InvalidOperationException($"受管事务 pending 包含非法动作：{paths.TransactionPath}。");
                    }

                    ApplyPendingAction(projectRoot, paths, newRoot, backupRoot, finalManaged, action, skillId, item);
                }

                JObject latestState = ReadJsonObjectIfExists(paths.StatePath);
                EnsureStateUnchanged(latestState, currentState, finalState, "事务完成前");
                if (!JToken.DeepEquals(latestState, finalState))
                {
                    WriteJsonAtomically(paths.StatePath, finalState);
                }

                GetManagedPaths(projectRoot);
                File.Delete(paths.TransactionPath);
                DeleteDirectorySafely(stagingDirectory);
                TryDeleteEmptyDirectory(paths.StagingRoot);
                return true;
            }

            /// <summary>
            /// 验证事务的前后 state 与 pending 差量完全一致，避免 journal 伪造目录所有权。
            /// </summary>
            /// <param name="transaction">原始事务 JSON。</param>
            /// <param name="transactionPath">事务日志物理路径。</param>
            /// <param name="transactionId">输出事务 id。</param>
            /// <param name="previousState">输出规划前 state；首次投影时为 null。</param>
            /// <param name="finalState">输出最终受管 state。</param>
            /// <param name="finalManaged">输出规范化后的最终受管记录。</param>
            /// <param name="pending">输出已验证的动作数组。</param>
            private static void ValidateTransaction(
                JObject transaction,
                string transactionPath,
                out string transactionId,
                out JObject previousState,
                out JObject finalState,
                out JObject finalManaged,
                out JArray pending)
            {
                ValidateObjectFields(transaction, s_TransactionFields, s_TransactionFields, "受管事务");
                if ((int?)transaction["schemaVersion"] != c_TransactionSchemaVersion)
                {
                    throw new InvalidOperationException($"受管事务 schemaVersion 不受支持：{transactionPath}。");
                }

                transactionId = (string)transaction["transactionId"];
                if (string.IsNullOrEmpty(transactionId) || !Regex.IsMatch(transactionId, "^[0-9a-f]{32}$"))
                {
                    throw new InvalidOperationException($"受管事务缺少合法 transactionId：{transactionPath}。");
                }

                JToken previousToken = transaction["previousState"];
                if (previousToken != null && previousToken.Type != JTokenType.Null && !(previousToken is JObject))
                {
                    throw new InvalidOperationException($"受管事务 previousState 格式错误：{transactionPath}。");
                }

                previousState = previousToken as JObject;
                JObject previousManaged = ValidateTransactionState(previousState, "previousState", transactionPath);
                finalState = transaction["finalState"] as JObject;
                if (finalState == null || (int?)finalState["schemaVersion"] != c_StateSchemaVersion)
                {
                    throw new InvalidOperationException($"受管事务 finalState schemaVersion 不受支持：{transactionPath}。");
                }

                finalManaged = ValidateTransactionState(finalState, "finalState", transactionPath);
                pending = transaction["pending"] as JArray;
                if (pending == null || pending.Count == 0)
                {
                    throw new InvalidOperationException($"受管事务 pending 必须是非空数组：{transactionPath}。");
                }

                var expectedManaged = (JObject)previousManaged.DeepClone();
                var seenIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (JToken token in pending)
                {
                    JObject item = token as JObject;
                    string action = item == null ? null : (string)item["action"];
                    string skillId = item == null ? null : (string)item["id"];
                    if ((action != "add" && action != "update" && action != "remove")
                        || !IsManagedProjectSkillId(skillId)
                        || !seenIds.Add(skillId))
                    {
                        throw new InvalidOperationException($"受管事务 pending 包含非法动作或项目组 Skill id：{transactionPath}。");
                    }

                    if (action == "add")
                    {
                        if (expectedManaged[skillId] != null)
                        {
                            throw new InvalidOperationException($"受管事务 add 不能覆盖 previousState 已有 Skill：{skillId}。");
                        }

                        expectedManaged[skillId] = CreateManagedEntry(
                            ReadRequiredHash(item, "sourceHash", skillId),
                            ReadRequiredHash(item, "targetHash", skillId));
                        continue;
                    }

                    string previousTargetHash = ReadRequiredHash(item, "previousTargetHash", skillId);
                    JToken previousEntry = expectedManaged[skillId];
                    if (previousEntry == null
                        || !string.Equals(
                            ReadManagedHash(previousEntry, "targetHash", skillId),
                            previousTargetHash,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"受管事务 {skillId} 的 previousTargetHash 与 previousState 不一致。");
                    }

                    if (action == "update")
                    {
                        expectedManaged[skillId] = CreateManagedEntry(
                            ReadRequiredHash(item, "sourceHash", skillId),
                            ReadRequiredHash(item, "targetHash", skillId));
                    }
                    else
                    {
                        expectedManaged.Remove(skillId);
                    }
                }

                if (!JToken.DeepEquals(expectedManaged, finalManaged))
                {
                    throw new InvalidOperationException($"受管事务 finalState 与 previousState/pending 不一致：{transactionPath}。");
                }
            }

            /// <summary>
            /// 校验 journal 中的前后 state，并返回可用于差量推导的规范化受管集合。
            /// </summary>
            /// <param name="state">待验证 state；null 表示首次投影前没有 state。</param>
            /// <param name="label">错误信息中的 journal 字段名。</param>
            /// <param name="transactionPath">事务日志物理路径。</param>
            /// <returns>规范化后的受管记录；state 为 null 时返回空对象。</returns>
            private static JObject ValidateTransactionState(JObject state, string label, string transactionPath)
            {
                if (state == null)
                {
                    return new JObject();
                }

                ValidateObjectFields(state, s_StateFields, s_StateFields, $"受管事务 {label}");
                ValidateStateHeader(state, c_StateSchemaVersion, transactionPath);
                ValidateHash((string)state["catalogHash"], $"事务 {label}.catalogHash");

                JObject managed = ValidateManagedEntries(state["managed"], transactionPath);
                state["managed"] = managed;
                return managed;
            }

            /// <summary>
            /// 应用一个已登记事务动作，并在任何未知目标内容前 fail-closed。
            /// </summary>
            /// <param name="projectRoot">消费项目根目录。</param>
            /// <param name="paths">当前受管路径。</param>
            /// <param name="newRoot">新增/更新 staging 根目录。</param>
            /// <param name="backupRoot">更新/删除备份根目录。</param>
            /// <param name="finalManaged">事务最终受管记录。</param>
            /// <param name="action">动作类型。</param>
            /// <param name="skillId">Skill id。</param>
            /// <param name="item">原始事务动作对象。</param>
            private static void ApplyPendingAction(
                string projectRoot,
                ManagedPaths paths,
                string newRoot,
                string backupRoot,
                JObject finalManaged,
                string action,
                string skillId,
                JObject item)
            {
                string targetDirectory = Path.Combine(paths.TargetRoot, skillId);
                string stagedDirectory = Path.Combine(newRoot, skillId);
                string backupDirectory = Path.Combine(backupRoot, skillId);
                if (IsReparsePoint(targetDirectory) || IsReparsePoint(stagedDirectory) || IsReparsePoint(backupDirectory))
                {
                    throw new InvalidOperationException($"中断事务包含链接目标，拒绝恢复：{skillId}。");
                }

                if (action == "add")
                {
                    string expectedHash = ReadRequiredHash(item, "targetHash", skillId);
                    EnsureFinalManagedHashes(finalManaged, skillId, item);
                    if (Directory.Exists(targetDirectory))
                    {
                        EnsureDirectoryHash(targetDirectory, expectedHash, $"中断新增目标已变化：{targetDirectory}");
                        return;
                    }

                    EnsureDirectoryHash(stagedDirectory, expectedHash, $"中断新增 staging 已变化：{stagedDirectory}");
                    GetManagedPaths(projectRoot);
                    Directory.Move(stagedDirectory, targetDirectory);
                    EnsureDirectoryHash(targetDirectory, expectedHash, $"中断新增恢复后哈希不一致：{targetDirectory}");
                    return;
                }

                string previousTargetHash = ReadRequiredHash(item, "previousTargetHash", skillId);
                if (action == "update")
                {
                    string expectedHash = ReadRequiredHash(item, "targetHash", skillId);
                    EnsureFinalManagedHashes(finalManaged, skillId, item);
                    if (Directory.Exists(targetDirectory) && DirectoryHashEquals(targetDirectory, expectedHash))
                    {
                        if (Directory.Exists(backupDirectory))
                        {
                            EnsureDirectoryHash(backupDirectory, previousTargetHash, $"中断更新备份已变化：{backupDirectory}");
                        }

                        return;
                    }

                    if (Directory.Exists(targetDirectory))
                    {
                        EnsureDirectoryHash(targetDirectory, previousTargetHash, $"中断更新目标已变化：{targetDirectory}");
                        if (Directory.Exists(backupDirectory))
                        {
                            throw new InvalidOperationException($"中断更新同时存在旧目标与备份：{skillId}。");
                        }

                        Directory.CreateDirectory(backupRoot);
                        GetManagedPaths(projectRoot);
                        Directory.Move(targetDirectory, backupDirectory);
                    }
                    else if (Directory.Exists(backupDirectory))
                    {
                        EnsureDirectoryHash(backupDirectory, previousTargetHash, $"中断更新备份已变化：{backupDirectory}");
                    }
                    else
                    {
                        throw new InvalidOperationException($"中断更新缺少旧目标与备份：{skillId}。");
                    }

                    EnsureDirectoryHash(stagedDirectory, expectedHash, $"中断更新 staging 已变化：{stagedDirectory}");
                    GetManagedPaths(projectRoot);
                    Directory.Move(stagedDirectory, targetDirectory);
                    EnsureDirectoryHash(targetDirectory, expectedHash, $"中断更新恢复后哈希不一致：{targetDirectory}");
                    return;
                }

                if (action == "remove")
                {
                    if (finalManaged[skillId] != null)
                    {
                        throw new InvalidOperationException($"受管事务删除 {skillId} 后仍保留最终记录。");
                    }

                    if (Directory.Exists(targetDirectory))
                    {
                        EnsureDirectoryHash(targetDirectory, previousTargetHash, $"中断删除目标已变化：{targetDirectory}");
                        if (Directory.Exists(backupDirectory))
                        {
                            throw new InvalidOperationException($"中断删除同时存在目标与备份：{skillId}。");
                        }

                        Directory.CreateDirectory(backupRoot);
                        GetManagedPaths(projectRoot);
                        Directory.Move(targetDirectory, backupDirectory);
                    }

                    if (!Directory.Exists(backupDirectory))
                    {
                        // 目标在建立事务前或恢复前已不存在。没有用户内容可删除时，
                        // 仅移除受管记录即可完成幂等 remove 动作。
                        return;
                    }

                    EnsureDirectoryHash(backupDirectory, previousTargetHash, $"中断删除备份已变化：{backupDirectory}");
                    return;
                }

                throw new InvalidOperationException($"受管事务包含未知 action：{action}。");
            }

            /// <summary>
            /// 读取并验证当前 state 与规划前/最终 state 的相等关系。
            /// </summary>
            /// <param name="currentState">当前磁盘 state。</param>
            /// <param name="allowedPreviousState">允许的规划前 state。</param>
            /// <param name="finalState">允许的最终 state。</param>
            /// <param name="phase">用于错误信息的阶段名称。</param>
            private static void EnsureStateUnchanged(JObject currentState, JObject allowedPreviousState, JObject finalState, string phase)
            {
                if (!JToken.DeepEquals(currentState, allowedPreviousState) && !JToken.DeepEquals(currentState, finalState))
                {
                    throw new InvalidOperationException($"受管状态在 {phase} 发生变化，拒绝覆盖并等待人工处理。");
                }
            }

            /// <summary>
            /// 校验 action 中 source/target hash 与最终 state 的同一 Skill 记录一致。
            /// </summary>
            /// <param name="finalManaged">最终受管记录。</param>
            /// <param name="skillId">Skill id。</param>
            /// <param name="item">事务 action。</param>
            private static void EnsureFinalManagedHashes(JObject finalManaged, string skillId, JObject item)
            {
                JToken entry = finalManaged[skillId];
                if (entry == null)
                {
                    throw new InvalidOperationException($"受管事务缺少 {skillId} 的最终哈希。");
                }

                string sourceHash = ReadRequiredHash(item, "sourceHash", skillId);
                string targetHash = ReadRequiredHash(item, "targetHash", skillId);
                if (!string.Equals(ReadManagedHash(entry, "sourceHash", skillId), sourceHash, StringComparison.Ordinal)
                    || !string.Equals(ReadManagedHash(entry, "targetHash", skillId), targetHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"受管事务中 {skillId} 的哈希与 finalState 不一致。");
                }
            }

            /// <summary>
            /// 构建首发受管 state，不记录机器绝对路径或选择性安装信息。
            /// </summary>
            /// <param name="packageVersion">Framework 包版本。</param>
            /// <param name="catalogHash">Catalog 内容 hash。</param>
            /// <param name="managed">最终受管记录。</param>
            /// <returns>新的 state JSON 对象。</returns>
            private static JObject CreateState(string packageVersion, string catalogHash, JObject managed)
            {
                ValidateHash(catalogHash, "catalogHash");
                return new JObject
                {
                    ["schemaVersion"] = c_StateSchemaVersion,
                    ["package"] = c_FrameworkPackageName,
                    ["packageVersion"] = packageVersion,
                    ["catalogHash"] = catalogHash,
                    ["managed"] = managed.DeepClone(),
                };
            }

            /// <summary>
            /// 构建单个受管 Skill 的 source/target hash 记录。
            /// </summary>
            /// <param name="sourceHash">真源目录 hash。</param>
            /// <param name="targetHash">投影目录 hash。</param>
            /// <returns>受管记录对象。</returns>
            private static JObject CreateManagedEntry(string sourceHash, string targetHash)
            {
                ValidateHash(sourceHash, "sourceHash");
                ValidateHash(targetHash, "targetHash");
                return new JObject
                {
                    ["sourceHash"] = sourceHash,
                    ["targetHash"] = targetHash,
                };
            }

            /// <summary>
            /// 校验 state header 的版本、包名与包版本。
            /// </summary>
            /// <param name="state">待校验 state。</param>
            /// <param name="schemaVersion">预期 schema 版本。</param>
            /// <param name="path">用于错误信息的 state 路径。</param>
            private static void ValidateStateHeader(JObject state, int schemaVersion, string path)
            {
                if ((int?)state["schemaVersion"] != schemaVersion)
                {
                    throw new InvalidOperationException($"受管状态 schemaVersion 不受支持：{path}。");
                }

                if (!string.Equals((string)state["package"], c_FrameworkPackageName, StringComparison.Ordinal)
                    || string.IsNullOrEmpty((string)state["packageVersion"]))
                {
                    throw new InvalidOperationException($"受管状态未声明正确的 Framework 包身份：{path}。");
                }
            }

            /// <summary>
            /// 校验并复制 managed 对象，防止损坏 state 取得用户目录所有权。
            /// </summary>
            /// <param name="token">managed JSON token。</param>
            /// <param name="path">用于错误信息的 state 路径。</param>
            /// <returns>规范化 managed 对象。</returns>
            private static JObject ValidateManagedEntries(JToken token, string path)
            {
                JObject managed = token as JObject;
                if (managed == null)
                {
                    throw new InvalidOperationException($"受管状态 managed 格式错误：{path}。");
                }

                var normalized = new JObject();
                foreach (JProperty property in managed.Properties())
                {
                    if (!IsManagedProjectSkillId(property.Name))
                    {
                        throw new InvalidOperationException($"受管状态包含非项目组 Skill id：{property.Name}。");
                    }

                    string sourceHash = ReadManagedHash(property.Value, "sourceHash", property.Name);
                    string targetHash = ReadManagedHash(property.Value, "targetHash", property.Name);
                    normalized[property.Name] = CreateManagedEntry(sourceHash, targetHash);
                }

                return normalized;
            }

            /// <summary>
            /// 从 managed entry 读取一个合法 SHA-256 值。
            /// </summary>
            /// <param name="entry">managed entry JSON token。</param>
            /// <param name="name">字段名。</param>
            /// <param name="skillId">Skill id。</param>
            /// <returns>合法 SHA-256 字符串。</returns>
            private static string ReadManagedHash(JToken entry, string name, string skillId)
            {
                JObject objectEntry = entry as JObject;
                string value = objectEntry == null ? null : (string)objectEntry[name];
                ValidateHash(value, $"{skillId}.{name}");
                return value;
            }

            /// <summary>
            /// 从事务动作读取一个合法 SHA-256 值。
            /// </summary>
            /// <param name="item">事务动作对象。</param>
            /// <param name="name">字段名。</param>
            /// <param name="skillId">Skill id。</param>
            /// <returns>合法 SHA-256 字符串。</returns>
            private static string ReadRequiredHash(JObject item, string name, string skillId)
            {
                string value = (string)item[name];
                ValidateHash(value, $"{skillId}.{name}");
                return value;
            }

            /// <summary>
            /// 校验 SHA-256 十六进制字符串。
            /// </summary>
            /// <param name="value">待校验值。</param>
            /// <param name="label">用于错误信息的字段标签。</param>
            private static void ValidateHash(string value, string label)
            {
                if (string.IsNullOrEmpty(value) || !s_Sha256Pattern.IsMatch(value))
                {
                    throw new InvalidOperationException($"{label} 不是合法 SHA-256 哈希。");
                }
            }

            /// <summary>
            /// 计算与 Python 工具相同的目录内容 hash：相对路径、NUL、原始字节、NUL。
            /// </summary>
            /// <param name="directory">普通目录路径。</param>
            /// <returns>小写 SHA-256 十六进制 hash。</returns>
            private static string ComputeTreeHash(string directory)
            {
                EnsureNormalDirectory(directory, "哈希目录");
                EnsureNoReparsePoints(directory);
                string root = AppendDirectorySeparator(Path.GetFullPath(directory));
                var files = new List<string>();
                CollectFiles(directory, files);
                files.Sort((left, right) => CompareUtf8Paths(
                    GetRelativePath(root, left).Replace('\\', '/'),
                    GetRelativePath(root, right).Replace('\\', '/')));
                using (SHA256 sha256 = SHA256.Create())
                {
                    foreach (string file in files)
                    {
                        byte[] relativePath = Encoding.UTF8.GetBytes(GetRelativePath(root, file).Replace('\\', '/'));
                        AppendHashBytes(sha256, relativePath);
                        AppendHashBytes(sha256, new byte[] { 0 });
                        AppendHashBytes(sha256, File.ReadAllBytes(file));
                        AppendHashBytes(sha256, new byte[] { 0 });
                    }

                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    return BytesToLowerHex(sha256.Hash);
                }
            }

            /// <summary>
            /// 计算单个文件的 SHA-256 内容 hash。
            /// </summary>
            /// <param name="path">文件路径。</param>
            /// <returns>小写 SHA-256 十六进制 hash。</returns>
            private static string ComputeFileHash(string path)
            {
                if (!File.Exists(path) || IsReparsePoint(path))
                {
                    throw new InvalidOperationException($"无法计算受管文件哈希：{path}。");
                }

                using (SHA256 sha256 = SHA256.Create())
                {
                    return BytesToLowerHex(sha256.ComputeHash(File.ReadAllBytes(path)));
                }
            }

            /// <summary>
            /// 收集目录树中的常规文件；调用方已提前拒绝所有重解析点。
            /// </summary>
            /// <param name="directory">当前目录。</param>
            /// <param name="files">输出文件集合。</param>
            private static void CollectFiles(string directory, List<string> files)
            {
                foreach (string entry in Directory.GetFileSystemEntries(directory))
                {
                    if (IsReparsePoint(entry))
                    {
                        throw new InvalidOperationException($"目录包含不允许的软链或 junction：{entry}。");
                    }

                    if (Directory.Exists(entry))
                    {
                        CollectFiles(entry, files);
                    }
                    else if (File.Exists(entry))
                    {
                        files.Add(entry);
                    }
                }
            }

            /// <summary>
            /// 向增量 SHA-256 写入一段字节。
            /// </summary>
            /// <param name="sha256">增量 SHA-256 对象。</param>
            /// <param name="bytes">待写入字节。</param>
            private static void AppendHashBytes(HashAlgorithm sha256, byte[] bytes)
            {
                sha256.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }

            /// <summary>
            /// 按 UTF-8 字节序比较已规范化的 POSIX 相对路径，保持与 Python bridge 一致。
            /// </summary>
            /// <param name="left">左侧 POSIX 相对路径。</param>
            /// <param name="right">右侧 POSIX 相对路径。</param>
            /// <returns>小于零表示左侧在前，零表示相同，大于零表示右侧在前。</returns>
            private static int CompareUtf8Paths(string left, string right)
            {
                byte[] leftBytes = Encoding.UTF8.GetBytes(left);
                byte[] rightBytes = Encoding.UTF8.GetBytes(right);
                int commonLength = Math.Min(leftBytes.Length, rightBytes.Length);
                for (int index = 0; index < commonLength; index++)
                {
                    int comparison = leftBytes[index].CompareTo(rightBytes[index]);
                    if (comparison != 0)
                    {
                        return comparison;
                    }
                }

                return leftBytes.Length.CompareTo(rightBytes.Length);
            }

            /// <summary>
            /// 将字节数组转为小写十六进制字符串。
            /// </summary>
            /// <param name="bytes">哈希字节数组。</param>
            /// <returns>小写十六进制文本。</returns>
            private static string BytesToLowerHex(byte[] bytes)
            {
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }

            /// <summary>
            /// 深复制普通目录，不跟随软链或 junction。
            /// </summary>
            /// <param name="sourceDirectory">源目录。</param>
            /// <param name="targetDirectory">目标目录，必须尚不存在。</param>
            private static void CopyDirectory(string sourceDirectory, string targetDirectory)
            {
                EnsureNormalDirectory(sourceDirectory, "Skill 真源目录");
                EnsureNoReparsePoints(sourceDirectory);
                if (Directory.Exists(targetDirectory) || File.Exists(targetDirectory))
                {
                    throw new InvalidOperationException($"staging 目标已存在：{targetDirectory}。");
                }

                Directory.CreateDirectory(targetDirectory);
                foreach (string entry in Directory.GetFileSystemEntries(sourceDirectory))
                {
                    if (IsReparsePoint(entry))
                    {
                        throw new InvalidOperationException($"Skill 真源包含软链或 junction：{entry}。");
                    }

                    string target = Path.Combine(targetDirectory, Path.GetFileName(entry));
                    if (Directory.Exists(entry))
                    {
                        CopyDirectory(entry, target);
                    }
                    else if (File.Exists(entry))
                    {
                        File.Copy(entry, target, false);
                    }
                }
            }

            /// <summary>
            /// 将 JSON 写入同目录临时文件，再用原子替换提交 state 或事务日志。
            /// </summary>
            /// <param name="path">目标 JSON 文件路径。</param>
            /// <param name="value">待写入 JSON 对象。</param>
            private static void WriteJsonAtomically(string path, JObject value)
            {
                string directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}");
                try
                {
                    File.WriteAllText(temporaryPath, value.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
                    if (File.Exists(path))
                    {
                        File.Replace(temporaryPath, path, null);
                    }
                    else
                    {
                        File.Move(temporaryPath, path);
                    }
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
            }

            /// <summary>
            /// 读取 JSON 对象，不接受数组、文本或损坏内容。
            /// </summary>
            /// <param name="path">JSON 文件路径。</param>
            /// <returns>已解析 JSON 对象。</returns>
            private static JObject ReadJsonObject(string path)
            {
                if (!File.Exists(path) || IsReparsePoint(path))
                {
                    throw new InvalidOperationException($"缺少或不安全的 JSON 文件：{path}。");
                }

                try
                {
                    JObject result = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                    return result;
                }
                catch (JsonException exception)
                {
                    throw new InvalidOperationException($"JSON 无法解析：{path}。", exception);
                }
            }

            /// <summary>
            /// 在文件存在时读取 JSON；首次投影没有 state 时返回 null。
            /// </summary>
            /// <param name="path">JSON 文件路径。</param>
            /// <returns>JSON 对象或 null。</returns>
            private static JObject ReadJsonObjectIfExists(string path)
            {
                return File.Exists(path) ? ReadJsonObject(path) : null;
            }

            /// <summary>
            /// 从 JSON 对象读取非空字符串字段。
            /// </summary>
            /// <param name="objectValue">JSON 对象。</param>
            /// <param name="name">字段名。</param>
            /// <param name="label">错误信息主体。</param>
            /// <returns>非空字符串值。</returns>
            private static string ReadRequiredString(JObject objectValue, string name, string label)
            {
                string value = (string)objectValue[name];
                if (string.IsNullOrEmpty(value))
                {
                    throw new InvalidOperationException($"{label} 缺少 {name}。");
                }

                return value;
            }

            /// <summary>
            /// 判断目录内容是否正好匹配预期 hash。
            /// </summary>
            /// <param name="directory">目录路径。</param>
            /// <param name="expectedHash">预期 hash。</param>
            /// <returns>匹配时返回 true。</returns>
            private static bool DirectoryHashEquals(string directory, string expectedHash)
            {
                return Directory.Exists(directory)
                    && !IsReparsePoint(directory)
                    && string.Equals(ComputeTreeHash(directory), expectedHash, StringComparison.Ordinal);
            }

            /// <summary>
            /// 断言目录存在、不是链接且内容 hash 匹配。
            /// </summary>
            /// <param name="directory">目录路径。</param>
            /// <param name="expectedHash">预期 hash。</param>
            /// <param name="message">不匹配时的错误信息。</param>
            private static void EnsureDirectoryHash(string directory, string expectedHash, string message)
            {
                if (!DirectoryHashEquals(directory, expectedHash))
                {
                    throw new InvalidOperationException(message);
                }
            }

            /// <summary>
            /// 安全删除 bridge 自己创建的 staging 目录。
            /// </summary>
            /// <param name="directory">待删除 staging 目录。</param>
            private static void DeleteDirectorySafely(string directory)
            {
                EnsureNormalDirectory(directory, "受管 staging 目录");
                EnsureNoReparsePoints(directory);
                Directory.Delete(directory, true);
            }

            /// <summary>
            /// 尝试删除空目录，不把非空目录视为错误。
            /// </summary>
            /// <param name="directory">候选空目录。</param>
            private static void TryDeleteEmptyDirectory(string directory)
            {
                if (!Directory.Exists(directory) || IsReparsePoint(directory))
                {
                    return;
                }

                try
                {
                    Directory.Delete(directory, false);
                }
                catch (IOException)
                {
                    // 同一父目录存在其它事务时保留。
                }
            }

            /// <summary>
            /// 将用户输入目录规范化为绝对路径并确认存在。
            /// </summary>
            /// <param name="path">待规范化目录。</param>
            /// <param name="label">错误信息标签。</param>
            /// <returns>规范化绝对路径。</returns>
            private static string NormalizeDirectory(string path, string label)
            {
                if (string.IsNullOrEmpty(path))
                {
                    throw new ArgumentException($"{label}为空。", nameof(path));
                }

                string normalized = Path.GetFullPath(path);
                EnsureNormalDirectory(normalized, label);
                return normalized;
            }

            /// <summary>
            /// 获取不允许离开 Agents 根的 Catalog 相对目录。
            /// </summary>
            /// <param name="root">Agents 根目录。</param>
            /// <param name="relativePath">Catalog 相对路径。</param>
            /// <param name="skillId">用于错误信息的 Skill id。</param>
            /// <returns>安全绝对目录路径。</returns>
            private static string GetSafeChild(string root, string relativePath, string skillId)
            {
                if (string.IsNullOrEmpty(relativePath))
                {
                    throw new InvalidOperationException($"{skillId} 缺少 path。");
                }

                string normalizedRoot = AppendDirectorySeparator(Path.GetFullPath(root));
                string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
                if (!candidate.StartsWith(normalizedRoot, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"{skillId} 的 path 越过 Agents 真源边界。");
                }

                return candidate;
            }

            /// <summary>
            /// 确保存在路径不是软链或 junction；尚不存在的首次投影路径合法。
            /// </summary>
            /// <param name="path">待检查路径。</param>
            /// <param name="label">错误信息标签。</param>
            private static void EnsureNotReparsePoint(string path, string label)
            {
                if (IsReparsePoint(path))
                {
                    throw new InvalidOperationException($"受管投影路径不能是软链或 junction：{label}。");
                }
            }

            /// <summary>
            /// 确保已存在路径是普通目录；不存在时允许后续首次创建。
            /// </summary>
            /// <param name="path">待检查目录路径。</param>
            /// <param name="label">错误信息标签。</param>
            private static void EnsureExpectedDirectory(string path, string label)
            {
                if ((Directory.Exists(path) || File.Exists(path)) && !Directory.Exists(path))
                {
                    throw new InvalidOperationException($"{label} 必须是目录，拒绝修改受管投影。");
                }
            }

            /// <summary>
            /// 确保已存在路径是普通文件；不存在时允许后续首次创建。
            /// </summary>
            /// <param name="path">待检查文件路径。</param>
            /// <param name="label">错误信息标签。</param>
            private static void EnsureExpectedFile(string path, string label)
            {
                if ((Directory.Exists(path) || File.Exists(path)) && !File.Exists(path))
                {
                    throw new InvalidOperationException($"{label} 必须是普通文件，拒绝修改受管投影。");
                }
            }

            /// <summary>
            /// 确保指定路径当前存在且是普通目录。
            /// </summary>
            /// <param name="path">目录路径。</param>
            /// <param name="label">错误信息标签。</param>
            private static void EnsureNormalDirectory(string path, string label)
            {
                if (IsReparsePoint(path) || !Directory.Exists(path))
                {
                    throw new InvalidOperationException($"{label}不存在、不是目录或是软链/junction：{path}。");
                }
            }

            /// <summary>
            /// 递归拒绝目录树中的重解析点，禁止复制、hash 或删除越过真源/事务边界。
            /// </summary>
            /// <param name="directory">普通目录路径。</param>
            private static void EnsureNoReparsePoints(string directory)
            {
                EnsureNotReparsePoint(directory, directory);
                foreach (string entry in Directory.GetFileSystemEntries(directory))
                {
                    if (IsReparsePoint(entry))
                    {
                        throw new InvalidOperationException($"目录包含不允许的软链或 junction：{entry}。");
                    }

                    if (Directory.Exists(entry))
                    {
                        EnsureNoReparsePoints(entry);
                    }
                }
            }

            /// <summary>
            /// 判断现有文件系统条目是否为软链、junction 或其它重解析点。
            /// </summary>
            /// <param name="path">待检查路径。</param>
            /// <returns>重解析点时返回 true；不存在时返回 false。</returns>
            private static bool IsReparsePoint(string path)
            {
                try
                {
                    return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
                }
                catch (FileNotFoundException)
                {
                    return false;
                }
                catch (DirectoryNotFoundException)
                {
                    return false;
                }
            }

            /// <summary>
            /// 将目录根标准化为带末尾分隔符的路径。
            /// </summary>
            /// <param name="path">目录路径。</param>
            /// <returns>带单个末尾分隔符的路径。</returns>
            private static string AppendDirectorySeparator(string path)
            {
                return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? path
                    : path + Path.DirectorySeparatorChar;
            }

            /// <summary>
            /// 获取已知位于根目录下的文件相对路径。
            /// </summary>
            /// <param name="rootWithSeparator">带末尾分隔符的根目录。</param>
            /// <param name="file">绝对文件路径。</param>
            /// <returns>相对路径。</returns>
            private static string GetRelativePath(string rootWithSeparator, string file)
            {
                string fullFile = Path.GetFullPath(file);
                if (!fullFile.StartsWith(rootWithSeparator, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"文件越过 hash 根目录边界：{file}。");
                }

                return fullFile.Substring(rootWithSeparator.Length);
            }
        }
    }
}
