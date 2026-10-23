/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidHybridClrGenerateEvidenceStore.cs
 * author:    taoye
 * created:   2026/10/20
 * descrip:   Android Generate独立typed证据的固定路径完整存取
 ***************************************************************/
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Editor
{
    /// <summary>Android材料使用独立根及DTO；不改变WebGL严格schema、codec或旧Receipt。</summary>
    internal sealed class AndroidHybridClrGenerateEvidenceStore
    {
        private const string Root = "Library/Nova/AgentActions/HybridCLRGenerateAndroid";
        private readonly string project;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Error, MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            NullValueHandling = NullValueHandling.Include, DefaultValueHandling = DefaultValueHandling.Include, MaxDepth = 128 };

        /// <summary>只绑定生产实际项目根；构造不创建文件。</summary>
        /// <param name="projectRoot">由BuildActionCommon取得的根，不能来自请求。</param>
        internal AndroidHybridClrGenerateEvidenceStore(string projectRoot) { project = Path.GetFullPath(projectRoot); }

        /// <summary>计算原完整before的有界指针，不写证据目录。</summary>
        /// <param name="plan">真实原计划。</param><returns>未写指针。</returns>
        internal HybridClrGenerateEvidenceStore.Reference Plan(PlayerBuildAction.AndroidGeneratePlan plan)
        {
            string id = RequirePlanRoot(plan);
            byte[] bytes = Encode(new PlayerBuildAction.AndroidGenerateEvidence { before = plan });
            return new HybridClrGenerateEvidenceStore.Reference { id = id, beforeLength = bytes.Length, beforeSha256 = Hash(bytes) };
        }

        /// <summary>完整计划供审阅；超传输预算则明确拒绝，不截断或影响WebGL codec。</summary>
        /// <param name="receipt">小恢复回执。</param><param name="plan">完整Android计划。</param><returns>准确DataJson。</returns>
        internal static string Preview(string receipt, PlayerBuildAction.AndroidGeneratePlan plan)
        {
            string value = JsonConvert.SerializeObject(new { receipt = JObject.Parse(receipt), androidGeneratePlan = plan }, Settings);
            if (Utf8.GetByteCount(value) > HybridClrGenerateEvidenceStore.MaxPlanPreviewDataBytes) throw new InvalidOperationException("Android完整Plan预览超过1536KiB，保持blocked。");
            return value;
        }

        /// <summary>只在唯一GUID域不存在时原子写before；摘要必须与原计划完全相符。</summary>
        /// <param name="plan">原冻结计划。</param><param name="reference">原未写指针。</param><returns>真实已写指针。</returns>
        internal HybridClrGenerateEvidenceStore.Reference WriteBefore(PlayerBuildAction.AndroidGeneratePlan plan, HybridClrGenerateEvidenceStore.Reference reference)
        {
            Validate(reference); string id = RequirePlanRoot(plan); byte[] bytes = Encode(new PlayerBuildAction.AndroidGenerateEvidence { before = plan });
            if (reference.beforeWritten || reference.finalWritten || id != reference.id || bytes.Length != reference.beforeLength || Hash(bytes) != reference.beforeSha256) throw new InvalidOperationException("Android before与原指针不同。");
            string directory = DirectoryPath(reference); Guard(directory);
            if (Directory.Exists(directory) || File.Exists(directory)) throw new InvalidOperationException("Android唯一证据目录已经存在。");
            Directory.CreateDirectory(directory); WriteAtomic(FilePath(reference, "before"), bytes);
            var saved = Clone(reference); saved.beforeWritten = true; return saved;
        }

        /// <summary>本次final只尝试一次且保持不可变；失败返回原before指针，不重放生成。</summary>
        /// <param name="reference">本次原before。</param><param name="evidence">实际已有材料。</param><param name="saved">真实保存阶段。</param><param name="error">失败原因。</param><returns>完整final已写时为true。</returns>
        internal bool TryWriteFinal(HybridClrGenerateEvidenceStore.Reference reference, PlayerBuildAction.AndroidGenerateEvidence evidence, out HybridClrGenerateEvidenceStore.Reference saved, out string error)
        {
            saved = reference; error = null;
            try
            {
                var before = Read(reference, "before", reference.beforeLength, reference.beforeSha256);
                if (!reference.beforeWritten || reference.finalWritten || evidence?.before == null || RequirePlanRoot(evidence.before) != reference.id || RequirePlanRoot(before.before) != reference.id || Serialize(before.before) != Serialize(evidence.before)) throw new InvalidOperationException("Android final不属于原完整before。");
                byte[] bytes = Encode(evidence); string path = FilePath(reference, "final"); if (File.Exists(path)) throw new InvalidOperationException("Android final已经存在，禁止改写历史。");
                WriteAtomic(path, bytes); saved = Clone(reference); saved.finalWritten = true; saved.finalLength = bytes.Length; saved.finalSha256 = Hash(bytes); return true;
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException)) { error = "Android完整final保存失败：" + exception.Message; return false; }
        }

        /// <summary>只读两份固定原文，严格核长度/SHA/DTO与同一before，不调用生成或补当前历史。</summary>
        /// <param name="reference">原编译载荷绑定的指针。</param><param name="evidence">完整历史。</param><param name="error">缺失或漂移原因。</param><returns>两份原文完整自洽时为true。</returns>
        internal bool TryRead(HybridClrGenerateEvidenceStore.Reference reference, out PlayerBuildAction.AndroidGenerateEvidence evidence, out string error)
        {
            evidence = null; error = null;
            try
            {
                Validate(reference); if (!reference.beforeWritten || !reference.finalWritten) throw new InvalidOperationException("Android原Operation没有完整before/final。");
                var before = Read(reference, "before", reference.beforeLength, reference.beforeSha256); var final = Read(reference, "final", reference.finalLength, reference.finalSha256);
                if (before.complete || before.chainEntered || before.chainReturned || before.before == null || final.before == null || RequirePlanRoot(before.before) != reference.id || RequirePlanRoot(final.before) != reference.id || Serialize(before.before) != Serialize(final.before)) throw new InvalidOperationException("Android历史before/final计划绑定不一致。");
                evidence = final; return true;
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException)) { error = "Android完整证据读取失败：" + exception.Message; return false; }
        }

        /// <summary>仅返回实际写成的固定证据文件。</summary>
        /// <param name="reference">真实阶段指针。</param><returns>已写Artifact集合。</returns>
        internal string[] Artifacts(HybridClrGenerateEvidenceStore.Reference reference) => new[] { reference.beforeWritten ? FilePath(reference, "before") : null, reference.finalWritten ? FilePath(reference, "final") : null }.Where(path => path != null).ToArray();

        /// <summary>返回本次准确GUID证据根，供Plan声明写集。</summary>
        /// <param name="reference">已验证的有限指针。</param><returns>准确绝对目录。</returns>
        internal string DirectoryPath(HybridClrGenerateEvidenceStore.Reference reference) { Validate(reference); return Path.Combine(project, Root, reference.id); }

        /// <summary>读取准确文件并严格解码；拒绝重复字段、元数据、未知字段及非规范原文。</summary>
        /// <param name="reference">原有限指针。</param><param name="name">固定before/final角色。</param><param name="length">原长度。</param><param name="hash">原SHA。</param><returns>完整typed正文。</returns>
        private PlayerBuildAction.AndroidGenerateEvidence Read(HybridClrGenerateEvidenceStore.Reference reference, string name, int length, string hash)
        {
            Validate(reference); string path = FilePath(reference, name); Guard(path);
            if (length < 1 || length > HybridClrGenerateEvidenceStore.MaxRawBytes || !File.Exists(path) || new FileInfo(path).Length != length) throw new InvalidOperationException("Android固定证据文件缺失或超预算。");
            byte[] bytes = File.ReadAllBytes(path); if (Hash(bytes) != hash) throw new InvalidOperationException("Android证据SHA漂移。");
            string json = Utf8.GetString(bytes); var parsed = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (parsed.DescendantsAndSelf().OfType<JProperty>().Any(property => property.Name.StartsWith("$", StringComparison.Ordinal))) throw new InvalidOperationException("Android证据含未知JSON元数据。");
            var value = parsed.ToObject<PlayerBuildAction.AndroidGenerateEvidence>(JsonSerializer.Create(Settings));
            if (value == null || Serialize(value) != json) throw new InvalidOperationException("Android证据不是准确完整规范DTO原文。"); return value;
        }

        /// <summary>完整UTF8正文硬限8MiB，不压缩/截断来绕过预算。</summary>
        /// <param name="value">完整实际材料。</param><returns>准确有界字节。</returns>
        private static byte[] Encode(PlayerBuildAction.AndroidGenerateEvidence value)
        { byte[] bytes = Utf8.GetBytes(Serialize(value)); if (bytes.Length < 1 || bytes.Length > HybridClrGenerateEvidenceStore.MaxRawBytes) throw new InvalidOperationException("Android完整typed证据超过8MiB。"); return bytes; }

        /// <summary>固定serializer保留null和默认值，不启用类型元数据。</summary>
        /// <param name="value">准确DTO。</param><returns>完整规范JSON。</returns>
        private static string Serialize(object value) => JsonConvert.SerializeObject(value, Formatting.None, Settings);

        /// <summary>仅从Plan的独立report-backup布局取得原N-GUID，不接受任意根路径。</summary>
        /// <param name="plan">原计划。</param><returns>准确N-GUID。</returns>
        private static string Id(PlayerBuildAction.AndroidGeneratePlan plan)
        { string id = plan?.recoveryDirectory == null ? null : Path.GetFileName(Path.GetDirectoryName(plan.recoveryDirectory)); if (!Guid.TryParseExact(id, "N", out Guid guid) || guid.ToString("N") != id) throw new InvalidOperationException("Android计划缺唯一规范N-GUID。"); return id; }

        /// <summary>冻结完整生产根与report-backup角色；同名GUID不能把外部恢复路径升级为本次证据。</summary>
        /// <param name="plan">原完整计划。</param><returns>准确规范N-GUID。</returns>
        private string RequirePlanRoot(PlayerBuildAction.AndroidGeneratePlan plan)
        {
            string id = Id(plan); string expected = Path.Combine(project, Root, id, "report-backup");
            if (!Path.IsPathRooted(plan.recoveryDirectory) || Path.GetFullPath(plan.recoveryDirectory) != expected || plan.recoveryDirectory != expected)
                throw new InvalidOperationException("Android计划恢复目录不是本项目本GUID准确report-backup角色。");
            Guard(expected); return id;
        }

        /// <summary>拒绝未知版本、GUID或不完整原SHA。</summary>
        /// <param name="reference">原指针。</param>
        private static void Validate(HybridClrGenerateEvidenceStore.Reference reference)
        {
            if (reference == null || reference.version != 1 || !Guid.TryParseExact(reference.id, "N", out Guid id) || id.ToString("N") != reference.id || reference.beforeLength < 1 || reference.beforeLength > HybridClrGenerateEvidenceStore.MaxRawBytes || !IsHash(reference.beforeSha256) ||
                (reference.finalWritten ? !reference.beforeWritten || reference.finalLength < 1 || reference.finalLength > HybridClrGenerateEvidenceStore.MaxRawBytes || !IsHash(reference.finalSha256) : reference.finalLength != 0 || reference.finalSha256 != null))
                throw new InvalidOperationException("Android证据指针无效。");
        }

        /// <summary>只接受规范小写完整SHA-256，不以长度相同代替摘要格式。</summary>
        /// <param name="value">指针摘要。</param><returns>64位小写十六进制时为true。</returns>
        private static bool IsHash(string value) => value?.Length == 64 && value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f');

        /// <summary>固定角色文件路径；任何其它名称拒绝。</summary>
        /// <param name="reference">原指针。</param><param name="name">固定角色。</param><returns>准确绝对路径。</returns>
        private string FilePath(HybridClrGenerateEvidenceStore.Reference reference, string name)
        { if (name != "before" && name != "final") throw new InvalidOperationException("Android证据角色未知。"); return Path.Combine(DirectoryPath(reference), name + ".json"); }

        /// <summary>复用准确拥有域的深层链接拒绝，不允许符号链接转出项目或证据根。</summary>
        /// <param name="path">准确本次证据路径。</param>
        private void Guard(string path) => new HybridClrGenerateInputProjection(new HybridClrGenerateInputProjection.Spec { projectRoot = project, directories = Array.Empty<string>(), files = new[] { path } }, Array.Empty<string>());

        /// <summary>同目录独占临时文件原子提交，finally只清理本次临时文件。</summary>
        /// <param name="path">准确最终路径。</param><param name="bytes">完整正文。</param>
        private void WriteAtomic(string path, byte[] bytes)
        { Guard(path); string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path); } finally { if (File.Exists(temporary)) File.Delete(temporary); } }

        /// <summary>复制小指针，不共享可变对象。</summary>
        /// <param name="value">原有限指针。</param><returns>独立副本。</returns>
        private static HybridClrGenerateEvidenceStore.Reference Clone(HybridClrGenerateEvidenceStore.Reference value) => JsonConvert.DeserializeObject<HybridClrGenerateEvidenceStore.Reference>(Serialize(value), Settings);

        /// <summary>完整字节的SHA-256，不输出正文。</summary>
        /// <param name="bytes">完整自有字节。</param><returns>小写SHA。</returns>
        private static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
