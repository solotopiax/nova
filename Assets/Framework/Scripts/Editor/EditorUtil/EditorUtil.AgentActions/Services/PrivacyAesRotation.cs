/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrivacyAesRotation.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   本机默认AES三存储轮换计划与完整来源校验
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using YooAsset;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    internal static partial class PersistenceMaintenance
    {
        /// <summary>本机默认 AES 轮换只接受标准存储的固定范围；不是设备、服务端或任意业务加密的迁移器。</summary>
        internal const string PrivacyScope = "current-editor-privacy-aes-data";
        private const string PrivacyJournalName = "privacy-journal.json";
        private static readonly JsonSerializerSettings PrivacyJson = new JsonSerializerSettings
        { MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None, MaxDepth = 48 };

#if UNITY_INCLUDE_TESTS
        private static ConfigMasterSO s_PrivacyFixtureMaster;
        private static string s_PrivacyFixtureRoot;
        private static Dictionary<string, string> s_PrivacyFixturePrefs;
        internal static Action<string> PrivacyFailureProbe;

        /// <summary>测试只使用唯一隔离Master与tmp数据；生产构建没有此入口，不改激活Globals或真实PlayerPrefs。</summary>
        internal static IDisposable UsePrivacyFixture(ConfigMasterSO master, string root, Dictionary<string, string> prefs)
        {
            string full = Path.GetFullPath(root), name = Path.GetFileName(full);
            const string prefix = "NovaPrivacyRotationFixture_";
            if (s_PrivacyFixtureRoot != null || master == null || !EditorUtility.IsPersistent(master) || Path.GetDirectoryName(full) != "/private/tmp" ||
                !name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name.Substring(prefix.Length), "N", out _) || prefs == null ||
                !File.Exists(Path.Combine(full, "privacy-fixture.owned")) || File.ReadAllText(Path.Combine(full, "privacy-fixture.owned")) != AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(master)))
                throw new InvalidOperationException("隔离轮换 fixture 无效。");
            FileFragmentMaintenance.CheckPath(full);
            s_PrivacyFixtureMaster = master; s_PrivacyFixtureRoot = full; s_PrivacyFixturePrefs = prefs;
            return new PrivacyFixtureScope();
        }

        /// <summary>精确移除本轮fixture入口，不删除调用方管理的文件或Vault。</summary>
        private sealed class PrivacyFixtureScope : IDisposable
        {
            /// <summary>只清静态测试入口；实际asset/tmp/keychain由fixture owner清场。</summary>
            public void Dispose() { s_PrivacyFixtureMaster = null; s_PrivacyFixtureRoot = null; s_PrivacyFixturePrefs = null; PrivacyFailureProbe = null; }
        }
#endif

        /// <summary>生产路径只当前项目，测试路径只隔离tmp；不从JSON接收可替换路径。</summary>
        private static string PrivacyProjectRoot
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (s_PrivacyFixtureRoot != null) return s_PrivacyFixtureRoot;
#endif
                return GenerateActionCommon.ProjectRoot;
            }
        }

        /// <summary>固定FileFragment路径，与运行时真实路径或明确隔离fixture一致。</summary>
        private static string PrivacyFileRoot => PrivacyDataPath("FileFragment", NovaFramework.Runtime.Path.Persist.FileFragment.FolderFullPath);
        /// <summary>固定SQLite路径，与运行时真实路径或明确隔离fixture一致。</summary>
        private static string PrivacyDatabasePath => PrivacyDataPath("SQLite/game.db", NovaFramework.Runtime.Path.Persist.SQLite.FileFullPath);

        /// <summary>只在编译进测试的fixture选择固定子路径，生产不允许外部自定义数据根。</summary>
        private static string PrivacyDataPath(string suffix, string production)
        {
#if UNITY_INCLUDE_TESTS
            if (s_PrivacyFixtureRoot != null) return Path.Combine(s_PrivacyFixtureRoot, suffix);
#endif
            return production;
        }

        /// <summary>规范operationId构造固定当前项目恢复目录，日志不得指定任意文件路径。</summary>
        private static string PrivacyOperationDirectory(string operationId)
        {
            if (!Guid.TryParseExact(operationId, "N", out _)) throw new InvalidOperationException("operationId 无效。");
            string path = Path.Combine(PrivacyProjectRoot, "Library/Nova/AgentActions/PersistenceMaintenance", operationId); FileFragmentMaintenance.CheckPath(path); return path;
        }

        /// <summary>只读确定真实激活Master；fixture显式绑定唯一asset且拒绝dirty，生产仍复用原持久激活来源。</summary>
        private static ConfigMasterSO ReadPrivacyMaster(out string path, out string guid, bool allowOwnedDirty = false)
        {
#if UNITY_INCLUDE_TESTS
            if (s_PrivacyFixtureMaster != null)
            {
                path = AssetDatabase.GetAssetPath(s_PrivacyFixtureMaster); guid = AssetDatabase.AssetPathToGUID(path);
                if (!allowOwnedDirty && EditorUtility.IsDirty(s_PrivacyFixtureMaster)) throw new InvalidOperationException("fixture Master未保存。");
                return s_PrivacyFixtureMaster;
            }
#endif
            if (!allowOwnedDirty) return ReadMaster(out path, out guid);
            if (!EditorUtil.Config.WorkspaceActive.TryGetPersistedConfigMaster(out var master, out guid, out path, out _) || master == null ||
                EditorUtil.Config.DesignTimeEditing.HasUnsavedDraft(master) || master.CurrentPlatform == PlatformType.None ||
                !master.TryGetEntry(master.CurrentPlatform, master.CurrentChannel, out _)) throw new InvalidOperationException("恢复配置来源或用户草稿无效。");
            return master;
        }

        /// <summary>默认PlayerPrefs存在性只读；fixture只访问独立内存字典。</summary>
        private static bool PrivacyPrefsHasKey(string key)
        {
#if UNITY_INCLUDE_TESTS
            if (s_PrivacyFixturePrefs != null) return s_PrivacyFixturePrefs.ContainsKey(key);
#endif
            return UnityEngine.PlayerPrefs.HasKey(key);
        }

        /// <summary>读取标准字符串；生产仍复用双默认值类型检查，fixture拒绝缺失键。</summary>
        private static string PrivacyPrefsRead(string key)
        {
#if UNITY_INCLUDE_TESTS
            if (s_PrivacyFixturePrefs != null) return s_PrivacyFixturePrefs[key];
#endif
            return ReadPlayerPrefsString(key);
        }

        /// <summary>固定完整已索引值读取，未知键不被扫描或维护。</summary>
        private static ValueEntry[] ReadPrivacyPrefs(CancellationToken token) => ReadPlayerPrefsValues(PrivacyPrefsHasKey, PrivacyPrefsRead, token);

        /// <summary>生产复用全局Save原语，fixture只更新独立字典并验证完整范围。</summary>
        private static void ApplyPrivacyPrefs(ValueEntry[] before, ValueEntry[] after, CancellationToken token)
        {
#if UNITY_INCLUDE_TESTS
            if (s_PrivacyFixturePrefs != null)
            {
                if (ValueHash(ReadPrivacyPrefs(token)) != ValueHash(before)) throw new InvalidOperationException("fixture prefs漂移。");
                foreach (ValueEntry entry in after) s_PrivacyFixturePrefs[entry.name] = entry.value;
                return;
            }
#endif
            ApplyPlayerPrefs(before, after, token);
        }

        /// <summary>故障probe只有隔离测试可设置，生产执行没有人为失败入口。</summary>
        private static void ProbePrivacyStage(string phase)
        {
#if UNITY_INCLUDE_TESTS
            PrivacyFailureProbe?.Invoke(phase);
#endif
        }

        /// <summary>仅在内存或 native Vault 内存在的 UTF-8 固定长度 Key/IV 对。</summary>
        [Serializable]
        internal sealed class PrivacyPair { public string key; public string iv; }

        /// <summary>非敏感的完整资产身份；整个序列化正文只能进入加密恢复文件。</summary>
        [Serializable]
        internal sealed class PrivacyAsset
        {
            public string guid, path, metaSha256, identitySha256, beforeFileSha256, beforeStateSha256, afterStateSha256, savedFileSha256;
        }

        /// <summary>固定后端的完整摘要及计数；没有玩家条目名、值或凭据。</summary>
        [Serializable]
        internal sealed class PrivacyBackend
        {
            public string storage, sourceRoot, beforeSha256, afterSha256, metadataSha256, phase;
            public bool enabled, present;
            public int count;
            public FileFragmentMaintenance.Entry[] entries;
        }

        /// <summary>schema4 唯一协调日志；多后端只组成轮换事务，不改变并列存储能力。</summary>
        [Serializable]
        internal sealed class PrivacyJournal
        {
            public int schemaVersion = 4;
            public string operation = "rotate-privacy-aes", dataScope = PrivacyScope;
            public string projectRoot, operationId, phase, coordinate, sourceSha256, targetCredentialHandle, recoveryCredentialHandle, placeholderTime;
            public bool generateNewCredential, acknowledgeGlobalSave, acknowledgeEditorOnly, acknowledgeDefaultAesConsumerScope;
            public SerializedTargetEditing.Target target;
            public string targetStateSha256, targetFileSha256, targetMetaSha256, targetScriptPath, targetScriptSha256;
            public PrivacyAsset master, runtime, yoo;
            public string[] coordinates;
            public PrivacyBackend[] backends;
            public Dictionary<string, string> recoveryFiles;
        }

        /// <summary>单次计划敏感信息仅在本进程；恢复必须重新读取持久日志与 Vault。</summary>
        internal sealed class PrivacyPlan : IDisposable
        {
            internal PrivacyJournal Journal;
            internal PrivacyPair Before, After;
            internal string DatabasePassword, BeforeMasterJson, AfterMasterJson, BeforeRuntimeJson, AfterRuntimeJson, JournalHash;
            internal FileFragmentMaintenance.Plan Files;
            internal ValueEntry[] BeforePlayerPrefs, AfterPlayerPrefs, BeforeSQLite, AfterSQLite;
            internal bool Recovery;
            internal int Consumed;
            internal byte[] CredentialBindingKey, CredentialBindingHash;
            /// <summary>执行后释放敏感引用与随机绑定字节；CLR 字符串不可原地擦除，不保存为长期计划。</summary>
            public void Dispose()
            {
                Before = null; After = null; DatabasePassword = null;
                BeforeMasterJson = null; AfterMasterJson = null; BeforeRuntimeJson = null; AfterRuntimeJson = null;
                BeforePlayerPrefs = null; AfterPlayerPrefs = null; BeforeSQLite = null; AfterSQLite = null; Files = null;
                if (CredentialBindingKey != null) Array.Clear(CredentialBindingKey, 0, CredentialBindingKey.Length);
                if (CredentialBindingHash != null) Array.Clear(CredentialBindingHash, 0, CredentialBindingHash.Length);
                CredentialBindingKey = null; CredentialBindingHash = null;
            }
        }

        /// <summary>只接受显式本机与默认消费者范围声明；检查不接受凭据生成、保存或源摘要。</summary>
        internal static bool ValidatePrivacyRotation(Request request, bool migration, out string error)
        {
            bool credential = request != null && (request.generateNewCredential == true && request.targetCredentialHandle == null ||
                !request.generateNewCredential.HasValue && PersistenceCredentialVault.ValidHandle(request.targetCredentialHandle));
            error = request == null || request.storage != "PrivacyAES" || request.dataScope != PrivacyScope ||
                !SerializedTargetEditing.ValidateTarget(ToTarget(request), out _) || string.IsNullOrEmpty(request.globalObjectId) ||
                request.acknowledgeEditorOnly != true || request.acknowledgeDefaultAesConsumerScope != true || request.enableAES.HasValue ||
                migration && (request.operation != "rotate-privacy-aes" || request.acknowledgeGlobalSave != true ||
                    !FileFragmentMaintenance.ValidHash(request.sourceSha256) || !credential) ||
                !migration && (request.operation != "inspect-privacy-aes" || request.acknowledgeGlobalSave.HasValue ||
                    request.sourceSha256 != null || request.generateNewCredential.HasValue || request.targetCredentialHandle != null)
                ? "须显式声明当前 Editor/标准默认 AES 消费者范围，固定 PrivacyAES scope；轮换须检查摘要、全局 Save 确认和单一 opaque 凭据策略。" : null;
            return error == null;
        }

        /// <summary>沿原厂 AES 字节契约严格解码、转换并重读；CBC 本身无鉴别码，不能声称数学证明历史密钥正确。</summary>
        internal static ValueEntry[] RotatePrivacyValues(ValueEntry[] source, PrivacyPair before, PrivacyPair after)
        {
            ValidatePrivacyPair(before); ValidatePrivacyPair(after);
            return source.Select(entry =>
            {
                if (entry == null || entry.name == null || entry.value == null) throw new InvalidOperationException("标准值无效。");
                string value = entry.value;
                if (!entry.index && value.Length != 0)
                {
                    byte[] cipher = Convert.FromBase64String(value);
                    if (cipher.Length == 0 || cipher.Length % 16 != 0) throw new InvalidOperationException("密文块格式无效。");
                    string plain = new UTF8Encoding(false, true).GetString(Util.Encrypt.AES.DecryptBytes(cipher, before.key, before.iv));
                    value = Util.Encrypt.AES.EncryptString(plain, after.key, after.iv);
                    string readback = value.Length == 0 ? string.Empty : new UTF8Encoding(false, true).GetString(
                        Util.Encrypt.AES.DecryptBytes(Convert.FromBase64String(value), after.key, after.iv));
                    if (plain != readback) throw new InvalidOperationException("轮换值重读不一致。");
                }
                return new ValueEntry { classify = entry.classify, name = entry.name, index = entry.index, value = value };
            }).ToArray();
        }

        /// <summary>拒绝空、半对或非 16-byte UTF-8 凭据，绝不回退到已加载默认值。</summary>
        private static void ValidatePrivacyPair(PrivacyPair pair)
        {
            if (pair == null || new UTF8Encoding(false, true).GetByteCount(pair.key ?? string.Empty) != 16 ||
                new UTF8Encoding(false, true).GetByteCount(pair.iv ?? string.Empty) != 16)
                throw new InvalidOperationException("隐私 AES pair 格式无效。");
        }

        /// <summary>从 native Vault 字符串严格读取唯一 pair，禁止未知成员、重复字段和其它凭据格式。</summary>
        internal static PrivacyPair ParsePrivacyPair(string json)
        {
            JObject value = ParsePrivacyObject(json, 2048);
            if (value.Count != 2 || value["key"]?.Type != JTokenType.String || value["iv"]?.Type != JTokenType.String)
                throw new InvalidOperationException("隐私凭据 pair 结构无效。");
            var result = value.ToObject<PrivacyPair>(); ValidatePrivacyPair(result); return result;
        }

        /// <summary>适配旧 UTF-8 字符串契约，使用无偏密码学随机可打印 ASCII；不复制第三方随机实现。</summary>
        private static string GeneratePrivacySecret()
        {
            byte[] raw = new byte[64]; var chars = new char[16]; int count = 0;
            using var random = RandomNumberGenerator.Create();
            try
            {
                while (count < chars.Length)
                {
                    random.GetBytes(raw);
                    foreach (byte value in raw) if (value < 188)
                    { chars[count++] = (char)(33 + value % 94); if (count == chars.Length) break; }
                }
                return new string(chars);
            }
            finally { Array.Clear(raw, 0, raw.Length); Array.Clear(chars, 0, chars.Length); }
        }

        /// <summary>仅读当前绑定的标准三后端、Master 和已导出 Runtime；不初始化存储或进行正式导出。</summary>
        internal static PrivacyPlan BuildPrivacyRotation(Request request, bool migration, CancellationToken token)
        {
            if (!ValidatePrivacyRotation(request, migration, out string error)) throw new InvalidOperationException(error);
            EnsureIdle(); NovaFramework.Runtime.PrivacyAesMaintenanceGuard.AssertProjectReady(PrivacyProjectRoot);
            if (!PersistenceCredentialVault.IsSupported) throw new PlatformNotSupportedException("当前只支持 macOS Editor native Vault。");
#if NOVA_DYSDK || NOVA_WXSDK || NOVA_ALIPAYSDK
            throw new InvalidOperationException("非默认 PlayerPrefs provider 不在本机轮换范围。");
#else
            PersistComponent component = ReadMetadataTarget(request, out _);
            if (component.CurPlayerPrefsManagerTypeName != "NovaFramework.Runtime.PlayerPrefsManager" ||
                component.CurFileFragmentManagerTypeName != "NovaFramework.Runtime.FileFragmentManager" || component.CurSQLiteManagerTypeName != "NovaFramework.Runtime.SQLiteManager")
                throw new InvalidOperationException("只支持标准三存储实现。");
            var target = SerializedTargetEditing.Inspect(ToTarget(request), token);
            if (!target.editable) throw new InvalidOperationException("目标不可维护。");
            ConfigMasterSO master = ReadPrivacyMaster(out string masterPath, out string masterGuid);
            var coordinate = new EditorUtil.Config.DimensionProjector.Coord(master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
            AssertPrivacyConfigComplete(master, coordinate);
            PrivacyConfigs privacy = master.TryGetEntry(coordinate.Platform, coordinate.Channel, out var row) ?
                row.PrivacyConfigsByMode.Single(item => item.Mode == coordinate.Mode).Config : null;
            var plan = new PrivacyPlan { Before = new PrivacyPair { key = privacy?.AESKey, iv = privacy?.AESIV } };
            ValidatePrivacyPair(plan.Before);
            string[] coordinates = PrivacyCoordinates(master, coordinate, plan.Before);
            ConfigRuntimeSO runtime = master.ExportTarget;
            if (runtime == null || !EditorUtility.IsPersistent(runtime) || EditorUtility.IsDirty(runtime)) throw new InvalidOperationException("缺少已保存的配对 Runtime。");
            AssertPrivacyRuntimeSource(master, runtime, coordinate);
            var journal = new PrivacyJournal { projectRoot = PrivacyProjectRoot, operationId = Guid.NewGuid().ToString("N"),
                phase = "planned", coordinate = Coordinate(master), target = ToTarget(request), coordinates = coordinates,
                acknowledgeEditorOnly = true, acknowledgeDefaultAesConsumerScope = true, acknowledgeGlobalSave = migration,
                master = CapturePrivacyAsset(master, token), runtime = CapturePrivacyAsset(runtime, token),
                targetStateSha256 = target.stateSha256, targetFileSha256 = target.fileSha256,
                targetMetaSha256 = AssetHash(target.assetPath + ".meta", token), targetScriptPath = target.scriptPath,
                targetScriptSha256 = AssetHash(target.scriptPath, token), placeholderTime = DateTime.Now.ToString("O"),
                generateNewCredential = migration && request.generateNewCredential == true,
                targetCredentialHandle = migration ? request.targetCredentialHandle ?? PersistenceCredentialVault.NewHandle() : null,
                recoveryCredentialHandle = migration ? PersistenceCredentialVault.NewHandle() : null };
            journal.yoo = CapturePrivacyYoo(master, coordinate, DateTime.Parse(journal.placeholderTime, null, System.Globalization.DateTimeStyles.RoundtripKind), token);
            plan.Journal = journal; plan.BeforeMasterJson = EditorJsonUtility.ToJson(master); plan.BeforeRuntimeJson = EditorJsonUtility.ToJson(runtime);
            using (var serialized = new SerializedObject(component)) plan.DatabasePassword = serialized.FindProperty("m_SQLiteCipherPassword").stringValue;
            bool[] enabled = new[] { "m_UseAESForFileFragment", "m_UseAESForPlayerPrefs", "m_UseAESForSQLite" }
                .Select(field => target.fields.Single(item => item.path == field).valueJson == "true").ToArray();
            journal.backends = ReadPrivacyBackends(plan, enabled, token);
            journal.sourceSha256 = GenerateActionCommon.ComputeTextHash(JsonConvert.SerializeObject(new { journal.target, journal.targetStateSha256,
                journal.targetFileSha256, journal.targetMetaSha256, journal.targetScriptSha256, journal.master, journal.runtime, journal.yoo,
                journal.coordinate, journal.coordinates, journal.backends }));
            if (migration)
            {
                if (request.sourceSha256 != journal.sourceSha256) throw new InvalidOperationException("完整本机源与检查摘要不一致。");
                if (!journal.generateNewCredential)
                {
                    string credential = PersistenceCredentialVault.ReadCredential(journal.targetCredentialHandle);
                    PrivacyPair desired = ParsePrivacyPair(credential);
                    if (plan.Before.key == desired.key && plan.Before.iv == desired.iv) throw new InvalidOperationException("目标 pair 与原值相同。");
                    plan.CredentialBindingKey = new byte[32];
                    using var random = RandomNumberGenerator.Create(); random.GetBytes(plan.CredentialBindingKey);
                    plan.CredentialBindingHash = BindPrivacyCredential(credential, plan.CredentialBindingKey);
                }
            }
            // 严格预验旧加密数据后丢弃秘密；生成新 pair 和完整目标预演只在 Execute 的写入前阶段执行。
            RotatePrivacyValues(plan.BeforePlayerPrefs, plan.Before, plan.Before);
            RotatePrivacyValues(plan.BeforeSQLite, plan.Before, plan.Before);
            AssertPrivacyAssets(journal, false, token); AssertPrivacyBackendSource(plan, token);
            plan.Before = null; plan.DatabasePassword = null; plan.BeforeMasterJson = null; plan.BeforeRuntimeJson = null;
            plan.BeforePlayerPrefs = null; plan.BeforeSQLite = null; plan.Files = null;
            return plan;
#endif
        }

        /// <summary>preset handle 内容冻结只使用内存随机 HMAC；不输出可供离线猜测的单凭据摘要。</summary>
        private static byte[] BindPrivacyCredential(string credential, byte[] key)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(credential);
            try { using var hmac = new HMACSHA256(key); return hmac.ComputeHash(bytes); }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }

        /// <summary>执行期重新读取冻结源并生成/读取 pair，完整目标预演全部先于恢复材料和数据写入。</summary>
        private static void StagePrivacyPlan(PrivacyPlan plan, CancellationToken token)
        {
            PrivacyJournal journal = plan.Journal;
            ConfigMasterSO master = ReadPrivacyMaster(out _, out _); ConfigRuntimeSO runtime = master.ExportTarget;
            var coordinate = ParsePrivacyCoordinate(journal.coordinate);
            PrivacyConfigs privacy = master.GetPrivacyConfigs(coordinate.Platform, coordinate.Channel, coordinate.Mode);
            plan.Before = new PrivacyPair { key = privacy.AESKey, iv = privacy.AESIV }; ValidatePrivacyPair(plan.Before);
            if (!PrivacyCoordinates(master, coordinate, plan.Before).SequenceEqual(journal.coordinates)) throw new InvalidOperationException("冻结逻辑组漂移。");
            AssertPrivacyRuntimeSource(master, runtime, coordinate);
            PersistComponent component = ReadMetadataTarget(new Request { targetGuid = journal.target.targetGuid, globalObjectId = journal.target.globalObjectId }, out _);
            using (var serialized = new SerializedObject(component)) plan.DatabasePassword = serialized.FindProperty("m_SQLiteCipherPassword").stringValue;
            PrivacyBackend[] previous = journal.backends;
            PrivacyBackend[] actual = ReadPrivacyBackends(plan, previous.Select(item => item.enabled).ToArray(), token);
            if (actual.Where((value, index) => !SamePrivacyBackendSource(value, previous[index])).Any()) throw new InvalidOperationException("完整后端源漂移。");
            // 使用原日志身份及非敏感 source；新文件表必须引用此次内存冻结，才能持久保存 after hash。
            journal.backends = actual;
            string preset = journal.generateNewCredential ? null : PersistenceCredentialVault.ReadCredential(journal.targetCredentialHandle);
            if (preset != null && (plan.CredentialBindingKey == null ||
                !BindPrivacyCredential(preset, plan.CredentialBindingKey).SequenceEqual(plan.CredentialBindingHash))) throw new InvalidOperationException("preset handle 内容变化。");
            plan.After = preset == null ? new PrivacyPair { key = GeneratePrivacySecret(), iv = GeneratePrivacySecret() } : ParsePrivacyPair(preset);
            if (plan.Before.key == plan.After.key && plan.Before.iv == plan.After.iv) throw new InvalidOperationException("目标 pair 相同。");
            plan.BeforeMasterJson = EditorJsonUtility.ToJson(master); plan.BeforeRuntimeJson = EditorJsonUtility.ToJson(runtime);
            ConfigMasterSO clone = UnityEngine.Object.Instantiate(master);
            try
            {
                clone.hideFlags = HideFlags.DontSave; ApplyPrivacyPair(clone, journal.coordinates, plan.Before, plan.After);
                using var so = new SerializedObject(clone);
                string expected = EditorUtil.Config.PanelEditing.Snapshot(clone);
                EditorUtil.Config.DesignTimeEditing.NormalizeWorkingCopy(clone, so, coordinate);
                if (EditorUtil.Config.PanelEditing.Snapshot(clone) != expected) throw new InvalidOperationException("归一触及其它配置。");
                plan.AfterMasterJson = EditorJsonUtility.ToJson(clone); journal.master.afterStateSha256 = PrivacyStateHash(clone);
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
            ConfigRuntimeSO runtimeClone = UnityEngine.Object.Instantiate(runtime);
            try
            {
                runtimeClone.PrivacyConfigs.AESKey = plan.After.key; runtimeClone.PrivacyConfigs.AESIV = plan.After.iv;
                plan.AfterRuntimeJson = EditorJsonUtility.ToJson(runtimeClone); journal.runtime.afterStateSha256 = PrivacyStateHash(runtimeClone);
            }
            finally { UnityEngine.Object.DestroyImmediate(runtimeClone); }
            plan.AfterPlayerPrefs = RotatePrivacyValues(plan.BeforePlayerPrefs, plan.Before, plan.After);
            plan.AfterSQLite = RotatePrivacyValues(plan.BeforeSQLite, plan.Before, plan.After);
            journal.backends[1].afterSha256 = ValueHash(plan.AfterPlayerPrefs); journal.backends[2].afterSha256 = ValueHash(plan.AfterSQLite);
        }

        /// <summary>只比较不可变的后端源，排除提交后的阶段与目标摘要。</summary>
        private static bool SamePrivacyBackendSource(PrivacyBackend first, PrivacyBackend second) => first != null && second != null &&
            first.storage == second.storage && first.sourceRoot == second.sourceRoot && first.enabled == second.enabled && first.present == second.present &&
            first.beforeSha256 == second.beforeSha256 && first.count == second.count;

        /// <summary>完整合法格先验证，避免 Getter 的自动补项污染只读计划。</summary>
        private static void AssertPrivacyConfigComplete(ConfigMasterSO master, EditorUtil.Config.DimensionProjector.Coord coordinate)
        {
            if (!master.TryGetEntry(coordinate.Platform, coordinate.Channel, out var row) ||
                new[] { row.AppConfigsByMode.Count(item => item?.Mode == coordinate.Mode && item.Config != null),
                    row.PrivacyConfigsByMode.Count(item => item?.Mode == coordinate.Mode && item.Config != null),
                    row.SDKConfigsByMode.Count(item => item?.Mode == coordinate.Mode), row.KitConfigsByMode.Count(item => item?.Mode == coordinate.Mode) }.Any(count => count != 1) ||
                EditorUtil.Config.Validator.ValidateDimensionInvariants(master).Count != 0 ||
                EditorUtil.Config.Validator.Validate(master, coordinate.Platform, coordinate.Channel, coordinate.Mode).Any(issue => issue.Level == EditorUtil.Config.Validator.Severity.Error))
                throw new InvalidOperationException("配置格、维度或导出前置不完整。");
        }

        /// <summary>整个当前逻辑组必须同 pair，其它独立组不得复用旧 pair；返回不含秘密的精确物理坐标。</summary>
        private static string[] PrivacyCoordinates(ConfigMasterSO master, EditorUtil.Config.DimensionProjector.Coord current, PrivacyPair before)
        {
            var result = new List<string>(); PanelDimensionMask mask = master.PrivacyConfigsMask;
            if (mask == null) throw new InvalidOperationException("Privacy mask 无效。");
            foreach (PlatformChannelEntry row in master.EditorEntries)
            foreach (DevelopModePrivacyConfigsEntry mode in row.PrivacyConfigsByMode)
            {
                bool member = (!mask.ByPlatform || row.Platform == current.Platform) && (!mask.ByChannel || row.Channel == current.Channel) && (!mask.ByDevelopMode || mode.Mode == current.Mode);
                bool same = mode?.Config?.AESKey == before.key && mode.Config.AESIV == before.iv;
                if (member != same) throw new InvalidOperationException("当前组凭据不一致或其它独立组复用相同旧 pair，不能遗漏共享本机存档消费者。");
                if (member) result.Add(row.Platform + "/" + row.Channel + "/" + mode.Mode);
            }
            if (result.Count == 0) throw new InvalidOperationException("没有完整逻辑组。");
            return result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        /// <summary>仅在内存工作副本按冻结坐标替换 pair，其它字段与维度不变。</summary>
        private static void ApplyPrivacyPair(ConfigMasterSO master, string[] coordinates, PrivacyPair expected, PrivacyPair desired)
        {
            foreach (string text in coordinates)
            {
                string[] parts = text.Split('/');
                if (parts.Length != 3 || !GenerateActionCommon.TryParseCoordinate(parts[0], parts[1], parts[2], out var platform, out var channel, out var mode, out _) ||
                    !master.TryGetEntry(platform, channel, out var row)) throw new InvalidOperationException("冻结坐标缺失。");
                PrivacyConfigs config = row.PrivacyConfigsByMode.Single(item => item.Mode == mode).Config;
                if (config.AESKey != expected.key || config.AESIV != expected.iv) throw new InvalidOperationException("冻结 pair 已变化。");
                config.AESKey = desired.key; config.AESIV = desired.iv;
            }
        }

        /// <summary>只读比较全体实际导出字段与源解析结果，避免轮换顺带刷新过期 Runtime 其它业务配置。</summary>
        private static void AssertPrivacyRuntimeSource(ConfigMasterSO master, ConfigRuntimeSO runtime, EditorUtil.Config.DimensionProjector.Coord coordinate)
        {
            if (runtime.Platform != coordinate.Platform || runtime.Channel != coordinate.Channel || runtime.DevelopMode != coordinate.Mode ||
                !master.TryGetEntry(coordinate.Platform, coordinate.Channel, out var row)) throw new InvalidOperationException("Runtime 坐标未配对。");
            var hybrid = EditorUtil.Config.DimensionalResolver.ResolveHybridCLR(master, coordinate.Platform, coordinate.Channel, coordinate.Mode);
            EditorUtil.HybridCLR.ValidateGameDllLists(hybrid.StartupGameDlls, hybrid.RunningGameDlls);
            object source = new { Namespace = EditorUtil.Config.DimensionalResolver.ResolveNamespace(master, coordinate.Platform, coordinate.Channel, coordinate.Mode),
                AppConfigs = row.AppConfigsByMode.Single(item => item.Mode == coordinate.Mode).Config,
                PrivacyConfigs = row.PrivacyConfigsByMode.Single(item => item.Mode == coordinate.Mode).Config,
                SDK = row.GetSDKConfigs(coordinate.Mode).Where(value => value != null && master.EnabledSDKs.Contains(value.GetType().FullName)).Select(TypedPrivacyValue).ToArray(),
                Kit = row.GetKitConfigs(coordinate.Mode).Where(value => value != null && master.EnabledKits.Contains(value.GetType().FullName)).Select(TypedPrivacyValue).ToArray(),
                hybrid.GameEntranceProcedureName, Aot = hybrid.AotMetadataDlls.Select(value => value.AssetLocation).ToArray(),
                Startup = hybrid.StartupGameDlls.Select(value => value.AssetLocation).ToArray(), Custom = master.Custom };
            object actual = new { runtime.Namespace, runtime.AppConfigs, runtime.PrivacyConfigs,
                SDK = runtime.EnabledSDKConfigs.Select(TypedPrivacyValue).ToArray(), Kit = runtime.EnabledKitConfigs.Select(TypedPrivacyValue).ToArray(),
                runtime.HybridConfigs.GameEntranceProcedureName, Aot = runtime.HybridConfigs.AotMetadataDlls.Select(value => value.AssetLocation).ToArray(),
                Startup = runtime.HybridConfigs.StartupGameDlls.Select(value => value.AssetLocation).ToArray(), Custom = runtime.Custom };
            if (!JToken.DeepEquals(JToken.FromObject(source), JToken.FromObject(actual))) throw new InvalidOperationException("Runtime 非当前完整源导出快照，先单独重新导出。");
        }

        /// <summary>验证列表内容时带上真实配置类型身份，避免相同 JSON 字段误认为相同 Plugin/Kit。</summary>
        private static object TypedPrivacyValue(object value) => value == null ? null : new { type = value.GetType().AssemblyQualifiedName, data = JToken.FromObject(value) };

        /// <summary>固定已有资产 GUID、meta、全文件与完整语义状态；不创建正式目标。</summary>
        private static PrivacyAsset CapturePrivacyAsset(UnityEngine.Object asset, CancellationToken token)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || EditorUtility.IsDirty(asset) ||
                !GenerateActionCommon.TryResolveProjectPath(path, "asset", out _, out _)) throw new InvalidOperationException("配置资产身份无效。");
            return new PrivacyAsset { path = path, guid = AssetDatabase.AssetPathToGUID(path), metaSha256 = AssetHash(path + ".meta", token),
                identitySha256 = PrivacyIdentityHash(asset), beforeFileSha256 = AssetHash(path, token), beforeStateSha256 = PrivacyStateHash(asset) };
        }

        /// <summary>完整语义摘要单独忽略的名称与隐藏标志仍冻结为身份，防止恢复覆盖无关磁盘身份变更。</summary>
        private static string PrivacyIdentityHash(UnityEngine.Object asset)
        {
            JObject raw = JObject.Parse(EditorJsonUtility.ToJson(asset));
            JObject value = raw.Count == 1 && raw["MonoBehaviour"] is JObject wrapped ? wrapped : raw;
            return GenerateActionCommon.ComputeTextHash(new JObject { ["m_Name"] = value["m_Name"]?.DeepClone(),
                ["m_ObjectHideFlags"] = value["m_ObjectHideFlags"]?.DeepClone() }.ToString(Formatting.None));
        }

        /// <summary>YooAsset 正规导出只允许两个字段同值；绑定完整资产，任何扩写都要另行配置流程。</summary>
        private static PrivacyAsset CapturePrivacyYoo(ConfigMasterSO master, EditorUtil.Config.DimensionProjector.Coord coordinate, DateTime time, CancellationToken token)
        {
            var config = EditorUtil.Config.DimensionalResolver.ResolveYooAsset(master, coordinate.Platform, coordinate.Channel, coordinate.Mode);
            if (string.IsNullOrWhiteSpace(config.YooAssetSettingsPath)) return null;
            YooAssetSettings settings = AssetDatabase.LoadAssetAtPath<YooAssetSettings>(config.YooAssetSettingsPath);
            if (settings == null) throw new InvalidOperationException("YooAsset 导出目标无效。");
            var context = EditorUtil.Placeholder.FromConfigMaster(master, coordinate.Platform, coordinate.Channel,
                EditorUtil.Placeholder.ResolveDefaultPackageName(), Application.version, time);
            if (settings.YooFolderName != (Util.Placeholder.Resolve(config.YooFolderName, context) ?? string.Empty) ||
                settings.PackageFilePrefix != (Util.Placeholder.Resolve(config.PackageFilePrefix, context) ?? string.Empty))
                throw new InvalidOperationException("正规导出将改变 YooAsset 设置，先单独导出修正。");
            return CapturePrivacyAsset(settings, token);
        }

        /// <summary>完整语义状态摘要去掉名称、隐藏标志及管理引用随机 RID；没有逐密钥摘要或玩家值输出。</summary>
        private static string PrivacyStateHash(UnityEngine.Object asset)
        {
            if (asset is ConfigMasterSO master) return EditorUtil.Config.PanelEditing.StateHash(master);
            JObject raw = JObject.Parse(EditorJsonUtility.ToJson(asset));
            JObject value = raw.Count == 1 && raw["MonoBehaviour"] is JObject wrapped ? wrapped : raw;
            value.Remove("m_Name"); value.Remove("m_ObjectHideFlags");
            var references = new Dictionary<long, JObject>();
            if (value["references"]?["RefIds"] is JArray entries) foreach (JObject item in entries) references.Add(item.Value<long>("rid"), item);
            value.Remove("references");
            return GenerateActionCommon.ComputeTextHash(ExpandPrivacyReferences(value, references, new HashSet<long>()).ToString(Formatting.None));
        }

        /// <summary>Runtime 的真实序列化引用只用于比较；循环、缺失或未知引用均拒绝。</summary>
        private static JToken ExpandPrivacyReferences(JToken value, Dictionary<long, JObject> references, HashSet<long> stack)
        {
            if (value is JObject obj)
            {
                if (obj.Count == 1 && obj["rid"]?.Type == JTokenType.Integer)
                {
                    long id = obj.Value<long>("rid"); if (id == -2) return JValue.CreateNull();
                    if (!references.TryGetValue(id, out JObject reference) || !stack.Add(id)) throw new InvalidOperationException("Runtime 管理引用无效。");
                    JObject result = (JObject)ExpandPrivacyReferences(reference["data"], references, stack);
                    result["_managedType"] = reference["type"]?.DeepClone(); stack.Remove(id); return result;
                }
                var resultObject = new JObject();
                foreach (var property in obj.Properties()) resultObject.Add(property.Name, ExpandPrivacyReferences(property.Value, references, stack));
                return resultObject;
            }
            if (value is JArray array) return new JArray(array.Select(item => ExpandPrivacyReferences(item, references, stack)));
            return value.DeepClone();
        }

        /// <summary>有界且拒绝重复字段/尾部 JSON 的严格对象读取；原样保留日期字符串以绑定原来源，不接受凭据路径或类型注入。</summary>
        private static JObject ParsePrivacyObject(string json, int maximumBytes)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > maximumBytes) throw new InvalidOperationException("维护 JSON 大小无效。");
            using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 48, DateParseHandling = DateParseHandling.None };
            JObject value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidOperationException("维护 JSON 尾部无效。"); return value;
        }
    }
}
