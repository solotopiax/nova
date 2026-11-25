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

            /// <summary>由实际工具入口记录固定结果，反馈故障不改变原操作的终态。</summary>
            internal static void ObserveImplementation(string stage, string outcome, string platform = null,
                string channel = null, string mode = null, string step = null, TimeSpan? elapsed = null,
                string buildKind = null, bool development = false, string attempt = null, int sequence = 0,
                string reportId = null)
            {
                if (Application.isBatchMode)
                    return;
                try
                {
                    if (!BindFrameworkPackage())
                        return;
                    var data = ImplementationData(stage, platform, channel, mode);
                    data["outcome"] = outcome;
                    if (IsKey(step)) data["step"] = step;
                    if (attempt != null) data["attempt"] = attempt;
                    if (sequence > 0) data["sequence"] = sequence;
                    if (elapsed.HasValue)
                        data["milliseconds"] = (long)Math.Max(0, Math.Min(86400000, elapsed.Value.TotalMilliseconds));
                    if (buildKind != null)
                    {
                        data["build_kind"] = buildKind;
                        data["development"] = development;
                    }
                    var input = new JObject { ["event"] = "implementation", ["implementation"] = data };
                    if (reportId != null) input["id"] = reportId;
                    Queue(input);
                }
                catch (Exception)
                {
                    // 反馈是被动观察，异常不能阻断原有导出、查询及构建。
                }
            }

            /// <summary>只读取 Unity 真实构建终态，脚本构建和平台工程导出保留各自证据。</summary>
            internal static void ObservePlayerBuild(BuildSummary summary, bool exportsProject)
            {
                string outcome = summary.result switch
                {
                    BuildResult.Succeeded => "success",
                    BuildResult.Cancelled => "cancelled",
                    BuildResult.Failed => "failed",
                    _ => "blocked"
                };
                string kind = (summary.options & BuildOptions.BuildScriptsOnly) != 0 ? "scripts" :
                    exportsProject ? "project" : "player";
                ObserveImplementation("player-build", outcome, summary.platform.ToString(), elapsed: summary.totalTime,
                    buildKind: kind, development: (summary.options & BuildOptions.Development) != 0);
            }

            /// <summary>在实际预加载中读取已加载的运行时坐标。</summary>
            private static void ObserveRuntimeConfig()
            {
                try
                {
                    var config = global::NovaFramework.Runtime.Nova.Config;
                    if (config != null && config.Platform != PlatformType.None && config.Channel != ChannelType.None)
                        ObserveImplementation("config-load", "success", config.Platform.ToString(), config.Channel.ToString());
                }
                catch (Exception)
                {
                    // 自定义 ConfigManager 的读取失败交由其原有运行时链路处理。
                }
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
