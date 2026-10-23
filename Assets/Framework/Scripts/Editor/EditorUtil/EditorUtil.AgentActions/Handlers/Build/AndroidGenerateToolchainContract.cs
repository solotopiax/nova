/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidGenerateToolchainContract.cs
 * author:    taoye
 * created:   2026/10/20
 * descrip:   Android Generate的有限工具输入与本次实际导出选择
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>不启动工具/预热SDKManager；明确区分静态预测与同次导出的实际选择。</summary>
    internal static class AndroidGenerateToolchainContract
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly string[][] Modules = {
            new[] { "UnityEditor.Android.Extensions.dll", "87f1eb3d4287f5f2522b7f19fde7e9d8678a070c9b0e94ebfc669ad54d98a606", "36c6f5b2-7427-4222-9afb-b75d38162d17" },
            new[] { "Unity.Android.Gradle.dll", "a0416d6869e54cd299fae3e99606e48083b9fc273cae22e736b26caa4aba895e", "866ab6d1-2ad8-4b08-8913-e0d852b1afdb" },
            new[] { "Unity.Android.Types.dll", "40acf7294132c450897b2b50a0329a23f732b84b41febf5f5bcd4744f5666e23", "0c8096a8-6f0d-4116-b896-3e5f0ad4fdd1" },
            new[] { "AndroidPlayerBuildProgram.Data.dll", "f633ad25fff7465b252e4237cd2e6fc4877272dbfc39ef8bbb4c94f9fa340f5a", "91a84b58-bfe9-455c-82fc-3bb294ae231d" },
            new[] { "AndroidPlayerBuildProgram.exe", "e139e39a79215d985dec85d0d4fc21e325cd079fa52fe7c57abcfa525fa6e6c2", "fd59755a-320e-43b6-ad0e-b3f2c7aa2c91" },
        };
        private static readonly string[][] Tools = {
            new[] { "SDK/cmdline-tools/16.0/source.properties", "03f5b26666088084eb66618b5a4f39f937cc9f75a8f4c22acbd859487e50ad04" },
            new[] { "SDK/cmdline-tools/16.0/bin/sdkmanager", "02d935912121139ccfeb1f19357f0ac914f1892ab7a4e852aeefd0a4cab50bb2" },
            new[] { "SDK/platform-tools/source.properties", "382f8b27c4af692501671a83e725217629c45883cfedeb29a1a3fd8e5b191677" },
            new[] { "SDK/platform-tools/package.xml", "4d622f5247b6f9c2f9fb3e75e8d5e15c0fd876b2c312ebab03303eea4b417333" },
            new[] { "SDK/platform-tools/adb", "0e8bb380b1993eac9d2433a05d92a33035971cd25542b1c36b3537ddd6defe94" },
            new[] { "SDK/build-tools/35.0.0/source.properties", "084847d70abc41284feee7ea717e7c92eab0d1be05f048c27445a359cfe109d8" },
            new[] { "SDK/build-tools/35.0.0/package.xml", "a391fe4f9034a033cd8873323dd4f348838fb305b3268760f4eb0af5bbe7ef45" },
            new[] { "SDK/build-tools/36.0.0/source.properties", "7dee6632e9ad6cb111da2bb99d747211e27927061b1276d040bb1d71fded5ebb" },
            new[] { "SDK/build-tools/36.0.0/package.xml", "bb75816b1a74a840a9c226db0845796f1d2739510152a38aa8b7122e285192de" },
            new[] { "SDK/build-tools/36.0.0/aapt2", "a8844d4089b442b034aed8953deee1893253053c900e03141ae7173e3edd8157" },
            new[] { "SDK/build-tools/36.0.0/zipalign", "0427144f4a3fd242c5a159e7088637082539ae556bc1d2bbc2032bb775d47cea" },
            new[] { "SDK/build-tools/36.0.0/lib/apksigner.jar", "3716d9311e55d2b0918a2fd9d54ba9e406c5f6abeea700b287f11259bc163dec" },
            new[] { "SDK/build-tools/36.0.0/lib/d8.jar", "4097ff9c46c185c6e7214da7fe9b1befb5adeea5cc9ca349270e0249904f9240" },
            new[] { "SDK/platforms/android-36/source.properties", "72291ff6611dbfc46fdfb6de2821c4a62c87befa5200713847eda426e3a501c2" },
            new[] { "SDK/platforms/android-36/package.xml", "2110f8ec9c213a77e287e4e92d89e28dd770e4377c24350758cbddebb75de9f3" },
            new[] { "SDK/platforms/android-36/android.jar", "d9eb9da824d9e247a352f570f01e1169e725b2954bca9e283a71786c59b59f9a" },
            new[] { "NDK/source.properties", "70f1c5165e6d997b18782a49cff71da7a3caacd0c4cd811da24c43359fb83cf6" },
            new[] { "NDK/meta/abis.json", "bc1af64d6acce410a34dcc19890a2356f5e8e346f3e76d386f11168912cd458c" },
            new[] { "NDK/meta/platforms.json", "05a32492d861153113a293bc9dec028aaba8e64d2f2cbe41f359d50371ac018a" },
            new[] { "NDK/build/cmake/android.toolchain.cmake", "22c1f80acd4a272be56b108b97320197046f407412757d3651c098327ea59a60" },
            new[] { "OpenJDK/release", "aad028617b79737613fb39c761d63a2b71fedcf85506d6c0d47bb48ed9e6da5e" },
            new[] { "OpenJDK/bin/java", "bab5eb3ed4324a2eb474865194d5dc8f64161a12d4c06406150cd7ce8bed56f1" },
            new[] { "OpenJDK/bin/javac", "e0e4c598bfdc81fc6a0ac448dd13b8090c27acd8e3de998ff36450099a6054a2" },
            new[] { "OpenJDK/bin/jar", "4de0068aa75b7e5f62f3328e2007f0475fead4c7aac6160392c7ed1f860325db" },
            new[] { "Tools/gradle/lib/gradle-launcher-8.13.jar", "56a30262647231bf3ab30b2bf090f5ecacff9329d3f1af1a1de73fd1ab4c64b4" },
            new[] { "Tools/gradle/lib/gradle-core-8.13.jar", "0a69dc9e20abce45bbe11135351dca180dd6b98b77274fd2b347655d0cd6fa41" },
            new[] { "OpenJDK/lib/libjli.dylib", "34cfda42999cae16aa643c3c09deb010da7bdb5b4c8c0f3394f201070f72a667" },
            new[] { "SDK/cmdline-tools/16.0/lib/sdkmanager-classpath.jar", "f08cacfd5217963d7b854bab6fd345de90f466959fbaa2d3810888029226a1c9" },
            new[] { "Tools/GradleTemplates/launcher/setupSymbols.gradle", "3384a59d8c7272fb8a49f50b42c93622a77cf9beebda2b0062f49c096ffdbd3e" },
            new[] { "Tools/GradleTemplates/shared/keepUnitySymbols.gradle", "9271cd0da61897c510d20ccb2fb510841aa8395526ba89261cda5c5c221a749c" },
            new[] { "Tools/GradleTemplates/shared/common.gradle", "c031dbe2964bda9ebfdc11746de82175f65905279ebed1d585ff1dff79b6ad00" },
            new[] { "Tools/UnityProGuardTemplate.txt", "442ee4284b4d02ccefc18e258fab45e6235665679ef5bd90e72e5a7b7e22eeab" },
            new[] { "Apk/UnityManifest.xml", "382ccffeb091e59c05b2ee9f9da8d31d9461fa59ce78ac5525b5663d32892d95" },
            new[] { "Apk/LauncherManifest.xml", "20d02042d43a5fce4a9d4fdc5b83ec5d285374c04de08aa2d08b54e052dfa969" },
        };
        private static readonly string[][] Links = {
            new[] { "NDK/toolchains/llvm/prebuilt/darwin-x86_64/bin/clang", "clang-18", "NDK/toolchains/llvm/prebuilt/darwin-x86_64/bin/clang-18", "f3060ee130cf6f162a739dbd9cbd9f4fae552890f59bf82e8c65176eb7887e22" },
            new[] { "NDK/toolchains/llvm/prebuilt/darwin-x86_64/bin/ld.lld", "lld", "NDK/toolchains/llvm/prebuilt/darwin-x86_64/bin/lld", "dd650face3f1d4e25dcbcbd7f7b0a969d4f10dc1471db8778b699fee9739b1c1" },
        };
        private static readonly string[][] Absent = {
            new[] { "SDK/cmdline-tools/16.0/package.xml" },
        };
        private static readonly string[] TemplateNames = { "mainTemplate.gradle", "launcherTemplate.gradle", "baseProjectTemplate.gradle", "gradleTemplate.properties", "settingsTemplate.gradle", "libTemplate.gradle", "AndroidManifest.xml", "LauncherManifest.xml", "proguard-user.txt" };
        private static readonly string[][] GetterIl = {
            new[] { "sdkRootPath", "KHcBAAZvYgEABio=" }, new[] { "ndkRootPath", "KOkAAAZvYgEABio=" }, new[] { "jdkRootPath", "KKsAAAZvYgEABio=" },
            new[] { "gradlePath", "KMEFAAYq" }, new[] { "gradleUserHomePath", "KMMFAAYq" },
        };
        /// <summary>真实普通文件身份，不保存正文。</summary>
        [Serializable] internal sealed class FileRow { public string path; public string sha256; public long length; }
        /// <summary>两条准确NDK链接的词法身份与实际目标。</summary>
        [Serializable] internal sealed class LinkRow { public string path; public string linkText; public string resolvedPath; public string targetSha256; }
        /// <summary>实际模板File.Exists选择，saved useCustom标志不代替来源。</summary>
        [Serializable] internal sealed class TemplateRow { public string name; public string path; public bool exists; public string origin; public string sha256; }
        /// <summary>本次工具输入与明确标注的预测；不声称捕获不存在的PostProcessorContext。</summary>
        [Serializable] internal sealed class Snapshot
        {
            public string root, sdk, ndk, jdk, gradle, legacyGradleUserHome, extensionsMvid;
            public string[] modulePins, inventory;
            public FileRow[] files;
            public LinkRow[] links;
            public TemplateRow[] templates;
            public int minSdk, targetSdk, predictedCompileSdk;
            public string predictedBuildTools, predictionBasis;
        }
        /// <summary>本次自然Gradle回调或原调用返回后读取的实际导出，不补执行前Context历史。</summary>
        [Serializable] internal sealed class ExportSelection
        {
            public string outputRoot, sdk, buildTools;
            public int targetSdk, compileSdk;
            public FileRow[] files;
        }

        /// <summary>核已审模块/根getter的准确IL，冻结有限工具、链接、模板选择和显式API预测；不启动SDK工具。</summary>
        /// <param name="token">当前取消。</param><returns>本次真实输入与静态预测。</returns>
        internal static Snapshot Capture(CancellationToken token)
        {
            if (Application.platform != RuntimePlatform.OSXEditor || Application.unityVersion != "6000.4.2f1" || EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Android Generate当前仅审Unity6000.4.2f1 macOS工具链。");
            string root = Path.GetFullPath(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None));
            var modulePins = new List<string>();
            foreach (string[] spec in Modules)
            {
                FileRow row = CaptureFile(Path.Combine(root, spec[0]), token);
                if (row.sha256 != spec[1]) throw new InvalidOperationException("Android已审工具模块漂移：" + spec[0]);
                modulePins.Add(spec[0] + "|" + spec[1] + "|reviewed-mvid=" + spec[2]);
            }
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(value => value.GetName().Name == "UnityEditor.Android.Extensions");
            if (assembly == null || assembly.ManifestModule.ModuleVersionId.ToString("D") != Modules[0][2] || Path.GetFullPath(assembly.Location) != Path.Combine(root, Modules[0][0]))
                throw new InvalidOperationException("Android root getter实际加载模块不符合已审磁盘/MVID。");
            Type settings = assembly.GetType("UnityEditor.Android.AndroidExternalToolsSettings", true);
            MethodInfo[] getters = GetterIl.Select(spec =>
            {
                MethodInfo method = settings.GetProperty(spec[0], BindingFlags.Static | BindingFlags.Public)?.GetGetMethod();
                if (method == null || method.ReturnType != typeof(string) || method.GetParameters().Length != 0 || method.IsGenericMethod || Convert.ToBase64String(method.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>()) != spec[1])
                    throw new InvalidOperationException("Android root getter准确签名/IL改变：" + spec[0]);
                return method;
            }).ToArray();
            string prefs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Preferences/com.unity3d.UnityEditor5.x.plist");
            string prefsBefore = OptionalHash(prefs, token);
            string[] roots = getters.Select(getter => (string)getter.Invoke(null, null)).ToArray();
            if (OptionalHash(prefs, token) != prefsBefore) throw new InvalidOperationException("Android root getter本次读取写入Editor prefs，保持blocked。");
            string[] expected = { Path.Combine(root, "SDK"), Path.Combine(root, "NDK"), Path.Combine(root, "OpenJDK"), Path.Combine(root, "Tools/gradle"), "" };
            if (!roots.SequenceEqual(expected, StringComparer.Ordinal)) throw new InvalidOperationException("Android自定义工具根或legacy Gradle userHome尚无合同。");
            FileRow[] files = Tools.Select(spec =>
            {
                FileRow row = CaptureFile(Path.Combine(root, spec[0]), token);
                if (row.sha256 != spec[1]) throw new InvalidOperationException("Android已审工具正文漂移：" + spec[0]);
                return row;
            }).ToArray();
            foreach (string[] spec in Absent) if (File.Exists(Path.Combine(root, spec[0])) || Directory.Exists(Path.Combine(root, spec[0]))) throw new InvalidOperationException("Android已审工具缺失角色发生变化。");
            LinkRow[] links = Links.Select(spec => CaptureLink(root, spec, token)).ToArray();
            string[] inventory = CaptureInventory(root);
            string[] expectedInventory = { "SDK/build-tools|35.0.0|directory", "SDK/build-tools|36.0.0|directory", "SDK/cmdline-tools|16.0|directory", "SDK/platforms|android-34|directory", "SDK/platforms|android-35|directory", "SDK/platforms|android-36|directory", "Tools/gradle/lib|gradle-launcher-8.13.jar|file" };
            if (!inventory.SequenceEqual(expectedInventory.OrderBy(value => value, StringComparer.Ordinal))) throw new InvalidOperationException("Android SDK/Gradle有限候选集合改变。");
            int target = (int)PlayerSettings.Android.targetSdkVersion, min = (int)PlayerSettings.Android.minSdkVersion;
            if (target != 36 || min <= 0 || min > target) throw new InvalidOperationException("Android仅审显式target36及platform36；Auto/其它API不继承本次预测。");
            TemplateRow[] templates = TemplateNames.Select(name =>
            {
                string path = Path.Combine(BuildActionCommon.ProjectRoot, "Assets/Plugins/Android", name); RejectLinks(path); bool exists = File.Exists(path);
                return new TemplateRow { name = name, path = path, exists = exists, origin = exists ? "project-file" : "unity-generated-model-or-builtin", sha256 = exists ? GenerateActionCommon.ComputeFileHash(path, token) : null };
            }).ToArray();
            return new Snapshot { root = root, sdk = roots[0], ndk = roots[1], jdk = roots[2], gradle = roots[3], legacyGradleUserHome = roots[4], extensionsMvid = assembly.ManifestModule.ModuleVersionId.ToString("D"), modulePins = modulePins.ToArray(), files = files, links = links, inventory = inventory, templates = templates, minSdk = min, targetSdk = target,
                predictedCompileSdk = Math.Max((PlayerSettings.Android.applicationEntry & AndroidApplicationEntry.GameActivity) != 0 ? 32 : 30, target), predictedBuildTools = "36.0.0", predictionBasis = "reviewed-local-package-metadata-and-explicit-target;not-observed-context" };
        }

        /// <summary>向原Player输入追加稳定语义；模板正文仍由原文件快照和准确拥有域处理。</summary>
        /// <param name="snapshot">真实本次工具输入。</param><returns>不含全局prefs或配置正文的准确身份。</returns>
        internal static string StableIdentity(Snapshot snapshot) => JsonConvert.SerializeObject(new { snapshot.root, snapshot.sdk, snapshot.ndk, snapshot.jdk, snapshot.gradle, snapshot.legacyGradleUserHome, snapshot.extensionsMvid, snapshot.modulePins, snapshot.files, snapshot.links, snapshot.inventory, snapshot.minSdk, snapshot.targetSdk, snapshot.predictedCompileSdk, snapshot.predictedBuildTools, snapshot.predictionBasis,
            templates = snapshot.templates.Select(row => new { row.name, row.path, row.exists, row.origin }).ToArray() });

        /// <summary>把普通文件及两条准确链接的真实目标交回既有输入快照；不递归扫描工具根。</summary>
        /// <param name="snapshot">已准入的工具输入。</param><returns>有限真实文件及缺失模板角色。</returns>
        internal static string[] InputFiles(Snapshot snapshot) => snapshot.files.Select(row => row.path).Concat(snapshot.links.Select(row => row.resolvedPath)).Concat(snapshot.templates.Select(row => row.path)).Concat(Modules.Select(row => Path.Combine(snapshot.root, row[0]))).Concat(Absent.Select(row => Path.Combine(snapshot.root, row[0]))).Distinct(StringComparer.Ordinal).ToArray();

        /// <summary>读取同次实际输出的四个文件核版本和SDK；调用方另绑定原Gradle回调与新BuildReport。</summary>
        /// <param name="before">原工具计划。</param><param name="outputRoot">本次准确官方输出根。</param><param name="token">当前取消。</param><returns>真正已存在的实际导出选择。</returns>
        internal static ExportSelection CaptureExport(Snapshot before, string outputRoot, CancellationToken token)
        {
            if (before == null || string.IsNullOrWhiteSpace(outputRoot)) throw new InvalidOperationException("Android导出选择缺原工具计划/准确根。");
            string root = Path.GetFullPath(outputRoot); string[] paths = { Path.Combine(root, "gradle.properties"), Path.Combine(root, "local.properties"), Path.Combine(root, "launcher/build.gradle"), Path.Combine(root, "unityLibrary/build.gradle") };
            byte[][] bytes = paths.Select(ReadBoundedBytes).ToArray();
            FileRow[] files = paths.Select((path, index) => new FileRow { path = path, length = bytes[index].Length, sha256 = Hash(bytes[index]) }).ToArray();
            string properties = Utf8.GetString(bytes[0]), local = Utf8.GetString(bytes[1]);
            string buildTools = Property(properties, "unity.buildToolsVersion"), sdk = Property(local, "sdk.dir");
            if (!int.TryParse(Property(properties, "unity.targetSdkVersion"), NumberStyles.None, CultureInfo.InvariantCulture, out int target) || !int.TryParse(Property(properties, "unity.compileSdkVersion"), NumberStyles.None, CultureInfo.InvariantCulture, out int compile) || buildTools != before.predictedBuildTools || target != before.targetSdk || compile != before.predictedCompileSdk || Path.GetFullPath(sdk) != before.sdk)
                throw new InvalidOperationException("Android同次Gradle实际SDK/BuildTools/target/compile不符合冻结预测。");
            for (int index = 2; index < paths.Length; index++)
            {
                string body = Utf8.GetString(bytes[index]);
                if (GradleValue(body, "compileSdk", false) != compile.ToString(CultureInfo.InvariantCulture) || GradleValue(body, "targetSdk", false) != target.ToString(CultureInfo.InvariantCulture) || GradleValue(body, "buildToolsVersion", true) != buildTools)
                    throw new InvalidOperationException("Android实际launcher/unityLibrary版本行尚未闭合。");
            }
            for (int index = 0; index < paths.Length; index++)
                if (CaptureFile(paths[index], token).sha256 != files[index].sha256) throw new InvalidOperationException("Android实际导出在同字节解析窗口中变化。");
            return new ExportSelection { outputRoot = root, sdk = before.sdk, buildTools = buildTools, targetSdk = target, compileSdk = compile, files = files };
        }

        /// <summary>只取唯一目标properties键，支持Java转义；不返回其他键值。</summary>
        private static string Property(string body, string key)
        {
            string prefix = "^\\s*" + Regex.Escape(key) + "\\s*[=:]\\s*";
            string[] values = body.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => Regex.IsMatch(line, prefix, RegexOptions.CultureInvariant)).ToArray();
            if (values.Length != 1) throw new InvalidOperationException("Android导出properties目标键缺失/重复：" + key);
            string value = Regex.Replace(values[0], prefix, "", RegexOptions.CultureInvariant); var result = new StringBuilder();
            for (int index = 0; index < value.Length; index++)
            {
                char item = value[index]; if (item != '\\') { result.Append(item); continue; }
                if (++index == value.Length) throw new InvalidOperationException("Android导出properties未审续行。"); item = value[index];
                if (item == 'u') { if (index + 4 >= value.Length || !ushort.TryParse(value.Substring(index + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code)) throw new InvalidOperationException("Android properties Unicode转义无效。"); result.Append((char)code); index += 4; }
                else result.Append(item == 't' ? '\t' : item == 'r' ? '\r' : item == 'n' ? '\n' : item == 'f' ? '\f' : item);
            }
            return result.ToString();
        }

        /// <summary>仅接受已审模板展开后的唯一字面版本行；变量、重复项和额外语法保持阻断。</summary>
        private static string GradleValue(string body, string name, bool quoted)
        {
            string pattern = "(?m)^\\s*" + Regex.Escape(name) + "\\s*=?\\s*" + (quoted ? "(?<quote>[\"'])(?<value>[0-9]+(?:\\.[0-9]+){1,3})\\k<quote>" : "(?<value>[0-9]+)") + "\\s*\\r?$";
            MatchCollection values = Regex.Matches(body, pattern, RegexOptions.CultureInvariant);
            if (values.Count != 1) throw new InvalidOperationException("Android Gradle目标版本不是唯一字面值：" + name);
            return values[0].Groups["value"].Value;
        }

        /// <summary>有界只读实际导出文本，不保存正文。</summary>
        private static byte[] ReadBoundedBytes(string path)
        {
            RejectLinks(path); var file = new FileInfo(path);
            if (!file.Exists || file.Length > 4 * 1024 * 1024) throw new InvalidOperationException("Android实际导出角色缺失或文本超过4MiB。");
            byte[] bytes = File.ReadAllBytes(path); if (bytes.Length > 4 * 1024 * 1024) throw new InvalidOperationException("Android导出读取窗口超过4MiB。"); return bytes;
        }
        /// <summary>实际字段解析与收据SHA使用同一份原字节，不把二次读取当原文见证。</summary>
        private static string Hash(byte[] bytes) { using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        /// <summary>只列SDK与Gradle四个有限角色的一层候选，不扫描全树。</summary>
        private static string[] CaptureInventory(string root) => new[] { "SDK/build-tools", "SDK/platforms", "SDK/cmdline-tools", "Tools/gradle/lib" }.SelectMany(role =>
        {
            string path = Path.Combine(root, role); RejectLinks(path);
            IEnumerable<string> values = role == "Tools/gradle/lib" ? Directory.GetFiles(path, "gradle-launcher-*.jar", SearchOption.TopDirectoryOnly) : Directory.GetFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly);
            return values.Select(value => { RejectLinks(value); return role + "|" + Path.GetFileName(value) + "|" + (Directory.Exists(value) ? "directory" : "file"); });
        }).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        /// <summary>核实际普通文件及祖先，不跟随新链接。</summary>
        private static FileRow CaptureFile(string path, CancellationToken token) { path = Path.GetFullPath(path); RejectLinks(path); if (!File.Exists(path)) throw new FileNotFoundException("Android已审工具/导出角色缺失。", path); return new FileRow { path = path, length = new FileInfo(path).Length, sha256 = GenerateActionCommon.ComputeFileHash(path, token) }; }
        /// <summary>只保护root getter这次读取窗口，不将整份全局prefs纳入Generate相等门。</summary>
        private static string OptionalHash(string path, CancellationToken token) { RejectLinks(path); return File.Exists(path) ? GenerateActionCommon.ComputeFileHash(path, token) : "absent"; }
        /// <summary>拒绝普通路径与祖先链接；两条准确NDK角色另有有限合同。</summary>
        private static void RejectLinks(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Android普通工具输入包含未审链接。");
        }
        /// <summary>核NDK原linktext、真正目标与已审目标正文；不允许跨根或任意其它链接。</summary>
        private static LinkRow CaptureLink(string root, string[] spec, CancellationToken token)
        {
            string path = Path.Combine(root, spec[0]); RejectLinks(Path.GetDirectoryName(path));
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0) throw new InvalidOperationException("Android已审NDK链接被替换。");
            byte[] buffer = new byte[4096]; long length = ReadLink(path, buffer, new UIntPtr((uint)buffer.Length)).ToInt64();
            if (length < 1 || length >= buffer.Length) throw new InvalidOperationException("Android NDK linktext不可完整读取。");
            string text = Utf8.GetString(buffer, 0, (int)length); IntPtr pointer = RealPath(path, IntPtr.Zero);
            if (pointer == IntPtr.Zero) throw new InvalidOperationException("Android NDK链接目标不可解析。");
            string resolved; try { resolved = Marshal.PtrToStringAnsi(pointer); } finally { Free(pointer); }
            if (text != spec[1] || resolved != Path.Combine(root, spec[2]) || !resolved.StartsWith(Path.Combine(root, "NDK") + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("Android NDK链接越界或目标改变。");
            FileRow target = CaptureFile(resolved, token); if (target.sha256 != spec[3]) throw new InvalidOperationException("Android NDK链接目标正文改变。");
            return new LinkRow { path = path, linkText = text, resolvedPath = resolved, targetSha256 = target.sha256 };
        }
        /// <summary>只读取两条已审NDK链接的原始文本。</summary>
        [DllImport("libc", EntryPoint = "readlink", SetLastError = true)] private static extern IntPtr ReadLink(string path, byte[] buffer, UIntPtr length);
        /// <summary>只解析两条已审NDK链接的真实目标。</summary>
        [DllImport("libc", EntryPoint = "realpath", SetLastError = true)] private static extern IntPtr RealPath(string path, IntPtr buffer);
        /// <summary>释放realpath自己分配的指针。</summary>
        [DllImport("libc", EntryPoint = "free")] private static extern void Free(IntPtr pointer);
    }
}
