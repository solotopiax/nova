/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerBuildAction.AndroidGenerateAdapter.cs
 * author:    taoye
 * created:   2026/10/20
 * descrip:   Android Generate的独立生产接线、真实观察与未闭合义务
 ***************************************************************/
using System;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using global::HybridCLR.Editor;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class PlayerBuildAction
    {
        /// <summary>Android专属完整计划；源码准入与本次DLL/MVID身份明确分开，未闭合项不能由外部请求清空。</summary>
        [Serializable] internal sealed class AndroidGeneratePlan
        {
            public string masterPath;
            public string masterLinkXml;
            public string output;
            public string previousReportGuid;
            public string recoveryDirectory;
            public string[] scenes;
            public string[] unboundContracts;
            public AndroidGenerateCallbackContract.Snapshot callbacks;
            public AndroidGenerateProviderContract.Snapshot provider;
            public AndroidGenerateToolchainContract.Snapshot toolchain;
            public AndroidGenerateNativePreviewContract.Plan nativePreview;
            public AndroidGenerateAppsFlyerScope.Plan appsFlyer;
            public NovaAndroidParentBuildContract.FactorySnapshot factory;
            public NovaAndroidSdkLeafBuildContract.ConfigurationRow[] configurationRows;
            public HybridClrStripBuildContract.Snapshot stripBefore;
            public HybridClrGenerateInputProjection.Spec owned;
            public PlayerInputSnapshot inputs;
            public FileState[] auditorBaseline;
            public GenerateSceneVisibilityContract.Snapshot sceneVisibilityBefore;
        }

        /// <summary>一次原GenerateAll产生的实际父阶段、Gradle、报告、源与输出材料；不从后来current补历史。</summary>
        [Serializable] internal sealed class AndroidGenerateEvidence
        {
            public bool complete;
            public bool chainEntered;
            public bool chainReturned;
            public bool cancelled;
            public string error;
            public string certificate;
            public AndroidGeneratePlan before;
            public HybridClrStripBuildContract.Snapshot stripAfter;
            public HybridClrStripGradleScope.Observation gradle;
            public NovaAndroidParentBuildObserverScope.Evidence parent;
            public AndroidGenerateNativePreviewScope.Evidence nativePreview;
            public AndroidGenerateAppsFlyerScope.Evidence appsFlyer;
            public AndroidGenerateProviderContract.Snapshot providerAfter;
            public AndroidGenerateCallbackContract.Snapshot callbacksAfter;
            public AndroidGenerateToolchainContract.ExportSelection toolchainAtGradle;
            public AndroidGenerateToolchainContract.ExportSelection toolchainAfter;
            public PlayerInputSnapshot inputsAfter;
            public GenerateSceneVisibilityContract.Snapshot sceneVisibilityAfter;
            public GenerateSceneVisibilityContract.Snapshot sceneVisibilityObserved;
            public string reportGuid;
            public string reportPlatform;
            public string reportOutput;
            public int reportOptions;
            public int reportErrors;
            public string reportResult;
            public int logErrors;
            public FileState[] ownedOutputs;
            public string[] generatedAssetIdentities;
            public ReportCopyReceipt reportCopy;
        }

        /// <summary>从当前Android完整工厂、原provider和全部回调建立专属计划；未知项进入结构化阻碍，不借WebGL路径准入。</summary>
        /// <param name="masterPath">激活Master准确路径。</param><param name="masterLinkXml">当前Master的AOT保留表。</param><param name="scenes">真实启用场景有序集合。</param><param name="token">本次取消令牌。</param><returns>包含准确未闭合项的生产计划；只有全部合同闭合才捕获可执行源闭包。</returns>
        internal static AndroidGeneratePlan CaptureForAndroidHybridClrGenerate(string masterPath, string masterLinkXml, string[] scenes, CancellationToken token)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android || scenes == null || scenes.Length == 0)
                throw new InvalidOperationException("Android Generate必须冻结真实Android与非空启用场景。");
            var factory = NovaAndroidParentBuildContract.CaptureReviewedFactory(token);
            AndroidGenerateProviderContract.Snapshot provider = AndroidGenerateProviderContract.Capture(masterPath, token);
            AndroidGenerateCallbackContract.Snapshot callbacks = AndroidGenerateCallbackContract.Capture(token);
            AndroidGenerateToolchainContract.Snapshot toolchain = null; string toolchainFailure = null;
            try { toolchain = AndroidGenerateToolchainContract.Capture(token); }
            catch (Exception toolchainError) when (!(toolchainError is OperationCanceledException)) { toolchainFailure = "Android.Toolchain:" + toolchainError.Message; }
            AndroidGenerateNativePreviewContract.Plan native = null; string nativeFailure = null;
            try { native = AndroidGenerateNativePreviewContract.Capture(scenes, token); }
            catch (Exception nativeError) when (!(nativeError is OperationCanceledException)) { nativeFailure = "Android.NativePreview:" + nativeError.Message; }
            AndroidGenerateAppsFlyerScope.Plan appsFlyer = null; string appsFlyerFailure = null;
            try { appsFlyer = AndroidGenerateAppsFlyerScope.CapturePlan(factory, token); }
            catch (Exception appsFlyerError) when (!(appsFlyerError is OperationCanceledException)) { appsFlyerFailure = "Android.AppsFlyer:" + appsFlyerError.Message; }
            string selected = new CustomAndroidManifest(Path.Combine(BuildActionCommon.ProjectRoot, NovaAndroidParentBuildContract.TemplatePath))
                .SelectUnityLauncherActivity(PlayerSettings.Android.applicationEntry);
            string[] unbound; NovaAndroidSdkLeafBuildContract.ConfigurationRow[] rows;
            using (var configurations = NovaAndroidSdkLeafBuildContract.CaptureReviewedConfigurations(factory, provider))
            {
                var models = native == null ? NovaAndroidSdkLeafBuildContract.Predict(factory, configurations, selected) : NovaAndroidSdkLeafBuildContract.PredictReviewedGenerate(factory, configurations, selected, native);
                unbound = native == null ? NovaAndroidSdkLeafBuildContract.DescribeReviewedUnboundContracts(factory, models, provider) : NovaAndroidSdkLeafBuildContract.DescribeReviewedGenerateUnboundContracts(factory, models, provider, native);
                rows = configurations.rows;
            }
            BuildReport previous = BuildReport.GetLatestReport();
            var plan = new AndroidGeneratePlan { masterPath = masterPath, masterLinkXml = masterLinkXml, scenes = scenes.ToArray(), factory = factory,
                provider = provider, callbacks = callbacks, toolchain = toolchain, nativePreview = native, appsFlyer = appsFlyer, configurationRows = rows, stripBefore = HybridClrStripBuildContract.CaptureAndroid(),
                output = AndroidGenerateOutput(), previousReportGuid = previous == null ? null : previous.summary.guid.ToString(),
                recoveryDirectory = PhysicalPath("Library/Nova/AgentActions/HybridCLRGenerateAndroid/" + Guid.NewGuid().ToString("N") + "/report-backup"),
                unboundContracts = callbacks.unsupported.Select(value => "PlayerCallback:" + value).Concat(unbound)
                    .Concat(toolchainFailure == null ? Array.Empty<string>() : new[] { toolchainFailure }).Concat(nativeFailure == null ? Array.Empty<string>() : new[] { nativeFailure })
                    .Concat(appsFlyerFailure == null ? Array.Empty<string>() : new[] { appsFlyerFailure }).Concat(new[] { AndroidGenerateAppsFlyerScope.TailUnbound }).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray() };
            if (plan.unboundContracts.Length != 0) return plan;
            var projection = CreateAndroidGenerateProjection(masterLinkXml, scenes.Concat(new[] { masterPath, provider.runtimePath }).Select(PhysicalPath).ToArray());
            var svs = GenerateSceneVisibilityContract.CaptureInitial(); plan.sceneVisibilityBefore = svs.baseline;
            if (!TryCaptureInputs(BuildTarget.Android, plan.output, EditorUserBuildSettings.development, token, out plan.inputs, out string error, null, projection, svs))
                throw new InvalidOperationException(error);
            plan.owned = projection.ExportSpec(); plan.auditorBaseline = CaptureAuditorBaseline(token); return plan;
        }

        /// <summary>比较两个新鲜Android计划，随机恢复目录不参加输入比较；全部语义输入和实际编译身份仍精确冻结。</summary>
        /// <param name="before">原计划。</param><param name="current">同域新捕获。</param><returns>完整业务输入一致时为true。</returns>
        internal static bool AndroidGeneratePlansEqual(AndroidGeneratePlan before, AndroidGeneratePlan current)
        {
            if (before == null || current == null) return false;
            return Util.Json.Serialize(new { before.masterPath, before.masterLinkXml, before.output, before.previousReportGuid, before.scenes, before.unboundContracts,
                before.callbacks, before.provider, before.toolchain, before.nativePreview, before.appsFlyer, before.factory, before.configurationRows, before.stripBefore, before.owned, before.inputs, before.auditorBaseline, before.sceneVisibilityBefore }) ==
                Util.Json.Serialize(new { current.masterPath, current.masterLinkXml, current.output, current.previousReportGuid, current.scenes, current.unboundContracts,
                current.callbacks, current.provider, current.toolchain, current.nativePreview, current.appsFlyer, current.factory, current.configurationRows, current.stripBefore, current.owned, current.inputs, current.auditorBaseline, current.sceneVisibilityBefore });
        }

        /// <summary>在全部实际义务闭合后恰一次走原GenerateAll、原父observer和Gradle guard；任何异常保留真实已有材料，不重放。</summary>
        /// <param name="plan">Action保存的真实完整计划。</param><param name="token">原取消令牌。</param><returns>完整或失败的真实typed材料。</returns>
        internal static async UniTask<AndroidGenerateEvidence> ExecuteAndroidHybridClrGenerateAsync(AndroidGeneratePlan plan, CancellationToken token)
        {
            var evidence = new AndroidGenerateEvidence { before = plan }; ProjectAuditorReportScope auditor = null; BuildReport report = null;
            try
            {
                RequireAndroidGenerateExecutable(plan);
                if (!AndroidGeneratePlansEqual(plan, CaptureForAndroidHybridClrGenerate(plan.masterPath, plan.masterLinkXml, plan.scenes, token)))
                    throw new InvalidOperationException("Android Generate真实写前输入漂移。");
                using (var configurations = NovaAndroidSdkLeafBuildContract.CaptureReviewedConfigurations(plan.factory, plan.provider))
                {
                    string selected = new CustomAndroidManifest(Path.Combine(BuildActionCommon.ProjectRoot, NovaAndroidParentBuildContract.TemplatePath)).SelectUnityLauncherActivity(PlayerSettings.Android.applicationEntry);
                    var models = NovaAndroidSdkLeafBuildContract.PredictReviewedGenerate(plan.factory, configurations, selected, plan.nativePreview);
                    if (models.Any(model => !model.contextComplete) || NovaAndroidSdkLeafBuildContract.DescribeReviewedGenerateUnboundContracts(plan.factory, models, plan.provider, plan.nativePreview).Length != 0)
                        throw new InvalidOperationException("Android SDK真实叶子义务仍未闭合；不接受修改DTO清空required。");
                    var settings = new NovaAndroidParentBuildContract.Settings { bundleId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android),
                        versionCode = PlayerSettings.Android.bundleVersionCode.ToString(System.Globalization.CultureInfo.InvariantCulture), versionName = PlayerSettings.bundleVersion,
                        entry = PlayerSettings.Android.applicationEntry, orientation = PlayerSettings.defaultInterfaceOrientation };
                    NovaAndroidParentBuildObserverScope observedParent = null;
                    AndroidGenerateNativePreviewScope observedNative = null;
                    AndroidGenerateAppsFlyerScope observedAppsFlyer = null;
                    try
                    {
                    using (var prepared = NovaAndroidParentBuildContract.PrepareOutputs(BuildActionCommon.ProjectRoot, settings, models.Select(model => model.effect).ToArray(), token))
                    using (var appsFlyer = observedAppsFlyer = AndroidGenerateAppsFlyerScope.Begin(plan.appsFlyer, plan.factory, token))
                    using (var parent = observedParent = NovaAndroidParentBuildObserverScope.Begin(plan.factory, prepared, token))
                    using (var native = observedNative = AndroidGenerateNativePreviewScope.Begin(plan.nativePreview, plan.factory, token))
                    using (var gradle = HybridClrStripGradleScope.BeginAndroidGenerate(plan.output, plan.toolchain))
                    {
                        auditor = new ProjectAuditorReportScope(plan.auditorBaseline, plan.recoveryDirectory);
                        Application.LogCallback observer = (message, trace, kind) => { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) Interlocked.Increment(ref evidence.logErrors); };
                        Application.logMessageReceivedThreaded += observer;
                        try { token.ThrowIfCancellationRequested(); evidence.chainEntered = true; EditorUtil.HybridCLR.GenerateAll(); evidence.chainReturned = true;
                            gradle.RequireComplete(); evidence.gradle = gradle.Observed; evidence.parent = parent.CompleteObservation(); evidence.nativePreview = native.CompleteObservation(); evidence.appsFlyer = appsFlyer.CompleteObservation(); }
                        finally { Application.logMessageReceivedThreaded -= observer; evidence.gradle = gradle.Observed; evidence.toolchainAtGradle = gradle.ActualToolSelection; evidence.parent = parent.CaptureEvidence(); evidence.nativePreview = native.CaptureEvidence(); evidence.appsFlyer = appsFlyer.CaptureEvidence(); }
                    }
                    }
                    finally
                    {
                        if (observedParent != null) evidence.parent = observedParent.CaptureEvidence();
                        if (observedNative != null) evidence.nativePreview = observedNative.CaptureEvidence();
                        if (observedAppsFlyer != null) evidence.appsFlyer = observedAppsFlyer.CaptureEvidence();
                    }
                }
                AndroidGenerateNativePreviewContract.VerifyCompleted(plan.nativePreview, evidence.nativePreview, token);
                if (evidence.appsFlyer?.unboundContracts == null || evidence.appsFlyer.unboundContracts.Length != 0) throw new InvalidOperationException("AF真实同步写入不代替Generated资产/Manifest导入尾链闭合。");
                evidence.sceneVisibilityAfter = GenerateSceneVisibilityContract.Capture();
                evidence.stripAfter = HybridClrStripBuildContract.CaptureAndroid();
                if (evidence.logErrors != 0 || !HybridClrStripBuildContract.AfterEqual(plan.stripBefore, evidence.stripAfter)) throw new InvalidOperationException("Android Generate错误日志或官方StripAOT后态不符。");
                report = BuildReport.GetLatestReport();
                if (report == null) throw new InvalidOperationException("Android Generate没有新的正式BuildReport。");
                evidence.reportGuid = report.summary.guid.ToString(); evidence.reportPlatform = report.summary.platform.ToString(); evidence.reportOutput = report.summary.outputPath;
                evidence.reportOptions = (int)report.summary.options; evidence.reportErrors = report.summary.totalErrors; evidence.reportResult = report.summary.result.ToString();
                if (!ValidateAndroidGenerateReport(plan.previousReportGuid, evidence.reportGuid, evidence.reportPlatform, evidence.reportOutput, evidence.reportOptions, evidence.reportErrors, evidence.reportResult, plan.output, plan.stripBefore.explicitBuildOptions, out string error)) throw new InvalidOperationException(error);
                evidence.toolchainAfter = AndroidGenerateToolchainContract.CaptureExport(plan.toolchain, plan.output, token);
                if (evidence.toolchainAtGradle == null || Util.Json.Serialize(evidence.toolchainAtGradle) != Util.Json.Serialize(evidence.toolchainAfter)) throw new InvalidOperationException("Android本次Gradle版本/SDK在后续回调中变化。");
                EditorUtil.HybridCLR.ValidateLinkXml(); evidence.reportCopy = await auditor.CompleteAsync(report);
                if (evidence.reportCopy?.complete != true) throw new InvalidOperationException(evidence.reportCopy?.error ?? "Android正式报告复制未完成。");
                var projection = new HybridClrGenerateInputProjection(plan.owned, plan.scenes.Select(PhysicalPath));
                var svs = GenerateSceneVisibilityContract.ApproveAfter(plan.sceneVisibilityBefore, evidence.sceneVisibilityAfter, evidence.sceneVisibilityObserved = GenerateSceneVisibilityContract.Capture(), null);
                if (!TryCaptureInputs(BuildTarget.Android, plan.output, EditorUserBuildSettings.development, token, out evidence.inputsAfter, out error, null, projection, svs) || evidence.inputsAfter.sha256 != plan.inputs.sha256) throw new InvalidOperationException(error ?? "Android Generate拥有域外输入变化。");
                evidence.providerAfter = AndroidGenerateProviderContract.Capture(plan.masterPath, token); evidence.callbacksAfter = AndroidGenerateCallbackContract.Capture(token);
                if (!AndroidGenerateProviderContract.Equal(plan.provider, evidence.providerAfter) || Util.Json.Serialize(plan.callbacks) != Util.Json.Serialize(evidence.callbacksAfter)) throw new InvalidOperationException("Android原provider或准确回调集合变化。");
                evidence.ownedOutputs = CapturePaths(plan.owned.directories.Concat(plan.owned.files).Concat(plan.owned.files.Select(path => path + ".meta")), token);
                evidence.generatedAssetIdentities = CaptureGeneratedAssetIdentities(plan.owned); token.ThrowIfCancellationRequested(); auditor.Commit();
                evidence.complete = true; evidence.certificate = ComputeAndroidGenerateCertificate(evidence);
            }
            catch (Exception error)
            {
                evidence.complete = false; evidence.certificate = null;
                evidence.cancelled = error is OperationCanceledException || error is AggregateException aggregate && aggregate.Flatten().InnerExceptions.Any(innerError => innerError is OperationCanceledException);
                evidence.error = error.ToString();
                if (auditor != null) evidence.reportCopy = await auditor.RecoverAsync(report, error.Message);
            }
            return evidence;
        }

        /// <summary>正式Verify只读取原历史和当前状态，重核原编译令牌的SVS首次域；不重建旧计划、SDK实例或回调。</summary>
        /// <param name="evidence">原完整typed材料。</param><param name="token">当前取消令牌。</param><param name="compilationToken">原编译令牌。</param><param name="payloadHash">原完整载荷摘要。</param><param name="error">首个不满足的义务。</param><returns>完整历史与当前持久义务均满足时为true。</returns>
        internal static bool VerifyAndroidHybridClrGenerate(AndroidGenerateEvidence evidence, CancellationToken token, string compilationToken, string payloadHash, out string error)
        {
            error = null;
            try
            {
                if (evidence?.complete != true || evidence.error != null || !evidence.chainEntered || !evidence.chainReturned || evidence.cancelled || evidence.logErrors != 0 || evidence.certificate != ComputeAndroidGenerateCertificate(evidence))
                    throw new InvalidOperationException("Android Generate历史未完成或证书漂移。");
                AndroidGeneratePlan plan = evidence.before; RequireAndroidGenerateExecutable(plan);
                AndroidGenerateNativePreviewContract.VerifyCompleted(plan.nativePreview, evidence.nativePreview, token);
                if (evidence.parent?.parentPhasesObserved != true || !HybridClrStripGradleScope.IsComplete(evidence.gradle, plan.output) || !HybridClrStripBuildContract.AfterEqual(plan.stripBefore, evidence.stripAfter) || !HybridClrStripBuildContract.BeforeEqual(evidence.stripAfter, HybridClrStripBuildContract.CaptureAndroid())) throw new InvalidOperationException("Android真实父阶段、Gradle或官方恢复证据不足。");
                if (!ValidateAndroidGenerateReport(plan.previousReportGuid, evidence.reportGuid, evidence.reportPlatform, evidence.reportOutput, evidence.reportOptions, evidence.reportErrors, evidence.reportResult, plan.output, plan.stripBefore.explicitBuildOptions, out error)) return false;
                if (evidence.toolchainAtGradle == null || Util.Json.Serialize(evidence.toolchainAtGradle) != Util.Json.Serialize(evidence.toolchainAfter) || Util.Json.Serialize(evidence.toolchainAfter) != Util.Json.Serialize(AndroidGenerateToolchainContract.CaptureExport(plan.toolchain, plan.output, token))) throw new InvalidOperationException("Android重载后同次实际导出工具选择漂移。");
                var svs = GenerateSceneVisibilityContract.ApproveAfterCompilationObserved(plan.sceneVisibilityBefore, evidence.sceneVisibilityAfter, evidence.sceneVisibilityObserved, GenerateSceneVisibilityContract.Capture(), null, compilationToken, payloadHash);
                var projection = new HybridClrGenerateInputProjection(plan.owned, plan.scenes.Select(PhysicalPath));
                if (!TryCaptureInputs(BuildTarget.Android, plan.output, EditorUserBuildSettings.development, token, out PlayerInputSnapshot inputs, out error, null, projection, svs) || inputs.sha256 != plan.inputs.sha256) throw new InvalidOperationException(error ?? "Android重载后源闭包漂移。");
                if (!AndroidGenerateProviderContract.Equal(evidence.providerAfter, AndroidGenerateProviderContract.Capture(plan.masterPath, token)) || Util.Json.Serialize(evidence.callbacksAfter) != Util.Json.Serialize(AndroidGenerateCallbackContract.Capture(token))) throw new InvalidOperationException("Android重载后provider或准确回调漂移。");
                var factory = NovaAndroidParentBuildContract.CaptureReviewedFactory(token); factory.domain = plan.factory.domain;
                if (Util.Json.Serialize(factory) != Util.Json.Serialize(plan.factory) || Util.Json.Serialize(evidence.ownedOutputs) != Util.Json.Serialize(CapturePaths(plan.owned.directories.Concat(plan.owned.files).Concat(plan.owned.files.Select(path => path + ".meta")), token)) || !evidence.generatedAssetIdentities.SequenceEqual(CaptureGeneratedAssetIdentities(plan.owned), StringComparer.Ordinal)) throw new InvalidOperationException("Android重载后工厂、DLL/MVID或正式输出漂移。");
                return VerifyAuditorReceipt(evidence.reportCopy, out error);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { error = exception.Message; return false; }
        }

        /// <summary>正式报告只接受Android、本次新GUID、准确官方输出和显式options；fixture不构成实际构建证据。</summary>
        /// <param name="previousGuid">执行前最近报告。</param><param name="guid">本次正式GUID。</param><param name="platform">实际平台。</param><param name="output">实际输出。</param><param name="options">实际选项。</param><param name="errors">实际错误数。</param><param name="result">实际结果。</param><param name="expectedOutput">冻结输出。</param><param name="expectedOptions">官方显式选项。</param><param name="error">失败说明。</param><returns>全部准确绑定时为true。</returns>
        internal static bool ValidateAndroidGenerateReport(string previousGuid, string guid, string platform, string output, int options, int errors, string result, string expectedOutput, int expectedOptions, out string error)
        {
            error = null;
            if (!Guid.TryParse(guid, out Guid parsed) || parsed == Guid.Empty || Guid.TryParse(previousGuid, out Guid previous) && previous == parsed || platform != BuildTarget.Android.ToString() || options != expectedOptions || errors != 0 || result != BuildResult.Succeeded.ToString()) error = "Android Generate报告GUID、平台、options、错误数或结果不符。";
            else
            {
                try { if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(expectedOutput) || Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar) != Path.GetFullPath(expectedOutput).TrimEnd(Path.DirectorySeparatorChar)) error = "Android Generate报告输出不是本次准确临时工程。"; }
                catch (ArgumentException) { error = "Android Generate报告输出路径无效。"; }
            }
            return error == null;
        }

        /// <summary>准确Android输出验证，独立于普通Player和WebGL输出规则。</summary>
        /// <param name="output">本次候选输出。</param><param name="projection">固定拥有域。</param><param name="error">失败说明。</param><returns>真实Android且输出准确时为true。</returns>
        private static bool TryValidateAndroidGenerateOutput(string output, HybridClrGenerateInputProjection projection, out string error)
        {
            error = null;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android || projection == null || string.IsNullOrWhiteSpace(output) || Path.GetFullPath(output) != AndroidGenerateOutput() || !projection.Owns(AndroidGenerateOutput())) { error = "Android Generate输出不属于官方准确临时工程。"; return false; }
            projection.ValidateOwnedTrees(CancellationToken.None); return true;
        }

        /// <summary>从实际Settings解析Android准确生成角色；只允许默认源码角色，不给其它源文件排除许可。</summary>
        /// <param name="linkXml">Master保留表。</param><param name="protectedFiles">必须保持的直接配置与场景。</param><returns>准确输出投影。</returns>
        private static HybridClrGenerateInputProjection CreateAndroidGenerateProjection(string linkXml, string[] protectedFiles)
        {
            var settings = SettingsUtil.HybridCLRSettings;
            if (settings.outputAOTGenericReferenceFile != "HybridCLRGenerate/AOTGenericReferences.cs" || settings.outputLinkFile != "HybridCLRGenerate/link.xml" || (settings.externalHotUpdateAssembliyDirs?.Length ?? 0) != 0) throw new InvalidOperationException("Android Generate自定义输出/外部热更解析目录尚未审查。");
            string generic = PhysicalPath("Assets/" + settings.outputAOTGenericReferenceFile); string owner = Path.GetFileNameWithoutExtension(CompilationPipeline.GetAssemblyNameFromScriptPath("Assets/" + settings.outputAOTGenericReferenceFile));
            var assembly = CompilationPipeline.GetAssemblies(AssembliesType.Player).SingleOrDefault(value => value.name == owner);
            if (assembly == null || !assembly.sourceFiles.Select(PhysicalPath).Contains(generic, StringComparer.Ordinal)) throw new InvalidOperationException("Android AOTReference缺准确编译源归属。");
            string library = PhysicalPath("Library/ScriptAssemblies/" + owner + ".dll");
            var spec = new HybridClrGenerateInputProjection.Spec { projectRoot = BuildActionCommon.ProjectRoot, compiledAssemblyName = owner,
                compiledArtifacts = new[] { library, Path.ChangeExtension(library, ".pdb"), PhysicalPath(assembly.outputPath), Path.ChangeExtension(PhysicalPath(assembly.outputPath), ".pdb") }.Distinct(StringComparer.Ordinal).ToArray(),
                directories = new[] { SettingsUtil.GetHotUpdateDllsOutputDirByTarget(BuildTarget.Android), SettingsUtil.GetAssembliesPostIl2CppStripDir(BuildTarget.Android), SettingsUtil.GeneratedCppDir, AndroidGenerateOutput() }.Select(Path.GetFullPath).ToArray(),
                files = new[] { generic, Path.GetFullPath(linkXml), PhysicalPath("Assets/" + settings.outputLinkFile), PhysicalPath(NovaAndroidParentBuildContract.ManifestPath), PhysicalPath(NovaAndroidParentBuildContract.ProguardPath), PhysicalPath("Assets/Plugins/Android/gradleTemplate.properties") }.Distinct(StringComparer.Ordinal).ToArray() };
            GetGenerateAssetParentMetas(spec); return new HybridClrGenerateInputProjection(spec, protectedFiles);
        }

        /// <summary>只从官方Settings派生Android临时工程位置，不使用请求路径。</summary>
        /// <returns>准确绝对路径。</returns>
        private static string AndroidGenerateOutput() => Path.GetFullPath(Path.Combine(SettingsUtil.HybridCLRDataDir, "StrippedAOTDllsTempProj", "Android"));

        /// <summary>重新核对不可由请求删除的生产义务；未闭合计划不进入任何写前证据或Generate。</summary>
        /// <param name="plan">原冻结计划。</param>
        private static void RequireAndroidGenerateExecutable(AndroidGeneratePlan plan)
        {
            if (plan?.unboundContracts == null || plan.unboundContracts.Length != 0 || plan.callbacks?.unsupported == null || plan.callbacks.unsupported.Length != 0 || plan.inputs == null || plan.owned == null || plan.factory == null || plan.provider == null || plan.toolchain == null || plan.nativePreview == null || plan.appsFlyer?.unboundContracts == null || plan.appsFlyer.unboundContracts.Length != 0 || plan.sceneVisibilityBefore == null)
                throw new InvalidOperationException("Android Generate完整typed执行合同尚未闭合。");
        }

        /// <summary>对完整真实材料计算编译前证书，排除证书字段自身。</summary>
        /// <param name="evidence">本次完整材料。</param><returns>完整JSON的SHA。</returns>
        internal static string ComputeAndroidGenerateCertificate(AndroidGenerateEvidence evidence) => GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new {
            evidence.complete, evidence.chainEntered, evidence.chainReturned, evidence.cancelled, evidence.error, evidence.before, evidence.stripAfter, evidence.gradle,
            evidence.parent, evidence.nativePreview, evidence.appsFlyer, evidence.providerAfter, evidence.callbacksAfter, evidence.toolchainAtGradle, evidence.toolchainAfter, evidence.inputsAfter, evidence.sceneVisibilityAfter, evidence.sceneVisibilityObserved,
            evidence.reportGuid, evidence.reportPlatform, evidence.reportOutput, evidence.reportOptions, evidence.reportErrors, evidence.reportResult, evidence.logErrors, evidence.ownedOutputs, evidence.generatedAssetIdentities, evidence.reportCopy }));
    }
}
