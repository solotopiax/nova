/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrivacyAesRotation.Transaction.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   默认AES多后端提交与正式配置保存导出
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    internal static partial class PersistenceMaintenance
    {
        /// <summary>标准后端只迁移已启用 AES 的现存数据；无数据也冻结不存在状态，不新建数据库或索引。</summary>
        private static PrivacyBackend[] ReadPrivacyBackends(PrivacyPlan plan, bool[] enabled, CancellationToken token)
        {
            string fileRoot = PrivacyFileRoot;
            string database = PrivacyDatabasePath;
            var files = new PrivacyBackend { storage = "FileFragment", sourceRoot = Path.GetFullPath(fileRoot), enabled = enabled[0], phase = "original" };
            var prefs = new PrivacyBackend { storage = "PlayerPrefs", sourceRoot = "UnityEngine.PlayerPrefs:managed-indexes", enabled = enabled[1], phase = "original" };
            var sqlite = new PrivacyBackend { storage = "SQLite", sourceRoot = Path.GetFullPath(database), enabled = enabled[2], phase = "original" };
            files.present = Directory.Exists(fileRoot);
            if (files.enabled && files.present)
            {
                if (Directory.GetFiles(fileRoot).Any(path => Path.GetFileName(path) != FileFragmentMaintenance.LockName))
                {
                    plan.Files = FileFragmentMaintenance.Build(fileRoot, true, true, plan.Before.key, plan.Before.iv, token);
                    files.entries = plan.Files.Entries; files.beforeSha256 = plan.Files.SnapshotSha256; files.count = files.entries.Length;
                }
                else files.beforeSha256 = GenerateActionCommon.ComputeTextHash("empty-filefragment");
            }
            else files.beforeSha256 = GenerateActionCommon.ComputeTextHash(files.present ? "disabled-filefragment" : "absent-filefragment");
            prefs.present = PrivacyPrefsHasKey(PlayerPrefsIndexKey);
            plan.BeforePlayerPrefs = prefs.enabled && prefs.present ? ReadPrivacyPrefs(token) : Array.Empty<ValueEntry>();
            prefs.beforeSha256 = ValueHash(plan.BeforePlayerPrefs); prefs.count = plan.BeforePlayerPrefs.Length;
            SQLiteFileMetadata metadata = InspectSQLiteFiles(database, token); sqlite.present = metadata.files[0].present;
            sqlite.metadataSha256 = metadata.metadataSha256;
            plan.BeforeSQLite = Array.Empty<ValueEntry>();
            if (sqlite.enabled && sqlite.present)
            {
                if (metadata.unexpectedEntryCount != 0 || metadata.files[0].byteCount == 0) throw new InvalidOperationException("SQLite 文件范围不满足标准轮换。");
                using (SQLiteMaintenanceLease.Acquire(database))
                using (var connection = OpenMaintenanceDatabase(database, plan.DatabasePassword))
                {
                    connection.Execute("BEGIN IMMEDIATE");
                    try { plan.BeforeSQLite = ReadSQLiteValues(connection, token); }
                    finally { connection.Execute("ROLLBACK"); }
                }
                sqlite.metadataSha256 = InspectSQLiteFiles(database, token).metadataSha256;
            }
            sqlite.beforeSha256 = ValueHash(plan.BeforeSQLite); sqlite.count = plan.BeforeSQLite.Length;
            files.afterSha256 = files.beforeSha256; prefs.afterSha256 = prefs.beforeSha256; sqlite.afterSha256 = sqlite.beforeSha256;
            return new[] { files, prefs, sqlite };
        }

        /// <summary>只读检查所有冻结后端与源资产；输出整体摘要和已知范围，无 native Vault 写入或玩家值回显。</summary>
        internal static AgentActionResult InspectPrivacyRotation(Request request, CancellationToken token)
        {
            try
            {
                using var plan = BuildPrivacyRotation(request, false, token);
                var result = PrivacyResult(plan.Journal, "success", "当前 Editor 标准默认 AES 消费范围已严格检查；未知业务存档、设备与服务端不包含在此结果中。");
                result.ReceiptJson = JsonConvert.SerializeObject(request); return result;
            }
            catch (OperationCanceledException) { throw; }
            catch { return AgentActionResult.Create(null, "blocked", "本机范围、配置、Runtime 源、标准后端、数据稳定性或宿主不满足轮换前置；未写入。"); }
        }

        /// <summary>单次执行唯一协调事务；恢复材料与启动标记落盘先于任何数据变化，配置最后按正式链保存/导出。</summary>
        internal static AgentActionResult ExecutePrivacyRotation(PrivacyPlan plan, CancellationToken token)
        {
            if (plan?.Journal == null || plan.Recovery || Interlocked.Exchange(ref plan.Consumed, 1) != 0)
                return AgentActionResult.Create(null, "blocked", "隐私 AES 计划无效或已消费。");
            PrivacyJournal journal = plan.Journal; string directory = PrivacyOperationDirectory(journal.operationId);
            bool created = false, sealedSecrets = false, createdCredential = false, marked = false, started = false;
            try
            {
                EnsureIdle(); NovaFramework.Runtime.PrivacyAesMaintenanceGuard.AssertProjectReady(PrivacyProjectRoot);
                AssertPrivacyAssets(journal, false, token); StagePrivacyPlan(plan, token); AssertPrivacyBackendSource(plan, token);
                using (AcquirePrivacyLease(journal, false))
                {
                    AssertPrivacyAssets(journal, false, token); AssertPrivacyBackendSource(plan, token);
                    if (Directory.Exists(directory)) throw new InvalidOperationException("维护目录已存在。");
                    Directory.CreateDirectory(directory); created = true;
                    if (journal.generateNewCredential)
                    {
                        PersistenceCredentialVault.StoreCredential(journal.targetCredentialHandle, JsonConvert.SerializeObject(plan.After)); createdCredential = true;
                    }
                    PrivacyPair target = ParsePrivacyPair(PersistenceCredentialVault.ReadCredential(journal.targetCredentialHandle));
                    if (target.key != plan.After.key || target.iv != plan.After.iv) throw new InvalidOperationException("目标凭据封存重读不同。");
                    var secrets = new PersistenceCredentialVault.RecoverySecrets { Before = JsonConvert.SerializeObject(plan.Before),
                        After = JsonConvert.SerializeObject(plan.After), Backup = GeneratePrivacySecret() };
                    ProbePrivacyStage("recovery-sealing");
                    PersistenceCredentialVault.Seal(journal.recoveryCredentialHandle, secrets); sealedSecrets = true;
                    var reopened = PersistenceCredentialVault.Unseal(journal.recoveryCredentialHandle);
                    if (reopened.Before != secrets.Before || reopened.After != secrets.After || reopened.Backup != secrets.Backup)
                        throw new InvalidOperationException("恢复秘密封存重读失败。");
                    journal.recoveryFiles = new Dictionary<string, string>();
                    ProtectPrivacyMaterial(journal, "before-master.bin", plan.BeforeMasterJson, secrets.Backup);
                    ProtectPrivacyMaterial(journal, "after-master.bin", plan.AfterMasterJson, secrets.Backup);
                    ProtectPrivacyMaterial(journal, "before-runtime.bin", plan.BeforeRuntimeJson, secrets.Backup);
                    ProtectPrivacyMaterial(journal, "after-runtime.bin", plan.AfterRuntimeJson, secrets.Backup);
                    ProtectPrivacyMaterial(journal, "before-prefs.bin", JsonConvert.SerializeObject(plan.BeforePlayerPrefs), secrets.Backup);
                    ProtectPrivacyMaterial(journal, "after-prefs.bin", JsonConvert.SerializeObject(plan.AfterPlayerPrefs), secrets.Backup);
                    ProtectPrivacyMaterial(journal, "before-sqlite-values.bin", JsonConvert.SerializeObject(plan.BeforeSQLite), secrets.Backup);
                    ProtectPrivacyMaterial(journal, "after-sqlite-values.bin", JsonConvert.SerializeObject(plan.AfterSQLite), secrets.Backup);
                    if (plan.Files != null) PreparePrivacyFiles(plan, token);
                    PrivacyBackend sqlite = journal.backends.Single(item => item.storage == "SQLite");
                    if (sqlite.enabled && sqlite.present)
                    {
                        string backup = Path.Combine(directory, "before-privacy.db");
                        using (var connection = OpenMaintenanceDatabase(sqlite.sourceRoot, plan.DatabasePassword)) BackupSQLite(connection, backup, secrets.Backup);
                        using (var connection = OpenMaintenanceDatabase(backup, secrets.Backup))
                            if (ValueHash(ReadSQLiteValues(connection, token)) != sqlite.beforeSha256) throw new InvalidOperationException("独立数据库备份不一致。");
                        journal.recoveryFiles.Add("before-privacy.db", FileFragmentMaintenance.Hash(backup, token));
                    }
                    AssertPrivacyAssets(journal, false, token); AssertPrivacyBackendSource(plan, token);
                    SavePrivacyJournal(journal); CreatePrivacyMarker(journal); marked = true;
                    journal.phase = "prepared"; SavePrivacyJournal(journal);
                    token.ThrowIfCancellationRequested(); journal.phase = "committing"; SavePrivacyJournal(journal); started = true; ProbePrivacyStage("committing");
                    if (plan.Files != null) FileFragmentMaintenance.Apply(plan.Files, journal.operationId, CancellationToken.None);
                    journal.backends[0].phase = "new"; SavePrivacyJournal(journal); ProbePrivacyStage("filefragment-committed");
                    if (sqlite.enabled && sqlite.present) ApplyPrivacySQLite(plan, false, CancellationToken.None);
                    sqlite.phase = "new"; SavePrivacyJournal(journal); ProbePrivacyStage("sqlite-committed");
                    PrivacyBackend prefs = journal.backends.Single(item => item.storage == "PlayerPrefs");
                    if (prefs.enabled && prefs.present) ApplyPrivacyPrefs(plan.BeforePlayerPrefs, plan.AfterPlayerPrefs, CancellationToken.None);
                    prefs.phase = "new"; journal.phase = "data-committed"; SavePrivacyJournal(journal); ProbePrivacyStage("data-committed");
                    SavePrivacyConfiguration(plan, false, CancellationToken.None);
                    journal.phase = "committed"; SavePrivacyJournal(journal);
                    if (plan.Files != null) FileFragmentMaintenance.CleanStage(plan.Files.Root, journal.operationId, plan.Files.Entries);
                    AssertPrivacyFinal(plan, false, CancellationToken.None); DeletePrivacyMarker(journal);
                    return PrivacyResult(journal, "success", "当前 Editor 全部受支持后端与配对正式配置已轮换并核验；原材料封存保留，设备及其它坐标需独立验证。");
                }
            }
            catch (OperationCanceledException) when (!started)
            {
                if (created && !CleanPrivacyUncommitted(journal, sealedSecrets, createdCredential, marked))
                    return PrivacyResult(journal, "partial", "未开始数据提交，但维护材料无法安全清理，保留现场。");
                throw;
            }
            catch
            {
                if (!started && created && !CleanPrivacyUncommitted(journal, sealedSecrets, createdCredential, marked))
                    return PrivacyResult(journal, "partial", "未开始数据提交，维护材料无法安全清理，保留现场。");
                return PrivacyResult(journal, started ? "partial" : "blocked", started ?
                    "轮换已进入多后端提交，启动门禁与恢复材料保留；禁止重放，须新建独立恢复计划。" : "轮换提交前条件、凭据或备份未满足，数据与配置未开始写入。");
            }
            finally { plan.Dispose(); }
        }

        /// <summary>复用版本化 FileFragment codec，以新 pair 在既有同文件系统阶段目录生成并完整重读；旧原字节独立保留。</summary>
        private static void PreparePrivacyFiles(PrivacyPlan plan, CancellationToken token)
        {
            string directory = PrivacyOperationDirectory(plan.Journal.operationId), backup = Path.Combine(directory, "backup");
            string stage = FileFragmentMaintenance.StagePath(plan.Files.Root, plan.Journal.operationId);
            if (Directory.Exists(stage) || Directory.Exists(backup)) throw new InvalidOperationException("分片阶段或备份已存在。");
            Directory.CreateDirectory(stage); Directory.CreateDirectory(backup);
            foreach (FileFragmentMaintenance.Entry entry in plan.Files.Entries)
            {
                token.ThrowIfCancellationRequested(); string source = Path.Combine(plan.Files.Root, entry.name), saved = Path.Combine(backup, entry.name);
                FileFragmentMaintenance.CheckPath(source); FileFragmentMaintenance.CheckPath(saved);
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(saved, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { input.CopyTo(output); output.Flush(true); }
                if (FileFragmentMaintenance.Hash(saved, token) != entry.beforeSha256) throw new InvalidOperationException("分片备份漂移。");
                var original = new FileFragmentItemGroup();
                if (!original.Deserialize(saved, true, plan.Before.key, plan.Before.iv)) throw new InvalidOperationException("旧分片解码失败。");
                string prepared = Path.Combine(stage, entry.name); original.Serialize(prepared, true, plan.After.key, plan.After.iv);
                var reopened = new FileFragmentItemGroup();
                if (!reopened.Deserialize(prepared, true, plan.After.key, plan.After.iv) || original.Count != reopened.Count ||
                    original.Items.Any(pair => !reopened.Items.TryGetValue(pair.Key, out string value) || pair.Value != value))
                    throw new InvalidOperationException("新分片完整条目不一致。");
                entry.afterSha256 = FileFragmentMaintenance.Hash(prepared, token);
            }
            plan.Journal.backends[0].afterSha256 = FileFragmentMaintenance.Fingerprint(plan.Files.Entries, true);
        }

        /// <summary>SQLite 已取得维护 lease 后以同库真实写事务核全逻辑；仅更新 Name/Value 行，不改变 Cipher 或 schema。</summary>
        private static void ApplyPrivacySQLite(PrivacyPlan plan, bool recovering, CancellationToken token)
        {
            PrivacyBackend backend = plan.Journal.backends.Single(item => item.storage == "SQLite");
            using var connection = OpenMaintenanceDatabase(backend.sourceRoot, plan.DatabasePassword);
            connection.Execute("BEGIN IMMEDIATE"); bool active = true;
            try
            {
                ValueEntry[] current = ReadSQLiteValues(connection, token);
                string currentHash = ValueHash(current);
                if (currentHash != backend.beforeSha256 && (!recovering || currentHash != backend.afterSha256)) throw new InvalidOperationException("数据库逻辑发生第三方变化。");
                ApplySQLiteValues(connection, recovering ? plan.BeforeSQLite : plan.AfterSQLite);
                if (ValueHash(ReadSQLiteValues(connection, token)) != (recovering ? backend.beforeSha256 : backend.afterSha256)) throw new InvalidOperationException("数据库全行后验失败。");
                token.ThrowIfCancellationRequested(); connection.Execute("COMMIT"); active = false;
            }
            finally { if (active) connection.Execute("ROLLBACK"); }
        }

        /// <summary>整组 Master 使用现有保存链；Runtime 使用正式 Exporter，禁止直接写 Runtime 或隐式生成密钥源码。</summary>
        private static void SavePrivacyConfiguration(PrivacyPlan plan, bool recovering, CancellationToken token)
        {
            PrivacyJournal journal = plan.Journal; AssertPrivacyAssets(journal, true, token);
            ConfigMasterSO master = ReadPrivacyMaster(out _, out _, true);
            string expectedMaster = recovering ? journal.master.beforeStateSha256 : journal.master.afterStateSha256;
            string json = recovering ? plan.BeforeMasterJson : plan.AfterMasterJson;
            ConfigMasterSO clone = ScriptableObject.CreateInstance<ConfigMasterSO>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(json, clone); clone.hideFlags = HideFlags.DontSave;
                if (PrivacyStateHash(clone) != expectedMaster) throw new InvalidOperationException("封存 Master 未绑定完整预演状态。");
                var coordinate = ParsePrivacyCoordinate(journal.coordinate);
                if (PrivacyStateHash(master) != expectedMaster || EditorUtility.IsDirty(master))
                {
                    using var so = new SerializedObject(clone);
                    EditorUtil.Config.DesignTimeEditing.SaveWorkingCopy(master, clone, so, coordinate,
                        new EditorUtil.Config.DesignTimeEditing.SaveOptions { SaveAllAssets = false, CompleteLegacyMigration = false }, normalized =>
                        { if (PrivacyStateHash(normalized) != expectedMaster) throw new InvalidOperationException("保存归一超出冻结写集。"); AssertPrivacyAssets(journal, true, token); });
                }
                if (PrivacyStateHash(master) != expectedMaster || EditorUtility.IsDirty(master)) throw new InvalidOperationException("Master 保存后验不一致。");
                journal.master.savedFileSha256 = AssetHash(journal.master.path, token); journal.phase = recovering ? "recover-master" : "master-committed"; SavePrivacyJournal(journal);
                ProbePrivacyStage(journal.phase);
                ConfigRuntimeSO runtime = EditorUtil.Config.Exporter.Export(master, coordinate.Platform, coordinate.Channel, coordinate.Mode,
                    journal.runtime.path, DateTime.Parse(journal.placeholderTime, null, System.Globalization.DateTimeStyles.RoundtripKind), true, false);
                if (runtime == null || AssetDatabase.GetAssetPath(runtime) != journal.runtime.path ||
                    PrivacyStateHash(runtime) != (recovering ? journal.runtime.beforeStateSha256 : journal.runtime.afterStateSha256) || EditorUtility.IsDirty(runtime))
                    throw new InvalidOperationException("正规 Runtime 导出未形成冻结完整快照。");
                if (journal.yoo != null && AssetHash(journal.yoo.path, token) != journal.yoo.beforeFileSha256)
                    throw new InvalidOperationException("YooAsset 同值导出产生未冻结文件变化，保留现场。");
                journal.runtime.savedFileSha256 = AssetHash(journal.runtime.path, token); journal.phase = recovering ? "recover-runtime" : "runtime-committed"; SavePrivacyJournal(journal);
                ProbePrivacyStage(journal.phase);
                EditorUtil.Config.DesignTimeEditing.NotifyMasterSaved(master);
                AssertPrivacyRuntimeSource(master, runtime, coordinate);
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }

        /// <summary>完整源身份、坐标、meta、脚本和正式资产摘要重核；恢复只允许本操作原/新语义状态。</summary>
        private static void AssertPrivacyAssets(PrivacyJournal journal, bool allowNew, CancellationToken token)
        {
            ConfigMasterSO master = ReadPrivacyMaster(out string masterPath, out string masterGuid, allowNew);
            if (masterGuid != journal.master.guid || masterPath != journal.master.path || Coordinate(master) != journal.coordinate ||
                master.ExportTarget == null || AssetDatabase.GetAssetPath(master.ExportTarget) != journal.runtime.path ||
                !allowNew && EditorUtility.IsDirty(master.ExportTarget)) throw new InvalidOperationException("激活配置或Runtime绑定漂移。");
            foreach (PrivacyAsset asset in new[] { journal.master, journal.runtime, journal.yoo }.Where(item => item != null))
            {
                UnityEngine.Object value = AssetDatabase.LoadMainAssetAtPath(asset.path);
                if (value == null || AssetDatabase.AssetPathToGUID(asset.path) != asset.guid || AssetHash(asset.path + ".meta", token) != asset.metaSha256 ||
                    PrivacyIdentityHash(value) != asset.identitySha256 || EditorUtility.IsDirty(value) && (!allowNew || asset == journal.yoo))
                    throw new InvalidOperationException("完整资产身份或meta漂移。");
                string state = PrivacyStateHash(value);
                if (state != asset.beforeStateSha256 && (!allowNew || state != asset.afterStateSha256)) throw new InvalidOperationException("资产非本操作原/新完整状态。");
                if (!allowNew && AssetHash(asset.path, token) != asset.beforeFileSha256) throw new InvalidOperationException("资产文件源漂移。");
                if (asset == journal.yoo && AssetHash(asset.path, token) != asset.beforeFileSha256) throw new InvalidOperationException("YooAsset 完整文件漂移。");
                if (allowNew && asset != journal.yoo) AssertPrivacyDiskAsset(asset, value.GetType(), token);
            }
            var view = SerializedTargetEditing.Inspect(journal.target, token);
            if (!view.editable || view.stateSha256 != journal.targetStateSha256 || view.fileSha256 != journal.targetFileSha256 ||
                AssetHash(view.assetPath + ".meta", token) != journal.targetMetaSha256 || view.scriptPath != journal.targetScriptPath ||
                AssetHash(view.scriptPath, token) != journal.targetScriptSha256) throw new InvalidOperationException("Persist 组件完整状态漂移。");
        }

        /// <summary>恢复覆盖前读取磁盘独立副本，完整字段只能为原来源或封存目标；不导入真实资产、不信任日志自填 saved SHA。</summary>
        private static void AssertPrivacyDiskAsset(PrivacyAsset asset, Type expectedType, CancellationToken token)
        {
            string absolute = Path.GetFullPath(Path.Combine(GenerateActionCommon.ProjectRoot, asset.path));
            FileFragmentMaintenance.CheckPath(absolute); token.ThrowIfCancellationRequested();
            var info = new FileInfo(absolute);
            if (!info.Exists || info.Length <= 0 || info.Length > 4 * 1024 * 1024) throw new InvalidOperationException("配置磁盘资产缺失或超限。");
            string before = AssetHash(asset.path, token);
            UnityEngine.Object[] copies = null;
            try
            {
                copies = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(absolute);
                if (copies == null || copies.Length != 1 || copies[0] == null || EditorUtility.IsPersistent(copies[0]) || copies[0].GetType() != expectedType ||
                    PrivacyIdentityHash(copies[0]) != asset.identitySha256) throw new InvalidOperationException("配置磁盘副本类型或完整身份不符。");
                string state = PrivacyStateHash(copies[0]);
                if (state != asset.beforeStateSha256 && state != asset.afterStateSha256)
                    throw new InvalidOperationException("磁盘配置不是本操作完整原或新状态，保留未导入的第三方修改。");
                if (AssetHash(asset.path, token) != before) throw new InvalidOperationException("配置磁盘副本读取期间发生变化。");
            }
            finally
            {
                if (copies != null) foreach (UnityEngine.Object copy in copies) if (copy != null && !EditorUtility.IsPersistent(copy)) UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        /// <summary>全体源摘要在备份与提交前重新核实；不存在数据不能在执行期间变成新数据。</summary>
        private static void AssertPrivacyBackendSource(PrivacyPlan plan, CancellationToken token)
        {
            PrivacyBackend files = plan.Journal.backends[0], prefs = plan.Journal.backends[1], sqlite = plan.Journal.backends[2];
            if (Directory.Exists(files.sourceRoot) != files.present) throw new InvalidOperationException("分片目录存在性变化。");
            if (plan.Files != null) FileFragmentMaintenance.AssertBefore(plan.Files, plan.Journal.operationId, token);
            else if (files.enabled && files.present && Directory.GetFiles(files.sourceRoot).Any(path => Path.GetFileName(path) != FileFragmentMaintenance.LockName))
                throw new InvalidOperationException("原空分片目录出现数据。");
            if (PrivacyPrefsHasKey(PlayerPrefsIndexKey) != prefs.present || prefs.enabled && prefs.present &&
                ValueHash(ReadPrivacyPrefs(token)) != prefs.beforeSha256)
                throw new InvalidOperationException("托管 PlayerPrefs 源漂移。");
            if (sqlite.enabled && sqlite.present)
            {
                using var connection = OpenMaintenanceDatabase(sqlite.sourceRoot, plan.DatabasePassword);
                connection.Execute("BEGIN IMMEDIATE");
                try { if (ValueHash(ReadSQLiteValues(connection, token)) != sqlite.beforeSha256) throw new InvalidOperationException("SQLite 逻辑源漂移。"); }
                finally { connection.Execute("ROLLBACK"); }
            }
            else if (InspectSQLiteFiles(sqlite.sourceRoot, token).files[0].present != sqlite.present) throw new InvalidOperationException("SQLite 存在性变化。");
        }

        /// <summary>所有后端共享同一项目写 lease；FF复用目录锁、SQLite复用连接登记门，禁止并行轮换与恢复。</summary>
        private static IDisposable AcquirePrivacyLease(PrivacyJournal journal, bool recovering)
        {
            string marker = NovaFramework.Runtime.PrivacyAesMaintenanceGuard.MarkerPath(journal.projectRoot);
            FileFragmentMaintenance.CheckPath(Path.GetDirectoryName(marker)); Directory.CreateDirectory(Path.GetDirectoryName(marker));
            var scopes = new List<IDisposable>();
            try
            {
                scopes.Add(new FileStream(Path.Combine(Path.GetDirectoryName(marker), "privacy-aes.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                if (recovering)
                {
                    if (File.Exists(marker) && NovaFramework.Runtime.PrivacyAesMaintenanceGuard.ReadOperation(journal.projectRoot) != journal.operationId)
                        throw new InvalidOperationException("门禁属于其它操作。");
                }
                else NovaFramework.Runtime.PrivacyAesMaintenanceGuard.AssertProjectReady(PrivacyProjectRoot);
                PrivacyBackend files = journal.backends[0], sqlite = journal.backends[2];
                if (files.enabled && files.present) scopes.Add(Acquire(new Journal { sourceRoot = files.sourceRoot, operationId = journal.operationId }));
                if (sqlite.enabled && sqlite.present) scopes.Add(SQLiteMaintenanceLease.Acquire(sqlite.sourceRoot));
                return new PrivacyLease(scopes);
            }
            catch { foreach (var scope in scopes.AsEnumerable().Reverse()) scope.Dispose(); throw; }
        }

        /// <summary>反序释放确实取得的维护门；稳定 lock 文件保留，避免删除与接手竞态。</summary>
        private sealed class PrivacyLease : IDisposable
        {
            private List<IDisposable> scopes;
            /// <summary>仅记录本次操作真实取得的资源。</summary>
            internal PrivacyLease(List<IDisposable> values) => scopes = values;
            /// <summary>每个 scope 只释放一次，绝不抢占其它操作。</summary>
            public void Dispose() { if (scopes == null) return; foreach (var value in scopes.AsEnumerable().Reverse()) value.Dispose(); scopes = null; }
        }

        /// <summary>复用现有随机 IV 受保护值文件；正文只在内存与独立密文恢复文件中。</summary>
        private static void ProtectPrivacyMaterial(PrivacyJournal journal, string name, string content, string key)
        {
            string path = Path.Combine(PrivacyOperationDirectory(journal.operationId), name);
            WriteProtectedValues(path, new[] { new ValueEntry { name = "payload", value = content, index = true } }, key);
            if (ReadProtectedValues(path, key).Single().value != content) throw new InvalidOperationException("受保护材料重读不一致。");
            journal.recoveryFiles.Add(name, FileFragmentMaintenance.Hash(path, CancellationToken.None));
        }

        /// <summary>固定schema4日志原子持久化；遗留临时日志须属于同一完整来源，未知临时材料保留。</summary>
        private static void SavePrivacyJournal(PrivacyJournal journal)
        {
            string path = Path.Combine(PrivacyOperationDirectory(journal.operationId), PrivacyJournalName), temporary = path + ".tmp";
            FileFragmentMaintenance.CheckPath(path); FileFragmentMaintenance.CheckPath(temporary);
            if (File.Exists(temporary))
            {
                try
                {
                    var old = ParsePrivacyObject(File.ReadAllText(temporary), 1024 * 1024).ToObject<PrivacyJournal>(JsonSerializer.Create(PrivacyJson));
                    if (!SamePrivacyOrigin(old, journal)) throw new InvalidOperationException("临时协调日志非本操作，保留现场。");
                    File.Delete(temporary);
                }
                catch
                {
                    // 崩溃留下的坏tmp不被删除；只在原正式日志身份完整且当前确认恢复token后隔离保留，恢复不依赖其内容。
                    if (!File.Exists(path)) throw;
                    var persisted = ParsePrivacyObject(File.ReadAllText(path), 1024 * 1024).ToObject<PrivacyJournal>(JsonSerializer.Create(PrivacyJson));
                    if (!SamePrivacyOrigin(persisted, journal)) throw;
                    string preserved = Path.Combine(PrivacyOperationDirectory(journal.operationId), "privacy-journal.interrupted-" + Guid.NewGuid().ToString("N") + ".bin");
                    File.Move(temporary, preserved);
                }
            }
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(journal));
            bool created = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { created = true; ProbePrivacyStage("journal-writing:" + journal.phase); stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch { if (created && File.Exists(temporary)) File.Delete(temporary); throw; }
        }

        /// <summary>数据提交前创建固定 owned marker；只有完整提交/恢复后才删除，未完成日志不会自动视为成功。</summary>
        private static void CreatePrivacyMarker(PrivacyJournal journal)
        {
            string path = NovaFramework.Runtime.PrivacyAesMaintenanceGuard.MarkerPath(journal.projectRoot);
            string temporary = Path.Combine(PrivacyOperationDirectory(journal.operationId), "privacy-marker.tmp");
            FileFragmentMaintenance.CheckPath(temporary);
            if (File.Exists(path) || File.Exists(temporary)) throw new InvalidOperationException("门禁或阶段文件已存在，保留现场。");
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { schemaVersion = 1, owner = NovaFramework.Runtime.PrivacyAesMaintenanceGuard.Owner,
                projectSha256 = NovaFramework.Runtime.PrivacyAesMaintenanceGuard.ProjectHash(journal.projectRoot), journal.operationId }));
            bool created = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { created = true; ProbePrivacyStage("marker-writing"); stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                File.Move(temporary, path);
            }
            catch
            {
                // 只删除本调用CreateNew确实创建、尚未晋升的临时文件；固定marker从不出现半写JSON。
                if (created && File.Exists(temporary)) File.Delete(temporary);
                throw;
            }
        }

        /// <summary>只删除本项目本操作的严格有效标记；未知/损坏标记不被清理。</summary>
        private static void DeletePrivacyMarker(PrivacyJournal journal)
        {
            string path = NovaFramework.Runtime.PrivacyAesMaintenanceGuard.MarkerPath(journal.projectRoot);
            if (!File.Exists(path)) return;
            if (NovaFramework.Runtime.PrivacyAesMaintenanceGuard.ReadOperation(journal.projectRoot) != journal.operationId) throw new InvalidOperationException("门禁归属不一致。");
            File.Delete(path);
        }

        /// <summary>提交前仅清理固定本操作材料；未知文件、错误摘要或凭据删除失败均保留并报告。</summary>
        private static bool CleanPrivacyUncommitted(PrivacyJournal journal, bool sealedSecrets, bool createdCredential, bool marked)
        {
            try
            {
                string directory = PrivacyOperationDirectory(journal.operationId);
                string[] allowed = { "before-master.bin", "after-master.bin", "before-runtime.bin", "after-runtime.bin", "before-prefs.bin", "after-prefs.bin",
                    "before-sqlite-values.bin", "after-sqlite-values.bin", "before-privacy.db", "before-privacy.db-wal", "before-privacy.db-shm", "before-privacy.db-journal", PrivacyJournalName, PrivacyJournalName + ".tmp", "backup" };
                if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Any(path => !allowed.Contains(Path.GetFileName(path)) ||
                    Directory.Exists(path) && Path.GetFileName(path) != "backup"))
                    throw new InvalidOperationException("有未知恢复材料。");
                PrivacyBackend files = journal.backends[0];
                foreach (string path in Directory.Exists(directory) ? Directory.GetFiles(directory) : Array.Empty<string>())
                {
                    FileFragmentMaintenance.CheckPath(path); string name = Path.GetFileName(path);
                    if (name == PrivacyJournalName)
                    {
                        var saved = ParsePrivacyObject(File.ReadAllText(path), 1024 * 1024).ToObject<PrivacyJournal>(JsonSerializer.Create(PrivacyJson));
                        if (!SamePrivacyOrigin(saved, journal)) throw new InvalidOperationException("正式协调日志归属不明。");
                    }
                    else if (journal.recoveryFiles == null || !journal.recoveryFiles.TryGetValue(name, out string expected) ||
                        FileFragmentMaintenance.Hash(path, CancellationToken.None) != expected)
                        throw new InvalidOperationException("未完成或变化的恢复材料保留，禁止删除。");
                }
                string backup = Path.Combine(directory, "backup");
                FileFragmentMaintenance.CheckPath(backup);
                if (Directory.Exists(backup)) foreach (string path in Directory.GetFileSystemEntries(backup))
                {
                    FileFragmentMaintenance.CheckPath(path);
                    var entry = files.entries?.SingleOrDefault(value => value.name == Path.GetFileName(path));
                    if (Directory.Exists(path) || entry == null || FileFragmentMaintenance.Hash(path, CancellationToken.None) != entry.beforeSha256)
                        throw new InvalidOperationException("分片恢复备份非本轮原字节。");
                }
                if (files.entries != null) FileFragmentMaintenance.CleanStage(files.sourceRoot, journal.operationId, files.entries);
                if (marked) DeletePrivacyMarker(journal);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                if (sealedSecrets && !PersistenceCredentialVault.Delete("recovery", journal.recoveryCredentialHandle)) return false;
                if (createdCredential && !PersistenceCredentialVault.Delete("credential", journal.targetCredentialHandle)) return false;
                return true;
            }
            catch { return false; }
        }

        /// <summary>固定三维字符串还原真实当前坐标；不会改变 Master 当前选择或 Unity BuildTarget。</summary>
        private static EditorUtil.Config.DimensionProjector.Coord ParsePrivacyCoordinate(string text)
        {
            string[] parts = text.Split('/');
            if (parts.Length != 3 || !GenerateActionCommon.TryParseCoordinate(parts[0], parts[1], parts[2], out var platform, out var channel, out var mode, out _))
                throw new InvalidOperationException("协调坐标无效。");
            return new EditorUtil.Config.DimensionProjector.Coord(platform, channel, mode);
        }

        /// <summary>只输出计数、整源/资产摘要、opaque handles与明确证据边界；不回显配置正文或玩家数据。</summary>
        internal static AgentActionResult PrivacyResult(PrivacyJournal journal, string status, string message)
        {
            var result = AgentActionResult.Create(null, status, message); result.ReceiptJson = JsonConvert.SerializeObject(journal);
            result.DataJson = JsonConvert.SerializeObject(new { journal.schemaVersion, journal.operationId, journal.phase, journal.operation, journal.dataScope,
                journal.sourceSha256, journal.coordinate, coordinateCount = journal.coordinates.Length,
                backends = journal.backends.Select(item => new { item.storage, item.enabled, item.present, item.count, item.phase, item.beforeSha256, item.afterSha256 }),
                journal.targetCredentialHandle, journal.recoveryCredentialHandle, durableState = "editor_cache_and_files_only", unknownDefaultAesConsumers = "caller_explicit_scope",
                devices = "not_migrated", serverProtocols = "not_migrated", retainedRecoveryDirectory = Directory.Exists(PrivacyOperationDirectory(journal.operationId)) ? "Library/Nova/AgentActions/PersistenceMaintenance/" + journal.operationId : null });
            result.EvidenceKinds = AgentActionEvidence.Static | (journal.phase == "planned" ? (AgentActionEvidence)0 : AgentActionEvidence.Artifact);
            return result;
        }
    }
}
