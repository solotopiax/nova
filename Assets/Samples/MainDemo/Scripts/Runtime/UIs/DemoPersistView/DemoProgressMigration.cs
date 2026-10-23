/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoProgressMigration.cs
 * author:    taoye
 * created:   2026/10/20
 * descrip:   项目业务进度迁移示例，复用公开 FileFragment 接口
 ***************************************************************/

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// 单写者业务迁移入口：保留旧分类，将进度与版本、来源摘要一起保存到独立目标分类。
    /// 存储初始化和独立重载由调用方负责；不初始化 Nova、不访问默认路径、不删除旧数据。
    /// 正常业务可注入已就绪的 Nova.Persist.FileFragment，隔离验收注入自有临时根的真实实现。
    /// </summary>
    public sealed class DemoProgressMigration
    {
        private const string c_Item = "progress";
        private static readonly Encoding s_Utf8 = new UTF8Encoding(false, true);
        private readonly IFileFragmentManager m_Store;
        private readonly string m_SourceClassify;
        private readonly string m_TargetClassify;
        private bool m_SaveOutcomeUncertain;

        /// <summary>已验证的业务进度；字段类型、取值范围由迁移与保存入口统一核对。</summary>
        public readonly struct Progress
        {
            /// <summary>至少为1的业务等级。</summary>
            public readonly int Level;
            /// <summary>非负Int32金币数量。</summary>
            public readonly int Coins;
            /// <summary>非null的显示名称。</summary>
            public readonly string DisplayName;

            /// <summary>建立业务值，不读取或写入存储；保存入口仍独立验证值范围。</summary>
            /// <param name="level">至少为1的等级。</param>
            /// <param name="coins">非负金币数量。</param>
            /// <param name="displayName">显示名称；null按空字符串处理。</param>
            public Progress(int level, int coins, string displayName)
            {
                Level = level;
                Coins = coins;
                DisplayName = displayName ?? string.Empty;
            }
        }

        /// <summary>绑定已初始化、由调用方独占写入的存储及两个明确分类，不自动选择真实存档。</summary>
        /// <param name="store">已就绪的公开 FileFragment 接口；生命周期归调用方。</param>
        /// <param name="sourceClassify">保留原值的旧分类，1至64个ASCII字母、数字、下划线或短横线。</param>
        /// <param name="targetClassify">不同于源且采用相同命名约束的目标分类，只允许本示例progress条目。</param>
        public DemoProgressMigration(IFileFragmentManager store, string sourceClassify, string targetClassify)
        {
            m_Store = store ?? throw new ArgumentNullException(nameof(store));
            if (string.IsNullOrWhiteSpace(sourceClassify) || string.IsNullOrWhiteSpace(targetClassify) ||
                string.Equals(sourceClassify, targetClassify, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("迁移需要两个明确且不同的分类。");
            // 排除大小写、Unicode规范化、尾随空格等常见文件系统别名，防止两个分类命中同一分片。
            foreach (string name in new[] { sourceClassify, targetClassify })
                if (name.Length > 64 || name.Any(ch => !((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') ||
                    (ch >= '0' && ch <= '9') || ch == '_' || ch == '-')))
                    throw new ArgumentException("示例分类只接受1至64个ASCII字母、数字、下划线或短横线。");
            m_SourceClassify = sourceClassify;
            m_TargetClassify = targetClassify;
        }

        /// <summary>
        /// 将v1进度保存为v2；已有同源v2时不写入，也不覆盖迁移后正常更新的进度。
        /// 未知结构、来源冲突或保存结果不确定时抛错；不自动回滚、重放或更新独立版本标记。
        /// </summary>
        /// <returns>本次创建目标时为true；已有同源目标时为false。</returns>
        public bool Migrate()
        {
            EnsureKnownState();
            string source = ReadSource();
            JObject legacy = Parse(source);
            RequireFields(legacy, "schemaVersion", "level", "coins", "name");
            if (ReadInt(legacy, "schemaVersion", 1) != 1) throw new InvalidDataException("不支持的源版本。");
            var progress = new Progress(ReadInt(legacy, "level", 1), ReadInt(legacy, "coins", 0),
                legacy["name"] == null ? string.Empty : ReadString(legacy, "name"));
            string origin = Hash(source);
            RequireOwnedTarget();
            if (m_Store.HasItem(m_TargetClassify, c_Item))
            {
                ReadTarget(origin);
                return false;
            }
            // 同一同步调用内不让已变化的内存源被旧快照覆盖；外部并发写入仍由单写者前提排除。
            if (!string.Equals(source, ReadSource(), StringComparison.Ordinal))
                throw new InvalidDataException("迁移源已变化，未发布目标。");
            SaveTarget(progress, origin);
            return true;
        }

        /// <summary>从当前接口读取并验证同源v2业务进度；需独立落盘验收时使用重新初始化的存储实例。</summary>
        /// <returns>已验证的等级、金币和显示名。</returns>
        public Progress ReadProgress()
        {
            EnsureKnownState();
            return ReadTarget(Hash(ReadSource()));
        }

        /// <summary>保存迁移后的正常游戏进度，保留来源标记；不会再次套用旧版初始进度。</summary>
        /// <param name="progress">目标业务值；等级至少1、金币非负。</param>
        public void SaveProgress(Progress progress)
        {
            EnsureKnownState();
            string origin = Hash(ReadSource());
            ReadTarget(origin);
            SaveTarget(progress, origin);
        }

        /// <summary>读取明确的旧progress条目，缺失时不创建默认存档。</summary>
        /// <returns>保留原始格式的旧JSON字符串。</returns>
        private string ReadSource()
        {
            if (!m_Store.HasItem(m_SourceClassify, c_Item)) throw new InvalidDataException("缺少源进度。");
            return m_Store.GetString(m_SourceClassify, c_Item);
        }

        /// <summary>拒绝目标分类中未冻结的其他条目，避免把共享分类当成示例自有分类。</summary>
        private void RequireOwnedTarget()
        {
            if (m_Store.GetAllItemNames(m_TargetClassify).Any(name => name != c_Item))
                throw new InvalidDataException("目标分类包含其他条目，不能自动接管。");
        }

        /// <summary>核对v2完整结构及原始来源摘要，不用缓存调用成功代替独立磁盘读回。</summary>
        /// <param name="origin">旧源原字符串的UTF8 SHA256小写摘要。</param>
        /// <returns>当前目标中的业务进度。</returns>
        private Progress ReadTarget(string origin)
        {
            RequireOwnedTarget();
            if (!m_Store.HasItem(m_TargetClassify, c_Item)) throw new InvalidDataException("尚无迁移后的进度。");
            JObject target = Parse(m_Store.GetString(m_TargetClassify, c_Item));
            RequireFields(target, "schemaVersion", "progress", "wallet", "displayName", "migratedFrom");
            if (ReadInt(target, "schemaVersion", 1) != 2) throw new InvalidDataException("不支持的目标版本。");
            if (!string.Equals(ReadString(target, "migratedFrom"), origin, StringComparison.Ordinal))
                throw new InvalidDataException("目标不属于当前旧源，保留冲突数据。");
            if (!(target["progress"] is JObject progress) || !(target["wallet"] is JObject wallet))
                throw new InvalidDataException("目标进度结构不完整。");
            RequireFields(progress, "level");
            RequireFields(wallet, "coins");
            return new Progress(ReadInt(progress, "level", 1), ReadInt(wallet, "coins", 0), ReadString(target, "displayName"));
        }

        /// <summary>将版本、来源标记与业务值作为一个目标条目保存；异常后锁住当前服务，等待独立重载核查。</summary>
        /// <param name="progress">已冻结目标业务值。</param>
        /// <param name="origin">与目标一起保存的旧源摘要。</param>
        private void SaveTarget(Progress progress, string origin)
        {
            if (progress.Level < 1 || progress.Coins < 0) throw new InvalidDataException("目标进度超出范围。");
            string json = new JObject
            {
                ["schemaVersion"] = 2,
                ["progress"] = new JObject { ["level"] = progress.Level },
                ["wallet"] = new JObject { ["coins"] = progress.Coins },
                ["displayName"] = progress.DisplayName ?? string.Empty,
                ["migratedFrom"] = origin
            }.ToString(Formatting.None);
            Parse(json); // 保存前采用与读回相同的大小与重复字段门，避免创建自身不可读取的目标。
            m_SaveOutcomeUncertain = true;
            m_Store.SetString(m_TargetClassify, c_Item, json);
            if (!m_Store.Save(m_TargetClassify)) throw new IOException("保存结果未确认，必须独立重载后核查，不自动重试。");
            m_SaveOutcomeUncertain = false;
        }

        /// <summary>拒绝保存失败后的同缓存重试，防止把未确认的内存目标当成已落盘。</summary>
        private void EnsureKnownState()
        {
            if (m_SaveOutcomeUncertain) throw new IOException("当前服务保存结果不确定，需要新的存储实例独立重载核查。");
        }

        /// <summary>有界读取单个JSON对象，拒绝重复字段与尾随内容；不转换日期或修改原文。</summary>
        /// <param name="json">最多64KiB UTF8的业务字符串。</param>
        /// <returns>待按具体版本进一步验证的对象。</returns>
        private static JObject Parse(string json)
        {
            if (json == null || s_Utf8.GetByteCount(json) > 65536) throw new InvalidDataException("进度为空或超过64KiB。");
            using var text = new StringReader(json);
            using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, SupportMultipleContent = true, MaxDepth = 8 };
            JObject value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("进度含有尾随JSON内容。");
            return value;
        }

        /// <summary>拒绝未声明字段；具体必需字段由对应类型读取器核对。</summary>
        /// <param name="value">已解析对象。</param>
        /// <param name="names">本版本允许的精确字段名。</param>
        private static void RequireFields(JObject value, params string[] names)
        {
            if (value.Properties().Any(property => !names.Contains(property.Name))) throw new InvalidDataException("进度包含未知字段。");
        }

        /// <summary>读取Int32整数并核对下界，拒绝字符串、小数及溢出。</summary>
        /// <param name="value">字段所属对象。</param>
        /// <param name="name">必需字段名。</param>
        /// <param name="minimum">允许的最小值。</param>
        /// <returns>验证后的业务整数。</returns>
        private static int ReadInt(JObject value, string name, int minimum)
        {
            JToken token = value[name];
            if (token?.Type != JTokenType.Integer || !int.TryParse(token.ToString(Formatting.None),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < minimum)
                throw new InvalidDataException("进度整数缺失或超出范围：" + name);
            return number;
        }

        /// <summary>读取必需字符串；null、对象和数字均不隐式转换。</summary>
        /// <param name="value">字段所属对象。</param>
        /// <param name="name">必需字段名。</param>
        /// <returns>原始字符串值。</returns>
        private static string ReadString(JObject value, string name)
        {
            if (value[name]?.Type != JTokenType.String) throw new InvalidDataException("进度字符串缺失或类型错误：" + name);
            return (string)value[name];
        }

        /// <summary>计算原始字符串来源摘要，不将摘要或玩家值发送到外部服务。</summary>
        /// <param name="value">保留原格式的旧源字符串。</param>
        /// <returns>64位小写十六进制SHA256。</returns>
        private static string Hash(string value)
        {
            if (value == null || s_Utf8.GetByteCount(value) > 65536) throw new InvalidDataException("源进度为空或超限。");
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(s_Utf8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
