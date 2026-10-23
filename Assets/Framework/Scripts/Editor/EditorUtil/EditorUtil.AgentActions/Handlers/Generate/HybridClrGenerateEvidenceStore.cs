/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HybridClrGenerateEvidenceStore.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Generate 完整本地证据与小型 Operation 回执的准确拥有域存储
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Editor
{
    /// <summary>完整 typed 正文保存在唯一 GUID 域，小 Receipt 只绑定准确长度与 SHA；不恢复执行权限。</summary>
    internal sealed class HybridClrGenerateEvidenceStore
    {
        internal const int MaxRawBytes = 8 * 1024 * 1024;
        // v3仅在两个固定files槽复用完整行；wire仍8MiB，展开和表/引用同时有硬限。
        internal const int MaxExpandedEvidenceBytes = 16 * 1024 * 1024;
        private const int MaxFileRows = 32768;
        private const int MaxFileRowReferences = 65536;
        private const string FileRowCodec = "nova-hybridclr-generate-file-rows-v3";
        internal const int MaxPlanPreviewCompressedBytes = 1024 * 1024;
        internal const int MaxPlanPreviewDataBytes = 1536 * 1024;
        private const string PlanPreviewCodec = "nova-hybridclr-generate-plan-gzip-v1";
        private static readonly string[] PlanPreviewFields = { "codec", "rawLength", "rawSha256", "gzipBase64" };
        private const string RelativeRoot = "Library/Nova/AgentActions/HybridCLRGenerate";
        private readonly string m_Project;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        /// <summary>固定 before/final 两个文件的完整指针；written 只证明已写文件，不证明生成成功。</summary>
        [Serializable]
        internal sealed class Reference
        {
            public int version = 1;
            public string id;
            public bool beforeWritten;
            public int beforeLength;
            public string beforeSha256;
            public bool finalWritten;
            public int finalLength;
            public string finalSha256;
        }

        /// <summary>生产调用只传 BuildActionCommon.ProjectRoot；长期 FS 测试可传其隔离项目根。</summary>
        /// <param name="projectRoot">当前准确项目根，不能来自 Action 请求或回执路径。</param>
        /// <returns>仅持有该根的内部存储实例，不创建文件。</returns>
        internal HybridClrGenerateEvidenceStore(string projectRoot)
        {
            m_Project = Path.GetFullPath(projectRoot);
            if (!Directory.Exists(m_Project)) throw new InvalidOperationException("Generate 证据项目根不存在。");
            new HybridClrGenerateInputProjection(new HybridClrGenerateInputProjection.Spec { projectRoot = m_Project, directories = Array.Empty<string>(), files = new[] { Path.Combine(m_Project, RelativeRoot, ".boundary.json") } }, Array.Empty<string>());
        }

        /// <summary>Plan 只计算完整 before 的字节长度与 SHA，不创建目录或声称已有执行证据。</summary>
        /// <param name="plan">已冻结的完整 typed 写前计划。</param>
        /// <returns>尚未写入的固定 before 指针，原文超过边界则拒绝 Plan。</returns>
        internal Reference Plan(PlayerBuildAction.GeneratePlan plan)
        {
            string id = Id(plan);
            byte[] bytes = EncodeBefore(new PlayerBuildAction.GenerateEvidence { before = plan });
            return new Reference { id = id, beforeLength = bytes.Length, beforeSha256 = Hash(bytes) };
        }

        /// <summary>执行写前要求唯一 GUID 目录完全不存在，再原子写完整 before，保留原 Plan 摘要绑定。</summary>
        /// <param name="plan">原 Plan，不能用当前状态重建旧 before。</param>
        /// <param name="reference">Plan 已保存的未写指针。</param>
        /// <returns>已写 before 的独立指针；任何目录占用或摘要不符在 Generate 前拒绝。</returns>
        internal Reference WriteBefore(PlayerBuildAction.GeneratePlan plan, Reference reference)
        {
            Validate(reference, false);
            byte[] bytes = EncodeBefore(new PlayerBuildAction.GenerateEvidence { before = plan });
            if (Id(plan) != reference.id || reference.beforeWritten || reference.finalWritten || bytes.Length != reference.beforeLength || Hash(bytes) != reference.beforeSha256)
                throw new InvalidOperationException("Generate before 指针与原冻结计划不符。");
            string directory = DirectoryPath(reference); Guard(directory);
            if (Directory.Exists(directory) || File.Exists(directory)) throw new InvalidOperationException("Generate 唯一证据目录已被占用。");
            Directory.CreateDirectory(directory);
            WriteAtomic(FilePath(reference, "before"), bytes);
            var result = Clone(reference); result.beforeWritten = true; return result;
        }

        /// <summary>成功或失败都可保存完整实际 typed 材料；失败安全返回原因，不让 catch 再抛导致原证据丢失。</summary>
        /// <param name="reference">本轮已写 before 的原指针。</param>
        /// <param name="evidence">本轮实际后验或失败材料，不补造回调。</param>
        /// <param name="saved">成功后的完整指针；失败仍保留原 before 指针。</param>
        /// <param name="error">无法完整保存的具体错误。</param>
        /// <returns>完整 final 文件已原子保存并与同一 before 绑定时为 true。</returns>
        internal bool TryWriteFinal(Reference reference, PlayerBuildAction.GenerateEvidence evidence, out Reference saved, out string error)
        {
            saved = reference;
            try
            {
                Validate(reference, true);
                PlayerBuildAction.GenerateEvidence before = Decode(ReadChecked(reference, "before", reference.beforeLength, reference.beforeSha256));
                if (evidence?.before == null || Id(evidence.before) != reference.id || Serialize(before.before) != Serialize(evidence.before))
                    throw new InvalidOperationException("Generate final 不属于原完整 before。");
                byte[] bytes = Encode(evidence); string path = FilePath(reference, "final");
                if (File.Exists(path))
                {
                    if (Hash(ReadChecked(reference, "final", bytes.Length, Hash(bytes))) != Hash(bytes)) throw new InvalidOperationException("Generate final 已被不同材料占用。");
                }
                else WriteAtomic(path, bytes);
                saved = Clone(reference); saved.finalWritten = true; saved.finalLength = bytes.Length; saved.finalSha256 = Hash(bytes);
                error = null; return true;
            }
            catch (Exception exception) when (IsDataFailure(exception)) { error = "Generate 完整证据保存失败：" + exception.Message; return false; }
        }

        /// <summary>只读准确 before/final，逐文件核长度、SHA、严格 JSON、同一计划，不恢复执行能力或补当前状态。</summary>
        /// <param name="reference">小 Receipt 已由原编译 payload 绑定的完整指针。</param>
        /// <param name="evidence">准确原文解码的完整材料。</param>
        /// <param name="error">缺失、越域、链接、漂移或结构错误。</param>
        /// <returns>两份文件均完整且属于同一原 Plan 时为 true；不据此宣称 Generate complete。</returns>
        internal bool TryRead(Reference reference, out PlayerBuildAction.GenerateEvidence evidence, out string error)
        {
            evidence = null;
            try
            {
                Validate(reference, true);
                if (!reference.finalWritten) throw new InvalidOperationException("Generate 原 Operation 尚无完整 final 证据，保持 partial。");
                var before = Decode(ReadChecked(reference, "before", reference.beforeLength, reference.beforeSha256));
                var final = Decode(ReadChecked(reference, "final", reference.finalLength, reference.finalSha256));
                if (before.complete || before.chainEntered || before.chainReturned || before.before == null || final.before == null || Id(before.before) != reference.id || Id(final.before) != reference.id || Serialize(before.before) != Serialize(final.before))
                    throw new InvalidOperationException("Generate 完整证据的原 before/最终计划绑定不符。");
                evidence = final; error = null; return true;
            }
            catch (Exception exception) when (IsDataFailure(exception)) { error = "Generate 完整证据读取失败：" + exception.Message; return false; }
        }

        /// <summary>返回本轮固定完整证据域；正式 before/final 和原子临时文件全部在该准确 GUID 目录内。</summary>
        /// <param name="reference">Plan 或完整 Receipt 指针。</param>
        /// <returns>准确目录路径，不创建、不接受任意路径。</returns>
        internal string DirectoryPath(Reference reference) { Validate(reference, false); return Path.Combine(m_Project, RelativeRoot, reference.id); }

        /// <summary>仅已写文件进入正式 Artifact；计划声明和失败诊断不冒充实际存在。</summary>
        /// <param name="reference">当前真实保存阶段的指针。</param>
        /// <returns>固定 before/final 中已经完成写入的准确路径。</returns>
        internal string[] Artifacts(Reference reference) => new[] { reference.beforeWritten ? FilePath(reference, "before") : null, reference.finalWritten ? FilePath(reference, "final") : null }.Where(path => path != null).ToArray();

        /// <summary>Plan 检查真实 OperationRecord 结构的完整预算并预留后续状态/消息空间，不扩大 Store 256KiB。</summary>
        /// <param name="receiptJson">小型恢复 Receipt 的完整字符串。</param>
        /// <param name="writes">本次准确写集。</param>
        /// <returns>无返回值；计划记录超过 192KiB 则在 Execute 前拒绝。</returns>
        internal static void RequireOperationBudget(string receiptJson, string[] writes)
        {
            var record = new AgentActionOperationRecord { OperationId = new string('a', 32), ActionId = "nova.project.hotfix.generate-artifacts", ContractMajor = 1, RegistryGeneration = long.MaxValue, Status = "planned", CreatedAtUtc = "2026-10-16T00:00:00.0000000Z", UpdatedAtUtc = "2026-10-16T00:00:00.0000000Z", RequestSha256 = new string('a', 64), WriteSet = writes, ReceiptJson = receiptJson };
            if (Utf8.GetByteCount(JsonConvert.SerializeObject(record, Formatting.None)) > 192 * 1024)
                throw new InvalidOperationException("Generate 小型 Operation 完整记录预算超过 192KiB，不能安全进入 Store。");
        }

        /// <summary>将完整计划无损压缩为专用审阅封装；不写文件，不返回可恢复或可执行的计划。</summary>
        /// <param name="plan">本次内存中已冻结的完整 GeneratePlan；只有预览表示变化，原 State 不变。</param>
        /// <returns>含准确 rawLength、SHA 和规范 Base64 的 JSON；原文8MiB、gzip1MiB，超限拒绝而不截断。</returns>
        internal static string EncodePlanPreview(PlayerBuildAction.GeneratePlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            string json = SerializePlanPreview(plan); byte[] raw = Utf8.GetBytes(json);
            if (raw.Length < 1 || raw.Length > MaxRawBytes) throw new InvalidOperationException("Generate Plan 预览原文超过8MiB。");
            ParseObject(json, generateMaterialType: typeof(PlayerBuildAction.GeneratePlan));
            byte[] compressed;
            using (var output = new BoundedPlanPreviewOutput())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true)) gzip.Write(raw, 0, raw.Length);
                compressed = output.ToArray();
            }
            return Serialize(new JObject { ["codec"] = PlanPreviewCodec, ["rawLength"] = raw.Length, ["rawSha256"] = Hash(raw), ["gzipBase64"] = Convert.ToBase64String(compressed) });
        }

        /// <summary>专用预览明确保留所有null与默认字段，与完整计划的可审阅结构一致，不改变文件证据serializer。</summary>
        /// <param name="plan">本次原内存GeneratePlan，不能用摘要或当前环境替换。</param>
        /// <returns>完整计划JSON原文，所有typed字段均保留。</returns>
        private static string SerializePlanPreview(PlayerBuildAction.GeneratePlan plan)
        {
            JsonSerializerSettings settings = Settings(); settings.NullValueHandling = NullValueHandling.Include; settings.DefaultValueHandling = DefaultValueHandling.Include;
            using (var output = new StringWriter(CultureInfo.InvariantCulture)) using (var writer = new JsonTextWriter(output)) { JsonSerializer.Create(settings).Serialize(writer, plan); return output.ToString(); }
        }

        /// <summary>只为完整审阅解码专用 Plan 封装；严格核对所有边界，不将内容交给 Execute 或恢复入口。</summary>
        /// <param name="payload">只接受本 codec 的完整 JSON 封装，不接受旧裸计划或其它 codec。</param>
        /// <param name="planJson">完整原始计划 JSON；仅供审阅，失败时为 null。</param>
        /// <param name="error">格式、大小、摘要、gzip、UTF8 或 schema 的具体拒绝原因。</param>
        /// <returns>原始JSON字节与已知DTO成员可解时为 true；不判断业务前提或授予执行、恢复权限。</returns>
        internal static bool TryDecodePlanPreview(string payload, out string planJson, out string error)
        {
            planJson = null;
            try
            {
                if (string.IsNullOrEmpty(payload) || payload.Length > MaxPlanPreviewDataBytes || Utf8.GetByteCount(payload) > MaxPlanPreviewDataBytes)
                    throw new InvalidOperationException("Generate Plan 预览封装为空或超限。");
                JObject wrapper = ParseObject(payload, true);
                if (wrapper.Count != PlanPreviewFields.Length || wrapper.Properties().Any(property => !PlanPreviewFields.Contains(property.Name, StringComparer.Ordinal)) ||
                    wrapper["codec"]?.Type != JTokenType.String || wrapper["codec"].Value<string>() != PlanPreviewCodec || wrapper["rawLength"]?.Type != JTokenType.Integer ||
                    wrapper["rawSha256"]?.Type != JTokenType.String || wrapper["gzipBase64"]?.Type != JTokenType.String)
                    throw new InvalidOperationException("Generate Plan 预览封装存在未知、缺失或类型错误的字段。");
                long length = wrapper["rawLength"].Value<long>(); string sha = wrapper["rawSha256"].Value<string>(), base64 = wrapper["gzipBase64"].Value<string>();
                if (length < 1 || length > MaxRawBytes || !IsHash(sha) || string.IsNullOrEmpty(base64) || base64.Length > ((MaxPlanPreviewCompressedBytes + 2) / 3) * 4 || base64.Any(char.IsWhiteSpace))
                    throw new InvalidOperationException("Generate Plan 预览长度、SHA 或 Base64 非规范或超限。");
                byte[] compressed = Convert.FromBase64String(base64);
                if (compressed.Length > MaxPlanPreviewCompressedBytes || Convert.ToBase64String(compressed) != base64)
                    throw new InvalidOperationException("Generate Plan 预览 Base64 不是有界规范编码。");
                byte[] raw = DecompressPlanPreview(compressed, (int)length);
                if (Hash(raw) != sha) throw new InvalidOperationException("Generate Plan 预览原文 SHA 漂移。");
                string json = Utf8.GetString(raw); JObject value = ParseObject(json, generateMaterialType: typeof(PlayerBuildAction.GeneratePlan));
                if (value.ToObject<PlayerBuildAction.GeneratePlan>(JsonSerializer.Create(Settings())) == null)
                    throw new InvalidOperationException("Generate Plan 预览不能解析为已知计划DTO表示。");
                planJson = json; error = null; return true;
            }
            catch (Exception exception) when (IsDataFailure(exception)) { error = "Generate Plan 预览解码失败：" + exception.Message; return false; }
        }

        /// <summary>小 Receipt 与完整压缩计划共同进入 DataJson；保留512KiB给现有2MiB传输外层，不更改通用上限。</summary>
        /// <param name="receiptJson">本次已通过192KiB Operation预算的小 Receipt 原文，不含完整 typed 正文。</param>
        /// <param name="plan">本次内存中完整冻结计划，保持未压缩 State 供原框架使用。</param>
        /// <returns>完整审阅 DataJson；最终 DataJson 超过1536KiB则在 Plan 中拒绝。</returns>
        internal static string CreatePlanPreviewData(string receiptJson, PlayerBuildAction.GeneratePlan plan)
        {
            if (string.IsNullOrEmpty(receiptJson) || receiptJson.Length > 192 * 1024 || Utf8.GetByteCount(receiptJson) > 192 * 1024)
                throw new InvalidOperationException("Generate Plan 小 Receipt 预览预算超限。");
            JObject receipt = ParseObject(receiptJson);
            var data = new JObject { ["receipt"] = receipt, ["webglGeneratePlan"] = ParseObject(EncodePlanPreview(plan), true) };
            string json = Serialize(data);
            if (Utf8.GetByteCount(json) > MaxPlanPreviewDataBytes) throw new InvalidOperationException("Generate Plan 完整审阅 DataJson 超过1536KiB。");
            return json;
        }

        /// <summary>精确解压一个无可选头的gzip member；拒绝zip bomb、截断、串联、尾随及CRC/ISIZE漂移。</summary>
        /// <param name="compressed">已核规范Base64且最多1MiB的完整gzip字节。</param>
        /// <param name="rawLength">已核1..8MiB的声明原文长度。</param>
        /// <returns>准确长度的完整原文；任何额外输出或未消费字节拒绝。</returns>
        private static byte[] DecompressPlanPreview(byte[] compressed, int rawLength)
        {
            if (compressed.Length < 18 || compressed[0] != 0x1f || compressed[1] != 0x8b || compressed[2] != 8 || compressed[3] != 0)
                throw new InvalidOperationException("Generate Plan gzip头损坏或使用未知可选字段。");
            if (ReadPlanPreviewUInt32(compressed, compressed.Length - 4) != (uint)rawLength)
                throw new InvalidOperationException("Generate Plan gzip ISIZE与声明不符。");
            byte[] raw = new byte[rawLength];
            using (var source = new ExactPlanPreviewDeflateInput(compressed))
            using (var inflate = new DeflateStream(source, CompressionMode.Decompress, true))
            {
                int offset = 0;
                while (offset < rawLength) { int count = inflate.Read(raw, offset, rawLength - offset); if (count == 0) throw new InvalidOperationException("Generate Plan gzip原文截断。"); offset += count; }
                if (inflate.ReadByte() != -1 || source.Position != source.Length || source.EndWasRequested)
                    throw new InvalidOperationException("Generate Plan gzip超出声明或含串联/尾随内容。");
            }
            if (ReadPlanPreviewUInt32(compressed, compressed.Length - 8) != PlanPreviewCrc32(raw)) throw new InvalidOperationException("Generate Plan gzip CRC错误。");
            return raw;
        }

        /// <summary>按gzip固定小端读取尾部校验值。</summary>
        /// <param name="bytes">已经过最小长度校验的gzip字节。</param>
        /// <param name="offset">源码派生的CRC或ISIZE起点。</param>
        /// <returns>准确无符号32位值。</returns>
        private static uint ReadPlanPreviewUInt32(byte[] bytes, int offset) => (uint)bytes[offset] | (uint)bytes[offset + 1] << 8 | (uint)bytes[offset + 2] << 16 | (uint)bytes[offset + 3] << 24;

        /// <summary>独立核验gzip CRC，完整原文身份仍由SHA绑定。</summary>
        /// <param name="bytes">已限8MiB的完整原文。</param>
        /// <returns>gzip规范CRC32。</returns>
        private static uint PlanPreviewCrc32(byte[] bytes)
        {
            uint crc = uint.MaxValue;
            foreach (byte value in bytes) { crc ^= value; for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? crc >> 1 ^ 0xedb88320u : crc >> 1; }
            return ~crc;
        }

        /// <summary>压缩器输出在写入前限1MiB，避免先无界增长后才检查。</summary>
        private sealed class BoundedPlanPreviewOutput : Stream
        {
            private readonly MemoryStream m_Output = new MemoryStream();
            /// <summary>压缩输出不可读取。</summary>
            /// <returns>固定false。</returns>
            public override bool CanRead => false;
            /// <summary>不能回退覆盖或绕过累计输出边界。</summary>
            /// <returns>固定false。</returns>
            public override bool CanSeek => false;
            /// <summary>仅接受有界顺序写。</summary>
            /// <returns>固定true。</returns>
            public override bool CanWrite => true;
            /// <summary>当前已写字节数。</summary>
            /// <returns>私有输出流当前长度。</returns>
            public override long Length => m_Output.Length;
            /// <summary>只能读取当前末尾，不能外设位置。</summary>
            /// <param name="value">setter隐式输入，任何外设位置均拒绝，不更改流。</param>
            /// <returns>getter返回已写长度；setter始终抛NotSupportedException且无返回值。</returns>
            public override long Position { get => m_Output.Length; set => throw new NotSupportedException(); }
            /// <summary>仅在gzip完整关闭后复制完整有界结果。</summary>
            /// <returns>最多1MiB的独立字节数组。</returns>
            internal byte[] ToArray() => m_Output.ToArray();
            /// <summary>每次写入前核对准确输出大小。</summary>
            /// <param name="buffer">压缩器当前字节块。</param>
            /// <param name="offset">当前块起点。</param>
            /// <param name="count">当前块大小。</param>
            /// <returns>无返回值；超限拒绝，不保留partial预览。</returns>
            public override void Write(byte[] buffer, int offset, int count)
            {
                if (count < 0 || Position > MaxPlanPreviewCompressedBytes - (long)count) throw new InvalidOperationException("Generate Plan gzip超过1MiB。");
                m_Output.Write(buffer, offset, count);
            }
            /// <summary>单字节输出同样不能绕过边界。</summary>
            /// <param name="value">压缩器输出字节。</param>
            /// <returns>无返回值；到达上限拒绝。</returns>
            public override void WriteByte(byte value) { if (Position >= MaxPlanPreviewCompressedBytes) throw new InvalidOperationException("Generate Plan gzip超过1MiB。"); m_Output.WriteByte(value); }
            /// <summary>仅提交当前内存输出，不写文件。</summary>
            /// <returns>无返回值。</returns>
            public override void Flush() => m_Output.Flush();
            /// <summary>拒绝读取压缩输出。</summary>
            /// <param name="buffer">不接受的目标。</param>
            /// <param name="offset">不接受的起点。</param>
            /// <param name="count">不接受的长度。</param>
            /// <returns>始终抛NotSupportedException。</returns>
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            /// <summary>拒绝定位以保证所有输出累计计入边界。</summary>
            /// <param name="offset">不接受的偏移。</param>
            /// <param name="origin">不接受的起点。</param>
            /// <returns>始终抛NotSupportedException。</returns>
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            /// <summary>拒绝直接扩大内存输出。</summary>
            /// <param name="value">不接受的长度。</param>
            /// <returns>始终抛NotSupportedException。</returns>
            public override void SetLength(long value) => throw new NotSupportedException();
            /// <summary>确定释放自己拥有的内存缓冲区。</summary>
            /// <param name="disposing">正常using清理时为true。</param>
            /// <returns>无返回值。</returns>
            protected override void Dispose(bool disposing) { if (disposing) m_Output.Dispose(); base.Dispose(disposing); }
        }

        /// <summary>只暴露gzip头尾之间的DEFLATE，每次给一个字节以精确检测未消费内容。</summary>
        private sealed class ExactPlanPreviewDeflateInput : Stream
        {
            private readonly byte[] m_Bytes;
            private int m_Position;
            /// <summary>持有调用方已经核定大小的单member窗口，不复制或写入。</summary>
            /// <param name="source">至少18字节的已核gzip。</param>
            /// <returns>只读窗口实例。</returns>
            internal ExactPlanPreviewDeflateInput(byte[] source) { m_Bytes = source; }
            /// <summary>只允许同步读取。</summary>
            /// <returns>固定true。</returns>
            public override bool CanRead => true;
            /// <summary>禁止改变读取顺序。</summary>
            /// <returns>固定false。</returns>
            public override bool CanSeek => false;
            /// <summary>禁止写入。</summary>
            /// <returns>固定false。</returns>
            public override bool CanWrite => false;
            /// <summary>准确DEFLATE区长度，不含固定头尾。</summary>
            /// <returns>调用方已核定的压缩长度减固定18字节头尾。</returns>
            public override long Length => m_Bytes.Length - 18;
            /// <summary>准确已馈入的DEFLATE字节数；不能外设。</summary>
            /// <param name="value">setter隐式输入，任何外设均拒绝，不改变已消费位置。</param>
            /// <returns>getter返回准确已馈入字节数；setter始终抛NotSupportedException且无返回值。</returns>
            public override long Position { get => m_Position; set => throw new NotSupportedException(); }
            /// <summary>解压器曾请求窗口末尾之外时标记截断。</summary>
            /// <param name="value">私有setter隐式输入；仅Read到达窗口末尾后仍被请求时写true，初始false。</param>
            /// <returns>getter返回是否发生越尾请求；setter只保存该本次读取标记且无返回值。</returns>
            internal bool EndWasRequested { get; private set; }
            /// <summary>每次最多返回一个字节，不让预读掩盖尾随member。</summary>
            /// <param name="buffer">解压器提供的目标缓冲区。</param>
            /// <param name="offset">目标起点。</param>
            /// <param name="count">解压器请求的最大读取量。</param>
            /// <returns>零或一字节；结束额外读取同时标记EndWasRequested。</returns>
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException(nameof(offset));
                if (count == 0) return 0;
                if (m_Position == Length) { EndWasRequested = true; return 0; }
                buffer[offset] = m_Bytes[10 + m_Position++]; return 1;
            }
            /// <summary>只读内存无需提交。</summary>
            /// <returns>无返回值。</returns>
            public override void Flush() { }
            /// <summary>拒绝改变精确消费顺序。</summary>
            /// <param name="offset">不接受的偏移。</param>
            /// <param name="origin">不接受的起点。</param>
            /// <returns>始终抛NotSupportedException。</returns>
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            /// <summary>拒绝改变有界窗口长度。</summary>
            /// <param name="value">不接受的长度。</param>
            /// <returns>始终抛NotSupportedException。</returns>
            public override void SetLength(long value) => throw new NotSupportedException();
            /// <summary>拒绝修改原压缩字节。</summary>
            /// <param name="buffer">不接受的来源。</param>
            /// <param name="offset">不接受的起点。</param>
            /// <param name="count">不接受的长度。</param>
            /// <returns>始终抛NotSupportedException。</returns>
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        /// <summary>由原计划的准确 report-backup 路径解析唯一 N-GUID，拒绝其它恢复位置。</summary>
        /// <param name="plan">原完整计划。</param>
        /// <returns>固定 GUID；只接受本实例项目根下的准确布局。</returns>
        private string Id(PlayerBuildAction.GeneratePlan plan)
        {
            string parent = Path.GetDirectoryName(plan?.recoveryDirectory ?? string.Empty); string id = Path.GetFileName(parent);
            if (id?.Length != 32 || !Guid.TryParseExact(id, "N", out _) || Path.GetFullPath(plan.recoveryDirectory) != Path.Combine(m_Project, RelativeRoot, id, "report-backup"))
                throw new InvalidOperationException("Generate 证据只能来自当前项目固定 GUID/report-backup 布局。");
            return id;
        }

        /// <summary>指针只允许准确版本/GUID和有界摘要；planned=false不要求已经有文件。</summary>
        /// <param name="value">有限小指针。</param>
        /// <param name="requireBefore">读写 final 时必须已经写 before。</param>
        /// <returns>无返回值；缺失、未知或非规范字段值拒绝。</returns>
        private static void Validate(Reference value, bool requireBefore)
        {
            if (value == null || value.version != 1 || value.id?.Length != 32 || !Guid.TryParseExact(value.id, "N", out _) || value.beforeLength < 1 || value.beforeLength > MaxRawBytes || !IsHash(value.beforeSha256) || requireBefore && !value.beforeWritten || value.finalWritten && (value.finalLength < 1 || value.finalLength > MaxRawBytes || !IsHash(value.finalSha256)))
                throw new InvalidOperationException("Generate 完整证据指针无效。");
        }

        /// <summary>只组合固定 before/final 文件名，拒绝其它角色。</summary>
        /// <param name="reference">有限指针。</param>
        /// <param name="phase">源码固定 before 或 final。</param>
        /// <returns>本项目固定 GUID 下的准确文件。</returns>
        private string FilePath(Reference reference, string phase)
        {
            if (phase != "before" && phase != "final") throw new InvalidOperationException("Generate 证据阶段未知。");
            return Path.Combine(DirectoryPath(reference), phase + ".json");
        }

        /// <summary>读前完整拒绝 GUID 树的所有链接，再只读准确长度及 SHA 对应的原文件。</summary>
        /// <param name="reference">原指针。</param>
        /// <param name="phase">固定文件角色。</param>
        /// <param name="length">原始字节长度。</param>
        /// <param name="sha">原始规范 SHA。</param>
        /// <returns>完整原字节；链接、缺失、大小或摘要不符均拒绝。</returns>
        private byte[] ReadChecked(Reference reference, string phase, int length, string sha)
        {
            Guard(DirectoryPath(reference)); string path = FilePath(reference, phase);
            if (length < 1 || length > MaxRawBytes || !File.Exists(path) || new FileInfo(path).Length != length) throw new InvalidOperationException("Generate 证据文件缺失或长度漂移。");
            byte[] bytes = new byte[length];
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length != length) throw new InvalidOperationException("Generate 证据读取前长度变化。");
                int offset = 0;
                while (offset < length) { int read = stream.Read(bytes, offset, length - offset); if (read == 0) throw new InvalidOperationException("Generate 证据读取被截断。"); offset += read; }
                if (stream.ReadByte() != -1 || stream.Length != length) throw new InvalidOperationException("Generate 证据读取中增长，拒绝超界材料。");
            }
            if (Hash(bytes) != sha) throw new InvalidOperationException("Generate 证据文件 SHA 漂移。");
            return bytes;
        }

        /// <summary>只验证准确拥有目录及其现存后代，不扫描其它 Library；悬空链接也拒绝。</summary>
        /// <param name="directory">本实例固定角色目录。</param>
        /// <returns>无返回值；不跟随链接或写入。</returns>
        private void Guard(string directory) => new HybridClrGenerateInputProjection(new HybridClrGenerateInputProjection.Spec { projectRoot = m_Project, directories = new[] { directory }, files = Array.Empty<string>() }, Array.Empty<string>()).ValidateOwnedTrees(System.Threading.CancellationToken.None);

        /// <summary>只在已声明目录内用 CreateNew 临时文件写全字节，再原子发布；不覆盖已有正式文件。</summary>
        /// <param name="path">固定 before/final 文件路径。</param>
        /// <param name="bytes">已通过严格编码和大小门的完整原字节。</param>
        /// <returns>无返回值；失败仅清理本次确实创建的临时文件，保留正式材料。</returns>
        private void WriteAtomic(string path, byte[] bytes)
        {
            Guard(Path.GetDirectoryName(path)); string temporary = path + ".tmp"; bool owned = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { owned = true; stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                File.Move(temporary, path); owned = false;
            }
            finally { if (owned && File.Exists(temporary)) File.Delete(temporary); }
        }

        /// <summary>Plan/WriteBefore保持原完整before raw8MiB硬限，不因行表可复用而接纳原先过大计划。</summary>
        /// <param name="evidence">仅内部Plan/WriteBefore构造的完整before材料。</param>
        /// <returns>与旧编码逐字相同的完整UTF8；超过8MiB时零写拒绝。</returns>
        private static byte[] EncodeBefore(PlayerBuildAction.GenerateEvidence evidence)
        {
            if (evidence?.before == null) throw new InvalidOperationException("Generate 完整 before 缺失。");
            string json = Serialize(evidence); byte[] bytes = Utf8.GetBytes(json);
            if (bytes.Length < 1 || bytes.Length > MaxRawBytes) throw new InvalidOperationException("Generate 完整证据超过 8MiB。");
            ParseObject(json, generateMaterialType: typeof(PlayerBuildAction.GenerateEvidence)); return bytes;
        }

        /// <summary>仅final使用：小于8MiB保持原完整typed字节；超出时仅两个固定files槽尝试无损行表，wire仍8MiB且展开至多16MiB。</summary>
        /// <param name="evidence">完整 before 或实际 final。</param>
        /// <returns>严格 UTF8 字节。</returns>
        private static byte[] Encode(PlayerBuildAction.GenerateEvidence evidence)
        {
            if (evidence?.before == null) throw new InvalidOperationException("Generate 完整 before 缺失。");
            string json = Serialize(evidence); byte[] bytes = Utf8.GetBytes(json);
            if (bytes.Length < 1 || bytes.Length > MaxExpandedEvidenceBytes) throw new InvalidOperationException("Generate 完整证据展开超过 16MiB。");
            JObject material = ParseObject(json, generateMaterialType: typeof(PlayerBuildAction.GenerateEvidence));
            return bytes.Length <= MaxRawBytes ? bytes : EncodeFileRowEvidence(material, bytes);
        }

        /// <summary>只解码固定 DTO，不允许未知字段、类型元数据或执行状态恢复。</summary>
        /// <param name="bytes">摘要已核对的原始 UTF8。</param>
        /// <returns>完整 typed 材料；不是当前环境重捕获。</returns>
        private static PlayerBuildAction.GenerateEvidence Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 1 || bytes.Length > MaxRawBytes) throw new InvalidOperationException("Generate 完整证据wire为空或超过8MiB。");
            JObject material = ParseObjectCore(Utf8.GetString(bytes), false, typeof(PlayerBuildAction.GenerateEvidence), true);
            if (material.Property("fileRowCodec") == null) return material.ToObject<PlayerBuildAction.GenerateEvidence>(JsonSerializer.Create(Settings()));
            if (Serialize(material) != Utf8.GetString(bytes)) throw new InvalidOperationException("Generate v3行表wire不是规范JSON表示。");
            return DecodeFileRowEvidence(material);
        }

        /// <summary>仅对两个固定输入 files 槽建立无损有序行表；不压缩、移除快照或引用外部 before。</summary>
        /// <param name="material">已经过原完整 DTO/SVS 校验的本次独立 JSON 树。</param>
        /// <param name="expanded">原完整 DTO 的规范 UTF8 字节，原证书算法不变。</param>
        /// <returns>最多8MiB的新版本完整 wire；任一预算不足明确拒绝。</returns>
        private static byte[] EncodeFileRowEvidence(JObject material, byte[] expanded)
        {
            var table = new JArray(); var index = new Dictionary<string, int>(StringComparer.Ordinal); int references = 0;
            foreach (JArray slot in FileRowSlots(material))
                for (int row = 0; row < slot.Count; row++)
                {
                    if (++references > MaxFileRowReferences) throw new InvalidOperationException("Generate 行表引用数超过固定预算。");
                    string json = Serialize(slot[row]);
                    if (!index.TryGetValue(json, out int id))
                    {
                        if (table.Count == MaxFileRows) throw new InvalidOperationException("Generate 行表数量超过固定预算。");
                        id = table.Count; index.Add(json, id); table.Add(slot[row].DeepClone());
                    }
                    slot[row] = id;
                }
            material["fileRowCodec"] = FileRowCodec; material["expandedLength"] = expanded.Length;
            material["expandedSha256"] = Hash(expanded); material["fileRows"] = table;
            byte[] bytes = Utf8.GetBytes(Serialize(material));
            if (bytes.Length > MaxRawBytes) throw new InvalidOperationException("Generate 完整证据行表wire超过 8MiB。");
            // 写前复验解码必须逐字还原完整DTO；不是以摘要替代材料，也不改变完整证书的输入。
            if (Serialize(Decode(bytes)) != Utf8.GetString(expanded)) throw new InvalidOperationException("Generate 行表没有逐字还原完整DTO。");
            return bytes;
        }

        /// <summary>仅定位 before.inputs.files 与 afterInputs.files；不搜索同名后代，不扩大可复用槽位。</summary>
        /// <param name="material">本次独立 typed JSON 树。</param>
        /// <returns>按固定先后顺序存在的两个有序数组；null保持原值，陌生形状拒绝。</returns>
        private static JArray[] FileRowSlots(JObject material)
        {
            JToken[] candidates = { material["before"]?["inputs"]?["files"], material["afterInputs"]?["files"] };
            var slots = new List<JArray>();
            foreach (JToken token in candidates)
            {
                if (token == null || token.Type == JTokenType.Null) continue;
                if (!(token is JArray array)) throw new InvalidOperationException("Generate 行表只接受两个准确files数组。");
                slots.Add(array);
            }
            return slots.ToArray();
        }

        /// <summary>先核行表/引用和完整展开预算，再生成原完整 DTO；只处理明确v3，旧裸表示走原解码。</summary>
        /// <param name="material">已严格解析语法/元数据及准确SVS的独立wire树，尚未当作DTO。</param>
        /// <returns>逐字完整原DTO；错误版本、表、索引、长度、SHA或任何未知字段拒绝。</returns>
        private static PlayerBuildAction.GenerateEvidence DecodeFileRowEvidence(JObject material)
        {
            if (material["fileRowCodec"]?.Type != JTokenType.String || material["fileRowCodec"].Value<string>() != FileRowCodec ||
                material["expandedLength"]?.Type != JTokenType.Integer || material["expandedSha256"]?.Type != JTokenType.String ||
                !(material["fileRows"] is JArray table) || table.Count > MaxFileRows)
                throw new InvalidOperationException("Generate 行表版本或必需字段无效。");
            long length = material["expandedLength"].Value<long>(); string expectedSha = material["expandedSha256"].Value<string>();
            if (length < 1 || length > MaxExpandedEvidenceBytes || !IsHash(expectedSha)) throw new InvalidOperationException("Generate 行表展开长度或SHA无效。");
            var texts = new List<string>(); var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken row in table)
            {
                if (row.Type != JTokenType.Null && (!(row is JObject value) || value.Properties().Any(p => (p.Name != "path" && p.Name != "kind" && p.Name != "sha256") || p.Value.Type != JTokenType.String && p.Value.Type != JTokenType.Null)))
                    throw new InvalidOperationException("Generate 行表只接受完整FileState或null。");
                string text = Serialize(row); if (!unique.Add(text)) throw new InvalidOperationException("Generate 行表存在重复定义。"); texts.Add(text);
            }
            material.Remove("fileRowCodec"); material.Remove("expandedLength"); material.Remove("expandedSha256"); material.Remove("fileRows");
            JArray[] slots = FileRowSlots(material); int references = 0, firstUnused = 0; long addedBytes = 0; var seen = new HashSet<int>(); var ids = new List<int[]>();
            foreach (JArray slot in slots)
            {
                if (slot.Count > MaxFileRowReferences - references) throw new InvalidOperationException("Generate 行表引用数超过固定预算。");
                references += slot.Count; var values = new int[slot.Count];
                for (int row = 0; row < slot.Count; row++)
                {
                    if (slot[row].Type != JTokenType.Integer) throw new InvalidOperationException("Generate 行表引用必须是准确整数。");
                    long id = slot[row].Value<long>(); if (id < 0 || id >= table.Count) throw new InvalidOperationException("Generate 行表引用越界。"); values[row] = (int)id;
                    if (seen.Add((int)id) && id != firstUnused++) throw new InvalidOperationException("Generate 行表首次使用顺序无效。");
                    addedBytes = checked(addedBytes + Utf8.GetByteCount(texts[(int)id]));
                    if (addedBytes > MaxExpandedEvidenceBytes) throw new InvalidOperationException("Generate 行表展开预算超限。");
                }
                addedBytes = checked(addedBytes + Math.Max(0, slot.Count - 1)); ids.Add(values); slot.Clear();
            }
            if (firstUnused != table.Count) throw new InvalidOperationException("Generate 行表包含未使用行。");
            // 空数组的两个括号已在skeleton中；先算准所有重复行的完整UTF8再DeepClone，阻止小表放大。
            long predicted = checked(Utf8.GetByteCount(Serialize(material)) + addedBytes);
            if (predicted != length || predicted > MaxExpandedEvidenceBytes) throw new InvalidOperationException("Generate 行表完整展开长度不符或超限。");
            for (int slot = 0; slot < slots.Length; slot++) foreach (int id in ids[slot]) slots[slot].Add(table[id].DeepClone());
            string expanded = Serialize(material); byte[] bytes = Utf8.GetBytes(expanded);
            if (bytes.Length != length || Hash(bytes) != expectedSha) throw new InvalidOperationException("Generate 行表完整展开SHA漂移。");
            var evidence = ParseObject(expanded, generateMaterialType: typeof(PlayerBuildAction.GenerateEvidence)).ToObject<PlayerBuildAction.GenerateEvidence>(JsonSerializer.Create(Settings()));
            if (Serialize(evidence) != expanded) throw new InvalidOperationException("Generate 行表并非规范完整DTO表示。");
            return evidence;
        }

        /// <summary>隔离全局默认 serializer，禁止任意类型与引用元数据。</summary>
        /// <returns>固定严格设置。</returns>
        private static JsonSerializerSettings Settings() => new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MetadataPropertyHandling = MetadataPropertyHandling.Ignore, MissingMemberHandling = MissingMemberHandling.Error, Culture = CultureInfo.InvariantCulture, DateParseHandling = DateParseHandling.None, MaxDepth = 64, Formatting = Formatting.None, NullValueHandling = NullValueHandling.Ignore };

        /// <summary>通过本地固定 serializer 序列化纯 DTO，不执行 vendor 对象回调。</summary>
        /// <param name="value">本合同的纯材料。</param>
        /// <returns>完整 JSON。</returns>
        private static string Serialize(object value)
        {
            using (var output = new StringWriter(CultureInfo.InvariantCulture)) using (var writer = new JsonTextWriter(output)) { JsonSerializer.Create(Settings()).Serialize(writer, value); return output.ToString(); }
        }

        /// <summary>复制有限小指针，防止失败路径改变原计划。</summary>
        /// <param name="value">已验证指针。</param>
        /// <returns>独立值副本。</returns>
        private static Reference Clone(Reference value) => new Reference { version = value.version, id = value.id, beforeWritten = value.beforeWritten, beforeLength = value.beforeLength, beforeSha256 = value.beforeSha256, finalWritten = value.finalWritten, finalLength = value.finalLength, finalSha256 = value.finalSha256 };

        /// <summary>检查规范 SHA。</summary>
        /// <param name="value">摘要文本。</param>
        /// <returns>恰64个小写十六进制字符时为 true。</returns>
        private static bool IsHash(string value) => value?.Length == 64 && value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f');

        /// <summary>计算实际字节摘要。</summary>
        /// <param name="bytes">完整原始字节。</param>
        /// <returns>规范 SHA256。</returns>
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant(); }

        /// <summary>只将数据/IO错误转换为安全失败，不吞掉进程终止或内存耗尽。</summary>
        /// <param name="error">实际异常。</param>
        /// <returns>可作为未完成材料保存的异常时为 true。</returns>
        private static bool IsDataFailure(Exception error) => error is JsonException || error is ArgumentException || error is InvalidOperationException || error is IOException || error is UnauthorizedAccessException || error is FormatException || error is OverflowException;
        /// <summary>保持原严格reader三参数入口与反射调用约定；不允许行表延迟转换。</summary>
        /// <param name="json">有界严格UTF8原文。</param>
        /// <param name="allowPlanEnvelope">仅原预览根封装许可。</param>
        /// <param name="generateMaterialType">仅原两个已审typed用途。</param>
        /// <returns>已完整校验原树；不产生执行许可。</returns>
        private static JObject ParseObject(string json, bool allowPlanEnvelope = false, Type generateMaterialType = null) => ParseObjectCore(json, allowPlanEnvelope, generateMaterialType, false);

        /// <summary>拒绝重复字段、注释、构造器、多个根和过深内容；仅接受一个普通 JSON object。</summary>
        /// <param name="json">有界严格 UTF8 原文。</param>
        /// <param name="allowPlanEnvelope">仅专用预览外壳允许根级codec/gzipBase64字段；内部原计划及其它证据保持false。</param>
        /// <param name="generateMaterialType">仅内部明确的GeneratePlan或GenerateEvidence材料可核固定SVS位置；其它用途保持null及原禁门。</param>
        /// <param name="deferGenerateDto">仅完整证据Decode首次阶段允许文件行索引；仍先核准确SVS/语法/元数据，展开后必须再核完整DTO。</param>
        /// <returns>单个普通对象，拒绝重复、元数据与嵌套封装。</returns>
        private static JObject ParseObjectCore(string json, bool allowPlanEnvelope, Type generateMaterialType, bool deferGenerateDto)
        {
            ValidateUnicodeEscapes(json);
            using (var tokens = new JsonTextReader(new StringReader(json)) { MaxDepth = 64, DateParseHandling = DateParseHandling.None })
                while (tokens.Read())
                {
                    if (tokens.TokenType == JsonToken.PropertyName && tokens.QuoteChar != '"')
                        throw new InvalidOperationException("Generate 完整证据字段名必须使用双引号。");
                    if (tokens.TokenType == JsonToken.Float && tokens.Value is double number && (double.IsNaN(number) || double.IsInfinity(number))) throw new InvalidOperationException("Generate 完整证据拒绝非有限 JSON 数字。");
                    if (tokens.TokenType == JsonToken.Comment || tokens.TokenType == JsonToken.StartConstructor || tokens.TokenType == JsonToken.Undefined)
                        throw new InvalidOperationException("Generate 完整证据包含非普通 JSON token。");
                }
            using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 64, DateParseHandling = DateParseHandling.None })
            {
                JObject value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidOperationException("Generate 完整证据包含多个根对象。");
                JObject[] svsBodies = ValidateGenerateStoredSnapshots(value, generateMaterialType, deferGenerateDto);
                foreach (JToken token in value.DescendantsAndSelf())
                {
                    if (token is JProperty property)
                    {
                        if (property.Name.StartsWith("$", StringComparison.Ordinal) || (property.Name == "codec" || property.Name == "gzipBase64") && (!allowPlanEnvelope || !ReferenceEquals(property.Parent, value)) && (property.Name != "gzipBase64" || !svsBodies.Any(body => ReferenceEquals(property.Parent, body)))) throw new InvalidOperationException("Generate 完整证据禁止类型/引用或嵌套封装元数据。");
                        Utf8.GetByteCount(property.Name);
                    }
                    else if (token is JValue scalar && scalar.Type == JTokenType.String) Utf8.GetByteCount(scalar.Value<string>());
                }
                return value;
            }
        }

        /// <summary>只在两个明确typed用途核四处完整SVS；许可绑定原树节点引用，不能通过同名字段、路径前缀或深相等复制扩张。</summary>
        /// <param name="root">严格语法读取的原始根树。</param><param name="materialType">内部指定的GeneratePlan或GenerateEvidence；null保持其它用途原禁门。</param>
        /// <param name="deferGenerateDto">仅GenerateEvidence解码首次阶段延后files索引的完整DTO转换；SVS校验保持，随后展开再完整校验。</param>
        /// <returns>完整固定wire验证通过的直系body及observation原树对象；不生成Native、执行或恢复许可。</returns>
        private static JObject[] ValidateGenerateStoredSnapshots(JObject root, Type materialType, bool deferGenerateDto = false)
        {
            if (materialType == null) return Array.Empty<JObject>();
            if (materialType != typeof(PlayerBuildAction.GeneratePlan) && materialType != typeof(PlayerBuildAction.GenerateEvidence)) throw new InvalidOperationException("Generate 存储材料用途未审。");
            if (deferGenerateDto && materialType != typeof(PlayerBuildAction.GenerateEvidence)) throw new InvalidOperationException("Generate 行表延迟转换用途未审。");
            if (!deferGenerateDto) root.ToObject(materialType, JsonSerializer.Create(Settings()));
            JToken[] snapshots = materialType == typeof(PlayerBuildAction.GeneratePlan)
                ? new[] { root["sceneVisibilityBefore"] }
                : new[] { root["before"]?["sceneVisibilityBefore"], root["sceneVisibilityAfter"], root["sceneVisibilityObserved"] };
            var bodies = new List<JObject>();
            foreach (JToken snapshot in snapshots)
            {
                if (snapshot == null || snapshot.Type == JTokenType.Null) continue;
                if (!(snapshot is JObject value)) throw new InvalidOperationException("Generate SVS 存储位置必须是完整Snapshot对象。");
                GenerateSceneVisibilityContract.ValidateStoredSnapshot(value);
                bodies.Add((JObject)value["body"]); bodies.Add((JObject)value["observation"]);
            }
            return bodies.ToArray();
        }

        /// <summary>在 Newtonsoft 替换非法代理项前核对 JSON Unicode 转义；合法代理对与转义反斜杠保持原语义。</summary>
        /// <param name="json">原始 JSON 文本。</param>
        /// <returns>无返回值；非法转义拒绝。</returns>
        private static void ValidateUnicodeEscapes(string json)
        {
            bool quoted = false;
            for (int index = 0; index < json.Length; index++)
            {
                if (json[index] == '"') { quoted = !quoted; continue; }
                if (quoted && json[index] < 0x20) throw new InvalidOperationException("Generate 完整证据字符串含未转义控制字符。");
                if (!quoted && json[index] == '\'') throw new InvalidOperationException("Generate 完整证据不接受单引号 JSON 字符串。");
                if (!quoted && json[index] == ',')
                {
                    int next = index + 1;
                    while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                    if (next < json.Length && (json[next] == '}' || json[next] == ']'))
                        throw new InvalidOperationException("Generate 完整证据不接受尾随逗号。");
                }
                if (!quoted || json[index] != '\\' || index + 1 >= json.Length) continue;
                if (json[index + 1] != 'u') { index++; continue; }
                if (index + 6 > json.Length || !int.TryParse(json.Substring(index + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                    throw new InvalidOperationException("Generate 完整证据 Unicode 转义损坏。");
                if (code >= 0xdc00 && code <= 0xdfff) throw new InvalidOperationException("Generate 完整证据含未配对的低代理项。");
                if (code >= 0xd800 && code <= 0xdbff)
                {
                    if (index + 12 > json.Length || json[index + 6] != '\\' || json[index + 7] != 'u' ||
                        !int.TryParse(json.Substring(index + 8, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int low) || low < 0xdc00 || low > 0xdfff)
                        throw new InvalidOperationException("Generate 完整证据含未配对的高代理项。");
                    index += 11;
                }
                else index += 5;
            }
        }

    }
}
