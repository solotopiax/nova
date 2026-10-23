/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IapProductCatalogAdapter.cs
 * author:    Codex
 * created:   2026/10/22
 * descrip:   IAP 商品目录固定可选包桥接与实际编译来源冻结
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Compilation;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using Assembly = System.Reflection.Assembly;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>仅绑定准确已安装 IAP 包的固定桥；不加载程序集、不接受外部类型名或任意调用参数。</summary>
    internal sealed class IapProductCatalogAdapter
    {
        /// <summary>唯一允许的可选包身份。</summary>
        private const string PackageName = "com.solotopia.nova.framework.sdk.iap";
        /// <summary>唯一允许的可选 Editor 程序集。</summary>
        private const string AssemblyName = "NovaFramework.SDK.IAP.Editor";
        /// <summary>唯一允许的公开静态桥。</summary>
        private const string BridgeName = "NovaFramework.SDK.IAP.Editor.IAPProductCatalogActionBridge";
        /// <summary>沿用项目路径策略：Windows 忽略大小写，其余平台按准确大小写比较。</summary>
        private static StringComparison PathComparison => Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        /// <summary>固定桥自己创建的驻内存准备类型，不从请求反序列化。</summary>
        private readonly Type m_PreparedType;
        /// <summary>已通过准确签名和来源核验的准备方法。</summary>
        private readonly MethodInfo m_Prepare;
        /// <summary>已通过准确签名和来源核验的脱敏描述方法。</summary>
        private readonly MethodInfo m_Describe;
        /// <summary>已通过准确签名和来源核验的单次提交方法。</summary>
        private readonly MethodInfo m_Submit;

        /// <summary>固定顺序序列化的真实包、源码与已加载映像摘要；不包含配置、凭据或请求正文。</summary>
        internal string IdentityJson { get; }

        /// <summary>保存固定调用与当前身份；只由完整核验后的创建入口构造。</summary>
        private IapProductCatalogAdapter(Type preparedType, MethodInfo prepare, MethodInfo describe, MethodInfo submit, string identity)
        {
            m_PreparedType = preparedType;
            m_Prepare = prepare;
            m_Describe = describe;
            m_Submit = submit;
            IdentityJson = identity;
        }

        /// <summary>只读核定唯一已安装包、固定桥、正常编译所有者和真实映像；缺包与已装损坏分别返回。</summary>
        /// <param name="adapter">全部身份和签名通过时返回适配器。</param>
        /// <param name="packageMissing">仅准确包未登记且无残留程序集或桥时为 true。</param>
        /// <param name="error">固定中性原因，不返回底层异常信息或物理路径。</param>
        /// <returns>可以绑定本次固定可选包桥时为 true。</returns>
        internal static bool TryCreate(out IapProductCatalogAdapter adapter, out bool packageMissing, out string error)
        {
            adapter = null;
            packageMissing = false;
            error = null;
            try
            {
                Require(!EditorApplication.isCompiling && !EditorApplication.isUpdating);
                Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
                Assembly[] assemblies = loaded.Where(item => item.GetName().Name == AssemblyName).ToArray();
                Type[] bridges = loaded.Select(item => item.GetType(BridgeName, false, false)).Where(item => item != null).Distinct().ToArray();
                PackageInfo[] packages = PackageInfo.GetAllRegisteredPackages().Where(item => item.name == PackageName).ToArray();
                if (packages.Length == 0 && assemblies.Length == 0 && bridges.Length == 0)
                {
                    packageMissing = true;
                    error = "当前项目未安装 IAP 商品目录包。";
                    return false;
                }
                Require(packages.Length == 1 && assemblies.Length == 1 && bridges.Length == 1);
                Assembly assembly = assemblies[0];
                Type bridge = bridges[0];
                Require(!assembly.IsDynamic && bridge.Assembly == assembly && bridge.Module == assembly.ManifestModule &&
                    bridge.IsPublic && bridge.IsAbstract && bridge.IsSealed && !bridge.ContainsGenericParameters && bridge.DeclaringType == null);
                Type prepared = bridge.GetNestedType("PreparedUpload", BindingFlags.NonPublic);
                Require(prepared != null && prepared.IsNestedPrivate && prepared.IsClass && prepared.IsSealed && !prepared.ContainsGenericParameters);
                MethodInfo prepare = Bind(bridge, "Prepare", typeof(object), typeof(ConfigRuntimeSO));
                MethodInfo describe = Bind(bridge, "Describe", typeof(string), typeof(object));
                MethodInfo submit = Bind(bridge, "SubmitAsync", typeof(Task<string>), typeof(object), typeof(CancellationToken));
                PackageInfo package = packages[0];
                string packageRoot = RequirePackage(package, PackageName);
                PackageInfo owner = PackageInfo.FindForAssembly(assembly);
                Require(owner != null && SamePackage(owner, package));
                var iapSources = new[]
                {
                    "Nova/Scripts/Editor/ProductCatalog/IAPProductCatalogActionBridge.cs",
                    "Nova/Scripts/Editor/ProductCatalog/IAPProductCatalogProtocol.cs",
                    "Nova/Scripts/Editor/ProductCatalog/IAPProductCatalogCanonicalizer.cs",
                    "Nova/Scripts/Editor/ProductCatalog/IAPProductCatalogDtos.cs",
                    "Nova/Scripts/Editor/ProductCatalog/IAPProductCatalogUploader.cs",
                };
                JObject iapIdentity = CaptureCompiled(assembly, packageRoot, package.assetPath,
                    "Nova/Scripts/Editor/NovaFramework.SDK.IAP.Editor.asmdef", iapSources, true);
                Assembly framework = typeof(EditorUtil.Network.Http).Assembly;
                Require(framework == typeof(IapProductCatalogAdapter).Assembly && framework.GetName().Name == "NovaFramework.Editor");
                PackageInfo frameworkPackage = PackageInfo.FindForAssembly(framework);
                string frameworkRoot = frameworkPackage == null
                    ? Path.Combine(GenerateActionCommon.ProjectRoot, "Assets/Framework")
                    : RequirePackage(frameworkPackage, "com.solotopia.nova.framework");
                string frameworkAssetRoot = frameworkPackage?.assetPath ?? "Assets/Framework";
                RequireManifest(frameworkRoot, "com.solotopia.nova.framework", frameworkPackage?.version);
                JObject frameworkIdentity = CaptureCompiled(framework, frameworkRoot, frameworkAssetRoot,
                    "Scripts/Editor/NovaFramework.Editor.asmdef", new[]
                    {
                        "Scripts/Editor/EditorUtil/EditorUtil.Network/EditorUtil.Network.Http.cs",
                        "Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Delivery/IapProductCatalogAdapter.cs",
                    }, false);
                var identity = new JObject
                {
                    ["schemaVersion"] = 1,
                    ["bridge"] = BridgeName,
                    ["preparedType"] = prepared.FullName,
                    ["package"] = PackageIdentity(package, packageRoot),
                    ["iapCompilation"] = iapIdentity,
                    ["frameworkPackage"] = frameworkPackage == null
                        ? new JObject { ["source"] = "development-assets", ["rootSha256"] = HashPath(frameworkRoot), ["manifestSha256"] = HashFile(OwnedFile(frameworkRoot, "package.json")) }
                        : PackageIdentity(frameworkPackage, frameworkRoot),
                    ["frameworkCompilation"] = frameworkIdentity,
                    ["methodTokens"] = new JArray(prepare.MetadataToken, describe.MetadataToken, submit.MetadataToken),
                };
                string json = identity.ToString(Formatting.None);
                Require(json.Length <= 65536);
                adapter = new IapProductCatalogAdapter(prepared, prepare, describe, submit, json);
                return true;
            }
            catch
            {
                error = "IAP 商品目录桥未通过包、固定签名、源码或编译映像身份核验。";
                return false;
            }
        }

        /// <summary>由固定桥从显式导出物准备一次请求；不选择其他 Runtime、不发送或保存。</summary>
        /// <param name="runtime">主线已冻结的准确 ConfigRuntimeSO。</param>
        /// <returns>同一固定桥创建的驻内存对象。</returns>
        internal object Prepare(ConfigRuntimeSO runtime)
        {
            if (runtime == null) throw new InvalidOperationException("IAP 商品目录准备缺少导出配置。");
            object result = Invoke(m_Prepare, new object[] { runtime });
            RequirePrepared(result);
            return result;
        }

        /// <summary>读取固定 Prepared 的脱敏描述；不接收外部反序列化对象或调用指令。</summary>
        /// <param name="prepared">同桥创建且准确类型相同的准备对象。</param>
        /// <returns>固定桥生成的脱敏结构化元数据。</returns>
        internal string Describe(object prepared)
        {
            RequirePrepared(prepared);
            string result = Invoke(m_Describe, new[] { prepared }) as string;
            if (string.IsNullOrEmpty(result)) throw new InvalidOperationException("IAP 商品目录桥没有返回描述。");
            return result;
        }

        /// <summary>等待固定桥单次提交的脱敏结果；保留正常同步上下文，不重读配置、不重试。</summary>
        /// <param name="prepared">原 Plan 保留的准确准备对象。</param>
        /// <param name="token">主线的本次取消令牌。</param>
        /// <returns>桥生成的脱敏结果 JSON；任何调用异常仅转换为中性异常。</returns>
        internal async Task<string> SubmitAsync(object prepared, CancellationToken token)
        {
            RequirePrepared(prepared);
            token.ThrowIfCancellationRequested();
            try
            {
                var task = Invoke(m_Submit, new object[] { prepared, token }) as Task<string>;
                if (task == null) throw new InvalidOperationException("IAP 商品目录桥没有返回提交任务。");
                string result = await task;
                if (string.IsNullOrEmpty(result)) throw new InvalidOperationException("IAP 商品目录桥没有返回结果。");
                return result;
            }
            catch (OperationCanceledException) { throw new OperationCanceledException(token); }
            catch { throw new InvalidOperationException("IAP 商品目录桥提交未取得有效结果。"); }
        }

        /// <summary>只允许固定桥的准确私有 Prepared 类型；不根据同名或可赋值类型放行。</summary>
        private void RequirePrepared(object prepared)
        {
            if (prepared == null || prepared.GetType() != m_PreparedType)
                throw new InvalidOperationException("IAP 商品目录准备对象与固定桥不匹配。");
        }

        /// <summary>调用已绑定的三个固定方法之一；异常原文与内部异常均不跨出适配器。</summary>
        private static object Invoke(MethodInfo method, object[] arguments)
        {
            try { return method.Invoke(null, arguments); }
            catch (TargetInvocationException exception) when (exception.InnerException is OperationCanceledException)
            { throw new OperationCanceledException(); }
            catch { throw new InvalidOperationException("IAP 商品目录桥调用失败。"); }
        }

        /// <summary>拒绝重载、泛型和变参，仅接纳本类型声明的准确公开静态签名。</summary>
        private static MethodInfo Bind(Type owner, string name, Type returns, params Type[] parameters)
        {
            MethodInfo[] methods = owner.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(item => item.Name == name).ToArray();
            Require(methods.Length == 1);
            MethodInfo method = methods[0];
            Require(!method.IsGenericMethod && method.ReturnType == returns &&
                (method.CallingConvention & CallingConventions.VarArgs) == 0 &&
                method.GetParameters().Select(item => item.ParameterType).SequenceEqual(parameters));
            return method;
        }

        /// <summary>核对登记包与准确 manifest，禁止把同名目录或残留缓存当已安装包。</summary>
        private static string RequirePackage(PackageInfo package, string expected)
        {
            Require(package != null && package.name == expected && package.assetPath == "Packages/" + expected &&
                !string.IsNullOrEmpty(package.resolvedPath) && !string.IsNullOrEmpty(package.version));
            string root = Path.GetFullPath(package.resolvedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            RequireManifest(root, expected, package.version);
            return root;
        }

        /// <summary>只比较真实登记身份及规范解析根；不公开可能含 URL 或本机路径的 packageId。</summary>
        private static bool SamePackage(PackageInfo left, PackageInfo right) => left.name == right.name && left.version == right.version &&
            left.packageId == right.packageId && left.source == right.source &&
            string.Equals(Path.GetFullPath(left.resolvedPath), Path.GetFullPath(right.resolvedPath), PathComparison);

        /// <summary>从准确包根读取 manifest 的 name/version；开发态也必须有真实 Framework 包描述。</summary>
        private static void RequireManifest(string root, string name, string version)
        {
            JObject manifest = JObject.Parse(File.ReadAllText(OwnedFile(root, "package.json")));
            Require(manifest["name"]?.Type == JTokenType.String && manifest["name"].Value<string>() == name &&
                manifest["version"]?.Type == JTokenType.String && !string.IsNullOrEmpty(manifest["version"].Value<string>()) &&
                (version == null || manifest["version"].Value<string>() == version));
        }

        /// <summary>只输出包名、版本、来源枚举及物理身份摘要，不带凭据、registry 地址或绝对路径。</summary>
        private static JObject PackageIdentity(PackageInfo package, string root) => new JObject
        {
            ["name"] = package.name, ["version"] = package.version, ["source"] = package.source.ToString(),
            ["assetPath"] = package.assetPath, ["packageIdSha256"] = GenerateActionCommon.ComputeTextHash(package.packageId),
            ["rootSha256"] = HashPath(root), ["manifestSha256"] = HashFile(OwnedFile(root, "package.json")),
        };

        /// <summary>绑定唯一 Unity Editor 编译输出、asmdef 与实际源码闭包；磁盘 MVID 必须等于已加载模块。</summary>
        private static JObject CaptureCompiled(Assembly assembly, string root, string assetRoot, string definition, string[] requiredSources, bool allSources)
        {
            string name = assembly.GetName().Name;
            Require(!assembly.IsDynamic && AppDomain.CurrentDomain.GetAssemblies().Count(item => item.GetName().Name == name) == 1);
            var owners = CompilationPipeline.GetAssemblies(AssembliesType.Editor).Where(item => item.name == name).ToArray();
            Require(owners.Length == 1 && owners[0].sourceFiles != null && !string.IsNullOrEmpty(owners[0].outputPath));
            string output = Physical(owners[0].outputPath, root, assetRoot);
            Require(string.Equals(output, Path.GetFullPath(assembly.Location), PathComparison));
            string asmdef = OwnedFile(root, definition);
            Require(string.Equals(Physical(CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name), root, assetRoot), asmdef, PathComparison) &&
                JObject.Parse(File.ReadAllText(asmdef))["name"]?.Value<string>() == name);
            string[] compiled = owners[0].sourceFiles.Select(value => Physical(value, root, assetRoot)).Distinct(StringComparer.Ordinal).ToArray();
            foreach (string source in requiredSources) Require(compiled.Contains(OwnedFile(root, source), StringComparer.Ordinal));
            string[] selected = allSources ? compiled : requiredSources.Select(source => OwnedFile(root, source)).ToArray();
            Require(selected.Length > 0 && selected.Length <= 256);
            var files = new List<JObject>();
            foreach (string source in selected.OrderBy(value => value, StringComparer.Ordinal))
            {
                string relative = Relative(root, source);
                Require(relative.EndsWith(".cs", StringComparison.Ordinal) &&
                    (!allSources || relative.StartsWith("Nova/Scripts/Editor/", StringComparison.Ordinal)));
                files.Add(FileIdentity(root, relative));
            }
            string firstHash = HashFile(output);
            Guid diskMvid = ReadDiskMvid(output);
            Require(diskMvid == assembly.ManifestModule.ModuleVersionId && firstHash == HashFile(output));
            return new JObject
            {
                ["assemblyFullName"] = assembly.FullName, ["moduleName"] = assembly.ManifestModule.Name,
                ["mvid"] = diskMvid.ToString("D"), ["dllSha256"] = firstHash, ["dllPathSha256"] = HashPath(output),
                ["asmdef"] = FileIdentity(root, definition), ["sources"] = new JArray(files),
            };
        }

        /// <summary>把 Unity 的包虚拟路径映射到真实解析根；其余路径复用项目根解析，不跟随链接。</summary>
        private static string Physical(string path, string root, string assetRoot)
        {
            Require(!string.IsNullOrEmpty(path));
            string prefix = assetRoot.TrimEnd('/') + "/";
            if (path.StartsWith(prefix, StringComparison.Ordinal)) return OwnedFile(root, path.Substring(prefix.Length));
            if (Path.IsPathRooted(path) && Path.GetFullPath(path).StartsWith(
                    Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, PathComparison))
                return OwnedFile(root, Relative(root, path));
            Require(GenerateActionCommon.TryResolveProjectPath(path, "编译来源", out string full, out _));
            Require(File.Exists(full));
            return full;
        }

        /// <summary>核对包根内的普通既存文件和全部父链，避免相对导航或链接逃逸真实所有者。</summary>
        private static string OwnedFile(string root, string relative)
        {
            Require(!string.IsNullOrEmpty(relative) && !Path.IsPathRooted(relative) &&
                !relative.Split('/', '\\').Any(part => part.Length == 0 || part == "." || part == ".."));
            string full = Path.GetFullPath(Path.Combine(root, relative));
            Relative(root, full);
            RequireOrdinaryFile(full);
            return full;
        }

        /// <summary>返回固定根下相对角色；路径摘要之外不公开本机绝对位置。</summary>
        private static string Relative(string root, string path)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            Require(full.StartsWith(prefix, PathComparison));
            return full.Substring(prefix.Length).Replace('\\', '/');
        }

        /// <summary>固定源码/asmdef 与其真实 meta 同时冻结，不创建或修复缺失的 Unity 身份文件。</summary>
        private static JObject FileIdentity(string root, string relative) => new JObject
        {
            ["path"] = relative, ["sha256"] = HashFile(OwnedFile(root, relative)),
            ["metaSha256"] = HashFile(OwnedFile(root, relative + ".meta")),
        };

        /// <summary>复用现有流式 SHA 算法；文件与祖先必须均为普通路径。</summary>
        private static string HashFile(string path)
        {
            RequireOrdinaryFile(path);
            return GenerateActionCommon.ComputeFileHash(path, CancellationToken.None);
        }

        /// <summary>只冻结规范物理路径摘要，避免把本机用户名或外部包目录写入回执。</summary>
        private static string HashPath(string path) => GenerateActionCommon.ComputeTextHash(Path.GetFullPath(path).Replace('\\', '/'));

        /// <summary>拒绝目录替代、文件缺失和任一级重解析点，不读取设置或触发包解析。</summary>
        private static void RequireOrdinaryFile(string path)
        {
            Require(!string.IsNullOrEmpty(path) && File.Exists(path));
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0);
        }

        /// <summary>只读 PE/CLI Module 表的 MVID，不加载或执行磁盘程序集，也不引入新的元数据依赖。</summary>
        private static Guid ReadDiskMvid(string path)
        {
            RequireOrdinaryFile(path);
            long length = new FileInfo(path).Length;
            Require(length >= 256 && length <= 64 * 1024 * 1024);
            byte[] bytes = File.ReadAllBytes(path);
            Require(bytes.Length >= 256 && bytes.Length <= 64 * 1024 * 1024);
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                Require(reader.ReadUInt16() == 0x5a4d);
                stream.Position = 0x3c;
                int pe = reader.ReadInt32();
                Require(pe >= 64 && pe <= bytes.Length - 24);
                stream.Position = pe;
                Require(reader.ReadUInt32() == 0x00004550);
                reader.ReadUInt16();
                int count = reader.ReadUInt16();
                stream.Position = pe + 20;
                int optionalSize = reader.ReadUInt16();
                int optional = pe + 24;
                stream.Position = optional;
                int magic = reader.ReadUInt16();
                Require((magic == 0x10b || magic == 0x20b) && count > 0 && count <= 96);
                int directory = optional + (magic == 0x10b ? 96 : 112);
                Require(directory + 15 * 8 <= optional + optionalSize && optional + optionalSize + count * 40 <= bytes.Length);
                stream.Position = directory + 14 * 8;
                uint cli = reader.ReadUInt32();
                int sections = optional + optionalSize;
                int cliOffset = RvaOffset(reader, cli, sections, count, bytes.Length);
                stream.Position = cliOffset + 8;
                uint metadataRva = reader.ReadUInt32();
                uint metadataSize = reader.ReadUInt32();
                int metadata = RvaOffset(reader, metadataRva, sections, count, bytes.Length);
                Require(metadataSize >= 24 && metadataSize <= bytes.Length - metadata);
                stream.Position = metadata;
                Require(reader.ReadUInt32() == 0x424a5342);
                stream.Position = metadata + 12;
                uint versionLength = reader.ReadUInt32();
                Require(versionLength <= 1024);
                stream.Position = (metadata + 16L + versionLength + 3) & ~3L;
                reader.ReadUInt16();
                int streams = reader.ReadUInt16();
                Require(streams > 0 && streams <= 64);
                int tables = -1, guid = -1, guidSize = 0;
                for (int i = 0; i < streams; i++)
                {
                    uint offset = reader.ReadUInt32(), size = reader.ReadUInt32();
                    Require(offset <= metadataSize && size <= metadataSize - offset);
                    var name = new List<char>();
                    byte value;
                    while ((value = reader.ReadByte()) != 0) { Require(name.Count < 32); name.Add((char)value); }
                    stream.Position = (stream.Position + 3) & ~3L;
                    string role = new string(name.ToArray());
                    if (role == "#~" || role == "#-") { Require(tables == -1); tables = checked(metadata + (int)offset); }
                    if (role == "#GUID") { Require(guid == -1); guid = checked(metadata + (int)offset); guidSize = checked((int)size); }
                }
                Require(tables >= 0 && guid >= 0);
                stream.Position = tables + 6;
                byte heaps = reader.ReadByte();
                reader.ReadByte();
                ulong valid = reader.ReadUInt64();
                reader.ReadUInt64();
                Require((valid & 1) == 1 && reader.ReadUInt32() == 1);
                for (int bit = 1; bit < 64; bit++) if ((valid & (1UL << bit)) != 0) reader.ReadUInt32();
                reader.ReadUInt16();
                if ((heaps & 1) != 0) reader.ReadUInt32(); else reader.ReadUInt16();
                uint index = (heaps & 2) != 0 ? reader.ReadUInt32() : reader.ReadUInt16();
                Require(index > 0 && index <= guidSize / 16);
                stream.Position = guid + (index - 1L) * 16;
                byte[] valueBytes = reader.ReadBytes(16);
                Require(valueBytes.Length == 16);
                return new Guid(valueBytes);
            }
        }

        /// <summary>仅在 PE 的真实 section 原始字节内映射 RVA，拒绝虚拟零区或越界偏移。</summary>
        private static int RvaOffset(BinaryReader reader, uint rva, int sections, int count, int length)
        {
            for (int i = 0; i < count; i++)
            {
                reader.BaseStream.Position = sections + i * 40 + 12;
                uint address = reader.ReadUInt32(), size = reader.ReadUInt32(), pointer = reader.ReadUInt32();
                if (rva < address || (ulong)rva - address >= size) continue;
                long offset = pointer + (long)rva - address;
                Require(offset >= 0 && offset <= length - 16);
                return checked((int)offset);
            }
            throw new InvalidOperationException("IAP 商品目录桥程序集映像不完整。");
        }

        /// <summary>内部身份断言统一失败，不把文件内容、路径或底层异常正文公开给调用方。</summary>
        private static void Require(bool condition)
        {
            if (!condition) throw new InvalidOperationException("IAP 商品目录桥身份不完整。");
        }
    }
}
