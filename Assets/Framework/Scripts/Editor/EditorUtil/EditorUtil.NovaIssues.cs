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
using System.Globalization;
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
using UnityEditor.Compilation;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PackageEvents = UnityEditor.PackageManager.Events;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>采集消费工程的 Editor 证据，通过本地队列向固定服务器发送。</summary>
        [InitializeOnLoad]
        internal static partial class NovaIssues
        {
            private const string c_Framework = "com.solotopia.nova.framework";
            private const string c_Mcp = c_Framework + ".mcp";
            private const string c_Server = "http://172.16.22.175:8787";
            private const string c_WorkDirectory = "Library/Nova/Issues";
            private const string c_CompilationDayKey = "Nova.Issues.CompilationDay";
            private const string c_EnvironmentReportKey = "editor_environment_report";
            private static readonly Regex s_Key = new(@"\A[a-zA-Z0-9][a-zA-Z0-9_.-]{0,95}\z");
            private static readonly Regex s_Version = new(@"\A[a-zA-Z0-9][a-zA-Z0-9.+-]{0,79}\z");
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
            private static string s_SdkAttempt;
            private static double s_NextSnapshot;
            private static int s_Sending;

            /// <summary>只在普通 Editor 会话中订阅观察事件，后台队列不阻塞 Unity 的加载。</summary>
            static NovaIssues()
            {
                if (Application.isBatchMode) return;

                EditorApplication.update += Update;
                PackageEvents.registeredPackages += _ => s_NextSnapshot = 0;
                CompilationPipeline.compilationFinished += OnCompilationFinished;
                Application.logMessageReceived += ObservePreload;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                AssemblyReloadEvents.beforeAssemblyReload += EndPreloadObservation;
                EditorApplication.quitting += EndPreloadObservation;
            }

            /// <summary>保存本次编译日期，重载脚本后仍可用已就绪的 Editor 生成环境快照。</summary>
            private static void OnCompilationFinished(object _)
            {
                SessionState.SetString(c_CompilationDayKey, LocalDay(Now()));
                s_NextSnapshot = 0;
            }

            /// <summary>定期读取当前注册包、配置坐标和已完成的工具链核验。</summary>
            private static void Update()
            {
                double stamp = EditorApplication.timeSinceStartup;
                if (stamp < s_NextSnapshot || EditorApplication.isCompiling || EditorApplication.isUpdating)
                    return;

                s_NextSnapshot = stamp + 30;
                var framework = PackageInfo.FindForAssembly(typeof(EditorUtil).Assembly);
                if (framework?.name != c_Framework || string.IsNullOrEmpty(framework.resolvedPath))
                    return;

                s_FrameworkRoot = Path.GetFullPath(framework.resolvedPath);
                s_FrameworkVersion = ReadVersion(framework.version);
                JObject snapshot = CaptureSnapshot(PackageInfo.GetAllRegisteredPackages());
                snapshot["compilation_day"] = SessionState.GetString(c_CompilationDayKey, string.Empty);
                Queue(snapshot);
            }

            /// <summary>将允许上报的字段复制到普通 JSON，避免采集资产内容和诊断原文。</summary>
            private static JObject CaptureSnapshot(PackageInfo[] packages)
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
                var pipeline = packages?.FirstOrDefault(package => package?.name == "com.unity.pipeline");
                if (pipeline != null)
                    tools["pipeline"] = new JObject { ["version"] = ReadVersion(pipeline.version), ["state"] = "registered" };
                var mcp = packages?.FirstOrDefault(package => package?.name == c_Mcp);
                tools["mcp"] = new JObject { ["version"] = ReadVersion(mcp?.version), ["state"] = mcp == null ? "not_observed" : "installed" };

                var result = new JObject
                {
                    ["event"] = "snapshot",
                    ["unity"] = ReadVersion(Application.unityVersion),
                    ["packages"] = new JArray(registered),
                    ["tools"] = tools,
                    ["enabled"] = new JArray(),
                    ["platform"] = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    ["sdk_attempt"] = s_SdkAttempt ?? string.Empty
                };
                if (Config.WorkspaceActive.TryGetPersistedConfigMaster(out var master, out _, out _, out _))
                {
                    result["channel"] = master.CurrentChannel.ToString();
                    result["mode"] = master.CurrentDevelopMode.ToString();
                    result["coordinate_source"] = "ConfigMaster";
                    result["enabled"] = new JArray(
                        (master.EnabledSDKs ?? new System.Collections.Generic.List<string>())
                        .Concat(master.EnabledKits ?? new System.Collections.Generic.List<string>()).Where(IsKey).Take(100));
                }
                return result;
            }

            /// <summary>从已有预加载日志提取阶段事件，不保留完整日志。</summary>
            private static void ObservePreload(string message, string stackTrace, LogType type)
            {
                if (!EditorApplication.isPlaying || s_FrameworkRoot == null || message == null)
                    return;

                if (message.Contains("[Procedure] Preload Step 4/8 — 初始化 SDK。"))
                {
                    EndPreloadObservation();
                    s_SdkAttempt = Guid.NewGuid().ToString("N");
                    Observe("sdk-start", s_SdkAttempt);
                }
                else if (s_SdkAttempt != null && message.Contains("[Procedure] Preload Step 5/8 — 加载数据表。"))
                {
                    Observe("sdk-finished", s_SdkAttempt);
                    s_SdkAttempt = null;
                }
            }

            /// <summary>离开 Play 时结束该次 SDK 等待观察。</summary>
            private static void OnPlayModeChanged(PlayModeStateChange change)
            {
                if (change == PlayModeStateChange.ExitingPlayMode)
                    EndPreloadObservation();
            }

            /// <summary>记录已结束的预加载观察，避免跨 Play 会话判断。</summary>
            private static void EndPreloadObservation()
            {
                if (s_SdkAttempt == null)
                    return;
                Observe("sdk-ended", s_SdkAttempt);
                s_SdkAttempt = null;
            }

            /// <summary>把编译和预加载阶段事件交给本地队列。</summary>
            private static void Observe(string eventName, string id)
            {
                if (s_FrameworkRoot != null)
                    Queue(new JObject { ["event"] = eventName, ["id"] = id });
            }

            /// <summary>将有限的工作交给后台发送线程，确保 Unity 主线程可以继续。</summary>
            private static void Queue(JObject input)
            {
                input["id"] ??= Guid.NewGuid().ToString("N");
                input["framework"] = s_FrameworkVersion;
                input["framework_root"] = s_FrameworkRoot;
                input["product"] = PlayerSettings.productGUID.ToString();
                input["name"] = SafeName(Application.productName);
                input["observed_at"] = Now();
                s_Pending.Enqueue(input);
                StartDrain();
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
                        if (s_Pending.TryPeek(out JObject first))
                            PrepareCodexHooksIfNeeded(s_Project, first, storage);
                        if (storage.Path == null)
                        {
                            while (s_Pending.TryDequeue(out _)) { }
                            return;
                        }

                        using var db = new SQLiteConnection(storage.Path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
                        db.EnableWriteAheadLogging();
                        db.Execute("CREATE TABLE IF NOT EXISTS state (key TEXT PRIMARY KEY, value TEXT NOT NULL)");
                        db.Execute(@"CREATE TABLE IF NOT EXISTS outbox (id TEXT PRIMARY KEY, payload TEXT NOT NULL,
                            attempts INTEGER NOT NULL DEFAULT 0, ready_at INTEGER NOT NULL, accepted INTEGER, issue_id INTEGER, error TEXT)");
                        db.Execute("CREATE INDEX IF NOT EXISTS ready_reports ON outbox(accepted, ready_at)");
                        while (s_Pending.TryPeek(out JObject input))
                        {
                            db.RunInTransaction(() => Record(db, input));
                            s_Pending.TryDequeue(out _);
                        }
                        drained = true;
                        SendPending(db);
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
                string framework = (string)input["framework"];
                string id = (string)input["id"];
                switch ((string)input["event"])
                {
                    case "snapshot":
                        RecordSnapshot(db, input, workspace, (string)project["id"], stamp);
                        break;
                    case "sdk-start":
                        SaveState(db, "sdk", new JObject { ["id"] = id, ["started"] = stamp, ["framework"] = framework });
                        break;
                    case "sdk-finished":
                        AppendReport(db, workspace, EventId("sdk-finished", id), stamp, "usage",
                            Usage("sdk", "editor.preload.sdk", "success", framework));
                        goto case "sdk-ended";
                    case "sdk-ended":
                        if ((string)ReadState(db, "sdk")?["id"] == id) SaveState(db, "sdk", new JObject());
                        break;
                }
            }

            /// <summary>保存实际注册包快照，随后核对同一 Play 会话中持续等待的 SDK 步骤。</summary>
            private static void RecordSnapshot(SQLiteConnection db, JObject input, string workspace, string projectId, long stamp)
            {
                var data = new JObject();
                foreach (string field in new[] { "framework", "unity", "packages", "tools", "enabled", "platform", "channel", "mode", "coordinate_source" })
                {
                    if (input[field] != null) data[field] = input[field].DeepClone();
                }
                var source = new JObject { ["at"] = stamp, ["sdk_attempt"] = input["sdk_attempt"].DeepClone() };
                foreach (var field in data.Properties()) source[field.Name] = field.Value.DeepClone();
                SaveState(db, "editor", source);
                SaveState(db, "editor_source", input["framework_root"]);
                long codexSeen = ReadState(db, "codex_seen")?.Value<long>() ?? 0;
                data["coverage"] = new JObject { ["agent"] = "observed", ["editor"] = "observed", ["codex"] = stamp - codexSeen < 1800 ? "observed" : "waiting" };

                // 按基础信息和本地自然日去重，工具状态变化仍保留在本地观察记录中。
                JObject previous = ReadState(db, c_EnvironmentReportKey) as JObject;
                string signature = EventId(projectId, (string)data["framework"], (string)data["unity"]);
                string day = LocalDay(stamp);
                bool compiledToday = (string)input["compilation_day"] == day;
                if ((string)previous?["hash"] != signature || compiledToday && (string)previous?["day"] != day)
                {
                    AppendReport(db, workspace, (string)input["id"], stamp, "snapshot", data);
                    SaveState(db, c_EnvironmentReportKey, new JObject { ["hash"] = signature, ["day"] = day });
                }
                RecordActionReceipts(db, workspace, (string)input["framework"], stamp);
                JObject sdk = ReadState(db, "sdk") as JObject;
                string id = (string)sdk?["id"];
                string framework = (string)sdk?["framework"];
                if (id == null || id != (string)input["sdk_attempt"] || framework == "unknown" ||
                    (bool?)sdk["reported"] == true || stamp - sdk["started"].Value<long>() < 60) return;

                AppendReport(db, workspace, EventId("sdk-preload", id), stamp, "issue", new JObject
                {
                    ["code"] = "editor-sdk-preload-exceeds-60-seconds", ["area"] = "sdk", ["priority"] = "P1",
                    ["title"] = "Editor 内 SDK 预加载等待超过 60 秒",
                    ["summary"] = "本地 Agent 观察到 Nova 预加载进入 SDK 步骤后，同一 Play 会话中的 Editor 仍响应，但步骤持续未完成。",
                    ["expected"] = "SDK 初始化应得到终态，之后继续执行后续预加载。",
                    ["actual"] = "同一 SDK 步骤在两个相隔至少 60 秒的 Editor 观察点仍处于等待。",
                    ["framework"] = framework, ["source"] = "editor",
                    ["evidence"] = new JArray(
                        new JObject { ["source"] = "editor.preload", ["detail"] = "已观察到 Nova 预加载开始 SDK 步骤。" },
                        new JObject { ["source"] = "editor.snapshot", ["detail"] = "60 秒后仍观察到同一个 SDK 步骤。" })
                });
                sdk["reported"] = true;
                SaveState(db, "sdk", sdk);
            }

            /// <summary>仅从已有 Nova Project Action 回执提炼用量，同一回执与 Codex 共用上报编号。</summary>
            private static void RecordActionReceipts(SQLiteConnection db, string workspace, string framework, long now)
            {
                string directory = Path.Combine(s_Project, "Library/Nova/AgentActions/Operations");
                if (!Directory.Exists(directory)) return;
                long earliest = Math.Max(now - 600, ReadState(db, "enrolled_at")?.Value<long>() ?? now);
                foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
                {
                    try
                    {
                        var info = new FileInfo(path);
                        long stamp = new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds();
                        if (stamp < earliest || info.Length > 256 * 1024 || IsLink(path)) continue;
                        JObject item = JObject.Parse(File.ReadAllText(path));
                        string action = (string)item["ActionId"];
                        string operation = (string)item["OperationId"];
                        string outcome = (string)item["OutcomeStatus"];
                        if ((string)item["Status"] is not ("executed" or "verified") || !IsKey(operation) || !IsKey(action) ||
                            !action.StartsWith("nova.project.", StringComparison.Ordinal) ||
                            outcome is not ("success" or "partial" or "blocked" or "failed" or "cancelled")) continue;
                        if (DateTimeOffset.TryParse((string)item["UpdatedAtUtc"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var updated))
                            stamp = updated.ToUnixTimeSeconds();
                        AppendReport(db, workspace, EventId("action", action, operation), stamp, "usage",
                            Usage(action.Split('.')[2], action, outcome, framework));
                    }
                    catch (IOException) { }
                    catch (JsonException) { }
                }
            }

            /// <summary>写入和 Python 本地 Agent 共用的版本化上报信封。</summary>
            private static void AppendReport(SQLiteConnection db, string workspace, string id, long stamp, string kind, JObject data)
            {
                var report = new JObject
                {
                    ["version"] = 1, ["id"] = id, ["workspace"] = workspace, ["observed_at"] = stamp,
                    ["kind"] = kind, ["data"] = data
                };
                db.Execute("INSERT OR IGNORE INTO outbox (id,payload,ready_at) VALUES (?,?,?)", id, report.ToString(Formatting.None), stamp);
            }

            /// <summary>把完整的本地报告发往固定目标，服务端确认后保留回执。</summary>
            private static void SendPending(SQLiteConnection db)
            {
                // 与 Python Agent 的待发查询一致，旧版缺少回执的自动入口遗漏保留在本机。
                var reports = db.Query<PendingReport>(@"SELECT id AS Id,payload AS Payload,attempts AS Attempts FROM outbox
                    WHERE accepted IS NULL AND ready_at<=? AND json_extract(payload,'$.kind')!='task'
                    AND (COALESCE(json_extract(payload,'$.data.source'),'')!='codex'
                         OR COALESCE(json_extract(payload,'$.data.code'),'') NOT GLOB 'missing-nova-route.*'
                         OR EXISTS (SELECT 1 FROM json_each(outbox.payload,'$.data.evidence') AS evidence
                                    WHERE json_extract(evidence.value,'$.source')='codex.PostToolUse.exit-zero'))
                    ORDER BY json_extract(payload,'$.kind')!='issue',ready_at,rowid LIMIT 8", Now());
                if (reports.Count == 0) return;
                var project = (JObject)ReadState(db, "automatic_project");
                string token = (string)ReadState(db, "automatic_token");
                JObject health = Request(HttpMethod.Get, "/health");
                bool ready = (string)health?["service"] == "nova-issues-server" && (int?)health["protocol"] == 1 && (bool?)health["automatic_reporting"] == true;
                foreach (var report in reports)
                {
                    JObject receipt = ready ? Request(HttpMethod.Post, "/api/v1/auto-reports", (string)project["id"], token,
                        new JObject { ["name"] = project["name"].DeepClone(), ["report"] = JObject.Parse(report.Payload) }) : null;
                    long stamp = Now();
                    if ((bool?)receipt?["accepted"] == true && (string)receipt["id"] == report.Id)
                        db.Execute("UPDATE outbox SET accepted=?,issue_id=?,error=NULL WHERE id=?", stamp, (long?)receipt["issue_id"], report.Id);
                    else
                        db.Execute("UPDATE outbox SET attempts=attempts+1,error=?,ready_at=? WHERE id=? AND accepted IS NULL",
                            ready ? "transport" : "service-unavailable", stamp + Math.Min(300, 1L << Math.Min(report.Attempts + 1, 9)), report.Id);
                }
                db.Execute("DELETE FROM outbox WHERE accepted IS NOT NULL AND accepted<?", Now() - 30 * 86400);
            }

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

            /// <summary>按同一格式生成 Nova 用量记录。</summary>
            private static JObject Usage(string module, string operation, string outcome, string framework) =>
                new() { ["module"] = module, ["operation"] = operation, ["outcome"] = outcome, ["framework"] = framework };

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

            /// <summary>只保留可用于维护台显示的项目名。</summary>
            private static string SafeName(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100 &&
                !s_Private.IsMatch(value) && !value.Any(char.IsControl) ? value.Trim() : "Nova 项目";

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

            /// <summary>使用工作站所在时区的自然日，供每日编译环境上报去重。</summary>
            private static string LocalDay(long stamp) => DateTimeOffset.FromUnixTimeSeconds(stamp).ToLocalTime()
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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
