/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerPackageCallbacksContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   WebGL 正常包场景、Shader 与固定统计回调的有限合同
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    /// <summary>只读真实输入和官方正常内存后态；不预调用回调、不清缓存、不更改统计或设置。</summary>
    internal static class PlayerPackageCallbacksContract
    {
        private const string Prefix = "PlayerPackage=";
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly string Domain = Guid.NewGuid().ToString("N");

        /// <summary>固定 Shader 编译统计字典中每个原列表的完整顺序摘要，不输出 shader payload。</summary>
        [Serializable] internal sealed class CacheRow
        {
            public int keyIdentity;
            public int listIdentity;
            public string[] items;
            public int[] targets;
        }

        /// <summary>UGUI 官方缓存的准确四项计数和报告绑定字段，不记录场景回调次数。</summary>
        [Serializable] internal sealed class UguiState
        {
            public string guid;
            public int buildType;
            public int world;
            public int screen;
            public int overlay;
            public int tmp;
        }

        /// <summary>同域、配置、原容器与正常内存状态；完整 before 与归一化不变量分别校验。</summary>
        [Serializable] internal sealed class Snapshot
        {
            public bool applicable;
            public string domain;
            public string[] branches;
            public bool analyticsEnabled;
            public int qualityShadows;
            public string dependencySha256;
            public int proBuilderSettingsIdentity;
            public int auditorPreferenceIdentity;
            public bool auditorPreferenceExists;
            public string proBuilderRepositoriesSha256;
            public int[] features;
            public int expectedFeature;
            public int featureListIdentity;
            public int shaderDictionaryIdentity;
            public int computeDictionaryIdentity;
            public CacheRow[] shaderRows;
            public CacheRow[] computeRows;
            public string iapConfig;
            public string iapExpectedConfig;
            public UguiState ugui;
        }

        /// <summary>正式 BuildReport 的 GUID/type 和官方 UGUI 缓存实际值；只证明本地 payload，不声称网络送达。</summary>
        [Serializable] internal sealed class ReportMaterial
        {
            public string guid;
            public int buildType;
            public UguiState ugui;
            public bool analyticsEnabled;
        }

        /// <summary>按精确类型和属性方法登记本轮已审分支；未知类型返回 false，不按包或名称前缀放行。</summary>
        /// <param name="type">当前实际发现的回调类型。</param>
        /// <param name="label">准确属性方法或接口声明标签。</param>
        /// <param name="inputs">追加源码、DLL、配置门和分支 marker。</param>
        /// <param name="writes">追加官方正常内存效果与准确同值 importer 写集。</param>
        /// <param name="token">只读文件摘要取消令牌。</param>
        /// <returns>类型属于本合同且所有静态身份门满足时为 true；已识别但不满足时抛具体错误。</returns>
        internal static bool Prepare(Type type, string label, ICollection<string> inputs, ISet<string> writes, CancellationToken token)
        {
            string name = type.FullName;
            string branch;
            switch (name)
            {
                case "GooglePlayServices.PlayServicesResolver":
                    if (label != name + ".OnPostProcessScene [PostProcessScene]") return false;
                    RequireAssembly(type, "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0", inputs, token);
                    branch = "Edm"; break;
                case "UnityEditor.ProBuilder.UnityScenePostProcessor":
                    if (label != name + ".OnPostprocessScene [PostProcessScene]") return false;
                    RequireAssembly(type, "991abad7655ae754aa9a325f5558a081f1731fdb1ab7b966c2a31d2e5c5f877b", inputs, token);
                    branch = "ProBuilder"; break;
                case "UnityEditor.Purchasing.UnityPurchasingEditor":
                    if (label != name + ".OnPostProcessScene [PostProcessScene]") return false;
                    RequireAssembly(type, "01c75cc355b18e2fabe40ab5b22f8ce53a479058b65ebf6b1ae3d969a1b1ba69", inputs, token);
                    RequireAssembly(Find("UnityEngine.Purchasing.StoreConfiguration"), "7f5339d474cdbedbdc6a2a2428de9f79d708016cd6b87f00fe0758ab28effae2", inputs, token);
                    RequireSource(type, "com.unity.purchasing", "5.3.1", "Runtime/Purchasing/Legacy/Configurations/StoreConfiguration.cs", "893b6aa0c528f52aee9b5ad2f2e8b02a5b5a9dd5383ebb9ede6533d90876dc26", inputs, token);
                    branch = "Iap"; break;
                case "UnityEditor.Rendering.BuiltIn.ShaderPreprocessor":
                case "UnityEditor.Rendering.BuiltIn.ShaderBuildPreprocessor":
                    RequireAssembly(type, "9ef94b48ace11890751ac9e01a5acc48b390f17d51b45b6d7cc4cb4c49472479", inputs, token);
                    RequireSource(type, "com.unity.shadergraph", "17.4.0", "Editor/Generation/Targets/BuiltIn/Editor/ShaderPreprocessor.cs", "c24566d7b7db6dc63f4e14ae2ba050348d330ef876bf1374bb1ca31e503dde73", inputs, token);
                    // 此准确编译没有 PROFILE_BUILD 的 Profiler 文件/开关支路。
                    if (Find("UnityEditor.Rendering.BuiltIn.ShaderBuildPreprocessor").GetInterfaces().Any(value => value.FullName == "UnityEditor.Build.IPostprocessBuildWithReport")) throw new InvalidOperationException("ShaderGraph 存在 PROFILE_BUILD 生命周期，超出当前无 profiler 写入合同。");
                    branch = "ShaderGraph"; break;
                case "UnityEditor.Rendering.ShaderVariantStripper":
                case "UnityEditor.Rendering.ComputeShaderVariantStripper":
                case "UnityEditor.Rendering.ShaderStrippingReportScope":
                    RequireAssembly(type, "28f303678ed87a2127048483d2279d3bc8fdfe6d693929bf3c4106019de40a07", inputs, token);
                    RequireAssembly(Find("UnityEngine.Rendering.ShaderStrippingSetting"), "15ac5102e62a1934ce2f4080a6848e56c69affc8f469eadd39f4c47aa6d4ae00", inputs, token);
                    RequireSource(type, "com.unity.render-pipelines.core", "17.4.0", "Editor/ShaderStripping/ShaderStrippingReport.cs", "34f295546da78f48361f2037b8c1667d51a5df656df3fba8a11e76f7a22e5291", inputs, token);
                    branch = "ShaderCore"; break;
                case "UnityEditor.Timeline.Analytics.TimelineAnalytics+TimelineAnalyticsPreProcess":
                case "UnityEditor.Timeline.Analytics.TimelineAnalytics+TimelineAnalyticsProcess":
                case "UnityEditor.Timeline.Analytics.TimelineAnalytics+TimelineAnalyticsPostProcess":
                    RequireAssembly(type, "7024d0048860e9f3e15942f8c038db2434c7b132d3e51c3a44c8f0fc952a9909", inputs, token);
                    RequireSource(type, "com.unity.timeline", "1.8.13", "Editor/Analytics/TimelineAnalytics.cs", "3056ac2166b20cbd464c1583a1ab2f3c83568673688452c99dcb2b09425968cd", inputs, token);
                    branch = "Timeline"; break;
                case "UnityEditor.UI.Analytics.UGUIBuildAnalytics":
                    RequireAssembly(type, "949cdce0bfbc9af8d56dc25579a8e947fe99c77bd3991664543a058e33024239", inputs, token);
                    foreach (string relative in new[] { "Editor/Analytics/UGUIBuildAnalytics.cs", "Editor/Analytics/UGUIAnalytics.cs", "Editor/Analytics/IAnalytic/UGUIBuildEvent.cs" })
                    {
                        string expected = relative.EndsWith("/UGUIBuildAnalytics.cs", StringComparison.Ordinal) ? "c13d24e5739c2abf326db6c8fe6c1c3358f52938fbe5aa26646f9e7d8a0918ca" : relative.EndsWith("/UGUIAnalytics.cs", StringComparison.Ordinal) ? "c69f4c042f71b3e1894fd1476ff95af16b580790fad1122479cddb810500f15f" : "726f6eb65632a77a50574b6b8f029b06ab5313656be098f3be5272f499d26a8f";
                        RequireSource(type, "com.unity.ugui", "2.0.0", relative, expected, inputs, token);
                    }
                    RequireAssembly(typeof(EditorAnalytics), "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
                    branch = "Ugui"; break;
                case "Unity.ProjectAuditor.Editor.ProjectAuditor":
                case "Unity.ProjectAuditor.Editor.Modules.ShadersModule":
                    RequireAssembly(type, "2c20ea0b8555c4851b9885a2115f2499cecdea88ed0ef28ccdd530829113eac9", inputs, token);
                    branch = name.EndsWith(".ProjectAuditor", StringComparison.Ordinal) ? "Auditor" : "AuditorShaders"; break;
                default: return false;
            }
            RequireAssembly(typeof(GraphicsSettings), "25281ab07e11130a45ede1e02cf3003666f9616cca68ae8ab9637f0ccffa33ad", inputs, token);
            RequireAssembly(typeof(EditorApplication), "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", inputs, token);
            if (branch == "Edm" || branch == "ProBuilder" || branch == "Iap" || name == "UnityEditor.Rendering.BuiltIn.ShaderPreprocessor" || name == "UnityEditor.Rendering.ShaderVariantStripper" || name == "UnityEditor.Rendering.ComputeShaderVariantStripper")
                if (!BuildActionCommon.TryCapturePlayerSharedPackageCallback(type, label, inputs, token, out string error)) throw new InvalidOperationException(error);
            inputs.Add(Prefix + branch);
            writes.Add(branch + " official Player callback memory effects; no callback replay, state preparation or cache cleanup");
            if (branch == "Iap") writes.Add("IAP official StoreConfiguration replacement; selected billing-8.3.0.aar PluginImporter Android compatibility setter only at already equal value; original file/meta remain frozen");
            if (branch == "ShaderCore") writes.Add("ShaderStripping official Empty reporter creation/reset; export disabled and null global; no report JSON files");
            if (branch == "Ugui") writes.Add("UGUI official ugui_onBuild local payload: real BuildReport GUID/type + four observed counts; configured native analytics attempt/diagnostics, no delivery claim");
            return true;
        }

        /// <summary>从完整 Player 依赖闭包和现有 backing 冻结自然分支；不加载场景或主动创建 SDK 实例。</summary>
        /// <param name="inputs">准确回调 marker。</param>
        /// <param name="dependencyPaths">Player 已完整冻结的场景、Resources、配置、插件及递归依赖逻辑路径。</param>
        /// <returns>当前实际值；未知结构或非空业务分支拒绝。</returns>
        internal static Snapshot Capture(IEnumerable<string> inputs, string[] dependencyPaths)
        {
            string[] branches = inputs.Where(value => value.StartsWith(Prefix, StringComparison.Ordinal)).Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var state = new Snapshot { applicable = branches.Length != 0, branches = branches };
            if (!state.applicable) return state;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL || dependencyPaths == null) throw new InvalidOperationException("Player 包回调只支持真实 WebGL 与完整依赖闭包。");
            state.domain = Domain; state.analyticsEnabled = EditorAnalytics.enabled; state.qualityShadows = (int)QualitySettings.shadows;
            state.dependencySha256 = Hash(string.Join("\n", dependencyPaths));
            if (Has(state, "ProBuilder") || Has(state, "Timeline")) ValidateAbsentContent(state, dependencyPaths);
            if (Has(state, "Edm") && ((int)Read(Find("GooglePlayServices.PlayServicesResolver"), "scenesProcessed", null) != 0 || Read(Find("GooglePlayServices.PlayServicesResolver"), "gradleResolverInstance", null) != null)) throw new InvalidOperationException("非 Android EDM 场景不是当前自然零计数/null resolver；不造条件。");
            if (Has(state, "ProBuilder"))
            {
                object settings = Read(Find("UnityEditor.ProBuilder.ProBuilderSettings"), "s_Instance", null);
                if (settings?.GetType().FullName != "UnityEditor.SettingsManagement.Settings") throw new InvalidOperationException("ProBuilder 设置尚未自然初始化。");
                state.proBuilderSettingsIdentity = RuntimeHelpers.GetHashCode(settings);
                if (Read(settings.GetType(), "beforeSettingsSaved", settings) != null || Read(settings.GetType(), "afterSettingsSaved", settings) != null) throw new InvalidOperationException("ProBuilder Settings 保存事件存在未知下游。");
                Array repositories = Read(settings.GetType(), "m_SettingsRepositories", settings) as Array;
                if (repositories == null) throw new InvalidOperationException("ProBuilder repository backing 缺失。");
                state.proBuilderRepositoriesSha256 = Hash(RuntimeHelpers.GetHashCode(repositories) + "|" + string.Join("\n", repositories.Cast<object>().Select(value => value?.GetType().AssemblyQualifiedName + "|" + (value == null ? 0 : RuntimeHelpers.GetHashCode(value)))));
            }
            if (Has(state, "ShaderGraph") || Has(state, "ShaderCore")) CaptureShader(state);
            if (Has(state, "Timeline")) ValidateEmptyTimeline();
            if (Has(state, "Iap"))
            {
                Type callback = Find("UnityEditor.Purchasing.UnityPurchasingEditor");
                object config = Read(callback, "config", null);
                state.iapConfig = ReadIapConfig(config);
                string path = Path.Combine(BuildActionCommon.ProjectRoot, "Assets/Resources/BillingMode.json");
                if (File.Exists(path))
                {
                    string name = (string)Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path))["androidStore"];
                    Type store = Find("UnityEngine.Purchasing.AppStore");
                    state.iapExpectedConfig = Enum.IsDefined(store, name) ? name : "GooglePlay";
                }
            }
            if (Has(state, "Ugui"))
            {
                if (Read(typeof(EditorAnalytics).Assembly.GetType("UnityEditor.DebuggerEventListHandler", true), "analyticSent", null) != null) throw new InvalidOperationException("UGUI native analyticSent 有未审下游。");
                state.ugui = CaptureUgui();
            }
            if (Has(state, "Auditor")) ValidateAuditorDisabled(state);
            if (Has(state, "AuditorShaders"))
            {
                Type module = Find("Unity.ProjectAuditor.Editor.Modules.ShadersModule");
                state.shaderRows = CaptureCache(module, "s_ShaderVariantData", typeof(Shader), "Unity.ProjectAuditor.Editor.Modules.ShaderVariantData", out state.shaderDictionaryIdentity);
                state.computeRows = CaptureCache(module, "s_ComputeShaderVariantData", typeof(ComputeShader), "Unity.ProjectAuditor.Editor.Modules.ComputeShaderVariantData", out state.computeDictionaryIdentity);
            }
            return state;
        }

        /// <summary>完整依赖脚本与现有全局对象双重排除会触发对象改写或 Timeline payload 的内容。</summary>
        /// <param name="state">准确分支集合。</param>
        /// <param name="paths">已冻结全部依赖，不以已加载场景代替。</param>
        private static void ValidateAbsentContent(Snapshot state, string[] paths)
        {
            var forbidden = new HashSet<string>(StringComparer.Ordinal);
            foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                Type type = script == null ? null : script.GetClass();
                if (type != null && IsForbiddenContent(type, Has(state, "ProBuilder"), Has(state, "Timeline"))) forbidden.Add(AssetDatabase.GetAssetPath(script));
            }
            if (paths.Any(forbidden.Contains)) throw new InvalidOperationException("Player 完整依赖含 ProBuilder/Timeline 内容，不能按空场景分支执行。");
            if (Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Any(value => value != null && IsForbiddenContent(value.GetType(), Has(state, "ProBuilder"), Has(state, "Timeline")))) throw new InvalidOperationException("存在已加载 ProBuilder/Timeline 内容，不能按空分支执行。");
        }

        /// <summary>仅读既有继承链，包含派生脚本；不实例化业务组件或读取虚拟属性。</summary>
        /// <param name="type">真实脚本或已加载对象类型。</param>
        /// <param name="proBuilder">是否排除 ProBuilder 内容。</param>
        /// <param name="timeline">是否排除 TimelineAsset 内容。</param>
        /// <returns>命中准确基类时为 true。</returns>
        internal static bool IsForbiddenContent(Type type, bool proBuilder, bool timeline)
        {
            for (Type current = type; current != null; current = current.BaseType)
                if (proBuilder && (current.FullName == "UnityEngine.ProBuilder.ProBuilderMesh" || current.FullName == "UnityEngine.ProBuilder.EntityBehaviour") || timeline && current.FullName == "UnityEngine.Timeline.TimelineAsset") return true;
            return false;
        }

        /// <summary>仅读 feature backing 与原报告器；null global/export false 保证正式 ReportBegin 选择 Empty，禁止 profiler 与外部事件。</summary>
        /// <param name="state">写入当前准确状态。</param>
        private static void CaptureShader(Snapshot state)
        {
            if (GraphicsSettings.currentRenderPipeline != null || GraphicsSettings.defaultRenderPipeline != null || Enumerable.Range(0, QualitySettings.names.Length).Any(index => QualitySettings.GetRenderPipelineAssetAt(index) != null)) throw new InvalidOperationException("Shader 当前或完整 Quality 存在 SRP。");
            PropertyInfo global = typeof(GraphicsSettings).GetProperty("currentRenderPipelineGlobalSettings", Static);
            if (global?.DeclaringType != typeof(GraphicsSettings) || global.PropertyType != typeof(RenderPipelineGlobalSettings) || (UnityEngine.Object)global.GetValue(null) != null) throw new InvalidOperationException("Shader global settings 非 null 或 getter 身份改变。");
            if (Has(state, "ShaderCore"))
            {
                Type reporting = Find("UnityEditor.Rendering.ShaderStripping");
                if (!Equals(Read(reporting, "s_DefaultExport", null), false) || !Equals(Read(reporting, "s_ShowWarningDebugShaders", null), false) || Read(reporting, "m_Reporter", null) != null) throw new InvalidOperationException("Shader 只接受自然 export false/warning false/null reporter；不覆盖旧报告。");
            }
            if (Has(state, "ShaderGraph"))
            {
                Type features = Find("UnityEditor.Rendering.BuiltIn.ShaderFeatures"), owner = Find("UnityEditor.Rendering.BuiltIn.ShaderBuildPreprocessor");
                object list = Read(owner, "s_SupportedFeaturesList", null);
                if (list?.GetType() != typeof(List<>).MakeGenericType(features)) throw new InvalidOperationException("ShaderGraph features 不是准确 List。");
                state.featureListIdentity = RuntimeHelpers.GetHashCode(list);
                state.features = ((IEnumerable)list).Cast<object>().Select(Convert.ToInt32).ToArray();
                string[] enabled = { "MainLight", "AdditionalLightShadows", "VertexLighting", "AdditionalLights", "MixedLighting", "DeferredShading", "DeferredWithoutAccurateGbufferNormals", "TerrainHoles" };
                state.expectedFeature = enabled.Aggregate(0, (value, name) => value | Convert.ToInt32(Enum.Parse(features, name)));
                if (QualitySettings.shadows != ShadowQuality.Disable) state.expectedFeature |= Convert.ToInt32(Enum.Parse(features, "MainLightShadows"));
                if (QualitySettings.shadows != ShadowQuality.Disable && QualitySettings.shadows != ShadowQuality.HardOnly) state.expectedFeature |= Convert.ToInt32(Enum.Parse(features, "SoftShadows"));
                if (state.features.Length != 0 && (state.features.Length != 1 || state.features[0] != state.expectedFeature)) throw new InvalidOperationException("ShaderGraph 旧 feature list 不属于当前准确自然特征。");
            }
        }

        /// <summary>验证 Timeline 固定空统计全部字段；允许官方 pre 用同结构新对象替换，不允许任何业务 timeline payload。</summary>
        private static void ValidateEmptyTimeline()
        {
            Type owner = Find("UnityEditor.Timeline.Analytics.TimelineAnalytics");
            object data = Read(owner, "_timelineSceneInfo", null);
            if (data?.GetType() != owner.Assembly.GetType("UnityEditor.Timeline.Analytics.TimelineSceneInfo", true)) throw new InvalidOperationException("Timeline 统计类型改变。");
            Type type = data.GetType();
            var track = Read(type, "trackCount", data) as Dictionary<string, int>;
            string[] keys = { "ActivationTrack", "AnimationTrack", "AudioTrack", "ControlTrack", "PlayableTrack", "UserType", "Other" };
            if (track == null || track.GetType() != typeof(Dictionary<string, int>) || !ReferenceEquals(track.Comparer, EqualityComparer<string>.Default) || track.Count != keys.Length || keys.Any(key => !track.TryGetValue(key, out int count) || count != 0) || !(Read(type, "userTrackTypesCount", data) is Dictionary<string, int> user) || user.GetType() != typeof(Dictionary<string, int>) || !ReferenceEquals(user.Comparer, EqualityComparer<string>.Default) || user.Count != 0) throw new InvalidOperationException("Timeline 存在 track 统计，不会清空。");
            object unique = Read(type, "uniqueDirectors", data);
            Type asset = Find("UnityEngine.Timeline.TimelineAsset");
            if (unique?.GetType() != typeof(HashSet<>).MakeGenericType(asset) || !ReferenceEquals(unique.GetType().GetProperty("Comparer").GetValue(unique), typeof(EqualityComparer<>).MakeGenericType(asset).GetProperty("Default").GetValue(null)) || (int)unique.GetType().GetProperty("Count").GetValue(unique) != 0) throw new InvalidOperationException("Timeline 已包含实际 director，不会清空。");
            foreach (string field in new[] { "numTracks", "numRecorded", "minDuration", "maxDuration", "minNumTracks", "maxNumTracks" })
            {
                int expected = field.StartsWith("min", StringComparison.Ordinal) ? int.MaxValue : field.StartsWith("max", StringComparison.Ordinal) ? int.MinValue : 0;
                if (!Equals(Read(type, field, data), expected)) throw new InvalidOperationException("Timeline 空统计哨兵不符：" + field);
            }
            if (type.GetFields(Instance).Length != 9) throw new InvalidOperationException("Timeline 统计字段 roster 改变。");
        }

        /// <summary>只接受原规则包未安装和实际 Pref/EditorPrefs 都关闭的自然分支，不移除已有任务。</summary>
        /// <param name="state">保存准确 Pref 对象身份与原 key 存在性。</param>
        private static void ValidateAuditorDisabled(Snapshot state)
        {
            Type type = Find("Unity.ProjectAuditor.Editor.UserPreferences");
            object pref = Read(type, "AnalyzeAfterBuild", null);
            Type baseType = pref?.GetType().BaseType;
            if (pref?.GetType() != type.GetNestedType("BoolPref", BindingFlags.Public | BindingFlags.NonPublic) || !Equals(Read(baseType, "<Name>k__BackingField", pref), "AnalyzeAfterBuild") || !Equals(Read(baseType, "<Value>k__BackingField", pref), false) || EditorPrefs.GetBool("ProjectAuditor.AnalyzeAfterBuild", false)) throw new InvalidOperationException("ProjectAuditor 自动分析实际启用或 Pref 身份改变。");
            state.auditorPreferenceIdentity = RuntimeHelpers.GetHashCode(pref);
            state.auditorPreferenceExists = EditorPrefs.HasKey("ProjectAuditor.AnalyzeAfterBuild");
            Type rules = Find("Unity.ProjectAuditor.Editor.ProjectAuditorRulesPackage");
            if (!Equals(Read(rules, "<IsInstalled>k__BackingField", null), false) || PackageInfo.GetAllRegisteredPackages().Any(package => package.name == "com.unity.project-auditor-rules")) throw new InvalidOperationException("ProjectAuditor 规则包已安装，构造会实例化模块；不进入未知下游。");
            Delegate update = Read(typeof(EditorApplication), "update", null) as Delegate;
            if (update != null && update.GetInvocationList().Any(value => value.Method.DeclaringType == Find("Unity.ProjectAuditor.Editor.ProjectAuditor") && value.Method.Name == "DelayedPostBuildAudit")) throw new InvalidOperationException("ProjectAuditor 存在旧延迟 Audit，不能由本次接管。");
        }

        /// <summary>复制准确 BCL 字典/List 和固定 DTO 字段摘要；不调用 ClearBuildData、分析器或 shader 回调。</summary>
        /// <param name="module">准确引擎模块类型。</param>
        /// <param name="field">固定字典字段。</param>
        /// <param name="keyType">准确 Shader 或 ComputeShader key。</param>
        /// <param name="dataName">准确 DTO 全名。</param>
        /// <param name="identity">返回实际原字典身份。</param>
        /// <returns>按 key 身份排序的全部原列表摘要。</returns>
        private static CacheRow[] CaptureCache(Type module, string field, Type keyType, string dataName, out int identity)
        {
            Type dataType = module.Assembly.GetType(dataName, true), listType = typeof(List<>).MakeGenericType(dataType);
            object value = Read(module, field, null);
            if (value?.GetType() != typeof(Dictionary<,>).MakeGenericType(keyType, listType)) throw new InvalidOperationException("ProjectAuditor Shader cache 字典身份改变。");
            object comparer = value.GetType().GetProperty("Comparer").GetValue(value);
            if (!ReferenceEquals(comparer, typeof(EqualityComparer<>).MakeGenericType(keyType).GetProperty("Default").GetValue(null))) throw new InvalidOperationException("ProjectAuditor Shader cache 存在未知 comparer 下游。");
            identity = RuntimeHelpers.GetHashCode(value);
            var rows = new List<CacheRow>();
            foreach (DictionaryEntry entry in (IDictionary)value)
            {
                if (entry.Key == null || entry.Key.GetType() != keyType || entry.Value?.GetType() != listType) throw new InvalidOperationException("ProjectAuditor Shader cache key/list 类型改变。");
                var items = ((IList)entry.Value).Cast<object>().ToArray();
                if (items.Any(item => item?.GetType() != dataType)) throw new InvalidOperationException("ProjectAuditor Shader cache 含未知 DTO。");
                rows.Add(new CacheRow { keyIdentity = RuntimeHelpers.GetHashCode(entry.Key), listIdentity = RuntimeHelpers.GetHashCode(entry.Value), items = items.Select(item => Hash(FixedData(item))).ToArray(), targets = items.Select(item => Convert.ToInt32(Read(dataType, "BuildTarget", item))).ToArray() });
            }
            return rows.OrderBy(row => row.keyIdentity).ToArray();
        }

        /// <summary>固定 DTO 只读字段递归投影；拒绝属性、任意集合、业务对象或未知非原始成员。</summary>
        /// <param name="value">准确已审 DTO、数组或原始值。</param>
        /// <returns>稳定字段 JSON；只在驻内存计算摘要。</returns>
        private static string FixedData(object value)
        {
            if (value == null) return "null";
            Type type = value.GetType();
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum) return NovaFramework.Runtime.Util.Json.Serialize(value);
            if (type.IsArray) return "[" + string.Join(",", ((Array)value).Cast<object>().Select(FixedData)) + "]";
            if (type.FullName != "Unity.ProjectAuditor.Editor.Modules.ShaderVariantData" && type.FullName != "Unity.ProjectAuditor.Editor.Modules.ComputeShaderVariantData") throw new InvalidOperationException("固定统计 DTO 包含未知类型：" + type.FullName);
            return "{" + string.Join(",", type.GetFields(Instance).OrderBy(field => field.Name, StringComparer.Ordinal).Select(field => field.Name + ":" + FixedData(field.GetValue(value)))) + "}";
        }

        /// <summary>只读官方 UGUI 事件与四项固定字段；不调用 GatherData/Send 或业务组件 getter。</summary>
        /// <returns>当前缓存或 null；未知字段类型拒绝。</returns>
        private static UguiState CaptureUgui()
        {
            Type owner = Find("UnityEditor.UI.Analytics.UGUIBuildAnalytics");
            object data = Read(owner, "buildEvent", null);
            if (data == null) return null;
            Type type = owner.Assembly.GetType("UnityEditor.UI.Analytics.UGUIBuildEvent", true);
            if (data.GetType() != type || type.GetFields(Instance).Length != 3) throw new InvalidOperationException("UGUI buildEvent 身份或字段 roster 改变。");
            object counter = Read(type, "counter", data);
            if (counter?.GetType() != type.GetNestedType("Counter", BindingFlags.NonPublic | BindingFlags.Public) || counter.GetType().GetFields(Instance).Length != 4) throw new InvalidOperationException("UGUI Counter 身份或字段 roster 改变。");
            return new UguiState { guid = (string)Read(type, "buildGuid", data), buildType = Convert.ToInt32(Read(type, "buildType", data)), world = (int)Read(counter.GetType(), "WorldSpaceCanvas", counter), screen = (int)Read(counter.GetType(), "ScreenSpaceCanvas", counter), overlay = (int)Read(counter.GetType(), "OverlayCanvas", counter), tmp = (int)Read(counter.GetType(), "TMPElements", counter) };
        }

        /// <summary>只读准确 StoreConfiguration 的枚举 backing，不调用厂商 JSON 或配置方法。</summary>
        /// <param name="config">当前已有配置对象或 null。</param>
        /// <returns>实际固定枚举名或 null。</returns>
        private static string ReadIapConfig(object config)
        {
            if (config == null) return null;
            Type type = Find("UnityEngine.Purchasing.StoreConfiguration");
            if (config.GetType() != type) throw new InvalidOperationException("IAP config 不是准确 StoreConfiguration。");
            return Read(type, "<androidStore>k__BackingField", config).ToString();
        }

        /// <summary>仅归一化明确官方可重建的 feature、IAP 配置、UGUI 统计及 Shader DTO 追加内容；配置与原容器仍冻结。</summary>
        /// <param name="state">完整真实 snapshot。</param>
        /// <returns>稳定不变量摘要。</returns>
        internal static string InvariantHash(Snapshot state)
        {
            if (state == null) throw new InvalidOperationException("Player 包回调状态缺失。");
            Snapshot clone = NovaFramework.Runtime.Util.Json.Deserialize<Snapshot>(NovaFramework.Runtime.Util.Json.Serialize(state));
            clone.features = null; clone.iapConfig = null; clone.ugui = null; clone.shaderRows = null; clone.computeRows = null;
            return Hash(NovaFramework.Runtime.Util.Json.Serialize(clone));
        }

        /// <summary>执行前完整值相等，不以正常后态投影吸收用户或 SDK 新状态。</summary>
        /// <param name="before">原计划实际值。</param>
        /// <param name="current">写前实际读回。</param>
        /// <returns>完整字段相等为 true。</returns>
        internal static bool BeforeEquals(Snapshot before, Snapshot current) => before != null && current != null && NovaFramework.Runtime.Util.Json.Serialize(before) == NovaFramework.Runtime.Util.Json.Serialize(current);

        /// <summary>验证同域完整不变量、feature 正常重建、IAP 旧值或准确新配置与统计字典只追加前缀；不写回任何状态。</summary>
        /// <param name="before">本轮原计划值。</param>
        /// <param name="after">官方链结束实际值。</param>
        /// <param name="current">当前只读值。</param>
        /// <param name="error">具体不足原因。</param>
        /// <returns>正常后验完整时为 true；旧或缺失回执仍 partial。</returns>
        internal static bool TryValidateAfter(Snapshot before, Snapshot after, Snapshot current, out string error)
        {
            error = null;
            if (before == null || after == null || current == null) error = "Player 包回调 before/after/current 缺失。";
            else if (InvariantHash(before) != InvariantHash(after) || InvariantHash(after) != InvariantHash(current) || !BeforeEquals(after, current)) error = "Player 包回调域、配置、依赖或原容器漂移。";
            else if (Has(before, "ShaderGraph") && (after.features?.Length != 1 || after.features[0] != before.expectedFeature)) error = "ShaderGraph 官方 feature 重建后态缺失。";
            else if (Has(before, "Iap") && after.iapConfig != before.iapConfig && (before.iapExpectedConfig == null || after.iapConfig != before.iapExpectedConfig)) error = "IAP 官方配置缓存出现未知变化。";
            else if (Has(before, "AuditorShaders") && (!CacheAppendsOnly(before.shaderRows, after.shaderRows) || !CacheAppendsOnly(before.computeRows, after.computeRows))) error = "ProjectAuditor Shader 原缓存被删除、替换或改写。";
            return error == null;
        }

        /// <summary>保留每个原 key/list 和全部原有顺序内容，只接受新增 WebGL 编译 DTO；空回调不要求伪造增长。</summary>
        /// <param name="before">原字典全部行。</param>
        /// <param name="after">官方链结束全部行。</param>
        /// <returns>准确前缀追加为 true。</returns>
        internal static bool CacheAppendsOnly(CacheRow[] before, CacheRow[] after)
        {
            if (before == null || after == null || before.Any(row => row == null) || after.Any(row => row == null) || before.Select(row => row.keyIdentity).Distinct().Count() != before.Length || after.Select(row => row.keyIdentity).Distinct().Count() != after.Length) return false;
            foreach (CacheRow row in after)
            {
                if (row.items == null || row.targets == null || row.items.Length != row.targets.Length || row.items.Any(value => value?.Length != 64)) return false;
                CacheRow original = before.SingleOrDefault(value => value.keyIdentity == row.keyIdentity);
                int count = original?.items?.Length ?? 0;
                if (original != null && (original.listIdentity != row.listIdentity || original.items == null || original.targets == null || original.targets.Length != count || row.items.Length < count || !row.items.Take(count).SequenceEqual(original.items) || !row.targets.Take(count).SequenceEqual(original.targets))) return false;
                if (row.targets.Skip(count).Any(target => target != (int)BuildTarget.WebGL)) return false;
            }
            return before.All(row => after.Any(value => value.keyIdentity == row.keyIdentity));
        }

        /// <summary>绑定本次真实 BuildReport 和官方 UGUI 缓存，不调用收集或发送方法。</summary>
        /// <param name="before">原计划分支。</param>
        /// <param name="report">本次真正返回的报告。</param>
        /// <returns>准确本地材料；无 UGUI 分支为 null。</returns>
        internal static ReportMaterial CaptureReport(Snapshot before, BuildReport report)
        {
            if (before == null || report == null) throw new InvalidOperationException("Player 包统计原状态或正式报告缺失。");
            if (!Has(before, "Ugui")) return null;
            if (report.summary.platform != BuildTarget.WebGL) throw new InvalidOperationException("UGUI 正式报告不是 WebGL。");
            return new ReportMaterial { guid = report.summary.guid.ToString(), buildType = (int)report.summary.buildType, ugui = CaptureUgui(), analyticsEnabled = EditorAnalytics.enabled };
        }

        /// <summary>要求真实 GUID/type 与官方四项非负统计完整一致；不据静态设置推断 native ack 或真实场景次数。</summary>
        /// <param name="before">原计划。</param>
        /// <param name="after">官方链结束状态。</param>
        /// <param name="material">当时正式报告材料。</param>
        /// <param name="guid">Player 回执 GUID。</param>
        /// <param name="error">具体材料错误。</param>
        /// <returns>本地 payload 与真实报告绑定时为 true。</returns>
        internal static bool TryValidateReport(Snapshot before, Snapshot after, ReportMaterial material, string guid, out string error)
        {
            error = null;
            if (before == null || after == null) error = "Player 包统计状态缺失。";
            else if (!Has(before, "Ugui")) { if (material != null) error = "inactive 包统计包含其他报告。"; }
            else if (material == null || material.ugui == null || after.ugui == null || string.IsNullOrEmpty(guid) || material.guid != guid || material.ugui.guid != guid || material.ugui.buildType != material.buildType || material.analyticsEnabled != before.analyticsEnabled || material.ugui.world < 0 || material.ugui.screen < 0 || material.ugui.overlay < 0 || material.ugui.tmp < 0 || NovaFramework.Runtime.Util.Json.Serialize(material.ugui) != NovaFramework.Runtime.Util.Json.Serialize(after.ugui)) error = "UGUI 正式 GUID/type/四计数后验不足或配置漂移；不补字段、不重发。";
            return error == null;
        }

        /// <summary>只比较合同固定的完整 marker，不按前缀近似匹配。</summary>
        /// <param name="state">实际 snapshot。</param>
        /// <param name="name">合同固定分支名。</param>
        /// <returns>准确 marker 存在时为 true。</returns>
        private static bool Has(Snapshot state, string name) => state?.branches != null && state.branches.Contains(Prefix + name, StringComparer.Ordinal);

        /// <summary>只定位已加载唯一实际 Type，转发同一对象去重；不加载或实例化类型。</summary>
        /// <param name="name">固定完整类型名。</param>
        /// <returns>唯一真实 Type，零或多个真实 Type 均拒绝。</returns>
        private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).Where(value => value != null).Distinct().Single();

        /// <summary>固定声明字段身份后只读 backing，不调用 getter 或回调。</summary>
        /// <param name="type">准确声明类型。</param>
        /// <param name="name">合同字段名。</param>
        /// <param name="instance">已有对象或 static null。</param>
        /// <returns>原字段实际值。</returns>
        private static object Read(Type type, string name, object instance)
        {
            FieldInfo field = type?.GetField(name, instance == null ? Static : Instance);
            if (field?.DeclaringType != type) throw new InvalidOperationException("Player 包 backing 身份改变：" + type?.FullName + "." + name);
            return field.GetValue(instance);
        }

        /// <summary>核对实际 DLL 正文并冻结完整程序集/MVID，不靠相同类型名放行。</summary>
        /// <param name="type">准确已加载类型。</param>
        /// <param name="expected">审查过的 DLL SHA。</param>
        /// <param name="inputs">追加稳定身份。</param>
        /// <param name="token">文件摘要取消令牌。</param>
        private static void RequireAssembly(Type type, string expected, ICollection<string> inputs, CancellationToken token)
        {
            if (type == null || string.IsNullOrEmpty(type.Assembly.Location) || GenerateActionCommon.ComputeFileHash(type.Assembly.Location, token) != expected) throw new InvalidOperationException("Player 包回调实际 DLL 与已审编译不符。");
            inputs.Add("PlayerPackageAssembly=" + type.Assembly.FullName + "|" + type.Module.ModuleVersionId + "|" + expected);
        }

        /// <summary>核对准确正式包版本和每个实际下游源码，不编辑包或触发 UPM。</summary>
        /// <param name="type">所属准确程序集的已加载类型。</param>
        /// <param name="packageName">固定正式包名。</param>
        /// <param name="version">已审包版本。</param>
        /// <param name="relative">包内准确相对源码。</param>
        /// <param name="expected">正式源码 SHA。</param>
        /// <param name="inputs">追加源码身份。</param>
        /// <param name="token">文件摘要取消令牌。</param>
        private static void RequireSource(Type type, string packageName, string version, string relative, string expected, ICollection<string> inputs, CancellationToken token)
        {
            PackageInfo package = PackageInfo.FindForAssembly(type.Assembly);
            if (package?.name != packageName || package.version != version || GenerateActionCommon.ComputeFileHash(Path.Combine(package.resolvedPath, relative), token) != expected) throw new InvalidOperationException("Player 包源码身份改变：" + packageName + "/" + relative);
            inputs.Add("PlayerPackageSource=" + packageName + "|" + version + "|" + relative + "|" + expected);
        }

        /// <summary>仅保存本地输入摘要，不输出统计原文或配置正文。</summary>
        /// <param name="value">驻内存材料。</param>
        /// <returns>SHA256。</returns>
        private static string Hash(string value) => GenerateActionCommon.ComputeTextHash(value);
    }
}
