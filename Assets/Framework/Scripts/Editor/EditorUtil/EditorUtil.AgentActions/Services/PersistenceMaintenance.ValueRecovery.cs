using System;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    internal static partial class PersistenceMaintenance
    {
        /// <summary>新值日志存在时按固定新存储恢复；旧分片日志继续原路线。</summary>
        internal static bool HasValueJournal(string operationId) => File.Exists(System.IO.Path.Combine(OperationDirectory(operationId), "values-journal.json"));

        /// <summary>恢复只依据原 Action 凭据绑定的原/新完整逻辑，不重放迁移。</summary>
        internal static ValuePlan BuildValueRecovery(string operationId, string recoveryToken, CancellationToken token)
        {
            EnsureIdle();
            ValueJournal journal = LoadValueJournal(operationId);
            AssertValueOperationBinding(journal, recoveryToken);
            if (!new[] { "prepared", "committing", "committed", "recovering" }.Contains(journal.origin.phase)) throw new InvalidOperationException("值维护阶段不可恢复。");
            AssertMaster(journal.origin, token); AssertAesSettingsRecoverable(journal.origin, token);
            ValuePlan plan = ReadValueRecoveryPlan(journal, token);
            AssertRecoverableValues(plan, token);
            var view = SerializedTargetEditing.Inspect(journal.origin.target, token);
            plan.RecoveryToken = recoveryToken; plan.RecoveryFileHash = view.fileSha256; plan.RecoveryStateHash = view.stateSha256;
            CheckAesJournalTemporary(System.IO.Path.Combine(OperationDirectory(operationId), "values-journal.json.tmp"),
                text => SameValueOrigin(JsonConvert.DeserializeObject<ValueJournal>(text, StrictValueJson), journal), true, false);
            plan.JournalHash = FileFragmentMaintenance.Hash(System.IO.Path.Combine(OperationDirectory(operationId), "values-journal.json"), token);
            return plan;
        }

        /// <summary>值恢复每次计划和执行均重核原Action令牌完整冻结来源，不信本地日志改变原字段或范围。</summary>
        private static void AssertValueOperationBinding(ValueJournal journal, string recoveryToken)
        {
            if (!AgentActionRuntime.OperationStore.TryLoad(recoveryToken, "nova.project.persistence.migrate-encryption", 1, out var operation))
                throw new InvalidOperationException("原迁移恢复凭据无效。");
            var envelope = JsonConvert.DeserializeObject<AgentActionReceiptEnvelope>(operation.ReceiptJson);
            ValueJournal original = JsonConvert.DeserializeObject<ValueJournal>(envelope?.PayloadJson, StrictValueJson);
            if (envelope?.ActionId != "nova.project.persistence.migrate-encryption" || envelope.ContractMajor != 1 || !SameValueOrigin(original, journal))
                throw new InvalidOperationException("值日志未绑定原迁移范围。");
        }

        /// <summary>核验受保护备份摘要和原/新逻辑摘要；数据库密码从真实目标读取，不从日志或请求接受。</summary>
        private static ValuePlan ReadValueRecoveryPlan(ValueJournal journal, CancellationToken token)
        {
            string directory = OperationDirectory(journal.origin.operationId);
            string before = System.IO.Path.Combine(directory, "before-values.bin");
            string after = System.IO.Path.Combine(directory, "after-values.bin");
            if (FileFragmentMaintenance.Hash(before, token) != journal.backupSha256 || FileFragmentMaintenance.Hash(after, token) != journal.protectedAfterSha256)
                throw new InvalidOperationException("受保护备份摘要漂移。");
            PrivacyConfigs privacy = ReadPrivacy(journal.origin);
            var plan = new ValuePlan { Journal = journal, Before = ReadProtectedValues(before, privacy.AESKey), After = ReadProtectedValues(after, privacy.AESKey) };
            if (ValueHash(plan.Before) != journal.logicalBeforeSha256 || ValueHash(plan.After) != journal.logicalAfterSha256 ||
                plan.Before.Length != journal.keyCount || plan.After.Length != journal.keyCount ||
                !plan.Before.Select(value => value.classify + ":" + value.name + ":" + value.index).SequenceEqual(plan.After.Select(value => value.classify + ":" + value.name + ":" + value.index)))
                throw new InvalidOperationException("恢复值不属于原/新完整逻辑集合。");
            if (journal.origin.storage == "SQLite")
            {
                string databaseBackup = System.IO.Path.Combine(directory, "before-sqlite.db");
                if (!FileFragmentMaintenance.ValidHash(journal.databaseBackupSha256) || FileFragmentMaintenance.Hash(databaseBackup, token) != journal.databaseBackupSha256)
                    throw new InvalidOperationException("一致数据库备份摘要漂移或未完成。");
                using (var backup = OpenMaintenanceDatabase(databaseBackup, privacy.AESKey))
                    if (ValueHash(ReadSQLiteValues(backup, token)) != journal.logicalBeforeSha256) throw new InvalidOperationException("一致数据库备份逻辑不匹配。");
                var component = UnityEditor.GlobalObjectId.GlobalObjectIdentifierToObjectSlow(ParseGlobalObjectId(journal.origin.target.globalObjectId)) as PersistComponent;
                using var serialized = new UnityEditor.SerializedObject(component);
                plan.Password = serialized.FindProperty("m_SQLiteCipherPassword").stringValue;
            }
            return plan;
        }

        /// <summary>PlayerPrefs 允许本操作部分键原/新混合；SQLite 真实事务只接受完整原或新逻辑。</summary>
        private static void AssertRecoverableValues(ValuePlan plan, CancellationToken token)
        {
            if (plan.Journal.origin.storage == "PlayerPrefs")
            {
                ValueEntry[] current = ReadPlayerPrefsValues(HasMaintenancePlayerPrefsKey, ReadPlayerPrefsString, token);
                if (!RecoverablePlayerPrefs(current, plan.Before, plan.After)) throw new InvalidOperationException("存在第三方 PlayerPrefs 修改。");
            }
            else
            {
                using (SQLiteMaintenanceLease.Acquire(plan.Journal.origin.sourceRoot))
                using (var connection = OpenMaintenanceDatabase(plan.Journal.origin.sourceRoot, plan.Password))
                {
                    connection.Execute("BEGIN IMMEDIATE");
                    try
                    {
                        string hash = ValueHash(ReadSQLiteValues(connection, token));
                        if (hash != plan.Journal.logicalBeforeSha256 && hash != plan.Journal.logicalAfterSha256) throw new InvalidOperationException("存在第三方数据库修改。");
                    }
                    finally { connection.Execute("ROLLBACK"); }
                }
            }
        }

        /// <summary>逐键身份和原/新值匹配，拒绝缺失、新增或其它值；未知键不在本恢复范围。</summary>
        internal static bool RecoverablePlayerPrefs(ValueEntry[] current, ValueEntry[] before, ValueEntry[] after) => current.Length == before.Length && before.Length == after.Length &&
            current.Zip(before, (value, original) => new { value, original }).Zip(after, (pair, changed) =>
                pair.value.name == pair.original.name && pair.value.name == changed.name && pair.value.index == pair.original.index &&
                (pair.value.value == pair.original.value || pair.value.value == changed.value)).All(value => value);

        /// <summary>独立恢复新事务与原字段；已结束的迁移事务不作伪回滚。</summary>
        internal static AgentActionResult RecoverValues(ValuePlan plan, CancellationToken token)
        {
            if (plan?.Journal == null || Interlocked.Exchange(ref plan.Consumed, 1) != 0) return AgentActionResult.Create(null, "blocked", "值恢复计划无效或已消费。");
            ValueJournal journal = plan.Journal; bool started = false;
            try
            {
                EnsureIdle(); AssertValueOperationBinding(journal, plan.RecoveryToken); AssertMaster(journal.origin, token); AssertAesSettingsRecoverable(journal.origin, token);
                if (FileFragmentMaintenance.Hash(System.IO.Path.Combine(OperationDirectory(journal.origin.operationId), "values-journal.json"), token) != plan.JournalHash)
                    throw new InvalidOperationException("恢复日志预演后漂移。");
                ValuePlan reloaded = ReadValueRecoveryPlan(journal, token);
                if (ValueHash(reloaded.Before) != ValueHash(plan.Before) || ValueHash(reloaded.After) != ValueHash(plan.After))
                    throw new InvalidOperationException("恢复备份预演后漂移。");
                AssertRecoverableValues(plan, token);
                var view = SerializedTargetEditing.Inspect(journal.origin.target, token);
                if (view.fileSha256 != plan.RecoveryFileHash || view.stateSha256 != plan.RecoveryStateHash)
                    throw new InvalidOperationException("恢复字段预演后漂移。");
                using (AcquireValueScope(journal))
                {
                    token.ThrowIfCancellationRequested(); journal.origin.phase = "recovering"; SaveValueJournal(journal, true); started = true;
                    if (journal.origin.storage == "PlayerPrefs")
                    {
                        ValueEntry[] current = ReadPlayerPrefsValues(HasMaintenancePlayerPrefsKey, ReadPlayerPrefsString, CancellationToken.None);
                        if (!RecoverablePlayerPrefs(current, plan.Before, plan.After)) throw new InvalidOperationException("PlayerPrefs 恢复前出现第三方修改。");
                        foreach (ValueEntry value in plan.Before) SetMaintenancePlayerPrefsString(value.name, value.value);
                        SaveMaintenancePlayerPrefs();
                    }
                    else
                    {
                        using var connection = OpenMaintenanceDatabase(journal.origin.sourceRoot, plan.Password);
                        connection.Execute("BEGIN IMMEDIATE"); bool active = true;
                        try
                        {
                            string hash = ValueHash(ReadSQLiteValues(connection, CancellationToken.None));
                            if (hash != journal.logicalBeforeSha256 && hash != journal.logicalAfterSha256) throw new InvalidOperationException("数据库恢复前出现第三方修改。");
                            ApplySQLiteValues(connection, plan.Before);
                            if (ValueHash(ReadSQLiteValues(connection, CancellationToken.None)) != journal.logicalBeforeSha256) throw new InvalidOperationException("恢复事务后验不匹配。");
                            connection.Execute("COMMIT"); active = false;
                        }
                        finally { if (active) connection.Execute("ROLLBACK"); }
                    }
                    AgentActionResult saved = SerializedTargetEditing.RestoreAesRecoveryTarget(journal.origin.settings, journal.origin.storage, journal.origin.beforeAES, journal.origin.afterAES,
                        () => AesDiskMatchesBoundOrigin(journal.origin, CancellationToken.None), CancellationToken.None);
                    if (saved.Status != "success") throw new InvalidOperationException("原字段保存失败。");
                    journal.origin.savedSettings = JsonConvert.DeserializeObject<SerializedTargetEditing.Receipt>(saved.ReceiptJson);
                    journal.origin.phase = "recovered"; SaveValueJournal(journal, true);
                }
                return VerifyValues(JsonConvert.SerializeObject(journal), CancellationToken.None);
            }
            catch (OperationCanceledException) when (!started) { throw; }
            catch { return ValueResult(journal, started ? "partial" : "blocked", "值恢复未通过；原/新恢复材料保留，未覆盖可确认的第三方数据。PlayerPrefs 全局提交影响不可恢复。"); }
        }

        /// <summary>重新验证真实日志、完整逻辑集合与唯一字段，不将同缓存观察当作设备耐久性。</summary>
        internal static AgentActionResult VerifyValues(string receiptJson, CancellationToken token)
        {
            try
            {
                ValueJournal receipt = JsonConvert.DeserializeObject<ValueJournal>(receiptJson, StrictValueJson);
                ValidateValueJournal(receipt, receipt.origin.operationId); EnsureIdle(); AssertMaster(receipt.origin, token);
                if (receipt.origin.phase == "planned") return ValueResult(receipt, "blocked", "值计划未执行。");
                if (receipt.origin.phase == "unchanged")
                {
                    AssertSettingsBefore(receipt.origin, token);
                    if (receipt.origin.storage == "PlayerPrefs")
                    {
                        if (ValueHash(ReadPlayerPrefsValues(HasMaintenancePlayerPrefsKey, ReadPlayerPrefsString, token)) != receipt.logicalBeforeSha256)
                            throw new InvalidOperationException("未写状态源漂移。");
                    }
                    else
                    {
                        string path = receipt.origin.sourceRoot;
                        if (InspectSQLiteFiles(path, token).metadataSha256 != receipt.metadataSha256) throw new InvalidOperationException("未写数据库元数据漂移。");
                    }
                    return ValueResult(receipt, "success", "状态已满足，未产生值维护写入。");
                }
                ValueJournal journal = LoadValueJournal(receipt.origin.operationId);
                if (!SameValueOrigin(receipt, journal)) throw new InvalidOperationException("值收据与日志不匹配。");
                ValuePlan plan = ReadValueRecoveryPlan(journal, token);
                if (journal.origin.phase != "committed" && journal.origin.phase != "recovered")
                    return ValueResult(journal, "partial", "值日志尚未完成；先独立计划恢复，不重放迁移。");
                string expected = journal.origin.phase == "recovered" ? journal.logicalBeforeSha256 : journal.logicalAfterSha256;
                string actual;
                if (journal.origin.storage == "PlayerPrefs") actual = ValueHash(ReadPlayerPrefsValues(HasMaintenancePlayerPrefsKey, ReadPlayerPrefsString, token));
                else
                {
                    using (SQLiteMaintenanceLease.Acquire(journal.origin.sourceRoot))
                    using (var connection = OpenMaintenanceDatabase(journal.origin.sourceRoot, plan.Password))
                    {
                        connection.Execute("BEGIN IMMEDIATE");
                        try { actual = ValueHash(ReadSQLiteValues(connection, token)); }
                        finally { connection.Execute("ROLLBACK"); }
                    }
                }
                if (actual != expected) throw new InvalidOperationException("值保存后验不匹配。");
                VerifyAesSavedSettings(journal.origin, token);
                return ValueResult(journal, "success", "完整托管逻辑、配置和受保护备份已重核；目标平台重启耐久性另验，PlayerPrefs 全局 Save 影响不可恢复。");
            }
            catch (OperationCanceledException) { throw; }
            catch { return AgentActionResult.Create(null, "partial", "值维护后验未通过；未执行额外玩家值写入，SQLite 查询可能改变伴随文件。"); }
        }
    }
}
