/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaAndroidParentBuildContract.cs
 * author:    taoye
 * created:   2026/10/19
 * descrip:   Nova Android父构建链的真实元数据、上下文与有限源输出合同
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
using System.Xml;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>内部父合同，不执行构建、SDK构造/回调/getter、导入、保存或网络；预测与观察不单独授予任何Action准入。</summary>
    internal static class NovaAndroidParentBuildContract
    {
        internal const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        internal const string ProguardPath = "Assets/Plugins/Android/proguard-user.txt";
        internal const string TemplatePath = "Assets/Framework/Scripts/Editor/BuildProcessor/Android/UnityManifest.xml";
        private const int MaxFileBytes = 2 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly string Domain = Guid.NewGuid().ToString("N");

        /// <summary>本项目已逐源审核的Android具体类型；不以SDK启用列表过滤真实构造roster。</summary>
        private sealed class ProcessorSpec
        {
            internal string type;
            internal string source;
            internal string sha256;
            internal int pre;
            internal int post;
        }
        private static readonly ProcessorSpec[] Specs = {
            new ProcessorSpec { type = "NovaFramework.Editor.NativeBuildProcessor", source = "Assets/Framework/Scripts/Editor/BuildProcessor/Native/NativeBuildProcessor.cs", sha256 = "d39e80105ff80fda84e02263524b940d1185204fbb1a674e0831add27611ae01", pre = 0, post = 0 },
            new ProcessorSpec { type = "NovaFramework.SDK.AppsFlyerPlugin.Editor.AppsFlyerPluginBuildProcessor", source = "UPMPackages/com.solotopia.nova.framework.sdk.appsflyer/Nova/Scripts/Editor/BuildProcessor/AppsFlyerPluginBuildProcessor.cs", sha256 = "7be334cf69d3787ad1f56e0527e7da4a9319d240b2cf05f966e061cf228d6ad2", pre = 300, post = 300 },
            new ProcessorSpec { type = "NovaFramework.SDK.AdMobAdPlugin.Editor.AdMobAdPluginBuildProcessor", source = "UPMPackages/com.solotopia.nova.framework.sdk.admob/Nova/Scripts/Editor/AdMobAdPluginBuildProcessor.cs", sha256 = "d1c3ff26598aae688413f612d4367138e55b3bc29ead804ca1cd174bafd672ba", pre = 600, post = 0 },
            new ProcessorSpec { type = "NovaFramework.SDK.Facebook.Editor.FacebookPluginBuildProcessor", source = "UPMPackages/com.solotopia.nova.framework.sdk.facebook/Nova/Scripts/Editor/BuildProcessor/FacebookPluginBuildProcessor.cs", sha256 = "794683c034f6e057180c9f8560d68cb6a4a9238d9690a43540ddf58335535f73", pre = 600, post = 600 },
            new ProcessorSpec { type = "NovaFramework.SDK.MaxAdPlugin.Editor.MaxAdPluginBuildProcessor", source = "UPMPackages/com.solotopia.nova.framework.sdk.max/Nova/Scripts/Editor/BuildProcessor/MaxAdPluginBuildProcessor.cs", sha256 = "3cef928bd4342b5590fbfed242390af089a900ea12750138b0744f332bf4bee7", pre = 600, post = 600 },
            new ProcessorSpec { type = "NovaFramework.SDK.GoogleSignIn.Editor.GoogleSignInPluginBuildProcessor", source = "UPMPackages/com.solotopia.nova.framework.sdk.googlesignin/Nova/Scripts/Editor/BuildProcessor/GoogleSignInPluginBuildProcessor.cs", sha256 = "0ccdcd9877faed9a5d04cadcd4fd6889a997855a692ae136809100646b1e22db", pre = 620, post = 620 },
            new ProcessorSpec { type = "NovaFramework.SDK.AppleSignIn.Editor.AppleSignInPluginBuildProcessor", source = "UPMPackages/com.solotopia.nova.framework.sdk.applesignin/Nova/Scripts/Editor/BuildProcessor/AppleSignInPluginBuildProcessor.cs", sha256 = "6787a735ea8d39d7d11376717e929cfe6f314c1b7312e6ec44878228949b8bb2", pre = 630, post = 630 },
            new ProcessorSpec { type = "NovaFramework.SDK.FirebasePlugin.Editor.FirebasePluginBuildProcessor", source = "UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Scripts/Editor/BuildProcessor/FirebasePluginBuildProcessor.cs", sha256 = "17365decefedbeadc9174f99a836a8006f672a84194cee228459d3d7ab8393a1", pre = 1200, post = 1200 },
        };
        private static readonly string[][] ParentSources = {
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Build/AndroidGenerateAppsFlyerScope.cs", "02629620daf25554a947a7b68f25117859f2764cff73043d57d64e78a630a05a" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Generate/AndroidResolveRefreshContract.cs", "060f6a4f9c92eaa5eed6512d95b761a38afff68f8d2a40f5a6139e0f3adf5260" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Build/AndroidGenerateNativePreviewContract.cs", "30bdc0793d977b5cfeb29d42170932fe419fdd506cd707b138968b024f7b57e6" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Build/AndroidGenerateNativePreviewScope.cs", "efa995930462101e4e7d61a058c076f820a192cdc8501b8ba941ff70ab6c511e" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Build/AndroidGenerateToolchainContract.cs", "88c0f84f19003a7bb1752880feba534302bfeebc4199c23ea8c077c546de0a78" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Native/NativeBuildPreviewObservers.cs", "4094febb68b8dade04c9158afee5a77ea196ffd9165d0576b775d7aec9c135cd" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Core/NovaBuildShared.cs", "4024f716e33ea5eacb06e02c04624a4ffdc4c9716aeae5b88273ef6303690e78" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Core/NovaBuildPreprocessor.cs", "b49ae79eea5bedbafa52e04e37ea7f2c20f2fae735a594ae5d0898aab83b6722" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Core/NovaBuildPostprocessor.cs", "8efdd2edeb3b2c811f8ac58d42d001284bf6a5d59f24502b7b92d3cb2a409513" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Core/NovaSDKBuildProcessor.cs", "a1a8c442997b86e38b541245a815aa491b9282de92913d8fdc478e798ac3441a" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Context/NovaBuildContext.cs", "cfe87a61d6fb8a74ac3a1128d6fa5dc18c63b947f4db91dad76839f3095a6892" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/ManifestRules/ManifestRule.cs", "885d382cf80a678fc557df1940f75c63ad97de75596ed8ab9b4f2055904887e9" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Tools/CustomAndroidManifest.cs", "55f1914bae236b964517c4c4c7ab953334115113d05bfb3b0fc6546e1d63efdc" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Tools/NovaManifestBootstrapper.cs", "b670e6afe203897396e330954a14425c3f100e2d7097ec2b0e9144e8f5c46e12" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Tools/NovaProguardBootstrapper.cs", "128e904b4d39ba511a4bf5029335ba233bcd7cef21ea6925b7e2b495c7bafc89" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Core/NovaBuildParentObservers.cs", "72ba83a02e00894526b59b0001c77e925295e51b30dac7add00ce58d0fa76051" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Build/NovaAndroidParentBuildObserverScope.cs", "665aaa6fe55e9059919e8cc8ec79862d08659ce0f83fcd859073d552f2236df4" },
        };

        /// <summary>由真实Android编译独立审查后冻结的pin，不能从本次捕获值自签或拿WebGL身份填充。</summary>
        [Serializable] internal sealed class AuditedTypePin
        {
            public string type;
            public string assemblyFullName;
            public string mvid;
            public string dllSha256;
        }
        /// <summary>TypeCache原顺序的一行；优先级由IL常量读出，绝不执行虚属性。</summary>
        [Serializable] internal sealed class ProcessorRow
        {
            public string type;
            public string assemblyFullName;
            public string mvid;
            public string dllSha256;
            public string sourceSha256;
            public string constructorIlSha256;
            public int pre;
            public int post;
        }
        /// <summary>真实元数据和当前BCL排序实现绑定；Type见证仅本域私有，不被JSON升级成可执行计划。</summary>
        [Serializable] internal sealed class FactorySnapshot
        {
            public string domain;
            public string admission;
            public bool adMobPackageInstalled;
            public string[] packageRoster;
            public string bclSha256;
            public string bclMvid;
            public string[] sourcePins;
            public ProcessorRow[] discovered;
            public string[] preprocessOrder;
            public string[] postprocessOrder;
            [NonSerialized, JsonIgnore] internal Type[] types;
        }
        /// <summary>已由父Action冻结的PlayerSettings纯值；不是公开请求，也不读取SDK业务设置或凭据。</summary>
        [Serializable] internal sealed class Settings
        {
            public string bundleId;
            public string versionCode;
            public string versionName;
            public AndroidApplicationEntry entry;
            public UIOrientation orientation;
        }
        /// <summary>一个正常SDK已审核分支的精确Context预期；调用方必须另证明实际配置分支，不能凭本DTO放行SDK。</summary>
        internal sealed class ContextEffect
        {
            internal string type;
            internal string registeredActivity;
            internal ManifestRuleSet[] manifestRules;
            internal (string sdkName, string rules)[] proguardRules;
            internal bool facebookSettingsApplied;
        }
        /// <summary>源文件/meta的只读完整摘要；不输出源XML正文或其中业务值。</summary>
        [Serializable] internal sealed class FileState
        {
            public string relativePath;
            public long length;
            public string sha256;
            public string metaSha256;
            public int attributes;
            public int metaAttributes;
        }
        /// <summary>预测摘要仅描述准确两个源写域；不覆盖其他SDK源写、Gradle、EDM、Burst或外部效果。</summary>
        [Serializable] internal sealed class OutputSummary
        {
            public FileState template;
            public FileState manifestBefore;
            public FileState proguardBefore;
            public string manifestExpectedSha256;
            public string proguardExpectedSha256;
            public string activity;
            public string documentExpectedSha256;
            public string[] expectedInstanceState;
            public string manifestRulesSha256;
            public string proguardRulesSha256;
            public string settingsSha256;
        }
        /// <summary>自然构造/普通pre/普通post阶段的完整自有材料；不记录SDK key、token或资源配置正文。</summary>
        [Serializable] internal sealed class ContextFrame
        {
            public string phase;
            public string domain;
            public string[] order;
            public string activity;
            public string manifestRulesSha256;
            public string proguardRulesSha256;
            public string documentSha256;
            public string[] instanceState;
        }

        /// <summary>Plan私有预测字节的生命周期；Dispose只清自有缓冲，不恢复/写源，不调用SDK或AssetDatabase。</summary>
        internal sealed class Prepared : IDisposable
        {
            internal readonly OutputSummary summary;
            private byte[] expectedManifest;
            private byte[] expectedProguard;
            private readonly string root;
            private bool disposed;
            private readonly string summarySha256;
            private readonly bool includeAdMob;
            /// <summary>绑定本次准确文件摘要及自有预测副本，不接管业务对象。</summary>
            /// <param name="projectRoot">已验证根目录。</param><param name="value">完整预测摘要。</param><param name="manifest">完整自有XML字节。</param><param name="proguard">完整自有规则字节。</param>
            internal Prepared(string projectRoot, OutputSummary value, byte[] manifest, byte[] proguard)
            { root = projectRoot; summary = value; summarySha256 = HashText(JsonConvert.SerializeObject(value)); expectedManifest = (byte[])manifest.Clone(); expectedProguard = (byte[])proguard.Clone(); includeAdMob = value.expectedInstanceState.Any(row => row.StartsWith(NovaAndroidSdkLeafBuildContract.AdMob + "|", StringComparison.Ordinal)); }
            /// <summary>把借用预测精确绑定到调用方实际工程根；只做完整路径规范化比较，不访问/创建目录或改写原root。</summary>
            /// <param name="expectedActualRoot">Scope从BuildActionCommon.ProjectRoot取得的实际根，不能由请求或预测DTO替代。</param>
            internal void RequireProjectRoot(string expectedActualRoot)
            {
                RequireAlive();
                if (string.IsNullOrWhiteSpace(expectedActualRoot)) throw new InvalidOperationException("Nova父预测缺实际工程根。");
                string expected = Path.GetFullPath(expectedActualRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string captured = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!string.Equals(captured, expected, StringComparison.Ordinal)) throw new InvalidOperationException("Nova父预测属于其他工程根，不能登记实际构建观察。");
            }
            /// <summary>原始源、meta、属性与模板必须逐值保持；不接受正常后态许可提前到写前。</summary>
            /// <param name="token">原Action取消令牌。</param>
            internal void VerifyBefore(CancellationToken token)
            {
                RequireAlive(); RequireSources(root, token, includeAdMob);
                if (!Same(summary.template, ReadFile(root, TemplatePath, token)) || !Same(summary.manifestBefore, ReadFile(root, ManifestPath, token)) || !Same(summary.proguardBefore, ReadFile(root, ProguardPath, token))) throw new InvalidOperationException("Nova Android父合同写前源/meta/属性漂移。");
            }
            /// <summary>在原父merge、Manifest.Save与Proguard.Rebuild完成后且OnAfterNovaPreprocess叶子之前验收两个源文件；叶子物理变换另由独立合同验收。</summary>
            /// <param name="token">原取消令牌。</param><returns>完整预测字节、原meta/属性及模板均保持才为真。</returns>
            internal bool VerifyAfter(CancellationToken token)
            {
                RequireAlive(); RequireSources(root, token, includeAdMob);
                FileState manifest = ReadFile(root, ManifestPath, token), proguard = ReadFile(root, ProguardPath, token);
                return Same(summary.template, ReadFile(root, TemplatePath, token)) && AfterFile(summary.manifestBefore, manifest, summary.manifestExpectedSha256, expectedManifest.Length) && AfterFile(summary.proguardBefore, proguard, summary.proguardExpectedSha256, expectedProguard.Length);
            }
            /// <summary>释放前禁止使用已失效或被内部错误修改的预测材料。</summary>
            private void RequireAlive()
            {
                if (disposed || HashText(JsonConvert.SerializeObject(summary)) != summarySha256 || expectedManifest == null || expectedProguard == null || Hash(expectedManifest) != summary.manifestExpectedSha256 || Hash(expectedProguard) != summary.proguardExpectedSha256) throw new InvalidOperationException("Nova Android预测材料已释放或损坏。");
            }
            /// <summary>仅清自有预测内存；绝不回写、删除源文件、meta或静态Context。</summary>
            public void Dispose()
            {
                if (disposed) return;
                disposed = true; Array.Clear(expectedManifest, 0, expectedManifest.Length); Array.Clear(expectedProguard, 0, expectedProguard.Length); expectedManifest = null; expectedProguard = null;
            }
        }

        /// <summary>真实自然构造后的同实例绑定，观察不产生构建授权；正常构造仍由原CollectProcessors负责。</summary>
        internal sealed class ContextBinding : IDisposable
        {
            private readonly FactorySnapshot factory;
            private NovaBuildContext context;
            private List<NovaSDKBuildProcessor> list;
            private NovaSDKBuildProcessor[] instances;
            private BuildReport report;
            private bool disposed;
            private string lastPhase;
            internal readonly ContextFrame constructed;
            /// <summary>仅实际普通构造返回后绑定原Context和原列表，不创建SDK实例或替换静态字段。</summary>
            /// <param name="expected">本域真实metadata计划。</param><param name="actual">普通Nova pre刚创建的原Context。</param><param name="processors">原CollectProcessors真实列表。</param><param name="actualReport">普通回调实际report，仅读引用。</param>
            internal ContextBinding(FactorySnapshot expected, NovaBuildContext actual, List<NovaSDKBuildProcessor> processors, BuildReport actualReport)
            {
                if (expected == null || expected.domain != Domain || expected.types == null || actual == null || processors == null || !ReferenceEquals(actual, NovaBuildShared.Context) || !ReferenceEquals(processors, NovaBuildShared.Processors) || !ReferenceEquals(actual.Report, actualReport) || actual.Target != BuildTarget.Android || processors.Count != expected.types.Length) throw new InvalidOperationException("Nova父合同只接受本域普通构造的真实Context/列表/report。");
                for (int index = 0; index < processors.Count; index++) if (processors[index] == null || processors[index].GetType() != expected.types[index]) throw new InvalidOperationException("普通构造失败、遗漏、替换或TypeCache顺序漂移。");
                if (actual.ManifestRuleSets.Count != 0 || actual.ProguardRules.Count != 0 || actual.AndroidManifest != null || actual.ActivityName != CustomAndroidManifest.c_UnityPlayerActivity) throw new InvalidOperationException("新Context的原始字段已改变。");
                if (expected.bclSha256 != HashFile(typeof(List<>).Assembly.Location, CancellationToken.None) || expected.bclMvid != typeof(List<>).Assembly.ManifestModule.ModuleVersionId.ToString("D")) throw new InvalidOperationException("Nova排序BCL身份在计划后变化。");
                RequireSources(BuildActionCommon.ProjectRoot, CancellationToken.None, expected.discovered.Any(row => row.type == NovaAndroidSdkLeafBuildContract.AdMob));
                factory = expected; context = actual; list = processors; instances = processors.ToArray(); report = actualReport;
                constructed = Observe("constructed");
                if (constructed.instanceState.Any(value => value.EndsWith("=True", StringComparison.Ordinal))) throw new InvalidOperationException("普通构造的Facebook实例初值不是false。");
            }
            /// <summary>观察原Context与同一批实例，明确保留当前pre或post排序和完整规则摘要；不调用虚属性或SDK方法。</summary>
            /// <param name="phase">仅constructed/pre/post三个准确阶段。</param><returns>真实当前材料；缺失或阶段顺序未知拒绝。</returns>
            internal ContextFrame Observe(string phase)
            {
                if (disposed || context == null || !ReferenceEquals(context, NovaBuildShared.Context) || !ReferenceEquals(list, NovaBuildShared.Processors) || !ReferenceEquals(context.Report, report) || context.Target != BuildTarget.Android || list.Count != instances.Length || instances.Any(instance => list.Count(item => ReferenceEquals(item, instance)) != 1)) throw new InvalidOperationException("Nova构建Context、report或同批实例已替换。");
                if (!IsNextPhase(lastPhase, phase)) throw new InvalidOperationException("Nova普通构造/pre/post观察次序缺失、重复或重排。");
                string[] order = list.Select(item => item.GetType().FullName).ToArray(); string[] expected = phase == "constructed" ? factory.discovered.Select(row => row.type).ToArray() : phase == "pre" ? factory.preprocessOrder : phase == "post" ? factory.postprocessOrder : throw new InvalidOperationException("Nova父合同阶段未知。");
                if (!order.SequenceEqual(expected)) throw new InvalidOperationException("Nova普通优先级/tie排序结果不符。");
                if (context.AndroidManifest != null && context.AndroidManifest.GetType() != typeof(CustomAndroidManifest)) throw new InvalidOperationException("Nova实际Manifest不是原领域类型。");
                var frame = new ContextFrame { phase = phase, domain = Domain, order = order, activity = context.ActivityName, manifestRulesSha256 = HashText(JsonConvert.SerializeObject(context.ManifestRuleSets)), proguardRulesSha256 = HashText(JsonConvert.SerializeObject(context.ProguardRules)), documentSha256 = context.AndroidManifest == null ? null : HashText(context.AndroidManifest.OuterXml), instanceState = list.Select(ReadInstanceState).ToArray() };
                lastPhase = phase; return frame;
            }
            /// <summary>仅释放观察引用，不清理原SDK、report、Context、列表或事件。</summary>
            public void Dispose() { disposed = true; context = null; list = null; instances = null; report = null; }
        }

        /// <summary>读取真实Android的完整TypeCache元数据；只核IL/字段/源码，不构造实例、不读取虚Priority或初始化静态类型。</summary>
        /// <param name="pins">八个类型的独立Android真实编译pin。</param><param name="token">原取消令牌。</param><returns>原发现顺序及本BCL的两轮准确排序。</returns>
        internal static FactorySnapshot CaptureFactory(AuditedTypePin[] pins, CancellationToken token)
            => CaptureFactoryCore(pins, token, null);

        /// <summary>由已审源码、准确构造/优先级IL与成员形状建立生产见证；DLL/MVID只绑定本次实际编译身份，不自称历史审核。</summary>
        /// <param name="token">读取已解析包、源码与实际模块时的取消令牌。</param>
        /// <returns>当前Android完整七型或安装AdMob后的八型工厂；未知包、类型、成员和IL均拒绝。</returns>
        internal static FactorySnapshot CaptureReviewedFactory(CancellationToken token)
        {
            string[] packages = CaptureInstalledProcessorPackages(out bool adMobInstalled);
            string[] expected = ReviewedRoster(adMobInstalled);
            Type[] types = TypeCache.GetTypesDerivedFrom<NovaSDKBuildProcessor>().Where(type => !type.IsAbstract).ToArray();
            if (!types.Select(type => type.FullName).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal)))
                throw new InvalidOperationException("Android真实TypeCache与已解析SDK包的完整七/八型集合不一致；不能按Runtime配置过滤。");
            foreach (Type type in types) RequireReviewedProcessorShape(type);
            var current = types.Select(type => new AuditedTypePin { type = type.FullName, assemblyFullName = type.Assembly.FullName,
                mvid = type.Module.ModuleVersionId.ToString("D"), dllSha256 = HashFile(type.Assembly.Location, token) }).ToArray();
            FactorySnapshot result = CaptureFactoryCore(current, token, expected);
            result.admission = "reviewed-source-signature-ctor-priority-il-v1";
            result.adMobPackageInstalled = adMobInstalled;
            result.packageRoster = packages;
            return result;
        }

        /// <summary>返回已审完整工厂名称；AdMob缺席只由包解析及TypeCache证明，不由SDK启用开关决定。</summary>
        /// <param name="adMobInstalled">准确AdMob包是否已解析。</param><returns>七型或八型准确名称的独立数组。</returns>
        internal static string[] ReviewedRoster(bool adMobInstalled) => Specs.Where(spec => adMobInstalled || spec.type != NovaAndroidSdkLeafBuildContract.AdMob).Select(spec => spec.type).ToArray();

        /// <summary>只读实际已解析包的准确位置/版本；当前合同要求七个基础包，AdMob只允许明确安装或明确缺席。</summary>
        /// <param name="adMobInstalled">返回AdMob包实际解析状态。</param><returns>与SDK工厂对应的完整包身份。</returns>
        private static string[] CaptureInstalledProcessorPackages(out bool adMobInstalled)
        {
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            string[] required = { "com.solotopia.nova.framework", "com.solotopia.nova.framework.sdk.appsflyer", "com.solotopia.nova.framework.sdk.facebook",
                "com.solotopia.nova.framework.sdk.max", "com.solotopia.nova.framework.sdk.googlesignin", "com.solotopia.nova.framework.sdk.applesignin", "com.solotopia.nova.framework.sdk.firebase" };
            if (packages == null || required.Any(name => packages.Count(package => package.name == name) != 1))
                throw new InvalidOperationException("Android已审七型工厂的基础包未完整解析；不能拿UPMPackages源码存在代替安装。");
            const string adMob = "com.solotopia.nova.framework.sdk.admob";
            if (packages.Count(package => package.name == adMob) > 1) throw new InvalidOperationException("AdMob包解析身份不唯一。");
            adMobInstalled = packages.Any(package => package.name == adMob);
            return packages.Where(package => required.Contains(package.name, StringComparer.Ordinal) || package.name == adMob)
                .Select(package => package.name + "|" + package.version + "|" + Path.GetFullPath(package.resolvedPath)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        /// <summary>核已审Android编译分支的准确成员签名；复杂SDK方法仍须独立叶子合同，本工厂不批准其副作用。</summary>
        /// <param name="type">实际TypeCache中的具体SDK类型。</param>
        private static void RequireReviewedProcessorShape(Type type)
        {
            if (type == null || !type.IsSealed || type.BaseType != typeof(NovaSDKBuildProcessor) || type.TypeInitializer != null || type.ContainsGenericParameters)
                throw new InvalidOperationException("Android源码见证的类型结构未知。");
            var names = new Dictionary<string, string[]>(StringComparer.Ordinal) {
                [NovaAndroidSdkLeafBuildContract.Native] = new[] { "OnPreprocessBuildOnAndroid", "HasEnabledAppInstaller", "CloseObservedPreview" },
                [NovaAndroidSdkLeafBuildContract.AppsFlyer] = new[] { "get_PreprocessPriority", "get_PostprocessPriority", "OnPreprocessBuildOnAndroid", "CreateAndroidManifestRules", "EnsureGradleTemplateProperty" },
                [NovaAndroidSdkLeafBuildContract.AdMob] = new[] { "get_PreprocessPriority", "OnPreprocessBuildOnAndroid", "OnPreprocessBuildOniOS", "ApplySettings", "EnsureFolder" },
                [NovaAndroidSdkLeafBuildContract.Facebook] = new[] { "get_PreprocessPriority", "get_PostprocessPriority", "OnPreprocessBuildOnAndroid", "OnAfterNovaPreprocessBuildOnAndroid", "OnPreprocessBuildOniOS", "ApplyFacebookSettings" },
                [NovaAndroidSdkLeafBuildContract.Max] = new[] { "get_PreprocessPriority", "get_PostprocessPriority", "OnPreprocessBuildOnAndroid", "OnPreprocessBuildOniOS", "ApplyMaxSettings" },
                [NovaAndroidSdkLeafBuildContract.Google] = new[] { "get_PreprocessPriority", "get_PostprocessPriority", "OnPreprocessBuildOnAndroid" },
                [NovaAndroidSdkLeafBuildContract.Apple] = new[] { "get_PreprocessPriority", "get_PostprocessPriority", "OnPreprocessBuildOniOS" },
                [NovaAndroidSdkLeafBuildContract.Firebase] = new[] { "get_PreprocessPriority", "get_PostprocessPriority", "OnPreprocessBuildOnAndroid", "CreateAndroidManifestRules" },
            };
            if (!names.TryGetValue(type.FullName, out string[] allowed)) throw new InvalidOperationException("Android源码见证类型未审。");
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (methods.Any(method => !allowed.Contains(method.Name, StringComparer.Ordinal) || method.IsGenericMethod || method.IsAbstract || method.GetMethodBody() == null) ||
                methods.GroupBy(method => method.Name, StringComparer.Ordinal).Any(group => group.Count() != 1) ||
                allowed.Where(name => name != "EnsureFolder").Any(name => methods.All(method => method.Name != name)))
                throw new InvalidOperationException("Android SDK新增/重载/未知方法不能继承旧源码见证。" + type.FullName);
            foreach (MethodInfo method in methods.Where(method => method.Name.StartsWith("On", StringComparison.Ordinal)))
                if (method.IsStatic || !method.IsPublic || !method.IsVirtual || method.ReturnType != typeof(void) ||
                    !method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(new[] { typeof(BuildReport), typeof(NovaBuildContext) }) ||
                    method.GetBaseDefinition().DeclaringType != typeof(NovaSDKBuildProcessor))
                    throw new InvalidOperationException("Android SDK普通回调签名改变。" + type.FullName + "." + method.Name);
        }

        /// <summary>分别保留旧八型独立pin入口与生产源码见证入口；两者均核全部构造、优先级与实际模块身份。</summary>
        /// <param name="pins">旧入口的独立审核pin，或生产语义准入后取得的本次身份。</param><param name="token">本次取消令牌。</param>
        /// <param name="reviewedRoster">null保持旧八型要求；生产入口传已解析包证明的准确集合。</param><returns>真实完整工厂快照。</returns>
        private static FactorySnapshot CaptureFactoryCore(AuditedTypePin[] pins, CancellationToken token, string[] reviewedRoster)
        {
            if (!NovaBuildParentObservers.IsInactive) throw new InvalidOperationException("Nova parent已有活动观察者，不能另取工厂计划。");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw new InvalidOperationException("Nova父metadata仅支持实际Android活动域，不移植WebGL身份。");
            string[] expected = reviewedRoster ?? Specs.Select(spec => spec.type).ToArray();
            string root = BuildActionCommon.ProjectRoot; RequireSources(root, token, expected.Contains(NovaAndroidSdkLeafBuildContract.AdMob));
            if (reviewedRoster == null) RequirePins(pins);
            else if (pins == null || pins.Length != expected.Length || !pins.Select(pin => pin?.type).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal)))
                throw new InvalidOperationException("Android生产工厂的实际模块身份集合不完整。");
            Type[] types = TypeCache.GetTypesDerivedFrom<NovaSDKBuildProcessor>().Where(type => !type.IsAbstract).ToArray();
            if (types.Length != expected.Length || types.Select(type => type.FullName).Distinct().Count() != types.Length || !types.Select(type => type.FullName).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal))) throw new InvalidOperationException("真实Nova SDK完整roster不同于已审集合；不按Runtime启用过滤未知类型。");
            var rows = new List<ProcessorRow>(); var memo = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Type type in types)
            {
                token.ThrowIfCancellationRequested(); ProcessorSpec spec = Specs.Single(value => value.type == type.FullName); AuditedTypePin pin = pins.Single(value => value.type == type.FullName);
                if (!memo.TryGetValue(type.Assembly.Location, out string dll)) { dll = HashFile(type.Assembly.Location, token); memo.Add(type.Assembly.Location, dll); }
                if (type.Assembly.FullName != pin.assemblyFullName || type.Module.ModuleVersionId.ToString("D") != pin.mvid || dll != pin.dllSha256 || !type.IsSealed || type.BaseType != typeof(NovaSDKBuildProcessor) || type.TypeInitializer != null) throw new InvalidOperationException("Nova SDK类型、Android DLL/MVID或cctor分支未审：" + type.FullName);
                RequireSimpleConstructor(typeof(NovaSDKBuildProcessor), typeof(object)); RequireSimpleConstructor(type, typeof(NovaSDKBuildProcessor));
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                bool facebook = type.FullName == "NovaFramework.SDK.Facebook.Editor.FacebookPluginBuildProcessor";
                if (facebook ? fields.Length != 1 || fields[0].Name != "m_AndroidSettingsApplied" || fields[0].FieldType != typeof(bool) || !fields[0].IsPrivate || fields[0].IsInitOnly : fields.Length != 0) throw new InvalidOperationException("Nova SDK实例字段不属于已审默认构造分支。");
                foreach (var pair in new[] { (name: nameof(NovaSDKBuildProcessor.PreprocessPriority), value: spec.pre), (name: nameof(NovaSDKBuildProcessor.PostprocessPriority), value: spec.post) })
                {
                    MethodInfo getter = type.GetProperty(pair.name)?.GetMethod;
                    if (getter == null || getter.IsStatic || getter.ReturnType != typeof(int) || getter.GetParameters().Length != 0 || getter.GetBaseDefinition().DeclaringType != typeof(NovaSDKBuildProcessor) || !IsConstantIntGetter(getter.GetMethodBody()?.GetILAsByteArray(), pair.value)) throw new InvalidOperationException("Nova Priority不是已审IL常量，不调用getter：" + type.FullName + "." + pair.name);
                }
                rows.Add(new ProcessorRow { type = type.FullName, assemblyFullName = pin.assemblyFullName, mvid = pin.mvid, dllSha256 = dll, sourceSha256 = spec.sha256, constructorIlSha256 = Hash(type.GetConstructor(Type.EmptyTypes).GetMethodBody().GetILAsByteArray()), pre = spec.pre, post = spec.post });
            }
            ProcessorRow[] pre = Sort(rows.ToArray(), true), post = Sort(pre, false);
            return new FactorySnapshot { domain = Domain, bclSha256 = HashFile(typeof(List<>).Assembly.Location, token), bclMvid = typeof(List<>).Assembly.ManifestModule.ModuleVersionId.ToString("D"), sourcePins = ParentSources.Concat(Specs.Where(spec => expected.Contains(spec.type)).Select(spec => new[] { spec.source, spec.sha256 })).Select(pair => pair[0] + "|" + pair[1]).ToArray(), discovered = rows.ToArray(), preprocessOrder = pre.Select(row => row.type).ToArray(), postprocessOrder = post.Select(row => row.type).ToArray(), types = types };
        }

        /// <summary>正常构建写前重核本域工厂见证、真实元数据/源码与BCL次序；不构造SDK，不从持久DTO补签types。</summary>
        /// <param name="expected">原CaptureFactory实际同域结果，保留其非序列化Type见证。</param><param name="token">原取消令牌。</param>
        internal static void VerifyFactoryBefore(FactorySnapshot expected, CancellationToken token)
        {
            if (expected == null || expected.domain != Domain || expected.types == null || expected.discovered == null) throw new InvalidOperationException("Nova parent缺同域真实工厂见证，不能从JSON恢复。");
            var pins = expected.discovered.Select(row => new AuditedTypePin { type = row.type, assemblyFullName = row.assemblyFullName, mvid = row.mvid, dllSha256 = row.dllSha256 }).ToArray();
            FactorySnapshot actual = expected.admission == "reviewed-source-signature-ctor-priority-il-v1" ? CaptureReviewedFactory(token) : CaptureFactory(pins, token);
            if (!Same(expected, actual) || !expected.types.SequenceEqual(actual.types)) throw new InvalidOperationException("Nova parent工厂在普通构建写前漂移。");
        }

        /// <summary>严格检查独立pin集合；缺失或重复不能从当前值补签。</summary>
        /// <param name="pins">独立审核的Android身份。</param>
        internal static void RequirePins(AuditedTypePin[] pins)
        {
            if (pins == null || pins.Length != Specs.Length || pins.Any(pin => pin == null || string.IsNullOrEmpty(pin.assemblyFullName) || !Guid.TryParseExact(pin.mvid, "D", out _) || !IsHash(pin.dllSha256)) || !pins.Select(pin => pin.type).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(Specs.Select(spec => spec.type).OrderBy(value => value, StringComparer.Ordinal))) throw new InvalidOperationException("Nova Android八型（含AdMob）独立pin缺失/重复/格式未知。");
        }

        /// <summary>精确识别默认构造，仅允许ldarg.0/call准确基类无参ctor/ret与nop；不执行构造或使用未初始化实例。</summary>
        /// <param name="type">只读实际声明类型。</param><param name="baseType">准确允许的直接基类。</param>
        internal static void RequireSimpleConstructor(Type type, Type baseType)
        {
            if (type == null || type.BaseType != baseType || type.TypeInitializer != null) throw new InvalidOperationException("Nova默认构造的基类或cctor未知。");
            ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (constructors.Length != 1 || constructors[0].GetParameters().Length != 0) throw new InvalidOperationException("Nova构造签名未知。");
            ConstructorInfo ctor = constructors[0]; byte[] il = ctor.GetMethodBody()?.GetILAsByteArray();
            if (!IsSimpleConstructorIl(il, metadataToken => ctor.Module.ResolveMethod(metadataToken) is ConstructorInfo target && target.DeclaringType == baseType && !target.IsStatic && target.GetParameters().Length == 0)) throw new InvalidOperationException("Nova构造正文包含未审行为，不执行它。");
        }

        /// <summary>只读构造IL纯门，call token必须由调用方按准确Module解析为直接基类无参构造。</summary>
        /// <param name="il">完整实际IL。</param><param name="isBaseConstructor">仅元数据解析predicate，不调用目标方法。</param><returns>严格有限指令序列时为真。</returns>
        internal static bool IsSimpleConstructorIl(byte[] il, Func<int, bool> isBaseConstructor)
        {
            if (il == null || isBaseConstructor == null) return false; int index = 0; SkipNops(il, ref index);
            if (index >= il.Length || il[index++] != 0x02) return false; SkipNops(il, ref index);
            if (index >= il.Length || il[index++] != 0x28 || index + 4 > il.Length) return false;
            int token = BitConverter.ToInt32(il, index); index += 4; if (!isBaseConstructor(token)) return false; SkipNops(il, ref index);
            if (index >= il.Length || il[index++] != 0x2a) return false; SkipNops(il, ref index); return index == il.Length;
        }

        /// <summary>只识别常量整数Priority的有限IL，不执行属性，不接受调用、字段或跳转。</summary>
        /// <param name="il">完整getter IL。</param><param name="expected">源已审核常量。</param><returns>完整指令与常量相符时为真。</returns>
        internal static bool IsConstantIntGetter(byte[] il, int expected)
        {
            if (il == null) return false; int index = 0; SkipNops(il, ref index); if (index >= il.Length) return false;
            byte code = il[index++]; int value;
            if (code >= 0x15 && code <= 0x1e) value = code - 0x16;
            else if (code == 0x1f && index < il.Length) value = unchecked((sbyte)il[index++]);
            else if (code == 0x20 && index + 4 <= il.Length) { value = BitConverter.ToInt32(il, index); index += 4; }
            else return false;
            SkipNops(il, ref index); if (index >= il.Length || il[index++] != 0x2a) return false; SkipNops(il, ref index); return value == expected && index == il.Length;
        }

        /// <summary>只跳过当前指令位置的nop，不删除token或常量操作数字节中的0。</summary>
        /// <param name="il">完整IL。</param><param name="index">当前解码位置。</param>
        private static void SkipNops(byte[] il, ref int index) { while (index < il.Length && il[index] == 0) index++; }

        /// <summary>按当前实际BCL重现原List.Sort，保留TypeCache输入顺序和tie实际算法；post以pre结果为输入。</summary>
        /// <param name="input">原完整次序。</param><param name="preprocess">true选pre，否则post常量。</param><returns>独立排序数组，原输入不变。</returns>
        internal static ProcessorRow[] Sort(ProcessorRow[] input, bool preprocess)
        {
            if (input == null || input.Any(row => row == null || string.IsNullOrEmpty(row.type)) || input.Select(row => row.type).Distinct().Count() != input.Length) throw new InvalidOperationException("Nova排序输入缺失或重复。");
            var list = input.ToList(); list.Sort((left, right) => (preprocess ? left.pre : left.post).CompareTo(preprocess ? right.pre : right.post)); return list.ToArray();
        }

        /// <summary>使用既有领域XML/规则算法产生准确父pre输出；不会写源或执行任何SDK。</summary>
        /// <param name="projectRoot">原工程根；测试只能用独占tmp。</param><param name="settings">已经冻结的纯值。</param><param name="orderedEffects">按真实pre排序、由各SDK有限分支另行证明的效果；本方法不替它们准入。</param><param name="token">原取消令牌。</param><returns>自有预测缓冲及准确摘要；调用方必须Dispose。</returns>
        internal static Prepared PrepareOutputs(string projectRoot, Settings settings, ContextEffect[] orderedEffects, CancellationToken token)
        {
            if (settings == null || settings.bundleId == null || settings.versionCode == null || settings.versionName == null || orderedEffects == null || orderedEffects.Any(effect => effect == null || string.IsNullOrEmpty(effect.type)) || orderedEffects.Select(effect => effect.type).Distinct().Count() != orderedEffects.Length) throw new InvalidOperationException("Nova父预测缺少完整设置或有序效果。");
            string root = Path.GetFullPath(projectRoot); FileState template = ReadFile(root, TemplatePath, token), manifestBefore = ReadFile(root, ManifestPath, token), proguardBefore = ReadFile(root, ProguardPath, token);
            // 构造的是原领域XML树，只有读文件；并非SDK processor构造，也不Save源文件。
            byte[] templateBytes = File.ReadAllBytes(Path.Combine(root, TemplatePath)); if (Hash(templateBytes) != template.sha256) throw new InvalidOperationException("Nova模板在预测前变化。");
            var document = new CustomAndroidManifest(templateBytes, Path.Combine(root, ManifestPath)); string selected = document.SelectUnityLauncherActivity(settings.entry), activity = selected;
            var rules = new List<ManifestRuleSet>(); var proguard = new List<(string sdkName, string rules)>(); bool registered = false;
            foreach (ContextEffect effect in orderedEffects)
            {
                token.ThrowIfCancellationRequested();
                if (!registered) { activity = effect.registeredActivity; registered = !string.IsNullOrEmpty(activity); }
                if (effect.manifestRules != null) rules.AddRange(effect.manifestRules.Where(value => value != null));
                if (effect.proguardRules != null) proguard.AddRange(effect.proguardRules.Where(value => !string.IsNullOrEmpty(value.rules)));
            }
            if (!registered) activity = selected;
            NovaManifestBootstrapper.InjectFrozen(document, activity, settings.bundleId, settings.versionCode, settings.versionName, settings.orientation);
            foreach (ManifestRuleSet rule in rules) document.Apply(rule);
            byte[] expectedManifest;
            using (var stream = new MemoryStream())
            {
                using (var writer = new XmlTextWriter(stream, new UTF8Encoding(false)) { Formatting = System.Xml.Formatting.Indented }) { document.Save(writer); writer.Flush(); expectedManifest = stream.ToArray(); }
            }
            byte[] expectedProguard = Utf8.GetBytes(NovaProguardBootstrapper.BuildContents(proguard));
            if (expectedManifest.Length > MaxFileBytes || expectedProguard.Length > MaxFileBytes) throw new InvalidOperationException("Nova父输出预测超过2MiB，不截断。");
            if (!Same(template, ReadFile(root, TemplatePath, token)) || !Same(manifestBefore, ReadFile(root, ManifestPath, token)) || !Same(proguardBefore, ReadFile(root, ProguardPath, token))) throw new InvalidOperationException("预测期间实际源/meta/属性漂移。");
            return new Prepared(root, new OutputSummary { template = template, manifestBefore = manifestBefore, proguardBefore = proguardBefore, manifestExpectedSha256 = Hash(expectedManifest), proguardExpectedSha256 = Hash(expectedProguard), activity = activity, documentExpectedSha256 = HashText(document.OuterXml), expectedInstanceState = orderedEffects.Select(effect => effect.type + (effect.type == "NovaFramework.SDK.Facebook.Editor.FacebookPluginBuildProcessor" ? "|m_AndroidSettingsApplied=" + effect.facebookSettingsApplied.ToString() : "|fields=0")).ToArray(), manifestRulesSha256 = HashText(JsonConvert.SerializeObject(rules)), proguardRulesSha256 = HashText(JsonConvert.SerializeObject(proguard)), settingsSha256 = HashText(JsonConvert.SerializeObject(settings)) }, expectedManifest, expectedProguard);
        }

        /// <summary>纯后态文件门，完整字节摘要/长度及原meta/属性都必须相符，不放宽任意目录。</summary>
        /// <param name="before">原准确文件。</param><param name="after">本次实际文件。</param><param name="expectedSha">唯一完整预期摘要。</param><param name="expectedLength">完整预期字节长度。</param><returns>全部条件通过时为真。</returns>
        internal static bool AfterFile(FileState before, FileState after, string expectedSha, long expectedLength) => before != null && after != null && IsHash(expectedSha) && expectedLength >= 0 && before.relativePath == after.relativePath && before.metaSha256 == after.metaSha256 && before.attributes == after.attributes && before.metaAttributes == after.metaAttributes && after.sha256 == expectedSha && after.length == expectedLength;

        /// <summary>纯阶段顺序门；一次普通链只接受constructed→pre→post，不据后来状态补造缺失阶段。</summary>
        /// <param name="previous">本scope最后成功阶段，初始null。</param><param name="next">此次实际观察阶段。</param><returns>准确下一阶段时为真。</returns>
        internal static bool IsNextPhase(string previous, string next) => previous == null ? next == "constructed" : previous == "constructed" ? next == "pre" : previous == "pre" && next == "post";

        /// <summary>核真实Context阶段摘要，预测只用于准确匹配，不把它当实际观测或SDK调用证据。</summary>
        /// <param name="frame">普通链真实pre后观测。</param><param name="expected">已冻结父预测摘要。</param><returns>Context完整规则与最终Activity一致时为真。</returns>
        internal static bool MatchesExpectedPre(ContextFrame frame, OutputSummary expected) => frame != null && expected != null && frame.phase == "pre" && frame.activity == expected.activity && frame.manifestRulesSha256 == expected.manifestRulesSha256 && frame.proguardRulesSha256 == expected.proguardRulesSha256 && frame.documentSha256 == expected.documentExpectedSha256 && frame.instanceState != null && expected.expectedInstanceState != null && frame.instanceState.SequenceEqual(expected.expectedInstanceState);

        /// <summary>只读单个已存在源/meta及无链接祖先；缺失资产/GUID创建或新目录需要另一个有限合同。</summary>
        /// <param name="projectRoot">准确实际根。</param><param name="relative">只允许固定三个角色。</param><param name="token">取消令牌。</param><returns>无正文的完整摘要。</returns>
        internal static FileState ReadFile(string projectRoot, string relative, CancellationToken token)
        {
            if (relative != ManifestPath && relative != ProguardPath && relative != TemplatePath) throw new InvalidOperationException("Nova父文件角色未知。");
            string root = Path.GetFullPath(projectRoot), path = Path.Combine(root, relative); RequireUnlinked(path); RequireUnlinked(path + ".meta");
            var info = new FileInfo(path); var meta = new FileInfo(path + ".meta"); if (!info.Exists || !meta.Exists || info.Length > MaxFileBytes || meta.Length > 65536) throw new InvalidOperationException("Nova父文件/meta缺失或越界，不制造新GUID。");
            long length = info.Length, modified = info.LastWriteTimeUtc.Ticks, metaModified = meta.LastWriteTimeUtc.Ticks; int attrs = (int)info.Attributes, metaAttrs = (int)meta.Attributes;
            string fileSha = HashFile(path, token), metaSha = HashFile(path + ".meta", token); info.Refresh(); meta.Refresh();
            if (!info.Exists || !meta.Exists || info.Length != length || info.LastWriteTimeUtc.Ticks != modified || meta.LastWriteTimeUtc.Ticks != metaModified || (int)info.Attributes != attrs || (int)meta.Attributes != metaAttrs) throw new InvalidOperationException("Nova父文件在捕获中漂移。");
            return new FileState { relativePath = relative, length = length, sha256 = fileSha, metaSha256 = metaSha, attributes = attrs, metaAttributes = metaAttrs };
        }

        /// <summary>完整祖先拒绝链接，包含悬空条目；不解析到其它工程或写入任何目录。</summary>
        /// <param name="path">固定实际文件或meta。</param>
        private static void RequireUnlinked(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Nova父源路径含链接。"); }
                catch (IOException error) when (error is FileNotFoundException || error is DirectoryNotFoundException)
                {
                    string parent = Path.GetDirectoryName(current);
                    if (parent != null && Directory.Exists(parent) && Directory.EnumerateFileSystemEntries(parent).Any(entry => Path.GetFileName(entry) == Path.GetFileName(current))) throw new InvalidOperationException("Nova父源路径含悬空或未知条目。", error);
                }
            }
        }

        /// <summary>逐源校验父链及全部八型源码，不把旧磁盘pin提升为Android编译身份。</summary>
        /// <param name="root">原工程根。</param><param name="token">取消令牌。</param>
        private static void RequireSources(string root, CancellationToken token, bool includeAdMob = true)
        {
            foreach (string[] pair in ParentSources.Concat(Specs.Where(spec => includeAdMob || spec.type != NovaAndroidSdkLeafBuildContract.AdMob).Select(spec => new[] { spec.source, spec.sha256 })))
            {
                string path = ResolveReviewedSource(root, pair[0]); RequireUnlinked(path); if (HashFile(path, token) != pair[1]) throw new InvalidOperationException("Nova父链源指纹变化：" + pair[0]);
            }
        }

        /// <summary>生产源沿实际已解析包定位，审核标签不等于消费项目目录；独立纯文件fixture仍只读取其自有根。</summary>
        /// <param name="root">准确工程根或原纯文件fixture根。</param><param name="reviewedPath">本类与叶子固定表中的已审逻辑位置。</param><returns>实际已解析包内同一相对源文件，不回退到未安装开发包。</returns>
        internal static string ResolveReviewedSource(string root, string reviewedPath)
        {
            if (System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) != System.IO.Path.GetFullPath(BuildActionCommon.ProjectRoot).TrimEnd(System.IO.Path.DirectorySeparatorChar)) return System.IO.Path.Combine(root, reviewedPath);
            string packageName, relative;
            if (reviewedPath.StartsWith("Assets/Framework/", StringComparison.Ordinal)) { packageName = "com.solotopia.nova.framework"; relative = reviewedPath.Substring("Assets/Framework/".Length); }
            else if (reviewedPath.StartsWith("UPMPackages/", StringComparison.Ordinal))
            {
                string value = reviewedPath.Substring("UPMPackages/".Length); int slash = value.IndexOf('/');
                if (slash <= 0) throw new InvalidOperationException("Android已审包源角色无相对文件。");
                packageName = value.Substring(0, slash); relative = value.Substring(slash + 1);
            }
            else if (reviewedPath.StartsWith("Library/PackageCache/", StringComparison.Ordinal))
            {
                string value = reviewedPath.Substring("Library/PackageCache/".Length); int slash = value.IndexOf('/'), at = value.IndexOf('@');
                if (at <= 0 || slash <= at) throw new InvalidOperationException("Android已审缓存源角色无准确包名。");
                packageName = value.Substring(0, at); relative = value.Substring(slash + 1);
            }
            else throw new InvalidOperationException("Android未审核的源码位置角色。");
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            if (packages == null || packages.Count(package => package.name == packageName) != 1) throw new InvalidOperationException("Android已审源码所属包未唯一解析：" + packageName);
            string resolved = System.IO.Path.GetFullPath(packages.Single(package => package.name == packageName).resolvedPath);
            string result = System.IO.Path.GetFullPath(System.IO.Path.Combine(resolved, relative));
            if (!result.StartsWith(resolved + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("Android已审源角色越出实际包根。");
            return result;
        }

        /// <summary>只读取已审唯一bool实例字段；不读取SDK设置、静态任务或凭据。</summary>
        /// <param name="instance">原普通构造的具体SDK实例。</param><returns>固定type及bool诊断，其他类型无实例状态。</returns>
        private static string ReadInstanceState(NovaSDKBuildProcessor instance)
        {
            if (instance.GetType().FullName != "NovaFramework.SDK.Facebook.Editor.FacebookPluginBuildProcessor") return instance.GetType().FullName + "|fields=0";
            FieldInfo field = instance.GetType().GetField("m_AndroidSettingsApplied", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null || field.FieldType != typeof(bool)) throw new InvalidOperationException("Facebook已审实例状态字段改变。");
            return instance.GetType().FullName + "|m_AndroidSettingsApplied=" + ((bool)field.GetValue(instance)).ToString();
        }

        /// <summary>固定格式小写SHA判定，不接受缺失材料。</summary>
        /// <param name="value">完整摘要。</param><returns>准确64位hex时为真。</returns>
        private static bool IsHash(string value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        /// <summary>纯值逐字段比较，不投影未知变化。</summary>
        /// <param name="left">原完整DTO。</param><param name="right">当前完整DTO。</param><returns>完整序列化值相同时为真。</returns>
        private static bool Same(object left, object right) => JsonConvert.SerializeObject(left) == JsonConvert.SerializeObject(right);
        /// <summary>完整文字SHA，不输出正文。</summary>
        /// <param name="value">自有完整文字。</param><returns>准确摘要。</returns>
        private static string HashText(string value) => Hash(Utf8.GetBytes(value));
        /// <summary>完整自有字节SHA，不截断。</summary>
        /// <param name="value">完整字节。</param><returns>准确摘要。</returns>
        private static string Hash(byte[] value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", "").ToLowerInvariant(); }
        /// <summary>只读流Hash，每块检查原取消令牌。</summary>
        /// <param name="path">准确实际源/DLL。</param><param name="token">取消令牌。</param><returns>完整SHA。</returns>
        private static string HashFile(string path, CancellationToken token)
        {
            using (var file = File.OpenRead(path)) using (var sha = SHA256.Create())
            {
                byte[] buffer = new byte[65536]; int read; while ((read = file.Read(buffer, 0, buffer.Length)) != 0) { token.ThrowIfCancellationRequested(); sha.TransformBlock(buffer, 0, read, buffer, 0); } token.ThrowIfCancellationRequested(); sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0); return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
