using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using SqlCipher4Unity3D;

namespace NovaFramework.Editor
{
    internal static partial class PersistenceMaintenance
    {
        /// <summary>受保护备份使用随机 IV，不保存 Key/IV；JSON 收据只记录备份摘要。</summary>
        internal static void WriteProtectedValues(string path, ValueEntry[] values, string key)
        {
            using var aes = Aes.Create(); aes.Key = Encoding.UTF8.GetBytes(key); aes.GenerateIV();
            byte[] plain = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(values));
            if (plain.LongLength > MaximumSQLiteBytes) throw new InvalidOperationException("受保护备份内容超限。");
            using var transform = aes.CreateEncryptor(); byte[] encrypted = transform.TransformFinalBlock(plain, 0, plain.Length);
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            file.Write(aes.IV, 0, aes.IV.Length); file.Write(encrypted, 0, encrypted.Length); file.Flush(true);
        }

        /// <summary>原备份仅经当前冻结 AES 凭据解开并重核逻辑摘要；不把内容回显到日志。</summary>
        internal static ValueEntry[] ReadProtectedValues(string path, string key)
        {
            FileFragmentMaintenance.CheckPath(path);
            if (!File.Exists(path) || new FileInfo(path).Length > MaximumSQLiteBytes + 1024 * 1024) throw new InvalidOperationException("值恢复文件无效或超限。");
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 32 || bytes.Length % 16 != 0) throw new InvalidOperationException("受保护恢复文件格式无效。");
            using var aes = Aes.Create(); aes.Key = Encoding.UTF8.GetBytes(key); aes.IV = bytes.Take(16).ToArray();
            using var transform = aes.CreateDecryptor();
            string json = new UTF8Encoding(false, true).GetString(transform.TransformFinalBlock(bytes, 16, bytes.Length - 16));
            return JsonConvert.DeserializeObject<ValueEntry[]>(json, StrictValueJson);
        }

        private static readonly JsonSerializerSettings StrictValueJson = new JsonSerializerSettings
        { MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None, MaxDepth = 32 };

        /// <summary>当前半写异常清本轮tmp；断电遗留半写仅原token独立恢复隔离保留，异域完整日志拒绝。</summary>
        private static void SaveValueJournal(ValueJournal journal, bool authorizedRecovery = false)
        {
            string path = System.IO.Path.Combine(OperationDirectory(journal.origin.operationId), "values-journal.json"), temporary = path + ".tmp";
            FileFragmentMaintenance.CheckPath(path); FileFragmentMaintenance.CheckPath(temporary);
            if (authorizedRecovery && !SameValueOrigin(LoadValueJournal(journal.origin.operationId), journal)) throw new InvalidOperationException("值恢复正式日志来源不符。");
            CheckAesJournalTemporary(temporary, text => SameValueOrigin(JsonConvert.DeserializeObject<ValueJournal>(text, StrictValueJson), journal), authorizedRecovery, true);
            if (File.Exists(temporary)) File.Delete(temporary);
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(journal)); bool created = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true; int first = bytes.Length / 2; stream.Write(bytes, 0, first);
#if UNITY_INCLUDE_TESTS
                    if (s_AesFixtureTarget != null && JsonConvert.SerializeObject(journal.origin.target) == JsonConvert.SerializeObject(s_AesFixtureTarget)) s_AesJournalWriteFault?.Invoke(journal.origin.phase);
#endif
                    stream.Write(bytes, first, bytes.Length - first); stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch { if (created && File.Exists(temporary)) File.Delete(temporary); throw; }
        }

        /// <summary>外部收据、持久日志必须属于同一固定目标与原字段/逻辑集合。</summary>
        internal static bool SameValueOrigin(ValueJournal first, ValueJournal second) => first?.origin != null && second?.origin != null &&
            first.schemaVersion == 2 && second.schemaVersion == 2 && SameOrigin(first.origin, second.origin) &&
            first.dataScope == second.dataScope && first.acknowledgeGlobalSave == second.acknowledgeGlobalSave &&
            first.logicalBeforeSha256 == second.logicalBeforeSha256 && first.logicalAfterSha256 == second.logicalAfterSha256 &&
            first.metadataSha256 == second.metadataSha256 && first.keyCount == second.keyCount && first.itemCount == second.itemCount;

        /// <summary>读取真实持久恢复日志，固定文件名与 schema，不接受任意路径。</summary>
        private static ValueJournal LoadValueJournal(string operationId)
        {
            string path = System.IO.Path.Combine(OperationDirectory(operationId), "values-journal.json");
            FileFragmentMaintenance.CheckPath(path);
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) throw new InvalidOperationException("值恢复日志不存在或超限。");
            ValueJournal journal = JsonConvert.DeserializeObject<ValueJournal>(File.ReadAllText(path), StrictValueJson);
            ValidateValueJournal(journal, operationId);
            return journal;
        }

        /// <summary>固定 schema、存储、单字段与逻辑摘要，不把日志变成任意配置或数据库恢复器。</summary>
        private static void ValidateValueJournal(ValueJournal journal, string operationId)
        {
            Journal origin = journal?.origin;
            string field = origin?.storage == "PlayerPrefs" ? "m_UseAESForPlayerPrefs" : "m_UseAESForSQLite";
            if (journal == null || journal.schemaVersion != 2 || origin == null || origin.operationId != operationId ||
                origin.projectRoot != GenerateActionCommon.ProjectRoot || origin.storage != "PlayerPrefs" && origin.storage != "SQLite" ||
                !SerializedTargetEditing.ValidateTarget(origin.target, out _) || origin.settings?.targetType != typeof(PersistComponent).FullName ||
                JsonConvert.SerializeObject(origin.settings.target) != JsonConvert.SerializeObject(origin.target) ||
                origin.settings.changes == null || origin.settings.changes.Any(change => change.propertyPath != field || change.propertyType != "Boolean") ||
                origin.settings.changes.Length != (origin.beforeAES == origin.afterAES ? 0 : 1) ||
                origin.settings.executionStarted || !FileFragmentMaintenance.ValidHash(origin.settings.beforeStateSha256) ||
                !FileFragmentMaintenance.ValidHash(origin.settings.expectedStateSha256) || !FileFragmentMaintenance.ValidHash(origin.settings.plannedFileSha256) ||
                origin.settings.target?.globalObjectId == null || !IsGuid(origin.masterGuid) ||
                !FileFragmentMaintenance.ValidHash(origin.masterSha256) || !FileFragmentMaintenance.ValidHash(origin.masterMetaSha256) ||
                string.IsNullOrEmpty(origin.coordinate) ||
                !FileFragmentMaintenance.ValidHash(journal.logicalBeforeSha256) || !FileFragmentMaintenance.ValidHash(journal.logicalAfterSha256) ||
                origin.beforeSha256 != journal.logicalBeforeSha256 || origin.afterSha256 != journal.logicalAfterSha256 ||
                journal.keyCount < 0 || journal.keyCount > 16384 || journal.itemCount < 0 || journal.itemCount > journal.keyCount ||
                origin.storage == "PlayerPrefs" && (!journal.acknowledgeGlobalSave || journal.dataScope != "current-editor-managed-indexed-data" || origin.sourceRoot != "UnityEngine.PlayerPrefs:managed-indexes") ||
                origin.storage == "SQLite" && (journal.acknowledgeGlobalSave || journal.dataScope != "current-editor-sqlite-managed-data" ||
                    origin.sourceRoot != System.IO.Path.GetFullPath(AesMaintenanceSqlitePath)) ||
                !new[] { "planned", "unchanged", "prepared", "committing", "committed", "recovering", "recovered" }.Contains(origin.phase))
                throw new InvalidOperationException("值恢复日志范围不匹配。");
        }

        /// <summary>单次执行先保留受保护原值，再保存阶段；SQLite 同库事务提交，PlayerPrefs 明确全局 Save。</summary>
        internal static AgentActionResult ExecuteValues(ValuePlan plan, CancellationToken token)
        {
            if (plan?.Journal == null || Interlocked.Exchange(ref plan.Consumed, 1) != 0) return AgentActionResult.Create(null, "blocked", "值维护计划无效或已消费。");
            ValueJournal journal = plan.Journal; Journal origin = journal.origin;
            bool started = false; bool created = false;
            string directory = OperationDirectory(origin.operationId);
            try
            {
                EnsureIdle(); AssertMaster(origin, token); AssertSettingsBefore(origin, token);
                PrivacyConfigs privacy = ReadPrivacy(origin);
                if (origin.beforeAES == origin.afterAES)
                { AssertValueCurrent(plan, token); origin.phase = "unchanged"; return ValueResult(journal, "success", "值加密状态已满足，未写数据或配置。"); }
                using (AcquireValueScope(journal))
                {
                    AssertValueCurrent(plan, token);
                    if (Directory.Exists(directory)) throw new InvalidOperationException("操作目录已存在。");
                    Directory.CreateDirectory(directory);
                    created = true;
                    PrepareAesAssetOrigin(origin, token);
                    WriteProtectedValues(System.IO.Path.Combine(directory, "before-values.bin"), plan.Before, privacy.AESKey);
                    WriteProtectedValues(System.IO.Path.Combine(directory, "after-values.bin"), plan.After, privacy.AESKey);
                    journal.backupSha256 = FileFragmentMaintenance.Hash(System.IO.Path.Combine(directory, "before-values.bin"), token);
                    journal.protectedAfterSha256 = FileFragmentMaintenance.Hash(System.IO.Path.Combine(directory, "after-values.bin"), token);
                    if (ValueHash(ReadProtectedValues(System.IO.Path.Combine(directory, "before-values.bin"), privacy.AESKey)) != journal.logicalBeforeSha256 ||
                        ValueHash(ReadProtectedValues(System.IO.Path.Combine(directory, "after-values.bin"), privacy.AESKey)) != journal.logicalAfterSha256)
                        throw new InvalidOperationException("受保护恢复材料重读不匹配。");
                    if (origin.storage == "SQLite")
                    {
                        string databaseBackup = System.IO.Path.Combine(directory, "before-sqlite.db");
                        using (var source = OpenMaintenanceDatabase(origin.sourceRoot, plan.Password)) BackupSQLite(source, databaseBackup, privacy.AESKey);
                        using (var backup = OpenMaintenanceDatabase(databaseBackup, privacy.AESKey))
                            if (ValueHash(ReadSQLiteValues(backup, token)) != journal.logicalBeforeSha256) throw new InvalidOperationException("一致备份与冻结源不匹配。");
                        journal.databaseBackupSha256 = FileFragmentMaintenance.Hash(databaseBackup, token);
                    }
                    origin.phase = "prepared"; SaveValueJournal(journal);
                    token.ThrowIfCancellationRequested(); AssertMaster(origin, token); AssertSettingsBefore(origin, token);
                    origin.phase = "committing"; SaveValueJournal(journal); started = true;
                    if (origin.storage == "PlayerPrefs") ApplyPlayerPrefs(plan.Before, plan.After, token);
                    else ApplySQLitePlan(plan, directory, token);
                    AgentActionResult saved = SerializedTargetEditing.Execute(plan.Settings, CancellationToken.None);
                    if (saved.Status != "success") throw new InvalidOperationException("值提交后配置保存未通过。");
                    origin.savedSettings = JsonConvert.DeserializeObject<SerializedTargetEditing.Receipt>(saved.ReceiptJson);
                    origin.phase = "committed"; SaveValueJournal(journal);
                    if (SerializedTargetEditing.Verify(saved.ReceiptJson, CancellationToken.None).Status != "success") throw new InvalidOperationException("配置后验未通过。");
                }
                AgentActionResult verified = VerifyValues(JsonConvert.SerializeObject(journal), CancellationToken.None);
                return verified.Status == "success" ? verified : ValueResult(journal, "partial", "提交后全体值、配置或恢复材料重读未通过，保留现场并独立恢复。");
            }
            catch (OperationCanceledException) when (!started)
            {
                if (created && !CleanValueUncommitted(directory)) return ValueResult(journal, "partial", "取消前未提交值，但本次临时恢复材料未能安全清理。");
                throw;
            }
            catch
            {
                if (!started && created && !CleanValueUncommitted(directory)) return ValueResult(journal, "partial", "未提交玩家值，临时恢复材料未能安全清理。");
                return ValueResult(journal, started ? "partial" : "blocked", started ?
                    "值维护已进入提交；保留原值和日志，须 Verify 后独立恢复，不重放迁移。PlayerPrefs 全局 Save 副作用不可回滚。" :
                    "提交前条件或备份不满足，玩家值与配置未开始写入。");
            }
        }

        /// <summary>PlayerPrefs 只重写完整已知键；Save 失败仍可能已有部分落盘，留给独立恢复处理。</summary>
        internal static void ApplyPlayerPrefs(ValueEntry[] before, ValueEntry[] after, CancellationToken token)
        {
            if (ValueHash(ReadPlayerPrefsValues(HasMaintenancePlayerPrefsKey, ReadPlayerPrefsString, token)) != ValueHash(before))
                throw new InvalidOperationException("PlayerPrefs 提交前源漂移。");
            foreach (ValueEntry value in after) SetMaintenancePlayerPrefsString(value.name, value.value);
            SaveMaintenancePlayerPrefs();
            if (ValueHash(ReadPlayerPrefsValues(HasMaintenancePlayerPrefsKey, ReadPlayerPrefsString, CancellationToken.None)) != ValueHash(after))
                throw new InvalidOperationException("PlayerPrefs 缓存后验不匹配。");
        }

        /// <summary>只使用参与协议的进程维护门；数据库实际并发写隔离依赖 BEGIN IMMEDIATE。</summary>
        private static IDisposable AcquireValueScope(ValueJournal journal) => journal.origin.storage == "SQLite" ?
            SQLiteMaintenanceLease.Acquire(journal.origin.sourceRoot) : new ValueNoopScope();
        private sealed class ValueNoopScope : IDisposable { public void Dispose() { } }

        /// <summary>冻结源复核；物理 metadata 仅变化探测，真实数据库行另核逻辑摘要。</summary>
        private static void AssertValueCurrent(ValuePlan plan, CancellationToken token)
        {
            if (plan.Journal.origin.storage == "PlayerPrefs")
            {
                if (ValueHash(ReadPlayerPrefsValues(HasMaintenancePlayerPrefsKey, ReadPlayerPrefsString, token)) != plan.Journal.logicalBeforeSha256)
                    throw new InvalidOperationException("PlayerPrefs 源漂移。");
            }
            else if (InspectSQLiteFiles(plan.Journal.origin.sourceRoot, token).metadataSha256 != plan.Journal.metadataSha256)
                throw new InvalidOperationException("SQLite 物理集合发生变化。");
        }

        /// <summary>backup 后取得真实写锁，重核当前与备份全值；转换失败 rollback，commit 后配置失败靠新恢复事务。</summary>
        private static void ApplySQLitePlan(ValuePlan plan, string directory, CancellationToken token)
        {
            using var connection = OpenMaintenanceDatabase(plan.Journal.origin.sourceRoot, plan.Password);
            connection.Execute("BEGIN IMMEDIATE"); bool active = true;
            try
            {
                if (ValueHash(ReadSQLiteValues(connection, token)) != plan.Journal.logicalBeforeSha256) throw new InvalidOperationException("事务源漂移。");
                ApplySQLiteValues(connection, plan.After);
                if (ValueHash(ReadSQLiteValues(connection, token)) != plan.Journal.logicalAfterSha256) throw new InvalidOperationException("事务转换后验不匹配。");
                token.ThrowIfCancellationRequested(); connection.Execute("COMMIT"); active = false;
            }
            finally { if (active) connection.Execute("ROLLBACK"); }
        }

        /// <summary>只清本次提交前的固定材料，不删除未知文件。</summary>
        private static bool CleanValueUncommitted(string directory)
        {
            try
            {
            if (!Directory.Exists(directory)) return true;
            string[] names = { "before-aes-asset.bin", "before-values.bin", "after-values.bin", "before-sqlite.db", "before-sqlite.db-wal", "before-sqlite.db-shm", "before-sqlite.db-journal", "values-journal.json", "values-journal.json.tmp" };
            if (Directory.GetFileSystemEntries(directory).Any(path => !names.Contains(System.IO.Path.GetFileName(path)) || Directory.Exists(path)))
                throw new InvalidOperationException("提交前临时材料存在未知项，保留现场。");
            foreach (string path in Directory.GetFiles(directory)) File.Delete(path);
            Directory.Delete(directory);
            return true;
            }
            catch { return false; }
        }

        /// <summary>只输出计数、逻辑摘要及明确保存边界，原值和凭据不进入收据。</summary>
        internal static AgentActionResult ValueResult(ValueJournal journal, string status, string message)
        {
            var result = AgentActionResult.Create(null, status, message);
            result.ReceiptJson = JsonConvert.SerializeObject(journal);
            result.DataJson = JsonConvert.SerializeObject(new { journal.origin.operationId, journal.origin.phase, journal.origin.storage,
                journal.dataScope, journal.logicalBeforeSha256, journal.logicalAfterSha256, journal.itemCount, journal.keyCount,
                journal.acknowledgeGlobalSave, globalSaveEffect = journal.origin.storage == "PlayerPrefs" ? "all-process-playerprefs-cache; unrelated-dirty-commit-not-recoverable" : null,
                sqliteIsolation = journal.origin.storage == "SQLite" ? "participating-Nova-lease-and-real-sqlite-write-transaction" : null,
                durableState = "target-platform-restart-not-verified", cipherRotationSupported = false,
                retainedRecoveryDirectory = Directory.Exists(OperationDirectory(journal.origin.operationId)) ? "Library/Nova/AgentActions/PersistenceMaintenance/" + journal.origin.operationId : null });
            result.EvidenceKinds = AgentActionEvidence.Static | (status == "success" ? AgentActionEvidence.Artifact : AgentActionEvidence.None);
            return result;
        }
    }
}
