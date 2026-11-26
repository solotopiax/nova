/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename: EditorUtil.NovaIssues.Implementation.cs
 * descrip:  记录消费工程已保存的实施状态及实际执行结果。
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        internal static partial class NovaIssues
        {
            private const string c_WorkspaceImplementation = "implementation_workspace";
            private const string c_UnityMcpPackage = "com.coplaydev.unity-mcp";
            private const double c_UnityMcpHeartbeatSeconds = 300;
            private static readonly DateTime s_EditorStartedUtc = DateTime.UtcNow.AddSeconds(-EditorApplication.timeSinceStartup);
            private static DateTime s_LastBuildReportWrite;
            private static double s_NextBuildReportCheck;
            private static string s_LastUnityMcpState;
            private static string s_LastUnityMcpVersion;
            private static double s_NextUnityMcpHeartbeat;

            /// <summary>状态变化时上报 Coplay 桥接，状态稳定时定期补一份最近观察。</summary>
            private static void ObserveUnityMcpConnection(PackageInfo[] packages, double editorTime)
            {
                try
                {
                    var package = packages?.FirstOrDefault(item => item?.name == c_UnityMcpPackage
                        && (item.errors == null || item.errors.Length == 0));
                    if (package == null)
                    {
                        s_LastUnityMcpState = null;
                        return;
                    }
                    var state = NovaFramework.Mcp.Editor.NovaToolchainBinding.ReadEditorTransport(c_UnityMcpPackage);
                    string status = state.ToString().ToLowerInvariant();
                    string version = ReadVersion(package.version);
                    if (status == s_LastUnityMcpState && version == s_LastUnityMcpVersion && editorTime < s_NextUnityMcpHeartbeat)
                        return;
                    var data = ImplementationData("unity-mcp-connection", null, null, null);
                    data["state"] = status;
                    data["provider_version"] = version;
                    Queue(new JObject { ["event"] = "implementation", ["implementation"] = data });
                    s_LastUnityMcpState = status;
                    s_LastUnityMcpVersion = version;
                    s_NextUnityMcpHeartbeat = editorTime + c_UnityMcpHeartbeatSeconds;
                }
                catch (Exception) { /* 状态观察不影响 Editor 或 MCP 的连接。 */ }
            }

            /// <summary>采集三工具的持久绑定、实际载入的 Framework 版本及 SDK 清单。</summary>
            private static JObject CaptureImplementationWorkspace(PackageInfo[] packages, ConfigMasterSO master)
            {
                var data = ImplementationData("workspace", EditorUserBuildSettings.activeBuildTarget.ToString(),
                    master?.CurrentChannel.ToString(), master?.CurrentDevelopMode.ToString());
                bool hasBatch = Config.WorkspaceActive.TryGetPersistedPipifySettings(out var settings, out _, out _, out _) &&
                                settings.Batches?.Any(batch => batch?.Items?.Count > 0) == true;
                data["tools"] = new JObject
                {
                    ["config"] = master != null ? "configured" : "pending",
                    ["pipify"] = hasBatch ? "configured" : "pending",
                    ["plugpals"] = PlugPals.HasPersistedRegistry() ? "configured" : "pending"
                };
                data["editor_framework"] = ReadVersion(global::NovaFramework.Runtime.Nova.Version);
                data["sdk_packages"] = new JArray((packages ?? Array.Empty<PackageInfo>())
                    .Where(item => item != null && IsKey(item.name) &&
                                   item.name.StartsWith(c_Framework + ".sdk.", StringComparison.Ordinal) &&
                                   (item.errors == null || item.errors.Length == 0))
                    .OrderBy(item => item.name, StringComparer.Ordinal).Take(100)
                    .Select(item => new JObject { ["id"] = item.name, ["version"] = ReadVersion(item.version) }));
                data["sdk_configs"] = new JArray((master?.EnabledSDKs ?? new System.Collections.Generic.List<string>())
                    .Where(IsKey).Distinct().OrderBy(id => id, StringComparer.Ordinal).Take(100));
                return data;
            }

            /// <summary>由实际工具入口记录固定结果，返回记录是否已交给本地队列。</summary>
            internal static bool ObserveImplementation(string stage, string outcome, string platform = null,
                string channel = null, string mode = null, string step = null, string buildKind = null,
                bool development = false, string reportId = null, long? observedAt = null, long? calls = null)
            {
                if (Application.isBatchMode)
                    return false;
                try
                {
                    if (!BindFrameworkPackage())
                        return false;
                    var data = ImplementationData(stage, platform, channel, mode);
                    data["outcome"] = outcome;
                    if (IsKey(step)) data["step"] = step;
                    if (calls.HasValue) data["calls"] = calls.Value;
                    if (buildKind != null)
                    {
                        data["build_kind"] = buildKind;
                        data["development"] = development;
                    }
                    var input = new JObject { ["event"] = "implementation", ["implementation"] = data };
                    if (reportId != null) input["id"] = reportId;
                    if (observedAt.HasValue) input["observed_at"] = observedAt.Value;
                    Queue(input);
                    return true;
                }
                catch (Exception)
                {
                    // 反馈是被动观察，异常不能阻断原有导出、查询及构建。
                    return false;
                }
            }

            private static bool IsMobileBuild(BuildTarget platform, BuildOptions options) =>
                (platform is BuildTarget.Android or BuildTarget.iOS) && (options & BuildOptions.BuildScriptsOnly) == 0;

            /// <summary>记录供 SDK 构建汇总使用的 Android 或 iOS 构建终态。</summary>
            internal static void ObservePlayerBuild(BuildSummary summary, bool exportsProject)
            {
                if (!IsMobileBuild(summary.platform, summary.options))
                    return;
                string outcome = summary.result switch
                {
                    BuildResult.Succeeded => "success",
                    BuildResult.Cancelled => "cancelled",
                    BuildResult.Failed => "failed",
                    _ => "blocked"
                };
                string id = EventId("player-build", summary.guid.ToString(),
                    summary.buildStartedAt.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), summary.platform.ToString());
                ObserveImplementation("player-build", outcome, summary.platform.ToString(),
                    buildKind: exportsProject ? "project" : "player",
                    development: (summary.options & BuildOptions.Development) != 0, reportId: id,
                    observedAt: summary.buildEndedAt > DateTime.UnixEpoch ? new DateTimeOffset(summary.buildEndedAt).ToUnixTimeSeconds() : null);
            }

            /// <summary>Nova 构建入口在 Unity 没有返回报告时记录可确认的拒绝或失败。</summary>
            internal static void ObservePlayerBuildFailure(BuildTarget platform, bool exportsProject, BuildOptions options,
                string outcome)
            {
                if (!IsMobileBuild(platform, options))
                    return;
                ObserveImplementation("player-build", outcome, platform.ToString(),
                    buildKind: exportsProject ? "project" : "player", development: (options & BuildOptions.Development) != 0);
            }

            /// <summary>构建目标使用当前绑定工作区的平台时，保留实际渠道和开发模式。</summary>
            internal static (string Channel, string Mode) CaptureBundleCoordinates(BuildTarget target)
            {
                try
                {
                    if (!Application.isBatchMode && target == EditorUserBuildSettings.activeBuildTarget &&
                        Config.WorkspaceActive.TryGetPersistedConfigMaster(out var master, out _, out _, out _))
                        return (master.CurrentChannel.ToString(), master.CurrentDevelopMode.ToString());
                }
                catch (Exception) { /* 未取得配置时保留独立的构建平台和结果。 */ }
                return default;
            }

            /// <summary>读取 Unity 保存的已完成报告，覆盖 Build Profiles 和直调 BuildPipeline。</summary>
            private static void PollPlayerBuild(double time)
            {
                if (time < s_NextBuildReportCheck || s_FrameworkRoot == null || BuildPipeline.isBuildingPlayer)
                    return;
                s_NextBuildReportCheck = time + 2;
                try
                {
                    DateTime written = System.IO.File.GetLastWriteTimeUtc(System.IO.Path.Combine(s_Project, "Library/LastBuild.buildreport"));
                    if (written == s_LastBuildReportWrite || written < s_EditorStartedUtc)
                        return;
                    s_LastBuildReportWrite = written;
                    TryObserveLatestPlayerBuild(s_EditorStartedUtc);
                }
                catch (Exception) { /* 读取异常时下次轮询重试。 */ }
            }

            /// <summary>只接收本时间窗内的终态 Player 报告，原生构建和 Nova 入口共享去重编号。</summary>
            internal static bool TryObserveLatestPlayerBuild(DateTime notBefore)
            {
                try
                {
                    BuildReport report = BuildReport.GetLatestReport();
                    if (report == null)
                        return false;
                    BuildSummary summary = report.summary;
                    if (summary.buildType != BuildType.Player || !IsMobileBuild(summary.platform, summary.options) ||
                        summary.buildStartedAt < notBefore ||
                        summary.result is not (BuildResult.Succeeded or BuildResult.Cancelled or BuildResult.Failed))
                        return false;
                    string extension = System.IO.Path.GetExtension(summary.outputPath)?.ToLowerInvariant();
                    bool project = summary.platform == BuildTarget.iOS ||
                        summary.platform == BuildTarget.Android &&
                        ((summary.options & BuildOptions.AcceptExternalModificationsToPlayer) != 0 ||
                         (!string.IsNullOrEmpty(summary.outputPath) && extension is not (".apk" or ".aab")));
                    ObservePlayerBuild(summary, project);
                    return true;
                }
                catch (Exception) { return false; }
            }

            /// <summary>记录 Config 加载实际返回的结果，坐标只来自成功载入的配置。</summary>
            private static void ObserveRuntimeConfig(string outcome, PlatformType platform, ChannelType channel, DevelopMode? mode)
            {
                ObserveImplementation("config-load", outcome, platform == PlatformType.None ? null : platform.ToString(),
                    channel == ChannelType.None ? null : channel.ToString(), mode?.ToString());
            }

            private static JObject ImplementationData(string stage, string platform, string channel, string mode)
            {
                var frame = new JObject
                {
                    ["stage"] = stage, ["framework"] = s_FrameworkVersion,
                    ["unity"] = ReadVersion(Application.unityVersion)
                };
                if (IsKey(platform)) frame["platform"] = platform;
                if (IsKey(channel)) frame["channel"] = channel;
                if (IsKey(mode)) frame["mode"] = mode;
                return frame;
            }

            /// <summary>基础实施只在状态变化时入队，旧维护台仍可读取其展示的请求版本。</summary>
            private static void RecordImplementation(SqlCipher4Unity3D.SQLiteConnection db, JObject data, string workspace,
                string id, long stamp)
            {
                if (data == null)
                    return;
                if ((string)data["stage"] == "workspace")
                {
                    if (SupportsCompactReports(db))
                    {
                        data.Remove("requested_framework");
                    }
                    else
                    {
                        string manifest = System.IO.Path.Combine(s_Project, "Packages", "manifest.json");
                        var dependencies = File.Exists(manifest) ? PlugPals.ReadManifest(manifest)?.dependencies : null;
                        if (dependencies != null && dependencies.TryGetValue(c_Framework, out string requested) &&
                            Version.TryParse(requested, out Version requestedVersion) && requestedVersion.Build >= 0)
                            data["requested_framework"] = requested;
                    }
                    string signature = EventId(data.ToString(Formatting.None));
                    var previous = ReadState(db, c_WorkspaceImplementation);
                    if ((string)previous == signature)
                        return;
                    SaveState(db, c_WorkspaceImplementation, new JValue(signature));
                }
                AppendReport(db, workspace, EventId("implementation", workspace, id), stamp, "implementation", data);
            }
        }
    }
}
