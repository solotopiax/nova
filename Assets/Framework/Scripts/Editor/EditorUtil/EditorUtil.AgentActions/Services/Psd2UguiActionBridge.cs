/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Psd2UguiActionBridge.cs
 * author:    Codex
 * created:   2026/11/10
 * descrip:   PSD 转 UI 可选包固定协议与预编译程序集身份核验
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using Assembly = System.Reflection.Assembly;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>只调用准确安装包的四个固定方法；不加载程序集或接纳用户指定的类型、方法和代码。</summary>
    internal sealed class Psd2UguiActionBridge
    {
        internal const string PackageName = "cn.efunstudio.psd2ugui";
        private const string AssemblyName = "cn.efunstudio.psd2ugui";
        private const string BridgeName = "UGF.EditorTools.Psd2UGUI.NovaPsdAutomation";
        private const string DllRelativePath = "Core/PSD2UIForm/Libs/cn.efunstudio.psd2ugui.dll";
        private const int MaxResponseBytes = 8 * 1024 * 1024;
        private static readonly string[] s_ResponseFields =
        {
            "protocolVersion", "success", "state", "message", "planJson", "receiptJson", "dataJson",
            "outputPaths", "issues", "writeSet", "evidence", "requiresExternalProcess",
        };
        private static readonly string[] s_ReceiptFields = { "schemaVersion", "actionId", "identityJson", "operation", "requestJson", "receiptJson" };
        private static StringComparison PathComparison => Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        private readonly MethodInfo m_Inspect;
        private readonly MethodInfo m_Plan;
        private readonly MethodInfo m_Execute;
        private readonly MethodInfo m_Verify;
        internal string IdentityJson { get; }

        /// <summary>保留已核验的固定方法及完整身份摘要，禁止从请求创建反射调用。</summary>
        private Psd2UguiActionBridge(string identity, MethodInfo inspect, MethodInfo plan, MethodInfo execute, MethodInfo verify)
        {
            IdentityJson = identity;
            m_Inspect = inspect;
            m_Plan = plan;
            m_Execute = execute;
            m_Verify = verify;
        }

        /// <summary>协议返回保留内包不透明计划和回执，节点数据不截断；成功仍须按阶段和证据判定。</summary>
        internal sealed class Response
        {
            internal bool Success;
            internal string State;
            internal string Message;
            internal string PlanJson;
            internal string ReceiptJson;
            internal string DataJson;
            internal string[] OutputPaths;
            internal string[] Issues;
            internal string[] WriteSet;
            internal string[] Evidence;
            internal bool RequiresExternalProcess;
        }

        /// <summary>恢复信封只保存身份和内包回执；不保存可重放 Execute 的计划。</summary>
        internal sealed class Receipt
        {
            public int schemaVersion = 1;
            public string actionId;
            public string identityJson;
            public string operation;
            public string requestJson;
            public string receiptJson;
        }

        /// <summary>只读核对唯一登记包、固定桥签名、真实归属和 DLL 的 PE/MVID/哈希；缺包不影响 Registry。</summary>
        internal static bool TryCreate(out Psd2UguiActionBridge bridge, out bool missing, out string error)
        {
            bridge = null;
            missing = false;
            error = null;
            try
            {
                Require(!EditorApplication.isCompiling && !EditorApplication.isUpdating);
                Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
                Assembly[] assemblies = loaded.Where(item => item.GetName().Name == AssemblyName).ToArray();
                Type[] types = loaded.Select(item => item.GetType(BridgeName, false, false)).Where(item => item != null).Distinct().ToArray();
                PackageInfo[] packages = PackageInfo.GetAllRegisteredPackages().Where(item => item.name == PackageName).ToArray();
                if (packages.Length == 0 && assemblies.Length == 0 && types.Length == 0)
                {
                    missing = true;
                    error = "当前项目未安装 PSD2UGUI（cn.efunstudio.psd2ugui）；请从 PlugPals 内部云仓库安装已授权版本。";
                    return false;
                }
                Require(packages.Length == 1 && assemblies.Length == 1 && types.Length == 1);
                PackageInfo package = packages[0];
                Assembly assembly = assemblies[0];
                Type type = types[0];
                Require(!assembly.IsDynamic && type.Assembly == assembly && type.Module == assembly.ManifestModule &&
                    type.IsPublic && type.IsAbstract && type.IsSealed && !type.ContainsGenericParameters && type.DeclaringType == null);
                Require(package.assetPath == "Packages/" + PackageName && !string.IsNullOrEmpty(package.version) && !string.IsNullOrEmpty(package.resolvedPath));
                PackageInfo owner = PackageInfo.FindForAssembly(assembly);
                Require(owner != null && owner.name == package.name && owner.version == package.version && owner.source == package.source &&
                    owner.packageId == package.packageId && string.Equals(Path.GetFullPath(owner.resolvedPath), Path.GetFullPath(package.resolvedPath), PathComparison));
                string root = Path.GetFullPath(package.resolvedPath);
                string manifestPath = Path.Combine(root, "package.json");
                RequireOrdinaryFile(manifestPath);
                JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
                Require((string)manifest["name"] == PackageName && (string)manifest["version"] == package.version);
                string packageDll = Path.GetFullPath(Path.Combine(root, DllRelativePath));
                string loadedDll = Path.GetFullPath(assembly.Location);
                RequireOrdinaryFile(packageDll);
                RequireOrdinaryFile(loadedDll);
                string hash = HashFile(packageDll);
                Guid mvid = ReadDiskMvid(packageDll);
                Require(mvid == assembly.ManifestModule.ModuleVersionId && ReadDiskMvid(loadedDll) == mvid &&
                    HashFile(loadedDll) == hash && HashFile(packageDll) == hash);
                FieldInfo protocol = type.GetField("ProtocolVersion", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                Require(protocol != null && protocol.IsLiteral && protocol.FieldType == typeof(int) && (int)protocol.GetRawConstantValue() == 1);
                MethodInfo inspect = Bind(type, "Inspect"), plan = Bind(type, "Plan"), execute = Bind(type, "Execute"), verify = Bind(type, "Verify");
                string identity = new JObject
                {
                    ["protocolVersion"] = 1, ["packageName"] = package.name, ["packageVersion"] = package.version,
                    ["packageSource"] = package.source.ToString(), ["packageIdSha256"] = GenerateActionCommon.ComputeTextHash(package.packageId),
                    ["packageRootSha256"] = GenerateActionCommon.ComputeTextHash(root), ["manifestSha256"] = HashFile(manifestPath),
                    ["dllRelativePath"] = DllRelativePath, ["dllSha256"] = hash, ["mvid"] = mvid.ToString("D"),
                    ["assemblyFullName"] = assembly.FullName, ["bridge"] = BridgeName,
                    ["methodTokens"] = new JArray(inspect.MetadataToken, plan.MetadataToken, execute.MetadataToken, verify.MetadataToken),
                }.ToString(Formatting.None);
                bridge = new Psd2UguiActionBridge(identity, inspect, plan, execute, verify);
                return true;
            }
            catch
            {
                error = "PSD2UGUI 已安装，但自动化桥未通过协议、包归属或 DLL 身份核验；请确认已升级到支持该功能的版本，并等待编译完成。";
                return false;
            }
        }

        /// <summary>读取固定 Inspect；空参数只用于发现既有 PSD、配置及节点，不创建资产。</summary>
        internal Response Inspect(string requestJson) => Invoke(m_Inspect, requestJson);

        /// <summary>只读冻结内包计划；计划字符串由内包维护，Framework 不拼接其内部结构。</summary>
        internal Response Plan(string requestJson) => Invoke(m_Plan, requestJson);

        /// <summary>转交原冻结计划一次；调用方须先消费 Core Plan 并重新核对本桥身份。</summary>
        internal Response Execute(string planJson) => Invoke(m_Execute, planJson);

        /// <summary>只读核验原内包回执；不会调用 Execute、轮询任务调度器或恢复外部进程。</summary>
        internal Response Verify(string receiptJson) => Invoke(m_Verify, receiptJson);

        /// <summary>序列化受控 DTO 时省略未提供的可选字段，保留显式 false 与零值。</summary>
        internal static string SerializeRequest(object request) => JsonConvert.SerializeObject(request, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore, TypeNameHandling = TypeNameHandling.None,
        });

        /// <summary>创建跨重载回执；运行请求不写入，只保留精确 operation 与内包恢复指针。</summary>
        internal static string CreateReceipt(string actionId, string identity, string operation, string innerReceipt, string inspectRequest = null)
        {
            return JsonConvert.SerializeObject(new Receipt
            {
                actionId = actionId, identityJson = identity, operation = operation, receiptJson = innerReceipt, requestJson = inspectRequest,
            });
        }

        /// <summary>严格读取原信封，避免跨 Action、协议版本或缺失身份的回执进入可选包。</summary>
        internal static Receipt ParseReceipt(string json, string actionId)
        {
            JObject value = ParseObject(json, 1024 * 1024);
            Require(!value.Properties().Any(item => !s_ReceiptFields.Contains(item.Name, StringComparer.Ordinal)));
            Require(value["schemaVersion"]?.Type == JTokenType.Integer && (int)value["schemaVersion"] == 1 &&
                ReadString(value, "actionId", true) == actionId && !string.IsNullOrWhiteSpace(ReadString(value, "identityJson", true)));
            foreach (string field in s_ReceiptFields.Skip(1)) ReadString(value, field, false);
            return value.ToObject<Receipt>();
        }

        /// <summary>返回可展示的内包结果，保留完整节点数据和进程声明，不泄露执行计划。</summary>
        internal static string PublicData(Response response) => new JObject
        {
            ["protocolVersion"] = 1, ["state"] = response.State, ["dataJson"] = response.DataJson,
            ["outputPaths"] = new JArray(response.OutputPaths), ["issues"] = new JArray(response.Issues),
            ["requiresExternalProcess"] = response.RequiresExternalProcess,
        }.ToString(Formatting.None);

        /// <summary>按 Core 允许的状态与实际证据转换；失效计划为 blocked，写入失败或运行中保留 partial 与原内包状态。</summary>
        internal static AgentActionResult BuildResult(string actionId, Response response, string receipt, bool inspect)
        {
            bool terminal = inspect ? response.State == "ready" || response.State == "succeeded" : response.State == "succeeded";
            bool evidence = response.Evidence.Length > 0 && (inspect || response.OutputPaths.Length > 0 && response.OutputPaths.All(OutputExists));
            string status = response.State == "stale" || response.State == "blocked" || inspect && response.State == "failed" ? "blocked" :
                terminal && response.Success && evidence ? "success" : "partial";
            AgentActionResult result = AgentActionResult.Create(actionId, status, response.Message);
            result.ReceiptJson = receipt;
            result.DataJson = PublicData(response);
            result.Artifacts.AddRange(response.OutputPaths);
            result.Evidence.AddRange(response.Evidence);
            result.Warnings.AddRange(response.Issues);
            if (status == "success") result.EvidenceKinds = inspect ? AgentActionEvidence.Static : AgentActionEvidence.Static | AgentActionEvidence.Artifact;
            if (terminal && !evidence) result.Warnings.Add("完成状态缺少可核验的证据或产物，仍需使用原回执核验。");
            return result;
        }

        /// <summary>仅验证项目内已存在的普通文件或目录；不加载资产，不创建缺失目录。</summary>
        private static bool OutputExists(string path)
        {
            if (!GenerateActionCommon.TryResolveProjectPath(path, "PSD 转 UI 产物", out string full, out _)) return false;
            if (!File.Exists(full) && !Directory.Exists(full)) return false;
            for (string current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
            return true;
        }

        /// <summary>只接收完整无重复字段的协议响应；超过上限时明确失败，绝不截断节点标识。</summary>
        internal static Response ParseResponse(string json)
        {
            JObject value = ParseObject(json, MaxResponseBytes);
            Require(!value.Properties().Any(item => !s_ResponseFields.Contains(item.Name, StringComparer.Ordinal)));
            Require(value["protocolVersion"]?.Type == JTokenType.Integer && (int)value["protocolVersion"] == 1 && value["success"]?.Type == JTokenType.Boolean);
            string state = ReadString(value, "state", true);
            Require(new[] { "ready", "blocked", "planned", "succeeded", "partial", "running", "failed", "stale" }.Contains(state, StringComparer.Ordinal));
            JToken external = value["requiresExternalProcess"];
            Require(external == null || external.Type == JTokenType.Boolean);
            return new Response
            {
                Success = (bool)value["success"], State = state, Message = ReadString(value, "message", true),
                PlanJson = ReadString(value, "planJson", false), ReceiptJson = ReadString(value, "receiptJson", false),
                DataJson = ReadString(value, "dataJson", false), OutputPaths = ReadStrings(value, "outputPaths"), Issues = ReadStrings(value, "issues"),
                WriteSet = ReadStrings(value, "writeSet"), Evidence = ReadStrings(value, "evidence"), RequiresExternalProcess = external != null && (bool)external,
            };
        }

        /// <summary>只接受合法项目 Assets 相对路径，拒绝空段、父目录与反斜线；存在性由内包按操作检查。</summary>
        internal static bool IsAssetPath(string path, bool allowEmpty = true)
        {
            if (string.IsNullOrEmpty(path)) return allowEmpty;
            return path.StartsWith("Assets/", StringComparison.Ordinal) && path.Length <= 1024 &&
                !path.Contains('\\') && !path.Any(char.IsControl) && path.Split('/').All(part => part.Length > 0 && part != "." && part != "..");
        }

        /// <summary>调用已绑定的方法并转换中性错误；不公开底层堆栈或自动重试。</summary>
        private static Response Invoke(MethodInfo method, string payload)
        {
            try { return ParseResponse(method.Invoke(null, new object[] { payload }) as string); }
            catch { throw new InvalidOperationException("PSD 转 UI 自动化未返回有效协议结果；请保留原恢复令牌检查状态，不要自动重跑写操作。"); }
        }

        /// <summary>仅接纳准确 public static string Method(string) 且无重载、泛型或可选参数的方法。</summary>
        private static MethodInfo Bind(Type owner, string name)
        {
            MethodInfo[] candidates = owner.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(item => item.Name == name).ToArray();
            Require(candidates.Length == 1);
            MethodInfo method = candidates[0];
            ParameterInfo[] parameters = method.GetParameters();
            Require(!method.IsGenericMethod && method.ReturnType == typeof(string) && (method.CallingConvention & CallingConventions.VarArgs) == 0 &&
                parameters.Length == 1 && parameters[0].ParameterType == typeof(string) && !parameters[0].IsOptional);
            return method;
        }

        /// <summary>有界读取严格 JSON 对象，禁止重复属性及过深对象。</summary>
        private static JObject ParseObject(string json, int maxBytes)
        {
            Require(!string.IsNullOrWhiteSpace(json) && System.Text.Encoding.UTF8.GetByteCount(json) <= maxBytes);
            using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 64, DateParseHandling = DateParseHandling.None })
            {
                JObject value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                Require(!reader.Read());
                return value;
            }
        }

        /// <summary>核对固定字符串字段，缺省可选字段统一为空，不做类型强制转换。</summary>
        private static string ReadString(JObject value, string field, bool required)
        {
            JToken token = value[field];
            if (token == null || token.Type == JTokenType.Null) { Require(!required); return null; }
            Require(token.Type == JTokenType.String);
            return (string)token;
        }

        /// <summary>读取纯字符串数组；空数组不构成成功证据，非字符串元素拒绝。</summary>
        private static string[] ReadStrings(JObject value, string field)
        {
            JToken token = value[field];
            if (token == null || token.Type == JTokenType.Null) return Array.Empty<string>();
            Require(token is JArray && token.All(item => item.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)item)));
            return token.Values<string>().ToArray();
        }

        /// <summary>读取普通文件的 SHA-256，不允许符号链接和包外重解析点。</summary>
        private static string HashFile(string path)
        {
            RequireOrdinaryFile(path);
            return GenerateActionCommon.ComputeFileHash(path, CancellationToken.None);
        }

        /// <summary>核对真实文件与全部父目录，不加载程序集或修改磁盘。</summary>
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
            throw new InvalidOperationException("PSD 转 UI 桥程序集映像不完整。");
        }

        /// <summary>身份和协议失败统一收口，不暴露物理路径或底层异常正文。</summary>
        private static void Require(bool condition)
        {
            if (!condition) throw new InvalidOperationException("PSD 转 UI 桥的身份或协议不完整。");
        }
    }
}
