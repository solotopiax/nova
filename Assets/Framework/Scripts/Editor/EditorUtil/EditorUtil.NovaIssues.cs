/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.NovaIssues.cs
 * author:    taoye
 * created:   2026/11/23
 * descrip:   采集 Editor 实际证据并通过 Nova 问题队列自动上报。
 ***************************************************************/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlCipher4Unity3D;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;
using EditorPlayEvidence = NovaFramework.Runtime.EditorPlayEvidence;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>采集消费工程的 Editor 证据，通过本地队列向固定服务器发送。</summary>
        [InitializeOnLoad]
        internal static partial class NovaIssues
        {
            private const string c_Framework = "com.solotopia.nova.framework";
            private const string c_Server = "http://172.16.22.175:8787";
            private const string c_WorkDirectory = "Library/Nova/Issues";
            private const string c_PendingReloadKey = "Nova.Issues.PendingObservations";
            private const string c_EnvironmentReportKey = "editor_environment_report";
            private const int c_EnvironmentCheckInterval = 3600;
            // 两个发送端均按服务确认的能力决定新报告的格式，已入队的正文固定不变。
            private const string c_CompactReportsKey = "compact_reports";
            private const int c_ReceiptRetentionSeconds = 30 * 86400;
            private const int c_ReportBatchSize = 8;
            private const long c_MinimumReclaimBytes = 1024 * 1024;
            private static readonly Regex s_Key = new(@"\A[a-zA-Z0-9][a-zA-Z0-9_.-]{0,95}\z");
            private static readonly Regex s_Version = new(@"\A[a-zA-Z0-9][a-zA-Z0-9.+-]{0,79}\z");
            private static readonly Regex s_ApplicationIdentifier = new(@"\A[a-zA-Z0-9_-]+(?:\.[a-zA-Z0-9_-]+)+\z");
            private static readonly Regex s_Private = new(@"(?:https?|ssh|file)://|[A-Z]:[\\/]|/(?:Users|home|private|tmp)/|\b(?:api[_-]?key|access[_-]?token|password|secret)\s*[:=]|\bbearer\s|PRIVATE KEY", RegexOptions.IgnoreCase);
            private static readonly HttpClient s_Client = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
            {
                Timeout = TimeSpan.FromSeconds(3),
                MaxResponseContentBufferSize = 4096
            };
            private static readonly ConcurrentQueue<JObject> s_Pending = new();
            private static readonly string s_Project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            private static string s_FrameworkRoot;
            private static string s_FrameworkVersion;
            private static bool s_FrameworkFromRegistry;
            private static double s_NextWorkspaceCheck;
            private static long s_LastEnvironmentCheck = -1;
            private static long s_QueuedEnvironmentCheck;
            private static int s_Sending;
            private static bool s_BatchFlushed;

            /// <summary>普通 Editor 采集环境，批处理退出时补齐实际的构建结果。</summary>
            static NovaIssues()
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.quitting += FlushBatchObservations;
                    return;
                }

                EditorApplication.update += Update;
                CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
                EditorPlayEvidence.TablesReady += ObserveTablesReady;
                EditorPlayEvidence.ConfigLoadFinished += ObserveRuntimeConfig;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                AssemblyReloadEvents.beforeAssemblyReload += BeforeObservationReload;
                EditorApplication.quitting += FlushObservationsBeforeQuit;
                RestoreObservationsAfterReload();
            }

            /// <summary>环境按工作区每小时检查，其他编辑器证据沿用各自的采集频率。</summary>
            private static void Update()
            {
                double stamp = EditorApplication.timeSinceStartup;
                PollPlayerBuild(stamp);
                PollPlayCalls(stamp);
                if (s_FrameworkRoot != null)
                    PollEditorDiagnostics(stamp);
                if (stamp < s_NextWorkspaceCheck || EditorApplication.isCompiling || EditorApplication.isUpdating)
                    return;

                s_NextWorkspaceCheck = stamp + 30;
                if (!BindFrameworkPackage())
                    return;

                PackageInfo[] packages = PackageInfo.GetAllRegisteredPackages();
                ObserveEditorStartup();
                PollEditorDiagnostics(stamp);
                ObserveUnityMcpConnection(packages, stamp);
                if (!Config.WorkspaceActive.TryGetPersistedConfigMaster(out var master, out _, out _, out _))
                    master = null;
                long lastCheck = Volatile.Read(ref s_LastEnvironmentCheck);
                long now = Now();
                if (lastCheck >= 0 && now - Math.Max(lastCheck, s_QueuedEnvironmentCheck) >= c_EnvironmentCheckInterval)
                {
                    try
                    {
                        Queue(CaptureSnapshot(packages, master));
                        s_QueuedEnvironmentCheck = now;
                    }
                    catch (Exception) { /* 暂时未能完成环境检查，下次继续。 */ }
                }
                try
                {
                    Queue(new JObject { ["event"] = "implementation", ["implementation"] = CaptureImplementationWorkspace(packages, master) });
                }
                catch (Exception) { /* 实施采集失败时继续刷新待发记录。 */ StartDrain(); }
            }

            /// <summary>以实际安装的 Framework 识别消费工程，供快照和编译结果共用。</summary>
            private static bool BindFrameworkPackage()
            {
                var framework = PackageInfo.FindForAssembly(typeof(EditorUtil).Assembly);
                if (framework?.name != c_Framework || string.IsNullOrEmpty(framework.resolvedPath))
                {
                    s_FrameworkRoot = null;
                    s_FrameworkVersion = null;
                    s_FrameworkFromRegistry = false;
                    return false;
                }

                s_FrameworkRoot = Path.GetFullPath(framework.resolvedPath);
                s_FrameworkVersion = ReadVersion(framework.version);
                s_FrameworkFromRegistry = framework.source == UnityEditor.PackageManager.PackageSource.Registry;
                return true;
            }

            /// <summary>将允许上报的字段复制到普通 JSON，避免采集资产内容和诊断原文。</summary>
            private static JObject CaptureSnapshot(PackageInfo[] packages, ConfigMasterSO master)
            {
                var registered = (packages ?? Array.Empty<PackageInfo>())
                    .Where(package => package != null && IsKey(package.name) && !package.name.StartsWith("com.unity.modules.", StringComparison.Ordinal)
                                      && (package.errors == null || package.errors.Length == 0))
                    .OrderBy(package => package.name, StringComparer.Ordinal)
                    .Take(350)
                    .Select(package => new JObject
                    {
                        ["id"] = package.name,
                        ["version"] = ReadVersion(package.version),
                        ["source"] = PackageSourceName(package.source.ToString()),
                        ["evidence"] = "registered"
                    });
                var tools = new JObject();
                var report = AgentSkills.LastToolchainReport;
                if (report != null)
                {
                    var cli = report.Items.FirstOrDefault(item => item.Id == "unity-cli");
                    var connection = report.Items.FirstOrDefault(item => item.Id == "unity-cli-binding");
                    if (cli != null)
                    {
                        tools["unity-cli"] = new JObject
                        {
                            ["state"] = connection?.Status == "success" ? "connected" :
                                cli.Status == "success" ? "installed" : "not_observed"
                        };
                    }
                }
                var result = new JObject
                {
                    ["event"] = "snapshot",
                    ["unity"] = ReadVersion(Application.unityVersion),
                    ["editor_framework"] = ReadVersion(global::NovaFramework.Runtime.Nova.Version),
                    ["packages"] = new JArray(registered),
                    ["tools"] = tools,
                    ["enabled"] = new JArray(),
                    ["application_identifiers"] = CaptureApplicationIdentifiers()
                };
                string operatingSystem = Application.platform switch
                {
                    RuntimePlatform.OSXEditor => "macos",
                    RuntimePlatform.WindowsEditor => "windows",
                    RuntimePlatform.LinuxEditor => "linux",
                    _ => null
                };
                if (operatingSystem != null)
                    result["operating_system"] = operatingSystem;
                if (master != null)
                {
                    result["enabled"] = new JArray(
                        (master.EnabledSDKs ?? new System.Collections.Generic.List<string>())
                        .Concat(master.EnabledKits ?? new System.Collections.Generic.List<string>()).Where(IsKey).Take(100));
                }
                return result;
            }

            /// <summary>读取 Android、iOS 和当前其他构建平台的 Player Settings 应用标识。</summary>
            private static JObject CaptureApplicationIdentifiers()
            {
                var identifiers = new JObject();
                var activeGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
                foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.iOS, activeGroup }.Distinct())
                {
                    if (group == BuildTargetGroup.Unknown)
                        continue;
                    var target = NamedBuildTarget.FromBuildTargetGroup(group);
                    string identifier = PlayerSettings.GetApplicationIdentifier(target);
                    if (identifier != null && identifier.Length <= 255 && s_ApplicationIdentifier.IsMatch(identifier))
                        identifiers[group == BuildTargetGroup.iOS ? "iOS" : group.ToString()] = identifier;
                }
                return identifiers;
            }

            /// <summary>为新 Play 重置表注册观察，在退出 Play 时提交已归集的调用。</summary>
            private static void OnPlayModeChanged(PlayModeStateChange change)
            {
                if (change == PlayModeStateChange.ExitingEditMode)
                {
                    s_PlayAttempt = null;
                    SessionState.EraseString(c_PlayAttemptKey);
                    EditorPlayEvidence.ResetTableRegistration();
                }
                else if (change is PlayModeStateChange.ExitingPlayMode or PlayModeStateChange.EnteredEditMode)
                    FlushPlayCalls();
            }

            /// <summary>在 Inspector 的原导出入口记录实际终态，空值表示缺少执行条件。</summary>
            internal static bool RunManualExport(string module, string scope, Func<bool?> export)
            {
                string outcome = "failed";
                try
                {
                    bool? succeeded = export();
                    outcome = succeeded == null ? "blocked" : succeeded.Value ? "success" : "failed";
                    return succeeded == true;
                }
                catch (OperationCanceledException)
                {
                    outcome = "cancelled";
                    throw;
                }
                finally
                {
                    ObserveUsage(module, "editor." + module + ".export-" + scope, outcome);
                }
            }

            /// <summary>将固定的编辑器操作和真实结果加入本地队列。</summary>
            private static void ObserveUsage(string module, string operation, string outcome, string actionOperation = null)
            {
                if (Application.isBatchMode)
                    return;
                try
                {
                    if (!BindFrameworkPackage())
                        return;
                    JObject data = Usage(module, operation, outcome, s_FrameworkVersion);
                    var input = new JObject { ["event"] = "usage", ["usage"] = data };
                    if (actionOperation != null)
                    {
                        data["entry"] = "action";
                        data["action_operation"] = actionOperation;
                        input["id"] = actionOperation;
                    }
                    Queue(input);
                }
                catch (Exception)
                {
                    // 反馈异常不改变编辑器操作的结果。
                }
            }

            /// <summary>将有限的工作交给后台发送线程，确保 Unity 主线程可以继续。</summary>
            private static void Queue(JObject input)
            {
                input["id"] ??= Guid.NewGuid().ToString("N");
                input["framework"] = s_FrameworkVersion;
                input["framework_root"] = s_FrameworkRoot;
                input["framework_registry"] = s_FrameworkFromRegistry;
                input["product"] = PlayerSettings.productGUID.ToString();
                input["name"] = SafeName(Application.productName);
                input["observed_at"] ??= Now();
                s_Pending.Enqueue(input);
                if (!Application.isBatchMode)
                    StartDrain();
            }

            /// <summary>把尚未落盘的证据跨域重载保存，保留最初编号和发生时间。</summary>
            private static void BeforeObservationReload()
            {
                FlushPlayCalls();
                JObject[] pending = s_Pending.ToArray();
                if (pending.Length != 0)
                    SessionState.SetString(c_PendingReloadKey,
                        new JArray(pending.Select(item => item.DeepClone())).ToString(Formatting.None));
            }

            /// <summary>域恢复后重入等待队列，SQLite 按原编号去重。</summary>
            private static void RestoreObservationsAfterReload()
            {
                string cached = SessionState.GetString(c_PendingReloadKey, string.Empty);
                if (cached.Length == 0)
                    return;
                SessionState.EraseString(c_PendingReloadKey);
                try
                {
                    foreach (var item in JArray.Parse(cached).OfType<JObject>())
                        s_Pending.Enqueue(item);
                    StartDrain();
                }
                catch (JsonException) { /* 无效的会话缓存不会阻止 Editor 启动。 */ }
            }

            internal static void FlushBatchObservations()
            {
                if (!Application.isBatchMode || s_BatchFlushed)
                    return;
                s_BatchFlushed = true;
                TryObserveLatestPlayerBuild(s_EditorStartedUtc);
                FlushObservationsBeforeQuit();
            }

            /// <summary>正常退出时补写本地账本，批处理会话另尝试发送本轮有终态的记录。</summary>
            private static void FlushObservationsBeforeQuit()
            {
                FlushPlayCalls();
                JObject[] pending = s_Pending.ToArray();
                if (pending.Length == 0)
                    return;
                try
                {
                    var storage = LedgerPath();
                    if (storage.Path == null)
                        return;
                    using var db = new SQLiteConnection(storage.Path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
                    db.BusyTimeout = TimeSpan.FromMilliseconds(250);
                    InitializeLedger(db);
                    foreach (var item in pending)
                    {
                        // 快照需要后台工具探测，退出时只补写已有确切终态的事件。
                        if ((string)item["event"] != "snapshot")
                            db.RunInTransaction(() => Record(db, (JObject)item.DeepClone()));
                    }
                    if (Application.isBatchMode)
                        SendPending(db, limit: 2);
                }
                catch (Exception) { /* 退出不会等待争用的账本。 */ }
            }

            /// <summary>在后台串行落盘后发送，失败的待发记录留在本地。</summary>
            private static void StartDrain()
            {
                if (Interlocked.CompareExchange(ref s_Sending, 1, 0) != 0)
                    return;

                _ = Task.Run(() =>
                {
                    bool drained = false;
                    try
                    {
                        var storage = LedgerPath();
                        if (s_Pending.ToArray().FirstOrDefault(item =>
                                (string)item["event"] == "snapshot" || (string)item["implementation"]?["stage"] == "workspace") is JObject first)
                            PrepareCodexHooksIfNeeded(s_Project, first, storage);
                        if (storage.Path == null)
                        {
                            if (!storage.Retryable)
                                while (s_Pending.TryDequeue(out _)) { }
                            return;
                        }

                        using var db = new SQLiteConnection(storage.Path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
                        InitializeLedger(db);
                        ReadEnvironmentCheck(db);
                        PruneDeliveredReports(db);
                        while (s_Pending.TryPeek(out JObject pending))
                        {
                            // 工作线程只编辑副本，重载回执可以同时读取原始证据。
                            var input = (JObject)pending.DeepClone();
                            if ((string)input["event"] == "snapshot")
                            {
                                var (hookState, addedHooks, expectedHooks) = ObserveCodexHookStatus(s_Project, Path.GetDirectoryName(storage.Path));
                                input["codex_hooks"] = hookState;
                                if (addedHooks.HasValue)
                                    input["codex_hook_counts"] = new JObject { ["added"] = addedHooks.Value, ["expected"] = expectedHooks };
                                input["git_user_name"] = ReadGitUserName(s_Project);
                            }
                            db.RunInTransaction(() => Record(db, input));
                            if ((string)input["event"] == "snapshot")
                                ReadEnvironmentCheck(db);
                            s_Pending.TryDequeue(out _);
                        }
                        drained = true;
                        SendPending(db);
                        ReclaimLedgerSpace(db);
                    }
                    catch (Exception)
                    {
                        // 数据库争用或服务不可用时，下个 Editor 观察点继续处理。
                    }
                    finally
                    {
                        Volatile.Write(ref s_Sending, 0);
                        if (drained && !s_Pending.IsEmpty)
                            StartDrain();
                    }
                });
            }

            /// <summary>初始化共用队列，保留具备 SQLite JSON 功能的旧发送端使用的确认触发器。</summary>
            internal static void InitializeLedger(SQLiteConnection db)
            {
                db.EnableWriteAheadLogging();
                db.Execute("CREATE TABLE IF NOT EXISTS state (key TEXT PRIMARY KEY, value TEXT NOT NULL)");
                db.Execute(@"CREATE TABLE IF NOT EXISTS outbox (id TEXT PRIMARY KEY, payload TEXT NOT NULL,
                    attempts INTEGER NOT NULL DEFAULT 0, ready_at INTEGER NOT NULL, accepted INTEGER, issue_id INTEGER, error TEXT)");
                db.Execute("CREATE INDEX IF NOT EXISTS ready_reports ON outbox(accepted, ready_at)");
                db.Execute(@"CREATE TABLE IF NOT EXISTS report_receipts (
                    id TEXT PRIMARY KEY, kind TEXT NOT NULL, accepted INTEGER NOT NULL, issue_id INTEGER
                ) WITHOUT ROWID");
                db.Execute("CREATE INDEX IF NOT EXISTS report_receipts_by_time ON report_receipts(accepted)");
                db.Execute(@"CREATE TRIGGER IF NOT EXISTS skip_delivered_report BEFORE INSERT ON outbox
                    WHEN EXISTS (SELECT 1 FROM report_receipts WHERE id=NEW.id)
                    BEGIN
                        SELECT RAISE(IGNORE);
                    END");
                db.Execute(@"CREATE TRIGGER IF NOT EXISTS discard_delivered_report AFTER UPDATE OF accepted ON outbox
                    WHEN NEW.accepted IS NOT NULL
                    BEGIN
                        INSERT OR IGNORE INTO report_receipts (id,kind,accepted,issue_id)
                            VALUES (NEW.id,COALESCE(json_extract(NEW.payload,'$.kind'),'unknown'),NEW.accepted,NEW.issue_id);
                        DELETE FROM outbox WHERE id=NEW.id;
                    END");
            }

            /// <summary>迁移旧版已确认的报告，回执保留原去重时长，待发和待归因证据继续留在队列。</summary>
            internal static void PruneDeliveredReports(SQLiteConnection db)
            {
                var delivered = db.Query<DeliveredReport>(@"SELECT id AS Id,payload AS Payload,
                    accepted AS Accepted,issue_id AS IssueId FROM outbox WHERE accepted IS NOT NULL");
                long cutoff = Now() - c_ReceiptRetentionSeconds;
                db.RunInTransaction(() =>
                {
                    foreach (var report in delivered)
                    {
                        string kind = ReportText(ReadQueuedReport(report.Payload), "kind") ?? "unknown";
                        StoreReceipt(db, report.Id, kind, report.Accepted, report.IssueId);
                    }
                    db.Execute("DELETE FROM report_receipts WHERE accepted<?", cutoff);
                });
            }

            /// <summary>在调用方的事务内转存已确认报告，沿用最先写入的回执。</summary>
            private static void StoreReceipt(SQLiteConnection db, string id, string kind, long accepted, long? issueId)
            {
                db.Execute("INSERT OR IGNORE INTO report_receipts (id,kind,accepted,issue_id) VALUES (?,?,?,?)",
                    id, kind, accepted, issueId);
                db.Execute("DELETE FROM outbox WHERE id=?", id);
            }

            /// <summary>确认成功时在同一事务保留回执并删除正文，无需执行旧发送端的触发器。</summary>
            internal static void AcknowledgeReport(SQLiteConnection db, string id, string kind, long accepted, long? issueId)
            {
                db.RunInTransaction(() => StoreReceipt(db, id, kind, accepted, issueId));
            }

            /// <summary>当账本积累了足够的空闲页时在后台归还磁盘空间，争用时由下一次处理继续。</summary>
            private static void ReclaimLedgerSpace(SQLiteConnection db)
            {
                long freePages = db.ExecuteScalar<long>("PRAGMA freelist_count");
                if (freePages * db.ExecuteScalar<long>("PRAGMA page_size") >= c_MinimumReclaimBytes &&
                    freePages * 2 >= db.ExecuteScalar<long>("PRAGMA page_count"))
                {
                    db.Execute("VACUUM");
                    db.QueryScalars<long>("PRAGMA wal_checkpoint(TRUNCATE)");
                }
            }

            /// <summary>核对工程的 Library 落盘路径，并给出阻塞授权入口准备的原因。</summary>
            private static (string Path, string Reason, bool Retryable) LedgerPath()
            {
                try
                {
                    bool gitProject = false;
                    for (var directory = new DirectoryInfo(s_Project); directory != null; directory = directory.Parent)
                    {
                        string marker = Path.Combine(directory.FullName, ".git");
                        if (!File.Exists(marker) && !Directory.Exists(marker)) continue;
                        gitProject = true;
                        break;
                    }
                    if (gitProject)
                    {
                        var ignored = CodexGit(s_Project, $"check-ignore -q -- {c_WorkDirectory}/");
                        if (!ignored.Success)
                            return !ignored.TimedOut && ignored.ExitCode == 1
                                ? (null, $"工程 Git 规则未忽略 {c_WorkDirectory}。", false)
                                : (null, c_CodexGitUnavailable, true);
                        var tracked = CodexGit(s_Project, $"ls-files -- {c_WorkDirectory}");
                        if (!tracked.Success)
                            return (null, c_CodexGitUnavailable, true);
                        if (!string.IsNullOrEmpty(tracked.Stdout))
                            return (null, $"工程 Git 已跟踪 {c_WorkDirectory} 下的文件。", false);
                    }
                    foreach (string relative in new[] { "Library", "Library/Nova", c_WorkDirectory,
                                 c_WorkDirectory + "/agent.sqlite3", c_WorkDirectory + "/agent.sqlite3-wal",
                                 c_WorkDirectory + "/agent.sqlite3-shm", c_WorkDirectory + "/agent.sqlite3-journal" })
                    {
                        if (IsLink(Path.Combine(s_Project, relative)))
                            return (null, $"反馈记录路径 {relative} 指向链接。", false);
                    }
                    string folder = Path.Combine(s_Project, c_WorkDirectory);
                    Directory.CreateDirectory(folder);
                    return (Path.Combine(folder, "agent.sqlite3"), null, false);
                }
                catch (UnauthorizedAccessException)
                {
                    return (null, $"当前账号缺少读写 {c_WorkDirectory} 的权限。", false);
                }
                catch (IOException)
                {
                    return (null, $"暂时无法读写 {c_WorkDirectory}。", true);
                }
            }

            /// <summary>通过文件属性拒绝把队列写到工程外部的符号链接。</summary>
            private static bool IsLink(string path)
            {
                try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
                catch (FileNotFoundException) { return false; }
                catch (DirectoryNotFoundException) { return false; }
            }

            /// <summary>复用 Codex Agent 的工作区、凭据和队列，对 Editor 事件归因。</summary>
            private static void Record(SQLiteConnection db, JObject input)
            {
                string workspace = RawState(db, "workspace");
                if (workspace == null)
                {
                    workspace = Guid.NewGuid().ToString("N");
                    db.Execute("INSERT INTO state VALUES ('workspace',?)", workspace);
                }
                JObject project = ReadState(db, "automatic_project") as JObject;
                if (project == null)
                {
                    string product = Guid.TryParse((string)input["product"], out Guid guid) && guid != Guid.Empty
                        ? guid.ToString("N") : workspace;
                    project = new JObject { ["id"] = "auto-" + product, ["name"] = input["name"].DeepClone() };
                    SaveState(db, "automatic_project", project);
                }
                if (RawState(db, "automatic_token") == null)
                {
                    var bytes = new byte[32];
                    using var random = RandomNumberGenerator.Create();
                    random.GetBytes(bytes);
                    SaveState(db, "automatic_token", new JValue(Hex(bytes)));
                }
                JToken destination = ReadState(db, "destination");
                if ((string)destination?["server"] != c_Server || (string)destination["project"] != (string)project["id"])
                    SaveState(db, "destination", new JObject { ["server"] = c_Server, ["project"] = project["id"].DeepClone() });

                long stamp = (long)input["observed_at"];
                if (RawState(db, "enrolled_at") == null) SaveState(db, "enrolled_at", new JValue(stamp));
                string id = (string)input["id"];
                switch ((string)input["event"])
                {
                    case "implementation":
                        var implementation = input["implementation"] as JObject;
                        if (ReportText(implementation, "stage") == "workspace")
                            RecordFrameworkUpgrade(db, input, workspace, stamp, ReportText(implementation, "editor_framework"));
                        RecordImplementation(db, implementation, workspace, id, stamp);
                        break;
                    case "editor-diagnostic":
                        var issue = (JObject)input["issue"];
                        AppendReport(db, workspace, EventId("editor-diagnostic", workspace, id), stamp, "issue", issue);
                        if (ReportText(input, "compiler_error") is string code)
                            RecordFrameworkUpgrade(db, input, workspace, stamp, null, code);
                        break;
                    case "snapshot":
                        RecordSnapshot(db, input, workspace, (string)project["id"], stamp);
                        break;
                    case "usage":
                        AppendReport(db, workspace, id, stamp, "usage", (JObject)input["usage"].DeepClone());
                        break;
                    case "workflow":
                        AppendReport(db, workspace, id, stamp, "workflow", (JObject)input["workflow"].DeepClone());
                        break;
                }
            }

            private static void ReadEnvironmentCheck(SQLiteConnection db) =>
                Volatile.Write(ref s_LastEnvironmentCheck, (long?)ReadState(db, c_EnvironmentReportKey)?["checked_at"] ?? 0);

            /// <summary>保存本次检查。首次采集或环境事实改变时，使用原发生时间入队。</summary>
            private static void RecordSnapshot(SQLiteConnection db, JObject input, string workspace, string projectId, long stamp)
            {
                JObject previous = ReadState(db, c_EnvironmentReportKey) as JObject;
                if (stamp - ((long?)previous?["checked_at"] ?? 0) < c_EnvironmentCheckInterval)
                    return;
                var data = new JObject();
                foreach (string field in new[] { "framework", "unity", "packages", "tools", "enabled",
                             "application_identifiers", "codex_hooks", "operating_system", "editor_framework", "codex_hook_counts" })
                {
                    if (input[field] != null) data[field] = input[field].DeepClone();
                }
                string gitName = (string)input["git_user_name"];
                if (gitName != null) data["git_user_name"] = gitName;
                var source = new JObject { ["at"] = stamp };
                foreach (var field in data.Properties()) source[field.Name] = field.Value.DeepClone();
                SaveState(db, "editor", source);
                SaveState(db, "editor_source", input["framework_root"]);
                long codexSeen = ReadState(db, "codex_seen")?.Value<long>() ?? 0;
                data["coverage"] = new JObject { ["agent"] = "observed", ["editor"] = "observed", ["codex"] = stamp - codexSeen < 1800 ? "observed" : "waiting" };

                // 与 Python 兼容入口共用签名，忽略清单顺序和短时活跃状态。
                var identifiers = (JObject)data["application_identifiers"];
                string appSignature = string.Join("|", identifiers.Properties().OrderBy(field => field.Name, StringComparer.Ordinal)
                    .Select(field => field.Name + "=" + (string)field.Value));
                string packageSignature = string.Join("\n", data["packages"].OrderBy(p => (string)p["id"], StringComparer.Ordinal)
                    .Select(p => $"{p["id"]}|{p["version"]}|{p["source"]}"));
                string enabledSignature = string.Join("\n", data["enabled"].Values<string>().Distinct().OrderBy(id => id, StringComparer.Ordinal));
                string toolSignature = string.Join("\n", ((JObject)data["tools"]).Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
                    .Select(p => $"{p.Name}|{p.Value["state"]}|{(string)p.Value["version"] ?? "unknown"}"));
                string[] signatureParts = { projectId, (string)data["framework"], (string)data["unity"], appSignature,
                    (string)data["codex_hooks"], packageSignature, enabledSignature, toolSignature, (string)data["operating_system"] };
                var hookCounts = data["codex_hook_counts"] as JObject;
                if (data["editor_framework"] != null || hookCounts != null)
                    signatureParts = signatureParts.Concat(new[] { (string)data["editor_framework"],
                        (string)hookCounts?["added"], (string)hookCounts?["expected"] }).ToArray();
                if (gitName != null) signatureParts = signatureParts.Append(gitName).ToArray();
                string signature = EventId(signatureParts);
                if ((string)previous?["hash"] != signature)
                    AppendReport(db, workspace, (string)input["id"], stamp, "snapshot", data);
                SaveState(db, c_EnvironmentReportKey, new JObject { ["hash"] = signature, ["checked_at"] = stamp });
            }

            /// <summary>在 Execute 结束时采集，避免随后验证覆盖执行时间或结果。</summary>
            internal static void ObserveActionExecution(string action, string operation, string outcome)
            {
                if (!IsKey(operation) || !IsKey(action) || !action.StartsWith("nova.project.", StringComparison.Ordinal) ||
                    outcome is not ("success" or "partial" or "blocked" or "failed" or "cancelled"))
                    return;
                string[] parts = action.Split('.');
                if (parts.Length >= 4)
                {
                    ObserveUsage(parts[2], action, outcome, EventId("action", action, operation));
                    ObserveActionWorkflow(action, operation, "execute", outcome);
                }
            }

            /// <summary>记录持久化 Action 回执的阶段，关联编号与 Codex 观察一致。</summary>
            internal static void ObserveActionWorkflow(string action, string operation, string stage, string outcome)
            {
                if (!IsKey(action) || !IsKey(operation) || !action.StartsWith("nova.project.", StringComparison.Ordinal))
                    return;
                string receipt = EventId("action", action, operation);
                ObserveWorkflow(stage, action, outcome, receipt, receipt);
            }

            private static void ObserveWorkflow(string stage, string operation, string outcome, string trace,
                string receipt = null, string reason = null)
            {
                if (outcome is not ("observed" or "executed" or "success" or "partial" or "blocked" or "failed" or "cancelled"))
                    return;
                try
                {
                    if (!BindFrameworkPackage())
                        return;
                    var data = new JObject
                    {
                        ["stage"] = stage, ["operation"] = operation, ["outcome"] = outcome,
                        ["source"] = "editor", ["trace"] = trace, ["framework"] = s_FrameworkVersion
                    };
                    if (receipt != null) data["receipt"] = receipt;
                    if (IsKey(reason)) data["reason"] = reason;
                    Queue(new JObject { ["event"] = "workflow", ["workflow"] = data });
                }
                catch (Exception) { /* 记录结果不改变操作本身。 */ }
            }

            /// <summary>写入固定上报信封；旧版服务需要的必填字段仅在兼容格式中保留。</summary>
            private static void AppendReport(SQLiteConnection db, string workspace, string id, long stamp, string kind, JObject data)
            {
                if (kind == "snapshot" && SupportsCompactReports(db))
                {
                    foreach (JObject package in (JArray)data["packages"])
                        package.Remove("evidence");
                }
                else if (kind == "issue")
                {
                    if (SupportsCompactReports(db))
                        data.Remove("priority");
                    else
                        data["priority"] = "P1";
                }

                var report = new JObject
                {
                    ["version"] = 1, ["id"] = id, ["workspace"] = workspace, ["observed_at"] = stamp,
                    ["kind"] = kind, ["data"] = data
                };
                db.Execute("INSERT OR IGNORE INTO outbox (id,payload,ready_at) VALUES (?,?,?)", id, report.ToString(Formatting.None), stamp);
            }

            /// <summary>判断本地账本上次确认的服务是否支持精简字段。</summary>
            private static bool SupportsCompactReports(SQLiteConnection db) =>
                (bool?)ReadState(db, c_CompactReportsKey) == true;

            /// <summary>按原内容发送待发报告，服务端确认原编号后转存回执。</summary>
            private static void SendPending(SQLiteConnection db, int limit = c_ReportBatchSize)
            {
                var reports = ReadPendingReports(db);
                if (reports.Count == 0) return;
                var project = (JObject)ReadState(db, "automatic_project");
                string token = (string)ReadState(db, "automatic_token");
                JObject health = Request(HttpMethod.Get, "/health");
                bool ready = (string)health?["service"] == "nova-issues-server" && (int?)health["protocol"] == 1 && (bool?)health["automatic_reporting"] == true;
                bool compactReports = (bool?)health?["compact_reports"] == true;
                if (ready && SupportsCompactReports(db) != compactReports)
                    SaveState(db, c_CompactReportsKey, new JValue(compactReports));
                foreach (var (row, report, kind) in reports.Take(limit))
                {
                    bool needsUpgrade = ready && RequiresServerUpgrade(health, kind, (JObject)report["data"], compactReports);
                    JObject receipt = ready && !needsUpgrade ? Request(HttpMethod.Post, "/api/v1/auto-reports", (string)project["id"], token,
                        new JObject { ["name"] = project["name"].DeepClone(), ["report"] = report }) : null;
                    long stamp = Now();
                    if (receipt?["accepted"]?.Type == JTokenType.Boolean && (bool)receipt["accepted"] &&
                        receipt["id"]?.Type == JTokenType.String && (string)receipt["id"] == row.Id)
                    {
                        db.RunInTransaction(() =>
                        {
                            if (row.Attempts > 0 && (bool?)health["workflow_monitoring"] == true)
                                ObserveQueueRecovery(db, report, row.Id);
                            StoreReceipt(db, row.Id, kind, stamp, (long?)receipt["issue_id"]);
                        });
                    }
                    else
                    {
                        db.RunInTransaction(() =>
                        {
                            if (db.Execute("UPDATE outbox SET attempts=attempts+1,error=?,ready_at=? WHERE id=? AND accepted IS NULL",
                                    !ready ? "service-unavailable" : needsUpgrade ? "server-upgrade-required" : "transport",
                                    stamp + Math.Min(300, 1L << Math.Min(row.Attempts + 1, 9)), row.Id) > 0 &&
                                !needsUpgrade && ReportText((JObject)report["data"], "stage") != "queue")
                                SaveState(db, "report_queue_interrupted", new JValue(true));
                        });
                    }
                }
            }

            /// <summary>失败后首次确认一份重试，记录此时仍在队列中的待发量和累计尝试次数。</summary>
            private static void ObserveQueueRecovery(SQLiteConnection db, JObject report, string id)
            {
                var data = (JObject)report["data"];
                if (ReportText(data, "stage") == "queue" ||
                    (bool?)ReadState(db, "report_queue_interrupted") != true ||
                    db.ExecuteScalar<int>("SELECT COUNT(*) FROM outbox WHERE id=? AND error IN ('service-unavailable','transport')", id) == 0)
                    return;
                const string pending = "FROM outbox WHERE accepted IS NULL AND COALESCE(json_extract(payload,'$.data.stage'),'')!='queue'";
                string trace = EventId("queue-resumed", id);
                AppendReport(db, ReportText(report, "workspace"), trace, Now(), "workflow", new JObject
                {
                    ["stage"] = "queue", ["operation"] = "reports-resumed", ["outcome"] = "success",
                    ["framework"] = ReportText(data, "framework") ?? "unknown", ["trace"] = trace, ["source"] = "editor",
                    ["pending"] = Math.Min(int.MaxValue, db.ExecuteScalar<long>("SELECT COUNT(*) " + pending)),
                    ["retries"] = Math.Min(int.MaxValue, db.ExecuteScalar<long>("SELECT COALESCE(SUM(attempts),0) " + pending))
                });
                SaveState(db, "report_queue_interrupted", new JValue(false));
            }

            /// <summary>先筛出可发送的记录，再按问题优先、就绪时间和入队顺序取一批。</summary>
            private static List<(PendingReport Row, JObject Report, string Kind)> ReadPendingReports(SQLiteConnection db)
            {
                var issues = new List<(PendingReport Row, JObject Report, string Kind)>(c_ReportBatchSize);
                var others = new List<(PendingReport Row, JObject Report, string Kind)>(c_ReportBatchSize);
                var ready = db.DeferredQuery<PendingReport>(@"SELECT id AS Id,payload AS Payload,attempts AS Attempts
                    FROM outbox WHERE accepted IS NULL AND ready_at<=? ORDER BY ready_at,rowid", Now());
                foreach (var row in ready)
                {
                    JObject report = ReadQueuedReport(row.Payload);
                    string kind = ReportText(report, "kind");
                    if (kind == null || report["data"] is not JObject data || !IsSendableReport(kind, data))
                        continue;

                    if (kind == "issue")
                    {
                        issues.Add((row, report, kind));
                        if (issues.Count == c_ReportBatchSize)
                            break;
                    }
                    else if (others.Count < c_ReportBatchSize)
                    {
                        others.Add((row, report, kind));
                    }
                }
                issues.AddRange(others.Take(c_ReportBatchSize - issues.Count));
                return issues;
            }

            /// <summary>与 Python Agent 的待发条件一致，保留已停用记录和缺少回执的入口遗漏。</summary>
            private static bool IsSendableReport(string kind, JObject data)
            {
                string stage = ReportText(data, "stage");
                if (kind == "task" || stage is "sdk-device" or "sdk-preload" or "startup-stage" or "framework-upgrade" or "pipify-step" ||
                    ReportText(data, "operation") == "editor.preload.sdk")
                    return false;

                if (stage == "player-build" &&
                    ((data["batch_mode"] == null && ReportText(data, "platform") is not ("Android" or "iOS")) ||
                     ReportText(data, "build_kind") is null or "scripts"))
                    return false;

                string code = ReportText(data, "code");
                if (code == "editor-sdk-preload-exceeds-60-seconds")
                    return false;
                if (ReportText(data, "source") == "codex" && code?.StartsWith("missing-nova-route.", StringComparison.Ordinal) == true)
                    return data["evidence"] is JArray evidence &&
                           evidence.OfType<JObject>().Any(item => ReportText(item, "source") == "codex.PostToolUse.exit-zero");
                return true;
            }

            /// <summary>为当前报告核对服务声明的能力，旧服务暂不可接收时保留原正文。</summary>
            private static bool RequiresServerUpgrade(JObject health, string kind, JObject data, bool compactReports)
            {
                string stage = ReportText(data, "stage");
                bool needsTimings = data["action_operation"] != null || stage == "table-ready" ||
                    (stage == "editor-module" && ReportText(data, "step") is "localization" or "ui" or "network") ||
                    (stage == "pipify-batch" && data["attempt"] != null);
                bool needsCompact = (kind == "issue" && data["priority"] == null) ||
                    (kind == "snapshot" && data["packages"] is JArray packages &&
                     packages.OfType<JObject>().Any(package => package["evidence"] == null));
                bool needsMcpMonitoring = stage == "unity-mcp-connection" || ReportText(data, "entry") == "unity-mcp";
                bool needsWorkflowMonitoring = kind == "workflow" || data["batch_mode"] != null ||
                    (stage == "editor-module" && ReportText(data, "step") is "persist" or "event" or "sound" or "vibrate");
                return (kind == "hook_activity" && (bool?)health["hook_activity"] != true) ||
                       (needsWorkflowMonitoring && (bool?)health["workflow_monitoring"] != true) ||
                       (kind == "implementation" && (bool?)health["implementation_tracking"] != true) ||
                       (stage == "editor-module" && (bool?)health["execution_evidence"] != true) ||
                       (data["calls"] != null && (bool?)health["play_call_counts"] != true) ||
                       (needsMcpMonitoring && (bool?)health["unity_mcp_monitoring"] != true) ||
                       (needsTimings && (bool?)health["feedback_timings"] != true) ||
                       (kind == "usage" && data["source"] != null && (bool?)health["codex_telemetry"] != true) ||
                       ((data["codex_hooks"] != null || data["application_identifiers"] != null) &&
                        (bool?)health["project_snapshot_details"] != true) ||
                       (data["git_user_name"] != null && (bool?)health["git_user_name_reporting"] != true) ||
                       (data["operating_system"] != null && (bool?)health["snapshot_operating_system"] != true) ||
                       (kind == "snapshot" && (data["editor_framework"] != null || data["codex_hook_counts"] != null) &&
                        (bool?)health["snapshot_version_and_hook_counts"] != true) ||
                       (needsCompact && !compactReports);
            }

            /// <summary>用 Editor 自带的 JSON 解析器读取正文，损坏的正文仍留在本地。</summary>
            private static JObject ReadQueuedReport(string payload)
            {
                try { return JObject.Parse(payload); }
                catch (JsonException) { return null; }
            }

            /// <summary>读取上报中的字符串字段，兼容旧记录里的缺失值。</summary>
            private static string ReportText(JObject report, string key) =>
                report?[key] is JValue { Type: JTokenType.String } value ? (string)value : null;

            /// <summary>只访问指定的内网地址，限制等待时间和返回内容体积。</summary>
            private static JObject Request(HttpMethod method, string path, string project = null, string token = null, JObject payload = null)
            {
                try
                {
                    using var request = new HttpRequestMessage(method, c_Server + path);
                    if (payload != null)
                    {
                        request.Headers.Add("X-Nova-Project", project);
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
                    }
                    using var response = s_Client.SendAsync(request).GetAwaiter().GetResult();
                    return response.IsSuccessStatusCode ? JObject.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult()) : null;
                }
                catch (HttpRequestException) { return null; }
                catch (OperationCanceledException) { return null; }
                catch (JsonException) { return null; }
            }

            /// <summary>用于读取下一批待发记录的固定列映射。</summary>
            private sealed class PendingReport
            {
                public PendingReport() { }
                public string Id { get; set; }
                public string Payload { get; set; }
                public int Attempts { get; set; }
            }

            /// <summary>从旧版队列读取仍带正文的已确认报告。</summary>
            private sealed class DeliveredReport
            {
                public DeliveredReport() { }
                public string Id { get; set; }
                public string Payload { get; set; }
                public long Accepted { get; set; }
                public long? IssueId { get; set; }
            }

            /// <summary>生成带实际操作标识和结果的 Editor 用量记录。</summary>
            private static JObject Usage(string module, string operation, string outcome, string framework) =>
                new() { ["module"] = module, ["operation"] = operation, ["outcome"] = outcome, ["framework"] = framework, ["source"] = "editor" };

            /// <summary>读取共享状态中的原始 SQLite 文本。</summary>
            private static string RawState(SQLiteConnection db, string key) => db.ExecuteScalar<string>("SELECT value FROM state WHERE key=?", key);

            /// <summary>读取共享状态中的 JSON 值，工作区原始编号由 RawState 读取。</summary>
            private static JToken ReadState(SQLiteConnection db, string key) => RawState(db, key) is string raw ? JToken.Parse(raw) : null;

            /// <summary>按 Python Agent 能读取的 JSON 格式写入本地状态。</summary>
            private static void SaveState(SQLiteConnection db, string key, JToken value) =>
                db.Execute("INSERT OR REPLACE INTO state VALUES (?,?)", key, value.ToString(Formatting.None));

            /// <summary>转换 Unity 包来源为上报协议中的固定枚举。</summary>
            private static string PackageSourceName(string source) => source.ToLowerInvariant() switch
            {
                "registry" => "registry", "local" or "localtarball" => "local", "embedded" => "embedded",
                "git" => "git", "builtin" => "builtin", _ => "unknown"
            };

            /// <summary>保留允许上报的版本格式。</summary>
            private static string ReadVersion(string value) => value != null && s_Version.IsMatch(value) ? value : "unknown";

            /// <summary>验证包、回执和模块标识。</summary>
            private static bool IsKey(string value) => value != null && s_Key.IsMatch(value);

            /// <summary>读取当前工程生效的 Git 用户名，供维护台辨认上报来源。</summary>
            private static string ReadGitUserName(string project)
            {
                var result = CodexGit(project, "config --get user.name");
                return result.Success ? SafeName(result.Stdout.Trim(), null) : null;
            }

            /// <summary>只保留可用于维护台显示的名称，缺失时返回调用方指定的默认值。</summary>
            private static string SafeName(string value, string fallback = "Nova 项目") =>
                !string.IsNullOrWhiteSpace(value) && value.Length <= 100 && !s_Private.IsMatch(value) &&
                !value.Any(char.IsControl) ? value.Trim() : fallback;

            /// <summary>从稳定的观察或回执标识生成与 Python Agent 一致的报告编号。</summary>
            private static string EventId(params string[] parts)
            {
                using var sha = SHA256.Create();
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(parts, Formatting.None)))).Substring(0, 32);
            }

            /// <summary>以小写十六进制表示随机凭据和稳定摘要。</summary>
            private static string Hex(byte[] value) => BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();

            /// <summary>使用 UTC Unix 秒，供共享队列排序和重试。</summary>
            private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            /// <summary>在 Windows 和 macOS 上完整传递包含空格的工程目录。</summary>
            private static string Quote(string value)
            {
                return "\"" + System.Text.RegularExpressions.Regex.Replace(value, "(\\\\*)(\"|$)",
                    match => match.Groups[1].Value + match.Groups[1].Value +
                        (match.Groups[2].Value == "\"" ? "\\\"" : "")) + "\"";
            }
        }
    }
}
