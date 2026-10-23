/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DataModuleExportActionCommon.cs
 * author:    taoye
 * created:   2026/8/21
 * descrip:   Sound/Vibrate 精确导出 Action 的产物计划与验证辅助
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Compilation;

namespace NovaFramework.Editor
{
    internal static class DataModuleExportActionCommon
    {
        [Serializable]
        internal sealed class NamespaceSourceSnapshot
        {
            public string masterGuid;
            public string masterPath;
            public string namespaceValue;
            public GenerateActionCommon.Artifact[] sourceFiles;
        }

        [Serializable]
        internal sealed class FileState
        {
            public string path;
            public bool exists;
            public string sha256;
        }

        [Serializable]
        internal sealed class DirectoryState
        {
            public string path;
            public bool exists;
            public FileState[] files;
        }

        [Serializable]
        internal sealed class InputSnapshot
        {
            public FileState[] files;
            public DirectoryState[] directories;
        }

        [Serializable]
        internal sealed class ConfigurationSnapshot
        {
            public string profileId;
            public string sourceDirectory;
            public string codeDirectory;
            public string[] sourcePaths;
            public bool includesCode;
            public bool fullCodeBatch;
        }

        /// <summary>识别显式新收据字段，包括 null；缺字段才走历史摘要兼容，不把损坏的新收据降为旧收据。</summary>
        internal static bool HasSourceContract(string receiptJson) => JObject.Parse(receiptJson).Properties().Any(property =>
            new[] { "sourceContractVersion", "namespaceSource", "inputSnapshot", "configurations", "configurationArtifacts", "compilationToken" }
                .Contains(property.Name, StringComparer.Ordinal));

        /// <summary>排除明确允许由本次 Sync 重写的配置文件，再只读核对其他来源与完整模板成员。</summary>
        internal static bool TryVerifyUnchangedInputs(InputSnapshot snapshot, IEnumerable<ConfigurationSnapshot> configurations,
            CancellationToken cancellationToken, out string error)
        {
            error = "导出输入快照不完整。";
            if (snapshot?.files == null || snapshot.directories == null || configurations == null || snapshot.files.Any(item => item == null)) return false;
            var writable = new HashSet<string>(configurations.SelectMany(ConfigurationFiles).Select(NormalizeReadPath), StringComparer.Ordinal);
            return TryVerifyInputs(new InputSnapshot { files = snapshot.files.Where(item => !writable.Contains(item.path)).ToArray(),
                directories = snapshot.directories }, cancellationToken, out error);
        }

        /// <summary>捕获实际持久配置产物摘要；全量代码包含真实 output-manifest，单 Unit 不要求该文件存在。</summary>
        internal static GenerateActionCommon.Artifact[] CaptureConfigurationArtifacts(IEnumerable<ConfigurationSnapshot> configurations,
            CancellationToken cancellationToken) => configurations.SelectMany(configuration => ConfigurationFiles(configuration)
                .Concat(configuration.fullCodeBatch ? new[] { Path.Combine(EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(configuration.sourceDirectory),
                    "output-manifests", configuration.profileId + ".json") } : Array.Empty<string>()))
            .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => GenerateActionCommon.CaptureFile(path, cancellationToken)).ToArray();

        /// <summary>核对新收据完整配置文件集合与正文 Hash，不能仅用一份有效摘要冒充全部生成配置。</summary>
        internal static bool TryVerifyConfigurationArtifacts(IEnumerable<ConfigurationSnapshot> configurations,
            GenerateActionCommon.Artifact[] artifacts, CancellationToken cancellationToken, out string error)
        {
            error = "导出配置产物摘要缺失或范围不完整。";
            if (configurations == null || artifacts == null) return false;
            try
            {
                GenerateActionCommon.Artifact[] current = CaptureConfigurationArtifacts(configurations, cancellationToken);
                if (!JToken.DeepEquals(JToken.FromObject(current), JToken.FromObject(artifacts))) return false;
                error = null;
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { error = exception.Message; return false; }
        }

        /// <summary>只读冻结已持久绑定的 Master 和 Globals；明确配置的空 Namespace 合法，不猜测场景或读取 Runtime。</summary>
        internal static bool TryCaptureNamespaceSource(CancellationToken cancellationToken,
            out NamespaceSourceSnapshot snapshot, out string error)
        {
            snapshot = null;
            if (!EditorUtil.Config.WorkspaceActive.TryGetPersistedConfigMaster(out ConfigMasterSO master,
                    out string guid, out string path, out error)) return false;
            if (EditorUtility.IsDirty(master)) { error = "ConfigMaster 存在未保存改动，请保存后重新 Plan。"; return false; }
            try
            {
                snapshot = new NamespaceSourceSnapshot
                {
                    masterGuid = guid, masterPath = path, namespaceValue = master.Namespace ?? string.Empty,
                    sourceFiles = new[] { GenerateActionCommon.CaptureFile(path, cancellationToken),
                        GenerateActionCommon.CaptureFile("ProjectSettings/Nova/Globals.json", cancellationToken) },
                };
                error = null;
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { error = exception.Message; return false; }
        }

        /// <summary>重读精确绑定、Namespace 和两个原文件字节；缺失、脏状态或任何漂移均不能沿用旧计划。</summary>
        internal static bool TryVerifyNamespaceSource(NamespaceSourceSnapshot snapshot,
            CancellationToken cancellationToken, out string error)
        {
            error = "命名空间来源快照缺失或不完整。";
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.masterGuid) ||
                string.IsNullOrWhiteSpace(snapshot.masterPath) || snapshot.namespaceValue == null ||
                snapshot.sourceFiles == null || snapshot.sourceFiles.Length != 2) return false;
            if (!TryCaptureNamespaceSource(cancellationToken, out NamespaceSourceSnapshot current, out error)) return false;
            if (snapshot.masterGuid != current.masterGuid || snapshot.masterPath != current.masterPath ||
                snapshot.namespaceValue != current.namespaceValue ||
                !JToken.DeepEquals(JToken.FromObject(snapshot.sourceFiles), JToken.FromObject(current.sourceFiles)))
            { error = "ConfigMaster、Globals 绑定或 Namespace 源字节已漂移，请重新 Plan。"; return false; }
            error = null;
            return true;
        }

        /// <summary>冻结领域入口已经解析的只读文件和完整目录成员；模板可在 PackageCache 物理目录，不改变项目写路径门。</summary>
        internal static InputSnapshot CaptureInputs(IEnumerable<string> filePaths, IEnumerable<string> directoryPaths,
            CancellationToken cancellationToken)
        {
            return new InputSnapshot
            {
                files = (filePaths ?? Array.Empty<string>()).Select(NormalizeReadPath).Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal).Select(path => CaptureInputFile(path, cancellationToken)).ToArray(),
                directories = (directoryPaths ?? Array.Empty<string>()).Select(NormalizeReadPath).Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal).Select(path => CaptureInputDirectory(path, cancellationToken)).ToArray(),
            };
        }

        /// <summary>复算文件存在性、目录成员和 SHA；新增、删除、重解析点或缺失字段均拒绝旧快照。</summary>
        internal static bool TryVerifyInputs(InputSnapshot snapshot, CancellationToken cancellationToken, out string error)
        {
            error = "导出输入快照缺失或不完整。";
            if (snapshot?.files == null || snapshot.directories == null || snapshot.files.Any(item => item == null) ||
                snapshot.directories.Any(item => item == null || item.files == null)) return false;
            try
            {
                InputSnapshot actual = CaptureInputs(snapshot.files.Select(item => item.path),
                    snapshot.directories.Select(item => item.path), cancellationToken);
                if (!JToken.DeepEquals(JToken.FromObject(snapshot), JToken.FromObject(actual)))
                { error = "导出输入文件、模板目录成员或 SHA-256 已漂移，请重新 Plan。"; return false; }
                error = null;
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { error = exception.Message; return false; }
        }

        /// <summary>比较冻结路径与领域重新解析的完整输入范围，避免空数组或删项绕过仅逐项 Hash 校验。</summary>
        internal static bool HasExactInputScope(InputSnapshot snapshot, InputSnapshot expected) =>
            snapshot?.files != null && snapshot.directories != null && expected?.files != null && expected.directories != null &&
            snapshot.files.All(item => item != null) && snapshot.directories.All(item => item != null) &&
            snapshot.files.Select(item => item.path).SequenceEqual(expected.files.Select(item => item.path), StringComparer.Ordinal) &&
            snapshot.directories.Select(item => item.path).SequenceEqual(expected.directories.Select(item => item.path), StringComparer.Ordinal);

        /// <summary>按既有固定 Profile 冻结配置来源和本次代码范围，不读取或同步 _configs。</summary>
        internal static ConfigurationSnapshot CreateConfigurationSnapshot(EditorUtil.Luban.LubanExportProfile profile,
            string sourceDirectory, string codeDirectory, IEnumerable<string> sourcePaths, bool includesCode, bool fullCodeBatch)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!GenerateActionCommon.TryResolveProjectPath(sourceDirectory, "配置来源", out string source, out string error))
                throw new InvalidDataException(error);
            if (includesCode && !GenerateActionCommon.TryResolveProjectPath(codeDirectory, "代码输出", out _, out error))
                throw new InvalidDataException(error);
            return new ConfigurationSnapshot
            {
                profileId = profile.Id, sourceDirectory = source, codeDirectory = includesCode ? NormalizeReadPath(codeDirectory) : null,
                sourcePaths = (sourcePaths ?? Array.Empty<string>()).Select(EditorUtil.Luban.LubanSchemaManifestValidator.NormalizeRelativeSourcePath)
                    .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                includesCode = includesCode, fullCodeBatch = includesCode && fullCodeBatch,
            };
        }

        /// <summary>枚举真实持久配置写入、元文件和目录元文件；全量代码才更新既有 output-manifest。</summary>
        internal static IEnumerable<string> EnumerateConfigurationWritePaths(IEnumerable<ConfigurationSnapshot> configurations)
        {
            foreach (ConfigurationSnapshot configuration in configurations ?? Array.Empty<ConfigurationSnapshot>())
            {
                string directory = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(configuration.sourceDirectory);
                IEnumerable<string> files = ConfigurationFiles(configuration);
                if (configuration.fullCodeBatch) files = files.Append(Path.Combine(directory, "output-manifests", configuration.profileId + ".json"));
                foreach (string path in EnumerateFileWritePaths(files)) yield return path;
            }
        }

        /// <summary>枚举三个 SyncFromInspector 持久文件，供计划冻结与写集共用真实定义。</summary>
        internal static IEnumerable<string> ConfigurationFiles(ConfigurationSnapshot configuration)
        {
            string directory = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(configuration.sourceDirectory);
            yield return Path.Combine(directory, EditorUtil.Luban.ConfigSyncer.c_LubanConfFileName);
            yield return Path.Combine(directory, EditorUtil.Luban.ConfigSyncer.c_TablesXmlFileName);
            yield return Path.Combine(directory, EditorUtil.Luban.LubanSchemaManifestStore.FileName);
        }

        /// <summary>枚举文件替换及 Unity 可能创建的元文件；只描述项目内实际路径，不执行写入。</summary>
        internal static IEnumerable<string> EnumerateFileWritePaths(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                if (!GenerateActionCommon.TryResolveProjectPath(path, "计划写入", out string full, out string error))
                    throw new InvalidDataException(error);
                yield return full; yield return full + ".meta";
                string parent = Path.GetDirectoryName(full);
                while (!string.IsNullOrEmpty(parent) && GenerateActionCommon.ToProjectRelative(parent).StartsWith("Assets/", StringComparison.Ordinal))
                {
                    if (!Directory.Exists(parent) || !File.Exists(parent + ".meta")) yield return parent + ".meta";
                    parent = Path.GetDirectoryName(parent);
                }
            }
        }

        /// <summary>描述代码目录及其新建目录 meta，目录内生成文件和过期文件删除均属于明确的目录范围。</summary>
        internal static IEnumerable<string> EnumerateDirectoryWritePaths(IEnumerable<string> paths)
        {
            foreach (string path in paths.Distinct(StringComparer.Ordinal))
            {
                if (!GenerateActionCommon.TryResolveProjectPath(path, "代码目录写入", out string full, out string error))
                    throw new InvalidDataException(error);
                yield return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "/**";
                string parent = full;
                while (!string.IsNullOrEmpty(parent) && GenerateActionCommon.ToProjectRelative(parent).StartsWith("Assets/", StringComparison.Ordinal))
                {
                    if (!Directory.Exists(parent) || !File.Exists(parent + ".meta")) yield return parent + ".meta";
                    parent = Path.GetDirectoryName(parent);
                }
            }
        }

        /// <summary>校验真实 JSON target、Manifest 和代码正文所有权；空字符串是明确 Namespace，缺键/null 不等价。</summary>
        internal static bool TryVerifyExportedConfigurations(NamespaceSourceSnapshot snapshot,
            IEnumerable<ConfigurationSnapshot> configurations, CancellationToken cancellationToken, out string error)
        {
            error = null;
            try
            {
                if (snapshot?.namespaceValue == null) throw new InvalidDataException("Namespace 来源快照缺失。");
                foreach (ConfigurationSnapshot configuration in configurations ?? throw new InvalidDataException("配置快照缺失。"))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EditorUtil.Luban.LubanExportProfile profile = ResolveProfile(configuration.profileId);
                    string conf = ConfigurationFiles(configuration).First();
                    JObject value = JObject.Parse(File.ReadAllText(conf));
                    JArray targets = value["targets"] as JArray;
                    JObject[] matches = targets?.OfType<JObject>().Where(item => (string)item["name"] == profile.TargetName).ToArray();
                    if (matches == null || matches.Length != 1 || matches[0]["manager"]?.Type != JTokenType.String ||
                        (string)matches[0]["manager"] != profile.ManagerName || matches[0]["topModule"]?.Type != JTokenType.String ||
                        (string)matches[0]["topModule"] != snapshot.namespaceValue)
                        throw new InvalidDataException("Luban target、manager 或 Namespace 与冻结真源不匹配。");
                    EditorUtil.Luban.LubanSchemaManifest manifest = EditorUtil.Luban.LubanSchemaManifestStore.Load(configuration.sourceDirectory);
                    if (manifest.ProfileId != profile.Id || configuration.sourcePaths == null || configuration.sourcePaths.Length == 0)
                        throw new InvalidDataException("Luban Manifest Profile 或源范围缺失。");
                    foreach (string source in configuration.sourcePaths) manifest.ResolveUnit(source);
                    if (configuration.includesCode)
                        foreach (string name in ExpectedCodeNames(configuration, manifest, profile))
                        {
                            string path = Path.Combine(configuration.codeDirectory, name + ".cs");
                            if (!GenerateActionCommon.TryResolveProjectPath(path, "生成代码", out _, out string pathError) ||
                                !EditorUtil.Luban.GeneratedOutput.IsOwnedAndUnmodified(path, profile.Id,
                                    GenerateActionCommon.ToProjectRelative(configuration.sourceDirectory)))
                                throw new InvalidDataException(pathError ?? "生成代码缺失、Profile 归属不符或正文被改写：" + name);
                        }
                }
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { error = exception.Message; return false; }
        }

        /// <summary>在调用方通过本次编译令牌后，精确查询已加载的生成类型；不实例化、执行业务或按短名回退。</summary>
        internal static bool TryVerifyLoadedTypes(NamespaceSourceSnapshot snapshot,
            IEnumerable<ConfigurationSnapshot> configurations, out string error)
        {
            error = null;
            try
            {
                foreach (ConfigurationSnapshot configuration in configurations.Where(item => item.includesCode))
                {
                    EditorUtil.Luban.LubanExportProfile profile = ResolveProfile(configuration.profileId);
                    EditorUtil.Luban.LubanSchemaManifest manifest = EditorUtil.Luban.LubanSchemaManifestStore.Load(configuration.sourceDirectory);
                    foreach (string name in ExpectedCodeNames(configuration, manifest, profile))
                    {
                        string fullName = string.IsNullOrEmpty(snapshot.namespaceValue) ? name : snapshot.namespaceValue + "." + name;
                        string scriptPath = Path.Combine(configuration.codeDirectory, name + ".cs");
                        string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(GenerateActionCommon.ToProjectRelative(scriptPath));
                        assemblyName = string.IsNullOrWhiteSpace(assemblyName) ? null : Path.GetFileNameWithoutExtension(assemblyName);
                        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name == assemblyName).ToArray();
                        Type type = assemblies.Length == 1 ? assemblies[0].GetType(fullName, false, false) : null;
                        if (type == null || type.FullName != fullName ||
                            type.Assembly.GetName().Name != assemblyName ||
                            (name == profile.ManagerName && !typeof(ILubanTables).IsAssignableFrom(type)) ||
                            (manifest.Units.SelectMany(unit => unit.Tables).Any(table => table.Name == name) && !typeof(ITable).IsAssignableFrom(type)))
                            throw new InvalidDataException("本次编译后未找到预期生成类型或 Tables 契约不符：" + fullName);
                    }
                }
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        /// <summary>从真实 Manifest 枚举本次生成类型名称，与 GeneratedOutput 的 Unit/manager 范围保持一致。</summary>
        private static IEnumerable<string> ExpectedCodeNames(ConfigurationSnapshot configuration,
            EditorUtil.Luban.LubanSchemaManifest manifest, EditorUtil.Luban.LubanExportProfile profile) =>
            configuration.sourcePaths.Select(manifest.ResolveUnit).SelectMany(unit => unit.Tables)
                .SelectMany(table => new[] { table.Name, table.ValueType }).Append(profile.ManagerName).Distinct(StringComparer.Ordinal);

        /// <summary>只允许复用已登记的领域 Profile，不接受收据创造新目标。</summary>
        private static EditorUtil.Luban.LubanExportProfile ResolveProfile(string id) =>
            EditorUtil.Luban.LubanExportProfiles.All.SingleOrDefault(item => item.Id == id) ??
            throw new InvalidDataException("未知 Luban Profile：" + id);

        /// <summary>捕获允许缺失的普通输入文件；字节 Hash 与存在性同时冻结。</summary>
        private static FileState CaptureInputFile(string path, CancellationToken cancellationToken)
        {
            RequireOrdinaryReadPath(path);
            if (Directory.Exists(path)) throw new InvalidDataException("输入文件路径被目录占用：" + path);
            bool exists = File.Exists(path);
            return new FileState { path = path, exists = exists, sha256 = exists ? GenerateActionCommon.ComputeFileHash(path, cancellationToken) : null };
        }

        /// <summary>递归捕获目录全部非 meta 成员；新文件、删除和目录替换均成为漂移，不接受链接树。</summary>
        private static DirectoryState CaptureInputDirectory(string path, CancellationToken cancellationToken)
        {
            RequireOrdinaryReadPath(path);
            if (File.Exists(path)) throw new InvalidDataException("输入目录被文件占用：" + path);
            bool exists = Directory.Exists(path);
            return new DirectoryState { path = path, exists = exists, files = exists
                ? EnumerateReadFiles(path, cancellationToken).OrderBy(value => value, StringComparer.Ordinal)
                    .Select(value => CaptureInputFile(value, cancellationToken)).ToArray() : Array.Empty<FileState>() };
        }

        /// <summary>逐层只读枚举并拒绝重解析点；meta 不影响模板与工具执行，不计入输入成员。</summary>
        private static IEnumerable<string> EnumerateReadFiles(string directory, CancellationToken cancellationToken)
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory).OrderBy(value => value, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested(); RequireOrdinaryReadPath(path);
                if (Directory.Exists(path)) foreach (string file in EnumerateReadFiles(path, cancellationToken)) yield return file;
                else if (!path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) yield return path;
            }
        }

        /// <summary>规范领域入口提供的只读路径，允许已解析的包物理位置，不赋予任何外部写权限。</summary>
        private static string NormalizeReadPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\0') >= 0) throw new InvalidDataException("只读输入路径为空或无效。");
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(GenerateActionCommon.ProjectRoot, path));
        }

        /// <summary>从盘根到只读目标检查现有路径段，拒绝符号链接/junction，避免快照跟随外部替换。</summary>
        private static void RequireOrdinaryReadPath(string path)
        {
            string current = Path.GetPathRoot(path);
            foreach (string part in path.Substring(current.Length).Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("只读输入包含符号链接或重解析点：" + current);
            }
        }

        [Serializable]
        internal sealed class Output
        {
            public string kind;
            public string sourcePath;
            public string path;
            public GenerateActionCommon.Artifact artifact;
        }

        /// <summary>
        /// 将导出范围规范为 all、code 或 data。
        /// </summary>
        internal static bool TryNormalizeScope(string value, out string scope)
        {
            scope = value?.Trim().ToLowerInvariant();
            return scope == "all" || scope == "code" || scope == "data";
        }

        /// <summary>
        /// 判断当前范围是否包含代码产物。
        /// </summary>
        internal static bool IncludesCode(string scope) => scope == "all" || scope == "code";

        /// <summary>
        /// 判断当前范围是否包含数据产物。
        /// </summary>
        internal static bool IncludesData(string scope) => scope == "all" || scope == "data";

        /// <summary>
        /// 校验可选源文件相对路径列表的数量、内容与唯一性。
        /// </summary>
        internal static bool TryValidateSourcePaths(string[] values, out string error)
        {
            error = null;
            if (values == null || values.Length == 0) return true;
            if (values.Length > 128 || values.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 1024 || value.IndexOf('\0') >= 0))
            {
                error = "sourcePaths 最多 128 项，每项必须是长度不超过 1024 的非空路径。";
                return false;
            }
            if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            {
                error = "sourcePaths 不能重复。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 校验所有输出均位于当前 Unity 项目内。
        /// </summary>
        internal static bool TryValidateOutputPaths(IEnumerable<string> paths, out string error)
        {
            error = null;
            foreach (string path in paths.Distinct(StringComparer.Ordinal))
            {
                if (!GenerateActionCommon.TryResolveProjectPath(path, "导出路径", out _, out error)) return false;
            }
            return true;
        }

        /// <summary>
        /// 校验源文件存在且没有逃逸已冻结的源目录。
        /// </summary>
        internal static bool TryValidateSourceFile(string sourceDirectory, string relativePath, out string error)
        {
            error = null;
            if (!GenerateActionCommon.TryResolveProjectPath(sourceDirectory, "源目录", out string root, out error) ||
                !GenerateActionCommon.TryResolveProjectPath(Path.Combine(sourceDirectory, relativePath), "源文件", out string file, out error))
                return false;
            root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(file))
            {
                error = "源文件必须存在且位于冻结的源目录内：" + relativePath;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 生成排序、去重且统一分隔符的写入集。
        /// </summary>
        internal static string[] BuildWriteSet(IEnumerable<string> paths)
        {
            return paths.Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Replace('\\', '/'))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 捕获单个文件或目录产物的稳定摘要。
        /// </summary>
        internal static Output Capture(string kind, string sourcePath, string path, CancellationToken cancellationToken)
        {
            GenerateActionCommon.Artifact artifact = File.Exists(path)
                ? GenerateActionCommon.CaptureFile(path, cancellationToken)
                : GenerateActionCommon.CaptureDirectory(path, "*", cancellationToken);
            return new Output { kind = kind, sourcePath = sourcePath, path = path, artifact = artifact };
        }

        /// <summary>
        /// 只读复算 Receipt 中全部产物摘要。
        /// </summary>
        internal static bool TryVerify(Output[] outputs, CancellationToken cancellationToken,
            out GenerateActionCommon.Artifact[] actual, out string error)
        {
            if (outputs == null || outputs.Length == 0 || outputs.Any(output => output?.artifact == null))
            {
                actual = Array.Empty<GenerateActionCommon.Artifact>();
                error = "Receipt 未包含完整导出产物。";
                return false;
            }
            return GenerateActionCommon.TryVerifyArtifacts(outputs.Select(output => output.artifact), cancellationToken, out actual, out error);
        }

        /// <summary>
        /// 校验 Receipt 精确覆盖计划内的产物身份与范围。
        /// </summary>
        internal static bool HasExactCoverage(Output[] actual, IEnumerable<Output> expected)
        {
            Output[] expectedArray = expected.ToArray();
            return actual != null && actual.Length == expectedArray.Length && expectedArray.All(item =>
                actual.Any(value => value != null && value.kind == item.kind && value.sourcePath == item.sourcePath && value.path == item.path));
        }
    }
}
