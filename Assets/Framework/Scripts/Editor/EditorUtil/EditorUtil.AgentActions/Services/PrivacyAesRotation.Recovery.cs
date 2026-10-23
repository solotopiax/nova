/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrivacyAesRotation.Recovery.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   默认AES独立恢复及持久最终状态校验
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    internal static partial class PersistenceMaintenance
    {
        /// <summary>schema4 恢复分流只探测本项目规范 operationId 固定文件，不接受任意日志路径。</summary>
        internal static bool HasPrivacyJournal(string operationId) => File.Exists(Path.Combine(PrivacyOperationDirectory(operationId), PrivacyJournalName));

        /// <summary>不可变原身份来自原 migrate-encryption 收据；执行生成的目标摘要不混入原计划身份。</summary>
        internal static bool SamePrivacyOrigin(PrivacyJournal first, PrivacyJournal second) => first != null && second != null &&
            first.schemaVersion == 4 && second.schemaVersion == 4 && first.operation == "rotate-privacy-aes" && second.operation == first.operation &&
            first.projectRoot == second.projectRoot && first.operationId == second.operationId && first.coordinate == second.coordinate &&
            first.sourceSha256 == second.sourceSha256 && first.dataScope == second.dataScope &&
            first.generateNewCredential == second.generateNewCredential && first.targetCredentialHandle == second.targetCredentialHandle &&
            first.recoveryCredentialHandle == second.recoveryCredentialHandle && first.placeholderTime == second.placeholderTime &&
            first.acknowledgeEditorOnly && second.acknowledgeEditorOnly && first.acknowledgeDefaultAesConsumerScope && second.acknowledgeDefaultAesConsumerScope &&
            first.acknowledgeGlobalSave && second.acknowledgeGlobalSave &&
            JsonConvert.SerializeObject(first.target) == JsonConvert.SerializeObject(second.target) && first.targetStateSha256 == second.targetStateSha256 &&
            first.targetFileSha256 == second.targetFileSha256 && first.targetMetaSha256 == second.targetMetaSha256 && first.targetScriptPath == second.targetScriptPath &&
            first.targetScriptSha256 == second.targetScriptSha256 && SamePrivacyAssetOrigin(first.master, second.master) &&
            SamePrivacyAssetOrigin(first.runtime, second.runtime) && SamePrivacyAssetOrigin(first.yoo, second.yoo) &&
            first.coordinates != null && second.coordinates != null && first.coordinates.SequenceEqual(second.coordinates) &&
            first.backends?.Length == 3 && second.backends?.Length == 3 && first.backends.Where((value, index) =>
                !SamePrivacyBackendSource(value, second.backends[index]) || JsonConvert.SerializeObject(value.entries?.Select(entry => new { entry.name, entry.beforeSha256, entry.byteCount, entry.itemCount })) !=
                JsonConvert.SerializeObject(second.backends[index].entries?.Select(entry => new { entry.name, entry.beforeSha256, entry.byteCount, entry.itemCount }))).Any() == false;

        /// <summary>整资产原 GUID/meta/file/semantic 状态不可变，目标及实际保存摘要由封存 pair 重新绑定。</summary>
        private static bool SamePrivacyAssetOrigin(PrivacyAsset first, PrivacyAsset second) => first == null && second == null || first != null && second != null &&
            first.guid == second.guid && first.path == second.path && first.metaSha256 == second.metaSha256 &&
            first.identitySha256 == second.identitySha256 && first.beforeFileSha256 == second.beforeFileSha256 && first.beforeStateSha256 == second.beforeStateSha256;

        /// <summary>有界严格日志只支持固定scope、三后端、项目资产与固定材料名，未知结构不成为文件恢复工具。</summary>
        private static PrivacyJournal LoadPrivacyJournal(string operationId)
        {
            string path = Path.Combine(PrivacyOperationDirectory(operationId), PrivacyJournalName); FileFragmentMaintenance.CheckPath(path);
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) throw new InvalidOperationException("协调日志缺失或超限。");
            PrivacyJournal journal = ParsePrivacyObject(File.ReadAllText(path), 1024 * 1024).ToObject<PrivacyJournal>(JsonSerializer.Create(PrivacyJson));
            if (journal.schemaVersion != 4 || journal.operationId != operationId || journal.projectRoot != PrivacyProjectRoot || journal.operation != "rotate-privacy-aes" ||
                journal.dataScope != PrivacyScope || !journal.acknowledgeEditorOnly || !journal.acknowledgeDefaultAesConsumerScope || !journal.acknowledgeGlobalSave ||
                !FileFragmentMaintenance.ValidHash(journal.sourceSha256) || !PersistenceCredentialVault.ValidHandle(journal.targetCredentialHandle) ||
                !PersistenceCredentialVault.ValidHandle(journal.recoveryCredentialHandle) || !SerializedTargetEditing.ValidateTarget(journal.target, out _) ||
                journal.backends?.Length != 3 || !journal.backends.Select(value => value.storage).SequenceEqual(new[] { "FileFragment", "PlayerPrefs", "SQLite" }) ||
                journal.backends[0].sourceRoot != Path.GetFullPath(PrivacyFileRoot) ||
                journal.backends[1].sourceRoot != "UnityEngine.PlayerPrefs:managed-indexes" ||
                journal.backends[2].sourceRoot != Path.GetFullPath(PrivacyDatabasePath) ||
                journal.backends.Any(value => value.count < 0 || value.count > 16384 || !FileFragmentMaintenance.ValidHash(value.beforeSha256) || !FileFragmentMaintenance.ValidHash(value.afterSha256)) ||
                journal.coordinates == null || journal.coordinates.Length == 0 || journal.coordinates.Length > 4096 ||
                !journal.coordinates.SequenceEqual(journal.coordinates.Distinct().OrderBy(value => value, StringComparer.Ordinal)))
                throw new InvalidOperationException("协调日志身份或固定范围无效。");
            ParsePrivacyCoordinate(journal.coordinate);
            foreach (string coordinate in journal.coordinates) ParsePrivacyCoordinate(coordinate);
            foreach (PrivacyAsset asset in new[] { journal.master, journal.runtime, journal.yoo }.Where(value => value != null))
                if (!GenerateActionCommon.TryValidateGuid(asset.guid, out _) || !asset.path.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !GenerateActionCommon.TryResolveProjectPath(asset.path, "asset", out _, out _) ||
                    !FileFragmentMaintenance.ValidHash(asset.metaSha256) || !FileFragmentMaintenance.ValidHash(asset.beforeFileSha256) ||
                    !FileFragmentMaintenance.ValidHash(asset.identitySha256) || !FileFragmentMaintenance.ValidHash(asset.beforeStateSha256)) throw new InvalidOperationException("配置资产身份无效。");
            if (journal.master == null || journal.runtime == null || !FileFragmentMaintenance.ValidHash(journal.master.afterStateSha256) ||
                !FileFragmentMaintenance.ValidHash(journal.runtime.afterStateSha256) || journal.recoveryFiles == null || journal.recoveryFiles.Count < 8 || journal.recoveryFiles.Count > 9)
                throw new InvalidOperationException("完整恢复目标或材料不足。");
            string[] required = { "before-master.bin", "after-master.bin", "before-runtime.bin", "after-runtime.bin", "before-prefs.bin", "after-prefs.bin", "before-sqlite-values.bin", "after-sqlite-values.bin" };
            if (required.Any(name => !journal.recoveryFiles.ContainsKey(name)) || journal.recoveryFiles.Keys.Any(name => !required.Contains(name) && name != "before-privacy.db") ||
                journal.recoveryFiles.Values.Any(hash => !FileFragmentMaintenance.ValidHash(hash))) throw new InvalidOperationException("恢复材料表无效。");
            if (journal.backends[0].entries != null) FileFragmentMaintenance.ValidateEntries(journal.backends[0].entries);
            return journal;
        }

        /// <summary>独立恢复先验证原迁移token、封存秘密、全部备份和当前原/新状态；返回计划没有明文恢复数据。</summary>
        internal static PrivacyPlan BuildPrivacyRecovery(string operationId, string recoveryToken, CancellationToken token)
        {
            EnsureIdle(); PrivacyJournal journal = LoadPrivacyJournal(operationId);
            if (!PersistenceCredentialVault.IsSupported ||
                !AgentActionRuntime.OperationStore.TryLoad(recoveryToken, "nova.project.persistence.migrate-encryption", 1, out var operation))
                throw new InvalidOperationException("恢复宿主、阶段或原迁移token无效。");
            var envelope = JsonConvert.DeserializeObject<AgentActionReceiptEnvelope>(operation.ReceiptJson);
            PrivacyJournal original = envelope?.PayloadJson == null ? null : JsonConvert.DeserializeObject<PrivacyJournal>(envelope.PayloadJson, PrivacyJson);
            if (envelope?.ActionId != "nova.project.persistence.migrate-encryption" || envelope.ContractMajor != 1 || !SamePrivacyOrigin(original, journal))
                throw new InvalidOperationException("协调日志未绑定原迁移来源。");
            var plan = new PrivacyPlan { Journal = journal, Recovery = true };
            try
            {
                using (AcquirePrivacyLease(journal, true))
                {
                    ReadPrivacyRecoveryMaterial(plan, token); AssertPrivacyAssets(journal, true, token); AssertPrivacyRecoverable(plan, token);
                }
                plan.JournalHash = FileFragmentMaintenance.Hash(Path.Combine(PrivacyOperationDirectory(operationId), PrivacyJournalName), token);
                plan.Before = null; plan.After = null; plan.DatabasePassword = null; plan.BeforeMasterJson = null; plan.AfterMasterJson = null;
                plan.BeforeRuntimeJson = null; plan.AfterRuntimeJson = null; plan.BeforePlayerPrefs = null; plan.AfterPlayerPrefs = null; plan.BeforeSQLite = null; plan.AfterSQLite = null;
                return plan;
            }
            catch { plan.Dispose(); throw; }
        }

        /// <summary>只读解开固定受保护文件，再以原摘要和原厂转换重建目标绑定，不允许伪造任意恢复内容。</summary>
        private static void ReadPrivacyRecoveryMaterial(PrivacyPlan plan, CancellationToken token)
        {
            PrivacyJournal journal = plan.Journal; var secrets = PersistenceCredentialVault.Unseal(journal.recoveryCredentialHandle);
            plan.Before = ParsePrivacyPair(secrets.Before); plan.After = ParsePrivacyPair(secrets.After);
            foreach (var pair in journal.recoveryFiles)
            {
                string path = Path.Combine(PrivacyOperationDirectory(journal.operationId), pair.Key); FileFragmentMaintenance.CheckPath(path);
                if (FileFragmentMaintenance.Hash(path, token) != pair.Value) throw new InvalidOperationException("受保护恢复材料漂移。");
            }
            Func<string, string> read = name => ReadProtectedValues(Path.Combine(PrivacyOperationDirectory(journal.operationId), name), secrets.Backup).Single(value => value.name == "payload" && value.index).value;
            plan.BeforeMasterJson = read("before-master.bin"); plan.AfterMasterJson = read("after-master.bin");
            plan.BeforeRuntimeJson = read("before-runtime.bin"); plan.AfterRuntimeJson = read("after-runtime.bin");
            plan.BeforePlayerPrefs = JsonConvert.DeserializeObject<ValueEntry[]>(read("before-prefs.bin"), PrivacyJson);
            plan.AfterPlayerPrefs = JsonConvert.DeserializeObject<ValueEntry[]>(read("after-prefs.bin"), PrivacyJson);
            plan.BeforeSQLite = JsonConvert.DeserializeObject<ValueEntry[]>(read("before-sqlite-values.bin"), PrivacyJson);
            plan.AfterSQLite = JsonConvert.DeserializeObject<ValueEntry[]>(read("after-sqlite-values.bin"), PrivacyJson);
            if (ValueHash(plan.BeforePlayerPrefs) != journal.backends[1].beforeSha256 || ValueHash(plan.AfterPlayerPrefs) != journal.backends[1].afterSha256 ||
                ValueHash(plan.BeforeSQLite) != journal.backends[2].beforeSha256 || ValueHash(plan.AfterSQLite) != journal.backends[2].afterSha256 ||
                ValueHash(RotatePrivacyValues(plan.BeforePlayerPrefs, plan.Before, plan.After)) != journal.backends[1].afterSha256 ||
                ValueHash(RotatePrivacyValues(plan.BeforeSQLite, plan.Before, plan.After)) != journal.backends[2].afterSha256)
                throw new InvalidOperationException("恢复值或新 pair 未绑定原来源。");
            AssertPrivacyConfigMaterial(plan);
            PersistComponent component = ReadMetadataTarget(new Request { targetGuid = journal.target.targetGuid, globalObjectId = journal.target.globalObjectId }, out _);
            using (var serialized = new SerializedObject(component)) plan.DatabasePassword = serialized.FindProperty("m_SQLiteCipherPassword").stringValue;
            if (journal.backends[2].enabled && journal.backends[2].present)
            {
                if (!journal.recoveryFiles.ContainsKey("before-privacy.db")) throw new InvalidOperationException("缺少独立数据库备份。");
                using var connection = OpenMaintenanceDatabase(Path.Combine(PrivacyOperationDirectory(journal.operationId), "before-privacy.db"), secrets.Backup);
                if (ValueHash(ReadSQLiteValues(connection, token)) != journal.backends[2].beforeSha256) throw new InvalidOperationException("独立数据库备份逻辑不一致。");
            }
        }

        /// <summary>原 Master 和 Runtime 必须匹配原token完整hash，唯一允许的目标差异由 Vault pair重算，不复制任意新配置。</summary>
        private static void AssertPrivacyConfigMaterial(PrivacyPlan plan)
        {
            var before = ScriptableObject.CreateInstance<ConfigMasterSO>(); var after = ScriptableObject.CreateInstance<ConfigMasterSO>();
            var runtime = ScriptableObject.CreateInstance<ConfigRuntimeSO>(); var targetRuntime = ScriptableObject.CreateInstance<ConfigRuntimeSO>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(plan.BeforeMasterJson, before); EditorJsonUtility.FromJsonOverwrite(plan.AfterMasterJson, after);
                EditorJsonUtility.FromJsonOverwrite(plan.BeforeRuntimeJson, runtime); EditorJsonUtility.FromJsonOverwrite(plan.AfterRuntimeJson, targetRuntime);
                if (PrivacyStateHash(before) != plan.Journal.master.beforeStateSha256 || PrivacyStateHash(runtime) != plan.Journal.runtime.beforeStateSha256)
                    throw new InvalidOperationException("原配置恢复材料未绑定原token。");
                if (!PrivacyCoordinates(before, ParsePrivacyCoordinate(plan.Journal.coordinate), plan.Before).SequenceEqual(plan.Journal.coordinates))
                    throw new InvalidOperationException("原逻辑组与pair不一致。");
                ApplyPrivacyPair(before, plan.Journal.coordinates, plan.Before, plan.After);
                if (PrivacyStateHash(before) != plan.Journal.master.afterStateSha256 || PrivacyStateHash(after) != plan.Journal.master.afterStateSha256)
                    throw new InvalidOperationException("目标Master非唯一pair替换。");
                runtime.PrivacyConfigs.AESKey = plan.After.key; runtime.PrivacyConfigs.AESIV = plan.After.iv;
                if (PrivacyStateHash(runtime) != plan.Journal.runtime.afterStateSha256 || PrivacyStateHash(targetRuntime) != plan.Journal.runtime.afterStateSha256)
                    throw new InvalidOperationException("目标Runtime非唯一pair替换。");
            }
            finally { UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after); UnityEngine.Object.DestroyImmediate(runtime); UnityEngine.Object.DestroyImmediate(targetRuntime); }
        }

        /// <summary>当前文件必须是原/新字节、PlayerPrefs每键原/新值，SQLite整个事务原/新逻辑；任何第三方写入拒绝覆盖。</summary>
        private static void AssertPrivacyRecoverable(PrivacyPlan plan, CancellationToken token)
        {
            PrivacyJournal journal = plan.Journal; PrivacyBackend files = journal.backends[0], prefs = journal.backends[1], sqlite = journal.backends[2];
            if (Directory.Exists(files.sourceRoot) != files.present || PrivacyPrefsHasKey(PlayerPrefsIndexKey) != prefs.present ||
                InspectSQLiteFiles(sqlite.sourceRoot, token).files[0].present != sqlite.present) throw new InvalidOperationException("后端存在性变化。");
            if (files.entries != null)
            {
                FileFragmentMaintenance.AssertRecoverable(files.sourceRoot, files.entries, journal.operationId, PrivacyOperationDirectory(journal.operationId), token);
                foreach (var entry in files.entries)
                {
                    string backup = Path.Combine(PrivacyOperationDirectory(journal.operationId), "backup", entry.name), path = Path.Combine(files.sourceRoot, entry.name);
                    var original = new FileFragmentItemGroup(); var actual = new FileFragmentItemGroup();
                    bool after = FileFragmentMaintenance.Hash(path, token) == entry.afterSha256;
                    PrivacyPair pair = after ? plan.After : plan.Before;
                    if (!original.Deserialize(backup, true, plan.Before.key, plan.Before.iv) || !actual.Deserialize(path, true, pair.key, pair.iv) ||
                        original.Count != actual.Count || original.Items.Any(value => !actual.Items.TryGetValue(value.Key, out string text) || text != value.Value))
                        throw new InvalidOperationException("当前分片非完整原数据。");
                }
            }
            else if (files.enabled && files.present && Directory.GetFiles(files.sourceRoot).Any(path => Path.GetFileName(path) != FileFragmentMaintenance.LockName))
                throw new InvalidOperationException("空分片目录出现新数据。");
            if (prefs.enabled && prefs.present && !RecoverablePlayerPrefs(ReadPrivacyPrefs(token), plan.BeforePlayerPrefs, plan.AfterPlayerPrefs))
                throw new InvalidOperationException("PlayerPrefs含第三方值或索引变化。");
            if (sqlite.enabled && sqlite.present)
            {
                using var connection = OpenMaintenanceDatabase(sqlite.sourceRoot, plan.DatabasePassword); connection.Execute("BEGIN IMMEDIATE");
                try { string hash = ValueHash(ReadSQLiteValues(connection, token)); if (hash != sqlite.beforeSha256 && hash != sqlite.afterSha256) throw new InvalidOperationException("SQLite非本操作完整原/新逻辑。"); }
                finally { connection.Execute("ROLLBACK"); }
            }
        }

        /// <summary>恢复用新的协调计划逐后端还原，最后正式保存Master并重导Runtime；失败保留门禁和材料，不自动重放。</summary>
        internal static AgentActionResult RecoverPrivacyRotation(PrivacyPlan plan, CancellationToken token)
        {
            if (plan?.Journal == null || !plan.Recovery || Interlocked.Exchange(ref plan.Consumed, 1) != 0)
                return AgentActionResult.Create(null, "blocked", "隐私 AES 恢复计划无效或已消费。");
            bool started = false; PrivacyJournal journal = plan.Journal;
            try
            {
                EnsureIdle();
                if (FileFragmentMaintenance.Hash(Path.Combine(PrivacyOperationDirectory(journal.operationId), PrivacyJournalName), token) != plan.JournalHash)
                    throw new InvalidOperationException("恢复日志预演后变化。");
                using (AcquirePrivacyLease(journal, true))
                {
                    ReadPrivacyRecoveryMaterial(plan, token); AssertPrivacyAssets(journal, true, token); AssertPrivacyRecoverable(plan, token);
                    string marker = NovaFramework.Runtime.PrivacyAesMaintenanceGuard.MarkerPath(journal.projectRoot);
                    if (!File.Exists(marker)) CreatePrivacyMarker(journal);
                    token.ThrowIfCancellationRequested(); journal.phase = "recovering"; SavePrivacyJournal(journal); started = true;
                    if (journal.backends[2].enabled && journal.backends[2].present) ApplyPrivacySQLite(plan, true, CancellationToken.None);
                    if (journal.backends[1].enabled && journal.backends[1].present)
                    {
                        ValueEntry[] current = ReadPrivacyPrefs(CancellationToken.None);
                        if (!RecoverablePlayerPrefs(current, plan.BeforePlayerPrefs, plan.AfterPlayerPrefs)) throw new InvalidOperationException("恢复前 PlayerPrefs漂移。");
                        ApplyPrivacyPrefs(current, plan.BeforePlayerPrefs, CancellationToken.None);
                    }
                    // 原token、Vault、完整备份和当前原/新内容已经重核，才允许隔离本操作中断的 .restore 文件。
                    if (journal.backends[0].entries != null) FileFragmentMaintenance.Restore(journal.backends[0].sourceRoot, journal.backends[0].entries,
                        journal.operationId, PrivacyOperationDirectory(journal.operationId), CancellationToken.None, true);
                    SavePrivacyConfiguration(plan, true, CancellationToken.None);
                    foreach (var backend in journal.backends) backend.phase = "original";
                    journal.phase = "recovered"; SavePrivacyJournal(journal);
                    if (journal.backends[0].entries != null) FileFragmentMaintenance.CleanStage(journal.backends[0].sourceRoot, journal.operationId, journal.backends[0].entries);
                    AssertPrivacyFinal(plan, true, CancellationToken.None); ProbePrivacyStage("recovery-verified"); DeletePrivacyMarker(journal);
                    return PrivacyResult(journal, "success", "当前 Editor 原存档与配对 Master/正规 Runtime 已恢复；原封存材料保留，PlayerPrefs全局Save旁效应不能回滚。");
                }
            }
            catch (OperationCanceledException) when (!started) { throw; }
            catch { return PrivacyResult(journal, started ? "partial" : "blocked", started ? "恢复已开始但未形成完整结果，门禁和原材料保留；先核对再重新计划。" : "恢复前来源、日志、材料或第三方数据变化，未开始恢复。"); }
            finally { plan.Dispose(); }
        }

        /// <summary>对schema4持久材料只读验证当前最终状态；不回放迁移或恢复，不把缓存读回表述为设备耐久性。</summary>
        internal static AgentActionResult VerifyPrivacyRotation(string receiptJson, CancellationToken token)
        {
            PrivacyJournal receipt = null; PrivacyPlan plan = null;
            try
            {
                receipt = ParsePrivacyObject(receiptJson, 1024 * 1024).ToObject<PrivacyJournal>(JsonSerializer.Create(PrivacyJson));
                PrivacyJournal journal = LoadPrivacyJournal(receipt.operationId);
                if (!SamePrivacyOrigin(receipt, journal)) throw new InvalidOperationException("收据与日志来源不一致。");
                if (journal.phase != "committed" && journal.phase != "recovered") return PrivacyResult(journal, "partial", "多后端协调未完成，启动门禁保留，须独立恢复。");
                EnsureIdle(); plan = new PrivacyPlan { Journal = journal };
                using (AcquirePrivacyLease(journal, true))
                {
                    ReadPrivacyRecoveryMaterial(plan, token); AssertPrivacyAssets(journal, true, token); AssertPrivacyFinal(plan, journal.phase == "recovered", token);
                }
                if (File.Exists(NovaFramework.Runtime.PrivacyAesMaintenanceGuard.MarkerPath(journal.projectRoot)))
                    return PrivacyResult(journal, "partial", "数据与配置完成，但启动门禁仍保留；需独立恢复核实清场。");
                return PrivacyResult(journal, "success", "schema4原来源、封存材料、全部后端与正式配置导出已核验；仅代表当前Editor范围。");
            }
            catch (OperationCanceledException) { throw; }
            catch { return receipt == null ? AgentActionResult.Create(null, "blocked", "隐私 AES收据不能验证。") : PrivacyResult(receipt, "partial", "当前数据、资产或恢复材料未通过，保留现场并停止启动。"); }
            finally { plan?.Dispose(); }
        }

        /// <summary>全数据、完整语义与实际保存文件摘要必须同时符合预期；未验证部分不能继承成功。</summary>
        private static void AssertPrivacyFinal(PrivacyPlan plan, bool restored, CancellationToken token)
        {
            PrivacyJournal journal = plan.Journal; AssertPrivacyAssets(journal, true, token);
            foreach (PrivacyAsset asset in new[] { journal.master, journal.runtime })
            {
                UnityEngine.Object value = AssetDatabase.LoadMainAssetAtPath(asset.path);
                if (EditorUtility.IsDirty(value) || PrivacyStateHash(value) != (restored ? asset.beforeStateSha256 : asset.afterStateSha256) ||
                    !FileFragmentMaintenance.ValidHash(asset.savedFileSha256) || AssetHash(asset.path, token) != asset.savedFileSha256)
                    throw new InvalidOperationException("正式配置保存/导出摘要未通过。");
            }
            if (journal.backends[0].entries != null)
            {
                if (restored) FileFragmentMaintenance.AssertRecoverable(journal.backends[0].sourceRoot, journal.backends[0].entries, journal.operationId, PrivacyOperationDirectory(journal.operationId), token);
                else FileFragmentMaintenance.AssertAfter(journal.backends[0].sourceRoot, journal.backends[0].entries, journal.operationId, token);
                if (restored && journal.backends[0].entries.Any(entry => FileFragmentMaintenance.Hash(Path.Combine(journal.backends[0].sourceRoot, entry.name), token) != entry.beforeSha256))
                    throw new InvalidOperationException("恢复分片未还原原字节。");
            }
            if (journal.backends[1].enabled && journal.backends[1].present && ValueHash(ReadPrivacyPrefs(token)) !=
                (restored ? journal.backends[1].beforeSha256 : journal.backends[1].afterSha256)) throw new InvalidOperationException("托管Prefs最终状态无效。");
            if (journal.backends[2].enabled && journal.backends[2].present)
            {
                using var connection = OpenMaintenanceDatabase(journal.backends[2].sourceRoot, plan.DatabasePassword); connection.Execute("BEGIN IMMEDIATE");
                try { if (ValueHash(ReadSQLiteValues(connection, token)) != (restored ? journal.backends[2].beforeSha256 : journal.backends[2].afterSha256)) throw new InvalidOperationException("SQLite最终状态无效。"); }
                finally { connection.Execute("ROLLBACK"); }
            }
            ConfigMasterSO master = ReadPrivacyMaster(out _, out _); AssertPrivacyRuntimeSource(master, master.ExportTarget, ParsePrivacyCoordinate(journal.coordinate));
        }
    }
}
