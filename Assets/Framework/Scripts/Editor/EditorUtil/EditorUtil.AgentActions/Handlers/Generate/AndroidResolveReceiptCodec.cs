/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveReceiptCodec.cs
 * author:    taoye
 * created:   2026/10/15
 * descrip:   Android依赖Action完整回执的有界无损表示与旧版兼容解码
 ***************************************************************/

using System;
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
    /// <summary>仅供Android依赖Action封装完整内部Payload；不改变逻辑快照、校验器、通用存储或传输上限。</summary>
    internal static class AndroidResolveReceiptCodec
    {
        // 原文物理界沿用已验证构建codec；完整gzip、封装和Operation预算独立核对，不以原文界代替执行许可。
        internal const int MaxRawBytes = 8 * 1024 * 1024;
        internal const int MaxCompressedBytes = 128 * 1024;
        internal const int MaxEncodedBytes = 192 * 1024;
        private const string c_Codec = "nova-android-resolve-receipt-gzip";
        private const int c_Version = 1;
        private const int c_MaxDepth = 64;
        private static readonly UTF8Encoding s_Utf8 = new UTF8Encoding(false, true);
        private static readonly string[] s_Fields = { "codec", "version", "rawLength", "rawSha256", "gzipBase64" };

        /// <summary>完整原回执JSON无损压缩；不重排字段、不改缩进、字符串或原逻辑摘要，超限时拒绝。</summary>
        /// <param name="receiptJson">原Action完整序列化回执，不包含通用Receipt外层。</param>
        /// <returns>含长度、完整SHA和规范gzip的有界专用封装。</returns>
        internal static string Encode(string receiptJson)
        {
            if (receiptJson == null) throw new ArgumentNullException(nameof(receiptJson));
            string json = receiptJson;
            byte[] raw = s_Utf8.GetBytes(json);
            if (raw.Length == 0 || raw.Length > MaxRawBytes) throw new InvalidOperationException($"Android依赖回执raw层：actualBytes={raw.Length}，limitBytes={MaxRawBytes}。");
            JObject original = ParseObject(json);
            if (IsEnvelopeCandidate(original))
                throw new InvalidOperationException("Android依赖回执不能重复封装。");
            byte[] compressed;
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true)) gzip.Write(raw, 0, raw.Length);
                compressed = output.ToArray();
            }
            if (compressed.Length > MaxCompressedBytes) throw new InvalidOperationException($"Android依赖回执gzip层：actualBytes={compressed.Length}，limitBytes={MaxCompressedBytes}。");
            var wrapper = new JObject
            {
                ["codec"] = c_Codec,
                ["version"] = c_Version,
                ["rawLength"] = raw.Length,
                ["rawSha256"] = Hash(raw),
                ["gzipBase64"] = Convert.ToBase64String(compressed),
            };
            string encoded = Serialize(wrapper);
            int encodedBytes = s_Utf8.GetByteCount(encoded);
            if (encodedBytes > MaxEncodedBytes) throw new InvalidOperationException($"Android依赖回执encoded层：actualBytes={encodedBytes}，limitBytes={MaxEncodedBytes}。");
            return encoded;
        }

        /// <summary>供 Plan 与失败恢复路径安全封装；无法完整保存时返回错误，不让 catch 再次抛出或截断证据。</summary>
        /// <param name="receiptJson">完整原始回执JSON。</param>
        /// <param name="payload">成功时的完整专用封装；失败为null。</param>
        /// <param name="error">容量或数据完整性错误。</param>
        /// <returns>完整无损编码成功时为true。</returns>
        internal static bool TryEncode(string receiptJson, out string payload, out string error)
        {
            payload = null;
            try
            {
                payload = Encode(receiptJson);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException || exception is InvalidOperationException || exception is IOException)
            {
                error = "Android依赖回执编码失败：" + exception.Message;
                return false;
            }
        }

        /// <summary>严格解码新封装或旧普通回执并逐字还原原JSON；不恢复执行状态、不读取文件或提升成功标记。</summary>
        /// <param name="payload">本Action的新专用封装或旧普通回执。</param>
        /// <param name="receiptJson">通过完整校验的原JSON；失败为null。</param>
        /// <param name="error">严格数据拒绝原因。</param>
        /// <returns>原文已完整恢复时为true，不表示领域验收成功。</returns>
        internal static bool TryDecode(string payload, out string receiptJson, out string error)
        {
            receiptJson = null;
            try
            {
                int inputBytes = payload == null ? 0 : s_Utf8.GetByteCount(payload);
                if (string.IsNullOrWhiteSpace(payload) || inputBytes > MaxRawBytes)
                    throw new InvalidOperationException($"Android依赖回执input层：actualBytes={inputBytes}，limitBytes={MaxRawBytes}。");
                JObject value = ParseObject(payload);
                string decodedJson = payload;
                if (IsEnvelopeCandidate(value))
                {
                    if (inputBytes > MaxEncodedBytes)
                        throw new InvalidOperationException($"Android依赖回执encoded层：actualBytes={inputBytes}，limitBytes={MaxEncodedBytes}。");
                    if (value.Count != s_Fields.Length ||
                        value.Properties().Any(property => !s_Fields.Contains(property.Name, StringComparer.Ordinal)))
                        throw new InvalidOperationException("Android依赖回执压缩封装含缺失或未知字段，或超过封装上限。");
                    if (value["codec"]?.Type != JTokenType.String || value["codec"].Value<string>() != c_Codec ||
                        value["version"]?.Type != JTokenType.Integer || value["version"].Value<long>() != c_Version ||
                        value["rawLength"]?.Type != JTokenType.Integer)
                        throw new InvalidOperationException("Android依赖回执压缩格式、版本或字段类型无效。");
                    long declaredLength = value["rawLength"].Value<long>();
                    if (declaredLength < 1 || declaredLength > MaxRawBytes)
                        throw new InvalidOperationException($"Android依赖回执raw声明层：declaredBytes={declaredLength}，limitBytes={MaxRawBytes}。");
                    string hash = ReadString(value, "rawSha256"), base64 = ReadString(value, "gzipBase64");
                    if (hash.Length != 64 || hash.Any(character => !((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))))
                        throw new InvalidOperationException("Android依赖回执 SHA-256 格式无效。");
                    if (base64.Length == 0 || base64.Length > ((MaxCompressedBytes + 2) / 3) * 4 || base64.Any(char.IsWhiteSpace))
                        throw new InvalidOperationException("Android依赖回执 Base64 为空、超限或含空白。");
                    byte[] compressed = Convert.FromBase64String(base64);
                    if (compressed.Length > MaxCompressedBytes)
                        throw new InvalidOperationException($"Android依赖回执gzip层：actualBytes={compressed.Length}，limitBytes={MaxCompressedBytes}。");
                    if (Convert.ToBase64String(compressed) != base64)
                        throw new InvalidOperationException("Android依赖回执 Base64 不是规范编码。");
                    byte[] raw = Decompress(compressed, (int)declaredLength);
                    if (Hash(raw) != hash) throw new InvalidOperationException("Android依赖回执原始 SHA-256 不匹配。");
                    decodedJson = s_Utf8.GetString(raw);
                    value = ParseObject(decodedJson);
                    if (IsEnvelopeCandidate(value))
                        throw new InvalidOperationException("Android依赖回执不允许嵌套压缩封装。");
                }
                // 仅返回完整普通JSON；原Action继续按原DTO与领域校验器解释，不在编解码层创建业务实例。
                receiptJson = decodedJson;
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException ||
                exception is InvalidOperationException || exception is IOException || exception is InvalidDataException || exception is OverflowException || exception is FormatException)
            {
                receiptJson = null;
                error = "Android依赖回执解码失败：" + exception.Message;
                return false;
            }
        }

        /// <summary>按通用信封和完整Operation外层预估本Action计划预算；不写Store，不增加通用容量。</summary>
        /// <param name="encodedReceipt">已完整编码的本Action回执。</param>
        /// <param name="writeSet">与正式Plan相同的完整写入集合。</param>
        /// <param name="error">超过局部保守预算或材料错误的原因。</param>
        /// <returns>完整外层低于192KiB局部预算时为true；通用Store仍保持256KiB。</returns>
        internal static bool TryRequireOperationBudget(string encodedReceipt, string[] writeSet, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrEmpty(encodedReceipt) || writeSet == null)
                    throw new InvalidOperationException("Android依赖回执缺完整编码或写集。");
                string envelope = NovaFramework.Runtime.Util.Json.Serialize(new AgentActionReceiptEnvelope
                { ActionId = "nova.project.android.resolve-dependencies", ContractMajor = 2, PayloadJson = encodedReceipt });
                var record = new AgentActionOperationRecord
                {
                    OperationId = new string('a', 32), ActionId = "nova.project.android.resolve-dependencies", ContractMajor = 2,
                    RegistryGeneration = long.MaxValue, Status = "planned", CreatedAtUtc = "2026-10-19T00:00:00.0000000Z",
                    UpdatedAtUtc = "2026-10-19T00:00:00.0000000Z", RequestSha256 = new string('a', 64), WriteSet = writeSet,
                    ReceiptJson = envelope,
                };
                int recordBytes = s_Utf8.GetByteCount(JsonConvert.SerializeObject(record, Formatting.None));
                if (recordBytes > 192 * 1024)
                    throw new InvalidOperationException($"Android依赖回执record层：actualBytes={recordBytes}，limitBytes={192 * 1024}，storeLimitBytes={256 * 1024}。");
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException || exception is InvalidOperationException)
            { error = "Android依赖回执Operation预算失败：" + exception.Message; return false; }
        }

        /// <summary>保留公开DataJson原对象或数组结构，只对其紧凑wire表示设局部预算，为2MiB通用响应保留外层余量。</summary>
        /// <param name="dataJson">原Action完整公开展示JSON，不会改写、裁剪或变为opaque对象。</param>
        /// <param name="error">实际wire数据字节数、局部界和通用响应界；不包含正文。</param>
        /// <returns>公开数据可在1536KiB局部预算完整表达时为true。</returns>
        internal static bool TryRequirePublicDataBudget(string dataJson, out string error)
        {
            error = null;
            try
            {
                int bytes = s_Utf8.GetByteCount(JToken.Parse(dataJson).ToString(Formatting.None));
                if (bytes > 1536 * 1024)
                    throw new InvalidOperationException($"Android依赖公开wire数据层：actualBytes={bytes}，limitBytes={1536 * 1024}，responseLimitBytes={2 * 1024 * 1024}。");
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException || exception is InvalidOperationException)
            { error = "Android依赖公开数据预算失败：" + exception.Message; return false; }
        }

        /// <summary>只以封装独有字段识别codec；Snapshot自身的version是领域对象，不能误认为重复封装。</summary>
        /// <param name="value">已经严格解析的完整根对象。</param>
        /// <returns>出现codec、原文长度/摘要或gzip字段时为true；真正封装仍须另验全部五字段和版本。</returns>
        private static bool IsEnvelopeCandidate(JObject value)
        {
            return value.Properties().Any(property => property.Name != "version" && s_Fields.Contains(property.Name, StringComparer.Ordinal));
        }

        /// <summary>固定内部 JSON 策略，隔离进程默认设置并禁止 TypeNameHandling 与外部类型实例化。</summary>
        /// <returns>仅用于本Action封装与预算JSON的独立设置。</returns>
        private static JsonSerializerSettings CreateSettings()
        {
            return new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                DateParseHandling = DateParseHandling.None,
                Culture = CultureInfo.InvariantCulture,
                MaxDepth = c_MaxDepth,
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
            };
        }

        /// <summary>通过显式创建的 serializer 输出 JSON，避免 JsonConvert.DefaultSettings 注入额外转换器或策略。</summary>
        /// <param name="value">本地封装或纯Operation预算DTO，不读取运行时环境。</param>
        /// <returns>完整紧凑JSON。</returns>
        private static string Serialize(object value)
        {
            using (var output = new StringWriter(CultureInfo.InvariantCulture))
            using (var writer = new JsonTextWriter(output))
            {
                JsonSerializer.Create(CreateSettings()).Serialize(writer, value);
                return output.ToString();
            }
        }

        /// <summary>拒绝重复字段、注释、构造器、多个根和过深内容；仅接受一个普通完整回执对象。</summary>
        /// <param name="json">完整回执JSON；不解释SDK或用户类型元数据。</param>
        /// <returns>只用于完整性校验的普通对象树。</returns>
        private static JObject ParseObject(string json)
        {
            ValidateUnicodeEscapes(json);
            using (var tokens = new JsonTextReader(new StringReader(json)) { MaxDepth = c_MaxDepth, DateParseHandling = DateParseHandling.None })
                while (tokens.Read())
                {
                    if (tokens.TokenType == JsonToken.PropertyName && tokens.QuoteChar != '"')
                        throw new InvalidOperationException("Android依赖回执字段名必须使用双引号。");
                    if (tokens.TokenType == JsonToken.Comment || tokens.TokenType == JsonToken.StartConstructor || tokens.TokenType == JsonToken.Undefined)
                        throw new InvalidOperationException("Android依赖回执包含非普通 JSON token。");
                }
            using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = c_MaxDepth, DateParseHandling = DateParseHandling.None })
            {
                JObject value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidOperationException("Android依赖回执包含多个根对象。");
                foreach (JToken token in value.DescendantsAndSelf())
                {
                    if (token is JProperty property) s_Utf8.GetByteCount(property.Name);
                    else if (token is JValue scalar && scalar.Type == JTokenType.String) s_Utf8.GetByteCount(scalar.Value<string>());
                }
                return value;
            }
        }

        /// <summary>在 Newtonsoft 替换非法代理项前核对 JSON Unicode 转义；合法代理对与转义反斜杠保持原语义。</summary>
        /// <param name="json">完整原始JSON文本，不在此处改写。</param>
        private static void ValidateUnicodeEscapes(string json)
        {
            bool quoted = false;
            for (int index = 0; index < json.Length; index++)
            {
                if (json[index] == '"') { quoted = !quoted; continue; }
                if (!quoted && json[index] == '\'') throw new InvalidOperationException("Android依赖回执不接受单引号 JSON 字符串。");
                if (!quoted && json[index] == ',')
                {
                    int next = index + 1;
                    while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                    if (next < json.Length && (json[next] == '}' || json[next] == ']'))
                        throw new InvalidOperationException("Android依赖回执不接受尾随逗号。");
                }
                if (!quoted || json[index] != '\\' || index + 1 >= json.Length) continue;
                if (json[index + 1] != 'u') { index++; continue; }
                if (index + 6 > json.Length || !int.TryParse(json.Substring(index + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                    throw new InvalidOperationException("Android依赖回执 Unicode 转义损坏。");
                if (code >= 0xdc00 && code <= 0xdfff) throw new InvalidOperationException("Android依赖回执含未配对的低代理项。");
                if (code >= 0xd800 && code <= 0xdbff)
                {
                    if (index + 12 > json.Length || json[index + 6] != '\\' || json[index + 7] != 'u' ||
                        !int.TryParse(json.Substring(index + 8, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int low) || low < 0xdc00 || low > 0xdfff)
                        throw new InvalidOperationException("Android依赖回执含未配对的高代理项。");
                    index += 11;
                }
                else index += 5;
            }
        }

        /// <summary>只读取封装内明确声明的字符串字段，不把数字、数组或 null 隐式转换为字符串。</summary>
        /// <param name="wrapper">已经通过固定封装字段集合检查的对象。</param>
        /// <param name="name">固定已知字符串字段名。</param>
        /// <returns>原字符串，不作隐式标量转换。</returns>
        private static string ReadString(JObject wrapper, string name)
        {
            if (wrapper[name]?.Type != JTokenType.String) throw new InvalidOperationException("Android依赖回执字段不是字符串：" + name);
            return wrapper[name].Value<string>();
        }

        /// <summary>解析一个无可选头的 gzip member；逐字节馈入以核对精确 DEFLATE 终点，拒绝尾随或串联内容。</summary>
        /// <param name="compressed">有界且规范Base64解出的gzip字节。</param>
        /// <param name="rawLength">已核原文长度，解压中不得超过。</param>
        /// <returns>完整原文；任何尾随、串联、CRC或长度变化均抛错。</returns>
        private static byte[] Decompress(byte[] compressed, int rawLength)
        {
            if (compressed.Length < 18 || compressed[0] != 0x1f || compressed[1] != 0x8b || compressed[2] != 8 || compressed[3] != 0)
                throw new InvalidOperationException("Android依赖回执 gzip 头损坏或使用未声明的可选字段。");
            if (ReadUInt32(compressed, compressed.Length - 4) != (uint)rawLength)
                throw new InvalidOperationException("Android依赖回执 gzip 长度尾部与声明不一致。");
            byte[] raw;
            using (var source = new ExactDeflateInput(compressed))
            using (var inflate = new DeflateStream(source, CompressionMode.Decompress, true))
            using (var output = new MemoryStream(Math.Min(rawLength, 8192)))
            {
                var buffer = new byte[8192];
                int count;
                while ((count = inflate.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + count > rawLength) throw new InvalidOperationException("Android依赖回执解压内容超过声明长度。");
                    output.Write(buffer, 0, count);
                }
                if (output.Length != rawLength || source.Position != source.Length || source.EndWasRequested)
                    throw new InvalidOperationException("Android依赖回执 gzip 被截断或包含额外 member/尾随字节。");
                raw = output.ToArray();
            }
            if (ReadUInt32(compressed, compressed.Length - 8) != Crc32(raw))
                throw new InvalidOperationException("Android依赖回执 gzip CRC-32 不匹配。");
            return raw;
        }

        /// <summary>按 gzip 固定小端读取尾部校验值，不依赖本机字节序。</summary>
        /// <param name="bytes">已核头尾长度的完整gzip字节。</param>
        /// <param name="offset">四字节小端字段起点。</param>
        /// <returns>不依赖主机字节序的无符号值。</returns>
        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)bytes[offset] | (uint)bytes[offset + 1] << 8 | (uint)bytes[offset + 2] << 16 | (uint)bytes[offset + 3] << 24;
        }

        /// <summary>核验 gzip 原始内容校验和；SHA-256 仍单独绑定完整原始 UTF-8。</summary>
        /// <param name="bytes">完整原始字节。</param>
        /// <returns>gzip规定的CRC32。</returns>
        private static uint Crc32(byte[] bytes)
        {
            uint crc = uint.MaxValue;
            foreach (byte value in bytes)
            {
                crc ^= value;
                for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? crc >> 1 ^ 0xedb88320u : crc >> 1;
            }
            return ~crc;
        }

        /// <summary>计算原始 UTF-8 的完整 SHA-256，输出规范小写十六进制。</summary>
        /// <param name="bytes">需要完整绑定的原始字节。</param>
        /// <returns>规范小写64字符十六进制SHA。</returns>
        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>只暴露 gzip 头尾之间的 DEFLATE，避免解压器预读后掩盖未消费的尾随内容。</summary>
        private sealed class ExactDeflateInput : Stream
        {
            private readonly byte[] bytes;
            private int position;

            /// <summary>建立只读有界窗口；调用方已验证 gzip 的固定头与尾长度。</summary>
            /// <param name="source">只读借用的完整gzip数组，窗口不修改或拥有外部资源。</param>
            internal ExactDeflateInput(byte[] source) { bytes = source; }
            /// <summary>只提供按顺序读取的窗口。</summary>
            public override bool CanRead => true;
            /// <summary>禁止改变读取位置。</summary>
            public override bool CanSeek => false;
            /// <summary>禁止修改原压缩数组。</summary>
            public override bool CanWrite => false;
            /// <summary>仅统计固定头尾之间的DEFLATE字节。</summary>
            public override long Length => bytes.Length - 18;
            /// <summary>返回实际消费字节数；设置位置始终拒绝。</summary>
            public override long Position { get => position; set => throw new NotSupportedException(); }
            /// <summary>记录解压器是否请求过窗口外字节，供完整性检查拒绝预读掩盖。</summary>
            internal bool EndWasRequested { get; private set; }

            /// <summary>每次最多返回一个压缩字节，使当前位置精确表示实际馈入的 DEFLATE 内容。</summary>
            /// <param name="buffer">接收最多一个压缩字节的调用方数组。</param>
            /// <param name="offset">目标数组起点。</param>
            /// <param name="count">最大读取字节数，零值不推进。</param>
            /// <returns>实际返回零或一个字节。</returns>
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException(nameof(offset));
                if (count == 0) return 0;
                if (position == Length) { EndWasRequested = true; return 0; }
                buffer[offset] = bytes[10 + position++];
                return 1;
            }

            /// <summary>只读内存流无需 Flush，不向外部文件提交内容。</summary>
            public override void Flush() { }
            /// <summary>不允许修改解压窗口或绕过精确消费顺序。</summary>
            /// <param name="offset">拒绝解释的偏移。</param>
            /// <param name="origin">拒绝解释的位置模式。</param>
            /// <returns>始终抛出不支持异常。</returns>
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            /// <summary>不允许扩大有界压缩输入。</summary>
            /// <param name="value">拒绝应用的新长度。</param>
            public override void SetLength(long value) => throw new NotSupportedException();
            /// <summary>不允许写入压缩输入。</summary>
            /// <param name="buffer">不会读取或写入的调用方数据。</param>
            /// <param name="offset">不会应用的偏移。</param>
            /// <param name="count">不会应用的数量。</param>
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
