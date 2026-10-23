/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidVersionHandlerReadOnlyContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   已绑定 EDM4U Version Handler 空可写集自然支路的只读合同
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using NovaFramework.Runtime;
using UnityEditor;
using Entry = NovaFramework.Editor.AndroidResolveActionCommon.SettingEntry;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 只观察已由 Unity InitializeOnLoad 完成初始化的当前正式实例；不调用 vendor 回调、队列、更新或取消方法。
    /// 本类只覆盖 Android、包内 gvh 命中、空重命名集及无窗口的自然通知支路，不能单独解除完整资源刷新门禁。
    /// </summary>
    internal static class AndroidVersionHandlerReadOnlyContract
    {
        private const string DllSha256 = "c3a6d0419b41b0cbc7ad518b0cc2b311af043b5d71ed709ea3baa576b79862ce";
        private static readonly string SchedulerProbeDomain = Guid.NewGuid().ToString("N");
        private const string DllMvid = "57534187-1fe2-4de8-b0f0-c598228153ae";
        private const string AnalyticsPrefix = "com.google.external-dependency-manager";
        private const string Module = "Google.VersionHandler.";
        private const string PackagePattern = @"^(Packages[/\\][^/\\]+)[/\\](.*)?$";
        private const string PhysicalPattern = @"^(Library[/\\]PackageCache[/\\])([^/\\]+)(@[^/\\]+)?[/\\](.*)?$";
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly string[] MarkerKeys =
        {
            "Temp/VersionHandlerImplCallbacks", "Temp/VersionHandlerImplRefresh", "Temp/VersionHandlerImplCleanupFilesPending",
        };
        private static readonly string[] VersionKeys =
        {
            Module + "VersionHandlingEnabled", Module + "CleanUpPromptEnabled", Module + "RenameToCanonicalFilenames",
            Module + "VerboseLoggingEnabled", Module + "RenameToDisableFilesEnabled",
        };
        private static readonly ConditionalWeakTable<object, OpaqueIdentity> Identities =
            new ConditionalWeakTable<object, OpaqueIdentity>();

        /// <summary>回执只保存布尔、键名和摘要；不保存 URL、设置原文、路径或 vendor 对象引用。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public bool enabled;
            public bool globallyEnabled;
            public bool consentRequested;
            public bool consentRequesting;
            public bool analyticsEnabled;
            public bool useProjectSettings;
            public bool pendingVersionUpdate;
            public bool pendingVersionNotification;
            public bool pendingCancelledJob;
            public bool compilingNotification;
            public bool naturallyComplete;
            public string[] observedMarkerKeys;
            public string objectIdentitySha256;
            public string assetsSha256;
            public string settingsSha256;
            public string persistenceSha256;
            public string analyticsFieldsSha256;
            public string loggerSha256;
            public string enabledEditorDllsSha256;
            public string schedulerInvariantSha256;
            public string schedulerCursorSha256;
            public string stableSha256;
            public string sha256;
        }

        /// <summary>弱身份只供本域内比较，避免持有 vendor 实例；域重载后的快照不会与旧身份相等。</summary>
        private sealed class OpaqueIdentity
        {
            internal readonly string value = Guid.NewGuid().ToString("N");
        }

        /// <summary>读取已审当前状态两次并验证一致；只应从 Unity 主线程、初始化已完成的 Action 观察阶段调用。</summary>
        /// <param name="cancellationToken">取消读取；取消异常向上保留，不消费任何 vendor 工作。</param>
        /// <param name="snapshot">成功时返回脱敏快照；失败时为 null。</param>
        /// <param name="error">失败原因；未知实现或分支必须保持 blocked，不能退化为成功。</param>
        /// <returns>两次只读观察完整且一致时为 true；不代表队列已经完成，须再检查 naturallyComplete。</returns>
        internal static bool TryCapture(CancellationToken cancellationToken, out Snapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Type owner = FindLoadedType("Google.VersionHandlerImpl");
                if (!AndroidResolveActionCommon.TryVerifyEdmType(owner, "Google.VersionHandlerImpl.dll", DllSha256,
                        cancellationToken, out error)) return false;
                Require(owner.Module.ModuleVersionId.ToString("D") == DllMvid, "Version Handler MVID 不在已审合同内。");
                Snapshot first = CaptureOnce(owner, cancellationToken);
                Snapshot second = CaptureOnce(owner, cancellationToken);
                Require(first.sha256 == second.sha256, "Version Handler 输入或实际队列在只读观察期间变化，须重新捕获。");
                snapshot = second;
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                error = exception is InvalidOperationException ? exception.Message :
                    "Version Handler 只读合同读取失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name);
                return false;
            }
        }

        /// <summary>只在两份完整快照之间读取已审队列；绑定当前域与实际类型，不持有或执行厂商委托。</summary>
        internal sealed class SchedulerProbe
        {
            private readonly Type owner;
            private readonly Type scheduler;
            private readonly string domain;
            private readonly string invariant;

            /// <summary>冻结完整快照已验证的实际类型、域身份及调度不变量，不能由外部输入代替。</summary>
            /// <param name="owner">当前唯一且已核对 DLL/MVID 的版本处理器类型。</param>
            /// <param name="scheduler">该实际程序集内的唯一调度器类型。</param>
            /// <param name="invariant">初始完整快照的固定平台及初始化委托摘要。</param>
            internal SchedulerProbe(Type owner, Type scheduler, string invariant)
            {
                this.owner = owner;
                this.scheduler = scheduler;
                this.invariant = invariant;
                domain = SchedulerProbeDomain;
            }

            /// <summary>双读精确调度合同和 pending，不遍历资源/标签或设置；未知身份、域或队列立即拒绝。</summary>
            /// <param name="token">取消只读观察，取消异常原样上抛。</param>
            /// <param name="naturallyComplete">只有本次全部已审 pending 均消失时为 true；不能替代最终完整快照。</param>
            /// <param name="error">身份、队列或稳定调度输入变化的具体拒绝原因。</param>
            /// <returns>双读身份及队列均符合初始合同且一致时为 true；是否完成单独由输出值表示。</returns>
            internal bool TryCapture(CancellationToken token, out bool naturallyComplete, out string error)
            {
                naturallyComplete = false;
                error = null;
                try
                {
                    token.ThrowIfCancellationRequested();
                    Require(domain == SchedulerProbeDomain && ReferenceEquals(owner, FindLoadedType("Google.VersionHandlerImpl")) &&
                        ReferenceEquals(scheduler, owner.Assembly.GetType("Google.RunOnMainThread", true)) &&
                        owner.Module.ModuleVersionId.ToString("D") == DllMvid,
                        "Version Handler 轻量观察的域、实际类型或 MVID 已变化。");
                    Require(AndroidResolveActionCommon.TryVerifyEdmType(owner, "Google.VersionHandlerImpl.dll", DllSha256,
                        token, out error), error);
                    Require(EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android && !EditorApplication.isCompiling &&
                        (int)Read(scheduler, "mainThreadId") == Thread.CurrentThread.ManagedThreadId &&
                        Path.GetFullPath(Environment.CurrentDirectory).TrimEnd(Path.DirectorySeparatorChar) == GenerateActionCommon.ProjectRoot,
                        "Version Handler 轻量观察须处于当前项目的稳定 Android 主线程。");
                    foreach (string key in MarkerKeys)
                        Require(!File.Exists(Path.Combine(GenerateActionCommon.ProjectRoot, key)) &&
                            !Directory.Exists(Path.Combine(GenerateActionCommon.ProjectRoot, key)),
                            "Version Handler 轻量观察发现未知回调、刷新或清理标记：" + key);
                    Snapshot first = CaptureSchedulerOnce(token);
                    Snapshot second = CaptureSchedulerOnce(token);
                    Require(first.schedulerInvariantSha256 == invariant && second.schedulerInvariantSha256 == invariant &&
                        first.sha256 == second.sha256, "Version Handler 轻量观察的调度不变量或实际队列在读取期间变化。");
                    naturallyComplete = second.naturallyComplete;
                    return true;
                }
                catch (Exception exception) when (!(exception is OperationCanceledException))
                {
                    error = exception is InvalidOperationException ? exception.Message :
                        "Version Handler 轻量观察失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name);
                    return false;
                }
            }

            /// <summary>复用同一精确调度检查读取瞬时 pending 和摘要；这些局部字段不作为完整 Snapshot 发布。</summary>
            /// <param name="token">只读观察取消标记。</param>
            /// <returns>仅供本探针双读比较的局部调度状态，不满足完整快照自洽合同。</returns>
            private Snapshot CaptureSchedulerOnce(CancellationToken token)
            {
                var result = new Snapshot { compilingNotification = (bool)Read(owner, "compiling") };
                CaptureScheduler(owner, scheduler, token, result);
                result.naturallyComplete = !result.pendingVersionUpdate && !result.pendingVersionNotification &&
                    !result.pendingCancelledJob && !result.compilingNotification;
                result.sha256 = Hash(new { result.pendingVersionUpdate, result.pendingVersionNotification,
                    result.pendingCancelledJob, result.compilingNotification, result.schedulerInvariantSha256, result.schedulerCursorSha256 });
                return result;
            }
        }

        /// <summary>从已自洽完整快照建立同域轻量观察器；不建立第二种成功合同，不调用任何厂商方法或队列。</summary>
        /// <param name="expected">本轮初始完整快照，必须已通过完整稳定输入比较。</param>
        /// <param name="token">取消只读初始化，取消异常原样上抛。</param>
        /// <param name="probe">成功时返回固定当前类型与调度不变量的观察器；失败时为 null。</param>
        /// <param name="error">无完整合同、实现漂移或未知队列的拒绝原因。</param>
        /// <returns>当前实际 DLL/MVID、域、类型和队列均已验证时为 true。</returns>
        internal static bool TryCreateSchedulerProbe(Snapshot expected, CancellationToken token, out SchedulerProbe probe, out string error)
        {
            probe = null;
            error = null;
            token.ThrowIfCancellationRequested();
            if (!IsSelfConsistent(expected))
            {
                error = "Version Handler 轻量观察必须绑定自洽的完整初始快照。";
                return false;
            }
            try
            {
                Type owner = FindLoadedType("Google.VersionHandlerImpl");
                var candidate = new SchedulerProbe(owner, owner.Assembly.GetType("Google.RunOnMainThread", true),
                    expected.schedulerInvariantSha256);
                if (!candidate.TryCapture(token, out _, out error)) return false;
                probe = candidate;
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                error = exception is InvalidOperationException ? exception.Message :
                    "Version Handler 轻量观察初始化失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name);
                return false;
            }
        }

        /// <summary>比较可复用输入；允许已审任务自然排队和完成，但不把游标变化伪装成完全相同的运行状态。</summary>
        /// <param name="expected">Plan 或 Execute 前冻结的完整快照。</param>
        /// <param name="actual">当前重新读取的完整快照。</param>
        /// <returns>两者均自洽且稳定输入一致时为 true；完成与否单独读取 actual.naturallyComplete。</returns>
        internal static bool Matches(Snapshot expected, Snapshot actual)
        {
            return IsSelfConsistent(expected) && IsSelfConsistent(actual) && expected.stableSha256 == actual.stableSha256;
        }

        /// <summary>检查摘要自洽和成功支路的固定布尔约束；不读取 Unity 或 vendor 状态。</summary>
        /// <param name="snapshot">待校验回执。</param>
        /// <returns>回执结构、支路和摘要全部有效时为 true。</returns>
        internal static bool IsSelfConsistent(Snapshot snapshot)
        {
            return snapshot != null && snapshot.enabled && !snapshot.consentRequesting &&
                (!snapshot.globallyEnabled || snapshot.consentRequested) &&
                snapshot.observedMarkerKeys != null && snapshot.observedMarkerKeys.SequenceEqual(MarkerKeys) &&
                new[] { snapshot.objectIdentitySha256, snapshot.assetsSha256, snapshot.settingsSha256,
                    snapshot.persistenceSha256, snapshot.analyticsFieldsSha256, snapshot.loggerSha256, snapshot.enabledEditorDllsSha256,
                    snapshot.schedulerInvariantSha256, snapshot.schedulerCursorSha256, snapshot.stableSha256, snapshot.sha256 }
                    .All(value => !string.IsNullOrEmpty(value)) &&
                snapshot.naturallyComplete == (!snapshot.pendingVersionUpdate && !snapshot.pendingVersionNotification &&
                    !snapshot.pendingCancelledJob && !snapshot.compilingNotification) &&
                snapshot.stableSha256 == StableHash(snapshot) && snapshot.sha256 == FullHash(snapshot);
        }

        /// <summary>冻结单次主线程观察；只调用已审公开设置读取，所有其它 vendor 成员均只读字段。</summary>
        /// <param name="owner">已核对实际 DLL、MVID 与当前包归属的 VersionHandlerImpl 类型。</param>
        /// <param name="token">读取取消标记。</param>
        /// <returns>含稳定输入和自然队列游标的完整快照。</returns>
        private static Snapshot CaptureOnce(Type owner, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Require(Path.GetFullPath(Environment.CurrentDirectory).TrimEnd(Path.DirectorySeparatorChar) ==
                GenerateActionCommon.ProjectRoot, "Unity 当前工作目录不等于冻结的项目根，vendor 相对路径语义未知。");
            Require(EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android && !EditorApplication.isCompiling,
                "Version Handler 合同仅覆盖已稳定的 Android 编辑器；编译中先等待正规编辑器完成。");
            string command = Environment.CommandLine;
            Require(!command.ToLower().Contains("-gvh_disable") &&
                command.IndexOf("-executeMethod", StringComparison.OrdinalIgnoreCase) < 0 && !UnityEngine.Application.isBatchMode,
                "Version Handler 合同不覆盖禁用标志、命令行执行模式或 batch 模式。");
            Type scheduler = owner.Assembly.GetType("Google.RunOnMainThread", true);
            Require((int)Read(scheduler, "mainThreadId") == Thread.CurrentThread.ManagedThreadId,
                "Version Handler 只读观察必须位于已初始化的 EDM 主线程。");
            foreach (string key in MarkerKeys)
                Require(!File.Exists(Path.Combine(GenerateActionCommon.ProjectRoot, key)) &&
                    !Directory.Exists(Path.Combine(GenerateActionCommon.ProjectRoot, key)),
                    "Version Handler 已有回调或刷新/清理标记，当前空效果合同不接纳：" + key);

            Type measurement = owner.Assembly.GetType("Google.EditorMeasurement", true);
            Type settingsType = owner.Assembly.GetType("Google.ProjectSettings", true);
            object settings = Read(owner, "settings");
            object analytics = Read(owner, "analytics");
            object logger = Read(owner, "logger");
            object project = Read(settingsType, "projectSettings");
            object system = Read(settingsType, "systemSettings");
            Require(settings?.GetType() == settingsType && analytics?.GetType() == measurement &&
                project?.GetType() == owner.Assembly.GetType("Google.InMemorySettings", true) &&
                system?.GetType() == owner.Assembly.GetType("Google.EditorSettings", true) &&
                ReferenceEquals(Read(settingsType, "loadedSettings"), project) &&
                ReferenceEquals(Read(measurement, "settings", analytics), settings) &&
                ReferenceEquals(Read(measurement, "logger", analytics), logger) &&
                Equals(Read(settingsType, "moduleName", settings), Module),
                "Version Handler、analytics 或设置未完成正规初始化、被替换或归属不匹配。");
            foreach (var pair in new[] {
                new Entry { name = "enabledPreferencesKey", value = AnalyticsPrefix + "AnalyticsEnabled" },
                new Entry { name = "consentRequestedPreferencesKey", value = AnalyticsPrefix + "AnalyticsConsentRequested" },
                new Entry { name = "cookiePreferencesKey", value = AnalyticsPrefix + "AnalyticsCookie" },
                new Entry { name = "systemCookiePreferencesKey", value = AnalyticsPrefix + "AnalyticsSystemCookie" } })
                Require(Equals(Read(measurement, pair.name, analytics), pair.value), "Version Handler analytics 固定偏好键漂移。");
            string before = SettingsBoundary(settingsType, project);
            string analyticsBefore = InstanceFieldsHash(measurement, analytics);
            bool global = (bool)Read(measurement, "GloballyEnabled");
            bool requesting = (bool)Read(measurement, "ConsentRequesting", analytics);
            bool consent = EditorPrefs.GetBool(AnalyticsPrefix + "AnalyticsConsentRequested", false);
            Require(!requesting && (!global || consent), "Version Handler analytics 尚未完成同意选择或已有进行中窗口。");
            var requests = VersionKeys.Select((key, index) => new AndroidResolveActionCommon.GoogleSettingRequest
                { key = key, defaultBoolean = index == 0 || index == 1 || index == 4 }).ToList();
            requests.Add(new AndroidResolveActionCommon.GoogleSettingRequest
                { key = AnalyticsPrefix + "AnalyticsEnabled", defaultBoolean = true });
            Require(AndroidResolveActionCommon.TryReadGoogleSettings(settings, requests.ToArray(), token,
                out Entry[] values, out string persistence, out string readError), readError ?? "Version Handler 设置读取失败。");
            Require(values.Single(item => item.name == VersionKeys[0]).value == bool.TrueString,
                "Version Handler 当前不是已审的 enabled=true 正规自然支路。");
            string after = SettingsBoundary(settingsType, project);
            string analyticsAfter = InstanceFieldsHash(measurement, analytics);
            Require(before == after && analyticsBefore == analyticsAfter &&
                ReferenceEquals(Read(owner, "settings"), settings) && ReferenceEquals(Read(owner, "analytics"), analytics) &&
                ReferenceEquals(Read(measurement, "settings", analytics), settings) &&
                ReferenceEquals(Read(settingsType, "projectSettings"), project) &&
                ReferenceEquals(Read(settingsType, "systemSettings"), system) &&
                ReferenceEquals(Read(settingsType, "loadedSettings"), project) &&
                Equals(Read(measurement, "GloballyEnabled"), global) &&
                Equals(Read(measurement, "ConsentRequesting", analytics), requesting),
                "Version Handler 设置、偏好、analytics 字段或对象归属在读取中变化。");

            var result = new Snapshot
            {
                enabled = true, globallyEnabled = global, consentRequested = consent, consentRequesting = requesting,
                analyticsEnabled = bool.Parse(values.Single(item => item.name == AnalyticsPrefix + "AnalyticsEnabled").value),
                useProjectSettings = EditorPrefs.GetBool(Module + "UseProjectSettings", true),
                observedMarkerKeys = MarkerKeys.ToArray(),
                objectIdentitySha256 = Hash(new[] { Identity(settings), Identity(analytics), Identity(project), Identity(system) }),
                assetsSha256 = CaptureAssetInputs(owner, token), settingsSha256 = Hash(new { after, values }),
                persistenceSha256 = persistence, analyticsFieldsSha256 = analyticsAfter,
                loggerSha256 = LoggerHash(owner, logger),
                enabledEditorDllsSha256 = EnabledDllsHash(owner, token),
                compilingNotification = (bool)Read(owner, "compiling"),
            };
            CaptureScheduler(owner, scheduler, token, result);
            result.naturallyComplete = !result.pendingVersionUpdate && !result.pendingVersionNotification &&
                !result.pendingCancelledJob && !result.compilingNotification;
            result.stableSha256 = StableHash(result);
            result.sha256 = FullHash(result);
            return result;
        }

        /// <summary>冻结真实查询、包内文件/元数据和 importer 标签；不实例化 FileMetadata，不调用 vendor 路径或标签方法。</summary>
        /// <param name="owner">已核对的版本处理器类型。</param>
        /// <param name="token">哈希读取取消标记。</param>
        /// <returns>包内 gvh 输入与两条真实路径规则的摘要；存在任何可写命中或重命名命中即抛异常。</returns>
        private static string CaptureAssetInputs(Type owner, CancellationToken token)
        {
            Type metadata = owner.Assembly.GetType("Google.VersionHandlerImpl+FileMetadata", true);
            Type files = owner.Assembly.GetType("Google.FileUtils", true);
            Require(Equals(Read(metadata, "ASSET_LABEL"), "gvh") &&
                Equals(Read(metadata, "ASSET_LABEL_RENAME_TO_DISABLE"), "gvh_rename_to_disable"), "Version Handler 标签键漂移。");
            Regex package = Read(files, "PACKAGES_ASSETDB_PATH_REGEX") as Regex;
            Regex physical = Read(files, "PACKAGES_PHYSICAL_PATH_REGEX") as Regex;
            Require(package?.ToString() == PackagePattern && physical?.ToString() == PhysicalPattern &&
                package.Options == RegexOptions.None && physical.Options == RegexOptions.None, "Version Handler 包路径规则漂移。");
            string[] rename = AssetDatabase.FindAssets("l:gvh_rename_to_disable");
            Require(rename.Length == 0, "存在 Version Handler 按平台重命名命中，当前空效果合同不接纳。");
            var rows = new List<Entry>();
            foreach (string guid in AssetDatabase.FindAssets("l:gvh").OrderBy(value => value, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string absolute = PhysicalPath(path);
                Require(package.IsMatch(path) && !path.Contains("/../") && !path.Contains("/./") &&
                    (File.Exists(absolute) || Directory.Exists(absolute)),
                    "gvh 查询包含可写、未知或失效路径；不能用只读过滤后的空集替代解析前检查。");
                AssetImporter importer = AssetImporter.GetAtPath(path);
                Require(importer != null, "gvh 命中缺少可读 importer，无法冻结正规标签解析输入。");
                rows.Add(new Entry { name = guid, value = Hash(new
                {
                    path, package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path)?.resolvedPath,
                    content = File.Exists(absolute) ? GenerateActionCommon.ComputeFileHash(absolute, token) : "directory",
                    meta = File.Exists(absolute + ".meta") ? GenerateActionCommon.ComputeFileHash(absolute + ".meta", token) : "absent",
                    labels = AssetDatabase.GetLabels(importer).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    importerType = importer.GetType().AssemblyQualifiedName,
                }) });
            }
            return Hash(new { packageRule = package.ToString(), physicalRule = physical.ToString(), rows });
        }

        /// <summary>将 AssetDatabase 包路径映射到当前正式包物理路径；只读 PackageInfo，不触发 Resolve。</summary>
        /// <param name="path">真实 AssetDatabase 路径。</param>
        /// <returns>当前已绑定包中的绝对路径；未知包或越界路径抛异常。</returns>
        private static string PhysicalPath(string path)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            Require(package != null && !string.IsNullOrEmpty(package.assetPath) && !string.IsNullOrEmpty(package.resolvedPath) &&
                path.StartsWith(package.assetPath + "/", StringComparison.Ordinal), "gvh 命中不属于当前正式包路径。");
            string root = Path.GetFullPath(package.resolvedPath).TrimEnd(Path.DirectorySeparatorChar);
            string absolute = Path.GetFullPath(Path.Combine(root, path.Substring(package.assetPath.Length + 1)));
            Require(absolute.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "gvh 包路径越界。");
            return absolute;
        }

        /// <summary>冻结现有五项 Version Handler 偏好、analytics 系统偏好及完整项目内存字典；Cookie 原文仅进入摘要。</summary>
        /// <param name="settingsType">已审 Google.ProjectSettings 类型。</param>
        /// <param name="project">已初始化且类型受限的项目内存设置对象。</param>
        /// <returns>存在状态、系统值与完整项目值的摘要。</returns>
        private static string SettingsBoundary(Type settingsType, object project)
        {
            var dictionary = Read(project.GetType(), "settings", project) as SortedDictionary<string, string>;
            Require(dictionary != null, "Google 项目设置集合签名漂移。");
            var keys = VersionKeys.Concat(new[] { Module + "UseProjectSettings", AnalyticsPrefix + "AnalyticsEnabled",
                AnalyticsPrefix + "AnalyticsConsentRequested", AnalyticsPrefix + "AnalyticsCookie", AnalyticsPrefix + "AnalyticsSystemCookie" });
            var preferences = keys.Select(key => new Entry { name = key, value = Hash(new
            {
                exists = EditorPrefs.HasKey(key),
                value = key.EndsWith("Cookie", StringComparison.Ordinal) ? EditorPrefs.GetString(key, "") :
                    EditorPrefs.GetBool(key, key != VersionKeys[2] && key != VersionKeys[3] &&
                        !key.EndsWith("ConsentRequested", StringComparison.Ordinal)).ToString(),
            }) }).ToArray();
            return Hash(new { preferences, project = dictionary,
                loaded = Identity(Read(settingsType, "loadedSettings")),
                projectIdentity = Identity(Read(settingsType, "projectSettings")), system = Identity(Read(settingsType, "systemSettings")) });
        }

        /// <summary>只读 analytics 实例字段；字符串和布尔做摘要，其余对象只记录本域不透明身份。</summary>
        /// <param name="type">已审 EditorMeasurement 类型。</param>
        /// <param name="value">现存 analytics 实例。</param>
        /// <returns>字段集合的脱敏摘要。</returns>
        private static string InstanceFieldsHash(Type type, object value)
        {
            return Hash(type.GetFields(Instance).OrderBy(field => field.Name, StringComparer.Ordinal).Select(field =>
            {
                object item = field.GetValue(value);
                return new Entry { name = field.Name, value = item is string || item is bool ? Hash(item) : Identity(item) };
            }).ToArray());
        }

        /// <summary>冻结 Version Handler 与 analytics 共用的现存日志器，只允许普通 Unity 控制台目标。</summary>
        /// <param name="owner">已审版本处理器类型。</param>
        /// <param name="logger">已绑定且与 analytics 同属的现存日志器。</param>
        /// <returns>日志器身份与标量配置摘要；文件目标、文件名或自定义委托存在时拒绝。</returns>
        private static string LoggerHash(Type owner, object logger)
        {
            Type type = owner.Assembly.GetType("Google.Logger", true);
            Require(logger?.GetType() == type && Read(type, "LogMessage", logger) == null &&
                Read(type, "<Target>k__BackingField", logger) is Enum &&
                Read(type, "<Level>k__BackingField", logger) is Enum &&
                string.IsNullOrEmpty(Read(type, "<LogFilename>k__BackingField", logger) as string),
                "Version Handler 日志器含未知类型、文件名或自定义回调。");
            int target = Convert.ToInt32(Read(type, "<Target>k__BackingField", logger));
            Require((target & ~6) == 0, "Version Handler 日志器包含文件或未知输出目标。");
            return Hash(new { identity = Identity(logger), target,
                level = Convert.ToInt32(Read(type, "<Level>k__BackingField", logger)) });
        }

        /// <summary>核对通知轮询需要的全部 DLL 已在本域加载；不调用 EnabledEditorDllsLoaded getter 或加载程序集。</summary>
        /// <param name="owner">版本处理器类型。</param>
        /// <param name="token">文件哈希取消标记。</param>
        /// <returns>已启用 DLL 集合及已加载模块绑定的摘要；不齐备时抛异常。</returns>
        private static string EnabledDllsHash(Type owner, CancellationToken token)
        {
            Require(Read(owner, "enabledEditorDlls") is HashSet<string>, "Version Handler 已启用 DLL 集合签名漂移。");
            var enabled = (HashSet<string>)Read(owner, "enabledEditorDlls");
            var loaded = new Dictionary<string, Assembly>(StringComparer.Ordinal);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { if (!string.IsNullOrEmpty(assembly.Location)) loaded[Path.GetFullPath(assembly.Location)] = assembly; }
                catch (NotSupportedException) { }
            }
            var result = new List<Entry>();
            foreach (string path in enabled.OrderBy(value => value, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                Require(loaded.TryGetValue(path, out Assembly assembly) && File.Exists(path),
                    "Version Handler 仍等待已启用 DLL 加载；当前不得声称通知支路完成。");
                result.Add(new Entry { name = Hash(path), value = Hash(new { assembly = assembly.FullName,
                    mvid = assembly.ManifestModule.ModuleVersionId, sha256 = GenerateActionCommon.ComputeFileHash(path, token) }) });
            }
            return Hash(result);
        }

        /// <summary>只观察真实队列与 ScheduledJob 注册表；只接纳已审自然更新、通知和当前恒 false 的平台初始化项。</summary>
        /// <param name="owner">版本处理器类型。</param>
        /// <param name="scheduler">同一实际 DLL 内的 RunOnMainThread 类型。</param>
        /// <param name="token">读取取消标记。</param>
        /// <param name="snapshot">输出已审 pending 布尔和脱敏队列摘要，不执行或移除任何条目。</param>
        private static void CaptureScheduler(Type owner, Type scheduler, CancellationToken token, Snapshot snapshot)
        {
            Require(Read(scheduler, "jobs") is Queue<Action> && Read(scheduler, "pollingJobs") is List<Func<bool>> &&
                Read(scheduler, "completePollingJobs") is List<Func<bool>> &&
                Equals(Read(scheduler, "runningExecuteAllCount"), 0) && Equals(Read(scheduler, "runningJobs"), false),
                "EDM 队列包含运行中或未知结构，当前无法只读冻结。");
            var jobs = (Queue<Action>)Read(scheduler, "jobs");
            var polls = (List<Func<bool>>)Read(scheduler, "pollingJobs");
            var complete = (List<Func<bool>>)Read(scheduler, "completePollingJobs");
            Require(complete.Count == 0, "EDM 已有尚未移除的完成项，须先等正规主线程循环自然收尾。");
            Type scheduledType = owner.Assembly.GetType("Google.RunOnMainThread+ScheduledJob", true);
            var registry = Read(scheduledType, "scheduledJobs") as IDictionary;
            Require(registry != null && registry.GetType().GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
                registry.GetType().GetGenericArguments().SequenceEqual(new[] { typeof(int), scheduledType }), "EDM 延迟任务注册表签名漂移。");
            Type singleton = owner.Assembly.GetType("Google.VersionHandlerImpl+<>c", true);
            object singletonValue = Read(singleton, "<>9");
            Type updateClosure = owner.Assembly.GetType("Google.VersionHandlerImpl+<>c__DisplayClass82_0", true);
            Type notifyClosure = owner.Assembly.GetType("Google.VersionHandlerImpl+<>c__DisplayClass39_0", true);
            Type checker = owner.Assembly.GetType("Google.VersionHandlerImpl+BuildTargetChecker", true);
            Require(Read(checker, "lastKnownBuildTarget") is BuildTarget known && known == BuildTarget.Android,
                "Version Handler 尚未记录当前 Android 平台，正规队列可能进入平台变化处理。");
            var invariants = new List<Entry>();
            var cursors = new List<Entry>();
            var seenScheduled = new HashSet<object>();
            int versionScheduled = 0;
            int checkerScheduled = 0;
            foreach (Action callback in jobs)
            {
                token.ThrowIfCancellationRequested();
                Require(Single(callback, typeof(void)) && callback.Method.DeclaringType == updateClosure &&
                    callback.Method.Name == "<UpdateVersionedAssets>b__0" && callback.Target?.GetType() == updateClosure &&
                    Equals(Read(updateClosure, "forceUpdate", callback.Target), false) &&
                    IsSingletonAction(Read(updateClosure, "complete", callback.Target) as Delegate, singleton, singletonValue,
                        "<UpdateVersionedAssets>b__80_0"), "EDM 普通队列包含未知工作或非默认完成回调。");
                snapshot.pendingVersionUpdate = true;
                cursors.Add(new Entry { name = "queuedUpdate", value = Identity(callback.Target) });
            }
            foreach (Func<bool> poll in polls)
            {
                token.ThrowIfCancellationRequested();
                Require(Single(poll, typeof(bool)) && poll.Target != null, "EDM 轮询委托签名未知。");
                Type targetType = poll.Target.GetType();
                if (targetType == scheduledType && poll.Method.DeclaringType == scheduledType && poll.Method.Name == "PollUntilExecutionTime")
                {
                    Delegate callback = Read(scheduledType, "Job", poll.Target) as Delegate;
                    int id = (int)Read(scheduledType, "JobId", poll.Target);
                    double delay = (double)Read(scheduledType, "DelayInMilliseconds", poll.Target);
                    object scheduledTime = Read(scheduledType, "scheduledTime", poll.Target);
                    Require(scheduledTime is DateTime && !double.IsNaN(delay) && !double.IsInfinity(delay) && delay >= 0,
                        "EDM 延迟任务游标签名未知。");
                    if (callback == null)
                    {
                        Require(id == 0 && !registry.Values.Cast<object>().Any(item => ReferenceEquals(item, poll.Target)),
                            "EDM 空延迟任务与注册表不一致。");
                        snapshot.pendingCancelledJob = true;
                        cursors.Add(new Entry { name = "cancelledNoOp", value = Hash(new { delay, scheduledTime, identity = Identity(poll.Target) }) });
                        continue;
                    }
                    Require(id != 0 && registry.Contains(id) && ReferenceEquals(registry[id], poll.Target) && seenScheduled.Add(poll.Target),
                        "EDM 延迟任务和真实注册表不一致。");
                    if (IsSingletonAction(callback, singleton, singletonValue, "<OnPostprocessAllAssets>b__89_0") && delay == 2000 &&
                        Equals(Read(owner, "updateVersionedAssetsJob"), id))
                    {
                        Require(++versionScheduled <= 1, "Version Handler 存在重复活动延迟更新。");
                        snapshot.pendingVersionUpdate = true;
                        cursors.Add(new Entry { name = "scheduledUpdate", value = Hash(new { id, delay, scheduledTime, identity = Identity(poll.Target) }) });
                    }
                    else if (Single(callback, typeof(void)) && callback.Target == null && callback.Method.IsStatic &&
                        callback.Method.DeclaringType == checker && callback.Method.Name == "CheckBuildTarget" && delay == 1000)
                    {
                        Require(++checkerScheduled <= 1, "Version Handler 存在重复平台检查任务。");
                        // 已记录 Android 且重命名查询为空；该正规项只重新排队，不应当作为完成屏障。
                        cursors.Add(new Entry { name = "platformCheck", value = Hash(new { id, delay, scheduledTime, identity = Identity(poll.Target) }) });
                    }
                    else throw new InvalidOperationException("EDM 延迟任务身份、时延或归属不在已审合同内。");
                }
                else if (targetType == notifyClosure && poll.Method.DeclaringType == notifyClosure &&
                    poll.Method.Name == "<NotifyWhenCompliationComplete>b__0" && Read(notifyClosure, "forceNotification", poll.Target) is bool force)
                {
                    snapshot.pendingVersionNotification = true;
                    cursors.Add(new Entry { name = "compilationNotification", value = Hash(new { force, identity = Identity(poll.Target) }) });
                }
                else
                {
                    string invariant = CaptureInactiveInitializer(owner, poll, token);
                    invariants.Add(new Entry { name = "inactiveInitializer", value = invariant });
                }
            }
            Require(registry.Count == seenScheduled.Count, "EDM 注册表中含有轮询集合外的未知延迟工作。");
            snapshot.schedulerInvariantSha256 = Hash(new { target = BuildTarget.Android.ToString(),
                command = Hash(Environment.CommandLine), initializers = invariants.ToArray() });
            // 历史 updateVersionedAssetsJob 即使非零也不代表待执行；仅把它记录为本次观察游标。
            snapshot.schedulerCursorSha256 = Hash(new { updateJob = Read(owner, "updateVersionedAssetsJob"), cursors });
        }

        /// <summary>核验当前 Android 下恒 false 的已有 iOS 初始化条件，不调用条件或初始化委托。</summary>
        /// <param name="owner">已审 Version Handler 类型。</param>
        /// <param name="poll">真实队列中的轮询项。</param>
        /// <param name="token">DLL 摘要读取取消标记。</param>
        /// <returns>完整现存条件身份的脱敏摘要；未知条件、启用命令行执行或 DLL 漂移时抛异常。</returns>
        private static string CaptureInactiveInitializer(Type owner, Delegate poll, CancellationToken token)
        {
            Type closure = owner.Assembly.GetType("Google.EditorInitializer+<>c__DisplayClass0_0", true);
            Require(poll.Target?.GetType() == closure && poll.Method.DeclaringType == closure &&
                poll.Method.Name == "<InitializeOnMainThread>b__0" && Equals(Read(closure, "isExecuteMethodEnabled", poll.Target), false),
                "EDM 轮询包含未知或可能执行的初始化工作。");
            Delegate condition = Read(closure, "condition", poll.Target) as Delegate;
            Require(Single(condition, typeof(bool)), "EDM 初始化条件签名漂移。");
            Type type = condition.Method.DeclaringType;
            bool ios = type?.FullName == "Google.IOSResolver+<>c" && condition.Method.Name == "<.cctor>b__65_0";
            bool firebase = type?.FullName == "Firebase.Editor.XcodeProjectPatcher+<>c" && condition.Method.Name == "<.cctor>b__11_0";
            Require((ios || firebase) && condition.Target?.GetType() == type &&
                ReferenceEquals(Read(type, "<>9"), condition.Target), "EDM 初始化条件不在已审 Android 恒 false 集合。");
            if (ios)
                Require(AndroidResolveActionCommon.TryVerifyEdmType(type, "Google.IOSResolver.dll",
                    "ca00858a5f4a657e949480015124f9c783ea80c42bb6000a9c5ec0112ffa5ca5", token, out string error), error);
            else Require(Path.GetFileName(type.Assembly.Location) == "Firebase.Editor.dll" &&
                GenerateActionCommon.ComputeFileHash(type.Assembly.Location, token) ==
                    "b50eefa93d23c673bc8a97266e3d36dcfba0e9d3e49f5e7cc7850d595c12f1bf", "Firebase 平台条件实际 DLL 漂移。");
            return Hash(new { closure = Identity(poll.Target), condition = condition.Method.ToString(),
                conditionOwner = type.AssemblyQualifiedName, target = Identity(condition.Target) });
        }

        /// <summary>检查已审默认单例动作的精确归属与单播签名；不执行委托。</summary>
        /// <param name="callback">待核验委托。</param>
        /// <param name="type">预期编译器单例类型。</param>
        /// <param name="target">预期已初始化单例对象。</param>
        /// <param name="name">已审方法名。</param>
        /// <returns>单播无参 void、方法归属及目标引用均相符时为 true。</returns>
        private static bool IsSingletonAction(Delegate callback, Type type, object target, string name)
        {
            return Single(callback, typeof(void)) && callback.Method.DeclaringType == type && callback.Method.Name == name &&
                callback.Target?.GetType() == type && ReferenceEquals(callback.Target, target);
        }

        /// <summary>核对无参数单播委托，不执行任何目标代码。</summary>
        /// <param name="callback">待核验委托。</param>
        /// <param name="returnType">允许的返回类型。</param>
        /// <returns>非空、单播、无参且返回类型相符时为 true。</returns>
        private static bool Single(Delegate callback, Type returnType)
        {
            return callback != null && callback.GetInvocationList().Length == 1 && callback.Method.ReturnType == returnType &&
                callback.Method.GetParameters().Length == 0;
        }

        /// <summary>在现存 AppDomain 中查找唯一类型，不加载程序集、不调用 Type.GetType 的自动加载链。</summary>
        /// <param name="fullName">固定完整类型名。</param>
        /// <returns>唯一已加载类型；缺失或重复时抛异常。</returns>
        private static Type FindLoadedType(string fullName)
        {
            Type[] types = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(fullName, false))
                .Where(type => type != null).Distinct().ToArray();
            Require(types.Length == 1, "Version Handler 类型缺失或重复加载。");
            return types[0];
        }

        /// <summary>读取已绑定类型的字段，缺失字段立即拒绝；不回退 getter、setter 或其它方法。</summary>
        /// <param name="type">字段声明类型。</param>
        /// <param name="name">固定字段名。</param>
        /// <param name="target">实例字段所属对象；null 仅表示读取静态字段。</param>
        /// <returns>当前字段值，合法 null 原样返回。</returns>
        private static object Read(Type type, string name, object target = null)
        {
            FieldInfo field = type.GetField(name, target == null ? Static : Instance);
            Require(field != null, "Version Handler 已审字段缺失：" + name);
            return field.GetValue(target);
        }

        /// <summary>分配本类域内弱身份；不调用或修改 vendor 对象。</summary>
        /// <param name="value">现存对象，允许为 null。</param>
        /// <returns>不可反推原对象的不透明身份；null 使用固定空标记。</returns>
        private static string Identity(object value)
        {
            return value == null ? "null" : Identities.GetValue(value, _ => new OpaqueIdentity()).value;
        }

        /// <summary>只序列化本类构造的纯 DTO/标量，复用现有 Nova SHA-256 算法。</summary>
        /// <param name="value">瞬时内存证据；不得传入 vendor 实例。</param>
        /// <returns>序列化内容的 SHA-256。</returns>
        private static string Hash(object value)
        {
            return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(value));
        }

        /// <summary>计算可复用输入摘要，显式排除调度任务编号、时刻、pending 状态及已审缓存失效。</summary>
        /// <param name="value">完整快照。</param>
        /// <returns>稳定输入的 SHA-256。</returns>
        private static string StableHash(Snapshot value)
        {
            return Hash(new { DllSha256, DllMvid, value.enabled, value.globallyEnabled, value.consentRequested,
                value.consentRequesting, value.analyticsEnabled, value.useProjectSettings, value.observedMarkerKeys,
                value.objectIdentitySha256, value.assetsSha256, value.settingsSha256, value.persistenceSha256,
                value.analyticsFieldsSha256, value.loggerSha256, value.enabledEditorDllsSha256, value.schedulerInvariantSha256 });
        }

        /// <summary>计算完整观察摘要，排除 sha256 自身；保存 pending 变化以供异步等待和审计。</summary>
        /// <param name="value">完整快照。</param>
        /// <returns>当前观察的 SHA-256。</returns>
        private static string FullHash(Snapshot value)
        {
            return Hash(new { stable = StableHash(value), value.pendingVersionUpdate, value.pendingVersionNotification,
                value.pendingCancelledJob, value.compilingNotification, value.naturallyComplete, value.schedulerCursorSha256 });
        }

        /// <summary>统一拒绝未知支路；异常由 TryCapture 转换为 blocked 原因。</summary>
        /// <param name="condition">必须成立的只读合同条件。</param>
        /// <param name="message">失败的精确范围。</param>
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
