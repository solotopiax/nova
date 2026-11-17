/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AndroidResolver.GradleEnvironment.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   已审 EDM 同步下载使用的 Gradle 分发配置与恢复作用域
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 官方 EDM 1.2.188/1.2.190 没有下载执行器钩子，内嵌分发仍是 Gradle 5.1.1。
    /// 仅同步下载支路临时配置受支持的官方分发；不修改厂商包、版本探测缓存或用户环境。
    /// </summary>
    [InitializeOnLoad]
    internal sealed class AndroidResolverGradleEnvironment : IDisposable
    {
        internal const string DistributionVersion = "8.13";
        // 来源为 services.gradle.org/distributions/gradle-8.13-bin.zip.sha256，2026-11-16 核实。
        internal const string DistributionSha256 = "20f1b1176237254a6fc204d8434196fa11a4cfb387567519c61556e8710aed78";
        private const string ResourcePrefix = "PlayServicesResolver.scripts.";
        private const string ArchiveName = ResourcePrefix + "gradle-template.zip";
        private const string PropertiesName = "gradle/wrapper/gradle-wrapper.properties";
        private const string OriginalDistribution = "distributionUrl=https\\://services.gradle.org/distributions/gradle-5.1.1-bin.zip";
        private const string CompatiblePropertiesHash = "b61b0f17aa36db521c4a6673839930f0a0eec7c9efc27514fbf1f9849f2a9edf";
        private static readonly object Gate = new object();
        private static readonly int MainThread = Thread.CurrentThread.ManagedThreadId;
        private static AndroidResolverGradleEnvironment s_Active;
        private static readonly Dictionary<string, string> OriginalHashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { ArchiveName, "20ac86b55ce8f2bd97e74fec55178864f00251cc1ca3d862c515e103113cdadc" },
            { "gradle/wrapper/gradle-wrapper.jar", "8e47da0b2656354d059609cae9d44b196d4f9b14512e688ffee4e0eb7e723ae9" },
            { PropertiesName, "7b4d93682b495f3ad359ae37f27730ff4d41fc545b0429de4b6f46d5f59f8379" },
            { "gradlew", "a6a4e251982a425740c10af6b1bcba5589ea6c1c5518baf3bdedeaa59abcab51" },
            { "gradlew.bat", "398db6d288bcbfc7b2059f15e2bb6f301cb39e8c421bd1e5c5682ab9bb8ed104" }
        };
        private Recovery m_Recovery;
        private string m_RecoveryDirectory;
        private FileStream m_Lock;
        private Func<string> m_ReadToolchain;
        private string m_Toolchain;
        private string m_JournalHash;
        private bool m_Disposed;

        /// <summary>只读暴露尚未恢复的同步事务；受控调用方不能把待恢复状态当作就绪。</summary>
        internal static bool IsActive
        {
            get
            {
                lock (Gate)
                {
                    string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                    return s_Active != null || Directory.Exists(RecoveryDirectory(project));
                }
            }
        }

        [Serializable]
        private sealed class FileState
        {
            public string relative;
            public bool existed;
            public string original;
            public string originalHash;
            public long originalTime;
            public int originalAttributes;
            public uint originalMode;
            public string expectedHash;
            public long expectedTime;
            public int expectedAttributes;
            public uint expectedMode;
        }

        [Serializable]
        private sealed class Recovery
        {
            public int version = 1;
            public string buildDirectory;
            public string[] createdDirectories;
            public FileState[] files;
        }

        /// <summary>恢复上次未结束的项目内事务，并注册重载/退出恢复；不发起解析或下载。</summary>
        static AndroidResolverGradleEnvironment()
        {
            RestorePending();
            AssemblyReloadEvents.beforeAssemblyReload += RestoreActive;
            EditorApplication.quitting += RestoreActive;
        }

        /// <summary>精确复用已审 ResolveUnsafe 的真实分支条件；模板支路不创建临时目录或恢复材料。</summary>
        internal static bool RequiresDownload(Type resolver)
        {
            bool template = ReadBoolean(resolver, "GradleTemplateEnabled", BindingFlags.Public);
            return !template || !ReadBoolean(resolver.Assembly.GetType("GooglePlayServices.SettingsDialog", true),
                "PatchMainTemplateGradle", BindingFlags.NonPublic);
        }

        /// <summary>只在无并发解析的主线程同步支路准备已审生成配置，并冻结实际选定工具链。</summary>
        internal static AndroidResolverGradleEnvironment Begin(Type resolver, string javaHome)
        {
            if (!RequiresDownload(resolver)) return null;
            if (Thread.CurrentThread.ManagedThreadId != MainThread)
                throw Error("Gradle 下载作用域必须由 Unity 主线程建立。");
            if (!AndroidEdmCompatibilityContract.TryVerifyType(resolver, "Google.JarResolver.dll", CancellationToken.None, out string error))
                throw Error(error);
            RequireResolverIdle(resolver);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (Path.GetFullPath(Directory.GetCurrentDirectory()) != project)
                throw Error("当前工作目录与 Unity 工程不一致，不能准备下载配置。");
            string build = Path.Combine(project, "Temp", "PlayServicesResolverGradle");
            object wrapper = resolver.GetProperty("Gradle", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            string actual = wrapper?.GetType().GetProperty("BuildDirectory")?.GetValue(wrapper) as string;
            if (actual == null || Path.GetFullPath(actual) != build) throw Error("EDM 实际下载目录与已审路径不一致。");
            Func<string> toolchain = () => CaptureToolchain(resolver, javaHome);
            string identity = toolchain();
            Dictionary<string, byte[]> originals = ReadOriginalFiles(resolver.Assembly);
            var scope = PrepareFiles(build, RecoveryDirectory(project), originals,
                File.GetLastWriteTimeUtc(resolver.Assembly.Location), true);
            scope.m_ReadToolchain = toolchain;
            scope.m_Toolchain = identity;
            return scope;
        }

        /// <summary>读取精确发行内嵌资源并校验完整字节；不会调用厂商提取器或替换 DLL。</summary>
        private static Dictionary<string, byte[]> ReadOriginalFiles(Assembly assembly)
        {
            byte[] archive = ReadResource(assembly, ArchiveName);
            if (Hash(archive) != OriginalHashes[ArchiveName]) throw Error("EDM 内嵌 Gradle wrapper 归档身份变化。");
            if (Hash(ReadResource(assembly, ResourcePrefix + "download_artifacts.gradle")) !=
                "ae9ac030c3295d03cbf0b3e07aa7f4e8f3127f4c5247eec48ceb063d52cd881a" ||
                Hash(ReadResource(assembly, ResourcePrefix + "settings.gradle")) !=
                "eb71dc27a5950dfc1e8364adc6f33aa4af6524efbfcd838d4df5312963d323c4")
                throw Error("EDM 原始下载脚本或 settings 资源身份变化。");
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal) { { ArchiveName, archive } };
            using (var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read))
                foreach (string name in OriginalHashes.Keys.Where(name => name != ArchiveName))
                {
                    ZipArchiveEntry entry = zip.GetEntry(name);
                    if (entry == null) throw Error("EDM wrapper 归档缺少：" + name);
                    using (var output = new MemoryStream())
                    { using (Stream input = entry.Open()) input.CopyTo(output); result.Add(name, output.ToArray()); }
                    if (Hash(result[name]) != OriginalHashes[name]) throw Error("EDM wrapper 文件身份变化：" + name);
                }
            return result;
        }

        /// <summary>创建五个生成文件的可恢复事务；测试可提供独立夹具，生产调用此前已校验官方资源。</summary>
        internal static AndroidResolverGradleEnvironment PrepareFiles(string buildDirectory, string recoveryDirectory,
            IDictionary<string, byte[]> originals, DateTime assemblyTime, bool requireAuditedOriginals = false)
        {
            lock (Gate)
            {
                if (s_Active != null || Directory.Exists(recoveryDirectory)) throw Error("另一项 Gradle 作用域或待恢复事务尚未收尾。");
                if (!Path.IsPathRooted(buildDirectory) || Path.GetFullPath(buildDirectory) != buildDirectory ||
                    !Path.IsPathRooted(recoveryDirectory) || Path.GetFullPath(recoveryDirectory) != recoveryDirectory)
                    throw Error("Gradle 生成与恢复目录必须是明确的规范绝对路径。");
                if (!OriginalHashes.Keys.OrderBy(value => value).SequenceEqual(originals.Keys.OrderBy(value => value)))
                    throw Error("Gradle 准备文件集合不完整。");
                if (requireAuditedOriginals && originals.Any(pair => Hash(pair.Value) != OriginalHashes[pair.Key]))
                    throw Error("Gradle 准备来源不属于已审原始资源。");
                RequireNoLinks(buildDirectory); RequireNoLinks(recoveryDirectory);
                // 使用可精确回读的整秒时间；不把文件时间改到未来来绕过上游检查。
                long preparedTime = DateTime.UtcNow.Ticks / (2 * TimeSpan.TicksPerSecond) * (2 * TimeSpan.TicksPerSecond);
                if (assemblyTime.ToUniversalTime().Ticks > preparedTime) throw Error("EDM DLL 时间晚于可冻结的当前时间，请等待正常文件状态稳定。");
                var scope = new AndroidResolverGradleEnvironment { m_RecoveryDirectory = recoveryDirectory };
                var records = new List<FileState>();
                foreach (var pair in originals)
                {
                    string path = Target(buildDirectory, pair.Key);
                    RequireNoLinks(path);
                    bool exists = File.Exists(path);
                    if (Directory.Exists(path)) throw Error("wrapper 目标已被目录占用：" + pair.Key);
                    byte[] original = exists ? File.ReadAllBytes(path) : null;
                    if (exists && Hash(original) != Hash(pair.Value)) throw Error("现存 wrapper 内容未知，保持原样：" + pair.Key);
                    FileAttributes attributes = exists ? File.GetAttributes(path) : FileAttributes.Normal;
                    if ((attributes & FileAttributes.ReadOnly) != 0) throw Error("现存 wrapper 为只读，保持原样：" + pair.Key);
                    bool isWindows = Path.DirectorySeparatorChar == '\\';
                    uint mode = SelectFileMode(exists ? ReadMode(path) : 0x1a4, false, isWindows);
                    records.Add(new FileState { relative = pair.Key, existed = exists,
                        original = exists ? Convert.ToBase64String(original) : null, originalHash = exists ? Hash(original) : null,
                        originalTime = exists ? File.GetLastWriteTimeUtc(path).Ticks : 0, originalAttributes = (int)attributes,
                        originalMode = mode, expectedHash = Hash(pair.Key == PropertiesName ? CompatibleProperties(pair.Value) : pair.Value),
                        expectedTime = preparedTime, expectedAttributes = (int)attributes,
                        expectedMode = SelectFileMode(mode, pair.Key == "gradlew", isWindows) });
                }
                var created = new List<string>();
                scope.m_Recovery = new Recovery { buildDirectory = buildDirectory, files = records.ToArray() };
                try
                {
                    CreateDirectory(recoveryDirectory, created);
                    scope.m_Lock = new FileStream(Path.Combine(recoveryDirectory, "scope.lock"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                    foreach (FileState record in records) CreateDirectory(Path.GetDirectoryName(Target(buildDirectory, record.relative)), created);
                    scope.m_Recovery.createdDirectories = created.ToArray();
                    byte[] journal = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(scope.m_Recovery));
                    scope.m_JournalHash = Hash(journal);
                    File.WriteAllBytes(Path.Combine(recoveryDirectory, "recovery.json"), journal);
                    s_Active = scope;
                    foreach (FileState record in records)
                    {
                        string path = Target(buildDirectory, record.relative);
                        if (!Matches(path, record, false)) throw Error("wrapper 写前内容或元数据发生变化：" + record.relative);
                        File.WriteAllBytes(path, record.relative == PropertiesName ? CompatibleProperties(originals[record.relative]) : originals[record.relative]);
                        File.SetAttributes(path, (FileAttributes)record.expectedAttributes);
                        SetMode(path, record.expectedMode);
                        File.SetLastWriteTimeUtc(path, new DateTime(record.expectedTime, DateTimeKind.Utc));
                        if (!Matches(path, record, true)) throw Error("wrapper 准备后身份或时间条件不一致：" + record.relative);
                    }
                    return scope;
                }
                catch
                {
                    if (s_Active == scope) scope.Dispose();
                    else
                    {
                        scope.m_Lock?.Dispose();
                        string lockPath = Path.Combine(recoveryDirectory, "scope.lock");
                        if (scope.m_Lock != null && File.Exists(lockPath)) File.Delete(lockPath);
                        foreach (string directory in created.AsEnumerable().Reverse()) DeleteEmptyDirectory(directory);
                    }
                    throw;
                }
            }
        }

        /// <summary>结束、失败或重载时恢复原始字节、权限和修改时间；未知并发内容保留现场及恢复材料。</summary>
        public void Dispose()
        {
            lock (Gate)
            {
                if (m_Disposed) return;
                string journal = Path.Combine(m_RecoveryDirectory, "recovery.json");
                if (!File.Exists(journal) || Hash(File.ReadAllBytes(journal)) != m_JournalHash)
                    throw Error("Gradle 恢复材料发生未知变化，保持现场：" + m_RecoveryDirectory);
                string toolchainError = null;
                try { if (m_ReadToolchain != null && m_ReadToolchain() != m_Toolchain) toolchainError = "解析期间 Unity 工具链身份变化。"; }
                catch (Exception exception) { toolchainError = exception.Message; }
                try { RestoreFiles(m_Recovery, m_RecoveryDirectory); }
                finally { m_Lock?.Dispose(); m_Lock = null; }
                File.Delete(Path.Combine(m_RecoveryDirectory, "recovery.json"));
                File.Delete(Path.Combine(m_RecoveryDirectory, "scope.lock"));
                foreach (string directory in m_Recovery.createdDirectories.Reverse()) DeleteEmptyDirectory(directory);
                m_Disposed = true;
                if (ReferenceEquals(s_Active, this)) s_Active = null;
                if (toolchainError != null) throw Error(toolchainError);
            }
        }

        /// <summary>恢复所有仍能证明归属的写入；不覆盖未知字节或元数据。</summary>
        private static void RestoreFiles(Recovery recovery, string recoveryDirectory)
        {
            var failures = new List<string>();
            foreach (FileState record in recovery.files.Reverse())
            {
                string path = Target(recovery.buildDirectory, record.relative);
                try
                {
                    RequireNoLinks(path);
                    if (Matches(path, record, false)) continue;
                    if (!Matches(path, record, true)) { failures.Add(record.relative); continue; }
                    if (!record.existed) { File.Delete(path); continue; }
                    File.WriteAllBytes(path, Convert.FromBase64String(record.original));
                    File.SetAttributes(path, (FileAttributes)record.originalAttributes);
                    SetMode(path, record.originalMode);
                    File.SetLastWriteTimeUtc(path, new DateTime(record.originalTime, DateTimeKind.Utc));
                    if (!Matches(path, record, false)) failures.Add(record.relative);
                }
                catch (Exception) { failures.Add(record.relative); }
            }
            if (failures.Count != 0) throw Error("Gradle 恢复未完成，未知变化保持原样：" + string.Join(", ", failures) + "；恢复材料：" + recoveryDirectory);
        }

        /// <summary>通过完整字节和元数据识别原始状态或本轮预期，不只按文件存在判断所有权。</summary>
        private static bool Matches(string path, FileState state, bool expected)
        {
            if (!expected && !state.existed) return !File.Exists(path) && !Directory.Exists(path);
            return File.Exists(path) && Hash(File.ReadAllBytes(path)) == (expected ? state.expectedHash : state.originalHash) &&
                File.GetLastWriteTimeUtc(path).Ticks == (expected ? state.expectedTime : state.originalTime) &&
                (int)File.GetAttributes(path) == (expected ? state.expectedAttributes : state.originalAttributes) &&
                ReadMode(path) == SelectFileMode(expected ? state.expectedMode : state.originalMode, false, Path.DirectorySeparatorChar == '\\');
        }

        /// <summary>恢复编辑器正常退出或程序集重载之前的活跃事务；异常保留材料并阻止下一次下载。</summary>
        private static void RestoreActive()
        {
            try { lock (Gate) { if (s_Active != null) s_Active.Dispose(); else RestorePending(); } }
            catch (Exception exception) { Debug.LogError(exception.Message); }
        }

        /// <summary>新域或异常退出后的启动仅恢复已审文件集合及原始内容，不接受任意路径恢复请求。</summary>
        private static void RestorePending()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string directory = RecoveryDirectory(project);
            string journal = Path.Combine(directory, "recovery.json");
            if (!File.Exists(journal)) return;
            try
            {
                RequireNoLinks(journal);
                if (new FileInfo(journal).Length > 512 * 1024) throw Error("Gradle 恢复材料超出已审容量。");
                Recovery recovery = JsonUtility.FromJson<Recovery>(File.ReadAllText(journal));
                string build = Path.Combine(project, "Temp", "PlayServicesResolverGradle");
                if (recovery == null || recovery.version != 1 || recovery.buildDirectory != build || recovery.files == null ||
                    !recovery.files.Select(file => file.relative).OrderBy(value => value).SequenceEqual(OriginalHashes.Keys.OrderBy(value => value)))
                    throw Error("Gradle 恢复材料身份或路径无效。");
                foreach (FileState file in recovery.files)
                {
                    if (file.expectedHash != (file.relative == PropertiesName ? CompatiblePropertiesHash : OriginalHashes[file.relative]))
                        throw Error("Gradle 恢复预期不是已审生成配置。");
                    if (file.existed && (file.originalHash != OriginalHashes[file.relative] || Hash(Convert.FromBase64String(file.original)) != file.originalHash))
                        throw Error("Gradle 恢复原始内容不是已审 wrapper。");
                }
                var scope = new AndroidResolverGradleEnvironment { m_Recovery = recovery, m_RecoveryDirectory = directory,
                    m_JournalHash = Hash(File.ReadAllBytes(journal)),
                    m_Lock = new FileStream(Path.Combine(directory, "scope.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None) };
                // 恢复不会重放解析；仅接受本项目两个根下的已记录新建目录。
                if (recovery.createdDirectories == null || recovery.createdDirectories.Any(path =>
                    !(path.StartsWith(build + Path.DirectorySeparatorChar, StringComparison.Ordinal) || path == build || path == Path.Combine(project, "Temp") ||
                      path.StartsWith(Path.Combine(project, "Library") + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                      (directory.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.Ordinal) || path == directory))))
                { scope.m_Lock.Dispose(); throw Error("Gradle 恢复目录集合无效。"); }
                scope.Dispose();
            }
            catch (Exception exception) { Debug.LogError("[AndroidResolver] Gradle 待恢复事务保留，禁止重放：" + exception.Message); }
        }

        /// <summary>核对实际继承的 JDK17 与已审 EDM 身份；Gradle 分发由 wrapper 的固定 URL 和校验值约束。</summary>
        private static string CaptureToolchain(Type resolver, string javaHome)
        {
            if (!AndroidEdmCompatibilityContract.TryVerifyType(resolver, "Google.JarResolver.dll", CancellationToken.None, out string edmError))
                throw Error(edmError);
            Type files = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name == "Google.VersionHandlerImpl")
                .Select(assembly => assembly.GetType("Google.FileUtils", false)).SingleOrDefault();
            if (!AndroidEdmCompatibilityContract.TryVerifyType(files, "Google.VersionHandlerImpl.dll", CancellationToken.None, out string utilityError))
                throw Error(utilityError);
            foreach (string option in new[] { "JAVA_OPTS", "GRADLE_OPTS" })
                if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(option))) throw Error("Gradle 含额外启动参数，须先核对：" + option);
            string release = Path.Combine(javaHome, "release");
            if (!File.ReadAllLines(release).Any(line => line.StartsWith("JAVA_VERSION=\"17.", StringComparison.Ordinal)))
                throw Error("EDM 下载兼容作用域当前仅审核 JDK17 与官方 Gradle8.13 分发。");
            string java = Path.Combine(javaHome, "bin", Path.DirectorySeparatorChar == '\\' ? "java.exe" : "java");
            return javaHome + "|" + Hash(File.ReadAllBytes(java)) + "|" + Hash(File.ReadAllBytes(release)) +
                "|" + File.GetLastWriteTimeUtc(resolver.Assembly.Location).Ticks;
        }

        /// <summary>只读核查实际厂商解析队列，既不取消自动任务，也不修改厂商调度状态。</summary>
        private static void RequireResolverIdle(Type resolver)
        {
            FieldInfo jobsField = resolver.GetField("resolutionJobs", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo autoField = resolver.GetField("autoResolving", BindingFlags.NonPublic | BindingFlags.Static);
            if (!(jobsField?.GetValue(null) is ICollection jobs) || jobs.Count != 0 || !(autoField?.GetValue(null) is bool auto) || auto)
                throw Error("EDM 已有解析任务或未知调度状态，不能改变下载生成配置。");
        }

        /// <summary>核对已审无参布尔属性的真实签名后读取原分支值。</summary>
        private static bool ReadBoolean(Type type, string name, BindingFlags visibility)
        {
            PropertyInfo property = type.GetProperty(name, visibility | BindingFlags.Static);
            if (property?.PropertyType != typeof(bool) || property.GetIndexParameters().Length != 0) throw Error("EDM 分支属性签名变化：" + name);
            return (bool)property.GetValue(null);
        }

        /// <summary>只改生成配置的精确原始分发条目，并启用官方分发包校验。</summary>
        internal static byte[] CompatibleProperties(byte[] original)
        {
            string value = Encoding.UTF8.GetString(original);
            if (value.Split(new[] { OriginalDistribution }, StringSplitOptions.None).Length != 2 || value.Contains("distributionSha256Sum"))
                throw Error("原始 Gradle wrapper 分发配置不符合已审结构。");
            return new UTF8Encoding(false).GetBytes(value.Replace(OriginalDistribution,
                "distributionUrl=https\\://services.gradle.org/distributions/gradle-8.13-bin.zip") +
                "\ndistributionSha256Sum=" + DistributionSha256 + "\n");
        }

        /// <summary>读取原始嵌入资源，缺失时明确拒绝，不自行补造下载脚本。</summary>
        private static byte[] ReadResource(Assembly assembly, string name)
        {
            using (Stream input = assembly.GetManifestResourceStream(name))
            using (var output = new MemoryStream())
            { if (input == null) throw Error("EDM 缺少原始嵌入资源：" + name); input.CopyTo(output); return output.ToArray(); }
        }

        /// <summary>计算完整内容摘要，用于来源、写前和恢复归属核对。</summary>
        private static string Hash(byte[] bytes)
        { using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant(); }

        /// <summary>限制恢复和写入目标为五个固定文件，拒绝路径穿越。</summary>
        private static string Target(string directory, string relative)
        { if (!OriginalHashes.ContainsKey(relative)) throw Error("Gradle 文件不在已审集合。"); return Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar)); }

        /// <summary>恢复事务固定属于当前工程 Library，不写入用户偏好或包缓存。</summary>
        private static string RecoveryDirectory(string project)
        { return Path.Combine(project, "Library", "Nova", "AndroidResolver", "GradleEnvironmentRecovery"); }

        /// <summary>逐层拒绝符号链接或重解析点，避免写入落到声明路径之外。</summary>
        private static void RequireNoLinks(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw Error("Gradle 生成或恢复路径含符号链接：" + current);
        }

        /// <summary>记录本轮真实新建目录，收尾仅移除仍为空的这些目录。</summary>
        private static void CreateDirectory(string path, List<string> created)
        {
            if (Directory.Exists(path)) return;
            string parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent)) CreateDirectory(parent, created);
            Directory.CreateDirectory(path); created.Add(path);
        }

        /// <summary>保留已有或厂商新生成内容，不递归删除目录。</summary>
        private static void DeleteEmptyDirectory(string path)
        { if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path); }

        /// <summary>选择记录及比对的文件权限；Windows 固定为零，只有 POSIX 的 gradlew 添加执行位。</summary>
        internal static uint SelectFileMode(uint mode, bool executable, bool isWindows)
        { return isWindows ? 0 : executable ? mode | 0x48u : mode; }

        /// <summary>POSIX 只读取得权限位；Windows 文件写入不替换 ACL，保留 FileAttributes。</summary>
        private static uint ReadMode(string path)
        {
            if (Path.DirectorySeparatorChar == '\\') return 0;
            string format = Application.platform == RuntimePlatform.OSXEditor ? "-f %Lp " : "-c %a ";
            var start = new ProcessStartInfo("/usr/bin/stat", format + "\"" + path.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"")
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (Process process = Process.Start(start))
            {
                if (process == null) throw Error("无法启动 wrapper 权限读取。");
                if (!process.WaitForExit(5000)) { process.Kill(); process.WaitForExit(2000); throw Error("读取 wrapper 权限超时。"); }
                string value = process.StandardOutput.ReadToEnd().Trim();
                if (process.ExitCode != 0 || !System.Text.RegularExpressions.Regex.IsMatch(value, "^[0-7]{3,4}$")) throw Error("无法读取 wrapper 原始权限。");
                return Convert.ToUInt32(value, 8);
            }
        }

        /// <summary>仅恢复已记录的 POSIX 权限位，不调用 shell 或修改用户环境。</summary>
        private static void SetMode(string path, uint mode)
        { if (Path.DirectorySeparatorChar != '\\' && Chmod(path, mode) != 0) throw Error("wrapper 权限恢复失败。"); }

        /// <summary>统一生成可定位的同步下载失败信息。</summary>
        private static InvalidOperationException Error(string message)
        { return new InvalidOperationException("[AndroidResolver] " + message); }

        /// <summary>按 POSIX 原生权限位设置本轮生成文件，不经过 shell。</summary>
        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int Chmod(string path, uint mode);
    }
}
