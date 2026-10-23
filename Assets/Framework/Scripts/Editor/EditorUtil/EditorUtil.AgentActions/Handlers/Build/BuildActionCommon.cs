/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BuildActionCommon.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   Nova Project Build Action 的冻结计划与产物只读验证辅助
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Text.RegularExpressions;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    internal static class BuildActionCommon
    {
        internal const string NoEncryptionPolicy = "none";

        [Serializable]
        internal sealed class ArtifactReceipt
        {
            public bool finalized;
            public string target;
            public string outputPath;
            public string outputKind;
            public string outputHashSha256;
            public string manifestPath;
            public string manifestHashSha256;
            public string bundledOutputPath;
            public string bundledOutputHashSha256;
            public string packageName;
            public string packageVersion;
            public string[] scenes;
            public bool? developmentBuild;
            public string buildMode;
            public string buildReportResult;
            public BundleWriteSnapshot bundleWrites;
            public BundleInputSnapshot bundleInputs;
            public GenerateActionCommon.Artifact[] bundleAuxiliaryArtifacts;
            public bool? preservedSceneInputsUnchanged;
            public string preservedSceneInputError;
            public CallbackExecutionReceipt callbackExecution;
        }

        [Serializable]
        internal sealed class BundleInputSnapshot
        {
            public string masterGuid;
            public string masterPath;
            public string masterHashSha256;
            public string platform;
            public string channel;
            public string developMode;
            public string yooAssetSettingsPath;
            public string yooAssetSettingsGuid;
            public string yooAssetSettingsHashSha256;
            public string yooFolderName;
            public string packageFilePrefix;
            public string bundleCollectorPath;
            public string bundleCollectorGuid;
            public string bundleCollectorHashSha256;
            public string packageName;
            public string[] collectorRuleIdentities;
        }

        /// <summary>Bundle 管线正式解析的共享写集；共享缓存路径不等于缓存完整性证据。</summary>
        [Serializable]
        internal sealed class BundleWriteSnapshot
        {
            public string buildPipeline;
            public int bundleType;
            public string pipelineOutputPath;
            public string scriptOutputPath;
            public string dependencyDatabasePath;
            public string[] cacheWritePaths;
            public string[] temporaryWritePaths;
            public BuildCallbackSnapshot callbacks;
            public GenerateActionCommon.Artifact[] preservedSceneInputs;
            public string[] preservedSceneDependencyPaths;
            public string[] preservedSceneDependencyIdentities;
            public string[] sceneSetup;
        }

        /// <summary>冻结回调的编译身份和已知写入契约；不实例化或调用回调。</summary>
        [Serializable]
        internal sealed class BuildCallbackSnapshot
        {
            public string[] identities;
            public string[] unsupported;
            public string[] knownEffects;
            public FirebaseCrashlyticsSnapshot firebaseCrashlytics;
            public AndroidResolveActionCommon.SceneResolutionSnapshot edmScene;
            public AndroidVersionHandlerReadOnlyContract.Snapshot edmVersionHandler;
            public string edmDomain;
        }

        /// <summary>Firebase 正常 Android 回调的冻结输入，生成文件与标签可写项保持独立。</summary>
        [Serializable]
        internal sealed class FirebaseCrashlyticsSnapshot
        {
            public bool generated;
            public string unityVersion;
            public string[] metadataRoots;
            public CallbackFileState[] resources;
            public CallbackFileState[] preservedFiles;
            public MetadataLabelInput[] metadata;
            public FirebaseLoggerState versionHandlerLogger;
        }

        /// <summary>冻结已审 VersionHandler 日志实例；仅允许当前 Console 分支，禁止隐含文件写入和外部委托。</summary>
        [Serializable]
        internal sealed class FirebaseLoggerState
        {
            public int target;
            public int level;
        }

        /// <summary>允许缺失的精确文件或目录状态；缺失不伪造 Hash。</summary>
        [Serializable]
        internal sealed class CallbackFileState
        {
            public string path;
            public string kind;
            public string sha256;
        }

        /// <summary>EDM 首次初始化候选及合法标签结果；源内容、GUID 和其它 importer 字段必须保持。</summary>
        [Serializable]
        internal sealed class MetadataLabelInput
        {
            public string path;
            public string guid;
            public string importerType;
            public string[] labels;
            public string[] expectedLabels;
            public bool readOnly;
            public bool manifest;
            public CallbackFileState source;
            public CallbackFileState meta;
            public string metaWithoutLabelsSha256;
        }

        /// <summary>一次正常回调执行的真实前后状态和日志；失败也保留已有写入，不重放。</summary>
        [Serializable]
        internal sealed class CallbackExecutionReceipt
        {
            public bool complete;
            public string error;
            public FirebaseCrashlyticsSnapshot before;
            public FirebaseCrashlyticsSnapshot after;
            public string[] successLogs;
            public string[] failureLogs;
            public string[] changedPaths;
            public EdmSceneExecutionReceipt edmScene;
            public EdmNaturalCompletionReceipt edmNatural;
        }

        /// <summary>本次作用域内自然 update 的类型化证据；域变化、未知工作及取消均保留未完成状态。</summary>
        [Serializable]
        internal sealed class EdmNaturalCompletionReceipt
        {
            public string beforeDomain;
            public string afterDomain;
            public bool sameDomain;
            public int naturalUpdates;
            /// <summary>仅计本次每个真实 update 后成功完成的廉价双读；旧回执缺失该字段时为零。</summary>
            public int schedulerProbeObservations;
            public AndroidVersionHandlerReadOnlyContract.Snapshot[] observations;
            public string schedulerDiagnostic;
            public string error;
        }

        /// <summary>一次正规 Android 场景回调的输入与真实事件；后续场景游标不能替代首次无需解析证据。</summary>
        [Serializable]
        internal sealed class EdmSceneExecutionReceipt
        {
            public AndroidResolveActionCommon.SceneResolutionSnapshot before;
            public AndroidResolveActionCommon.SceneResolutionSnapshot after;
            public string[] events;
            public string[] failureKinds;
            public SbpSceneCacheEvidence.Receipt cacheEvidence;
        }

        /// <summary>
        /// 按实际 Bundle 参数类解析 OutputCache、脚本输出和共享缓存，拒绝未知回调与未保存资产。
        /// 默认正规 XML 预读用于 Plan/Execute；执行后及 Verify 传 false，仅复核当前场景输入。
        /// </summary>
        /// <param name="target">已解析且与当前活动平台一致的冻结目标。</param>
        /// <param name="packageName">已验证的目标 Package 名称。</param>
        /// <param name="version">本次冻结的 Package 版本。</param>
        /// <param name="rawFile">为 true 时沿 RawFile 参数与写集；为 false 时冻结 SBP 回调、场景及脏资产门。</param>
        /// <param name="useDependencyDatabase">是否把正规 AssetDependencyDB 路径纳入计划写集。</param>
        /// <param name="inputs">已经冻结的 ConfigMaster、YooAsset 与 Collector 输入。</param>
        /// <param name="cancellationToken">只读捕获与文件哈希的取消令牌，不取消厂商任务。</param>
        /// <param name="snapshot">返回完整写集及回调闭包；回调不支持时仍保留已捕获内容供诊断。</param>
        /// <param name="error">路径、回调、场景或脏资产合同不满足时的具体原因。</param>
        /// <param name="readXmlDependencies">默认 true 仅供 Plan 和 Execute 写前公开 XML 内存预读；执行后及 Verify 必须显式 false。</param>
        /// <returns>全部写集与安全门已冻结时为 true；失败时为 false，不保存资产或调用构建回调。</returns>
        internal static bool TryCaptureBundleWrites(
            BuildTarget target, string packageName, string version, bool rawFile, bool useDependencyDatabase, BundleInputSnapshot inputs,
            CancellationToken cancellationToken, out BundleWriteSnapshot snapshot, out string error, bool readXmlDependencies = true)
        {
            snapshot = null;
            BuildParameters parameters = rawFile ? (BuildParameters)new RawFileBuildParameters() : new ScriptableBuildParameters();
            parameters.BuildTarget = target;
            parameters.PackageName = packageName;
            parameters.PackageVersion = version;
            parameters.BuildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot();
            if (!GenerateActionCommon.TryResolveProjectPath(parameters.GetPipelineOutputDirectory(), "Bundle pipeline output", out string pipeline, out error)) return false;
            string script = null;
            var caches = new List<string>();
            var temporary = new List<string>();
            GenerateActionCommon.Artifact[] sceneInputs = null;
            string[] sceneDependencyPaths = null;
            string[] sceneDependencyIdentities = null;
            string[] sceneSetup = null;
            BuildCallbackSnapshot callbacks = null;
            if (!rawFile)
            {
                if (!GenerateActionCommon.TryResolveProjectPath(UnityEditor.Build.Pipeline.ContentPipeline.kScriptBuildPath, "SBP script output", out script, out error) ||
                    !GenerateActionCommon.TryResolveProjectPath(UnityEditor.Build.Pipeline.ContentPipeline.kTempBuildPath, "SBP temporary output", out string temp, out error) ||
                    !GenerateActionCommon.TryResolveProjectPath("Library/BuildCache", "SBP shared cache", out string cache, out error) ||
                    !GenerateActionCommon.TryResolveProjectPath("Library/Bee", "Unity compile workspace", out string bee, out error)) return false;
                if (Directory.Exists(temp) || File.Exists(temp))
                {
                    error = "SBP 会删除已有临时输出，当前路径已存在且无法归属本次操作：" + temp;
                    return false;
                }
                temporary.Add(temp);
                caches.Add(cache);
                caches.Add(bee);
                callbacks = CaptureBuildCallbacks(false, cancellationToken, readXmlDependencies);
                if (!TryCaptureSavedScenes(inputs, cancellationToken, out sceneInputs, out sceneDependencyPaths, out sceneDependencyIdentities, out sceneSetup, out error)) return false;
                if (callbacks.firebaseCrashlytics != null)
                {
                    var writes = new HashSet<string>(GetCallbackWriteSet(callbacks), StringComparer.Ordinal);
                    sceneInputs = sceneInputs.Where(input => !writes.Contains(input.path)).ToArray();
                    sceneDependencyIdentities = sceneDependencyPaths.Select(path =>
                    {
                        string physical = FileUtil.GetPhysicalPath(path);
                        string full = Path.GetFullPath(Path.IsPathRooted(physical) ? physical : Path.Combine(ProjectRoot, string.IsNullOrEmpty(physical) ? path : physical));
                        return path + "|" + AssetDatabase.AssetPathToGUID(path) + "|" + (writes.Contains(full) || writes.Contains(full + ".meta") ? "callback-write-verified-separately" : AssetDatabase.GetAssetDependencyHash(path).ToString());
                    }).ToArray();
                }
            }
            string database = null;
            if (useDependencyDatabase && !GenerateActionCommon.TryResolveProjectPath("Library/AssetDependencyDB", "YooAsset dependency database", out database, out error)) return false;
            snapshot = new BundleWriteSnapshot
            {
                buildPipeline = rawFile ? nameof(EBuildPipeline.RawFileBuildPipeline) : nameof(EBuildPipeline.ScriptableBuildPipeline),
                bundleType = (int)(rawFile ? EBundleType.RawBundle : EBundleType.AssetBundle),
                pipelineOutputPath = pipeline,
                scriptOutputPath = script,
                dependencyDatabasePath = database,
                cacheWritePaths = caches.ToArray(),
                temporaryWritePaths = temporary.ToArray(),
                callbacks = callbacks,
                preservedSceneInputs = sceneInputs,
                preservedSceneDependencyPaths = sceneDependencyPaths,
                preservedSceneDependencyIdentities = sceneDependencyIdentities,
                sceneSetup = sceneSetup,
            };
            if (callbacks?.unsupported?.Length > 0)
            {
                error = "SBP 回调尚无确定写入契约，不能执行：" + string.Join("; ", callbacks.unsupported);
                return false;
            }
            if (!rawFile && !TryValidateNoDirtyPersistentAssets(out error)) return false;
            error = null;
            return true;
        }

        /// <summary>把共享管线、脚本、数据库、缓存及已审单键 EditorPrefs 效果纳入精确计划；删除范围另行声明。</summary>
        /// <param name="snapshot">本次完整共享管线、缓存、临时路径与回调快照。</param>
        /// <returns>去空去重后的精确写集；包含已审单键 EditorPrefs 的效果说明，不把它当作磁盘路径。</returns>
        internal static string[] GetBundleSharedWriteSet(BundleWriteSnapshot snapshot)
        {
            return new[] { snapshot.pipelineOutputPath, snapshot.scriptOutputPath, snapshot.dependencyDatabasePath }
                .Concat(snapshot.cacheWritePaths ?? Array.Empty<string>())
                .Concat(snapshot.temporaryWritePaths ?? Array.Empty<string>())
                .Concat(GetCallbackWriteSet(snapshot.callbacks))
                .Concat(snapshot.callbacks?.edmScene?.settingsPersistence == null ? Array.Empty<string>() :
                    new[] { "Unity EditorPrefs:GooglePlayServices.UseJetifier (normal callback materializes effective True only)" })
                .Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).ToArray();
        }

        /// <summary>只读比较正式路径及完整稳定输入；Version Handler 周期任务游标单独记录，执行前仍必须自然完成。</summary>
        /// <param name="expected">已经冻结的共享写集和回调输入。</param>
        /// <param name="actual">当前只读捕获的完整写集和回调输入。</param>
        /// <returns>仅已审周期游标变化且其它字段均相同时为 true。</returns>
        internal static bool BundleWritesEqual(BundleWriteSnapshot expected, BundleWriteSnapshot actual)
        {
            if (expected == null || actual == null) return false;
            BundleWriteSnapshot copy = Util.Json.Deserialize<BundleWriteSnapshot>(Util.Json.Serialize(actual));
            if (expected.callbacks?.edmVersionHandler != null)
            {
                if (!AndroidVersionHandlerReadOnlyContract.Matches(expected.callbacks.edmVersionHandler, copy.callbacks?.edmVersionHandler) ||
                    !copy.callbacks.edmVersionHandler.naturallyComplete) return false;
                copy.callbacks.edmVersionHandler = expected.callbacks.edmVersionHandler;
            }
            return Util.Json.Serialize(expected) == Util.Json.Serialize(copy);
        }

        /// <summary>声明正常回调的精确资源、meta 和正规同值设置文件；EditorPrefs 键由共享计划单独声明。</summary>
        /// <param name="callbacks">已冻结的 Firebase 与可选 Android 场景回调闭包。</param>
        /// <returns>正常回调允许写入的资源、meta 与正规同值设置文件的唯一排序路径集；不证明实际已经写入。</returns>
        internal static string[] GetCallbackWriteSet(BuildCallbackSnapshot callbacks)
        {
            FirebaseCrashlyticsSnapshot firebase = callbacks?.firebaseCrashlytics;
            var writes = new List<string>();
            if (firebase != null)
                writes.AddRange(firebase.resources.Where(file => file.kind != "directory" || file.path.EndsWith("FirebaseCrashlytics.androidlib", StringComparison.Ordinal) || file.path.Contains("FirebaseCrashlytics.androidlib/"))
                    .Select(file => file.path).Concat(firebase.metadata.Where(input => !input.readOnly && !string.IsNullOrEmpty(input.guid)).Select(input => input.meta.path)));
            if (callbacks?.edmScene?.settingsPersistence != null) writes.Add(callbacks.edmScene.settingsPersistence.projectPath);
            return writes.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        /// <summary>只允许 Receipt 已核验的回调写入变化；其它共享路径、场景输入和回调身份仍逐项冻结。</summary>
        private static bool BundleWritesEqualAfter(BundleWriteSnapshot expected, BundleWriteSnapshot actual, CallbackExecutionReceipt execution)
        {
            if (expected == null || actual == null || !BuildCallbacksEqualAfter(expected.callbacks, actual.callbacks, execution)) return false;
            BundleWriteSnapshot copy = Util.Json.Deserialize<BundleWriteSnapshot>(Util.Json.Serialize(actual));
            copy.callbacks = expected.callbacks;
            return BundleWritesEqual(expected, copy);
        }

        /// <summary>分别核对 Firebase 写入和 Android 场景真实凭据；场景计数只允许已证明的自然增量。</summary>
        /// <param name="expected">执行前冻结的回调闭包。</param>
        /// <param name="actual">执行后或 Verify 当前只读回调闭包。</param>
        /// <param name="execution">本次实际执行的完整类型化凭据。</param>
        /// <returns>凭据完整、同域且只存在已声明变化时为 true。</returns>
        internal static bool BuildCallbacksEqualAfter(BuildCallbackSnapshot expected, BuildCallbackSnapshot actual, CallbackExecutionReceipt execution)
        {
            if (expected == null || actual == null) return expected == actual;
            BuildCallbackSnapshot copy = Util.Json.Deserialize<BuildCallbackSnapshot>(Util.Json.Serialize(actual));
            if (expected.firebaseCrashlytics != null)
            {
                if (execution == null || !execution.complete || Util.Json.Serialize(execution.before) != Util.Json.Serialize(expected.firebaseCrashlytics) ||
                    Util.Json.Serialize(execution.after) != Util.Json.Serialize(actual.firebaseCrashlytics)) return false;
                copy.firebaseCrashlytics = expected.firebaseCrashlytics;
            }
            if (expected.edmScene != null)
            {
                if (execution == null || !execution.complete || execution.edmScene == null ||
                    Util.Json.Serialize(execution.edmScene.before) != Util.Json.Serialize(expected.edmScene) ||
                    !AndroidResolveActionCommon.SceneResolutionSnapshotsMatch(execution.edmScene.after, actual.edmScene) ||
                    actual.edmScene.scenesProcessed < execution.edmScene.after.scenesProcessed) return false;
                try { ValidateEdmSceneExecution(execution.edmScene); }
                catch (InvalidOperationException) { return false; }
                if (expected.edmVersionHandler != null)
                {
                    if (expected.edmDomain != SbpSceneCacheEvidence.Domain || actual.edmDomain != expected.edmDomain ||
                        !AndroidVersionHandlerReadOnlyContract.Matches(expected.edmVersionHandler, actual.edmVersionHandler) ||
                        !actual.edmVersionHandler.naturallyComplete ||
                        !HasValidEdmNaturalCompletion(expected.edmVersionHandler, expected.edmDomain, execution.edmNatural)) return false;
                    copy.edmVersionHandler = expected.edmVersionHandler;
                }
                copy.edmScene = expected.edmScene;
            }
            return Util.Json.Serialize(expected) == Util.Json.Serialize(copy);
        }

        /// <summary>纯回执复核要求同域、真实 update、完整首末稳定输入及最终无 pending；旧逐次完整模式与新精确探针计数分别验证。</summary>
        /// <param name="expected">执行前已自然完成的 Version Handler 快照。</param>
        /// <param name="domain">执行前冻结的 Unity 程序集域身份。</param>
        /// <param name="receipt">旧模式每次 update 对应完整快照；新模式每次 update 对应一次成功探针且仅保留首末两份完整快照。</param>
        /// <returns>所有观察稳定、同域且最终完成时为 true，不读取或推进队列。</returns>
        internal static bool HasValidEdmNaturalCompletion(AndroidVersionHandlerReadOnlyContract.Snapshot expected,
            string domain, EdmNaturalCompletionReceipt receipt)
        {
            return expected != null && expected.naturallyComplete && !string.IsNullOrEmpty(domain) && receipt?.sameDomain == true &&
                receipt.beforeDomain == domain && receipt.afterDomain == domain && receipt.error == null &&
                receipt.naturalUpdates > 0 && receipt.naturalUpdates < 512 && receipt.observations != null &&
                receipt.schedulerProbeObservations >= 0 &&
                (receipt.schedulerProbeObservations == 0
                    ? receipt.observations.Length == receipt.naturalUpdates + 1
                    : receipt.schedulerProbeObservations == receipt.naturalUpdates && receipt.observations.Length == 2) &&
                receipt.observations.All(item => AndroidVersionHandlerReadOnlyContract.Matches(expected, item)) &&
                receipt.observations.Last().naturallyComplete;
        }

        /// <summary>读取唯一截止点前的剩余自然等待；到期只结束等待，交给唯一最终完整快照裁决，不重开窗口。</summary>
        /// <param name="deadline">首次完整采集与探针绑定后固定的单调时钟截止点。</param>
        /// <param name="observedAt">本次廉价只读队列探针结束的单调时钟秒数。</param>
        /// <param name="token">只取消本轮观察，取消优先于任何后续等待。</param>
        /// <param name="remainingMilliseconds">未到期时的剩余毫秒；到期为零。</param>
        /// <returns>尚有原预算可等待时为 true；到期为 false，不增加真实 update 计数。</returns>
        internal static bool TryGetEdmNaturalWaitRemaining(double deadline, double observedAt, CancellationToken token,
            out int remainingMilliseconds)
        {
            token.ThrowIfCancellationRequested();
            double remaining = deadline - observedAt;
            remainingMilliseconds = remaining <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(remaining * 1000));
            return remaining > 0;
        }

        /// <summary>首份完整快照之后仅启动一次十秒等待预算；真实 update 后完整采集已经完成时优先验收，仍 pending 越期则失败。</summary>
        /// <param name="naturallyComplete">刚通过完整身份与稳定输入复核的本次快照是否自然完成。</param>
        /// <param name="naturalUpdates">本 scope 已真实观察到的 Editor update 数；零次不能完成。</param>
        /// <param name="observedAt">该完整快照采集完成后的单调时钟秒数，不是开始采集的时刻。</param>
        /// <param name="deadline">首次为 null，随后保留同一个截止时刻，不因 pending 或慢采集重开预算。</param>
        /// <param name="token">只取消本次观察，取消优先于完成判定，不操作厂商队列。</param>
        /// <param name="remainingMilliseconds">尚需等待时返回固定截止点前的剩余毫秒；完成时为零。</param>
        /// <returns>至少一次真实 update 后快照已自然完成时为 true；否则为 false 继续等，超时或取消则抛异常。</returns>
        internal static bool TryCompleteEdmNaturalObservation(bool naturallyComplete, int naturalUpdates, double observedAt,
            ref double? deadline, CancellationToken token, out int remainingMilliseconds)
        {
            token.ThrowIfCancellationRequested();
            remainingMilliseconds = 0;
            if (!deadline.HasValue) deadline = observedAt + 10;
            if (naturallyComplete && naturalUpdates > 0) return true;
            double remaining = deadline.Value - observedAt;
            if (remaining <= 0)
                throw new InvalidOperationException("EDM 正规任务未在十秒观察窗内自然完成，保留 partial。");
            remainingMilliseconds = Math.Max(1, (int)Math.Ceiling(remaining * 1000));
            return false;
        }

        /// <summary>首次场景须有本次有序无需解析日志；计数不增长仅接纳同域完整本次缓存 trace 与输入完全保持。</summary>
        /// <param name="receipt">本次真实场景事件、游标和可选缓存证据；不完整或矛盾时抛异常。</param>
        internal static void ValidateEdmSceneExecution(EdmSceneExecutionReceipt receipt)
        {
            if (receipt?.cacheEvidence != null)
            {
                SbpSceneCacheEvidence.Verify(receipt.cacheEvidence, CancellationToken.None);
                if (receipt.before == null || receipt.after == null || receipt.events?.Length != 0 || receipt.failureKinds?.Length != 0 ||
                    receipt.before.scenesProcessed != receipt.after.scenesProcessed ||
                    !AndroidResolveActionCommon.SceneResolutionSnapshotsMatch(receipt.before, receipt.after))
                    throw new InvalidOperationException("缓存零回调支路仍需相同游标、零真实事件及全量完全相同输入。");
                return;
            }
            if (receipt?.before == null || receipt.after == null || receipt.events == null || receipt.failureKinds == null ||
                receipt.failureKinds.Length != 0 || !AndroidResolveActionCommon.SceneResolutionInputsMatchAfter(receipt.before, receipt.after) ||
                receipt.before.scenesProcessed < 0 || receipt.after.scenesProcessed <= receipt.before.scenesProcessed)
                throw new InvalidOperationException("EDM 场景缺少真实游标增长，或完整输入、工具、日志、调度及产物未保持。");
            string[] expected = receipt.before.scenesProcessed == 0
                ? new[] { "starting", "checking", "skipped", "succeeded" } : Array.Empty<string>();
            if (!receipt.events.SequenceEqual(expected))
                throw new InvalidOperationException("EDM 首次场景缺少本次有序无需解析/成功证据，或后续场景出现额外解析事件。");
        }

        /// <summary>只读核对执行前的源资产与场景 setup；失败凭据也明确给出恢复核查结果，不保存或修复资产。</summary>
        internal static void RecordPreservedSceneState(ArtifactReceipt receipt, CancellationToken cancellationToken)
        {
            if (receipt.bundleWrites?.bundleType != (int)EBundleType.AssetBundle) return;
            string[] actualSetup = EditorSceneManager.GetSceneManagerSetup().Select(scene => scene.path + "|" + scene.isLoaded + "|" + scene.isActive).ToArray();
            bool filesMatch = GenerateActionCommon.TryVerifyArtifacts(receipt.bundleWrites.preservedSceneInputs, cancellationToken, out _, out string error);
            bool setupMatches = (receipt.bundleWrites.sceneSetup ?? Array.Empty<string>()).SequenceEqual(actualSetup);
            bool scenesClean = EditorSceneManager.GetSceneManagerSetup().Where(scene => scene.isLoaded)
                .All(scene => !string.IsNullOrEmpty(scene.path) && !UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty);
            bool persistentAssetsClean = TryValidateNoDirtyPersistentAssets(out string dirtyError);
            receipt.preservedSceneInputsUnchanged = filesMatch && setupMatches && scenesClean && persistentAssetsClean;
            receipt.preservedSceneInputError = error ?? (setupMatches ? null : "SBP 未恢复执行前的场景 setup。") ??
                (scenesClean ? null : "执行后场景含未保存数据。") ?? dirtyError;
        }

        /// <summary>记录共享 OutputCache、正式脚本输出和可选依赖数据库；异步共享 SBP 缓存不作完整性承诺。</summary>
        internal static GenerateActionCommon.Artifact[] CaptureBundleAuxiliaryArtifacts(BundleWriteSnapshot snapshot, CancellationToken cancellationToken)
        {
            var artifacts = new List<GenerateActionCommon.Artifact>
            {
                GenerateActionCommon.CaptureDirectory(snapshot.pipelineOutputPath, "*", cancellationToken),
            };
            if (!string.IsNullOrEmpty(snapshot.scriptOutputPath)) artifacts.Add(GenerateActionCommon.CaptureDirectory(snapshot.scriptOutputPath, "*", cancellationToken));
            if (!string.IsNullOrEmpty(snapshot.dependencyDatabasePath)) artifacts.Add(GenerateActionCommon.CaptureFile(snapshot.dependencyDatabasePath, cancellationToken));
            string leftover = (snapshot.temporaryWritePaths ?? Array.Empty<string>()).FirstOrDefault(path => File.Exists(path) || Directory.Exists(path));
            if (leftover != null) throw new InvalidOperationException("SBP 临时输出尚未清理：" + leftover);
            return artifacts.ToArray();
        }

        /// <summary>
        /// 发现 Player 或 SBP 回调并冻结编译身份；未知包、业务回调及自定义委托阻断。
        /// 执行后和 Verify 禁止公开 XML 预读，以当前完整只读快照核对真实执行凭据。
        /// </summary>
        /// <param name="playerBuild">是否冻结 Player 专属回调；SBP 仅冻结实际相关类型。</param>
        /// <param name="cancellationToken">只读捕获取消令牌。</param>
        /// <param name="readXmlDependencies">仅 Plan/Execute 前允许正规公开 XML 预读；执行后及 Verify 必须为 false。</param>
        /// <returns>含逐项身份、稳定输入和明确不支持项的完整回调闭包。</returns>
        internal static BuildCallbackSnapshot CaptureBuildCallbacks(bool playerBuild, CancellationToken cancellationToken, bool readXmlDependencies = true)
        {
            var identities = new List<string>();
            var unsupported = new List<string>();
            var knownEffects = new List<string>();
            FirebaseCrashlyticsSnapshot firebase = null;
            AndroidResolveActionCommon.SceneResolutionSnapshot edmScene = null;
            AndroidVersionHandlerReadOnlyContract.Snapshot edmVersion = null;
            var kinds = new HashSet<string>(StringComparer.Ordinal)
            {
                "UnityEditor.Build.IProcessScene", "UnityEditor.Build.IProcessSceneWithReport",
                "UnityEditor.Build.IPreprocessShaders", "UnityEditor.Build.IPreprocessComputeShaders",
            };
            foreach (Type type in TypeCache.GetTypesDerivedFrom<UnityEditor.Build.IOrderedCallback>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters) continue;
                if (type.FullName == "UnityEditor.Build.BuildPipelineInterfaces+AttributeCallbackWrapper" &&
                    IsVerifiedEngineAssembly(type, "UnityEditor.CoreModule.dll", "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", cancellationToken)) continue;
                // 已核五个类型只有 Target 切换职责；当前 Action 不切换 Target，不把它们列为 Player 构建回调。
                if (playerBuild && IsVerifiedTargetChangeOnlyCallback(type, cancellationToken)) continue;
                if (!playerBuild && !type.GetInterfaces().Any(contract => kinds.Contains(contract.FullName))) continue;
                AddBuildCallbackIdentity(type, type.FullName, playerBuild, identities, unsupported, knownEffects, cancellationToken);
            }
            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<UnityEditor.Callbacks.PostProcessSceneAttribute>())
                if (method.IsDefined(typeof(UnityEditor.Callbacks.PostProcessSceneAttribute), false) && HasBuildAttributeSignature(method, Type.EmptyTypes))
                {
                    if (!playerBuild && method.DeclaringType?.FullName == "Firebase.Crashlytics.Editor.AndroidPreBuild" && method.Name == "UpdateResourceFiles")
                    {
                        Type type = method.DeclaringType;
                        identities.Add(type.FullName + "." + method.Name + " [PostProcessScene]|" + type.Assembly.FullName + "|" + type.Module.ModuleVersionId);
                        if (!TryCaptureFirebaseCrashlytics(type, identities, cancellationToken, out firebase, out string firebaseError))
                            unsupported.Add(type.FullName + ": " + firebaseError);
                        else knownEffects.Add("Firebase Crashlytics: normal Android callback writes four declared resources, may normalize exact EDM metadata labels on first Measurement initialization, then Refresh; Report(string,string) has no network send in the verified DLL");
                        continue;
                    }
                    if (!playerBuild && method.DeclaringType?.FullName == "GooglePlayServices.PlayServicesResolver" && method.Name == "OnPostProcessScene")
                    {
                        Type type = method.DeclaringType;
                        identities.Add(type.FullName + "." + method.Name + " [PostProcessScene]|" + type.Assembly.FullName + "|" + type.Module.ModuleVersionId);
                        string edmError = "EDM 场景解析程序集身份不匹配。";
                        if (!IsVerifiedEdmAssembly(type, "Google.JarResolver.dll", "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0", cancellationToken))
                            unsupported.Add(type.FullName + ": " + edmError);
                        else if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                        {
                            identities.Add("EDM.scene.activeTarget=" + EditorUserBuildSettings.activeBuildTarget);
                            identities.Add("EDM.scene.resolverIsNull=False");
                            identities.Add("EDM.scene.branch=android-state-match-no-resolution");
                            if (!AndroidResolveActionCommon.TryCaptureSceneResolutionInputs(cancellationToken, out edmScene, out edmError, readXmlDependencies))
                                unsupported.Add(type.FullName + ": " + edmError);
                            else
                            {
                                if (!AndroidVersionHandlerReadOnlyContract.TryCapture(cancellationToken, out edmVersion, out string versionError) ||
                                    !edmVersion.naturallyComplete)
                                    unsupported.Add(type.FullName + ": 正规 Version Handler 尚未完成或缺少合同：" + versionError);
                                knownEffects.Add("EDM Android scene: normal callback may read Java version and poll the verified main-thread queue; first scene requires actual no-change/success evidence before accepting no resolution");
                                if (edmScene.settingsPersistence != null) knownEffects.Add("EDM normal Jetifier preflight: writes exact ProjectSettings/GvhProjectSettings.xml with frozen canonical SHA and materializes effective True only to Unity EditorPrefs:GooglePlayServices.UseJetifier; other entries/preferences remain frozen; VCS checkout is disabled");
                            }
                        }
                        else if (!TryCaptureEdmSceneNoOp(type, identities, out edmError)) unsupported.Add(type.FullName + ": " + edmError);
                        continue;
                    }
                    AddBuildCallbackIdentity(method.DeclaringType, method.DeclaringType?.FullName + "." + method.Name + " [PostProcessScene]", playerBuild, identities, unsupported, knownEffects, cancellationToken);
                }
            if (playerBuild)
            {
                foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<UnityEditor.Callbacks.PostProcessBuildAttribute>())
                    if (method.IsDefined(typeof(UnityEditor.Callbacks.PostProcessBuildAttribute), false) && HasBuildAttributeSignature(method, new[] { typeof(BuildTarget), typeof(string) }))
                        AddBuildCallbackIdentity(method.DeclaringType, method.DeclaringType?.FullName + "." + method.Name + " [PostProcessBuild]", true, identities, unsupported, knownEffects, cancellationToken);
                // Nova 会实例化所有具体 SDK 处理器；不能因某个当前平台方法为空就假定构造函数无副作用。
                foreach (Type type in TypeCache.GetTypesDerivedFrom<NovaSDKBuildProcessor>())
                    if (!type.IsAbstract && !type.ContainsGenericParameters)
                        AddBuildCallbackIdentity(type, type.FullName + " [NovaSDKBuildProcessor]", true, identities, unsupported, knownEffects, cancellationToken);
                // BuildPlayerProcessor 本身实现 IOrderedCallback，已在上方唯一类型循环发现；不重复登记第二种标签。
            }
            else
            {
                UnityEditor.Build.Pipeline.BuildCallbacks callbacks = UnityEditor.Build.Pipeline.ContentPipeline.BuildCallbacks;
                if (callbacks == null || callbacks.GetType() != typeof(UnityEditor.Build.Pipeline.BuildCallbacks))
                    unsupported.Add("ContentPipeline.BuildCallbacks 不是正式默认类型");
                else
                {
                    Delegate[] delegates = { callbacks.PostScriptsCallbacks, callbacks.PostDependencyCallback, callbacks.PostPackingCallback, callbacks.PostWritingCallback };
                    foreach (Delegate callback in delegates.Where(value => value != null))
                        foreach (Delegate entry in callback.GetInvocationList())
                        {
                            string id = entry.Method.DeclaringType?.FullName + "." + entry.Method.Name + " [SBP delegate]";
                            identities.Add(id + "|" + entry.Method.Module.ModuleVersionId);
                            unsupported.Add(id);
                        }
                }
            }
            return new BuildCallbackSnapshot
            {
                identities = identities.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                unsupported = unsupported.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                knownEffects = knownEffects.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                firebaseCrashlytics = firebase,
                edmScene = edmScene,
                edmVersionHandler = edmVersion,
                edmDomain = edmScene == null ? null : SbpSceneCacheEvidence.Domain,
            };
        }

        /// <summary>只排除本轮 metadata 已核的五个 Target 切换专属类型；身份或接口形状变化时仍回到未知回调审查。</summary>
        /// <param name="type">当前发现的具体回调类型，仅核对五个已审 Target 切换专属身份。</param>
        /// <param name="token">读取程序集指纹时使用的本次操作取消令牌。</param>
        /// <returns>仅当类型职责、精确程序集和已审身份均吻合时返回 true；其余返回 false，由常规回调审查继续处理。</returns>
        private static bool IsVerifiedTargetChangeOnlyCallback(Type type, CancellationToken token)
        {
            if (type.BaseType != typeof(object) || type.TypeInitializer != null ||
                type.GetInterfaces().Any(contract => contract.FullName != "UnityEditor.Build.IOrderedCallback" &&
                    contract.FullName != "UnityEditor.Build.IActiveBuildTargetChanged")) return false;
            switch (type.FullName)
            {
                case "UnityEditor.CodeEditorProjectSync+BuildTargetChangedHandler":
                case "UnityEditor.GameViewSizes+BuildTargetChangedHandler":
                case "UnityEditor.Modules.ModuleManager+BuildTargetChangedHandler":
                    return IsVerifiedEngineAssembly(type, "UnityEditor.CoreModule.dll", "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", token);
                case "UnityEditor.Build.Profile.ActiveBuildTargetListener":
                    return IsVerifiedEngineAssembly(type, "UnityEditor.BuildProfileModule.dll", "f097c7d64c7f245bb96898ba007a77500899e5cd04f8c237ae86e2c395c54dbc", token);
                case "Unity.VisualScripting.EditorPlatformWatcher":
                    UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly);
                    return package?.name == "com.unity.visualscripting" &&
                        Path.GetFileName(type.Assembly.Location) == "Unity.VisualScripting.Core.Editor.dll" &&
                        ComputeFileHash(type.Assembly.Location, token) == "0974b584ca7d365bb21ef4e914ff65a418c0456c64aa1585753a369a638e4658";
                default: return false;
            }
        }

        /// <summary>匹配当前引擎 ValidateMethod 的静态、非泛型和精确参数规则；引擎未校验返回类型，不额外改变它的语义。</summary>
        internal static bool HasBuildAttributeSignature(MethodInfo method, Type[] expectedArguments)
        {
            if (method == null || !method.IsStatic || method.IsGenericMethod || method.IsGenericMethodDefinition) return false;
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == expectedArguments.Length && parameters.Select(parameter => parameter.ParameterType).SequenceEqual(expectedArguments);
        }

        /// <summary>记录精确程序集及模块身份；只有 Editor 安装内回调和已核实的本仓只读门禁可默认接受。</summary>
        /// <param name="type">发现的真实回调所属类型；无法解析时记录不支持项。</param>
        /// <param name="label">本次发现入口的准确标签。</param>
        /// <param name="playerBuild">是否审查 Player/Generate 回调；SBP 使用独立的精确已知分支。</param>
        /// <param name="identities">接收程序集、模块与入口身份的当前闭包集合。</param>
        /// <param name="unsupported">接收身份或副作用尚无完整合同的明确拒绝项。</param>
        /// <param name="knownEffects">接收已审统计和内存效果的说明，不改变 Analytics 设置。</param>
        /// <param name="cancellationToken">只读核验程序集或包源码指纹的取消令牌。</param>
        private static void AddBuildCallbackIdentity(Type type, string label, bool playerBuild, ICollection<string> identities, ICollection<string> unsupported,
            ICollection<string> knownEffects, CancellationToken cancellationToken)
        {
            if (type == null) { unsupported.Add(label ?? "无法解析回调所属类型"); return; }
            identities.Add(label + "|" + type.Assembly.FullName + "|" + type.Module.ModuleVersionId);
            if (type.FullName == "UnityEditor.AdaptivePerformance.Editor.Analytics.Hooks.BuildHook" &&
                IsVerifiedEngineAssembly(type, "UnityEditor.AdaptivePerformanceModule.dll", "7988058106626eccc3acb284e4e89a27ce3936a80439c8cf9d0a40fd451aa6db", cancellationToken))
                knownEffects.Add("AdaptivePerformance: when report!=null and EditorAnalytics.enabled, SendAnalytic sends buildGuid/targetPlatform/platformVersion/installedProviders to Unity Editor Analytics; delivery is not proven by Bundle output");
            bool readOnlyGuard = playerBuild && type.Assembly == typeof(EditorUtil).Assembly &&
                (type == typeof(ResourceKeyBuildPreprocessor) || type == typeof(PrivacyAesMaintenanceGuard) ||
                 type == typeof(HybridClrStripGradleScope));
            if (readOnlyGuard) return;
            string error = null;
            if (!playerBuild && TryCaptureKnownSbpCallback(type, identities, cancellationToken, out error))
            {
                if (type.FullName == "Unity.ProjectAuditor.Editor.Modules.ShadersModule" || type.FullName == "UnityEditor.Timeline.Analytics.TimelineAnalytics+TimelineAnalyticsProcess" || type.FullName == "UnityEditor.UI.Analytics.UGUIBuildAnalytics")
                    knownEffects.Add(label + ": current SBP branch may accumulate in-memory build/variant statistics; no state reset is performed");
                return;
            }
            unsupported.Add(label + (string.IsNullOrEmpty(error) ? string.Empty : ": " + error));
        }

        /// <summary>核对当前安装引擎的精确文件与 Hash，不能把任意 com.unity 包或安装目录内容默认放行。</summary>
        private static bool IsVerifiedEngineAssembly(Type type, string fileName, string sha256, CancellationToken cancellationToken)
        {
            string path = Path.GetFullPath(Path.Combine(EditorApplication.applicationContentsPath, "Resources/Scripting/Managed/UnityEngine", fileName));
            return Path.GetFullPath(type.Assembly.Location) == path && File.Exists(path) &&
                string.Equals(ComputeFileHash(path, cancellationToken), sha256, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>只读冻结已审 Firebase 回调；不读取会触发 Measurement 初始化的静态实例，不更改同意或统计设置。</summary>
        private static bool TryCaptureFirebaseCrashlytics(Type type, ICollection<string> identities, CancellationToken token,
            out FirebaseCrashlyticsSnapshot snapshot, out string error)
        {
            snapshot = null;
            if (!IsVerifiedFirebaseAssembly(type, "Firebase.Crashlytics.Editor.dll", "5a23d6ae59c11fd1a2ddfee94087dec26eec054d3618fad4ea9e7c1dfeae1e18", token))
            { error = "Firebase Crashlytics 回调 DLL 或包身份不匹配。"; return false; }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            { identities.Add("Firebase.Crashlytics=non-android-noop"); error = null; return true; }
            if (EditorApplication.isPlaying)
            { error = "Firebase 构建回调不能在播放模式执行。"; return false; }
            FieldInfo generated = type.GetField("Generated", BindingFlags.NonPublic | BindingFlags.Static);
            Type measurement = type.Assembly.GetType("Firebase.Editor.Measurement", false) ??
                AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("Firebase.Editor.Measurement", false)).SingleOrDefault(value => value != null);
            if (type.TypeInitializer != null || generated?.FieldType != typeof(bool) || measurement == null ||
                !IsVerifiedFirebaseAssembly(measurement, "Firebase.Editor.dll", "b50eefa93d23c673bc8a97266e3d36dcfba0e9d3e49f5e7cc7850d595c12f1bf", token))
            { error = "Firebase 固定字段或 Measurement 编译身份已变化。"; return false; }
            Type projectSettings = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("Google.ProjectSettings", false)).SingleOrDefault(value => value != null);
            if (projectSettings == null || !IsVerifiedEdmAssembly(projectSettings, "Google.VersionHandlerImpl.dll", "c3a6d0419b41b0cbc7ad518b0cc2b311af043b5d71ed709ea3baa576b79862ce", token))
            { error = "Firebase 使用的 EDM 设置实现身份不匹配。"; return false; }
            if (!TryCaptureFirebaseVersionHandlerLogger(projectSettings.Assembly, out FirebaseLoggerState logger, out error)) return false;
            // ProjectSettings 的已审 cctor 只建立内存设置；Measurement 的 cctor 会改标签，绝不能在 Plan 读取它的 analytics 字段。
            object project = projectSettings.GetField("projectSettings", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            object system = projectSettings.GetField("systemSettings", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            object loaded = projectSettings.GetField("loadedSettings", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            bool useProject = EditorPrefs.GetBool("Google.Firebase.UseProjectSettings", true);
            if (project?.GetType() != projectSettings.Assembly.GetType("Google.InMemorySettings", false) ||
                system?.GetType() != projectSettings.Assembly.GetType("Google.EditorSettings", false) || useProject && !ReferenceEquals(loaded, project))
            { error = "Firebase 设置包含自定义实现或项目缓存尚未加载，当前只读设置契约不足。"; return false; }
            var values = project.GetType().GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(project) as SortedDictionary<string, string>;
            if (values == null) { error = "Firebase 设置字典身份已变化。"; return false; }
            bool consent = EditorPrefs.GetBool("com.google.firebaseAnalyticsConsentRequested", false);
            if (!consent) { error = "Firebase 尚未完成统计同意选择；正常回调会弹出用户选择窗口，需先完成该选择。"; return false; }
            bool enabled = useProject ? values.TryGetValue("com.google.firebaseAnalyticsEnabled", out string raw) && bool.TryParse(raw, out bool parsed) ? parsed : true
                : EditorPrefs.GetBool("com.google.firebaseAnalyticsEnabled", true);
            identities.Add("Firebase.Measurement.UseProjectSettings=" + useProject);
            identities.Add("Firebase.Measurement.Enabled=" + enabled);
            identities.Add("Firebase.Measurement.ConsentRequested=" + consent);
            identities.Add("Firebase.Measurement.DLL=" + measurement.Assembly.FullName + "|" + measurement.Module.ModuleVersionId);
            try
            {
                string[] roots = FindFirebaseMetadataRoots();
                MetadataLabelInput[] metadata = CaptureFirebaseMetadata(roots, token);
                string resourceRoot = "Assets/Plugins/Android/FirebaseCrashlytics.androidlib";
                string[] resourcePaths = new[] { resourceRoot + "/AndroidManifest.xml", resourceRoot + "/project.properties",
                    resourceRoot + "/res/values/crashlytics_build_id.xml", resourceRoot + "/res/values/crashlytics_unity_version.xml" };
                string[] directoryPaths = new[] { "Assets/Plugins", "Assets/Plugins/Android", resourceRoot, resourceRoot + "/res", resourceRoot + "/res/values" };
                var resources = resourcePaths.Select(path => CaptureCallbackFile(path, false, token)).ToList();
                resources.AddRange(directoryPaths.Select(path => CaptureCallbackFile(path, true, token)));
                resources.AddRange(resourcePaths.Concat(directoryPaths).Select(path => CaptureCallbackFile(path + ".meta", false, token)));
                snapshot = new FirebaseCrashlyticsSnapshot
                {
                    generated = (bool)generated.GetValue(null), unityVersion = Application.unityVersion,
                    metadataRoots = roots, metadata = metadata, resources = resources.ToArray(),
                    versionHandlerLogger = logger,
                    preservedFiles = new[] { CaptureCallbackFile("ProjectSettings/GvhProjectSettings.xml", false, token) },
                };
                error = null;
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "Firebase 精确输入冻结失败：" + exception.Message; return false; }
        }

        /// <summary>仅读取已由 Editor 初始化的正规日志实例字段，不调用日志方法，不修改设置或订阅委托。</summary>
        private static bool TryCaptureFirebaseVersionHandlerLogger(Assembly assembly, out FirebaseLoggerState snapshot, out string error)
        {
            snapshot = null;
            Type handler = assembly.GetType("Google.VersionHandlerImpl", false);
            Type loggerType = assembly.GetType("Google.Logger", false);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            object logger = handler?.GetField("logger", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            FieldInfo target = loggerType?.GetField("<Target>k__BackingField", flags);
            FieldInfo level = loggerType?.GetField("<Level>k__BackingField", flags);
            FieldInfo filename = loggerType?.GetField("<LogFilename>k__BackingField", flags);
            FieldInfo callback = loggerType?.GetField("LogMessage", flags);
            if (logger == null || logger.GetType() != loggerType || target == null || level == null ||
                filename?.FieldType != typeof(string) || callback == null || !typeof(Delegate).IsAssignableFrom(callback.FieldType))
            { error = "Firebase VersionHandler 日志实例或字段身份已变化。"; return false; }
            int targetValue = Convert.ToInt32(target.GetValue(logger), CultureInfo.InvariantCulture);
            int levelValue = Convert.ToInt32(level.GetValue(logger), CultureInfo.InvariantCulture);
            if (!TryValidateFirebaseLoggerState(targetValue, levelValue, (string)filename.GetValue(logger),
                    callback.GetValue(logger) != null, out error)) return false;
            snapshot = new FirebaseLoggerState { target = targetValue, level = levelValue };
            return true;
        }

        /// <summary>仅接纳已审 Console 与空文件分支；空字符串文件名也会尝试写盘，必须拒绝。</summary>
        internal static bool TryValidateFirebaseLoggerState(int target, int level, string filename, bool hasDelegate, out string error)
        {
            if (target != 6 || level < 0 || level > 4 || filename != null || hasDelegate)
            { error = "Firebase VersionHandler 日志存在未冻结的文件、外部委托或输出分支。"; return false; }
            error = null;
            return true;
        }

        /// <summary>核对正式 Firebase 包与指定 Editor DLL，不以名字相同作为放行依据。</summary>
        private static bool IsVerifiedFirebaseAssembly(Type type, string fileName, string hash, CancellationToken token)
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly);
            string path = package == null ? null : Path.GetFullPath(Path.Combine(package.resolvedPath, "Firebase/Editor", fileName));
            return package?.name == "com.solotopia.nova.framework.sdk.firebase" && package.version == "0.1.19" &&
                Path.GetFullPath(type.Assembly.Location) == path && File.Exists(path) && ComputeFileHash(path, token) == hash;
        }

        /// <summary>忠实镜像 FindAllAssets 的标签查询与原路径存在性筛选，不调用会创建 FileMetadata 的 vendor API。</summary>
        private static string[] FindFirebaseMetadataRoots()
        {
            return AssetDatabase.FindAssets("l:gvh").Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => File.Exists(path) || Directory.Exists(path)).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        /// <summary>冻结初始 metadata 与所有可能读取的 manifest 非空行；行路径不重定位，不递归打开行引用的 manifest。</summary>
        private static MetadataLabelInput[] CaptureFirebaseMetadata(string[] roots, CancellationToken token)
        {
            var inputs = new Dictionary<string, MetadataLabelInput>(StringComparer.Ordinal);
            foreach (string path in roots) inputs.Add(path, CaptureFirebaseMetadataInput(path, token));
            foreach (MetadataLabelInput manifest in inputs.Values.Where(input => input.manifest && input.source.kind == "file").ToArray())
                foreach (string rawLine in File.ReadLines(manifest.source.path))
                {
                    string path = rawLine.Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                    if (path.Length == 0 || inputs.ContainsKey(path)) continue;
                    inputs.Add(path, CaptureFirebaseMetadataInput(path, token));
                }
            return inputs.Values.OrderBy(input => input.path, StringComparer.Ordinal).ToArray();
        }

        /// <summary>只读捕获一个 metadata 候选；可写候选必须在 Assets 内，包候选保持真实文件与标签完全不变。</summary>
        private static MetadataLabelInput CaptureFirebaseMetadataInput(string path, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Split('/').Any(part => part == ".." || part == "."))
                throw new InvalidOperationException("Metadata 路径不是已审项目相对路径：" + path);
            bool readOnly = Regex.IsMatch(path, @"^(Packages[/\\][^/\\]+)[/\\](.*)?$") || Regex.IsMatch(path, @"^(Library[/\\]PackageCache[/\\])([^/\\]+)(@[^/\\]+)?[/\\](.*)?$");
            if (!readOnly && !path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("Metadata 可写路径必须位于 Assets：" + path);
            string physical = FileUtil.GetPhysicalPath(path);
            if (string.IsNullOrEmpty(physical)) physical = path;
            bool folder = Directory.Exists(physical);
            AssetImporter importer = AssetImporter.GetAtPath(path);
            string[] labels = importer == null ? Array.Empty<string>() : AssetDatabase.GetLabels(importer);
            string[] expected = labels;
            bool manifest = IsFirebaseManifestCandidate(path, labels);
            if (!readOnly && importer != null && !TryPredictFirebaseMetadataLabels(path, labels, importer is PluginImporter, out expected, out manifest, out string error))
                throw new InvalidOperationException(path + ": " + error);
            CallbackFileState meta = CaptureCallbackFile(physical + ".meta", false, token);
            string nonLabelsHash = null;
            if (!readOnly && meta.kind == "file")
            {
                if (!TryHashMetadataWithoutLabels(File.ReadAllBytes(meta.path), out nonLabelsHash, out string metaError)) throw new InvalidOperationException(path + ": " + metaError);
            }
            return new MetadataLabelInput
            {
                path = path, guid = AssetDatabase.AssetPathToGUID(path), importerType = importer?.GetType().AssemblyQualifiedName,
                labels = labels, expectedLabels = expected, readOnly = readOnly, manifest = manifest,
                source = CaptureCallbackFile(physical, folder, token), meta = meta, metaWithoutLabelsSha256 = nonLabelsHash,
            };
        }

        /// <summary>识别可能作为 manifest 的原文件 token 与标签，保守冻结全部候选而不提前执行归并。</summary>
        private static bool IsFirebaseManifestCandidate(string path, string[] labels)
        {
            return Path.GetFileNameWithoutExtension(path).Split('_').Skip(1).Any(value => value.ToLower().StartsWith("manifest")) ||
                labels.Any(value => value.ToLower().StartsWith("gvh_manifest") || value.ToLower().StartsWith("gvhp_manifest"));
        }

        /// <summary>捕获精确可缺失文件/目录并拒绝类型冲突与软链；目录不伪造完整内容 Hash。</summary>
        private static CallbackFileState CaptureCallbackFile(string path, bool directory, CancellationToken token)
        {
            if (!GenerateActionCommon.TryResolveProjectPath(path, "Callback input", out string full, out string error)) throw new InvalidOperationException(error);
            bool fileExists = File.Exists(full), directoryExists = Directory.Exists(full);
            if (directory && fileExists || !directory && directoryExists) throw new InvalidOperationException("回调路径存在文件/目录类型冲突：" + full);
            return new CallbackFileState { path = full, kind = directoryExists ? "directory" : fileExists ? "file" : "absent", sha256 = fileExists ? ComputeFileHash(full, token) : null };
        }

        /// <summary>已审 FileMetadata 的纯数据模型；不实例化 vendor 类型或触发任何资产标签写入。</summary>
        private sealed class FirebaseMetadataValue
        {
            public string version;
            public string linux;
            public string export;
            public bool manifest;
            public readonly HashSet<string> targets = new HashSet<string>();
            public readonly HashSet<string> dotnet = new HashSet<string>();
            public readonly SortedList<int, string> names = new SortedList<int, string>();
        }

        /// <summary>按 filename、原始标签顺序、export filename 的原顺序预测正规标签；未知受管 token 明确拒绝，不调用私有 parser。</summary>
        internal static bool TryPredictFirebaseMetadataLabels(string path, string[] labels, bool pluginImporter,
            out string[] expected, out bool manifest, out string error)
        {
            expected = null;
            manifest = false;
            try
            {
                var data = new FirebaseMetadataValue();
                ParseFirebaseFilename(path, data);
                foreach (string label in labels)
                {
                    string lower = label.ToLower();
                    if (lower.StartsWith("gvhp_") || lower.StartsWith("gvh_"))
                    {
                        string token = label.Substring(lower.StartsWith("gvhp_") ? 5 : 4);
                        if (lower == "gvh_rename_to_disable") continue;
                        if (!ParseFirebaseMetadataToken(token, data)) throw new InvalidOperationException("存在未审受管标签：" + label);
                    }
                }
                if (!string.IsNullOrEmpty(data.export))
                {
                    string export = data.export.Replace('\\', '/');
                    if (export.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) export = export.Substring(7);
                    ParseFirebaseFilename("Assets/" + export, data);
                }
                var old = new HashSet<string>(labels, StringComparer.Ordinal);
                var result = new HashSet<string>(labels.Where(value => !value.ToLower().StartsWith("gvh_") && value.ToLower() != "gvh"), StringComparer.Ordinal) { "gvh" };
                if (!string.IsNullOrEmpty(data.version)) result.Add(CreateFirebaseMetadataLabel("version-", data.version, old, false));
                foreach (string target in data.targets) result.Add(CreateFirebaseMetadataLabel("targets-", target, old, false));
                if (data.targets.Count > 0 && !pluginImporter) result.Add("gvh_rename_to_disable");
                foreach (string target in data.dotnet) result.Add(CreateFirebaseMetadataLabel("dotnet-", target, old, false));
                if (!string.IsNullOrEmpty(data.linux)) result.Add(CreateFirebaseMetadataLabel("linuxlibname-", data.linux, old, false));
                if (!string.IsNullOrEmpty(data.export)) result.Add(CreateFirebaseMetadataLabel("exportpath-", data.export, old, true));
                if (data.manifest) result.Add(CreateFirebaseMetadataLabel("manifest", null, old, false));
                foreach (KeyValuePair<int, string> name in data.names)
                {
                    int index = name.Key + 1073741823;
                    result.Add(CreateFirebaseMetadataLabel("manifestname-", index < 1073741823 ? index.ToString(CultureInfo.CurrentCulture) + name.Value : name.Value, old, true));
                }
                expected = result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
                manifest = data.manifest;
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            { error = "Metadata 标签预测失败：" + exception.Message; return false; }
        }

        /// <summary>只解析 basename 的下划线后 token；保留 .dll.mdb 的已审多段扩展名处理。</summary>
        private static void ParseFirebaseFilename(string path, FirebaseMetadataValue data)
        {
            string file = Path.GetFileName(path);
            string basename = file.EndsWith(".dll.mdb", StringComparison.OrdinalIgnoreCase) ? file.Substring(0, file.Length - 8) : Path.GetFileNameWithoutExtension(file);
            foreach (string token in basename.Split('_').Skip(1)) ParseFirebaseMetadataToken(token, data);
        }

        /// <summary>忠实匹配已审 token 的大小写判定、覆盖与集合累加规则，保持未知 filename token 被忽略。</summary>
        private static bool ParseFirebaseMetadataToken(string token, FirebaseMetadataValue data)
        {
            string lower = token.ToLower();
            if (lower.StartsWith("manifestname-"))
            {
                string value = token.Substring(13);
                Match match = Regex.Match(value, @"^([0-9]+)(.*)");
                int key = 1073741823 - data.names.Count;
                if (match.Success) { if (int.TryParse(match.Groups[1].Value, out int index)) key = index - 1073741823; value = match.Groups[2].Value; }
                data.names.Remove(key); data.names.Add(key, value); data.manifest = true;
                return true;
            }
            if (lower.StartsWith("manifest")) { data.manifest = true; return true; }
            if (lower.StartsWith("dotnet-"))
            {
                string[] values = token.Substring(7).Split('-');
                if (!values.All(value => Regex.IsMatch(value, @"^(3.5|4.5|Legacy)$", RegexOptions.IgnoreCase))) return false;
                data.dotnet.UnionWith(values); return true;
            }
            string targetsPrefix = lower.StartsWith("targets-") ? "targets-" : lower.StartsWith("t") ? "t" : null;
            if (targetsPrefix != null)
            {
                string[] values = token.Substring(targetsPrefix.Length).Split('-');
                const string valid = @"^(editor|osx|osxintel|windows|ios|ps3|xbox360|android|linux32|windows64|webgl|linux64|linux|osxintel64|tizen|psp2|ps4|xboxone|samsungtv|n3rds|nintendo3ds|wiiu|tvos|switch)$";
                if (values.All(value => Regex.IsMatch(value, valid, RegexOptions.IgnoreCase))) { data.targets.UnionWith(values.Select(value => value.ToLower())); return true; }
            }
            string versionPrefix = lower.StartsWith("version-") ? "version-" : lower.StartsWith("v") ? "v" : null;
            if (versionPrefix != null)
            {
                string[] values = token.Substring(versionPrefix.Length).Split('-');
                if (values.All(value => Regex.IsMatch(value, @"^[0-9][0-9.]+$"))) { data.version = values[0]; return true; }
            }
            if (lower.StartsWith("linuxlibname-")) { data.linux = token.Substring(13); return true; }
            if (lower.StartsWith("exportpath-")) { data.export = token.Substring(11).Replace('\\', '/'); return true; }
            return false;
        }

        /// <summary>保留 vendor 对普通旧标签、preserve 旧标签和默认标签形式的精确优先级。</summary>
        private static string CreateFirebaseMetadataLabel(string key, string value, ISet<string> old, bool preserveDefault)
        {
            string normal = "gvh_" + key + value;
            string preserved = "gvhp_" + key + value;
            return old.Contains(normal) ? normal : old.Contains(preserved) ? preserved : preserveDefault ? preserved : normal;
        }

        /// <summary>剥离唯一顶层 labels 块后计算原字节 Hash；只读比较其它 importer 内容，不重写 YAML。</summary>
        internal static bool TryHashMetadataWithoutLabels(byte[] bytes, out string hash, out string error)
        {
            hash = null;
            try
            {
                string value = new UTF8Encoding(false, true).GetString(bytes);
                MatchCollection headers = Regex.Matches(value, @"(?m)^labels:[^\S\r\n]*(?:\[\])?[^\S\r\n]*(?:\r?\n|$)");
                if (headers.Count > 1 || Regex.IsMatch(value, @"(?m)^[ \t]+labels:"))
                { error = "Metadata 含重复或嵌套 labels，不能精确限定写入。"; return false; }
                if (headers.Count == 0 && Regex.IsMatch(value, @"(?m)^labels:"))
                { error = "Metadata labels 使用未审布局，不能精确限定写入。"; return false; }
                if (headers.Count == 1)
                {
                    Match header = headers[0]; int end = header.Index + header.Length;
                    while (end < value.Length)
                    {
                        Match item = Regex.Match(value.Substring(end), @"^(?:[ \t]*-[^\r\n]*(?:\r?\n|$))");
                        if (!item.Success) break;
                        end += item.Length;
                    }
                    value = value.Remove(header.Index, end - header.Index);
                }
                using (SHA256 sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
                error = null; return true;
            }
            catch (DecoderFallbackException exception) { error = "Metadata 不是可验证 UTF-8：" + exception.Message; return false; }
        }

        /// <summary>限定一次领域构建的 Firebase 与 Android 场景证据窗口，所有异常路径解除订阅。</summary>
        internal sealed class CallbackExecutionScope : IDisposable
        {
            private readonly BuildCallbackSnapshot callbacks;
            private readonly List<string> successes = new List<string>();
            private readonly List<string> failures = new List<string>();
            private bool disposed;
            private readonly AndroidResolveActionCommon.ResolutionLogCapture sceneLogs;
            private readonly long startedUtcTicks = DateTime.UtcNow.Ticks;
            private readonly EdmNaturalCompletionReceipt natural = new EdmNaturalCompletionReceipt();
            private Task<CallbackExecutionReceipt> completionTask;
            private CallbackExecutionReceipt completedReceipt;
            private Func<SbpSceneCacheEvidence.Receipt> captureCacheEvidence;

            /// <summary>写前核对并临时订阅自有公开日志观察者；构造失败立即清场，不调用或重置构建回调及日志设置。</summary>
            /// <param name="snapshot">已冻结的当前同域回调合同。</param>
            internal CallbackExecutionScope(BuildCallbackSnapshot snapshot)
            {
                callbacks = snapshot;
                natural.beforeDomain = snapshot?.edmDomain;
                natural.afterDomain = SbpSceneCacheEvidence.Domain;
                natural.sameDomain = natural.beforeDomain == natural.afterDomain;
                natural.observations = Array.Empty<AndroidVersionHandlerReadOnlyContract.Snapshot>();
                try
                {
                    if (snapshot?.edmVersionHandler != null && (!natural.sameDomain || !snapshot.edmVersionHandler.naturallyComplete ||
                        !AndroidVersionHandlerReadOnlyContract.IsSelfConsistent(snapshot.edmVersionHandler)))
                        throw new InvalidOperationException("EDM 场景计划域已变化或执行前 Version Handler 尚未严格完成。");
                    if (callbacks?.edmScene != null)
                    {
                        sceneLogs = new AndroidResolveActionCommon.ResolutionLogCapture(true);
                        sceneLogs.AttachSceneLogger(callbacks.edmScene.logging, CancellationToken.None);
                    }
                    if (callbacks?.firebaseCrashlytics != null) Application.logMessageReceived += CaptureLog;
                }
                catch
                {
                    if (callbacks?.firebaseCrashlytics != null) Application.logMessageReceived -= CaptureLog;
                    sceneLogs?.Dispose();
                    throw;
                }
            }

            /// <summary>只观察已审 Version Handler 的自然 update，最多十秒；未知任务、域变化或取消均保留 partial，不执行或移除队列。</summary>
            /// <param name="outputPath">本次构建独占的输出目录。</param>
            /// <param name="packageName">冻结 Package 名称。</param>
            /// <param name="version">冻结 Package 版本。</param>
            /// <param name="writes">本次冻结写集及依赖闭包。</param>
            /// <param name="token">只取消本次观察，不取消任何厂商任务。</param>
            /// <returns>同一次调用复用同一任务及回执；失败保留真实未完成状态。</returns>
            internal Task<CallbackExecutionReceipt> CompleteAsync(string outputPath, string packageName, string version,
                BundleWriteSnapshot writes, CancellationToken token)
            {
                return completionTask ?? (completionTask = ObserveNaturalCompletionAsync(outputPath, packageName, version, writes, token));
            }

            /// <summary>初末完整采集之间仅核对精确调度探针并等待真实 update；零回调只接受本次 report/trace，不升级旧产物。</summary>
            /// <param name="outputPath">本次独占输出目录。</param>
            /// <param name="packageName">冻结 Package 名称。</param>
            /// <param name="version">冻结版本。</param>
            /// <param name="writes">冻结依赖闭包和正式管线写集。</param>
            /// <param name="token">取消只影响观察，回执仍保留 partial。</param>
            /// <returns>真实自然完成后的只读回执；不重放任何构建或回调。</returns>
            private async Task<CallbackExecutionReceipt> ObserveNaturalCompletionAsync(string outputPath, string packageName, string version,
                BundleWriteSnapshot writes, CancellationToken token)
            {
                if (callbacks?.edmVersionHandler == null) return Complete(token);
                var observations = new List<AndroidVersionHandlerReadOnlyContract.Snapshot>();
                double? deadline = null;
                try
                {
                    for (int captureIndex = 0; captureIndex < 2; captureIndex++)
                    {
                        token.ThrowIfCancellationRequested();
                        natural.afterDomain = SbpSceneCacheEvidence.Domain;
                        natural.sameDomain = natural.beforeDomain == natural.afterDomain;
                        if (!natural.sameDomain) throw new InvalidOperationException("EDM 作用域跨越域重载，不能用当前域补造执行后状态。");
                        if (!AndroidVersionHandlerReadOnlyContract.TryCapture(token, out var current, out string error))
                        {
                            AndroidResolveActionCommon.TryCaptureSceneScheduler(token, out _, out string diagnostic);
                            natural.schedulerDiagnostic = diagnostic;
                            throw new InvalidOperationException(error);
                        }
                        if (!AndroidVersionHandlerReadOnlyContract.Matches(callbacks.edmVersionHandler, current))
                            throw new InvalidOperationException("自然收尾期间 Version Handler 稳定输入已漂移。");
                        observations.Add(current);
                        if (captureIndex == 1)
                        {
                            if (!TryCompleteEdmNaturalObservation(current.naturallyComplete, natural.naturalUpdates,
                                EditorApplication.timeSinceStartup, ref deadline, token, out _))
                                throw new InvalidOperationException("EDM 最终完整快照仍未自然完成，保留 partial，不能重开观察预算。");
                            break;
                        }
                        if (!AndroidVersionHandlerReadOnlyContract.TryCreateSchedulerProbe(current, token, out var probe, out error))
                            throw new InvalidOperationException(error);
                        TryCompleteEdmNaturalObservation(current.naturallyComplete, natural.naturalUpdates,
                            EditorApplication.timeSinceStartup, ref deadline, token, out int remaining);
                        while (await NextEditorUpdate(remaining, token))
                        {
                            natural.naturalUpdates++;
                            natural.afterDomain = SbpSceneCacheEvidence.Domain;
                            natural.sameDomain = natural.beforeDomain == natural.afterDomain;
                            if (!natural.sameDomain) throw new InvalidOperationException("EDM 作用域跨越域重载，不能用当前域补造执行后状态。");
                            if (!probe.TryCapture(token, out bool naturallyComplete, out error))
                                throw new InvalidOperationException(error);
                            natural.schedulerProbeObservations++;
                            if (naturallyComplete || !TryGetEdmNaturalWaitRemaining(deadline.Value,
                                EditorApplication.timeSinceStartup, token, out remaining)) break;
                        }
                    }
                    captureCacheEvidence = () => SbpSceneCacheEvidence.Capture(outputPath, packageName, version, startedUtcTicks, writes, token);
                }
                catch (Exception exception) { natural.error = exception is OperationCanceledException ? "构建后观察已取消，保留 partial。" : exception.Message; }
                natural.observations = observations.ToArray();
                return Complete(CancellationToken.None);
            }

            /// <summary>仅注册自有一次性 update；后台计时器只发送超时信号，解除订阅仍在调用方 Unity 上下文，不消费厂商任务。</summary>
            /// <param name="timeoutMilliseconds">剩余观察预算，超时仅完成自有等待信号。</param>
            /// <param name="token">仅取消自有等待的令牌。</param>
            /// <returns>实际自然 update 到达时为 true，计时器超时为 false，取消则抛取消异常。</returns>
            private static async Task<bool> NextEditorUpdate(int timeoutMilliseconds, CancellationToken token)
            {
                var completion = new TaskCompletionSource<bool>();
                EditorApplication.CallbackFunction callback = null;
                callback = () => { EditorApplication.update -= callback; completion.TrySetResult(true); };
                EditorApplication.update += callback;
                using (var timer = new Timer(_ => completion.TrySetResult(false), null, timeoutMilliseconds, Timeout.Infinite))
                using (token.Register(() => completion.TrySetCanceled()))
                {
                    try { return await completion.Task; }
                    finally { EditorApplication.update -= callback; }
                }
            }

            /// <summary>采集普通日志级别的写失败及两个资源成功行，避免 Console 无 Error 被误当成功。</summary>
            private void CaptureLog(string condition, string trace, LogType type)
            {
                if (condition.StartsWith("Generated resource for com.crashlytics.android.build_id : ", StringComparison.Ordinal) ||
                    condition.StartsWith("Generated resource for com.google.firebase.crashlytics.unity_version : ", StringComparison.Ordinal)) successes.Add(condition);
                if (condition.StartsWith("Could not write ", StringComparison.Ordinal) && condition.Contains("resource file.") ||
                    condition.StartsWith("Failed to generate Firebase Android Plugins folder ", StringComparison.Ordinal) ||
                    condition.StartsWith("Failed to generate Firebase Resources file ", StringComparison.Ordinal) ||
                    condition.StartsWith("Firebase Android Plugins folder should match pattern ", StringComparison.Ordinal)) failures.Add(condition);
            }

            /// <summary>只读记录真实前后状态、标签和场景事件；不调用 ReadAll，失败不能借旧文件升级成功。</summary>
            /// <param name="token">只读收尾取消令牌，已产生的回执不会重复捕获。</param>
            /// <returns>完整或明确未完成的真实回执；无相关合同则为 null。</returns>
            internal CallbackExecutionReceipt Complete(CancellationToken token)
            {
                if (completedReceipt != null) return completedReceipt;
                Dispose();
                if (callbacks?.firebaseCrashlytics == null && callbacks?.edmScene == null) return null;
                var receipt = new CallbackExecutionReceipt
                {
                    before = callbacks.firebaseCrashlytics, successLogs = successes.ToArray(), failureLogs = failures.ToArray(),
                    changedPaths = Array.Empty<string>(),
                    edmScene = callbacks.edmScene == null ? null : new EdmSceneExecutionReceipt
                    { before = callbacks.edmScene, events = sceneLogs.SceneEvents, failureKinds = sceneLogs.FailureKinds },
                    edmNatural = callbacks.edmVersionHandler == null ? null : natural,
                };
                completedReceipt = receipt;
                try
                {
                    if (callbacks.edmVersionHandler != null && !HasValidEdmNaturalCompletion(callbacks.edmVersionHandler, callbacks.edmDomain, natural))
                        throw new InvalidOperationException(natural.error ?? "EDM 缺少同域自然 update 完成证据。");
                    BuildCallbackSnapshot current = CaptureBuildCallbacks(false, token, false);
                    if (callbacks.edmScene != null) receipt.edmScene.after = current.edmScene;
                    if (current.unsupported.Length > 0) throw new InvalidOperationException("执行后回调闭包缺少确定合同：" + string.Join("; ", current.unsupported));
                    if (callbacks.edmVersionHandler != null && (current.edmDomain != natural.beforeDomain ||
                        !AndroidVersionHandlerReadOnlyContract.Matches(callbacks.edmVersionHandler, current.edmVersionHandler) ||
                        !current.edmVersionHandler.naturallyComplete))
                        throw new InvalidOperationException("自然等待后域或 Version Handler 完整稳定输入未保持。");
                    if (captureCacheEvidence != null && receipt.edmScene?.events.Length == 0 &&
                        receipt.edmScene.after?.scenesProcessed == receipt.edmScene.before.scenesProcessed)
                        receipt.edmScene.cacheEvidence = captureCacheEvidence();
                    if (callbacks.firebaseCrashlytics != null)
                    {
                        if (current.firebaseCrashlytics == null) throw new InvalidOperationException("执行后 Firebase 回调合同缺失。");
                        receipt.after = current.firebaseCrashlytics;
                        receipt.changedPaths = GetCallbackChangedPaths(receipt.before, receipt.after);
                        ValidateFirebaseExecution(receipt, token);
                    }
                    if (callbacks.edmScene != null) ValidateEdmSceneExecution(receipt.edmScene);
                    receipt.complete = true;
                }
                catch (Exception exception) when (!(exception is OperationCanceledException)) { receipt.error = exception.Message; }
                return receipt;
            }

            /// <summary>解除本次日志订阅，重复清理不产生副作用。</summary>
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (callbacks?.firebaseCrashlytics != null) Application.logMessageReceived -= CaptureLog;
                sceneLogs?.Dispose();
            }
        }

        /// <summary>创建正常回调作用域，调用方以 using/finally 保证清场。</summary>
        internal static CallbackExecutionScope BeginCallbackExecution(BuildCallbackSnapshot callbacks) => new CallbackExecutionScope(callbacks);

        /// <summary>列出真实文件与标签变化，不从计划写集推断实际已写入。</summary>
        private static string[] GetCallbackChangedPaths(FirebaseCrashlyticsSnapshot before, FirebaseCrashlyticsSnapshot after)
        {
            var changes = new List<string>();
            foreach (CallbackFileState file in after.resources)
                if (Util.Json.Serialize(file) != Util.Json.Serialize(before.resources.Single(input => input.path == file.path))) changes.Add(file.path);
            foreach (MetadataLabelInput input in after.metadata)
            {
                MetadataLabelInput old = before.metadata.Single(value => value.path == input.path);
                if (Util.Json.Serialize(input.meta) != Util.Json.Serialize(old.meta) || !new HashSet<string>(input.labels).SetEquals(old.labels)) changes.Add(input.meta.path);
            }
            return changes.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        /// <summary>核对 Firebase 四文件语义、普通写失败日志、允许标签变化和其它元数据保持，Generated=true 只证明回调到达末尾。</summary>
        /// <param name="receipt">本次回调前后快照、日志及可选缓存零回调凭据。</param>
        /// <param name="token">只读文件核验取消令牌。</param>
        private static void ValidateFirebaseExecution(CallbackExecutionReceipt receipt, CancellationToken token)
        {
            FirebaseCrashlyticsSnapshot before = receipt.before, after = receipt.after;
            if (receipt.edmScene?.cacheEvidence != null)
            {
                ValidateEdmSceneExecution(receipt.edmScene);
                if (before == null || Util.Json.Serialize(before) != Util.Json.Serialize(after) ||
                    receipt.successLogs?.Length != 0 || receipt.failureLogs?.Length != 0 || receipt.changedPaths?.Length != 0)
                    throw new InvalidOperationException("缓存零回调支路的 Firebase 输入、生成状态、日志或文件未完全保持。");
                return;
            }
            if (before == null || after == null || before.unityVersion != after.unityVersion || !after.generated ||
                before.versionHandlerLogger == null || after.versionHandlerLogger == null ||
                Util.Json.Serialize(before.versionHandlerLogger) != Util.Json.Serialize(after.versionHandlerLogger) ||
                Util.Json.Serialize(before.preservedFiles) != Util.Json.Serialize(after.preservedFiles) ||
                !before.metadata.Select(input => input.path).SequenceEqual(after.metadata.Select(input => input.path)))
                throw new InvalidOperationException("Firebase 预输入、候选集合或 Generated 末尾状态未满足冻结合同。");
            var originalRoots = new HashSet<string>(before.metadataRoots, StringComparer.Ordinal);
            var currentRoots = new HashSet<string>(after.metadataRoots, StringComparer.Ordinal);
            if (!originalRoots.IsSubsetOf(currentRoots) || currentRoots.Except(originalRoots).Any(path =>
                !before.metadata.Any(input => input.path == path && !input.readOnly && !string.IsNullOrEmpty(input.guid) && input.expectedLabels.Contains("gvh"))))
                throw new InvalidOperationException("Firebase metadata 标签查询候选出现未声明的增加或减少。");
            if (receipt.failureLogs.Length > 0) throw new InvalidOperationException("Firebase 普通日志记录了写入失败：" + string.Join("; ", receipt.failureLogs));
            foreach (MetadataLabelInput input in after.metadata)
            {
                MetadataLabelInput old = before.metadata.Single(value => value.path == input.path);
                if (input.guid != old.guid || input.importerType != old.importerType || Util.Json.Serialize(input.source) != Util.Json.Serialize(old.source))
                    throw new InvalidOperationException("EDM metadata 的源内容、GUID 或 importer 已漂移：" + input.path);
                bool labelsUnchanged = new HashSet<string>(old.labels).SetEquals(input.labels);
                if (old.readOnly || before.generated || string.IsNullOrEmpty(old.guid))
                {
                    if (!labelsUnchanged || Util.Json.Serialize(input.meta) != Util.Json.Serialize(old.meta)) throw new InvalidOperationException("应保持的 metadata 已写入：" + input.path);
                }
                else
                {
                    if (!labelsUnchanged && !new HashSet<string>(old.expectedLabels).SetEquals(input.labels) || input.metaWithoutLabelsSha256 != old.metaWithoutLabelsSha256)
                        throw new InvalidOperationException("EDM 标签变化不符合合法结果或其它 importer 字段已改动：" + input.path);
                }
            }
            string root = Path.Combine(ProjectRoot, "Assets/Plugins/Android/FirebaseCrashlytics.androidlib");
            string idPath = Path.Combine(root, "res/values/crashlytics_build_id.xml");
            string versionPath = Path.Combine(root, "res/values/crashlytics_unity_version.xml");
            string id = ReadFirebaseResource(idPath, "com.crashlytics.android.build_id");
            string version = ReadFirebaseResource(versionPath, "com.google.firebase.crashlytics.unity_version");
            if (!Guid.TryParseExact(id, "D", out _) || version != before.unityVersion) throw new InvalidOperationException("Firebase XML 的 build id 或 Unity 版本不符合正常生成语义。");
            foreach (string path in new[] { Path.Combine(root, "AndroidManifest.xml"), Path.Combine(root, "project.properties") })
            {
                CallbackFileState old = before.resources.Single(file => file.path == path);
                CallbackFileState current = after.resources.Single(file => file.path == path);
                if (current.kind != "file" || old.kind == "file" && old.sha256 != current.sha256) throw new InvalidOperationException("Firebase 仅创建文件缺失或覆盖了原有文件：" + path);
                if (old.kind == "absent" && path.EndsWith("project.properties", StringComparison.Ordinal) && File.ReadAllText(path) != "target=android-9\nandroid.library=true")
                    throw new InvalidOperationException("Firebase 新建 project.properties 不符合已审模板。");
                if (old.kind == "absent" && path.EndsWith("AndroidManifest.xml", StringComparison.Ordinal))
                {
                    var document = new XmlDocument { XmlResolver = null };
                    using (XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) document.Load(reader);
                    XmlElement manifest = document.DocumentElement;
                    const string android = "http://schemas.android.com/apk/res/android";
                    if (manifest?.Name != "manifest" || manifest.GetAttribute("package") != "com.google.firebase.crashlytics.unity" ||
                        manifest.GetAttribute("versionCode", android) != "1" || manifest.GetAttribute("versionName", android) != "1.0" || manifest.ChildNodes.Count != 0)
                        throw new InvalidOperationException("Firebase 新建 AndroidManifest.xml 不符合已审模板。");
                }
            }
            foreach (CallbackFileState current in after.resources.Where(file => file.path.EndsWith(".meta", StringComparison.Ordinal)))
            {
                CallbackFileState old = before.resources.Single(file => file.path == current.path);
                if (old.kind != "absent" && Util.Json.Serialize(current) != Util.Json.Serialize(old)) throw new InvalidOperationException("Firebase Refresh 改变了原有 meta：" + current.path);
            }
            if (after.resources.Where(file => !file.path.EndsWith(".meta", StringComparison.Ordinal) &&
                !file.path.EndsWith(".xml", StringComparison.Ordinal) && !file.path.EndsWith("project.properties", StringComparison.Ordinal)).Any(file => file.kind != "directory"))
                throw new InvalidOperationException("Firebase 正常生成所需目录尚未完整建立。");
            if (before.generated)
            {
                if (receipt.successLogs.Length != 0 || Util.Json.Serialize(before.resources) != Util.Json.Serialize(after.resources))
                    throw new InvalidOperationException("Firebase 已生成分支出现未预期重复写入；不会重置 Generated 或重放。");
            }
            else if (receipt.successLogs.Length != 2 || !receipt.successLogs.Contains("Generated resource for com.crashlytics.android.build_id : " + id) ||
                !receipt.successLogs.Contains("Generated resource for com.google.firebase.crashlytics.unity_version : " + version))
                throw new InvalidOperationException("Firebase 缺少本次两个资源写成功日志，已有合法文件不能作为本次生成证明。");
            token.ThrowIfCancellationRequested();
        }

        /// <summary>只读解析一个精确 Firebase XML resource，禁止 DTD/外部实体，不以字符串片段冒充合法资源。</summary>
        private static string ReadFirebaseResource(string path, string key)
        {
            var document = new XmlDocument { XmlResolver = null };
            using (XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) document.Load(reader);
            XmlNodeList entries = document.SelectNodes("/resources/string");
            if (entries?.Count != 1 || entries[0].Attributes?["name"]?.Value != key || entries[0].Attributes?["translatable"]?.Value != "false")
                throw new InvalidOperationException("Firebase XML 不符合精确 string resource 合同：" + path);
            return entries[0].InnerText;
        }

        /// <summary>只接受本轮实际精读的 SBP 回调分支；SBP flags274 不进入 Player pre/post 分支。</summary>
        private static bool TryCaptureKnownSbpCallback(Type type, ICollection<string> identities, CancellationToken cancellationToken, out string error)
        {
            error = null;
            switch (type.FullName)
            {
                case "UnityEditor.UnityBuildPostprocessor":
                    if (!IsVerifiedEngineAssembly(type, "UnityEditor.CoreModule.dll", "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", cancellationToken)) break;
                    identities.Add("UnityStaticBatching=" + PlayerSettings.GetStaticBatchingForPlatform(EditorUserBuildSettings.activeBuildTarget));
                    return true;
                case "Unity.ProjectAuditor.Editor.Modules.ShadersModule":
                    if (!IsVerifiedEngineAssembly(type, "UnityEditor.ProjectAuditorModule.dll", "2c20ea0b8555c4851b9885a2115f2499cecdea88ed0ef28ccdd530829113eac9", cancellationToken)) break;
                    return true;
                case "UnityEditor.AdaptivePerformance.Editor.Analytics.Hooks.BuildHook":
                    if (!IsVerifiedEngineAssembly(type, "UnityEditor.AdaptivePerformanceModule.dll", "7988058106626eccc3acb284e4e89a27ce3936a80439c8cf9d0a40fd451aa6db", cancellationToken)) break;
                    identities.Add("EditorAnalytics.enabled=" + UnityEditor.EditorAnalytics.enabled);
                    if (UnityEditor.EditorAnalytics.enabled && !TryCaptureEmptyAdaptiveProviders(type, identities, out error)) return false;
                    return true;
                case "UnityEditor.Timeline.Analytics.TimelineAnalytics+TimelineAnalyticsProcess":
                    return IsVerifiedPackageSource(type, "com.unity.timeline", "1.8.13", "Editor/Analytics/TimelineAnalytics.cs", "3056ac2166b20cbd464c1583a1ab2f3c83568673688452c99dcb2b09425968cd", cancellationToken, out error);
                case "UnityEditor.UI.Analytics.UGUIBuildAnalytics":
                    return IsVerifiedPackageSource(type, "com.unity.ugui", "2.0.0", "Editor/Analytics/UGUIBuildAnalytics.cs", "c13d24e5739c2abf326db6c8fe6c1c3358f52938fbe5aa26646f9e7d8a0918ca", cancellationToken, out error);
                case "UnityEditor.Purchasing.UnityPurchasingEditor":
                    if (!IsVerifiedPackageSource(type, "com.unity.purchasing", "5.3.1", "Editor/UnityPurchasingEditor.cs", "996756f712cd01434c89407f836b5357a3d51b845bf9b58fad17055a75d0e773", cancellationToken, out error)) return false;
                    bool hasBillingMode = File.Exists(Path.Combine(ProjectRoot, "Assets/Resources/BillingMode.json"));
                    identities.Add("IAP.BillingModeExists=" + hasBillingMode);
                    if (hasBillingMode) return TryCaptureUnchangedIapImporter(type, identities, cancellationToken, out error);
                    // 只接受已审构建方法的缺文件分支；不调用类型设置 API 或触发其延迟初始化。
                    return true;
                case "GooglePlayServices.PlayServicesPreBuild":
                    if (!IsVerifiedEdmAssembly(type, "Google.JarResolver.dll", "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0", cancellationToken)) break;
                    return TryCaptureEdmWarningSettings(type, identities, cancellationToken, out error);
                case "GooglePlayServices.PlayServicesResolver":
                    if (!IsVerifiedEdmAssembly(type, "Google.JarResolver.dll", "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0", cancellationToken)) break;
                    return TryCaptureEdmSceneNoOp(type, identities, out error);
                case "UnityEditor.Rendering.ShaderVariantStripper":
                case "UnityEditor.Rendering.ComputeShaderVariantStripper":
                    return TryCaptureShaderCallback(type, false, identities, cancellationToken, out error);
                case "UnityEditor.Rendering.BuiltIn.ShaderPreprocessor":
                    return TryCaptureShaderCallback(type, true, identities, cancellationToken, out error);
                case "UnityEditor.ProBuilder.UnityScenePostProcessor":
                    if (!IsVerifiedPackageSource(type, "com.unity.probuilder", "6.1.2", "Editor/EditorCore/UnityScenePostProcessor.cs", "3ee2bb501318a3dbb09e25ac1558b5b569062dabf46f3389ef4b993934e202c5", cancellationToken, out error)) return false;
                    if (!TryCaptureInitializedProBuilderSettings(type, identities, cancellationToken, out error)) return false;
                    // ProBuilder 会全局找对象且可能缓存 Mesh 资产；当前先只放行无这些对象的确定空分支。
                    bool hasProBuilder = Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Any(value => value != null &&
                        (value.GetType().FullName == "UnityEngine.ProBuilder.ProBuilderMesh" || IsDerivedFromNamedType(value.GetType(), "UnityEngine.ProBuilder.EntityBehaviour")));
                    identities.Add("ProBuilder.loadedObjectsPresent=" + hasProBuilder);
                    if (hasProBuilder) { error = "当前已加载 ProBuilder 对象可能改场景与 Mesh 资产，尚缺源依赖恢复契约"; return false; }
                    return true;
            }
            error = "当前回调实现不在已核对的精确 SBP 契约中";
            return false;
        }

        /// <summary>只复用 Player 与 SBP 正文相同的六个精确回调分支；Player 额外内容和生命周期由专属合同验证，不泛化 SBP flags。</summary>
        /// <param name="type">当前实际发现的准确类型。</param>
        /// <param name="label">准确属性方法或接口类型标签，防止同类不同方法混用。</param>
        /// <param name="identities">追加既有源码、配置、内部 stripper 与事件材料。</param>
        /// <param name="token">只读源码和 DLL 摘要取消令牌。</param>
        /// <param name="error">身份或原自然分支不符的具体原因。</param>
        /// <returns>真实 WebGL 域中六项准确分支通过既有同正文检查时为 true。</returns>
        internal static bool TryCapturePlayerSharedPackageCallback(Type type, string label, ICollection<string> identities, CancellationToken token, out string error)
        {
            error = "Player shared package callback 不属于六项已审准确声明。";
            if (type == null || EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) return false;
            bool known = type.FullName == "GooglePlayServices.PlayServicesResolver" && label == type.FullName + ".OnPostProcessScene [PostProcessScene]" ||
                type.FullName == "UnityEditor.ProBuilder.UnityScenePostProcessor" && label == type.FullName + ".OnPostprocessScene [PostProcessScene]" ||
                type.FullName == "UnityEditor.Purchasing.UnityPurchasingEditor" && label == type.FullName + ".OnPostProcessScene [PostProcessScene]" ||
                type.FullName == "UnityEditor.Rendering.ShaderVariantStripper" && label == type.FullName ||
                type.FullName == "UnityEditor.Rendering.ComputeShaderVariantStripper" && label == type.FullName ||
                type.FullName == "UnityEditor.Rendering.BuiltIn.ShaderPreprocessor" && label == type.FullName;
            return known && TryCaptureKnownSbpCallback(type, identities, token, out error);
        }

        /// <summary>按 IAP 原配置解析与 importer 选择规则冻结已经满足目标值的分支，不修改包或插件配置。</summary>
        private static bool TryCaptureUnchangedIapImporter(Type type, ICollection<string> identities, CancellationToken cancellationToken, out string error)
        {
            Type[] stores = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("UnityEngine.Purchasing.AppStore", false)).Where(value => value != null).ToArray();
            if (stores.Length != 1 || !IsVerifiedPackageSource(stores[0], "com.unity.purchasing", "5.3.1", "Runtime/Purchasing/Stores/AppStore.cs", "50900060d0fa287526bdf9e0b4cfb48161b03325541b436b6018cf27eae31e7b", cancellationToken, out error) ||
                !IsVerifiedPackageSource(type, "com.unity.purchasing", "5.3.1", "Runtime/Purchasing/Legacy/Configurations/StoreConfiguration.cs", "893b6aa0c528f52aee9b5ad2f2e8b02a5b5a9dd5383ebb9ede6533d90876dc26", cancellationToken, out error))
            { error = "IAP AppStore 或 StoreConfiguration 的已审身份不匹配。"; return false; }
            string modePath = Path.Combine(ProjectRoot, "Assets/Resources/BillingMode.json");
            if (!TryGetIapAndroidCompatibility(File.ReadAllText(modePath), stores[0], out bool expected, out error)) return false;
            identities.Add("IAP.BillingModeSHA=" + ComputeFileHash(modePath, cancellationToken));
            const string pluginName = "billing-8.3.0.aar";
            const string canonical = "Packages/com.unity.purchasing/Plugins/UnityPurchasing/Android/billing-8.3.0.aar";
            PluginImporter importer = AssetImporter.GetAtPath(canonical) as PluginImporter;
            string selected = canonical;
            if (importer == null)
            {
                string[] candidates = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(pluginName)).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => Path.GetFileName(path) == pluginName).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                identities.Add("IAP.fallbackCandidates=" + string.Join(",", candidates));
                if (candidates.Length != 1) { identities.Add("IAP.importer=no-unique-target"); error = null; return true; }
                selected = candidates[0];
                importer = AssetImporter.GetAtPath(selected) as PluginImporter;
                if (importer == null) { error = "IAP 唯一候选不是可用 PluginImporter。"; return false; }
            }
            bool actual = importer.GetCompatibleWithPlatform(BuildTarget.Android);
            identities.Add("IAP.importer=" + selected + "|" + AssetDatabase.AssetPathToGUID(selected) + "|Android=" + actual + "|expected=" + expected);
            string physical = FileUtil.GetPhysicalPath(selected);
            if (string.IsNullOrEmpty(physical)) physical = selected;
            string absolute = Path.GetFullPath(Path.IsPathRooted(physical) ? physical : Path.Combine(ProjectRoot, physical));
            identities.Add("IAP.pluginSHA=" + ComputeFileHash(absolute, cancellationToken));
            identities.Add("IAP.pluginMetaSHA=" + (File.Exists(absolute + ".meta") ? ComputeFileHash(absolute + ".meta", cancellationToken) : "absent"));
            if (actual != expected) { error = "IAP 会改变插件 Android 兼容性，当前只接受已经满足目标值的分支：" + selected; return false; }
            error = null;
            return true;
        }

        /// <summary>接受标准对象/字符串配置并保留 IAP 大小写敏感枚举判定与未知值回退 GooglePlay 的原语义。</summary>
        internal static bool TryGetIapAndroidCompatibility(string json, Type appStoreType, out bool enabled, out string error)
        {
            enabled = false;
            try
            {
                var value = Newtonsoft.Json.Linq.JObject.Parse(json, new Newtonsoft.Json.Linq.JsonLoadSettings
                { DuplicatePropertyNameHandling = Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error });
                Newtonsoft.Json.Linq.JToken store = value["androidStore"];
                if (appStoreType == null || !appStoreType.IsEnum || store?.Type != Newtonsoft.Json.Linq.JTokenType.String)
                { error = "IAP BillingMode 必须含 androidStore 字符串与准确 AppStore 枚举。"; return false; }
                string name = (string)store;
                string effective = Enum.IsDefined(appStoreType, name) ? name : "GooglePlay";
                enabled = effective == "NotSpecified" || effective == "GooglePlay";
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is Newtonsoft.Json.JsonException || exception is ArgumentException)
            { error = "IAP BillingMode 解析失败：" + exception.Message; return false; }
        }

        /// <summary>核对 EDM 正式包与 DLL Hash，不以相同类型名或任意已加载程序集作为契约证据。</summary>
        private static bool IsVerifiedEdmAssembly(Type type, string fileName, string expectedHash, CancellationToken cancellationToken)
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly);
            string path = package == null ? null : Path.GetFullPath(Path.Combine(package.resolvedPath, "ExternalDependencyManager/Editor/1.2.188", fileName));
            return package?.name == "com.google.external-dependency-manager" && package.version == "1.2.188" &&
                Path.GetFullPath(type.Assembly.Location) == path && File.Exists(path) &&
                string.Equals(ComputeFileHash(path, cancellationToken), expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 冻结正规场景回调的确定空分支：非 Android 且 Gradle resolver 为空时，已审 getter 不构造实例，回调在读取设置前返回。
        /// Android 或已有 resolver 必须进入完整解析输入合同，不能通过改场景计数、自动解析设置或私有状态跳过。
        /// </summary>
        private static bool TryCaptureEdmSceneNoOp(Type resolverType, ICollection<string> identities, out string error)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo resolverField = resolverType.GetField("gradleResolverInstance", flags);
            FieldInfo countField = resolverType.GetField("scenesProcessed", flags);
            if (resolverField?.FieldType.FullName != "GooglePlayServices.GradleResolver" || countField?.FieldType != typeof(int))
            { error = "EDM 场景回调的固定 resolver 或场景计数字段身份已变化。"; return false; }
            identities.Add("EDM.scene.activeTarget=" + EditorUserBuildSettings.activeBuildTarget);
            identities.Add("EDM.scene.scenesProcessed=" + countField.GetValue(null));
            bool resolverIsNull = resolverField.GetValue(null) == null;
            identities.Add("EDM.scene.resolverIsNull=" + resolverIsNull);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android && resolverIsNull)
            { identities.Add("EDM.scene.branch=non-android-null-resolver-noop"); error = null; return true; }
            error = "EDM 场景回调存在或将创建 Gradle resolver，仍需完整输入、工具与本次无需解析日志合同。";
            return false;
        }

        /// <summary>冻结已加载 EDM 正式设置的三个警告输入；只读固定字段和 EditorPrefs，不调用私有 getter 或加载 XML。</summary>
        private static bool TryCaptureEdmWarningSettings(Type warningType, ICollection<string> identities, CancellationToken cancellationToken, out string error)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            { identities.Add("EDM.warning=non-android-noop"); error = null; return true; }
            Type settingsDialog = warningType.Assembly.GetType("GooglePlayServices.SettingsDialog", false);
            FieldInfo dialogSettingsField = settingsDialog?.GetField("projectSettings", BindingFlags.NonPublic | BindingFlags.Static);
            object settingsInstance = dialogSettingsField?.GetValue(null); // 已核 cctor 仅常量与无副作用 ProjectSettings 构造。
            Type projectSettings = settingsInstance?.GetType();
            if (projectSettings?.FullName != "Google.ProjectSettings" ||
                !IsVerifiedEdmAssembly(projectSettings, "Google.VersionHandlerImpl.dll", "c3a6d0419b41b0cbc7ad518b0cc2b311af043b5d71ed709ea3baa576b79862ce", cancellationToken))
            { error = "EDM 警告的正式设置实现身份不匹配。"; return false; }
            FieldInfo systemField = projectSettings.GetField("systemSettings", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo projectField = projectSettings.GetField("projectSettings", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo loadedField = projectSettings.GetField("loadedSettings", BindingFlags.NonPublic | BindingFlags.Static);
            if (systemField == null || projectField == null || loadedField == null)
            { error = "EDM 固定设置字段已变化。"; return false; }
            object system = systemField.GetValue(null);
            object project = projectField.GetValue(null);
            bool useProject = EditorPrefs.GetBool("GooglePlayServices.UseProjectSettings", true);
            if (system == null || system.GetType() != projectSettings.Assembly.GetType("Google.EditorSettings", false) ||
                project == null || project.GetType() != projectSettings.Assembly.GetType("Google.InMemorySettings", false))
            { error = "EDM 自定义 ISettings 尚无只读契约。"; return false; }
            if (useProject && !ReferenceEquals(loadedField.GetValue(null), project))
            { error = "EDM 项目设置尚未加载，回调会初始化 XML 输入；需完整加载副作用合同。"; return false; }
            FieldInfo valuesField = project.GetType().GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance);
            var values = valuesField?.GetValue(project) as SortedDictionary<string, string>;
            if (values == null) { error = "EDM 内存设置字典身份已变化。"; return false; }
            identities.Add("EDM.UseProjectSettings=" + useProject);
            foreach (string key in new[] { "GooglePlayServices.AutoResolutionDisabledWarning", "GooglePlayServices.AutoResolverEnabled", "GooglePlayServices.AutoResolveOnBuild" })
            {
                bool value = useProject
                    ? values.TryGetValue(key, out string raw) && bool.TryParse(raw, out bool parsed) ? parsed : true
                    : EditorPrefs.GetBool(key, true);
                identities.Add(key + "=" + value);
            }
            string path = Path.Combine(ProjectRoot, "ProjectSettings/GvhProjectSettings.xml");
            identities.Add("EDM.settingsFile=" + (File.Exists(path) ? ComputeFileHash(path, cancellationToken) : "absent"));
            error = null;
            return true;
        }

        /// <summary>允许既有 Analytics 设置下已审的空 provider 分支；未知 metadata getter 不执行，不关闭统计设置。</summary>
        private static bool TryCaptureEmptyAdaptiveProviders(Type type, ICollection<string> identities, out string error)
        {
            Type metadata = type.Assembly.GetType("UnityEditor.AdaptivePerformance.Editor.Metadata.AdaptivePerformancePackageMetadataStore", false);
            FieldInfo packagesField = metadata?.GetField("s_Packages", BindingFlags.NonPublic | BindingFlags.Static);
            Type packageContract = type.Assembly.GetType("UnityEditor.AdaptivePerformance.Editor.Metadata.IAdaptivePerformancePackage", false);
            if (packagesField == null || packageContract == null)
            { error = "Adaptive Performance metadata 固定类型或字段身份已变化。"; return false; }
            Type expectedDictionary = typeof(Dictionary<,>).MakeGenericType(typeof(string), packageContract);
            object packages = packagesField.GetValue(null); // 已核 cctor 仅建内存容器与既有 Editor play 事件，不解析或安装 provider。
            if (packages == null || packages.GetType() != expectedDictionary || !(packages is System.Collections.IDictionary dictionary) || dictionary.Count != 0)
            { error = "Adaptive Performance provider 集合非空，metadata/interface getter 尚缺逐实现契约；Analytics 原设置保持。"; return false; }
            identities.Add("AdaptivePerformance.providers=verified-empty");
            error = null;
            return true;
        }

        /// <summary>ProBuilder 静态 Pref 初始化可能迁移设置并注册保存；仅接受已建立正式 Settings 的无对象分支。</summary>
        private static bool TryCaptureInitializedProBuilderSettings(Type type, ICollection<string> identities, CancellationToken cancellationToken, out string error)
        {
            if (!IsVerifiedPackageSource(type, "com.unity.probuilder", "6.1.2", "Editor/EditorCore/Pref.cs", "a4776c6d524877b00401b1cf13bd0c6dd7145a401c0b07eea44d997f11f40081", cancellationToken, out error) ||
                !IsVerifiedPackageSource(type, "com.unity.probuilder", "6.1.2", "Editor/EditorCore/ProBuilderSettings.cs", "24529ae0b80bf380cd1ff0f725e84730d6cecc22ba7719bbdfde9b70a4faca2c", cancellationToken, out error)) return false;
            Type settings = type.Assembly.GetType("UnityEditor.ProBuilder.ProBuilderSettings", false);
            FieldInfo instanceField = settings?.GetField("s_Instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (settings == null || settings.TypeInitializer != null || instanceField == null)
            { error = "ProBuilder 设置类型或无初始化字段读取契约已变化。"; return false; }
            object instance = instanceField.GetValue(null);
            if (instance == null || instance.GetType().FullName != "UnityEditor.SettingsManagement.Settings")
            { error = "ProBuilder 设置尚未初始化，首次回调可能迁移配置并新增保存回调；Action 不触发该初始化。"; return false; }
            Type settingsType = instance.GetType();
            Type userSetting = settingsType.Assembly.GetType("UnityEditor.SettingsManagement.UserSetting`1", false);
            if (userSetting == null || !IsVerifiedPackageSource(settingsType, "com.unity.settings-manager", "2.1.1", "Editor/Settings.cs", "196185d5c64589a2d87a3d34becd8f68d6454a1066372d0cc4856df7afd14f29", cancellationToken, out error) ||
                !IsVerifiedPackageSource(userSetting, "com.unity.settings-manager", "2.1.1", "Editor/UserSetting.cs", "af5f350d6a249942eb8c65ce5e7b5a42d87620114b164cbda6fac3770e18567f", cancellationToken, out error)) return false;
            identities.Add("ProBuilder.settings=verified-initialized");
            error = null;
            return true;
        }

        /// <summary>仅沿现有类型继承链判断已知场景组件，不实例化或执行项目类型。</summary>
        private static bool IsDerivedFromNamedType(Type type, string fullName)
        {
            for (Type current = type; current != null; current = current.BaseType) if (current.FullName == fullName) return true;
            return false;
        }

        /// <summary>确认当前回调确由匹配版本正式包的指定源码提供；Hash 由调用方的已审契约给出。</summary>
        private static bool IsVerifiedPackageSource(Type type, string packageName, string version, string relativePath, string expectedHash,
            CancellationToken cancellationToken, out string error)
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly);
            string path = package == null ? null : Path.Combine(package.resolvedPath, relativePath);
            if (package == null || package.name != packageName || package.version != version || !File.Exists(path) || string.IsNullOrEmpty(expectedHash) ||
                !string.Equals(ComputeFileHash(path, cancellationToken), expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                error = "回调包版本或已审源码 Hash 不匹配：" + packageName + "/" + relativePath;
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>冻结三项已核 Shader 回调的内部实例发现、报告器与事件；未知实现或委托不执行。</summary>
        private static bool TryCaptureShaderCallback(Type type, bool builtin, ICollection<string> identities, CancellationToken cancellationToken, out string error)
        {
            string source = builtin ? "Editor/Generation/Targets/BuiltIn/Editor/ShaderPreprocessor.cs" : "Editor/ShaderStripping/ShaderPreprocessor.cs";
            string hash = builtin ? "c24566d7b7db6dc63f4e14ae2ba050348d330ef876bf1374bb1ca31e503dde73" : "044a638c85424f0acf32a2cf85c3541c2f51098de4acb7b63e4c91e6c5312687";
            if (!IsVerifiedPackageSource(type, builtin ? "com.unity.shadergraph" : "com.unity.render-pipelines.core", "17.4.0", source, hash, cancellationToken, out error)) return false;
            if (builtin) return TryCaptureEmptyStaticDelegate(type, "shaderPreprocessed", identities, out error);
            if (!IsVerifiedPackageSource(type, "com.unity.render-pipelines.core", "17.4.0", "Editor/ShaderStripping/ShaderStrippingReport.cs", "34f295546da78f48361f2037b8c1667d51a5df656df3fba8a11e76f7a22e5291", cancellationToken, out error) ||
                !IsVerifiedPackageSource(type, "com.unity.render-pipelines.core", "17.4.0", "Editor/ShaderStripping/ShaderStrippingWatcher.cs", "80690485f66595414be6bff3c723e915e4a1a42f84779b210145e4da977bcf14", cancellationToken, out error)) return false;
            Type contract = type.Assembly.GetType("UnityEditor.Rendering.IVariantStripper`2", false);
            Type watcher = type.Assembly.GetType("UnityEditor.Rendering.ShaderStrippingWatcher", false);
            Type reporting = type.Assembly.GetType("UnityEditor.Rendering.ShaderStripping", false);
            FieldInfo reporterField = reporting?.GetField("m_Reporter", BindingFlags.Static | BindingFlags.NonPublic);
            if (contract == null || watcher == null || reporterField == null)
            { error = "Shader 内部契约类型或字段已变化"; return false; }
            Type closedContract = contract.MakeGenericType(type.FullName == "UnityEditor.Rendering.ComputeShaderVariantStripper" ? typeof(ComputeShader) : typeof(Shader),
                type.FullName == "UnityEditor.Rendering.ComputeShaderVariantStripper" ? typeof(string) : typeof(UnityEditor.Rendering.ShaderSnippetData));
            foreach (Type candidate in TypeCache.GetTypesDerivedFrom(closedContract))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (candidate.IsAbstract || candidate.IsInterface || candidate.ContainsGenericParameters || candidate.GetConstructor(Type.EmptyTypes) == null) continue;
                identities.Add(candidate.FullName + "|" + candidate.Assembly.FullName + "|" + candidate.Module.ModuleVersionId + " [IVariantStripper]");
                if (candidate.FullName == "UnityEditor.Rendering.SRPDisabledComputeShaderVariantStripper" && candidate.Assembly == type.Assembly &&
                    TryCaptureNoSrpComputeStripper(candidate, identities, cancellationToken, out error)) continue;
                if (!string.IsNullOrEmpty(error)) return false;
                error = "Shader 将实例化尚无写入契约的内部 stripper：" + candidate.FullName;
                return false;
            }
            if (!TryCaptureNoRenderPipeline(identities, out error)) return false;
            object reporter = reporterField.GetValue(null);
            string reporterName = reporter?.GetType().FullName;
            identities.Add("ShaderStripping.reporter=verified-memory-or-log-only");
            if (reporter != null && (reporter.GetType().Assembly != type.Assembly ||
                reporterName != "UnityEditor.Rendering.ShaderStrippingReportLogger" && reporterName != "UnityEditor.Rendering.ShaderStrippingReportEmpty" && reporterName != "UnityEditor.Rendering.ShaderStrippingReport"))
            { error = "Shader 当前报告器缺少确定写入契约"; return false; }
            return TryCaptureEmptyStaticDelegate(watcher, "shaderProcessed", identities, out error) &&
                   TryCaptureEmptyStaticDelegate(watcher, "computeShaderProcessed", identities, out error);
        }

        /// <summary>只接受所有 Quality 与默认 Graphics 均未配置 SRP 的分支，避免调用自定义 RenderPipelineAsset getter 或 GPU 接口。</summary>
        private static bool TryCaptureNoRenderPipeline(ICollection<string> identities, out string error)
        {
            bool hasPipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null ||
                Enumerable.Range(0, QualitySettings.names.Length).Any(index => QualitySettings.GetRenderPipelineAssetAt(index) != null);
            identities.Add("Shader.renderPipelineConfigured=" + hasPipeline);
            error = hasPipeline ? "SRP 资产的 getter 与 GPU 资源初始化尚无确定写入契约；当前只接受未配置 SRP 的分支。" : null;
            return !hasPipeline;
        }

        /// <summary>核对已审 Compute stripper 与 CoreBuildData 的无 SRP、空缓存路径；仅读固定字段，不触发 lazy instance。</summary>
        private static bool TryCaptureNoSrpComputeStripper(Type type, ICollection<string> identities, CancellationToken cancellationToken, out string error)
        {
            if (!IsVerifiedPackageSource(type, "com.unity.render-pipelines.core", "17.4.0", "Editor/BuildProcessors/ShaderStrippers/SRPDisabledComputeShaderVariantStripper.cs", "b89a22dcfe43cf2bcc803e5b5e04dd9417d6c1c43339b757554fdbeb7cb18e9c", cancellationToken, out error) ||
                !IsVerifiedPackageSource(type, "com.unity.render-pipelines.core", "17.4.0", "Editor/BuildProcessors/CoreBuildData.cs", "5ca66359d8329ea1e88afedfaba140ee7278db615b72fef1fa5f69898b881e76", cancellationToken, out error) ||
                !IsVerifiedPackageSource(type, "com.unity.render-pipelines.core", "17.4.0", "Editor/BuildTargetExtensions.cs", "d7309c7dfe0edb0dbfff9f5f6db8f17f655b0740acd736a423b0f1334b5fb29f", cancellationToken, out error) ||
                !TryCaptureNoRenderPipeline(identities, out error)) return false;
            Type buildData = type.Assembly.GetType("UnityEditor.Rendering.CoreBuildData", false);
            FieldInfo instanceField = buildData?.GetField("m_Instance", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo pipelineField = buildData?.GetField("<buildingPlayerForRenderPipeline>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo cacheField = buildData?.GetField("<computeShaderCache>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (instanceField == null || pipelineField == null || cacheField == null)
            { error = "CoreBuildData 固定字段身份已变化。"; return false; }
            object instance = instanceField.GetValue(null);
            if (instance != null && (instance.GetType() != buildData || !(pipelineField.GetValue(instance) is bool buildingPipeline) || buildingPipeline ||
                !(cacheField.GetValue(instance) is System.Collections.IDictionary cache) || cache.Count != 0))
            { error = "当前 CoreBuildData 不属于已审的无 SRP、空 Compute 缓存分支。"; return false; }
            // null 只在正式回调中建立等价的无 SRP 空缓存内存状态；Plan 不初始化它。
            identities.Add("CoreBuildData=verified-no-srp-empty-compute-cache");
            error = null;
            return true;
        }

        /// <summary>仅读取已知版本固定事件字段，不调用委托；事件非空必须逐方法补契约。</summary>
        private static bool TryCaptureEmptyStaticDelegate(Type type, string fieldName, ICollection<string> identities, out string error)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null || !typeof(Delegate).IsAssignableFrom(field.FieldType))
            { error = "已知 Shader 事件字段已变化：" + type.FullName + "." + fieldName; return false; }
            var value = field.GetValue(null) as Delegate;
            identities.Add(type.FullName + "." + fieldName + "=" + (value == null ? "empty" : "subscribed"));
            if (value != null)
            {
                foreach (Delegate item in value.GetInvocationList()) identities.Add(item.Method.DeclaringType?.FullName + "." + item.Method.Name + "|" + item.Method.Module.ModuleVersionId);
                error = "Shader 事件订阅尚无写入契约：" + type.FullName + "." + fieldName;
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>冻结已保存场景、有效 Collector 根及递归依赖；只查资产路径与脚本类型，不打开场景或执行采集规则。</summary>
        private static bool TryCaptureSavedScenes(BundleInputSnapshot bundleInputs, CancellationToken cancellationToken,
            out GenerateActionCommon.Artifact[] inputs, out string[] dependencyPaths, out string[] dependencyIdentities, out string[] setup, out string error)
        {
            UnityEditor.SceneManagement.SceneSetup[] scenes = EditorSceneManager.GetSceneManagerSetup();
            setup = scenes.Select(scene => scene.path + "|" + scene.isLoaded + "|" + scene.isActive).ToArray();
            inputs = null;
            dependencyPaths = null;
            dependencyIdentities = null;
            var files = new List<string>();
            var roots = new HashSet<string>(StringComparer.Ordinal);
            foreach (UnityEditor.SceneManagement.SceneSetup scene in scenes)
            {
                if (string.IsNullOrEmpty(scene.path) || scene.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty)
                {
                    error = "SBP 场景恢复要求所有已加载场景已保存且无脏数据；Action 不自动保存用户场景。"; return false;
                }
                roots.Add(scene.path);
            }
            BundleCollectorSetting setting = bundleInputs == null ? null : AssetDatabase.LoadAssetAtPath<BundleCollectorSetting>(bundleInputs.bundleCollectorPath);
            BundleCollectorPackage package = setting?.GetPackage(bundleInputs.packageName);
            if (package == null) { error = "场景依赖冻结缺少已确认的 Collector Package 输入。"; return false; }
            foreach (BundleCollector collector in package.Groups.Where(group => group.ActiveRuleName != nameof(DisableGroup)).SelectMany(group => group.Collectors))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (AssetDatabase.IsValidFolder(collector.CollectPath))
                    foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { collector.CollectPath }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!AssetDatabase.IsValidFolder(path)) roots.Add(path);
                    }
                else roots.Add(collector.CollectPath);
            }
            dependencyPaths = AssetDatabase.GetDependencies(roots.OrderBy(path => path, StringComparer.Ordinal).ToArray(), true)
                .Concat(roots).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            dependencyIdentities = dependencyPaths.Select(path => path + "|" + AssetDatabase.AssetPathToGUID(path) + "|" + AssetDatabase.GetAssetDependencyHash(path)).ToArray();
            var prohibitedScripts = new HashSet<string>(MonoImporter.GetAllRuntimeMonoScripts()
                .Where(script => script != null && script.GetClass() != null &&
                    (script.GetClass().FullName == "UnityEngine.ProBuilder.ProBuilderMesh" || IsDerivedFromNamedType(script.GetClass(), "UnityEngine.ProBuilder.EntityBehaviour")))
                .Select(AssetDatabase.GetAssetPath), StringComparer.Ordinal);
            string proBuilderInput = dependencyPaths.FirstOrDefault(prohibitedScripts.Contains);
            if (proBuilderInput != null)
            {
                error = "Collector 或已打开场景的依赖含 ProBuilder 组件，不能把回调视为空分支：" + proBuilderInput;
                return false;
            }
            foreach (string path in dependencyPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string physicalPath = FileUtil.GetPhysicalPath(path);
                if (string.IsNullOrEmpty(physicalPath)) physicalPath = path;
                string absolutePath = Path.GetFullPath(Path.IsPathRooted(physicalPath) ? physicalPath : Path.Combine(ProjectRoot, physicalPath));
                if (!File.Exists(absolutePath))
                {
                    if (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal))
                    { error = "场景依赖源文件不存在，不能冻结：" + path; return false; }
                    continue; // Unity 内置的无磁盘依赖以逻辑路径冻结，不能伪造文件 Hash。
                }
                files.Add(absolutePath);
                if (File.Exists(absolutePath + ".meta")) files.Add(absolutePath + ".meta");
            }
            files.AddRange(new[] { "ProjectSettings/ProjectSettings.asset", "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" }.Where(path => File.Exists(Path.Combine(ProjectRoot, path))));
            inputs = files.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => GenerateActionCommon.CaptureFile(path, cancellationToken)).ToArray();
            error = null;
            return true;
        }

        /// <summary>SBP 的全局 SaveAssets 不得顺带持久化用户已有脏资产；仅返回路径，不输出序列化内容。</summary>
        /// <param name="error">可能被全局 SaveAssets 持久化的脏资产路径；没有候选时为 null。</param>
        /// <returns>没有真实或未知的持久化脏资产候选时为 true；仅诊断，不保存或清除 dirty。</returns>
        internal static bool TryValidateNoDirtyPersistentAssets(out string error)
        {
            string[] dirty = Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                .Where(IsDirtyPersistentAssetSaveCandidate)
                .Select(AssetDatabase.GetAssetPath).Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            error = dirty.Length == 0 ? null : "构建可能执行全局 SaveAssets，请先保存或恢复当前已有脏资产，再重新 Plan：" + string.Join(", ", dirty);
            return dirty.Length == 0;
        }

        /// <summary>只读筛选全局保存可能持久化的脏对象；保留全部原生资产和未知类型，不按包路径或只读状态豁免。</summary>
        /// <param name="value">当前已加载对象；须先同时满足非空、持久化与 dirty，再判断普通保存候选。</param>
        /// <returns>原生或仍无法排除普通保存的脏对象为 true；仅已审导入代码、内建临时和准确归属的零字体纹理缓存为 false。</returns>
        internal static bool IsDirtyPersistentAssetSaveCandidate(UnityEngine.Object value)
        {
            if (value == null || !EditorUtility.IsPersistent(value) || !EditorUtility.IsDirty(value)) return false;
            if (value is Texture2D && BuildImportedFontTextureCacheGuard.IsImportedEmptyFontTextureCache(value)) return false;
            return MayPersistDirtyAsset(AssetDatabase.IsNativeAsset(value), value is Shader || value is MonoScript,
                value.hideFlags, AssetDatabase.GetAssetPath(value));
        }

        /// <summary>排除导入产生的代码对象及明确不可保存的内置临时资源；非原生 Prefab 等未知对象继续阻断。</summary>
        /// <param name="nativeAsset">AssetDatabase.IsNativeAsset 的真实分类；为 true 时优先保留。</param>
        /// <param name="importedCodeObject">对象是否为 Shader 或 MonoScript；只允许排除非原生导入代码对象。</param>
        /// <param name="hideFlags">对象原始 HideFlags，仅与三个精确内建资源路径共同判断。</param>
        /// <param name="assetPath">AssetDatabase 返回的原逻辑路径；未知路径与非原生 Prefab 等继续保留。</param>
        /// <returns>不能排除普通保存持久化时为 true；不按包路径前缀或 DontSave 标志整体豁免。</returns>
        internal static bool MayPersistDirtyAsset(bool nativeAsset, bool importedCodeObject, HideFlags hideFlags, string assetPath)
        {
            // DontSaveInEditor 主要约束场景序列化，不能据此放行已持久化的原生资产。
            if (nativeAsset) return true;
            if (importedCodeObject) return false;
            bool builtInResource = assetPath == "Library/unity default resources" ||
                assetPath == "Library/unity editor resources" || assetPath == "Resources/unity_builtin_extra";
            return !builtInResource || (hideFlags & HideFlags.DontSaveInEditor) == 0;
        }

        [Serializable]
        internal sealed class PlayerSettingsSnapshot
        {
            public string target;
            public string standaloneSubtarget;
            public bool developmentBuild;
            public string projectSettingsHashSha256;
            public string companyName;
            public string productName;
            public string bundleVersion;
        }

        internal static string ProjectRoot =>
            Path.GetFullPath(Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath);

        internal static bool TryResolveActiveTarget(string value, out BuildTarget target, out string error)
        {
            target = BuildTarget.NoTarget;
            error = null;
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64 ||
                !Enum.GetNames(typeof(BuildTarget)).Contains(value, StringComparer.Ordinal) ||
                !Enum.TryParse(value, false, out target) || target == BuildTarget.NoTarget)
            {
                error = "target 必须是 Unity BuildTarget 的精确枚举值，且不能为 NoTarget。";
                return false;
            }

            if (target != EditorUserBuildSettings.activeBuildTarget)
            {
                error = $"target={target} 与当前 activeBuildTarget={EditorUserBuildSettings.activeBuildTarget} 不一致；Build Action 不会切换活动平台。";
                return false;
            }
            return true;
        }

        internal static bool TryValidateName(string field, string value, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            {
                error = field + " 必须是 1-128 个字符的非空值。";
                return false;
            }
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!char.IsLetterOrDigit(character) && character != '.' && character != '_' && character != '-')
                {
                    error = field + " 只能包含字母、数字、点、下划线和连字符。";
                    return false;
                }
            }
            if (value == "." || value == "..")
            {
                error = field + " 不能为路径导航片段。";
                return false;
            }
            return true;
        }

        internal static bool TryResolveOutputPath(string value, out string outputPath, out string error)
        {
            outputPath = null;
            error = null;
            if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value.IndexOf('\0') >= 0)
            {
                error = "outputPath 必须是长度不超过 2048 的非空文件或目录路径。";
                return false;
            }
            if (value.StartsWith("~", StringComparison.Ordinal))
            {
                error = "outputPath 不支持 ~ 展开，请提供项目相对路径或绝对路径。";
                return false;
            }
            try
            {
                outputPath = Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(ProjectRoot, value));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                error = "outputPath 无法规范化：" + exception.Message;
                return false;
            }
            if (string.Equals(outputPath, Path.GetPathRoot(outputPath), StringComparison.Ordinal))
            {
                error = "outputPath 不能是文件系统根目录。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 判断 Player Action 是否已具备可冻结、可验证的目标平台契约。
        /// </summary>
        /// <param name="target">待构建的 Unity 目标平台。</param>
        /// <returns>当前支持 Standalone 桌面平台或 WebGL 时返回 true。</returns>
        internal static bool IsSupportedPlayerTarget(BuildTarget target)
        {
            return target == BuildTarget.StandaloneOSX ||
                   target == BuildTarget.StandaloneWindows ||
                   target == BuildTarget.StandaloneWindows64 ||
                   target == BuildTarget.StandaloneLinux64 ||
                   target == BuildTarget.WebGL;
        }

        internal static PlayerSettingsSnapshot CapturePlayerSettings(
            BuildTarget target,
            bool developmentBuild,
            CancellationToken cancellationToken)
        {
            string projectSettingsPath = Path.Combine(ProjectRoot, "ProjectSettings/ProjectSettings.asset");
            return new PlayerSettingsSnapshot
            {
                target = target.ToString(),
                standaloneSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget.ToString(),
                developmentBuild = developmentBuild,
                projectSettingsHashSha256 = File.Exists(projectSettingsPath)
                    ? ComputeFileHash(projectSettingsPath, cancellationToken)
                    : null,
                companyName = PlayerSettings.companyName,
                productName = PlayerSettings.productName,
                bundleVersion = PlayerSettings.bundleVersion,
            };
        }

        internal static bool PlayerSettingsEqual(PlayerSettingsSnapshot expected, PlayerSettingsSnapshot actual)
        {
            return expected != null && actual != null &&
                   string.Equals(Util.Json.Serialize(expected), Util.Json.Serialize(actual), StringComparison.Ordinal);
        }

        internal static bool TryCaptureBundleInputs(
            string packageName,
            CancellationToken cancellationToken,
            out BundleInputSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = null;
            if (!EditorUtil.Config.WorkspaceActive.TryGetPersistedConfigMaster(
                    out ConfigMasterSO master, out string masterGuid, out string masterPath, out error))
            {
                return false;
            }
            if (master.CurrentPlatform == PlatformType.None)
            {
                error = "激活 ConfigMaster 当前 Platform 不能为 None。";
                return false;
            }

            EditorUtil.Config.DimensionalResolver.YooAssetResult resolved =
                EditorUtil.Config.DimensionalResolver.ResolveYooAsset(
                    master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
            string settingsPath = NormalizeAssetPath(resolved.YooAssetSettingsPath);
            string collectorPath = NormalizeAssetPath(resolved.BundleCollectorSettingPath);
            YooAssetSettings settings = string.IsNullOrEmpty(settingsPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<YooAssetSettings>(settingsPath);
            BundleCollectorSetting collector = string.IsNullOrEmpty(collectorPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<BundleCollectorSetting>(collectorPath);
            if (settings == null || collector == null)
            {
                error = "当前坐标必须显式指向可加载的 YooAssetSettings 与 BundleCollectorSetting。";
                return false;
            }
            int packageCount = collector.Packages?.Count(item => item != null && item.PackageName == packageName) ?? 0;
            if (packageCount != 1)
            {
                error = packageCount == 0
                    ? "BundleCollectorSetting 未配置目标 Package：" + packageName
                    : "BundleCollectorSetting 中目标 Package 名称不唯一：" + packageName;
                return false;
            }
            BundleCollectorPackage package = collector.GetPackage(packageName);
            if (!TryValidateBundlePackageStructure(package, out string packageError))
            {
                error = "目标 Package 的 Collector 配置未通过只读结构校验：" + packageError;
                return false;
            }
            if (!TryCaptureCollectorRules(package, out string[] ruleIdentities, out error)) return false;

            snapshot = new BundleInputSnapshot
            {
                masterGuid = masterGuid,
                masterPath = masterPath,
                masterHashSha256 = ComputeAssetHash(master, masterPath, cancellationToken),
                platform = master.CurrentPlatform.ToString(),
                channel = master.CurrentChannel.ToString(),
                developMode = master.CurrentDevelopMode.ToString(),
                yooAssetSettingsPath = settingsPath,
                yooAssetSettingsGuid = AssetDatabase.AssetPathToGUID(settingsPath),
                yooAssetSettingsHashSha256 = ComputeAssetHash(settings, settingsPath, cancellationToken),
                yooFolderName = settings.YooFolderName,
                packageFilePrefix = settings.PackageFilePrefix,
                bundleCollectorPath = collectorPath,
                bundleCollectorGuid = AssetDatabase.AssetPathToGUID(collectorPath),
                bundleCollectorHashSha256 = ComputeAssetHash(collector, collectorPath, cancellationToken),
                packageName = packageName,
                collectorRuleIdentities = ruleIdentities,
            };
            return true;
        }

        /// <summary>只读冻结所选采集规则身份；默认规则已核实无外部写入，自定义规则须先补确定契约。</summary>
        private static bool TryCaptureCollectorRules(BundleCollectorPackage package, out string[] identities, out string error)
        {
            Type[] defaults =
            {
                typeof(EnableGroup), typeof(DisableGroup), typeof(AddressDisable), typeof(AddressByFileName),
                typeof(AddressByGroupAndFileName), typeof(AddressByFolderAndFileName), typeof(PackSeparately),
                typeof(PackDirectory), typeof(PackTopDirectory), typeof(PackCollector), typeof(PackGroup),
                typeof(PackRawFile), typeof(PackVideoFile), typeof(PackShader), typeof(PackShaderVariants),
                typeof(CollectAll), typeof(CollectScene), typeof(CollectPrefab), typeof(CollectSprite),
                typeof(CollectShader), typeof(CollectShaderVariants), typeof(NormalIgnoreRule), typeof(RawFileIgnoreRule),
            };
            var selected = new List<(Type contract, string name)> { (typeof(IAssetIgnoreRule), package.IgnoreRuleName) };
            foreach (BundleCollectorGroup group in package.Groups)
            {
                selected.Add((typeof(IGroupActiveRule), group.ActiveRuleName));
                if (group.ActiveRuleName == nameof(DisableGroup)) continue;
                foreach (BundleCollector collector in group.Collectors)
                {
                    selected.Add((typeof(IBundlePackRule), collector.PackRuleName));
                    selected.Add((typeof(IAddressRule), collector.AddressRuleName));
                    selected.Add((typeof(IAssetFilterRule), collector.FilterRuleName));
                }
            }
            var captured = new List<string>();
            foreach ((Type contract, string name) in selected)
            {
                Type type = defaults.FirstOrDefault(candidate => candidate.Name == name && contract.IsAssignableFrom(candidate));
                if (type == null)
                {
                    identities = captured.ToArray();
                    error = "Collector 自定义规则尚无确定写入契约，不能执行或在 Plan 中实例化：" + contract.Name + "/" + name;
                    return false;
                }
                if (TypeCache.GetTypesDerivedFrom(contract).Any(candidate => candidate != type && candidate.Name == name &&
                    !candidate.IsAbstract && !candidate.IsInterface && !candidate.ContainsGenericParameters))
                {
                    identities = captured.ToArray();
                    error = "Collector 规则名称与其它具体类型冲突，无法诚实冻结实际实现：" + contract.Name + "/" + name;
                    return false;
                }
                captured.Add(type.FullName + "|" + type.Assembly.FullName + "|" + type.Module.ModuleVersionId);
            }
            identities = captured.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            error = null;
            return true;
        }

        /// <summary>Raw 管线不自动过滤采集；受控入口仅接受有效组全为 PackRawFile 主 Collector 的独立 Package。</summary>
        internal static bool TryValidateRawPackage(BundleInputSnapshot inputs, out string status, out string error)
        {
            status = "blocked";
            BundleCollectorSetting setting = AssetDatabase.LoadAssetAtPath<BundleCollectorSetting>(inputs.bundleCollectorPath);
            BundleCollectorPackage package = setting?.GetPackage(inputs.packageName);
            if (package == null) { error = "RawFile Package 无法加载。"; return false; }
            BundleCollector[] collectors = package.Groups.Where(group => group.ActiveRuleName != nameof(DisableGroup))
                .SelectMany(group => group.Collectors).ToArray();
            int rawCount = collectors.Count(item => item.CollectorType == ECollectorType.MainAssetCollector && item.PackRuleName == nameof(PackRawFile));
            if (rawCount == 0)
            {
                status = "not_applicable";
                error = "目标 Package 没有有效 PackRawFile 主 Collector；不创建测试采集器或执行 RawFile 构建。";
                return false;
            }
            if (rawCount != collectors.Length)
            {
                error = "RawFile 管线会收集整个 Package，当前混有非 PackRawFile 主 Collector；请使用独立 RawFile Package。";
                return false;
            }
            error = null;
            return true;
        }

        private static bool TryValidateBundlePackageStructure(
            BundleCollectorPackage package,
            out string error)
        {
            error = null;
            if (package == null)
            {
                error = "Package 为空。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(package.IgnoreRuleName) ||
                !BundleCollectorSettingData.HasAssetIgnoreRuleName(package.IgnoreRuleName))
            {
                error = "IgnoreRuleName 无效：" + package.IgnoreRuleName;
                return false;
            }
            if (package.Groups == null)
            {
                error = "Groups 为空。";
                return false;
            }

            foreach (BundleCollectorGroup group in package.Groups)
            {
                if (group == null)
                {
                    error = "Groups 包含空项。";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(group.ActiveRuleName) ||
                    !BundleCollectorSettingData.HasGroupActiveRuleName(group.ActiveRuleName))
                {
                    error = $"Group '{group.GroupName}' 的 ActiveRuleName 无效：{group.ActiveRuleName}";
                    return false;
                }
                if (group.Collectors == null)
                {
                    error = $"Group '{group.GroupName}' 的 Collectors 为空。";
                    return false;
                }

                foreach (BundleCollector item in group.Collectors)
                {
                    if (item == null)
                    {
                        error = $"Group '{group.GroupName}' 的 Collectors 包含空项。";
                        return false;
                    }
                    if (string.IsNullOrWhiteSpace(item.CollectPath) ||
                        string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(item.CollectPath)))
                    {
                        error = "Collector 路径无效：" + item.CollectPath;
                        return false;
                    }
                    if (item.CollectorType == ECollectorType.None ||
                        !BundleCollectorSettingData.HasBundlePackRuleName(item.PackRuleName) ||
                        !BundleCollectorSettingData.HasAssetFilterRuleName(item.FilterRuleName) ||
                        !BundleCollectorSettingData.HasAddressRuleName(item.AddressRuleName))
                    {
                        error = "Collector 规则无效：" + item.CollectPath;
                        return false;
                    }
                }
            }
            return true;
        }

        internal static bool BundleInputsEqual(BundleInputSnapshot expected, BundleInputSnapshot actual)
        {
            return expected != null && actual != null &&
                   string.Equals(Util.Json.Serialize(expected), Util.Json.Serialize(actual), StringComparison.Ordinal);
        }

        internal static bool TryValidateActiveBundleRuntime(
            BundleInputSnapshot expected,
            string packageVersion,
            out string error)
        {
            error = null;
            if (expected == null)
            {
                error = "Bundle 冻结输入为空。";
                return false;
            }
            BundleCollectorSetting activeCollector;
            try
            {
                activeCollector = BundleCollectorSettingData.Setting;
            }
            catch (Exception exception)
            {
                error = "当前 YooAsset Collector 无法加载：" + exception.Message;
                return false;
            }
            if (activeCollector == null ||
                !string.Equals(AssetDatabase.GetAssetPath(activeCollector), expected.bundleCollectorPath, StringComparison.Ordinal))
            {
                error = "YooAsset 当前 Collector 缓存未指向冻结的 BundleCollectorSetting。";
                return false;
            }

            string expectedManifestName = string.IsNullOrEmpty(expected.packageFilePrefix)
                ? $"{expected.packageName}_{packageVersion}.bytes"
                : $"{expected.packageFilePrefix}_{expected.packageName}_{packageVersion}.bytes";
            if (!string.Equals(YooAssetConfiguration.GetYooFolderName(), expected.yooFolderName, StringComparison.Ordinal) ||
                !string.Equals(
                    YooAssetConfiguration.GetManifestBinaryFileName(expected.packageName, packageVersion),
                    expectedManifestName,
                    StringComparison.Ordinal))
            {
                error = "YooAssetConfiguration 当前缓存未指向冻结的 YooAssetSettings。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 将 Bundle 加密逻辑值映射为固定正式类型；拒绝类型名、别名及未声明的方案。
        /// </summary>
        internal static bool TryResolveEncryptionPolicy(string value, out string bundleEncryptorClassName, out string error)
        {
            Type type;
            switch (value)
            {
                case NoEncryptionPolicy: type = typeof(EncryptionNone); break;
                case "offset": type = typeof(ResourceOffsetEncryptor); break;
                case "xor": type = typeof(ResourceXorEncryptor); break;
                case "aes-memory": type = typeof(ResourceAesMemoryEncryptor); break;
                case "aes-stream": type = typeof(ResourceAesStreamEncryptor); break;
                default:
                    bundleEncryptorClassName = null;
                    error = "encryptionPolicy 仅允许 none、offset、xor、aes-memory 或 aes-stream；不接受 C# 类型名或别名。";
                    return false;
            }
            bundleEncryptorClassName = type.FullName;
            error = null;
            return true;
        }

        /// <summary>
        /// 将可选清单加密逻辑值映射为固定配对类型；省略或 null 使用 none，显式无效值阻断。
        /// </summary>
        internal static bool TryResolveManifestEncryptionPolicy(
            string value,
            out string policy,
            out string manifestEncryptorClassName,
            out string manifestDecryptorClassName,
            out string error)
        {
            policy = value ?? NoEncryptionPolicy;
            Type encryptorType;
            Type decryptorType;
            switch (policy)
            {
                case NoEncryptionPolicy:
                    encryptorType = typeof(ManifestEncryptorNone);
                    decryptorType = typeof(ManifestDecryptorNone);
                    break;
                case "aes":
                    encryptorType = typeof(ResourceManifestAesEncryptor);
                    decryptorType = typeof(ResourceManifestAesDecryptor);
                    break;
                case "xor":
                    encryptorType = typeof(ResourceManifestXorEncryptor);
                    decryptorType = typeof(ResourceManifestXorDecryptor);
                    break;
                default:
                    policy = null;
                    manifestEncryptorClassName = null;
                    manifestDecryptorClassName = null;
                    error = "manifestEncryptionPolicy 仅允许 none、aes 或 xor；不接受 C# 类型名或别名。";
                    return false;
            }
            manifestEncryptorClassName = encryptorType.FullName;
            manifestDecryptorClassName = decryptorType.FullName;
            error = null;
            return true;
        }

        internal static bool TryResolveCompression(string value, out ECompressOption result, out string error)
        {
            switch (value)
            {
                case "uncompressed": result = ECompressOption.Uncompressed; error = null; return true;
                case "lzma": result = ECompressOption.LZMA; error = null; return true;
                case "lz4": result = ECompressOption.LZ4; error = null; return true;
                default: result = default; error = "compression 仅允许 uncompressed、lzma 或 lz4。"; return false;
            }
        }

        internal static bool TryResolveFileNameStyle(string value, out EFileNameStyle result, out string error)
        {
            switch (value)
            {
                case "hash-name": result = EFileNameStyle.HashName; error = null; return true;
                case "bundle-name": result = EFileNameStyle.BundleName; error = null; return true;
                case "bundle-name-hash-name": result = EFileNameStyle.BundleName_HashName; error = null; return true;
                default: result = default; error = "fileNameStyle 仅允许 hash-name、bundle-name 或 bundle-name-hash-name。"; return false;
            }
        }

        internal static bool TryResolveBundledCopyOption(
            string value,
            out EBundledCopyOption result,
            out string error)
        {
            switch (value)
            {
                case "none": result = EBundledCopyOption.None; error = null; return true;
                case "clear-and-copy-all": result = EBundledCopyOption.ClearAndCopyAll; error = null; return true;
                case "clear-and-copy-by-tags": result = EBundledCopyOption.ClearAndCopyByTags; error = null; return true;
                case "only-copy-all": result = EBundledCopyOption.OnlyCopyAll; error = null; return true;
                case "only-copy-by-tags": result = EBundledCopyOption.OnlyCopyByTags; error = null; return true;
                default:
                    result = default;
                    error = "bundledCopyOption 不是允许的逻辑值。";
                    return false;
            }
        }

        internal static bool RequiresTags(EBundledCopyOption option)
        {
            return option == EBundledCopyOption.ClearAndCopyByTags || option == EBundledCopyOption.OnlyCopyByTags;
        }

        internal static string ResolveBundleOutput(BuildTarget target, string packageName, string version)
        {
            return Path.GetFullPath(Path.Combine(
                BundleBuilderHelper.GetDefaultBuildOutputRoot(),
                target.ToString(),
                packageName,
                version));
        }

        internal static string ResolveBundleManifest(string outputPath, string packageName, string version)
        {
            return Path.Combine(outputPath, YooAssetConfiguration.GetManifestBinaryFileName(packageName, version));
        }

        internal static string ResolveBundleManifest(
            string outputPath,
            string packageName,
            string version,
            string packageFilePrefix)
        {
            string fileName = string.IsNullOrEmpty(packageFilePrefix)
                ? $"{packageName}_{version}.bytes"
                : $"{packageFilePrefix}_{packageName}_{version}.bytes";
            return Path.Combine(outputPath, fileName);
        }

        internal static string ResolveStreamingAssetsRoot()
        {
            string path = BundleBuilderHelper.GetStreamingAssetsRoot();
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(ProjectRoot, path));
        }

        internal static string ResolveBundledPackageRoot(string packageName)
        {
            return Path.Combine(ResolveStreamingAssetsRoot(), packageName);
        }

        /// <summary>只读核对正式产物、共享写集及真实回调凭据，不预读依赖 XML 或重放构建与解析。</summary>
        /// <param name="expected">执行前冻结路径、输入、共享写集及本次真实回调凭据；旧 Player 路径可无 Bundle 字段。</param>
        /// <param name="buildReportResult">实际领域构建结果字符串，仅用于保存证据，不代替产物与回调后验。</param>
        /// <param name="cancellationToken">只读核对源输入、报告与产物哈希的取消令牌。</param>
        /// <returns>返回已完整捕获的产物回执；缺失、漂移或回调证据不足时抛出异常，不重放构建或解析。</returns>
        internal static ArtifactReceipt CaptureArtifact(
            ArtifactReceipt expected,
            string buildReportResult,
            CancellationToken cancellationToken)
        {
            if (expected == null || string.IsNullOrWhiteSpace(expected.outputPath))
            {
                throw new InvalidOperationException("Build Receipt 缺少 outputPath。");
            }

            var receipt = new ArtifactReceipt
            {
                finalized = true,
                target = expected.target,
                outputPath = Path.GetFullPath(expected.outputPath),
                manifestPath = string.IsNullOrWhiteSpace(expected.manifestPath)
                    ? null
                    : Path.GetFullPath(expected.manifestPath),
                bundledOutputPath = string.IsNullOrWhiteSpace(expected.bundledOutputPath)
                    ? null
                    : Path.GetFullPath(expected.bundledOutputPath),
                packageName = expected.packageName,
                packageVersion = expected.packageVersion,
                scenes = expected.scenes == null ? null : (string[])expected.scenes.Clone(),
                developmentBuild = expected.developmentBuild,
                buildMode = expected.buildMode,
                buildReportResult = buildReportResult,
                bundleWrites = expected.bundleWrites,
                bundleInputs = expected.bundleInputs,
                callbackExecution = expected.callbackExecution,
            };

            if (File.Exists(receipt.outputPath))
            {
                receipt.outputKind = "file";
                receipt.outputHashSha256 = ComputeFileHash(receipt.outputPath, cancellationToken);
            }
            else if (Directory.Exists(receipt.outputPath))
            {
                receipt.outputKind = "directory";
                receipt.outputHashSha256 = ComputeDirectoryHash(receipt.outputPath, cancellationToken);
            }
            else
            {
                throw new FileNotFoundException("构建产物不存在。", receipt.outputPath);
            }

            if (!string.IsNullOrWhiteSpace(receipt.manifestPath))
            {
                if (!File.Exists(receipt.manifestPath))
                {
                    throw new FileNotFoundException("构建 manifest 不存在。", receipt.manifestPath);
                }
                receipt.manifestHashSha256 = ComputeFileHash(receipt.manifestPath, cancellationToken);
            }
            if (!string.IsNullOrWhiteSpace(receipt.bundledOutputPath))
            {
                if (!Directory.Exists(receipt.bundledOutputPath))
                {
                    throw new DirectoryNotFoundException("BundledCopy 目标目录不存在：" + receipt.bundledOutputPath);
                }
                receipt.bundledOutputHashSha256 = ComputeDirectoryHash(receipt.bundledOutputPath, cancellationToken);
            }
            if (!string.IsNullOrEmpty(receipt.packageName))
            {
                if (receipt.bundleWrites == null || receipt.bundleInputs == null) throw new InvalidOperationException("Bundle Receipt 缺少执行前冻结的共享写集或输入。");
                RecordPreservedSceneState(receipt, cancellationToken);
                if (receipt.preservedSceneInputsUnchanged == false) throw new InvalidOperationException("Bundle 执行前源资产或场景 setup 未保持：" + receipt.preservedSceneInputError);
                if (!Enum.TryParse(receipt.target, out BuildTarget target) ||
                    !string.Equals(receipt.outputPath, ResolveBundleOutput(target, receipt.packageName, receipt.packageVersion), StringComparison.Ordinal))
                    throw new InvalidOperationException("Bundle Receipt 输出不等于冻结 Target、Package 与版本的正式目录。");
                bool raw = receipt.bundleWrites.bundleType == (int)EBundleType.RawBundle;
                if (!TryCaptureBundleWrites(target, receipt.packageName, receipt.packageVersion, raw, !string.IsNullOrEmpty(receipt.bundleWrites.dependencyDatabasePath), receipt.bundleInputs,
                        cancellationToken, out BundleWriteSnapshot currentWrites, out string writeError, false) ||
                    !BundleWritesEqualAfter(receipt.bundleWrites, currentWrites, receipt.callbackExecution))
                    throw new InvalidOperationException("Bundle 共享路径、回调或当前脏资产已漂移：" + writeError);
                if (receipt.bundleWrites.callbacks?.firebaseCrashlytics != null) ValidateFirebaseExecution(receipt.callbackExecution, cancellationToken);
                if (receipt.bundleWrites.callbacks?.edmScene != null) ValidateEdmSceneExecution(receipt.callbackExecution?.edmScene);
                if (!TryCaptureBundleInputs(receipt.packageName, cancellationToken, out BundleInputSnapshot currentInputs, out string inputError) ||
                    !BundleInputsEqual(receipt.bundleInputs, currentInputs))
                    throw new InvalidOperationException("Bundle 执行后冻结输入已漂移：" + inputError);
                ValidateBundleReport(receipt, cancellationToken);
                receipt.bundleAuxiliaryArtifacts = CaptureBundleAuxiliaryArtifacts(receipt.bundleWrites, cancellationToken);
            }
            return receipt;
        }

        /// <summary>只读核对本次正式报告的 Target、Package、版本、管线与报告引用的全部 Bundle 文件。</summary>
        /// <param name="receipt">本次正式产物和绑定缓存证据。</param>
        /// <param name="cancellationToken">只读报告和 Bundle 文件核验取消令牌。</param>
        internal static void ValidateBundleReport(ArtifactReceipt receipt, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SbpSceneCacheEvidence.Receipt cache = receipt.callbackExecution?.edmScene?.cacheEvidence;
            if (cache != null && (cache.outputPath != receipt.outputPath || cache.packageName != receipt.packageName || cache.version != receipt.packageVersion))
                throw new InvalidOperationException("缓存零回调证据不能借用另一输出目录、Package 或版本的报告。");
            if (receipt.bundleWrites == null || !Enum.TryParse(receipt.target, out BuildTarget target))
                throw new InvalidOperationException("Bundle 报告核验缺少冻结 Target 或管线。");
            string reportPath = Path.Combine(receipt.outputPath, YooAssetConfiguration.GetBuildReportFileName(receipt.packageName, receipt.packageVersion));
            if (!GenerateActionCommon.TryResolveProjectPath(reportPath, "YooAsset report", out reportPath, out string pathError))
                throw new InvalidOperationException(pathError);
            if (!File.Exists(reportPath)) throw new FileNotFoundException("本次 YooAsset 构建报告不存在。", reportPath);
            YooAsset.Editor.BuildReport report = YooAsset.Editor.BuildReport.Deserialize(File.ReadAllText(reportPath));
            ReportSummary summary = report?.Summary;
            if (summary == null || summary.BuildTarget != target || summary.BuildPackageName != receipt.packageName ||
                summary.BuildPackageVersion != receipt.packageVersion || summary.BuildPipeline != receipt.bundleWrites.buildPipeline ||
                summary.BuildBundleType != receipt.bundleWrites.bundleType)
                throw new InvalidOperationException("YooAsset 构建报告与冻结 Target、Package、版本或管线不一致。");
            if (report.BundleInfos == null || report.BundleInfos.Count == 0 || summary.AllBundleTotalCount != report.BundleInfos.Count)
                throw new InvalidOperationException("YooAsset 构建报告 Bundle 集合为空或总数不一致。");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (ReportBundleInfo bundle in report.BundleInfos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (bundle == null || string.IsNullOrEmpty(bundle.FileName) || Path.GetFileName(bundle.FileName) != bundle.FileName ||
                    !names.Add(bundle.FileName) || !File.Exists(Path.Combine(receipt.outputPath, bundle.FileName)) ||
                    new FileInfo(Path.Combine(receipt.outputPath, bundle.FileName)).Length != bundle.FileSize)
                    throw new InvalidOperationException("YooAsset 报告引用 Bundle 的路径、唯一性、存在性或大小不一致。");
            }
        }

        internal static AgentActionResult VerifyArtifact(
            string receiptJson,
            string actionLabel,
            CancellationToken cancellationToken)
        {
            ArtifactReceipt expected;
            try
            {
                if (!BuildArtifactReceiptCodec.TryDecode(receiptJson, out expected, out string decodeError))
                    return AgentActionResult.Create(null, "blocked", actionLabel + " Receipt 无法解析：" + decodeError);
            }
            catch (Exception exception)
            {
                return AgentActionResult.Create(null, "blocked", actionLabel + " Receipt 无法解析：" + exception.Message);
            }

            try
            {
                if (expected == null || !expected.finalized)
                {
                    return AgentActionResult.Create(null, "partial", actionLabel + " Recovery Receipt 尚未包含执行后产物证据。" );
                }
                if (!string.Equals(expected.buildReportResult, "Succeeded", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(expected.outputKind) ||
                    string.IsNullOrWhiteSpace(expected.outputHashSha256) ||
                    !string.IsNullOrWhiteSpace(expected.manifestPath) && string.IsNullOrWhiteSpace(expected.manifestHashSha256) ||
                    !string.IsNullOrWhiteSpace(expected.bundledOutputPath) && string.IsNullOrWhiteSpace(expected.bundledOutputHashSha256))
                {
                    return AgentActionResult.Create(null, "partial", actionLabel + " Receipt 缺少完整的执行后 Hash 或 Succeeded 证据。" );
                }
                if (!string.IsNullOrEmpty(expected.packageName))
                {
                    if (expected.bundleWrites == null || expected.bundleInputs == null || expected.bundleAuxiliaryArtifacts == null)
                        return AgentActionResult.Create(null, "partial", actionLabel + " Receipt 缺少共享写集与执行后辅助产物证据。");
                    if (!GenerateActionCommon.TryVerifyArtifacts(expected.bundleAuxiliaryArtifacts, cancellationToken, out _, out string auxiliaryError))
                        return AgentActionResult.Create(null, "partial", actionLabel + " 共享输出已漂移或不完整：" + auxiliaryError);
                }
                ArtifactReceipt actual = CaptureArtifact(expected, expected?.buildReportResult, cancellationToken);
                if (!string.IsNullOrEmpty(expected.packageName) &&
                    Util.Json.Serialize(expected.bundleAuxiliaryArtifacts) != Util.Json.Serialize(actual.bundleAuxiliaryArtifacts))
                    return AgentActionResult.Create(null, "partial", actionLabel + " Receipt 的辅助产物集合与冻结共享写集不一致。");
                if (!string.IsNullOrWhiteSpace(expected.outputKind) && expected.outputKind != actual.outputKind)
                {
                    return AgentActionResult.Create(null, "partial", actionLabel + " 产物类型已变化。" );
                }
                if (!RequiredHashMatches(expected.outputHashSha256, actual.outputHashSha256) ||
                    !OptionalHashMatches(expected.manifestHashSha256, actual.manifestHashSha256) ||
                    !OptionalHashMatches(expected.bundledOutputHashSha256, actual.bundledOutputHashSha256))
                {
                    AgentActionResult changed = AgentActionResult.Create(null, "partial", actionLabel + " 产物或 manifest Hash 与 Receipt 不一致。");
                    changed.DataJson = Util.Json.Serialize(actual);
                    changed.Artifacts.Add(actual.outputPath);
                    return changed;
                }

                AgentActionResult success = AgentActionResult.Create(null, "success", actionLabel + " 产物路径、类型、manifest 与 SHA-256 已只读核对。");
                success.DataJson = Util.Json.Serialize(actual);
                success.EvidenceKinds = AgentActionEvidence.Artifact;
                success.Artifacts.Add(actual.outputPath);
                if (!string.IsNullOrWhiteSpace(actual.manifestPath)) success.Artifacts.Add(actual.manifestPath);
                if (!string.IsNullOrWhiteSpace(actual.bundledOutputPath)) success.Artifacts.Add(actual.bundledOutputPath);
                if (actual.bundleAuxiliaryArtifacts != null)
                {
                    success.Artifacts.AddRange(actual.bundleAuxiliaryArtifacts.Select(artifact => artifact.path));
                    success.Evidence.Add("已核对 YooAsset 报告的目标、包名、版本、管线及引用 Bundle；共享 OutputCache、脚本输出与可选依赖数据库 Hash 一致。");
                    success.Warnings.Add("共享 SBP / Unity 缓存可增写与后台清理，仅声明写入范围，不提供整个缓存完整性证据。");
                }
                success.Evidence.Add("Verify 仅只读检查文件/目录存在性、manifest 与 SHA-256；不会重放构建。" );
                if (!string.IsNullOrWhiteSpace(actual.buildReportResult))
                {
                    success.Evidence.Add("BuildReport 只证明 Unity 构建完成，不证明 Player 已启动或业务运行正确。" );
                }
                else
                {
                    success.Warnings.Add("RecoveryPayload 没有 BuildReport 结果；当前只证明期望路径上的产物状态满足约束。" );
                }
                return success;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return AgentActionResult.Create(null, "partial", actionLabel + " 产物验证未通过：" + exception.Message);
            }
        }

        internal static bool SceneSnapshotsEqual(string[] expected, string[] actual)
        {
            return (expected ?? Array.Empty<string>()).SequenceEqual(actual ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        internal static string[] GetEnabledBuildScenes()
        {
            return EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        }

        private static bool RequiredHashMatches(string expected, string actual)
        {
            return !string.IsNullOrWhiteSpace(expected) && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        private static bool OptionalHashMatches(string expected, string actual)
        {
            return string.IsNullOrWhiteSpace(expected)
                ? string.IsNullOrWhiteSpace(actual)
                : string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeAssetHash(UnityEngine.Object asset, string assetPath, CancellationToken cancellationToken)
        {
            string absolutePath = Path.Combine(ProjectRoot, NormalizeAssetPath(assetPath));
            string fileHash = File.Exists(absolutePath) ? ComputeFileHash(absolutePath, cancellationToken) : string.Empty;
            string serialized = asset == null ? string.Empty : EditorJsonUtility.ToJson(asset, false);
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(fileHash + "\n" + serialized)));
            }
        }

        private static string NormalizeAssetPath(string value)
        {
            return string.IsNullOrEmpty(value) ? value : value.Replace('\\', '/');
        }

        private static string ComputeFileHash(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] buffer = new byte[1024 * 1024];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sha256.AppendData(buffer, 0, read);
                }
                return ToHex(sha256.GetHashAndReset());
            }
        }

        private static string ComputeDirectoryHash(string directory, CancellationToken cancellationToken)
        {
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            using (IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    string entry = relative + "\n" + new FileInfo(file).Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                                   ComputeFileHash(file, cancellationToken) + "\n";
                    byte[] bytes = Encoding.UTF8.GetBytes(entry);
                    sha256.AppendData(bytes);
                }
                return ToHex(sha256.GetHashAndReset());
            }
        }

        private static string ToHex(byte[] bytes)
        {
            return bytes == null ? null : string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
