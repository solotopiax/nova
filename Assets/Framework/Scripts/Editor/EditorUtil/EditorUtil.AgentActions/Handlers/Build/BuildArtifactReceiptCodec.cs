/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BuildArtifactReceiptCodec.cs
 * author:    taoye
 * created:   2026/10/15
 * descrip:   构建完整产物回执的有界压缩与旧版兼容解码
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
    /// <summary>只压缩构建内部 Payload，不改变公共信封、OperationStore 上限或回执证据。</summary>
    internal static class BuildArtifactReceiptCodec
    {
        internal const int MaxRawBytes = 8 * 1024 * 1024;
        internal const int MaxCompressedBytes = 128 * 1024;
        internal const int MaxEncodedBytes = 192 * 1024;
        private const string c_Codec = "nova-build-artifact-gzip";
        private const int c_Version = 1;
        private const int c_MaxDepth = 64;
        private static readonly UTF8Encoding s_Utf8 = new UTF8Encoding(false, true);
        private static readonly string[] s_Fields = { "codec", "version", "rawLength", "rawSha256", "gzipBase64" };

        /// <summary>序列化完整类型化回执并压缩；超限或非严格 UTF-8 时失败，不截断、落盘或修改原回执。</summary>
        internal static string Encode(BuildActionCommon.ArtifactReceipt receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            string json = Serialize(receipt);
            byte[] raw = s_Utf8.GetBytes(json);
            if (raw.Length == 0 || raw.Length > MaxRawBytes) throw new InvalidOperationException("构建回执原始 UTF-8 超过有界大小。");
            ParseObject(json);
            byte[] compressed;
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true)) gzip.Write(raw, 0, raw.Length);
                compressed = output.ToArray();
            }
            if (compressed.Length > MaxCompressedBytes) throw new InvalidOperationException("构建回执压缩结果超过有界大小。");
            var wrapper = new JObject
            {
                ["codec"] = c_Codec,
                ["version"] = c_Version,
                ["rawLength"] = raw.Length,
                ["rawSha256"] = Hash(raw),
                ["gzipBase64"] = Convert.ToBase64String(compressed),
            };
            string encoded = Serialize(wrapper);
            if (s_Utf8.GetByteCount(encoded) > MaxEncodedBytes) throw new InvalidOperationException("构建回执封装超过有界大小。");
            return encoded;
        }

        /// <summary>供 Plan 与失败恢复路径安全封装；无法完整保存时返回错误，不让 catch 再次抛出或截断证据。</summary>
        internal static bool TryEncode(BuildActionCommon.ArtifactReceipt receipt, out string payload, out string error)
        {
            payload = null;
            try
            {
                payload = Encode(receipt);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException || exception is InvalidOperationException || exception is IOException)
            {
                error = "构建回执编码失败：" + exception.Message;
                return false;
            }
        }

        /// <summary>严格解码新封装或旧普通回执；失败返回空回执，不恢复执行状态、不读取文件或提升 finalized。</summary>
        internal static bool TryDecode(string payload, out BuildActionCommon.ArtifactReceipt receipt, out string error)
        {
            receipt = null;
            try
            {
                if (string.IsNullOrWhiteSpace(payload) || payload.Length > MaxRawBytes || s_Utf8.GetByteCount(payload) > MaxRawBytes)
                    throw new InvalidOperationException("构建回执为空或超过原始输入上限。");
                JObject value = ParseObject(payload);
                if (value.Properties().Any(property => s_Fields.Contains(property.Name, StringComparer.Ordinal)))
                {
                    if (s_Utf8.GetByteCount(payload) > MaxEncodedBytes || value.Count != s_Fields.Length ||
                        value.Properties().Any(property => !s_Fields.Contains(property.Name, StringComparer.Ordinal)))
                        throw new InvalidOperationException("构建回执压缩封装含缺失或未知字段，或超过封装上限。");
                    if (value["codec"]?.Type != JTokenType.String || value["codec"].Value<string>() != c_Codec ||
                        value["version"]?.Type != JTokenType.Integer || value["version"].Value<long>() != c_Version ||
                        value["rawLength"]?.Type != JTokenType.Integer)
                        throw new InvalidOperationException("构建回执压缩格式、版本或字段类型无效。");
                    long declaredLength = value["rawLength"].Value<long>();
                    if (declaredLength < 1 || declaredLength > MaxRawBytes)
                        throw new InvalidOperationException("构建回执声明的解压长度超过有界大小。");
                    string hash = ReadString(value, "rawSha256"), base64 = ReadString(value, "gzipBase64");
                    if (hash.Length != 64 || hash.Any(character => !((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))))
                        throw new InvalidOperationException("构建回执 SHA-256 格式无效。");
                    if (base64.Length == 0 || base64.Length > ((MaxCompressedBytes + 2) / 3) * 4 || base64.Any(char.IsWhiteSpace))
                        throw new InvalidOperationException("构建回执 Base64 为空、超限或含空白。");
                    byte[] compressed = Convert.FromBase64String(base64);
                    if (compressed.Length > MaxCompressedBytes || Convert.ToBase64String(compressed) != base64)
                        throw new InvalidOperationException("构建回执 Base64 不是有界规范编码。");
                    byte[] raw = Decompress(compressed, (int)declaredLength);
                    if (Hash(raw) != hash) throw new InvalidOperationException("构建回执原始 SHA-256 不匹配。");
                    value = ParseObject(s_Utf8.GetString(raw));
                    if (value.Properties().Any(property => s_Fields.Contains(property.Name, StringComparer.Ordinal)))
                        throw new InvalidOperationException("构建回执不允许嵌套压缩封装。");
                }
                // 旧回执保留 Newtonsoft 的普通字段兼容；类型元数据不会被解释成可实例化类型。
                receipt = value.ToObject<BuildActionCommon.ArtifactReceipt>(JsonSerializer.Create(CreateSettings()));
                if (receipt == null) throw new InvalidOperationException("构建回执根对象无法解析。");
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException ||
                exception is InvalidOperationException || exception is IOException || exception is InvalidDataException || exception is OverflowException || exception is FormatException)
            {
                receipt = null;
                error = "构建回执解码失败：" + exception.Message;
                return false;
            }
        }

        /// <summary>固定内部 JSON 策略，隔离进程默认设置并禁止 TypeNameHandling 与外部类型实例化。</summary>
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
        private static string Serialize(object value)
        {
            using (var output = new StringWriter(CultureInfo.InvariantCulture))
            using (var writer = new JsonTextWriter(output))
            {
                JsonSerializer.Create(CreateSettings()).Serialize(writer, value);
                return output.ToString();
            }
        }

        /// <summary>拒绝重复字段、注释、构造器、多个根和过深内容；仅接受一个普通 JSON object。</summary>
        private static JObject ParseObject(string json)
        {
            ValidateUnicodeEscapes(json);
            using (var tokens = new JsonTextReader(new StringReader(json)) { MaxDepth = c_MaxDepth, DateParseHandling = DateParseHandling.None })
                while (tokens.Read())
                {
                    if (tokens.TokenType == JsonToken.PropertyName && tokens.QuoteChar != '"')
                        throw new InvalidOperationException("构建回执字段名必须使用双引号。");
                    if (tokens.TokenType == JsonToken.Comment || tokens.TokenType == JsonToken.StartConstructor || tokens.TokenType == JsonToken.Undefined)
                        throw new InvalidOperationException("构建回执包含非普通 JSON token。");
                }
            using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = c_MaxDepth, DateParseHandling = DateParseHandling.None })
            {
                JObject value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidOperationException("构建回执包含多个根对象。");
                foreach (JToken token in value.DescendantsAndSelf())
                {
                    if (token is JProperty property) s_Utf8.GetByteCount(property.Name);
                    else if (token is JValue scalar && scalar.Type == JTokenType.String) s_Utf8.GetByteCount(scalar.Value<string>());
                }
                return value;
            }
        }

        /// <summary>在 Newtonsoft 替换非法代理项前核对 JSON Unicode 转义；合法代理对与转义反斜杠保持原语义。</summary>
        private static void ValidateUnicodeEscapes(string json)
        {
            bool quoted = false;
            for (int index = 0; index < json.Length; index++)
            {
                if (json[index] == '"') { quoted = !quoted; continue; }
                if (!quoted && json[index] == '\'') throw new InvalidOperationException("构建回执不接受单引号 JSON 字符串。");
                if (!quoted && json[index] == ',')
                {
                    int next = index + 1;
                    while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                    if (next < json.Length && (json[next] == '}' || json[next] == ']'))
                        throw new InvalidOperationException("构建回执不接受尾随逗号。");
                }
                if (!quoted || json[index] != '\\' || index + 1 >= json.Length) continue;
                if (json[index + 1] != 'u') { index++; continue; }
                if (index + 6 > json.Length || !int.TryParse(json.Substring(index + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                    throw new InvalidOperationException("构建回执 Unicode 转义损坏。");
                if (code >= 0xdc00 && code <= 0xdfff) throw new InvalidOperationException("构建回执含未配对的低代理项。");
                if (code >= 0xd800 && code <= 0xdbff)
                {
                    if (index + 12 > json.Length || json[index + 6] != '\\' || json[index + 7] != 'u' ||
                        !int.TryParse(json.Substring(index + 8, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int low) || low < 0xdc00 || low > 0xdfff)
                        throw new InvalidOperationException("构建回执含未配对的高代理项。");
                    index += 11;
                }
                else index += 5;
            }
        }

        /// <summary>只读取封装内明确声明的字符串字段，不把数字、数组或 null 隐式转换为字符串。</summary>
        private static string ReadString(JObject wrapper, string name)
        {
            if (wrapper[name]?.Type != JTokenType.String) throw new InvalidOperationException("构建回执字段不是字符串：" + name);
            return wrapper[name].Value<string>();
        }

        /// <summary>解析一个无可选头的 gzip member；逐字节馈入以核对精确 DEFLATE 终点，拒绝尾随或串联内容。</summary>
        private static byte[] Decompress(byte[] compressed, int rawLength)
        {
            if (compressed.Length < 18 || compressed[0] != 0x1f || compressed[1] != 0x8b || compressed[2] != 8 || compressed[3] != 0)
                throw new InvalidOperationException("构建回执 gzip 头损坏或使用未声明的可选字段。");
            if (ReadUInt32(compressed, compressed.Length - 4) != (uint)rawLength)
                throw new InvalidOperationException("构建回执 gzip 长度尾部与声明不一致。");
            byte[] raw;
            using (var source = new ExactDeflateInput(compressed))
            using (var inflate = new DeflateStream(source, CompressionMode.Decompress, true))
            using (var output = new MemoryStream(Math.Min(rawLength, 8192)))
            {
                var buffer = new byte[8192];
                int count;
                while ((count = inflate.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + count > rawLength) throw new InvalidOperationException("构建回执解压内容超过声明长度。");
                    output.Write(buffer, 0, count);
                }
                if (output.Length != rawLength || source.Position != source.Length || source.EndWasRequested)
                    throw new InvalidOperationException("构建回执 gzip 被截断或包含额外 member/尾随字节。");
                raw = output.ToArray();
            }
            if (ReadUInt32(compressed, compressed.Length - 8) != Crc32(raw))
                throw new InvalidOperationException("构建回执 gzip CRC-32 不匹配。");
            return raw;
        }

        /// <summary>按 gzip 固定小端读取尾部校验值，不依赖本机字节序。</summary>
        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)bytes[offset] | (uint)bytes[offset + 1] << 8 | (uint)bytes[offset + 2] << 16 | (uint)bytes[offset + 3] << 24;
        }

        /// <summary>核验 gzip 原始内容校验和；SHA-256 仍单独绑定完整原始 UTF-8。</summary>
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
            internal ExactDeflateInput(byte[] source) { bytes = source; }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => bytes.Length - 18;
            public override long Position { get => position; set => throw new NotSupportedException(); }
            internal bool EndWasRequested { get; private set; }

            /// <summary>每次最多返回一个压缩字节，使当前位置精确表示实际馈入的 DEFLATE 内容。</summary>
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
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            /// <summary>不允许扩大有界压缩输入。</summary>
            public override void SetLength(long value) => throw new NotSupportedException();
            /// <summary>不允许写入压缩输入。</summary>
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
