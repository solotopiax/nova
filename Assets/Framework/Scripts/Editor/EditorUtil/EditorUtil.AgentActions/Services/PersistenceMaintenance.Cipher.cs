/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistenceMaintenance.Cipher.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   SQLite Cipher同库安全轮换
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using SqlCipher4Unity3D;

namespace NovaFramework.Editor
{
    internal static partial class PersistenceMaintenance
    {
        private static string CipherDatabasePath =>
#if UNITY_INCLUDE_TESTS
            s_CipherTestPath ??
#endif
            System.IO.Path.GetFullPath(NovaFramework.Runtime.Path.Persist.SQLite.FileFullPath);
#if UNITY_INCLUDE_TESTS
        private static string s_CipherTestPath;
        private static Action s_CipherAfterDatabaseCommit;
        private static Action s_CipherBeforeFieldSave;
        private static Action<string> s_CipherJournalWrite;
        private static string s_CipherFixtureCheckpoint;
        /// <summary>仅隔离测试读取最近固定阶段编号；不包含凭据、玩家值或底层异常。</summary>
        internal static string CipherFixtureCheckpoint => s_CipherTestPath == null ? "C00" : s_CipherFixtureCheckpoint ?? "C00";
        /// <summary>仅隔离fixture可模拟数据库提交后资产保存前的中断；dispose清除故障点。</summary>
        internal static void SetCipherCommitFixtureFault(Action fault)
        {
            if (s_CipherTestPath == null) throw new InvalidOperationException("提交故障点仅允许测试 fixture。");
            s_CipherAfterDatabaseCommit = fault;
        }
        /// <summary>测试唯一 fixture 中模拟 Apply 后保存前错误，Generic 编辑不调用。</summary>
        internal static void SetCipherFieldSaveFixtureFault(Action fault)
        { if (s_CipherTestPath == null) throw new InvalidOperationException("仅 Cipher fixture 可用。"); s_CipherBeforeFieldSave = fault; }
        /// <summary>测试唯一 fixture 中触发实际字段保存故障点。</summary>
        internal static void ProbeCipherFieldSaveFixture() { if (s_CipherTestPath != null) s_CipherBeforeFieldSave?.Invoke(); }
        /// <summary>测试唯一 fixture 中模拟日志写入中断，生产没有此入口。</summary>
        internal static void SetCipherJournalFixtureFault(Action<string> fault)
        { if (s_CipherTestPath == null) throw new InvalidOperationException("仅 Cipher fixture 可用。"); s_CipherJournalWrite = fault; }
        /// <summary>长期测试的内部隔离 scope，仅接受唯一 Temp fixture；公开请求没有路径覆盖能力。</summary>
        internal static IDisposable UseCipherFixture(string path)
        {
            string full = System.IO.Path.GetFullPath(path);
            string directory = System.IO.Path.GetDirectoryName(full);
            if (s_CipherTestPath != null || !directory.StartsWith("/private/tmp/", StringComparison.Ordinal) ||
                !System.IO.Path.GetFileName(directory).StartsWith("NovaCipherActionFixture_", StringComparison.Ordinal) ||
                System.IO.Path.GetFileName(full) != "game.db" || full == System.IO.Path.GetFullPath(NovaFramework.Runtime.Path.Persist.SQLite.FileFullPath))
                throw new InvalidOperationException("Cipher 测试仅允许唯一隔离 fixture。");
            FileFragmentMaintenance.CheckPath(full); s_CipherTestPath = full; s_CipherFixtureCheckpoint = null; return new CipherFixtureScope();
        }
        private sealed class CipherFixtureScope : IDisposable
        {
            /// <summary>退出唯一测试 scope，恢复权威数据库路径并清除测试中断回调。</summary>
            public void Dispose() { s_CipherTestPath = null; s_CipherAfterDatabaseCommit = null; s_CipherBeforeFieldSave = null; s_CipherJournalWrite = null; s_CipherFixtureCheckpoint = null; }
        }
#endif
        /// <summary>仅测试编译保留调用，隔离fixture记录源码固定编号；正式执行不记录诊断材料。</summary>
        [System.Diagnostics.Conditional("UNITY_INCLUDE_TESTS")]
        private static void MarkCipherFixtureCheckpoint(string code)
        {
#if UNITY_INCLUDE_TESTS
            if (s_CipherTestPath != null) s_CipherFixtureCheckpoint = code;
#endif
        }
        private const string CipherOperation = "rotate-sqlite-cipher";
        private static readonly JsonSerializerSettings CipherJson = new JsonSerializerSettings {
            MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None, MaxDepth = 24 };
        /// <summary>schema3 仅冻结策略、整资产/整数据摘要与 Keychain opaque handle；不保存密码或逐密码摘要。</summary>
        [Serializable]
        internal sealed class CipherJournal
        {
            public int schemaVersion = 3;
            public string operation = CipherOperation;
            public string operationId;
            public string projectRoot;
            public string phase;
            public SerializedTargetEditing.Target target;
            public SerializedTargetEditing.Receipt beforeSettings;
            public SerializedTargetEditing.Receipt afterSettings;
            public SerializedTargetEditing.Receipt savedSettings;
            public string sourceRoot;
            public string metadataSha256;
            public string logicalSha256;
            public string databaseBackupSha256;
            public int keyCount;
            public bool generateNewCredential;
            public string targetCredentialHandle;
            public string recoveryCredentialHandle;
        }
        /// <summary>冻结计划不携带密码；密码只在执行/恢复瞬间从真实组件或平台 vault 读取。</summary>
        internal sealed class CipherPlan : IDisposable
        {
            internal CipherJournal Journal;
            internal string JournalHash;
            internal string CurrentMetadataHash;
            internal bool Recovery;
            internal byte[] CredentialBindingKey;
            internal byte[] CredentialBindingDigest;
            internal int Consumed;
            /// <summary>计划被丢弃、过期或消费后清除内存随机绑定；保留正式恢复材料。</summary>
            public void Dispose()
            {
                if (CredentialBindingKey != null) Array.Clear(CredentialBindingKey, 0, CredentialBindingKey.Length);
                if (CredentialBindingDigest != null) Array.Clear(CredentialBindingDigest, 0, CredentialBindingDigest.Length);
                CredentialBindingKey = null; CredentialBindingDigest = null;
            }
        }
        /// <summary>只接受固定SQLite scope、唯一组件、整源摘要和二选一凭据策略；拒绝AES开关及密码输入。</summary>
        internal static bool ValidateCipher(Request request, out string error)
        {
            error = null;
            if (request == null || request.operation != CipherOperation || request.storage != "SQLite" ||
                request.acknowledgeEditorOnly.HasValue || request.acknowledgeDefaultAesConsumerScope.HasValue ||
                request.dataScope != "current-editor-sqlite-managed-data" || request.enableAES.HasValue || request.acknowledgeGlobalSave.HasValue ||
                !FileFragmentMaintenance.ValidHash(request.sourceSha256) || !SerializedTargetEditing.ValidateTarget(ToTarget(request), out _) ||
                string.IsNullOrEmpty(request.globalObjectId) ||
                !(request.generateNewCredential == true && request.targetCredentialHandle == null ||
                  !request.generateNewCredential.HasValue && PersistenceCredentialVault.ValidHandle(request.targetCredentialHandle)))
            { error = "Cipher 轮换须固定 SQLite 托管范围、源摘要、唯一组件，并选择 generateNewCredential=true 或已有 targetCredentialHandle；不接收密码或 AES 开关。"; return false; }
            return true;
        }
        /// <summary>从真实唯一标准PersistComponent读取非空当前Cipher密码，不从日志或请求取得。</summary>
        private static string ReadCipherPassword(SerializedTargetEditing.Target target)
        {
            var component = ReadMetadataTarget(new Request { targetGuid = target.targetGuid, globalObjectId = target.globalObjectId }, out _);
            if (component.CurSQLiteManagerTypeName != "NovaFramework.Runtime.SQLiteManager") throw new InvalidOperationException("须为标准 SQLiteManager。");
            using var serialized = new UnityEditor.SerializedObject(component);
            string password = serialized.FindProperty("m_SQLiteCipherPassword").stringValue;
            if (string.IsNullOrEmpty(password) || Encoding.UTF8.GetByteCount(password) > 4096) throw new InvalidOperationException("当前 Cipher 必须非空；明文库转换不在轮换范围。");
            return password;
        }
        /// <summary>冻结原库逻辑、整资产和生成/既有handle策略；Plan不生成新密码或创建恢复材料。</summary>
        internal static CipherPlan BuildCipher(Request request, CancellationToken token)
        {
            if (!ValidateCipher(request, out string error)) throw new InvalidOperationException(error);
            EnsureIdle();
            if (!PersistenceCredentialVault.IsSupported) throw new PlatformNotSupportedException("安全凭据封存当前需要 macOS Editor 宿主。");
            var target = ToTarget(request);
            string password = ReadCipherPassword(target);
            string frozenTarget = request.targetCredentialHandle == null ? null : PersistenceCredentialVault.ReadCredential(request.targetCredentialHandle);
            if (frozenTarget == password) throw new InvalidOperationException("目标凭据与原凭据相同。");
            var settings = SerializedTargetEditing.BuildPersistenceCipherPlan(target, password, token);
            string path = CipherDatabasePath;
            SQLiteFileMetadata metadata = InspectSQLiteFiles(path, token);
            if (metadata.metadataSha256 != request.sourceSha256 || !metadata.files[0].present || metadata.files[0].byteCount == 0 || metadata.unexpectedEntryCount != 0)
                throw new InvalidOperationException("库不存在、空文件、未知文件或源摘要变化。");
            ValueEntry[] values;
            using (SQLiteMaintenanceLease.Acquire(path))
            using (var connection = OpenMaintenanceDatabase(path, password))
            {
                connection.Execute("BEGIN IMMEDIATE");
                try { values = ReadSQLiteValues(connection, token); }
                finally { connection.Execute("ROLLBACK"); }
            }
            var journal = new CipherJournal { operationId = Guid.NewGuid().ToString("N"), projectRoot = GenerateActionCommon.ProjectRoot,
                phase = "planned", target = target, beforeSettings = CloneCipherReceipt(settings.Receipt), sourceRoot = path,
                metadataSha256 = InspectSQLiteFiles(path, token).metadataSha256, logicalSha256 = ValueHash(values), keyCount = values.Length,
                generateNewCredential = request.generateNewCredential == true,
                targetCredentialHandle = request.targetCredentialHandle ?? PersistenceCredentialVault.NewHandle(),
                recoveryCredentialHandle = PersistenceCredentialVault.NewHandle() };
            AssertCipherSettings(journal, false, token);
            var plan = new CipherPlan { Journal = journal };
            if (frozenTarget != null)
            {
                plan.CredentialBindingKey = new byte[32];
                using (var random = System.Security.Cryptography.RandomNumberGenerator.Create()) random.GetBytes(plan.CredentialBindingKey);
                plan.CredentialBindingDigest = CipherCredentialBinding(frozenTarget, plan.CredentialBindingKey);
            }
            return plan;
        }
        /// <summary>现有 handle 冻结只保存内存随机 HMAC，秘密与摘要都不进入外部计划或日志。</summary>
        private static byte[] CipherCredentialBinding(string password, byte[] key)
        {
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(password);
            try { using var hmac = new System.Security.Cryptography.HMACSHA256(key); return hmac.ComputeHash(bytes); }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }
        /// <summary>深拷贝脱敏整资产收据，避免执行标记修改原计划身份。</summary>
        private static SerializedTargetEditing.Receipt CloneCipherReceipt(SerializedTargetEditing.Receipt receipt) =>
            JsonConvert.DeserializeObject<SerializedTargetEditing.Receipt>(JsonConvert.SerializeObject(receipt));
        /// <summary>核对唯一目标、script/meta和完整原或新状态，阻断无关资产变化。</summary>
        private static void AssertCipherSettings(CipherJournal journal, bool recovering, CancellationToken token)
        {
            var view = SerializedTargetEditing.Inspect(journal.target, token);
            var before = journal.beforeSettings;
            if (!view.editable || view.targetType != typeof(PersistComponent).FullName || view.assetPath != before.assetPath ||
                view.scriptPath != before.scriptPath || view.scriptGuid != before.scriptGuid ||
                AssetHash(view.assetPath + ".meta", token) != before.metaSha256 || AssetHash(view.scriptPath, token) != before.scriptSha256 ||
                (!recovering && (view.stateSha256 != before.beforeStateSha256 || view.fileSha256 != before.plannedFileSha256) ||
                 recovering && view.stateSha256 != before.beforeStateSha256 && view.stateSha256 != journal.afterSettings?.expectedStateSha256))
                throw new InvalidOperationException("Cipher 配置身份或完整原/新状态漂移。");
        }
        /// <summary>在已取得数据库写事务内调用 vendor 原生 rekey；参数是暂存字节，错误不包含密码或 SQL。</summary>
        internal static void RekeySQLite(SQLiteConnection connection, string password)
        {
            if (!PersistenceCredentialVault.IsSupported || connection == null || connection.Handle == IntPtr.Zero || string.IsNullOrEmpty(password))
                throw new InvalidOperationException("Cipher 原生轮换前置不满足。");
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(password);
            try { if (sqlite3_rekey(connection.Handle, bytes, bytes.Length) != 0) throw new InvalidOperationException("Cipher 原生轮换未通过。"); }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }
        /// <summary>使用现vendor原生句柄执行库级轮换；密码以字节参数传入，调用方持有写事务。</summary>
        [DllImport("sqlcipher", EntryPoint = "sqlite3_rekey", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_rekey(IntPtr database, byte[] key, int length);

        /// <summary>一次性封存凭据、生成独立加密备份、同库rekey与单字段保存；提交后异常保留partial。</summary>
        internal static AgentActionResult ExecuteCipher(CipherPlan plan, CancellationToken token)
        {
            if (plan?.Journal == null || plan.Recovery || Interlocked.Exchange(ref plan.Consumed, 1) != 0)
                return AgentActionResult.Create(null, "blocked", "Cipher 计划无效或已消费。");
            CipherJournal journal = plan.Journal;
            string directory = OperationDirectory(journal.operationId);
            bool started = false, created = false, credentialCreated = false, sealedRecovery = false;
            try
            {
                MarkCipherFixtureCheckpoint("C01"); EnsureIdle();
                MarkCipherFixtureCheckpoint("C02"); AssertCipherSettings(journal, false, token);
                if (!PersistenceCredentialVault.IsSupported) throw new PlatformNotSupportedException();
                MarkCipherFixtureCheckpoint("C03");
                using (SQLiteMaintenanceLease.Acquire(journal.sourceRoot))
                {
                    MarkCipherFixtureCheckpoint("C04");
                    if (InspectSQLiteFiles(journal.sourceRoot, token).metadataSha256 != journal.metadataSha256) throw new InvalidOperationException("Cipher 源漂移。");
                    MarkCipherFixtureCheckpoint("C05");
                    string before = ReadCipherPassword(journal.target);
                    string after = journal.generateNewCredential ? PersistenceCredentialVault.GeneratePassword() : PersistenceCredentialVault.ReadCredential(journal.targetCredentialHandle);
                    if (before == after || string.IsNullOrEmpty(after) || !journal.generateNewCredential && (plan.CredentialBindingKey == null ||
                        !CipherCredentialBinding(after, plan.CredentialBindingKey).SequenceEqual(plan.CredentialBindingDigest)))
                        throw new InvalidOperationException("新凭据须不同、非空且与预置 handle 计划一致。");
                    MarkCipherFixtureCheckpoint("C06");
                    if (journal.generateNewCredential) { PersistenceCredentialVault.StoreCredential(journal.targetCredentialHandle, after); credentialCreated = true;
                        if (PersistenceCredentialVault.ReadCredential(journal.targetCredentialHandle) != after) throw new InvalidOperationException("新凭据封存重读失败。"); }
                    var secrets = new PersistenceCredentialVault.RecoverySecrets { Before = before, After = after,
                        Backup = PersistenceCredentialVault.GeneratePassword().Substring(0, 16) };
                    MarkCipherFixtureCheckpoint("C07"); PersistenceCredentialVault.Seal(journal.recoveryCredentialHandle, secrets); sealedRecovery = true;
                    MarkCipherFixtureCheckpoint("C08");
                    var unsealed = PersistenceCredentialVault.Unseal(journal.recoveryCredentialHandle);
                    if (unsealed.Before != before || unsealed.After != after || unsealed.Backup != secrets.Backup)
                        throw new InvalidOperationException("恢复秘密封存重读失败。");
                    MarkCipherFixtureCheckpoint("C09");
                    var settings = SerializedTargetEditing.BuildPersistenceCipherPlan(journal.target, after, token);
                    journal.afterSettings = CloneCipherReceipt(settings.Receipt);
                    if (Directory.Exists(directory)) throw new InvalidOperationException("操作目录存在。");
                    Directory.CreateDirectory(directory); created = true;
                    PrepareCipherAssetOrigin(journal, secrets, token);
                    MarkCipherFixtureCheckpoint("C13");
                    string backupPath = System.IO.Path.Combine(directory, "before-cipher.db");
                    using (var source = OpenMaintenanceDatabase(journal.sourceRoot, before)) BackupSQLite(source, backupPath, secrets.Backup);
                    MarkCipherFixtureCheckpoint("C14");
                    using (var backup = OpenMaintenanceDatabase(backupPath, secrets.Backup))
                        if (ValueHash(ReadSQLiteValues(backup, token)) != journal.logicalSha256) throw new InvalidOperationException("独立加密备份重读不匹配。");
                    journal.databaseBackupSha256 = FileFragmentMaintenance.Hash(backupPath, token);
                    MarkCipherFixtureCheckpoint("C15"); journal.phase = "prepared"; SaveCipherJournal(journal);
                    MarkCipherFixtureCheckpoint("C16");
                    token.ThrowIfCancellationRequested(); AssertCipherSettings(journal, false, token);
                    using (var source = OpenMaintenanceDatabase(journal.sourceRoot, before))
                    {
                        source.Execute("BEGIN IMMEDIATE"); bool active = true;
                        try
                        {
                            AssertCipherBackup(journal, secrets, token);
                            if (ValueHash(ReadSQLiteValues(source, token)) != journal.logicalSha256) throw new InvalidOperationException("Cipher 事务源漂移。");
                            MarkCipherFixtureCheckpoint("C17"); journal.phase = "committing"; SaveCipherJournal(journal); started = true;
                            MarkCipherFixtureCheckpoint("C18");
                            RekeySQLite(source, after);
                            if (ValueHash(ReadSQLiteValues(source, CancellationToken.None)) != journal.logicalSha256) throw new InvalidOperationException("Cipher 轮换逻辑后验失败。");
                            source.Execute("COMMIT"); active = false;
                        }
                        finally { if (active) source.Execute("ROLLBACK"); }
                    }
#if UNITY_INCLUDE_TESTS
                    MarkCipherFixtureCheckpoint("C19");
                    s_CipherAfterDatabaseCommit?.Invoke();
#endif
                    MarkCipherFixtureCheckpoint("C20");
                    AgentActionResult saved = SerializedTargetEditing.Execute(settings, CancellationToken.None);
                    if (saved.Status != "success") throw new InvalidOperationException("Cipher 提交后组件保存未通过。");
                    journal.savedSettings = JsonConvert.DeserializeObject<SerializedTargetEditing.Receipt>(saved.ReceiptJson);
                    MarkCipherFixtureCheckpoint("C21"); journal.phase = "committed"; SaveCipherJournal(journal);
                }
                MarkCipherFixtureCheckpoint("C22");
                return VerifyCipher(JsonConvert.SerializeObject(journal), CancellationToken.None);
            }
            catch (OperationCanceledException) when (!started)
            {
                if (!CleanCipherBeforeWrite(journal, created, credentialCreated, sealedRecovery))
                    return CipherResult(journal, "partial", "未写玩家数据，但本轮恢复材料清理失败。");
                throw;
            }
            catch
            {
                bool cleaned = started || CleanCipherBeforeWrite(journal, created, credentialCreated, sealedRecovery);
                return CipherResult(journal, started || !cleaned ? "partial" : "blocked", started ?
                    "Cipher 已进入提交，保留独立加密备份和 Keychain 恢复秘密；须独立恢复，不重放。" :
                    cleaned ? "安全封存、备份或源条件未通过，未开始玩家数据或组件写入。" : "未写玩家数据，恢复材料清理未完成。");
            }
            finally { plan.Dispose(); }
        }
        /// <summary>提交前以独立随机Key保护原Prefab文本，并用原token冻结的整文件摘要重读；不输出字段内容。</summary>
        private static void PrepareCipherAssetOrigin(CipherJournal journal, PersistenceCredentialVault.RecoverySecrets secrets, CancellationToken token)
        {
            MarkCipherFixtureCheckpoint("C10");
            string text = File.ReadAllText(System.IO.Path.Combine(GenerateActionCommon.ProjectRoot, journal.beforeSettings.assetPath), new UTF8Encoding(false, true));
            if (GenerateActionCommon.ComputeTextHash(text) != journal.beforeSettings.plannedFileSha256 || text.Length > 4 * 1024 * 1024)
                throw new InvalidOperationException("Cipher 目标须为有界UTF8正式Prefab文本。");
            MarkCipherFixtureCheckpoint("C11"); NormalizeCipherPrefab(text, journal.target, secrets.Before);
            MarkCipherFixtureCheckpoint("C12");
            WriteProtectedValues(System.IO.Path.Combine(OperationDirectory(journal.operationId), "before-cipher-asset.bin"),
                new[] { new ValueEntry { name = "prefab", value = text, index = true } }, secrets.Backup);
            AssertCipherAssetOrigin(journal, secrets, token);
        }
        /// <summary>受保护原文本必须与原token整文件SHA完全一致，禁止日志自改savedSHA扩大磁盘恢复范围。</summary>
        private static string AssertCipherAssetOrigin(CipherJournal journal, PersistenceCredentialVault.RecoverySecrets secrets, CancellationToken token)
        {
            var values = ReadProtectedValues(System.IO.Path.Combine(OperationDirectory(journal.operationId), "before-cipher-asset.bin"), secrets.Backup);
            if (values.Length != 1 || values[0].name != "prefab" || !values[0].index ||
                GenerateActionCommon.ComputeTextHash(values[0].value) != journal.beforeSettings.plannedFileSha256)
                throw new InvalidOperationException("Cipher 原资产备份不属于原token。");
            NormalizeCipherPrefab(values[0].value, journal.target, secrets.Before); token.ThrowIfCancellationRequested(); return values[0].value;
        }
        /// <summary>完整磁盘文本必须除唯一密码标量外逐字节等于已保护的原Prefab；只接受封存旧或新密码。</summary>
        internal static bool CipherDiskMatchesBoundOrigin(CipherJournal journal, PersistenceCredentialVault.RecoverySecrets secrets, CancellationToken token)
        {
            string original = AssertCipherAssetOrigin(journal, secrets, token);
            string disk = File.ReadAllText(System.IO.Path.Combine(GenerateActionCommon.ProjectRoot, journal.beforeSettings.assetPath), new UTF8Encoding(false, true));
            if (disk.Length > 4 * 1024 * 1024) throw new InvalidOperationException("Cipher磁盘资产超限。");
            string expected = NormalizeCipherPrefab(original, journal.target, secrets.Before);
            try { if (NormalizeCipherPrefab(disk, journal.target, secrets.Before) == expected) return true; } catch { }
            try { if (NormalizeCipherPrefab(disk, journal.target, secrets.After) == expected) return true; } catch { }
            return false;
        }
        /// <summary>固定Unity文本Prefab，精确组件localId与唯一密码标量；拒多行/未知格式，仅密码值规整，余下完整文本不变。</summary>
        private static string NormalizeCipherPrefab(string text, SerializedTargetEditing.Target target, string password)
        {
            if (!text.StartsWith("%YAML 1.1", StringComparison.Ordinal) || !UnityEditor.GlobalObjectId.TryParse(target.globalObjectId, out var id))
                throw new InvalidOperationException("Cipher 资产文本格式或身份不合法。");
            string marker = "--- !u!114 &" + id.targetObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n";
            int start = text.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) throw new InvalidOperationException("Cipher 精确组件未在原资产文本中。");
            int end = text.IndexOf("\n--- !u!", start + marker.Length, StringComparison.Ordinal); if (end < 0) end = text.Length;
            string field = "  m_SQLiteCipherPassword: "; int position = text.IndexOf(field, start, end - start, StringComparison.Ordinal);
            if (position < 0 || text.IndexOf(field, position + field.Length, end - position - field.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Cipher 密码标量不唯一。");
            int lineEnd = text.IndexOf('\n', position); if (lineEnd < 0 || lineEnd > end) throw new InvalidOperationException("Cipher 密码须单行标量。");
            string scalar = text.Substring(position + field.Length, lineEnd - position - field.Length);
            string value;
            if (scalar.StartsWith("\"", StringComparison.Ordinal)) value = JsonConvert.DeserializeObject<string>(scalar);
            else if (scalar.StartsWith("'", StringComparison.Ordinal) && scalar.EndsWith("'", StringComparison.Ordinal)) value = scalar.Substring(1, scalar.Length - 2).Replace("''", "'");
            else value = scalar;
            if (value != password) throw new InvalidOperationException("Cipher 原/新磁盘密码与封存值不一致。");
            return text.Substring(0, position + field.Length) + "__NOVA_CIPHER_BOUND_SCALAR__" + text.Substring(lineEnd);
        }
        /// <summary>核对独立加密备份整文件摘要和完整原逻辑计数，并认证原资产文本与原token及独立封存凭据的绑定；不恢复第三方数据。</summary>
        private static void AssertCipherBackup(CipherJournal journal, PersistenceCredentialVault.RecoverySecrets secrets, CancellationToken token)
        {
            AssertCipherAssetOrigin(journal, secrets, token);
            string path = System.IO.Path.Combine(OperationDirectory(journal.operationId), "before-cipher.db");
            if (FileFragmentMaintenance.Hash(path, token) != journal.databaseBackupSha256) throw new InvalidOperationException("Cipher 备份被修改。");
            using var backup = OpenMaintenanceDatabase(path, secrets.Backup);
            ValueEntry[] entries = ReadSQLiteValues(backup, token);
            if (entries.Length != journal.keyCount || ValueHash(entries) != journal.logicalSha256) throw new InvalidOperationException("Cipher 备份逻辑范围不符。");
        }
        /// <summary>仅清本轮提交前创建的固定文件和自有Keychain项；未知项/失败保留并返回false。</summary>
        private static bool CleanCipherBeforeWrite(CipherJournal journal, bool created, bool credentialCreated, bool sealedRecovery)
        {
            try
            {
                if (created)
                {
                    string directory = OperationDirectory(journal.operationId);
                    if (Directory.GetFileSystemEntries(directory).Any(path => Directory.Exists(path) || !new[] { "before-cipher.db", "before-cipher.db-wal", "before-cipher.db-shm", "before-cipher.db-journal", "cipher-journal.json", "cipher-journal.json.tmp", "before-cipher-asset.bin" }.Contains(System.IO.Path.GetFileName(path)))) return false;
                    foreach (string path in Directory.GetFiles(directory)) { FileFragmentMaintenance.CheckPath(path); File.Delete(path); }
                    Directory.Delete(directory);
                }
                if (sealedRecovery && !PersistenceCredentialVault.Delete("recovery", journal.recoveryCredentialHandle)) return false;
                if (credentialCreated && !PersistenceCredentialVault.Delete("credential", journal.targetCredentialHandle)) return false;
                return true;
            }
            catch { return false; }
        }
        /// <summary>只探测规范当前项目操作目录的固定schema3日志，不读取秘密。</summary>
        internal static bool HasCipherJournal(string operationId) => File.Exists(System.IO.Path.Combine(OperationDirectory(operationId), "cipher-journal.json"));
        /// <summary>先刷新固定脱敏临时日志再原子替换；陌生临时日志保留并阻断。</summary>
        private static void SaveCipherJournal(CipherJournal journal, bool authorizedRecovery = false)
        {
            string path = System.IO.Path.Combine(OperationDirectory(journal.operationId), "cipher-journal.json");
            string temporary = path + ".tmp";
            FileFragmentMaintenance.CheckPath(path); FileFragmentMaintenance.CheckPath(temporary);
            if (File.Exists(temporary))
            {
                if (new FileInfo(temporary).Length > 1024 * 1024) throw new InvalidOperationException("Cipher 临时日志超限，保留现场。");
                CipherJournal candidate = null;
                try { candidate = JsonConvert.DeserializeObject<CipherJournal>(File.ReadAllText(temporary), CipherJson); } catch { }
                if (candidate != null)
                {
                    if (!SameCipherOrigin(candidate, journal)) throw new InvalidOperationException("遗留 Cipher 临时日志不属于本操作。");
                    File.Delete(temporary);
                }
                else
                {
                    // 仅独立恢复已由原token绑定正式日志后隔离半写材料；未知内容保留，不依赖坏tmp恢复。
                    if (!authorizedRecovery || !SameCipherOrigin(LoadCipherJournal(journal.operationId), journal))
                        throw new InvalidOperationException("半写 Cipher 日志需先绑定原操作独立恢复。");
                    string preserved = System.IO.Path.Combine(OperationDirectory(journal.operationId), "cipher-journal.interrupted-" + Guid.NewGuid().ToString("N") + ".bin");
                    FileFragmentMaintenance.CheckPath(preserved); File.Move(temporary, preserved);
                }
            }
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(journal)); bool created = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true; int first = bytes.Length / 2; stream.Write(bytes, 0, first);
#if UNITY_INCLUDE_TESTS
                    s_CipherJournalWrite?.Invoke(journal.phase);
#endif
                    stream.Write(bytes, first, bytes.Length - first); stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch { if (created && File.Exists(temporary)) File.Delete(temporary); throw; }
        }
        /// <summary>精确比较Cipher原计划身份、逻辑集合、策略、opaque凭据引用和原字段收据；阶段不充当来源授权。</summary>
        private static bool SameCipherOrigin(CipherJournal first, CipherJournal second) => first != null && second != null &&
            first.schemaVersion == 3 && second.schemaVersion == 3 && first.operation == CipherOperation && second.operation == CipherOperation &&
            first.operationId == second.operationId && first.projectRoot == second.projectRoot && first.sourceRoot == second.sourceRoot &&
            first.logicalSha256 == second.logicalSha256 && first.keyCount == second.keyCount && first.metadataSha256 == second.metadataSha256 &&
            first.generateNewCredential == second.generateNewCredential && first.targetCredentialHandle == second.targetCredentialHandle &&
            first.recoveryCredentialHandle == second.recoveryCredentialHandle && JsonConvert.SerializeObject(first.target) == JsonConvert.SerializeObject(second.target) &&
            JsonConvert.SerializeObject(first.beforeSettings) == JsonConvert.SerializeObject(second.beforeSettings);
        /// <summary>严格读取规范schema3日志，核单密码字段、来源、容量与合法阶段。</summary>
        private static CipherJournal LoadCipherJournal(string operationId)
        {
            string path = System.IO.Path.Combine(OperationDirectory(operationId), "cipher-journal.json"); FileFragmentMaintenance.CheckPath(path);
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) throw new InvalidOperationException("Cipher 日志不存在或超限。");
            CipherJournal journal = JsonConvert.DeserializeObject<CipherJournal>(File.ReadAllText(path), CipherJson);
            if (journal?.schemaVersion != 3 || journal.operation != CipherOperation || journal.operationId != operationId ||
                journal.projectRoot != GenerateActionCommon.ProjectRoot || journal.sourceRoot != CipherDatabasePath ||
                !SerializedTargetEditing.ValidateTarget(journal.target, out _) || journal.beforeSettings?.targetType != typeof(PersistComponent).FullName ||
                journal.beforeSettings.executionStarted || journal.beforeSettings.changes?.Length != 0 ||
                JsonConvert.SerializeObject(journal.beforeSettings.target) != JsonConvert.SerializeObject(journal.target) ||
                !FileFragmentMaintenance.ValidHash(journal.logicalSha256) || !FileFragmentMaintenance.ValidHash(journal.metadataSha256) ||
                !FileFragmentMaintenance.ValidHash(journal.databaseBackupSha256) || journal.keyCount < 0 || journal.keyCount > 16384 ||
                !PersistenceCredentialVault.ValidHandle(journal.targetCredentialHandle) || !PersistenceCredentialVault.ValidHandle(journal.recoveryCredentialHandle) ||
                journal.afterSettings?.targetType != typeof(PersistComponent).FullName || journal.afterSettings.executionStarted ||
                JsonConvert.SerializeObject(journal.afterSettings.target) != JsonConvert.SerializeObject(journal.target) ||
                journal.afterSettings.beforeStateSha256 != journal.beforeSettings.beforeStateSha256 ||
                journal.afterSettings.changes?.Length != 1 || journal.afterSettings.changes[0].propertyPath != "m_SQLiteCipherPassword" ||
                journal.afterSettings.changes[0].propertyType != "String" || !journal.afterSettings.changes[0].sensitive ||
                !new[] { "prepared", "committing", "committed", "recovering", "recovered" }.Contains(journal.phase))
                throw new InvalidOperationException("Cipher 日志身份、单字段或阶段不符。");
            return journal;
        }
        /// <summary>只输出整数据/资产摘要、阶段和opaque handle；不输出密码或玩家值。</summary>
        internal static AgentActionResult CipherResult(CipherJournal journal, string status, string message)
        {
            var result = AgentActionResult.Create(null, status, message);
            result.ReceiptJson = JsonConvert.SerializeObject(journal);
            result.DataJson = JsonConvert.SerializeObject(new { journal.schemaVersion, journal.operation, journal.operationId, journal.phase, journal.target,
                storage = "SQLite", dataScope = "current-editor-sqlite-managed-data", journal.generateNewCredential, journal.targetCredentialHandle,
                journal.recoveryCredentialHandle, journal.logicalSha256, journal.keyCount, backupProtection = "independent-project-keychain-secret",
                editorHost = "macOS", playerValues = "read_not_disclosed", databaseReplacement = false });
            result.EvidenceKinds = AgentActionEvidence.Static | (HasCipherJournal(journal.operationId) ? AgentActionEvidence.Artifact : AgentActionEvidence.None);
            return result;
        }
    }
}
