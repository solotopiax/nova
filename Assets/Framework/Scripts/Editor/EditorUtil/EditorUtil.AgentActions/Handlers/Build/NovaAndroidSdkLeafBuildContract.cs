/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * filename:  NovaAndroidSdkLeafBuildContract.cs
 * author:    taoye
 * created:   2026/10/19
 * descrip:   Android八型叶子的配置冻结、原纯规则复用与未绑定副作用边界
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using UnityEditor;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>内部叶子材料，不构造/提前执行SDK回调，不保存/导入/联网；预测不是SDK或Android Action准入。</summary>
    internal static class NovaAndroidSdkLeafBuildContract
    {
        internal const string Native = "NovaFramework.Editor.NativeBuildProcessor";
        internal const string AppsFlyer = "NovaFramework.SDK.AppsFlyerPlugin.Editor.AppsFlyerPluginBuildProcessor";
        internal const string AdMob = "NovaFramework.SDK.AdMobAdPlugin.Editor.AdMobAdPluginBuildProcessor";
        internal const string Facebook = "NovaFramework.SDK.Facebook.Editor.FacebookPluginBuildProcessor";
        internal const string Max = "NovaFramework.SDK.MaxAdPlugin.Editor.MaxAdPluginBuildProcessor";
        internal const string Google = "NovaFramework.SDK.GoogleSignIn.Editor.GoogleSignInPluginBuildProcessor";
        internal const string Apple = "NovaFramework.SDK.AppleSignIn.Editor.AppleSignInPluginBuildProcessor";
        internal const string Firebase = "NovaFramework.SDK.FirebasePlugin.Editor.FirebasePluginBuildProcessor";
        private static readonly string[] Roster = { Native, AppsFlyer, AdMob, Facebook, Max, Google, Apple, Firebase };
        private static readonly string[] ConfigurationTypes = {
            "NovaFramework.SDK.AppsFlyerPlugin.Runtime.AppsFlyerPluginConfig", "NovaFramework.SDK.AdPlugin.Runtime.AdPluginConfig",
            "NovaFramework.SDK.Facebook.FacebookPluginConfig", "NovaFramework.SDK.GoogleSignIn.GoogleSignInPluginConfig" };
        private static readonly string[][] Sources = {
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Build/AndroidGenerateAppsFlyerScope.cs", "02629620daf25554a947a7b68f25117859f2764cff73043d57d64e78a630a05a" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Generate/AndroidResolveRefreshContract.cs", "060f6a4f9c92eaa5eed6512d95b761a38afff68f8d2a40f5a6139e0f3adf5260" },             new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Build/AndroidGenerateNativePreviewContract.cs", "30bdc0793d977b5cfeb29d42170932fe419fdd506cd707b138968b024f7b57e6" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Build/AndroidGenerateNativePreviewScope.cs", "efa995930462101e4e7d61a058c076f820a192cdc8501b8ba941ff70ab6c511e" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Build/AndroidGenerateToolchainContract.cs", "88c0f84f19003a7bb1752880feba534302bfeebc4199c23ea8c077c546de0a78" },
new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Native/NativeBuildPreviewObservers.cs", "4094febb68b8dade04c9158afee5a77ea196ffd9165d0576b775d7aec9c135cd" }, new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Core/NovaBuildShared.cs", "4024f716e33ea5eacb06e02c04624a4ffdc4c9716aeae5b88273ef6303690e78" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Core/NovaBuildPreprocessor.cs", "b49ae79eea5bedbafa52e04e37ea7f2c20f2fae735a594ae5d0898aab83b6722" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Core/NovaBuildPostprocessor.cs", "8efdd2edeb3b2c811f8ac58d42d001284bf6a5d59f24502b7b92d3cb2a409513" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Core/NovaSDKBuildProcessor.cs", "a1a8c442997b86e38b541245a815aa491b9282de92913d8fdc478e798ac3441a" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Context/NovaBuildContext.cs", "cfe87a61d6fb8a74ac3a1128d6fa5dc18c63b947f4db91dad76839f3095a6892" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/ManifestRules/ManifestRule.cs", "885d382cf80a678fc557df1940f75c63ad97de75596ed8ab9b4f2055904887e9" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Tools/CustomAndroidManifest.cs", "55f1914bae236b964517c4c4c7ab953334115113d05bfb3b0fc6546e1d63efdc" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Tools/NovaManifestBootstrapper.cs", "b670e6afe203897396e330954a14425c3f100e2d7097ec2b0e9144e8f5c46e12" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Tools/NovaProguardBootstrapper.cs", "128e904b4d39ba511a4bf5029335ba233bcd7cef21ea6925b7e2b495c7bafc89" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Native/NativeBuildProcessor.cs", "d39e80105ff80fda84e02263524b940d1185204fbb1a674e0831add27611ae01" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.appsflyer/Nova/Scripts/Editor/BuildProcessor/AppsFlyerPluginBuildProcessor.cs", "7be334cf69d3787ad1f56e0527e7da4a9319d240b2cf05f966e061cf228d6ad2" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.admob/Nova/Scripts/Editor/AdMobAdPluginBuildProcessor.cs", "d1c3ff26598aae688413f612d4367138e55b3bc29ead804ca1cd174bafd672ba" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.facebook/Nova/Scripts/Editor/BuildProcessor/FacebookPluginBuildProcessor.cs", "794683c034f6e057180c9f8560d68cb6a4a9238d9690a43540ddf58335535f73" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.max/Nova/Scripts/Editor/BuildProcessor/MaxAdPluginBuildProcessor.cs", "3cef928bd4342b5590fbfed242390af089a900ea12750138b0744f332bf4bee7" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.googlesignin/Nova/Scripts/Editor/BuildProcessor/GoogleSignInPluginBuildProcessor.cs", "0ccdcd9877faed9a5d04cadcd4fd6889a997855a692ae136809100646b1e22db" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.applesignin/Nova/Scripts/Editor/BuildProcessor/AppleSignInPluginBuildProcessor.cs", "6787a735ea8d39d7d11376717e929cfe6f314c1b7312e6ec44878228949b8bb2" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Scripts/Editor/BuildProcessor/FirebasePluginBuildProcessor.cs", "17365decefedbeadc9174f99a836a8006f672a84194cee228459d3d7ab8393a1" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Config/ConfigRuntimeSO.cs", "5ffd7778b56aab2671a645a5995fedf119ec208b88adfa7a8779800366dd127a" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.RuntimeProvider.cs", "61dfacd7dfbe804aad895c4559e9fa9648c36c4f731593120908874374b2e6f6" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.ad/Nova/Scripts/Runtime/AdPluginConfig.cs", "c7c7c32a0562b9d1c7422bff466dcf69611718fb97b4420436f3760d0b077337" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.ad/Nova/Scripts/Runtime/AdChannelPluginBase/AdChannelConfigList.cs", "0a4d7081a2419b321ae80e9c9c2c82a8003533f0f613dcfc7e41aa3c11869227" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.appsflyer/Nova/Scripts/Runtime/AppsFlyerPluginConfig.cs", "e22cf4ab5a748a1cd5cb1f527917f326eb5e65d2ba76787a65bc9f00dc84ef27" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.facebook/Nova/Scripts/Runtime/FacebookPluginConfig.cs", "e73ebdf590863ac0c08820f24a941fa14369521a47c9faa3ce3e5621fe91892d" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.googlesignin/Nova/Scripts/Runtime/GoogleSignInPluginConfig.cs", "5d7ef0e0ba8ced2398e53c5954c887bc0f59cf9bacabaebbde0e2f6a0b0b9f51" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.max/Nova/Scripts/Runtime/MaxAdChannelConfig.cs", "572b5df8c8e98f5c266b5415a4ec18a8b1e78f8cc13ad09bcff5ad73eb801f2b" },
            new[] { "UPMPackages/com.solotopia.nova.framework.sdk.admob/Nova/Scripts/Runtime/AdMobAdChannelConfig.cs", "15d83b5e44b5e05fe44340d6641d4e707e6ffa4e4f99396b1e4704fce0a18d72" },
            new[] { "Library/PackageCache/com.applovin.mediation.ads@6c8fcb4e757f/Scripts/IntegrationManager/Editor/AppLovinSettings.cs", "7da9b18781e7fd124684493a9ebcc5bd01c2e598fc9136d20103081c05d75492" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Build/NovaAndroidParentBuildObserverScope.cs", "665aaa6fe55e9059919e8cc8ec79862d08659ce0f83fcd859073d552f2236df4" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Core/NovaBuildParentObservers.cs", "72ba83a02e00894526b59b0001c77e925295e51b30dac7add00ce58d0fa76051" } };

        /// <summary>只公开配置选择位置与完整Runtime摘要，不输出任何业务key/token或配置正文。</summary>
        [Serializable] internal sealed class ConfigurationRow
        {
            public string type;
            public int selectedIndex;
            public bool present;
            public string assemblyFullName;
            public string moduleName;
            public string mvid;
            public string dllSha256;
        }
        /// <summary>原激活Runtime准确来源与内存摘要；不包含配置正文或凭据，dirty仅保护不清理。</summary>
        [Serializable] internal sealed class RuntimeSource
        {
            public string path;
            public string guid;
            public long localId;
            public ulong nativeId;
            public bool dirty;
            public string fileSha256;
            public string metaSha256;
            public string jsonSha256;
        }
        /// <summary>准确副作用描述，不是可写路径白名单；未绑定实际选择/dirty全集时始终保持独立合同缺口。</summary>
        [Serializable] internal sealed class SideEffect
        {
            public string phase;
            public string operation;
            public string fixedPath;
            public string unresolvedBinding;
        }
        /// <summary>单叶子纯模型；完整Context规则与SDK调用许可分别表达，未接效果不能默认为完成。</summary>
        internal sealed class LeafModel
        {
            internal string type;
            internal NovaAndroidParentBuildContract.ContextEffect effect;
            internal bool contextComplete;
            internal string[] requiredContracts;
            internal SideEffect[] sideEffects;
        }
        /// <summary>同一原Runtime与选中配置实例的私有冻结，不持有SDK实例；释放只清自有引用。</summary>
        internal sealed class ConfigurationBinding : IDisposable
        {
            private ConfigRuntimeSO runtime;
            private Dictionary<string, ISDKPluginConfig> selected;
            private readonly string factorySignature;
            private NovaAndroidParentBuildContract.FactorySnapshot capturedFactory;
            private readonly Type[] capturedTypes;
            internal readonly RuntimeSource source;
            private readonly string sourceSignature;
            private readonly string rowsSignature;
            internal readonly ConfigurationRow[] rows;
            private bool disposed;
            /// <summary>只由真实Android捕获入口建立，完整原JSON仅局部计算SHA，不保正文。</summary>
            /// <param name="actual">原激活Runtime。</param><param name="configs">原API选中的真实配置引用。</param><param name="summary">完整位置及独立类型pin。</param><param name="origin">原准确Native/磁盘/meta/完整JSON来源。</param><param name="factory">同次父实际对象，不接受仅等值替换。</param>
            internal ConfigurationBinding(ConfigRuntimeSO actual, Dictionary<string, ISDKPluginConfig> configs, ConfigurationRow[] summary, RuntimeSource origin, NovaAndroidParentBuildContract.FactorySnapshot factory)
            { runtime = actual; selected = configs; rows = summary; source = origin; capturedFactory = factory; capturedTypes = factory.types.ToArray(); factorySignature = FactorySignature(factory); sourceSignature = JsonConvert.SerializeObject(origin); rowsSignature = JsonConvert.SerializeObject(summary); }
            /// <summary>未来实际observer租约之外重核原父工厂、Runtime、完整JSON与原配置引用；不把较晚状态补作早期材料。</summary>
            /// <param name="factory">同次真实父factory。</param>
            internal void Verify(NovaAndroidParentBuildContract.FactorySnapshot factory)
            {
                RequireSources(BuildActionCommon.ProjectRoot, capturedFactory);
                NovaAndroidParentBuildContract.VerifyFactoryBefore(factory, CancellationToken.None);
                if (disposed || runtime == null || !ReferenceEquals(runtime, EditorUtil.Config.RuntimeProvider.GetCurrent()) || !ReferenceEquals(factory, capturedFactory) || factory.types == null || !factory.types.SequenceEqual(capturedTypes) || FactorySignature(factory) != factorySignature || JsonConvert.SerializeObject(source) != sourceSignature || JsonConvert.SerializeObject(rows) != rowsSignature || JsonConvert.SerializeObject(ReadRuntimeSource(runtime)) != sourceSignature) throw new InvalidOperationException("Android叶子Runtime/factory已经替换或漂移。");
                foreach (ConfigurationRow row in rows)
                {
                    Type type = ResolveConfigurationType(row.type);
                    if (!ReferenceEquals(type.Module, type.Assembly.ManifestModule) || type.Assembly.FullName != row.assemblyFullName || type.Module.Name != row.moduleName || type.Module.ModuleVersionId.ToString("D") != row.mvid || Hash(File.ReadAllBytes(type.Assembly.Location)) != row.dllSha256 || !ReferenceEquals(runtime.GetSDKPluginConfig(type), selected[row.type])) throw new InvalidOperationException("Android叶子配置Module/DLL或选择实例/顺序漂移。");
                }
            }
            /// <summary>只读原选中数据字段，不调用SDK配置getter；仅OneLink三string供原纯规则builder使用。</summary>
            /// <param name="type">固定配置类型名。</param><param name="field">已审确切私有字段名。</param><returns>原字段字符串，可null。</returns>
            internal string ReadString(string type, string field)
            {
                if (disposed || !selected.TryGetValue(type, out ISDKPluginConfig config)) throw new InvalidOperationException("叶子配置binding已释放或角色未知。");
                if (config == null) return null;
                if (type != ConfigurationTypes[0] || field != "m_OneLinkHost" && field != "m_OneLinkPathPrefix" && field != "m_OneLinkFallbackName") throw new InvalidOperationException("叶子不能任意读取SDK字段或凭据。");
                FieldInfo actual = config.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (actual == null || actual.FieldType != typeof(string)) throw new InvalidOperationException("已审OneLink字段形状变化。");
                return (string)actual.GetValue(config);
            }
            /// <summary>只查原配置选择是否存在，不能由此宣称writer已经写入或可跳过。</summary>
            /// <param name="type">固定角色。</param><returns>原选中引用非null时true。</returns>
            internal bool Has(string type) => !disposed && selected != null && selected.TryGetValue(type, out ISDKPluginConfig config) && config != null;
            /// <summary>释放本计划自有引用；不Dispose业务配置，不清Runtime或原静态Context。</summary>
            public void Dispose() { disposed = true; selected?.Clear(); selected = null; runtime = null; capturedFactory = null; }
        }

        /// <summary>仅未来真实Android Plan使用：先复用原Parent真实工厂重核，再正常provider读取原Runtime并冻结原选择；不调用SDK构造/回调/业务getter。</summary>
        /// <param name="factory">原父合同真实捕获，必须含本域Type见证。</param><param name="pins">四配置类型独立审核的实际Android DLL身份，不从捕获值自签。</param><returns>私有引用binding；本轮离线不调用。</returns>
        internal static ConfigurationBinding CaptureConfigurations(NovaAndroidParentBuildContract.FactorySnapshot factory, NovaAndroidParentBuildContract.AuditedTypePin[] pins)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw new InvalidOperationException("叶子配置必须在实际Android域捕获。");
            RequireFactory(factory); RequireSources(BuildActionCommon.ProjectRoot, factory);
            NovaAndroidParentBuildContract.VerifyFactoryBefore(factory, CancellationToken.None);
            RequireConfigurationPins(pins);
            ConfigRuntimeSO runtime = EditorUtil.Config.RuntimeProvider.GetCurrent();
            if (runtime == null || runtime.GetType() != typeof(ConfigRuntimeSO) || runtime.Platform != PlatformType.Android) throw new InvalidOperationException("当前原Runtime不是准确Android配置，不创建或导出替代。");
            RuntimeSource before = ReadRuntimeSource(runtime); var selected = new Dictionary<string, ISDKPluginConfig>(); var rows = new List<ConfigurationRow>();
            foreach (string name in ConfigurationTypes)
            {
                Type type = ResolveConfigurationType(name); var pin = pins.Single(value => value.type == name); string dll = Hash(File.ReadAllBytes(type.Assembly.Location));
                if (!type.IsSealed || !ReferenceEquals(type.Module, type.Assembly.ManifestModule) || !typeof(ISDKPluginConfig).IsAssignableFrom(type) || type.Assembly.FullName != pin.assemblyFullName || type.Module.ModuleVersionId.ToString("D") != pin.mvid || dll != pin.dllSha256) throw new InvalidOperationException("叶子配置Android身份/完整DLL不同于独立审核值。");
                ISDKPluginConfig config = runtime.GetSDKPluginConfig(type); int index = config == null ? -1 : runtime.EnabledSDKConfigs.FindIndex(value => ReferenceEquals(value, config));
                selected.Add(name, config); rows.Add(new ConfigurationRow { type = name, selectedIndex = index, present = config != null, assemblyFullName = pin.assemblyFullName, moduleName = type.Module.Name, mvid = pin.mvid, dllSha256 = dll });
            }
            if (!ReferenceEquals(runtime, EditorUtil.Config.RuntimeProvider.GetCurrent()) || JsonConvert.SerializeObject(before) != JsonConvert.SerializeObject(ReadRuntimeSource(runtime))) throw new InvalidOperationException("叶子配置捕获期间原Runtime变化。");
            return new ConfigurationBinding(runtime, selected, rows.ToArray(), before, factory);
        }

        /// <summary>生产入口先核四种配置的已审源码与准确元数据，再将本次实际DLL/MVID交给原绑定器；不把当前身份称为历史pin。</summary>
        /// <param name="factory">本次源码/构造IL准入的真实工厂。</param><param name="provider">同次已证明不会修复Globals的实际provider来源。</param><returns>原Runtime和原配置引用的冻结。</returns>
        internal static ConfigurationBinding CaptureReviewedConfigurations(NovaAndroidParentBuildContract.FactorySnapshot factory, AndroidGenerateProviderContract.Snapshot provider)
        {
            RequireFactory(factory); RequireSources(BuildActionCommon.ProjectRoot, factory);
            if (factory.admission != "reviewed-source-signature-ctor-priority-il-v1" || provider == null ||
                !AndroidGenerateProviderContract.Equal(provider, AndroidGenerateProviderContract.Capture(provider.masterPath, CancellationToken.None)))
                throw new InvalidOperationException("Android配置缺实际生产工厂或原provider完整来源。");
            var pins = ConfigurationTypes.Select(name =>
            {
                Type type = ResolveConfigurationType(name);
                if (!type.IsSealed || type.TypeInitializer != null || !typeof(ISDKPluginConfig).IsAssignableFrom(type) || type.ContainsGenericParameters)
                    throw new InvalidOperationException("Android已审配置类型含未知结构或静态初始化。");
                return new NovaAndroidParentBuildContract.AuditedTypePin { type = name, assemblyFullName = type.Assembly.FullName,
                    mvid = type.Module.ModuleVersionId.ToString("D"), dllSha256 = Hash(File.ReadAllBytes(type.Assembly.Location)) };
            }).ToArray();
            return CaptureConfigurations(factory, pins);
        }

        /// <summary>仅核四配置角色独立pin的完整形状，不能从格式通过推断已审核或实际DLL匹配。</summary>
        /// <param name="pins">Android配置pin全集。</param>
        internal static void RequireConfigurationPins(NovaAndroidParentBuildContract.AuditedTypePin[] pins)
        {
            if (pins == null || pins.Length != ConfigurationTypes.Length || pins.Any(pin => pin == null || string.IsNullOrWhiteSpace(pin.assemblyFullName) || !Guid.TryParseExact(pin.mvid, "D", out _) || pin.dllSha256 == null || pin.dllSha256.Length != 64 || pin.dllSha256.Any(character => character < '0' || character > '9' && character < 'a' || character > 'f')) || !pins.Select(pin => pin.type).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(ConfigurationTypes.OrderBy(value => value, StringComparer.Ordinal))) throw new InvalidOperationException("叶子Runtime类型独立Android pins缺失、重复或格式未知。");
        }

        /// <summary>按原pre顺序生成八型Context模型；只调用明确纯规则builder/常量元数据，不提前执行SDK回调，未接Native或writer始终保留缺口。</summary>
        /// <param name="factory">原真实父factory。</param><param name="configs">同次原配置binding。</param><param name="selectedLauncher">原领域模板选出的准确初始launcher，不是最终Firebase activity。</param><returns>完整八型模型；不能直接作为SDK准入。</returns>
        internal static LeafModel[] Predict(NovaAndroidParentBuildContract.FactorySnapshot factory, ConfigurationBinding configs, string selectedLauncher)
        {
            RequireFactory(factory); if (configs == null || string.IsNullOrEmpty(selectedLauncher)) throw new InvalidOperationException("叶子预测缺少实际配置或原launcher。"); configs.Verify(factory);
            var models = new List<LeafModel>(); string activity = selectedLauncher; bool registered = false;
            foreach (string name in factory.preprocessOrder)
            {
                Type type = factory.types.Single(value => value.FullName == name); bool present = name == AppsFlyer ? configs.Has(ConfigurationTypes[0]) : name == Facebook ? configs.Has(ConfigurationTypes[2]) : name == AdMob || name == Max ? configs.Has(ConfigurationTypes[1]) : name == Google && configs.Has(ConfigurationTypes[3]);
                var effect = ConstantEffect(type); bool complete = name != Native && !(name == Facebook && present);
                if (name == AppsFlyer && present)
                {
                    string host = configs.ReadString(ConfigurationTypes[0], "m_OneLinkHost"), prefix = configs.ReadString(ConfigurationTypes[0], "m_OneLinkPathPrefix"), scheme = configs.ReadString(ConfigurationTypes[0], "m_OneLinkFallbackName");
                    if (!string.IsNullOrEmpty(host) || !string.IsNullOrEmpty(scheme)) effect.manifestRules = new[] { InvokeRules(type, new[] { activity, host, prefix, scheme }) };
                }
                if (!registered && !string.IsNullOrEmpty(effect.registeredActivity)) { activity = effect.registeredActivity; registered = true; }
                models.Add(new LeafModel { type = name, effect = effect, contextComplete = complete, requiredContracts = RequiredContracts(name, present).Concat(new[] { "ConfigRuntime.active-provider-natural-capture-observation" }).ToArray(), sideEffects = WriteSet(name, present) });
            }
            configs.Verify(factory); return models.ToArray();
        }

        /// <summary>仅生产Generate接纳已审Native当前场景预测；旧Predict仍保留未绑定语义，实际false须由原scope证明。</summary>
        internal static LeafModel[] PredictReviewedGenerate(NovaAndroidParentBuildContract.FactorySnapshot factory, ConfigurationBinding configs, string selectedLauncher, AndroidGenerateNativePreviewContract.Plan native)
        {
            AndroidGenerateNativePreviewContract.VerifyPlan(native, CancellationToken.None);
            LeafModel[] models = Predict(factory, configs, selectedLauncher); LeafModel model = models.Single(value => value.type == Native);
            if (native.predictedAppInstaller) throw new InvalidOperationException("Native当前源码合同只审明确false预测。");
            model.effect.manifestRules = new[] { new ManifestRuleSet { Permissions = new[] { new PermissionRule("android.permission.POST_NOTIFICATIONS") } } };
            model.contextComplete = true;
            model.requiredContracts = model.requiredContracts.Where(value => value != "Native.enabled-scenes.preview-open-close-and-AppInstaller").ToArray();
            return models;
        }

        /// <summary>复用原SDK常量及纯规则树；这里只产生普通数据，未知类型/额外builder/cctor不执行。</summary>
        /// <param name="type">父factory已核的真实类型；不实例化。</param><returns>已知固定效果，不含Native场景条件或FB非空实例布尔。</returns>
        internal static NovaAndroidParentBuildContract.ContextEffect ConstantEffect(Type type)
        {
            if (type == null || !Roster.Contains(type.FullName) || type.BaseType != typeof(NovaSDKBuildProcessor) || type.TypeInitializer != null) throw new InvalidOperationException("叶子类型/静态初始化未知。");
            string name = type.FullName; var effect = new NovaAndroidParentBuildContract.ContextEffect { type = name };
            if (name == Google || name == Facebook || name == Max)
            {
                FieldInfo field = type.GetField("c_AndroidProguardRules", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null || !field.IsLiteral || field.FieldType != typeof(string)) throw new InvalidOperationException("原Proguard常量形状未知。");
                effect.proguardRules = new[] { (name == Google ? "GoogleSignInPlugin" : name == Facebook ? "FacebookPlugin" : "MaxAdPlugin", (string)field.GetRawConstantValue()) };
            }
            if (name == Firebase)
            {
                FieldInfo field = type.GetField("c_ActivityName", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null || !field.IsLiteral || field.FieldType != typeof(string)) throw new InvalidOperationException("Firebase原activity常量未知。");
                effect.registeredActivity = (string)field.GetRawConstantValue(); effect.manifestRules = new[] { InvokeRules(type, Array.Empty<string>()) };
            }
            return effect;
        }

        /// <summary>仅调用原源码抽出的无Native纯规则builder；不反射任意方法/SDK callback，不构造processor。</summary>
        /// <param name="type">准确AppsFlyer或Firebase元数据类型。</param><param name="values">原构造参数的纯字符串。</param><returns>原领域新规则树。</returns>
        internal static ManifestRuleSet InvokeRules(Type type, string[] values)
        {
            int count = type?.FullName == Firebase ? 0 : type?.FullName == AppsFlyer ? 4 : -1;
            if (count < 0 || type.TypeInitializer != null || type.BaseType != typeof(NovaSDKBuildProcessor) || values == null || values.Length != count) throw new InvalidOperationException("纯规则builder类型/参数未知。");
            MethodInfo method = type.GetMethod("CreateAndroidManifestRules", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Enumerable.Repeat(typeof(string), count).ToArray(), null);
            if (method == null || method.ReturnType != typeof(ManifestRuleSet) || !method.IsAssembly || method.IsGenericMethod) throw new InvalidOperationException("原SDK纯规则builder缺失或签名变化。");
            return (ManifestRuleSet)method.Invoke(null, values.Cast<object>().ToArray());
        }

        /// <summary>准确列出本批仍未绑定的独立效果；配置存在只说明需证明自然分支，不能声称必定写或已完成。</summary>
        /// <param name="type">完整八型之一。</param><param name="configPresent">原Runtime选中相关配置非null。</param><returns>独立合同名称；无通用receipt或布尔豁免入口。</returns>
        internal static string[] RequiredContracts(string type, bool configPresent)
        {
            if (!Roster.Contains(type)) throw new InvalidOperationException("未知Android叶子。");
            if (type == Native) return new[] { "Native.enabled-scenes.preview-open-close-and-AppInstaller" };
            if (type == AppsFlyer) return new[] { "AppsFlyer.existing-properties-transform-and-normal-ImportAsset" };
            if (type == AdMob && configPresent) return new[] { "AdMob.natural-channel-selection-and-settings-save-or-proven-noop" };
            if (type == Max && configPresent) return new[] { "MAX.first-channel-including-disabled-and-selected-settings-save-or-proven-noop" };
            if (type == Facebook && configPresent) return new[] { "Facebook.same-instance-settings-flag-and-global-SaveAssets-Refresh", "Facebook.AfterNova-official-Manifest-or-proven-noop" };
            return Array.Empty<string>();
        }

        /// <summary>写域逐源说明，未解析的原选择/全局dirty集合明确保留，不能误作任意目录写许可。</summary>
        /// <param name="type">完整叶子。</param><param name="configPresent">原配置是否存在。</param><returns>本分支可能效果；不消费或授予任何写权限。</returns>
        internal static SideEffect[] WriteSet(string type, bool configPresent)
        {
            RequiredContracts(type, configPresent);
            if (type == Native) return new[] { new SideEffect { phase = "pre", operation = "OpenPreviewScene/ClosePreviewScene; context rules", unresolvedBinding = "actual enabled scene topology and NativeComponent activation/config" } };
            if (type == AppsFlyer) return new[] { new SideEffect { phase = "pre-before-config", operation = "Directory.CreateDirectory + File.WriteAllLines + ImportAsset even identical bytes", fixedPath = "Assets/Plugins/Android/gradleTemplate.properties", unresolvedBinding = "existing file/meta/DefaultImporter normal tail; no generic new GUID grant" } };
            if (type == AdMob && configPresent) return new[] { new SideEffect { phase = "pre", operation = "natural unique-enabled AdMob selection; optional CreateFolder/CreateAsset; ApplyModifiedPropertiesWithoutUndo/SetDirty/SaveAssetIfDirty", fixedPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset", unresolvedBinding = "exact two folders+meta, asset+meta, target adMobAndroidAppId; other fields preserved; SDK/versiondefine and valid ID proof" } };
            if (type == Max && configPresent) return new[] { new SideEffect { phase = "pre", operation = "original Instance lookup/possible create; sdkKey+adMobAndroidAppId; SaveAsync(SetDirty)+SaveAssetIfDirty", unresolvedBinding = "first Max channel regardless Enabled; actual existing first FindAssets result OR original package-layout creation path, never blanket Assets/MaxSdk allowlist" } };
            if (type == Facebook && configPresent) return new[] { new SideEffect { phase = "pre", operation = "original Instance and AppIds/AppLabels/ClientTokens lists; SetDirty+global SaveAssets+Refresh", unresolvedBinding = "actual selected resource/settings, entire original dirty save set and import effects; no credential body in contract" }, new SideEffect { phase = "AfterNova-pre", operation = "same instance m_AndroidSettingsApplied && official IsValidAppId then ManifestMod.GenerateManifest", fixedPath = "Assets/Plugins/Android/AndroidManifest.xml", unresolvedBinding = "official complete generator write set + original selected settings; no flag fabrication" } };
            return Array.Empty<SideEffect>();
        }

        /// <summary>只查询当前明确缺口；可变模型永远不能颁发Complete许可。Native/Import/实际provider观察未接时，外报清空数组仍不能抹掉缺口。</summary>
        /// <param name="models">完整八型普通预测数据，可核对但不是历史观测或授权。</param><returns>去重排序的准确待接项；本方法没有执行/恢复许可输出。</returns>
        internal static string[] DescribeUnboundContracts(LeafModel[] models)
        {
            if (models == null || models.Length != Roster.Length || !models.Select(model => model?.type).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(Roster.OrderBy(value => value, StringComparer.Ordinal)) || models.Any(model => model.effect == null || model.effect.type != model.type || model.requiredContracts == null)) throw new InvalidOperationException("缺口查询必须保留完整八型模型。");
            return RequiredContracts(Native, false).Concat(RequiredContracts(AppsFlyer, false)).Concat(new[] { "ConfigRuntime.active-provider-natural-capture-observation" }).Concat(models.SelectMany(model => model.requiredContracts)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        /// <summary>生产七/八型查询复核原provider后，仅消解已经取得的provider来源义务；Native和真实writer缺口不接受DTO自报清空。</summary>
        /// <param name="factory">真实完整工厂。</param><param name="models">按原pre顺序的完整预测。</param><param name="provider">原provider读取完整证据。</param><returns>仍须闭合的具体叶子副作用。</returns>
        internal static string[] DescribeReviewedUnboundContracts(NovaAndroidParentBuildContract.FactorySnapshot factory, LeafModel[] models, AndroidGenerateProviderContract.Snapshot provider)
        {
            RequireFactory(factory);
            if (provider == null || !AndroidGenerateProviderContract.Equal(provider, AndroidGenerateProviderContract.Capture(provider.masterPath, CancellationToken.None)) ||
                models == null || models.Length != factory.discovered.Length || models.Any(model => model?.effect == null || model.requiredContracts == null) ||
                !models.Select(model => model.type).SequenceEqual(factory.preprocessOrder, StringComparer.Ordinal))
                throw new InvalidOperationException("Android生产叶子查询缺完整工厂、预测或同次原provider来源。");
            return RequiredContracts(Native, false).Concat(RequiredContracts(AppsFlyer, false)).Concat(models.SelectMany(model => model.requiredContracts))
                .Where(value => value != "ConfigRuntime.active-provider-natural-capture-observation").Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        /// <summary>生产Generate单独验证Native计划后才消解该项；原查询入口仍明确保留Native/import缺口。</summary>
        internal static string[] DescribeReviewedGenerateUnboundContracts(NovaAndroidParentBuildContract.FactorySnapshot factory, LeafModel[] models, AndroidGenerateProviderContract.Snapshot provider, AndroidGenerateNativePreviewContract.Plan native)
        {
            AndroidGenerateNativePreviewContract.VerifyPlan(native, CancellationToken.None);
            LeafModel value = models?.SingleOrDefault(model => model?.type == Native);
            if (value?.contextComplete != true || value.requiredContracts.Contains("Native.enabled-scenes.preview-open-close-and-AppInstaller")) throw new InvalidOperationException("Native生产预测未绑定。");
            return DescribeReviewedUnboundContracts(factory, models, provider).Where(contract => contract != "Native.enabled-scenes.preview-open-close-and-AppInstaller").ToArray();
        }

        /// <summary>完整真实factory见证必需，不接受只由JSON重建的types缺失材料。</summary>
        /// <param name="factory">父实际捕获。</param>
        private static void RequireFactory(NovaAndroidParentBuildContract.FactorySnapshot factory)
        {
            string[] expected = factory?.admission == "reviewed-source-signature-ctor-priority-il-v1" ? NovaAndroidParentBuildContract.ReviewedRoster(factory.adMobPackageInstalled) : Roster;
            if (factory == null || factory.types == null || factory.types.Length != expected.Length || factory.discovered == null || factory.discovered.Length != expected.Length || factory.preprocessOrder == null || !factory.types.Select(type => type.FullName).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal)) || !factory.preprocessOrder.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal))) throw new InvalidOperationException("叶子不能从缺失真实Type或包解析见证的父DTO批准SDK。");
        }
        /// <summary>只解析已加载的固定配置类型，不LoadAssembly或初始化类型。</summary>
        /// <param name="name">固定全名。</param><returns>唯一真实类型。</returns>
        private static Type ResolveConfigurationType(string name) => ConfigurationTypes.Contains(name) ? AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).Where(type => type != null).SingleOrDefault() ?? throw new InvalidOperationException("叶子配置类型未自然加载。") : throw new InvalidOperationException("未知配置角色。");
        /// <summary>只读原激活Runtime来源、GUID/localId、dirty及完整Native JSON；provider加载与序列化自然效果仍需真实观察，不称零变化。</summary>
        /// <param name="runtime">原已选真实主资产。</param><returns>准确来源摘要；不创建、修复或输出正文。</returns>
        private static RuntimeSource ReadRuntimeSource(ConfigRuntimeSO runtime)
        {
            string path = AssetDatabase.GetAssetPath(runtime), guid; long local;
            if (!EditorUtility.IsPersistent(runtime) || !AssetDatabase.IsMainAsset(runtime) || string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(runtime, out guid, out local)) throw new InvalidOperationException("Runtime不是原Assets主资产或完整身份缺失。");
            string full = Path.Combine(BuildActionCommon.ProjectRoot, path); RequireUnlinked(full); RequireUnlinked(full + ".meta");
            if (new FileInfo(full).Length > 2 * 1024 * 1024 || new FileInfo(full + ".meta").Length > 65536) throw new InvalidOperationException("Runtime源或meta超本合同有界预算。");
            string fileHash = Hash(File.ReadAllBytes(full)), metaHash = Hash(File.ReadAllBytes(full + ".meta")); ulong id = UnityEngine.EntityId.ToULong(runtime.GetEntityId()); bool dirty = EditorUtility.IsDirty(runtime); string json = RuntimeHash(runtime);
            if (AssetDatabase.GetAssetPath(runtime) != path || UnityEngine.EntityId.ToULong(runtime.GetEntityId()) != id || EditorUtility.IsDirty(runtime) != dirty || fileHash != Hash(File.ReadAllBytes(full)) || metaHash != Hash(File.ReadAllBytes(full + ".meta"))) throw new InvalidOperationException("Runtime来源在完整捕获中漂移。");
            return new RuntimeSource { path = path, guid = guid, localId = local, nativeId = id, dirty = dirty, fileSha256 = fileHash, metaSha256 = metaHash, jsonSha256 = json };
        }
        /// <summary>固定来源完整祖先拒绝链接，不改路径或读取其它工程。</summary>
        /// <param name="path">原文件或meta。</param>
        private static void RequireUnlinked(string path) { for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current)) if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Runtime来源祖先含链接。"); }
        /// <summary>仅完整真实Runtime序列化的摘要；不存/返回正文，不声明Native序列化零效果。</summary>
        /// <param name="runtime">原已有资产。</param><returns>完整JSON SHA，超2MiB明确拒绝。</returns>
        private static string RuntimeHash(ConfigRuntimeSO runtime) { string json = EditorJsonUtility.ToJson(runtime); byte[] bytes = new UTF8Encoding(false, true).GetBytes(json); if (bytes.Length > 2 * 1024 * 1024) throw new InvalidOperationException("叶子完整Runtime超预算，不截断。"); return Hash(bytes); }
        /// <summary>纯父材料完整摘要不包含私有Type强引用，不把摘要当执行授权。</summary>
        /// <param name="factory">原完整父DTO。</param><returns>完整SHA。</returns>
        private static string FactorySignature(NovaAndroidParentBuildContract.FactorySnapshot factory) => Hash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(factory)));
        /// <summary>核全部已审来源，只读固定源文件，不修改业务文件。</summary>
        /// <param name="root">准确工程根。</param>
        private static void RequireSources(string root, NovaAndroidParentBuildContract.FactorySnapshot factory)
        {
            bool includeAdMob = factory?.admission != "reviewed-source-signature-ctor-priority-il-v1" || factory.adMobPackageInstalled;
            foreach (string[] source in Sources.Where(source => includeAdMob || !source[0].StartsWith("UPMPackages/com.solotopia.nova.framework.sdk.admob/", StringComparison.Ordinal)))
            {
                string path = NovaAndroidParentBuildContract.ResolveReviewedSource(root, source[0]); RequireUnlinked(path);
                if (Hash(File.ReadAllBytes(path)) != source[1]) throw new InvalidOperationException("叶子已审源码漂移：" + source[0]);
            }
        }
        /// <summary>仅SHA计算，不输出原数据。</summary>
        /// <param name="bytes">完整自有输入。</param><returns>小写SHA256。</returns>
        private static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
