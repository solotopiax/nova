/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename: EditorUtil.NovaIssues.Upgrade.cs
 * descrip:  跟踪 Nova 升级提交、UPM 解析、编译和新程序集加载。
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        internal static partial class NovaIssues
        {
            private const string c_UpgradeSessionKey = "Nova.Issues.FrameworkUpgrade";
            private static FrameworkUpgrade s_Upgrade;
            private static bool s_UpgradeLoaded;
            private static double s_NextUpgradeCheck;

            private sealed class FrameworkUpgrade
            {
                public string Id;
                public string Previous;
                public string Requested;
                public double Started;
                public int Sequence;
                public double? Resolved;
                public double? CompileStarted;
                public bool CompileError;
                public bool HadCompileError;
                public bool Finished;
                public string ResolveOutcome;
                public bool LoadWait;
                public JArray Reports = new();
            }

            /// <summary>在 Nova 已保存明确的 Framework 版本请求后开始观察。</summary>
            internal static void BeginFrameworkUpgrade(string package, string target)
            {
                if (package != c_Framework || Application.isBatchMode || ReadVersion(target) == "unknown")
                    return;
                try
                {
                    if (!BindFrameworkPackage())
                        return;
                    LoadUpgradeSession();
                    string previous = ReadVersion(global::NovaFramework.Runtime.Nova.Version);
                    if (previous == target || previous == "unknown")
                        return;
                    if (s_Upgrade is { Finished: false })
                    {
                        if (s_Upgrade.Requested == target)
                            return;
                        EndSupersededUpgrade(EditorApplication.timeSinceStartup);
                    }
                    s_Upgrade = new FrameworkUpgrade
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Previous = previous,
                        Requested = target,
                        Started = EditorApplication.timeSinceStartup
                    };
                    s_NextUpgradeCheck = 0;
                    RecordUpgradeStage("request", "running", 0);
                }
                catch (Exception) { /* 上报不影响已经提交的包请求。 */ }
            }

            private static void LoadUpgradeSession()
            {
                if (s_UpgradeLoaded || !BindFrameworkPackage())
                    return;
                s_UpgradeLoaded = true;
                string cached = SessionState.GetString(c_UpgradeSessionKey, string.Empty);
                if (string.IsNullOrEmpty(cached))
                    return;
                s_Upgrade = JsonConvert.DeserializeObject<FrameworkUpgrade>(cached);
                if (s_Upgrade == null)
                    return;
                foreach (var report in s_Upgrade.Reports.OfType<JObject>())
                    Queue((JObject)report.DeepClone());
            }

            /// <summary>注册事件在旧程序集仍存活时也能确认目标包的解析结果。</summary>
            private static void ObserveUpgradeRegistration(PackageRegistrationEventArgs args)
            {
                if (s_Upgrade is not { Finished: false })
                    return;
                try
                {
                    var package = args.added.Concat(args.changedTo).FirstOrDefault(item => item.name == c_Framework);
                    if (package != null)
                        ObserveUpgradeResolution(package, EditorApplication.timeSinceStartup);
                }
                catch (Exception) { /* UPM 注册沿用原有行为。 */ }
            }

            private static void PollFrameworkUpgrade(double time)
            {
                if (time < s_NextUpgradeCheck || EditorApplication.isCompiling || EditorApplication.isUpdating)
                    return;
                s_NextUpgradeCheck = time + (s_Upgrade is { Finished: false } ? 2 : 30);
                try
                {
                    LoadUpgradeSession();
                    if (s_Upgrade is not { Finished: false })
                        return;
                    var manifest = JObject.Parse(File.ReadAllText(Path.Combine(s_Project, "Packages", "manifest.json")));
                    if ((string)manifest["dependencies"]?[c_Framework] != s_Upgrade.Requested)
                    {
                        EndSupersededUpgrade(time);
                        return;
                    }

                    ObserveUpgradeResolution(PackageInfo.FindForAssetPath("Packages/" + c_Framework + "/package.json"), time);
                    if (s_Upgrade.Resolved is not double resolved)
                    {
                        if (s_Upgrade.ResolveOutcome == null && time - s_Upgrade.Started >= 60)
                        {
                            s_Upgrade.ResolveOutcome = "waiting";
                            RecordUpgradeStage("resolve", "waiting", time - s_Upgrade.Started);
                        }
                        return;
                    }
                    if (global::NovaFramework.Runtime.Nova.Version == s_Upgrade.Requested && BindFrameworkPackage() &&
                        s_FrameworkVersion == s_Upgrade.Requested && !EditorUtility.scriptCompilationFailed)
                    {
                        s_Upgrade.Finished = true;
                        RecordUpgradeStage("load", s_Upgrade.HadCompileError ? "recovered" : "success", time - resolved);
                    }
                    else if (!s_Upgrade.LoadWait && time - resolved >= 60)
                    {
                        s_Upgrade.LoadWait = true;
                        RecordUpgradeStage("load", "waiting", time - resolved);
                    }
                }
                catch (Exception) { /* 解析期间可能暂时无法读取包信息。 */ }
            }

            private static void ObserveUpgradeResolution(PackageInfo package, double time)
            {
                if (s_Upgrade.Resolved.HasValue || package?.name != c_Framework || package.version != s_Upgrade.Requested)
                    return;
                if (package.errors?.Length > 0)
                {
                    if (s_Upgrade.ResolveOutcome != "failed")
                    {
                        s_Upgrade.ResolveOutcome = "failed";
                        RecordUpgradeStage("resolve", "failed", time - s_Upgrade.Started, "upm-package-error");
                    }
                    return;
                }
                if (string.IsNullOrEmpty(package.resolvedPath))
                    return;
                s_Upgrade.Resolved = time;
                s_Upgrade.ResolveOutcome = "success";
                RecordUpgradeStage("resolve", "success", time - s_Upgrade.Started);
            }

            private static void ObserveUpgradeCompileStarted(object _)
            {
                if (s_Upgrade is not { Finished: false })
                    return;
                try
                {
                    double time = EditorApplication.timeSinceStartup;
                    ObserveUpgradeResolution(PackageInfo.FindForAssetPath("Packages/" + c_Framework + "/package.json"), time);
                    if (!s_Upgrade.Resolved.HasValue)
                        return;
                    s_Upgrade.CompileStarted = time;
                    s_Upgrade.CompileError = false;
                    RecordUpgradeStage("compile", "running", 0);
                }
                catch (Exception) { /* 上报与编译生命周期隔离。 */ }
            }

            private static void ObserveUpgradeCompileError()
            {
                if (s_Upgrade is { Finished: false, CompileStarted: not null })
                    s_Upgrade.CompileError = true;
            }

            private static void ObserveUpgradeCompileFinished()
            {
                if (s_Upgrade is not { Finished: false, CompileStarted: double started })
                    return;
                try
                {
                    s_Upgrade.CompileStarted = null;
                    bool failed = s_Upgrade.CompileError;
                    s_Upgrade.HadCompileError |= failed;
                    RecordUpgradeStage("compile", failed ? "failed" : "success", EditorApplication.timeSinceStartup - started,
                        failed ? "compiler-error" : null);
                    s_NextUpgradeCheck = 0;
                }
                catch (Exception) { /* 上报与编译生命周期隔离。 */ }
            }

            private static void EndSupersededUpgrade(double time)
            {
                s_Upgrade.Finished = true;
                bool resolved = s_Upgrade.Resolved.HasValue;
                RecordUpgradeStage(resolved ? "load" : "resolve", "cancelled",
                    time - (s_Upgrade.Resolved ?? s_Upgrade.Started), "request-changed");
            }

            /// <summary>在域重载后以同一标识补入队，避免重复记录或遗漏提交阶段。</summary>
            private static void RecordUpgradeStage(string phase, string outcome, double seconds, string reason = null)
            {
                var data = ImplementationData("framework-upgrade", null, null, null);
                data["attempt"] = s_Upgrade.Id;
                data["step"] = phase;
                data["sequence"] = ++s_Upgrade.Sequence;
                data["outcome"] = outcome;
                data["from_framework"] = s_Upgrade.Previous;
                data["requested_framework"] = s_Upgrade.Requested;
                data["milliseconds"] = (long)Math.Max(0, Math.Min(86400000, seconds * 1000));
                if (reason != null)
                    data["reason"] = reason;
                var report = new JObject
                {
                    ["event"] = "implementation",
                    ["id"] = Guid.NewGuid().ToString("N"),
                    ["observed_at"] = Now(),
                    ["implementation"] = data
                };
                s_Upgrade.Reports.Add(report);
                if (s_Upgrade.Reports.Count > 24)
                    s_Upgrade.Reports.RemoveAt(1);
                SessionState.SetString(c_UpgradeSessionKey, JsonConvert.SerializeObject(s_Upgrade, Formatting.None));
                Queue((JObject)report.DeepClone());
            }
        }
    }
}
