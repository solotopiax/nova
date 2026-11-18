/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidRefreshSourceIdentityContract.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   Android 刷新回调的可移植源码与真实 Unity 编译归属合同
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.Compilation;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>已审来源绑定完整编译源；当前项目的 DLL/MVID 和编译环境只用于本次计划冻结，不充当跨工程准入摘要。</summary>
    internal static class AndroidRefreshSourceIdentityContract
    {
        /// <summary>随包维护的源码身份；不保存开发机路径或某个项目编译出来的 DLL。</summary>
        internal sealed class ReviewedAssembly
        {
            internal string Package, Version, Assembly, Definition, DefinitionSha256, ManifestSha256, SourcesSha256;
            internal int SourceCount;
        }

        private static readonly string[] s_AssemblyNames =
        {
            "Unity.Timeline.Editor", "Unity.TextMeshPro.Editor", "Unity.ShaderGraph.Editor", "Unity.InputSystem",
            "Unity.2D.Tilemap.Editor", "Unity.2D.SpriteShape.Editor", "Unity.2D.SpriteShape.Runtime", "Unity.2D.Sprite.Editor",
            "Unity.2D.Psdimporter.Editor", "Unity.2D.Animation.Editor", "NovaFramework.MemoryProtection.Editor",
            "NovaFramework.Mcp.Editor", "Unity.AI.Assistant.Editor", "Unity.RenderPipelines.Universal.Editor", "Unity.RenderPipelines.Universal.Runtime",
            "Unity.AI.Animate", "Unity.AI.Image", "Unity.AI.Mesh", "Unity.AI.ModelSelector", "Unity.AI.Pbr", "Unity.AI.Sound", "Unity.AI.Generators.UI",
            "Unity.AI.Generators.Redux", "Unity.AI.Toolkit.Utility", "Unity.AI.Toolkit.Asset", "Unity.AI.Search.Editor",
            "Unity.InferenceEngine.Editor.Visualizer", "Unity.PlasticSCM.Editor"
        };
        [ThreadStatic] private static ReadScope s_ReadScope;
        private static StringComparison PathComparison => Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        /// <summary>仅在同一同步只读遍历内去重文件读取；退出后丢弃，执行重核不继承旧缓存。</summary>
        private sealed class ReadScope : IDisposable
        {
            internal readonly Dictionary<string, string> Hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly Dictionary<System.Reflection.Assembly, string> Assemblies = new Dictionary<System.Reflection.Assembly, string>();
            internal ReviewedAssembly[] Reviewed;
            /// <summary>建立最外层同步只读缓存；嵌套读取不创建独立实例。</summary>
            internal ReadScope() { s_ReadScope = this; }

            /// <summary>结束最外层读取并丢弃缓存；不修改 Unity、源码或任何编译输出。</summary>
            public void Dispose() { if (ReferenceEquals(s_ReadScope, this)) s_ReadScope = null; }
        }

        /// <summary>同一同步只读阶段共享已有缓存，仅最外层持有释放租约；写入、await 或返回下一阶段前必须结束。</summary>
        internal static IDisposable BeginReadScope() => s_ReadScope == null ? new ReadScope() : null;

        /// <summary>只路由固定已审程序集族；reference 不能通过添加任意程序集扩大可执行范围。</summary>
        internal static bool SupportsAssembly(string assemblyName) => s_AssemblyNames.Contains(assemblyName, StringComparer.Ordinal);

        /// <summary>核真实包、完整已审输入、唯一编译输出及加载映像；不加载程序集、不运行回调或触发编译。</summary>
        /// <param name="type">当前域已经加载的实际类型。</param>
        /// <param name="token">本次只读采集的取消令牌。</param>
        /// <param name="identity">含实际编译条件、引用和当前 DLL/MVID 的当次冻结摘要。</param>
        /// <param name="error">具体准入失败原因，不自动采纳未知来源。</param>
        internal static bool TryVerifySourceAssembly(Type type, CancellationToken token, out string identity, out string error)
        {
            identity = null;
            error = null;
            using (s_ReadScope == null ? BeginReadScope() : null)
            try
            {
                token.ThrowIfCancellationRequested();
                Require(type != null && !type.Assembly.IsDynamic && type.Module == type.Assembly.ManifestModule,
                    "源码类型不属于普通单模块程序集。");
                var assembly = type.Assembly;
                string name = assembly.GetName().Name;
                Require(SupportsAssembly(name) && AppDomain.CurrentDomain.GetAssemblies().Count(item => item.GetName().Name == name) == 1 &&
                    assembly.GetType(type.FullName, false, false) == type, "源码程序集没有唯一实际加载身份。");
                if (s_ReadScope.Assemblies.TryGetValue(assembly, out identity)) return true;
                PackageInfo package = PackageInfo.FindForAssembly(assembly);
                Require(package != null && !string.IsNullOrEmpty(package.resolvedPath) && !string.IsNullOrEmpty(package.assetPath) &&
                    PackageInfo.GetAllRegisteredPackages().Count(item => item.name == package.name && item.packageId == package.packageId) == 1,
                    "源码程序集缺唯一真实 PackageInfo 归属。");
                ReviewedAssembly[] reviewed = ReadReviewed();
                ReviewedAssembly spec = reviewed.SingleOrDefault(item => item.Assembly == name && item.Package == package.name && item.Version == package.version);
                Require(spec != null, "源码包版本尚未纳入已审回调合同：" + package.name + "@" + package.version + " / " + name);
                var owners = CompilationPipeline.GetAssemblies(AssembliesType.Editor).Where(item => item.name == name).ToArray();
                Require(owners.Length == 1 && owners[0].sourceFiles != null && owners[0].sourceFiles.Length > 0,
                    "源码程序集缺唯一 Unity Editor 编译所有者。");
                var owner = owners[0];
                string output = PhysicalCompilerPath(owner.outputPath);
                string location = Path.GetFullPath(assembly.Location);
                string expectedOutput = Path.Combine(GenerateActionCommon.ProjectRoot, "Library", "ScriptAssemblies", name + ".dll");
                Require(string.Equals(output, location, PathComparison) && string.Equals(output, expectedOutput, PathComparison),
                    "源码程序集加载路径不等于唯一 Unity 编译输出。");
                string[] sources = owner.sourceFiles.Select(value => PhysicalSourcePath(value, package)).ToArray();
                string definition = PhysicalSourcePath(CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name), package);
                Require(TryVerifySourceInputs(spec, package.resolvedPath, sources, definition, token, out string inputs, out string inputsError), inputsError);
                Require(TryVerifyLoadedImage(location, assembly.ManifestModule.ModuleVersionId, token, out string dll, out string imageError), imageError);
                string compilation = CaptureCompilationIdentity(owner, token);
                identity = GenerateActionCommon.ComputeTextHash(string.Join("|", package.name, package.version, package.source,
                    GenerateActionCommon.ComputeTextHash(package.packageId), GenerateActionCommon.ComputeTextHash(Path.GetFullPath(package.resolvedPath)),
                    inputs, compilation, assembly.FullName, assembly.ManifestModule.ModuleVersionId.ToString("D"), dll));
                s_ReadScope.Assemblies.Add(assembly, identity);
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                error = exception is InvalidOperationException ? exception.Message : "源码编译身份读取失败：" + exception.GetType().Name;
                identity = null;
                return false;
            }
        }

        /// <summary>以相对路径和完整内容核对全部实际编译源，拒绝增加、删除、重排后的重复和跨根 asmref 注入。</summary>
        /// <param name="spec">维护方审核过的包版本、asmdef 和完整源集合摘要。</param>
        /// <param name="root">实际 PackageInfo 解析的包根。</param>
        /// <param name="sourceFiles">唯一编译所有者报告的全部物理源码路径。</param>
        /// <param name="definition">该编译所有者的物理 asmdef 路径。</param>
        /// <param name="token">本次读取取消令牌。</param>
        /// <param name="identity">与开发机位置无关的完整来源身份。</param>
        /// <param name="error">拒绝时的具体原因。</param>
        internal static bool TryVerifySourceInputs(ReviewedAssembly spec, string root, string[] sourceFiles, string definition,
            CancellationToken token, out string identity, out string error)
        {
            identity = null;
            error = null;
            try
            {
                Require(spec != null && sourceFiles != null && sourceFiles.Length == spec.SourceCount && sourceFiles.Length > 0 &&
                    sourceFiles.Length <= 4096, "实际编译源数量与已审完整集合不同。");
                Require(string.Equals(Path.GetFullPath(definition), OwnedFile(root, spec.Definition), PathComparison) &&
                    HashFile(definition, token) == spec.DefinitionSha256 && ManifestHash(OwnedFile(root, "package.json"), spec.Package, spec.Version) == spec.ManifestSha256,
                    "源码包清单或实际 asmdef 归属/内容已变化。");
                var rows = new List<string>();
                foreach (string source in sourceFiles)
                {
                    token.ThrowIfCancellationRequested();
                    string relative = Relative(root, source);
                    Require(relative.EndsWith(".cs", StringComparison.Ordinal), "实际编译输入包含非源码文件。");
                    rows.Add(relative + "|" + HashFile(OwnedFile(root, relative), token));
                }
                Require(rows.Distinct(StringComparer.Ordinal).Count() == rows.Count, "实际编译源集合含重复路径。");
                rows.Sort(StringComparer.Ordinal);
                string hash = GenerateActionCommon.ComputeTextHash(string.Join("\n", rows));
                Require(hash == spec.SourcesSha256, "实际完整编译源已修改、被替换或增加；需维护方审核来源后更新合同。");
                identity = GenerateActionCommon.ComputeTextHash(string.Join("|", spec.Package, spec.Version, spec.Assembly,
                    spec.Definition, spec.DefinitionSha256, spec.ManifestSha256, spec.SourceCount, hash));
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            { error = exception is InvalidOperationException ? exception.Message : "完整编译源读取失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>严格解析有界的可移植来源合同；旧的某工程 DLL/MVID 对不再作为源码回调的许可。</summary>
        internal static bool TryParseReviewed(string json, out ReviewedAssembly[] rows, out string error)
        {
            rows = null;
            error = "Android 刷新源码 reference 格式不完整。";
            if (string.IsNullOrEmpty(json) || json.Length > 32768) return false;
            try
            {
                var value = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (value.Count != 2 || value["schemaVersion"]?.Type != JTokenType.Integer || (int)value["schemaVersion"] != 1 ||
                    !(value["assemblies"] is JArray assemblies) || assemblies.Count < 1 || assemblies.Count > 32) return false;
                var result = new List<ReviewedAssembly>();
                foreach (JToken item in assemblies)
                {
                    if (!(item is JObject row) || row.Count != 8 || new[] { "package", "version", "assembly", "definition", "definitionSha256", "manifestSha256", "sourcesSha256" }
                        .Any(key => row[key]?.Type != JTokenType.String || string.IsNullOrEmpty((string)row[key])) || row["sourceCount"]?.Type != JTokenType.Integer) return false;
                    var spec = new ReviewedAssembly { Package = (string)row["package"], Version = (string)row["version"], Assembly = (string)row["assembly"],
                        Definition = (string)row["definition"], DefinitionSha256 = (string)row["definitionSha256"], ManifestSha256 = (string)row["manifestSha256"],
                        SourcesSha256 = (string)row["sourcesSha256"], SourceCount = (int)row["sourceCount"] };
                    if (!SupportsAssembly(spec.Assembly) || spec.SourceCount < 1 || spec.SourceCount > 4096 || !ValidRelative(spec.Definition) ||
                        !spec.Definition.EndsWith(".asmdef", StringComparison.Ordinal) ||
                        new[] { spec.DefinitionSha256, spec.ManifestSha256, spec.SourcesSha256 }.Any(hash => hash.Length != 64 || hash.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')))) return false;
                    result.Add(spec);
                }
                if (result.Select(item => item.Package + "|" + item.Version + "|" + item.Assembly).Distinct(StringComparer.Ordinal).Count() != result.Count) return false;
                rows = result.ToArray(); error = null; return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is OverflowException || exception is FormatException) { return false; }
        }

        /// <summary>每次只读采集重新打开随包 reference，不自动生成或补写审核数据。</summary>
        private static ReviewedAssembly[] ReadReviewed()
        {
            if (s_ReadScope.Reviewed != null) return s_ReadScope.Reviewed;
            string path = Path.Combine(EditorUtil.AgentSkills.ResolveAgentsRoot(), "Skills", "nova-project-resolve-android-dependencies", "references", "android-refresh-compiled-identity.json");
            Require(File.Exists(path) && new FileInfo(path).Length <= 32768,
                "缺少有效有界 Android 刷新源码 reference。");
            Require(TryParseReviewed(File.ReadAllText(path), out ReviewedAssembly[] rows, out string error), error);
            return s_ReadScope.Reviewed = rows;
        }

        /// <summary>只审核影响源码归属及依赖解析的包名、版本和依赖；展示文字、发布时注入的 Samples 与 UPM 缓存标识不属于编译输入。</summary>
        private static string ManifestHash(string path, string package, string version)
        {
            Require(new FileInfo(path).Length <= 65536, "源码包清单超过容量。");
            var manifest = JObject.Parse(File.ReadAllText(path), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            Require(manifest["name"]?.Type == JTokenType.String && (string)manifest["name"] == package &&
                manifest["version"]?.Type == JTokenType.String && (string)manifest["version"] == version, "源码包清单的名称或版本不匹配实际 PackageInfo。");
            JToken dependencies = manifest["dependencies"] ?? new JObject();
            Require(dependencies is JObject values && values.Properties().All(item => item.Value.Type == JTokenType.String), "源码包依赖声明格式未知。");
            var relevant = new JObject { ["name"] = package, ["version"] = version, ["dependencies"] = dependencies.DeepClone() };
            return GenerateActionCommon.ComputeTextHash(CanonicalJson(relevant).ToString(Formatting.None));
        }

        /// <summary>对象键按 Ordinal 排序，数组保留原顺序，使无语义的清单格式差异不破坏跨项目来源身份。</summary>
        private static JToken CanonicalJson(JToken value)
        {
            if (value is JObject obj) return new JObject(obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal)
                .Select(item => new JProperty(item.Name, CanonicalJson(item.Value))));
            if (value is JArray array) return new JArray(array.Select(CanonicalJson));
            return value.DeepClone();
        }

        /// <summary>冻结当前编译条件、所有引用映像和编译选项文件；这些项目差异不被写回跨工程审核 reference。</summary>
        internal static string CaptureCompilationIdentity(UnityEditor.Compilation.Assembly owner, CancellationToken token)
        {
            Require(owner.defines != null && owner.allReferences != null && owner.allReferences.Length <= 1024 && owner.compilerOptions != null,
                "源码程序集缺完整编译条件或引用映射。");
            var options = owner.compilerOptions;
            var files = new List<string>(owner.allReferences);
            files.AddRange(options.ResponseFiles ?? Array.Empty<string>());
            files.AddRange(options.RoslynAnalyzerDllPaths ?? Array.Empty<string>());
            files.AddRange(options.RoslynAdditionalFilePaths ?? Array.Empty<string>());
            if (!string.IsNullOrEmpty(options.RoslynAnalyzerRulesetPath)) files.Add(options.RoslynAnalyzerRulesetPath);
            if (!string.IsNullOrEmpty(options.AnalyzerConfigPath)) files.Add(options.AnalyzerConfigPath);
            Require(files.Count <= 2048, "编译选项及引用文件数量超出合同容量。");
            string[] identities = files.Select(value => PhysicalCompilerPath(value)).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).Select(path => GenerateActionCommon.ComputeTextHash(path) + "|" + HashFile(path, token)).ToArray();
            // 6000.4 的 EmitReferenceAssembly getter 会抛 NotImplementedException；只读已核字段，不对编译选项执行通用 getter 序列化。
            FieldInfo deterministic = typeof(ScriptCompilerOptions).GetField("<UseDeterministicCompilation>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(options.GetType() == typeof(ScriptCompilerOptions) && deterministic?.FieldType == typeof(bool), "编译选项的实际字段身份已变化。");
            return GenerateActionCommon.ComputeTextHash(JsonConvert.SerializeObject(new
            {
                owner.flags, defines = owner.defines, references = owner.allReferences, files = identities,
                options.AllowUnsafeCode, options.CodeOptimization, options.ApiCompatibilityLevel, options.EditorAssembliesCompatibilityLevel,
                options.LanguageVersion, options.AdditionalCompilerArguments, options.ResponseFiles, options.RoslynAnalyzerDllPaths,
                options.RoslynAdditionalFilePaths, options.RoslynAnalyzerRulesetPath, options.AnalyzerConfigPath,
                deterministic = (bool)deterministic.GetValue(options)
            }));
        }

        /// <summary>对比已加载模块和磁盘映像；本次双读摘要不使用来源遍历缓存，替换或读取中漂移都拒绝。</summary>
        internal static bool TryVerifyLoadedImage(string path, Guid loadedMvid, CancellationToken token, out string hash, out string error)
        {
            hash = null;
            error = null;
            try
            {
                RequireOrdinaryFile(path);
                string first = GenerateActionCommon.ComputeFileHash(path, token);
                Require(loadedMvid != Guid.Empty && ReadDiskMvid(path) == loadedMvid && first == GenerateActionCommon.ComputeFileHash(path, token),
                    "源码程序集的磁盘与实际加载 MVID/DLL 不一致。");
                hash = first; return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            { error = exception is InvalidOperationException ? exception.Message : "编译映像读取失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>仅将当前包的虚拟源码映射到它的真实根；其他包或工程源码会在完整输入校验中拒绝。</summary>
        private static string PhysicalSourcePath(string value, PackageInfo package)
        {
            Require(!string.IsNullOrEmpty(value), "编译来源路径为空。");
            string prefix = package.assetPath.TrimEnd('/') + "/";
            return value.StartsWith(prefix, StringComparison.Ordinal) ? OwnedFile(package.resolvedPath, value.Substring(prefix.Length)) :
                Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(GenerateActionCommon.ProjectRoot, value));
        }

        /// <summary>解析实际编译输出或引用文件，只读映射 Unity 已登记包，不猜 PackageCache 目录。</summary>
        private static string PhysicalCompilerPath(string value)
        {
            Require(!string.IsNullOrEmpty(value), "编译输出或引用路径为空。");
            if (value.StartsWith("Packages/", StringComparison.Ordinal))
            {
                PackageInfo package = PackageInfo.FindForAssetPath(value);
                Require(package != null, "编译引用缺实际包归属。");
                return PhysicalSourcePath(value, package);
            }
            return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(GenerateActionCommon.ProjectRoot, value));
        }

        /// <summary>判断合同内路径语法，拒绝绝对路径、空段、目录导航及反斜杠。</summary>
        private static bool ValidRelative(string relative) => !string.IsNullOrEmpty(relative) && !Path.IsPathRooted(relative) &&
            relative.IndexOf('\\') < 0 && !relative.Split('/').Any(part => part.Length == 0 || part == "." || part == "..");

        /// <summary>固定包根内的普通文件，防止链接和相对路径把审核来源指向另一个所有者。</summary>
        private static string OwnedFile(string root, string relative)
        {
            Require(ValidRelative(relative), "源码合同包含越界相对路径。");
            string path = Path.GetFullPath(Path.Combine(root, relative));
            Relative(root, path); RequireOrdinaryFile(path); return path;
        }

        /// <summary>把实际编译源转成可移植包内位置；跨根源码即使同名也不能准入。</summary>
        private static string Relative(string root, string path)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            Require(full.StartsWith(prefix, PathComparison), "实际编译源位于已审包根之外，可能存在 asmref 注入。");
            return full.Substring(prefix.Length).Replace('\\', '/');
        }

        /// <summary>先保持源码专属的普通文件检查，再复用当前同步阶段的文件摘要。</summary>
        private static string HashFile(string path, CancellationToken token)
        {
            RequireOrdinaryFile(path);
            return ScopedFileHash(path, token);
        }

        /// <summary>在同步只读作用域内去重文件摘要；缓存命中仍检查取消，文件准入规则由调用方负责。</summary>
        /// <param name="path">调用方已按自身来源规则核对的实际文件路径。</param>
        /// <param name="token">本次读取取消令牌，缓存命中也不能忽略。</param>
        /// <returns>当前同步阶段首次读取的 SHA-256；没有作用域时直接重新读取。</returns>
        internal static string ScopedFileHash(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (s_ReadScope != null && s_ReadScope.Hashes.TryGetValue(path, out string cached)) return cached;
            string hash = GenerateActionCommon.ComputeFileHash(path, token);
            if (s_ReadScope != null) s_ReadScope.Hashes[path] = hash;
            return hash;
        }

        /// <summary>拒绝缺失文件和父链上重解析点，不跟随链接采纳其它来源。</summary>
        private static void RequireOrdinaryFile(string path)
        {
            Require(!string.IsNullOrEmpty(path) && File.Exists(path), "编译身份文件缺失。");
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "编译来源包含链接或重解析点。");
        }

        /// <summary>复用既有桥接合同的只读 PE/CLI Module 解析，读取磁盘 MVID，不加载或执行映像。</summary>
        internal static Guid ReadDiskMvid(string path)
        {
            RequireOrdinaryFile(path);
            long length = new FileInfo(path).Length;
            Require(length >= 256 && length <= 64 * 1024 * 1024, "编译程序集映像容量无效。");
            byte[] bytes = File.ReadAllBytes(path);
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                Require(reader.ReadUInt16() == 0x5a4d, "编译程序集不是 PE 映像。");
                stream.Position = 0x3c; int pe = reader.ReadInt32();
                Require(pe >= 64 && pe <= bytes.Length - 24, "PE 头越界。");
                stream.Position = pe; Require(reader.ReadUInt32() == 0x00004550, "PE 签名无效。");
                reader.ReadUInt16(); int count = reader.ReadUInt16();
                stream.Position = pe + 20; int optionalSize = reader.ReadUInt16(), optional = pe + 24;
                stream.Position = optional; int magic = reader.ReadUInt16();
                Require((magic == 0x10b || magic == 0x20b) && count > 0 && count <= 96, "PE 可选头无效。");
                int directory = optional + (magic == 0x10b ? 96 : 112);
                Require(directory + 15 * 8 <= optional + optionalSize && optional + optionalSize + count * 40 <= bytes.Length, "PE 目录越界。");
                stream.Position = directory + 14 * 8; uint cli = reader.ReadUInt32(); int sections = optional + optionalSize;
                int cliOffset = RvaOffset(reader, cli, sections, count, bytes.Length);
                stream.Position = cliOffset + 8; uint metadataRva = reader.ReadUInt32(), metadataSize = reader.ReadUInt32();
                int metadata = RvaOffset(reader, metadataRva, sections, count, bytes.Length);
                Require(metadataSize >= 24 && metadataSize <= bytes.Length - metadata, "CLI 元数据越界。");
                stream.Position = metadata; Require(reader.ReadUInt32() == 0x424a5342, "CLI 元数据签名无效。");
                stream.Position = metadata + 12; uint versionLength = reader.ReadUInt32();
                Require(versionLength <= 1024, "CLI 版本区越界。");
                stream.Position = (metadata + 16L + versionLength + 3) & ~3L;
                reader.ReadUInt16(); int streams = reader.ReadUInt16();
                Require(streams > 0 && streams <= 64, "CLI 流数量越界。");
                int tables = -1, guid = -1, guidSize = 0;
                for (int i = 0; i < streams; i++)
                {
                    uint offset = reader.ReadUInt32(), size = reader.ReadUInt32();
                    Require(offset <= metadataSize && size <= metadataSize - offset, "CLI 流越界。");
                    var name = new List<char>(); byte value;
                    while ((value = reader.ReadByte()) != 0) { Require(name.Count < 32, "CLI 流名称过长。"); name.Add((char)value); }
                    stream.Position = (stream.Position + 3) & ~3L; string role = new string(name.ToArray());
                    if (role == "#~" || role == "#-") { Require(tables == -1, "CLI 表流重复。"); tables = checked(metadata + (int)offset); }
                    if (role == "#GUID") { Require(guid == -1, "CLI GUID 流重复。"); guid = checked(metadata + (int)offset); guidSize = checked((int)size); }
                }
                Require(tables >= 0 && guid >= 0, "CLI 缺模块表或 GUID 流。");
                stream.Position = tables + 6; byte heaps = reader.ReadByte(); reader.ReadByte();
                ulong valid = reader.ReadUInt64(); reader.ReadUInt64();
                Require((valid & 1) == 1 && reader.ReadUInt32() == 1, "CLI Module 表不是唯一行。");
                for (int bit = 1; bit < 64; bit++) if ((valid & (1UL << bit)) != 0) reader.ReadUInt32();
                reader.ReadUInt16(); if ((heaps & 1) != 0) reader.ReadUInt32(); else reader.ReadUInt16();
                uint index = (heaps & 2) != 0 ? reader.ReadUInt32() : reader.ReadUInt16();
                Require(index > 0 && index <= guidSize / 16, "CLI MVID 索引无效。");
                stream.Position = guid + (index - 1L) * 16; byte[] valueBytes = reader.ReadBytes(16);
                Require(valueBytes.Length == 16, "CLI MVID 内容缺失。"); return new Guid(valueBytes);
            }
        }

        /// <summary>只在 PE section 真实原始字节内映射 RVA，拒绝虚拟零区或越界偏移。</summary>
        private static int RvaOffset(BinaryReader reader, uint rva, int sections, int count, int length)
        {
            for (int i = 0; i < count; i++)
            {
                reader.BaseStream.Position = sections + i * 40 + 12;
                uint address = reader.ReadUInt32(), size = reader.ReadUInt32(), pointer = reader.ReadUInt32();
                if (rva < address || (ulong)rva - address >= size) continue;
                long offset = pointer + (long)rva - address;
                Require(offset >= 0 && offset <= length - 16, "PE RVA 超出真实文件。"); return checked((int)offset);
            }
            throw new InvalidOperationException("编译程序集 PE RVA 没有实际 section。");
        }

        /// <summary>准入失败只报告合同边界，不通过补写来源或重编译来放行。</summary>
        private static void Require(bool condition, string error)
        { if (!condition) throw new InvalidOperationException(error ?? "Android 刷新源码身份不完整。"); }
    }
}
