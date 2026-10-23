/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HybridClrStripBuildContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Android/WebGL Generate 的官方 StripAOT 输入与正常状态转换合同
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using global::HybridCLR.Editor;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>只读捕获 Android/WebGL StripAOT 的输入与官方后态；不调用生成、不改变设置或恢复进程环境。</summary>
    internal static class HybridClrStripBuildContract
    {
        internal const string MsvcFlag = "/D_SILENCE_STDEXT_HASH_DEPRECATION_WARNINGS";

        [Serializable]
        internal sealed class Snapshot
        {
            public string target;
            public string targetGroup;
            public bool development;
            public bool allowDebugging;
            public bool connectProfiler;
            public bool deepProfiling;
            public bool buildScriptsOnly;
            public bool exportAndroidProject;
            public bool installInBuildFolder;
            public bool buildAppBundle;
            public string buildLocation;
            public bool methodBridgeCheckDisabled;
            public bool useGlobalIl2cpp;
            public string localIl2cppDirectory;
            public int explicitBuildOptions;
            public int explicitSubtarget;
            public string unityIl2cppHash;
            public string clHash;
            public string expectedUnityIl2cppAfterHash;
            public string expectedClAfterHash;
            public string assemblySha256;
            public string[] sourceFingerprints;
        }

        /// <summary>捕获本机 Android 分支；所有全局值必须由原实现消费，未审平台沿用其原有合同。</summary>
        /// <returns>只读输入快照、当前进程值摘要和官方正常后态摘要；不执行生成或更改状态。</returns>
        internal static Snapshot CaptureAndroid() => CaptureTarget(BuildTarget.Android);

        /// <summary>只读捕获当前真实 WebGL Generate 的官方 StripAOT 输入与进程转换。</summary>
        /// <returns>当前真实输入；不是生成成功证据，也不校验旧 MethodBridge 的开发标记。</returns>
        internal static Snapshot CaptureWebGL()
        {
            Snapshot snapshot = CaptureTarget(BuildTarget.WebGL);
            if (snapshot.assemblySha256 != "d32a7ac1477f862ff138394ef2d383925f64de0dc56a3b27040e5152e888f2bd")
                throw new InvalidOperationException("WebGL Generate 当前完整 HybridCLR 编译身份未审；不以当前捕获替代已审实现。");
            snapshot.sourceFingerprints = snapshot.sourceFingerprints.Concat(RequireReviewedWebGlGenerateSources()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return snapshot;
        }

        /// <summary>捕获两个已审平台的共同输入；禁止据请求切换实际构建目标。</summary>
        /// <param name="target">只允许调用方选择已审 Android 或 WebGL，且必须等于当前真实目标。</param>
        /// <returns>输入快照及官方正常后态摘要。</returns>
        private static Snapshot CaptureTarget(BuildTarget target)
        {
            if ((target != BuildTarget.Android && target != BuildTarget.WebGL) || EditorUserBuildSettings.activeBuildTarget != target)
                throw new InvalidOperationException("StripAOT 合同不适用于当前 Target。");
            string[] sources = RequireReviewedSources();
            bool development = EditorUserBuildSettings.development;
            string cl = Environment.GetEnvironmentVariable("_CL_");
            string il2cpp = Environment.GetEnvironmentVariable("UNITY_IL2CPP_PATH");
            bool useGlobal = SettingsUtil.HybridCLRSettings.useGlobalIl2cpp;
            Type check = typeof(SettingsUtil).Assembly.GetType("HybridCLR.Editor.BuildProcessors.CheckSettings", true);
            PropertyInfo disabled = check.GetProperty("DisableMethodBridgeDevelopmentFlagChecking", BindingFlags.Public | BindingFlags.Static);
            if (disabled == null || disabled.PropertyType != typeof(bool) || check.TypeInitializer != null)
                throw new InvalidOperationException("HybridCLR 检查开关不符合已审自动属性合同。");
            var result = new Snapshot
            {
                target = target.ToString(),
                targetGroup = BuildPipeline.GetBuildTargetGroup(target).ToString(),
                development = development,
                allowDebugging = EditorUserBuildSettings.allowDebugging,
                connectProfiler = EditorUserBuildSettings.connectProfiler,
                deepProfiling = EditorUserBuildSettings.buildWithDeepProfilingSupport,
                buildScriptsOnly = EditorUserBuildSettings.buildScriptsOnly,
                exportAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject,
                installInBuildFolder = EditorUserBuildSettings.installInBuildFolder,
                buildAppBundle = EditorUserBuildSettings.buildAppBundle,
                buildLocation = EditorUserBuildSettings.GetBuildLocation(target),
                methodBridgeCheckDisabled = (bool)disabled.GetValue(null),
                useGlobalIl2cpp = useGlobal,
                localIl2cppDirectory = SettingsUtil.LocalIl2CppDir,
                unityIl2cppHash = HashEnvironment(il2cpp),
                clHash = HashEnvironment(cl),
                expectedUnityIl2cppAfterHash = HashEnvironment(useGlobal ? string.Empty : SettingsUtil.LocalIl2CppDir),
                expectedClAfterHash = HashEnvironment(GetOfficialClAfter(cl)),
                assemblySha256 = HashBytes(File.ReadAllBytes(typeof(SettingsUtil).Assembly.Location)),
                sourceFingerprints = sources,
            };
            result.explicitBuildOptions = (int)GetExplicitAndroidBuildOptions(result.development, result.allowDebugging, result.connectProfiler, result.deepProfiling);
#if UNITY_SERVER
            result.explicitSubtarget = (int)StandaloneBuildSubtarget.Server;
#else
            result.explicitSubtarget = 0;
#endif
            return result;
        }

        /// <summary>精确重现当前包在 Android/WebGL 传给 BuildPlayer 的相同 options；不把全局 scripts-only 偷换为显式选项。</summary>
        /// <param name="development">当前冻结的 Development Build 值。</param>
        /// <param name="debugging">当前冻结的允许调试值，仅在开发构建中生效。</param>
        /// <param name="profiler">当前冻结的连接 Profiler 值，仅在这两个已审平台的开发构建中生效。</param>
        /// <param name="deepProfiling">当前冻结的深度分析值，仅在开发构建中生效。</param>
        /// <returns>与已审官方 Android/WebGL 分支一致的显式 BuildOptions。</returns>
        internal static BuildOptions GetExplicitAndroidBuildOptions(bool development, bool debugging, bool profiler, bool deepProfiling)
        {
            BuildOptions result = BuildOptions.None;
            if (development) result |= BuildOptions.Development;
            if (debugging && development) result |= BuildOptions.AllowDebugging;
            if (profiler && development) result |= BuildOptions.ConnectWithProfiler;
            if (deepProfiling && development) result |= BuildOptions.EnableDeepProfilingSupport;
#if UNITY_2021_2_OR_NEWER
            result |= BuildOptions.CleanBuildCache;
#endif
            return result;
        }

        /// <summary>计算官方 _CL_ 追加规则，保留原字符串且不重复添加已存在的标志。</summary>
        /// <param name="before">进入构建前的原始 _CL_ 值；允许 null 或空字符串。</param>
        /// <returns>官方回调正常执行后的唯一字符串值，不写入真实进程环境。</returns>
        internal static string GetOfficialClAfter(string before)
        {
            if (string.IsNullOrEmpty(before)) return MsvcFlag;
            return before.Contains(MsvcFlag) ? before : before + " " + MsvcFlag;
        }

        /// <summary>比较 Plan 与 Execute 前态；进入时检查开关必须为 false，避免官方 finally 固定 false 覆盖其他操作状态。</summary>
        /// <param name="expected">Plan 保存的冻结快照。</param>
        /// <param name="actual">Execute 前重新读取的快照。</param>
        /// <returns>必须稳定的输入、环境摘要和 false 检查开关均一致时返回 true。</returns>
        internal static bool BeforeEqual(Snapshot expected, Snapshot actual)
        {
            return StableEqual(expected, actual) && !expected.methodBridgeCheckDisabled && !actual.methodBridgeCheckDisabled &&
                expected.unityIl2cppHash == actual.unityIl2cppHash && expected.clHash == actual.clHash;
        }

        /// <summary>验证成功生成后的官方转换；进程变量允许且必须进入已冻结后态，不盲目恢复。</summary>
        /// <param name="before">进入本次 Generate 前保存的冻结快照。</param>
        /// <param name="after">本次 Generate 正常返回后读取的真实快照。</param>
        /// <returns>稳定输入保持不变且进程环境等于冻结的官方后态时返回 true。</returns>
        internal static bool AfterEqual(Snapshot before, Snapshot after)
        {
            return StableEqual(before, after) && !before.methodBridgeCheckDisabled && !after.methodBridgeCheckDisabled &&
                before.expectedUnityIl2cppAfterHash == after.unityIl2cppHash && before.expectedClAfterHash == after.clHash;
        }

        /// <summary>失败或取消只接受原值及官方可能已经发生的两个独立环境转换；不把此证据称为正常成功。</summary>
        /// <param name="before">进入本轮 Generate 前冻结的真实前态。</param>
        /// <param name="after">失败或取消后实际读取的状态。</param>
        /// <returns>稳定设置已恢复、检查开关为 false，且环境分别属于原值或唯一官方后值时为 true。</returns>
        internal static bool FailureEqual(Snapshot before, Snapshot after)
        {
            return StableEqual(before, after) && !before.methodBridgeCheckDisabled && !after.methodBridgeCheckDisabled &&
                (after.unityIl2cppHash == before.unityIl2cppHash || after.unityIl2cppHash == before.expectedUnityIl2cppAfterHash) &&
                (after.clHash == before.clHash || after.clHash == before.expectedClAfterHash);
        }

        /// <summary>比较必须保持的输入与已恢复的三个临时 Editor 设置；环境变量另按阶段核对。</summary>
        /// <param name="expected">保存的稳定输入与临时设置基线。</param>
        /// <param name="actual">当前读取的稳定输入与临时设置。</param>
        /// <returns>所有必须保持或恢复的字段一致时返回 true；不比较阶段相关的当前环境摘要。</returns>
        private static bool StableEqual(Snapshot expected, Snapshot actual)
        {
            return expected != null && actual != null && expected.target == actual.target && expected.targetGroup == actual.targetGroup &&
                expected.development == actual.development && expected.allowDebugging == actual.allowDebugging &&
                expected.connectProfiler == actual.connectProfiler && expected.deepProfiling == actual.deepProfiling &&
                expected.buildScriptsOnly == actual.buildScriptsOnly && expected.exportAndroidProject == actual.exportAndroidProject &&
                expected.installInBuildFolder == actual.installInBuildFolder && expected.buildAppBundle == actual.buildAppBundle &&
                expected.buildLocation == actual.buildLocation && expected.useGlobalIl2cpp == actual.useGlobalIl2cpp &&
                expected.localIl2cppDirectory == actual.localIl2cppDirectory && expected.explicitBuildOptions == actual.explicitBuildOptions &&
                expected.explicitSubtarget == actual.explicitSubtarget && expected.assemblySha256 == actual.assemblySha256 &&
                expected.expectedUnityIl2cppAfterHash == actual.expectedUnityIl2cppAfterHash && expected.expectedClAfterHash == actual.expectedClAfterHash &&
                expected.sourceFingerprints != null && actual.sourceFingerprints != null &&
                expected.sourceFingerprints.SequenceEqual(actual.sourceFingerprints, StringComparer.Ordinal);
        }

        /// <summary>将空和未设置环境值按官方读取语义规范化；Receipt 只保存摘要，不公开原始编译参数。</summary>
        /// <param name="value">待绑定的进程环境字符串；null 按空字符串处理。</param>
        /// <returns>规范化 UTF-8 字节的 SHA-256 小写十六进制摘要。</returns>
        internal static string HashEnvironment(string value) => HashBytes(Encoding.UTF8.GetBytes(value ?? string.Empty));

        /// <summary>计算确定性 SHA-256，用于编译身份、包源码与环境前后态绑定。</summary>
        /// <param name="value">需要绑定的原始字节集合。</param>
        /// <returns>字节集合的 SHA-256 小写十六进制摘要。</returns>
        private static string HashBytes(byte[] value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>冻结 WebGL 全链入口、默认 DLL/AOT 解析器和准确模板，防止只凭旧 CPP 存在声称消费本次编译。</summary>
        /// <returns>准确相对 Core 路径和已审 SHA；任何漂移拒绝，不修改 vendor。</returns>
        private static string[] RequireReviewedWebGlGenerateSources()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(SettingsUtil).Assembly);
            if (package?.name != "com.solotopia.hybridclr") throw new InvalidOperationException("WebGL Generate 包身份不匹配。");
            string[][] rows =
            {
                new[] { "Editor/Commands/CompileDllCommand.cs", "338d8c53b1553c8d3374788ec098e94ef4f4964dcde929b60e937013598a5baf" },
                new[] { "Editor/Commands/MethodBridgeGeneratorCommand.cs", "d75d6209d1a1f6506368ed1553f1cf9a91ccc98fefca544b5c71df17770c8edf" },
                new[] { "Editor/Commands/AOTReferenceGeneratorCommand.cs", "5f5b4c02fbbd7948de71ff464a75bbe3e1b0dc5004586c8c2f7a7c055a77fa07" },
                new[] { "Editor/Commands/Il2CppDefGeneratorCommand.cs", "7f1b9a182e3d383136db880cb277a66eabd654e84df9af5dbb2ae01bb3d67e8b" },
                new[] { "Editor/Commands/LinkGeneratorCommand.cs", "598d1d536c5372379175c4bc012a9963ebdd223a56fea58ea90f5374721419a1" },
                new[] { "Editor/Meta/MetaUtil.cs", "0eccfd28d8b519418fc3e4ab40bfc390900242e1df6c217cb16214b3250389a0" },
                new[] { "Editor/Meta/AssemblyCacheBase.cs", "b37b01f194326332d3141bf93dbb27d1aa114834185026209ec04aa3f9a916a4" },
                new[] { "Editor/Meta/FixedSetAssemblyResolver.cs", "0bf03f1f8ebf8bd5ebf2227c4382125bc200a84df126b2f8d2939774088dd42b" },
                new[] { "Editor/Meta/PathAssemblyResolver.cs", "8a7f5ac369e3ec966a536a85dfa031c71080de6ddc12dc851d147d804e5451a1" },
                new[] { "Editor/Meta/CombinedAssemblyResolver.cs", "6a886ad5487b7274d27da871d10a459b63b07de4ec8793bb56e6d84e7ce81f1c" },
                new[] { "Editor/Meta/AssemblyReferenceDeepCollector.cs", "9183f81f37140b06729476afe4aaad047592bfc75b04328dfc174b65178dca12" },
                new[] { "Editor/AOT/GenericReferenceWriter.cs", "98867daf54b60900ee3f41e5bd54d5be527a772545464b2e1c82146816728bf8" },
                new[] { "Data~/Templates/MethodBridge.cpp.tpl", "8b53ed7960c0e438003fe9c744c65bb588970d98a9a5ae56bb55b2069dcb97fe" },
                new[] { "Data~/Templates/UnityVersion.h.tpl", "030219b228a7dfa0b7a056fa91de9b89d8422742982f727449663b71e0fa1e55" },
                new[] { "Data~/Templates/AssemblyManifest.cpp.tpl", "4a53713a9fa20c7810872599fc1f90c0a927e79ecbc1a1b69ab1a90cf8192b28" },
            };
            return rows.Select(row =>
            {
                string file = Path.Combine(package.resolvedPath, "Core", row[0]);
                if (!File.Exists(file) || HashBytes(File.ReadAllBytes(file)) != row[1]) throw new InvalidOperationException("WebGL Generate 已审源码或模板漂移：" + row[0]);
                return row[0] + "|" + row[1];
            }).ToArray();
        }

        /// <summary>要求本轮已精读的包源码保持指纹；不修改 vendor，也不以包名前缀替代行为审查。</summary>
        /// <returns>全部已审相对源码路径与 SHA 指纹；包身份或字节变化时抛出异常。</returns>
        private static string[] RequireReviewedSources()
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(SettingsUtil).Assembly);
            if (package?.name != "com.solotopia.hybridclr") throw new InvalidOperationException("HybridCLR 包身份不匹配。");
            string[][] files =
            {
                new[] { "Commands/PrebuildCommand.cs", "c6f8554aae2f3c0eaf0ecc66d44a114fe493617de338acef1f3e1df25f7368c2" },
                new[] { "Commands/StripAOTDllCommand.cs", "7658322d64410a788cec655385717fc349d477dfbbc514805e3fbab28e68b68c" },
                new[] { "BuildProcessors/CheckSettings.cs", "8ee35ca507f002bedf8825ba726ea8cacd33d78aaf37a22ece6fbfccb054bf00" },
                new[] { "BuildProcessors/MsvcStdextWorkaround.cs", "9ea10f6b6001a5c06f1c858a5a0aa48bdc6b3c428c47339fe59ae5570f699f9e" },
                new[] { "BuildProcessors/CopyStrippedAOTAssemblies.cs", "d1faa5b527bf5483f07d42b9386b0d5a62ee889d3239546d89a95cd89f887ee4" },
                new[] { "BuildProcessors/PatchScriptingAssemblyList.cs", "9a79a34c94b34a84c3457fd7aca2ba5f013aa6fa2592a15747ef7fc6fff05e3d" },
                new[] { "SettingsUtil.cs", "9c0cb3d954bb750f252d4d61fecdb86069d995776dea0ab876f7c03686dcd320" },
            };
            return files.Select(entry =>
            {
                string path = Path.Combine(package.resolvedPath, "Core/Editor", entry[0]);
                if (!File.Exists(path) || HashBytes(File.ReadAllBytes(path)) != entry[1])
                    throw new InvalidOperationException("HybridCLR 已审源码发生变化：" + entry[0]);
                return entry[0] + "|" + entry[1];
            }).ToArray();
        }
    }
}
