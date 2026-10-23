/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IapProductCatalogReceiptStore.cs
 * author:    Codex
 * created:   2026/10/22
 * descrip:   IAP 商品目录一次提交的有界意图与结果记录
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Editor
{
    /// <summary>保存一次提交的脱敏意图；没有结果不能推断远端没有写入，记录永不驱动重发。</summary>
    internal sealed class IapProductCatalogReceiptStore
    {
        internal const string Root = "Library/Nova/AgentActions/IapCatalogUploads";
        internal const int MaxBytes = 32 * 1024;
        private static readonly UTF8Encoding s_Utf8 = new UTF8Encoding(false, true);
        private readonly string m_Root;

        /// <summary>选择固定生产目录，或由内部测试提供同一受控 Library 域内的独立目录。</summary>
        internal IapProductCatalogReceiptStore(string root = Root)
        {
            if (root != Root)
            {
                string prefix = Root + ".Tests/";
                string suffix = root != null && root.StartsWith(prefix, StringComparison.Ordinal) ? root.Substring(prefix.Length) : null;
                if (!Guid.TryParseExact(suffix, "N", out Guid id) || id.ToString("N") != suffix)
                    throw new InvalidOperationException("IAP 回执目录不属于当前受控域。");
            }
            m_Root = root;
        }

        /// <summary>在网络调用前独占创建意图并强制落盘；已有记录或保存失败时不得发送。</summary>
        internal string CreateIntent(string attemptId, string snapshotHash)
        {
            string path = Resolve(attemptId);
            if (!IsHash(snapshotHash)) throw new InvalidOperationException("IAP 冻结摘要无效。");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            path = Resolve(attemptId);
            var record = new JObject
            {
                ["schemaVersion"] = 1, ["attemptId"] = attemptId, ["snapshotSha256"] = snapshotHash,
                ["phase"] = "dispatching", ["createdAtUtc"] = DateTime.UtcNow.ToString("O"), ["outcome"] = null,
            };
            byte[] bytes = Encode(record);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            string hash = GenerateActionCommon.ComputeTextHash(s_Utf8.GetString(bytes));
            if (GenerateActionCommon.ComputeFileHash(path, default) != hash)
                throw new IOException("IAP 发送意图读回不一致，禁止发送。");
            return hash;
        }

        /// <summary>仅将当前同一意图转为完整结果；先写私有临时文件，再原子替换并读回。</summary>
        internal string Complete(string attemptId, string snapshotHash, string intentHash, JObject outcome)
        {
            JObject record = Read(attemptId, snapshotHash, out string currentHash);
            if (currentHash != intentHash || (string)record["phase"] != "dispatching" || record["outcome"].Type != JTokenType.Null)
                throw new InvalidOperationException("IAP 原意图已变化；不覆盖已有记录。");
            record["phase"] = "completed";
            record["outcome"] = outcome?.DeepClone() ?? throw new ArgumentNullException(nameof(outcome));
            byte[] bytes = Encode(record);
            string path = Resolve(attemptId);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                if (!GenerateActionCommon.TryResolveProjectPath(temporary, "IAP 回执临时文件", out temporary, out _))
                    throw new IOException("IAP 回执临时路径无效。");
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                Read(attemptId, snapshotHash, out currentHash);
                if (currentHash != intentHash) throw new IOException("IAP 结果保存前原意图已变化。");
                File.Replace(temporary, Resolve(attemptId), null);
                string expected = GenerateActionCommon.ComputeTextHash(s_Utf8.GetString(bytes));
                Read(attemptId, snapshotHash, out string actual);
                if (actual != expected) throw new IOException("IAP 提交结果读回不一致。");
                return actual;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        /// <summary>只读核验唯一记录的身份、容量、固定结构与阶段；不接触服务器或当前配置。</summary>
        internal JObject Read(string attemptId, string snapshotHash, out string fileHash)
        {
            string path = Resolve(attemptId);
            if (!File.Exists(path)) throw new IOException("尚无此提交的持久化意图或结果。");
            byte[] bytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length == 0 || stream.Length > MaxBytes) throw new IOException("IAP 回执容量非法。");
                bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new IOException("IAP 回执读取不完整。");
                    offset += count;
                }
                if (stream.ReadByte() != -1) throw new IOException("IAP 回执在读取期间变化。");
            }
            string json = s_Utf8.GetString(bytes);
            JObject record = Parse(json, new[] { "schemaVersion", "attemptId", "snapshotSha256", "phase", "createdAtUtc", "outcome" });
            if (record["schemaVersion"].Type != JTokenType.Integer || (int)record["schemaVersion"] != 1 ||
                record["attemptId"].Type != JTokenType.String || record["snapshotSha256"].Type != JTokenType.String || record["phase"].Type != JTokenType.String ||
                (string)record["attemptId"] != attemptId || (string)record["snapshotSha256"] != snapshotHash ||
                !IsHash(snapshotHash) || record["createdAtUtc"].Type != JTokenType.String ||
                !DateTimeOffset.TryParse((string)record["createdAtUtc"], out _))
                throw new IOException("IAP 回执身份或版本无效。");
            string phase = (string)record["phase"];
            if (phase != "dispatching" && phase != "completed" ||
                phase == "dispatching" && record["outcome"].Type != JTokenType.Null ||
                phase == "completed" && !(record["outcome"] is JObject))
                throw new IOException("IAP 回执阶段与结果不一致。");
            fileHash = GenerateActionCommon.ComputeTextHash(json);
            return record;
        }

        /// <summary>返回本操作唯一的项目相对记录路径，只允许规范小写 GUID。</summary>
        internal string RelativePath(string attemptId)
        {
            if (!Guid.TryParseExact(attemptId, "N", out Guid id) || id.ToString("N") != attemptId)
                throw new InvalidOperationException("IAP attemptId 无效。");
            return m_Root + "/" + attemptId + ".json";
        }

        /// <summary>在每次 IO 前重新检查路径边界及全部现存祖先的重解析点。</summary>
        private string Resolve(string attemptId)
        {
            if (!GenerateActionCommon.TryResolveProjectPath(RelativePath(attemptId), "IAP 回执", out string path, out _))
                throw new IOException("IAP 回执路径不安全。");
            return path;
        }

        /// <summary>严格解析固定字段对象，拒绝重复字段、未知字段、过深内容和超额字节。</summary>
        internal static JObject Parse(string json, string[] fields)
        {
            if (string.IsNullOrWhiteSpace(json) || s_Utf8.GetByteCount(json) > MaxBytes)
                throw new InvalidOperationException("IAP JSON 载荷为空或超限。");
            ValidateJsonLexemes(json);
            using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 16, DateParseHandling = DateParseHandling.None })
            {
                JObject value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read() || value.Count != fields.Length || fields.Any(name => value.Property(name, StringComparison.Ordinal) == null))
                    throw new InvalidOperationException("IAP JSON 必须只含完整已知字段。");
                return value;
            }
        }

        /// <summary>验证规范小写 SHA-256 文本，不接受截断或非十六进制摘要。</summary>
        internal static bool IsHash(string value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');

        /// <summary>筛除 Json.NET 的宽松词法扩展；字符串内保持原文，结构与深度仍由同一 JSON 读取器校验。</summary>
        private static void ValidateJsonLexemes(string json)
        {
            bool quoted = false, escaped = false;
            char previous = '\0';
            for (int i = 0; i < json.Length; i++)
            {
                char value = json[i];
                if (quoted)
                {
                    if (value < 0x20) throw new InvalidOperationException("IAP JSON 字符串不能包含未转义控制字符。");
                    if (escaped)
                    {
                        if ("\"\\/bfnrtu".IndexOf(value) < 0) throw new InvalidOperationException("IAP JSON 含非标准转义。");
                        escaped = false;
                    }
                    else if (value == '\\') escaped = true;
                    else if (value == '"') { quoted = false; previous = '"'; }
                    continue;
                }
                if (value == ' ' || value == '\t' || value == '\r' || value == '\n') continue;
                if ((value == '}' || value == ']') && previous == ',')
                    throw new InvalidOperationException("IAP JSON 不允许尾逗号。");
                if (value == '"') { quoted = true; previous = value; continue; }
                if ("{}[],:".IndexOf(value) < 0)
                {
                    int start = i;
                    while (i < json.Length && "{}[],:\" \t\r\n".IndexOf(json[i]) < 0) i++;
                    string atom = json.Substring(start, i - start);
                    if (atom != "true" && atom != "false" && atom != "null" &&
                        !Regex.IsMatch(atom, @"\A-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?\z", RegexOptions.CultureInvariant))
                        throw new InvalidOperationException("IAP JSON 包含非标准字面量。");
                    i--;
                }
                previous = value;
            }
        }

        /// <summary>以严格 UTF-8 无 BOM 编码完整记录，保持通用 Store 与 MCP 上限不变。</summary>
        private static byte[] Encode(JObject value)
        {
            byte[] bytes = s_Utf8.GetBytes(value.ToString(Formatting.None));
            if (bytes.Length > MaxBytes) throw new IOException("IAP 回执超过领域容量上限。");
            return bytes;
        }
    }
}
