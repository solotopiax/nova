using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    internal static partial class PersistenceMaintenance
    {
        /// <summary>已冻结的索引键或数据库行；原值只在内存和受保护恢复文件中保存。</summary>
        [Serializable]
        internal sealed class ValueEntry
        {
            public string classify;
            public string name;
            public string value;
            public bool index;
        }

        /// <summary>不含玩家值、密码或 AES Key/IV 的持久维护身份。</summary>
        [Serializable]
        internal sealed class ValueJournal
        {
            public int schemaVersion = 2;
            public Journal origin;
            public string dataScope;
            public bool acknowledgeGlobalSave;
            public string metadataSha256;
            public string logicalBeforeSha256;
            public string logicalAfterSha256;
            public string backupSha256;
            public string protectedAfterSha256;
            public string databaseBackupSha256;
            public int itemCount;
            public int keyCount;
        }

        /// <summary>敏感值与凭据仅驻留本进程，不进入外部协议。</summary>
        internal sealed class ValuePlan
        {
            internal ValueJournal Journal;
            internal ValueEntry[] Before;
            internal ValueEntry[] After;
            internal SerializedTargetEditing.Plan Settings;
            internal string Password;
            internal int Consumed;
            internal string JournalHash;
            internal string RecoveryToken;
            internal string RecoveryFileHash;
            internal string RecoveryStateHash;
        }

        /// <summary>固定值维护范围；PlayerPrefs 必须明确接受进程全局 Save，SQLite 不接收密码或 SQL。</summary>
        internal static bool ValidateValues(Request request, bool migration, out string error)
        {
            bool pp = request?.storage == "PlayerPrefs";
            error = null;
            if (request == null || !pp && request.storage != "SQLite" ||
                request.acknowledgeEditorOnly.HasValue || request.acknowledgeDefaultAesConsumerScope.HasValue ||
                request.dataScope != (pp ? "current-editor-managed-indexed-data" : "current-editor-sqlite-managed-data") ||
                !SerializedTargetEditing.ValidateTarget(ToTarget(request), out _) || string.IsNullOrEmpty(request.globalObjectId) ||
                migration && (!request.enableAES.HasValue || !FileFragmentMaintenance.ValidHash(request.sourceSha256) ||
                    pp && request.acknowledgeGlobalSave != true) ||
                !migration && (request.enableAES.HasValue || request.sourceSha256 != null || request.acknowledgeGlobalSave.HasValue) ||
                !pp && request.acknowledgeGlobalSave.HasValue)
            { error = "须提供固定值维护 scope、唯一 PersistComponent；迁移须 enableAES/sourceSha256，PlayerPrefs 须 acknowledgeGlobalSave=true。"; return false; }
            return true;
        }

        /// <summary>读取完整有效索引以及已有字符串值，不枚举或触碰未知键；缺失索引不能当作空数据。</summary>
        internal static ValueEntry[] ReadPlayerPrefsValues(Func<string, bool> hasKey, Func<string, string> getString, CancellationToken token)
        {
            var values = new List<ValueEntry>();
            long bytes = 0;
            int indexBytes = 0; int itemCount = 0;
            Action<string, bool> add = (key, index) =>
            {
                token.ThrowIfCancellationRequested();
                if (!hasKey(key)) throw new InvalidOperationException("托管索引或条目缺失。");
                string value = getString(key);
                if (value == null) throw new InvalidOperationException("托管值不是已确认字符串。");
                bytes += Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(value);
                if (index) indexBytes += Encoding.UTF8.GetByteCount(value); else itemCount++;
                if (indexBytes > MaximumIndexBytes || itemCount > MaximumIndexEntries) throw new InvalidOperationException("托管索引超出维护容量。");
                if (bytes > MaximumSQLiteBytes || values.Count >= 16384) throw new InvalidOperationException("托管数据超出维护容量。");
                values.Add(new ValueEntry { name = key, value = value, index = index });
            };
            add(PlayerPrefsIndexKey, true);
            foreach (string classify in ParsePlayerPrefsIndex(values[0].value, false))
            {
                string key = classify + "::__items__";
                add(key, true);
                string raw = values[values.Count - 1].value;
                foreach (string item in ParsePlayerPrefsIndex(raw, true)) add(classify + "::" + item, false);
            }
            return values.OrderBy(value => value.name, StringComparer.Ordinal).ToArray();
        }

        /// <summary>对精确行身份与值作长度分帧摘要，避免拼接碰撞；不把摘要描述为平台耐久性。</summary>
        internal static string ValueHash(ValueEntry[] entries)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                foreach (ValueEntry entry in entries.OrderBy(value => value.classify, StringComparer.Ordinal).ThenBy(value => value.name, StringComparer.Ordinal))
                { writer.Write(entry.classify ?? string.Empty); writer.Write(entry.name); writer.Write(entry.index); writer.Write(entry.value); }
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>沿用 AES CBC/PKCS7 字节契约并严格 UTF8 解码；坏密文不改为空串或跳过。</summary>
        internal static ValueEntry[] ConvertValues(ValueEntry[] source, bool beforeAES, bool afterAES, string key, string iv)
        {
            if (Encoding.UTF8.GetByteCount(key ?? string.Empty) != 16 || Encoding.UTF8.GetByteCount(iv ?? string.Empty) != 16)
                throw new InvalidOperationException("维护 AES 凭据不满足固定长度。");
            return source.Select(entry =>
            {
                string value = entry.value;
                if (!entry.index && value.Length != 0)
                {
                    string plain = beforeAES ? new UTF8Encoding(false, true).GetString(
                        Util.Encrypt.AES.DecryptBytes(Convert.FromBase64String(value), key, iv)) : value;
                    if (beforeAES != afterAES) value = afterAES ? Util.Encrypt.AES.EncryptString(plain, key, iv) : plain;
                }
                return new ValueEntry { classify = entry.classify, name = entry.name, index = entry.index, value = value };
            }).ToArray();
        }

        /// <summary>严格值检查仅读 indexed keys；只输出计数与源摘要。</summary>
        internal static AgentActionResult InspectPlayerPrefsValues(Request request, CancellationToken token)
        {
            if (!ValidateValues(request, false, out string error)) throw new InvalidOperationException(error);
            EnsureIdle();
            PersistComponent target = ReadMetadataTarget(request, out _);
            if (target.CurPlayerPrefsManagerTypeName != "NovaFramework.Runtime.PlayerPrefsManager")
                throw new InvalidOperationException("只支持标准 PlayerPrefsManager 索引。");
#if NOVA_DYSDK || NOVA_WXSDK || NOVA_ALIPAYSDK
            return AgentActionResult.Create(null, "not_applicable", "仅支持默认 Unity PlayerPrefs provider。");
#else
            ValueEntry[] first = ReadPlayerPrefsValues(HasMaintenancePlayerPrefsKey, ReadPlayerPrefsString, token);
            string hash = ValueHash(first);
            if (hash != ValueHash(ReadPlayerPrefsValues(HasMaintenancePlayerPrefsKey, ReadPlayerPrefsString, token)))
                throw new InvalidOperationException("托管值在检查期间变化。");
            var result = AgentActionResult.Create(null, "success", "已读取完整托管索引值摘要；未知键、全局草稿和耐久性未知，未保存。");
            result.ReceiptJson = JsonConvert.SerializeObject(request);
            result.DataJson = JsonConvert.SerializeObject(new { request.storage, request.dataScope, sourceSha256 = hash,
                itemCount = first.Count(value => !value.index), keyCount = first.Length, playerValues = "read_not_disclosed",
                wholeKeySpace = "unknown", globalDirtyState = "unknown", durableState = "unknown", dataOrConfigWritten = false });
            result.EvidenceKinds = AgentActionEvidence.Static;
            return result;
#endif
        }

        /// <summary>两个不同默认值可拒绝已有非字符串键；不依靠单次 GetString 的空默认值推断类型。</summary>
        private static string ReadPlayerPrefsString(string key)
        {
#if UNITY_INCLUDE_TESTS
            if (s_AesFixturePrefs != null) return s_AesFixturePrefs[key];
#endif
            string first = UnityEngine.PlayerPrefs.GetString(key, "__nova_missing_string_a__");
            string second = UnityEngine.PlayerPrefs.GetString(key, "__nova_missing_string_b__");
            if (first != second) throw new InvalidOperationException("已索引键不是字符串。");
            return first;
        }

        /// <summary>预演值转换、源与唯一字段；SQLite 逻辑读取由真实事务保护，回滚不写玩家值。</summary>
        internal static ValuePlan BuildValues(Request request, CancellationToken token)
        {
            if (!ValidateValues(request, true, out string error)) throw new InvalidOperationException(error);
            EnsureIdle();
            var target = ToTarget(request);
            var view = SerializedTargetEditing.Inspect(target, token);
            string field = request.storage == "PlayerPrefs" ? "m_UseAESForPlayerPrefs" : "m_UseAESForSQLite";
            string managerField = request.storage == "PlayerPrefs" ? "m_CurPlayerPrefsManagerTypeName" : "m_CurSQLiteManagerTypeName";
            if (!view.editable || view.targetType != typeof(PersistComponent).FullName ||
                view.fields.SingleOrDefault(value => value.path == managerField)?.valueJson != "\"NovaFramework.Runtime." + request.storage + "Manager\"")
                throw new InvalidOperationException("只支持可维护标准 PersistComponent 与对应标准存储。");
            bool beforeAES = view.fields.Single(value => value.path == field).valueJson == "true";
            var settings = SerializedTargetEditing.BuildPersistenceValueAESPlan(target, request.storage, request.enableAES.Value, token);
            ConfigMasterSO master = ReadMaster(out string masterPath, out string masterGuid);
            PrivacyConfigs privacy = master.GetPrivacyConfigs(master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
            var origin = new Journal { operationId = Guid.NewGuid().ToString("N"), projectRoot = GenerateActionCommon.ProjectRoot,
                storage = request.storage, phase = "planned", target = target, settings = JsonConvert.DeserializeObject<SerializedTargetEditing.Receipt>(JsonConvert.SerializeObject(settings.Receipt)), beforeAES = beforeAES,
                afterAES = request.enableAES.Value, masterGuid = masterGuid, masterSha256 = AssetHash(masterPath, token),
                masterMetaSha256 = AssetHash(masterPath + ".meta", token), coordinate = Coordinate(master), entries = Array.Empty<FileFragmentMaintenance.Entry>() };
            var journal = new ValueJournal { origin = origin, dataScope = request.dataScope, acknowledgeGlobalSave = request.acknowledgeGlobalSave == true };
            var plan = new ValuePlan { Journal = journal, Settings = settings };
            if (request.storage == "PlayerPrefs")
            {
#if NOVA_DYSDK || NOVA_WXSDK || NOVA_ALIPAYSDK
                throw new InvalidOperationException("非默认 PlayerPrefs provider 不支持维护。");
#else
                plan.Before = ReadPlayerPrefsValues(HasMaintenancePlayerPrefsKey, ReadPlayerPrefsString, token);
                journal.logicalBeforeSha256 = ValueHash(plan.Before);
                if (request.sourceSha256 != journal.logicalBeforeSha256 || journal.logicalBeforeSha256 !=
                    ValueHash(ReadPlayerPrefsValues(HasMaintenancePlayerPrefsKey, ReadPlayerPrefsString, token))) throw new InvalidOperationException("托管源漂移。");
                origin.sourceRoot = "UnityEngine.PlayerPrefs:managed-indexes";
#endif
            }
            else BuildSQLiteValues(plan, request.sourceSha256, token);
            plan.After = ConvertValues(plan.Before, beforeAES, origin.afterAES, privacy?.AESKey, privacy?.AESIV);
            journal.logicalAfterSha256 = ValueHash(plan.After);
            origin.beforeSha256 = journal.logicalBeforeSha256; origin.afterSha256 = journal.logicalAfterSha256;
            journal.itemCount = plan.Before.Count(value => !value.index); journal.keyCount = plan.Before.Length;
            AssertMaster(origin, token); AssertSettingsBefore(origin, token);
            return plan;
        }
    }
}
