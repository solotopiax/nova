/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistenceMaintenance.CipherRecovery.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   SQLite Cipher独立恢复与验证
 ***************************************************************/

using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using SqlCipher4Unity3D;

namespace NovaFramework.Editor
{
    internal static partial class PersistenceMaintenance
    {
        /// <summary>绑定原迁移token、封存秘密、完整备份及原/新状态，构造新的独立恢复计划。</summary>
        internal static CipherPlan BuildCipherRecovery(string operationId, string recoveryToken, CancellationToken token)
        {
            EnsureIdle();
            CipherJournal journal = LoadCipherJournal(operationId);
            if (!PersistenceCredentialVault.IsSupported || !AgentActionRuntime.OperationStore.TryLoad(recoveryToken, "nova.project.persistence.migrate-encryption", 1, out var operation))
                throw new InvalidOperationException("宿主或原迁移凭据无效。");
            var envelope = JsonConvert.DeserializeObject<AgentActionReceiptEnvelope>(operation.ReceiptJson);
            var original = JsonConvert.DeserializeObject<CipherJournal>(envelope?.PayloadJson, CipherJson);
            if (envelope?.ActionId != "nova.project.persistence.migrate-encryption" || envelope.ContractMajor != 1 || !SameCipherOrigin(original, journal) || journal.phase == "recovered")
                throw new InvalidOperationException("Cipher 日志未绑定原迁移或已恢复。");
            var secrets = PersistenceCredentialVault.Unseal(journal.recoveryCredentialHandle);
            AssertCipherSettingsBinding(journal, secrets, token);
            AssertCipherBackup(journal, secrets, token);
            using (NovaFramework.Runtime.SQLiteMaintenanceLease.Acquire(journal.sourceRoot))
                ReadCurrentCipher(journal, secrets, token);
            return new CipherPlan { Journal = journal, Recovery = true,
                JournalHash = FileFragmentMaintenance.Hash(System.IO.Path.Combine(OperationDirectory(operationId), "cipher-journal.json"), token),
                CurrentMetadataHash = InspectSQLiteFiles(journal.sourceRoot, token).metadataSha256 };
        }
        /// <summary>把日志两个整资产状态重新绑定到 vault 的旧/新密码；日志不能伪造任意敏感字段恢复目标。</summary>
        private static void AssertCipherSettingsBinding(CipherJournal journal, PersistenceCredentialVault.RecoverySecrets secrets, CancellationToken token)
        {
            SerializedTargetEditing.AssertCipherRecoveryTarget(journal.beforeSettings, journal.afterSettings, journal.savedSettings,
                secrets.Before, secrets.After, () => CipherDiskMatchesBoundOrigin(journal, secrets, token), token);
        }
        /// <summary>当前完整逻辑必须与原备份完全相同，且可用本操作旧或新密码读取；不恢复第三方数据。</summary>
        private static string ReadCurrentCipher(CipherJournal journal, PersistenceCredentialVault.RecoverySecrets secrets, CancellationToken token)
        {
            foreach (string password in new[] { secrets.Before, secrets.After })
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var connection = OpenMaintenanceDatabase(journal.sourceRoot, password);
                    connection.Execute("BEGIN IMMEDIATE");
                    try
                    {
                        var entries = ReadSQLiteValues(connection, token);
                        if (entries.Length != journal.keyCount || ValueHash(entries) != journal.logicalSha256)
                            throw new InvalidOperationException("Cipher 当前值被第三方更改。");
                        return password;
                    }
                    finally { connection.Execute("ROLLBACK"); }
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }
            throw new InvalidOperationException("Cipher 当前完整逻辑或凭据不属于本操作，拒绝覆盖。");
        }
        /// <summary>重新核计划后同库恢复旧Cipher及原字段；提交后失败保持独立可恢复partial。</summary>
        internal static AgentActionResult RecoverCipher(CipherPlan plan, CancellationToken token)
        {
            if (plan?.Journal == null || !plan.Recovery || Interlocked.Exchange(ref plan.Consumed, 1) != 0)
                return AgentActionResult.Create(null, "blocked", "Cipher 恢复计划无效或已消费。");
            CipherJournal journal = plan.Journal; bool started = false;
            try
            {
                EnsureIdle();
                if (FileFragmentMaintenance.Hash(System.IO.Path.Combine(OperationDirectory(journal.operationId), "cipher-journal.json"), token) != plan.JournalHash)
                    throw new InvalidOperationException("恢复日志发生变化。");
                var secrets = PersistenceCredentialVault.Unseal(journal.recoveryCredentialHandle);
                AssertCipherSettingsBinding(journal, secrets, token); AssertCipherBackup(journal, secrets, token);
                using (NovaFramework.Runtime.SQLiteMaintenanceLease.Acquire(journal.sourceRoot))
                {
                    if (InspectSQLiteFiles(journal.sourceRoot, token).metadataSha256 != plan.CurrentMetadataHash) throw new InvalidOperationException("恢复源变化。");
                    string current = ReadCurrentCipher(journal, secrets, token);
                    AssertCipherSettingsBinding(journal, secrets, token);
                    using (var connection = OpenMaintenanceDatabase(journal.sourceRoot, current))
                    {
                        connection.Execute("BEGIN IMMEDIATE"); bool active = true;
                        try
                        {
                            AssertCipherBackup(journal, secrets, token);
                            if (ValueHash(ReadSQLiteValues(connection, token)) != journal.logicalSha256) throw new InvalidOperationException("恢复事务源变化。");
                            token.ThrowIfCancellationRequested(); journal.phase = "recovering"; SaveCipherJournal(journal, true); started = true;
                            if (current != secrets.Before) RekeySQLite(connection, secrets.Before);
                            if (ValueHash(ReadSQLiteValues(connection, CancellationToken.None)) != journal.logicalSha256) throw new InvalidOperationException("Cipher 恢复逻辑后验失败。");
                            connection.Execute("COMMIT"); active = false;
                        }
                        finally { if (active) connection.Execute("ROLLBACK"); }
                    }
                    var saved = SerializedTargetEditing.RestoreCipherRecoveryTarget(journal.beforeSettings, journal.afterSettings,
                        journal.savedSettings, secrets.Before, secrets.After, () => CipherDiskMatchesBoundOrigin(journal, secrets, CancellationToken.None), CancellationToken.None);
                    if (saved.Status != "success") throw new InvalidOperationException("恢复后字段保存未通过。");
                    journal.savedSettings = JsonConvert.DeserializeObject<SerializedTargetEditing.Receipt>(saved.ReceiptJson);
                    journal.phase = "recovered"; SaveCipherJournal(journal, true);
                }
                return VerifyCipher(JsonConvert.SerializeObject(journal), CancellationToken.None);
            }
            catch (OperationCanceledException) when (!started) { throw; }
            catch { return CipherResult(journal, started ? "partial" : "blocked", started ? "恢复已进入提交，独立备份与封存凭据保留；先验证再重新计划恢复。" : "日志、备份、当前值或完整资产状态变化，未开始恢复写入。"); }
        }
        /// <summary>从持久schema3和Keychain只读核当前库、资产保存收据与恢复材料；不重放。</summary>
        internal static AgentActionResult VerifyCipher(string receiptJson, CancellationToken token)
        {
            CipherJournal receipt = null;
            try
            {
                receipt = JsonConvert.DeserializeObject<CipherJournal>(receiptJson, CipherJson);
                if (receipt == null || receipt.schemaVersion != 3 || !HasCipherJournal(receipt.operationId))
                    throw new InvalidOperationException("Cipher 缺少提交日志。");
                CipherJournal journal = LoadCipherJournal(receipt.operationId);
                if (!SameCipherOrigin(receipt, journal)) throw new InvalidOperationException("Cipher 收据来源不符。");
                if (journal.phase != "committed" && journal.phase != "recovered")
                    return CipherResult(journal, "partial", "Cipher 未形成完整提交证据，需独立恢复。");
                EnsureIdle();
                var secrets = PersistenceCredentialVault.Unseal(journal.recoveryCredentialHandle);
                AssertCipherSettingsBinding(journal, secrets, token); AssertCipherBackup(journal, secrets, token);
                string expected = journal.phase == "recovered" ? secrets.Before : secrets.After;
                var view = SerializedTargetEditing.Inspect(journal.target, token);
                if (ReadCipherPassword(journal.target) != expected || journal.savedSettings == null || !journal.savedSettings.executionStarted ||
                    view.stateSha256 != journal.savedSettings.expectedStateSha256 || view.fileSha256 != journal.savedSettings.observedFileSha256 || !view.editable)
                    throw new InvalidOperationException("Cipher 保存字段证据不符。");
                using (NovaFramework.Runtime.SQLiteMaintenanceLease.Acquire(journal.sourceRoot))
                {
                    if (ReadCurrentCipher(journal, secrets, token) != expected) throw new InvalidOperationException("Cipher 当前库不是预期凭据。");
                }
                return CipherResult(journal, "success", journal.phase == "recovered" ? "已在原库恢复旧 Cipher 与原组件字段，逻辑值和独立加密备份一致。" : "已在原库轮换 Cipher 并保存唯一组件字段，逻辑值和独立加密备份一致；设备耐久性另验。");
            }
            catch (OperationCanceledException) { throw; }
            catch { return receipt == null ? AgentActionResult.Create(null, "blocked", "Cipher 收据无法验证。") :
                CipherResult(receipt, "partial", "Cipher 当前库、资产或恢复材料未通过完整验证；保留现场。"); }
        }
    }
}
